using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PocketView {
    sealed class NavigationMenu : ContextMenuStrip {
        public static readonly Color Surface=Color.FromArgb(35,40,49),TextColor=Color.FromArgb(232,236,243),Muted=Color.FromArgb(150,161,179);
        public NavigationMenu() {
            Name="navigationMenu";AutoSize=false;Width=304;Padding=new Padding(8);ShowImageMargin=false;ShowCheckMargin=false;DropShadowEnabled=false;
            BackColor=Surface;ForeColor=TextColor;Font=new Font("Microsoft YaHei UI",9.5f);Renderer=new NavigationMenuRenderer();
        }
        public void FitContent() { int height=Padding.Vertical;foreach(ToolStripItem item in Items)height+=item.Height+item.Margin.Vertical;Size=new Size(304,height+2);UiStyle.Round(this,14); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e);UiStyle.Round(this,14); }
    }
    sealed class NavigationMenuRenderer : ToolStripRenderer {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(NavigationMenu.Surface); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var pen=new Pen(Color.FromArgb(59,67,80)))using(var path=UiStyle.Rounded(new Rectangle(0,0,e.ToolStrip.Width,e.ToolStrip.Height),14))e.Graphics.DrawPath(pen,path);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
            using(var background=new SolidBrush(NavigationMenu.Surface))e.Graphics.FillRectangle(background,new Rectangle(Point.Empty,e.Item.Size));
            if(!e.Item.Selected)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var brush=new SolidBrush(Color.FromArgb(51,59,73)))using(var path=UiStyle.Rounded(new Rectangle(0,1,e.Item.Width,e.Item.Height-2),8))e.Graphics.FillPath(brush,path);
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { using(var pen=new Pen(Color.FromArgb(57,65,78)))e.Graphics.DrawLine(pen,12,e.Item.Height/2,e.Item.Width-12,e.Item.Height/2); }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
            var item=e.Item as ToolStripMenuItem;if(item==null){base.OnRenderItemText(e);return;}
            // Render the title once; draw shortcut text in its own fixed right column.
            if(e.Text!=item.Text)return;
            DrawIcon(e.Graphics,item.Tag as string,new Point(12,(item.Height-18)/2),NavigationMenu.Muted);
            var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics,item.Text,item.Font,new Rectangle(40,0,130,item.Height),NavigationMenu.TextColor,flags);
            using(var font=new Font("Segoe UI",8.5f))TextRenderer.DrawText(e.Graphics,item.ShortcutKeyDisplayString,font,new Rectangle(170,0,item.Width-182,item.Height),NavigationMenu.Muted,flags|TextFormatFlags.Right);
        }
        internal static void DrawIcon(Graphics g,string code,Point point,Color color) {
            var saved=g.Save();g.TranslateTransform(point.X,point.Y);g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var p=new Pen(color,1.5f)) {
                p.StartCap=p.EndCap=LineCap.Round;p.LineJoin=LineJoin.Round;
                switch(code) {
                    case "home":g.DrawLines(p,new[]{new Point(1,8),new Point(9,1),new Point(17,8)});g.DrawLines(p,new[]{new Point(4,7),new Point(4,16),new Point(14,16),new Point(14,7)});break;
                    case "favorite":g.DrawLines(p,new[]{new Point(4,2),new Point(14,2),new Point(14,17),new Point(9,13),new Point(4,17),new Point(4,2)});break;
                    case "pin":g.DrawLines(p,new[]{new Point(5,2),new Point(13,2),new Point(12,8),new Point(15,11),new Point(3,11),new Point(6,8),new Point(5,2)});g.DrawLine(p,9,11,9,17);break;
                    case "move":g.DrawLine(p,9,1,9,17);g.DrawLine(p,1,9,17,9);g.DrawLines(p,new[]{new Point(6,4),new Point(9,1),new Point(12,4)});g.DrawLines(p,new[]{new Point(6,14),new Point(9,17),new Point(12,14)});break;
                    case "resize":g.DrawLine(p,3,15,15,3);g.DrawLines(p,new[]{new Point(3,9),new Point(3,15),new Point(9,15)});g.DrawLines(p,new[]{new Point(9,3),new Point(15,3),new Point(15,9)});break;
                    case "folder":g.DrawLines(p,new[]{new Point(1,5),new Point(1,15),new Point(17,15),new Point(17,5),new Point(9,5),new Point(7,2),new Point(1,2),new Point(1,5)});break;
                    case "help":g.DrawEllipse(p,1,1,16,16);g.DrawArc(p,6,4,6,6,180,230);g.DrawLine(p,9,10,9,11);g.DrawLine(p,9,14,9,14.2f);break;
                    case "zoom":g.DrawEllipse(p,1,1,12,12);g.DrawLine(p,11,11,17,17);break;
                }
            }
            g.Restore(saved);
        }
    }
    sealed class ZoomMenuPanel : Panel {
        readonly Func<double> getZoom;readonly Action<double> setZoom;readonly Button reset;
        public ZoomMenuPanel(Func<double> get,Action<double> set) {
            getZoom=get;setZoom=set;Name="menuZoom";Size=new Size(288,44);BackColor=NavigationMenu.Surface;DoubleBuffered=true;
            var minus=MakeButton("−","zoom-out",new Rectangle(147,6,32,32));minus.AccessibleName="缩小网页";minus.Click+=delegate{Change(Math.Max(.25,getZoom()-.1));};
            reset=MakeButton("","zoom-reset",new Rectangle(181,6,59,32));reset.AccessibleName="恢复网页缩放";reset.Click+=delegate{Change(1);};
            var plus=MakeButton("＋","zoom-in",new Rectangle(242,6,32,32));plus.AccessibleName="放大网页";plus.Click+=delegate{Change(Math.Min(5,getZoom()+.1));};RefreshValue();
        }
        Button MakeButton(string text,string name,Rectangle bounds) {
            var button=new Button{Text=text,Name=name,Bounds=bounds,BackColor=Color.FromArgb(48,55,68),ForeColor=NavigationMenu.TextColor,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",9),Cursor=Cursors.Hand,TabStop=true};
            button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=Color.FromArgb(65,76,95);Controls.Add(button);UiStyle.Round(button,7);return button;
        }
        void Change(double zoom) { setZoom(zoom);RefreshValue(); }
        void RefreshValue() { reset.Text=Math.Round(getZoom()*100)+"%"; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e);NavigationMenuRenderer.DrawIcon(e.Graphics,"zoom",new Point(12,13),NavigationMenu.Muted);TextRenderer.DrawText(e.Graphics,"网页缩放",Font,new Rectangle(40,0,100,Height),NavigationMenu.TextColor,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine); }
    }
}
