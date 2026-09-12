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
    /// <summary>
    /// Bitmap edge length. The shell asks for the DPI-scaled small-icon size,
    /// so rendering at exactly that avoids a resample. The floor of 32 is what
    /// actually applies up to 200% scaling, and is the size the proportions
    /// below were tuned against.
    /// </summary>
    private static readonly int Size = Math.Max(32, SystemInformation.SmallIconSize.Width);

    // Radial budget, measured from the bitmap edge inwards to the centre. The
    // four bands are a fixed allowance: they must add up to half the canvas,
    // so widening a stroke can only be paid for out of the gap or the hole.
    //
    //     outer stroke 5.5 | gap 3.0 | inner stroke 4.0 | centre hole 3.5
    //
    // Two things set these numbers. The pair only reads as two rings while the
    // gap stays around half the stroke width — at 2.5 against a 5.5 stroke the
    // two merge into one thick blob. And the inner ring's radius is well under
    // half the outer's, so an identical stroke there looks distinctly heavier;
    // tapering it evens out the apparent weight and leaves a centre hole big
    // enough to still read as a hole. The wider ring going to the session also
    // matches which number moves, and the popup's top-to-bottom order.
    private const float OuterWidthRatio = 5.5f / 32f;
    private const float RingGapRatio = 3.0f / 32f;
    private const float InnerWidthRatio = 4.0f / 32f;

    public static Icon RenderDualRing(double sessionPercentage, double weekPercentage, bool isStale)
    {
        float outerWidth = Size * OuterWidthRatio;
        float innerWidth = Size * InnerWidthRatio;
        float gap = Size * RingGapRatio;
        float centre = Size / 2f;

        using var bmp = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Unfilled portion of each ring. Alpha 130 keeps it clearly
            // legible against the taskbar without competing with the fill.
            var track = Color.FromArgb(130, 255, 255, 255);
            var sessionColor = isStale ? QuotaPalette.GdiStale : QuotaPalette.GdiFor(sessionPercentage);
            var weekColor = isStale ? QuotaPalette.GdiStale : QuotaPalette.GdiFor(weekPercentage);

            // Outer ring = session (5 hour window), flush with the bitmap edge.
            DrawRing(g, centre, centre - outerWidth / 2, outerWidth,
                track, sessionColor, sessionPercentage);

            // Inner ring = week (7 day window), one gap further in.
            DrawRing(g, centre, centre - outerWidth - gap - innerWidth / 2, innerWidth,
                track, weekColor, weekPercentage);
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

    /// <summary>
    /// Strokes one ring: the full circle in the track color, then the used
    /// portion on top as an arc.
    /// </summary>
    private static void DrawRing(Graphics g, float centre, float radius, float width,
        Color track, Color fill, double percentage)
    {
        var rect = new RectangleF(centre - radius, centre - radius, radius * 2, radius * 2);

        using var trackPen = new Pen(track, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawEllipse(trackPen, rect);

        double clamped = Math.Clamp(percentage, 0, 100);
        if (clamped <= 0) return;

        float sweep = (float)(clamped / 100.0 * 360.0);
        using var fillPen = new Pen(fill, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        // Start at -90 degrees so progress begins at 12 o'clock, matching the popup's progress bars.
        g.DrawArc(fillPen, rect, -90, sweep);
    }

    // Cloning the icon above (Icon.FromHandle(hIcon).Clone()) copies the
    // bitmap data into a new, independently-owned icon, so it's safe to
    // destroy the original native handle immediately instead of leaving
    // that responsibility to the caller.
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint handle);

    private static void DestroyIconHandle(nint handle) => DestroyIcon(handle);
}
