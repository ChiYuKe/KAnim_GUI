using System.Windows;
using System.Windows.Input;

namespace KAnimGui.Windows
{
    public partial class OniResourceBridgeSettingsWindow : Window
    {
        public OniResourceBridgeSettingsWindow()
        {
            InitializeComponent();

            string layout = OniResourceBridgeWindow.GetKAnimExportLayout();
            GroupedLayoutRadioButton.IsChecked = layout == OniResourceBridgeWindow.KAnimExportLayoutGrouped;
            SplitLayoutRadioButton.IsChecked = layout == OniResourceBridgeWindow.KAnimExportLayoutSplit;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            KAnimGui.Properties.Default.OniKAnimExportLayout = SplitLayoutRadioButton.IsChecked == true
                ? OniResourceBridgeWindow.KAnimExportLayoutSplit
                : OniResourceBridgeWindow.KAnimExportLayoutGrouped;
            KAnimGui.Properties.Default.Save();
            DialogResult = true;
            Close();
        }
    }
}
