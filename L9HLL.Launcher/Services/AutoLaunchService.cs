using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using L9HLL.Launcher.Dialogs;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class AutoLaunchService : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _monitorTimer;
        private readonly LaunchService _launchService;
        private readonly ServerQueryService _queryService;
        private readonly ConfigService _configService;
        private readonly Action<string> _onStatus;
        private readonly Dispatcher _dispatcher;
        private bool _triggeredToday;
        private DateTime _lastDate;
        private ServerInfo? _launchedServer;
        private bool _seededDialogShown;
        private AutoLaunchDialog? _pendingDialog;

        public bool Enabled
        {
            get => _configService.LoadSettings().AutoLaunchEnabled;
            set
            {
                var settings = _configService.LoadSettings();
                settings.AutoLaunchEnabled = value;
                _configService.SaveSettings(settings);
            }
        }

        public TimeSpan ScheduledTime
        {
            get
            {
                var settings = _configService.LoadSettings();
                return TimeSpan.TryParse(settings.AutoLaunchTime, out var t) ? t : new TimeSpan(22, 0, 0);
            }
            set
            {
                var settings = _configService.LoadSettings();
                settings.AutoLaunchTime = value.ToString(@"hh\:mm");
                _configService.SaveSettings(settings);
            }
        }

        public AutoLaunchService(
            LaunchService launchService,
            ServerQueryService queryService,
            ConfigService configService,
            Action<string> onStatus)
        {
            _launchService = launchService;
            _queryService = queryService;
            _configService = configService;
            _onStatus = onStatus;
            _dispatcher = Application.Current.Dispatcher;
            _lastDate = DateTime.Today.AddDays(-1);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        public void ResetTrigger()
        {
            _triggeredToday = false;
            _lastDate = DateTime.Today.AddDays(-1);
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            if (now.Date != _lastDate)
            {
                _triggeredToday = false;
                _lastDate = now.Date;
            }

            if (!Enabled || _triggeredToday) return;

            var currentTime = now.TimeOfDay;
            var target = ScheduledTime;

            if (currentTime >= target && currentTime < target + TimeSpan.FromSeconds(30))
            {
                _triggeredToday = true;
                CheckAndLaunch();
            }
        }

        private async void CheckAndLaunch()
        {
            if (IsGameRunning(true))
            {
                UpdateStatus("Auto-Launch skipped: Vietnam already running");
                return;
            }

            if (IsGameRunning(false))
            {
                UpdateStatus("Auto-Launch skipped: WW2 already running");
                return;
            }

            UpdateStatus("Auto-Launch: querying servers...");

            var server1Info = new ServerInfo
            {
                Name = "[L9] The Loyal Nine |#1|",
                Ip = "40.27.41.16",
                Port = 7777,
                Game = "hll"
            };
            var server2Info = new ServerInfo
            {
                Name = "[L9] The Loyal Nine |#2|",
                Ip = "40.27.41.9",
                Port = 7777,
                Game = "hll"
            };

            var (r1, _) = await _queryService.QueryAsync(server1Info);
            var (r2, _) = await _queryService.QueryAsync(server2Info);

            ServerStatus server;

            if (r1.IsOnline && r1.PlayerCount < 60)
            {
                server = r1;
            }
            else if (r2.IsOnline)
            {
                server = r2;
            }
            else
            {
                server = new ServerStatus
                {
                    Name = "[L9] The Loyal Nine |#1|",
                    Ip = "40.27.41.16",
                    Port = 7777,
                    Game = "hll"
                };
            }

            var dialog = new AutoLaunchDialog(server.Name);
            _pendingDialog = dialog;
            dialog.Closed += (s, e) =>
            {
                _pendingDialog = null;
            {
                if (!dialog.WasCancelled)
                {
                    UpdateStatus($"Auto-launching {server.Name}...");
                    _launchedServer = new ServerInfo
                    {
                        Name = server.Name,
                        Ip = server.Ip,
                        Port = server.Port,
                        Game = server.Game
                    };
                    _seededDialogShown = false;
                    StartSeedingMonitor();
                    _launchService.LaunchServer(server);
                }
                else
                {
                    UpdateStatus("Auto-Launch cancelled");
                }
            };
            dialog.Show();
        }

        private bool IsGameRunning(bool isVietnam)
        {
            try
            {
                string[] processNames = isVietnam
                    ? new[] { "HLLVietnam-Win64-Shipping", "HLLVietnam" }
                    : new[] { "HLL-Win64-Shipping", "HLL" };

                foreach (var name in processNames)
                {
                    if (Process.GetProcessesByName(name).Length > 0)
                        return true;
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }

            return false;
        }

        private void UpdateStatus(string message)
        {
            _onStatus?.Invoke(message);
        }

        private void StartSeedingMonitor()
        {
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _monitorTimer.Tick += OnMonitorTick;
            _monitorTimer.Start();
        }

        private async void OnMonitorTick(object? sender, EventArgs e)
        {
            if (_launchedServer == null || _seededDialogShown) return;

            try
            {
                var (status, _) = await _queryService.QueryAsync(_launchedServer);
                if (status.PlayerCount >= 90)
                {
                    _monitorTimer.Stop();
                    _seededDialogShown = true;
                    UpdateStatus($"Server seeded! {status.PlayerCount}/{status.MaxPlayers} players");

                    var dialog = new ServerSeededDialog();
                    dialog.Closed += (s, e) =>
                    {
                        if (!dialog.WasCancelled)
                        {
                            UpdateStatus("Seeding timer expired. Closing game.");
                            _launchService.CloseGame();
                        }
                        else
                        {
                            UpdateStatus("Player chose to keep playing.");
                        }
                    };
                    dialog.Show();
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _monitorTimer?.Stop();
        }
    }
}