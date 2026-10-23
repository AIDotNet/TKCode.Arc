using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Threading;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class DataGrid
{
    private const int SelectOptionLimit = 200;

    private readonly Dictionary<string, double> _userWidths = new(StringComparer.Ordinal);
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private readonly HashSet<object> _checked = new();
    private readonly Dictionary<int, IReadOnlyList<string>> _options = new();
    private DataGridView _view = DataGridView.Empty;
    private bool _viewDirty = true;
    private bool _columnsDirty = true;
    private bool _definitionsDirty = true;
    private int[] _visible = [];
    private int _pinCount;
    private DataGridLayout _layout = DataGridLayout.Empty;
    private double _layoutWidth = double.NaN;
    private int _layoutVersion;
    private int _columnsVersion;
    private int _widthsVersion;
    private int _editVersion;
    private int _viewVersion;
    private decimal?[] _totals = [];
    private int _totalsKey = -1;
    private bool _slidePending;
    private bool _syncingChecked;

    #region Columns

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DataGridColumn column in e.OldItems)
            {
                column.DefinitionChanged -= OnColumnDefinitionChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (DataGridColumn column in e.NewItems)
            {
                column.DefinitionChanged += OnColumnDefinitionChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var column in Columns)
            {
                column.DefinitionChanged -= OnColumnDefinitionChanged;
                column.DefinitionChanged += OnColumnDefinitionChanged;
            }
        }
        _definitionsDirty = true;
        InvalidateColumns();
    }

    private void OnColumnDefinitionChanged(object? sender, EventArgs e)
    {
        _definitionsDirty = true;
        InvalidateColumns();
    }

    internal void InvalidateColumns()
    {
        _columnsDirty = true;
        _layoutWidth = double.NaN;
        InvalidateOptions();
        InvalidateView();
        Viewport.InvalidateText();
    }

    private void EnsureColumns()
    {
        if (!_columnsDirty)
        {
            return;
        }
        _columnsDirty = false;
        if (_definitionsDirty)
        {
            _definitionsDirty = false;
            _store.SetColumns(Columns);
            ClearErrors();
            _options.Clear();
        }
        var columns = Columns;
        var hidden = HiddenColumns is { } explicitHidden
            ? new HashSet<string>(explicitHidden, StringComparer.Ordinal)
            : columns.Where(c => c.IsHidden).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var pinned = PinnedColumnSet();
        var order = ColumnIndexOrder();
        var visible = new List<int>();
        foreach (var i in order)
        {
            if (!hidden.Contains(columns[i].Key) && pinned.Contains(columns[i].Key))
            {
                visible.Add(i);
            }
        }
        _pinCount = visible.Count;
        foreach (var i in order)
        {
            if (!hidden.Contains(columns[i].Key) && !pinned.Contains(columns[i].Key))
            {
                visible.Add(i);
            }
        }
        var previous = _visible;
        _visible = visible.ToArray();
        _columnsVersion++;
        RemapSelection(previous, _visible);
    }

    private HashSet<string> PinnedColumnSet()
    {
        if (PinnedColumns is { } explicitPinned)
        {
            return new HashSet<string>(explicitPinned, StringComparer.Ordinal);
        }
        if (Columns.Any(c => c.IsPinned.HasValue))
        {
            return Columns.Where(c => c.IsPinned == true).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        }
        return Columns.Take(1).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Accessor indices of the visible columns, pinned first.</summary>
    internal int[] VisibleAccessors
    {
        get
        {
            EnsureColumns();
            return _visible;
        }
    }

    internal int PinCount
    {
        get
        {
            EnsureColumns();
            return _pinCount;
        }
    }

    internal int ColumnsVersion => _columnsVersion;

    /// <summary>Changes whenever a column is resized (geometry only; see <see cref="ColumnsVersion"/>).</summary>
    internal int WidthsVersion => _widthsVersion;

    internal DataGridColumn ColumnOf(int accessor) => _store.Accessors[accessor].Column;

    /// <summary>The visible column at a visible index.</summary>
    internal DataGridColumn VisibleColumn(int index) => ColumnOf(VisibleAccessors[index]);

    internal int VisibleIndexOf(string key)
    {
        var visible = VisibleAccessors;
        for (var i = 0; i < visible.Length; i++)
        {
            if (_store.Accessors[visible[i]].Key == key)
            {
                return i;
            }
        }
        return -1;
    }

    internal double WidthOf(DataGridColumn column) =>
        _userWidths.TryGetValue(column.Key, out var width) ? width : column.Width;

    /// <summary>The column geometry for a viewport width (cached).</summary>
    internal DataGridLayout LayoutFor(double viewportWidth)
    {
        EnsureColumns();
        if (_layoutVersion == _columnsVersion && (_layoutWidth.Equals(viewportWidth) || Math.Abs(_layoutWidth - viewportWidth) < 0.01))
        {
            return _layout;
        }
        var widths = new double[_visible.Length];
        var mins = new double[_visible.Length];
        for (var i = 0; i < _visible.Length; i++)
        {
            var column = ColumnOf(_visible[i]);
            mins[i] = Math.Max(1, column.MinWidth);
            widths[i] = Math.Clamp(WidthOf(column), mins[i], Math.Max(mins[i], ColumnMaxWidth));
        }
        _layout = DataGridLayout.Compute(_visible, widths, mins, _pinCount, EffectiveGutterWidth, viewportWidth, PinnedColumnMinWidth);
        _layoutWidth = viewportWidth;
        _layoutVersion = _columnsVersion;
        return _layout;
    }

    /// <summary>The geometry at the viewport's current width.</summary>
    internal DataGridLayout Layout => LayoutFor(Viewport.LayoutWidth);

    /// <summary>Sets a column's width (pointer drag, Alt + arrows, double click resets).</summary>
    internal void ResizeColumn(DataGridColumn column, double? width)
    {
        if (width is { } w)
        {
            _userWidths[column.Key] = Math.Clamp(Math.Round(w), Math.Max(1, column.MinWidth), Math.Max(column.MinWidth, ColumnMaxWidth));
        }
        else
        {
            _userWidths.Remove(column.Key);
        }
        // Only the geometry changed: the cell texts stay valid (a text layout is made again only when its own column's
        // width moves), so ColumnsVersion, which every cached text and the totals and find caches are keyed on, stays.
        _widthsVersion++;
        _layoutWidth = double.NaN;
        Viewport.InvalidateMeasure();
        Viewport.InvalidateWidths();
        Viewport.SnapSelection();
        _syncingWidths = true;
        try
        {
            SetCurrentValue(ColumnWidthsProperty, new Dictionary<string, double>(_userWidths));
        }
        finally
        {
            _syncingWidths = false;
        }
    }

    #endregion

    #region Rows and view

    private void OnItemsSourceChanged()
    {
        if (HasDataSource)
        {
            return;
        }
        _store.SetSource(ItemsSource, RowKeySelector);
        ClearErrors();
    }

    private void OnStoreReset()
    {
        PruneErrors();
        InvalidateOptions();
        InvalidateView();
        Viewport.InvalidateText();
        PruneChecked();
    }

    private void OnStoreRowChanged(int sourceIndex)
    {
        _editVersion++;
        InvalidateOptions();
        Viewport.InvalidateLayers();
        UpdateStats();
    }

    internal void InvalidateView()
    {
        _viewDirty = true;
        Viewport.InvalidateMeasure();
        Viewport.InvalidateLayers();
    }

    internal void InvalidateOptions() => _options.Clear();

    private void OnSortChanged()
    {
        if (HasDataSource)
        {
            ResetBlocks();
        }
        _slidePending = IsLoaded;
        InvalidateView();
        Viewport.UpdateHeaders();
    }

    /// <summary>The current view (rebuilt when needed).</summary>
    internal DataGridView View
    {
        get
        {
            EnsureView();
            return _view;
        }
    }

    internal int ViewVersion => _viewVersion;

    internal int EditVersion => _editVersion;

    internal void EnsureView()
    {
        EnsureColumns();
        if (!_viewDirty)
        {
            return;
        }
        _viewDirty = false;
        var captured = _slidePending ? Viewport.CaptureRowPositions() : null;
        _slidePending = false;
        if (HasDataSource)
        {
            _view = BuildDataSourceView();
        }
        else
        {
            var spec = new DataGridViewSpec
            {
                Sort = SortDescriptions,
                Filters = Filters,
                Search = SearchText ?? string.Empty,
                VisibleAccessors = _visible,
                GroupBy = GroupBy,
                Culture = EffectiveCulture,
                OptionsOf = OptionsOf,
                FormatOf = a => ColumnOf(a).CellFormat
            };
            _view = DataGridView.Build(_store, spec, _collapsed);
        }
        _viewVersion++;
        _totalsKey = -1;
        VisibleRowCount = HasDataSource ? _dataSourceCount : _view.Filtered.Length;
        ClampSelection();
        if (captured is not null)
        {
            Viewport.StartSlide(captured);
        }
        Dispatcher.UIThread.Post(AfterViewChanged, DispatcherPriority.Background);
    }

    private void AfterViewChanged()
    {
        UpdatePseudoClasses();
        UpdateToolbarState();
        UpdateStatus();
        Viewport.UpdateEmptyState();
        Viewport.UpdateTotals();
    }

    /// <summary>Folds or unfolds a group row.</summary>
    internal void ToggleGroup(DataGridGroupNode node)
    {
        if (!_collapsed.Remove(node.Id))
        {
            _collapsed.Add(node.Id);
        }
        _view = View.WithCollapsed(_store.Count, _collapsed);
        _viewVersion++;
        ClampSelection();
        Viewport.InvalidateMeasure();
        Viewport.InvalidateLayers();
        Viewport.AnimateChevron(node.Id, !_collapsed.Contains(node.Id));
        UpdateToolbarState();
    }

    internal bool IsCollapsed(DataGridGroupNode node) => _collapsed.Contains(node.Id);

    internal void SetAllGroupsCollapsed(bool collapse)
    {
        _collapsed.Clear();
        if (collapse)
        {
            foreach (var group in View.AllGroups())
            {
                _collapsed.Add(group.Id);
            }
        }
        _view = View.WithCollapsed(_store.Count, _collapsed);
        _viewVersion++;
        ClampSelection();
        Viewport.InvalidateMeasure();
        Viewport.InvalidateLayers();
        UpdateToolbarState();
        Announce(DataGridText.Get(collapse ? DataGridLangResourceKind.CollapsedAllGroups : DataGridLangResourceKind.ExpandedAllGroups));
    }

    internal bool AnyCollapsed => _collapsed.Count > 0;

    /// <summary>Data rows outside collapsed groups (the selection's row space).</summary>
    internal int DisplayCount => IsShowingSkeleton ? 0 : HasDataSource ? _dataSourceCount : View.Display.Length;

    /// <summary>Drawn body lines (group rows and data rows).</summary>
    internal int LineCount => IsShowingSkeleton ? 0 : HasDataSource ? _dataSourceCount : View.Lines.Length;

    /// <summary>Whether the body shows placeholder rows instead of data.</summary>
    internal bool IsShowingSkeleton => IsLoading || HasDataSource && _dataSourceCount < 0;

    internal int LineOfRow(int row)
    {
        if (HasDataSource)
        {
            return row;
        }
        var lines = View.LineOfRow;
        return row >= 0 && row < lines.Length ? lines[row] : row;
    }

    internal int RowOfLine(int line)
    {
        if (HasDataSource)
        {
            return line;
        }
        var rows = View.RowOfLine;
        return line >= 0 && line < rows.Length ? rows[line] : Math.Max(0, DisplayCount - 1);
    }

    internal DataGridGroupNode? GroupAtLine(int line)
    {
        if (HasDataSource)
        {
            return null;
        }
        var lines = View.Lines;
        return line >= 0 && line < lines.Length ? lines[line].Group : null;
    }

    /// <summary>The source index behind a display row (−1 for data source rows or out of range).</summary>
    internal int SourceOfRow(int row)
    {
        if (HasDataSource)
        {
            return -1;
        }
        var display = View.Display;
        return row >= 0 && row < display.Length ? display[row] : -1;
    }

    /// <summary>The row item at a display row (null when not loaded).</summary>
    internal object? ItemOfRow(int row)
    {
        if (HasDataSource)
        {
            return DataSourceItem(row);
        }
        var source = SourceOfRow(row);
        return source >= 0 ? _store.ItemAt(source) : null;
    }

    internal object? KeyOfRow(int row)
    {
        if (HasDataSource)
        {
            return DataSourceKey(row);
        }
        var source = SourceOfRow(row);
        return source >= 0 ? _store.KeyAt(source) : null;
    }

    /// <summary>Changes whenever the values of a row change (text caches compare it).</summary>
    /// <summary>Cache version of a row's cell text: changes when the row's values or the column definitions change.</summary>
    internal int CellVersion(int row) => RowVersionOf(row) * 31 + ColumnsVersion;

    internal int RowVersionOf(int row)
    {
        if (HasDataSource)
        {
            return DataSourceVersion(row);
        }
        var source = SourceOfRow(row);
        return source >= 0 ? _store.RowVersion(source) : 0;
    }

    /// <summary>The normalized value of a cell (null when not loaded).</summary>
    internal object? ValueAt(int row, int accessor)
    {
        if (HasDataSource)
        {
            return DataSourceValue(row, accessor);
        }
        var source = SourceOfRow(row);
        return source >= 0 ? _store.Value(source, accessor) : null;
    }

    internal bool IsRowLoaded(int row) => !HasDataSource || DataSourceItem(row) is not null;

    /// <summary>Display text of a value in a column.</summary>
    internal string FormatValue(object? value, int accessor)
    {
        var column = ColumnOf(accessor);
        return column.EffectiveRenderer is { } renderer
            ? renderer.GetText(value, column.CellFormat, EffectiveCulture)
            : DataGridValueFormat.Format(value, column.CellFormat, EffectiveCulture);
    }

    internal string CellText(int row, int accessor) => FormatValue(ValueAt(row, accessor), accessor);

    /// <summary>The name of a row for accessible labels: the first visible column's text, else the key.</summary>
    internal string RowName(int row)
    {
        var visible = VisibleAccessors;
        if (visible.Length > 0)
        {
            var text = CellText(row, visible[0]);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }
        return Convert.ToString(KeyOfRow(row), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>The options of a select column: its <see cref="DataGridColumn.Options"/>, else up to 200 distinct values.</summary>
    internal IReadOnlyList<string> OptionsOf(int accessor)
    {
        if (_options.TryGetValue(accessor, out var cached))
        {
            return cached;
        }
        var column = ColumnOf(accessor);
        IReadOnlyList<string> result;
        if (column.Type == DataGridColumnType.Boolean)
        {
            result = ["true", "false"];
        }
        else if (column.Options is { } options)
        {
            result = options.ToArray();
        }
        else
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (!HasDataSource)
            {
                foreach (var value in _store.Column(accessor))
                {
                    if (value is not null && seen.Add(DataGridSorting.TextOf(value)) && seen.Count > SelectOptionLimit)
                    {
                        break;
                    }
                }
            }
            var compare = EffectiveCulture.CompareInfo;
            result = seen.OrderBy(s => s, Comparer<string>.Create((a, b) => compare.Compare(a, b, CompareOptions.IgnoreCase))).ToArray();
        }
        _options[accessor] = result;
        return result;
    }

    /// <summary>The text a filter checklist shows for an option: the words for a boolean column, the option itself otherwise.</summary>
    internal string FilterLabelOf(DataGridColumn column, string option) =>
        column.Type == DataGridColumnType.Boolean && DataGridValueParser.TryParseBoolean(option, out var flag)
            ? DataGridValueFormat.FormatBoolean(flag, EffectiveCulture)
            : option;

    /// <summary>Options plus the empty value when the column has empty cells (filter checklists).</summary>
    internal IReadOnlyList<string> FilterOptionsOf(int accessor)
    {
        var options = OptionsOf(accessor);
        if (HasDataSource)
        {
            return options;
        }
        foreach (var value in _store.Column(accessor))
        {
            if (DataGridSorting.IsEmpty(value))
            {
                return options.Append(string.Empty).ToArray();
            }
        }
        return options;
    }

    /// <summary>The totals of the visible columns (null where the aggregate is None or there are no numbers).</summary>
    internal decimal?[] Totals
    {
        get
        {
            var key = HashCode.Combine(_viewVersion, _editVersion, _columnsVersion, _dataSourceTotalsVersion);
            if (key == _totalsKey && _totals.Length == VisibleAccessors.Length)
            {
                return _totals;
            }
            _totalsKey = key;
            var visible = VisibleAccessors;
            _totals = new decimal?[visible.Length];
            for (var i = 0; i < visible.Length; i++)
            {
                var column = ColumnOf(visible[i]);
                var kind = column.EffectiveAggregate;
                if (kind == DataGridAggregate.None)
                {
                    continue;
                }
                if (HasDataSource)
                {
                    _totals[i] = _dataSourceTotals is { } totals && totals.TryGetValue(column.Key, out var total) ? total : null;
                    continue;
                }
                var values = _store.Column(visible[i]);
                var accumulator = new DataGridAggregates.Accumulator();
                foreach (var row in View.Filtered)
                {
                    accumulator.Add(values[row]);
                }
                _totals[i] = accumulator.Result(kind);
            }
            return _totals;
        }
    }

    /// <summary>A group's subtotal for a column (cached until the next edit).</summary>
    internal decimal? SubtotalOf(DataGridGroupNode node, int accessor)
    {
        if (node.SubtotalVersion != _editVersion || node.Subtotals is null)
        {
            node.Subtotals = new Dictionary<int, decimal?>();
            node.SubtotalVersion = _editVersion;
        }
        if (node.Subtotals.TryGetValue(accessor, out var cached))
        {
            return cached;
        }
        var kind = ColumnOf(accessor).EffectiveAggregate;
        decimal? result = null;
        if (kind != DataGridAggregate.None)
        {
            var values = _store.Column(accessor);
            var accumulator = new DataGridAggregates.Accumulator();
            foreach (var row in node.Rows)
            {
                accumulator.Add(values[row]);
            }
            result = accumulator.Result(kind);
        }
        node.Subtotals[accessor] = result;
        return result;
    }

    /// <summary>Display text of an aggregate (percent averages keep at least one decimal; counts are whole numbers).</summary>
    internal string FormatAggregate(decimal? value, int accessor)
    {
        if (value is not { } number)
        {
            return string.Empty;
        }
        var column = ColumnOf(accessor);
        var kind = column.EffectiveAggregate;
        if (kind == DataGridAggregate.Count)
        {
            return number.ToString("N0", EffectiveCulture);
        }
        var format = column.CellFormat;
        if (column.Type == DataGridColumnType.Percent && kind == DataGridAggregate.Average)
        {
            format = format with { Decimals = Math.Max(1, format.Decimals) };
        }
        return DataGridValueFormat.FormatNumber(Math.Round(number, Math.Max(2, format.Decimals)), format, EffectiveCulture);
    }

    #endregion

    #region Checked rows

    internal bool IsChecked(object? key) => key is not null && _checked.Contains(key);

    internal int CheckedCount => _checked.Count;

    private void OnCheckedRowKeysChanged()
    {
        if (_syncingChecked)
        {
            return;
        }
        _checked.Clear();
        foreach (var key in CheckedRowKeys ?? s_noKeys)
        {
            _checked.Add(key);
        }
        AfterCheckedChanged();
    }

    /// <summary>Replaces the checked set (user actions) and publishes the keys in data order.</summary>
    internal void SetChecked(IEnumerable<object> keys)
    {
        var next = new HashSet<object>(keys);
        if (next.SetEquals(_checked))
        {
            return;
        }
        _checked.Clear();
        _checked.UnionWith(next);
        _syncingChecked = true;
        try
        {
            SetCurrentValue(CheckedRowKeysProperty, CheckedKeysInDataOrder());
        }
        finally
        {
            _syncingChecked = false;
        }
        AfterCheckedChanged();
    }

    private IReadOnlyList<object> CheckedKeysInDataOrder()
    {
        if (HasDataSource)
        {
            return _checked.ToArray();
        }
        var ordered = new List<object>(_checked.Count);
        for (var i = 0; i < _store.Count; i++)
        {
            var key = _store.KeyAt(i);
            if (_checked.Contains(key))
            {
                ordered.Add(key);
            }
        }
        foreach (var key in _checked)
        {
            if (_store.IndexOfKey(key) < 0)
            {
                ordered.Add(key);
            }
        }
        return ordered;
    }

    /// <summary>The checked row items in data order.</summary>
    internal IReadOnlyList<object> CheckedItems()
    {
        if (HasDataSource)
        {
            return CheckedDataSourceItems();
        }
        var items = new List<object>(_checked.Count);
        for (var i = 0; i < _store.Count; i++)
        {
            if (_checked.Contains(_store.KeyAt(i)))
            {
                items.Add(_store.ItemAt(i));
            }
        }
        return items;
    }

    private void PruneChecked()
    {
        if (HasDataSource || _checked.Count == 0)
        {
            return;
        }
        var present = _checked.Where(k => _store.IndexOfKey(k) >= 0).ToList();
        if (present.Count != _checked.Count)
        {
            SetChecked(present);
        }
    }

    private void AfterCheckedChanged()
    {
        Viewport.InvalidateLayers();
        Viewport.UpdateSelectAll();
        UpdatePseudoClasses();
        UpdateToolbarState();
    }

    /// <summary>Checks or unchecks one row; with <paramref name="extend"/> the rows from the last toggled one get the same state.</summary>
    internal void ToggleRowChecked(int row, bool extend)
    {
        var key = KeyOfRow(row);
        if (key is null)
        {
            return;
        }
        var check = !_checked.Contains(key);
        var next = new HashSet<object>(_checked);
        if (extend && _lastToggledRow >= 0)
        {
            var (from, to) = (Math.Min(_lastToggledRow, row), Math.Max(_lastToggledRow, row));
            for (var r = from; r <= to; r++)
            {
                if (KeyOfRow(r) is { } k)
                {
                    if (check)
                    {
                        next.Add(k);
                    }
                    else
                    {
                        next.Remove(k);
                    }
                }
            }
        }
        else if (check)
        {
            next.Add(key);
        }
        else
        {
            next.Remove(key);
        }
        _lastToggledRow = row;
        SetChecked(next);
    }

    private int _lastToggledRow = -1;

    /// <summary>Checks every filtered row, or clears them when all are checked.</summary>
    internal void ToggleAllChecked()
    {
        if (HasDataSource)
        {
            return;
        }
        var filtered = View.Filtered;
        var all = filtered.Length > 0 && filtered.All(r => _checked.Contains(_store.KeyAt(r)));
        var next = new HashSet<object>(_checked);
        foreach (var row in filtered)
        {
            if (all)
            {
                next.Remove(_store.KeyAt(row));
            }
            else
            {
                next.Add(_store.KeyAt(row));
            }
        }
        SetChecked(next);
        Announce(all
            ? DataGridText.Get(DataGridLangResourceKind.RowSelectionCleared)
            : DataGridText.Count(DataGridLangResourceKind.CheckedRowsOne, DataGridLangResourceKind.CheckedRowsOther, filtered.Length, EffectiveCulture));
    }

    /// <summary>The select-all state: true (all filtered rows checked), null (some), false (none).</summary>
    internal bool? SelectAllState
    {
        get
        {
            if (HasDataSource || _checked.Count == 0)
            {
                return false;
            }
            var filtered = View.Filtered;
            if (filtered.Length == 0)
            {
                return false;
            }
            var count = filtered.Count(r => _checked.Contains(_store.KeyAt(r)));
            return count == filtered.Length ? true : count > 0 ? null : false;
        }
    }

    internal void ClearChecked()
    {
        if (_checked.Count == 0)
        {
            return;
        }
        SetChecked([]);
        Announce(DataGridText.Get(DataGridLangResourceKind.RowSelectionCleared));
    }

    #endregion

    #region Filters, hidden and pinned columns, grouping (user actions)

    internal DataGridFilter? FilterOf(string key) => Filters.TryGetValue(key, out var filter) ? filter : null;

    internal void SetFilter(string key, DataGridFilter? filter)
    {
        var next = new Dictionary<string, DataGridFilter>(Filters, StringComparer.Ordinal);
        if (filter is null || filter.Expression is { } e && string.IsNullOrEmpty(e))
        {
            next.Remove(key);
        }
        else
        {
            next[key] = filter;
        }
        SetCurrentValue(FiltersProperty, next);
    }

    /// <summary>Clears every column filter and the quick search.</summary>
    public void ClearFilters() => ClearAllFilters();

    internal void ClearAllFilters(bool announce = true)
    {
        SetCurrentValue(FiltersProperty, new Dictionary<string, DataGridFilter>());
        SetCurrentValue(SearchTextProperty, string.Empty);
        if (announce)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.FiltersCleared));
        }
    }

    /// <summary>Number of active filters (hidden columns included).</summary>
    internal int ActiveFilterCount
    {
        get
        {
            var count = 0;
            foreach (var (key, filter) in Filters)
            {
                var accessor = _store.AccessorIndex(key);
                if (accessor >= 0 && DataGridFilterExpression.IsActive(ColumnOf(accessor).Type, filter, EffectiveCulture))
                {
                    count++;
                }
            }
            return count;
        }
    }

    internal bool IsFiltering => ActiveFilterCount > 0 || !string.IsNullOrWhiteSpace(SearchText);

    internal bool IsHidden(string key)
    {
        if (HiddenColumns is { } hidden)
        {
            return hidden.Contains(key);
        }
        return Columns.FirstOrDefault(c => c.Key == key)?.IsHidden == true;
    }

    internal void ToggleHidden(string key)
    {
        var hidden = (HiddenColumns ?? Columns.Where(c => c.IsHidden).Select(c => c.Key).ToArray()).ToList();
        if (hidden.Remove(key))
        {
            SetCurrentValue(HiddenColumnsProperty, hidden.ToArray());
            return;
        }
        if (VisibleAccessors.Length <= 1)
        {
            return;
        }
        hidden.Add(key);
        SetCurrentValue(HiddenColumnsProperty, hidden.ToArray());
    }

    internal void ShowAllColumns() => SetCurrentValue(HiddenColumnsProperty, Array.Empty<string>());

    internal bool IsPinnedColumn(string key) => PinnedColumnSet().Contains(key);

    internal void TogglePinned(string key)
    {
        var pinned = PinnedColumnSet();
        if (!pinned.Remove(key))
        {
            pinned.Add(key);
        }
        SetCurrentValue(PinnedColumnsProperty, Columns.Select(c => c.Key).Where(pinned.Contains).ToArray());
    }

    internal void ToggleGroupBy(string key)
    {
        var groups = GroupBy.ToList();
        var adding = !groups.Remove(key);
        if (adding)
        {
            groups.Add(key);
        }
        SetCurrentValue(GroupByProperty, groups.ToArray());
        var label = Columns.FirstOrDefault(c => c.Key == key)?.Header ?? key;
        Announce(DataGridText.Format(adding ? DataGridLangResourceKind.GroupedBy : DataGridLangResourceKind.StoppedGroupingBy, label));
    }

    /// <summary>Applies a header click: plain cycles ascending, descending, off; Shift adds or changes a sort key.</summary>
    internal void CycleSort(string key, bool add)
    {
        var current = SortDescriptions.ToList();
        var existing = current.FindIndex(s => s.Key == key);
        DataGridSortDirection? next = existing < 0
            ? DataGridSortDirection.Ascending
            : current[existing].Direction == DataGridSortDirection.Ascending ? DataGridSortDirection.Descending : null;
        List<DataGridSort> sort;
        if (add)
        {
            sort = current;
            if (existing < 0)
            {
                sort.Add(DataGridSort.Ascending(key));
            }
            else if (next is { } direction)
            {
                sort[existing] = new DataGridSort(key, direction);
            }
            else
            {
                sort.RemoveAt(existing);
            }
        }
        else
        {
            sort = next is { } direction ? [new DataGridSort(key, direction)] : [];
        }
        ApplySort(sort);
    }

    internal void ApplySort(IReadOnlyList<DataGridSort> sort)
    {
        SetCurrentValue(SortDescriptionsProperty, sort.ToArray());
        if (sort.Count == 0)
        {
            Announce(DataGridText.Get(DataGridLangResourceKind.SortCleared));
            return;
        }
        var parts = sort.Select(s =>
        {
            var label = Columns.FirstOrDefault(c => c.Key == s.Key)?.Header ?? s.Key;
            return DataGridText.Format(s.Direction == DataGridSortDirection.Ascending
                ? DataGridLangResourceKind.SortKeyAscending
                : DataGridLangResourceKind.SortKeyDescending, label);
        });
        Announce(DataGridText.Format(DataGridLangResourceKind.SortedBy, string.Join(DataGridText.Get(DataGridLangResourceKind.SortJoin), parts)));
    }

    #endregion

    /// <summary>True when motion runs at full level (springs and slides).</summary>
    internal bool IsMotionFull => ArcMotion.IsFull(this);
}
