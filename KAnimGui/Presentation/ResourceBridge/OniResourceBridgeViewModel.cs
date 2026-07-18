using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KAnimGui.Application.ResourceBridge;
using KAnimGui.Core;

namespace KAnimGui.Presentation.ResourceBridge;

/// <summary>
/// Coordinates the resource bridge browsing and export workflow.
/// The window deliberately stays focused on three actions: filter, preview and export.
/// </summary>
public partial class OniResourceBridgeViewModel : ObservableObject, IDisposable
{
    private readonly IResourceBridgeClient client;
    private readonly IResourceBridgeExportService exporter;
    private readonly IResourceBridgeStateStore stateStore;
    private readonly IThumbnailCache thumbnailCache;
    private readonly IApplicationPathProvider paths;
    private CancellationTokenSource? refreshCancellation;
    private CancellationTokenSource? operationCancellation;
    private CancellationTokenSource? thumbnailCancellation;
    private BridgeSnapshot? snapshot;
    private BridgeState state = BridgeState.Empty;
    private bool initialized;
    private bool applyingLayout;

    private string PreviewCacheDirectory => Path.Combine(
        paths.ResourceBridgeCacheDirectory,
        "Preview");

    public OniResourceBridgeViewModel(
        IResourceBridgeClient client,
        IResourceBridgeExportService exporter,
        IResourceBridgeStateStore stateStore,
        IThumbnailCache thumbnailCache,
        IApplicationPathProvider paths)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.thumbnailCache = thumbnailCache ?? throw new ArgumentNullException(nameof(thumbnailCache));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        ExportResourceCommand = new AsyncRelayCommand<BridgeResourceRowViewModel?>(
            ExportResourceAsync,
            row => !IsBusy && row?.CanExport == true);
        ExportFilteredCommand = new AsyncRelayCommand(
            ExportFilteredAsync,
            () => !IsBusy && ExportableResourceCount > 0);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    public ObservableCollection<BridgeResourceRowViewModel> FilteredResources { get; } = [];

    public IReadOnlyList<BridgeResourceRowViewModel> SelectedResources { get; private set; } = [];

    public string ExportDirectory => paths.ResourceBridgeExportDirectory;

    public string ConnectedBridgeVersion { get; private set; } = string.Empty;

    public string BundledBridgeVersion => OniResourceBridgeModInstaller.BundledVersion;

    public bool IsBridgeUpdateAvailable { get; private set; }

    public event EventHandler? BridgeUpdateAvailable;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand<BridgeResourceRowViewModel?> ExportResourceCommand { get; }

    public IAsyncRelayCommand ExportFilteredCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IReadOnlyList<string> ExportLayoutOptions { get; } = new[]
    {
        "按资源分目录",
        "按文件类型分组"
    };

    [ObservableProperty]
    private string connectionText = "未连接";

    [ObservableProperty]
    private string statusText = "等待连接";

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private string resourceTypeFilter = "KAnim";

    [ObservableProperty]
    private string exportLayoutText = "按资源分目录";

    [ObservableProperty]
    private string exportLayoutDescription = "每个资源独立一个文件夹，PNG 和 bytes 文件放在一起。";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isExporting;

    [ObservableProperty]
    private double exportProgress;

    [ObservableProperty]
    private int exportCompleted;

    [ObservableProperty]
    private int exportTotal;

    [ObservableProperty]
    private string exportPhaseText = string.Empty;

    [ObservableProperty]
    private string exportResultText = string.Empty;

    [ObservableProperty]
    private BridgeResourceRowViewModel? selectedResource;

    public int ExportableResourceCount =>
        (SelectedResources.Count > 0 ? SelectedResources : FilteredResources)
        .Count(row => row.CanExport);

    public string FilterSummaryText => $"显示 {FilteredResources.Count} / {AllRows.Count} 个资源";

    public string ExportButtonText => IsExporting
        ? (ExportTotal > 0 ? $"{ExportPhaseText} {ExportCompleted}/{ExportTotal}" : "处理中...")
        : SelectedResources.Count > 0
        ? $"导出选中 ({ExportableResourceCount})"
        : $"导出当前结果 ({ExportableResourceCount})";

    public string ExportProgressText => ExportTotal > 0
        ? $"{ExportPhaseText}：{ExportCompleted} / {ExportTotal}"
        : string.Empty;

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnResourceTypeFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedResourceChanged(BridgeResourceRowViewModel? value)
    {
        ExportResourceCommand.NotifyCanExecuteChanged();
        if (value != null)
        {
            EnsureThumbnailLoaded(value);
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ExportResourceCommand.NotifyCanExecuteChanged();
        ExportFilteredCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnExportLayoutTextChanged(string value)
    {
        BridgeExportLayout layout = ParseExportLayout(value);
        ExportLayoutDescription = GetExportLayoutDescription(layout);
        if (!initialized || applyingLayout)
        {
            return;
        }

        state = state with { ExportLayout = layout };
        _ = stateStore.SaveAsync(state);
    }

    partial void OnExportCompletedChanged(int value)
    {
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(ExportButtonText));
    }

    partial void OnExportTotalChanged(int value)
    {
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(ExportButtonText));
    }

    partial void OnExportPhaseTextChanged(string value)
    {
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(ExportButtonText));
    }

    partial void OnIsExportingChanged(bool value)
    {
        OnPropertyChanged(nameof(ExportProgressText));
        OnPropertyChanged(nameof(ExportButtonText));
    }

    public async Task InitializeAsync()
    {
        if (initialized)
        {
            return;
        }

        // Remove preview files left behind by an interrupted previous session.
        CleanupAnimationPreviewCache();
        initialized = true;
        await RefreshAsync().ConfigureAwait(true);
    }

    public void SetSelectedResources(IEnumerable<BridgeResourceRowViewModel> resources)
    {
        SelectedResources = resources.Distinct().ToList();
        NotifyExportStateChanged();
        if (SelectedResource != null)
        {
            EnsureThumbnailLoaded(SelectedResource);
        }
    }

    public async Task<ExportArtifact> PrepareAnimationPreviewAsync(
        BridgeResourceRowViewModel? row,
        CancellationToken cancellationToken = default)
    {
        if (row?.CanPreview != true || snapshot == null || IsBusy)
        {
            throw new InvalidOperationException("请选择一个可预览的 KAnim 动画。");
        }

        SetBusy(true, $"正在准备 {row.Name} 的动画预览...");
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CleanupAnimationPreviewCache();
        string previewDirectory = Path.Combine(
            PreviewCacheDirectory,
            Guid.NewGuid().ToString("N"));
        try
        {
            BridgeExportRequest request = await PrepareExportRequestAsync(
                row,
                operationCancellation.Token).ConfigureAwait(true);
            request = request with
            {
                OutputDirectory = previewDirectory
            };
            ExportArtifact artifact = await exporter.ExportAsync(
                request,
                operationCancellation.Token).ConfigureAwait(true);
            StatusText = $"已准备 {row.Name} 的动画预览";
            return artifact;
        }
        catch
        {
            // If preparation failed, do not leave a partial preview package behind.
            TryDeleteDirectory(previewDirectory);
            throw;
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            SetBusy(false);
        }
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        SetBusy(true, "正在连接游戏资源桥...");
        CancelThumbnailLoads();
        using var refreshOperation = new CancellationTokenSource();
        refreshCancellation = refreshOperation;

        try
        {
            state = await stateStore.LoadAsync(refreshOperation.Token).ConfigureAwait(true);
            SetExportLayoutText(state.ExportLayout);
            snapshot = await client.GetSnapshotAsync(refreshOperation.Token).ConfigureAwait(true);
            ConnectionText = $"已加载资源 {snapshot.Status.AnimationCount} / 游戏资源包 {snapshot.Status.ResourcePackageCount}";
            ConnectedBridgeVersion = snapshot.Status.Version;
            IsBridgeUpdateAvailable = OniResourceBridgeModInstaller.IsOlderVersion(
                ConnectedBridgeVersion,
                BundledBridgeVersion);

            var rows = snapshot.AllResources
                .OrderBy(resource => resource.Key.Name, StringComparer.OrdinalIgnoreCase)
                .Select(resource => new BridgeResourceRowViewModel(resource))
                .ToList();
            ReconcileRows(rows);
            ApplyFilter();
            StatusText = IsBridgeUpdateAvailable
                ? $"检测到资源桥旧版本 {ConnectedBridgeVersion}，内置版本为 {BundledBridgeVersion}。"
                : snapshot.Status.AssetsReady
                    ? $"在线资源已就绪，可导出 {AllRows.Count(row => row.CanExport)} 个资源。"
                    : $"资源桥已连接，但游戏资源可能还在加载：已加载 {snapshot.Animations.Count}，离线 {snapshot.OfflineAnimations.Count}";
            if (IsBridgeUpdateAvailable)
            {
                BridgeUpdateAvailable?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) when (refreshOperation.IsCancellationRequested)
        {
            StatusText = "操作已取消";
        }
        catch (OperationCanceledException)
        {
            snapshot = null;
            FilteredResources.Clear();
            ConnectionText = "未连接";
            ConnectedBridgeVersion = string.Empty;
            IsBridgeUpdateAvailable = false;
            StatusText = "连接资源桥超时，请确认缺氧正在运行且已启用 ONI Resource Bridge 模组，然后点击刷新资源重试。";
            NotifyExportStateChanged();
        }
        catch (Exception ex)
        {
            snapshot = null;
            FilteredResources.Clear();
            ConnectionText = "未连接";
            ConnectedBridgeVersion = string.Empty;
            IsBridgeUpdateAvailable = false;
            StatusText = ex.Message;
            NotifyExportStateChanged();
        }
        finally
        {
            if (ReferenceEquals(refreshCancellation, refreshOperation))
            {
                refreshCancellation = null;
            }

            SetBusy(false);
        }
    }

    private async Task ExportResourceAsync(BridgeResourceRowViewModel? row)
    {
        if (row == null || !row.CanExport || snapshot == null || IsBusy)
        {
            return;
        }

        SetExportState(1, $"正在准备 {row.Name} 的导出...");
        operationCancellation = new CancellationTokenSource();
        try
        {
            BridgeExportRequest request = await PrepareExportRequestAsync(row, operationCancellation.Token).ConfigureAwait(true);
            ExportPhaseText = "写入导出文件";
            StatusText = $"资源包已准备，正在写入 {row.Name} 的导出文件...";
            ExportArtifact artifact = await exporter.ExportAsync(request, operationCancellation.Token).ConfigureAwait(true);
            ExportCompleted = 1;
            ExportProgress = 100;
            ExportResultText = $"已导出到：{GetArtifactSummary(artifact)}";
            StatusText = $"已导出 {row.Name}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "导出已取消";
        }
        catch (Exception ex)
        {
            ExportResultText = string.Empty;
            StatusText = $"导出失败：{ex.Message}";
        }
        finally
        {
            CompleteExportOperation();
        }
    }

    private async Task ExportFilteredAsync()
    {
        if (snapshot == null || IsBusy)
        {
            return;
        }

        IReadOnlyList<BridgeResourceRowViewModel> rows = SelectedResources.Count > 0
            ? SelectedResources.Where(row => row.CanExport).ToList()
            : FilteredResources.Where(row => row.CanExport).ToList();
        if (rows.Count == 0)
        {
            StatusText = "当前结果没有可导出的资源。";
            return;
        }

        SetExportState(rows.Count, $"正在从游戏资源桥准备 {rows.Count} 个资源包...");
        operationCancellation = new CancellationTokenSource();
        try
        {
            var requests = new List<BridgeExportRequest>();
            var preparationFailures = new List<string>();
            for (int index = 0; index < rows.Count; index++)
            {
                BridgeResourceRowViewModel row = rows[index];
                ExportPhaseText = "读取资源包";
                ExportCompleted = index;
                ExportProgress = rows.Count == 0 ? 0 : index * 100d / rows.Count;
                StatusText = $"正在从游戏资源桥读取资源包 {index + 1} / {rows.Count}：{row.Name}";
                try
                {
                    requests.Add(await PrepareExportRequestAsync(row, operationCancellation.Token).ConfigureAwait(true));
                }
                catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
                {
                    StatusText = "批量导出已取消";
                    return;
                }
                catch (Exception ex)
                {
                    preparationFailures.Add($"{row.Name}: {ex.Message}");
                }

                ExportCompleted = index + 1;
                ExportProgress = rows.Count == 0 ? 0 : (index + 1) * 100d / rows.Count;
            }

            ExportTotal = requests.Count;
            ExportCompleted = 0;
            ExportProgress = 0;
            ExportPhaseText = requests.Count > 0 ? "写入导出文件" : "整理失败报告";
            StatusText = requests.Count > 0
                ? $"资源包准备完成，正在写入 {requests.Count} 个导出文件..."
                : "资源包准备完成，正在整理失败报告...";
            var progress = new Progress<BatchProgress>(item =>
            {
                ExportCompleted = item.Completed;
                ExportProgress = item.Total == 0 ? 0 : item.Completed * 100d / item.Total;
                StatusText = item.Succeeded
                    ? $"正在导出 {item.Completed} / {item.Total}：{item.ResourceName}"
                    : $"导出失败 {item.ResourceName}：{item.ErrorMessage}";
            });
            BatchExportResult result = await exporter.ExportBatchAsync(
                requests,
                progress,
                operationCancellation.Token).ConfigureAwait(true);
            ExportCompleted = result.Succeeded.Count;
            ExportProgress = rows.Count == 0 ? 0 : ExportCompleted * 100d / rows.Count;
            int failed = result.Failed.Count + preparationFailures.Count;
            string? failureReportPath = result.FailureReportPath;
            if (preparationFailures.Count > 0)
            {
                var reportLines = preparationFailures
                    .Concat(result.Failed.Select(resource => $"{resource.Name}: 导出阶段失败"))
                    .ToList();
                failureReportPath = await exporter.WriteFailureReportAsync(
                    reportLines,
                    operationCancellation.Token).ConfigureAwait(true);
            }

            StatusText = result.WasCanceled
                ? $"批量导出已取消，已完成 {result.Succeeded.Count} 个"
                : $"批量导出完成：成功 {result.Succeeded.Count} 个，失败 {failed} 个";
            ExportResultText = failureReportPath == null
                ? $"输出目录：{ExportDirectory}"
                : $"输出目录：{ExportDirectory}；失败报告：{Path.GetFileName(failureReportPath)}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "批量导出已取消";
        }
        catch (Exception ex)
        {
            ExportResultText = string.Empty;
            StatusText = $"批量导出失败：{ex.Message}";
        }
        finally
        {
            CompleteExportOperation();
        }
    }

    private async Task<BridgeExportRequest> PrepareExportRequestAsync(
        BridgeResourceRowViewModel row,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot == null)
        {
            throw new InvalidOperationException("资源桥尚未连接。");
        }

        return row.Resource switch
        {
            BridgeAnimationResource => new BridgeExportRequest(
                row.Resource,
                KAnimPackage: await client.GetKAnimPackageAsync(snapshot.BaseUrl, row.Resource.Key, cancellationToken).ConfigureAwait(true),
                Layout: state.ExportLayout),
            BridgeSpriteResource => new BridgeExportRequest(
                row.Resource,
                SpritePackage: await client.GetSpritePackageAsync(snapshot.BaseUrl, row.Resource.Key, cancellationToken).ConfigureAwait(true),
                Layout: state.ExportLayout),
            _ => throw new InvalidOperationException("不支持的资源类型。")
        };
    }

    private void ApplyFilter()
    {
        IEnumerable<BridgeResourceRowViewModel> query = AllRows;
        if (ResourceTypeFilter.Equals("KAnim", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(row => row.Resource is BridgeAnimationResource);
        }
        else if (ResourceTypeFilter.Equals("Sprite", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(row => row.Resource is BridgeSpriteResource);
        }

        string filter = FilterText.Trim();
        if (filter.Length > 0)
        {
            query = query.Where(row =>
                row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (row.Bundle?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        FilteredResources.Clear();
        foreach (BridgeResourceRowViewModel row in query.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase))
        {
            FilteredResources.Add(row);
        }

        OnPropertyChanged(nameof(FilterSummaryText));
        NotifyExportStateChanged();
    }

    private List<BridgeResourceRowViewModel> AllRows { get; } = [];

    private void ReconcileRows(IEnumerable<BridgeResourceRowViewModel> rows)
    {
        AllRows.Clear();
        AllRows.AddRange(rows);
        SelectedResource = null;
        SetSelectedResources([]);
        OnPropertyChanged(nameof(FilterSummaryText));
    }

    private void StartThumbnailLoads(IReadOnlyList<BridgeResourceRowViewModel> rows)
    {
        CancelThumbnailLoads();
        if (snapshot == null || rows.Count == 0)
        {
            return;
        }

        thumbnailCancellation = new CancellationTokenSource();
        _ = LoadThumbnailsAsync(rows, thumbnailCancellation.Token);
    }

    /// <summary>
    /// Loads only the rows currently visible in the virtualized resource list,
    /// plus the small buffer supplied by the view. Scrolling cancels the old
    /// queue so requests follow the user's viewport instead of preloading the
    /// entire result set.
    /// </summary>
    public void LoadThumbnailsForVisibleRows(IEnumerable<BridgeResourceRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        IReadOnlyList<BridgeResourceRowViewModel> targets = rows
            .Distinct()
            .ToList();
        if (SelectedResource is not null && !targets.Contains(SelectedResource))
        {
            targets = targets.Append(SelectedResource).ToList();
        }

        if (targets.Count == 0)
        {
            CancelThumbnailLoads();
            return;
        }

        StartThumbnailLoads(targets);
    }

    private void EnsureThumbnailLoaded(BridgeResourceRowViewModel row)
    {
        if (snapshot == null || row.HasPreviewLoaded || row.IsLoadingThumbnail)
        {
            return;
        }

        thumbnailCancellation ??= new CancellationTokenSource();
        _ = LoadThumbnailAsync(row, thumbnailCancellation.Token);
    }

    private async Task LoadThumbnailsAsync(
        IReadOnlyList<BridgeResourceRowViewModel> rows,
        CancellationToken cancellationToken)
    {
        try
        {
            await Parallel.ForEachAsync(
                rows,
                new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
                async (row, token) => await LoadThumbnailAsync(row, token).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async ValueTask LoadThumbnailAsync(
        BridgeResourceRowViewModel row,
        CancellationToken cancellationToken)
    {
        if (snapshot == null || row.HasPreviewLoaded || row.IsLoadingThumbnail)
        {
            return;
        }

        row.IsLoadingThumbnail = true;
        row.HasThumbnailError = false;
        try
        {
            string? path = thumbnailCache.GetPath(row.Resource.Key);
            if (path == null)
            {
                string png = row.Resource switch
                {
                    BridgeAnimationResource => (await client.GetPreviewAsync(snapshot.BaseUrl, row.Resource.Key, cancellationToken).ConfigureAwait(false)).PngBytes,
                    BridgeSpriteResource => (await client.GetSpritePackageAsync(snapshot.BaseUrl, row.Resource.Key, cancellationToken).ConfigureAwait(false)).PngBytes,
                    _ => throw new InvalidOperationException("不支持的资源类型。")
                };
                path = await thumbnailCache.SaveAsync(row.Resource.Key, png, cancellationToken).ConfigureAwait(false);
            }

            BitmapImage bitmap = LoadBitmap(path);
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                row.Thumbnail = bitmap;
                row.HasPreviewLoaded = true;
            }
            else if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    row.Thumbnail = bitmap;
                    row.HasPreviewLoaded = true;
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            row.HasThumbnailError = true;
        }
        finally
        {
            row.IsLoadingThumbnail = false;
        }
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 256;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void SetExportLayoutText(BridgeExportLayout layout)
    {
        applyingLayout = true;
        try
        {
            ExportLayoutText = FormatExportLayout(layout);
            ExportLayoutDescription = GetExportLayoutDescription(layout);
        }
        finally
        {
            applyingLayout = false;
        }
    }

    private void SetExportState(int total, string status)
    {
        ExportTotal = total;
        ExportCompleted = 0;
        ExportProgress = 0;
        ExportResultText = string.Empty;
        ExportPhaseText = "准备资源包";
        IsExporting = true;
        SetBusy(true, status);
    }

    private void CompleteExportOperation()
    {
        operationCancellation?.Dispose();
        operationCancellation = null;
        IsExporting = false;
        ExportPhaseText = string.Empty;
        SetBusy(false);
    }

    private void NotifyExportStateChanged()
    {
        OnPropertyChanged(nameof(ExportableResourceCount));
        OnPropertyChanged(nameof(ExportButtonText));
        ExportFilteredCommand.NotifyCanExecuteChanged();
        ExportResourceCommand.NotifyCanExecuteChanged();
    }

    private static string GetArtifactSummary(ExportArtifact artifact)
    {
        string path = artifact.PngPath ?? artifact.AnimPath ?? artifact.BuildPath ?? string.Empty;
        return string.IsNullOrWhiteSpace(path) ? "输出目录" : path;
    }

    private static string FormatExportLayout(BridgeExportLayout layout) =>
        layout == BridgeExportLayout.Split ? "按文件类型分组" : "按资源分目录";

    private static string GetExportLayoutDescription(BridgeExportLayout layout) =>
        layout == BridgeExportLayout.Split
            ? "所有 PNG 放入 KAnim_Png，bytes 文件放入 KAnim_Bytes，适合批量处理。"
            : "每个资源独立一个文件夹，PNG 和 bytes 文件放在一起，最适合逐个使用。";

    private static BridgeExportLayout ParseExportLayout(string value) =>
        value.Equals("按文件类型分组", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Split", StringComparison.OrdinalIgnoreCase)
            ? BridgeExportLayout.Split
            : BridgeExportLayout.Grouped;

    private void SetBusy(bool value, string? status = null)
    {
        IsBusy = value;
        if (!string.IsNullOrWhiteSpace(status))
        {
            StatusText = status;
        }
    }

    private void Cancel()
    {
        refreshCancellation?.Cancel();
        operationCancellation?.Cancel();
        thumbnailCancellation?.Cancel();
        StatusText = "正在取消...";
    }

    private void CancelThumbnailLoads()
    {
        thumbnailCancellation?.Cancel();
        thumbnailCancellation?.Dispose();
        thumbnailCancellation = null;
    }

    /// <summary>
    /// Deletes the temporary KAnim files generated for the preview window.
    /// </summary>
    public void CleanupAnimationPreviewCache() => TryDeleteDirectory(PreviewCacheDirectory);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Preview cleanup is best effort and must not interrupt closing the UI.
        }
        catch (UnauthorizedAccessException)
        {
            // Preview cleanup is best effort and must not interrupt closing the UI.
        }
    }

    public void Dispose()
    {
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = null;
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
        CancelThumbnailLoads();
        CleanupAnimationPreviewCache();
    }
}

public partial class BridgeResourceRowViewModel : ObservableObject
{
    public BridgeResourceRowViewModel(BridgeResource resource)
    {
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
    }

    public BridgeResource Resource { get; }

    public string Name => Resource.Key.Name;

    public string ResourceTypeLabel => Resource.Key.Type == BridgeResourceType.KAnim ? "KAnim" : "Sprite";

    public string SourceLabel => Resource.IsOffline ? "离线资源" : "在线资源";

    public string? Bundle => Resource.Bundle;

    public string BundleText => string.IsNullOrWhiteSpace(Bundle) ? "" : $"Bundle：{Bundle}";

    public string SummaryText => Resource switch
    {
        BridgeAnimationResource animation => $"{animation.AnimationCount} 动画 / {animation.FrameCount} 帧 / {animation.ElementCount} 元素",
        BridgeSpriteResource sprite => $"{sprite.Width} x {sprite.Height}",
        _ => string.Empty
    };

    public bool CanExport => Resource switch
    {
        BridgeAnimationResource animation => animation.HasAnimation,
        BridgeSpriteResource => true,
        _ => false
    };

    public bool CanPreview => Resource is BridgeAnimationResource animation && animation.HasAnimation;

    public string RowActionText => CanPreview ? "预览" : "导出";

    public string ExportStatus => CanExport ? "可导出" : "没有可用动画，无法导出";

    public string ThumbnailStatusText => HasPreviewLoaded
        ? "在线缩略图已就绪"
        : IsLoadingThumbnail
            ? "正在加载在线缩略图..."
            : HasThumbnailError
                ? "在线缩略图加载失败"
                : "等待加载缩略图";

    [ObservableProperty]
    private BitmapImage? thumbnail;

    [ObservableProperty]
    private bool hasPreviewLoaded;

    [ObservableProperty]
    private bool isLoadingThumbnail;

    [ObservableProperty]
    private bool hasThumbnailError;

    partial void OnHasPreviewLoadedChanged(bool value) => OnPropertyChanged(nameof(ThumbnailStatusText));

    partial void OnIsLoadingThumbnailChanged(bool value) => OnPropertyChanged(nameof(ThumbnailStatusText));

    partial void OnHasThumbnailErrorChanged(bool value) => OnPropertyChanged(nameof(ThumbnailStatusText));
}
