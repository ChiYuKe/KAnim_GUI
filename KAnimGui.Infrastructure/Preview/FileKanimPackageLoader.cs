using KAnimGui.Application.Preview;
using KAnimGui.Core.Kanim;
using KanimLib;

namespace KAnimGui.Infrastructure.Preview;

/// <summary>
/// Reads KAnim files and PNG bytes at the infrastructure boundary.
/// </summary>
public sealed class FileKanimPackageLoader : IKanimPackageLoader
{
    private readonly IPngTextureCodec textureCodec;

    public FileKanimPackageLoader(IPngTextureCodec textureCodec)
    {
        this.textureCodec = textureCodec ?? throw new ArgumentNullException(nameof(textureCodec));
    }

    public async Task<KAnimPackageData> LoadAsync(
        string texturePath,
        string buildPath,
        string animPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texturePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(animPath);
        cancellationToken.ThrowIfCancellationRequested();

        KBuild? build = await ReadBuildAsync(buildPath, cancellationToken).ConfigureAwait(false);
        KAnim? anim = await ReadAnimAsync(animPath, cancellationToken).ConfigureAwait(false);
        KAnimTextureData? texture = await ReadTextureAsync(texturePath, cancellationToken).ConfigureAwait(false);

        if (build is not null && anim is not null)
        {
            anim.RepairStringsFromBuild(build);
        }

        return new KAnimPackageData(build, anim, texture);
    }

    private static Task<KBuild?> ReadBuildAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Task.FromResult<KBuild?>(null);
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<KBuild?>(KAnimBinaryCodec.ReadBuild(stream));
    }

    private static Task<KAnim?> ReadAnimAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Task.FromResult<KAnim?>(null);
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<KAnim?>(KAnimBinaryCodec.ReadAnim(stream));
    }

    private async Task<KAnimTextureData?> ReadTextureAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return textureCodec.Decode(bytes);
    }
}
