using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace L9HLL.Launcher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void RootBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is Button || source is ComboBox || source is ScrollViewer ||
                    source is TextBlock)
                {
                    return;
                }
                source = VisualTreeHelper.GetParent(source);
            }
            DragMove();
        }
    }
}