using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The list of a <see cref="TocProgress"/>: the rows stacked top to bottom and, along their left edge, the line. The line
/// runs from the first row to the last through one lane per level, bending in a short S where two rows of different
/// levels meet. The part already read is drawn wider in the accent, a marker rides its tip, and the marker cuts a small gap
/// into the line around itself.
/// </summary>
/// <remarks>
/// Code-drawn because the read part and the marker follow a continuous reading position (owned and animated by the
/// <see cref="TocProgress"/>) along a path measured from the rows' layout; a ControlTheme cannot place a point along a
/// path. The path is flattened once per layout; scrolling only redraws.
/// </remarks>
internal sealed class TocProgressList : Control
{
    /// <summary>Line segments per S bend.</summary>
    private const int BendSteps = 16;

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<TocProgressList, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<TocProgressList, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<double> RailThicknessProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(RailThickness), 1.5);

    public static readonly StyledProperty<double> FillThicknessProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(FillThickness), 2);

    public static readonly StyledProperty<double> RailGutterProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(RailGutter), 22);

    public static readonly StyledProperty<double> LevelIndentProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(LevelIndent), 12);

    public static readonly StyledProperty<double> LaneStartProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(LaneStart), 5);

    public static readonly StyledProperty<double> BendHeightProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(BendHeight), 14);

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(DotSize), 7);

    public static readonly StyledProperty<double> DotGapProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(DotGap), 2);

    public static readonly StyledProperty<Thickness> RowPaddingProperty =
        AvaloniaProperty.Register<TocProgressList, Thickness>(nameof(RowPadding), new Thickness(0, 4, 8, 4));

    public static readonly StyledProperty<double> TextSizeProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(TextSize), 13);

    public static readonly StyledProperty<double> TextLineHeightProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(TextLineHeight), 18.2);

    public static readonly StyledProperty<double> TextLetterSpacingProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(TextLetterSpacing));

    private readonly List<TocProgressRow> _rows = [];
    private readonly List<Point> _points = [];
    private readonly List<double> _lengths = [];
    private double[] _knots = [];
    private Pen? _trackPen;
    private Pen? _fillPen;
    private StreamGeometry? _trackGeometry;
    private StreamGeometry? _fillGeometry;
    private double _drawnAt = double.NaN;
    private double _reading;
    private int _active = -1;

    static TocProgressList()
    {
        AffectsRender<TocProgressList>(TrackBrushProperty, FillBrushProperty, RailThicknessProperty, FillThicknessProperty,
            DotSizeProperty, DotGapProperty);
        AffectsMeasure<TocProgressList>(RailGutterProperty, LevelIndentProperty, RowPaddingProperty, TextSizeProperty,
            TextLineHeightProperty, TextLetterSpacingProperty);
        AffectsArrange<TocProgressList>(LaneStartProperty, BendHeightProperty);
        ClipToBoundsProperty.OverrideDefaultValue<TocProgressList>(false);
    }

    public TocProgressList()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    public TocProgress? Owner { get; set; }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public double RailThickness { get => GetValue(RailThicknessProperty); set => SetValue(RailThicknessProperty, value); }

    public double FillThickness { get => GetValue(FillThicknessProperty); set => SetValue(FillThicknessProperty, value); }

    public double RailGutter { get => GetValue(RailGutterProperty); set => SetValue(RailGutterProperty, value); }

    public double LevelIndent { get => GetValue(LevelIndentProperty); set => SetValue(LevelIndentProperty, value); }

    public double LaneStart { get => GetValue(LaneStartProperty); set => SetValue(LaneStartProperty, value); }

    public double BendHeight { get => GetValue(BendHeightProperty); set => SetValue(BendHeightProperty, value); }

    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    public double DotGap { get => GetValue(DotGapProperty); set => SetValue(DotGapProperty, value); }

    public Thickness RowPadding { get => GetValue(RowPaddingProperty); set => SetValue(RowPaddingProperty, value); }

    public double TextSize { get => GetValue(TextSizeProperty); set => SetValue(TextSizeProperty, value); }

    public double TextLineHeight { get => GetValue(TextLineHeightProperty); set => SetValue(TextLineHeightProperty, value); }

    public double TextLetterSpacing { get => GetValue(TextLetterSpacingProperty); set => SetValue(TextLetterSpacingProperty, value); }

    public IReadOnlyList<TocProgressRow> Rows => _rows;

    /// <summary>The reading position drawn now: section index plus fraction, 0 to the number of rows.</summary>
    public double Reading => _reading;

    /// <summary>Length of the whole line (tests read it).</summary>
    public double LineLength => _lengths.Count > 0 ? _lengths[^1] : 0;

    /// <summary>Distance along the line to the marker.</summary>
    public double MarkerDistance => DistanceAt(_reading);

    /// <summary>Where the marker is, in the list's coordinates.</summary>
    public Point MarkerPoint => PointAt(MarkerDistance);

    /// <summary>The point of row <paramref name="index"/> on the line: its lane at its vertical centre.</summary>
    public Point HeadingPoint(int index) => index >= 0 && index < _knots.Length - 1 ? PointAt(_knots[index]) : default;

    /// <summary>Rebuilds the rows for <paramref name="entries"/>, reusing the rows that exist.</summary>
    public void SetEntries(IReadOnlyList<TocProgressEntry> entries)
    {
        while (_rows.Count > entries.Count)
        {
            var row = _rows[^1];
            _rows.RemoveAt(_rows.Count - 1);
            VisualChildren.Remove(row);
            LogicalChildren.Remove(row);
        }
        for (var i = 0; i < entries.Count; i++)
        {
            TocProgressRow row;
            if (i < _rows.Count)
            {
                row = _rows[i];
            }
            else
            {
                row = new TocProgressRow(this, i);
                _rows.Add(row);
                LogicalChildren.Add(row);
                VisualChildren.Add(row);
            }
            row.Index = i;
            row.Title = entries[i].Title;
            row.Level = entries[i].Level;
        }
        ApplyStates();
        InvalidateMeasure();
    }

    /// <summary>Shows the reader at <paramref name="reading"/> (section index plus fraction) with row <paramref name="active"/> current.</summary>
    public void SetReading(double reading, int active)
    {
        _reading = reading;
        if (active != _active)
        {
            _active = active;
            ApplyStates();
        }
        InvalidateVisual();
    }

    private void ApplyStates()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].State = i < _active ? TocProgressRowState.Read : i == _active ? TocProgressRowState.Active : TocProgressRowState.Upcoming;
        }
    }

    public bool FocusRow(int index)
    {
        if (_rows.Count == 0)
        {
            return false;
        }
        var row = _rows[Math.Clamp(index, 0, _rows.Count - 1)];
        row.Focus(NavigationMethod.Directional);
        return true;
    }

    public void Invoke(TocProgressRow row, bool keyboard) => Owner?.ScrollTo(row.Index, keyboard);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TrackBrushProperty || change.Property == RailThicknessProperty)
        {
            _trackPen = null;
        }
        else if (change.Property == FillBrushProperty || change.Property == FillThicknessProperty)
        {
            _fillPen = null;
        }
        else if (change.Property == TextSizeProperty || change.Property == TextLineHeightProperty
                 || change.Property == TextLetterSpacingProperty
                 || change.Property == Avalonia.Controls.Documents.TextElement.FontFamilyProperty)
        {
            foreach (var row in _rows)
            {
                row.ClearLayout();
                row.InvalidateMeasure();
            }
        }
        if (change.Property == DotSizeProperty || change.Property == DotGapProperty || change.Property == RailThicknessProperty
            || change.Property == FillThicknessProperty)
        {
            _drawnAt = double.NaN;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 260 : availableSize.Width;
        double height = 0;
        foreach (var row in _rows)
        {
            row.Measure(new Size(width, double.PositiveInfinity));
            height += row.DesiredSize.Height;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        foreach (var row in _rows)
        {
            var h = row.DesiredSize.Height;
            row.Arrange(new Rect(0, y, finalSize.Width, h));
            y += h;
        }
        BuildPath();
        return finalSize;
    }

    private double LaneOf(int level) => LaneStart + (Math.Clamp(level, 1, 3) - 1) * LevelIndent;

    /// <summary>
    /// Flattens the line: from the first row's point down each lane, an S bend centred on the boundary between rows of
    /// different levels, and on to the last row's bottom edge (inside its padding). The distance at each row's point is
    /// kept, so a section spans the line from its heading to the next one, bends included.
    /// </summary>
    private void BuildPath()
    {
        _points.Clear();
        _lengths.Clear();
        _drawnAt = double.NaN;
        var n = _rows.Count;
        if (_knots.Length != n + 1)
        {
            _knots = new double[n + 1];
        }
        if (n == 0)
        {
            return;
        }
        Add(new Point(LaneOf(_rows[0].Level), Centre(_rows[0])));
        _knots[0] = 0;
        var half = BendHeight / 2;
        for (var i = 0; i + 1 < n; i++)
        {
            var from = _rows[i];
            var to = _rows[i + 1];
            var x0 = LaneOf(from.Level);
            var x1 = LaneOf(to.Level);
            var c1 = Centre(to);
            if (Math.Abs(x1 - x0) > 0.01)
            {
                var boundary = to.Bounds.Top;
                var h = Math.Max(0, Math.Min(half, Math.Min(boundary - Centre(from), c1 - boundary) - 0.5));
                Add(new Point(x0, boundary - h));
                for (var k = 1; k <= BendSteps; k++)
                {
                    var t = (double)k / BendSteps;
                    // Cubic with vertical tangents: (x0, b−h) → (x0, b) → (x1, b) → (x1, b+h).
                    var u = 1 - t;
                    var bx = u * u * u * x0 + 3 * u * u * t * x0 + 3 * u * t * t * x1 + t * t * t * x1;
                    var by = u * u * u * (boundary - h) + 3 * u * u * t * boundary + 3 * u * t * t * boundary + t * t * t * (boundary + h);
                    Add(new Point(bx, by));
                }
            }
            Add(new Point(x1, c1));
            _knots[i + 1] = _lengths[^1];
        }
        var last = _rows[^1];
        var end = Math.Max(Centre(last), last.Bounds.Bottom - RowPadding.Bottom);
        Add(new Point(LaneOf(last.Level), end));
        _knots[n] = _lengths[^1];
        InvalidateVisual();
    }

    private static double Centre(TocProgressRow row) => row.Bounds.Top + row.Bounds.Height / 2;

    private void Add(Point point)
    {
        if (_points.Count == 0)
        {
            _points.Add(point);
            _lengths.Add(0);
            return;
        }
        var previous = _points[^1];
        var d = Point.Distance(previous, point);
        if (d < 0.001)
        {
            return;
        }
        _points.Add(point);
        _lengths.Add(_lengths[^1] + d);
    }

    /// <summary>The distance along the line of reading position <paramref name="reading"/>.</summary>
    public double DistanceAt(double reading)
    {
        var n = _knots.Length - 1;
        if (n <= 0 || _lengths.Count == 0)
        {
            return 0;
        }
        var r = Math.Clamp(reading, 0, n);
        var i = (int)Math.Floor(r);
        if (i >= n)
        {
            return _knots[n];
        }
        return _knots[i] + (r - i) * (_knots[i + 1] - _knots[i]);
    }

    private int SegmentAt(double distance)
    {
        // The last point whose distance is at or below the given one.
        int lo = 0, hi = _lengths.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (_lengths[mid] <= distance)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return lo;
    }

    private Point PointAt(double distance)
    {
        if (_points.Count == 0)
        {
            return default;
        }
        if (distance <= 0)
        {
            return _points[0];
        }
        if (distance >= _lengths[^1])
        {
            return _points[^1];
        }
        var i = SegmentAt(distance);
        var a = _points[i];
        var b = _points[Math.Min(i + 1, _points.Count - 1)];
        var span = _lengths[Math.Min(i + 1, _lengths.Count - 1)] - _lengths[i];
        var t = span <= 0 ? 0 : (distance - _lengths[i]) / span;
        return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>The part of the line between two distances, or null when it is empty.</summary>
    private StreamGeometry? Range(double from, double to)
    {
        from = Math.Max(0, from);
        to = Math.Min(_lengths.Count > 0 ? _lengths[^1] : 0, to);
        if (to - from < 0.01)
        {
            return null;
        }
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(PointAt(from), false);
        var i = SegmentAt(from) + 1;
        while (i < _points.Count && _lengths[i] < to)
        {
            ctx.LineTo(_points[i]);
            i++;
        }
        ctx.LineTo(PointAt(to));
        ctx.EndFigure(false);
        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        if (_rows.Count == 0 || _points.Count < 2)
        {
            return;
        }
        var d = DistanceAt(_reading);
        var r = DotSize / 2;
        if (!(Math.Abs(d - _drawnAt) < 0.001))
        {
            // The marker cuts a clear gap of DotGap around itself; a round cap reaches half the stroke past the end.
            _drawnAt = d;
            _trackGeometry = Range(d + r + DotGap + RailThickness / 2, double.PositiveInfinity);
            _fillGeometry = Range(0, d - r - DotGap - FillThickness / 2);
        }
        _trackPen ??= TrackBrush is { } track ? new Pen(track, RailThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round) : null;
        _fillPen ??= FillBrush is { } fill ? new Pen(fill, FillThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round) : null;
        if (_trackPen is not null && _trackGeometry is not null)
        {
            context.DrawGeometry(null, _trackPen, _trackGeometry);
        }
        if (_fillPen is not null && _fillGeometry is not null)
        {
            context.DrawGeometry(null, _fillPen, _fillGeometry);
        }
        if (FillBrush is { } marker)
        {
            context.DrawEllipse(marker, null, PointAt(d), r, r);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer(TocProgressList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}
