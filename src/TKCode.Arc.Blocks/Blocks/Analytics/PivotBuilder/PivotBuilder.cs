using System.Collections;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Build a pivot table by hand: drag fields from the field list into Rows, Columns and Values (or move them with the
/// keyboard from each chip's menu), reorder chips within a zone, pick each value's aggregation, and the table below
/// regroups at once, with a total for every row, every column and the whole.
/// <para>
/// With no records the block pivots built-in invented sales. Give it <see cref="ItemsSource"/> and <see cref="Fields"/>
/// (each field reads one value from a record) to pivot your own; <see cref="RowFields"/>, <see cref="ColumnFields"/> and
/// <see cref="ValueFields"/> hold the layout and change as the person drags.
/// </para>
/// </summary>
public partial class PivotBuilder : TemplatedControl
{
    /// <summary>The records to pivot; null (with no <see cref="Fields"/>) pivots the sample sales.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<PivotBuilder, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Whether the table shows a total column and a total row.</summary>
    public static readonly StyledProperty<bool> ShowTotalsProperty =
        AvaloniaProperty.Register<PivotBuilder, bool>(nameof(ShowTotals), true);

    /// <summary>Draws a bordered card (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<PivotBuilder, bool>(nameof(IsFramed), true);

    /// <summary>The words the block shows and reads out.</summary>
    public static readonly StyledProperty<PivotBuilderLabels> LabelsProperty =
        AvaloniaProperty.Register<PivotBuilder, PivotBuilderLabels>(nameof(Labels), new PivotBuilderLabels());

    /// <summary>The pivot as computed for the current layout.</summary>
    public static readonly DirectProperty<PivotBuilder, PivotBuilderResult?> ResultProperty =
        AvaloniaProperty.RegisterDirect<PivotBuilder, PivotBuilderResult?>(nameof(Result), o => o.Result);

    /// <summary>Raised when a field moved to another zone or place, or a value's aggregation changed, by the person or by code.</summary>
    public static readonly RoutedEvent<PivotBuilderLayoutChangedEventArgs> LayoutChangedEvent =
        RoutedEvent.Register<PivotBuilder, PivotBuilderLayoutChangedEventArgs>(nameof(LayoutChanged), RoutingStrategies.Bubble);

    private PivotBuilderResult? _result;

    /// <summary>The fields a record offers. Empty with no <see cref="ItemsSource"/>: the sample fields.</summary>
    public AvaloniaList<PivotBuilderField> Fields { get; } = new();

    /// <summary>The keys of the fields in Rows, outermost first.</summary>
    public AvaloniaList<string> RowFields { get; } = new();

    /// <summary>The keys of the fields in Columns, outermost first.</summary>
    public AvaloniaList<string> ColumnFields { get; } = new();

    /// <summary>The fields in Values with their aggregations, in column order.</summary>
    public AvaloniaList<PivotBuilderValue> ValueFields { get; } = new();

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="ShowTotalsProperty"/>
    public bool ShowTotals { get => GetValue(ShowTotalsProperty); set => SetValue(ShowTotalsProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public PivotBuilderLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ResultProperty"/>
    public PivotBuilderResult? Result { get => _result; private set => SetAndRaise(ResultProperty, ref _result, value); }

    /// <inheritdoc cref="LayoutChangedEvent"/>
    public event EventHandler<PivotBuilderLayoutChangedEventArgs>? LayoutChanged
    {
        add => AddHandler(LayoutChangedEvent, value);
        remove => RemoveHandler(LayoutChangedEvent, value);
    }
}
