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
                        }
                        else
                        {
                            existingItem.Enabled = false;
                            existingItem.ForeColor = Color.FromArgb(100, 100, 100);
                        }
                    }
                    else
                    {
                        var item = new ToolStripMenuItem(text);
                        if (server.IsOnline)
                        {
                            item.Click += async (s, ev) => await OnQuickLaunch(server, (ToolStripMenuItem)s!);
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

        private async Task OnQuickLaunch(ServerStatus server, ToolStripMenuItem item)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;

            try
            {
                SetItemText(item, $"[L9] {server.Name} (Querying...)");
                item.Enabled = false;

                var serverInfo = new ServerInfo
                {
                    Name = server.Name,
                    Ip = server.Ip,
                    Port = server.Port,
                    Game = server.Game
                };

                var (status, _) = await _queryService.QueryAsync(serverInfo);

                if (status.IsOnline)
                {
                    SetItemText(item, $"[L9] {server.Name} ({status.PlayerCount}/{status.MaxPlayers})");
                    item.Enabled = true;
                    _launchService.LaunchServer(server);
                }
                else
                {
                    SetItemText(item, $"[L9] {server.Name} (Offline)");
                    item.Enabled = false;
                    item.ForeColor = Color.FromArgb(100, 100, 100);
                }
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
                try
                {
                    SetItemText(item, $"[L9] {server.Name} (Query Failed)");
                    item.Enabled = false;
                    item.ForeColor = Color.FromArgb(100, 100, 100);
                }
                catch { }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void SetItemText(ToolStripMenuItem item, string text)
        {
            try
            {
                _contextMenu.Invoke(new Action(() => item.Text = text));
            }
            catch (Exception ex)
            {
                ConfigService.LogError(ex);
            }
        }

        public void Restore()
        {
            try
            {
                if (_mainWindow.IsLoaded)
                {
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }
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