using System.Diagnostics;
using KAnimGui.Application.Platform;

namespace KAnimGui.Infrastructure.Platform;

public sealed class ShellExternalLauncher : IExternalLauncher
{
    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
