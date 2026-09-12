namespace ClaudeQuotaTracker;

/// <summary>
/// Single source of truth for the usage-severity colors and their thresholds.
///
/// Two renderers need the same scale in incompatible types — the tray icon
/// draws with System.Drawing and the popup bars with WPF brushes — so the
/// values live here once and each side gets a converted accessor, rather than
/// the scale being duplicated (and drifting) in both.
/// </summary>
internal static class QuotaPalette
{
    public const double DangerThreshold = 90;
    public const double WarningThreshold = 70;

    // Warm ramp anchored on Claude's terracotta:
    //   normal  - terracotta, the same hue as the UI accent, reads as "resting"
    //   warning - amber, a clear jump in brightness toward yellow
    //   danger  - deep red, the most saturated and darkest step
    // Terracotta and red are hue neighbours, so the separation between normal
    // and danger is carried by saturation and lightness rather than hue.
    private const int NormalRgb = 0xD97757;
    private const int WarningRgb = 0xF0AD2E;
    private const int DangerRgb = 0xD93025;

    // Shown when the snapshot is missing or over two hours old. Light enough
    // to stay legible against a dark taskbar.
    private const int StaleRgb = 0xC8C8C8;

    private static int RgbFor(double percentage) => percentage switch
    {
        >= DangerThreshold => DangerRgb,
        >= WarningThreshold => WarningRgb,
        _ => NormalRgb
    };

    /// <summary>Severity color for the tray icon rings.</summary>
    public static System.Drawing.Color GdiFor(double percentage) => Gdi(RgbFor(percentage));

    /// <summary>Muted color for the tray icon when there is no recent data.</summary>
    public static System.Drawing.Color GdiStale => Gdi(StaleRgb);

    private static System.Drawing.Color Gdi(int rgb) =>
        System.Drawing.Color.FromArgb(rgb >> 16 & 0xFF, rgb >> 8 & 0xFF, rgb & 0xFF);

    // Frozen and cached: the popup re-renders every second off the countdown
    // timer, so it must not allocate a fresh brush per tick.
    private static readonly System.Windows.Media.Brush NormalBrush = Frozen(NormalRgb);
    private static readonly System.Windows.Media.Brush WarningBrush = Frozen(WarningRgb);
    private static readonly System.Windows.Media.Brush DangerBrush = Frozen(DangerRgb);

    /// <summary>Severity brush for the popup progress bars.</summary>
    public static System.Windows.Media.Brush BrushFor(double percentage) => percentage switch
    {
        >= DangerThreshold => DangerBrush,
        >= WarningThreshold => WarningBrush,
        _ => NormalBrush
    };

    private static System.Windows.Media.Brush Frozen(int rgb)
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(
                (byte)(rgb >> 16 & 0xFF), (byte)(rgb >> 8 & 0xFF), (byte)(rgb & 0xFF)));
        brush.Freeze();
        return brush;
    }
}
