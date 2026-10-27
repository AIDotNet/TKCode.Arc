using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How <see cref="HorizonChart"/> orders its rows.</summary>
public enum HorizonSort
{
    /// <summary>By name, ascending.</summary>
    Name,

    /// <summary>By the largest absolute value of the row, descending (the default: the biggest swings first).</summary>
    Peak,

    /// <summary>By the row's last value, descending.</summary>
    Latest
}

/// <summary>One row of a <see cref="HorizonChart"/>: a name and one signed deviation from the baseline for each day.</summary>
public sealed class HorizonSeries : AvaloniaObject
{
    /// <summary>A stable identifier for the row (events and announcements); defaults to the name.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<HorizonSeries, string?>(nameof(Key));

    /// <summary>The row's name (shown in the name column, ellipsized with its full text in a tooltip).</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<HorizonSeries, string>(nameof(Name), string.Empty);

    /// <summary>One signed value per day: positive above the baseline, negative below it. NaN draws as zero and reads as missing.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>> ValuesProperty =
        AvaloniaProperty.Register<HorizonSeries, IReadOnlyList<double>>(nameof(Values), Array.Empty<double>());

    /// <summary>Creates an empty row (set <see cref="Name"/> and <see cref="Values"/>).</summary>
    public HorizonSeries()
    {
    }

    /// <summary>Creates a row.</summary>
    public HorizonSeries(string name, IReadOnlyList<double> values, string? key = null)
    {
        Name = name;
        Values = values;
        Key = key;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public IReadOnlyList<double> Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>The key, or the name when there is none.</summary>
    public string EffectiveKey => Key ?? Name;
}

/// <summary>One day of the horizontal axis of a <see cref="HorizonChart"/>, oldest first.</summary>
public sealed class HorizonDay : AvaloniaObject
{
    /// <summary>The short label ("Sep 27"): the table and announcements.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<HorizonDay, string?>(nameof(Label));

    /// <summary>The long label ("Sunday, September 27"): the readout's heading.</summary>
    public static readonly StyledProperty<string?> LongLabelProperty =
        AvaloniaProperty.Register<HorizonDay, string?>(nameof(LongLabel));

    /// <summary>The date; when the labels are not set they are formatted from it ("MMM d" and "dddd, MMMM d").</summary>
    public static readonly StyledProperty<DateOnly?> DateProperty =
        AvaloniaProperty.Register<HorizonDay, DateOnly?>(nameof(Date));

    /// <summary>Creates an empty day.</summary>
    public HorizonDay()
    {
    }

    /// <summary>Creates a day from its date.</summary>
    public HorizonDay(DateOnly date)
    {
        Date = date;
    }

    /// <summary>Creates a day with both labels.</summary>
    public HorizonDay(string label, string longLabel, DateOnly? date = null)
    {
        Label = label;
        LongLabel = longLabel;
        Date = date;
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="LongLabelProperty"/>
    public string? LongLabel { get => GetValue(LongLabelProperty); set => SetValue(LongLabelProperty, value); }

    /// <inheritdoc cref="DateProperty"/>
    public DateOnly? Date { get => GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <summary>The short label, or the date as "MMM d", or empty.</summary>
    public string ShortText => Label ?? Date?.ToString("MMM d", AnalyticsFormat.DefaultCulture) ?? string.Empty;

    /// <summary>The long label, or the date as "dddd, MMMM d", or the short text.</summary>
    public string LongText => LongLabel ?? Date?.ToString("dddd, MMMM d", AnalyticsFormat.DefaultCulture) ?? ShortText;
}

/// <summary>A labelled tick on the day axis of a <see cref="HorizonChart"/>.</summary>
public sealed class HorizonTick : AvaloniaObject
{
    /// <summary>The index of the day the tick marks.</summary>
    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<HorizonTick, int>(nameof(Index));

    /// <summary>The tick's text ("Mar").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<HorizonTick, string>(nameof(Label), string.Empty);

    /// <summary>Creates a tick at day 0 without a label.</summary>
    public HorizonTick()
    {
    }

    /// <summary>Creates a tick.</summary>
    public HorizonTick(int index, string label)
    {
        Index = index;
        Label = label;
    }

    /// <inheritdoc cref="IndexProperty"/>
    public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}

/// <summary>Arguments of <see cref="HorizonChart.SortChanged"/>.</summary>
public sealed class HorizonSortChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public HorizonSortChangedEventArgs(RoutedEvent routedEvent, object? source, HorizonSort sort)
        : base(routedEvent, source)
    {
        Sort = sort;
    }

    /// <summary>The new order.</summary>
    public HorizonSort Sort { get; }
}

/// <summary>Arguments of <see cref="HorizonChart.BandsChanged"/>.</summary>
public sealed class HorizonBandsChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public HorizonBandsChangedEventArgs(RoutedEvent routedEvent, object? source, int bands)
        : base(routedEvent, source)
    {
        Bands = bands;
    }

    /// <summary>The new band count (1–4).</summary>
    public int Bands { get; }
}

/// <summary>Arguments of <see cref="HorizonChart.DayChanged"/>.</summary>
public sealed class HorizonDayChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public HorizonDayChangedEventArgs(RoutedEvent routedEvent, object? source, int? index, HorizonDay? day)
        : base(routedEvent, source)
    {
        Index = index;
        Day = day;
    }

    /// <summary>The index of the crosshair's day, or null when it was cleared.</summary>
    public int? Index { get; }

    /// <summary>The crosshair's day, or null when it was cleared.</summary>
    public HorizonDay? Day { get; }
}
