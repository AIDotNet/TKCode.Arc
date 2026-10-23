using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvailabilityPickerBoundDemo : UserControl
{
    private readonly List<string> _lines = new();

    public AvailabilityPickerBoundDemo()
    {
        InitializeComponent();
        var week = AvailabilityPickerDemoData.Create();
        Picker.Attendees.AddRange(week.People);
        Picker.Days.AddRange(week.Days);

        Previous.Click += (_, _) => Step(-1);
        Next.Click += (_, _) => Step(1);

        Picker.SelectedDayChanged += (_, e) =>
            Write($"Day {e.OldDay?.Date:ddd d} → {e.NewDay?.Date:ddd d} ({(e.Direction < 0 ? "back" : "forward")}){(e.IsUserInitiated ? "" : ", from code")}");
        Picker.SelectedSlotChanged += (_, e) =>
            Write($"Time {e.OldSlot?.ToString() ?? "none"} → {e.NewSlot?.ToString() ?? "none"}");
        Picker.ConfirmRequested += (_, e) => Write($"Booking {e.DisplayDate}, {e.DisplayTime}…");
        Picker.Confirmed += (_, e) =>
            Write(e.Conflicts.Count == 0
                ? $"Booked {e.Start:ddd HH:mm}–{e.End:HH:mm}"
                : $"Booked {e.Start:ddd HH:mm}; {string.Join(", ", e.Conflicts.Select(p => p.DisplayShortName))} must move a meeting");
        Picker.ConfirmationReset += (_, e) => Write($"Booking of {e.Start:ddd HH:mm} undone");
    }

    private void Step(int direction)
    {
        var days = Picker.Days;
        var index = Picker.SelectedDay is { } day ? days.IndexOf(day) : 0;
        var next = Math.Clamp(index + direction, 0, days.Count - 1);
        Picker.SelectDay(days[next]);
    }

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 6)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
