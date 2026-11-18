using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PocketView {
    class NavigationIsland : Panel {
        public NavigationIsland() {
            DoubleBuffered=true; Name="navigationIsland";
            BackColor=Color.FromArgb(31,35,43); Visible=false;
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var separator=new Pen(Color.FromArgb(65,70,80)))foreach(string name in new[]{"nav-address","nav-more","nav-minimize"}){var c=Controls[name];if(c!=null)g.DrawLine(separator,c.Left-5,19,c.Left-5,35);}
            if(Height<=60)return;
            int x=14,y=54,w=Width-28,h=36,d=20;
            using(var path=new GraphicsPath())using(var b=new SolidBrush(Color.FromArgb(244,246,249))) {
                path.AddArc(x,y,d,d,180,90);path.AddArc(x+w-d,y,d,d,270,90);path.AddArc(x+w-d,y+h-d,d,d,0,90);path.AddArc(x,y+h-d,d,d,90,90);path.CloseFigure();g.FillPath(b,path);
            }
        }
        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
            if(Width<1 || Height<1) return;
            int radius=Math.Min(18,Height/2), d=Math.Max(1,radius*2);
            using(var path=new GraphicsPath()) {
                path.AddArc(0,0,d,d,180,90); path.AddArc(Width-d-1,0,d,d,270,90);
                path.AddArc(Width-d-1,Height-d-1,d,d,0,90); path.AddArc(0,Height-d-1,d,d,90,90); path.CloseFigure();
                var old=Region; Region=new Region(path); if(old!=null)old.Dispose();
            }
        }
    }
    class NavigationButton : Button {
        public readonly string IconCode;
        public bool Active;
        bool hovered;
        public NavigationButton(string code) {
            IconCode=code; Name="nav-"+code; FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0;
            Cursor=Cursors.Hand; TabStop=false; SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            MouseEnter+=delegate{hovered=true;Invalidate();}; MouseLeave+=delegate{hovered=false;Invalidate();};
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent.BackColor);
            if(hovered && Enabled) using(var b=new SolidBrush(IconCode=="close"?Color.FromArgb(142,61,69):Color.FromArgb(57,61,71))) g.FillEllipse(b,2,2,Width-4,Height-4);
            Color color=!Enabled?Color.FromArgb(91,96,108):Active?Color.FromArgb(137,180,255):Color.FromArgb(224,227,235);
            using(var p=new Pen(color,1.65f)) {
                p.StartCap=p.EndCap=LineCap.Round; p.LineJoin=LineJoin.Round;
                float x=(Width-20)/2f,y=(Height-20)/2f;g.TranslateTransform(x,y);
                switch(IconCode) {
                    case "home":g.DrawLines(p,new[]{new PointF(2,9),new PointF(10,2),new PointF(18,9)});g.DrawLines(p,new[]{new PointF(5,8),new PointF(5,17),new PointF(15,17),new PointF(15,8)});break;
                    case "back":g.DrawLine(p,4,10,17,10);g.DrawLines(p,new[]{new PointF(9,4),new PointF(3,10),new PointF(9,16)});break;
                    case "forward":g.DrawLine(p,3,10,16,10);g.DrawLines(p,new[]{new PointF(11,4),new PointF(17,10),new PointF(11,16)});break;
                    case "reload":g.DrawArc(p,3,3,14,14,35,285);g.DrawLines(p,new[]{new PointF(17,3),new PointF(17,8),new PointF(12,8)});break;
                    case "address":g.DrawEllipse(p,2,2,16,16);g.DrawEllipse(p,6,2,8,16);g.DrawLine(p,2,10,18,10);break;
                    case "tabs":g.DrawRectangle(p,2,3,16,14);g.DrawLine(p,8,3,8,17);g.DrawLine(p,4,7,6,7);g.DrawLine(p,4,11,6,11);break;
                    case "favorite":var points=new PointF[10];for(int i=0;i<10;i++){double a=-Math.PI/2+i*Math.PI/5;double r=i%2==0?8:3.6;points[i]=new PointF(10+(float)(Math.Cos(a)*r),10+(float)(Math.Sin(a)*r));}if(Active)using(var b=new SolidBrush(color))g.FillPolygon(b,points);else g.DrawPolygon(p,points);break;
                    case "pin":g.DrawLines(p,new[]{new PointF(6,3),new PointF(14,3),new PointF(13,9),new PointF(16,12),new PointF(4,12),new PointF(7,9),new PointF(6,3)});g.DrawLine(p,10,12,10,18);break;
                    case "more":using(var b=new SolidBrush(color)){g.FillEllipse(b,3,9,2,2);g.FillEllipse(b,9,9,2,2);g.FillEllipse(b,15,9,2,2);}break;
                    case "move":g.DrawLine(p,10,2,10,18);g.DrawLine(p,2,10,18,10);g.DrawLines(p,new[]{new PointF(7,5),new PointF(10,2),new PointF(13,5)});g.DrawLines(p,new[]{new PointF(7,15),new PointF(10,18),new PointF(13,15)});break;
                    case "minimize":g.DrawLine(p,4,13,16,13);break;
                    case "maximize":g.DrawRectangle(p,4,4,12,12);break;
                    case "close":g.DrawLine(p,5,5,15,15);g.DrawLine(p,15,5,5,15);break;
                }
            }
        }
    }
}
