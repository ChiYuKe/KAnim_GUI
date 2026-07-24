using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace KAnimGui.Presentation.Theme;

internal static class WindowThemeAssist
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

    public static void ApplyNativeTitleBar(Window window, bool useDarkTheme)
    {
        ArgumentNullException.ThrowIfNull(window);

        void Apply()
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int enabled = useDarkTheme ? 1 : 0;
            int result = DwmSetWindowAttribute(
                handle,
                DwmUseImmersiveDarkMode,
                ref enabled,
                Marshal.SizeOf<int>());
            if (result != 0)
            {
                _ = DwmSetWindowAttribute(
                    handle,
                    DwmUseImmersiveDarkModeBefore20H1,
                    ref enabled,
                    Marshal.SizeOf<int>());
            }
        }

        if (new WindowInteropHelper(window).Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) => Apply();
        }
        else
        {
            Apply();
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
