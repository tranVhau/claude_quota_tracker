using System.Windows.Forms;

namespace ClaudeQuotaTracker;

/// <summary>
/// Owns the single NotifyIcon for the app's lifetime. WPF has no native
/// tray-icon class, so this deliberately uses System.Windows.Forms.NotifyIcon
/// (enabled via UseWindowsForms in the csproj) alongside the WPF popup/settings
/// windows — a common, supported hybrid pattern for WPF tray apps.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly SnapshotStore _snapshotStore;
    private readonly AppSettings _settings;
    private readonly ToolStripMenuItem _intervalMenu;
    private readonly ToolStripMenuItem _lastUpdatedItem;
    private PopupWindow? _popup;

    public TrayIconManager(SnapshotStore snapshotStore, AppSettings settings)
    {
        _snapshotStore = snapshotStore;
        _settings = settings;

        _intervalMenu = new ToolStripMenuItem("Refresh interval");
        _lastUpdatedItem = new ToolStripMenuItem("Last updated: \u2014") { Enabled = false };

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Claude quota",
            ContextMenuStrip = BuildContextMenu()
        };
        _notifyIcon.MouseUp += OnTrayMouseUp;

        _snapshotStore.SnapshotChanged += (_, data) =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => UpdateIcon(data));

        UpdateIcon(_snapshotStore.Current);
    }

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        // Right-click is handled natively by ContextMenuStrip; only left-click needs manual wiring.
        if (e.Button == MouseButtons.Left) TogglePopup();
    }

    private void TogglePopup()
    {
        if (_popup is { IsVisible: true })
        {
            _popup.Hide();
            return;
        }

        _popup ??= new PopupWindow(_snapshotStore);
        _popup.ShowNearTray();
    }

    private void UpdateIcon(SnapshotData? data)
    {
        bool stale = data is null || DateTimeOffset.UtcNow - data.UpdatedAt > TimeSpan.FromHours(2);
        double session = data?.FiveHour?.UsedPercentage ?? 0;
        double week = data?.SevenDay?.UsedPercentage ?? 0;

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = IconRenderer.RenderDualRing(session, week, stale);
        oldIcon?.Dispose();

        _notifyIcon.Text = stale
            ? "Claude quota \u2014 no recent data"
            : $"Session {session:0}%  \u00b7  Week {week:0}%";

        _lastUpdatedItem.Text = data is null
            ? "Last updated: \u2014"
            : $"Last updated: {data.UpdatedAt.LocalDateTime:t}";
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(_lastUpdatedItem);

        // Re-reads snapshot.json and repaints from it. That is all a refresh
        // can do: the numbers only ever change when Claude Code runs its
        // statusLine command and the bridge script rewrites the file, so
        // nothing here can pull fresher data. It is still worth having —
        // FileSystemWatcher does miss events, and this is the manual recovery.
        var refresh = new ToolStripMenuItem("Refresh");
        refresh.Click += (_, _) => _snapshotStore.ReloadNow();
        menu.Items.Add(refresh);

        menu.Items.Add(new ToolStripSeparator());

        foreach (var (label, seconds) in new (string Label, int Seconds)[]
                 { ("1 min", 60), ("5 min", 300), ("30 min", 1800), ("2 hr", 7200) })
        {
            var item = new ToolStripMenuItem(label) { Checked = _settings.RefreshIntervalSeconds == seconds };
            item.Click += (_, _) =>
            {
                _settings.RefreshIntervalSeconds = seconds;
                _settings.Save();
                foreach (ToolStripMenuItem sibling in _intervalMenu.DropDownItems)
                    sibling.Checked = sibling.Text == label;
            };
            _intervalMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(_intervalMenu);

        menu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem("Settings\u2026");
        settingsItem.Click += (_, _) => new SettingsWindow(_settings).Show();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        var quit = new ToolStripMenuItem("Quit");
        quit.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        menu.Items.Add(quit);

        return menu;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
        _popup?.Close();
    }
}
