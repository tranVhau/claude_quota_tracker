using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// WinForms is enabled project-wide (for the tray NotifyIcon), so these control
// and brush names collide with System.Windows.Forms / System.Drawing. Pin them
// to the WPF side for this window.
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using GroupBox = System.Windows.Controls.GroupBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;

namespace ClaudeQuotaTracker;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    /// Shift names present when the window opened. A shift's scheduled task is
    /// keyed by its name, so Save needs this to tell which Task Scheduler
    /// entries belong to shifts the user has since removed.
    private readonly List<string> _initialShiftNames;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        _settings = settings;
        _initialShiftNames = settings.Shifts.Select(s => s.Name).ToList();

        foreach (ComboBoxItem item in IntervalCombo.Items)
            if (item.Tag?.ToString() == _settings.RefreshIntervalSeconds.ToString())
                IntervalCombo.SelectedItem = item;

        MetricCombo.SelectedIndex = (int)_settings.Metric;
        AutoStartCheck.IsChecked = _settings.AutoStartEnabled;

        BuildShiftRows();
    }

    /// Pulls a palette brush out of Theme.xaml so rows built here match the
    /// ones declared in XAML instead of drifting to hard-coded named colors.
    private static Brush Themed(string key) =>
        (Brush)Application.Current.Resources[key];

    private void BuildShiftRows()
    {
        ShiftsPanel.Children.Clear();

        if (_settings.Shifts.Count == 0)
        {
            ShiftsPanel.Children.Add(new TextBlock
            {
                Text = "No shifts. Add one to schedule a pre-trigger.",
                Foreground = Themed("ThemeTextSecondary"), FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 10)
            });
        }

        foreach (var shift in _settings.Shifts)
        {
            var box = new GroupBox { Header = BuildShiftHeader(shift), Margin = new Thickness(0, 0, 0, 8) };
            var stack = new StackPanel();

            var timeRow = new StackPanel { Orientation = Orientation.Horizontal };
            var startBox = new TextBox { Text = shift.Start.ToString(@"hh\:mm"), Width = 60 };
            var endBox = new TextBox { Text = shift.End.ToString(@"hh\:mm"), Width = 60, Margin = new Thickness(8, 0, 0, 0) };
            timeRow.Children.Add(startBox);
            timeRow.Children.Add(new TextBlock
            {
                Text = "to", Foreground = Themed("ThemeTextSecondary"),
                Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            });
            timeRow.Children.Add(endBox);
            stack.Children.Add(timeRow);

            var resultText = new TextBlock
            {
                Foreground = Themed("ThemeAccent"), FontSize = 11.5, Margin = new Thickness(0, 8, 0, 0)
            };
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
                Content = "Auto-trigger",
                IsChecked = shift.AutoTriggerEnabled, Margin = new Thickness(0, 10, 0, 0)
            };
            autoCheck.Checked += (_, _) => shift.AutoTriggerEnabled = true;
            autoCheck.Unchecked += (_, _) => shift.AutoTriggerEnabled = false;
            stack.Children.Add(autoCheck);

            box.Content = stack;
            ShiftsPanel.Children.Add(box);
        }

        var addButton = new Button
        {
            Content = "+ Add shift",
            Margin = new Thickness(0, 4, 0, 0),
            // Qualified: the simple name binds to this window's own
            // HorizontalAlignment property inside an instance method.
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };
        addButton.Click += (_, _) =>
        {
            _settings.Shifts.Add(new ShiftConfig
            {
                Name = NextShiftName(),
                Start = new TimeSpan(9, 0, 0),
                End = new TimeSpan(17, 0, 0)
            });
            BuildShiftRows();
        };
        ShiftsPanel.Children.Add(addButton);
    }

    /// <summary>
    /// Builds a shift's GroupBox header: the name, and a remove button pushed
    /// to the right edge. Removal only touches the in-memory list — nothing is
    /// written and no scheduled task is touched until Save, so closing the
    /// window instead is the way to back out of an accidental click.
    /// </summary>
    private UIElement BuildShiftHeader(ShiftConfig shift)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = shift.Name,
            VerticalAlignment = VerticalAlignment.Center
        });

        var remove = new Button
        {
            Content = "✕",
            Style = (Style)Application.Current.Resources["IconDangerButton"],
            ToolTip = $"Remove {shift.Name}"
        };
        remove.Click += (_, _) =>
        {
            _settings.Shifts.Remove(shift);
            BuildShiftRows();
        };
        Grid.SetColumn(remove, 1);
        header.Children.Add(remove);

        return header;
    }

    /// <summary>
    /// First unused "Shift N" name. Scheduled tasks are keyed by shift name,
    /// so duplicates would collide on a single Task Scheduler entry — and
    /// numbering from the list count is not enough once shifts can be removed
    /// (delete "Shift 1" of two, and the next add would reuse "Shift 2").
    /// </summary>
    private string NextShiftName()
    {
        var taken = _settings.Shifts
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; ; i++)
        {
            string name = $"Shift {i}";
            if (!taken.Contains(name)) return name;
        }
    }

    private static void RenderResult(TextBlock target, TimeSpan start, TimeSpan end)
    {
        var (trigger, sessions) = TriggerScheduler.Calculate(start, end);
        target.Text = trigger is null
            ? $"No pre-trigger needed \u00b7 {sessions} sessions"
            : $"Trigger at {trigger:hh\\:mm} \u00b7 {sessions} sessions";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.RefreshIntervalSeconds = int.Parse(((ComboBoxItem)IntervalCombo.SelectedItem).Tag!.ToString()!);
        _settings.Metric = (IconMetric)MetricCombo.SelectedIndex;
        _settings.AutoStartEnabled = AutoStartCheck.IsChecked == true;
        _settings.Save();

        AutoStartManager.SetEnabled(_settings.AutoStartEnabled);

        // Drop the Task Scheduler entries of shifts removed in this session
        // first. Their tasks are named after the shift, so skipping this would
        // leave an orphan firing `claude -p "."` daily with nothing in the app
        // referring to it any more.
        foreach (string name in _initialShiftNames)
            if (!_settings.Shifts.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
                TriggerScheduler.RemoveTask(name);

        foreach (var shift in _settings.Shifts)
            TriggerScheduler.RegisterTask(shift, "claude");

        Close();
    }
}
