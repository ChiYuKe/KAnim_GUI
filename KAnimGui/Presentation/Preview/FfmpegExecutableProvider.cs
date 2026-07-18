using System.IO.Compression;
using System.IO;
using System.Reflection;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Resolves the bundled FFmpeg executable and extracts it to the per-user tool
/// cache when the application is running from a compressed publish package.
/// </summary>
internal sealed class FfmpegExecutableProvider
{
    private const string BundledVersion = "8.1.2";
    private const string BundledArchiveRelativePath = "Resources\\ffmpeg\\ffmpeg.exe.zip";
    private const string BundledExecutableRelativePath = "Resources\\ffmpeg\\ffmpeg.exe";
    private const string BundledArchiveResourceName = "KAnimGui.ffmpeg.exe.zip";

    public string Resolve()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string directBundle = Path.Combine(baseDirectory, BundledExecutableRelativePath);
        if (File.Exists(directBundle))
        {
            return directBundle;
        }

        string archive = Path.Combine(baseDirectory, BundledArchiveRelativePath);
        if (File.Exists(archive))
        {
            return ExtractBundledExecutable(archive);
        }

        Stream? embeddedArchive = OpenEmbeddedArchive();
        if (embeddedArchive is not null)
        {
            using (embeddedArchive)
            {
                return ExtractBundledExecutable(embeddedArchive);
            }
        }

        string applicationDirectoryExecutable = Path.Combine(baseDirectory, "ffmpeg.exe");
        if (File.Exists(applicationDirectoryExecutable))
        {
            return applicationDirectoryExecutable;
        }

        string? pathExecutable = FindOnPath();
        if (!string.IsNullOrWhiteSpace(pathExecutable))
        {
            return pathExecutable;
        }

        throw new FileNotFoundException(
            "未找到 FFmpeg。请使用完整发布包，或将 ffmpeg.exe 放到程序目录并重试。",
            "ffmpeg.exe");
    }

    private static string ExtractBundledExecutable(string archivePath)
    {
        using FileStream archiveStream = File.OpenRead(archivePath);
        return ExtractBundledExecutable(archiveStream);
    }

    private static string ExtractBundledExecutable(Stream archiveStream)
    {
        string toolsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KAnimGui",
            "Tools",
            $"ffmpeg-{BundledVersion}");
        string executablePath = Path.Combine(toolsDirectory, "ffmpeg.exe");
        if (File.Exists(executablePath))
        {
            return executablePath;
        }

        Directory.CreateDirectory(toolsDirectory);
        string temporaryPath = Path.Combine(toolsDirectory, $"ffmpeg-{Guid.NewGuid():N}.tmp");
        try
        {
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
            ZipArchiveEntry? entry = archive.GetEntry("ffmpeg.exe") ??
                archive.Entries.FirstOrDefault(item =>
                    string.Equals(item.Name, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                throw new InvalidDataException("内置 FFmpeg 压缩包中缺少 ffmpeg.exe。");
            }

            using (Stream source = entry.Open())
            using (FileStream destination = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(destination);
            }

            try
            {
                File.Move(temporaryPath, executablePath, overwrite: false);
            }
            catch (IOException) when (File.Exists(executablePath))
            {
                // Another application instance finished extracting the same version.
            }

            return executablePath;
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static Stream? OpenEmbeddedArchive()
    {
        Assembly?[] assemblies =
        {
            Assembly.GetExecutingAssembly(),
            Assembly.GetEntryAssembly()
        };

        foreach (Assembly assembly in assemblies
            .Where(item => item is not null)
            .Cast<Assembly>()
            .Distinct())
        {
            Stream? stream = assembly.GetManifestResourceStream(BundledArchiveResourceName);
            if (stream is not null)
            {
                return stream;
            }
        }

        return null;
    }

    private static string? FindOnPath()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), "ffmpeg.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A failed cleanup must not hide the original extraction error.
        }
        catch (UnauthorizedAccessException)
        {
            // A failed cleanup must not hide the original extraction error.
        }
    }
}
