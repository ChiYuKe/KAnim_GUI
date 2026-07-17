using KAnimGui.Application.ResourceBridge;

namespace KAnimGui.Infrastructure.ResourceBridge;

internal static class ResourceBridgePath
{
    public static string SafeFileName(string value, string fallback = "oni_resource")
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] sanitized = value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray();
        string result = new string(sanitized).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(result))
        {
            return fallback;
        }

        return result.Length <= 120 ? result : result[..120].TrimEnd('.');
    }

    public static string ResourceKey(BridgeResourceKey resource)
    {
        string source = resource.Source == BridgeResourceSource.Offline ? "offline" : "loaded";
        string key = string.IsNullOrWhiteSpace(resource.Id) ? resource.Name : resource.Id;
        return source + "_" + SafeFileName(key, "resource");
    }

    public static string SpriteFileName(BridgeResourceKey resource)
    {
        return SafeFileName(resource.Name, "sprite") + ".png";
    }

    public static string KAnimName(BridgeResourceKey resource)
    {
        return SafeFileName(resource.Name, "oni_kanim");
    }

    public static string TemporaryPath(string path)
    {
        return path + ".tmp-" + Guid.NewGuid().ToString("N");
    }
}
