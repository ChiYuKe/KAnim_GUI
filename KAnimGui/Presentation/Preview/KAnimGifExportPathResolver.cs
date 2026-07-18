using System.IO;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// Resolves the persisted GIF export root and keeps single/batch output layouts consistent.
/// </summary>
public static class KAnimGifExportPathResolver
{
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "KSE_Output",
        "KAnim_Gif");

    public static string GetConfiguredDirectory()
    {
        string configured = Properties.Default.GifExportOutputDirectory?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultDirectory;
        }

        try
        {
            return Path.GetFullPath(configured);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return DefaultDirectory;
        }
    }

    public static string GetSingleExportDirectory(string configuredDirectory) =>
        configuredDirectory;

    public static string GetBatchExportDirectory(string configuredDirectory, string kanimName) =>
        Path.Combine(configuredDirectory, SanitizeSegment(kanimName, "animation"));

    public static string BuildGifFileName(string kanimName, string animationName) =>
        $"{SanitizeSegment(kanimName, "kanim")}_{SanitizeSegment(animationName, "animation")}.gif";

    private static string SanitizeSegment(string value, string fallback)
    {
        string result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }
}
