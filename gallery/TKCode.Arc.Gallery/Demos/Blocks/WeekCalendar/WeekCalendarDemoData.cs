namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The WeekCalendar demos' calendars, people and events (all invented): a designer's busy week built around the current
/// week, so the current time line shows past and coming events, four sparser weeks either side, and a week whose
/// Wednesday has five all-day events to show the all-day row folding. Times are local wall-clock times in the zone given.
/// </summary>
public static class WeekCalendarDemoData
{
    public const string Studio = "studio";
    public const string Crew = "crew";
    public const string Home = "home";
    public const string Trips = "trips";
    public const string Health = "health";
    public const string Learning = "learning";

    /// <summary>Six calendars, one per preset color; Health starts hidden.</summary>
    public static List<WeekCalendarSource> CreateCalendars() =>
    [
        new(Studio, "Studio", WeekCalendarColor.Blue),
        new(Crew, "Crew", WeekCalendarColor.Violet),
        new(Home, "Home", WeekCalendarColor.Green),
        new(Trips, "Trips", WeekCalendarColor.Amber),
        new(Health, "Health", WeekCalendarColor.Rose) { IsVisible = false },
        new(Learning, "Learning", WeekCalendarColor.Teal),
    ];

    /// <summary>The people invited (generated avatars: initials on a tint).</summary>
    public static List<ProjectPerson> CreatePeople() =>
    [
        new("Iris Vale", "iris"),
        new("Tomas Brandt", "tomas"),
        new("Noor Halim", "noor"),
        new("Kenji Arai", "kenji"),
        new("Lena Moreau", "lena"),
        new("Sami Okafor", "sami"),
        new("Priya Nand", "priya"),
        new("Felix Grau", "felix"),
    ];

    /// <summary>The Monday of the week holding <paramref name="date"/>.</summary>
    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>The Monday of this week in <paramref name="zone"/> (the local zone by default).</summary>
    public static DateOnly ThisMonday(TimeZoneInfo? zone = null)
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, zone ?? TimeZoneInfo.Local);
        return MondayOf(DateOnly.FromDateTime(now.DateTime));
    }

    private sealed class Builder(TimeZoneInfo zone, List<ProjectPerson> people)
    {
        private int _next;

        public List<WeekCalendarEvent> Events { get; } = new();

        private DateTimeOffset At(DateOnly day, double hours)
        {
            var wall = day.ToDateTime(TimeOnly.MinValue).AddHours(hours);
            return new DateTimeOffset(wall, zone.GetUtcOffset(wall));
        }

        public WeekCalendarEvent Timed(DateOnly day, double from, double to, string title, string calendar, string? location = null,
                                       int attendees = 0, int firstPerson = 0)
        {
            var e = new WeekCalendarEvent($"e{++_next}", title, At(day, from), At(day, to), calendar) { Location = location };
            for (var i = 0; i < attendees; i++)
            {
                e.Attendees.Add(people[((firstPerson + i) % people.Count + people.Count) % people.Count]);
            }
            Events.Add(e);
            return e;
        }

        public WeekCalendarEvent AllDay(DateOnly first, int days, string title, string calendar)
        {
            var start = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var e = new WeekCalendarEvent($"e{++_next}", title, start, start.AddDays(days), calendar) { IsAllDay = true };
            Events.Add(e);
            return e;
        }
    }

    /// <summary>
    /// The demo weeks: the busy week of <paramref name="monday"/> (34 timed and 3 all-day events) and four sparser weeks on
    /// each side; later weeks are empty.
    /// </summary>
    public static List<WeekCalendarEvent> CreateEvents(DateOnly monday, TimeZoneInfo? zone = null, List<ProjectPerson>? people = null)
    {
        var b = new Builder(zone ?? TimeZoneInfo.Local, people ?? CreatePeople());
        DateOnly D(int offset) => monday.AddDays(offset);
        for (var d = 0; d < 5; d++)
        {
            b.Timed(D(d), 9, 9.25, "Morning sync", Crew, "Video room", 5, d);
        }
        // Monday
        b.Timed(D(0), 9.5, 11.5, "Write launch brief", Studio);
        b.Timed(D(0), 12, 13, "Lunch with Noor", Home, "Corner Bistro", 1, 2);
        b.Timed(D(0), 13.5, 14, "1:1 with Tomas", Crew, null, 1, 1);
        b.Timed(D(0), 14, 15, "Pricing review", Studio, "Room Maple", 3, 3);
        b.Timed(D(0), 14, 14.5, "Quick check-in", Crew, "Video room");
        b.Timed(D(0), 14.5, 15.25, "Hiring sync", Crew, "Room Maple", 2, 5);
        b.Timed(D(0), 18, 19, "Bouldering", Home, "North Wall Gym");
        // Tuesday
        b.Timed(D(1), 10, 11.5, "Design critique: onboarding", Studio, "Room Cedar, 3rd floor", 4, 0);
        b.Timed(D(1), 11, 11.5, "Portfolio chat", Crew, "Video room", 1, 6);
        b.Timed(D(1), 13, 15, "Prototype the editor", Studio);
        b.Timed(D(1), 15, 15.75, "Client call: Harbor Labs", Studio, "Video room", 2, 3);
        b.Timed(D(1), 17, 17.75, "Physio", Health, "Riverside Clinic");
        // Wednesday
        b.Timed(D(2), 8, 8.5, "Pack for Lisbon", Home);
        b.Timed(D(2), 9.5, 10.5, "Sprint kickoff", Crew, "Room Maple", 5, 1);
        b.Timed(D(2), 10.5, 11.5, "Roadmap pass", Studio);
        b.Timed(D(2), 11, 12, "Interview: staff designer", Crew, "Room Cedar", 2, 4);
        b.Timed(D(2), 12.5, 18 + 5 / 60.0, "Flight to Lisbon", Trips, "Terminal 2, gate B7");
        // Thursday
        b.Timed(D(3), 9.5, 12, "Offsite: strategy", Studio, "Riverside Hall", 4, 2);
        b.Timed(D(3), 12, 13, "Team lunch", Home, "Mercado", 3, 0);
        b.Timed(D(3), 13, 16.5, "Offsite: workshops", Studio, "Riverside Hall", 3, 5);
        b.Timed(D(3), 16.5, 17, "Walk-and-talk with Kenji", Crew, null, 1, 3);
        b.Timed(D(3), 19, 21, "Dinner with friends", Home, "12 Harbour St", 2, 6);
        // Friday
        b.Timed(D(4), 10, 12, "Offsite: wrap-up", Studio, "Riverside Hall", 2, 1);
        b.Timed(D(4), 13, 13.5, "Metrics check", Studio);
        b.Timed(D(4), 15, 21.5, "Flight home", Trips, "Terminal 1");
        // Weekend
        b.Timed(D(5), 8, 9, "Morning run, riverside loop", Home);
        b.Timed(D(5), 9.5, 11, "Flea market stroll", Home, "Old Town Square");
        b.Timed(D(6), 11, 12.5, "Brunch with Lena", Home, "Café Lumen", 1, 4);
        b.Timed(D(6), 17, 17.5, "Plan next week", Studio);
        b.Timed(D(6), 19, 20, "Reading club", Learning, "Library, room 2");
        // All day
        b.AllDay(D(2), 3, "Hotel: Casa Azul", Trips);
        b.AllDay(D(3), 2, "Planning retreat", Studio);
        b.AllDay(D(3), 1, "Dad's birthday", Home);

        // Four quieter weeks on each side.
        foreach (var week in new[] { -4, -3, -2, -1, 1, 2, 3, 4 })
        {
            var m = monday.AddDays(7 * week);
            for (var d = 0; d < 5; d++)
            {
                b.Timed(m.AddDays(d), 9, 9.25, "Morning sync", Crew, "Video room", 5, d + week);
            }
            b.Timed(m, 13, 15, "Focus: deep work", Studio);
            b.Timed(m.AddDays(1), 12, 13, "Lunch with Iris", Home, "Corner Bistro", 1, 0);
            b.Timed(m.AddDays(2), 13, 15, "Focus: deep work", Studio);
            b.Timed(m.AddDays(3), 15.5, 16.5, "Research readout", Studio, "Room Maple", 3, 2);
            b.Timed(m.AddDays(4), 10.5, 12, "Spec writing", Studio);
            if (week % 2 == 0)
            {
                b.Timed(m.AddDays(1), 16, 16.75, "Course: color theory", Learning, "Video room");
                b.Timed(m.AddDays(6), 9, 10.5, "Long run", Home, "Harbor path");
            }
            else
            {
                b.Timed(m.AddDays(2), 10, 11, "Product review", Studio, "Room Cedar", 2, 4);
                b.Timed(m.AddDays(5), 10, 11.5, "Climbing", Home, "North Wall Gym");
            }
            if (week == -2)
            {
                b.AllDay(m.AddDays(4), 1, "Studio day off", Studio);
            }
            if (week == 2)
            {
                b.AllDay(m.AddDays(1), 2, "Conference: Type Days", Learning);
            }
        }
        return b.Events;
    }

    /// <summary>A week whose Wednesday has five all-day events (the row folds after three lanes) and a few timed ones.</summary>
    public static List<WeekCalendarEvent> CreateBusyAllDayWeek(DateOnly monday, TimeZoneInfo? zone = null)
    {
        var b = new Builder(zone ?? TimeZoneInfo.Local, CreatePeople());
        var wednesday = monday.AddDays(2);
        b.AllDay(monday.AddDays(1), 3, "Away: Porto", Trips);
        b.AllDay(wednesday, 1, "Quarter planning", Studio);
        b.AllDay(wednesday, 1, "Report due", Studio);
        b.AllDay(wednesday, 1, "School holiday", Home);
        b.AllDay(wednesday, 1, "Mira's birthday", Home);
        b.AllDay(monday.AddDays(4), 1, "Release freeze", Crew);
        for (var d = 0; d < 5; d++)
        {
            b.Timed(monday.AddDays(d), 9, 9.25, "Morning sync", Crew, "Video room", 5, d);
        }
        b.Timed(monday, 10, 12, "Write launch brief", Studio);
        b.Timed(wednesday, 14, 15, "Planning review", Studio, "Room Maple", 3, 2);
        b.Timed(monday.AddDays(3), 13, 14.5, "Prototype the editor", Studio);
        return b.Events;
    }

    public const string Clients = "clients";

    /// <summary>The calendars of the Editing demo: a small ceramics studio's week.</summary>
    public static List<WeekCalendarSource> CreateEditingCalendars() =>
    [
        new(Studio, "Studio", WeekCalendarColor.Blue),
        new(Clients, "Clients", WeekCalendarColor.Violet),
        new(Home, "Home", WeekCalendarColor.Green),
        new(Trips, "Trips", WeekCalendarColor.Amber),
    ];

    /// <summary>
    /// The Editing demo's week of <paramref name="monday"/>: short and long events, clusters of two and three to show the
    /// columns making room after a drop, an all-day fair and a late Sunday event near the bottom of the day.
    /// </summary>
    public static List<WeekCalendarEvent> CreateEditingWeek(DateOnly monday, TimeZoneInfo? zone = null)
    {
        var b = new Builder(zone ?? TimeZoneInfo.Local, CreatePeople());
        DateOnly D(int offset) => monday.AddDays(offset);
        b.Timed(D(0), 9, 9.25, "Morning check-in", Clients, "Video room");
        b.Timed(D(0), 9.5, 11, "Kiln loading plan", Studio, "Bay 2");
        b.Timed(D(0), 10.5, 11.5, "Glaze test review", Studio);
        b.Timed(D(0), 14, 15, "Clay supplier call", Clients, "Video room", 3, 4);
        b.Timed(D(1), 8, 8.5, "Clay delivery", Studio, "Loading dock");
        b.Timed(D(1), 13, 16.5, "Wheel class", Studio, "Room C", 3, 1);
        b.Timed(D(1), 13.5, 14, "Catch-up with Priya", Clients, null, 1, 6);
        b.AllDay(D(2), 2, "Craft fair setup", Trips);
        b.Timed(D(2), 12.5, 17.75, "Train to Porto", Trips, "Platform 6");
        b.Timed(D(3), 9, 9.25, "Morning check-in", Clients, "Video room");
        b.Timed(D(3), 11, 12, "Product photos", Studio, "Light box");
        b.Timed(D(3), 11.25, 11.75, "Courier pickup", Home);
        b.Timed(D(3), 11.5, 12.5, "Mentor lunch", Home, "Café Verde", 1, 2);
        b.Timed(D(4), 15, 15.75, "Eye exam", Home);
        b.Timed(D(5), 8, 9, "Trail run", Home, "Riverside path");
        b.Timed(D(6), 22.5, 23.5, "Weekly review", Studio);
        return b.Events;
    }

    /// <summary>The zone with <paramref name="id"/> (IANA or Windows), or a fixed-offset stand-in when the system lacks it.</summary>
    public static TimeZoneInfo Zone(string id, TimeSpan fallbackOffset, string name)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception)
        {
            // Browsers and trimmed hosts may not ship every zone; a fixed offset keeps the demo meaningful.
            return TimeZoneInfo.CreateCustomTimeZone(id, fallbackOffset, name, name);
        }
    }
}
