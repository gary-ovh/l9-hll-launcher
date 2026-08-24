using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using L9HLL.Launcher.Dialogs;
using L9HLL.Launcher.Models;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ServerQueryService _queryService = new();
        private readonly ConfigService _configService = new();
        private readonly LaunchService _launchService = new();
        private DiscordService? _discordService;
        private Timer? _refreshTimer;
        private readonly Dispatcher _dispatcher;
        private AutoLaunchService? _autoLaunchService;
        private UpdateService? _updateService;

        public ConfigService ConfigService => _configService;
        public ServerQueryService QueryService => _queryService;
        public LaunchService LaunchService => _launchService;
        public UpdateService? UpdateService => _updateService;
        public AutoLaunchService? AutoLaunchService => _autoLaunchService;

        public bool StartupOnBoot
        {
            get => _configService.LoadSettings().StartupOnBoot;
            set { var s = _configService.LoadSettings(); s.StartupOnBoot = value; _configService.SaveSettings(s); OnPropertyChanged(); }
        }

        public bool StartMinimized
        {
            get => _configService.LoadSettings().StartMinimized;
            set { var s = _configService.LoadSettings(); s.StartMinimized = value; _configService.SaveSettings(s); OnPropertyChanged(); }
        }

        public bool CheckUpdates
        {
            get => _configService.LoadSettings().CheckUpdates;
            set { var s = _configService.LoadSettings(); s.CheckUpdates = value; _configService.SaveSettings(s); OnPropertyChanged(); }
        }

        public bool AutoLaunchEnabled
        {
            get => _autoLaunchService?.Enabled ?? false;
            set
            {
                if (_autoLaunchService != null)
                {
                    _autoLaunchService.Enabled = value;
                    _autoLaunchService.ResetTrigger();
                }
                OnPropertyChanged();
            }
        }

        public string AutoLaunchTime
        {
            get
            {
                if (_autoLaunchService == null || !_autoLaunchService.Enabled) return "Not set";
                return _autoLaunchService.ScheduledTime.ToString(@"hh\:mm");
            }
        }

        public string AutoLaunchButtonText
        {
            get
            {
                if (_autoLaunchService == null) return "Auto-Launch: Off";
                if (!_autoLaunchService.Enabled) return "Auto-Launch: Off";
                return $"Auto-Launch: {_autoLaunchService.ScheduledTime:hh\\:mm}";
            }
        }

        public ICommand ToggleAutoLaunchCommand { get; }
        public ICommand ToggleSettingsCommand { get; }

        private string _selectedGame = "all";
        public string SelectedGame
        {
            get => _selectedGame;
            set { _selectedGame = value; OnPropertyChanged(); FilterServers(); }
        }

        public string[] GameOptions { get; } = { "All", "WW2", "Vietnam" };

        private List<ServerInfo> _serverInfos = new();
        private List<ServerStatus> _allServers = new();
        public ObservableCollection<ServerStatus> Servers { get; } = new();
        public ICommand LaunchCommand { get; }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }
        private string _statusText = "Loading...";

        public MainViewModel()
        {
            _dispatcher = Application.Current.Dispatcher;
            _discordService = new DiscordService();
            LaunchCommand = new RelayCommand<ServerStatus>(OnLaunch);
            ToggleAutoLaunchCommand = new RelayCommand<object>(OnToggleAutoLaunch);
            ToggleSettingsCommand = new RelayCommand<object>(OnToggleSettings);

            SyncStartupSetting();

            _autoLaunchService = new AutoLaunchService(
                _launchService, _queryService, _configService,
                s => _dispatcher.Invoke(() => StatusText = s));

            _updateService = new UpdateService(_configService, s => _dispatcher.Invoke(() => StatusText = s));
            _updateService.UpdateAvailable += OnUpdateAvailable;

            LoadAndRefresh();
            _refreshTimer = new Timer(OnRefresh, null, 10000, 10000);
        }

        public void Dispose()
        {
            _refreshTimer?.Dispose();
            _autoLaunchService?.Dispose();
            _updateService?.Dispose();
        }

        private void SyncStartupSetting()
        {
            var settings = _configService.LoadSettings();
            if (StartupService.IsInStartup() && !settings.StartupOnBoot)
                StartupService.RemoveFromStartup();
            else if (!StartupService.IsInStartup() && settings.StartupOnBoot)
                StartupService.AddToStartup();
        }

        private void OnUpdateAvailable(string version, string downloadUrl)
        {
            _dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"Version {version} is available!\n\nUpdate now?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    UpdateService.DownloadAndUpdate(downloadUrl);
            });
        }

        private void OnToggleSettings(object? parameter)
        {
            try
            {
                var dialog = new SettingsDialog(
                    _configService,
                    _updateService!,
                    _autoLaunchService!,
                    () =>
                    {
                        OnPropertyChanged(nameof(StartupOnBoot));
                        OnPropertyChanged(nameof(StartMinimized));
                        OnPropertyChanged(nameof(CheckUpdates));
                        OnPropertyChanged(nameof(AutoLaunchEnabled));
                        OnPropertyChanged(nameof(AutoLaunchTime));
                        OnPropertyChanged(nameof(AutoLaunchButtonText));
                    });

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnToggleAutoLaunch(object? parameter)
        {
            try
            {
                if (_autoLaunchService == null) return;

                if (_autoLaunchService.Enabled)
                {
                    var result = MessageBox.Show(
                        $"Auto-launch is currently set for {_autoLaunchService.ScheduledTime:hh\\:mm}.\n\nClick OK to change time, or Cancel to disable.",
                        "Auto-Launch",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel)
                    {
                        _autoLaunchService.Enabled = false;
                        _autoLaunchService.ResetTrigger();
                        OnPropertyChanged(nameof(AutoLaunchButtonText));
                        StatusText = "Auto-Launch disabled";
                        return;
                    }
                }

                var dialog = new TimePickerDialog(
                    (int)_autoLaunchService.ScheduledTime.TotalHours,
                    _autoLaunchService.ScheduledTime.Minutes);

                if (dialog.ShowDialog() == true)
                {
                    _autoLaunchService.ScheduledTime = new TimeSpan(dialog.SelectedHour, dialog.SelectedMinute, 0);
                    _autoLaunchService.Enabled = true;
                    _autoLaunchService.ResetTrigger();
                    OnPropertyChanged(nameof(AutoLaunchButtonText));
                    StatusText = $"Auto-Launch enabled for {_autoLaunchService.ScheduledTime:hh\\:mm}";
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAndRefresh()
        {
            var serverInfos = _configService.LoadServers();
            StatusText = $"Loaded {serverInfos.Count} server(s)";

            if (serverInfos.Count == 0)
            {
                StatusText = "No servers found in config!";
                return;
            }

            _serverInfos.Clear();
            _allServers.Clear();
            foreach (var server in serverInfos)
            {
                _serverInfos.Add(server);
                _allServers.Add(new ServerStatus
                {
                    Name = server.Name,
                    Ip = server.Ip,
                    Port = server.Port,
                    Game = server.Game
                });
            }

            FilterServers();
            Task.Run(RefreshAll);
        }

        private void FilterServers()
        {
            _dispatcher.Invoke(() =>
            {
                Servers.Clear();
                var game = _selectedGame.ToLower();
                var internalGame = game == "ww2" ? "hll" : game;
                var filtered = _allServers.Where(s =>
                    game == "all" ||
                    (internalGame == "hll" && s.Game == "hll") ||
                    (internalGame == "vietnam" && s.Game == "vietnam")
                ).ToList();

                foreach (var server in filtered)
                    Servers.Add(server);
            });
        }

        private async void RefreshAll()
        {
            try
            {
                StatusText = "Querying servers...";

                var tasks = _serverInfos.Select(s => _queryService.QueryAsync(s));
                var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                _dispatcher.BeginInvoke((Action)(() =>
                {
                    var onlineCount = 0;

                    for (int i = 0; i < _allServers.Count && i < results.Length; i++)
                    {
                        var result = results[i].status;
                        var original = _allServers[i];

                        original.IsOnline = result.IsOnline;
                        original.PlayerCount = result.PlayerCount;
                        original.MaxPlayers = result.MaxPlayers;
                        original.Map = result.Map;

                        if (result.IsOnline)
                            onlineCount++;
                    }

                    StatusText = $"{DateTime.Now:HH:mm:ss} | {onlineCount}/{results.Length} online";
                }));
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                StatusText = $"Error: {ex.Message}";
            }
        }

        private void OnLaunch(ServerStatus? server)
        {
            if (server == null) return;
            _discordService?.SetConnectingPresence(server);
            StatusText = $"Connecting to {server.Name}...";
            _launchService.LaunchServer(server);
        }

        private void OnRefresh(object? state)
        {
            Task.Run(RefreshAll);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;

        public RelayCommand(Action<T?> execute) => _execute = execute;

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute((T?)parameter);
        public event EventHandler? CanExecuteChanged;
    }
}