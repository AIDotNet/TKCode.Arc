using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>Sort direction of a <see cref="SortableDataTable"/> column.</summary>
public enum DataTableSortDirection
{
    /// <summary>Rows keep the order of the source.</summary>
    None,
    Ascending,
    Descending
}

/// <summary>
/// One column of a <see cref="SortableDataTable"/>. The cell value comes from <see cref="ValueSelector"/> or
/// <see cref="Binding"/> (compiled against the table's item type in XAML); sorting uses
/// <see cref="SortValueSelector"/> / <see cref="SortBinding"/> when given, else the cell value, and
/// <see cref="Comparer"/> when given, else Arc's comparison (numbers numerically, text with natural ordering,
/// empty values last in both directions). Cells are drawn as text; <see cref="CellTemplate"/> hosts custom content.
/// </summary>
public class DataTableColumn : AvaloniaObject
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<DataTableColumn, string>(nameof(Header), string.Empty);

    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<DataTableColumn, string?>(nameof(Key));

    public static readonly StyledProperty<string?> SortMemberPathProperty =
        AvaloniaProperty.Register<DataTableColumn, string?>(nameof(SortMemberPath));

    public static readonly StyledProperty<bool> IsSortableProperty =
        AvaloniaProperty.Register<DataTableColumn, bool>(nameof(IsSortable), true);

    public static readonly StyledProperty<bool?> IsNumericProperty =
        AvaloniaProperty.Register<DataTableColumn, bool?>(nameof(IsNumeric));

    public static readonly StyledProperty<GridLength> WidthProperty =
        AvaloniaProperty.Register<DataTableColumn, GridLength>(nameof(Width), GridLength.Auto);

    public static readonly StyledProperty<string?> StringFormatProperty =
        AvaloniaProperty.Register<DataTableColumn, string?>(nameof(StringFormat));

    public static readonly StyledProperty<IDataTemplate?> CellTemplateProperty =
        AvaloniaProperty.Register<DataTableColumn, IDataTemplate?>(nameof(CellTemplate));

    private BindingBase? _binding;
    private BindingBase? _sortBinding;
    private Func<object, object?>? _valueSelector;
    private Func<object, object?>? _sortValueSelector;
    private Func<object?, string>? _formatter;
    private IComparer? _comparer;

    public DataTableColumn()
    {
    }

    public DataTableColumn(string header, Func<object, object?> valueSelector)
    {
        Header = header;
        _valueSelector = valueSelector;
    }

    /// <summary>Creates a column whose value comes from a typed selector (AOT safe, no bindings).</summary>
    public static DataTableColumn Create<T>(string header, Func<T, object?> valueSelector, string? key = null)
        => new(header, item => item is T typed ? valueSelector(typed) : null) { Key = key };

    /// <summary>Column label; also the default <see cref="EffectiveKey"/>.</summary>
    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>Stable id reported by <see cref="SortableDataTable.SortKey"/> and sort events (defaults to <see cref="SortMemberPath"/>, then <see cref="Header"/>).</summary>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <summary>Name of the member the column sorts by, reported as the sort key. The sorted value itself comes from <see cref="SortBinding"/>, <see cref="SortValueSelector"/> or the cell value.</summary>
    public string? SortMemberPath { get => GetValue(SortMemberPathProperty); set => SetValue(SortMemberPathProperty, value); }

    /// <summary>When false the header is a plain label (Arc <c>sortable: false</c>).</summary>
    public bool IsSortable { get => GetValue(IsSortableProperty); set => SetValue(IsSortableProperty, value); }

    /// <summary>Right-aligns the column. Null detects it: every non-empty value is a number.</summary>
    public bool? IsNumeric { get => GetValue(IsNumericProperty); set => SetValue(IsNumericProperty, value); }

    /// <summary>Pixel widths are fixed; Auto columns are measured once and held so sorting never reflows them; star columns share the rest.</summary>
    public GridLength Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

    /// <summary>Format for cell text: a composite format (<c>{0:N0} items</c>) or a format string (<c>C0</c>).</summary>
    public string? StringFormat { get => GetValue(StringFormatProperty); set => SetValue(StringFormatProperty, value); }

    /// <summary>Custom cell content (Arc <c>render</c>); the data context is the row item.</summary>
    [InheritDataTypeFromItems(nameof(SortableDataTable.ItemsSource), AncestorType = typeof(SortableDataTable))]
    public IDataTemplate? CellTemplate { get => GetValue(CellTemplateProperty); set => SetValue(CellTemplateProperty, value); }

    /// <summary>Binding evaluated against each row item for the cell value.</summary>
    [AssignBinding]
    [InheritDataTypeFromItems(nameof(SortableDataTable.ItemsSource), AncestorType = typeof(SortableDataTable))]
    public BindingBase? Binding
    {
        get => _binding;
        set
        {
            _binding = value;
            OnDefinitionChanged();
        }
    }

    /// <summary>Binding evaluated against each row item for the sort value.</summary>
    [AssignBinding]
    [InheritDataTypeFromItems(nameof(SortableDataTable.ItemsSource), AncestorType = typeof(SortableDataTable))]
    public BindingBase? SortBinding
    {
        get => _sortBinding;
        set
        {
            _sortBinding = value;
            OnDefinitionChanged();
        }
    }

    /// <summary>Returns the cell value of a row item (takes precedence over <see cref="Binding"/>).</summary>
    public Func<object, object?>? ValueSelector
    {
        get => _valueSelector;
        set
        {
            _valueSelector = value;
            OnDefinitionChanged();
        }
    }

    /// <summary>Returns the sort value of a row item (takes precedence over <see cref="SortBinding"/>).</summary>
    public Func<object, object?>? SortValueSelector
    {
        get => _sortValueSelector;
        set
        {
            _sortValueSelector = value;
            OnDefinitionChanged();
        }
    }

    /// <summary>Formats a cell value to text (takes precedence over <see cref="StringFormat"/>).</summary>
    public Func<object?, string>? Formatter
    {
        get => _formatter;
        set
        {
            _formatter = value;
            OnDefinitionChanged();
        }
    }

    /// <summary>Compares two sort values of this column (ascending order); empty values still go last.</summary>
    public IComparer? Comparer
    {
        get => _comparer;
        set
        {
            _comparer = value;
            OnDefinitionChanged();
        }
    }

    /// <summary>The id used for <see cref="SortableDataTable.SortKey"/>.</summary>
    public string EffectiveKey => Key ?? SortMemberPath ?? Header;

    internal event EventHandler? DefinitionChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        OnDefinitionChanged();
    }

    private void OnDefinitionChanged() => DefinitionChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Arguments of <see cref="SortableDataTable.SortChanged"/>.</summary>
public sealed class DataTableSortChangedEventArgs : RoutedEventArgs
{
    public DataTableSortChangedEventArgs(RoutedEvent routedEvent, DataTableColumn? column, DataTableSortDirection direction)
        : base(routedEvent)
    {
        Column = column;
        Direction = direction;
    }

    /// <summary>The sorted column, or null when the sort was cleared.</summary>
    public DataTableColumn? Column { get; }

    /// <summary>The column's <see cref="DataTableColumn.EffectiveKey"/>.</summary>
    public string? Key => Column?.EffectiveKey;

    public DataTableSortDirection Direction { get; }
}

/// <summary>Arguments of <see cref="SortableDataTable.SelectionChanged"/>.</summary>
public sealed class DataTableSelectionChangedEventArgs : RoutedEventArgs
{
    public DataTableSelectionChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<object> selectedItems,
                                              IReadOnlyList<object> addedItems, IReadOnlyList<object> removedItems)
        : base(routedEvent)
    {
        SelectedItems = selectedItems;
        AddedItems = addedItems;
        RemovedItems = removedItems;
    }

    /// <summary>Selected rows in display order.</summary>
    public IReadOnlyList<object> SelectedItems { get; }

    public IReadOnlyList<object> AddedItems { get; }

    public IReadOnlyList<object> RemovedItems { get; }
}
