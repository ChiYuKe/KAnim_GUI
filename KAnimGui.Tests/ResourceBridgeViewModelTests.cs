using KAnimGui.Application.ResourceBridge;
using KAnimGui.Presentation.ResourceBridge;

namespace KAnimGui.Tests;

public sealed class ResourceBridgeViewModelTests
{
    [Fact]
    public async Task ViewModel_FiltersAndPersistsExportLayout()
    {
        var client = new FakeResourceBridgeClient(CreateSnapshot());
        var stateStore = new InMemoryStateStore();
        using var viewModel = CreateViewModel(client, stateStore);

        await viewModel.InitializeAsync();

        Assert.Single(viewModel.FilteredResources);
        Assert.Equal("hero", viewModel.FilteredResources[0].Name);

        viewModel.ResourceTypeFilter = "全部";
        Assert.Equal(2, viewModel.FilteredResources.Count);
        viewModel.ExportLayoutText = "按文件类型分组";

        Assert.Equal(BridgeExportLayout.Split, stateStore.State.ExportLayout);
        Assert.Equal(2, viewModel.ExportableResourceCount);
        Assert.Contains("导出", viewModel.ExportButtonText);
    }

    [Fact]
    public async Task ViewModel_ExportsSelectedAnimation()
    {
        var client = new FakeResourceBridgeClient(CreateSnapshot());
        var exporter = new FakeExportService();
        using var viewModel = CreateViewModel(client, new InMemoryStateStore(), exporter);

        await viewModel.InitializeAsync();
        BridgeResourceRowViewModel row = viewModel.FilteredResources[0];
        viewModel.SelectedResource = row;
        viewModel.SetSelectedResources(new[] { row });

        await viewModel.ExportResourceCommand.ExecuteAsync(row);

        Assert.NotNull(exporter.LastRequest);
        Assert.Equal("hero", exporter.LastRequest!.Resource.Key.Name);
    }

    [Fact]
    public async Task ViewModel_PreparesAnimationPreviewInCache()
    {
        var exporter = new FakeExportService();
        using var viewModel = CreateViewModel(
            new FakeResourceBridgeClient(CreateSnapshot()),
            new InMemoryStateStore(),
            exporter);

        await viewModel.InitializeAsync();
        BridgeResourceRowViewModel row = viewModel.FilteredResources[0];
        ExportArtifact artifact = await viewModel.PrepareAnimationPreviewAsync(row);

        Assert.NotNull(artifact.AnimPath);
        Assert.NotNull(exporter.LastRequest);
        Assert.Contains("Preview", exporter.LastRequest!.OutputDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.True(row.CanPreview);
        Assert.Equal("预览", row.RowActionText);
    }

    [Fact]
    public async Task ViewModel_WritesPreparationFailureReport()
    {
        var exporter = new FakeExportService();
        using var viewModel = new OniResourceBridgeViewModel(
            new FailingKAnimPackageClient(CreateSnapshot()),
            exporter,
            new InMemoryStateStore(),
            new FakeThumbnailCache(),
            new FakePathProvider());

        await viewModel.InitializeAsync();
        await viewModel.ExportFilteredCommand.ExecuteAsync(null);

        Assert.Single(exporter.LastFailureReport);
        Assert.Contains("资源包请求失败", exporter.LastFailureReport[0]);
        Assert.Contains("失败报告", viewModel.ExportResultText);
    }

    [Fact]
    public async Task ViewModel_FiltersFiveThousandResourcesWithoutMaterializingExtraRows()
    {
        var resources = Enumerable.Range(0, 5000)
            .Select(index => new BridgeAnimationResource(
                new BridgeResourceKey(
                    BridgeResourceType.KAnim,
                    BridgeResourceSource.Loaded,
                    $"hero-{index}",
                    $"hero-{index}"),
                "bundle",
                1,
                2,
                3))
            .ToList();
        var snapshot = new BridgeSnapshot(
            "http://bridge/",
            new BridgeStatus(true, "bridge", "1", 17871, true, 5000, 5000),
            resources,
            Array.Empty<BridgeAnimationResource>(),
            Array.Empty<BridgeSpriteResource>(),
            Array.Empty<BridgeSpriteResource>());
        using var viewModel = CreateViewModel(
            new FakeResourceBridgeClient(snapshot),
            new InMemoryStateStore());

        await viewModel.InitializeAsync();
        viewModel.FilterText = "hero-4999";

        Assert.Single(viewModel.FilteredResources);
        Assert.Equal("hero-4999", viewModel.FilteredResources[0].Name);
    }

    [Fact]
    public async Task ViewModel_DisposeCancelsInFlightThumbnailLoads()
    {
        var client = new BlockingThumbnailClient(CreateSnapshot());
        using var viewModel = new OniResourceBridgeViewModel(
            client,
            new FakeExportService(),
            new InMemoryStateStore(),
            new FakeThumbnailCache(),
            new FakePathProvider());

        await viewModel.InitializeAsync();
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        viewModel.Dispose();

        await client.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(client.Canceled.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ViewModel_LimitsThumbnailConcurrencyToEight()
    {
        var snapshot = CreateManyAnimationSnapshot(40);
        var client = new CountingThumbnailClient(snapshot, expectedRequests: 40);
        using var viewModel = new OniResourceBridgeViewModel(
            client,
            new FakeExportService(),
            new InMemoryStateStore(),
            new FakeThumbnailCache(),
            new FakePathProvider());

        await viewModel.InitializeAsync();
        await client.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.InRange(client.MaximumConcurrency, 1, 8);
    }

    private static OniResourceBridgeViewModel CreateViewModel(
        FakeResourceBridgeClient client,
        InMemoryStateStore stateStore,
        FakeExportService? exporter = null)
    {
        return new OniResourceBridgeViewModel(
            client,
            exporter ?? new FakeExportService(),
            stateStore,
            new FakeThumbnailCache(),
            new FakePathProvider());
    }

    private static BridgeSnapshot CreateSnapshot()
    {
        return new BridgeSnapshot(
            "http://bridge/",
            new BridgeStatus(true, "bridge", "1", 17871, true, 1, 1),
            new[]
            {
                new BridgeAnimationResource(
                    new BridgeResourceKey(BridgeResourceType.KAnim, BridgeResourceSource.Loaded, "hero-id", "hero"),
                    "bundle", 1, 2, 3)
            },
            Array.Empty<BridgeAnimationResource>(),
            new[]
            {
                new BridgeSpriteResource(
                    new BridgeResourceKey(BridgeResourceType.Sprite, BridgeResourceSource.Loaded, "sprite-id", "sprite"),
                    null, 32, 32)
            },
            Array.Empty<BridgeSpriteResource>());
    }

    private static BridgeSnapshot CreateManyAnimationSnapshot(int count)
    {
        var resources = Enumerable.Range(0, count)
            .Select(index => new BridgeAnimationResource(
                new BridgeResourceKey(
                    BridgeResourceType.KAnim,
                    BridgeResourceSource.Loaded,
                    $"id-{index}",
                    $"hero-{index}"),
                "bundle",
                1,
                2,
                3))
            .ToList();
        return new BridgeSnapshot(
            "http://bridge/",
            new BridgeStatus(true, "bridge", "1", 17871, true, count, count),
            resources,
            Array.Empty<BridgeAnimationResource>(),
            Array.Empty<BridgeSpriteResource>(),
            Array.Empty<BridgeSpriteResource>());
    }

    private sealed class FakeResourceBridgeClient : IResourceBridgeClient
    {
        private readonly BridgeSnapshot snapshot;

        public FakeResourceBridgeClient(BridgeSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public Task<BridgeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);

        public Task<BridgeKAnimPackage> GetKAnimPackageAsync(string baseUrl, BridgeResourceKey resource, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BridgeKAnimPackage(
                true,
                resource.Name,
                resource.Source,
                Convert.ToBase64String(new byte[] { 1 }),
                Convert.ToBase64String(new byte[] { 2 }),
                new[] { new BridgeTexture(0, resource.Name, 1, 1, Convert.ToBase64String(new byte[] { 3 })) },
                null,
                null));
        }

        public Task<BridgePreview> GetPreviewAsync(string baseUrl, BridgeResourceKey resource, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BridgePreview(true, resource.Name, 1, 1, Convert.ToBase64String(new byte[] { 3 }), null, null));

        public Task<BridgeSpritePackage> GetSpritePackageAsync(string baseUrl, BridgeResourceKey resource, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BridgeSpritePackage(true, resource.Name, resource.Source, Convert.ToBase64String(new byte[] { 3 }), 1, 1, null, null));
    }

    private sealed class InMemoryStateStore : IResourceBridgeStateStore
    {
        public BridgeState State { get; private set; } = BridgeState.Empty;

        public Task<BridgeState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(State);

        public Task SaveAsync(BridgeState state, CancellationToken cancellationToken = default)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExportService : IResourceBridgeExportService
    {
        public BridgeExportRequest? LastRequest { get; private set; }

        public IReadOnlyList<string> LastFailureReport { get; private set; } = Array.Empty<string>();

        public Task<ExportArtifact> ExportAsync(BridgeExportRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ExportArtifact(
                request.Resource.Key,
                "hero.png",
                "hero_anim.bytes",
                "hero_build.bytes"));
        }

        public Task<BatchExportResult> ExportBatchAsync(IEnumerable<BridgeExportRequest> requests, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BatchExportResult(Array.Empty<ExportArtifact>(), Array.Empty<BridgeResourceKey>(), null, false));
        }

        public Task<string?> WriteFailureReportAsync(IEnumerable<string> failures, CancellationToken cancellationToken = default)
        {
            LastFailureReport = failures.ToList();
            return Task.FromResult<string?>(LastFailureReport.Count == 0 ? null : "failure-report.log");
        }
    }

    private sealed class FailingKAnimPackageClient : IResourceBridgeClient
    {
        private readonly FakeResourceBridgeClient inner;

        public FailingKAnimPackageClient(BridgeSnapshot snapshot)
        {
            inner = new FakeResourceBridgeClient(snapshot);
        }

        public Task<BridgeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            inner.GetSnapshotAsync(cancellationToken);

        public Task<BridgeKAnimPackage> GetKAnimPackageAsync(
            string baseUrl,
            BridgeResourceKey resource,
            CancellationToken cancellationToken = default) =>
            Task.FromException<BridgeKAnimPackage>(new InvalidOperationException("资源包请求失败：source runtime 无法解析。"));

        public Task<BridgePreview> GetPreviewAsync(
            string baseUrl,
            BridgeResourceKey resource,
            CancellationToken cancellationToken = default) =>
            inner.GetPreviewAsync(baseUrl, resource, cancellationToken);

        public Task<BridgeSpritePackage> GetSpritePackageAsync(
            string baseUrl,
            BridgeResourceKey resource,
            CancellationToken cancellationToken = default) =>
            inner.GetSpritePackageAsync(baseUrl, resource, cancellationToken);
    }

    private sealed class FakeThumbnailCache : IThumbnailCache
    {
        public string? GetPath(BridgeResourceKey resource) => null;

        public Task<string> SaveAsync(BridgeResourceKey resource, string pngBase64, CancellationToken cancellationToken = default) =>
            Task.FromResult(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png"));
    }

    private sealed class BlockingThumbnailClient : IResourceBridgeClient
    {
        private readonly BridgeSnapshot snapshot;

        public BlockingThumbnailClient(BridgeSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<BridgeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public async Task<BridgePreview> GetPreviewAsync(
            string baseUrl,
            BridgeResourceKey resource,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Canceled.TrySetResult(true);
                throw;
            }

            throw new InvalidOperationException("Thumbnail request should remain pending until cancellation.");
        }

        public Task<BridgeKAnimPackage> GetKAnimPackageAsync(
            string baseUrl,
            BridgeResourceKey resource,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BridgeSpritePackage> GetSpritePackageAsync(
            string baseUrl,
            BridgeResourceKey resource,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CountingThumbnailClient : IResourceBridgeClient
    {
        private readonly BridgeSnapshot snapshot;
        private readonly int expectedRequests;
        private int activeRequests;
        private int completedRequests;
        private int maximumConcurrency;

        public CountingThumbnailClient(BridgeSnapshot snapshot, int expectedRequests)
        {
            this.snapshot = snapshot;
            this.expectedRequests = expectedRequests;
        }

        public int MaximumConcurrency => Volatile.Read(ref maximumConcurrency);

        public TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<BridgeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public async Task<BridgePreview> GetPreviewAsync(
            string baseUrl,
            BridgeResourceKey resource,
            CancellationToken cancellationToken = default)
        {
            int active = Interlocked.Increment(ref activeRequests);
            int previous;
            do
            {
                previous = Volatile.Read(ref maximumConcurrency);
                if (previous >= active)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref maximumConcurrency, active, previous) != previous);

            try
            {
                await Task.Delay(25, cancellationToken);
                return new BridgePreview(true, resource.Name, 1, 1, Convert.ToBase64String(new byte[] { 1 }), null, null);
            }
            finally
            {
                Interlocked.Decrement(ref activeRequests);
                if (Interlocked.Increment(ref completedRequests) == expectedRequests)
                {
                    Completed.TrySetResult(true);
                }
            }
        }

        public Task<BridgeKAnimPackage> GetKAnimPackageAsync(
            string baseUrl,
            BridgeResourceKey resource,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BridgeSpritePackage> GetSpritePackageAsync(
            string baseUrl,
            BridgeResourceKey resource,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakePathProvider : IApplicationPathProvider
    {
        public string StatusFilePath => Path.Combine(Path.GetTempPath(), "status.json");
        public string ResourceBridgeStateFilePath => Path.Combine(Path.GetTempPath(), "state.json");
        public string ResourceBridgeCacheDirectory => Path.GetTempPath();
        public string ResourceBridgeExportDirectory => Path.GetTempPath();
    }
}
