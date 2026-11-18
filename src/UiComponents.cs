using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PocketView {
    static class UiStyle {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wparam,IntPtr lparam);
        public static void Drag(Control surface,Form owner) { surface.MouseDown+=delegate(object s,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(owner.Handle,0x00A1,(IntPtr)2,IntPtr.Zero);}}; }
        public static readonly Color Ink=Color.FromArgb(32,39,51),Muted=Color.FromArgb(112,121,135),Surface=Color.FromArgb(246,247,249),Blue=Color.FromArgb(37,99,235);
        public static GraphicsPath Rounded(Rectangle r,int radius) {
            var path=new GraphicsPath();int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
            path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d-1,r.Top,d,d,270,90);
            path.AddArc(r.Right-d-1,r.Bottom-d-1,d,d,0,90);path.AddArc(r.Left,r.Bottom-d-1,d,d,90,90);path.CloseFigure();return path;
        }
        public static void Round(Control c,int radius) { if(c.Width<2||c.Height<2)return;using(var path=Rounded(c.ClientRectangle,radius)){var old=c.Region;c.Region=new Region(path);if(old!=null)old.Dispose();} }
        public static Button Button(string text,bool primary) {
            var b=new Button{Text=text,FlatStyle=FlatStyle.Flat,BackColor=primary?Blue:Color.White,ForeColor=primary?Color.White:Ink,Font=new Font("Microsoft YaHei UI",9),Cursor=Cursors.Hand,Height=36,Width=90,TabStop=true};
            b.FlatAppearance.BorderSize=0;b.SizeChanged+=delegate{Round(b,8);};Round(b,8);return b;
        }
    }
    sealed class TabSelectButton : Button {
        public string Caption,Subtitle;public bool Selected,Loading;bool hovered;
        public TabSelectButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;TabStop=false;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);MouseEnter+=delegate{hovered=true;Invalidate();};MouseLeave+=delegate{hovered=false;Invalidate();};}
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent.BackColor);
            if(hovered&&!Selected)using(var b=new SolidBrush(Color.FromArgb(235,238,243)))using(var path=UiStyle.Rounded(ClientRectangle,9))g.FillPath(b,path);
            using(var b=new SolidBrush(Selected?Color.FromArgb(233,240,255):Color.FromArgb(228,232,238)))g.FillEllipse(b,10,17,28,28);
            string monogram=Loading?"·":String.IsNullOrEmpty(Caption)?"P":Caption.Substring(0,1).ToUpperInvariant();
            using(var f=new Font("Segoe UI",10,FontStyle.Bold))TextRenderer.DrawText(g,monogram,f,new Rectangle(10,17,28,28),Selected?UiStyle.Blue:UiStyle.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            using(var f=new Font("Microsoft YaHei UI",9))TextRenderer.DrawText(g,Caption,f,new Rectangle(48,11,Math.Max(1,Width-54),22),UiStyle.Ink,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            using(var f=new Font("Segoe UI",8))TextRenderer.DrawText(g,Subtitle,f,new Rectangle(48,34,Math.Max(1,Width-54),18),UiStyle.Muted,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }
    }
    sealed class HelpDialog : Form {
        public HelpDialog() {
            Text="PocketView · 快捷键";Name="helpDialog";FormBorderStyle=FormBorderStyle.None;ClientSize=new Size(720,560);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;BackColor=Color.White;ForeColor=UiStyle.Ink;Font=new Font("Microsoft YaHei UI",9);KeyPreview=true;
            var header=new Panel{Dock=DockStyle.Top,Height=84,Padding=new Padding(28,20,20,16)};
            var heading=new Label{Text="快捷键",Font=new Font("Microsoft YaHei UI",17,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};
            var close=UiStyle.Button("×",false);close.Width=32;close.Dock=DockStyle.Right;close.AccessibleName="关闭说明";close.Click+=delegate{Close();};header.Controls.Add(heading);header.Controls.Add(close);
            UiStyle.Drag(header,this);UiStyle.Drag(heading,this);
            var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(28,0,28,0)};
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            var left=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Margin=new Padding(0,0,16,0)};
            var right=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Margin=new Padding(16,0,0,0)};
            content.Controls.Add(left,0,0);content.Controls.Add(right,1,0);
            AddSection(left,"浏览",new[,]{{"收藏首页","Alt + Home"},{"输入网址 / 搜索","Ctrl + L"},{"后退 / 前进","Alt + ← / →"},{"收藏当前网页","Ctrl + D"},{"刷新","F5"}});
            AddSection(left,"窗口",new[,]{{"固定显示导航","F2"},{"置顶","Ctrl + Shift + T"},{"拖动窗口","Alt + 鼠标拖动"},{"移动 / 调整大小","Alt + F7 / F8"},{"最大化 / 还原","F11"},{"关闭窗口","Alt + F4"}});
            AddSection(right,"标签页",new[,]{{"新建 / 关闭","Ctrl + T / W"},{"切换标签","Ctrl + Tab"},{"反向切换","Ctrl + Shift + Tab"},{"显示 / 隐藏侧栏","Ctrl + Shift + B"}});
            AddSection(right,"唤出窗口",new[,]{{"运行时有效","Ctrl + Alt + Space"}});
            var noteTitle=new Label{Text="观看",Font=new Font("Microsoft YaHei UI",9,FontStyle.Bold),Width=310,Height=28,Margin=new Padding(0,18,0,0)};
            var note=new Label{Text="鼠标移到窗口顶部显示导航。\n\n拖动四边或四角调整窗口大小。\n\n视频全屏填满当前窗口，按 Esc 退出。",ForeColor=UiStyle.Muted,AutoSize=false,Width=308,Height=140,Margin=Padding.Empty};right.Controls.Add(noteTitle);right.Controls.Add(note);
            var footer=new Panel{Dock=DockStyle.Bottom,Height=66,Padding=new Padding(28,14,28,16)};var done=UiStyle.Button("完成",true);done.Dock=DockStyle.Right;done.DialogResult=DialogResult.OK;footer.Controls.Add(done);AcceptButton=done;CancelButton=done;
            Controls.Add(content);Controls.Add(footer);Controls.Add(header);SizeChanged+=delegate{UiStyle.Round(this,14);};UiStyle.Round(this,14);
            KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Escape)Close();};
        }
        void AddSection(FlowLayoutPanel content,string title,string[,] rows) {
            var heading=new Label{Text=title,Font=new Font("Microsoft YaHei UI",9,FontStyle.Bold),ForeColor=UiStyle.Ink,Width=310,Height=28,Margin=new Padding(0,12,0,0)};content.Controls.Add(heading);
            for(int i=0;i<rows.GetLength(0);i++) {
                var row=new Panel{Width=310,Height=27,Margin=Padding.Empty};
                row.Controls.Add(new Label{Text=rows[i,0],AutoSize=false,Bounds=new Rectangle(0,3,118,24),ForeColor=UiStyle.Muted});
                row.Controls.Add(new Label{Text=rows[i,1],AutoSize=false,Bounds=new Rectangle(118,3,186,24),TextAlign=ContentAlignment.TopRight,Font=new Font("Segoe UI",9),ForeColor=UiStyle.Ink});content.Controls.Add(row);
            }
        }
    }
}
