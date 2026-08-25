using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher.Dialogs
{
    public partial class SettingsDialog : Window
    {
        private readonly ConfigService _config;
        private readonly UpdateService _updateService;
        private readonly AutoLaunchService _autoLaunch;
        private readonly Action _onSettingsChanged;

        public SettingsDialog(
            ConfigService config,
            UpdateService updateService,
            AutoLaunchService autoLaunch,
            Action onSettingsChanged)
        {
            InitializeComponent();
            _config = config;
            _updateService = updateService;
            _autoLaunch = autoLaunch;
            _onSettingsChanged = onSettingsChanged;

            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _config.LoadSettings();
                StartupCheckBox.IsChecked = settings.StartupOnBoot;
                MinimizedCheckBox.IsChecked = settings.StartMinimized;
                UpdatesCheckBox.IsChecked = settings.CheckUpdates;

                if (_autoLaunch.Enabled)
                    AutoLaunchTimeText.Text = _autoLaunch.ScheduledTime.ToString(@"hh\:mm");
                else
                    AutoLaunchTimeText.Text = "Not set";

                VersionText.Text = $"Version {ConfigService.CurrentVersion}";
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        private void OnStartupChecked(object sender, RoutedEventArgs e)
        {
            var settings = _config.LoadSettings();
            settings.StartupOnBoot = true;
            _config.SaveSettings(settings);
            StartupService.AddToStartup();
            _onSettingsChanged?.Invoke();
        }

        private void OnStartupUnchecked(object sender, RoutedEventArgs e)
        {
            var settings = _config.LoadSettings();
            settings.StartupOnBoot = false;
            _config.SaveSettings(settings);
            StartupService.RemoveFromStartup();
            _onSettingsChanged?.Invoke();
        }

        private void OnMinimizedChecked(object sender, RoutedEventArgs e)
        {
            var settings = _config.LoadSettings();
            settings.StartMinimized = true;
            _config.SaveSettings(settings);
            _onSettingsChanged?.Invoke();
        }

        private void OnMinimizedUnchecked(object sender, RoutedEventArgs e)
        {
            var settings = _config.LoadSettings();
            settings.StartMinimized = false;
            _config.SaveSettings(settings);
            _onSettingsChanged?.Invoke();
        }

        private void OnUpdatesChecked(object sender, RoutedEventArgs e)
        {
            var settings = _config.LoadSettings();
            settings.CheckUpdates = true;
            _config.SaveSettings(settings);
            _onSettingsChanged?.Invoke();
        }

        private void OnUpdatesUnchecked(object sender, RoutedEventArgs e)
        {
            var settings = _config.LoadSettings();
            settings.CheckUpdates = false;
            _config.SaveSettings(settings);
            _onSettingsChanged?.Invoke();
        }

        private void ChangeTimeBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hour = (int)_autoLaunch.ScheduledTime.TotalHours;
                var minute = _autoLaunch.ScheduledTime.Minutes;

                var timePicker = new TimePickerDialog(hour, minute);
                if (timePicker.ShowDialog() == true)
                {
                    _autoLaunch.ScheduledTime = new TimeSpan(timePicker.SelectedHour, timePicker.SelectedMinute, 0);
                    _autoLaunch.Enabled = true;
                    _autoLaunch.ResetTrigger();
                    AutoLaunchTimeText.Text = _autoLaunch.ScheduledTime.ToString(@"hh\:mm");
                    _onSettingsChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckNowBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatusText.Text = "Checking...";
                UpdateStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8a9a5b"));
                await _updateService.ForceCheckAsync();
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                UpdateStatusText.Text = "Check failed";
                UpdateStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        public void SetUpdateTimeStatus(string status)
        {
            try
            {
                if (status.Contains("up to date", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatusText.Text = "Up to date";
                    UpdateStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8a9a5b"));
                }
                else if (status.Contains("available", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatusText.Text = $"v{status.Split(' ')[0]} available!";
                    UpdateStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#d4a017"));
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    }
}