using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace ClaudeQuotaTracker;

public partial class PopupWindow : Window
{
    private const double TrackWidth = 216;

    private readonly SnapshotStore _store;
    private readonly DispatcherTimer _countdownTimer;

    public PopupWindow(SnapshotStore store)
    {
        InitializeComponent();
        _store = store;

        _store.SnapshotChanged += (_, data) => Dispatcher.Invoke(() => Render(data));

        // The countdown text needs to tick even when the underlying snapshot
        // hasn't changed, so it runs on its own timer rather than only
        // re-rendering on SnapshotChanged.
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => Render(_store.Current);
        _countdownTimer.Start();

        Render(_store.Current);
    }

    public void ShowNearTray()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 12;
        Top = workArea.Bottom - ActualHeight - 12;
        Show();
        Activate();
    }

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    private void Render(SnapshotData? data)
    {
        double session = data?.FiveHour?.UsedPercentage ?? 0;
        double week = data?.SevenDay?.UsedPercentage ?? 0;

        SessionLabel.Text = $"Session (5h) \u00b7 {session:0}%";
        WeekLabel.Text = $"Week (7d) \u00b7 {week:0}%";

        SessionBar.Width = Math.Clamp(session, 0, 100) / 100.0 * TrackWidth;
        WeekBar.Width = Math.Clamp(week, 0, 100) / 100.0 * TrackWidth;
        SessionBar.Background = QuotaPalette.BrushFor(session);
        WeekBar.Background = QuotaPalette.BrushFor(week);

        SessionReset.Text = FormatCountdown(data?.FiveHour?.ResetsAt);
        WeekReset.Text = FormatCountdown(data?.SevenDay?.ResetsAt);

        UpdatedLabel.Text = data is null ? "No data yet" : $"Updated {FormatAgo(data.UpdatedAt)}";
    }

    private static string FormatCountdown(DateTimeOffset? resetsAt)
    {
        if (resetsAt is null) return "\u2014";

        var remaining = resetsAt.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return "Reset pending \u2014 open Claude Code to refresh";

        return remaining.TotalDays >= 1
            ? $"Resets in {(int)remaining.TotalDays}d {remaining.Hours}h"
            : $"Resets in {(int)remaining.TotalHours}h {remaining.Minutes}m";
    }

    private static string FormatAgo(DateTimeOffset updatedAt)
    {
        var span = DateTimeOffset.UtcNow - updatedAt;
        return span.TotalMinutes < 1 ? "just now" : $"{(int)span.TotalMinutes} min ago";
    }

    private void OpenClaudeCode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Opens a normal interactive session in the default terminal;
            // deliberately does NOT pass a prompt — see the plan doc for why
            // this button must not silently spend an API call on its own.
            Process.Start(new ProcessStartInfo("wt.exe", "-p \"Windows PowerShell\" claude")
            {
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Windows Terminal not installed — fall back to plain PowerShell.
            Process.Start(new ProcessStartInfo("powershell.exe", "-NoExit -Command claude")
            {
                UseShellExecute = true
            });
        }
    }
}
