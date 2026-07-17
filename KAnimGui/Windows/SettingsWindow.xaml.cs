using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KAnimGui.Windows;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += SettingsWindow_Loaded;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        EnableFeatureCheckBox.IsChecked = Properties.Default.OpenFolderAfterConvert;
        NoSuccessPopupCheckBox.IsChecked = Properties.Default.NoSuccessPopup;
        EnableTxtToBytesCheckBox.IsChecked = Properties.Default.EnableTxtToBytes;
        UseCustomKsePathCheckBox.IsChecked = Properties.Default.UseCustomKsePath;
        CustomKsePathTextBox.Text = Properties.Default.CustomKsePath ?? string.Empty;
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Close();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Properties.Default.OpenFolderAfterConvert = EnableFeatureCheckBox.IsChecked == true;
        Properties.Default.NoSuccessPopup = NoSuccessPopupCheckBox.IsChecked == true;
        Properties.Default.EnableTxtToBytes = EnableTxtToBytesCheckBox.IsChecked == true;
        Properties.Default.UseCustomKsePath = UseCustomKsePathCheckBox.IsChecked == true;
        Properties.Default.CustomKsePath = CustomKsePathTextBox.Text;
        Properties.Default.Save();
        if (Owner is MainWindow mainWindow)
        {
            mainWindow.NotifySettingsChanged();
        }
        Close();
    }

    private void BrowseKseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            CustomKsePathTextBox.Text = dialog.FileName;
        }
    }
}
