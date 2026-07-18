using System.Windows.Input;

namespace KAnimGui.Presentation.Preview;

/// <summary>
/// A persisted preview shortcut that supports a key with optional Ctrl/Alt/Shift/Win modifiers.
/// </summary>
public readonly record struct PreviewShortcut(Key Key, ModifierKeys Modifiers)
{
    private const ModifierKeys SupportedModifiers =
        ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows;

    public bool IsDisabled => Key == Key.None;

    public bool Matches(KeyEventArgs e) => !IsDisabled && this == FromKeyEvent(e);

    public static PreviewShortcut FromKeyEvent(KeyEventArgs e)
    {
        Key key = e.Key switch
        {
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.System => e.SystemKey,
            _ => e.Key
        };
        return new PreviewShortcut(key, e.KeyboardDevice.Modifiers & SupportedModifiers);
    }

    public static PreviewShortcut Parse(string? value, PreviewShortcut fallback)
    {
        if (string.Equals(value?.Trim(), "None", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value?.Trim(), "禁用", StringComparison.OrdinalIgnoreCase))
        {
            return new PreviewShortcut(Key.None, ModifierKeys.None);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string[] parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !Enum.TryParse(parts[^1], true, out Key key) || IsModifierKey(key))
        {
            return fallback;
        }

        ModifierKeys modifiers = ModifierKeys.None;
        foreach (string part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => ModifierKeys.Control,
                "alt" => ModifierKeys.Alt,
                "shift" => ModifierKeys.Shift,
                "win" or "windows" => ModifierKeys.Windows,
                _ => (ModifierKeys)(-1)
            };
        }

        return modifiers == (ModifierKeys)(-1)
            ? fallback
            : new PreviewShortcut(key, modifiers & SupportedModifiers);
    }

    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    public override string ToString()
    {
        if (IsDisabled)
        {
            return "None";
        }

        var parts = new List<string>(5);
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join('+', parts);
    }
}
