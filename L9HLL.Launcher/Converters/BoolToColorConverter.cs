using System;
using System.Windows.Data;
using System.Windows.Media;

namespace L9HLL.Launcher.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public static SolidColorBrush OnlineBrush = new SolidColorBrush(Color.FromRgb(100, 200, 100));
        public static SolidColorBrush OfflineBrush = new SolidColorBrush(Color.FromRgb(200, 80, 80));

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isOnline)
                return isOnline ? OnlineBrush : OfflineBrush;
            return OfflineBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }
}