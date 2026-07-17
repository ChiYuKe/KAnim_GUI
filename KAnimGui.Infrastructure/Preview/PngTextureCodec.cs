using System.Windows.Media.Imaging;
using KAnimGui.Application.Preview;
using KAnimGui.Core.Kanim;

namespace KAnimGui.Infrastructure.Preview;

public sealed class PngTextureCodec : IPngTextureCodec
{
    public KAnimTextureData Decode(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        using var stream = new MemoryStream(pngBytes, writable: false);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        return new KAnimTextureData(frame.PixelWidth, frame.PixelHeight, pngBytes);
    }
}
