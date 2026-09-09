using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using L9HLL.Launcher.Models;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher.Dialogs
{
    public partial class SettingsDialog : Window
    {
        private readonly ConfigService _config;
        private readonly UpdateService _updateService;
        private readonly AutoLaunchService _autoLaunch;
        private readonly LaunchService _launchService;
        private readonly Action _onSettingsChanged;

        public SettingsDialog(
            ConfigService config,
            UpdateService updateService,
            AutoLaunchService autoLaunch,
            LaunchService launchService,
            Action onSettingsChanged)
        {
            InitializeComponent();
            _config = config;
            _updateService = updateService;
            _autoLaunch = autoLaunch;
            _launchService = launchService;
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
                AutoTeamCheckBox.IsChecked = settings.AutoTeamEnabled;

                if (_autoLaunch.Enabled)
                    AutoLaunchTimeText.Text = _autoLaunch.ScheduledTime.ToString(@"hh\:mm");
                else
                    AutoLaunchTimeText.Text = "Not set";

                VersionText.Text = $"Version {ConfigService.CurrentVersion}";

                var preference = settings.AutoTeamPreference ?? "random";
                foreach (var item in TeamCombo.Items)
                {
                    if (item is ComboBoxItem ci && ci.Tag?.ToString() == preference)
                    {
                        TeamCombo.SelectedItem = item;
                        break;
                    }
                }
                TeamCombo.SelectionChanged += (s, e) =>
                {
                    if (TeamCombo.SelectedItem is ComboBoxItem sel && sel.Tag is string tag)
                    {
                        var cfg = _config.LoadSettings();
                        cfg.AutoTeamPreference = tag;
                        _config.SaveSettings(cfg);
                    }
                };

                UpdateCalibrationStatus(settings);
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        private void UpdateCalibrationStatus(AppSettings settings)
        {
            if (settings.TeamCalibrations != null && settings.TeamCalibrations.Count > 0)
            {
                CalibrationStatusText.Text = $"Calibrated ({settings.TeamCalibrations.Count} resolution(s))";
                CalibrationStatusText.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#8a9a5b"));
            }
            else
            {
                CalibrationStatusText.Text = "Not calibrated";
                CalibrationStatusText.Foreground = Brushes.Orange;
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

        private void OnAutoTeamChecked(object sender, RoutedEventArgs e)
        {
            var settings = _config.LoadSettings();
            settings.AutoTeamEnabled = true;
            _config.SaveSettings(settings);
            _onSettingsChanged?.Invoke();
        }

        private void OnAutoTeamUnchecked(object sender, RoutedEventArgs e)
        {
            var settings = _config.LoadSettings();
            settings.AutoTeamEnabled = false;
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

        private async void CalibrateBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "Calibration will launch the game and connect to a server.\n" +
                    "When the team selection screen is visible, you will be asked to click\n" +
                    "the Axis and Allies team buttons in the game.\n\nProceed?",
                    "Calibrate Team Join",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                var server = new ServerStatus
                {
                    Name = "[L9] The Loyal Nine |#1|",
                    Ip = "40.27.41.16",
                    Port = 7777,
                    Game = "hll"
                };

                System.Windows.MessageBox.Show(
                    "The game will now launch and connect. When you see the team selection\n" +
                    "screen, a calibration window will appear. Click 'I'm Ready' there, then\n" +
                    "click the team buttons in-game.",
                    "Calibration", MessageBoxButton.OK, MessageBoxImage.Information);

                _launchService.LaunchServer(server);
                CalibrationStatusText.Text = "Waiting for game window...";
                CalibrationStatusText.Foreground = Brushes.Orange;

                IntPtr gameWindow = IntPtr.Zero;
                for (int i = 0; i < 180; i++)
                {
                    await Task.Delay(1000);
                    gameWindow = _launchService.FindGameWindowPublic(false);
                    if (gameWindow != IntPtr.Zero) break;
                }

                if (gameWindow == IntPtr.Zero)
                {
                    CalibrationStatusText.Text = "Failed";
                    CalibrationStatusText.Foreground = Brushes.Red;
                    System.Windows.MessageBox.Show("Could not find the game window. Make sure the game launched.", "Calibration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dialog = new TeamCalibrationDialog(() => _launchService.FindGameWindowPublic(false)) { Owner = Application.Current.MainWindow };
                dialog.ShowDialog();

                if (dialog.WasCalibrated)
                {
                    var settings = _config.LoadSettings();
                    var key = $"{dialog.CapturedWidth}x{dialog.CapturedHeight}";
                    settings.TeamCalibrations[key] = new TeamCalibrationData
                    {
                        AxisX = dialog.AxisX,
                        AxisY = dialog.AxisY,
                        AlliesX = dialog.AlliesX,
                        AlliesY = dialog.AlliesY
                    };
                    _config.SaveSettings(settings);
                    UpdateCalibrationStatus(settings);

                    System.Windows.MessageBox.Show("Calibration complete! Team join will use these values.", "Calibration", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    CalibrationStatusText.Text = "Cancelled";
                    CalibrationStatusText.Foreground = Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                CalibrationStatusText.Text = "Error";
                CalibrationStatusText.Foreground = Brushes.Red;
            }
        }

        private async void CheckNowBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatusText.Text = "Checking...";
                UpdateStatusText.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#8a9a5b"));

                var status = await _updateService.ForceCheckAsync();
                SetUpdateTimeStatus(status);
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
                    UpdateStatusText.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#8a9a5b"));
                }
                else if (status.Contains("available", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatusText.Text = $"{status}";
                    UpdateStatusText.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#d4a017"));
                }
                else if (status.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                         status.Contains("already", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStatusText.Text = status;
                    UpdateStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    UpdateStatusText.Text = "Check failed";
                    UpdateStatusText.Foreground = System.Windows.Media.Brushes.Red;
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
