using KAnimGui.Application.ResourceBridge;

namespace KAnimGui.Infrastructure.ResourceBridge;

public sealed class FileResourceBridgeExportService : IResourceBridgeExportService
{
    private readonly IApplicationPathProvider paths;

    public FileResourceBridgeExportService(IApplicationPathProvider paths)
    {
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task<ExportArtifact> ExportAsync(
        BridgeExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return request.Resource.Key.Type switch
        {
            BridgeResourceType.KAnim => await ExportKAnimAsync(request, cancellationToken).ConfigureAwait(false),
            BridgeResourceType.Sprite => await ExportSpriteAsync(request, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("不支持的资源类型。")
        };
    }

    public async Task<BatchExportResult> ExportBatchAsync(
        IEnumerable<BridgeExportRequest> requests,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        List<BridgeExportRequest> requestList = requests.ToList();
        List<ExportArtifact> succeeded = [];
        List<BridgeResourceKey> failed = [];
        List<string> failures = [];

        for (int index = 0; index < requestList.Count; index++)
        {
            BridgeExportRequest request = requestList[index];
            try
            {
                ExportArtifact artifact = await ExportAsync(request, cancellationToken).ConfigureAwait(false);
                succeeded.Add(artifact);
                progress?.Report(new BatchProgress(index + 1, requestList.Count, request.Resource.Key.Name, true, null));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new BatchExportResult(succeeded, failed, await WriteFailureReportAsync(failures, cancellationToken).ConfigureAwait(false), true);
            }
            catch (Exception ex)
            {
                failed.Add(request.Resource.Key);
                failures.Add($"{request.Resource.Key.Name}: {ex.Message}");
                progress?.Report(new BatchProgress(index + 1, requestList.Count, request.Resource.Key.Name, false, ex.Message));
            }
        }

        string? reportPath = failures.Count == 0
            ? null
            : await WriteFailureReportAsync(failures, cancellationToken).ConfigureAwait(false);
        return new BatchExportResult(succeeded, failed, reportPath, false);
    }

    private async Task<ExportArtifact> ExportKAnimAsync(
        BridgeExportRequest request,
        CancellationToken cancellationToken)
    {
        BridgeKAnimPackage package = request.KAnimPackage ??
            throw new InvalidOperationException("KAnim 导出缺少资源包。");
        BridgeTexture texture = package.Textures
            .OrderBy(item => item.Index)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.PngBytes)) ??
            throw new InvalidOperationException("这个资源没有独立 PNG 贴图，暂时不能作为完整 KAnim 包导出。");

        byte[] png = DecodeBase64(texture.PngBytes, "png texture");
        byte[] anim = DecodeBase64(package.AnimBytes, "anim bytes");
        byte[] build = DecodeBase64(package.BuildBytes, "build bytes");
        string safeName = ResourceBridgePath.KAnimName(request.Resource.Key);
        string root = paths.ResourceBridgeExportDirectory;
        string outputDirectory = request.Layout == BridgeExportLayout.Split
            ? root
            : Path.Combine(root, safeName);
        string pngPath = request.Layout == BridgeExportLayout.Split
            ? Path.Combine(root, "KAnim_Png", safeName + "_0.png")
            : Path.Combine(outputDirectory, safeName + "_0.png");
        string animPath = request.Layout == BridgeExportLayout.Split
            ? Path.Combine(root, "KAnim_Bytes", safeName + "_anim.bytes")
            : Path.Combine(outputDirectory, safeName + "_anim.bytes");
        string buildPath = request.Layout == BridgeExportLayout.Split
            ? Path.Combine(root, "KAnim_Bytes", safeName + "_build.bytes")
            : Path.Combine(outputDirectory, safeName + "_build.bytes");

        await WritePackageAsync(
            new[]
            {
                (pngPath, png),
                (animPath, anim),
                (buildPath, build)
            },
            cancellationToken).ConfigureAwait(false);
        return new ExportArtifact(request.Resource.Key, pngPath, animPath, buildPath);
    }

    private async Task<ExportArtifact> ExportSpriteAsync(
        BridgeExportRequest request,
        CancellationToken cancellationToken)
    {
        BridgeSpritePackage package = request.SpritePackage ??
            throw new InvalidOperationException("Sprite 导出缺少资源包。");
        byte[] png = DecodeBase64(package.PngBytes, "sprite png");
        string path = Path.Combine(
            paths.ResourceBridgeExportDirectory,
            "Sprites",
            ResourceBridgePath.SpriteFileName(request.Resource.Key));
        await WritePackageAsync(new[] { (path, png) }, cancellationToken).ConfigureAwait(false);
        return new ExportArtifact(request.Resource.Key, path, null, null);
    }

    private static async Task WritePackageAsync(
        IEnumerable<(string Path, byte[] Bytes)> files,
        CancellationToken cancellationToken)
    {
        var fileList = files.ToList();
        foreach ((string path, byte[] bytes) in fileList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("导出路径没有有效目录。");
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = ResourceBridgePath.TemporaryPath(path);
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken).ConfigureAwait(false);
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }
    }

    private async Task<string?> WriteFailureReportAsync(
        IReadOnlyList<string> failures,
        CancellationToken cancellationToken)
    {
        if (failures.Count == 0)
        {
            return null;
        }

        string root = paths.ResourceBridgeExportDirectory;
        Directory.CreateDirectory(root);
        string path = Path.Combine(
            root,
            "batch_export_failures_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff") + ".log");
        await File.WriteAllLinesAsync(path, failures, cancellationToken).ConfigureAwait(false);
        return path;
    }

    private static byte[] DecodeBase64(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"资源桥返回的 {label} 为空。");
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"资源桥返回的 {label} 不是有效 Base64。", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
