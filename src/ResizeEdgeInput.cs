using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PocketView {
    // Chromium owns child HWNDs in another process. Parent WM_NCHITTEST alone
    // never receives their edge clicks. Handle only this window's edge input,
    // then hand sizing and capture back to Windows without drawing an overlay.
    sealed class ResizeEdgeInput : IDisposable {
        readonly Form owner;
        readonly HookProc callback;
        IntPtr hook;
        readonly Timer cursorTimer=new Timer();
        public bool IsSizing { get; set; }
        public bool Installed { get { return hook!=IntPtr.Zero; } }
        delegate IntPtr HookProc(int code,IntPtr wParam,IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] struct MouseData { public Point Point; public uint MouseDataValue,Flags,Time;public UIntPtr ExtraInfo; }
        [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
        [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wp,IntPtr lp);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
        [DllImport("user32.dll")]static extern IntPtr SetCursor(IntPtr cursor);
        [DllImport("user32.dll")]static extern bool ReleaseCapture();
        [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr hwnd,int message,IntPtr wp,IntPtr lp);
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        public ResizeEdgeInput(Form form) {
            owner=form;callback=OnMouse;
            hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);
            if(hook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法启用窗口边缘缩放。");
            cursorTimer.Interval=25;cursorTimer.Tick+=delegate {
                if(IsSizing||GetAsyncKeyState(1)<0||GetAsyncKeyState(2)<0)return;
                Point point=Cursor.Position;int hit=HitTestAt(point);
                if(hit!=0&&GetAncestor(WindowFromPoint(point),2)==owner.Handle)SetCursor(ResizeCursor(hit).Handle);
            };cursorTimer.Start();
        }
        public static int HitTest(Rectangle bounds,Point screen,int edge,int corner) {
            int x=screen.X-bounds.X,y=screen.Y-bounds.Y;
            if(x<0||y<0||x>=bounds.Width||y>=bounds.Height)return 0;
            bool l=x<edge,r=x>=bounds.Width-edge,t=y<edge,b=y>=bounds.Height-edge;
            if((l||t)&&x<corner&&y<corner)return 13;
            if((r||t)&&x>=bounds.Width-corner&&y<corner)return 14;
            if((l||b)&&x<corner&&y>=bounds.Height-corner)return 16;
            if((r||b)&&x>=bounds.Width-corner&&y>=bounds.Height-corner)return 17;
            return l?10:r?11:t?12:b?15:0;
        }
        public int HitTestAt(Point screen) {
            if(owner.IsDisposed||!owner.Visible||owner.WindowState!=FormWindowState.Normal)return 0;
            int edge=Math.Max(5,(int)Math.Round(owner.DeviceDpi*6.0/96));
            return HitTest(owner.Bounds,screen,edge,edge*2);
        }
        static Cursor ResizeCursor(int hit) { return hit==10||hit==11?Cursors.SizeWE:hit==12||hit==15?Cursors.SizeNS:hit==13||hit==17?Cursors.SizeNWSE:Cursors.SizeNESW; }
        IntPtr OnMouse(int code,IntPtr wp,IntPtr lp) {
            if(code<0)return CallNextHookEx(hook,code,wp,lp);
            try {
                int message=wp.ToInt32();
                if(message==0x0202){IsSizing=false;return CallNextHookEx(hook,code,wp,lp);}
                if(IsSizing||(message!=0x0200&&message!=0x0201))return CallNextHookEx(hook,code,wp,lp);
                if(message==0x0200&&(GetAsyncKeyState(1)<0||GetAsyncKeyState(2)<0))return CallNextHookEx(hook,code,wp,lp);
                var data=(MouseData)Marshal.PtrToStructure(lp,typeof(MouseData));
                int hit=HitTestAt(data.Point);
                if(hit==0||GetAncestor(WindowFromPoint(data.Point),2)!=owner.Handle)return CallNextHookEx(hook,code,wp,lp);
                SetCursor(ResizeCursor(hit).Handle);
                if(message==0x0201) {
                    IsSizing=true;SetForegroundWindow(owner.Handle);ReleaseCapture();
                    int coords=(data.Point.X&0xffff)|((data.Point.Y&0xffff)<<16);
                    if(!PostMessage(owner.Handle,0x00A1,(IntPtr)hit,(IntPtr)coords))IsSizing=false;
                }
                // Only consume the press. Mouse movement must always continue
                // through Windows, including when entering/leaving the margin.
                return message==0x0201?(IntPtr)1:CallNextHookEx(hook,code,wp,lp);
            } catch { return CallNextHookEx(hook,code,wp,lp); }
        }
        public void Dispose() { cursorTimer.Dispose();if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}GC.KeepAlive(callback); }
    }
}
