using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClaudeQuotaTracker;

/// <summary>
/// Opts a window's non-client area (title bar, border) into dark mode.
///
/// Theme.xaml only reaches WPF's own controls; the title bar is drawn by DWM,
/// which defaults unpackaged Win32 apps to light regardless of the user's
/// Windows dark-mode setting. Without this the app would show a white caption
/// bar above a dark window — exactly the mismatch the dark theme is fixing.
/// </summary>
internal static class DarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;
    // Windows 10 builds before 19041 used attribute 19 for the same flag.
    private const int UseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Applies the dark caption bar. Safe to call on any Windows version:
    /// DWM simply returns a failure code on builds that don't know the
    /// attribute, and an unthemed caption bar is not worth failing startup
    /// over — so errors are swallowed.
    /// </summary>
    public static void Apply(Window window)
    {
        // The HWND only exists once the window is sourced, so defer if needed.
        if (new WindowInteropHelper(window).Handle == nint.Zero)
        {
            window.SourceInitialized += (_, _) => Apply(window);
            return;
        }

        nint hwnd = new WindowInteropHelper(window).Handle;
        int enabled = 1;

        try
        {
            if (DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, UseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // No dwmapi.dll — nothing to do.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}
