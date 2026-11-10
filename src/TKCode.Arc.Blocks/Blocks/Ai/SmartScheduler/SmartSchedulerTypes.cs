using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A busy stretch in a person's week: a day of the shown week (0 is its first day) and hours as decimals (13.5 is 1:30 PM).</summary>
/// <param name="Day">The day, from 0.</param>
/// <param name="Start">When it starts, in hours.</param>
/// <param name="End">When it ends, in hours.</param>
/// <param name="Label">What it is ("Focus time"); never shown to others, only counted.</param>
public sealed record SmartSchedulerBusy(int Day, double Start, double End, string? Label = null);

/// <summary>Someone who could join the meeting, with their busy times for the shown week.</summary>
public sealed record SmartSchedulerPerson
{
    /// <summary>Your identifier of the person.</summary>
    public required string Id { get; init; }

    /// <summary>The person's name; also their generated avatar's initials.</summary>
    public required string Name { get; init; }

    /// <summary>A line under the name (role, time zone); null leaves it out.</summary>
    public string? Detail { get; init; }

    /// <summary>When they are busy in the shown week.</summary>
    public IReadOnlyList<SmartSchedulerBusy> Busy { get; init; } = [];

    /// <summary>Whether they start invited.</summary>
    public bool IsInvited { get; init; } = true;
}

/// <summary>A proposed meeting time, with why it was picked.</summary>
public sealed record SmartSchedulerSlot
{
    /// <summary>The day, from 0 (the first day of the shown week).</summary>
    public int Day { get; init; }

    /// <summary>When it starts, in hours.</summary>
    public double Start { get; init; }

    /// <summary>When it ends, in hours.</summary>
    public double End { get; init; }

    /// <summary>Short reasons, best first ("Everyone is free", "In the afternoon, as asked").</summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];

    /// <summary>How well it fits; higher is better. Slots are ranked by it.</summary>
    public double Score { get; init; }
}

/// <summary>What the person asked for, read from their words by <see cref="SmartSchedulerPlanner.Parse"/>.</summary>
public sealed record SmartSchedulerConstraints
{
    /// <summary>Days of the shown week that are ruled out (0 is the first day).</summary>
    public IReadOnlyList<int> ExcludedDays { get; init; } = [];

    /// <summary>The earliest start, in hours; null for none.</summary>
    public double? NotBefore { get; init; }

    /// <summary>The latest end, in hours; null for none.</summary>
    public double? NotAfter { get; init; }

    /// <summary>Keep 12:00 to 13:00 free.</summary>
    public bool AvoidLunch { get; init; }

    /// <summary>Prefer the earliest times that work.</summary>
    public bool PreferSoon { get; init; }

    /// <summary>Leave half an hour free before and after for everyone where possible.</summary>
    public bool PreferBuffer { get; init; }

    /// <summary>The meeting's length in minutes when the words named one ("45 min"); null keeps the chosen length.</summary>
    public int? DurationMinutes { get; init; }

    /// <summary>Short descriptions of what was understood, shown as chips ("Afternoons", "Not Friday").</summary>
    public IReadOnlyList<string> Understood { get; init; } = [];
}

/// <summary>Everything a slot finder needs.</summary>
/// <param name="People">The invited people.</param>
/// <param name="DurationMinutes">How long the meeting is.</param>
/// <param name="Constraints">What was asked for.</param>
/// <param name="Days">How many days the week shows.</param>
/// <param name="DayStart">The first hour of a working day.</param>
/// <param name="DayEnd">The last hour of a working day.</param>
public sealed record SmartSchedulerRequest(IReadOnlyList<SmartSchedulerPerson> People, int DurationMinutes,
    SmartSchedulerConstraints Constraints, int Days, double DayStart, double DayEnd);

/// <summary>Arguments of <see cref="SmartScheduler.SlotBooked"/>.</summary>
public sealed class SmartSchedulerBookedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SmartSchedulerBookedEventArgs(RoutedEvent routedEvent, SmartSchedulerSlot slot, DateTime start, DateTime end,
        IReadOnlyList<SmartSchedulerPerson> people) : base(routedEvent)
    {
        Slot = slot;
        Start = start;
        End = end;
        People = people;
    }

    /// <summary>The slot that was booked.</summary>
    public SmartSchedulerSlot Slot { get; }

    /// <summary>When it starts (local time).</summary>
    public DateTime Start { get; }

    /// <summary>When it ends (local time).</summary>
    public DateTime End { get; }

    /// <summary>Who is invited.</summary>
    public IReadOnlyList<SmartSchedulerPerson> People { get; }
}

/// <summary>Where a <see cref="SmartScheduler"/> stands.</summary>
public enum SmartSchedulerState
{
    /// <summary>Nothing searched yet.</summary>
    Idle,

    /// <summary>Looking for times.</summary>
    Finding,

    /// <summary>Proposals are shown.</summary>
    Proposed,

    /// <summary>Nothing works for everyone.</summary>
    NoTimes,

    /// <summary>Booking the chosen time.</summary>
    Booking,

    /// <summary>The meeting is booked.</summary>
    Booked
}

/// <summary>
/// Every word a <see cref="SmartScheduler"/> shows or reads out, with English defaults. Assign a new instance to
/// <see cref="SmartScheduler.Labels"/>.
/// </summary>
public sealed record SmartSchedulerLabels
{
    /// <summary>The heading ("Find a time").</summary>
    public string Title { get; init; } = "Find a time";

    /// <summary>The people heading ("Who").</summary>
    public string People { get; init; } = "Who";

    /// <summary>The length heading ("How long").</summary>
    public string Duration { get; init; } = "How long";

    /// <summary>A length option ("30 min").</summary>
    public Func<int, string> Minutes { get; init; } = static minutes => minutes % 60 == 0 && minutes >= 60 ? $"{minutes / 60} h" : $"{minutes} min";

    /// <summary>The constraints heading ("Anything else?").</summary>
    public string Constraints { get; init; } = "Anything else?";

    /// <summary>The constraints placeholder.</summary>
    public string ConstraintsPlaceholder { get; init; } = "Afternoons, not Friday, keep lunch free";

    /// <summary>The find button ("Find times").</summary>
    public string Find { get; init; } = "Find times";

    /// <summary>The line before any search.</summary>
    public string Idle { get; init; } = "Pick who comes and say what matters; the best times show up on the week.";

    /// <summary>The line while finding ("Comparing calendars…").</summary>
    public string Finding { get; init; } = "Comparing calendars…";

    /// <summary>The line when nothing works.</summary>
    public string NoTimes { get; init; } = "No time this week works for everyone. Try a shorter meeting or fewer limits.";

    /// <summary>The proposals heading ("Best times").</summary>
    public string Proposals { get; init; } = "Best times";

    /// <summary>The book button from the slot's text ("Book Tue 2:00 PM").</summary>
    public Func<string, string> Book { get; init; } = static slot => $"Book {slot}";

    /// <summary>The booked line from the slot's text and the number of people.</summary>
    public Func<string, int, string> Booked { get; init; } = static (slot, people) => $"Booked for {slot}. Invites went to {people} people.";

    /// <summary>The button that starts over after booking.</summary>
    public string StartOver { get; init; } = "Find another time";

    /// <summary>The week's accessible name.</summary>
    public string Week { get; init; } = "Week with busy times and proposed slots";

    /// <summary>Hour labels on the week ("9 AM").</summary>
    public Func<int, string> Hour { get; init; } = static hour => hour == 12 ? "12 PM" : hour < 12 ? $"{hour} AM" : $"{hour - 12} PM";

    /// <summary>A slot's text from its start and end ("Tue, Nov 17 · 2:00–2:30 PM").</summary>
    public Func<DateTime, DateTime, string> Slot { get; init; } = static (start, end) =>
        $"{start.ToString("ddd, MMM d", System.Globalization.CultureInfo.CurrentCulture)} · {start.ToString("h:mm", System.Globalization.CultureInfo.CurrentCulture)}–{end.ToString("h:mm tt", System.Globalization.CultureInfo.CurrentCulture)}";

    /// <summary>A slot's short text for the book button ("Tue 2:00 PM").</summary>
    public Func<DateTime, string> SlotShort { get; init; } = static start => start.ToString("ddd h:mm tt", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>The ranked slot's accessible name ("Option 1: Tue, Nov 17 · 2:00–2:30 PM").</summary>
    public Func<int, string, string> Option { get; init; } = static (rank, slot) => $"Option {rank}: {slot}";

    /// <summary>The reasons the built-in planner gives and the chips it shows.</summary>
    public SmartSchedulerReasons Reasons { get; init; } = new();
}

/// <summary>The words of the built-in planner's reasons and chips, with English defaults.</summary>
public sealed record SmartSchedulerReasons
{
    /// <summary>Everyone invited is free.</summary>
    public string EveryoneFree { get; init; } = "Everyone is free";

    /// <summary>A break before and after.</summary>
    public string Buffer { get; init; } = "Leaves a break before and after";

    /// <summary>The earliest time that works.</summary>
    public string Soonest { get; init; } = "Earliest time that works";

    /// <summary>Outside lunch.</summary>
    public string Lunch { get; init; } = "Keeps lunch free";

    /// <summary>Morning chip and reason.</summary>
    public string Mornings { get; init; } = "Mornings";

    /// <summary>Afternoon chip and reason.</summary>
    public string Afternoons { get; init; } = "Afternoons";

    /// <summary>A ruled-out day chip ("Not Friday").</summary>
    public Func<string, string> NotDay { get; init; } = static day => $"Not {day}";

    /// <summary>An earliest-start chip ("After 10 AM").</summary>
    public Func<string, string> After { get; init; } = static time => $"After {time}";

    /// <summary>A latest-end chip ("Before 4 PM").</summary>
    public Func<string, string> Before { get; init; } = static time => $"Before {time}";

    /// <summary>A length chip ("45 min").</summary>
    public Func<int, string> Length { get; init; } = static minutes => $"{minutes} min";

    /// <summary>The "as soon as possible" chip.</summary>
    public string Soon { get; init; } = "As soon as possible";

    /// <summary>The "lunch" chip.</summary>
    public string LunchChip { get; init; } = "Keep lunch free";

    /// <summary>The "breaks" chip.</summary>
    public string BufferChip { get; init; } = "Breaks around it";

    /// <summary>A slot with the most free time around it for everyone.</summary>
    public string Quiet { get; init; } = "Quietest part of the day";
}

/// <summary>Invented sample data for <see cref="SmartScheduler"/>: its defaults.</summary>
public static class SmartSchedulerSamples
{
    /// <summary>The sample week starts on Monday, November 16 2026.</summary>
    public static readonly DateOnly WeekStart = new(2026, 11, 16);

    /// <summary>How long the simulated search and booking take (0.6 s).</summary>
    public static readonly TimeSpan SimulatedDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>The sample constraints.</summary>
    public const string Constraints = "Afternoons, not Friday, keep lunch free";

    /// <summary>Four people with invented calendars.</summary>
    public static IReadOnlyList<SmartSchedulerPerson> People() =>
    [
        new()
        {
            Id = "ana", Name = "Ana Ribeiro", Detail = "Product · Lisbon",
            Busy =
            [
                new(0, 9, 11, "Focus time"), new(0, 14, 15), new(1, 13, 14.5), new(2, 9, 10), new(2, 15, 17),
                new(3, 13, 13.5), new(3, 16, 17), new(4, 9, 12),
            ],
        },
        new()
        {
            Id = "kwame", Name = "Kwame Mensah", Detail = "Engineering · Accra",
            Busy = [new(0, 10, 12), new(0, 15, 16), new(1, 9, 10), new(1, 15, 16.5), new(2, 13, 14), new(3, 10, 11.5), new(3, 14, 15)],
        },
        new()
        {
            Id = "hana", Name = "Hana Sato", Detail = "Design · Remote",
            Busy = [new(0, 13, 14), new(1, 14.5, 15), new(1, 16.5, 18), new(2, 11, 12), new(3, 15, 16), new(4, 13, 15)],
        },
        new()
        {
            Id = "leo", Name = "Leo Fischer", Detail = "Sales · Berlin", IsInvited = false,
            Busy = [new(0, 9, 10), new(1, 10, 12), new(2, 14, 16), new(3, 9, 10), new(4, 10, 11)],
        },
    ];

    /// <summary>The length choices in minutes.</summary>
    public static IReadOnlyList<int> Durations() => [15, 30, 45, 60];
}
