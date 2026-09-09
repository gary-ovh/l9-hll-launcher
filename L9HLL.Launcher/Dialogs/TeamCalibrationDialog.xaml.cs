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
        private readonly DispatcherTimer _pollTimer;
        private int _step;
        private bool _wasMouseDown;

        public TeamCalibrationDialog(IntPtr gameWindow)
        {
            InitializeComponent();
            _gameWindow = gameWindow;
            _dispatcher = Application.Current.Dispatcher;
            Topmost = true;
            ShowInTaskbar = false;
            SetStep(0);

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _pollTimer.Tick += OnPollTick;
        }

        private void ReadyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 0)
            {
                _step = 1;
                SetStep(1);
                _pollTimer.Start();
            }
            else if (_step == 3)
            {
                Close();
            }
            else
            {
                _pollTimer.Stop();
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
                    StepText.Text = "Calibration complete!";
                    StepText.Foreground = (Brush)new BrushConverter().ConvertFromString("#8a9a5b");
                    ReadyBtn.Content = "Close";
                    break;
            }
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            if (_step != 1 && _step != 2) return;

            bool isDown = (GetAsyncKeyState(VK_LEFT) & 0x8000) != 0;

            if (isDown && !_wasMouseDown)
            {
                // Mouse just pressed down - capture position
                POINT pt;
                GetCursorPos(out pt);

                var client = new POINT { x = pt.x, y = pt.y };
                if (ScreenToClientNative(_gameWindow, ref client))
                {
                    RECT r;
                    if (GetClientRect(_gameWindow, out r))
                    {
                        int w = r.Right - r.Left;
                        int h = r.Bottom - r.Top;
                        ConfigService.Log($"Calibration: click detected screen=({pt.x},{pt.y}) client=({client.x},{client.y}) area={w}x{h}");

                        if (w > 0 && h > 0 && client.x >= 0 && client.x < w && client.y >= 0 && client.y < h)
                        {
                            float xPct = (float)client.x / w;
                            float yPct = (float)client.y / h;
                            CapturedWidth = w;
                            CapturedHeight = h;
                            ConfigService.Log($"Calibration: VALID click at ({xPct:P1},{yPct:P1})");

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
                                _pollTimer.Stop();
                            }
                        }
                        else
                        {
                            ConfigService.Log($"Calibration: click outside game window area");
                        }
                    }
                }
                else
                {
                    ConfigService.Log($"Calibration: ScreenToClient failed for window {_gameWindow}");
                }
            }

            _wasMouseDown = isDown;
        }

        protected override void OnClosed(EventArgs e)
        {
            _pollTimer.Stop();
            base.OnClosed(e);
        }

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

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ScreenToClientNative(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        private const int VK_LEFT = 0x01;
    }
}
