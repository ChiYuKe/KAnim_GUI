using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KAnimGui.Application.ResourceBridge;
using KAnimGui.Infrastructure.ResourceBridge;

namespace KAnimGui.Tests;

public sealed class ResourceBridgeInfrastructureTests
{
    [Fact]
    public async Task StateStore_RoundTripsExportLayout()
    {
        string root = CreateTempDirectory();
        try
        {
            var paths = new LocalApplicationPathProvider(root, root);
            var store = new JsonResourceBridgeStateStore(paths);
            var state = new BridgeState(2, BridgeExportLayout.Split);

            await store.SaveAsync(state);
            BridgeState loaded = await store.LoadAsync();

            Assert.Equal(BridgeExportLayout.Split, loaded.ExportLayout);
            Assert.Equal(2, loaded.SchemaVersion);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task StateStore_QuarantinesCorruptJson()
    {
        string root = CreateTempDirectory();
        try
        {
            var paths = new LocalApplicationPathProvider(root, root);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.ResourceBridgeStateFilePath)!);
            await File.WriteAllTextAsync(paths.ResourceBridgeStateFilePath, "{not-json");
            var store = new JsonResourceBridgeStateStore(paths);

            BridgeState state = await store.LoadAsync();

            Assert.Equal(BridgeState.Empty.ExportLayout, state.ExportLayout);
            Assert.Equal(2, state.SchemaVersion);
            Assert.False(File.Exists(paths.ResourceBridgeStateFilePath));
            Assert.NotEmpty(Directory.GetFiles(
                Path.GetDirectoryName(paths.ResourceBridgeStateFilePath)!,
                "ResourceBridgeState.json.corrupt-*.json"));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ExportService_WritesGroupedKAnimPackage()
    {
        string root = CreateTempDirectory();
        try
        {
            var paths = new LocalApplicationPathProvider(root, root);
            var exporter = new FileResourceBridgeExportService(paths);
            var resource = new BridgeAnimationResource(
                new BridgeResourceKey(BridgeResourceType.KAnim, BridgeResourceSource.Loaded, "id", "hero/unit"),
                null,
                1,
                2,
                3);
            var package = new BridgeKAnimPackage(
                true,
                resource.Key.Name,
                BridgeResourceSource.Loaded,
                Convert.ToBase64String(Encoding.UTF8.GetBytes("anim")),
                Convert.ToBase64String(Encoding.UTF8.GetBytes("build")),
                new[]
                {
                    new BridgeTexture(0, "hero", 1, 1, Convert.ToBase64String(new byte[] { 1, 2, 3 }))
                },
                null,
                null);

            ExportArtifact artifact = await exporter.ExportAsync(
                new BridgeExportRequest(resource, package, Layout: BridgeExportLayout.Grouped));

            Assert.NotNull(artifact.PngPath);
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(artifact.PngPath!));
            Assert.Equal("anim", await File.ReadAllTextAsync(artifact.AnimPath!));
            Assert.Equal("build", await File.ReadAllTextAsync(artifact.BuildPath!));
            Assert.DoesNotContain("/", Path.GetFileName(artifact.PngPath!));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ExportBatch_ContinuesAfterInvalidResourceAndWritesReport()
    {
        string root = CreateTempDirectory();
        try
        {
            var paths = new LocalApplicationPathProvider(root, root);
            var exporter = new FileResourceBridgeExportService(paths);
            var goodResource = new BridgeSpriteResource(
                new BridgeResourceKey(BridgeResourceType.Sprite, BridgeResourceSource.Loaded, "good", "good"),
                null,
                2,
                3);
            var badResource = goodResource with
            {
                Key = new BridgeResourceKey(BridgeResourceType.Sprite, BridgeResourceSource.Loaded, "bad", "bad")
            };

            BatchExportResult result = await exporter.ExportBatchAsync(new[]
            {
                new BridgeExportRequest(
                    goodResource,
                    SpritePackage: new BridgeSpritePackage(true, "good", BridgeResourceSource.Loaded, Convert.ToBase64String(new byte[] { 9 }), 2, 3, null, null)),
                new BridgeExportRequest(
                    badResource,
                    SpritePackage: new BridgeSpritePackage(true, "bad", BridgeResourceSource.Loaded, "not-base64", 2, 3, null, null))
            });

            Assert.Single(result.Succeeded);
            Assert.Single(result.Failed);
            Assert.NotNull(result.FailureReportPath);
            Assert.Contains("bad", await File.ReadAllTextAsync(result.FailureReportPath!));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task HttpClient_UsesStatusFileAndEscapesResourceQuery()
    {
        string root = CreateTempDirectory();
        try
        {
            var paths = new LocalApplicationPathProvider(root, root);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.StatusFilePath)!);
            await File.WriteAllTextAsync(
                paths.StatusFilePath,
                JsonSerializer.Serialize(new { url = "http://resource-bridge/" }));
            var requestedUris = new List<string>();
            using var http = new HttpClient(new StubHttpMessageHandler(request =>
            {
                requestedUris.Add(request.RequestUri!.OriginalString);
                return JsonResponse(request.RequestUri.AbsolutePath switch
                {
                    "/status" => "{\"ok\":true,\"mod\":\"bridge\",\"version\":\"1\",\"port\":17871,\"assetsReady\":true,\"animCount\":1,\"resourcePackageCount\":1}",
                    "/assets/anims" => "{\"ok\":true,\"items\":[{\"id\":\"loaded-id\",\"name\":\"loaded\",\"bundle\":null,\"animCount\":1,\"frameCount\":2,\"elementCount\":3}]}",
                    "/assets/offline-anims" => "{\"ok\":true,\"items\":[]}",
                    "/assets/sprites" => "{\"ok\":true,\"items\":[]}",
                    "/assets/offline-sprites" => "{\"ok\":true,\"items\":[]}",
                    "/assets/kanim" => "{\"ok\":true,\"name\":\"hero unit\",\"source\":\"runtime\",\"animBytes\":\"YQ==\",\"buildBytes\":\"Yg==\",\"textures\":[],\"error\":null,\"detail\":null}",
                    _ => "{}"
                });
            }));
            var client = new OniResourceBridgeHttpClient(http, paths);

            BridgeSnapshot snapshot = await client.GetSnapshotAsync();
            BridgeResourceKey resource = snapshot.Animations[0].Key with { Name = "hero unit" };
            BridgeKAnimPackage package = await client.GetKAnimPackageAsync(snapshot.BaseUrl, resource);

            Assert.Equal("http://resource-bridge/status", requestedUris[0]);
            Assert.Contains("hero%20unit", requestedUris[^1]);
            Assert.Single(snapshot.Animations);
            Assert.Equal(BridgeResourceSource.Runtime, package.Source);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task HttpClient_ProbesPortsInAscendingOrderAfterStatusFileMiss()
    {
        string root = CreateTempDirectory();
        try
        {
            var paths = new LocalApplicationPathProvider(root, root);
            var requestedUris = new List<string>();
            using var http = new HttpClient(new StubHttpMessageHandler(request =>
            {
                requestedUris.Add(request.RequestUri!.OriginalString);
                return request.RequestUri.Port == 17872
                    ? JsonResponse(request.RequestUri.AbsolutePath switch
                    {
                        "/status" => "{\"ok\":true,\"mod\":\"bridge\",\"version\":\"1\",\"port\":17872,\"assetsReady\":true,\"animCount\":0,\"resourcePackageCount\":0}",
                        "/assets/anims" => "{\"ok\":true,\"items\":[]}",
                        "/assets/offline-anims" => "{\"ok\":true,\"items\":[]}",
                        "/assets/sprites" => "{\"ok\":true,\"items\":[]}",
                        "/assets/offline-sprites" => "{\"ok\":true,\"items\":[]}",
                        _ => "{}"
                    })
                    : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }));

            BridgeSnapshot snapshot = await new OniResourceBridgeHttpClient(http, paths).GetSnapshotAsync();

            Assert.Equal(17872, snapshot.Status.Port);
            Assert.Equal("http://127.0.0.1:17871/status", requestedUris[0]);
            Assert.Equal("http://127.0.0.1:17872/status", requestedUris[1]);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task HttpClient_TranslatesNonSuccessResponsesToConnectionFailure()
    {
        string root = CreateTempDirectory();
        try
        {
            var paths = new LocalApplicationPathProvider(root, root);
            using var http = new HttpClient(new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.BadGateway)));

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new OniResourceBridgeHttpClient(http, paths).GetSnapshotAsync());

            Assert.Contains("没有连接到 ONI Resource Bridge", exception.Message);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task HttpClient_TranslatesEmptyAndMalformedJsonResponses()
    {
        string root = CreateTempDirectory();
        try
        {
            var paths = new LocalApplicationPathProvider(root, root);
            using var emptyHttp = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(string.Empty)));
            InvalidOperationException empty = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new OniResourceBridgeHttpClient(emptyHttp, paths).GetSnapshotAsync());
            Assert.Contains("没有连接到 ONI Resource Bridge", empty.Message);

            using var malformedHttp = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse("{not-json")));
            InvalidOperationException malformed = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new OniResourceBridgeHttpClient(malformedHttp, paths).GetSnapshotAsync());
            Assert.Contains("没有连接到 ONI Resource Bridge", malformed.Message);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task HttpClient_HonorsCallerCancellationDuringProbe()
    {
        string root = CreateTempDirectory();
        try
        {
            var paths = new LocalApplicationPathProvider(root, root);
            var handler = new BlockingHttpMessageHandler();
            using var http = new HttpClient(handler);
            using var cancellation = new CancellationTokenSource();
            Task<BridgeSnapshot> request = new OniResourceBridgeHttpClient(http, paths)
                .GetSnapshotAsync(cancellation.Token);

            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            Assert.True(handler.Canceled.Task.IsCompletedSuccessfully);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "KAnimGuiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responder(request));
        }
    }

    private sealed class BlockingHttpMessageHandler : HttpMessageHandler
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
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

            throw new InvalidOperationException("Request should remain pending until cancellation.");
        }
    }
}
