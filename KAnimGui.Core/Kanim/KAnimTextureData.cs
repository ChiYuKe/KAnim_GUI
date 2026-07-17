namespace KAnimGui.Core.Kanim;

/// <summary>
/// UI-neutral texture payload. The Core layer keeps PNG bytes and dimensions;
/// WPF-specific BitmapSource creation belongs to the presentation boundary.
/// </summary>
public sealed class KAnimTextureData
{
    public KAnimTextureData(int width, int height, byte[] pngBytes)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        ArgumentNullException.ThrowIfNull(pngBytes);
        if (pngBytes.Length == 0)
        {
            throw new ArgumentException("纹理字节不能为空。", nameof(pngBytes));
        }

        Width = width;
        Height = height;
        PngBytes = pngBytes.ToArray();
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] PngBytes { get; }
}

public sealed record KAnimPackageData(
    KanimLib.KBuild? Build,
    KanimLib.KAnim? Anim,
    KAnimTextureData? Texture)
{
    public bool HasTexture => Texture is not null;
    public bool HasBuild => Build is not null;
    public bool HasAnim => Anim is not null;
    public bool HasAnyData => HasTexture || HasBuild || HasAnim;
}
