using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher.Dialogs
{
    public partial class TeamCalibrationDialog : Window
    {
        public float AxisX { get; set; }
        public float AxisY { get; set; }
        public float AlliesX { get; set; }
        public float AlliesY { get; set; }
        public int CapturedWidth { get; set; }
        public int CapturedHeight { get; set; }
        public bool WasCalibrated { get; private set; }

        private readonly IntPtr _gameWindow;
        private readonly Dispatcher _dispatcher;
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelMouseProc? _hookProc;
        private int _step;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private static readonly Random Rng = new();

        public TeamCalibrationDialog(IntPtr gameWindow)
        {
            InitializeComponent();
            _gameWindow = gameWindow;
            _dispatcher = Application.Current.Dispatcher;
            Topmost = true;
            ShowInTaskbar = false;
            SetStep(0);
        }

        private void ReadyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 0)
            {
                _step = 1;
                SetStep(1);
                InstallHook();
            }
            else if (_step == 3)
            {
                Close();
            }
            else
            {
                UninstallHook();
                _step = 0;
                SetStep(0);
            }
        }

        private void SetStep(int step)
        {
            _step = step;
            switch (step)
            {
                case 0:
                    StepText.Text = "Make sure you're connected to a server and the team selection screen is visible in the game. Then click below to start capturing.";
                    StepText.Foreground = Brushes.White;
                    ReadyBtn.Content = "I'm Ready";
                    break;
                case 1:
                    StepText.Text = "Step 1/2 - Now click the AXIS team button in the game window.";
                    StepText.Foreground = (Brush)new BrushConverter().ConvertFromString("#ffd27f");
                    ReadyBtn.Content = "Cancel";
                    break;
                case 2:
                    StepText.Text = "Step 2/2 - Now click the ALLIES team button in the game window.";
                    StepText.Foreground = (Brush)new BrushConverter().ConvertFromString("#7fb8ff");
                    ReadyBtn.Content = "Cancel";
                    break;
                case 3:
                    UninstallHook();
                    StepText.Text = "Calibration complete!";
                    StepText.Foreground = (Brush)new BrushConverter().ConvertFromString("#8a9a5b");
                    ReadyBtn.Content = "Close";
                    break;
            }
        }

        private void InstallHook()
        {
            if (_hookId != IntPtr.Zero) return;
            _hookProc = HookCallback;
            _hookId = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, GetModuleHandle(null), 0);
            if (_hookId == IntPtr.Zero)
            {
                ConfigService.Log("Calibration: failed to install mouse hook");
                _hookProc = null;
                _step = 0;
                SetStep(0);
            }
        }

        private void UninstallHook()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _hookProc = null;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN && (_step == 1 || _step == 2))
            {
                try
                {
                    var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    _dispatcher.BeginInvoke((Action)(() => OnGameClick(info.pt)));
                }
                catch (Exception ex)
                {
                    ConfigService.LogError(ex);
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void OnGameClick(POINT pt)
        {
            if (_step != 1 && _step != 2) return;

            var client = new POINT { x = pt.x, y = pt.y };
            if (!ScreenToClientNative(_gameWindow, ref client)) return;

            RECT r;
            if (!GetClientRect(_gameWindow, out r)) return;
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) return;

            if (client.x < 0 || client.x >= w || client.y < 0 || client.y >= h)
            {
                StepText.Text = "That click was outside the game window. Try again.";
                return;
            }

            float xPct = (float)client.x / w;
            float yPct = (float)client.y / h;
            CapturedWidth = w;
            CapturedHeight = h;
            ConfigService.Log($"Calibration: captured click at {client.x},{client.y} ({xPct:P1},{yPct:P1}) of {w}x{h}");

            if (_step == 1)
            {
                AxisX = xPct;
                AxisY = yPct;
                SetStep(2);
            }
            else
            {
                AlliesX = xPct;
                AlliesY = yPct;
                WasCalibrated = true;
                SetStep(3);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            UninstallHook();
            base.OnClosed(e);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ScreenToClientNative(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    }
}
