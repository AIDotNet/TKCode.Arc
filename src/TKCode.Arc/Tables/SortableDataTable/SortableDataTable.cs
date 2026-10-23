using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc's sortable data table: compare structured records with sortable columns. Click a header (or press
/// Enter / Space on it) to sort ascending, again for descending; rows slide to their new places on a smooth
/// spring and the sort arrow flips on a snappy one. <see cref="IsSelectable"/> adds a checkbox column, row-click
/// selection, Shift range selection, Escape to clear and a count line with Clear selection. Rows are drawn as
/// light text cells, virtualized for thousands of records, sorted by index (never by container), and the header
/// sticks to the top of the visible area. Below 620px rows fold into two lines and the header becomes a strip of
/// sort pills, as in Arc.
/// </summary>
[TemplatePart(ScrollViewerPart, typeof(ScrollViewer))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[TemplatePart(ClearPart, typeof(AvaloniaButton))]
[PseudoClasses(CompactPseudoClass, SelectablePseudoClass, LoadingPseudoClass, EmptyPseudoClass, SelectionPseudoClass)]
public class SortableDataTable : TemplatedControl
{
    public const string ScrollViewerPart = "PART_ScrollViewer";
    public const string AnnouncerPart = "PART_Announcer";
    public const string ClearPart = "PART_Clear";
    public const string CompactPseudoClass = ":compact";
    public const string SelectablePseudoClass = ":selectable";
    public const string LoadingPseudoClass = ":loading";
    public const string EmptyPseudoClass = ":empty";
    public const string SelectionPseudoClass = ":has-selection";

    #region Public properties

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SortableDataTable, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<DataTableColumn?> SortColumnProperty =
        AvaloniaProperty.Register<SortableDataTable, DataTableColumn?>(nameof(SortColumn), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> SortKeyProperty =
        AvaloniaProperty.Register<SortableDataTable, string?>(nameof(SortKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Sort direction of the sorted column (two-way): None, Ascending or Descending.</summary>
    public static readonly StyledProperty<DataTableSortDirection> SortDirectionProperty =
        AvaloniaProperty.Register<SortableDataTable, DataTableSortDirection>(nameof(SortDirection), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsSortClearableProperty =
        AvaloniaProperty.Register<SortableDataTable, bool>(nameof(IsSortClearable));

    public static readonly StyledProperty<bool> IsSelectableProperty =
        AvaloniaProperty.Register<SortableDataTable, bool>(nameof(IsSelectable));

    public static readonly StyledProperty<IList?> SelectedItemsProperty =
        AvaloniaProperty.Register<SortableDataTable, IList?>(nameof(SelectedItems), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Func<object, object>?> RowKeySelectorProperty =
        AvaloniaProperty.Register<SortableDataTable, Func<object, object>?>(nameof(RowKeySelector));

    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<SortableDataTable, string>(nameof(Caption), "Data table");

    /// <summary>Text shown when there are no rows (default "No rows to show").</summary>
    public static readonly StyledProperty<string> EmptyMessageProperty =
        AvaloniaProperty.Register<SortableDataTable, string>(nameof(EmptyMessage), "No rows to show");

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<SortableDataTable, bool>(nameof(IsLoading));

    /// <summary>Text shown with the loading spinner (default "Loading rows").</summary>
    public static readonly StyledProperty<string> LoadingMessageProperty =
        AvaloniaProperty.Register<SortableDataTable, string>(nameof(LoadingMessage), "Loading rows");

    public static readonly StyledProperty<string> ItemNameOneProperty =
        AvaloniaProperty.Register<SortableDataTable, string>(nameof(ItemNameOne), "row");

    public static readonly StyledProperty<string> ItemNameOtherProperty =
        AvaloniaProperty.Register<SortableDataTable, string>(nameof(ItemNameOther), "rows");

    /// <summary>Word for the selected rows in the count line, as in "2 selected" (default "selected").</summary>
    public static readonly StyledProperty<string> SelectedNounProperty =
        AvaloniaProperty.Register<SortableDataTable, string>(nameof(SelectedNoun), "selected");

    /// <summary>Accessible name of the select-all checkbox in the header (default "Select all rows").</summary>
    public static readonly StyledProperty<string> SelectAllLabelProperty =
        AvaloniaProperty.Register<SortableDataTable, string>(nameof(SelectAllLabel), "Select all rows");

    /// <summary>Text of the Clear selection action in the count line (default "Clear selection").</summary>
    public static readonly StyledProperty<string> ClearSelectionLabelProperty =
        AvaloniaProperty.Register<SortableDataTable, string>(nameof(ClearSelectionLabel), "Clear selection");

    public static readonly RoutedEvent<DataTableSortChangedEventArgs> SortChangedEvent =
        RoutedEvent.Register<SortableDataTable, DataTableSortChangedEventArgs>(nameof(SortChanged), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<DataTableSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<SortableDataTable, DataTableSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Row items (any enumerable; <see cref="INotifyCollectionChanged"/> sources are followed).</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>Column definitions (the XAML content of the table).</summary>
    [Content]
    public AvaloniaList<DataTableColumn> Columns { get; } = new();

    /// <summary>The sorted column (two-way). Setting a column while <see cref="SortDirection"/> is None sorts ascending.</summary>
    public DataTableColumn? SortColumn { get => GetValue(SortColumnProperty); set => SetValue(SortColumnProperty, value); }

    /// <summary>The sorted column's <see cref="DataTableColumn.EffectiveKey"/> (two-way, for view models).</summary>
    public string? SortKey { get => GetValue(SortKeyProperty); set => SetValue(SortKeyProperty, value); }

    /// <inheritdoc cref="SortDirectionProperty"/>
    public DataTableSortDirection SortDirection { get => GetValue(SortDirectionProperty); set => SetValue(SortDirectionProperty, value); }

    /// <summary>When true a third click on a sorted header clears the sort (Arc toggles between ascending and descending).</summary>
    public bool IsSortClearable { get => GetValue(IsSortClearableProperty); set => SetValue(IsSortClearableProperty, value); }

    /// <summary>Adds the checkbox column, row-click selection and the count line with Clear selection.</summary>
    public bool IsSelectable { get => GetValue(IsSelectableProperty); set => SetValue(IsSelectableProperty, value); }

    /// <summary>Selected row items in display order (two-way). A bound mutable list is updated in place.</summary>
    public IList? SelectedItems { get => GetValue(SelectedItemsProperty); set => SetValue(SelectedItemsProperty, value); }

    /// <summary>Row identity (Arc <c>rowKey</c>); defaults to the item itself. Selection and row motion follow keys across source changes.</summary>
    public Func<object, object>? RowKeySelector { get => GetValue(RowKeySelectorProperty); set => SetValue(RowKeySelectorProperty, value); }

    /// <summary>Accessible name of the table.</summary>
    public string Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <inheritdoc cref="EmptyMessageProperty"/>
    public string EmptyMessage { get => GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }

    /// <summary>Shows a spinner over the body (in place of the empty message when there are no rows).</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="LoadingMessageProperty"/>
    public string LoadingMessage { get => GetValue(LoadingMessageProperty); set => SetValue(LoadingMessageProperty, value); }

    /// <summary>Singular noun of the count line, as in "1 project".</summary>
    public string ItemNameOne { get => GetValue(ItemNameOneProperty); set => SetValue(ItemNameOneProperty, value); }

    /// <summary>Plural noun of the count line, as in "6 projects".</summary>
    public string ItemNameOther { get => GetValue(ItemNameOtherProperty); set => SetValue(ItemNameOtherProperty, value); }

    /// <inheritdoc cref="SelectedNounProperty"/>
    public string SelectedNoun { get => GetValue(SelectedNounProperty); set => SetValue(SelectedNounProperty, value); }

    /// <inheritdoc cref="SelectAllLabelProperty"/>
    public string SelectAllLabel { get => GetValue(SelectAllLabelProperty); set => SetValue(SelectAllLabelProperty, value); }

    /// <inheritdoc cref="ClearSelectionLabelProperty"/>
    public string ClearSelectionLabel { get => GetValue(ClearSelectionLabelProperty); set => SetValue(ClearSelectionLabelProperty, value); }

    /// <summary>Raised when the user sorts (header click or keyboard) or <see cref="Sort"/> is called.</summary>
    public event EventHandler<DataTableSortChangedEventArgs>? SortChanged
    {
        add => AddHandler(SortChangedEvent, value);
        remove => RemoveHandler(SortChangedEvent, value);
    }

    /// <summary>Raised when the user changes the selection (checkboxes, row clicks, Select all, Clear, Escape).</summary>
    public event EventHandler<DataTableSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    #endregion

    #region Theme-provided values

    internal static readonly StyledProperty<IBrush?> ColumnTintBrushProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(ColumnTintBrush));
    internal static readonly StyledProperty<IBrush?> SecondaryForegroundProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(SecondaryForeground));
    internal static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(MutedForeground));
    internal static readonly StyledProperty<IBrush?> RuleBrushProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(RuleBrush));
    internal static readonly StyledProperty<IBrush?> DividerBrushProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(DividerBrush));
    internal static readonly StyledProperty<IBrush?> CheckboxBorderBrushProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(CheckboxBorderBrush));
    internal static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(AccentBrush));
    internal static readonly StyledProperty<IBrush?> CheckboxOnBrushProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(CheckboxOnBrush));
    internal static readonly StyledProperty<IBrush?> CheckmarkBrushProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(CheckmarkBrush));
    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<SortableDataTable, IBrush?>(nameof(FocusRingBrush));
    internal static readonly StyledProperty<double> SmallFontSizeProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(SmallFontSize), 12);
    internal static readonly StyledProperty<FontWeight> MediumWeightProperty =
        AvaloniaProperty.Register<SortableDataTable, FontWeight>(nameof(MediumWeight), FontWeight.Medium);
    internal static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(RowHeight), 52);
    internal static readonly StyledProperty<double> HeaderHeightProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(HeaderHeight), 44);
    internal static readonly StyledProperty<double> CompactRowHeightProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(CompactRowHeight), 64);
    internal static readonly StyledProperty<double> CompactHeaderHeightProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(CompactHeaderHeight), 41);
    internal static readonly StyledProperty<double> EmptyRowHeightProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(EmptyRowHeight), 120);
    internal static readonly StyledProperty<double> CellInlinePaddingProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(CellInlinePadding), 16);
    internal static readonly StyledProperty<double> PrimaryInsetSelectableProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(PrimaryInsetSelectable), 8);
    internal static readonly StyledProperty<double> SelectColumnWidthProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(SelectColumnWidth), 44);
    internal static readonly StyledProperty<double> MinTableWidthProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(MinTableWidth), 560);
    internal static readonly StyledProperty<double> CompactBreakpointProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(CompactBreakpoint), 620);
    internal static readonly StyledProperty<double> ControlRadiusProperty =
        AvaloniaProperty.Register<SortableDataTable, double>(nameof(ControlRadius), 18);

    internal IBrush? ColumnTintBrush { get => GetValue(ColumnTintBrushProperty); set => SetValue(ColumnTintBrushProperty, value); }
    internal IBrush? SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }
    internal IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    internal IBrush? RuleBrush { get => GetValue(RuleBrushProperty); set => SetValue(RuleBrushProperty, value); }
    internal IBrush? DividerBrush { get => GetValue(DividerBrushProperty); set => SetValue(DividerBrushProperty, value); }
    internal IBrush? CheckboxBorderBrush { get => GetValue(CheckboxBorderBrushProperty); set => SetValue(CheckboxBorderBrushProperty, value); }
    internal IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    internal IBrush? CheckboxOnBrush { get => GetValue(CheckboxOnBrushProperty); set => SetValue(CheckboxOnBrushProperty, value); }
    internal IBrush? CheckmarkBrush { get => GetValue(CheckmarkBrushProperty); set => SetValue(CheckmarkBrushProperty, value); }
    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }
    internal double SmallFontSize { get => GetValue(SmallFontSizeProperty); set => SetValue(SmallFontSizeProperty, value); }
    internal FontWeight MediumWeight { get => GetValue(MediumWeightProperty); set => SetValue(MediumWeightProperty, value); }
    internal double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    internal double HeaderHeight { get => GetValue(HeaderHeightProperty); set => SetValue(HeaderHeightProperty, value); }
    internal double CompactRowHeight { get => GetValue(CompactRowHeightProperty); set => SetValue(CompactRowHeightProperty, value); }
    internal double CompactHeaderHeight { get => GetValue(CompactHeaderHeightProperty); set => SetValue(CompactHeaderHeightProperty, value); }
    internal double EmptyRowHeight { get => GetValue(EmptyRowHeightProperty); set => SetValue(EmptyRowHeightProperty, value); }
    internal double CellInlinePadding { get => GetValue(CellInlinePaddingProperty); set => SetValue(CellInlinePaddingProperty, value); }
    internal double PrimaryInsetSelectable { get => GetValue(PrimaryInsetSelectableProperty); set => SetValue(PrimaryInsetSelectableProperty, value); }
    internal double SelectColumnWidth { get => GetValue(SelectColumnWidthProperty); set => SetValue(SelectColumnWidthProperty, value); }
    internal double MinTableWidth { get => GetValue(MinTableWidthProperty); set => SetValue(MinTableWidthProperty, value); }
    internal double CompactBreakpoint { get => GetValue(CompactBreakpointProperty); set => SetValue(CompactBreakpointProperty, value); }
    internal double ControlRadius { get => GetValue(ControlRadiusProperty); set => SetValue(ControlRadiusProperty, value); }

    internal static readonly DirectProperty<SortableDataTable, string> CountTextProperty =
        AvaloniaProperty.RegisterDirect<SortableDataTable, string>(nameof(CountText), o => o.CountText);

    internal static readonly DirectProperty<SortableDataTable, string> CountNounProperty =
        AvaloniaProperty.RegisterDirect<SortableDataTable, string>(nameof(CountNoun), o => o.CountNoun);

    internal static readonly DirectProperty<SortableDataTable, int> CountDirectionProperty =
        AvaloniaProperty.RegisterDirect<SortableDataTable, int>(nameof(CountDirection), o => o.CountDirection);

    internal static readonly DirectProperty<SortableDataTable, int> NounDirectionProperty =
        AvaloniaProperty.RegisterDirect<SortableDataTable, int>(nameof(NounDirection), o => o.NounDirection);

    internal static readonly DirectProperty<SortableDataTable, bool> HasSelectionProperty =
        AvaloniaProperty.RegisterDirect<SortableDataTable, bool>(nameof(HasSelection), o => o.HasSelection);

    private string _countText = "0";
    private string _countNoun = "rows";
    private int _countDirection = 1;
    private int _nounDirection = -1;
    private bool _hasSelection;

    internal string CountText { get => _countText; private set => SetAndRaise(CountTextProperty, ref _countText, value); }
    internal string CountNoun { get => _countNoun; private set => SetAndRaise(CountNounProperty, ref _countNoun, value); }
    internal int CountDirection { get => _countDirection; private set => SetAndRaise(CountDirectionProperty, ref _countDirection, value); }
    internal int NounDirection { get => _nounDirection; private set => SetAndRaise(NounDirectionProperty, ref _nounDirection, value); }
    internal bool HasSelection { get => _hasSelection; private set => SetAndRaise(HasSelectionProperty, ref _hasSelection, value); }

    #endregion

    private sealed class ColumnState
    {
        public ColumnState(DataTableColumn column)
        {
            Column = column;
            if (column.ValueSelector is null && column.Binding is { } binding)
            {
                ValueProbe = new SortableDataTableBindingProbe(binding);
            }
            if (column.SortValueSelector is null && column.SortBinding is { } sortBinding)
            {
                SortProbe = new SortableDataTableBindingProbe(sortBinding);
            }
        }

        public DataTableColumn Column { get; }
        public SortableDataTableBindingProbe? ValueProbe { get; }
        public SortableDataTableBindingProbe? SortProbe { get; }
        public bool? Numeric { get; set; }
        public object?[]? SortValues { get; set; }
        public double Natural { get; set; } = -1;
    }

    private List<object> _items = new();
    private List<object> _keys = new();
    private Dictionary<object, int> _indexByKey = new();
    private int[] _order = [];
    private int[] _position = [];
    private HashSet<object> _selected = new();
    private object? _anchor;
    private ColumnState[] _states = [];
    private bool _naturalLocked;
    private int _compactLead = -1;
    private SortableDataTablePresenter? _presenter;
    private ScrollViewer? _scroll;
    private TextBlock? _announcer;
    private AvaloniaButton? _clear;
    private INotifyCollectionChanged? _observedSource;
    private INotifyCollectionChanged? _observedSelection;
    private bool _syncingSort;
    private bool _syncingSelection;
    private int _lastShown = -1;
    private int _currentRow = -1;
    private double _widthHint = double.NaN;

    internal SortableDataTableLayout Layout { get; } = new();
    internal SortableDataTablePalette Palette { get; } = new();

    public SortableDataTable()
    {
        Columns.CollectionChanged += OnColumnsChanged;
        SetCurrentValue(SelectedItemsProperty, new AvaloniaList<object>());
        _presenter = new SortableDataTablePresenter(this);
    }

    #region Data access (presenter, rows and header)

    internal int RowCount => _items.Count;

    /// <summary>Changes whenever the source is re-read; rows refresh their cached text when it moves.</summary>
    internal int DataVersion { get; private set; }
    internal int ColumnCount => _states.Length;
    internal int CurrentRow => _currentRow;
    internal double TableWidthHint => _widthHint;
    internal bool IsCompact => Layout.Compact;
    internal int CompactLeadNumericIndex => _compactLead;
    internal SortableDataTablePresenter? Presenter => _presenter;

    internal DataTableColumn? ColumnAt(int index) => index >= 0 && index < _states.Length ? _states[index].Column : null;

    internal int SourceAt(int displayIndex) => _order[displayIndex];

    internal int DisplayIndexOf(int sourceIndex) => sourceIndex >= 0 && sourceIndex < _position.Length ? _position[sourceIndex] : -1;

    internal object ItemAt(int sourceIndex) => _items[sourceIndex];

    internal object KeyAt(int sourceIndex) => _keys[sourceIndex];

    internal int SourceIndexOfKey(object key) => _indexByKey.TryGetValue(key, out var index) ? index : -1;

    internal bool IsSourceSelected(int sourceIndex) => IsSelectable && _selected.Contains(_keys[sourceIndex]);

    internal int SortedColumnIndex
    {
        get
        {
            if (SortDirection == DataTableSortDirection.None || SortColumn is not { } column)
            {
                return -1;
            }
            for (var i = 0; i < _states.Length; i++)
            {
                if (ReferenceEquals(_states[i].Column, column))
                {
                    return i;
                }
            }
            return -1;
        }
    }

    internal double LeadingPadding(int columnIndex) =>
        columnIndex == 0 && IsSelectable ? PrimaryInsetSelectable : CellInlinePadding;

    internal bool IsCompactLeadNumeric(int columnIndex) => columnIndex == _compactLead && columnIndex > 0;

    private object? ValueOf(int sourceIndex, int columnIndex)
    {
        var state = _states[columnIndex];
        var item = _items[sourceIndex];
        if (state.Column.ValueSelector is { } selector)
        {
            return selector(item);
        }
        if (state.ValueProbe is { } probe)
        {
            return probe.Evaluate(item);
        }
        return state.Column.CellTemplate is null ? item : null;
    }

    private object? SortValueOf(int sourceIndex, int columnIndex)
    {
        var state = _states[columnIndex];
        var item = _items[sourceIndex];
        if (state.Column.SortValueSelector is { } selector)
        {
            return selector(item);
        }
        if (state.SortProbe is { } probe)
        {
            return probe.Evaluate(item);
        }
        return ValueOf(sourceIndex, columnIndex);
    }

    internal string CellText(int sourceIndex, int columnIndex) =>
        SortableDataTableValues.Format(_states[columnIndex].Column, ValueOf(sourceIndex, columnIndex));

    /// <summary>Arc right-aligns a column when every non-empty value is a number (sampled for large sources).</summary>
    internal bool IsNumericColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _states.Length)
        {
            return false;
        }
        var state = _states[columnIndex];
        if (state.Column.IsNumeric is { } explicitValue)
        {
            return explicitValue;
        }
        if (state.Numeric is { } known)
        {
            return known;
        }
        var any = false;
        var all = true;
        var limit = Math.Min(_items.Count, 500);
        for (var i = 0; i < limit && all; i++)
        {
            var value = ValueOf(i, columnIndex);
            if (SortableDataTableValues.IsNumber(value))
            {
                any = true;
            }
            else if (!SortableDataTableValues.IsEmpty(value))
            {
                all = false;
            }
        }
        state.Numeric = any && all;
        state.ValueProbe?.Release();
        return state.Numeric.Value;
    }

    internal string RowName(SortableDataTableRow row)
    {
        if (row.SourceIndex < 0 || _states.Length == 0)
        {
            return string.Empty;
        }
        var text = CellText(row.SourceIndex, 0);
        return string.IsNullOrEmpty(text) ? Convert.ToString(row.Key, CultureInfo.CurrentCulture) ?? string.Empty : text;
    }

    #endregion

    #region Columns and layout

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DataTableColumn column in e.OldItems)
            {
                column.DefinitionChanged -= OnColumnDefinitionChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (DataTableColumn column in e.NewItems)
            {
                column.DefinitionChanged += OnColumnDefinitionChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var state in _states)
            {
                state.Column.DefinitionChanged -= OnColumnDefinitionChanged;
            }
            foreach (var column in Columns)
            {
                column.DefinitionChanged -= OnColumnDefinitionChanged;
                column.DefinitionChanged += OnColumnDefinitionChanged;
            }
        }
        RebuildColumns();
    }

    private void OnColumnDefinitionChanged(object? sender, EventArgs e) => RebuildColumns();

    private void RebuildColumns()
    {
        foreach (var state in _states)
        {
            state.ValueProbe?.Release();
            state.SortProbe?.Release();
        }
        _states = Columns.Select(column => new ColumnState(column)).ToArray();
        _naturalLocked = false;
        UpdateCompactLead();
        _presenter?.Header.Rebuild();
        _presenter?.Reset();
        if (SortColumn is { } sorted && !Columns.Contains(sorted))
        {
            var replacement = Columns.FirstOrDefault(c => c.EffectiveKey == sorted.EffectiveKey);
            SetSortState(replacement, SortDirection);
        }
        else if (SortColumn is null && SortKey is { } key)
        {
            var match = Columns.FirstOrDefault(c => c.EffectiveKey == key);
            if (match is not null)
            {
                SetSortState(match, SortDirection == DataTableSortDirection.None ? DataTableSortDirection.Ascending : SortDirection);
            }
        }
        ApplySort(animate: false, captureFlip: false);
    }

    private void UpdateCompactLead()
    {
        _compactLead = -1;
        for (var i = 1; i < _states.Length; i++)
        {
            if (IsNumericColumn(i))
            {
                _compactLead = i;
                break;
            }
        }
    }

    /// <summary>Natural width of every column: header and the first rows, measured once and held.</summary>
    private void EnsureNatural()
    {
        if (_naturalLocked && _states.All(s => s.Natural >= 0))
        {
            return;
        }
        var header = _presenter?.Header;
        var sample = Math.Min(_items.Count, 40);
        for (var c = 0; c < _states.Length; c++)
        {
            var state = _states[c];
            var width = header?.CellFor(c)?.MeasureNaturalWidth() ?? 0;
            var lead = LeadingPadding(c);
            for (var i = 0; i < sample; i++)
            {
                var source = _order.Length > i ? _order[i] : i;
                var text = new FormattedText(CellText(source, c), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    Palette.Regular, Palette.FontSize, null);
                width = Math.Max(width, Math.Ceiling(text.WidthIncludingTrailingWhitespace + lead + CellInlinePadding));
            }
            state.Natural = width;
            state.ValueProbe?.Release();
        }
        // Arc measures the natural widths once, then holds them so nothing reflows while rows move.
        _naturalLocked = _items.Count > 0;
    }

    internal double NaturalTableWidth()
    {
        EnsureNatural();
        var sum = IsSelectable ? SelectColumnWidth : 0;
        foreach (var state in _states)
        {
            sum += state.Column.Width.IsAbsolute ? state.Column.Width.Value : state.Natural;
        }
        return sum;
    }

    internal void ComputeColumns(SortableDataTableLayout layout)
    {
        EnsureNatural();
        var count = _states.Length;
        var select = IsSelectable && !layout.Compact ? SelectColumnWidth : 0;
        layout.SelectWidth = select;
        var widths = new double[count];
        var xs = new double[count];
        var available = Math.Max(0, layout.Width - select);
        double fixedSum = 0, autoSum = 0, starSum = 0;
        for (var i = 0; i < count; i++)
        {
            var width = _states[i].Column.Width;
            if (width.IsAbsolute)
            {
                widths[i] = width.Value;
                fixedSum += width.Value;
            }
            else if (width.IsStar)
            {
                starSum += Math.Max(0, width.Value);
            }
            else
            {
                widths[i] = _states[i].Natural;
                autoSum += _states[i].Natural;
            }
        }
        var remaining = available - fixedSum - autoSum;
        if (starSum > 0)
        {
            var share = Math.Max(0, remaining);
            for (var i = 0; i < count; i++)
            {
                if (_states[i].Column.Width.IsStar)
                {
                    widths[i] = share * Math.Max(0, _states[i].Column.Width.Value) / starSum;
                }
            }
        }
        else if (autoSum > 0)
        {
            // Like a 100%-wide auto table: extra room (or a shortfall) is shared in proportion to natural width.
            var scale = Math.Max(0.1, (available - fixedSum) / autoSum);
            for (var i = 0; i < count; i++)
            {
                if (_states[i].Column.Width.IsAuto)
                {
                    widths[i] = _states[i].Natural * scale;
                }
            }
        }
        var x = select;
        for (var i = 0; i < count; i++)
        {
            xs[i] = x;
            x += widths[i];
        }
        layout.SetColumns(xs, widths);
    }

    #endregion

    #region Items

    private void OnItemsSourceChanged(IEnumerable? oldValue, IEnumerable? newValue)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceCollectionChanged;
            _observedSource = null;
        }
        if (newValue is INotifyCollectionChanged observable)
        {
            _observedSource = observable;
            if (!_sourcesDetached)
            {
                observable.CollectionChanged += OnSourceCollectionChanged;
            }
        }
        RebuildItems(animate: IsLoaded);
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems(animate: IsLoaded);

    private bool _sourcesDetached;

    // A view-model collection that outlives the page would keep the table alive and re-sort it on every change while it
    // is not shown: the table listens only while attached and catches up once when it comes back.
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_sourcesDetached)
        {
            return;
        }
        _sourcesDetached = true;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceCollectionChanged;
        }
        if (_observedSelection is not null)
        {
            _observedSelection.CollectionChanged -= OnSelectedCollectionChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_sourcesDetached)
        {
            return;
        }
        _sourcesDetached = false;
        if (_observedSelection is not null)
        {
            _observedSelection.CollectionChanged += OnSelectedCollectionChanged;
        }
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceCollectionChanged;
            RebuildItems(animate: false);
        }
        if (_observedSelection is not null && !_syncingSelection)
        {
            SyncSelectedItemsIn();
        }
    }

    private void RebuildItems(bool animate)
    {
        // Rows that survive the change slide from where they were drawn (FLIP keyed by row key).
        var oldIndex = _indexByKey;
        var oldPosition = _position;
        _presenter?.CaptureFlip(key => oldIndex.TryGetValue(key, out var source) && source < oldPosition.Length ? oldPosition[source] : -1,
            animate && ArcMotion.IsFull(this));
        var items = new List<object>();
        if (ItemsSource is { } source)
        {
            foreach (var item in source)
            {
                if (item is not null)
                {
                    items.Add(item);
                }
            }
        }
        var selector = RowKeySelector;
        var keys = new List<object>(items.Count);
        var index = new Dictionary<object, int>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var key = selector?.Invoke(items[i]) ?? items[i];
            keys.Add(key);
            index.TryAdd(key, i);
        }
        _items = items;
        _keys = keys;
        _indexByKey = index;
        foreach (var state in _states)
        {
            state.SortValues = null;
            state.Numeric = null;
        }
        if (!_naturalLocked)
        {
            foreach (var state in _states)
            {
                state.Natural = -1;
            }
        }
        DataVersion++;
        UpdateCompactLead();
        ApplySort(animate, captureFlip: false);
        SyncSelectedItemsOut(raise: false);
        UpdateCount();
        UpdatePseudoClasses();
        _presenter?.InvalidateMeasure();
        _presenter?.InvalidateVisual();
    }

    #endregion

    #region Sorting

    /// <summary>Sorts by <paramref name="column"/> (null or None restores the source order) and raises <see cref="SortChanged"/>.</summary>
    public void Sort(DataTableColumn? column, DataTableSortDirection direction)
    {
        SetSortState(column, column is null ? DataTableSortDirection.None : direction, clearKey: true);
        ApplySort(animate: IsLoaded);
        RaiseSortChanged();
    }

    /// <summary>Arc: a new column sorts ascending, the sorted column toggles; with <see cref="IsSortClearable"/> descending clears.</summary>
    internal void CycleSort(DataTableColumn column)
    {
        var current = ReferenceEquals(SortColumn, column) ? SortDirection : DataTableSortDirection.None;
        var next = current switch
        {
            DataTableSortDirection.Ascending => DataTableSortDirection.Descending,
            DataTableSortDirection.Descending when IsSortClearable => DataTableSortDirection.None,
            _ => DataTableSortDirection.Ascending
        };
        Sort(next == DataTableSortDirection.None ? null : column, next);
        Announce(next == DataTableSortDirection.None
            ? "Sort cleared"
            : $"Sorted by {column.Header}, {(next == DataTableSortDirection.Ascending ? "ascending" : "descending")}");
        if (_presenter?.Header.CellFor(Columns.IndexOf(column)) is { } cell)
        {
            _presenter.Header.RevealPill(cell);
        }
    }

    private void RaiseSortChanged() =>
        RaiseEvent(new DataTableSortChangedEventArgs(SortChangedEvent, SortDirection == DataTableSortDirection.None ? null : SortColumn, SortDirection));

    /// <summary>
    /// Writes the three sort properties together. A key without a matching column (columns not added yet) is kept
    /// with its direction and resolved when the columns arrive.
    /// </summary>
    private void SetSortState(DataTableColumn? column, DataTableSortDirection direction, bool clearKey = false)
    {
        _syncingSort = true;
        try
        {
            SetCurrentValue(SortColumnProperty, column);
            SetCurrentValue(SortKeyProperty, column?.EffectiveKey ?? (clearKey ? null : SortKey));
            SetCurrentValue(SortDirectionProperty, direction);
        }
        finally
        {
            _syncingSort = false;
        }
    }

    private void ApplySort(bool animate, bool captureFlip = true)
    {
        var count = _items.Count;
        if (captureFlip && _presenter is not null)
        {
            var oldPosition = _position;
            _presenter.CaptureFlip(key => _indexByKey.TryGetValue(key, out var source) && source < oldPosition.Length ? oldPosition[source] : -1,
                animate && ArcMotion.IsFull(this));
        }
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
        }
        var columnIndex = SortedColumnIndex;
        if (columnIndex >= 0 && count > 1)
        {
            var state = _states[columnIndex];
            var values = state.SortValues;
            if (values is null || values.Length != count)
            {
                values = new object?[count];
                for (var i = 0; i < count; i++)
                {
                    values[i] = SortValueOf(i, columnIndex);
                }
                state.SortValues = values;
                state.SortProbe?.Release();
                state.ValueProbe?.Release();
            }
            var descending = SortDirection == DataTableSortDirection.Descending;
            var comparer = state.Column.Comparer;
            Array.Sort(order, (a, b) =>
            {
                var left = values[a];
                var right = values[b];
                var emptyLeft = SortableDataTableValues.IsEmpty(left);
                var emptyRight = SortableDataTableValues.IsEmpty(right);
                // Empty values stay at the bottom in both directions.
                if (emptyLeft || emptyRight)
                {
                    return emptyLeft == emptyRight ? a - b : emptyLeft ? 1 : -1;
                }
                var result = SortableDataTableValues.Compare(left!, right!, comparer);
                if (descending)
                {
                    result = -result;
                }
                return result != 0 ? result : a - b;
            });
        }
        var position = new int[count];
        for (var i = 0; i < count; i++)
        {
            position[order[i]] = i;
        }
        _order = order;
        _position = position;
        if (_currentRow >= count)
        {
            _currentRow = count - 1;
        }
        UpdateHeaderSort(animate);
        _presenter?.InvalidateMeasure();
        _presenter?.InvalidateVisual();
        foreach (var row in _presenter?.RealizedRows ?? [])
        {
            row.InvalidateVisual();
        }
    }

    private void UpdateHeaderSort(bool animate)
    {
        if (_presenter is null)
        {
            return;
        }
        var sorted = SortedColumnIndex;
        var descending = SortDirection == DataTableSortDirection.Descending;
        foreach (var cell in _presenter.Header.Cells)
        {
            if (!cell.IsSelectAll)
            {
                cell.SetSort(cell.ColumnIndex == sorted, descending, animate && IsLoaded);
            }
        }
    }

    #endregion

    #region Selection

    private int SelectedPresentCount()
    {
        if (!IsSelectable || _selected.Count == 0)
        {
            return 0;
        }
        var count = 0;
        foreach (var key in _keys)
        {
            if (_selected.Contains(key))
            {
                count++;
            }
        }
        return count;
    }

    internal void ToggleAll()
    {
        if (!IsSelectable)
        {
            return;
        }
        var all = _items.Count > 0 && SelectedPresentCount() == _items.Count;
        Commit(all ? new HashSet<object>() : new HashSet<object>(_keys));
    }

    /// <summary>Selects every row.</summary>
    public void SelectAll()
    {
        if (IsSelectable)
        {
            Commit(new HashSet<object>(_keys));
        }
    }

    /// <summary>Clears the selection and moves focus to Select all (Arc's Clear selection).</summary>
    public void ClearSelection()
    {
        Commit(new HashSet<object>());
        _presenter?.Header.CellFor(-1)?.Focus();
    }

    /// <summary>Toggles one row; with <paramref name="extend"/> the range from the last toggled row takes the same state.</summary>
    private void ToggleRow(object key, bool extend)
    {
        var next = new HashSet<object>(_selected);
        var check = !_selected.Contains(key);
        var to = _indexByKey.TryGetValue(key, out var source) ? _position[source] : -1;
        var from = extend && _anchor is not null && _indexByKey.TryGetValue(_anchor, out var anchorSource) ? _position[anchorSource] : -1;
        if (from < 0 || to < 0)
        {
            Apply(key);
        }
        else
        {
            for (var d = Math.Min(from, to); d <= Math.Max(from, to); d++)
            {
                Apply(_keys[_order[d]]);
            }
        }
        _anchor = key;
        Commit(next);

        void Apply(object item)
        {
            if (check)
            {
                next.Add(item);
            }
            else
            {
                next.Remove(item);
            }
        }
    }

    private void Commit(HashSet<object> next)
    {
        var present = new HashSet<object>();
        var added = new List<object>();
        var removed = new List<object>();
        foreach (var key in _keys)
        {
            if (!next.Contains(key))
            {
                continue;
            }
            present.Add(key);
        }
        for (var d = 0; d < _order.Length; d++)
        {
            var source = _order[d];
            var key = _keys[source];
            var was = _selected.Contains(key);
            var now = present.Contains(key);
            if (was != now)
            {
                (now ? added : removed).Add(_items[source]);
            }
        }
        _selected = present;
        ApplySelectionVisuals(animate: true);
        var selectedItems = SyncSelectedItemsOut(raise: true);
        Announce(selectedItems.Count > 0 ? $"{selectedItems.Count} of {_items.Count} selected" : "Selection cleared");
        RaiseEvent(new DataTableSelectionChangedEventArgs(SelectionChangedEvent, selectedItems, added, removed));
    }

    private void ApplySelectionVisuals(bool animate)
    {
        if (_presenter is not null)
        {
            foreach (var row in _presenter.RealizedRows)
            {
                if (row.SourceIndex >= 0)
                {
                    row.SetSelected(IsSourceSelected(row.SourceIndex), animate && IsLoaded);
                }
            }
            var count = SelectedPresentCount();
            _presenter.Header.CellFor(-1)?.Checkbox?.Set(_items.Count > 0 && count == _items.Count,
                count > 0 && count < _items.Count, animate && IsLoaded);
            _presenter.Header.CellFor(-1)?.InvalidateVisual();
        }
        UpdateCount();
        UpdatePseudoClasses();
    }

    private List<object> SelectedInDisplayOrder()
    {
        var list = new List<object>();
        if (!IsSelectable)
        {
            return list;
        }
        foreach (var source in _order)
        {
            if (_selected.Contains(_keys[source]))
            {
                list.Add(_items[source]);
            }
        }
        return list;
    }

    private List<object> SyncSelectedItemsOut(bool raise)
    {
        var selected = SelectedInDisplayOrder();
        var list = SelectedItems;
        _syncingSelection = true;
        try
        {
            if (list is null || list.IsReadOnly || list.IsFixedSize)
            {
                SetCurrentValue(SelectedItemsProperty, new AvaloniaList<object>(selected));
            }
            else
            {
                var same = list.Count == selected.Count;
                for (var i = 0; same && i < selected.Count; i++)
                {
                    same = ReferenceEquals(list[i], selected[i]);
                }
                if (!same)
                {
                    list.Clear();
                    foreach (var item in selected)
                    {
                        list.Add(item);
                    }
                }
            }
        }
        finally
        {
            _syncingSelection = false;
        }
        _ = raise;
        return selected;
    }

    private void OnSelectedItemsChanged(IList? value)
    {
        if (_observedSelection is not null)
        {
            _observedSelection.CollectionChanged -= OnSelectedCollectionChanged;
            _observedSelection = null;
        }
        if (value is INotifyCollectionChanged observable)
        {
            _observedSelection = observable;
            if (!_sourcesDetached)
            {
                observable.CollectionChanged += OnSelectedCollectionChanged;
            }
        }
        if (!_syncingSelection)
        {
            SyncSelectedItemsIn();
        }
    }

    private void OnSelectedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_syncingSelection)
        {
            SyncSelectedItemsIn();
        }
    }

    private void SyncSelectedItemsIn()
    {
        var next = new HashSet<object>();
        if (SelectedItems is { } list)
        {
            var selector = RowKeySelector;
            foreach (var item in list)
            {
                if (item is not null)
                {
                    next.Add(selector?.Invoke(item) ?? item);
                }
            }
        }
        _selected = next;
        ApplySelectionVisuals(animate: true);
    }

    internal void OnRowClicked(SortableDataTableRow row, bool extend)
    {
        if (!IsSelectable || row.Key is null)
        {
            return;
        }
        ToggleRow(row.Key, extend);
        // Keep the keyboard path where the pointer left off, so arrows continue from this row.
        _currentRow = row.DisplayIndex;
        row.Focus(NavigationMethod.Pointer);
        _presenter?.UpdateTabStops();
    }

    internal void OnRowFocused(SortableDataTableRow row)
    {
        if (row.DisplayIndex >= 0)
        {
            _currentRow = row.DisplayIndex;
            _presenter?.UpdateTabStops();
        }
    }

    #endregion

    #region Keyboard

    /// <summary>Header: ←/→ and Home/End walk the header controls; ↓ enters the row checkboxes.</summary>
    internal bool NavigateFromHeader(SortableDataTableHeaderCell cell, Key key)
    {
        if (_presenter is null)
        {
            return false;
        }
        var cells = _presenter.Header.Cells.Where(c => c.Focusable).ToList();
        var index = cells.IndexOf(cell);
        SortableDataTableHeaderCell? target = key switch
        {
            Key.Right => index + 1 < cells.Count ? cells[index + 1] : null,
            Key.Left => index > 0 ? cells[index - 1] : null,
            Key.Home => cells.FirstOrDefault(),
            Key.End => cells.LastOrDefault(),
            _ => null
        };
        if (target is not null)
        {
            target.Focus(NavigationMethod.Directional);
            _presenter.Header.RevealPill(target);
            return true;
        }
        if (key == Key.Down && IsSelectable && cell.IsSelectAll && _items.Count > 0)
        {
            FocusRow(0);
            return true;
        }
        return false;
    }

    /// <summary>Rows: ↑/↓, Home/End, PageUp/PageDown move between checkboxes; Space / Enter toggles (Shift extends).</summary>
    internal bool NavigateFromRow(SortableDataTableRow row, Key key, KeyModifiers modifiers)
    {
        var count = _items.Count;
        var current = row.DisplayIndex;
        if (count == 0 || current < 0)
        {
            return false;
        }
        var page = Math.Max(1, (int)((_presenter?.Viewport.Height ?? 400) / Layout.RowHeight) - 1);
        switch (key)
        {
            case Key.Down:
                if (current + 1 < count)
                {
                    FocusRow(current + 1);
                }
                return true;
            case Key.Up:
                if (current > 0)
                {
                    FocusRow(current - 1);
                }
                else
                {
                    _presenter?.Header.CellFor(-1)?.Focus(NavigationMethod.Directional);
                }
                return true;
            case Key.Home:
                FocusRow(0);
                return true;
            case Key.End:
                FocusRow(count - 1);
                return true;
            case Key.PageDown:
                FocusRow(Math.Min(count - 1, current + page));
                return true;
            case Key.PageUp:
                FocusRow(Math.Max(0, current - page));
                return true;
            case Key.Space:
            case Key.Enter:
                if (IsSelectable && row.Key is not null)
                {
                    ToggleRow(row.Key, modifiers.HasFlag(KeyModifiers.Shift));
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    /// <summary>Moves keyboard focus to the row at <paramref name="displayIndex"/>, scrolling it into view.</summary>
    internal void FocusRow(int displayIndex)
    {
        if (_presenter is null || displayIndex < 0 || displayIndex >= _items.Count)
        {
            return;
        }
        _currentRow = displayIndex;
        _presenter.RevealRow(displayIndex);
        var row = _presenter.EnsureRow(displayIndex);
        _presenter.UpdateTabStops();
        row?.Focus(NavigationMethod.Directional);
    }

    /// <summary>Scrolls the row showing <paramref name="item"/> into view.</summary>
    public void ScrollIntoView(object item)
    {
        var key = RowKeySelector?.Invoke(item) ?? item;
        if (_presenter is not null && _indexByKey.TryGetValue(key, out var source))
        {
            _presenter.RevealRow(_position[source]);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Escape || SelectedPresentCount() == 0)
        {
            return;
        }
        e.Handled = true;
        if (_clear is not null && _clear.IsFocused)
        {
            ClearSelection();
        }
        else
        {
            Commit(new HashSet<object>());
        }
    }

    #endregion

    #region Template, palette, count

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroll is not null)
        {
            _scroll.Content = null;
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        _clear = e.NameScope.Find<AvaloniaButton>(ClearPart);
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }
        _presenter ??= new SortableDataTablePresenter(this);
        if (_scroll is not null)
        {
            _scroll.Content = _presenter;
        }
        UpdatePalette();
        _presenter.Header.Rebuild();
        _presenter.Reset();
        UpdateHeaderSort(animate: false);
        ApplySelectionVisuals(animate: false);
        UpdateCount();
    }

    private void OnClearClick(object? sender, RoutedEventArgs e) => ClearSelection();

    protected override Size MeasureOverride(Size availableSize)
    {
        var inner = availableSize.Width - BorderThickness.Left - BorderThickness.Right;
        var hint = double.IsFinite(inner) ? Math.Max(0, inner) : double.NaN;
        var changed = double.IsNaN(hint) ? !double.IsNaN(_widthHint) : double.IsNaN(_widthHint) || Math.Abs(hint - _widthHint) > 0.01;
        if (changed)
        {
            _widthHint = hint;
            _presenter?.InvalidateMeasure();
        }
        return base.MeasureOverride(availableSize);
    }

    private void UpdatePalette()
    {
        static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        Palette.Update(ColorOf(Background), ColorOf(ColumnTintBrush), ColorOf(Foreground), ColorOf(SecondaryForeground),
            ColorOf(MutedForeground), ColorOf(RuleBrush), ColorOf(DividerBrush), ColorOf(CheckboxBorderBrush),
            ColorOf(AccentBrush), ColorOf(CheckboxOnBrush), ColorOf(CheckmarkBrush),
            FocusRingBrush is ISolidColorBrush ring ? ring.Color : null, FontFamily, FontSize, SmallFontSize, MediumWeight);
        _presenter?.InvalidateRows();
        _presenter?.InvalidateMeasure();
    }

    private void UpdateCount()
    {
        var selected = SelectedPresentCount();
        var total = _items.Count;
        var shown = selected > 0 ? selected : total;
        if (_lastShown >= 0 && shown != _lastShown)
        {
            CountDirection = shown > _lastShown ? 1 : -1;
        }
        _lastShown = shown;
        NounDirection = selected > 0 ? 1 : -1;
        CountText = shown.ToString(CultureInfo.CurrentCulture);
        CountNoun = selected > 0 ? SelectedNoun : total == 1 ? ItemNameOne : ItemNameOther;
        HasSelection = selected > 0;
    }

    private void Announce(string message)
    {
        if (_announcer is null)
        {
            return;
        }
        _announcer.Text = message;
        AutomationProperties.SetName(_announcer, message);
    }

    internal void OnLayoutModeChanged() => UpdatePseudoClasses();

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(SelectablePseudoClass, IsSelectable);
        PseudoClasses.Set(LoadingPseudoClass, IsLoading);
        PseudoClasses.Set(EmptyPseudoClass, _items.Count == 0);
        PseudoClasses.Set(SelectionPseudoClass, SelectedPresentCount() > 0);
        PseudoClasses.Set(CompactPseudoClass, Layout.Compact);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ItemsSourceProperty)
        {
            OnItemsSourceChanged(change.OldValue as IEnumerable, change.NewValue as IEnumerable);
        }
        else if (property == SortColumnProperty || property == SortDirectionProperty)
        {
            if (_syncingSort)
            {
                return;
            }
            if (property == SortColumnProperty && SortColumn is not null && SortDirection == DataTableSortDirection.None)
            {
                SetSortState(SortColumn, DataTableSortDirection.Ascending);
            }
            else
            {
                SetSortState(SortColumn, SortDirection);
            }
            ApplySort(animate: IsLoaded);
        }
        else if (property == SortKeyProperty)
        {
            if (_syncingSort)
            {
                return;
            }
            var key = SortKey;
            var column = key is null ? null : Columns.FirstOrDefault(c => c.EffectiveKey == key);
            SetSortState(column, column is not null && SortDirection == DataTableSortDirection.None
                ? DataTableSortDirection.Ascending
                : key is null ? DataTableSortDirection.None : SortDirection);
            ApplySort(animate: IsLoaded);
        }
        else if (property == IsSelectableProperty)
        {
            _naturalLocked = false;
            foreach (var state in _states)
            {
                state.Natural = -1;
            }
            _presenter?.Header.Rebuild();
            _presenter?.Reset();
            UpdateHeaderSort(animate: false);
            ApplySelectionVisuals(animate: false);
            UpdatePseudoClasses();
        }
        else if (property == SelectedItemsProperty)
        {
            OnSelectedItemsChanged(change.NewValue as IList);
        }
        else if (property == RowKeySelectorProperty)
        {
            RebuildItems(animate: false);
        }
        else if (property == IsLoadingProperty)
        {
            UpdatePseudoClasses();
            _presenter?.InvalidateVisual();
        }
        else if (property == EmptyMessageProperty)
        {
            _presenter?.InvalidateVisual();
        }
        else if (property == ItemNameOneProperty || property == ItemNameOtherProperty || property == SelectedNounProperty)
        {
            UpdateCount();
        }
        else if (property == SelectAllLabelProperty)
        {
            _presenter?.Header.CellFor(-1)?.UpdateAutomation();
        }
        else if (property == CaptionProperty)
        {
            AutomationProperties.SetName(this, Caption);
        }
        else if (property == CornerRadiusProperty)
        {
            ControlRadius = CornerRadius.TopLeft;
        }
        else if (property == BackgroundProperty || property == ForegroundProperty || property == ColumnTintBrushProperty ||
                 property == SecondaryForegroundProperty || property == MutedForegroundProperty || property == RuleBrushProperty ||
                 property == DividerBrushProperty || property == CheckboxBorderBrushProperty || property == AccentBrushProperty ||
                 property == CheckboxOnBrushProperty || property == CheckmarkBrushProperty || property == FocusRingBrushProperty ||
                 property == FontFamilyProperty || property == FontSizeProperty || property == SmallFontSizeProperty ||
                 property == MediumWeightProperty)
        {
            UpdatePalette();
        }
        else if (property == RowHeightProperty || property == HeaderHeightProperty || property == CompactRowHeightProperty ||
                 property == CompactHeaderHeightProperty || property == EmptyRowHeightProperty || property == CellInlinePaddingProperty ||
                 property == PrimaryInsetSelectableProperty || property == SelectColumnWidthProperty ||
                 property == MinTableWidthProperty || property == CompactBreakpointProperty)
        {
            _presenter?.InvalidateMeasure();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AutomationProperties.SetName(this, Caption);
        UpdatePseudoClasses();
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new TablePeer(this);

    private sealed class TablePeer : Avalonia.Automation.Peers.ControlAutomationPeer
    {
        public TablePeer(SortableDataTable owner) : base(owner)
        {
        }

        protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() =>
            Avalonia.Automation.Peers.AutomationControlType.Table;
    }

    #endregion
}
