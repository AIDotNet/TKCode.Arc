using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WeekCalendarEditingDemo : UserControl
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private readonly List<string> _entries = new();

    public WeekCalendarEditingDemo()
    {
        InitializeComponent();
        Calendar.Calendars.AddRange(WeekCalendarDemoData.CreateEditingCalendars());
        Calendar.Events.AddRange(WeekCalendarDemoData.CreateEditingWeek(WeekCalendarDemoData.ThisMonday()));
        // The calendar has already changed the events (and Events) when these run; persist from here.
        Calendar.EventCreated += (_, e) => Write($"Created \"{e.Event.Title}\", {When(e.Event)}");
        Calendar.EventUpdated += (_, e) => Write(Describe(e.Previous, e.Event));
        Calendar.EventDeleted += (_, e) => Write($"Deleted \"{e.Event.Title}\"");
        ReadOnly.IsCheckedChanged += (_, _) => Calendar.IsReadOnly = ReadOnly.IsChecked == true;
    }

    private static string When(WeekCalendarEvent e) =>
        e.Start.ToLocalTime().ToString("ddd h:mm tt", English) + " – " + e.End.ToLocalTime().ToString("h:mm tt", English);

    private string Describe(WeekCalendarEvent before, WeekCalendarEvent now)
    {
        if (before.Title != now.Title)
        {
            return $"Renamed \"{before.Title}\" to \"{now.Title}\"";
        }
        if (before.CalendarId != now.CalendarId)
        {
            return $"Moved \"{now.Title}\" from {NameOf(before.CalendarId)} to {NameOf(now.CalendarId)}";
        }
        if (before.Location != now.Location)
        {
            return $"\"{now.Title}\" is now at {now.Location ?? "no place"}";
        }
        return $"Moved \"{now.Title}\" from {When(before)} to {When(now)}";
    }

    private string NameOf(string? id) => Calendar.Calendars.FirstOrDefault(c => c.Id == id)?.Name ?? "no calendar";

    private void Write(string entry)
    {
        // Newest first, the last six changes.
        _entries.Insert(0, entry);
        if (_entries.Count > 6)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _entries);
    }
}
