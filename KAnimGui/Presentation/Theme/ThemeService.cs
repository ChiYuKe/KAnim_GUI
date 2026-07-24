using System;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace KAnimGui.Presentation.Theme;

public sealed class ThemeService : IThemeService
{
    private readonly System.Windows.Application application;
    private ResourceDictionary? activeThemeDictionary;

    public ThemeService(System.Windows.Application application)
    {
        this.application = application ?? throw new ArgumentNullException(nameof(application));
        CurrentTheme = Properties.Default.Theme;
    }

    public AppTheme CurrentTheme { get; private set; }

    public event EventHandler<AppThemeChangedEventArgs>? ThemeChanged;

    public void Apply(AppTheme theme)
    {
        if (!Enum.IsDefined(theme))
        {
            theme = AppTheme.Light;
        }

        ApplyMaterialDesignTheme(theme);

        ResourceDictionary dictionary = new()
        {
            Source = new Uri($"/KAnimGui;component/Themes/{theme}.xaml", UriKind.Relative)
        };

        if (activeThemeDictionary is not null)
        {
            application.Resources.MergedDictionaries.Remove(activeThemeDictionary);
        }

        application.Resources.MergedDictionaries.Add(dictionary);
        activeThemeDictionary = dictionary;
        CurrentTheme = theme;
        ThemeChanged?.Invoke(this, new AppThemeChangedEventArgs(theme));
    }

    private static void ApplyMaterialDesignTheme(AppTheme theme)
    {
        PaletteHelper paletteHelper = new();
        MaterialDesignThemes.Wpf.Theme materialTheme = paletteHelper.GetTheme();
        materialTheme.SetBaseTheme(theme == AppTheme.Light ? BaseTheme.Light : BaseTheme.Dark);

        (Color primary, Color secondary) = theme switch
        {
            AppTheme.Dark => (Color.FromRgb(0x1B, 0x9A, 0x8E), Color.FromRgb(0x48, 0xA8, 0x9E)),
            AppTheme.Oni => (Color.FromRgb(0xC9, 0x6D, 0x47), Color.FromRgb(0xD8, 0x86, 0x5D)),
            _ => (Color.FromRgb(0x15, 0x9A, 0x8C), Color.FromRgb(0xD7, 0x77, 0x4C))
        };

        materialTheme.SetPrimaryColor(primary);
        materialTheme.SetSecondaryColor(secondary);
        paletteHelper.SetTheme(materialTheme);
    }
}
