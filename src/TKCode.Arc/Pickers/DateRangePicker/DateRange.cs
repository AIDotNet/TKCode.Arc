using Avalonia.Interactivity;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>An inclusive range of whole days.</summary>
public readonly record struct DateRange(DateOnly Start, DateOnly End)
{
    /// <summary>The range between two days in either order.</summary>
    public static DateRange Ordered(DateOnly a, DateOnly b) => a <= b ? new DateRange(a, b) : new DateRange(b, a);

    /// <summary>Number of days, both ends included.</summary>
    public int Days => End.DayNumber - Start.DayNumber + 1;

    /// <summary>True when <paramref name="date"/> lies inside the range (ends included).</summary>
    public bool Contains(DateOnly date) => date >= Start && date <= End;
}

/// <summary>A shortcut in the DateRangePicker preset rail. <see cref="Range"/> receives the viewer's local today.</summary>
public sealed class DateRangePreset
{
    public DateRangePreset(string label, Func<DateOnly, DateRange> range)
    {
        Label = label;
        Range = range;
    }

    /// <summary>Button text ("Last 7 days").</summary>
    public string Label { get; }

    /// <summary>The range for a given today.</summary>
    public Func<DateOnly, DateRange> Range { get; }
}

/// <summary>Arc's default presets.</summary>
public static class DateRangePresets
{
    private static DateOnly MonthStart(DateOnly d) => new(d.Year, d.Month, 1);

    private static readonly (PickerLangResourceKind Label, Func<DateOnly, DateRange> Range)[] s_definitions =
    [
        (PickerLangResourceKind.Today, today => new DateRange(today, today)),
        (PickerLangResourceKind.Yesterday, today => new DateRange(today.AddDays(-1), today.AddDays(-1))),
        (PickerLangResourceKind.LastSevenDays, today => new DateRange(today.AddDays(-6), today)),
        (PickerLangResourceKind.LastThirtyDays, today => new DateRange(today.AddDays(-29), today)),
        (PickerLangResourceKind.ThisMonth, today => new DateRange(MonthStart(today), today)),
        (PickerLangResourceKind.LastMonth, today => new DateRange(MonthStart(today).AddMonths(-1), MonthStart(today).AddDays(-1))),
        (PickerLangResourceKind.ThisQuarter, today => new DateRange(new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1), today)),
        (PickerLangResourceKind.YearToDate, today => new DateRange(new DateOnly(today.Year, 1, 1), today))
    ];

    /// <summary>Today, Yesterday, Last 7 days, Last 30 days, This month, Last month, This quarter, Year to date.</summary>
    public static IReadOnlyList<DateRangePreset> Default { get; } = Array.AsReadOnly(s_definitions.Select(definition =>
        new DateRangePreset(PickerLangResources.Catalog.SourceText(PickerLangResources.Index(definition.Label)), definition.Range)).ToArray());

    internal static string GetDefaultLabel(int index) => PickerLangResources.Get(s_definitions[index].Label);
}

/// <summary>How many months a <see cref="DateRangePicker"/> shows.</summary>
public enum DateRangePickerMonths
{
    /// <summary>Two months beside a preset rail when the boundary is at least 712px wide, otherwise one month under a preset row.</summary>
    Auto,

    /// <summary>Always one month (compact panel).</summary>
    One,

    /// <summary>Always two months.</summary>
    Two
}

/// <summary>Raised when a range is applied.</summary>
public sealed class DateRangePickerSelectedRangeChangedEventArgs : RoutedEventArgs
{
    public DateRangePickerSelectedRangeChangedEventArgs(RoutedEvent routedEvent, DateRange? oldRange, DateRange? newRange)
        : base(routedEvent)
    {
        OldRange = oldRange;
        NewRange = newRange;
    }

    public DateRange? OldRange { get; }

    public DateRange? NewRange { get; }
}
