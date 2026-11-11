using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher
{
    public partial class MainWindow : Window
    {
        private TrayService? _trayService;
        private bool _isHiding;
        private DispatcherTimer? _profileTimer;
        private bool _stopRestoreBusy;

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

            var guard = viewModel.SettingsGuard;
            _profileTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _profileTimer.Tick += (s, e) =>
            {
                bool active = guard.IsSeededProfileActive;
                if (StopRestoreBtn.Visibility != (active ? Visibility.Visible : Visibility.Collapsed))
                    StopRestoreBtn.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            };
            _profileTimer.Start();

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
            if (!HasOpenDialogs)
                Hide();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.ToggleSettingsCommand.Execute(null);
        }

        private async void StopRestoreBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_stopRestoreBusy) return;
            if (DataContext is not MainViewModel vm) return;

            _stopRestoreBusy = true;
            StopRestoreBtn.IsEnabled = false;
            StopRestoreBtn.Content = "Closing game...";

            try
            {
                vm.LaunchService.CloseGame();
                for (int i = 0; i < 30 && vm.SettingsGuard.IsSeededProfileActive; i++)
                {
                    await Task.Delay(1000);
                    vm.SettingsGuard.TryRestoreIfIdle();
                    StopRestoreBtn.Content = "Waiting for game to exit...";
                }

                string? error = vm.SettingsGuard.IsSeededProfileActive
                    ? vm.SettingsGuard.Restore()
                    : null;

                if (error != null)
                {
                    MessageBox.Show(error, "Settings Restore", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show("Game stopped and your settings have been restored.", "Settings Restored",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                MessageBox.Show($"Restore failed: {ex.Message}", "Settings Restore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _stopRestoreBusy = false;
                StopRestoreBtn.IsEnabled = true;
            }
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