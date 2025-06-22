using KAnimGui.Core;
using KAnimGui.Models;
using KAnimGui.Utils;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf;
using MaterialDesignThemes.Wpf;


namespace KAnimGui
{
    public partial class MainWindow : Window
    {
        private LogManager kanimLog;
        private LogManager scmlLog;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            kanimLog = new LogManager(LogTextBox, StatusText);
            scmlLog = new LogManager(ScmlLogTextBox, StatusText);

            SetDefaultOutputDirectory();
            LogTextBox.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            LogTextBox.FontSize = 12;

            this.PreviewDragOver += OnDragOver;
            this.PreviewDrop += OnDrop;
        }

        #region 拖放逻辑

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                bool allowDrop;

                if (MainTabControl.SelectedItem == ScmlToKanimTab)
                {
                    allowDrop = IsScml(files);
                }
                else
                {
                    allowDrop = IsKanim(files);
                }

                e.Effects = allowDrop ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }




        private async void OnDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (MainTabControl.SelectedItem == ScmlToKanimTab)
            {
                HandleScmlDroppedFiles(files);
            }
            else
            {
                await HandleKanimDroppedFiles(files);
            }
        }



        private void HandleScmlDroppedFiles(string[] files)
        {
            var scml = files.FirstOrDefault(f => f.EndsWith(".scml"));
            if (scml != null)
            {
                ScmlPathTextBox.Text = scml;
                scmlLog.Log($"拖放SCML文件: {Path.GetFileName(scml)}");
            }
            else scmlLog.Log("拖放的不是有效的SCML文件", true);
        }
        // 处理拖入的Kanim文件
        private async Task HandleKanimDroppedFiles(string[] files)
        {
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                var fileName = Path.GetFileName(file);

                if (ext == ".txt")
                {
                    if (!AppSettings.EnableTxtToBytes)
                    {
                        kanimLog.Log($"拖入txt文件被禁止，请在设置中启用.txt支持: {fileName}", true);
                        continue;
                    }

                    if (file.EndsWith("_anim.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        AnimPathTextBox.Text = file;
                        kanimLog.Log($"已拖入 .txt 文件，等待转换: {fileName}", false);
                    }
                    else if (file.EndsWith("_build.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        BuildPathTextBox.Text = file;
                        kanimLog.Log($"已拖入 .txt 文件，等待转换: {fileName}", false);
                    }
                    else
                    {
                        kanimLog.Log($"不支持的txt文件格式: {fileName}", true);
                    }
                }
                else if (ext == ".png")
                {
                    PngPathTextBox.Text = file;
                    kanimLog.Log($"已拖入: {fileName}", false);
                }
                else if (file.EndsWith("_anim.bytes", StringComparison.OrdinalIgnoreCase))
                {
                    AnimPathTextBox.Text = file;
                    kanimLog.Log($"已拖入: {fileName}", false);
                }
                else if (file.EndsWith("_build.bytes", StringComparison.OrdinalIgnoreCase))
                {
                    BuildPathTextBox.Text = file;
                    kanimLog.Log($"已拖入: {fileName}", false);
                }
                else
                {
                    kanimLog.Log($"不支持的文件类型: {fileName}", true);
                }
            }
        }



        #endregion

        #region 浏览按钮

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;

            switch (btn.Name)
            {
                case "BrowsePngButton":
                    SelectSingleFile("PNG 文件|*.png", PngPathTextBox);
                    break;
                case "BrowseAnimButton":
                    SelectSingleFile("Anim 文件|*_anim.bytes", AnimPathTextBox);
                    break;
                case "BrowseBuildButton":
                    SelectSingleFile("Build 文件|*_build.bytes", BuildPathTextBox);
                    break;
                case "BrowseScmlButton":
                    SelectSingleFile("SCML 文件|*.scml", ScmlPathTextBox);
                    break;
                case "BrowseOutputDirButton":
                    SelectFolderPath(OutputDirTextBox);
                    break;
                case "BrowseScmlOutputDirButton":
                    SelectFolderPath(ScmlOutputDirTextBox);
                    break;
            }
        }


        #endregion

        #region 转换逻辑入口

        private async void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateKanimInputs()) return;

            SetUiState(true);

            // 处理 AnimPath
            string animPath = AnimPathTextBox.Text;
            if (Path.GetExtension(animPath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                bool success = RenameTxtToBytes(animPath);
                if (!success)
                {
                    scmlLog.Log("转换 .txt 为 .bytes 失败 (AnimPath)", true);
                    SetUiState(false);
                    return;
                }
                animPath = Path.ChangeExtension(animPath, ".bytes");
                AnimPathTextBox.Text = animPath;
            }

            // 处理 BuildPath
            string buildPath = BuildPathTextBox.Text;
            if (Path.GetExtension(buildPath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                bool success = RenameTxtToBytes(buildPath);
                if (!success)
                {
                    scmlLog.Log("转换 .txt 为 .bytes 失败 (BuildPath)", true);
                    SetUiState(false);
                    return;
                }
                buildPath = Path.ChangeExtension(buildPath, ".bytes");
                BuildPathTextBox.Text = buildPath;
            }

            var converter = new KanimConverter
            {
                PngPath = PngPathTextBox.Text,
                AnimPath = animPath,
                BuildPath = buildPath,
                OutputDir = OutputDirTextBox.Text,
                StrictOrder = StrictOrderCheckBox.IsChecked == true,
                StrictMode = StrictModeCheckBox.IsChecked == true
            };

            var result = await converter.ConvertAsync(kanimLog.Log);

            SetUiState(false);

            if (result.Success)
            {
                if (!AppSettings.NoSuccessPopup)
                {
                    MessageBox.Show("转换成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
               
                TryOpenFolder(converter.ActualOutputDir);
            }
            else
            {
                MessageBox.Show(result.ErrorMessage, "失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }





        private async void ConvertScmlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateScmlInputs()) return;

            SetScmlUiState(true);
            var converter = new ScmlConverter
            {
                ScmlPath = ScmlPathTextBox.Text,
                OutputDir = ScmlOutputDirTextBox.Text,
                Interpolate = InterpolateCheckBox.IsChecked == true,
                Debone = DeboneCheckBox.IsChecked == true
            };

            var result = await converter.ConvertAsync(scmlLog.Log);
            SetScmlUiState(false);

            if (result.Success)
            {
                if (!AppSettings.NoSuccessPopup)
                {
                     MessageBox.Show("SCML转换成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                TryOpenFolder(converter.ActualOutputDir);
            }

            else
            {
                MessageBox.Show(result.ErrorMessage, "失败", MessageBoxButton.OK, MessageBoxImage.Error);

            }
               
        }

     

        #endregion

        #region 状态管理与验证

        private void SetUiState(bool isBusy)
        {
            ProgressBar.IsIndeterminate = isBusy;
            StatusText.Text = isBusy ? "转换中..." : "就绪";

            var names = new[] { BrowsePngButton, BrowseAnimButton, BrowseBuildButton,
                BrowseOutputDirButton, ConvertButton, ClearButton };

            foreach (var btn in names) btn.IsEnabled = !isBusy;
        }

        private void SetScmlUiState(bool isBusy)
        {
            ProgressBar.IsIndeterminate = isBusy;
            StatusText.Text = isBusy ? "SCML转换中..." : "就绪";

            var names = new[] { BrowseScmlButton, BrowseScmlOutputDirButton, ConvertScmlButton, ScmlClearButton };
            foreach (var btn in names) btn.IsEnabled = !isBusy;
        }

        private void TryOpenFolder(string folderPath)
        {
            if (AppSettings.OpenFolderAfterConvert)
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开文件夹失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool IsScml(string[] files) =>
         files.Any(f => f.EndsWith(".scml", StringComparison.OrdinalIgnoreCase));


        // 判断是否是Kanim相关文件，控制拖放效果
        private bool IsKanim(string[] files)
        {
            bool allowTxt = AppSettings.EnableTxtToBytes;

            return files.Any(f =>
                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith("_anim.bytes", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith("_build.bytes", StringComparison.OrdinalIgnoreCase)
                || (allowTxt && f.EndsWith("_anim.txt", StringComparison.OrdinalIgnoreCase))
                || (allowTxt && f.EndsWith("_build.txt", StringComparison.OrdinalIgnoreCase))
            );
        }


        // 简单的改后缀函数（复制文件）
        private bool RenameTxtToBytes(string txtFilePath)
        {
            try
            {
                if (!File.Exists(txtFilePath)) return false;

                var bytesFilePath = Path.ChangeExtension(txtFilePath, ".bytes");

                if (File.Exists(bytesFilePath))
                    File.Delete(bytesFilePath);

                File.Copy(txtFilePath, bytesFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }


        private bool ValidateKanimInputs()
        {
            if (string.IsNullOrWhiteSpace(PngPathTextBox.Text)) return Show("缺少PNG文件");
            if (string.IsNullOrWhiteSpace(AnimPathTextBox.Text)) return Show("缺少Anim文件");
            if (string.IsNullOrWhiteSpace(BuildPathTextBox.Text)) return Show("缺少Build文件");
            if (string.IsNullOrWhiteSpace(OutputDirTextBox.Text)) return Show("缺少输出目录");
            return true;

            bool Show(string msg)
            {
                MessageBox.Show(msg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool ValidateScmlInputs()
        {
            if (string.IsNullOrWhiteSpace(ScmlPathTextBox.Text)) return Show("缺少SCML文件");
            if (string.IsNullOrWhiteSpace(ScmlOutputDirTextBox.Text)) return Show("缺少输出目录");
            return true;

            bool Show(string msg)
            {
                MessageBox.Show(msg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void SetDefaultOutputDirectory()
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KSE_Output");
            OutputDirTextBox.Text = Path.Combine(baseDir, "KSE_Scml");
            ScmlOutputDirTextBox.Text = Path.Combine(baseDir, "KSE_Kanim");

            Directory.CreateDirectory(OutputDirTextBox.Text);
            Directory.CreateDirectory(ScmlOutputDirTextBox.Text);
        }

        private void SelectSingleFile(string filter, TextBox targetTextBox)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = filter
            };

            if (dialog.ShowDialog() == true)
            {
                targetTextBox.Text = dialog.FileName;
            }
        }

        private void SelectFolderPath(System.Windows.Controls.TextBox targetBox)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "请选择一个文件夹",
                UseDescriptionForTitle = true, // 使用上面的描述作为窗口标题
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == true)
            {
                targetBox.Text = dialog.SelectedPath;
            }   
        }





        #endregion

        #region 其他按钮

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("版本: 1.0.0\n什么？你居然点了帮助？", "帮助", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow();
            settings.Owner = this; // 设置所属主窗口
            settings.Show(); // 非模态打开

        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {

            this.Topmost = !this.Topmost;

            // 可选：动态更新图标和提示
            var icon = this.Topmost ? "PinOff" : "Pin";
            var tooltip = this.Topmost ? "取消置顶" : "窗口置顶";

            PinButton.Content = new PackIcon { Kind = (PackIconKind)Enum.Parse(typeof(PackIconKind), icon) };
            PinButton.ToolTip = tooltip;

        }



        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            PngPathTextBox.Clear();
            AnimPathTextBox.Clear();
            BuildPathTextBox.Clear();
            LogTextBox.Clear();
            StatusText.Text = "已重置";
        }

        private void ScmlClearButton_Click(object sender, RoutedEventArgs e)
        {
            ScmlPathTextBox.Clear();
            ScmlOutputDirTextBox.Clear();
            ScmlLogTextBox.Clear();
        }


        #endregion

      
    }
}
