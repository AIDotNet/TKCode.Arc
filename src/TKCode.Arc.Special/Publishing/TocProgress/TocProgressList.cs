using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The list of a <see cref="TocProgress"/>: the rows stacked top to bottom and, along their left edge, the rail. The rail
/// is a hairline track, an accent fill that grows down the list as the article is read (ending inside the active row as
/// far as its section has been read), a dot at the fill's end and a brighter segment beside the active row.
/// </summary>
/// <remarks>
/// Code-driven because the fill's end and the active segment follow the scroll position continuously and must keep their
/// velocity when the reader changes direction mid-glide; both are springs drawn in <see cref="Render"/>, so scrolling
/// never lays the rows out again.
/// </remarks>
internal sealed class TocProgressList : Control
{
    /// <summary>The fill follows the reading position on a quick spring without overshoot.</summary>
    internal static readonly SpringSpec FillSpring = SpringSpec.FromVisual(0.3, 0);

    public static readonly StyledProperty<IBrush?> ActiveBrushProperty =
        AvaloniaProperty.Register<TocProgressList, IBrush?>(nameof(ActiveBrush));

    public static readonly StyledProperty<IBrush?> ReadBrushProperty =
        AvaloniaProperty.Register<TocProgressList, IBrush?>(nameof(ReadBrush));

    public static readonly StyledProperty<IBrush?> UpcomingBrushProperty =
        AvaloniaProperty.Register<TocProgressList, IBrush?>(nameof(UpcomingBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<TocProgressList, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<TocProgressList, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> DotRingBrushProperty =
        AvaloniaProperty.Register<TocProgressList, IBrush?>(nameof(DotRingBrush));

    public static readonly StyledProperty<double> RailThicknessProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(RailThickness), 2);

    public static readonly StyledProperty<double> RailGutterProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(RailGutter), 18);

    public static readonly StyledProperty<double> LevelIndentProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(LevelIndent), 12);

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(DotSize), 8);

    public static readonly StyledProperty<Thickness> RowPaddingProperty =
        AvaloniaProperty.Register<TocProgressList, Thickness>(nameof(RowPadding), new Thickness(0, 5));

    public static readonly StyledProperty<double> TextSizeProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(TextSize), 14);

    public static readonly StyledProperty<double> TextLineHeightProperty =
        AvaloniaProperty.Register<TocProgressList, double>(nameof(TextLineHeight), 20);

    public static readonly StyledProperty<FontWeight> ActiveWeightProperty =
        AvaloniaProperty.Register<TocProgressList, FontWeight>(nameof(ActiveWeight), FontWeight.Medium);

    private readonly List<TocProgressRow> _rows = [];
    private double[] _rowTops = [];
    private double[] _rowHeights = [];
    private MotionValue<double>? _fill;
    private MotionValue<Vector>? _segment;
    private Pen? _trackPen;
    private Pen? _fillPen;
    private Pen? _segmentPen;
    private Pen? _dotPen;
    private int _active = -1;
    private double _section;
    private bool _placed;

    static TocProgressList()
    {
        AffectsRender<TocProgressList>(TrackBrushProperty, FillBrushProperty, DotRingBrushProperty, RailThicknessProperty, DotSizeProperty);
        AffectsMeasure<TocProgressList>(RailGutterProperty, LevelIndentProperty, RowPaddingProperty, TextSizeProperty, TextLineHeightProperty);
        ClipToBoundsProperty.OverrideDefaultValue<TocProgressList>(false);
    }

    public TocProgressList()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    public TocProgress? Owner { get; set; }

    public IBrush? ActiveBrush { get => GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }

    public IBrush? ReadBrush { get => GetValue(ReadBrushProperty); set => SetValue(ReadBrushProperty, value); }

    public IBrush? UpcomingBrush { get => GetValue(UpcomingBrushProperty); set => SetValue(UpcomingBrushProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public IBrush? DotRingBrush { get => GetValue(DotRingBrushProperty); set => SetValue(DotRingBrushProperty, value); }

    public double RailThickness { get => GetValue(RailThicknessProperty); set => SetValue(RailThicknessProperty, value); }

    public double RailGutter { get => GetValue(RailGutterProperty); set => SetValue(RailGutterProperty, value); }

    public double LevelIndent { get => GetValue(LevelIndentProperty); set => SetValue(LevelIndentProperty, value); }

    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    public Thickness RowPadding { get => GetValue(RowPaddingProperty); set => SetValue(RowPaddingProperty, value); }

    public double TextSize { get => GetValue(TextSizeProperty); set => SetValue(TextSizeProperty, value); }

    public double TextLineHeight { get => GetValue(TextLineHeightProperty); set => SetValue(TextLineHeightProperty, value); }

    public FontWeight ActiveWeight { get => GetValue(ActiveWeightProperty); set => SetValue(ActiveWeightProperty, value); }

    public IReadOnlyList<TocProgressRow> Rows => _rows;

    /// <summary>Where the fill ends now (tests read it).</summary>
    public double FillEnd => _fill?.Value ?? 0;

    /// <summary>Where the fill is heading.</summary>
    public double FillTarget => _fill?.Target ?? 0;

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
        _rowTops = new double[_rows.Count];
        _rowHeights = new double[_rows.Count];
        _placed = false;
        UpdateTabStops();
        InvalidateMeasure();
    }

    /// <summary>Shows the reader at <paramref name="active"/>, <paramref name="section"/> of the way through it.</summary>
    public void SetPosition(int active, double section, bool animate)
    {
        var changed = active != _active;
        _active = active;
        _section = section;
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].State = i < active ? TocProgressRowState.Read : i == active ? TocProgressRowState.Active : TocProgressRowState.Upcoming;
        }
        if (changed)
        {
            UpdateTabStops();
        }
        Glide(animate);
    }

    private void Glide(bool animate)
    {
        if (!_placed || _rows.Count == 0)
        {
            return;
        }
        var fillTarget = TocProgressMath.FillEnd(_rowTops, _rowHeights, _active, _section);
        var segmentTarget = _active >= 0 && _active < _rows.Count
            ? new Vector(_rowTops[_active], _rowTops[_active] + _rowHeights[_active])
            : new Vector(0, 0);
        var fill = FillValue();
        var segment = SegmentValue();
        if (animate)
        {
            if (Math.Abs(fill.Target - fillTarget) > 0.01)
            {
                _ = fill.AnimateTo(fillTarget, MotionSpec.Spring(FillSpring));
            }
            if (segment.Target != segmentTarget)
            {
                _ = segment.AnimateTo(segmentTarget, MotionSpec.Spring(ArcSpring.Snappy));
            }
        }
        else
        {
            fill.Jump(fillTarget);
            segment.Jump(segmentTarget);
        }
        InvalidateVisual();
    }

    private MotionValue<double> FillValue()
    {
        if (_fill is null)
        {
            _fill = new MotionValue<double>(this, 0, MotionVectors.Double);
            _fill.Changed += _ => InvalidateVisual();
        }
        return _fill;
    }

    private MotionValue<Vector> SegmentValue()
    {
        if (_segment is null)
        {
            _segment = new MotionValue<Vector>(this, default, MotionVectors.Vector);
            _segment.Changed += _ => InvalidateVisual();
        }
        return _segment;
    }

    /// <summary>Only one row is in the tab order: the focused one, else the active one, else the first.</summary>
    private void UpdateTabStops(TocProgressRow? focused = null)
    {
        var stop = focused?.Index ?? (_active >= 0 ? _active : 0);
        for (var i = 0; i < _rows.Count; i++)
        {
            KeyboardNavigation.SetIsTabStop(_rows[i], i == stop);
        }
    }

    public void OnRowFocused(TocProgressRow row) => UpdateTabStops(row);

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
        if (change.Property == TrackBrushProperty || change.Property == FillBrushProperty || change.Property == RailThicknessProperty
            || change.Property == DotRingBrushProperty)
        {
            _trackPen = null;
            _fillPen = null;
            _segmentPen = null;
            _dotPen = null;
        }
        else if (change.Property == ActiveBrushProperty || change.Property == ReadBrushProperty || change.Property == UpcomingBrushProperty
                 || change.Property == ActiveWeightProperty || change.Property == TextSizeProperty || change.Property == TextLineHeightProperty
                 || change.Property == Avalonia.Controls.Documents.TextElement.FontFamilyProperty)
        {
            foreach (var row in _rows)
            {
                row.ClearLayouts();
                row.InvalidateMeasure();
                row.InvalidateVisual();
            }
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
        var moved = false;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var h = row.DesiredSize.Height;
            row.Arrange(new Rect(0, y, finalSize.Width, h));
            moved |= Math.Abs(_rowTops[i] - y) > 0.01 || Math.Abs(_rowHeights[i] - h) > 0.01;
            _rowTops[i] = y;
            _rowHeights[i] = h;
            y += h;
        }
        var first = !_placed;
        _placed = true;
        if (moved || first)
        {
            // Rows moved (first layout, wrap change): the rail lands at once, it does not glide from the old layout.
            Glide(animate: false);
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_rows.Count == 0)
        {
            return;
        }
        var x = RailThickness / 2 + Math.Max(0, (DotSize - RailThickness) / 2);
        var bottom = _rowTops[^1] + _rowHeights[^1];
        var top = _rowTops[0];
        _trackPen ??= TrackBrush is { } track ? new Pen(track, RailThickness, lineCap: PenLineCap.Round) : null;
        if (_trackPen is not null)
        {
            context.DrawLine(_trackPen, new Point(x, top), new Point(x, bottom));
        }
        var fill = Math.Clamp(_fill?.Value ?? 0, top, bottom);
        if (FillBrush is { } fillBrush)
        {
            _fillPen ??= new Pen(fillBrush, RailThickness, lineCap: PenLineCap.Round);
            _segmentPen ??= new Pen(fillBrush, RailThickness + 1, lineCap: PenLineCap.Round);
            if (fill > top + 0.5)
            {
                context.DrawLine(_fillPen, new Point(x, top), new Point(x, fill));
            }
            if (_segment is { } segment && segment.Value.Y - segment.Value.X > 1 && _active >= 0)
            {
                var inset = RowPadding.Top;
                context.DrawLine(_segmentPen, new Point(x, segment.Value.X + inset), new Point(x, Math.Max(segment.Value.X + inset, segment.Value.Y - inset)));
            }
            if (_active >= 0)
            {
                _dotPen ??= DotRingBrush is { } ring ? new Pen(ring, 2) : null;
                var r = DotSize / 2;
                context.DrawEllipse(fillBrush, _dotPen, new Point(x, Math.Max(fill, top + r)), r, r);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer(TocProgressList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}
