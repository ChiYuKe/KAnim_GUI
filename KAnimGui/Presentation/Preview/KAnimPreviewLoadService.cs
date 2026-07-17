using System.IO;
using System.Windows.Media.Imaging;
using KAnimGui.Application.Preview;
using KAnimGui.Core.Kanim;
using KanimLib;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Loads preview data off the UI thread while keeping WPF file-loading orchestration out of the window.
/// </summary>
public sealed class KAnimPreviewLoadService
{
    private readonly IKanimPackageLoader packageLoader;

    public KAnimPreviewLoadService(IKanimPackageLoader packageLoader)
    {
        this.packageLoader = packageLoader ?? throw new ArgumentNullException(nameof(packageLoader));
    }

    public KAnimPackage Load(string texturePath, string buildPath, string animPath)
    {
        ValidatePaths(texturePath, buildPath, animPath);
        return Adapt(packageLoader.LoadAsync(texturePath, buildPath, animPath).GetAwaiter().GetResult());
    }

    public Task<KAnimPackage> LoadAsync(
        string texturePath,
        string buildPath,
        string animPath,
        CancellationToken cancellationToken = default)
    {
        ValidatePaths(texturePath, buildPath, animPath);
        return LoadPackageAsync(texturePath, buildPath, animPath, cancellationToken);
    }

    private async Task<KAnimPackage> LoadPackageAsync(
        string texturePath,
        string buildPath,
        string animPath,
        CancellationToken cancellationToken)
    {
        KAnimPackageData package = await packageLoader
            .LoadAsync(texturePath, buildPath, animPath, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return Adapt(package);
    }

    private static KAnimPackage Adapt(KAnimPackageData package) => new()
    {
        Texture = DecodeTexture(package.Texture),
        Build = package.Build,
        Anim = package.Anim
    };

    private static BitmapImage? DecodeTexture(KAnimTextureData? texture)
    {
        if (texture is null)
        {
            return null;
        }

        using var stream = new MemoryStream(texture.PngBytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void ValidatePaths(string texturePath, string buildPath, string animPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texturePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(animPath);
    }
}
