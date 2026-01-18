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
using KAnimGui.Windows;
using System.Net.Http;


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
            AppSettings.ApplyAll();


            kanimLog = new LogManager(LogTextBox, StatusText);
            scmlLog = new LogManager(ScmlLogTextBox, StatusText);

            var ksePath = KseLocator.FindExecutable();
            if (string.IsNullOrEmpty(ksePath))
            {
                kanimLog.Log("警告：未找到 kanimal-cli.exe。请确保它位于程序目录或在设置中手动指定路径。", true);
                StatusText.Text = "状态：缺少核心组件";
            }

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




        private void OnDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (MainTabControl.SelectedItem == ScmlToKanimTab)
            {
                HandleScmlDroppedFiles(files);
            }
            else
            {
                HandleKanimDroppedFiles(files);
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



        /// <summary>
        /// 处理拖入的 Kanim 相关文件（PNG, Anim, Build）
        /// </summary>
        /// <param name="files">文件路径数组</param>
        private void HandleKanimDroppedFiles(string[] files)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string ext = Path.GetExtension(file).ToLowerInvariant();

                // 1. 处理 PNG
                if (ext == ".png")
                {
                    PngPathTextBox.Text = file;
                    kanimLog.Log($"已拖入: {fileName}", false);
                    continue;
                }

                // 2. 处理 TXT 权限校验
                bool isTxt = ext == ".txt";
                if (isTxt && !AppSettings.EnableTxtToBytes)
                {
                    kanimLog.Log($"拖入txt文件被禁止，请在设置中启用.txt支持: {fileName}", true);
                    continue;
                }

                // 3. 处理 Anim 和 Build 文件 (支持 .txt 和 .bytes)
                if (file.EndsWith("_anim.bytes", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith("_anim.txt", StringComparison.OrdinalIgnoreCase))
                {
                    AnimPathTextBox.Text = file;
                    string msg = isTxt ? $"已拖入 .txt 文件，等待转换: {fileName}" : $"已拖入: {fileName}";
                    kanimLog.Log(msg, false);
                }
                else if (file.EndsWith("_build.bytes", StringComparison.OrdinalIgnoreCase) ||
                         file.EndsWith("_build.txt", StringComparison.OrdinalIgnoreCase))
                {
                    BuildPathTextBox.Text = file;
                    string msg = isTxt ? $"已拖入 .txt 文件，等待转换: {fileName}" : $"已拖入: {fileName}";
                    kanimLog.Log(msg, false);
                }
                else
                {
                    // 如果是 txt 但不是上述两种，或者是完全不支持的后缀
                    string errorMsg = isTxt ? $"不支持的txt文件格式: {fileName}" : $"不支持的文件类型: {fileName}";
                    kanimLog.Log(errorMsg, true);
                }
            }
        }

        public void NotifySettingsChanged()
        {
            string ksePath = KseLocator.FindExecutable(); // 自动获取当前生效的路径

            if (!string.IsNullOrEmpty(ksePath) && File.Exists(ksePath))
            {
                kanimLog.Log($"[配置] 已启用核心组件: {Path.GetFileName(ksePath)}", false);
                StatusText.Text = "状态：就绪";
            }
            else
            {
                kanimLog.Log("[配置] 警告：当前设置的路径下未找到 kanimal-cli.exe！", true);
                StatusText.Text = "状态：缺少核心组件";
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
            // 1. 验证输入（包括检查 kanimal-cli.exe 是否存在）
            if (!ValidateKanimInputs()) return;

            // 2. 更新 UI 状态为忙碌
            SetUiState(true);

            try
            {
                // 3. 处理 AnimPath 文件格式（.txt -> .bytes）
                string animPath = AnimPathTextBox.Text;
                if (Path.GetExtension(animPath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    bool success = RenameTxtToBytes(animPath);
                    if (!success)
                    {
                        kanimLog.Log("转换 .txt 为 .bytes 失败 (AnimPath)", true);
                        SetUiState(false);
                        return;
                    }
                    animPath = Path.ChangeExtension(animPath, ".bytes");
                    AnimPathTextBox.Text = animPath; // 更新界面显示
                }

                // 4. 处理 BuildPath 文件格式（.txt -> .bytes）
                string buildPath = BuildPathTextBox.Text;
                if (Path.GetExtension(buildPath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    bool success = RenameTxtToBytes(buildPath);
                    if (!success)
                    {
                        kanimLog.Log("转换 .txt 为 .bytes 失败 (BuildPath)", true);
                        SetUiState(false);
                        return;
                    }
                    buildPath = Path.ChangeExtension(buildPath, ".bytes");
                    BuildPathTextBox.Text = buildPath; // 更新界面显示
                }

                // 5. 初始化转换器
                var converter = new KanimConverter
                {
                    PngPath = PngPathTextBox.Text,
                    AnimPath = animPath,
                    BuildPath = buildPath,
                    OutputDir = OutputDirTextBox.Text,
                    StrictOrder = StrictOrderCheckBox.IsChecked == true,
                    StrictMode = StrictModeCheckBox.IsChecked == true
                };

                // 6. 执行异步转换过程
                var result = await converter.ConvertAsync(kanimLog.Log);

                // 7. 恢复 UI 状态
                SetUiState(false);

                // 8. 根据结果显示反馈
                if (result.Success)
                {
                    // 成功提示
                    if (!AppSettings.NoSuccessPopup)
                    {
                        var msgBox = new CustomMessageBox("转换成功！", "成功", PackIconKind.Information);
                        msgBox.Owner = this;
                        msgBox.ShowDialog();
                    }

                    // 自动打开输出目录
                    TryOpenFolder(converter.ActualOutputDir);
                }
                else
                {
                    // 失败提示：显示具体的错误信息（例如：找不到 kanimal-cli.exe）
                    string errorDetail = string.IsNullOrEmpty(result.ErrorMessage) ? "未知原因" : result.ErrorMessage;
                    var msgBox = new CustomMessageBox($"转换失败：{errorDetail}", "失败", PackIconKind.CloseCircle);
                    msgBox.Owner = this;
                    msgBox.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                // 捕获意外异常
                SetUiState(false);
                var msgBox = new CustomMessageBox($"程序运行异常：{ex.Message}", "错误", PackIconKind.AlertCircle);
                msgBox.Owner = this;
                msgBox.ShowDialog();
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
                    var msgBox = new CustomMessageBox("SCML转换成功！", "成功", PackIconKind.Information);
                    msgBox.Owner = this;
                    msgBox.ShowDialog();
                   
                }
                TryOpenFolder(converter.ActualOutputDir);
            }

            else
            {
                var msgBox = new CustomMessageBox($"{result.ErrorMessage}", "失败", PackIconKind.CloseCircle);
                msgBox.Owner = this;
                msgBox.ShowDialog();
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
            // 检查核心组件是否存在
            if (string.IsNullOrEmpty(KseLocator.FindExecutable()))
            {
                // 直接使用你的 CustomMessageBox 提示缺失核心组件
                var msgBox = new CustomMessageBox("未找到核心组件 kanimal-cli.exe。\n\n 请将其放入程序目录，或在设置中手动指定路径。", "缺失核心组件", PackIconKind.CloseCircle);
                msgBox.Owner = this;
                msgBox.ShowDialog();
                return false;
            }

            // 检查输入框是否为空
            if (string.IsNullOrWhiteSpace(PngPathTextBox.Text) ||
                string.IsNullOrWhiteSpace(AnimPathTextBox.Text) ||
                string.IsNullOrWhiteSpace(BuildPathTextBox.Text))
            {
                var msgBox = new CustomMessageBox("请确保 PNG、Anim 和 Build 文件路径都已正确填写。", "提示", PackIconKind.Information);
                msgBox.Owner = this;
                msgBox.ShowDialog();
                return false;
            }

            if (string.IsNullOrWhiteSpace(OutputDirTextBox.Text))
            {
                var msgBox = new CustomMessageBox("请选择输出目录。", "提示", PackIconKind.FolderAlert);
                msgBox.Owner = this;
                msgBox.ShowDialog();
                return false;
            }

            return true;
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
            var msgBox = new CustomMessageBox("当前版本为：1.0.2 \n具体可以前往git仓库了解", "帮助", PackIconKind.Information);
            msgBox.Owner = this;
            msgBox.ShowDialog();
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
        // git地址
        private void GithubButton_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://github.com/ChiYuKe/KAnim_GUI/tree/master";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开网页: " + ex.Message);
            }

        }

       



        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            // var settings = new CustomMessageBox();
            var settings = new KAnimRenderWindow();
           // settings.Owner = this; // 设置所属主窗口
            settings.Show(); // 非模态打开

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
