namespace KAnimGui.Application.Platform;

public interface IFileSystemGateway
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void EnsureDirectory(string path);
    bool TryEnsureWritableDirectory(string path, out string? error);
}

public interface IExternalLauncher
{
    void Open(string path);
}
