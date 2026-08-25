using System;
using System.Windows;
using System.Windows.Threading;

namespace L9HLL.Launcher.Dialogs
{
    public partial class ServerSeededDialog : Window
    {
        private readonly DispatcherTimer _timer;
        private int _remaining = 60;
        public bool WasCancelled { get; private set; }

        public ServerSeededDialog()
        {
            InitializeComponent();

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

        private void KeepPlayingBtn_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            WasCancelled = true;
            Close();
        }
    }
}