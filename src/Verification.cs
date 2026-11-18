using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
namespace PocketView {
    static class Verification {
        static readonly List<string> results = new List<string>();
        static void Check(bool value,string label) { if(!value) throw new Exception(label); results.Add("PASS " + label); }
        static void CaptureIsland(Control island,string path) {
            using(var bitmap=new Bitmap(island.Width,island.Height)) {
                island.DrawToBitmap(bitmap,island.ClientRectangle);
                using(var output=new Bitmap(island.Width+24,island.Height+24)) {
                    using(var g=Graphics.FromImage(output)) { g.Clear(Color.FromArgb(247,248,252));g.TranslateTransform(12,12);g.SetClip(island.Region, System.Drawing.Drawing2D.CombineMode.Replace);g.DrawImageUnscaled(bitmap,0,0); }
                    output.Save(path,System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
        static async Task Wait(WebView2 web,string expression) {
            for(int i=0;i<100;i++) { if(await web.ExecuteScriptAsync(expression)=="true") return; await Task.Delay(100); }
            throw new Exception("Timed out: " + expression);
        }
        public static async Task Run(Form form,WebView2 web,Store data,Action state,Action pin,Action home,Action save) {
            string output = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","work"));
            Directory.CreateDirectory(output);
            try {
                await Wait(web,"document.documentElement.dataset.ready === 'true'");
                Check(form.FormBorderStyle==FormBorderStyle.None,"frameless native window");
                Check(form.RectangleToScreen(form.ClientRectangle)==form.Bounds,"native client occupies entire outer window without frame");
                var resizeEdges=(ResizeEdgeInput)typeof(BrowserWindow).GetField("resizeEdges",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                Check(resizeEdges!=null&&resizeEdges.Installed,"native edge input installed over Chromium child windows");
                Rectangle rb=form.Bounds;
                var targets=new[]{new Point(rb.Left+1,rb.Top+rb.Height/2),new Point(rb.Right-2,rb.Top+rb.Height/2),new Point(rb.Left+rb.Width/2,rb.Top+1),new Point(rb.Left+rb.Width/2,rb.Bottom-2),new Point(rb.Left+1,rb.Top+1),new Point(rb.Right-2,rb.Top+1),new Point(rb.Left+1,rb.Bottom-2),new Point(rb.Right-2,rb.Bottom-2)};
                int[] expectedHits={10,11,12,15,13,14,16,17};
                for(int i=0;i<targets.Length;i++)Check(resizeEdges.HitTestAt(targets[i])==expectedHits[i],"resize direction "+expectedHits[i]+" recognized in screen coordinates");
                Check(resizeEdges.HitTestAt(new Point(rb.Left+rb.Width/2,rb.Top+rb.Height/2))==0 && resizeEdges.HitTestAt(new Point(rb.Left-1,rb.Top))==0,"page content and outside windows excluded from resize input");
                Check(web.CoreWebView2.Settings.IsNonClientRegionSupportEnabled,"WebView native drag-region support enabled");
                Check(await web.ExecuteScriptAsync("getComputedStyle(document.body).getPropertyValue('-webkit-app-region') === 'drag'")=="true","homepage background participates in native window dragging");
                Check(await web.ExecuteScriptAsync("getComputedStyle(document.getElementById('add')).getPropertyValue('-webkit-app-region') === 'no-drag'")=="true","homepage buttons excluded from window dragging");
                Check(web.CoreWebView2.Environment.BrowserVersionString.StartsWith("157.0.4341.60"),"bundled fixed runtime selected");
                Check(data.Root.EndsWith("verification-profile"),"verification uses isolated data directory");
                var island=form.Controls.Find("navigationIsland",true).Single();
                var show=typeof(BrowserWindow).GetMethod("ShowBar",BindingFlags.Instance|BindingFlags.NonPublic);
                var hide=typeof(BrowserWindow).GetMethod("HideBar",BindingFlags.Instance|BindingFlags.NonPublic);
                Check(!island.Visible,"homepage navigation hidden by default");
                Rectangle originalBounds=form.Bounds;
                foreach(int width in new[]{540,760,1200}) {
                    form.Width=width;show.Invoke(form,new object[]{false});await Task.Delay(350);
                    var controls=island.Controls.Cast<Control>().Where(c=>c is NavigationButton).ToList();
                    Check(island.Left>0 && island.Right<form.ClientSize.Width && Math.Abs(island.Left-(form.ClientSize.Width-island.Width)/2)<=1,"floating navigation centered at width "+width);
                    Check(controls.Count==12 && controls.All(c=>island.ClientRectangle.Contains(c.Bounds)) && controls.SelectMany((a,i)=>controls.Skip(i+1).Select(b=>!a.Bounds.IntersectsWith(b.Bounds))).All(x=>x),"navigation buttons fit without overlap at width "+width);
                }
                CaptureIsland(island,Path.Combine(output,"island-compact.png"));
                show.Invoke(form,new object[]{true});await Task.Delay(350);
                var input=island.Controls.Find("addressInput",true).Single();
                Check(input.Visible && input.Top>44 && island.ClientRectangle.Contains(input.Bounds),"address editor expands on separate row without clipping");
                CaptureIsland(island,Path.Combine(output,"island-address.png"));
                web.Focus();hide.Invoke(form,new object[]{false});await Task.Delay(450);
                Check(!island.Visible,"floating navigation completes hide animation");
                form.Bounds=originalBounds;
                var showMenu=typeof(BrowserWindow).GetMethod("ShowMenu",BindingFlags.Instance|BindingFlags.NonPublic);
                var menuField=typeof(BrowserWindow).GetField("navigationMenu",BindingFlags.Instance|BindingFlags.NonPublic);
                for(int attempt=0;attempt<3;attempt++) {
                    show.Invoke(form,new object[]{false});await Task.Delay(200);showMenu.Invoke(form,null);
                    var menu=(ContextMenuStrip)menuField.GetValue(form);
                    if(attempt==0) {
                        Check(menu is NavigationMenu&&menu.Region!=null,"more menu uses a rounded custom surface");
                        var zoom=menu.Items.OfType<ToolStripControlHost>().Single().Control;
                        double startingZoom=web.ZoomFactor;
                        ((Button)zoom.Controls.Find("zoom-in",true).Single()).PerformClick();
                        Check(Math.Abs(web.ZoomFactor-Math.Min(5,startingZoom+.1))<.001&&menu.Visible,"zoom increment works without dismissing menu");
                        ((Button)zoom.Controls.Find("zoom-reset",true).Single()).PerformClick();
                        Check(Math.Abs(web.ZoomFactor-1)<.001,"zoom percentage restores actual webpage scale");
                        ((Button)zoom.Controls.Find("zoom-out",true).Single()).PerformClick();
                        Check(Math.Abs(web.ZoomFactor-.9)<.001&&menu.Visible,"zoom decrement works without dismissing menu");
                        ((Button)zoom.Controls.Find("zoom-reset",true).Single()).PerformClick();
                        using(var bitmap=new Bitmap(menu.Width,menu.Height))using(var preview=new Bitmap(menu.Width,menu.Height)) {
                            menu.DrawToBitmap(bitmap,menu.ClientRectangle);
                            using(var graphics=Graphics.FromImage(preview)){graphics.Clear(Color.Transparent);graphics.SetClip(menu.Region,System.Drawing.Drawing2D.CombineMode.Replace);graphics.DrawImageUnscaled(bitmap,0,0);}
                            preview.Save(Path.Combine(output,"navigation-menu.png"),System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    var helpItem=menu.Items.Cast<ToolStripItem>().Single(item=>item.Text=="快捷键说明");
                    menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                    Check(!menu.IsDisposed,"menu survives synchronous ItemClicked close dispatch "+attempt);
                    helpItem.PerformClick();
                    HelpDialog help=null;
                    for(int i=0;i<40&&help==null;i++){await Task.Delay(50);help=Application.OpenForms.Cast<Form>().OfType<HelpDialog>().FirstOrDefault();}
                    Check(help!=null&&help.Visible,"help opens from a closing native menu "+attempt);
                    Check(menu.IsDisposed,"menu disposed after click dispatch "+attempt);
                    if(attempt==0)using(var bitmap=new Bitmap(help.Width,help.Height)){help.DrawToBitmap(bitmap,help.ClientRectangle);bitmap.Save(Path.Combine(output,"help-dialog.png"),System.Drawing.Imaging.ImageFormat.Png);}
                    help.Close();await Task.Delay(100);
                    Check(!Application.OpenForms.Cast<Form>().Any(f=>f is HelpDialog),"help closes cleanly "+attempt);
                }
                hide.Invoke(form,new object[]{true});
                await web.ExecuteScriptAsync("document.getElementById('help').click()");
                HelpDialog homeHelp=null;
                for(int i=0;i<40&&homeHelp==null;i++){await Task.Delay(50);homeHelp=Application.OpenForms.Cast<Form>().OfType<HelpDialog>().FirstOrDefault();}
                Check(homeHelp!=null,"homepage help entry opens the same shortcut panel through trusted bridge");homeHelp.Close();await Task.Delay(100);
                using(var favoriteDialog=new FavoriteDialog(null,"测试网页","https://example.com/",new[]{"常用"})) {
                    favoriteDialog.Show(form);await Task.Delay(100);
                    Check(favoriteDialog.FavoriteTitle=="测试网页"&&favoriteDialog.FavoriteUrl=="https://example.com/","native favorite editor preserves initial page title and URL");
                    using(var bitmap=new Bitmap(favoriteDialog.Width,favoriteDialog.Height)){favoriteDialog.DrawToBitmap(bitmap,favoriteDialog.ClientRectangle);bitmap.Save(Path.Combine(output,"favorite-dialog.png"),System.Drawing.Imaging.ImageFormat.Png);}
                    favoriteDialog.Close();
                }
                data.Favorites.Clear();data.History.Clear();data.SaveFavorites();data.SaveHistory();state();await Task.Delay(200);
                Check(await web.ExecuteScriptAsync("document.getElementById('empty').hidden === false")=="true","empty favorites homepage");
                using(var stream=File.Create(Path.Combine(output,"home-empty.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
                var screenshotBounds=form.Bounds;form.Size=new Size(1080,720);
                data.Upsert(null,"GitHub","https://github.com/","开发");
                data.Upsert(null,"MDN Web Docs","https://developer.mozilla.org/","开发");
                data.Upsert(null,"维基百科","https://zh.wikipedia.org/","阅读");
                data.Upsert(null,"YouTube","https://www.youtube.com/","视频");
                state();await Task.Delay(250);
                using(var stream=File.Create(Path.Combine(output,"readme-home.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
                data.Favorites.Clear();data.SaveFavorites();state();form.Bounds=screenshotBounds;await Task.Delay(100);
                await web.ExecuteScriptAsync("document.getElementById('add').click();document.getElementById('nameInput').value='窗口内视频测试';document.getElementById('urlInput').value='https://pocketview.local/verify.html';document.getElementById('groupInput').value='视频';document.getElementById('editorForm').requestSubmit();");
                await Wait(web,"document.querySelectorAll('.card').length === 1 && !document.getElementById('editor').open");
                Check(data.Favorites.Count==1,"favorite created through homepage form and native bridge");
                Check(await web.ExecuteScriptAsync("getComputedStyle(document.querySelector('.card')).getPropertyValue('-webkit-app-region') === 'no-drag' && document.querySelector('.card').draggable")=="true","card sorting is separate from window dragging");
                string firstId=data.Favorites[0].Id;
                data.Upsert(null,"开发文档","https://learn.microsoft.com/","阅读");
                data.Upsert(null,"Google","https://www.google.com/","常用");
                data.Upsert(null,"<img src=x onerror=alert(1)>","https://example.com/","阅读");state();await Task.Delay(200);
                Check(await web.ExecuteScriptAsync("document.querySelectorAll('.card h3 img').length === 0 && document.querySelectorAll('.card').length === 4")=="true","favorite text escaped against HTML injection");
                await web.ExecuteScriptAsync("document.getElementById('search').value='开发';document.getElementById('search').dispatchEvent(new Event('input')); ");
                Check(await web.ExecuteScriptAsync("document.querySelectorAll('.card').length === 1")=="true","favorite search filters cards");
                await web.ExecuteScriptAsync("document.getElementById('search').value='';document.getElementById('search').dispatchEvent(new Event('input'));document.querySelector('.card .card-actions button').click();document.getElementById('nameInput').value='窗口内视频';document.getElementById('editorForm').requestSubmit();");
                await Wait(web,"!document.getElementById('editor').open");Check(data.Favorites[0].Title=="窗口内视频","favorite edited through form");
                string lastId=data.Favorites.Last().Id;
                await web.ExecuteScriptAsync("window.chrome.webview.postMessage({action:'reorder',ids:"+data.Json.Serialize(data.Favorites.AsEnumerable().Reverse().Select(f=>f.Id).ToArray())+"});");
                await Task.Delay(250);Check(data.Favorites[0].Id==lastId,"reorder persists through bridge");
                using(var stream=File.Create(Path.Combine(output,"home-cards.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
                foreach(int width in new[]{540,760,1200}) {
                    form.Width=width;await Task.Delay(150);
                    Check(await web.ExecuteScriptAsync("document.documentElement.scrollWidth <= innerWidth && [...document.querySelectorAll('.card')].every(c=>c.getBoundingClientRect().right<=innerWidth)")=="true","homepage layout fits without horizontal overflow at width "+width);
                }
                form.Bounds=originalBounds;await Task.Delay(150);
                await web.ExecuteScriptAsync("document.querySelector('.card .card-actions button:last-child').click();document.getElementById('confirmOk').click();");
                await Wait(web,"document.querySelectorAll('.card').length === 3");Check(data.Favorites.Count==3,"delete confirmation removes only selected favorite");
                var reloaded=new Store(data.Root);Check(reloaded.Favorites.Count==3,"favorites survive reload from disk");
                pin();Check(form.TopMost,"pin enabled");pin();Check(!form.TopMost,"pin disabled");
                await web.ExecuteScriptAsync("document.querySelector('[data-view=history]').click();");
                Check(await web.ExecuteScriptAsync("!document.getElementById('historyView').hidden")=="true","history view switches correctly");
                await web.ExecuteScriptAsync("document.querySelector('[data-view=settings]').click();");
                Check(await web.ExecuteScriptAsync("!document.getElementById('settingsView').hidden")=="true","settings view switches correctly");
                using(var stream=File.Create(Path.Combine(output,"settings-view.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
                var legacyFavorite=data.Favorites.Find(f=>f.Id==firstId);
                legacyFavorite.Window=new WindowStateData{X=form.Left+30,Y=form.Top+30,Width=540,Height=360,Pinned=true,Zoom=.5};data.SaveFavorites();
                state();await Task.Delay(100);
                Rectangle favoriteBounds=form.Bounds;web.ZoomFactor=1.2;
                await web.ExecuteScriptAsync("document.querySelector('[data-view=favorites]').click();document.querySelector('[data-id=\""+firstId+"\"] .card-open').click();");
                await Wait(web,"location.pathname === '/verify.html' && document.readyState === 'complete'");
                Check(form.Bounds==favoriteBounds&&form.WindowState==FormWindowState.Normal,"opening legacy favorite preserves current window size and position");
                Check(!form.TopMost&&Math.Abs(web.ZoomFactor-1.2)<.001,"opening legacy favorite preserves current pin and zoom instead of saved values");
                web.ZoomFactor=1;
                await Task.Delay(2000);
                await web.ExecuteScriptAsync("window.dispatchEvent(new KeyboardEvent('keydown',{key:'Alt',altKey:true}))");
                Check(await web.ExecuteScriptAsync("document.documentElement.style.getPropertyValue('-webkit-app-region') === 'drag'")=="true","Alt enables page-wide native drag region");
                await web.ExecuteScriptAsync("window.dispatchEvent(new KeyboardEvent('keyup',{key:'Alt'}))");
                Check(await web.ExecuteScriptAsync("document.documentElement.style.getPropertyValue('-webkit-app-region') === ''")=="true","Alt release restores normal webpage interaction");
                await web.ExecuteScriptAsync("window.dispatchEvent(new KeyboardEvent('keydown',{key:'Alt',altKey:true}));window.dispatchEvent(new Event('blur'))");
                Check(await web.ExecuteScriptAsync("document.documentElement.style.getPropertyValue('-webkit-app-region') === ''")=="true","focus loss cancels page drag mode");
                Check(web.RectangleToScreen(web.ClientRectangle)==form.Bounds,"webpage reaches all four window edges without padding");
                Check(form.Controls.Cast<Control>().Where(c=>c!=web).All(c=>!c.Visible),"viewing mode has no visible toolbar, trigger strip, or progress border");
                using(var stream=File.Create(Path.Combine(output,"borderless-page.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
                int before=data.Favorites.Count;
                await web.ExecuteScriptAsync("window.chrome.webview.postMessage({action:'remove',id:"+data.Json.Serialize(firstId)+"});");
                await Task.Delay(200);Check(data.Favorites.Count==before,"non-home pages cannot mutate local favorites");
                Rectangle bounds=form.Bounds;
                form.Activate();web.Focus();await Task.Delay(100);
                await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate","{\"expression\":\"document.getElementById('player').requestFullscreen()\",\"userGesture\":true,\"awaitPromise\":true}");
                await Wait(web,"document.fullscreenElement !== null");
                Check(web.CoreWebView2.ContainsFullScreenElement,"web fullscreen detected");
                Check(form.Bounds==bounds && form.WindowState==FormWindowState.Normal,"video fullscreen preserves window bounds");
                Check(web.RectangleToScreen(web.ClientRectangle)==form.Bounds,"fullscreen video viewport reaches outer window edges");
                using(var stream=File.Create(Path.Combine(output,"video-window-fullscreen.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
                await web.ExecuteScriptAsync("document.exitFullscreen()");await Wait(web,"document.fullscreenElement === null");
                save();reloaded=new Store(data.Root);Check(reloaded.Prefs.LastWindow.Width==form.Width&&reloaded.Prefs.LastWindow.Height==form.Height,"shared window size persisted in application settings");
                Check(reloaded.Favorites.Find(f=>f.Id==firstId).Window.Width==540,"saving shared window leaves legacy favorite data intact");
                Check(data.History.Any(h=>h.Url.Contains("verify.html")),"successful page navigation recorded in browsing data");
                home();await Wait(web,"document.documentElement.dataset.ready === 'true'");
                form.WindowState=FormWindowState.Maximized;await Task.Delay(200);
                Rectangle maximizedBounds=form.Bounds;web.ZoomFactor=1.1;
                await web.ExecuteScriptAsync("document.querySelector('[data-id=\""+firstId+"\"] .card-open').click();");
                await Wait(web,"location.pathname === '/verify.html' && document.readyState === 'complete'");
                Check(form.WindowState==FormWindowState.Maximized&&form.Bounds==maximizedBounds,"opening legacy favorite keeps maximized window maximized");
                Check(Math.Abs(web.ZoomFactor-1.1)<.001&&!form.TopMost,"maximized favorite navigation preserves zoom and pin");
                home();await Wait(web,"document.documentElement.dataset.ready === 'true'");
                form.WindowState=FormWindowState.Normal;form.Bounds=favoriteBounds;
                var allTabs=(List<BrowserTab>)typeof(BrowserWindow).GetField("tabs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var firstTab=allTabs.Single(t=>t.View==web);
                await web.ExecuteScriptAsync("window.tabRetainedValue='first-page-state';document.getElementById('search').value='保留的输入'");
                await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate","{\"expression\":\"window.open('https://pocketview.local/blocked.html','_blank');1\",\"userGesture\":true}");
                for(int i=0;i<100&&allTabs.Count<2;i++)await Task.Delay(100);
                Check(allTabs.Count==2 && Application.OpenForms.Cast<Form>().Count(f=>f is BrowserWindow)==1,"target-blank creates a tab in the same native window");
                var secondTab=allTabs.Single(t=>t!=firstTab);
                await Wait(secondTab.View,"location.pathname === '/blocked.html' && document.readyState === 'complete'");
                Check(secondTab.View.Visible&&!web.Visible,"new tab selected while original page remains allocated");
                var selectTab=typeof(BrowserWindow).GetMethod("SelectTab",BindingFlags.Instance|BindingFlags.NonPublic);
                selectTab.Invoke(form,new object[]{firstTab});
                Check(await web.ExecuteScriptAsync("window.tabRetainedValue === 'first-page-state' && document.getElementById('search').value === '保留的输入'")=="true","switching tabs preserves page variables and form input");
                var toggleTabs=typeof(BrowserWindow).GetMethod("ToggleTabSidebar",BindingFlags.Instance|BindingFlags.NonPublic);
                toggleTabs.Invoke(form,null);var sidebar=form.Controls.Find("tabSidebar",true).Single();
                Check(sidebar.Visible&&web.Left==sidebar.Width&&web.Width+sidebar.Width==form.ClientSize.Width,"visible vertical tabs reserve space without covering page");
                int wideWidth=form.Width;form.Width=540;await Task.Delay(150);
                Check(await web.ExecuteScriptAsync("document.documentElement.scrollWidth <= innerWidth")=="true","homepage fits minimum window width with vertical tabs visible");
                form.Width=wideWidth;await Task.Delay(100);
                using(var bitmap=new Bitmap(sidebar.Width,sidebar.Height)){sidebar.DrawToBitmap(bitmap,sidebar.ClientRectangle);bitmap.Save(Path.Combine(output,"vertical-tabs.png"),System.Drawing.Imaging.ImageFormat.Png);}
                toggleTabs.Invoke(form,null);
                Check(!sidebar.Visible&&web.RectangleToScreen(web.ClientRectangle)==form.Bounds,"hiding vertical tabs restores edge-to-edge webpage");
                var closeTab=typeof(BrowserWindow).GetMethod("CloseTab",BindingFlags.Instance|BindingFlags.NonPublic);
                selectTab.Invoke(form,new object[]{secondTab});closeTab.Invoke(form,new object[]{secondTab});
                Check(allTabs.Count==1&&!form.IsDisposed&&web.Visible,"closing active tab returns to remaining page without closing window");
                await web.ExecuteScriptAsync("document.getElementById('search').value='';document.getElementById('search').dispatchEvent(new Event('input'))");
                web.CoreWebView2.Navigate("https://pocketview.local/blocked.html");
                await Wait(web,"location.pathname === '/blocked.html' && document.readyState === 'complete'");
                var handleKey=typeof(BrowserWindow).GetMethod("HandleKey",BindingFlags.Instance|BindingFlags.NonPublic);
                handleKey.Invoke(form,new object[]{Keys.F2});await Task.Delay(350);
                Check(island.Visible && island.Controls.Find("nav-close",true).Single().Visible,"native escape navigation opens over keyboard-capturing page");
                ((Button)island.Controls.Find("nav-back",true).Single()).PerformClick();
                await Wait(web,"location.pathname === '/index.html' && document.documentElement.dataset.ready === 'true'");
                Check(web.Source.AbsolutePath=="/index.html","native back button leaves validation fixture");
                var failed=new TaskCompletionSource<bool>();
                EventHandler<CoreWebView2NavigationCompletedEventArgs> onFailure=null;
                onFailure=delegate(object sender,CoreWebView2NavigationCompletedEventArgs args){if(!args.IsSuccess)failed.TrySetResult(true);};
                web.CoreWebView2.NavigationCompleted+=onFailure;
                web.CoreWebView2.Navigate("http://127.0.0.1:1/");
                await Task.WhenAny(failed.Task,Task.Delay(10000));
                web.CoreWebView2.NavigationCompleted-=onFailure;
                Check(failed.Task.IsCompleted,"connection failure reproduced without external network");
                await Task.Delay(1400);
                Check(island.Visible,"failed navigation keeps recovery buttons visible");
                ((Button)island.Controls.Find("nav-home",true).Single()).PerformClick();
                await Wait(web,"location.pathname === '/index.html' && document.documentElement.dataset.ready === 'true'");
                Check(web.Source.Host=="pocketview.local","home button recovers from built-in network error page");
                handleKey.Invoke(form,new object[]{Keys.F2});await Task.Delay(250);
                ((Button)island.Controls.Find("nav-close",true).Single()).PerformClick();
                Check(form.IsDisposed,"native close button exits independently of webpage");
                results.Add("SUCCESS " + results.Count + " integration checks");
            } catch(Exception ex) { results.Add("FAIL " + ex); data.Log(ex.ToString()); }
            File.WriteAllLines(Path.Combine(output,"verification.txt"),results);
            if(!form.IsDisposed)form.Close();
        }
    }
}
