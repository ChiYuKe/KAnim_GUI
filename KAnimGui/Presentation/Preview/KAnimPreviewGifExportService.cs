using System.IO;
using System.Windows.Media.Imaging;

namespace KAnimGui.Presentation.Preview;

public sealed record KAnimGifExportOptions(
    double FramesPerSecond,
    int Width,
    int Height)
{
    public int DelayCentiseconds => Math.Clamp(
        (int)Math.Round(100d / FramesPerSecond),
        1,
        ushort.MaxValue);
}

/// <summary>
/// Encodes rendered animation frames as a looping GIF without requiring an
/// external executable such as FFmpeg.
/// </summary>
public sealed class KAnimPreviewGifExportService
{
    public Task ExportAsync(
        int frameCount,
        Func<int, BitmapSource> renderFrame,
        KAnimGifExportOptions options,
        string outputPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameCount);
        ArgumentNullException.ThrowIfNull(renderFrame);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ValidateOptions(options);

        var encoder = new GifBitmapEncoder();
        for (int index = 0; index < frameCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BitmapSource source = renderFrame(index) ??
                throw new InvalidOperationException($"无法渲染第 {index + 1} 帧。");
            BitmapSource resized = Resize(source, options.Width, options.Height);
            encoder.Frames.Add(CreateGifFrame(resized, options.DelayCentiseconds, index == 0));
            progress?.Report(index + 1);

        }

        cancellationToken.ThrowIfCancellationRequested();
        string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
        return Task.CompletedTask;
    }

    private static BitmapFrame CreateGifFrame(
        BitmapSource source,
        int delayCentiseconds,
        bool includeLoopExtension)
    {
        var metadata = new BitmapMetadata("gif");
        metadata.SetQuery("/grctlext/Delay", (ushort)delayCentiseconds);
        metadata.SetQuery("/grctlext/Disposal", (byte)2);
        if (includeLoopExtension)
        {
            metadata.SetQuery("/appext/Application", new byte[]
            {
                (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C',
                (byte)'A', (byte)'P', (byte)'E', (byte)'2', (byte)'.',
                (byte)'0'
            });
            metadata.SetQuery("/appext/Data", new byte[] { 1, 0, 0 });
        }

        return BitmapFrame.Create(source, null, metadata, null);
    }

    private static BitmapSource Resize(BitmapSource source, int width, int height)
    {
        if (source.PixelWidth == width && source.PixelHeight == height)
        {
            return source;
        }

        var scaled = new TransformedBitmap(
            source,
            new System.Windows.Media.ScaleTransform(
                (double)width / source.PixelWidth,
                (double)height / source.PixelHeight));
        scaled.Freeze();
        return scaled;
    }

    private static void ValidateOptions(KAnimGifExportOptions options)
    {
        if (double.IsNaN(options.FramesPerSecond) ||
            double.IsInfinity(options.FramesPerSecond) ||
            options.FramesPerSecond is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "GIF 帧率必须在 1 到 100 FPS 之间。");
        }

        if (options.Width is < 16 or > 4096 || options.Height is < 16 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "GIF 尺寸必须在 16 到 4096 像素之间。");
        }
    }
}
