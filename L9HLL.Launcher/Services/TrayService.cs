using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Icon = System.Drawing.Icon;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly MainWindow _mainWindow;
        private readonly MainViewModel _viewModel;
        private readonly LaunchService _launchService;
        private readonly ServerQueryService _queryService;
        private readonly DispatcherTimer _refreshTimer;
        private readonly Dictionary<string, ToolStripMenuItem> _serverMenuItems = new();
        private bool _isRefreshing;

        public TrayService(
            MainWindow mainWindow,
            MainViewModel viewModel,
            LaunchService launchService,
            ServerQueryService queryService)
        {
            _mainWindow = mainWindow;
            _viewModel = viewModel;
            _launchService = launchService;
            _queryService = queryService;

            _contextMenu = new ContextMenuStrip();

            var serversHeader = new ToolStripMenuItem("Servers");
            serversHeader.Font = new Font(serversHeader.Font, System.Drawing.FontStyle.Bold);
            serversHeader.Enabled = false;
            _contextMenu.Items.Add(serversHeader);

            var serversSeparator = new ToolStripSeparator();
            _contextMenu.Items.Add(serversSeparator);

            _contextMenu.Items.Add("-");
            _contextMenu.Items.Add("Settings", null, (s, e) =>
            {
                _contextMenu.Close();
                Restore();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(300);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => _viewModel.ToggleSettingsCommand.Execute(null));
                });
            });
            _contextMenu.Items.Add("Restore", null, (s, e) => Restore());
            _contextMenu.Items.Add("Exit", null, (s, e) => Exit());

            _notifyIcon = new NotifyIcon
            {
                Text = "[L9] HLL Launcher",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            try
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var exePath = Path.Combine(exeDir, "L9HLL.Launcher.exe");
                if (File.Exists(exePath))
                {
                    _notifyIcon.Icon = Icon.ExtractAssociatedIcon(exePath);
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }

            _notifyIcon.DoubleClick += (s, e) => Restore();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _refreshTimer.Tick += OnRefreshTick;
            _refreshTimer.Start();

            BuildServerMenu();
        }

        private void OnRefreshTick(object? sender, EventArgs e)
        {
            try
            {
                BuildServerMenu();
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        private async void OnServerItemClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && _serverMenuItems.Values.Contains(item))
            {
                var serverName = _serverMenuItems.Keys.FirstOrDefault(k => _serverMenuItems[k] == item);
                if (serverName != null)
                {
                    var server = _viewModel.Servers.FirstOrDefault(s => s.Name == serverName);
                    if (server != null)
                    {
                        _contextMenu.Close();
                        await OnQuickLaunch(server);
                    }
                }
            }
        }

        private void BuildServerMenu()
        {
            if (_isRefreshing) return;

            try
            {
                var servers = _viewModel.Servers;
                var currentKeys = new HashSet<string>();

                foreach (var server in servers)
                {
                    currentKeys.Add(server.Name);
                    var status = server.IsOnline ? $"({server.PlayerCount}/{server.MaxPlayers})" : "(Offline)";
                    var mapInfo = string.IsNullOrEmpty(server.Map) ? "" : $" - {server.Map}";
                    var text = $"{server.Name} {status}{mapInfo}";

                    if (_serverMenuItems.TryGetValue(server.Name, out var existingItem))
                    {
                        existingItem.Text = text;
                        if (server.IsOnline)
                        {
                            existingItem.Enabled = true;
                            existingItem.ForeColor = Color.Empty;
                            existingItem.Click -= OnServerItemClick;
                            existingItem.Click += OnServerItemClick;
                        }
                        else
                        {
                            existingItem.Enabled = false;
                            existingItem.ForeColor = Color.FromArgb(100, 100, 100);
                            existingItem.Click -= OnServerItemClick;
                        }
                    }
                    else
                    {
                        var item = new ToolStripMenuItem(text);
                        if (server.IsOnline)
                        {
                            item.Click += OnServerItemClick;
                        }
                        else
                        {
                            item.Enabled = false;
                            item.ForeColor = Color.FromArgb(100, 100, 100);
                        }
                        _serverMenuItems[server.Name] = item;
                        _contextMenu.Items.Insert(2, item);
                    }
                }

                var staleKeys = _serverMenuItems.Keys.ToList();
                foreach (var name in staleKeys)
                {
                    if (!currentKeys.Contains(name))
                    {
                        _contextMenu.Items.Remove(_serverMenuItems[name]);
                        _serverMenuItems[name].Dispose();
                        _serverMenuItems.Remove(name);
                    }
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        private async Task OnQuickLaunch(ServerStatus server)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;

            var item = _serverMenuItems.TryGetValue(server.Name, out var existing) ? existing : null;

            try
            {
                // Menu already closed, safe to access directly
                if (item != null)
                {
                    item.Text = $"[L9] {server.Name} (Querying...)";
                    item.Enabled = false;
                    item.Click -= OnServerItemClick;
                }

                var serverInfo = new ServerInfo
                {
                    Name = server.Name,
                    Ip = server.Ip,
                    Port = server.Port,
                    Game = server.Game
                };

                var result = await Task.Run(() => _queryService.QueryAsync(serverInfo));
                var status = result.Item1;

                if (status.IsOnline)
                {
                    if (item != null)
                    {
                        item.Text = $"[L9] {server.Name} ({status.PlayerCount}/{status.MaxPlayers})";
                        item.Enabled = true;
                        item.ForeColor = Color.Empty;
                        item.Click += OnServerItemClick;
                    }
                    _launchService.LaunchServer(server);
                }
                else
                {
                    if (item != null)
                    {
                        item.Text = $"[L9] {server.Name} (Offline)";
                        item.Enabled = false;
                        item.ForeColor = Color.FromArgb(100, 100, 100);
                    }
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                if (item != null)
                {
                    item.Text = $"[L9] {server.Name} (Query Failed)";
                    item.Enabled = false;
                    item.ForeColor = Color.FromArgb(100, 100, 100);
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        public void Restore()
        {
            try
            {
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                    _mainWindow.BringIntoView();
                });
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        public void Exit()
        {
            try
            {
                _notifyIcon.Visible = false;
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
            System.Windows.Application.Current.Shutdown();
        }

        public void Dispose()
        {
            try
            {
                _refreshTimer.Stop();
            }
            catch { }
            try
            {
                _notifyIcon?.Dispose();
            }
            catch { }
            try
            {
                _contextMenu?.Dispose();
            }
            catch { }
        }
    }
}