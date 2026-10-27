using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The order of the panels of a <see cref="SmallMultiples"/>.</summary>
public enum SmallMultiplesSort
{
    /// <summary>Largest latest value first.</summary>
    Latest,

    /// <summary>Fastest growth first.</summary>
    Growth,

    /// <summary>By name, A to Z.</summary>
    Name,
}

/// <summary>How the panels of a <see cref="SmallMultiples"/> scale their y axis.</summary>
public enum SmallMultiplesScale
{
    /// <summary>Each panel fits its own range, so panels compare shape.</summary>
    Independent,

    /// <summary>Every panel starts at zero on one range, so panels compare size.</summary>
    Shared,
}

/// <summary>
/// A group shown in a <see cref="SmallMultiples"/>, which gets a panel of its own. Give it a key, a name and its values,
/// one for each of the block's periods in the same order.
/// </summary>
public class SmallMultiplesSeries : AvaloniaObject
{
    /// <summary>A stable id that keeps the panel (and its animation) when the list is rebuilt; null uses <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<SmallMultiplesSeries, string?>(nameof(Key));

    /// <summary>The panel's heading ("United States").</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<SmallMultiplesSeries, string?>(nameof(Name));

    /// <summary>One value per period, oldest first; a missing or non-finite value leaves a gap in the line.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<SmallMultiplesSeries, IReadOnlyList<double>?>(nameof(Values));

    public SmallMultiplesSeries()
    {
    }

    public SmallMultiplesSeries(string key, string name, IReadOnlyList<double> values)
    {
        Key = key;
        Name = name;
        Values = values;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    internal string EffectiveKey => Key ?? Name ?? "";
}

/// <summary>
/// A step of the x axis that every panel of a <see cref="SmallMultiples"/> shares, named twice: briefly ("Oct 2026"),
/// where a panel says what its growth is measured against, and in full ("October 2026"), in the readout and for screen
/// readers.
/// </summary>
public class SmallMultiplesPeriod : AvaloniaObject
{
    /// <summary>The brief name, as in "vs Oct 2025" under a panel's value.</summary>
    public static readonly StyledProperty<string?> ShortProperty =
        AvaloniaProperty.Register<SmallMultiplesPeriod, string?>(nameof(Short));

    /// <summary>The full name for the readout and screen readers ("October 2026"); without it, <see cref="Short"/> is used.</summary>
    public static readonly StyledProperty<string?> LongProperty =
        AvaloniaProperty.Register<SmallMultiplesPeriod, string?>(nameof(Long));

    public SmallMultiplesPeriod()
    {
    }

    public SmallMultiplesPeriod(string shortLabel, string longLabel)
    {
        Short = shortLabel;
        Long = longLabel;
    }

    /// <inheritdoc cref="ShortProperty"/>
    public string? Short { get => GetValue(ShortProperty); set => SetValue(ShortProperty, value); }

    /// <inheritdoc cref="LongProperty"/>
    public string? Long { get => GetValue(LongProperty); set => SetValue(LongProperty, value); }

    internal string ShortText => Short ?? Long ?? "";

    internal string LongText => Long ?? Short ?? "";
}

/// <summary>Arguments of <see cref="SmallMultiples.SortChanged"/>.</summary>
public sealed class SmallMultiplesSortChangedEventArgs : RoutedEventArgs
{
    public SmallMultiplesSortChangedEventArgs(RoutedEvent routedEvent, SmallMultiplesSort sort) : base(routedEvent)
    {
        Sort = sort;
    }

    /// <summary>The new order.</summary>
    public SmallMultiplesSort Sort { get; }
}

/// <summary>Arguments of <see cref="SmallMultiples.ScaleChanged"/>.</summary>
public sealed class SmallMultiplesScaleChangedEventArgs : RoutedEventArgs
{
    public SmallMultiplesScaleChangedEventArgs(RoutedEvent routedEvent, SmallMultiplesScale scale) : base(routedEvent)
    {
        Scale = scale;
    }

    /// <summary>The new y scale.</summary>
    public SmallMultiplesScale Scale { get; }
}

/// <summary>Arguments of <see cref="SmallMultiples.CrosshairIndexChanged"/>.</summary>
public sealed class SmallMultiplesIndexChangedEventArgs : RoutedEventArgs
{
    public SmallMultiplesIndexChangedEventArgs(RoutedEvent routedEvent, int? index) : base(routedEvent)
    {
        Index = index;
    }

    /// <summary>The period under the synced crosshair, or null when it left the panels.</summary>
    public int? Index { get; }
}
