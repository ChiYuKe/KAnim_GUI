using KAnimGui.Application.Platform;

namespace KAnimGui.Infrastructure.Platform;

public sealed class LocalFileSystemGateway : IFileSystemGateway
{
    public bool FileExists(string path) =>
        !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path);

    public bool DirectoryExists(string path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    public void EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(path);
    }

    public bool TryEnsureWritableDirectory(string path, out string? error)
    {
        error = null;
        try
        {
            EnsureDirectory(path);
            string probePath = Path.Combine(path, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }
}
