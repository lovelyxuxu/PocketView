using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace PocketView {
    sealed class BrowserTab {
        public readonly string Id=Guid.NewGuid().ToString("N");
        public readonly WebView2 View=new WebView2();
        public string Title="新标签页",FavoriteId;
        public bool Ready,Closed,Loading,Recovery,Fullscreen;
        public ulong NavigationId;
    }
    partial class BrowserWindow {
        readonly List<BrowserTab> tabs=new List<BrowserTab>();
        BrowserTab activeTab;
        readonly Panel tabSidebar=new Panel();
        readonly FlowLayoutPanel tabRows=new FlowLayoutPanel();
        bool sidebarVisible;
        static bool IsHomeView(WebView2 view) { return view.Source!=null&&view.Source.Scheme=="https"&&view.Source.Host=="pocketview.local"&&view.Source.AbsolutePath=="/index.html"; }
        void CreateInitialTab() { activeTab=CreateTab(); }
        BrowserTab CreateTab() {
            var tab=new BrowserTab();tabs.Add(tab);
            tab.View.Name="tab-"+tab.Id;tab.View.DefaultBackgroundColor=Color.White;tab.View.Visible=false;Controls.Add(tab.View);return tab;
        }
        async Task<BrowserTab> AddTab(string url,bool select,bool navigate) {
            var tab=CreateTab();if(select)SelectTab(tab);else LayoutTabViews();RenderTabRows();
            await InitializeTab(tab,url,navigate);return tab;
        }
        void SelectTab(BrowserTab tab) {
            if(tab==null||tab.Closed||!tabs.Contains(tab))return;
            if(activeTab!=tab&&initialized){try{SaveWindow();if(activeTab.Fullscreen)activeTab.View.ExecuteScriptAsync("if(document.fullscreenElement)document.exitFullscreen()");}catch(Exception ex){Data.Log(ex.Message);}}
            activeTab=tab;loadingTimer.Stop();if(tab.Loading)loadingTimer.Start();
            HideBar(true);LayoutTabViews();UpdateChrome();RenderTabRows();
            if(tab.Ready){tab.View.Focus();if(IsHomeView(tab.View))SendState();if(tab.Recovery)ShowBar(false);}
        }
        void CloseTab(BrowserTab tab) {
            if(tab==null||tab.Closed||!tabs.Contains(tab))return;
            if(tabs.Count==1){Close();return;}
            int index=tabs.IndexOf(tab);
            if(activeTab==tab){if(initialized){try{SaveWindow();}catch(Exception ex){Data.Log(ex.Message);}}activeTab=null;}
            tabs.Remove(tab);tab.Closed=true;
            if(tab.Ready)tab.View.Dispose(); // Pending initialization disposes after completion.
            else tab.View.Visible=false;
            if(activeTab==null)SelectTab(tabs[Math.Min(index,tabs.Count-1)]);else{LayoutTabViews();RenderTabRows();}
        }
        void CloseActiveTab() { CloseTab(activeTab); }
        void CycleTab(int step) { if(tabs.Count>1)SelectTab(tabs[(tabs.IndexOf(activeTab)+step+tabs.Count)%tabs.Count]); }
        void ToggleTabSidebar() { SetTabSidebar(!sidebarVisible); }
        void SetTabSidebar(bool visible) { sidebarVisible=visible;LayoutOverlays();RenderTabRows(); }
        void BuildTabSidebar() {
            tabSidebar.Name="tabSidebar";tabSidebar.BackColor=UiStyle.Surface;tabSidebar.Visible=false;
            var header=new Panel{Dock=DockStyle.Top,Height=68,Padding=new Padding(18,18,12,12)};
            var title=new Label{Text="标签页",Dock=DockStyle.Fill,ForeColor=UiStyle.Ink,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Microsoft YaHei UI",10,FontStyle.Bold)};
            var hide=UiStyle.Button("‹",false);hide.Width=30;hide.Dock=DockStyle.Right;hide.BackColor=tabSidebar.BackColor;hide.TabStop=false;hide.Click+=delegate{SetTabSidebar(false);};tips.SetToolTip(hide,"隐藏标签页 · Ctrl+Shift+B");header.Controls.Add(title);header.Controls.Add(hide);AttachDrag(header);AttachDrag(title);
            var create=new Button{Text="＋  新标签页",Dock=DockStyle.Bottom,Height=52,FlatStyle=FlatStyle.Flat,ForeColor=UiStyle.Blue,Font=new Font("Microsoft YaHei UI",9),TabStop=false,Cursor=Cursors.Hand};create.FlatAppearance.BorderSize=0;create.Click+=async delegate{await AddTab(null,true,true);};tips.SetToolTip(create,"新标签页 · Ctrl+T");
            tabRows.Dock=DockStyle.Fill;tabRows.FlowDirection=FlowDirection.TopDown;tabRows.WrapContents=false;tabRows.AutoScroll=true;tabRows.Padding=new Padding(10,2,10,4);tabRows.SizeChanged+=delegate{ResizeTabRows();};
            tabSidebar.Controls.Add(tabRows);tabSidebar.Controls.Add(create);tabSidebar.Controls.Add(header);Controls.Add(tabSidebar);
        }
        void LayoutTabViews() {
            int inset=sidebarVisible?Math.Min(224,Math.Max(180,ClientSize.Width/3)):0;
            tabSidebar.SetBounds(0,0,inset,ClientSize.Height);tabSidebar.Visible=sidebarVisible;
            foreach(var tab in tabs){if(tab.View.IsDisposed)continue;tab.View.SetBounds(inset,0,Math.Max(1,ClientSize.Width-inset),ClientSize.Height);tab.View.Visible=tab==activeTab&&!tab.Closed;}
            if(sidebarVisible)tabSidebar.BringToFront();
        }
        void ResizeTabRows() { foreach(Control row in tabRows.Controls)row.Width=Math.Max(100,tabRows.ClientSize.Width-24); }
        void RenderTabRows() {
            if(tabRows.IsDisposed)return;
            int scroll=-tabRows.AutoScrollPosition.Y;
            tabRows.SuspendLayout();
            foreach(Control row in tabRows.Controls.Cast<Control>().ToArray()){tabRows.Controls.Remove(row);row.Dispose();}
            foreach(var tab in tabs) {
                var captured=tab;var row=new Panel{Name="tab-row-"+tab.Id,Height=62,Width=Math.Max(100,tabRows.ClientSize.Width-24),Margin=new Padding(0,0,0,7),BackColor=tab==activeTab?Color.White:tabSidebar.BackColor};
                row.SizeChanged+=delegate(object s,EventArgs e){UiStyle.Round((Control)s,9);};UiStyle.Round(row,9);
                string text=String.IsNullOrWhiteSpace(tab.Title)?"新标签页":tab.Title;
                string subtitle=tab.View.Source==null?"新标签页":IsHomeView(tab.View)?"收藏首页":tab.View.Source.Host;
                var select=new TabSelectButton{Caption=text,Subtitle=subtitle,Selected=tab==activeTab,Loading=tab.Loading,Dock=DockStyle.Fill,AccessibleName=text};select.Click+=delegate{SelectTab(captured);};
                var close=new Button{Text="×",Width=26,Dock=DockStyle.Right,FlatStyle=FlatStyle.Flat,TabStop=false,Font=new Font("Segoe UI",11),ForeColor=UiStyle.Muted,AccessibleName="关闭标签页",Cursor=Cursors.Hand};close.FlatAppearance.BorderSize=0;close.FlatAppearance.MouseOverBackColor=Color.FromArgb(231,235,242);close.Click+=delegate{CloseTab(captured);};
                string url=tab.View.Source==null?"":tab.View.Source.AbsoluteUri;tips.SetToolTip(select,text+"\n"+url);row.Controls.Add(select);row.Controls.Add(close);tabRows.Controls.Add(row);
            }
            tabRows.ResumeLayout();
            tabRows.AutoScrollPosition=new Point(0,scroll);
            var toggle=bar.Controls.Find("nav-tabs",true).FirstOrDefault() as NavigationButton;if(toggle!=null){toggle.Active=sidebarVisible;toggle.Invalidate();}
        }
    }
}
