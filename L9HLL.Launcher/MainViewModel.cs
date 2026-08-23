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
using L9HLL.Launcher.Models;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ServerQueryService _queryService = new();
        private readonly ConfigService _configService = new();
        private readonly LaunchService _launchService = new();
        private DiscordService? _discordService;
        private Timer? _refreshTimer;
        private readonly Dispatcher _dispatcher;

        private string _selectedGame = "all";
        public string SelectedGame
        {
            get => _selectedGame;
            set
            {
                _selectedGame = value;
                OnPropertyChanged();
                FilterServers();
            }
        }

        public string[] GameOptions { get; } = { "All", "HLL", "Vietnam" };

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
            LoadAndRefresh();
            _refreshTimer = new Timer(OnRefresh, null, 10000, 10000);
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

            _allServers.Clear();
            foreach (var server in serverInfos)
            {
                var status = new ServerStatus
                {
                    Name = server.Name,
                    Ip = server.Ip,
                    Port = server.Port,
                    Game = server.Game
                };
                _allServers.Add(status);
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
                var filtered = _allServers.Where(s =>
                    game == "all" ||
                    (game == "hll" && s.Game == "hll") ||
                    (game == "vietnam" && s.Game == "vietnam")
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

                var serverInfos = _allServers.Select(s => new ServerInfo
                {
                    Name = s.Name,
                    Ip = s.Ip,
                    Port = s.Port,
                    Game = s.Game
                }).ToList();

                var tasks = serverInfos.Select(s => _queryService.QueryAsync(s));
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