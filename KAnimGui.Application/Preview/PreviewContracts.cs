using KAnimGui.Core.Kanim;

namespace KAnimGui.Application.Preview;

public interface IKanimPackageLoader
{
    Task<KAnimPackageData> LoadAsync(
        string texturePath,
        string buildPath,
        string animPath,
        CancellationToken cancellationToken = default);
}

public interface IPngTextureCodec
{
    KAnimTextureData Decode(byte[] pngBytes);
}
