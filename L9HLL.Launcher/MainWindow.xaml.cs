using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher
{
    public partial class MainWindow : Window
    {
        private TrayService? _trayService;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            _trayService = new TrayService(this);
            StateChanged += OnStateChanged;
            Closing += OnWindowClosing;

            MinimizeBtn.MouseEnter += (s, e) => MinimizeBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
            MinimizeBtn.MouseLeave += (s, e) => MinimizeBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.LightGray);
            CloseBtn.MouseEnter += (s, e) => CloseBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Red);
            CloseBtn.MouseLeave += (s, e) => CloseBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.LightGray);
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            _trayService?.Exit();
        }

        private void RootBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is Button || source is ComboBox || source is ScrollViewer ||
                    source is TextBlock)
                {
                    return;
                }
                source = VisualTreeHelper.GetParent(source);
            }
            DragMove();
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayService?.Dispose();
            base.OnClosed(e);
        }
    }
}