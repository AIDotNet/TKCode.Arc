using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A way to narrow the results of a <see cref="SearchResults"/>: a <see cref="SearchValueFilter"/> (a group of checkbox
/// values) or a <see cref="SearchDateFilter"/> (a date range). Filters AND together; the values of one value filter OR.
/// </summary>
public abstract class SearchFilter : AvaloniaObject
{
    /// <summary>Names the attribute of <see cref="SearchResultItem"/> the filter reads (see <see cref="SearchResultItem.Attributes"/>).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<SearchFilter, string>(nameof(Key), string.Empty);

    /// <summary>The heading above the filter ("Type", "Team", "Edited").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SearchFilter, string>(nameof(Title), string.Empty);

    /// <summary>A muted line under the filter ("Applies to docs and files").</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<SearchFilter, string?>(nameof(Caption));

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="CaptionProperty"/>
    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <summary>Whether the filter narrows the results right now.</summary>
    public abstract bool IsActive { get; }

    /// <summary>Puts the filter back to "everything".</summary>
    public abstract void Clear();
}

/// <summary>
/// A group of checkbox values over one attribute. Tick several values to see results with any of them. Leave
/// <see cref="Values"/> empty to list the values found in the items (in the order they first appear); list them yourself
/// to fix their order and labels. <see cref="SearchResults"/> writes each value's <see cref="SearchFilterValue.Count"/>:
/// the results that value would show with the query and the other filters applied.
/// </summary>
public class SearchValueFilter : SearchFilter
{
    /// <summary>The values, in the order they are listed.</summary>
    [Content]
    public AvaloniaList<SearchFilterValue> Values { get; } = new();

    /// <summary>Set when the control listed the values itself (the filter had none): it keeps listing the values new items bring.</summary>
    internal bool IsAutoPopulated { get; set; }

    /// <summary>True while at least one value is ticked.</summary>
    public override bool IsActive
    {
        get
        {
            foreach (var value in Values)
            {
                if (value.IsSelected)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The ticked values.</summary>
    public IReadOnlyList<SearchFilterValue> SelectedValues => Values.Where(v => v.IsSelected).ToList();

    /// <summary>Unticks every value.</summary>
    public override void Clear()
    {
        foreach (var value in Values)
        {
            value.IsSelected = false;
        }
    }
}

/// <summary>One value of a <see cref="SearchValueFilter"/>: a checkbox with a label and a count.</summary>
public class SearchFilterValue : AvaloniaObject
{
    /// <summary>What an item's attribute has to equal (ignoring case).</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<SearchFilterValue, string>(nameof(Value), string.Empty);

    /// <summary>The text beside the checkbox; <see cref="Value"/> when null.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SearchFilterValue, string?>(nameof(Label));

    /// <summary>
    /// The results behind the value. <see cref="SearchResults"/> writes it while it filters; with
    /// <see cref="SearchResults.IsFilteringEnabled"/> false it is yours (a server's facet counts) and the control
    /// only reads it. A value with a count of 0 is dimmed and cannot be ticked, but keeps its place.
    /// </summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<SearchFilterValue, int>(nameof(Count));

    /// <summary>Whether the checkbox is ticked (two-way).</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SearchFilterValue, bool>(nameof(IsSelected), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Creates an empty value.</summary>
    public SearchFilterValue()
    {
    }

    /// <summary>Creates a value with its text.</summary>
    public SearchFilterValue(string value, string? label = null)
    {
        Value = value;
        Label = label;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="CountProperty"/>
    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    internal string DisplayLabel => string.IsNullOrEmpty(Label) ? Value : Label;

    /// <summary>The label.</summary>
    public override string ToString() => DisplayLabel;
}

/// <summary>
/// A date range over <see cref="SearchResultItem.Date"/>, edited with a two-thumb slider. The range spans
/// <see cref="Minimum"/> to <see cref="Maximum"/> (the oldest and newest item date when null); <see cref="Start"/> and
/// <see cref="End"/> are the chosen ends (null: that end of the range). Items of kinds outside
/// <see cref="AppliesTo"/> pass the filter, so a range over documents does not hide people.
/// </summary>
public class SearchDateFilter : SearchFilter
{
    /// <summary>The oldest date the slider reaches. Null: the oldest item date.</summary>
    public static readonly StyledProperty<DateTimeOffset?> MinimumProperty =
        AvaloniaProperty.Register<SearchDateFilter, DateTimeOffset?>(nameof(Minimum));

    /// <summary>The newest date the slider reaches. Null: the newest item date.</summary>
    public static readonly StyledProperty<DateTimeOffset?> MaximumProperty =
        AvaloniaProperty.Register<SearchDateFilter, DateTimeOffset?>(nameof(Maximum));

    /// <summary>The chosen start of the range (two-way). Null: the minimum.</summary>
    public static readonly StyledProperty<DateTimeOffset?> StartProperty =
        AvaloniaProperty.Register<SearchDateFilter, DateTimeOffset?>(nameof(Start), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The chosen end of the range (two-way). Null: the maximum.</summary>
    public static readonly StyledProperty<DateTimeOffset?> EndProperty =
        AvaloniaProperty.Register<SearchDateFilter, DateTimeOffset?>(nameof(End), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// The kinds the range applies to, as a comma separated list ("Docs, Files"). Empty: every item. Also words the
    /// default caption ("Applies to docs and files").
    /// </summary>
    public static readonly StyledProperty<string?> AppliesToProperty =
        AvaloniaProperty.Register<SearchDateFilter, string?>(nameof(AppliesTo));

    /// <inheritdoc cref="MinimumProperty"/>
    public DateTimeOffset? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <inheritdoc cref="MaximumProperty"/>
    public DateTimeOffset? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <inheritdoc cref="StartProperty"/>
    public DateTimeOffset? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <inheritdoc cref="EndProperty"/>
    public DateTimeOffset? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <inheritdoc cref="AppliesToProperty"/>
    public string? AppliesTo { get => GetValue(AppliesToProperty); set => SetValue(AppliesToProperty, value); }

    /// <summary>True while the chosen range is narrower than the available one.</summary>
    public override bool IsActive => Start is not null || End is not null;

    /// <summary>Puts both ends back to the full range.</summary>
    public override void Clear()
    {
        Start = null;
        End = null;
    }

    internal bool Applies(string? kind)
    {
        var list = AppliesTo;
        if (string.IsNullOrWhiteSpace(list))
        {
            return true;
        }
        if (kind is null)
        {
            return false;
        }
        foreach (var part in list.Split(','))
        {
            if (string.Equals(part.Trim(), kind, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    internal IEnumerable<string> AppliesToKinds()
    {
        if (string.IsNullOrWhiteSpace(AppliesTo))
        {
            yield break;
        }
        foreach (var part in AppliesTo.Split(','))
        {
            var text = part.Trim();
            if (text.Length > 0)
            {
                yield return text;
            }
        }
    }
}
