using KAnimGui.Core;
using KAnimGui.Models;
using KAnimGui.Utils;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf;
using MaterialDesignThemes.Wpf;
using KAnimGui.Windows;


namespace KAnimGui
{
    public partial class MainWindow : Window
    {
        private LogManager kanimLog = null!;
        private LogManager scmlLog = null!;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            AppSettings.ApplyAll();


            kanimLog = new LogManager(LogTextBox, StatusText, "KanimToScml");
            scmlLog = new LogManager(ScmlLogTextBox, StatusText, "ScmlToKanim");

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
            if (TryGetDroppedFiles(e, out var files))
            {
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
            if (!TryGetDroppedFiles(e, out var files))
            {
                return;
            }

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
            var scml = files.FirstOrDefault(f => f.EndsWith(".scml", StringComparison.OrdinalIgnoreCase));
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
            string? ksePath = KseLocator.FindExecutable(); // 自动获取当前生效的路径

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
                    SelectSingleFile(GetKanimFileFilter("Anim 文件", "_anim"), AnimPathTextBox);
                    break;
                case "BrowseBuildButton":
                    SelectSingleFile(GetKanimFileFilter("Build 文件", "_build"), BuildPathTextBox);
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

            try
            {
                string? animPath = EnsureBytesFile(AnimPathTextBox.Text, "AnimPath");
                if (animPath == null) return;

                string? buildPath = EnsureBytesFile(BuildPathTextBox.Text, "BuildPath");
                if (buildPath == null) return;

                AnimPathTextBox.Text = animPath;
                BuildPathTextBox.Text = buildPath;

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

                if (result.Success)
                {
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
                var msgBox = new CustomMessageBox($"程序运行异常：{ex.Message}", "错误", PackIconKind.AlertCircle);
                msgBox.Owner = this;
                msgBox.ShowDialog();
            }
            finally
            {
                SetUiState(false);
            }
        }







        private async void ConvertScmlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateScmlInputs()) return;

            SetScmlUiState(true);
            try
            {
                var converter = new ScmlConverter
                {
                    ScmlPath = ScmlPathTextBox.Text,
                    OutputDir = ScmlOutputDirTextBox.Text,
                    Interpolate = InterpolateCheckBox.IsChecked == true,
                    Debone = DeboneCheckBox.IsChecked == true
                };

                var result = await converter.ConvertAsync(scmlLog.Log);

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
                    var msgBox = new CustomMessageBox(result.ErrorMessage ?? "未知原因", "失败", PackIconKind.CloseCircle);
                    msgBox.Owner = this;
                    msgBox.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                var msgBox = new CustomMessageBox($"程序运行异常：{ex.Message}", "错误", PackIconKind.AlertCircle);
                msgBox.Owner = this;
                msgBox.ShowDialog();
            }
            finally
            {
                SetScmlUiState(false);
            }
        }

        private async void BatchConvertButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "请选择包含 PNG、_anim.bytes、_build.bytes 的文件夹",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (!EnsureOutputDirectoryReady(OutputDirTextBox.Text))
            {
                return;
            }

            var fileSets = FindKanimFileSets(dialog.SelectedPath).ToList();
            if (fileSets.Count == 0)
            {
                ShowMessage("没有找到可转换的 KAnim 文件组。", "批量转换", PackIconKind.Information);
                return;
            }

            SetUiState(true);

            try
            {
                int successCount = 0;
                kanimLog.Log($"开始批量转换，共 {fileSets.Count} 组文件。");

                foreach (var fileSet in fileSets)
                {
                    kanimLog.Log($"批量转换: {fileSet.Name}");
                    string? animPath = EnsureBytesFile(fileSet.AnimPath, $"{fileSet.Name} Anim");
                    string? buildPath = EnsureBytesFile(fileSet.BuildPath, $"{fileSet.Name} Build");

                    if (animPath == null || buildPath == null)
                    {
                        kanimLog.Log($"{fileSet.Name} 转换失败：无法准备 .bytes 文件", true);
                        continue;
                    }

                    var converter = new KanimConverter
                    {
                        PngPath = fileSet.PngPath,
                        AnimPath = animPath,
                        BuildPath = buildPath,
                        OutputDir = OutputDirTextBox.Text,
                        StrictOrder = StrictOrderCheckBox.IsChecked == true,
                        StrictMode = StrictModeCheckBox.IsChecked == true
                    };

                    var result = await converter.ConvertAsync(kanimLog.Log);
                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        kanimLog.Log($"{fileSet.Name} 转换失败：{result.ErrorMessage ?? "未知原因"}", true);
                    }
                }

                ShowMessage($"批量转换完成：成功 {successCount} / {fileSets.Count}", "批量转换", PackIconKind.Information);
            }
            finally
            {
                SetUiState(false);
            }
        }

        private async void BatchConvertScmlButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "SCML 文件|*.scml"
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            {
                return;
            }

            if (!EnsureOutputDirectoryReady(ScmlOutputDirTextBox.Text))
            {
                return;
            }

            SetScmlUiState(true);

            try
            {
                int successCount = 0;
                scmlLog.Log($"开始批量转换，共 {dialog.FileNames.Length} 个 SCML 文件。");

                foreach (var scmlPath in dialog.FileNames)
                {
                    scmlLog.Log($"批量转换: {Path.GetFileName(scmlPath)}");

                    var converter = new ScmlConverter
                    {
                        ScmlPath = scmlPath,
                        OutputDir = ScmlOutputDirTextBox.Text,
                        Interpolate = InterpolateCheckBox.IsChecked == true,
                        Debone = DeboneCheckBox.IsChecked == true
                    };

                    var result = await converter.ConvertAsync(scmlLog.Log);
                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        scmlLog.Log($"{Path.GetFileName(scmlPath)} 转换失败：{result.ErrorMessage ?? "未知原因"}", true);
                    }
                }

                ShowMessage($"批量转换完成：成功 {successCount} / {dialog.FileNames.Length}", "批量转换", PackIconKind.Information);
            }
            finally
            {
                SetScmlUiState(false);
            }
        }

     

        #endregion

        #region 状态管理与验证

        private void SetUiState(bool isBusy)
        {
            ProgressBar.IsIndeterminate = isBusy;
            StatusText.Text = isBusy ? "转换中..." : "就绪";

            var names = new[] { BrowsePngButton, BrowseAnimButton, BrowseBuildButton,
                BrowseOutputDirButton, ConvertButton, ClearButton, BatchConvertButton };

            foreach (var btn in names) btn.IsEnabled = !isBusy;
        }

        private void SetScmlUiState(bool isBusy)
        {
            ProgressBar.IsIndeterminate = isBusy;
            StatusText.Text = isBusy ? "SCML转换中..." : "就绪";

            var names = new[] { BrowseScmlButton, BrowseScmlOutputDirButton, ConvertScmlButton, ScmlClearButton,
                BatchConvertScmlButton };
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

        private static bool TryGetDroppedFiles(DragEventArgs e, out string[] files)
        {
            files = Array.Empty<string>();

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return false;
            }

            files = e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
            return files.Length > 0;
        }


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

        private string? EnsureBytesFile(string path, string label)
        {
            if (!Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            if (!RenameTxtToBytes(path))
            {
                kanimLog.Log($"转换 .txt 为 .bytes 失败 ({label})", true);
                return null;
            }

            return Path.ChangeExtension(path, ".bytes");
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
                ShowMessage("请确保 PNG、Anim 和 Build 文件路径都已正确填写。", "提示", PackIconKind.Information);
                return false;
            }

            if (!ValidateKanimFileSet(PngPathTextBox.Text, AnimPathTextBox.Text, BuildPathTextBox.Text))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(OutputDirTextBox.Text))
            {
                ShowMessage("请选择输出目录。", "提示", PackIconKind.FolderAlert);
                return false;
            }

            return EnsureOutputDirectoryReady(OutputDirTextBox.Text);
        }

        private bool ValidateScmlInputs()
        {
            if (string.IsNullOrWhiteSpace(ScmlPathTextBox.Text)) return Show("缺少SCML文件");
            if (string.IsNullOrWhiteSpace(ScmlOutputDirTextBox.Text)) return Show("缺少输出目录");
            if (!ValidateExistingFile(ScmlPathTextBox.Text, ".scml", "SCML文件")) return false;
            return EnsureOutputDirectoryReady(ScmlOutputDirTextBox.Text);

            bool Show(string msg)
            {
                ShowMessage(msg, "错误", PackIconKind.CloseCircle);
                return false;
            }
        }

        private bool ValidateKanimFileSet(string pngPath, string animPath, string buildPath)
        {
            if (!ValidateExistingFile(pngPath, ".png", "PNG文件")) return false;

            var kanimExtensions = AppSettings.EnableTxtToBytes ? new[] { ".bytes", ".txt" } : new[] { ".bytes" };
            if (!ValidateExistingFile(animPath, kanimExtensions, "Anim文件")) return false;
            if (!ValidateExistingFile(buildPath, kanimExtensions, "Build文件")) return false;

            if (!Path.GetFileNameWithoutExtension(animPath).EndsWith("_anim", StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("Anim文件名应以 _anim 结尾。", "文件名不匹配", PackIconKind.AlertCircle);
                return false;
            }

            if (!Path.GetFileNameWithoutExtension(buildPath).EndsWith("_build", StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("Build文件名应以 _build 结尾。", "文件名不匹配", PackIconKind.AlertCircle);
                return false;
            }

            var pngBase = Path.GetFileNameWithoutExtension(pngPath);
            var animBase = TrimSuffix(Path.GetFileNameWithoutExtension(animPath), "_anim");
            var buildBase = TrimSuffix(Path.GetFileNameWithoutExtension(buildPath), "_build");

            if (!string.Equals(pngBase, animBase, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(animBase, buildBase, StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("PNG、Anim、Build 的基础文件名不一致，请确认是否为同一套资源。", "文件不匹配", PackIconKind.AlertCircle);
                return false;
            }

            return true;
        }

        private bool ValidateExistingFile(string path, string expectedExtension, string displayName) =>
            ValidateExistingFile(path, new[] { expectedExtension }, displayName);

        private bool ValidateExistingFile(string path, string[] expectedExtensions, string displayName)
        {
            if (!File.Exists(path))
            {
                ShowMessage($"{displayName}不存在：{path}", "文件不存在", PackIconKind.CloseCircle);
                return false;
            }

            var extension = Path.GetExtension(path);
            if (!expectedExtensions.Any(ext => extension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            {
                ShowMessage($"{displayName}类型不正确，应为：{string.Join(" / ", expectedExtensions)}", "文件类型错误", PackIconKind.CloseCircle);
                return false;
            }

            return true;
        }

        private bool EnsureOutputDirectoryReady(string outputDir)
        {
            try
            {
                Directory.CreateDirectory(outputDir);
                var probePath = Path.Combine(outputDir, $".write_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probePath, string.Empty);
                File.Delete(probePath);
                return true;
            }
            catch (Exception ex)
            {
                ShowMessage($"输出目录不可写：{ex.Message}", "输出目录错误", PackIconKind.FolderAlert);
                return false;
            }
        }

        private static IEnumerable<KanimFileSet> FindKanimFileSets(string folderPath)
        {
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly).ToList();
            var pngFiles = files
                .Where(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(path => Path.GetFileNameWithoutExtension(path), path => path, StringComparer.OrdinalIgnoreCase);

            var animFiles = files
                .Where(IsAnimFile)
                .GroupBy(path => TrimSuffix(Path.GetFileNameWithoutExtension(path), "_anim"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => ChoosePreferredKanimDataFile(group), StringComparer.OrdinalIgnoreCase);

            var buildFiles = files
                .Where(IsBuildFile)
                .GroupBy(path => TrimSuffix(Path.GetFileNameWithoutExtension(path), "_build"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => ChoosePreferredKanimDataFile(group), StringComparer.OrdinalIgnoreCase);

            foreach (var pair in animFiles.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                var baseName = pair.Key;

                if (buildFiles.TryGetValue(baseName, out var buildPath) &&
                    pngFiles.TryGetValue(baseName, out var pngPath))
                {
                    yield return new KanimFileSet(baseName, pngPath, pair.Value, buildPath);
                }
            }
        }

        private static bool IsAnimFile(string path) =>
            path.EndsWith("_anim.bytes", StringComparison.OrdinalIgnoreCase) ||
            (AppSettings.EnableTxtToBytes && path.EndsWith("_anim.txt", StringComparison.OrdinalIgnoreCase));

        private static bool IsBuildFile(string path) =>
            path.EndsWith("_build.bytes", StringComparison.OrdinalIgnoreCase) ||
            (AppSettings.EnableTxtToBytes && path.EndsWith("_build.txt", StringComparison.OrdinalIgnoreCase));

        private static string ChoosePreferredKanimDataFile(IEnumerable<string> paths)
        {
            return paths
                .OrderBy(path => Path.GetExtension(path).Equals(".bytes", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        private static string GetKanimFileFilter(string title, string suffix)
        {
            return AppSettings.EnableTxtToBytes
                ? $"{title}|*{suffix}.bytes;*{suffix}.txt|Bytes 文件|*{suffix}.bytes|Txt 文件|*{suffix}.txt"
                : $"{title}|*{suffix}.bytes";
        }

        private static string TrimSuffix(string value, string suffix)
        {
            return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(0, value.Length - suffix.Length)
                : value;
        }

        private void ShowMessage(string message, string title, PackIconKind iconKind)
        {
            var msgBox = new CustomMessageBox(message, title, iconKind)
            {
                Owner = this
            };
            msgBox.ShowDialog();
        }

        private sealed record KanimFileSet(string Name, string PngPath, string AnimPath, string BuildPath);

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
