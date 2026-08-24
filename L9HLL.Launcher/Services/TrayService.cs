using System;
using System.Drawing;
using System.IO;
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
        private ToolStripMenuItem? _serversHeader;
        private ToolStripSeparator? _serversSeparator;
        private bool _isRefreshing;

        public TrayService(MainWindow mainWindow, MainViewModel viewModel, LaunchService launchService, ServerQueryService queryService)
        {
            _mainWindow = mainWindow;
            _viewModel = viewModel;
            _launchService = launchService;
            _queryService = queryService;

            _contextMenu = new ContextMenuStrip();

            _serversHeader = new ToolStripMenuItem("Servers");
            _serversHeader.Font = new Font(_serversHeader.Font, System.Drawing.FontStyle.Bold);
            _serversHeader.Enabled = false;
            _contextMenu.Items.Add(_serversHeader);

            _serversSeparator = new ToolStripSeparator();
            _contextMenu.Items.Add(_serversSeparator);

            _contextMenu.Items.Add("-");
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
                var iconPath = Path.Combine(exeDir, "L9HLL.Launcher.exe");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = Icon.ExtractAssociatedIcon(iconPath);
                }
            }
            catch { }

            _notifyIcon.DoubleClick += (s, e) => Restore();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _refreshTimer.Tick += OnRefreshTick;
            _refreshTimer.Start();

            BuildServerMenu();
        }

        private void OnRefreshTick(object? sender, EventArgs e)
        {
            BuildServerMenu();
        }

        private void BuildServerMenu()
        {
            if (_isRefreshing) return;

            var existingItems = _contextMenu.Items;
            int startIdx = 2;
            int count = 0;
            while (startIdx + count < existingItems.Count && existingItems[startIdx + count] is not ToolStripSeparator)
                count++;

            for (int i = 0; i < count; i++)
                existingItems.RemoveAt(startIdx);

            var servers = _viewModel.Servers;
            foreach (var server in servers)
            {
                var status = server.IsOnline ? $"({server.PlayerCount}/{server.MaxPlayers})" : "(Offline)";
                var mapInfo = string.IsNullOrEmpty(server.Map) ? "" : $" - {server.Map}";
                var item = new ToolStripMenuItem($"{server.Name} {status}{mapInfo}");

                if (server.IsOnline)
                {
                    item.Click += async (s, e) => await OnQuickLaunch(server, (ToolStripMenuItem)s!);
                }
                else
                {
                    item.Enabled = false;
                    item.ForeColor = Color.FromArgb(100, 100, 100);
                }

                existingItems.Insert(startIdx, item);
            }
        }

        private async Task OnQuickLaunch(ServerStatus server, ToolStripMenuItem item)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;

            item.Enabled = false;
            item.Text = $"[L9] {server.Name} (Querying...)";

            try
            {
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
                    item.Text = $"[L9] {server.Name} ({status.PlayerCount}/{status.MaxPlayers})";
                    item.Enabled = true;
                    _launchService.LaunchServer(server);
                }
                else
                {
                    item.Text = $"[L9] {server.Name} (Offline)";
                    item.Enabled = false;
                    item.ForeColor = Color.FromArgb(100, 100, 100);
                }
            }
            catch
            {
                item.Text = $"[L9] {server.Name} (Query Failed)";
                item.Enabled = false;
                item.ForeColor = Color.FromArgb(100, 100, 100);
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        public void Restore()
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        public void Exit()
        {
            _notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
        }
    }
}