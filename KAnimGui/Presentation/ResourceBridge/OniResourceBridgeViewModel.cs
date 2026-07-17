using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KAnimGui.Application.ResourceBridge;

namespace KAnimGui.Presentation.ResourceBridge;

public partial class OniResourceBridgeViewModel : ObservableObject, IDisposable
{
    private readonly IResourceBridgeClient client;
    private readonly IResourceBridgeExportService exporter;
    private readonly IResourceBridgeStateStore stateStore;
    private readonly IThumbnailCache thumbnailCache;
    private readonly IApplicationPathProvider paths;
    private readonly IKanimWorkspaceGateway workspaceGateway;
    private CancellationTokenSource? operationCancellation;
    private CancellationTokenSource? thumbnailCancellation;
    private BridgeSnapshot? snapshot;
    private BridgeState state = BridgeState.Empty;
    private bool initialized;

    public OniResourceBridgeViewModel(
        IResourceBridgeClient client,
        IResourceBridgeExportService exporter,
        IResourceBridgeStateStore stateStore,
        IThumbnailCache thumbnailCache,
        IApplicationPathProvider paths,
        IKanimWorkspaceGateway workspaceGateway)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.thumbnailCache = thumbnailCache ?? throw new ArgumentNullException(nameof(thumbnailCache));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.workspaceGateway = workspaceGateway ?? throw new ArgumentNullException(nameof(workspaceGateway));

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        ImportSelectedCommand = new AsyncRelayCommand(ImportSelectedAsync, CanImportSelected);
        ExportFilteredCommand = new AsyncRelayCommand(ExportFilteredAsync, () => !IsBusy && FilteredResources.Any(row => row.CanExport));
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        ToggleFavoriteCommand = new AsyncRelayCommand<BridgeResourceRowViewModel?>(ToggleFavoriteAsync);
        SaveTagsCommand = new AsyncRelayCommand(SaveTagsAsync, () => SelectedResource != null && !IsBusy);
    }

    public ObservableCollection<BridgeResourceRowViewModel> FilteredResources { get; } = [];

    public IReadOnlyList<BridgeResourceRowViewModel> SelectedResources { get; private set; } = [];

    public string ExportDirectory => paths.ResourceBridgeExportDirectory;

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ImportSelectedCommand { get; }

    public IAsyncRelayCommand ExportFilteredCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand<BridgeResourceRowViewModel?> ToggleFavoriteCommand { get; }

    public IAsyncRelayCommand SaveTagsCommand { get; }

    public IReadOnlyList<string> ExportLayoutOptions { get; } = new[] { "Grouped", "Split" };

    [ObservableProperty]
    private string connectionText = "未连接";

    [ObservableProperty]
    private string statusText = "等待连接";

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private string resourceTypeFilter = "KAnim";

    [ObservableProperty]
    private string exportLayoutText = "Grouped";

    [ObservableProperty]
    private string tagEditText = string.Empty;

    [ObservableProperty]
    private bool favoritesOnly;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private BridgeResourceRowViewModel? selectedResource;

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnResourceTypeFilterChanged(string value) => ApplyFilter();

    partial void OnFavoritesOnlyChanged(bool value) => ApplyFilter();

    partial void OnSelectedResourceChanged(BridgeResourceRowViewModel? value)
    {
        TagEditText = value?.TagText ?? string.Empty;
        ImportSelectedCommand.NotifyCanExecuteChanged();
        SaveTagsCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ImportSelectedCommand.NotifyCanExecuteChanged();
        ExportFilteredCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        SaveTagsCommand.NotifyCanExecuteChanged();
    }

    partial void OnExportLayoutTextChanged(string value)
    {
        if (!initialized)
        {
            return;
        }

        state = state with { ExportLayout = ParseExportLayout(value) };
        _ = stateStore.SaveAsync(state);
    }

    public async Task InitializeAsync()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        await RefreshAsync().ConfigureAwait(true);
    }

    public void SetSelectedResources(IEnumerable<BridgeResourceRowViewModel> resources)
    {
        SelectedResources = resources.Distinct().ToList();
        ImportSelectedCommand.NotifyCanExecuteChanged();
        ExportFilteredCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        SetBusy(true, "正在连接游戏资源桥...");
        CancelThumbnailLoads();

        try
        {
            state = await stateStore.LoadAsync().ConfigureAwait(true);
            ExportLayoutText = state.ExportLayout == BridgeExportLayout.Split ? "Split" : "Grouped";
            snapshot = await client.GetSnapshotAsync().ConfigureAwait(true);
            ConnectionText = $"已加载资源 {snapshot.Status.AnimationCount} / 游戏资源包 {snapshot.Status.ResourcePackageCount}";

            var rows = snapshot.AllResources
                .OrderBy(resource => resource.Key.Name, StringComparer.OrdinalIgnoreCase)
                .Select(CreateRow)
                .ToList();
            ReconcileRows(rows);
            ApplyFilter();
            StatusText = snapshot.Status.AssetsReady
                ? $"已读取 {snapshot.Animations.Count} 个已加载资源，扫描到 {snapshot.OfflineAnimations.Count} 个离线资源"
                : $"资源桥已连接，但游戏资源可能还在加载：已加载 {snapshot.Animations.Count}，离线 {snapshot.OfflineAnimations.Count}";
            StartThumbnailLoads(FilteredResources.Take(96).ToList());
        }
        catch (OperationCanceledException)
        {
            StatusText = "操作已取消";
        }
        catch (Exception ex)
        {
            snapshot = null;
            FilteredResources.Clear();
            ConnectionText = "未连接";
            StatusText = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ImportSelectedAsync()
    {
        BridgeResourceRowViewModel? row = SelectedResource;
        if (row == null || !row.CanExport || snapshot == null || IsBusy)
        {
            return;
        }

        SetBusy(true, $"正在准备 {row.Name}...");
        try
        {
            BridgeExportRequest request = await PrepareExportRequestAsync(row, CancellationToken.None).ConfigureAwait(true);
            ExportArtifact artifact = await exporter.ExportAsync(request).ConfigureAwait(true);
            StatusText = $"已导出 {row.Name}";
            if (row.Resource is BridgeAnimationResource)
            {
                await OpenInWorkspaceAsync(artifact).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"导出失败：{ex.Message}";
        }
        finally
        {
            SetBusy(false);
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

        SetBusy(true, $"正在准备批量导出 {rows.Count} 个资源...");
        operationCancellation = new CancellationTokenSource();
        try
        {
            var requests = new List<BridgeExportRequest>();
            var preparationFailures = new List<string>();
            foreach (BridgeResourceRowViewModel row in rows)
            {
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
            }

            int completed = 0;
            var progress = new Progress<BatchProgress>(item =>
            {
                completed = item.Completed;
                StatusText = item.Succeeded
                    ? $"正在导出 {completed} / {item.Total}: {item.ResourceName}"
                    : $"导出失败 {item.ResourceName}: {item.ErrorMessage}";
            });
            BatchExportResult result = await exporter.ExportBatchAsync(
                requests,
                progress,
                operationCancellation.Token).ConfigureAwait(true);
            StatusText = result.WasCanceled
                ? $"批量导出已取消，已完成 {result.Succeeded.Count} 个"
                : $"批量导出完成：成功 {result.Succeeded.Count} 个，失败 {result.Failed.Count + preparationFailures.Count} 个";
        }
        catch (OperationCanceledException)
        {
            StatusText = "批量导出已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"批量导出失败：{ex.Message}";
        }
        finally
        {
            operationCancellation?.Dispose();
            operationCancellation = null;
            SetBusy(false);
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

    private async Task ToggleFavoriteAsync(BridgeResourceRowViewModel? row)
    {
        if (row == null)
        {
            return;
        }

        row.IsFavorite = !row.IsFavorite;
        state = BuildState();
        await stateStore.SaveAsync(state).ConfigureAwait(true);
        StatusText = row.IsFavorite ? $"已收藏：{row.Name}" : $"已取消收藏：{row.Name}";
        ApplyFilter();
    }

    private async Task SaveTagsAsync()
    {
        if (SelectedResource == null)
        {
            return;
        }

        SelectedResource.SetTags(TagEditText
            .Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList());
        state = BuildState();
        await stateStore.SaveAsync(state).ConfigureAwait(true);
        ApplyFilter();
        StatusText = $"已保存标签：{SelectedResource.Name}";
    }

    private Task OpenInWorkspaceAsync(ExportArtifact artifact)
    {
        if (artifact.PngPath == null || artifact.AnimPath == null || artifact.BuildPath == null)
        {
            return Task.CompletedTask;
        }

        return workspaceGateway.OpenAsync(artifact);
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

        if (FavoritesOnly)
        {
            query = query.Where(row => row.IsFavorite);
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

        StatusText = snapshot == null
            ? StatusText
            : $"显示 {FilteredResources.Count} / {AllRows.Count} 个资源";
        ExportFilteredCommand.NotifyCanExecuteChanged();
        StartThumbnailLoads(FilteredResources.Take(96).ToList());
    }

    private List<BridgeResourceRowViewModel> AllRows { get; } = [];

    private void ReconcileRows(IEnumerable<BridgeResourceRowViewModel> rows)
    {
        AllRows.Clear();
        AllRows.AddRange(rows);
        SelectedResource = null;
        SetSelectedResources([]);
    }

    private BridgeResourceRowViewModel CreateRow(BridgeResource resource)
    {
        state.ResourceTags.TryGetValue(resource.Key.Id, out IReadOnlyList<string>? tags);
        return new BridgeResourceRowViewModel(
            resource,
            state.FavoriteResourceIds.Contains(resource.Key.Id),
            tags ?? []);
    }

    private void StartThumbnailLoads(IReadOnlyList<BridgeResourceRowViewModel> rows)
    {
        CancelThumbnailLoads();
        if (snapshot == null || rows.Count == 0)
        {
            return;
        }

        thumbnailCancellation = new CancellationTokenSource();
        CancellationToken token = thumbnailCancellation.Token;
        _ = LoadThumbnailsAsync(rows, token);
    }

    private async Task LoadThumbnailsAsync(IReadOnlyList<BridgeResourceRowViewModel> rows, CancellationToken cancellationToken)
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

    private async ValueTask LoadThumbnailAsync(BridgeResourceRowViewModel row, CancellationToken cancellationToken)
    {
        if (snapshot == null || row.HasPreviewLoaded)
        {
            return;
        }

        row.IsLoadingThumbnail = true;
        bool loaded = false;
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
            loaded = true;
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                row.Thumbnail = bitmap;
            }
            else if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => row.Thumbnail = bitmap);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // A missing preview must not make the resource unusable.
        }
        finally
        {
            if (loaded && System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            {
                row.HasPreviewLoaded = true;
            }
            else if (loaded && System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => row.HasPreviewLoaded = true);
            }
            row.IsLoadingThumbnail = false;
        }
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 96;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private BridgeState BuildState()
    {
        return new BridgeState(
            1,
            AllRows.Where(row => row.IsFavorite).Select(row => row.Resource.Key.Id).ToHashSet(StringComparer.OrdinalIgnoreCase),
            AllRows
                .Where(row => row.Tags.Count > 0)
                .ToDictionary(
                    row => row.Resource.Key.Id,
                    row => (IReadOnlyList<string>)row.Tags.ToList(),
                    StringComparer.OrdinalIgnoreCase),
            state.ExportLayout);
    }

    private bool CanImportSelected() => !IsBusy && SelectedResource?.CanExport == true;

    private static BridgeExportLayout ParseExportLayout(string value) =>
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
        operationCancellation?.Cancel();
        StatusText = "正在取消...";
    }

    private void CancelThumbnailLoads()
    {
        thumbnailCancellation?.Cancel();
        thumbnailCancellation?.Dispose();
        thumbnailCancellation = null;
    }

    public void Dispose()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
        CancelThumbnailLoads();
    }
}

public partial class BridgeResourceRowViewModel : ObservableObject
{
    public BridgeResourceRowViewModel(
        BridgeResource resource,
        bool isFavorite,
        IReadOnlyList<string> tags)
    {
        Resource = resource;
        IsFavorite = isFavorite;
        Tags = new ObservableCollection<string>(tags);
    }

    public BridgeResource Resource { get; }

    public string Name => Resource.Key.Name;

    public string ResourceTypeLabel => Resource.Key.Type == BridgeResourceType.KAnim ? "KAnim" : "Sprite";

    public string SourceLabel => Resource.IsOffline ? "离线" : "已加载";

    public string? Bundle => Resource.Bundle;

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

    public string ImportStatus => CanExport ? (HasPreviewLoaded ? "可导出" : "缩略图加载中") : "无动画，不能导出";

    public ObservableCollection<string> Tags { get; }

    public string TagText => string.Join(", ", Tags);

    public void SetTags(IEnumerable<string> tags)
    {
        Tags.Clear();
        foreach (string tag in tags)
        {
            Tags.Add(tag);
        }

        OnPropertyChanged(nameof(TagText));
    }

    [ObservableProperty]
    private bool isFavorite;

    public string FavoriteGlyph => IsFavorite ? "★" : string.Empty;

    partial void OnIsFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteGlyph));
    }

    [ObservableProperty]
    private BitmapImage? thumbnail;

    [ObservableProperty]
    private bool hasPreviewLoaded;

    partial void OnHasPreviewLoadedChanged(bool value) => OnPropertyChanged(nameof(ImportStatus));

    [ObservableProperty]
    private bool isLoadingThumbnail;

    partial void OnIsLoadingThumbnailChanged(bool value) => OnPropertyChanged(nameof(ImportStatus));
}
