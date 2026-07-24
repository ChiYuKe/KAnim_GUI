using KAnimGui.Application.Conversion;
using KAnimGui.Infrastructure.Platform;
using KAnimGui.Presentation.Conversion;

namespace KAnimGui.Tests;

public sealed class ConversionWorkspaceViewModelTests
{
    [Fact]
    public void Constructor_UsesDefaultOutputDirectories()
    {
        using var viewModel = new ConversionWorkspaceViewModel(
            new FakeConversionService(),
            new FakeInputFilePreparer(),
            new LocalFileSystemGateway());

        Assert.Equal(ConversionOutputPathResolver.KanimToScmlDirectory, viewModel.KanimOutputDirectory);
        Assert.Equal(ConversionOutputPathResolver.ScmlToKanimDirectory, viewModel.ScmlOutputDirectory);
    }

    [Fact]
    public async Task ViewModel_BuildsTypedKanimRequestAndPublishesLog()
    {
        string root = Path.Combine(Path.GetTempPath(), "KAnimGuiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string png = Path.Combine(root, "hero.png");
            string anim = Path.Combine(root, "hero_anim.bytes");
            string build = Path.Combine(root, "hero_build.bytes");
            await File.WriteAllBytesAsync(png, new byte[] { 1 });
            await File.WriteAllBytesAsync(anim, new byte[] { 2 });
            await File.WriteAllBytesAsync(build, new byte[] { 3 });
            var service = new FakeConversionService();
            using var viewModel = new ConversionWorkspaceViewModel(service, new FakeInputFilePreparer(), new LocalFileSystemGateway())
            {
                PngPath = png,
                AnimPath = anim,
                BuildPath = build,
                KanimOutputDirectory = root,
                StrictMode = true,
                StrictOrder = true
            };

            await viewModel.ConvertKanimCommand.ExecuteAsync(null);
            await Task.Delay(50);

            var request = Assert.IsType<KanimToScmlRequest>(service.LastRequest);
            Assert.True(request.StrictMode);
            Assert.True(request.StrictOrder);
            Assert.Contains("转换完成", viewModel.KanimLog);
            Assert.Equal("转换成功", viewModel.KanimStatus);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task ViewModel_BatchUsesFileMatcherAndReportsResults()
    {
        string root = Path.Combine(Path.GetTempPath(), "KAnimGuiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "hero.png"), new byte[] { 1 });
            await File.WriteAllBytesAsync(Path.Combine(root, "hero_anim.bytes"), new byte[] { 2 });
            await File.WriteAllBytesAsync(Path.Combine(root, "hero_build.bytes"), new byte[] { 3 });
            var service = new FakeConversionService();
            using var viewModel = new ConversionWorkspaceViewModel(service, new FakeInputFilePreparer(), new LocalFileSystemGateway())
            {
                KanimOutputDirectory = root
            };

            ConversionBatchResult? result = await viewModel.ConvertKanimBatchAsync(root);

            Assert.NotNull(result);
            Assert.Single(result!.Results);
            Assert.Single(service.BatchRequests);
            Assert.IsType<KanimToScmlRequest>(service.BatchRequests[0]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class FakeConversionService : IConversionService
    {
        public ConversionRequest? LastRequest { get; private set; }
        public List<ConversionRequest> BatchRequests { get; } = [];

        public Task<ConversionExecutionResult> ConvertAsync(
            ConversionRequest request,
            IProgress<OperationEvent>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            progress?.Report(new OperationEvent("test", "转换完成", Stage: "conversion"));
            return Task.FromResult(new ConversionExecutionResult(true, request.OutputDirectory, null, false, false));
        }

        public Task<ConversionBatchResult> ConvertBatchAsync(
            IEnumerable<ConversionRequest> requests,
            IProgress<ConversionBatchProgress>? batchProgress = null,
            IProgress<OperationEvent>? progress = null,
            CancellationToken cancellationToken = default)
        {
            BatchRequests.Clear();
            BatchRequests.AddRange(requests);
            var results = requests
                .Select(request => new ConversionExecutionResult(true, request.OutputDirectory, null, false, false))
                .ToList();
            return Task.FromResult(new ConversionBatchResult(results, false));
        }
    }

    private sealed class FakeInputFilePreparer : IInputFilePreparer
    {
        public Task<string> PrepareBytesAsync(string path, bool allowTxt, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(path);
        }
    }
}
