using System;
using System.Windows;

namespace L9HLL.Launcher.Dialogs
{
    public partial class TimePickerDialog : Window
    {
        public int SelectedHour { get; private set; }
        public int SelectedMinute { get; private set; }
        public bool TimeConfirmed => SelectedHour >= 0 && SelectedMinute >= 0;

        public TimePickerDialog(int defaultHour = 22, int defaultMinute = 0)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;

            for (int i = 0; i < 24; i++)
            {
                HourList.Items.Add(i.ToString("D2"));
            }
            for (int i = 0; i < 60; i++)
            {
                MinuteList.Items.Add(i.ToString("D2"));
            }

            SelectedHour = defaultHour;
            SelectedMinute = defaultMinute;
            HourList.SelectedItem = defaultHour.ToString("D2");
            MinuteList.SelectedItem = defaultMinute.ToString("D2");
            UpdateDisplay();

            HourList.SelectionChanged += (s, e) => { SelectedHour = HourList.SelectedIndex; UpdateDisplay(); };
            MinuteList.SelectionChanged += (s, e) => { SelectedMinute = MinuteList.SelectedIndex; UpdateDisplay(); };
        }

        private void UpdateDisplay()
        {
            if (HourList.SelectedIndex >= 0 && MinuteList.SelectedIndex >= 0)
            {
                SelectedTimeText.Text = $"{HourList.SelectedIndex:D2}:{MinuteList.SelectedIndex:D2}";
            }
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            if (HourList.SelectedIndex >= 0 && MinuteList.SelectedIndex >= 0)
            {
                SelectedHour = HourList.SelectedIndex;
                SelectedMinute = MinuteList.SelectedIndex;
                DialogResult = true;
                Close();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}