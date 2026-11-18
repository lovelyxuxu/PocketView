using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PocketView {
    static class Program {
        public static Store Data;
        public static EventWaitHandle Wake;
        public static bool Verify;
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static void Main(string[] args) {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string root = AppDomain.CurrentDomain.BaseDirectory;
            Verify = args.Contains("--verify");
            if(Verify) root = Path.GetFullPath(Path.Combine(root, "..", "work", "verification-profile"));
            string token;
            using(var hash = SHA256.Create()) token = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(root.ToLowerInvariant()))).Replace("-", "").Substring(0, 20);
            bool owner;
            using(var mutex = new Mutex(true, "Local\\PocketView-" + token, out owner)) {
                Wake = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PocketViewWake-" + token);
                if(!owner) { Wake.Set(); return; }
                try {
                    Data = new Store(root);
                    Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Data.Log(e.Exception.ToString()); MessageBox.Show(e.Exception.Message, "PocketView"); };
                    Application.Run(new BrowserWindow(args.FirstOrDefault(a => !a.StartsWith("--")), true));
                } catch(Exception ex) { MessageBox.Show("无法启动 PocketView，请将整个文件夹放在可写的位置。\n\n" + ex.Message, "PocketView"); }
                finally { Wake.Dispose(); }
                GC.KeepAlive(mutex);
            }
        }
    }
}
