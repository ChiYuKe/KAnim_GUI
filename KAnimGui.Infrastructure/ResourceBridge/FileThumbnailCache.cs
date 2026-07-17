using KAnimGui.Application.ResourceBridge;

namespace KAnimGui.Infrastructure.ResourceBridge;

public sealed class FileThumbnailCache : IThumbnailCache
{
    private readonly IApplicationPathProvider paths;

    public FileThumbnailCache(IApplicationPathProvider paths)
    {
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public string? GetPath(BridgeResourceKey resource)
    {
        string path = GetThumbnailPath(resource);
        return File.Exists(path) ? path : null;
    }

    public async Task<string> SaveAsync(
        BridgeResourceKey resource,
        string pngBase64,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pngBase64))
        {
            throw new InvalidOperationException("资源桥返回的缩略图为空。");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(pngBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("资源桥返回的缩略图不是有效 Base64。", ex);
        }

        string path = GetThumbnailPath(resource);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = ResourceBridgePath.TemporaryPath(path);
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, true);
            return path;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private string GetThumbnailPath(BridgeResourceKey resource)
    {
        string sourceFolder = resource.Source == BridgeResourceSource.Offline ? "offline" : "loaded";
        return Path.Combine(
            paths.ResourceBridgeCacheDirectory,
            sourceFolder,
            ResourceBridgePath.ResourceKey(resource) + ".png");
    }
}
