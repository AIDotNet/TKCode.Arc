using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>An applied filter shown as a chip: "Label · Value". The id is the field id, so one chip per field.</summary>
public sealed class FilterChip : IEquatable<FilterChip>
{
    public FilterChip()
    {
    }

    public FilterChip(string id, string label, string? value = null)
    {
        Id = id;
        Label = label;
        Value = value;
    }

    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string? Value { get; init; }

    /// <summary>"Status: Open", as read by the remove button and announcements.</summary>
    public string Text => string.IsNullOrEmpty(Value) ? Label : $"{Label}: {Value}";

    public bool Equals(FilterChip? other) =>
        other is not null && Id == other.Id && Label == other.Label && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as FilterChip);

    public override int GetHashCode() => HashCode.Combine(Id, Label, Value);

    public override string ToString() => Text;
}

/// <summary>One value a <see cref="FilterField"/> can take. <see cref="Hint"/> sits at the end of the row, for example a count.</summary>
public sealed class FilterOption
{
    public FilterOption()
    {
    }

    public FilterOption(string value, string? label = null, string? hint = null)
    {
        Value = value;
        Label = label;
        Hint = hint;
    }

    public string Value { get; set; } = string.Empty;

    public string? Label { get; set; }

    public string? Hint { get; set; }

    /// <summary>Optional 24x24 stroked glyph (for example <see cref="ArcIcons"/>).</summary>
    public Geometry? Icon { get; set; }

    /// <summary>The text shown for the option (and stored as the chip value).</summary>
    public string Text => Label ?? Value;
}

/// <summary>A filterable field of the Add filter menu: pick the field, then one of its options.</summary>
public sealed class FilterField
{
    public FilterField()
    {
    }

    public FilterField(string id, string label, params string[] options)
    {
        Id = id;
        Label = label;
        foreach (var option in options)
        {
            Options.Add(new FilterOption(option));
        }
    }

    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Optional 24x24 stroked glyph shown before the label.</summary>
    public Geometry? Icon { get; set; }

    [Content]
    public AvaloniaList<FilterOption> Options { get; } = new();
}

/// <summary>Side of the trigger the Add filter panel lines up with (it flips when there is no room).</summary>
public enum FilterMenuAlignment
{
    Start,
    End
}

/// <summary>What happened to the active filters.</summary>
public enum FilterChangeKind
{
    Added,
    Changed,
    Removed,
    Cleared
}

/// <summary>Arguments of <see cref="FilterToolbar.FiltersChanged"/>.</summary>
public sealed class FilterToolbarFiltersChangedEventArgs : RoutedEventArgs
{
    public FilterToolbarFiltersChangedEventArgs(RoutedEvent routedEvent, FilterChangeKind kind, FilterChip? filter,
                                                FilterChip? previous, FilterField? field, IReadOnlyList<FilterChip> activeFilters)
        : base(routedEvent)
    {
        Kind = kind;
        Filter = filter;
        Previous = previous;
        Field = field;
        ActiveFilters = activeFilters;
    }

    public FilterChangeKind Kind { get; }

    /// <summary>The added, changed or removed chip (null when all were cleared).</summary>
    public FilterChip? Filter { get; }

    /// <summary>The replaced chip when <see cref="Kind"/> is <see cref="FilterChangeKind.Changed"/>.</summary>
    public FilterChip? Previous { get; }

    /// <summary>The field picked in the Add filter menu (Added and Changed from the menu).</summary>
    public FilterField? Field { get; }

    /// <summary>The filters after the change.</summary>
    public IReadOnlyList<FilterChip> ActiveFilters { get; }
}

/// <summary>Arguments of <see cref="FilterMenu.FilterSelected"/>.</summary>
public sealed class FilterMenuSelectedEventArgs : RoutedEventArgs
{
    public FilterMenuSelectedEventArgs(RoutedEvent routedEvent, FilterChip filter, FilterField field) : base(routedEvent)
    {
        Filter = filter;
        Field = field;
    }

    /// <summary>A chip whose id is the field id, so a second pick for a field replaces the first.</summary>
    public FilterChip Filter { get; }

    public FilterField Field { get; }
}
