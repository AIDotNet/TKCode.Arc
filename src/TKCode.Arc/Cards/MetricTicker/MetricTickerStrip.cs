using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The window of a <see cref="MetricTicker"/>. Scrolling, it holds one <see cref="MetricTickerRow"/> with the metrics
/// twice in a line and slides it left at the ticker's speed (a render transform, no layout per frame), wrapping after one
/// full copy, with the edges faded. In columns (or under reduced motion) the row lays the metrics out in even columns
/// that wrap onto more lines, and nothing moves.
/// </summary>
internal sealed class MetricTickerStrip : Control
{
    internal static readonly StyledProperty<double> FadeWidthProperty =
        AvaloniaProperty.Register<MetricTickerStrip, double>(nameof(FadeWidth), 24);

    private readonly MetricTickerRow _row = new();
    private readonly TranslateTransform _shift = new();
    private readonly LiveLoop _loop;
    private MetricTicker? _owner;
    private double _offset;
    private bool _scrolling;
    private IBrush? _mask;
    private double _maskWidth = -1;
    private double _maskFade = -1;

    public MetricTickerStrip()
    {
        _row.RenderTransform = _shift;
        VisualChildren.Add(_row);
        LogicalChildren.Add(_row);
        ClipToBounds = true;
        _loop = new LiveLoop(this, OnFrame);
    }

    /// <summary>The width of the faded edges while scrolling.</summary>
    public double FadeWidth { get => GetValue(FadeWidthProperty); set => SetValue(FadeWidthProperty, value); }

    /// <summary>The row of cells (tests).</summary>
    internal MetricTickerRow Row => _row;

    /// <summary>How far the line has scrolled within one copy, in px (tests).</summary>
    internal double Offset => _offset;

    /// <summary>True while the line is laid out to scroll (it may still be held).</summary>
    internal bool IsScrollLayout => _scrolling;

    /// <summary>True while the line is moving.</summary>
    internal bool IsMoving => _loop.IsRunning;

    public void Attach(MetricTicker owner)
    {
        _owner = owner;
        Refresh();
    }

    /// <summary>Rebuilds the cells for the owner's metrics and layout.</summary>
    public void Refresh()
    {
        if (_owner is null)
        {
            return;
        }
        _scrolling = _owner.UsesScrollLayout;
        _row.SetItems(_owner.CurrentItems, _scrolling);
        if (!_scrolling)
        {
            _offset = 0;
            _shift.X = 0;
        }
        _maskWidth = -1;
        UpdateMask();
        UpdateLoop();
        InvalidateMeasure();
    }

    /// <summary>Starts or stops the line as the owner's state changes.</summary>
    public void UpdateLoop()
    {
        if (_owner is not null && _scrolling && _owner.IsMovingAllowed && _row.CopyWidth > Bounds.Width + 0.5)
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    /// <summary>Moves the line on by <paramref name="pixels"/> at once (tests; keyboard nudges).</summary>
    internal void Nudge(double pixels)
    {
        if (!_scrolling || _row.CopyWidth <= 0)
        {
            return;
        }
        _offset = Wrap(_offset + pixels, _row.CopyWidth);
        _shift.X = -Math.Round(_offset * 2) / 2;
    }

    private static double Wrap(double value, double length)
    {
        var wrapped = value % length;
        return wrapped < 0 ? wrapped + length : wrapped;
    }

    private bool OnFrame(double dt)
    {
        if (_owner is null || !_scrolling)
        {
            return false;
        }
        var copy = _row.CopyWidth;
        if (copy <= Bounds.Width + 0.5)
        {
            return false;
        }
        _offset = Wrap(_offset + _owner.Speed * dt, copy);
        _shift.X = -_offset;
        return true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_scrolling)
        {
            _row.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : _row.CopyWidth, _row.DesiredSize.Height);
        }
        _row.Measure(availableSize);
        return _row.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_scrolling)
        {
            _row.Arrange(new Rect(0, 0, _row.DesiredSize.Width, finalSize.Height));
            if (_row.CopyWidth > 0)
            {
                _offset = Wrap(_offset, _row.CopyWidth);
                _shift.X = -_offset;
            }
        }
        else
        {
            _row.Arrange(new Rect(finalSize));
        }
        UpdateMask(finalSize.Width);
        UpdateLoop();
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FadeWidthProperty)
        {
            _maskWidth = -1;
            UpdateMask();
        }
    }

    private void UpdateMask(double? width = null)
    {
        var w = width ?? Bounds.Width;
        var fade = FadeWidth;
        var wants = _scrolling && w > 0 && fade > 0 && _row.CopyWidth > w + 0.5;
        if (!wants)
        {
            if (OpacityMask is not null)
            {
                OpacityMask = null;
            }
            return;
        }
        if (Math.Abs(w - _maskWidth) < 0.5 && Math.Abs(fade - _maskFade) < 0.01 && _mask is not null)
        {
            return;
        }
        _maskWidth = w;
        _maskFade = fade;
        var edge = Math.Clamp(fade / w, 0, 0.45);
        _mask = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Colors.Transparent),
                new ImmutableGradientStop(edge, Colors.Black),
                new ImmutableGradientStop(1 - edge, Colors.Black),
                new ImmutableGradientStop(1, Colors.Transparent)
            ],
            startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative),
            endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));
        OpacityMask = _mask;
    }
}

/// <summary>
/// The cells of a <see cref="MetricTicker"/>: in one line (each metric twice, the second copy hidden from screen readers,
/// a small dot between neighbours) or in even columns of at least <see cref="MinColumnWidth"/> that wrap.
/// </summary>
internal sealed class MetricTickerRow : Control
{
    internal static readonly StyledProperty<double> ItemGapProperty =
        AvaloniaProperty.Register<MetricTickerRow, double>(nameof(ItemGap), 24);

    internal static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<MetricTickerRow, double>(nameof(MinColumnWidth), 150);

    internal static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<MetricTickerRow, double>(nameof(RowGap), 8);

    internal static readonly StyledProperty<IBrush?> SeparatorBrushProperty =
        AvaloniaProperty.Register<MetricTickerRow, IBrush?>(nameof(SeparatorBrush));

    private readonly List<MetricTickerCell> _cells = new();
    private readonly List<MetricTickerCell> _pool = new();
    private readonly List<double> _separators = new();
    private bool _line;
    private int _count;
    private int _columns = 1;

    static MetricTickerRow()
    {
        AffectsMeasure<MetricTickerRow>(ItemGapProperty, MinColumnWidthProperty, RowGapProperty);
        AffectsRender<MetricTickerRow>(SeparatorBrushProperty);
    }

    public double ItemGap { get => GetValue(ItemGapProperty); set => SetValue(ItemGapProperty, value); }

    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public IBrush? SeparatorBrush { get => GetValue(SeparatorBrushProperty); set => SetValue(SeparatorBrushProperty, value); }

    /// <summary>The width of one copy of the line, gap after the last metric included.</summary>
    public double CopyWidth { get; private set; }

    /// <summary>The cells (both copies while scrolling).</summary>
    public IReadOnlyList<MetricTickerCell> Cells => _cells;

    /// <summary>Columns in the column layout.</summary>
    public int Columns => _columns;

    /// <summary>Shows <paramref name="items"/>, reusing cells: twice in one line, or once in columns.</summary>
    public void SetItems(IReadOnlyList<MetricTickerItem> items, bool line)
    {
        _line = line;
        _count = items.Count;
        var needed = line ? items.Count * 2 : items.Count;
        while (_cells.Count > needed)
        {
            var cell = _cells[^1];
            _cells.RemoveAt(_cells.Count - 1);
            VisualChildren.Remove(cell);
            LogicalChildren.Remove(cell);
            cell.Item = null;
            _pool.Add(cell);
        }
        while (_cells.Count < needed)
        {
            MetricTickerCell cell;
            if (_pool.Count > 0)
            {
                cell = _pool[^1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                cell = new MetricTickerCell();
            }
            _cells.Add(cell);
            VisualChildren.Add(cell);
            LogicalChildren.Add(cell);
        }
        for (var i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            cell.Item = items.Count == 0 ? null : items[i % items.Count];
            cell.IsStacked = !line;
            // The second copy only fills the line; screen readers hear each metric once.
            AutomationProperties.SetAccessibilityView(cell, line && i >= items.Count ? AccessibilityView.Raw : AccessibilityView.Control);
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        if (_line)
        {
            double width = 0;
            double height = 0;
            for (var i = 0; i < _cells.Count; i++)
            {
                _cells[i].Measure(infinite);
                if (i < _count)
                {
                    width += _cells[i].DesiredSize.Width + ItemGap;
                }
                height = Math.Max(height, _cells[i].DesiredSize.Height);
            }
            CopyWidth = width;
            return new Size(width * 2, height);
        }
        var available = double.IsFinite(availableSize.Width) ? availableSize.Width : _cells.Count * MinColumnWidth;
        _columns = Math.Max(1, Math.Min(Math.Max(1, _cells.Count), (int)Math.Floor((available + ItemGap) / (MinColumnWidth + ItemGap))));
        var columnWidth = Math.Max(0, (available - (_columns - 1) * ItemGap) / _columns);
        double total = 0;
        double rowHeight = 0;
        for (var i = 0; i < _cells.Count; i++)
        {
            _cells[i].Measure(new Size(columnWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, _cells[i].DesiredSize.Height);
            if (i % _columns == _columns - 1 || i == _cells.Count - 1)
            {
                total += rowHeight + (total > 0 ? RowGap : 0);
                rowHeight = 0;
            }
        }
        CopyWidth = 0;
        return new Size(available, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _separators.Clear();
        if (_line)
        {
            double x = 0;
            foreach (var cell in _cells)
            {
                cell.Arrange(new Rect(x, (finalSize.Height - cell.DesiredSize.Height) / 2, cell.DesiredSize.Width, cell.DesiredSize.Height));
                x += cell.DesiredSize.Width + ItemGap;
                _separators.Add(x - ItemGap / 2);
            }
            InvalidateVisual();
            return finalSize;
        }
        var columnWidth = Math.Max(0, (finalSize.Width - (_columns - 1) * ItemGap) / _columns);
        double y = 0;
        for (var start = 0; start < _cells.Count; start += _columns)
        {
            double rowHeight = 0;
            for (var i = start; i < Math.Min(_cells.Count, start + _columns); i++)
            {
                rowHeight = Math.Max(rowHeight, _cells[i].DesiredSize.Height);
            }
            for (var i = start; i < Math.Min(_cells.Count, start + _columns); i++)
            {
                var column = i - start;
                _cells[i].Arrange(new Rect(column * (columnWidth + ItemGap), y, columnWidth, rowHeight));
            }
            y += rowHeight + RowGap;
        }
        InvalidateVisual();
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (!_line || SeparatorBrush is not { } brush)
        {
            return;
        }
        var y = Bounds.Height / 2;
        foreach (var x in _separators)
        {
            context.DrawEllipse(brush, null, new Point(x, y), 1.5, 1.5);
        }
    }
}
