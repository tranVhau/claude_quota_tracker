using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// WinForms is enabled project-wide (for the tray NotifyIcon), so these control
// and brush names collide with System.Windows.Forms / System.Drawing. Pin them
// to the WPF side for this window.
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using GroupBox = System.Windows.Controls.GroupBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;

namespace ClaudeQuotaTracker;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        foreach (ComboBoxItem item in IntervalCombo.Items)
            if (item.Tag?.ToString() == _settings.RefreshIntervalSeconds.ToString())
                IntervalCombo.SelectedItem = item;

        MetricCombo.SelectedIndex = (int)_settings.Metric;
        AutoStartCheck.IsChecked = _settings.AutoStartEnabled;

        SnapshotPathBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeQuotaTracker", "snapshot.json");

        BuildShiftRows();
    }

    private void BuildShiftRows()
    {
        ShiftsPanel.Children.Clear();

        foreach (var shift in _settings.Shifts)
        {
            var box = new GroupBox { Header = shift.Name, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) };
            var stack = new StackPanel { Margin = new Thickness(6) };

            var timeRow = new StackPanel { Orientation = Orientation.Horizontal };
            var startBox = new TextBox { Text = shift.Start.ToString(@"hh\:mm"), Width = 60 };
            var endBox = new TextBox { Text = shift.End.ToString(@"hh\:mm"), Width = 60, Margin = new Thickness(8, 0, 0, 0) };
            timeRow.Children.Add(startBox);
            timeRow.Children.Add(new TextBlock
            {
                Text = " to ", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center
            });
            timeRow.Children.Add(endBox);
            stack.Children.Add(timeRow);

            var resultText = new TextBlock { Foreground = Brushes.LightBlue, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) };
            RenderResult(resultText, shift.Start, shift.End);
            stack.Children.Add(resultText);

            void Recalculate()
            {
                if (TimeSpan.TryParse(startBox.Text, out var s) && TimeSpan.TryParse(endBox.Text, out var e))
                {
                    shift.Start = s;
                    shift.End = e;
                    RenderResult(resultText, s, e);
                }
            }
            startBox.LostFocus += (_, _) => Recalculate();
            endBox.LostFocus += (_, _) => Recalculate();

            var autoCheck = new CheckBox
            {
                Content = "Auto-trigger", Foreground = Brushes.White,
                IsChecked = shift.AutoTriggerEnabled, Margin = new Thickness(0, 6, 0, 0)
            };
            autoCheck.Checked += (_, _) => shift.AutoTriggerEnabled = true;
            autoCheck.Unchecked += (_, _) => shift.AutoTriggerEnabled = false;
            stack.Children.Add(autoCheck);

            box.Content = stack;
            ShiftsPanel.Children.Add(box);
        }

        var addButton = new Button { Content = "+ Add shift", Margin = new Thickness(0, 4, 0, 0) };
        addButton.Click += (_, _) =>
        {
            _settings.Shifts.Add(new ShiftConfig
            {
                Name = $"Shift {_settings.Shifts.Count + 1}",
                Start = new TimeSpan(9, 0, 0),
                End = new TimeSpan(17, 0, 0)
            });
            BuildShiftRows();
        };
        ShiftsPanel.Children.Add(addButton);
    }

    private static void RenderResult(TextBlock target, TimeSpan start, TimeSpan end)
    {
        var (trigger, sessions) = TriggerScheduler.Calculate(start, end);
        target.Text = trigger is null
            ? $"No pre-trigger needed \u00b7 {sessions} sessions"
            : $"Trigger at {trigger:hh\\:mm} \u00b7 {sessions} sessions";
    }

    private void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var info = new FileInfo(SnapshotPathBox.Text);
            ConnStatusText.Text = info.Exists
                ? $"Last snapshot: {(int)(DateTime.UtcNow - info.LastWriteTimeUtc).TotalMinutes} min ago"
                : "No snapshot found yet \u2014 open Claude Code once to generate one.";
        }
        catch (Exception ex)
        {
            ConnStatusText.Text = $"Couldn't read snapshot: {ex.Message}";
        }
    }

    private void RegisterStatusLine_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string claudeSettingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

            JsonObject root = new();
            if (File.Exists(claudeSettingsPath))
            {
                var parsed = JsonNode.Parse(File.ReadAllText(claudeSettingsPath)) as JsonObject;
                root = parsed ?? new JsonObject();
                // Back up before touching a file we don't own the rest of.
                File.Copy(claudeSettingsPath, claudeSettingsPath + ".bak", overwrite: true);
            }

            string bridgePath = Path.Combine(AppContext.BaseDirectory, "bridge", "statusline-bridge.ps1");
            string command = $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{bridgePath}\"";

            root["statusLine"] = new JsonObject
            {
                ["type"] = "command",
                ["command"] = command,
                ["refreshInterval"] = _settings.RefreshIntervalSeconds
            };

            Directory.CreateDirectory(Path.GetDirectoryName(claudeSettingsPath)!);
            File.WriteAllText(claudeSettingsPath,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            ConnStatusText.Text = "statusLine registered. Restart any open Claude Code session to apply.";
        }
        catch (Exception ex)
        {
            ConnStatusText.Text = $"Couldn't update settings.json: {ex.Message}";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.RefreshIntervalSeconds = int.Parse(((ComboBoxItem)IntervalCombo.SelectedItem).Tag!.ToString()!);
        _settings.Metric = (IconMetric)MetricCombo.SelectedIndex;
        _settings.AutoStartEnabled = AutoStartCheck.IsChecked == true;
        _settings.Save();

        AutoStartManager.SetEnabled(_settings.AutoStartEnabled);
        foreach (var shift in _settings.Shifts)
            TriggerScheduler.RegisterTask(shift, "claude");

        Close();
    }
}
