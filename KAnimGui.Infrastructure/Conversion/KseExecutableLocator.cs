using KAnimGui.Application.Conversion;

namespace KAnimGui.Infrastructure.Conversion;

public sealed class KseExecutableLocator : IKseExecutableLocator
{
    private readonly IKsePathSettings settings;

    public KseExecutableLocator(IKsePathSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string? FindExecutable()
    {
        if (settings.UseCustomKsePath &&
            !string.IsNullOrWhiteSpace(settings.CustomKsePath) &&
            File.Exists(settings.CustomKsePath))
        {
            return settings.CustomKsePath;
        }

        string currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "kanimal-cli.exe");
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        string applicationDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kanimal-cli.exe");
        if (File.Exists(applicationDirectoryPath))
        {
            return applicationDirectoryPath;
        }

        string? environmentPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(environmentPath))
        {
            return null;
        }

        foreach (string path in environmentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(path, "kanimal-cli.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }
}
