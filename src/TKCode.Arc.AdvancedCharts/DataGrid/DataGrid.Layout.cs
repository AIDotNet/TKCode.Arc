using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DataGrid
{
    /// <summary>
    /// Column keys in display order (people drag headers, or press Alt+Shift+arrows in one, to change it). Columns not listed
    /// follow in their defined order; null keeps the defined order. Pinned columns still come first. Two-way.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ColumnOrderProperty =
        AvaloniaProperty.Register<DataGrid, IReadOnlyList<string>?>(nameof(ColumnOrder), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The widths people set by dragging, double clicking or pressing Alt+arrows on a header, by column key. Two-way.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, double>?> ColumnWidthsProperty =
        AvaloniaProperty.Register<DataGrid, IReadOnlyDictionary<string, double>?>(nameof(ColumnWidths), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private bool _syncingWidths;

    /// <inheritdoc cref="ColumnOrderProperty"/>
    public IReadOnlyList<string>? ColumnOrder { get => GetValue(ColumnOrderProperty); set => SetValue(ColumnOrderProperty, value); }

    /// <inheritdoc cref="ColumnWidthsProperty"/>
    public IReadOnlyDictionary<string, double>? ColumnWidths { get => GetValue(ColumnWidthsProperty); set => SetValue(ColumnWidthsProperty, value); }

    /// <summary>Indices into <see cref="Columns"/> in display order before pinning and hiding: the listed keys first, then the rest as defined.</summary>
    private int[] ColumnIndexOrder()
    {
        var columns = Columns;
        var indices = Enumerable.Range(0, columns.Count);
        if (ColumnOrder is not { Count: > 0 } order)
        {
            return indices.ToArray();
        }
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < order.Count; i++)
        {
            rank.TryAdd(order[i], i);
        }
        return indices.OrderBy(i => rank.TryGetValue(columns[i].Key, out var r) ? r : int.MaxValue).ToArray();
    }

    /// <summary>The visible index a column can be moved to: within its own region, pinned or scrolling.</summary>
    internal int ClampMoveTarget(int from, int target)
    {
        var pinCount = PinCount;
        var count = VisibleAccessors.Length;
        return from < pinCount ? Math.Clamp(target, 0, Math.Max(0, pinCount - 1)) : Math.Clamp(target, pinCount, Math.Max(pinCount, count - 1));
    }

    /// <summary>Moves a visible column to another visible index within its region (pinned columns stay among the pinned ones).</summary>
    public void MoveColumn(string key, int visibleIndex)
    {
        var visible = VisibleAccessors;
        var from = VisibleIndexOf(key);
        if (from < 0)
        {
            return;
        }
        var target = ClampMoveTarget(from, visibleIndex);
        if (target == from)
        {
            return;
        }
        var pinCount = PinCount;
        var (lo, hi) = from < pinCount ? (0, pinCount - 1) : (pinCount, visible.Length - 1);
        var region = new List<string>();
        for (var i = lo; i <= hi; i++)
        {
            region.Add(_store.Accessors[visible[i]].Key);
        }
        var moving = region[from - lo];
        region.RemoveAt(from - lo);
        region.Insert(target - lo, moving);
        var all = ColumnIndexOrder().Select(i => Columns[i].Key).ToList();
        var members = region.ToHashSet(StringComparer.Ordinal);
        var next = 0;
        for (var i = 0; i < all.Count; i++)
        {
            if (members.Contains(all[i]))
            {
                all[i] = region[next++];
            }
        }
        SetCurrentValue(ColumnOrderProperty, all.ToArray());
        Announce(DataGridText.Format(DataGridLangResourceKind.ColumnMoved, Columns.First(c => c.Key == key).Header, target + 1, visible.Length));
    }

    /// <summary>Moves a visible column one place left or right (Alt+Shift+arrows in a header).</summary>
    internal void MoveColumnBy(string key, int delta)
    {
        var from = VisibleIndexOf(key);
        if (from >= 0)
        {
            MoveColumn(key, from + delta);
        }
    }

    /// <summary>Keeps the selection on the same columns when only their order changed.</summary>
    private void RemapSelection(int[] oldVisible, int[] newVisible)
    {
        if (!HasCells || oldVisible.Length != newVisible.Length || oldVisible.Length == 0 || oldVisible.SequenceEqual(newVisible))
        {
            return;
        }
        if (!oldVisible.OrderBy(i => i).SequenceEqual(newVisible.OrderBy(i => i)) || _anchor.Column >= oldVisible.Length)
        {
            return;
        }
        var anchorColumn = Array.IndexOf(newVisible, oldVisible[_anchor.Column]);
        _anchor = new DataGridCellAddress(_anchor.Row, anchorColumn);
        _focus = _anchor;
        PublishSelection();
    }

    /// <summary>Sets every column the width its widest cell and header need (the first 1,000 rows are measured).</summary>
    public void AutoFitColumns()
    {
        EnsureView();
        var widths = new Dictionary<string, double>(ColumnWidths ?? new Dictionary<string, double>(), StringComparer.Ordinal);
        foreach (var accessor in VisibleAccessors)
        {
            var column = ColumnOf(accessor);
            widths[column.Key] = ClampWidth(column, MeasureColumn(accessor, column));
        }
        SetColumnWidths(widths);
    }

    /// <summary>Sets one column to the width its widest cell and header need (double click on a header's right edge does this).</summary>
    public void AutoFitColumn(string key)
    {
        EnsureView();
        var accessor = _store.AccessorIndex(key);
        if (accessor >= 0)
        {
            var column = ColumnOf(accessor);
            ResizeColumn(column, MeasureColumn(accessor, column));
        }
    }

    /// <summary>Forgets every width people set, so columns return to their defined widths.</summary>
    public void ResetColumnWidths() => SetColumnWidths(new Dictionary<string, double>());

    private double ClampWidth(DataGridColumn column, double width) =>
        Math.Clamp(Math.Round(width), Math.Max(1, column.MinWidth), Math.Max(column.MinWidth, ColumnMaxWidth));

    private double MeasureColumn(int accessor, DataGridColumn column)
    {
        var palette = Palette;
        var padding = CellInlinePadding;
        var widest = 0.0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = Math.Min(DisplayCount, 1000);
        for (var row = 0; row < rows; row++)
        {
            var text = CellText(row, accessor);
            if (text.Length > 0 && seen.Add(text))
            {
                widest = Math.Max(widest, TextWidth(text, palette.Regular, CellFontSize, column.IsNumeric));
            }
        }
        if (column.EffectiveRenderer is not null && column.Renderer is not null)
        {
            widest += 24;
        }
        // The header carries the label plus the sort and menu buttons.
        var header = TextWidth(column.Header, palette.Medium, CellFontSize, tabular: false) + 64;
        return Math.Max(widest + padding * 2 + 2, header);
    }

    private static double TextWidth(string text, Typeface typeface, double size, bool tabular)
    {
        using var layout = new TextLayout(text, typeface, size, Brushes.Black, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, null,
            FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, double.NaN, 0, 1, tabular ? DataGridPalette.TabularNumbers : null);
        return layout.WidthIncludingTrailingWhitespace;
    }

    private void SetColumnWidths(Dictionary<string, double> widths)
    {
        _syncingWidths = true;
        try
        {
            SetCurrentValue(ColumnWidthsProperty, widths);
        }
        finally
        {
            _syncingWidths = false;
        }
        ApplyColumnWidths();
    }

    /// <summary>Rebuilds the widths people set from <see cref="ColumnWidths"/> and redraws.</summary>
    private void ApplyColumnWidths()
    {
        _userWidths.Clear();
        if (ColumnWidths is { } widths)
        {
            foreach (var (key, width) in widths)
            {
                if (double.IsFinite(width) && width > 0)
                {
                    _userWidths[key] = width;
                }
            }
        }
        _columnsVersion++;
        _layoutWidth = double.NaN;
        Viewport.InvalidateMeasure();
        Viewport.InvalidateText();
        Viewport.SnapSelection();
    }

    /// <summary>Reads the shapeable view: order, widths, pinned and hidden columns, sort, filters, grouping and density.</summary>
    public DataGridLayoutState GetLayoutState() => new()
    {
        ColumnOrder = ColumnOrder?.ToList(),
        ColumnWidths = _userWidths.Count > 0 ? new Dictionary<string, double>(_userWidths) : null,
        PinnedColumns = PinnedColumns?.ToList(),
        HiddenColumns = HiddenColumns?.ToList(),
        SortDescriptions = SortDescriptions.ToList(),
        Filters = Filters.ToDictionary(f => f.Key, f => new DataGridFilterState { Expression = f.Value.Expression, Values = f.Value.Values?.ToList() }),
        GroupBy = GroupBy.ToList(),
        Density = Density
    };

    /// <summary>Applies a state: what it lacks falls back to the defaults, and keys of columns that no longer exist are ignored.</summary>
    public void SetLayoutState(DataGridLayoutState state)
    {
        var keys = Columns.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        List<string>? Known(List<string>? list) => list?.Where(keys.Contains).ToList();
        SetCurrentValue(ColumnOrderProperty, Known(state.ColumnOrder));
        SetCurrentValue(PinnedColumnsProperty, Known(state.PinnedColumns));
        SetCurrentValue(HiddenColumnsProperty, Known(state.HiddenColumns));
        SetColumnWidths(state.ColumnWidths is { } widths
            ? widths.Where(w => keys.Contains(w.Key)).ToDictionary(w => w.Key, w => w.Value)
            : new Dictionary<string, double>());
        SetCurrentValue(GroupByProperty, state.GroupBy.Where(keys.Contains).ToList());
        SetCurrentValue(DensityProperty, state.Density);
        SetCurrentValue(FiltersProperty, state.Filters.Where(f => keys.Contains(f.Key)).ToDictionary(f => f.Key,
            f => f.Value.Values is { } values ? DataGridFilter.OneOf(values) : DataGridFilter.Text(f.Value.Expression ?? string.Empty)));
        ApplySort(state.SortDescriptions.Where(s => keys.Contains(s.Key)).ToList());
    }
}
