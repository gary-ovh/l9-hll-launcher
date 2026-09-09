using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
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

        private readonly Func<IntPtr> _windowProvider;
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelMouseProc? _hookProc;
        private int _step;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        public TeamCalibrationDialog(Func<IntPtr> windowProvider)
        {
            InitializeComponent();
            _windowProvider = windowProvider;
            Topmost = true;
            ShowInTaskbar = false;
            SetStep(0);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            InstallHook();
        }

        private void ReadyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 0)
            {
                _step = 1;
                SetStep(1);
            }
            else if (_step == 4)
            {
                Close();
            }
            else
            {
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
                    StepText.Text = "Make sure the team selection screen is visible in the game. Then click below to start capturing.";
                    StepText.Foreground = Brushes.White;
                    ReadyBtn.Content = "I'm Ready";
                    break;
                case 1:
                    StepText.Text = "Step 1/3 - Now click the AXIS team button in the game window.";
                    StepText.Foreground = (Brush)new BrushConverter().ConvertFromString("#ffd27f");
                    ReadyBtn.Content = "Cancel";
                    break;
                case 2:
                    StepText.Text = "Step 2/3 - Now click SWITCH TEAMS in the game to go back to team selection. (This click is not recorded.)";
                    StepText.Foreground = Brushes.Gray;
                    ReadyBtn.Content = "Cancel";
                    break;
                case 3:
                    StepText.Text = "Step 3/3 - Now click the ALLIES team button in the game window.";
                    StepText.Foreground = (Brush)new BrushConverter().ConvertFromString("#7fb8ff");
                    ReadyBtn.Content = "Cancel";
                    break;
                case 4:
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
                int err = Marshal.GetLastWin32Error();
                ConfigService.Log($"Calibration: FAILED to install mouse hook, error {err}");
                _hookProc = null;
                StepText.Text = "Failed to install mouse hook (see debug.log).";
                StepText.Foreground = Brushes.Red;
                ReadyBtn.Content = "Close";
                _step = 4;
                return;
            }
            ConfigService.Log($"Calibration: mouse hook installed");
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
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                try
                {
                    var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    HandleGameClick(info.pt);
                }
                catch (Exception ex)
                {
                    ConfigService.LogError(ex);
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        // Runs on the UI thread (hook was installed there) - direct UI updates are safe.
        private void HandleGameClick(POINT screenPt)
        {
            if (_step != 1 && _step != 2 && _step != 3) return;

            var gameWindow = _windowProvider();
            if (gameWindow == IntPtr.Zero || !IsWindow(gameWindow))
            {
                ConfigService.Log($"Calibration: LBUTTONDOWN screen=({screenPt.x},{screenPt.y}) but no valid game window found (step={_step})");
                return;
            }

            var client = new POINT { x = screenPt.x, y = screenPt.y };
            bool screenOk = ScreenToClient(gameWindow, ref client);
            GetClientRect(gameWindow, out RECT r);
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            ConfigService.Log($"Calibration: LBUTTONDOWN screen=({screenPt.x},{screenPt.y}) client=({client.x},{client.y}) screenOk={screenOk} clientArea={w}x{h} step={_step}");

            if (!screenOk || w <= 0 || h <= 0) return;

            if (client.x < 0 || client.x >= w || client.y < 0 || client.y >= h)
            {
                StepText.Text = "That click was outside the game window. Click the button in the game.";
                return;
            }

            if (_step == 2)
            {
                ConfigService.Log("Calibration: switch teams click (discarded)");
                SetStep(3);
                return;
            }

            float xPct = (float)client.x / w;
            float yPct = (float)client.y / h;
            CapturedWidth = w;
            CapturedHeight = h;
            ConfigService.Log($"Calibration: captured step {_step} at ({xPct:P2},{yPct:P2})");

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
                SetStep(4);
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

        [DllImport("user32.dll", SetLastError = true)]
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
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);
    }
}
