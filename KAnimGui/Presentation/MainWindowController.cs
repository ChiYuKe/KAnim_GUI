using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using KAnimGui.Application.Conversion;
using KAnimGui.Application.Platform;
using KAnimGui.Core;
using KAnimGui.Presentation.Conversion;
using KAnimGui.Presentation.Preview;
using KAnimGui.Windows;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;

namespace KAnimGui.Presentation;

/// <summary>
/// Coordinates the main workbench without putting conversion, navigation, or file
/// workflow policy in the WPF window code-behind.
/// </summary>
public sealed class MainWindowController : IDisposable
{
    private readonly Window owner;
    private readonly FrameworkElement root;
    private readonly IServiceProvider serviceProvider;
    private readonly ConversionWorkspaceViewModel conversionViewModel;
    private readonly IKseExecutableLocator executableLocator;
    private readonly IFileSystemGateway fileSystem;
    private readonly IExternalLauncher externalLauncher;
    private readonly TextBox pngPath;
    private readonly TextBox animPath;
    private readonly TextBox buildPath;
    private readonly TextBox scmlPath;
    private readonly TextBox outputDirectory;
    private readonly TextBox scmlOutputDirectory;
    private readonly TabControl mainTabs;
    private readonly TabItem kanimTab;
    private readonly TabItem scmlTab;
    private readonly TextBox kanimLog;
    private readonly TextBox scmlLog;
    private readonly TextBlock statusText;
    private readonly Button pinButton;

    public MainWindowController(
        Window owner,
        FrameworkElement root,
        IServiceProvider serviceProvider,
        IFileSystemGateway fileSystem,
        IExternalLauncher externalLauncher)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.root = root ?? throw new ArgumentNullException(nameof(root));
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.externalLauncher = externalLauncher ?? throw new ArgumentNullException(nameof(externalLauncher));
        executableLocator = serviceProvider.GetRequiredService<IKseExecutableLocator>();
        conversionViewModel = serviceProvider.GetRequiredService<ConversionWorkspaceViewModel>();

        pngPath = Find<TextBox>("PngPathTextBox");
        animPath = Find<TextBox>("AnimPathTextBox");
        buildPath = Find<TextBox>("BuildPathTextBox");
        scmlPath = Find<TextBox>("ScmlPathTextBox");
        outputDirectory = Find<TextBox>("OutputDirTextBox");
        scmlOutputDirectory = Find<TextBox>("ScmlOutputDirTextBox");
        mainTabs = Find<TabControl>("MainTabControl");
        kanimTab = Find<TabItem>("KanimToScmlTab");
        scmlTab = Find<TabItem>("ScmlToKanimTab");
        kanimLog = Find<TextBox>("LogTextBox");
        scmlLog = Find<TextBox>("ScmlLogTextBox");
        statusText = Find<TextBlock>("StatusText");
        pinButton = Find<Button>("PinButton");

        root.DataContext = conversionViewModel;
        conversionViewModel.KanimLog.CollectionChanged += (_, _) => kanimLog.Text = conversionViewModel.KanimLogText;
        conversionViewModel.ScmlLog.CollectionChanged += (_, _) => scmlLog.Text = conversionViewModel.ScmlLogText;
        conversionViewModel.PropertyChanged += ConversionViewModel_PropertyChanged;
        conversionViewModel.ConversionSucceeded += TryOpenFolder;
        owner.PreviewDragOver += OnDragOver;
        owner.PreviewDrop += OnDrop;
        InitializeApplication();
    }

    public void NotifySettingsChanged()
    {
        conversionViewModel.EnableTxtToBytes = Properties.Default.EnableTxtToBytes;
        string? ksePath = executableLocator.FindExecutable();
        if (!string.IsNullOrEmpty(ksePath) && fileSystem.FileExists(ksePath))
        {
            conversionViewModel.AppendKanimMessage($"[配置] 已启用核心组件: {Path.GetFileName(ksePath)}");
            statusText.Text = "状态：就绪";
        }
        else
        {
            conversionViewModel.AppendKanimMessage("[配置] 未找到 kanimal-cli.exe；普通 KAnim ↔ SCML 转换将使用内置内核，高级 SCML 选项需要配置 kanimal-cli.exe。");
            statusText.Text = "状态：内置内核就绪";
        }
    }

    public void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        switch (button.Name)
        {
            case "BrowsePngButton": SelectSingleFile("PNG 文件|*.png", pngPath); break;
            case "BrowseAnimButton": SelectSingleFile(GetKanimFileFilter("Anim 文件", "_anim"), animPath); break;
            case "BrowseBuildButton": SelectSingleFile(GetKanimFileFilter("Build 文件", "_build"), buildPath); break;
            case "BrowseScmlButton": SelectSingleFile("SCML 文件|*.scml", scmlPath); break;
            case "BrowseOutputDirButton": SelectFolderPath(outputDirectory); break;
            case "BrowseScmlOutputDirButton": SelectFolderPath(scmlOutputDirectory); break;
        }
    }

    public async void BatchConvertButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "请选择包含 PNG、_anim.bytes、_build.bytes 的文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() != true || !EnsureOutputDirectoryReady(outputDirectory.Text))
        {
            return;
        }

        ConversionBatchResult? result = await conversionViewModel.ConvertKanimBatchAsync(dialog.SelectedPath);
        if (result == null)
        {
            ShowMessage(conversionViewModel.KanimStatus, "批量转换", PackIconKind.Information);
            return;
        }

        ShowBatchResult(result, "批量转换");
    }

    public async void BatchConvertScmlButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "SCML 文件|*.scml" };
        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0 ||
            !EnsureOutputDirectoryReady(scmlOutputDirectory.Text))
        {
            return;
        }

        ConversionBatchResult? result = await conversionViewModel.ConvertScmlBatchAsync(dialog.FileNames);
        if (result == null)
        {
            ShowMessage(conversionViewModel.ScmlStatus, "批量转换", PackIconKind.Information);
            return;
        }

        ShowBatchResult(result, "批量转换");
    }

    public void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "未知";
        ShowMessage($"当前版本为：{version} \n具体可以前往git仓库了解", "帮助", PackIconKind.Information);
    }

    public void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow();
        TrySetOwner(settings);
        settings.Show();
    }

    public void GithubButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            externalLauncher.Open("https://github.com/ChiYuKe/KAnim_GUI/tree/master");
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法打开网页: " + ex.Message);
        }
    }

    public void PinButton_Click(object sender, RoutedEventArgs e)
    {
        owner.Topmost = !owner.Topmost;
        string icon = owner.Topmost ? "PinOff" : "Pin";
        pinButton.Content = new PackIcon { Kind = (PackIconKind)Enum.Parse(typeof(PackIconKind), icon) };
        pinButton.ToolTip = owner.Topmost ? "取消置顶" : "窗口置顶";
    }

    public void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var previewWindow = serviceProvider.GetRequiredService<KAnimRenderWindow>();
        TrySetOwner(previewWindow);
        previewWindow.Show();
        if (!TryGetCurrentKanimFileSet(out string png, out string anim, out string build))
        {
            return;
        }

        try
        {
            previewWindow.OpenFiles(png, build, anim);
            conversionViewModel.AppendKanimMessage($"已在预览器打开: {Path.GetFileNameWithoutExtension(png)}");
            statusText.Text = "状态：预览器已加载当前 KAnim";
        }
        catch (Exception ex)
        {
            ShowMessage($"预览器加载失败：{ex.Message}", "预览失败", PackIconKind.AlertCircle);
        }
    }

    public void OniBridgeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOniResourceBridgeModInstalled())
        {
            return;
        }

        var existing = owner.OwnedWindows.OfType<OniResourceBridgeWorkspaceWindow>().FirstOrDefault();
        if (existing != null)
        {
            existing.Activate();
            return;
        }

        var bridgeWindow = serviceProvider.GetRequiredService<OniResourceBridgeWorkspaceWindow>();
        TrySetOwner(bridgeWindow);
        bridgeWindow.Show();
    }

    public void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        pngPath.Clear();
        animPath.Clear();
        buildPath.Clear();
        kanimLog.Clear();
        conversionViewModel.KanimLog.Clear();
        statusText.Text = "已重置";
    }

    public void ScmlClearButton_Click(object sender, RoutedEventArgs e)
    {
        scmlPath.Clear();
        scmlOutputDirectory.Clear();
        scmlLog.Clear();
        conversionViewModel.ScmlLog.Clear();
    }

    public void ImportKanimFileSet(string png, string anim, string build)
    {
        mainTabs.SelectedItem = kanimTab;
        pngPath.Text = png;
        animPath.Text = anim;
        buildPath.Text = build;
        conversionViewModel.AppendKanimMessage($"已从 ONI 资源桥导入: {Path.GetFileNameWithoutExtension(png)}");
        statusText.Text = "状态：已导入 ONI 资源";

        var preview = owner.OwnedWindows.OfType<KAnimRenderWindow>().FirstOrDefault();
        if (preview != null)
        {
            _ = SyncPreviewWindowAsync(preview, png, anim, build);
        }
    }

    public void Dispose()
    {
        owner.PreviewDragOver -= OnDragOver;
        owner.PreviewDrop -= OnDrop;
        conversionViewModel.PropertyChanged -= ConversionViewModel_PropertyChanged;
        conversionViewModel.ConversionSucceeded -= TryOpenFolder;
        conversionViewModel.Dispose();
    }

    private void InitializeApplication()
    {
        Properties.Default.Reload();
        conversionViewModel.EnableTxtToBytes = Properties.Default.EnableTxtToBytes;
        if (string.IsNullOrEmpty(executableLocator.FindExecutable()))
        {
            conversionViewModel.AppendKanimMessage("未找到 kanimal-cli.exe；普通 KAnim ↔ SCML 转换将使用内置内核，高级 SCML 选项需要配置 kanimal-cli.exe。");
            statusText.Text = "状态：内置内核就绪";
        }

        SetDefaultOutputDirectory();
        kanimLog.FontFamily = new System.Windows.Media.FontFamily("Consolas");
        kanimLog.FontSize = 12;
    }

    private void ConversionViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ConversionWorkspaceViewModel.KanimStatus) or nameof(ConversionWorkspaceViewModel.ScmlStatus))
        {
            statusText.Text = args.PropertyName == nameof(ConversionWorkspaceViewModel.KanimStatus)
                ? conversionViewModel.KanimStatus
                : conversionViewModel.ScmlStatus;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedFiles(e, out string[] files) &&
            (mainTabs.SelectedItem == scmlTab ? IsScml(files) : IsKanim(files))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!TryGetDroppedFiles(e, out string[] files))
        {
            return;
        }

        if (mainTabs.SelectedItem == scmlTab)
        {
            string? scml = files.FirstOrDefault(f => f.EndsWith(".scml", StringComparison.OrdinalIgnoreCase));
            if (scml != null)
            {
                scmlPath.Text = scml;
                conversionViewModel.AppendScmlMessage($"拖放SCML文件: {Path.GetFileName(scml)}");
            }
            else
            {
                conversionViewModel.AppendScmlMessage("拖放的不是有效的SCML文件", true);
            }
            return;
        }

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            string extension = Path.GetExtension(file).ToLowerInvariant();
            bool isTxt = extension == ".txt";
            if (extension == ".png")
            {
                pngPath.Text = file;
                conversionViewModel.AppendKanimMessage($"已拖入: {name}");
            }
            else if (isTxt && !Properties.Default.EnableTxtToBytes)
            {
                conversionViewModel.AppendKanimMessage($"拖入txt文件被禁止，请在设置中启用.txt支持: {name}", true);
            }
            else if (file.EndsWith("_anim.bytes", StringComparison.OrdinalIgnoreCase) || file.EndsWith("_anim.txt", StringComparison.OrdinalIgnoreCase))
            {
                animPath.Text = file;
                conversionViewModel.AppendKanimMessage(isTxt ? $"已拖入 .txt 文件，等待转换: {name}" : $"已拖入: {name}");
            }
            else if (file.EndsWith("_build.bytes", StringComparison.OrdinalIgnoreCase) || file.EndsWith("_build.txt", StringComparison.OrdinalIgnoreCase))
            {
                buildPath.Text = file;
                conversionViewModel.AppendKanimMessage(isTxt ? $"已拖入 .txt 文件，等待转换: {name}" : $"已拖入: {name}");
            }
            else
            {
                conversionViewModel.AppendKanimMessage(isTxt ? $"不支持的txt文件格式: {name}" : $"不支持的文件类型: {name}", true);
            }
        }
    }

    private void TryOpenFolder(string folderPath)
    {
        if (!Properties.Default.OpenFolderAfterConvert || string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(folderPath);
            fileSystem.EnsureDirectory(fullPath);
            externalLauncher.Open(fullPath);
        }
        catch
        {
            try
            {
                externalLauncher.Open(Path.GetFullPath(folderPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件夹失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task SyncPreviewWindowAsync(KAnimRenderWindow preview, string png, string anim, string build)
    {
        try
        {
            statusText.Text = "状态：正在同步到预览器";
            await preview.OpenFilesAndPlayAsync(png, build, anim);
            conversionViewModel.AppendKanimMessage("已同步到 KAnim 预览器并开始播放。");
            statusText.Text = "状态：已同步到预览器";
        }
        catch (Exception ex)
        {
            conversionViewModel.AppendKanimMessage($"同步到 KAnim 预览器失败：{ex.Message}", true);
            statusText.Text = "状态：预览器同步失败";
        }
    }

    private bool EnsureOniResourceBridgeModInstalled()
    {
        if (OniResourceBridgeModInstaller.IsInstalled())
        {
            return true;
        }

        if (!OniResourceBridgeModInstaller.CanInstallBundledMod(out string zipPath))
        {
            ShowMessage($"没有在缺氧 Mods 目录中找到 ONI Resource Bridge 模组，也没有找到内置模组包：\n{zipPath}", "缺少 ONI Resource Bridge", PackIconKind.AlertCircle);
            return false;
        }

        var result = MessageBox.Show(
            "没有在缺氧 Mods 目录中找到 ONI Resource Bridge 模组。\n\n" +
            $"是否将内置模组解压到：\n{OniResourceBridgeModInstaller.TargetModDirectory}\n\n" +
            "安装后需要重启缺氧，并在模组列表中启用它。",
            "安装 ONI Resource Bridge 模组",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return false;
        }

        try
        {
            OniResourceBridgeModInstaller.InstallBundledMod();
            ShowMessage($"ONI Resource Bridge 模组已安装到：\n{OniResourceBridgeModInstaller.TargetModDirectory}\n\n请重启缺氧，并在模组列表中启用它。", "安装完成", PackIconKind.CheckCircle);
            return true;
        }
        catch (Exception ex)
        {
            ShowMessage($"安装 ONI Resource Bridge 模组失败：\n{ex.Message}", "安装失败", PackIconKind.AlertCircle);
            return false;
        }
    }

    private void ShowBatchResult(ConversionBatchResult result, string title)
    {
        int successCount = result.Results.Count(item => item.Succeeded);
        ShowMessage(
            result.WasCanceled ? $"批量转换已取消：成功 {successCount} / {result.Results.Count}" : $"批量转换完成：成功 {successCount} / {result.Results.Count}",
            title,
            PackIconKind.Information);
    }

    private bool TryGetCurrentKanimFileSet(out string png, out string anim, out string build)
    {
        png = pngPath.Text?.Trim() ?? string.Empty;
        anim = animPath.Text?.Trim() ?? string.Empty;
        build = buildPath.Text?.Trim() ?? string.Empty;
        return fileSystem.FileExists(png) && fileSystem.FileExists(anim) && fileSystem.FileExists(build);
    }

    private bool EnsureOutputDirectoryReady(string path)
    {
        try
        {
            if (fileSystem.TryEnsureWritableDirectory(path, out string? error))
            {
                return true;
            }
            ShowMessage($"输出目录不可写：{error}", "输出目录错误", PackIconKind.FolderAlert);
        }
        catch (Exception ex)
        {
            ShowMessage($"输出目录不可写：{ex.Message}", "输出目录错误", PackIconKind.FolderAlert);
        }
        return false;
    }

    private void SetDefaultOutputDirectory()
    {
        string baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KSE_Output");
        outputDirectory.Text = Path.Combine(baseDirectory, "KSE_Scml");
        scmlOutputDirectory.Text = Path.Combine(baseDirectory, "KSE_Kanim");
        fileSystem.EnsureDirectory(outputDirectory.Text);
        fileSystem.EnsureDirectory(scmlOutputDirectory.Text);
    }

    private void SelectSingleFile(string filter, TextBox target)
    {
        var dialog = new OpenFileDialog { Multiselect = false, Filter = filter };
        if (dialog.ShowDialog() == true)
        {
            target.Text = dialog.FileName;
        }
    }

    private void SelectFolderPath(TextBox target)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "请选择一个文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == true)
        {
            target.Text = dialog.SelectedPath;
        }
    }

    private void ShowMessage(string message, string title, PackIconKind iconKind)
    {
        var messageBox = new CustomMessageBox(message, title, iconKind);
        TrySetOwner(messageBox);
        messageBox.ShowDialog();
    }

    private bool TrySetOwner(Window child)
    {
        if (!owner.IsLoaded || !owner.IsVisible)
        {
            return false;
        }
        try
        {
            child.Owner = owner;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

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

    private static bool IsScml(IEnumerable<string> files) =>
        files.Any(file => file.EndsWith(".scml", StringComparison.OrdinalIgnoreCase));

    private static bool IsKanim(IEnumerable<string> files) =>
        files.Any(file => KanimFileMatcher.IsKanimFile(file, Properties.Default.EnableTxtToBytes));

    private static string GetKanimFileFilter(string title, string suffix) =>
        Properties.Default.EnableTxtToBytes
            ? $"{title}|*{suffix}.bytes;*{suffix}.txt|Bytes 文件|*{suffix}.bytes|Txt 文件|*{suffix}.txt"
            : $"{title}|*{suffix}.bytes";

    private T Find<T>(string name) where T : class => (root.FindName(name) as T)
        ?? throw new InvalidOperationException($"Main window control '{name}' was not found.");
}
