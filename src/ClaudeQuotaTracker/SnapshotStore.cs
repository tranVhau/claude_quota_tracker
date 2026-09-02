using System.IO;
using System.Text.Json;

namespace ClaudeQuotaTracker;

/// <summary>
/// Owns reading and watching snapshot.json. The bridge script writes via a
/// temp-file-then-rename pattern, so a Changed/Created event can still fire
/// while the file is momentarily locked or half-written; ReloadNow() treats
/// that as a soft failure and keeps the last known good value instead of
/// throwing or clearing the UI.
/// </summary>
public sealed class SnapshotStore : IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher _watcher;
    private System.Threading.Timer? _debounceTimer;

    public SnapshotData? Current { get; private set; }

    /// <summary>Raised on the watcher's background thread — subscribers must marshal to the UI thread themselves.</summary>
    public event EventHandler<SnapshotData?>? SnapshotChanged;

    public SnapshotStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeQuotaTracker");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "snapshot.json");

        _watcher = new FileSystemWatcher(dir, "snapshot.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _watcher.Changed += (_, _) => ScheduleReload();
        _watcher.Created += (_, _) => ScheduleReload();
        _watcher.Renamed += (_, _) => ScheduleReload(); // covers the tmp-file rename

        ReloadNow();
    }

    private void ScheduleReload()
    {
        // Coalesce bursts of filesystem events (rename fires Changed+Created
        // in quick succession on some drivers) into a single reload 250ms later.
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ => ReloadNow(), null, 250, Timeout.Infinite);
    }

    public void ReloadNow()
    {
        try
        {
            if (!File.Exists(_path)) return;

            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var data = JsonSerializer.Deserialize<SnapshotData>(stream);
            Current = data;
            SnapshotChanged?.Invoke(this, data);
        }
        catch (IOException)
        {
            // File is mid-write (temp-file rename in progress) — the next
            // watcher event will retry, so it is safe to ignore this one.
        }
        catch (JsonException)
        {
            // Malformed/partial JSON — keep the last known good snapshot
            // rather than surfacing a broken UI state.
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounceTimer?.Dispose();
    }
}
