using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using KAnimGui.Presentation.Theme;

namespace KAnimGui;

/// <summary>
/// Application settings persisted as JSON in the current user's local app-data folder.
/// This intentionally does not use System.Configuration so stale user.config files from
/// older releases cannot prevent the application from starting.
/// </summary>
internal sealed class Properties
{
    private static readonly Properties defaultInstance = new();
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private readonly object syncRoot = new();
    private SettingsData values;

    private Properties()
    {
        values = LoadValues(out bool migrated);
        if (migrated)
        {
            Save();
        }
    }

    public static Properties Default => defaultInstance;

    public AppTheme Theme
    {
        get => Enum.TryParse(values.Theme, ignoreCase: true, out AppTheme theme) && Enum.IsDefined(theme)
            ? theme
            : AppTheme.Light;
        set => Set(value.ToString(), next => values.Theme = next);
    }

    public bool OpenFolderAfterConvert
    {
        get => Get(() => values.OpenFolderAfterConvert);
        set => Set(value, next => values.OpenFolderAfterConvert = next);
    }

    public bool EnableTxtToBytes
    {
        get => Get(() => values.EnableTxtToBytes);
        set => Set(value, next => values.EnableTxtToBytes = next);
    }

    public bool UseCustomKsePath
    {
        get => Get(() => values.UseCustomKsePath);
        set => Set(value, next => values.UseCustomKsePath = next);
    }

    public string CustomKsePath
    {
        get => Get(() => values.CustomKsePath);
        set => Set(value ?? string.Empty, next => values.CustomKsePath = next);
    }

    public bool NoSuccessPopup
    {
        get => Get(() => values.NoSuccessPopup);
        set => Set(value, next => values.NoSuccessPopup = next);
    }

    public bool ShowGifExportCompletionNotification
    {
        get => Get(() => values.ShowGifExportCompletionNotification);
        set => Set(value, next => values.ShowGifExportCompletionNotification = next);
    }

    public double GifExportPlaybackSpeed
    {
        get => Get(() => values.GifExportPlaybackSpeed);
        set => Set(value, next => values.GifExportPlaybackSpeed = next);
    }

    public int GifExportWidth
    {
        get => Get(() => values.GifExportWidth);
        set => Set(value, next => values.GifExportWidth = next);
    }

    public int GifExportHeight
    {
        get => Get(() => values.GifExportHeight);
        set => Set(value, next => values.GifExportHeight = next);
    }

    public int GifExportScalingMode
    {
        get => Get(() => values.GifExportScalingMode);
        set => Set(value, next => values.GifExportScalingMode = next);
    }

    public string GifExportOutputDirectory
    {
        get => Get(() => values.GifExportOutputDirectory);
        set => Set(value ?? string.Empty, next => values.GifExportOutputDirectory = next);
    }

    public string PreviewResetViewShortcut
    {
        get => Get(() => values.PreviewResetViewShortcut);
        set => Set(value ?? "H", next => values.PreviewResetViewShortcut = next);
    }

    public string PreviewPlayPauseShortcut
    {
        get => Get(() => values.PreviewPlayPauseShortcut);
        set => Set(value ?? "Space", next => values.PreviewPlayPauseShortcut = next);
    }

    public bool PreviewWheelAnimationSwitch
    {
        get => Get(() => values.PreviewWheelAnimationSwitch);
        set => Set(value, next => values.PreviewWheelAnimationSwitch = next);
    }

    public bool PreviewAutoPlayAnimation
    {
        get => Get(() => values.PreviewAutoPlayAnimation);
        set => Set(value, next => values.PreviewAutoPlayAnimation = next);
    }

    public bool PreviewShowOrigin
    {
        get => Get(() => values.PreviewShowOrigin);
        set => Set(value, next => values.PreviewShowOrigin = next);
    }

    public bool PreviewShowBounds
    {
        get => Get(() => values.PreviewShowBounds);
        set => Set(value, next => values.PreviewShowBounds = next);
    }

    public bool PreviewHighlightElement
    {
        get => Get(() => values.PreviewHighlightElement);
        set => Set(value, next => values.PreviewHighlightElement = next);
    }

    public bool PreviewDarkBackground
    {
        get => Get(() => values.PreviewDarkBackground);
        set => Set(value, next => values.PreviewDarkBackground = next);
    }

    public void Save()
    {
        lock (syncRoot)
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(directory);
                string temporaryPath = SettingsPath + ".tmp";
                string json = JsonSerializer.Serialize(values, jsonOptions);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                File.Move(temporaryPath, SettingsPath, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                Trace.WriteLine($"Unable to save application settings: {ex}");
            }
        }
    }

    public void Reload()
    {
        lock (syncRoot)
        {
            values = LoadValues(out bool migrated);
            if (migrated)
            {
                Save();
            }
        }
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KAnimGui",
        "settings.json");

    private static SettingsData LoadValues(out bool migrated)
    {
        migrated = false;
        if (File.Exists(SettingsPath))
        {
            try
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<SettingsData>(json, jsonOptions) ?? new SettingsData();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                Trace.WriteLine($"Unable to load application settings: {ex}");
            }

            return new SettingsData();
        }

        SettingsData candidate = new();
        if (TryMigrateLegacySettings(candidate))
        {
            migrated = true;
        }

        return candidate;
    }

    private static bool TryMigrateLegacySettings(SettingsData target)
    {
        string legacyRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KAnimGui");
        if (!Directory.Exists(legacyRoot))
        {
            return false;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(legacyRoot, "user.config", SearchOption.AllDirectories)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path));
        }
        catch (IOException)
        {
            return false;
        }

        foreach (string file in files)
        {
            try
            {
                var document = XDocument.Load(file);
                bool imported = false;
                foreach (XElement setting in document.Descendants("setting"))
                {
                    string? name = setting.Attribute("name")?.Value;
                    string value = setting.Element("value")?.Value ?? string.Empty;
                    imported |= ApplyLegacySetting(target, name, value);
                }

                if (imported)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
            {
                Trace.WriteLine($"Unable to migrate legacy settings from '{file}': {ex.Message}");
            }
        }

        return false;
    }

    private static bool ApplyLegacySetting(SettingsData target, string? name, string value)
    {
        switch (name)
        {
            case nameof(SettingsData.OpenFolderAfterConvert) when bool.TryParse(value, out bool openFolder):
                target.OpenFolderAfterConvert = openFolder;
                return true;
            case nameof(SettingsData.EnableTxtToBytes) when bool.TryParse(value, out bool txtToBytes):
                target.EnableTxtToBytes = txtToBytes;
                return true;
            case nameof(SettingsData.UseCustomKsePath) when bool.TryParse(value, out bool customPath):
                target.UseCustomKsePath = customPath;
                return true;
            case nameof(SettingsData.CustomKsePath):
                target.CustomKsePath = value;
                return true;
            case nameof(SettingsData.NoSuccessPopup) when bool.TryParse(value, out bool noPopup):
                target.NoSuccessPopup = noPopup;
                return true;
            case nameof(SettingsData.ShowGifExportCompletionNotification) when bool.TryParse(value, out bool notification):
                target.ShowGifExportCompletionNotification = notification;
                return true;
            case nameof(SettingsData.GifExportPlaybackSpeed) when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double speed):
                target.GifExportPlaybackSpeed = speed;
                return true;
            case nameof(SettingsData.GifExportWidth) when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width):
                target.GifExportWidth = width;
                return true;
            case nameof(SettingsData.GifExportHeight) when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height):
                target.GifExportHeight = height;
                return true;
            case nameof(SettingsData.GifExportScalingMode) when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int scalingMode):
                target.GifExportScalingMode = scalingMode;
                return true;
            case nameof(SettingsData.GifExportOutputDirectory):
                target.GifExportOutputDirectory = value;
                return true;
            case nameof(SettingsData.PreviewResetViewShortcut):
                target.PreviewResetViewShortcut = value;
                return true;
            case nameof(SettingsData.PreviewPlayPauseShortcut):
                target.PreviewPlayPauseShortcut = value;
                return true;
            case nameof(SettingsData.PreviewWheelAnimationSwitch) when bool.TryParse(value, out bool wheelSwitch):
                target.PreviewWheelAnimationSwitch = wheelSwitch;
                return true;
            case nameof(SettingsData.PreviewAutoPlayAnimation) when bool.TryParse(value, out bool autoPlay):
                target.PreviewAutoPlayAnimation = autoPlay;
                return true;
            case nameof(SettingsData.PreviewShowOrigin) when bool.TryParse(value, out bool showOrigin):
                target.PreviewShowOrigin = showOrigin;
                return true;
            case nameof(SettingsData.PreviewShowBounds) when bool.TryParse(value, out bool showBounds):
                target.PreviewShowBounds = showBounds;
                return true;
            case nameof(SettingsData.PreviewHighlightElement) when bool.TryParse(value, out bool highlight):
                target.PreviewHighlightElement = highlight;
                return true;
            case nameof(SettingsData.PreviewDarkBackground) when bool.TryParse(value, out bool darkBackground):
                target.PreviewDarkBackground = darkBackground;
                return true;
            default:
                return false;
        }
    }

    private T Get<T>(Func<T> getter)
    {
        lock (syncRoot)
        {
            return getter();
        }
    }

    private void Set<T>(T value, Action<T> setter)
    {
        lock (syncRoot)
        {
            setter(value);
        }
    }

    private sealed class SettingsData
    {
        public string Theme { get; set; } = nameof(AppTheme.Light);
        public bool OpenFolderAfterConvert { get; set; }
        public bool EnableTxtToBytes { get; set; }
        public bool UseCustomKsePath { get; set; }
        public string CustomKsePath { get; set; } = string.Empty;
        public bool NoSuccessPopup { get; set; }
        public bool ShowGifExportCompletionNotification { get; set; } = true;
        public double GifExportPlaybackSpeed { get; set; } = 1;
        public int GifExportWidth { get; set; } = 768;
        public int GifExportHeight { get; set; } = 768;
        public int GifExportScalingMode { get; set; }
        public string GifExportOutputDirectory { get; set; } = string.Empty;
        public string PreviewResetViewShortcut { get; set; } = "H";
        public string PreviewPlayPauseShortcut { get; set; } = "Space";
        public bool PreviewWheelAnimationSwitch { get; set; } = true;
        public bool PreviewAutoPlayAnimation { get; set; } = true;
        public bool PreviewShowOrigin { get; set; } = true;
        public bool PreviewShowBounds { get; set; }
        public bool PreviewHighlightElement { get; set; } = true;
        public bool PreviewDarkBackground { get; set; }
    }
}
