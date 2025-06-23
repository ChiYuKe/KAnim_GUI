using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KAnimGui.Models;


namespace KAnimGui
{
    /// <summary>
    /// SettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
           
            // 读取保存的配置，初始化控件状态

            this.Loaded += SettingsWindow_Loaded;


            // 初始化勾选框和文本框状态
            EnableFeatureCheckBox.IsChecked = AppSettings.OpenFolderAfterConvert;
            NoSuccessPopupCheckBox.IsChecked = AppSettings.NoSuccessPopup;
            EnableTxtToBytesCheckBox.IsChecked = AppSettings.EnableTxtToBytes;
            UseCustomKsePathCheckBox.IsChecked = AppSettings.UseCustomKsePath;
            CustomKsePathTextBox.Text = AppSettings.CustomKsePath ?? "";

            // 绑定事件，状态变化实时保存
            EnableFeatureCheckBox.Checked += EnableFeatureCheckBox_CheckedChanged;
            EnableFeatureCheckBox.Unchecked += EnableFeatureCheckBox_CheckedChanged;

            NoSuccessPopupCheckBox.Checked += NoSuccessPopupCheckBox_CheckedChanged;
            NoSuccessPopupCheckBox.Unchecked += NoSuccessPopupCheckBox_CheckedChanged;

            EnableTxtToBytesCheckBox.Checked += EnableTxtToBytesCheckBox_CheckedChanged;
            EnableTxtToBytesCheckBox.Unchecked += EnableTxtToBytesCheckBox_CheckedChanged;

            UseCustomKsePathCheckBox.Checked += UseCustomKsePathCheckBox_CheckedChanged;
            UseCustomKsePathCheckBox.Unchecked += UseCustomKsePathCheckBox_CheckedChanged;

            CustomKsePathTextBox.TextChanged += CustomKsePathTextBox_TextChanged;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }

        // 读取保存的配置并赋值给控件
        private void LoadSettings()
        {
            EnableFeatureCheckBox.IsChecked = Properties.Default.OpenFolderAfterConvert;
            NoSuccessPopupCheckBox.IsChecked = Properties.Default.NoSuccessPopup;
            EnableTxtToBytesCheckBox.IsChecked = Properties.Default.EnableTxtToBytes;
            UseCustomKsePathCheckBox.IsChecked = Properties.Default.UseCustomKsePath;
            CustomKsePathTextBox.Text = Properties.Default.CustomKsePath ?? "";
        }

        private void EnableFeatureCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AppSettings.OpenFolderAfterConvert = EnableFeatureCheckBox.IsChecked == true;
        }

        private void NoSuccessPopupCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AppSettings.NoSuccessPopup = NoSuccessPopupCheckBox.IsChecked == true;
        }

        private void EnableTxtToBytesCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AppSettings.EnableTxtToBytes = EnableTxtToBytesCheckBox.IsChecked == true;
        }

        private void UseCustomKsePathCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            AppSettings.UseCustomKsePath = UseCustomKsePathCheckBox.IsChecked == true;
        }
        private void CustomKsePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.CustomKsePath = CustomKsePathTextBox.Text;
        }


        private void UpdateSettings()
        {
            Properties.Default.OpenFolderAfterConvert = EnableFeatureCheckBox.IsChecked == true;
            Properties.Default.NoSuccessPopup = NoSuccessPopupCheckBox.IsChecked == true;
            Properties.Default.EnableTxtToBytes = EnableTxtToBytesCheckBox.IsChecked == true;
            Properties.Default.UseCustomKsePath = UseCustomKsePathCheckBox.IsChecked == true;
            Properties.Default.CustomKsePath = CustomKsePathTextBox.Text;
            Properties.Default.Save();
        }


        // 让窗口可以通过鼠标拖动移动
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // 只最小化当前窗口（SettingsWindow）
            this.WindowState = WindowState.Minimized;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击时关闭窗口
                this.Close();
            }
        }


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // 关闭设置窗口
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSettings();
            this.Close(); // 保存后关闭窗口
        }

        private void BrowseKseButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                CustomKsePathTextBox.Text = dlg.FileName;
            }
        }

    }
}
