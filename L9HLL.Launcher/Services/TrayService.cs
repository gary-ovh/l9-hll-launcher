using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Icon = System.Drawing.Icon;

namespace L9HLL.Launcher.Services
{
    public class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly MainWindow _mainWindow;

        public TrayService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;

            _contextMenu = new ContextMenuStrip();
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
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
        }
    }
}