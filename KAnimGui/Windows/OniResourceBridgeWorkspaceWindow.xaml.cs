using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using KAnimGui.Application.Platform;
using KAnimGui.Application.ResourceBridge;
using KAnimGui.Core;
using KAnimGui.Presentation.ResourceBridge;
using MaterialDesignThemes.Wpf;

namespace KAnimGui.Windows;

public partial class OniResourceBridgeWorkspaceWindow : UserControl, IDisposable
{
    private readonly OniResourceBridgeViewModel viewModel;
    private readonly IFileSystemGateway fileSystem;
    private readonly IExternalLauncher externalLauncher;
    private bool updatePromptShown;
    private bool initialized;
    private bool disposed;

    public OniResourceBridgeWorkspaceWindow(
        OniResourceBridgeViewModel viewModel,
        IFileSystemGateway fileSystem,
        IExternalLauncher externalLauncher)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.externalLauncher = externalLauncher ?? throw new ArgumentNullException(nameof(externalLauncher));
        InitializeComponent();
        DataContext = viewModel;
        viewModel.FilteredResources.CollectionChanged += FilteredResources_CollectionChanged;
        viewModel.BridgeUpdateAvailable += ViewModel_BridgeUpdateAvailable;
        Loaded += Workspace_Loaded;
    }

    public Func<string, string, string, Task>? PreviewRequestedAsync { get; set; }

    private async void Workspace_Loaded(object sender, RoutedEventArgs e)
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        await viewModel.InitializeAsync();
        PromptBridgeUpdateIfNeeded();
    }

    private void PromptBridgeUpdateIfNeeded()
    {
        if (updatePromptShown || !viewModel.IsBridgeUpdateAvailable)
        {
            return;
        }

        updatePromptShown = true;
        MessageBoxResult result = CustomMessageBox.Show(
            Window.GetWindow(this),
            $"检测到 ONI Resource Bridge 版本 {viewModel.ConnectedBridgeVersion}。\n" +
            $"KAnimGUI 内置版本为 {viewModel.BundledBridgeVersion}。\n\n" +
            "是否自动更新模组？更新后需要重启缺氧才能生效。",
            "ONI Resource Bridge 更新",
            PackIconKind.Update,
            MessageBoxButton.YesNo);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            if (!OniResourceBridgeModInstaller.CanInstallBundledMod(out string zipPath))
            {
                viewModel.StatusText = $"找不到内置资源桥安装包：{zipPath}";
                return;
            }

            OniResourceBridgeModInstaller.InstallBundledMod();
            viewModel.StatusText =
                $"资源桥已更新到 {viewModel.BundledBridgeVersion}，请重启缺氧后点击刷新资源。";
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"资源桥更新失败：{ex.Message}";
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Loaded -= Workspace_Loaded;
        viewModel.FilteredResources.CollectionChanged -= FilteredResources_CollectionChanged;
        viewModel.BridgeUpdateAvailable -= ViewModel_BridgeUpdateAvailable;
        viewModel.CleanupAnimationPreviewCache();
        viewModel.Dispose();
    }

    private bool thumbnailViewportUpdateQueued;

    private void ResourceListView_Loaded(object sender, RoutedEventArgs e) => ScheduleVisibleThumbnailLoad();

    private void ResourceListView_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.ViewportHeightChange != 0 || e.ExtentHeightChange != 0)
        {
            ScheduleVisibleThumbnailLoad();
        }
    }

    private void FilteredResources_CollectionChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        ScheduleVisibleThumbnailLoad();

    private void ScheduleVisibleThumbnailLoad()
    {
        if (thumbnailViewportUpdateQueued || !IsLoaded)
        {
            return;
        }

        thumbnailViewportUpdateQueued = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                thumbnailViewportUpdateQueued = false;
                viewModel.LoadThumbnailsForVisibleRows(GetVisibleResourceRows());
            }));
    }

    private IReadOnlyList<BridgeResourceRowViewModel> GetVisibleResourceRows()
    {
        ScrollViewer? scrollViewer = FindVisualChildren<ScrollViewer>(ResourceListView).FirstOrDefault();
        if (scrollViewer is null)
        {
            return [];
        }

        var visibleIndexes = new List<int>();
        foreach (ListViewItem item in FindVisualChildren<ListViewItem>(ResourceListView))
        {
            if (item.DataContext is not BridgeResourceRowViewModel)
            {
                continue;
            }

            try
            {
                Rect bounds = item.TransformToAncestor(scrollViewer)
                    .TransformBounds(new Rect(new Point(0, 0), item.RenderSize));
                if (bounds.Bottom >= 0 && bounds.Top <= scrollViewer.ViewportHeight)
                {
                    int index = ResourceListView.ItemContainerGenerator.IndexFromContainer(item);
                    if (index >= 0)
                    {
                        visibleIndexes.Add(index);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // The container can be recycled while a scroll event is raised.
            }
        }

        if (visibleIndexes.Count == 0)
        {
            return [];
        }

        const int preloadBuffer = 4;
        int first = Math.Max(0, visibleIndexes.Min() - preloadBuffer);
        int last = Math.Min(
            viewModel.FilteredResources.Count - 1,
            visibleIndexes.Max() + preloadBuffer);
        return Enumerable.Range(first, last - first + 1)
            .Select(index => viewModel.FilteredResources[index])
            .ToList();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is null)
        {
            yield break;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void ViewModel_BridgeUpdateAvailable(object? sender, EventArgs e)
    {
        PromptBridgeUpdateIfNeeded();
    }

    private void ResourceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        viewModel.SetSelectedResources(ResourceListView.SelectedItems.Cast<BridgeResourceRowViewModel>());
    }

    private async void ResourceRowAction_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.IsBusy ||
            (sender as FrameworkElement)?.DataContext is not BridgeResourceRowViewModel row ||
            !row.CanExport)
        {
            return;
        }

        if (!row.CanPreview)
        {
            await viewModel.ExportResourceCommand.ExecuteAsync(row);
            return;
        }

        try
        {
            ExportArtifact artifact = await viewModel.PrepareAnimationPreviewAsync(row);
            if (artifact.PngPath is null || artifact.AnimPath is null || artifact.BuildPath is null)
            {
                throw new InvalidOperationException("预览文件不完整，缺少 PNG、anim 或 build 文件。");
            }

            if (PreviewRequestedAsync is null)
            {
                throw new InvalidOperationException("主窗口尚未连接预览工作区。");
            }

            try
            {
                await PreviewRequestedAsync(
                    artifact.PngPath,
                    artifact.BuildPath,
                    artifact.AnimPath);
            }
            finally
            {
                viewModel.CleanupAnimationPreviewCache();
            }
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"预览失败：{ex.Message}";
        }
    }

    private void OpenExportFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            fileSystem.EnsureDirectory(viewModel.ExportDirectory);
            externalLauncher.Open(viewModel.ExportDirectory);
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"打开目录失败：{ex.Message}";
        }
    }

}
