using System.Collections.ObjectModel;

namespace KAnimGui.Application.ResourceBridge;

public enum BridgeResourceType
{
    KAnim,
    Sprite
}

public enum BridgeResourceSource
{
    Loaded,
    Offline,
    Runtime
}

public enum BridgeExportLayout
{
    Grouped,
    Split
}

public sealed record BridgeResourceKey(
    BridgeResourceType Type,
    BridgeResourceSource Source,
    string Id,
    string Name);

public abstract record BridgeResource(
    BridgeResourceKey Key,
    string? Bundle)
{
    public bool IsOffline => Key.Source == BridgeResourceSource.Offline;
}

public sealed record BridgeAnimationResource(
    BridgeResourceKey Key,
    string? Bundle,
    int AnimationCount,
    int FrameCount,
    int ElementCount)
    : BridgeResource(Key, Bundle)
{
    public bool HasAnimation => AnimationCount > 0 && FrameCount > 0;
}

public sealed record BridgeSpriteResource(
    BridgeResourceKey Key,
    string? Bundle,
    int Width,
    int Height)
    : BridgeResource(Key, Bundle);

public sealed record BridgeStatus(
    bool Ok,
    string Mod,
    string Version,
    int Port,
    bool AssetsReady,
    int AnimationCount,
    int ResourcePackageCount);

public sealed record BridgeSnapshot(
    string BaseUrl,
    BridgeStatus Status,
    IReadOnlyList<BridgeAnimationResource> Animations,
    IReadOnlyList<BridgeAnimationResource> OfflineAnimations,
    IReadOnlyList<BridgeSpriteResource> Sprites,
    IReadOnlyList<BridgeSpriteResource> OfflineSprites)
{
    public IReadOnlyList<BridgeResource> AllResources =>
        new ReadOnlyCollection<BridgeResource>(
            Animations
                .Cast<BridgeResource>()
                .Concat(OfflineAnimations)
                .Concat(Sprites)
                .Concat(OfflineSprites)
                .ToList());
}

public sealed record BridgeTexture(int Index, string Name, int Width, int Height, string PngBytes);

public sealed record BridgeKAnimPackage(
    bool Ok,
    string Name,
    BridgeResourceSource? Source,
    string AnimBytes,
    string BuildBytes,
    IReadOnlyList<BridgeTexture> Textures,
    string? Error,
    string? Detail);

public sealed record BridgePreview(
    bool Ok,
    string Name,
    int Width,
    int Height,
    string PngBytes,
    string? Error,
    string? Detail);

public sealed record BridgeSpritePackage(
    bool Ok,
    string Name,
    BridgeResourceSource? Source,
    string PngBytes,
    int Width,
    int Height,
    string? Error,
    string? Detail);

public sealed record BridgeState(
    int SchemaVersion,
    BridgeExportLayout ExportLayout)
{
    public static BridgeState Empty { get; } = new(
        SchemaVersion: 2,
        ExportLayout: BridgeExportLayout.Grouped);
}

public sealed record BridgeExportRequest(
    BridgeResource Resource,
    BridgeKAnimPackage? KAnimPackage = null,
    BridgeSpritePackage? SpritePackage = null,
    BridgeExportLayout Layout = BridgeExportLayout.Grouped);

public sealed record ExportArtifact(
    BridgeResourceKey Resource,
    string? PngPath,
    string? AnimPath,
    string? BuildPath);

public sealed record BatchProgress(
    int Completed,
    int Total,
    string ResourceName,
    bool Succeeded,
    string? ErrorMessage);

public sealed record BatchExportResult(
    IReadOnlyList<ExportArtifact> Succeeded,
    IReadOnlyList<BridgeResourceKey> Failed,
    string? FailureReportPath,
    bool WasCanceled);

public interface IResourceBridgeClient
{
    Task<BridgeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<BridgeKAnimPackage> GetKAnimPackageAsync(
        string baseUrl,
        BridgeResourceKey resource,
        CancellationToken cancellationToken = default);

    Task<BridgePreview> GetPreviewAsync(
        string baseUrl,
        BridgeResourceKey resource,
        CancellationToken cancellationToken = default);

    Task<BridgeSpritePackage> GetSpritePackageAsync(
        string baseUrl,
        BridgeResourceKey resource,
        CancellationToken cancellationToken = default);
}

public interface IResourceBridgeExportService
{
    Task<ExportArtifact> ExportAsync(
        BridgeExportRequest request,
        CancellationToken cancellationToken = default);

    Task<BatchExportResult> ExportBatchAsync(
        IEnumerable<BridgeExportRequest> requests,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<string?> WriteFailureReportAsync(
        IEnumerable<string> failures,
        CancellationToken cancellationToken = default);
}

public interface IResourceBridgeStateStore
{
    Task<BridgeState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(BridgeState state, CancellationToken cancellationToken = default);
}

public interface IThumbnailCache
{
    string? GetPath(BridgeResourceKey resource);

    Task<string> SaveAsync(
        BridgeResourceKey resource,
        string pngBase64,
        CancellationToken cancellationToken = default);
}

public interface IApplicationPathProvider
{
    string StatusFilePath { get; }

    string ResourceBridgeStateFilePath { get; }

    string ResourceBridgeCacheDirectory { get; }

    string ResourceBridgeExportDirectory { get; }
}
