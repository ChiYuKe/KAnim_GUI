using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace KAnimGui.Presentation.Preview;

public enum KAnimGifScalingMode
{
    Lanczos,
    Bicubic,
    Spline,
    Nearest
}

public sealed record KAnimGifExportOptions(
    double PlaybackSpeed,
    int Width,
    int Height,
    bool ShowCompletionNotification = true,
    KAnimGifScalingMode ScalingMode = KAnimGifScalingMode.Lanczos)
{
    public double GetEffectiveFramesPerSecond(double animationFramesPerSecond)
    {
        double safeRate = animationFramesPerSecond > 0 &&
                          !double.IsNaN(animationFramesPerSecond) &&
                          !double.IsInfinity(animationFramesPerSecond)
            ? animationFramesPerSecond
            : 30;
        return Math.Clamp(safeRate * PlaybackSpeed, 0.01, 100);
    }

    public int GetDelayCentiseconds(double animationFramesPerSecond)
    {
        return Math.Clamp(
            (int)Math.Round(100d / GetEffectiveFramesPerSecond(animationFramesPerSecond)),
            1,
            ushort.MaxValue);
    }
}

/// <summary>
/// Encodes rendered animation frames as a looping GIF through FFmpeg's
/// palettegen/paletteuse pipeline for higher-quality colors, dithering, and
/// Lanczos scaling than the WPF GIF encoder.
/// </summary>
public sealed class KAnimPreviewGifExportService
{
    private readonly Func<string> resolveFfmpeg;

    public KAnimPreviewGifExportService()
        : this(new FfmpegExecutableProvider().Resolve)
    {
    }

    internal KAnimPreviewGifExportService(Func<string> resolveFfmpeg)
    {
        this.resolveFfmpeg = resolveFfmpeg ?? throw new ArgumentNullException(nameof(resolveFfmpeg));
    }

    public async Task ExportAsync(
        int frameCount,
        double animationFramesPerSecond,
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

        string ffmpegPath = resolveFfmpeg();
        string? outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "KAnimGui",
            $"gif-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            string framePattern = Path.Combine(temporaryDirectory, "frame_%06d.png");
            for (int index = 0; index < frameCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BitmapSource source = renderFrame(index) ??
                    throw new InvalidOperationException($"无法渲染第 {index + 1} 帧。");
                SavePng(source, Path.Combine(temporaryDirectory, $"frame_{index:D6}.png"));
                progress?.Report(index + 1);
            }

            cancellationToken.ThrowIfCancellationRequested();
            string palettePath = Path.Combine(temporaryDirectory, "palette.png");
            string temporaryOutputPath = Path.Combine(temporaryDirectory, "output.gif");
            double effectiveFramesPerSecond = options.GetEffectiveFramesPerSecond(animationFramesPerSecond);
            string scalingFlag = GetScalingFlag(options.ScalingMode);

            await RunFfmpegAsync(
                ffmpegPath,
                BuildPaletteArguments(
                    framePattern,
                    palettePath,
                    effectiveFramesPerSecond,
                    options.Width,
                    options.Height,
                    scalingFlag),
                cancellationToken).ConfigureAwait(false);

            await RunFfmpegAsync(
                ffmpegPath,
                BuildGifArguments(
                    framePattern,
                    palettePath,
                    temporaryOutputPath,
                    effectiveFramesPerSecond,
                    options.Width,
                    options.Height,
                    scalingFlag),
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(temporaryOutputPath, outputPath, overwrite: true);
        }
        finally
        {
            TryDeleteDirectory(temporaryDirectory);
        }
    }

    private static IReadOnlyList<string> BuildPaletteArguments(
        string framePattern,
        string palettePath,
        double framesPerSecond,
        int width,
        int height,
        string scalingFlag)
    {
        return new[]
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-framerate", FormatFramesPerSecond(framesPerSecond),
            "-start_number", "0",
            "-i", framePattern,
            "-vf", $"scale={width}:{height}:flags={scalingFlag},palettegen=max_colors=256:reserve_transparent=1:stats_mode=full",
            palettePath
        };
    }

    private static IReadOnlyList<string> BuildGifArguments(
        string framePattern,
        string palettePath,
        string outputPath,
        double framesPerSecond,
        int width,
        int height,
        string scalingFlag)
    {
        return new[]
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-framerate", FormatFramesPerSecond(framesPerSecond),
            "-start_number", "0",
            "-i", framePattern,
            "-i", palettePath,
            "-filter_complex",
            $"[0:v]scale={width}:{height}:flags={scalingFlag}:force_original_aspect_ratio=disable[scaled];[scaled][1:v]paletteuse=dither=sierra2_4a:diff_mode=rectangle:alpha_threshold=128",
            "-loop", "0",
            "-f", "gif",
            outputPath
        };
    }

    private static async Task RunFfmpegAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("无法启动 FFmpeg。");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException("无法启动内置 FFmpeg，请检查程序文件是否完整。", ex);
        }

        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryKill(process);
            throw;
        }

        string error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            string detail = string.IsNullOrWhiteSpace(error)
                ? $"退出码 {process.ExitCode}"
                : error.Trim();
            throw new InvalidOperationException($"FFmpeg GIF 导出失败：{detail}");
        }
    }

    private static void SavePng(BitmapSource source, string outputPath)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static string FormatFramesPerSecond(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetScalingFlag(KAnimGifScalingMode scalingMode) => scalingMode switch
    {
        KAnimGifScalingMode.Lanczos => "lanczos",
        KAnimGifScalingMode.Bicubic => "bicubic",
        KAnimGifScalingMode.Spline => "spline",
        KAnimGifScalingMode.Nearest => "neighbor",
        _ => throw new ArgumentOutOfRangeException(nameof(scalingMode), scalingMode, "不支持的 GIF 缩放算法。")
    };

    private static void ValidateOptions(KAnimGifExportOptions options)
    {
        if (double.IsNaN(options.PlaybackSpeed) ||
            double.IsInfinity(options.PlaybackSpeed) ||
            options.PlaybackSpeed is < 0.1 or > 2.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "GIF 播放速度必须在 0.1 到 2.0 倍之间。");
        }

        if (options.Width is < 16 or > 4096 || options.Height is < 16 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "GIF 尺寸必须在 16 到 4096 像素之间。");
        }

        if (!Enum.IsDefined(options.ScalingMode))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "不支持的 GIF 缩放算法。");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process already exited while cancellation was being handled.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // A failed kill must not hide the original cancellation or process error.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temporary files are best-effort cleanup and must not mask export errors.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary files are best-effort cleanup and must not mask export errors.
        }
    }
}
