using System.Diagnostics;

namespace ClaudeQuotaTracker;

/// <summary>
/// Computes the optimal pre-trigger time for a work shift and registers it
/// as a daily Windows Task Scheduler entry — deliberately NOT an in-process
/// timer, since the tray app may not be running yet (fresh boot, before
/// login, or after a crash) when the trigger time arrives. schtasks.exe
/// fires independently of this process.
/// </summary>
public static class TriggerScheduler
{
    private const double WindowHours = 5.0;
    private const double SafetyBufferHours = 10.0 / 60.0; // 10 minutes

    /// <summary>
    /// Returns the recommended trigger time (local clock time) and the
    /// number of rolling 5-hour sessions that will touch the shift if
    /// triggered at that time. Returns a null trigger time when the shift
    /// length is an exact multiple of 5 hours, since no pre-trigger helps
    /// in that case (see the plan doc, section 2.1 / 3.1).
    /// </summary>
    public static (TimeSpan? triggerTime, int sessions) Calculate(TimeSpan shiftStart, TimeSpan shiftEnd)
    {
        double start = shiftStart.TotalHours;
        double end = shiftEnd.TotalHours;
        double duration = end - start;
        if (duration <= 0) duration += 24; // overnight shift (e.g. 22:00-02:00)

        double remainder = duration % WindowHours;
        double delta = remainder == 0 ? 0 : WindowHours - remainder;
        int sessions = (int)Math.Floor((duration + delta) / WindowHours) + 1;

        if (delta == 0) return (null, sessions);

        double triggerHours = start - delta - SafetyBufferHours;
        while (triggerHours < 0) triggerHours += 24;

        return (TimeSpan.FromHours(triggerHours), sessions);
    }

    public static void RegisterTask(ShiftConfig shift, string claudeExePath)
    {
        var (triggerTime, _) = Calculate(shift.Start, shift.End);

        if (triggerTime is null || !shift.AutoTriggerEnabled)
        {
            RemoveTask(shift.Name);
            return;
        }

        string taskName = TaskNameFor(shift.Name);
        string time = triggerTime.Value.ToString(@"hh\:mm");
        // A trivial single-character prompt is enough to force one real API
        // response, which is what actually opens the new 5-hour window —
        // see the plan doc's explanation of why this differs from the
        // (rejected) idea of polling quota with an empty request in a loop.
        string action = $"\"{claudeExePath}\" -p \".\"";

        // /F overwrites an existing task with the same name, so calling this
        // again after the user edits a shift's time just updates it in place.
        RunSchtasks($"/Create /F /SC DAILY /TN \"{taskName}\" /TR {action} /ST {time}");
    }

    public static void RemoveTask(string shiftName) =>
        RunSchtasks($"/Delete /F /TN \"{TaskNameFor(shiftName)}\"");

    private static string TaskNameFor(string shiftName) => $"ClaudeQuotaTracker_{shiftName}";

    private static void RunSchtasks(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit();

            // TODO: surface a non-zero ExitCode / StandardError to the
            // Settings UI (e.g. via ConnStatusText in SettingsWindow)
            // instead of silently swallowing scheduling failures.
        }
        catch (Exception)
        {
            // schtasks.exe missing or blocked by policy — leave the task
            // unregistered rather than crashing the tray app on startup.
        }
    }
}
