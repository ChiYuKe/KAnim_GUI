using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using KAnimGui.Application.ResourceBridge;

namespace KAnimGui.Infrastructure.ResourceBridge;

public sealed class OniResourceBridgeHttpClient : IResourceBridgeClient
{
    private const int DefaultPort = 17871;
    private const int MaxPort = 17890;
    private static readonly TimeSpan SnapshotProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CandidateTimeout = TimeSpan.FromMilliseconds(600);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient httpClient;
    private readonly IApplicationPathProvider paths;

    public OniResourceBridgeHttpClient(HttpClient httpClient, IApplicationPathProvider paths)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<BridgeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var overallTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overallTimeout.CancelAfter(SnapshotProbeTimeout);

        Exception? lastError = null;
        foreach (string baseUrl in GetCandidateUrls())
        {
            overallTimeout.Token.ThrowIfCancellationRequested();

            using var candidateTimeout = CancellationTokenSource.CreateLinkedTokenSource(overallTimeout.Token);
            candidateTimeout.CancelAfter(CandidateTimeout);

            try
            {
                BridgeStatusDto status = await GetJsonAsync<BridgeStatusDto>(
                    baseUrl,
                    "status",
                    candidateTimeout.Token).ConfigureAwait(false);
                AnimationListDto animations = await GetJsonAsync<AnimationListDto>(
                    baseUrl,
                    "assets/anims",
                    candidateTimeout.Token).ConfigureAwait(false);
                AnimationListDto offlineAnimations = await GetJsonAsync<AnimationListDto>(
                    baseUrl,
                    "assets/offline-anims",
                    candidateTimeout.Token).ConfigureAwait(false);
                SpriteListDto sprites = await GetJsonAsync<SpriteListDto>(
                    baseUrl,
                    "assets/sprites",
                    candidateTimeout.Token).ConfigureAwait(false);
                SpriteListDto offlineSprites = await GetJsonAsync<SpriteListDto>(
                    baseUrl,
                    "assets/offline-sprites",
                    candidateTimeout.Token).ConfigureAwait(false);

                return new BridgeSnapshot(
                    baseUrl,
                    ToStatus(status),
                    animations.Items.Select(item => ToAnimation(item, BridgeResourceSource.Loaded)).ToList(),
                    offlineAnimations.Items.Select(item => ToAnimation(item, BridgeResourceSource.Offline)).ToList(),
                    sprites.Items.Select(item => ToSprite(item, BridgeResourceSource.Loaded)).ToList(),
                    offlineSprites.Items.Select(item => ToSprite(item, BridgeResourceSource.Offline)).ToList());
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException(BuildConnectionFailureMessage(lastError), lastError);
    }

    public Task<BridgeKAnimPackage> GetKAnimPackageAsync(
        string baseUrl,
        BridgeResourceKey resource,
        CancellationToken cancellationToken = default)
    {
        ValidateResource(baseUrl, resource, BridgeResourceType.KAnim);
        string path = resource.Source == BridgeResourceSource.Offline
            ? "assets/offline-kanim?id=" + Uri.EscapeDataString(resource.Id)
            : "assets/kanim?name=" + Uri.EscapeDataString(resource.Name);

        return GetPackageAsync(baseUrl, path, cancellationToken);
    }

    public Task<BridgePreview> GetPreviewAsync(
        string baseUrl,
        BridgeResourceKey resource,
        CancellationToken cancellationToken = default)
    {
        ValidateResource(baseUrl, resource, BridgeResourceType.KAnim);
        string path = resource.Source == BridgeResourceSource.Offline
            ? "assets/offline-preview?id=" + Uri.EscapeDataString(resource.Id)
            : "assets/preview?name=" + Uri.EscapeDataString(resource.Name);

        return GetJsonAsync<BridgePreview>(baseUrl, path, cancellationToken);
    }

    public async Task<BridgeSpritePackage> GetSpritePackageAsync(
        string baseUrl,
        BridgeResourceKey resource,
        CancellationToken cancellationToken = default)
    {
        ValidateResource(baseUrl, resource, BridgeResourceType.Sprite);
        string path = resource.Source == BridgeResourceSource.Offline
            ? "assets/offline-sprite?id=" + Uri.EscapeDataString(resource.Id)
            : "assets/sprite?id=" + Uri.EscapeDataString(resource.Id);
        return await GetJsonAsync<BridgeSpritePackage>(baseUrl, path, cancellationToken).ConfigureAwait(false);
    }

    private async Task<BridgeKAnimPackage> GetPackageAsync(
        string baseUrl,
        string path,
        CancellationToken cancellationToken)
    {
        var package = await GetJsonAsync<BridgeKAnimPackage>(baseUrl, path, cancellationToken).ConfigureAwait(false);
        if (!package.Ok)
        {
            string detail = string.IsNullOrWhiteSpace(package.Detail) ? string.Empty : $" ({package.Detail})";
            throw new InvalidOperationException($"游戏资源桥返回失败：{package.Error ?? "未知错误"}{detail}");
        }

        return package;
    }

    private async Task<T> GetJsonAsync<T>(
        string baseUrl,
        string path,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(new Uri(EnsureTrailingSlash(baseUrl)), path));
        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        T? result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new JsonException($"资源桥返回空响应：{path}");
    }

    private IEnumerable<string> GetCandidateUrls()
    {
        string? statusUrl = TryReadStatusFileUrl();
        if (!string.IsNullOrWhiteSpace(statusUrl))
        {
            yield return EnsureTrailingSlash(statusUrl);
        }

        for (int port = DefaultPort; port <= MaxPort; port++)
        {
            yield return $"http://127.0.0.1:{port}/";
        }
    }

    private string? TryReadStatusFileUrl()
    {
        try
        {
            if (!File.Exists(paths.StatusFilePath))
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(paths.StatusFilePath));
            if (document.RootElement.TryGetProperty("url", out JsonElement urlElement))
            {
                return urlElement.GetString();
            }

            if (document.RootElement.TryGetProperty("port", out JsonElement portElement) &&
                portElement.TryGetInt32(out int port))
            {
                return $"http://127.0.0.1:{port}/";
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // A stale or partially-written status file should not prevent port probing.
        }

        return null;
    }

    private string BuildConnectionFailureMessage(Exception? lastError)
    {
        string statusFileText = File.Exists(paths.StatusFilePath)
            ? $"检测到资源桥状态文件：{paths.StatusFilePath}"
            : $"没有检测到资源桥状态文件：{paths.StatusFilePath}";
        IReadOnlySet<int> occupiedPorts = GetOccupiedPorts();
        string portText = occupiedPorts.Count > 0
            ? $"检测到端口 {string.Join(", ", occupiedPorts.Order())} 正在被占用，但没有返回 ONI Resource Bridge 数据。"
            : $"没有检测到 {DefaultPort}-{MaxPort} 端口上有资源桥服务。";
        string likelyReason = occupiedPorts.Count > 0
            ? "可能是端口被其它程序占用，或者资源桥模组启动失败。"
            : "可能是缺氧没有启动，或者缺氧 Mods 目录里没有安装/启用 ONI Resource Bridge 模组。";
        string detail = lastError == null ? string.Empty : $"\n\n最后一次连接错误：{lastError.Message}";

        return "没有连接到 ONI Resource Bridge。\n\n" +
            statusFileText + "\n" +
            portText + "\n\n" +
            likelyReason + "\n\n" +
            "建议：\n" +
            "1. 确认缺氧正在运行。\n" +
            "2. 确认 ONI Resource Bridge 模组已放入缺氧 Mods 目录并已启用。\n" +
            $"3. 如果提示端口被占用，请关闭占用 {DefaultPort}-{MaxPort} 的其它程序后重启游戏。" +
            detail;
    }

    private static IReadOnlySet<int> GetOccupiedPorts()
    {
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Where(endpoint => IPAddress.IsLoopback(endpoint.Address) ||
                    endpoint.Address.Equals(IPAddress.Any) ||
                    endpoint.Address.Equals(IPAddress.IPv6Any))
                .Select(endpoint => endpoint.Port)
                .ToHashSet();

            return Enumerable.Range(DefaultPort, MaxPort - DefaultPort + 1)
                .Where(listeners.Contains)
                .ToHashSet();
        }
        catch (NetworkInformationException)
        {
            return new HashSet<int>();
        }
    }

    private static BridgeStatus ToStatus(BridgeStatusDto source) => new(
        source.Ok,
        source.Mod,
        source.Version,
        source.Port,
        source.AssetsReady,
        source.AnimCount,
        source.ResourcePackageCount);

    private static BridgeAnimationResource ToAnimation(AnimationInfoDto source, BridgeResourceSource resourceSource) =>
        new(
            new BridgeResourceKey(BridgeResourceType.KAnim, resourceSource, source.Id, source.Name),
            source.Bundle,
            source.AnimCount,
            source.FrameCount,
            source.ElementCount);

    private static BridgeSpriteResource ToSprite(SpriteInfoDto source, BridgeResourceSource resourceSource) =>
        new(
            new BridgeResourceKey(BridgeResourceType.Sprite, resourceSource, source.Id, source.Name),
            source.Bundle,
            source.Width,
            source.Height);

    private static void ValidateResource(string baseUrl, BridgeResourceKey resource, BridgeResourceType expectedType)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("资源桥地址为空。", nameof(baseUrl));
        }

        if (resource.Type != expectedType)
        {
            throw new ArgumentException($"资源类型必须是 {expectedType}。", nameof(resource));
        }

        if (string.IsNullOrWhiteSpace(resource.Id) && string.IsNullOrWhiteSpace(resource.Name))
        {
            throw new ArgumentException("资源 ID 和名称不能同时为空。", nameof(resource));
        }
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";

    private sealed record BridgeStatusDto(
        bool Ok,
        string Mod,
        string Version,
        int Port,
        bool AssetsReady,
        int AnimCount,
        int ResourcePackageCount);

    private sealed record AnimationListDto(bool Ok, List<AnimationInfoDto> Items);

    private sealed record AnimationInfoDto(
        string Id,
        string Name,
        string? Bundle,
        int AnimCount,
        int FrameCount,
        int ElementCount);

    private sealed record SpriteListDto(bool Ok, List<SpriteInfoDto> Items);

    private sealed record SpriteInfoDto(
        string Id,
        string Name,
        string? Bundle,
        int Width,
        int Height);
}
