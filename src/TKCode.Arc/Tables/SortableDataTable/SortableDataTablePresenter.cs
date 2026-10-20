using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The scrolled content of a <see cref="SortableDataTable"/>: the sticky header row and the realized body rows.
/// Rows are virtualized against the effective viewport (works inside the table's own scroller and inside any
/// scrolling ancestor), recycled through a pool, and keyed by row key so that a re-sort or a source change can
/// slide surviving rows from where they were drawn to their new slots (FLIP). Rows entering or leaving the
/// viewport travel from or to just beyond its edge, then the leaving ones are recycled.
/// </summary>
internal sealed class SortableDataTablePresenter : Control
{
    private const int Buffer = 2;

    private readonly SortableDataTable _table;
    private readonly SortableDataTableHeaderRow _header;
    private readonly Dictionary<object, SortableDataTableRow> _realized = new();
    private readonly List<SortableDataTableRow> _pool = new();
    private readonly List<object> _stale = new();
    private Rect _viewport;
    private bool _hasViewport;
    private int _first;
    private int _last = -1;
    private int _generation;
    private int _pinned = -1;
    private Flip? _flip;
    private FormattedText? _empty;
    private string? _emptyText;
    private int _emptyVersion = -1;

    private sealed class Flip
    {
        public readonly Dictionary<object, double> VisualY = new();
        public Func<object, int>? OldDisplay;
        public bool Animate;
        public Rect Viewport;
        public double RowHeight;
        public double HeaderHeight;
    }

    public SortableDataTablePresenter(SortableDataTable table)
    {
        _table = table;
        _header = new SortableDataTableHeaderRow(table);
        LogicalChildren.Add(_header);
        VisualChildren.Add(_header);
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    public SortableDataTableHeaderRow Header => _header;

    public IEnumerable<SortableDataTableRow> RealizedRows => _realized.Values;

    internal int RealizedCount => _realized.Count;

    internal int PooledCount => _pool.Count;

    /// <summary>Visible part of the presenter (its own coordinates), when known.</summary>
    public Rect Viewport => _hasViewport ? _viewport : new Rect(Bounds.Size);

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var viewport = e.EffectiveViewport;
        if (viewport.Width <= 0 || viewport.Height <= 0 || !double.IsFinite(viewport.Y) || !double.IsFinite(viewport.Height))
        {
            return;
        }
        _viewport = viewport;
        _hasViewport = true;
        _header.StickOffset = StickOffset();
        var layout = _table.Layout;
        var count = _table.RowCount;
        if (count == 0)
        {
            return;
        }
        var (first, last) = RangeFor(layout);
        if (first < _first || last > _last || first > _first + Buffer * 2 || last < _last - Buffer * 2)
        {
            InvalidateMeasure();
        }
    }

    private double StickOffset()
    {
        if (!_hasViewport)
        {
            return 0;
        }
        var max = Math.Max(0, Bounds.Height - _table.Layout.HeaderHeight);
        return Math.Clamp(_viewport.Top, 0, max);
    }

    private (int First, int Last) RangeFor(SortableDataTableLayout layout)
    {
        var count = _table.RowCount;
        double top;
        double bottom;
        if (_hasViewport)
        {
            top = _viewport.Top;
            bottom = _viewport.Bottom;
        }
        else
        {
            top = 0;
            bottom = 900;
        }
        var first = (int)Math.Floor((top - layout.HeaderHeight) / layout.RowHeight) - Buffer;
        var last = (int)Math.Ceiling((bottom - layout.HeaderHeight) / layout.RowHeight) + Buffer;
        return (Math.Max(0, first), Math.Min(count - 1, last));
    }

    /// <summary>Records where every realized row is drawn before the order or the source changes.</summary>
    public void CaptureFlip(Func<object, int>? oldDisplay, bool animate)
    {
        var layout = _table.Layout;
        var flip = new Flip
        {
            OldDisplay = oldDisplay,
            Animate = animate && _hasViewport,
            Viewport = Viewport,
            RowHeight = layout.RowHeight,
            HeaderHeight = layout.HeaderHeight
        };
        foreach (var pair in _realized)
        {
            if (pair.Value.HasLayout)
            {
                flip.VisualY[pair.Key] = pair.Value.VisualY;
            }
        }
        _flip = flip;
        InvalidateMeasure();
    }

    /// <summary>Recycles every row (columns or templates changed).</summary>
    public void Reset()
    {
        foreach (var row in _realized.Values)
        {
            Recycle(row);
        }
        _realized.Clear();
        foreach (var row in _pool)
        {
            row.SyncTemplates();
            row.InvalidateTexts();
        }
        _flip = null;
        InvalidateMeasure();
    }

    public void InvalidateRows()
    {
        foreach (var row in _realized.Values)
        {
            row.InvalidateTexts();
        }
        _header.InvalidateVisual();
        foreach (var cell in _header.Cells)
        {
            cell.InvalidateVisual();
        }
        InvalidateVisual();
    }

    public SortableDataTableRow? RowAt(int displayIndex)
    {
        foreach (var row in _realized.Values)
        {
            if (row.DisplayIndex == displayIndex && !row.IsLeaving)
            {
                return row;
            }
        }
        return null;
    }

    /// <summary>Realizes the row at <paramref name="displayIndex"/> now (keyboard moves past the viewport).</summary>
    public SortableDataTableRow? EnsureRow(int displayIndex)
    {
        if (RowAt(displayIndex) is { } existing)
        {
            return existing;
        }
        _pinned = displayIndex;
        InvalidateMeasure();
        UpdateLayout();
        _pinned = -1;
        return RowAt(displayIndex);
    }

    /// <summary>Scrolls a row into view below the sticky header.</summary>
    public void RevealRow(int displayIndex)
    {
        var layout = _table.Layout;
        var y = layout.HeaderHeight + displayIndex * layout.RowHeight;
        this.BringIntoView(new Rect(0, Math.Max(0, y - layout.HeaderHeight), Math.Min(Bounds.Width, 1), layout.RowHeight + layout.HeaderHeight));
    }

    public void UpdateTabStops()
    {
        var current = _table.CurrentRow;
        SortableDataTableRow? stop = null;
        SortableDataTableRow? firstVisible = null;
        foreach (var row in _realized.Values)
        {
            if (row.IsLeaving)
            {
                continue;
            }
            if (row.DisplayIndex == current)
            {
                stop = row;
            }
            if (firstVisible is null || row.DisplayIndex < firstVisible.DisplayIndex)
            {
                var y = _table.Layout.HeaderHeight + row.DisplayIndex * _table.Layout.RowHeight;
                if (!_hasViewport || y >= _viewport.Top)
                {
                    firstVisible = row;
                }
            }
        }
        stop ??= firstVisible;
        foreach (var row in _realized.Values)
        {
            row.IsTabStop = _table.IsSelectable && ReferenceEquals(row, stop);
        }
    }

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        var layout = _table.Layout;
        var hint = _table.TableWidthHint;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width
            : double.IsFinite(hint) ? hint : _table.NaturalTableWidth();
        var reference = double.IsFinite(hint) ? hint : width;
        var compact = reference < _table.CompactBreakpoint;
        if (!compact)
        {
            width = Math.Max(width, _table.MinTableWidth);
        }
        var compactChanged = compact != layout.Compact;
        layout.Compact = compact;
        layout.Width = width;
        layout.RowHeight = compact ? _table.CompactRowHeight : _table.RowHeight;
        layout.HeaderHeight = compact ? _table.CompactHeaderHeight : _table.HeaderHeight;
        _table.ComputeColumns(layout);
        if (compactChanged)
        {
            InvalidateRows();
            _table.OnLayoutModeChanged();
        }
        _header.Measure(new Size(width, layout.HeaderHeight));
        Realize(layout);
        foreach (var row in _realized.Values)
        {
            row.Measure(new Size(width, layout.RowHeight));
        }
        var count = _table.RowCount;
        var body = count > 0 ? count * layout.RowHeight : _table.EmptyRowHeight;
        return new Size(width, layout.HeaderHeight + body);
    }

    private void Realize(SortableDataTableLayout layout)
    {
        var count = _table.RowCount;
        _generation++;
        var generation = _generation;
        var (first, last) = count > 0 ? RangeFor(layout) : (0, -1);
        _first = first;
        _last = last;
        for (var display = first; display <= last; display++)
        {
            Use(display, generation, leaving: false);
        }
        if (_pinned >= 0 && _pinned < count)
        {
            Use(_pinned, generation, leaving: false);
        }
        _stale.Clear();
        foreach (var pair in _realized)
        {
            var row = pair.Value;
            if (row.Stamp == generation)
            {
                continue;
            }
            var source = _table.SourceIndexOfKey(pair.Key);
            var keep = source >= 0 && (row.IsFocused || row.IsKeyboardFocusWithin
                                       || (_flip is { Animate: true } flip && flip.VisualY.ContainsKey(pair.Key))
                                       || (row.IsLeaving && row.IsSettling));
            if (keep)
            {
                var display = _table.DisplayIndexOf(source);
                row.Configure(pair.Key, source, display, _table.ItemAt(source), _table.IsSourceSelected(source));
                row.IsLast = display == count - 1;
                row.IsLeaving = !(row.IsFocused || row.IsKeyboardFocusWithin);
                row.Stamp = generation;
                continue;
            }
            _stale.Add(pair.Key);
        }
        foreach (var key in _stale)
        {
            Recycle(_realized[key]);
            _realized.Remove(key);
        }
        _stale.Clear();
        UpdateTabStops();
    }

    private void Use(int display, int generation, bool leaving)
    {
        var source = _table.SourceAt(display);
        var key = _table.KeyAt(source);
        if (!_realized.TryGetValue(key, out var row))
        {
            row = Rent();
            _realized[key] = row;
        }
        row.Configure(key, source, display, _table.ItemAt(source), _table.IsSourceSelected(source));
        row.IsLast = display == _table.RowCount - 1;
        row.IsLeaving = leaving;
        row.Stamp = generation;
    }

    private SortableDataTableRow Rent()
    {
        SortableDataTableRow row;
        if (_pool.Count > 0)
        {
            row = _pool[^1];
            _pool.RemoveAt(_pool.Count - 1);
            row.IsVisible = true;
        }
        else
        {
            row = new SortableDataTableRow(_table);
            LogicalChildren.Add(row);
            VisualChildren.Add(row);
            row.SyncTemplates();
        }
        return row;
    }

    private void Recycle(SortableDataTableRow row)
    {
        row.Detach();
        row.IsVisible = false;
        row.IsTabStop = false;
        _pool.Add(row);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = _table.Layout;
        var width = finalSize.Width;
        _header.Arrange(new Rect(0, 0, width, layout.HeaderHeight));
        var flip = _flip;
        _flip = null;
        foreach (var row in _realized.Values)
        {
            var y = layout.HeaderHeight + row.DisplayIndex * layout.RowHeight;
            if (row.IsLeaving && flip is not null)
            {
                y = Band(flip, y);
            }
            else if (row.IsLeaving)
            {
                y = row.HasLayout ? row.LayoutY : y;
            }
            if (flip is not null)
            {
                double from = 0;
                if (row.Key is { } key && flip.VisualY.TryGetValue(key, out var oldVisual))
                {
                    from = oldVisual - y;
                }
                else if (row.Key is { } enteringKey && (flip.OldDisplay?.Invoke(enteringKey) ?? -1) is var old && old >= 0)
                {
                    from = Band(flip, flip.HeaderHeight + old * flip.RowHeight) - y;
                }
                row.SlideFrom(from, flip.Animate);
                if (row.IsLeaving)
                {
                    _ = RecycleWhenSettled(row);
                }
            }
            else if (row.HasLayout && Math.Abs(row.LayoutY - y) > 0.01 && row.IsSettling)
            {
                row.Rebase(y - row.LayoutY);
            }
            row.LayoutY = y;
            row.HasLayout = true;
            row.Arrange(new Rect(0, y, width, layout.RowHeight));
        }
        _header.StickOffset = StickOffset();
        return finalSize;
    }

    /// <summary>Clamps a position to just beyond the visible band so off-screen travel stays short.</summary>
    private static double Band(Flip flip, double y)
    {
        var viewport = flip.Viewport;
        return Math.Clamp(y, viewport.Top - flip.RowHeight, Math.Max(viewport.Top, viewport.Bottom));
    }

    private async Task RecycleWhenSettled(SortableDataTableRow row)
    {
        if (row.Settle is { } settle)
        {
            await settle;
        }
        if (row.IsLeaving && !row.IsSettling)
        {
            InvalidateMeasure();
        }
    }

    #endregion

    public override void Render(DrawingContext context)
    {
        var palette = _table.Palette;
        var layout = _table.Layout;
        var count = _table.RowCount;
        if (count == 0)
        {
            if (_table.IsLoading)
            {
                return;
            }
            var text = _table.EmptyMessage;
            if (_empty is null || _emptyText != text || _emptyVersion != palette.Version)
            {
                _emptyText = text;
                _emptyVersion = palette.Version;
                _empty = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, palette.Regular,
                    palette.FontSize, palette.TextMutedBrush)
                {
                    TextAlignment = TextAlignment.Center
                };
            }
            _empty.MaxTextWidth = Math.Max(1, Bounds.Width - 32);
            var area = new Rect(0, layout.HeaderHeight, Bounds.Width, _table.EmptyRowHeight);
            context.DrawText(_empty, new Point(16, area.Y + (area.Height - _empty.Height) / 2));
            return;
        }
        var sorted = _table.SortedColumnIndex;
        if (sorted >= 0 && !layout.Compact)
        {
            // A still tint under the sorted column fills the gaps that open while rows pass each other.
            context.FillRectangle(palette.ColumnTintBrush,
                new Rect(layout.ColumnX(sorted), layout.HeaderHeight, layout.ColumnWidth(sorted), count * layout.RowHeight));
        }
    }
}
