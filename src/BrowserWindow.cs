using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PocketView {
    partial class BrowserWindow : Form {
        const string HomeUrl = "https://pocketview.local/index.html";
        const int HotkeyId = 43021;
        static Task<CoreWebView2Environment> environment;
        WebView2 web { get { return activeTab.View; } }
        readonly Panel bar = new NavigationIsland();
        readonly TextBox address = new TextBox();
        ResizeEdgeInput resizeEdges;
        readonly ToolTip tips = new ToolTip();
        readonly Timer hideTimer = new Timer(), wakeTimer = new Timer(), hoverTimer = new Timer(), islandAnimation = new Timer(), loadingTimer=new Timer();
        readonly bool primary; readonly string initial;
        Button pinButton, favoriteButton, backButton, forwardButton;
        bool initialized { get { return activeTab!=null&&activeTab.Ready; } }
        bool pageFullscreen { get { return activeTab!=null&&activeTab.Fullscreen; } }
        bool hotkeyRegistered, addressExpanded, menuOpen;
        int targetIslandHeight;
        bool recoveryNavigation { get { return activeTab!=null&&activeTab.Recovery; } set { if(activeTab!=null)activeTab.Recovery=value; } }
        bool escapeNavigation;
        readonly Dictionary<int,Keys> escapeHotkeys=new Dictionary<int,Keys>();
        string currentFavorite { get { return activeTab.FavoriteId; } set { activeTab.FavoriteId=value; } }
        Store Data { get { return Program.Data; } }
        bool AtHome { get { return web.Source != null && web.Source.AbsoluteUri.StartsWith(HomeUrl, StringComparison.OrdinalIgnoreCase); } }

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SendMessageW")] static extern IntPtr SendMessageText(IntPtr hwnd, int msg, IntPtr wparam, string text);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        public BrowserWindow(string url, bool isPrimary) {
            initial = url; primary = isPrimary;
            Text = "PocketView · 口袋视窗"; FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.White; Padding = Padding.Empty;
            StartPosition = FormStartPosition.CenterScreen; Size = new Size(1080, 720); MinimumSize = new Size(540, 360);
            try { Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "pocketview.ico")); } catch { }
            CreateInitialTab();BuildTabSidebar();
            BuildBar();
            Resize += delegate { LayoutOverlays(); };
            hoverTimer.Interval = 150;
            hoverTimer.Tick += delegate {
                if((resizeEdges!=null&&resizeEdges.IsSizing) || bar.Visible || WindowState==FormWindowState.Minimized || GetAncestor(WindowFromPoint(Cursor.Position),2)!=Handle) return;
                Point p = PointToClient(Cursor.Position);
                // Detect the top edge without painting a strip over the webpage.
                if(p.X>=0 && p.X<ClientSize.Width && p.Y>=0 && p.Y<24) ShowBar(false);
            }; hoverTimer.Start();
            hideTimer.Interval = 850;
            hideTimer.Tick += delegate {
                Point p=PointToClient(Cursor.Position);
                bool overIsland=(p.X>=0&&p.X<ClientSize.Width&&p.Y>=0&&p.Y<24)||(p.X>=bar.Left && p.X<bar.Right && p.Y>=0 && p.Y<bar.Bottom+6);
                if(escapeNavigation || recoveryNavigation || menuOpen || (ContainsFocus && address.Focused) || overIsland) return;
                HideBar(false);
            };
            islandAnimation.Interval=15;
            islandAnimation.Tick+=delegate {
                int delta=targetIslandHeight-bar.Height;
                if(Math.Abs(delta)<=2){bar.Height=targetIslandHeight;islandAnimation.Stop();if(targetIslandHeight==0)bar.Visible=false;}
                else bar.Height+=Math.Sign(delta)*Math.Max(2,(int)Math.Ceiling(Math.Abs(delta)*.28));
            };
            loadingTimer.Interval=5000;loadingTimer.Tick+=delegate{loadingTimer.Stop();recoveryNavigation=true;ShowBar(false);};
            wakeTimer.Interval = 300; wakeTimer.Tick += delegate { if(primary && Program.Wake.WaitOne(0)) Reveal(); }; wakeTimer.Start();
            Shown += async delegate { ApplyWindow(Data.Prefs.LastWindow);resizeEdges=new ResizeEdgeInput(this);await InitializeTab(activeTab,initial,true);if(IsDisposed)return;RegisterShortcut();if(Program.Verify)await RunVerification(); };
            FormClosing += delegate { try{if(initialized)SaveWindow();}catch(Exception ex){Data.Log("Close save failed: "+ex.Message);} ClearEscapeHotkeys();if(hotkeyRegistered) UnregisterHotKey(Handle, HotkeyId); };
            FormClosed += delegate { if(resizeEdges!=null)resizeEdges.Dispose();hideTimer.Dispose(); wakeTimer.Dispose(); hoverTimer.Dispose(); islandAnimation.Dispose(); loadingTimer.Dispose(); tips.Dispose();foreach(var tab in tabs.ToArray()){tab.Closed=true;tab.View.Dispose();} };
            LayoutOverlays();
        }
        Button MakeButton(string code, string tip, Action action, int width) {
            var b=new NavigationButton(code) { Width=width,Height=34,AccessibleName=tip,BackColor=bar.BackColor };
            b.Click+=delegate{action();};tips.SetToolTip(b,tip);return b;
        }
        void BuildBar() {
            bar.Visible=false;
            bar.Controls.Add(MakeButton("home","收藏首页 · Alt+Home",Home,34));
            backButton=MakeButton("back","返回 · Alt+Left",BackOrHome,34);bar.Controls.Add(backButton);
            forwardButton=MakeButton("forward","前进",()=>{if(initialized&&web.CanGoForward)web.GoForward();},34);bar.Controls.Add(forwardButton);
            bar.Controls.Add(MakeButton("reload","刷新 · F5",()=>{if(initialized)web.Reload();},34));
            bar.Controls.Add(MakeButton("address","地址栏 · Ctrl+L",()=>ShowBar(true),34));
            favoriteButton=MakeButton("favorite","收藏 · Ctrl+D",AddCurrent,34);bar.Controls.Add(favoriteButton);
            pinButton=MakeButton("pin","置顶 · Ctrl+Shift+T",TogglePin,34);bar.Controls.Add(pinButton);
            bar.Controls.Add(MakeButton("more","更多",ShowMenu,34));
            bar.Controls.Add(MakeButton("tabs","显示 / 隐藏标签页 · Ctrl+Shift+B",ToggleTabSidebar,34));
            bar.Controls.Add(MakeButton("minimize","最小化",()=>{WindowState=FormWindowState.Minimized;HideBar(true);},34));
            bar.Controls.Add(MakeButton("maximize","最大化 / 还原 · F11",ToggleMax,34));
            bar.Controls.Add(MakeButton("close","关闭 · Alt+F4",Close,34));
            address.Name="addressInput";address.BorderStyle=BorderStyle.None;address.BackColor=Color.FromArgb(244,246,249);address.ForeColor=UiStyle.Ink;address.Font=new Font("Microsoft YaHei UI",10);address.Visible=false;
            address.HandleCreated+=delegate{SendMessageText(address.Handle,0x1501,(IntPtr)1,"输入网址或搜索");};
            address.KeyDown+=delegate(object sender,KeyEventArgs e){
                if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;NavigateText(address.Text);web.Focus();}
                if(e.KeyCode==Keys.Escape){e.SuppressKeyPress=true;web.Focus();HideBar(false);}
            };
            address.Leave+=delegate{hideTimer.Start();};bar.Controls.Add(address);AttachDrag(bar);Controls.Add(bar);bar.BringToFront();
        }
        void AttachDrag(Control surface) {
            surface.MouseDown += delegate(object sender, MouseEventArgs e) {
                if(e.Button != MouseButtons.Left) return;
                if(e.Clicks == 2) { ToggleMax(); return; }
                hideTimer.Stop(); ReleaseCapture();
                // Let Windows own capture, restored bounds, snapping and the move loop.
                SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
                hideTimer.Start();
            };
        }
        protected override void OnResizeEnd(EventArgs e) {
            base.OnResizeEnd(e);
            if(resizeEdges!=null)resizeEdges.IsSizing=false;
            if(initialized) {
                SaveWindow();
                web.CoreWebView2.ExecuteScriptAsync("window.dispatchEvent(new KeyboardEvent('keyup',{key:'Alt'}))");
            }
        }
        protected override void OnResizeBegin(EventArgs e) { if(resizeEdges!=null)resizeEdges.IsSizing=true;base.OnResizeBegin(e); }
        void LayoutOverlays() {
            LayoutTabViews();
            int width=Math.Min(480,Math.Max(100,ClientSize.Width-24));
            bar.SetBounds((ClientSize.Width-width)/2,12,width,bar.Visible?Math.Max(1,bar.Height):0);
            var buttons=bar.Controls.Cast<Control>().Where(c=>c is NavigationButton).ToList();
            int spacing=36, start=(width-buttons.Count*spacing-18+2)/2;
            for(int i=0;i<buttons.Count;i++)buttons[i].SetBounds(start+i*spacing+(i>=4?6:0)+(i>=7?6:0)+(i>=9?6:0),10,34,34);
            address.SetBounds(22,61,Math.Max(80,width-44),24);
            address.Visible=addressExpanded && bar.Visible;
            bar.BringToFront();
        }
        static Task<CoreWebView2Environment> GetEnvironment(Store data) {
            if(environment == null) {
                string runtime = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime");
                string exe = Directory.Exists(runtime) ? Directory.GetFiles(runtime, "msedgewebview2.exe", SearchOption.AllDirectories).FirstOrDefault() : null;
                var options = new CoreWebView2EnvironmentOptions(); options.Language = "zh-CN";
                environment = CoreWebView2Environment.CreateAsync(exe == null ? null : Path.GetDirectoryName(exe), data.Browsing, options);
            }
            return environment;
        }
        async Task InitializeTab(BrowserTab tab,string url,bool navigate) {
            var web=tab.View;
            try {
                await web.EnsureCoreWebView2Async(await GetEnvironment(Data)); tab.Ready = true;
                if(tab.Closed||IsDisposed){web.Dispose();return;}
                var core = web.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping("pocketview.local", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets"), CoreWebView2HostResourceAccessKind.DenyCors);
                core.Settings.IsStatusBarEnabled = false; core.Settings.IsBuiltInErrorPageEnabled = true;
                core.Settings.AreDefaultScriptDialogsEnabled = true;
                core.Settings.IsNonClientRegionSupportEnabled = true;
                await core.AddScriptToExecuteOnDocumentCreatedAsync(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","window-drag.js")));
                core.NavigationStarting += delegate(object s, CoreWebView2NavigationStartingEventArgs e) {
                    Uri uri; if(!Uri.TryCreate(e.Uri, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https" && e.Uri != "about:blank")) { e.Cancel = true; return; }
                    tab.NavigationId=e.NavigationId;tab.Loading=true;tab.Recovery=false;RenderTabRows();if(activeTab==tab){loadingTimer.Stop();loadingTimer.Start();if(!escapeNavigation)HideBar(true);}
                };
                core.NavigationCompleted += delegate(object s, CoreWebView2NavigationCompletedEventArgs e) {
                    if(tab.Closed||e.NavigationId!=tab.NavigationId)return;
                    tab.Loading=false;tab.Recovery=!e.IsSuccess;if(activeTab==tab)loadingTimer.Stop();
                    if(activeTab==tab)UpdateChrome();RenderTabRows();
                    if(e.IsSuccess && web.Source != null && !IsHomeView(web)) Data.Visit(core.DocumentTitle, web.Source.AbsoluteUri);
                    if(!e.IsSuccess){Data.Log("Navigation failed " + e.WebErrorStatus);if(activeTab==tab)ShowBar(false);}
                };
                core.SourceChanged += delegate { if(activeTab==tab)UpdateChrome();RenderTabRows(); };
                core.HistoryChanged += delegate { if(activeTab==tab)UpdateChrome(); };
                core.DocumentTitleChanged += delegate { tab.Title=IsHomeView(web)?"收藏":core.DocumentTitle;if(activeTab==tab)UpdateChrome();RenderTabRows(); };
                core.NewWindowRequested += async delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e) {
                    var deferral=e.GetDeferral();e.Handled=true;
                    try { var child=await AddTab(null,true,false);if(!child.Closed)e.NewWindow=child.View.CoreWebView2; }
                    catch(Exception ex){Data.Log("New tab failed: "+ex);}
                    finally{deferral.Complete();}
                };
                core.WindowCloseRequested+=delegate{BeginInvoke((Action)(()=>CloseTab(tab)));};
                core.ContainsFullScreenElementChanged += delegate {
                    tab.Fullscreen=core.ContainsFullScreenElement;if(activeTab==tab){if(tab.Fullscreen)SetTabSidebar(false);HideBar(true);LayoutOverlays();}
                    Data.Log("WindowFullscreen=" + pageFullscreen + " bounds=" + Bounds.ToString());
                };
                core.WebMessageReceived += OnMessage;
                core.DownloadStarting += delegate(object s, CoreWebView2DownloadStartingEventArgs e) {
                    string folder = Path.Combine(Data.Browsing, "下载"); Directory.CreateDirectory(folder);
                    string name = Path.GetFileName(e.ResultFilePath); if(String.IsNullOrEmpty(name)) name = "download";
                    string dest = Path.Combine(folder, name); string stem = Path.GetFileNameWithoutExtension(name), ext = Path.GetExtension(name); int n = 1;
                    while(File.Exists(dest)) dest = Path.Combine(folder, stem + " (" + (n++) + ")" + ext);
                    e.ResultFilePath = dest;
                };
                core.ProcessFailed += delegate(object s, CoreWebView2ProcessFailedEventArgs e) { Data.Log("WebView failure " + e.ProcessFailedKind); };
                web.KeyDown += delegate(object s, KeyEventArgs e) {
                    Keys combo = e.KeyCode | e.Modifiers;
                    if(activeTab==tab && IsOurKey(combo)) { e.Handled = true; BeginInvoke((Action)(() => HandleKey(combo))); }
                };
                Data.Log("Tab ready runtime="+core.Environment.BrowserVersionString);
                if(tab.Closed){web.Dispose();return;}
                if(navigate)core.Navigate(String.IsNullOrWhiteSpace(url)?HomeUrl:url);
                if(activeTab==tab)UpdateChrome();RenderTabRows();
            } catch(Exception ex) {
                if(tab.Closed||IsDisposed)return;
                Data.Log(ex.ToString());
                MessageBox.Show("网页内核启动失败。请保留 runtime 文件夹，并把程序放到可写目录。\n\n" + ex.Message, "PocketView");
                if(Program.Verify) Close();
            }
        }
        void RegisterShortcut() {
            if(!primary) return;
            if(hotkeyRegistered) { UnregisterHotKey(Handle, HotkeyId); hotkeyRegistered = false; }
            if(Data.Prefs.GlobalHotkey) hotkeyRegistered = RegisterHotKey(Handle, HotkeyId, 0x0002 | 0x0001 | 0x4000, (uint)Keys.Space);
        }
        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);RegisterEscapeHotkeys();
        }
        protected override void OnDeactivate(EventArgs e) {
            ClearEscapeHotkeys();base.OnDeactivate(e);
        }
        void RegisterEscapeHotkeys() {
            ClearEscapeHotkeys();int id=43100;
            foreach(Keys key in new[]{Keys.F2,Keys.Control|Keys.T,Keys.Control|Keys.Shift|Keys.B,Keys.Control|Keys.Tab,Keys.Control|Keys.Shift|Keys.Tab,Keys.Control|Keys.W,Keys.Control|Keys.L,Keys.Alt|Keys.Home,Keys.Alt|Keys.Left}) {
                uint mods=0x4000;if((key&Keys.Control)!=0)mods|=2;if((key&Keys.Alt)!=0)mods|=1;if((key&Keys.Shift)!=0)mods|=4;
                if(RegisterHotKey(Handle,id,mods,(uint)(key&Keys.KeyCode)))escapeHotkeys[id]=key;
                id++;
            }
        }
        void ClearEscapeHotkeys() { foreach(int id in escapeHotkeys.Keys)UnregisterHotKey(Handle,id);escapeHotkeys.Clear(); }
        void Reveal() { if(WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Show(); Activate(); SetForegroundWindow(Handle);ShowBar(false); }
        void UpdateChrome() {
            if(!initialized) return;
            if(!address.Focused) address.Text = AtHome ? "PocketView  /  收藏" : (web.Source == null ? "" : web.Source.ToString());
            backButton.Enabled = !AtHome; forwardButton.Enabled = web.CanGoForward;
            ((NavigationButton)favoriteButton).Active=web.Source!=null&&Data.Favorites.Any(f=>f.Url==web.Source.AbsoluteUri);
            ((NavigationButton)pinButton).Active=TopMost;
            favoriteButton.Invalidate();pinButton.Invalidate();
            Text=AtHome?"PocketView · 收藏首页":(String.IsNullOrWhiteSpace(activeTab.Title)?"PocketView":activeTab.Title+" · PocketView");
            LayoutOverlays();
        }
        void ShowBar(bool focus) {
            if(!bar.Visible)addressExpanded=false;
            if(focus)addressExpanded=true;
            if(!bar.Visible){bar.Height=2;bar.Visible=true;}
            targetIslandHeight=addressExpanded?102:54;
            LayoutOverlays();islandAnimation.Start();hideTimer.Stop();hideTimer.Start();
            if(focus){address.Focus();if(AtHome)address.Text="";address.SelectAll();}
        }
        void HideBar(bool immediate) {
            hideTimer.Stop();targetIslandHeight=0;
            if(immediate){islandAnimation.Stop();bar.Visible=false;bar.Height=0;addressExpanded=false;address.Visible=false;}
            else islandAnimation.Start();
        }
        void TogglePin() { TopMost = !TopMost; UpdateChrome(); SaveWindow(); }
        void ToggleMax() { MaximizedBounds = Screen.FromControl(this).WorkingArea; WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
        void MoveOrSize(bool move) {
            if(WindowState != FormWindowState.Normal) return;
            ReleaseCapture();
            PostMessage(Handle, 0x0112, (IntPtr)(move ? 0xF010 : 0xF000), IntPtr.Zero);
        }
        void NavigateText(string value) { if(!initialized) return; try { SaveWindow(); currentFavorite = null;escapeNavigation=false; web.CoreWebView2.Navigate(Store.Normalize(value, true)); } catch(Exception e) { MessageBox.Show(e.Message, "PocketView"); } }
        void StopLoading() { try{if(initialized)web.CoreWebView2.Stop();}catch(Exception ex){Data.Log("Stop failed: "+ex.Message);} }
        void BackOrHome() {
            if(!initialized){ShowBar(false);return;} escapeNavigation=false;StopLoading();
            try{if(web.CanGoBack)web.GoBack();else Home();}catch(Exception ex){Data.Log("Back failed: "+ex.Message);Home();}
        }
        void Home() { if(!initialized){ShowBar(false);return;} escapeNavigation=false;StopLoading();try{SaveWindow();}catch(Exception ex){Data.Log("Home save failed: "+ex.Message);} currentFavorite = null; web.ZoomFactor = 1; web.CoreWebView2.Navigate(HomeUrl); HideBar(true); }
        void OpenFavorite(string id) {
            var f = Data.Favorites.Find(x => x.Id == id); if(f == null) return;
            SaveWindow(); currentFavorite = f.Id;
            // Favorites navigate within the shared window; legacy per-favorite bounds must not resize it.
            web.CoreWebView2.Navigate(f.Url); web.Focus(); hideTimer.Start();
        }
        void ApplyWindow(WindowStateData state) {
            if(state == null) return;
            Rectangle area = Screen.FromControl(this).WorkingArea;
            var desired = new Rectangle(state.X, state.Y, Math.Max(540,state.Width), Math.Max(360,state.Height));
            var screen = Screen.AllScreens.FirstOrDefault(s => Rectangle.Intersect(s.WorkingArea, desired).Width >= 160 && Rectangle.Intersect(s.WorkingArea, desired).Height >= 100);
            if(screen != null) area = screen.WorkingArea;
            int w = Math.Min(desired.Width, area.Width), h = Math.Min(desired.Height, area.Height);
            int x = screen == null ? area.X + (area.Width-w)/2 : Math.Max(area.X, Math.Min(state.X, area.Right-w));
            int y = screen == null ? area.Y + (area.Height-h)/2 : Math.Max(area.Y, Math.Min(state.Y, area.Bottom-h));
            WindowState = FormWindowState.Normal; Bounds = new Rectangle(x,y,w,h); TopMost = state.Pinned;
            if(initialized) web.ZoomFactor = Math.Max(0.25,Math.Min(5,state.Zoom));
        }
        void SaveWindow() {
            if(!initialized || WindowState == FormWindowState.Minimized) return;
            Rectangle r = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var state = new WindowStateData { X=r.X, Y=r.Y, Width=r.Width, Height=r.Height, Pinned=TopMost, Zoom=web.ZoomFactor };
            if(primary) { Data.Prefs.LastWindow = state; Data.SavePrefs(); }
        }
        void AddCurrent() {
            if(!initialized || AtHome || web.Source == null) return;
            var existing = Data.Favorites.Find(f => f.Url == web.Source.AbsoluteUri);
            using(var dialog = new FavoriteDialog(existing, web.CoreWebView2.DocumentTitle, web.Source.AbsoluteUri, Data.Favorites.Select(f => f.Group))) {
                if(dialog.ShowDialog(this) != DialogResult.OK) return;
                try { var f = Data.Upsert(existing == null ? null : existing.Id, dialog.FavoriteTitle, dialog.FavoriteUrl, dialog.FavoriteGroup); currentFavorite = f.Id; SaveWindow(); UpdateChrome(); }
                catch(Exception ex) { MessageBox.Show(ex.Message,"收藏失败"); }
            }
        }
        ContextMenuStrip navigationMenu;
        void AddMenuAction(ContextMenuStrip menu,string text,string shortcut,Action action) {
            var item=new ToolStripMenuItem(text){ShortcutKeyDisplayString=shortcut,Padding=new Padding(4,5,4,5)};
            item.Click+=delegate { if(!IsDisposed)BeginInvoke((Action)(()=>{if(!IsDisposed)action();})); };
            menu.Items.Add(item);
        }
        void ShowMenu() {
            if(navigationMenu!=null&&!navigationMenu.IsDisposed)navigationMenu.Close();
            menuOpen=true;
            var menu=new ContextMenuStrip{ShowImageMargin=false,Font=new Font("Microsoft YaHei UI",9),BackColor=Color.White,ForeColor=UiStyle.Ink,Padding=new Padding(6),Name="navigationMenu"};
            navigationMenu=menu;
            AddMenuAction(menu,"收藏首页","Alt+Home",Home);
            AddMenuAction(menu,"收藏当前页","Ctrl+D",AddCurrent);
            AddMenuAction(menu,"切换置顶","Ctrl+Shift+T",TogglePin);
            menu.Items.Add(new ToolStripSeparator());
            AddMenuAction(menu,"缩小网页","",()=>{web.ZoomFactor=Math.Max(.25,web.ZoomFactor-.1);});
            AddMenuAction(menu,"放大网页","",()=>{web.ZoomFactor=Math.Min(5,web.ZoomFactor+.1);});
            AddMenuAction(menu,"恢复网页缩放","",()=>{web.ZoomFactor=1;});
            menu.Items.Add(new ToolStripSeparator());
            AddMenuAction(menu,"移动窗口","Alt+F7",()=>MoveOrSize(true));
            AddMenuAction(menu,"调整窗口大小","Alt+F8",()=>MoveOrSize(false));
            AddMenuAction(menu,"打开程序文件夹","",()=>Process.Start("explorer.exe","\""+Data.Root+"\""));
            AddMenuAction(menu,"快捷键说明","F1",ShowHelp);
            menu.Closed+=delegate {
                menuOpen=false;if(!IsDisposed)hideTimer.Start();
                // ToolStrip still accesses its items while dispatching ItemClicked. Dispose on the next UI turn.
                if(!IsDisposed)BeginInvoke((Action)(()=>{menu.Dispose();if(navigationMenu==menu)navigationMenu=null;}));
            };
            menu.Show(bar,new Point(Math.Max(0,bar.Width-244),bar.Height+6));
        }
        void ShowHelp() { using(var dialog=new HelpDialog())dialog.ShowDialog(this); }
        static bool IsOurKey(Keys key) { return key==(Keys.Control|Keys.T) || key==(Keys.Control|Keys.Shift|Keys.B) || key==(Keys.Control|Keys.Tab) || key==(Keys.Control|Keys.Shift|Keys.Tab) || key==Keys.F2 || key==(Keys.Control|Keys.W) || key == (Keys.Alt|Keys.F7) || key == (Keys.Alt|Keys.F8) || key == (Keys.Control|Keys.L) || key == (Keys.Control|Keys.D) || key == (Keys.Control|Keys.Shift|Keys.T) || key == (Keys.Alt|Keys.Home) || key == (Keys.Alt|Keys.Left) || key == (Keys.Alt|Keys.Right) || key == Keys.F5 || key == Keys.F11 || key == Keys.F1; }
        void HandleKey(Keys key) {
            if(key==Keys.F2){escapeNavigation=!escapeNavigation;if(escapeNavigation)ShowBar(false);else HideBar(false);}
            else if(key==(Keys.Control|Keys.W))CloseActiveTab();
            else if(key==(Keys.Control|Keys.T)){var pending=AddTab(null,true,true); }
            else if(key==(Keys.Control|Keys.Shift|Keys.B))ToggleTabSidebar();
            else if(key==(Keys.Control|Keys.Tab))CycleTab(1);
            else if(key==(Keys.Control|Keys.Shift|Keys.Tab))CycleTab(-1);
            else if(key==(Keys.Control|Keys.L)) ShowBar(true);
            else if(key==(Keys.Control|Keys.D)) AddCurrent();
            else if(key==(Keys.Control|Keys.Shift|Keys.T)) TogglePin();
            else if(key==(Keys.Alt|Keys.F7)) MoveOrSize(true);
            else if(key==(Keys.Alt|Keys.F8)) MoveOrSize(false);
            else if(key==(Keys.Alt|Keys.Home)) Home();
            else if(key==(Keys.Alt|Keys.Left)) BackOrHome();
            else if(key==(Keys.Alt|Keys.Right)) { if(web.CanGoForward) web.GoForward(); }
            else if(key==Keys.F5) web.Reload(); else if(key==Keys.F11) ToggleMax(); else if(key==Keys.F1) ShowHelp();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { if(initialized && IsOurKey(keyData)) { BeginInvoke((Action)(() => HandleKey(keyData))); return true; } return base.ProcessCmdKey(ref msg,keyData); }
        protected override CreateParams CreateParams {
            get { var cp=base.CreateParams; cp.Style |= 0x00040000; return cp; }
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            // Windows 11's optional compositor outline is separate from our own padding.
            try { int none = unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(Handle, 34, ref none, sizeof(int)); } catch(DllNotFoundException) { }
        }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x0312 && escapeHotkeys.ContainsKey(m.WParam.ToInt32())) { Keys key=escapeHotkeys[m.WParam.ToInt32()];BeginInvoke((Action)(()=>HandleKey(key)));return; }
            if(m.Msg == 0x0312 && m.WParam.ToInt32() == HotkeyId) { Reveal(); return; }
            // Retain native resizing while giving every frame pixel to the client.
            if(m.Msg == 0x83) { m.Result=IntPtr.Zero; return; }
            base.WndProc(ref m);
            if(m.Msg == 0x84 && WindowState == FormWindowState.Normal) {
                Point p = PointToClient(new Point(unchecked((short)(long)m.LParam),unchecked((short)((long)m.LParam>>16))));
                int hit=ResizeEdgeInput.HitTest(ClientRectangle,p,6,12);
                if(hit!=0) m.Result=(IntPtr)hit;
            }
        }

        bool TrustedHome(string source) { Uri uri; return Uri.TryCreate(source, UriKind.Absolute, out uri) && uri.Scheme == "https" && uri.Host == "pocketview.local" && uri.AbsolutePath == "/index.html" && AtHome; }
        string Get(Dictionary<string,object> message, string key) { object value; return message.TryGetValue(key,out value) && value != null ? value.ToString() : ""; }
        void SendState() { if(AtHome) web.CoreWebView2.PostWebMessageAsJson(Data.Json.Serialize(new { type="state", favorites=Data.Favorites, history=Data.History, settings=Data.Prefs, hotkey=hotkeyRegistered, root=Data.Root, warnings=Data.Warnings })); }
        void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
            if(!initialized || !ReferenceEquals(sender,web.CoreWebView2) || !TrustedHome(e.Source)) return;
            try {
                var m = Data.Json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson); string action = Get(m,"action");
                switch(action) {
                    case "ready": SendState(); break;
                    case "save": Data.Upsert(Get(m,"id"),Get(m,"title"),Get(m,"url"),Get(m,"group")); SendState(); break;
                    case "remove": Data.Remove(Get(m,"id")); SendState(); break;
                    case "open": OpenFavorite(Get(m,"id")); break;
                    case "navigate": NavigateText(Get(m,"url")); break;
                    case "reorder": var ids = ((IEnumerable)m["ids"]).Cast<object>().Select(x => x.ToString()); Data.Reorder(ids); SendState(); break;
                    case "clearHistory": Data.History.Clear(); Data.SaveHistory(); SendState(); break;
                    case "hotkey": Data.Prefs.GlobalHotkey = Get(m,"value") == "True"; Data.SavePrefs(); RegisterShortcut(); SendState(); break;
                    case "folder": Process.Start("explorer.exe","\""+Data.Root+"\""); break;
                    case "help": BeginInvoke((Action)ShowHelp); break;
                    case "export": BeginInvoke((Action)ExportFavorites); break;
                    case "import": BeginInvoke((Action)ImportFavorites); break;
                }
            } catch(Exception ex) { Data.Log(ex.ToString()); if(AtHome) web.CoreWebView2.PostWebMessageAsJson(Data.Json.Serialize(new { type="error", message=ex.Message })); }
        }
        void ExportFavorites() {
            using(var dialog = new SaveFileDialog { Filter="JSON 收藏文件|*.json", FileName="PocketView收藏.json" }) {
                if(dialog.ShowDialog(this)==DialogResult.OK) { try { Data.Write(dialog.FileName,Data.Favorites); } catch(Exception ex) { MessageBox.Show(ex.Message,"导出失败"); } }
            }
        }
        void ImportFavorites() {
            using(var dialog = new OpenFileDialog { Filter="JSON 收藏文件|*.json" }) {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                try {
                    var entries = Data.Json.Deserialize<List<Favorite>>(File.ReadAllText(dialog.FileName));
                    if(entries == null || entries.Count>5000) throw new ArgumentException("收藏文件为空或条目过多。");
                    foreach(var f in entries) {
                        if(f==null) throw new ArgumentException("收藏格式错误。"); Store.Normalize(f.Url,false);
                        if((f.Title??"").Trim().Length>160 || (f.Group??"").Trim().Length>60) throw new ArgumentException("收藏名称或分组太长，未导入任何条目。");
                    }
                    foreach(var f in entries) { if(!Data.Favorites.Any(x=>x.Url==Store.Normalize(f.Url,false))) Data.Upsert(null,f.Title,f.Url,f.Group); }
                    SendState();
                } catch(Exception ex) { MessageBox.Show(ex.Message,"导入失败"); }
            }
        }

        async Task RunVerification() {
            await Verification.Run(this, web, Data, () => SendState(), () => TogglePin(), () => Home(), () => SaveWindow());
        }
    }
}
