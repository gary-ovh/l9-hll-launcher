using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher
{
    public partial class MainWindow : Window
    {
        private TrayService? _trayService;
        private bool _isHiding;

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainViewModel();
            DataContext = viewModel;
            _trayService = new TrayService(this, viewModel, viewModel.LaunchService, viewModel.QueryService);
            StateChanged += OnStateChanged;
            Closing += OnWindowClosing;

            App.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            App.Current.Exit += OnAppExit;

            SetIconFromResource();

            Loaded += (s, e) =>
            {
                var settings = viewModel.ConfigService.LoadSettings();
                if (settings.StartMinimized)
                {
                    Hide();
                }
            };

            MinimizeBtn.MouseEnter += (s, e) => MinimizeBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
            MinimizeBtn.MouseLeave += (s, e) => MinimizeBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.LightGray);
            CloseBtn.MouseEnter += (s, e) => CloseBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Red);
            CloseBtn.MouseLeave += (s, e) => CloseBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.LightGray);
        }

        private void SetIconFromResource()
        {
            try
            {
                var stream = System.Windows.Application.GetResourceStream(new Uri("Assets/icon.ico", UriKind.Relative));
                if (stream?.Stream != null)
                {
                    var decoder = BitmapDecoder.Create(stream.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    Icon = decoder.Frames[0];
                }
            }
            catch { }
        }

        private bool HasOpenDialogs => OwnedWindows.Cast<Window>().Any(w => w.IsVisible);

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                System.IO.File.AppendAllText("crash.log", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {e.Exception.GetType().Name}: {e.Exception.Message}\n{e.Exception.StackTrace}\n\n");
            }
            catch { }
            e.Handled = true;
        }

        private void OnAppExit(object sender, ExitEventArgs e)
        {
            _trayService?.Dispose();
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && !_isHiding && !HasOpenDialogs)
            {
                _isHiding = true;
                Hide();
                _isHiding = false;
            }
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            if (!HasOpenDialogs)
                Hide();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!HasOpenDialogs)
                Hide();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            _trayService?.Exit();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.ToggleSettingsCommand.Execute(null);
        }

        private void SettingsBtn_MouseEnter(object sender, MouseEventArgs e)
        {
            SettingsBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
        }

        private void SettingsBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            SettingsBtn.Foreground = new SolidColorBrush(System.Windows.Media.Colors.LightGray);
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

                if (source is System.Windows.Documents.Run ||
                    source is System.Windows.Documents.TextPointer ||
                    source is System.Windows.Documents.Paragraph ||
                    source is System.Windows.Documents.FlowDocument)
                {
                    break;
                }

                if (source is System.Windows.Media.Visual ||
                    source is System.Windows.Media.Media3D.Visual3D)
                {
                    source = System.Windows.Media.VisualTreeHelper.GetParent(source);
                }
                else
                {
                    break;
                }
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