using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One column of a <see cref="TreeTable"/>. The cell value comes from <see cref="ValueSelector"/> or
/// <see cref="Binding"/> (compiled against the row item in XAML); <see cref="CellTemplate"/> hosts custom content. The
/// first column shown holds the tree: its chevron, indent and guides sit before the cell content. A column is sortable
/// when it has a value (<see cref="SortValueSelector"/> or <see cref="ValueSelector"/>) and <see cref="IsSortable"/> is true.
/// </summary>
public class TreeTableColumn : AvaloniaObject
{
    /// <summary>Column label; also the default <see cref="EffectiveKey"/>.</summary>
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<TreeTableColumn, string>(nameof(Header), string.Empty);

    /// <summary>Stable id reported by sort events and <see cref="TreeTable.SortKey"/>.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<TreeTableColumn, string?>(nameof(Key));

    /// <summary>Arc's grid track: pixels (<c>96</c>), a share of the rest (<c>2*</c>). Defaults to <c>1*</c> (<c>minmax(0, 1fr)</c>).</summary>
    public static readonly StyledProperty<GridLength> WidthProperty =
        AvaloniaProperty.Register<TreeTableColumn, GridLength>(nameof(Width), new GridLength(1, GridUnitType.Star));

    /// <summary>Right-aligns the column and sets its figures in tabular width (Arc <c>align: "end"</c>).</summary>
    public static readonly StyledProperty<bool> IsEndAlignedProperty =
        AvaloniaProperty.Register<TreeTableColumn, bool>(nameof(IsEndAligned));

    /// <summary>Hides the column while the table is narrower than this many pixels (0: never; Arc <c>hideBelow</c>).</summary>
    public static readonly StyledProperty<double> HideBelowProperty =
        AvaloniaProperty.Register<TreeTableColumn, double>(nameof(HideBelow));

    /// <summary>When false the header is a plain label even though the column has a value.</summary>
    public static readonly StyledProperty<bool> IsSortableProperty =
        AvaloniaProperty.Register<TreeTableColumn, bool>(nameof(IsSortable), true);

    /// <summary>Format for cell text: a composite format (<c>{0:N0} MB</c>) or a format string (<c>C0</c>).</summary>
    public static readonly StyledProperty<string?> StringFormatProperty =
        AvaloniaProperty.Register<TreeTableColumn, string?>(nameof(StringFormat));

    /// <summary>Custom cell content (Arc <c>cell</c>); the data context is the row item.</summary>
    public static readonly StyledProperty<IDataTemplate?> CellTemplateProperty =
        AvaloniaProperty.Register<TreeTableColumn, IDataTemplate?>(nameof(CellTemplate));

    private BindingBase? _binding;
    private Func<object, object?>? _valueSelector;
    private Func<object, object?>? _sortValueSelector;
    private Func<object?, string>? _formatter;

    public TreeTableColumn()
    {
    }

    /// <summary>Creates a column whose value comes from a typed selector (AOT safe, no bindings).</summary>
    public static TreeTableColumn Create<T>(string header, Func<T, object?> valueSelector, string? key = null)
        => new()
        {
            Header = header,
            Key = key,
            ValueSelector = item => item is T typed ? valueSelector(typed) : null
        };

    /// <inheritdoc cref="HeaderProperty"/>
    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="WidthProperty"/>
    public GridLength Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

    /// <inheritdoc cref="IsEndAlignedProperty"/>
    public bool IsEndAligned { get => GetValue(IsEndAlignedProperty); set => SetValue(IsEndAlignedProperty, value); }

    /// <inheritdoc cref="HideBelowProperty"/>
    public double HideBelow { get => GetValue(HideBelowProperty); set => SetValue(HideBelowProperty, value); }

    /// <inheritdoc cref="IsSortableProperty"/>
    public bool IsSortable { get => GetValue(IsSortableProperty); set => SetValue(IsSortableProperty, value); }

    /// <inheritdoc cref="StringFormatProperty"/>
    public string? StringFormat { get => GetValue(StringFormatProperty); set => SetValue(StringFormatProperty, value); }

    /// <inheritdoc cref="CellTemplateProperty"/>
    [InheritDataTypeFromItems(nameof(TreeTable.ItemsSource), AncestorType = typeof(TreeTable))]
    public IDataTemplate? CellTemplate { get => GetValue(CellTemplateProperty); set => SetValue(CellTemplateProperty, value); }

    /// <summary>Binding evaluated against each row item for the cell value.</summary>
    [AssignBinding]
    [InheritDataTypeFromItems(nameof(TreeTable.ItemsSource), AncestorType = typeof(TreeTable))]
    public BindingBase? Binding
    {
        get => _binding;
        set
        {
            _binding = value;
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

    /// <summary>Returns the value rows are sorted by (Arc <c>sortValue</c>); defaults to the cell value. Numbers compare numerically, text with natural ordering.</summary>
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

    /// <summary>The id used for <see cref="TreeTable.SortKey"/> and sort events.</summary>
    public string EffectiveKey => Key ?? Header;

    internal bool HasSortValue => _sortValueSelector is not null || _valueSelector is not null || _binding is not null;

    internal event EventHandler? DefinitionChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        OnDefinitionChanged();
    }

    private void OnDefinitionChanged() => DefinitionChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Arguments of <see cref="TreeTable.RowExpandedChanged"/>.</summary>
public sealed class TreeTableExpandedChangedEventArgs : RoutedEventArgs
{
    public TreeTableExpandedChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<object> expandedKeys)
        : base(routedEvent) => ExpandedKeys = expandedKeys;

    /// <summary>The keys of every expanded row.</summary>
    public IReadOnlyList<object> ExpandedKeys { get; }
}

/// <summary>Arguments of <see cref="TreeTable.SelectionChanged"/>.</summary>
public sealed class TreeTableSelectionChangedEventArgs : RoutedEventArgs
{
    public TreeTableSelectionChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<object> selectedKeys, IReadOnlyList<object> selectedItems)
        : base(routedEvent)
    {
        SelectedKeys = selectedKeys;
        SelectedItems = selectedItems;
    }

    /// <summary>The keys of every selected row. Parents are included when all their children are.</summary>
    public IReadOnlyList<object> SelectedKeys { get; }

    /// <summary>The selected row items that are loaded.</summary>
    public IReadOnlyList<object> SelectedItems { get; }
}

/// <summary>Arguments of <see cref="TreeTable.SortChanged"/>.</summary>
public sealed class TreeTableSortChangedEventArgs : RoutedEventArgs
{
    public TreeTableSortChangedEventArgs(RoutedEvent routedEvent, TreeTableColumn? column, DataTableSortDirection direction)
        : base(routedEvent)
    {
        Column = column;
        Direction = direction;
    }

    /// <summary>The sorted column, or null when the sort was cleared.</summary>
    public TreeTableColumn? Column { get; }

    public DataTableSortDirection Direction { get; }
}

/// <summary>Arguments of <see cref="TreeTable.RowActivated"/> and <see cref="TreeTable.ChildrenLoadFailed"/>.</summary>
public sealed class TreeTableRowEventArgs : RoutedEventArgs
{
    public TreeTableRowEventArgs(RoutedEvent routedEvent, object item, object key, Exception? error = null)
        : base(routedEvent)
    {
        Item = item;
        Key = key;
        Error = error;
    }

    /// <summary>The row item.</summary>
    public object Item { get; }

    /// <summary>The row's key.</summary>
    public object Key { get; }

    /// <summary>The exception that failed the load; null for activation.</summary>
    public Exception? Error { get; }
}
