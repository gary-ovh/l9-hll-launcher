using System;
using System.Threading.Tasks;
using System.Windows;

namespace L9HLL.Launcher.Dialogs
{
    public partial class UpdateProgressDialog : Window
    {
        public bool Cancelled { get; set; }

        public UpdateProgressDialog()
        {
            InitializeComponent();
        }

        public void SetStatus(string text)
        {
            Dispatcher.BeginInvoke(new Action(() => StatusText.Text = text));
        }

        public void SetProgress(int percent)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Progress.Value = percent;
                PercentText.Text = $"{percent}%";
            }));
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Cancelled = true;
            CancelBtn.IsEnabled = false;
            Close();
        }

        public void CloseWithoutCancel()
        {
            Dispatcher.Invoke(() => Close());
        }
    }
}