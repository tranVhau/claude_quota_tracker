using System.Windows;

namespace ClaudeQuotaTracker;

public partial class App : System.Windows.Application
{
    private SnapshotStore? _snapshotStore;
    private TrayIconManager? _trayIconManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettings.Load();

        // Keep the "start with Windows" registry entry in sync with the saved
        // setting every launch, in case the user edited it outside the app.
        AutoStartManager.SetEnabled(settings.AutoStartEnabled);

        // Re-register (or clean up) the scheduled tasks on every launch so a
        // manual edit to config.json, or a shift added on another machine via
        // synced config, still takes effect without opening Settings.
        foreach (var shift in settings.Shifts)
            TriggerScheduler.RegisterTask(shift, "claude");

        _snapshotStore = new SnapshotStore();
        _trayIconManager = new TrayIconManager(_snapshotStore, settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        _snapshotStore?.Dispose();
        base.OnExit(e);
    }
}
