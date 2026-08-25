using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

            HourList.SelectionChanged += (s, e) => { SelectedHour = HourList.SelectedIndex; };
            MinuteList.SelectionChanged += (s, e) => { SelectedMinute = MinuteList.SelectedIndex; };
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is ListBoxItem || source is Button)
                    return;
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }
            DragMove();
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