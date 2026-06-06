using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KAnimGui.Core
{
    public sealed class OniResourceBridgeClient
    {
        private const int DefaultPort = 17871;
        private const int MaxPort = 17890;
        private static readonly TimeSpan SnapshotProbeTimeout = TimeSpan.FromSeconds(10);
        private static readonly string StatusFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KAnimGui",
            "ONIResourceBridge.json");

        public async Task<OniBridgeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            var candidates = GetCandidateUrls().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Exception? lastError = null;
            using var overallTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            overallTimeout.CancelAfter(SnapshotProbeTimeout);

            foreach (var baseUrl in candidates)
            {
                if (overallTimeout.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(overallTimeout.Token);
                    timeout.CancelAfter(TimeSpan.FromMilliseconds(600));

                    using var http = new HttpClient
                    {
                        BaseAddress = new Uri(baseUrl),
                        Timeout = TimeSpan.FromMilliseconds(800)
                    };

                    var status = await ReadJsonAsync<OniBridgeStatus>(http, "status", timeout.Token).ConfigureAwait(false);
                    var anims = await ReadJsonAsync<OniBridgeAnimList>(http, "assets/anims", timeout.Token).ConfigureAwait(false);
                    var offlineAnims = await ReadJsonAsync<OniBridgeAnimList>(http, "assets/offline-anims", timeout.Token).ConfigureAwait(false);
                    var sprites = await ReadJsonAsync<OniBridgeSpriteList>(http, "assets/sprites", timeout.Token).ConfigureAwait(false);
                    var offlineSprites = await ReadJsonAsync<OniBridgeSpriteList>(http, "assets/offline-sprites", timeout.Token).ConfigureAwait(false);

                    return new OniBridgeSnapshot(
                        baseUrl,
                        status,
                        MarkAnimSource(anims.Items, "loaded"),
                        MarkAnimSource(offlineAnims.Items, "offline"),
                        MarkSpriteSource(sprites.Items, "loaded"),
                        MarkSpriteSource(offlineSprites.Items, "offline"));
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException)
                {
                    lastError = ex;
                }
            }

            throw new InvalidOperationException(BuildConnectionFailureMessage(lastError), lastError);
        }

        public async Task<OniBridgeKAnimPackage> GetKAnimPackageAsync(string baseUrl, string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("资源桥地址为空。", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("动画名称为空。", nameof(name));
            }

            using var http = new HttpClient
            {
                BaseAddress = new Uri(EnsureTrailingSlash(baseUrl)),
                Timeout = TimeSpan.FromSeconds(15)
            };

            string path = "assets/kanim?name=" + Uri.EscapeDataString(name);
            var package = await ReadJsonAsync<OniBridgeKAnimPackage>(http, path, cancellationToken).ConfigureAwait(false);
            if (!package.Ok)
            {
                string detail = string.IsNullOrWhiteSpace(package.Detail) ? string.Empty : $" ({package.Detail})";
                throw new InvalidOperationException($"游戏资源桥返回失败：{package.Error ?? "未知错误"}{detail}");
            }

            return package;
        }

        public async Task<OniBridgeKAnimPackage> GetOfflineKAnimPackageAsync(string baseUrl, string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("资源桥地址为空。", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("离线资源 ID 为空。", nameof(id));
            }

            using var http = new HttpClient
            {
                BaseAddress = new Uri(EnsureTrailingSlash(baseUrl)),
                Timeout = TimeSpan.FromSeconds(30)
            };

            string path = "assets/offline-kanim?id=" + Uri.EscapeDataString(id);
            var package = await ReadJsonAsync<OniBridgeKAnimPackage>(http, path, cancellationToken).ConfigureAwait(false);
            if (!package.Ok)
            {
                string detail = string.IsNullOrWhiteSpace(package.Detail) ? string.Empty : $" ({package.Detail})";
                throw new InvalidOperationException($"离线资源桥返回失败：{package.Error ?? "未知错误"}{detail}");
            }

            return package;
        }

        public async Task<OniBridgeSpritePackage> GetSpritePackageAsync(string baseUrl, string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("资源桥地址为空。", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Sprite ID 为空。", nameof(id));
            }

            using var http = new HttpClient
            {
                BaseAddress = new Uri(EnsureTrailingSlash(baseUrl)),
                Timeout = TimeSpan.FromSeconds(20)
            };

            string path = "assets/sprite?id=" + Uri.EscapeDataString(id);
            var package = await ReadJsonAsync<OniBridgeSpritePackage>(http, path, cancellationToken).ConfigureAwait(false);
            if (!package.Ok)
            {
                string detail = string.IsNullOrWhiteSpace(package.Detail) ? string.Empty : $" ({package.Detail})";
                throw new InvalidOperationException($"Sprite 资源桥返回失败：{package.Error ?? "未知错误"}{detail}");
            }

            return package;
        }

        public async Task<OniBridgeSpritePackage> GetOfflineSpritePackageAsync(string baseUrl, string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("资源桥地址为空。", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("离线 Sprite ID 为空。", nameof(id));
            }

            using var http = new HttpClient
            {
                BaseAddress = new Uri(EnsureTrailingSlash(baseUrl)),
                Timeout = TimeSpan.FromSeconds(30)
            };

            string path = "assets/offline-sprite?id=" + Uri.EscapeDataString(id);
            var package = await ReadJsonAsync<OniBridgeSpritePackage>(http, path, cancellationToken).ConfigureAwait(false);
            if (!package.Ok)
            {
                string detail = string.IsNullOrWhiteSpace(package.Detail) ? string.Empty : $" ({package.Detail})";
                throw new InvalidOperationException($"离线 Sprite 资源桥返回失败：{package.Error ?? "未知错误"}{detail}");
            }

            return package;
        }

        public async Task<OniBridgePreview> GetPreviewAsync(string baseUrl, string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("资源桥地址为空。", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("动画名称为空。", nameof(name));
            }

            using var http = new HttpClient
            {
                BaseAddress = new Uri(EnsureTrailingSlash(baseUrl)),
                Timeout = TimeSpan.FromSeconds(8)
            };

            string path = "assets/preview?name=" + Uri.EscapeDataString(name);
            var preview = await ReadJsonAsync<OniBridgePreview>(http, path, cancellationToken).ConfigureAwait(false);
            if (!preview.Ok)
            {
                string detail = string.IsNullOrWhiteSpace(preview.Detail) ? string.Empty : $" ({preview.Detail})";
                throw new InvalidOperationException($"资源桥缩略图返回失败：{preview.Error ?? "未知错误"}{detail}");
            }

            return preview;
        }

        public async Task<OniBridgePreview> GetOfflinePreviewAsync(string baseUrl, string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("资源桥地址为空。", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("离线资源 ID 为空。", nameof(id));
            }

            using var http = new HttpClient
            {
                BaseAddress = new Uri(EnsureTrailingSlash(baseUrl)),
                Timeout = TimeSpan.FromSeconds(20)
            };

            string path = "assets/offline-preview?id=" + Uri.EscapeDataString(id);
            var preview = await ReadJsonAsync<OniBridgePreview>(http, path, cancellationToken).ConfigureAwait(false);
            if (!preview.Ok)
            {
                string detail = string.IsNullOrWhiteSpace(preview.Detail) ? string.Empty : $" ({preview.Detail})";
                throw new InvalidOperationException($"离线资源缩略图返回失败：{preview.Error ?? "未知错误"}{detail}");
            }

            return preview;
        }

        private static async Task<T> ReadJsonAsync<T>(HttpClient http, string path, CancellationToken cancellationToken)
        {
            using var response = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return result ?? throw new JsonException($"Empty response for {path}");
        }

        private static List<OniBridgeAnimInfo> MarkAnimSource(IEnumerable<OniBridgeAnimInfo> items, string source)
        {
            return items.Select(item => item with { Source = source }).ToList();
        }

        private static List<OniBridgeSpriteInfo> MarkSpriteSource(IEnumerable<OniBridgeSpriteInfo> items, string source)
        {
            return items.Select(item => item with { Source = source }).ToList();
        }

        private static IEnumerable<string> GetCandidateUrls()
        {
            var statusUrl = TryReadStatusFileUrl();
            if (!string.IsNullOrWhiteSpace(statusUrl))
            {
                yield return EnsureTrailingSlash(statusUrl);
            }

            for (int port = DefaultPort; port <= MaxPort; port++)
            {
                yield return $"http://127.0.0.1:{port}/";
            }
        }

        private static string BuildConnectionFailureMessage(Exception? lastError)
        {
            var occupiedPorts = GetOccupiedBridgePorts().ToList();
            string statusFileText = File.Exists(StatusFilePath)
                ? $"检测到资源桥状态文件：{StatusFilePath}"
                : $"没有检测到资源桥状态文件：{StatusFilePath}";

            string portText = occupiedPorts.Count > 0
                ? $"检测到端口 {string.Join(", ", occupiedPorts)} 正在被占用，但没有返回 ONI Resource Bridge 数据。"
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

        private static IEnumerable<int> GetOccupiedBridgePorts()
        {
            try
            {
                var listeners = IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Where(endpoint => IPAddress.IsLoopback(endpoint.Address) || endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.IPv6Any))
                    .Select(endpoint => endpoint.Port)
                    .ToHashSet();

                for (int port = DefaultPort; port <= MaxPort; port++)
                {
                    if (listeners.Contains(port))
                    {
                        yield return port;
                    }
                }
            }
            finally
            {
            }
        }

        private static string? TryReadStatusFileUrl()
        {
            try
            {
                if (!File.Exists(StatusFilePath))
                {
                    return null;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(StatusFilePath));
                if (document.RootElement.TryGetProperty("url", out var urlElement))
                {
                    return urlElement.GetString();
                }

                if (document.RootElement.TryGetProperty("port", out var portElement) && portElement.TryGetInt32(out int port))
                {
                    return $"http://127.0.0.1:{port}/";
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public sealed record OniBridgeSnapshot(
        string BaseUrl,
        OniBridgeStatus Status,
        IReadOnlyList<OniBridgeAnimInfo> Anims,
        IReadOnlyList<OniBridgeAnimInfo> OfflineAnims,
        IReadOnlyList<OniBridgeSpriteInfo> Sprites,
        IReadOnlyList<OniBridgeSpriteInfo> OfflineSprites);

    public sealed record OniBridgeStatus(
        bool Ok,
        string Mod,
        string Version,
        int Port,
        bool AssetsReady,
        int AnimCount,
        int ResourcePackageCount);

    public sealed record OniBridgeAnimList(bool Ok, List<OniBridgeAnimInfo> Items);

    public sealed record OniBridgeAnimInfo(
        string Id,
        string Name,
        string Source,
        string? Bundle,
        int AnimCount,
        int FrameCount,
        int ElementCount);

    public sealed record OniBridgeSpriteList(bool Ok, List<OniBridgeSpriteInfo> Items);

    public sealed record OniBridgeSpriteInfo(
        string Id,
        string Name,
        string Source,
        string? Bundle,
        int Width,
        int Height);

    public sealed record OniBridgeKAnimPackage(
        bool Ok,
        string Name,
        string? Source,
        string AnimBytes,
        string BuildBytes,
        List<OniBridgeTextureInfo> Textures,
        string? Error,
        string? Detail);

    public sealed record OniBridgeTextureInfo(
        int Index,
        string Name,
        int Width,
        int Height,
        string PngBytes);

    public sealed record OniBridgePreview(
        bool Ok,
        string Name,
        int Width,
        int Height,
        string PngBytes,
        string? Error,
        string? Detail);

    public sealed record OniBridgeSpritePackage(
        bool Ok,
        string Name,
        string? Source,
        string PngBytes,
        int Width,
        int Height,
        string? Error,
        string? Detail);

}
