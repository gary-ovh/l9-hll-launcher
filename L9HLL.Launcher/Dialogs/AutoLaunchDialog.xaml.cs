using System;
using System.Windows;
using System.Windows.Threading;

namespace L9HLL.Launcher.Dialogs
{
    public partial class AutoLaunchDialog : Window
    {
        private readonly DispatcherTimer _timer;
        private int _remaining = 30;
        public bool WasCancelled { get; private set; }

        public AutoLaunchDialog(string serverName)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;

            ServerNameText.Text = serverName;
            CountDownText.Text = $"{_remaining}s";

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                _remaining--;
                CountDownText.Text = $"{_remaining}s";
                if (_remaining <= 0)
                {
                    _timer.Stop();
                    WasCancelled = false;
                    Close();
                }
            };

            Loaded += (s, e) => _timer.Start();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            WasCancelled = true;
            Close();
        }
    }
}