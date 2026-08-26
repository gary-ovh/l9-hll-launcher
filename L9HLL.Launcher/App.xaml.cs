using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace L9HLL.Launcher
{
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, "L9HLL.Launcher.SingleInstanceMutex", out createdNew);

            if (!createdNew)
            {
                var existing = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName)
                    .FirstOrDefault(p => p.Id != Process.GetCurrentProcess().Id);

                if (existing != null)
                {
                    var hwnd = existing.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        ShowWindowAsync(hwnd, 9);
                        SetForegroundWindow(hwnd);
                    }
                }

                _mutex.Dispose();
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}