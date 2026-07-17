using KAnimGui.Application.Conversion;

namespace KAnimGui.Presentation.Settings;

public sealed class PropertiesKsePathSettings : IKsePathSettings
{
    public bool UseCustomKsePath => KAnimGui.Properties.Default.UseCustomKsePath;

    public string CustomKsePath => KAnimGui.Properties.Default.CustomKsePath ?? string.Empty;
}
