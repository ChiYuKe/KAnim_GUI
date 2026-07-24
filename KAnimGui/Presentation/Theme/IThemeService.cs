using System;

namespace KAnimGui.Presentation.Theme;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    event EventHandler<AppThemeChangedEventArgs>? ThemeChanged;

    void Apply(AppTheme theme);
}

public sealed class AppThemeChangedEventArgs : EventArgs
{
    public AppThemeChangedEventArgs(AppTheme theme) => Theme = theme;

    public AppTheme Theme { get; }
}
