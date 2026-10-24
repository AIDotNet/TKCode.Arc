using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One column of a <see cref="DataGrid"/>. The cell value comes from <see cref="ValueGetter"/>, else from
/// <see cref="Binding"/> (a compiled binding against the row item, written back two-way), else from the
/// <see cref="DataGridRow"/> field (or dictionary entry) named by <see cref="Key"/>. Values are normalized to a
/// string, a <see cref="decimal"/>, a <see cref="DateOnly"/> or null by <see cref="Type"/>.
/// </summary>
public class DataGridColumn : AvaloniaObject
{
    /// <summary>Row field the column reads and writes; also the id used by sorting, filters, pinning and hiding.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<DataGridColumn, string>(nameof(Key), string.Empty);

    /// <summary>Header text; also used for copied header rows, exports and accessible names.</summary>
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<DataGridColumn, string>(nameof(Header), string.Empty);

    /// <summary>Starting width in px (default 140). The last column absorbs spare width on a wide grid.</summary>
    public static readonly StyledProperty<double> WidthProperty =
        AvaloniaProperty.Register<DataGridColumn, double>(nameof(Width), 140);

    /// <summary>Narrowest a resize can reach (default 72). Resizing stops at 560px.</summary>
    public static readonly StyledProperty<double> MinWidthProperty =
        AvaloniaProperty.Register<DataGridColumn, double>(nameof(MinWidth), 72);

    /// <summary>Kind of data: Text, Number, Currency, Percent, Select or Date.</summary>
    public static readonly StyledProperty<DataGridColumnType> TypeProperty =
        AvaloniaProperty.Register<DataGridColumn, DataGridColumnType>(nameof(Type));

    /// <summary>Decimal places of Number, Currency and Percent values.</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<DataGridColumn, int>(nameof(Decimals));

    /// <summary>Symbol of a Currency column (default "$"); it does not change with the interface language.</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<DataGridColumn, string>(nameof(CurrencySymbol), "$");

    /// <summary>.NET format of a Date column (such as "yyyy-MM-dd"); null uses the language's short date with the month name.</summary>
    public static readonly StyledProperty<string?> DateFormatProperty =
        AvaloniaProperty.Register<DataGridColumn, string?>(nameof(DateFormat));

    /// <summary>False makes the column read only: edits, pastes, fills and clears skip it.</summary>
    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<DataGridColumn, bool>(nameof(IsEditable), true);

    /// <summary>Choices of a Select column, in sort order; null uses up to 200 distinct values already in the column.</summary>
    public static readonly StyledProperty<IList<string>?> OptionsProperty =
        AvaloniaProperty.Register<DataGridColumn, IList<string>?>(nameof(Options));

    /// <summary>Starts pinned to the left edge. When no column sets it, the first column is pinned.</summary>
    public static readonly StyledProperty<bool?> IsPinnedProperty =
        AvaloniaProperty.Register<DataGridColumn, bool?>(nameof(IsPinned));

    /// <summary>Starts hidden; people can show it again from the Columns menu.</summary>
    public static readonly StyledProperty<bool> IsHiddenProperty =
        AvaloniaProperty.Register<DataGridColumn, bool>(nameof(IsHidden));

    /// <summary>What the totals row and group subtotals show; null sums numbers and currency, averages percent and shows nothing otherwise.</summary>
    public static readonly StyledProperty<DataGridAggregate?> AggregateProperty =
        AvaloniaProperty.Register<DataGridColumn, DataGridAggregate?>(nameof(Aggregate));

    /// <summary>False disables the header sort button and the sort items of the column menu.</summary>
    public static readonly StyledProperty<bool> IsSortableProperty =
        AvaloniaProperty.Register<DataGridColumn, bool>(nameof(IsSortable), true);

    /// <summary>False leaves the filter cell empty and drops Filter from the column menu.</summary>
    public static readonly StyledProperty<bool> IsFilterableProperty =
        AvaloniaProperty.Register<DataGridColumn, bool>(nameof(IsFilterable), true);

    /// <summary>Offers Group by in the column menu; null allows it for Text, Select and Date columns.</summary>
    public static readonly StyledProperty<bool?> IsGroupableProperty =
        AvaloniaProperty.Register<DataGridColumn, bool?>(nameof(IsGroupable));

    /// <summary>
    /// Draws the column's cells in place of the plain text: a status badge, a progress bar, a link, an icon, or your own
    /// <see cref="DataGridCellRenderer"/>. Null draws the formatted value. Sorting, filtering, totals and export still use the value.
    /// </summary>
    public static readonly StyledProperty<DataGridCellRenderer?> RendererProperty =
        AvaloniaProperty.Register<DataGridColumn, DataGridCellRenderer?>(nameof(Renderer));

    /// <summary>
    /// Replaces the text box that edits the column's cells with your own content (a slider, a rating, a picker). The template
    /// gets a <see cref="DataGridEditContext"/> as its data context; only the cell being edited is a real control.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> EditTemplateProperty =
        AvaloniaProperty.Register<DataGridColumn, IDataTemplate?>(nameof(EditTemplate));

    private readonly AvaloniaList<DataGridRule> _rules = new();
    private readonly AvaloniaList<DataGridFormatRule> _formatRules = new();
    private BindingBase? _binding;
    private Func<object, object?>? _valueGetter;
    private Action<object, object?>? _valueSetter;
    private EventHandler? _definitionChanged;
    private DataGridCellRenderer? _followedRenderer;

    public DataGridColumn()
    {
        HookRules();
    }

    public DataGridColumn(string key, string header, DataGridColumnType type = DataGridColumnType.Text)
    {
        Key = key;
        Header = header;
        Type = type;
        HookRules();
    }

    private void HookRules()
    {
        _rules.CollectionChanged += (_, _) => OnDefinitionChanged();
        _formatRules.CollectionChanged += (_, _) => OnDefinitionChanged();
    }

    /// <summary>
    /// A column over typed rows (AOT safe, no bindings). <paramref name="setter"/> receives the normalized value: a
    /// string, a <see cref="decimal"/>, a <see cref="DateOnly"/> or null; null makes the column read only.
    /// </summary>
    public static DataGridColumn Create<T>(string key, string header, Func<T, object?> getter, Action<T, object?>? setter = null,
                                           DataGridColumnType type = DataGridColumnType.Text)
    {
        var column = new DataGridColumn(key, header, type)
        {
            ValueGetter = item => item is T typed ? getter(typed) : null
        };
        if (setter is null)
        {
            column.IsEditable = false;
        }
        else
        {
            column.ValueSetter = (item, value) =>
            {
                if (item is T typed)
                {
                    setter(typed, value);
                }
            };
        }
        return column;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="HeaderProperty"/>
    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <inheritdoc cref="WidthProperty"/>
    public double Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

    /// <inheritdoc cref="MinWidthProperty"/>
    public double MinWidth { get => GetValue(MinWidthProperty); set => SetValue(MinWidthProperty, value); }

    /// <inheritdoc cref="TypeProperty"/>
    public DataGridColumnType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }

    /// <inheritdoc cref="DecimalsProperty"/>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="DateFormatProperty"/>
    public string? DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }

    /// <inheritdoc cref="IsEditableProperty"/>
    public bool IsEditable { get => GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    /// <inheritdoc cref="OptionsProperty"/>
    public IList<string>? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }

    /// <inheritdoc cref="IsPinnedProperty"/>
    public bool? IsPinned { get => GetValue(IsPinnedProperty); set => SetValue(IsPinnedProperty, value); }

    /// <inheritdoc cref="IsHiddenProperty"/>
    public bool IsHidden { get => GetValue(IsHiddenProperty); set => SetValue(IsHiddenProperty, value); }

    /// <inheritdoc cref="AggregateProperty"/>
    public DataGridAggregate? Aggregate { get => GetValue(AggregateProperty); set => SetValue(AggregateProperty, value); }

    /// <inheritdoc cref="IsSortableProperty"/>
    public bool IsSortable { get => GetValue(IsSortableProperty); set => SetValue(IsSortableProperty, value); }

    /// <inheritdoc cref="IsFilterableProperty"/>
    public bool IsFilterable { get => GetValue(IsFilterableProperty); set => SetValue(IsFilterableProperty, value); }

    /// <inheritdoc cref="IsGroupableProperty"/>
    public bool? IsGroupable { get => GetValue(IsGroupableProperty); set => SetValue(IsGroupableProperty, value); }

    /// <inheritdoc cref="RendererProperty"/>
    public DataGridCellRenderer? Renderer { get => GetValue(RendererProperty); set => SetValue(RendererProperty, value); }

    /// <summary>
    /// Checks for the values of the column (required, a range, a length, a pattern, or your own). They run when a value is typed,
    /// pasted, filled or set by a renderer; see <see cref="DataGrid.ValidationMode"/> for what a failure does.
    /// </summary>
    public AvaloniaList<DataGridRule> Rules => _rules;

    /// <summary>Tints cells whose value passes a test, such as a negative amount or a stage that reads "Lost". The first matching rule decides.</summary>
    public AvaloniaList<DataGridFormatRule> FormatRules => _formatRules;

    /// <inheritdoc cref="EditTemplateProperty"/>
    public IDataTemplate? EditTemplate { get => GetValue(EditTemplateProperty); set => SetValue(EditTemplateProperty, value); }

    /// <summary>Compiled binding evaluated against each row item for the cell value; edits write back through it.</summary>
    [AssignBinding]
    [InheritDataTypeFromItems(nameof(DataGrid.ItemsSource), AncestorType = typeof(DataGrid))]
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
    public Func<object, object?>? ValueGetter
    {
        get => _valueGetter;
        set
        {
            _valueGetter = value;
            OnDefinitionChanged();
        }
    }

    /// <summary>Writes a normalized value into a row item (pairs with <see cref="ValueGetter"/>).</summary>
    public Action<object, object?>? ValueSetter
    {
        get => _valueSetter;
        set
        {
            _valueSetter = value;
            OnDefinitionChanged();
        }
    }

    /// <summary>The renderer that draws the cells: <see cref="Renderer"/>, or the checkbox of a Boolean column, else null.</summary>
    internal DataGridCellRenderer? EffectiveRenderer => Renderer ?? (Type == DataGridColumnType.Boolean ? CheckboxRenderer.Instance : null);

    /// <summary>True for Number, Currency and Percent columns.</summary>
    public bool IsNumeric => Type is DataGridColumnType.Number or DataGridColumnType.Currency or DataGridColumnType.Percent;

    /// <summary>The aggregate actually used: <see cref="Aggregate"/>, else the default for <see cref="Type"/>.</summary>
    public DataGridAggregate EffectiveAggregate => Aggregate ?? DataGridAggregates.DefaultFor(Type);

    /// <summary>Whether the column menu offers Group by.</summary>
    public bool EffectiveIsGroupable => IsGroupable ?? !IsNumeric;

    /// <summary>The format the pure helpers (<see cref="DataGridValueFormat"/>, <see cref="DataGridValueParser"/>) take.</summary>
    public DataGridCellFormat CellFormat => new(Type, Decimals, CurrencySymbol, DateFormat);

    /// <summary>
    /// Raised when anything that shapes the column changes, its renderer included. The column follows its renderer only while
    /// this has a listener (an attached grid), so a renderer shared from resources never holds the column or its grid.
    /// </summary>
    internal event EventHandler? DefinitionChanged
    {
        add
        {
            _definitionChanged += value;
            FollowRenderer();
        }
        remove
        {
            _definitionChanged -= value;
            FollowRenderer();
        }
    }

    /// <summary>Counts definition changes, heard or not (a grid compares it to find changes made while it was away).</summary>
    internal int DefinitionVersion { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RendererProperty)
        {
            FollowRenderer();
        }
        OnDefinitionChanged();
    }

    private void FollowRenderer()
    {
        var renderer = _definitionChanged is null ? null : Renderer;
        if (ReferenceEquals(renderer, _followedRenderer))
        {
            return;
        }
        if (_followedRenderer is not null)
        {
            _followedRenderer.Changed -= OnDefinitionChanged;
        }
        _followedRenderer = renderer;
        if (renderer is not null)
        {
            renderer.Changed += OnDefinitionChanged;
        }
    }

    private void OnDefinitionChanged()
    {
        DefinitionVersion++;
        _definitionChanged?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => string.IsNullOrEmpty(Header) ? Key : Header;
}

/// <summary>The columns of a <see cref="DataGrid"/>, in definition order.</summary>
public sealed class DataGridColumnCollection : AvaloniaList<DataGridColumn>
{
}

/// <summary>
/// A row of a <see cref="DataGrid"/> without a class behind it: a stable <see cref="Key"/> and named fields
/// (<c>row["arr"] = 1200m</c>). Fields raise <see cref="INotifyPropertyChanged.PropertyChanged"/> so the grid redraws
/// changes made elsewhere. Works with collection initializers: <c>new DataGridRow("r1") { { "account", "Lumen" } }</c>.
/// </summary>
public sealed class DataGridRow : INotifyPropertyChanged, IEnumerable<KeyValuePair<string, object?>>
{
    private readonly Dictionary<string, object?> _fields = new(StringComparer.Ordinal);

    public DataGridRow(string key)
    {
        Key = key;
    }

    /// <summary>Unique, stable id of the row.</summary>
    public string Key { get; }

    /// <summary>A field value; missing fields read as null. Setting raises PropertyChanged with the field name.</summary>
    public object? this[string field]
    {
        get => _fields.TryGetValue(field, out var value) ? value : null;
        set
        {
            if (_fields.TryGetValue(field, out var old) && Equals(old, value))
            {
                return;
            }
            _fields[field] = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(field));
        }
    }

    /// <summary>The field names that have a value.</summary>
    public IReadOnlyCollection<string> Fields => _fields.Keys;

    /// <summary>Raised when a field changes; the property name is the field name.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Sets a field (collection initializer support).</summary>
    public void Add(string field, object? value) => this[field] = value;

    /// <summary>A copy with the same key and fields.</summary>
    public DataGridRow Clone()
    {
        var copy = new DataGridRow(Key);
        foreach (var pair in _fields)
        {
            copy._fields[pair.Key] = pair.Value;
        }
        return copy;
    }

    /// <summary>Enumerates the fields and their values.</summary>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _fields.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => Key;
}

/// <summary>An extra toolbar action for checked rows, shown between Export and Delete.</summary>
public class DataGridBulkAction : AvaloniaObject
{
    /// <summary>Button text; also the accessible name. The label hides below 640px of grid width.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<DataGridBulkAction, string>(nameof(Label), string.Empty);

    /// <summary>A 24x24 Lucide geometry shown before the label (such as <c>ArcIcons.Archive</c>).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DataGridBulkAction, Geometry?>(nameof(Icon));

    /// <summary>Draws the button in the danger color.</summary>
    public static readonly StyledProperty<bool> IsDangerProperty =
        AvaloniaProperty.Register<DataGridBulkAction, bool>(nameof(IsDanger));

    /// <summary>Runs with the checked rows (an <c>IReadOnlyList&lt;object&gt;</c> in data order) as its parameter.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<DataGridBulkAction, ICommand?>(nameof(Command));

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="IsDangerProperty"/>
    public bool IsDanger { get => GetValue(IsDangerProperty); set => SetValue(IsDangerProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
}
