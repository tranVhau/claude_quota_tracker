using System.Drawing;
using System.Drawing.Drawing2D;

namespace ClaudeQuotaTracker;

/// <summary>
/// Draws the tray icon as two concentric progress rings directly with
/// System.Drawing — no webview/canvas round-trip needed, since NotifyIcon
/// consumes a System.Drawing.Icon natively.
/// </summary>
public static class IconRenderer
{
    private const int Size = 32;
    private const float RingWidth = 4.5f;
    private const float RingGap = 3f;

    public static Icon RenderDualRing(double sessionPercentage, double weekPercentage, bool isStale)
    {
        using var bmp = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var track = Color.FromArgb(60, 255, 255, 255);
            var sessionColor = isStale ? Color.Gray : ColorForPercentage(sessionPercentage);
            var weekColor = isStale ? Color.Gray : ColorForPercentage(weekPercentage);

            // Outer ring = week (7 day window)
            var outerRect = new RectangleF(RingWidth / 2 + 1, RingWidth / 2 + 1,
                Size - RingWidth - 2, Size - RingWidth - 2);
            DrawRing(g, outerRect, RingWidth, track, weekColor, weekPercentage);

            // Inner ring = session (5 hour window)
            float inset = RingWidth + RingGap;
            var innerRect = new RectangleF(outerRect.X + inset, outerRect.Y + inset,
                outerRect.Width - inset * 2, outerRect.Height - inset * 2);
            DrawRing(g, innerRect, RingWidth, track, sessionColor, sessionPercentage);
        }

        // GetHicon() allocates a native GDI icon handle that Icon.FromHandle
        // does not take ownership of — left alone, each call would leak a
        // handle. Cloning copies the bitmap into a new, independently-owned
        // managed Icon, so the native handle can be destroyed immediately
        // below. The caller only needs to call the returned Icon's normal
        // Dispose() when it's replaced (see TrayIconManager.UpdateIcon).
        nint hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIconHandle(hIcon);
        return icon;
    }

    private static void DrawRing(Graphics g, RectangleF rect, float width, Color track, Color fill, double percentage)
    {
        using var trackPen = new Pen(track, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawEllipse(trackPen, rect);

        double clamped = Math.Clamp(percentage, 0, 100);
        if (clamped <= 0) return;

        float sweep = (float)(clamped / 100.0 * 360.0);
        using var fillPen = new Pen(fill, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        // Start at -90 degrees so progress begins at 12 o'clock, matching the popup's progress bars.
        g.DrawArc(fillPen, rect, -90, sweep);
    }

    private static Color ColorForPercentage(double percentage) => percentage switch
    {
        >= 90 => Color.FromArgb(0xE2, 0x4B, 0x4A), // red
        >= 70 => Color.FromArgb(0xF5, 0x9E, 0x0B), // amber
        _ => Color.FromArgb(0x22, 0xC5, 0x5E)      // green
    };

    // Cloning the icon above (Icon.FromHandle(hIcon).Clone()) copies the
    // bitmap data into a new, independently-owned icon, so it's safe to
    // destroy the original native handle immediately instead of leaving
    // that responsibility to the caller.
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint handle);

    private static void DestroyIconHandle(nint handle) => DestroyIcon(handle);
}
