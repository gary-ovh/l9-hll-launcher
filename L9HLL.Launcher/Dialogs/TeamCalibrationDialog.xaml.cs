using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using L9HLL.Launcher.Services;
using GdiBitmap = System.Drawing.Bitmap;
using GdiGraphics = System.Drawing.Graphics;
using GdiImageFormat = System.Drawing.Imaging.ImageFormat;
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

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
        private int _step;
        private int _imgW;
        private int _imgH;
        private double _clickScaleX = 1.0;
        private double _clickScaleY = 1.0;

        public TeamCalibrationDialog(IntPtr gameWindow)
        {
            InitializeComponent();
            _gameWindow = gameWindow;
            Topmost = true;
            ShowInTaskbar = false;
            SetStep(0);
            Loaded += async (s, e) => await CaptureAsync();
        }

        private void ReadyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 0)
            {
                _step = 1;
                SetStep(1);
            }
            else if (_step == 3)
            {
                Close();
            }
            else
            {
                _step = 0;
                SetStep(0);
                AxisDot.Visibility = Visibility.Collapsed;
                AxisMarker.Visibility = Visibility.Collapsed;
                AlliesDot.Visibility = Visibility.Collapsed;
                AlliesMarker.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e) => _ = CaptureAsync();

        private void SetStep(int step)
        {
            _step = step;
            switch (step)
            {
                case 0:
                    StepText.Text = "Click a team button on the screenshot below.";
                    StepText.Foreground = Brushes.White;
                    ReadyBtn.Content = "Start";
                    RefreshBtn.Visibility = Visibility.Collapsed;
                    ScreenshotImage.Cursor = Cursors.Arrow;
                    break;
                case 1:
                    StepText.Text = "Step 1/2 - Click the AXIS team button on the screenshot.";
                    StepText.Foreground = (Brush)new BrushConverter().ConvertFromString("#ffd27f");
                    ReadyBtn.Content = "Cancel";
                    RefreshBtn.Visibility = Visibility.Visible;
                    ScreenshotImage.Cursor = Cursors.Cross;
                    break;
                case 2:
                    StepText.Text = "Step 2/2 - Click the ALLIES team button on the screenshot.";
                    StepText.Foreground = (Brush)new BrushConverter().ConvertFromString("#7fb8ff");
                    ReadyBtn.Content = "Cancel";
                    RefreshBtn.Visibility = Visibility.Visible;
                    ScreenshotImage.Cursor = Cursors.Cross;
                    break;
                case 3:
                    StepText.Text = "Calibration complete!";
                    StepText.Foreground = (Brush)new BrushConverter().ConvertFromString("#8a9a5b");
                    ReadyBtn.Content = "Close";
                    RefreshBtn.Visibility = Visibility.Visible;
                    ScreenshotImage.Cursor = Cursors.Arrow;
                    break;
            }
        }

        private void ScreenshotImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_step != 1 && _step != 2) return;

            var pos = e.GetPosition(ScreenshotImage);
            double imgX = pos.X / _clickScaleX;
            double imgY = pos.Y / _clickScaleY;
            if (imgX < 0 || imgY < 0 || imgX >= _imgW || imgY >= _imgH) return;

            float xPct = (float)(imgX / _imgW);
            float yPct = (float)(imgY / _imgH);
            CapturedWidth = _imgW;
            CapturedHeight = _imgH;
            ConfigService.Log($"Calibration: click at image ({imgX:F0},{imgY:F0}) of {_imgW}x{_imgH} -> ({xPct:P2},{yPct:P2})");

            if (_step == 1)
            {
                AxisX = xPct;
                AxisY = yPct;
                PlaceDot(AxisDot, AxisMarker, pos.X, pos.Y);
                SetStep(2);
            }
            else
            {
                AlliesX = xPct;
                AlliesY = yPct;
                PlaceDot(AlliesDot, AlliesMarker, pos.X, pos.Y);
                WasCalibrated = true;
                SetStep(3);
            }
        }

        private void PlaceDot(Ellipse dot, Line line, double x, double y)
        {
            dot.Visibility = Visibility.Visible;
            Canvas.SetLeft(dot, x - 6);
            Canvas.SetTop(dot, y - 6);
            line.Visibility = Visibility.Visible;
            line.X1 = x; line.Y1 = 0;
            line.X2 = x; line.Y2 = ScreenshotImage.Height;
        }

        private async System.Threading.Tasks.Task CaptureAsync()
        {
            try
            {
                using var bmp = CaptureWindow(_gameWindow);
                if (bmp == null || bmp.Width == 0) return;

                _imgW = bmp.Width;
                _imgH = bmp.Height;
                ConfigService.Log($"Calibration: captured game window {_imgW}x{_imgH}");

                using var ms = new System.IO.MemoryStream();
                bmp.Save(ms, GdiImageFormat.Png);
                ms.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();

                ScreenshotImage.Source = image;
                ScreenshotImage.Width = _imgW;
                ScreenshotImage.Height = _imgH;

                double scale = 1.0;
                double maxW = SystemParameters.PrimaryScreenWidth * 0.8;
                double maxH = SystemParameters.PrimaryScreenHeight * 0.65;
                if (_imgW > maxW || _imgH > maxH)
                {
                    scale = Math.Min(maxW / _imgW, maxH / _imgH);
                    ScreenshotImage.Width = _imgW * scale;
                    ScreenshotImage.Height = _imgH * scale;
                    ScreenshotImage.Stretch = Stretch.Fill;
                }
                else
                {
                    ScreenshotImage.Stretch = Stretch.None;
                }

                ScreenshotBorder.Width = ScreenshotImage.Width + 2;
                ScreenshotBorder.Height = ScreenshotImage.Height + 2;
                Width = ScreenshotImage.Width + 64;
                Height = ScreenshotImage.Height + 150;

                // Store scale for click mapping
                _clickScaleX = scale;
                _clickScaleY = scale;
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                StepText.Text = "Failed to capture game window. Is the game visible (not minimized)?";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }

        private static GdiBitmap? CaptureWindow(IntPtr hWnd)
        {
            if (!GetClientRect(hWnd, out RECT cr)) return null;
            int w = cr.Right - cr.Left;
            int h = cr.Bottom - cr.Top;
            if (w <= 0 || h <= 0) return null;

            using var bmp = new GdiBitmap(w, h, GdiPixelFormat.Format32bppArgb);
            using var g = GdiGraphics.FromImage(bmp);
            IntPtr hdc = g.GetHdc();
            IntPtr mem = CreateDC("DISPLAY", null, null, IntPtr.Zero);
            IntPtr old = SelectObject(mem, CreateCompatibleBitmap(mem, w, h));

            bool ok = PrintWindow(hWnd, mem, 0x00000002); // PW_RENDERFULLCONTENT
            if (!ok)
                ok = BitBlt(mem, 0, 0, w, h, hdc, 0, 0, 0xcc0020);

            SelectObject(mem, old);
            g.ReleaseHdc();
            DeleteDC(mem);

            return ok ? bmp : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateDC(string lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInit);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BitBlt(IntPtr hdc, int x, int y, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);
    }
}
