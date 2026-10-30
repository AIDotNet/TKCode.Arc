using Avalonia;
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

/// <summary>
/// One value a <see cref="FilterField"/> can take. <see cref="Hint"/> sits at the end of the row, for example a count.
/// Its properties can be bound in XAML; an open menu follows later changes.
/// </summary>
public sealed class FilterOption : AvaloniaObject
{
    /// <summary>The option's key; also the text shown when there is no <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<FilterOption, string>(nameof(Value), string.Empty);

    /// <summary>The text shown for the option; falls back to <see cref="Value"/>.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FilterOption, string?>(nameof(Label));

    /// <summary>Short text at the end of the row, for example a count.</summary>
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<FilterOption, string?>(nameof(Hint));

    /// <summary>Optional 24x24 stroked glyph (for example <see cref="ArcIcons"/>).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<FilterOption, Geometry?>(nameof(Icon));

    public FilterOption()
    {
    }

    public FilterOption(string value, string? label = null, string? hint = null)
    {
        Value = value;
        Label = label;
        Hint = hint;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="HintProperty"/>
    public string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The text shown for the option (and stored as the chip value).</summary>
    public string Text => Label ?? Value;
}

/// <summary>
/// A filterable field of the Add filter menu: pick the field, then one of its options. Its properties can be bound in
/// XAML; an open menu follows later changes. Chips already applied keep the label they were created with.
/// </summary>
public sealed class FilterField : AvaloniaObject
{
    /// <summary>Stable id; also the id of the chip the field produces, so there is one chip per field.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<FilterField, string>(nameof(Id), string.Empty);

    /// <summary>The field's name in the menu and on its chip ("Status").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FilterField, string>(nameof(Label), string.Empty);

    /// <summary>Optional 24x24 stroked glyph shown before the label.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<FilterField, Geometry?>(nameof(Icon));

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

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The values the field can take (the XAML content of the field).</summary>
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
