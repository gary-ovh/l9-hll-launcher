using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using L9HLL.Launcher.Models;
using L9HLL.Launcher.Services;

namespace L9HLL.Launcher
{
    public class MainViewModel
    {
        private readonly ServerQueryService _queryService = new();
        private readonly ConfigService _configService = new();
        private readonly LaunchService _launchService = new();
        private DiscordService? _discordService;
        private Timer? _refreshTimer;

        public ObservableCollection<ServerStatus> Servers { get; } = new();
        public ICommand LaunchCommand { get; }
        public string StatusText { get; set; } = "Loading servers...";

        public MainViewModel()
        {
            _discordService = new DiscordService();
            LaunchCommand = new RelayCommand<ServerStatus>(OnLaunch);
            LoadAndRefresh();
            _refreshTimer = new Timer(OnRefresh, null, 15000, 15000);
        }

        private void LoadAndRefresh()
        {
            var servers = _configService.LoadServers();
            foreach (var server in servers)
            {
                Servers.Add(new ServerStatus
                {
                    Name = server.Name,
                    Ip = server.Ip,
                    Port = server.Port
                });
            }
            Task.Run(RefreshAll);
        }

        private async void RefreshAll()
        {
            var tasks = Servers.Select(s => _queryService.QueryAsync(new ServerInfo
            {
                Name = s.Name,
                Ip = s.Ip,
                Port = s.Port
            }));

            var results = await Task.WhenAll(tasks);

            foreach (var (result, original) in results.Zip(Servers, (r, o) => (r, o)))
            {
                original.IsOnline = result.IsOnline;
                original.PlayerCount = result.PlayerCount;
                original.MaxPlayers = result.MaxPlayers;
                original.Map = result.Map;
                original.Ping = result.Ping;
            }

            StatusText = $"Updated {DateTime.Now:HH:mm:ss}";
        }

        private void OnLaunch(ServerStatus? server)
        {
            if (server == null) return;

            _discordService?.SetConnectingPresence(server);
            StatusText = $"Launching {server.Name}...";
            _launchService.LaunchServer(server);
        }

        private void OnRefresh(object? state)
        {
            Task.Run(RefreshAll);
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