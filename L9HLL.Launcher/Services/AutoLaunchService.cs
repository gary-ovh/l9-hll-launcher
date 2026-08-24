using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
        private readonly LaunchService _launchService;
        private readonly Action<string> _onStatus;
        private readonly Dispatcher _dispatcher;
        private bool _enabled;
        private TimeSpan _scheduledTime;
        private bool _triggeredToday;
        private DateTime _lastDate;
        private int _server1PlayerCount = -1;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                SaveConfig();
            }
        }

        public TimeSpan ScheduledTime
        {
            get => _scheduledTime;
            set
            {
                _scheduledTime = value;
                SaveConfig();
            }
        }

        public AutoLaunchService(LaunchService launchService, Action<string> onStatus)
        {
            _launchService = launchService;
            _onStatus = onStatus;
            _dispatcher = System.Windows.Application.Current.Dispatcher;
            _scheduledTime = new TimeSpan(22, 0, 0);
            _lastDate = DateTime.Today.AddDays(-1);

            LoadConfig();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        public void ResetTrigger()
        {
            _triggeredToday = false;
            _lastDate = DateTime.Today.AddDays(-1);
        }

        public void UpdateServer1PlayerCount(int count)
        {
            _server1PlayerCount = count;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (!_enabled || _triggeredToday) return;

            var now = DateTime.Now;
            if (now.Date != _lastDate)
            {
                _triggeredToday = false;
                _lastDate = now.Date;
            }

            var currentTime = now.TimeOfDay;
            var target = _scheduledTime;

            if (currentTime >= target && currentTime < target + TimeSpan.FromSeconds(30))
            {
                _triggeredToday = true;
                CheckAndLaunch();
            }
        }

        private void CheckAndLaunch()
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

            var server = ResolveServer();

            _dispatcher.Invoke(() =>
            {
                var dialog = new AutoLaunchDialog(server.Name);
                dialog.ShowDialog();

                if (!dialog.WasCancelled)
                {
                    UpdateStatus($"Auto-launching {server.Name}...");
                    _launchService.LaunchServer(server);
                }
                else
                {
                    UpdateStatus("Auto-Launch cancelled");
                }
            });
        }

        private ServerStatus ResolveServer()
        {
            if (_server1PlayerCount >= 0 && _server1PlayerCount >= 60)
            {
                return new ServerStatus
                {
                    Name = "[L9] The Loyal Nine |#2|",
                    Ip = "40.27.41.9",
                    Port = 7777,
                    Game = "hll"
                };
            }

            return new ServerStatus
            {
                Name = "[L9] The Loyal Nine |#1|",
                Ip = "40.27.41.16",
                Port = 7777,
                Game = "hll"
            };
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
            catch { }

            return false;
        }

        private void UpdateStatus(string message)
        {
            _onStatus?.Invoke(message);
        }

        private void LoadConfig()
        {
            try
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var configPath = Path.Combine(exeDir, "Config", "autolaunch.json");

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AutoLaunchConfig>(json);
                    if (config != null)
                    {
                        _enabled = config.Enabled;
                        _scheduledTime = config.ScheduledTime;
                    }
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var configDir = Path.Combine(exeDir, "Config");
                Directory.CreateDirectory(configDir);
                var configPath = Path.Combine(configDir, "autolaunch.json");

                var config = new AutoLaunchConfig { Enabled = _enabled, ScheduledTime = _scheduledTime };
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch { }
        }

        public void Dispose()
        {
            _timer.Stop();
        }

        private class AutoLaunchConfig
        {
            public bool Enabled { get; set; }
            public TimeSpan ScheduledTime { get; set; }
        }
    }
}