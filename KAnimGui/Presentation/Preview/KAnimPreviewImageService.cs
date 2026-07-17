using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Encapsulates PNG encoding and texture-region replacement for the previewer.
/// </summary>
public sealed class KAnimPreviewImageService
{
    public void SavePng(BitmapSource image, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(fileStream);
    }

    public BitmapImage ReplaceRegion(BitmapImage texture, Int32Rect targetRect, string replacementPath)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementPath);
        if (targetRect.Width <= 0 || targetRect.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetRect), "替换区域必须包含像素。");
        }

        var originalImage = new BitmapImage();
        originalImage.BeginInit();
        originalImage.CacheOption = BitmapCacheOption.OnLoad;
        originalImage.UriSource = new Uri(replacementPath, UriKind.Absolute);
        originalImage.EndInit();
        originalImage.Freeze();

        var scaled = new TransformedBitmap(originalImage, new ScaleTransform(
            (double)targetRect.Width / originalImage.PixelWidth,
            (double)targetRect.Height / originalImage.PixelHeight));
        scaled.Freeze();

        var writeable = new WriteableBitmap(texture);
        int stride = targetRect.Width * 4;
        byte[] pixels = new byte[stride * targetRect.Height];
        scaled.CopyPixels(pixels, stride, 0);
        writeable.WritePixels(targetRect, pixels, stride, 0);
        return ToBitmapImage(writeable);
    }

    private static BitmapImage ToBitmapImage(WriteableBitmap writeable)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(writeable));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
