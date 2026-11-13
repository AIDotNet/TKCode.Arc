using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The drawn bar of a <see cref="SplitBar"/>: one tinted part per segment with a solid strip at its end and a small gap
/// between parts, inside a rounded track; labels inside the parts that fit them; and, under the bar, the lane with the
/// labels that do not fit and their leader lines. It is the bar's tab stop: the arrow keys, Home and End move the
/// highlight (wrapping), Enter or Space switches the highlighted part off, Escape clears it.
/// </summary>
internal sealed class SplitBarTrack : Control
{
    internal static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(TrackBrush));

    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(SurfaceBrush));

    internal static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(FocusBrush));

    internal static readonly StyledProperty<IBrush?> LeaderBrushProperty =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(LeaderBrush));

    internal static readonly StyledProperty<IBrush?> Series1Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Series1));

    internal static readonly StyledProperty<IBrush?> Series2Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Series2));

    internal static readonly StyledProperty<IBrush?> Series3Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Series3));

    internal static readonly StyledProperty<IBrush?> Series4Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Series4));

    internal static readonly StyledProperty<IBrush?> Neutral1Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Neutral1));

    internal static readonly StyledProperty<IBrush?> Neutral2Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Neutral2));

    internal static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(BarHeight), 44);

    internal static readonly StyledProperty<CornerRadius> BarRadiusProperty =
        AvaloniaProperty.Register<SplitBarTrack, CornerRadius>(nameof(BarRadius));

    internal static readonly StyledProperty<double> SegmentGapProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(SegmentGap), 2);

    internal static readonly StyledProperty<CornerRadius> SegmentRadiusProperty =
        AvaloniaProperty.Register<SplitBarTrack, CornerRadius>(nameof(SegmentRadius));

    internal static readonly StyledProperty<double> HighlightLiftProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(HighlightLift));

    internal static readonly StyledProperty<double> DimOpacityProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(DimOpacity), 0.5);

    internal static readonly StyledProperty<double> CapWidthProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(CapWidth), 3);

    internal static readonly StyledProperty<double> FillMixProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(FillMix), 0.42);

    internal static readonly StyledProperty<double> HighlightFillMixProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(HighlightFillMix), 0.62);

    internal static readonly StyledProperty<double> HighlightGrowProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(HighlightGrow), 0.045);

    internal static readonly StyledProperty<double> LabelInsetProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(LabelInset), 10);

    internal static readonly StyledProperty<double> LabelFitPaddingProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(LabelFitPadding), 24);

    internal static readonly StyledProperty<double> LabelNameGapProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(LabelNameGap), 5);

    internal static readonly StyledProperty<double> LaneNameGapProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(LaneNameGap), 4);

    internal static readonly StyledProperty<double> LaneHeightProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(LaneHeight), 40);

    internal static readonly StyledProperty<double> LaneLabelTopProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(LaneLabelTop), 18);

    internal static readonly StyledProperty<double> LeaderDepthProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(LeaderDepth), 15);

    internal static readonly StyledProperty<double> LaneLabelGapProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(LaneLabelGap), 16);

    internal static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(LabelFontSize), 12);

    internal static readonly StyledProperty<double> SectionGapProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(SectionGap), 12);

    /// <summary>The extra room the last part needs for its name, clear of the bar's rounded end.</summary>
    internal const double LastLabelExtraWidth = 6;

    /// <summary>Lane labels keep this far from the bar's right end.</summary>
    internal const double LaneEdge = 4;

    private readonly List<SplitBarSlot> _labelled = new();
    private SplitBar? _owner;
    private IPen? _focusPen;
    private IBrush? _focusPenBrush;
    private IPen? _leaderPen;
    private IBrush? _leaderPenBrush;
    private double _measuredLane = -1;
    private int _anchor = -1;
    private bool _keyboardHighlight;

    static SplitBarTrack()
    {
        FocusableProperty.OverrideDefaultValue<SplitBarTrack>(true);
        FocusAdornerProperty.OverrideDefaultValue<SplitBarTrack>(null);
        ClipToBoundsProperty.OverrideDefaultValue<SplitBarTrack>(false);
        AffectsRender<SplitBarTrack>(TrackBrushProperty, SurfaceBrushProperty, FocusBrushProperty, LeaderBrushProperty,
            Series1Property, Series2Property, Series3Property, Series4Property, Neutral1Property, Neutral2Property,
            BarRadiusProperty, SegmentGapProperty, SegmentRadiusProperty, DimOpacityProperty, CapWidthProperty,
            FillMixProperty, HighlightFillMixProperty);
        AffectsMeasure<SplitBarTrack>(BarHeightProperty, HighlightLiftProperty, SectionGapProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SplitBarTrack>(AutomationControlType.Group);
    }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }

    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    public IBrush? LeaderBrush { get => GetValue(LeaderBrushProperty); set => SetValue(LeaderBrushProperty, value); }

    public IBrush? Series1 { get => GetValue(Series1Property); set => SetValue(Series1Property, value); }

    public IBrush? Series2 { get => GetValue(Series2Property); set => SetValue(Series2Property, value); }

    public IBrush? Series3 { get => GetValue(Series3Property); set => SetValue(Series3Property, value); }

    public IBrush? Series4 { get => GetValue(Series4Property); set => SetValue(Series4Property, value); }

    /// <summary>The fifth and sixth default colors, neutrals for catch-all parts such as "Other".</summary>
    public IBrush? Neutral1 { get => GetValue(Neutral1Property); set => SetValue(Neutral1Property, value); }

    public IBrush? Neutral2 { get => GetValue(Neutral2Property); set => SetValue(Neutral2Property, value); }

    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    public CornerRadius BarRadius { get => GetValue(BarRadiusProperty); set => SetValue(BarRadiusProperty, value); }

    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }

    public CornerRadius SegmentRadius { get => GetValue(SegmentRadiusProperty); set => SetValue(SegmentRadiusProperty, value); }

    /// <summary>How far the highlighted part grows above and below the bar (0 by default: it widens instead).</summary>
    public double HighlightLift { get => GetValue(HighlightLiftProperty); set => SetValue(HighlightLiftProperty, value); }

    /// <summary>The opacity of the parts that are not highlighted.</summary>
    public double DimOpacity { get => GetValue(DimOpacityProperty); set => SetValue(DimOpacityProperty, value); }

    public double CapWidth { get => GetValue(CapWidthProperty); set => SetValue(CapWidthProperty, value); }

    public double FillMix { get => GetValue(FillMixProperty); set => SetValue(FillMixProperty, value); }

    public double HighlightFillMix { get => GetValue(HighlightFillMixProperty); set => SetValue(HighlightFillMixProperty, value); }

    public double HighlightGrow { get => GetValue(HighlightGrowProperty); set => SetValue(HighlightGrowProperty, value); }

    public double LabelInset { get => GetValue(LabelInsetProperty); set => SetValue(LabelInsetProperty, value); }

    public double LabelFitPadding { get => GetValue(LabelFitPaddingProperty); set => SetValue(LabelFitPaddingProperty, value); }

    public double LabelNameGap { get => GetValue(LabelNameGapProperty); set => SetValue(LabelNameGapProperty, value); }

    public double LaneNameGap { get => GetValue(LaneNameGapProperty); set => SetValue(LaneNameGapProperty, value); }

    public double LaneHeight { get => GetValue(LaneHeightProperty); set => SetValue(LaneHeightProperty, value); }

    public double LaneLabelTop { get => GetValue(LaneLabelTopProperty); set => SetValue(LaneLabelTopProperty, value); }

    public double LeaderDepth { get => GetValue(LeaderDepthProperty); set => SetValue(LeaderDepthProperty, value); }

    public double LaneLabelGap { get => GetValue(LaneLabelGapProperty); set => SetValue(LaneLabelGapProperty, value); }

    public double LabelFontSize { get => GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }

    public double SectionGap { get => GetValue(SectionGapProperty); set => SetValue(SectionGapProperty, value); }

    public double LastLabelExtra => LastLabelExtraWidth;

    public double LaneEdgeInset => LaneEdge;

    /// <summary>The width the parts share.</summary>
    public double BarWidth => Bounds.Width;

    public void Attach(SplitBar owner)
    {
        _owner = owner;
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void Detach()
    {
        foreach (var slot in _labelled)
        {
            RemoveLabels(slot);
        }
        _labelled.Clear();
        _owner = null;
    }

    /// <summary>Drops the labels of a part that left the bar.</summary>
    public void Release(SplitBarSlot slot)
    {
        RemoveLabels(slot);
        _labelled.Remove(slot);
    }

    private void RemoveLabels(SplitBarSlot slot)
    {
        if (slot.InsideLabel is { } inside)
        {
            VisualChildren.Remove(inside);
            LogicalChildren.Remove(inside);
            slot.InsideLabel = null;
        }
        if (slot.LaneLabel is { } lane)
        {
            VisualChildren.Remove(lane);
            LogicalChildren.Remove(lane);
            slot.LaneLabel = null;
        }
    }

    /// <summary>A frame moved something: lay the labels out again and draw.</summary>
    public void Refresh()
    {
        if (_owner is not null && Math.Abs(_owner.LaneHeight - _measuredLane) > 0.01)
        {
            InvalidateMeasure();
        }
        InvalidateArrange();
        InvalidateVisual();
    }

    /// <summary>The default color of a part at <paramref name="index"/>: series 1 to 4, then two neutrals, then again.</summary>
    public IBrush? SeriesBrush(int index) => (index % 6) switch
    {
        0 => Series1,
        1 => Series2,
        2 => Series3,
        3 => Series4,
        4 => Neutral1,
        _ => Neutral2
    };

    /// <summary>The solid color of a part (its strip, its readout swatch).</summary>
    public IBrush? CapBrush(SplitBarSlot slot) => slot.Segment.Brush ?? SeriesBrush(slot.ColorIndex);

    /// <summary>Gives every labelled part its texts.</summary>
    public void UpdateLabels(bool animate)
    {
        if (_owner is null)
        {
            return;
        }
        foreach (var slot in _owner.Slots)
        {
            if (slot.Mode is SplitBarLabelMode.Full or SplitBarLabelMode.Share || slot.Inside.Target > 0 || slot.InsideLabel is not null)
            {
                EnsureInside(slot).Set(slot.Segment.Label, slot.BaseShare, !animate || slot.InsideLabel!.IsFresh);
            }
            if (slot.Mode == SplitBarLabelMode.Lane || slot.LaneLabel is not null)
            {
                EnsureLane(slot).Set(slot.Segment.Label, slot.BaseShare, !animate || slot.LaneLabel!.IsFresh);
            }
        }
        InvalidateArrange();
    }

    private SplitBarInsideLabel EnsureInside(SplitBarSlot slot)
    {
        if (slot.InsideLabel is null)
        {
            slot.InsideLabel = new SplitBarInsideLabel(this);
            LogicalChildren.Add(slot.InsideLabel);
            VisualChildren.Add(slot.InsideLabel);
            if (!_labelled.Contains(slot))
            {
                _labelled.Add(slot);
            }
        }
        return slot.InsideLabel;
    }

    private SplitBarLaneLabel EnsureLane(SplitBarSlot slot)
    {
        if (slot.LaneLabel is null)
        {
            slot.LaneLabel = new SplitBarLaneLabel(this);
            LogicalChildren.Add(slot.LaneLabel);
            VisualChildren.Add(slot.LaneLabel);
            if (!_labelled.Contains(slot))
            {
                _labelled.Add(slot);
            }
        }
        return slot.LaneLabel;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in VisualChildren)
        {
            if (child is Layoutable layoutable)
            {
                layoutable.Measure(Size.Infinity);
            }
        }
        var lane = _owner?.LaneHeight ?? 0;
        _measuredLane = lane;
        return new Size(0, BarHeight + 2 * HighlightLift + SectionGap + Math.Max(0, lane - SectionGap));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_owner is null)
        {
            return finalSize;
        }
        var bar = BarRect(finalSize.Width);
        var wall = _owner.Wall;
        var slots = _owner.Slots;
        var dimOpacity = DimOpacity;
        var inset = LabelInset;
        var nameGap = LabelNameGap;
        var lineHeight = LabelFontSize * 1.4;
        Span<double> starts = slots.Count <= 64 ? stackalloc double[slots.Count] : new double[slots.Count];
        Span<double> widths = slots.Count <= 64 ? stackalloc double[slots.Count] : new double[slots.Count];
        Layout(bar, starts, widths);
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var visible = Visible(i, slots.Count, starts[i], widths[i], bar);
            var dim = 1 - Math.Clamp(slot.Dim.Value(wall), 0, 1) * (1 - dimOpacity);
            if (slot.InsideLabel is { } inside)
            {
                var opacity = Math.Clamp(slot.Inside.Value(wall), 0, 1);
                inside.Opacity = opacity * dim;
                inside.IsVisible = opacity > 0.001 && visible.Width > 0.5;
                inside.Layout(Math.Clamp(slot.NameOpen.Value(wall), 0, 1), nameGap, starts[i] + inset - visible.X, lineHeight);
                inside.Arrange(visible.Width > 0 ? visible : new Rect(visible.X, visible.Y, 0, visible.Height));
            }
            if (slot.LaneLabel is { } lane)
            {
                var opacity = Math.Clamp(slot.Outside.Value(wall), 0, 1);
                lane.Opacity = opacity;
                lane.IsVisible = opacity > 0.001;
                var size = lane.DesiredSize;
                var y = bar.Bottom + HighlightLift + LaneLabelTop - 4 * (1 - opacity);
                lane.Arrange(new Rect(slot.OutsideX.Value(wall), y, size.Width, size.Height));
            }
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        // The whole bar takes pointer input, gaps included.
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, width, BarHeight + 2 * HighlightLift));
        var bar = BarRect(width);
        if (bar.Width <= 0)
        {
            return;
        }
        var radius = Math.Min(BarRadius.TopLeft, bar.Height / 2);
        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, bar, radius, radius);
        }
        if (_owner is null)
        {
            return;
        }
        var wall = _owner.Wall;
        var slots = _owner.Slots;
        Span<double> starts = slots.Count <= 64 ? stackalloc double[slots.Count] : new double[slots.Count];
        Span<double> widths = slots.Count <= 64 ? stackalloc double[slots.Count] : new double[slots.Count];
        Layout(bar, starts, widths);
        var segmentRadius = SegmentRadius.TopLeft;
        var dimOpacity = DimOpacity;
        var highlighted = _owner.HighlightedIndex;
        Rect? lifted = null;
        SplitBarSlot? liftedSlot = null;
        using (context.PushClip(new RoundedRect(bar, radius)))
        {
            var visibleIndex = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var isHighlighted = !slot.IsLeaving && visibleIndex == highlighted;
                if (!slot.IsLeaving)
                {
                    visibleIndex++;
                }
                var rect = Visible(i, slots.Count, starts[i], widths[i], bar);
                if (rect.Width <= 0.01)
                {
                    continue;
                }
                if (isHighlighted && HighlightLift > 0)
                {
                    lifted = rect;
                    liftedSlot = slot;
                    continue;
                }
                DrawPart(context, slot, rect, widths[i], segmentRadius, dimOpacity, wall);
            }
        }
        if (lifted is { } up && liftedSlot is not null)
        {
            var rect = up.Inflate(new Thickness(0, HighlightLift));
            DrawPart(context, liftedSlot, rect, rect.Width, segmentRadius + HighlightLift, dimOpacity, wall);
        }
        DrawLeaders(context, bar, starts, widths, wall);
        if (IsFocused && Classes.Contains(":focus-visible") && FocusPen() is { } pen)
        {
            // Arc's outline: 2px, 2px off the bar.
            var ring = bar.Inflate(3);
            context.DrawRectangle(null, pen, ring, radius + 3, radius + 3);
        }
    }

    private void DrawPart(DrawingContext context, SplitBarSlot slot, Rect rect, double fullWidth, double segmentRadius,
                          double dimOpacity, double wall)
    {
        var color = CapBrush(slot);
        if (color is null)
        {
            return;
        }
        var dim = 1 - Math.Clamp(slot.Dim.Value(wall), 0, 1) * (1 - dimOpacity);
        var r = Math.Min(segmentRadius, Math.Min(rect.Width, rect.Height) / 2);
        using (context.PushOpacity(dim))
        {
            Fills(slot, color, out var fill, out var highlight);
            context.DrawRectangle(fill, null, rect, r, r);
            var lit = Math.Clamp(slot.Fill.Value(wall), 0, 1);
            if (lit > 0.001)
            {
                using (context.PushOpacity(lit))
                {
                    context.DrawRectangle(highlight, null, rect, r, r);
                }
            }
            // The solid strip fades on slivers so a 1% part does not read as a line of color.
            var cap = Math.Clamp((fullWidth - 4) / 6, 0, 1);
            if (cap > 0.001)
            {
                using (context.PushClip(new RoundedRect(rect, r)))
                using (context.PushOpacity(cap))
                {
                    context.FillRectangle(color, new Rect(rect.Right - CapWidth, rect.Y, CapWidth, rect.Height));
                }
            }
        }
    }

    private void Fills(SplitBarSlot slot, IBrush color, out IBrush fill, out IBrush highlight)
    {
        var surface = SurfaceBrush;
        if (slot.FillCache is null || !ReferenceEquals(slot.FillColor, color) || !ReferenceEquals(slot.FillSurface, surface) ||
            slot.FillMix != FillMix || slot.HighlightMix != HighlightFillMix)
        {
            slot.FillColor = color;
            slot.FillSurface = surface;
            slot.FillMix = FillMix;
            slot.HighlightMix = HighlightFillMix;
            slot.FillCache = SplitBarColors.Mix(color, surface, FillMix);
            slot.HighlightCache = SplitBarColors.Mix(color, surface, HighlightFillMix);
        }
        fill = slot.FillCache;
        highlight = slot.HighlightCache!;
    }

    private void DrawLeaders(DrawingContext context, Rect bar, ReadOnlySpan<double> starts, ReadOnlySpan<double> widths, double wall)
    {
        if (_owner is null || LeaderPen() is not { } pen)
        {
            return;
        }
        var slots = _owner.Slots;
        var top = bar.Bottom + HighlightLift;
        var depth = LeaderDepth;
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.LaneLabel is not { } label)
            {
                continue;
            }
            var opacity = Math.Clamp(slot.Outside.Value(wall), 0, 1);
            if (opacity <= 0.001)
            {
                continue;
            }
            var x0 = starts[i] + widths[i] / 2;
            var x1 = slot.OutsideX.Value(wall) + label.DesiredSize.Width / 2;
            var geometry = slot.Leader(x0, x1, top, depth);
            using (context.PushOpacity(opacity))
            {
                context.DrawGeometry(null, pen, geometry);
            }
        }
    }

    private IPen? LeaderPen()
    {
        if (LeaderBrush is not { } brush)
        {
            return null;
        }
        if (!ReferenceEquals(brush, _leaderPenBrush))
        {
            _leaderPenBrush = brush;
            _leaderPen = new ImmutablePen(brush.ToImmutable(), 1);
        }
        return _leaderPen;
    }

    private IPen? FocusPen()
    {
        if (FocusBrush is not { } brush)
        {
            return null;
        }
        if (!ReferenceEquals(brush, _focusPenBrush))
        {
            _focusPenBrush = brush;
            _focusPen = new ImmutablePen(brush.ToImmutable(), 2);
        }
        return _focusPen;
    }

    /// <summary>The bar itself (without the room for a lifted part and without the lane).</summary>
    internal Rect BarRect(double width) => new(0, HighlightLift, Math.Max(0, width), BarHeight);

    /// <summary>Where each part starts and how wide it is now, left to right (px).</summary>
    private void Layout(Rect bar, Span<double> starts, Span<double> widths)
    {
        if (_owner is null)
        {
            return;
        }
        var slots = _owner.Slots;
        var wall = _owner.Wall;
        double total = 0;
        for (var i = 0; i < slots.Count; i++)
        {
            total += Math.Max(0, slots[i].Share.Value(wall));
        }
        // A spring that overshoots a little must not push the last part out of the bar.
        var scale = total > 1 ? bar.Width / total : bar.Width;
        var x = bar.X;
        for (var i = 0; i < slots.Count; i++)
        {
            var width = Math.Max(0, slots[i].Share.Value(wall)) * scale;
            starts[i] = x;
            widths[i] = width;
            x += width;
        }
    }

    /// <summary>The visible part of a part: half a gap off each inner edge (the first and last keep the bar's ends).</summary>
    private Rect Visible(int index, int count, double start, double width, Rect bar)
    {
        var half = SegmentGap / 2;
        var left = start + (index == 0 ? 0 : half);
        var right = start + width - (index == count - 1 ? 0 : half);
        return new Rect(left, bar.Y, Math.Max(0, right - left), bar.Height);
    }

    /// <summary>The drawn rectangle of each visible part, in order (pointer hit tests and tests).</summary>
    internal List<Rect> PartRects()
    {
        var rects = new List<Rect>();
        if (_owner is null)
        {
            return rects;
        }
        var bar = BarRect(Bounds.Width);
        var slots = _owner.Slots;
        var starts = new double[slots.Count];
        var widths = new double[slots.Count];
        Layout(bar, starts, widths);
        for (var i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsLeaving)
            {
                rects.Add(Visible(i, slots.Count, starts[i], widths[i], bar));
            }
        }
        return rects;
    }

    /// <summary>The visible part (that is on) under <paramref name="x"/>; -1 over the empty rest.</summary>
    internal int HitTest(double x)
    {
        if (_owner is null)
        {
            return -1;
        }
        var rects = PartRects();
        var half = SegmentGap / 2;
        for (var i = 0; i < rects.Count; i++)
        {
            if (rects[i].Width > 0 && x >= rects[i].Left - half && x <= rects[i].Right + half &&
                _owner.SlotAt(i) is { Segment.IsHidden: false })
            {
                return i;
            }
        }
        return -1;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == Series1Property || change.Property == Series2Property || change.Property == Series3Property ||
            change.Property == Series4Property || change.Property == Neutral1Property || change.Property == Neutral2Property)
        {
            // A theme or accent change: the legend swatches and the readout take the new colors.
            _owner?.OnPaletteChanged();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.01)
        {
            _owner?.OnBarWidthChanged(e.PreviousSize.Width <= 0);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_owner is not null && _keyboardHighlight && !IsPointerOver)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, -1);
        }
        _keyboardHighlight = false;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_owner is null || e.Handled)
        {
            return;
        }
        var index = _owner.HighlightedIndex;
        int? next = e.Key switch
        {
            Key.Right or Key.Down => Step(index < 0 ? _anchor : index, 1, index < 0 && _anchor < 0),
            Key.Left or Key.Up => Step(index < 0 ? _anchor : index, -1, index < 0 && _anchor < 0),
            Key.Home => Step(-1, 1, true),
            Key.End => Step(-1, -1, true),
            _ => null
        };
        if (next is { } value)
        {
            if (value >= 0)
            {
                _keyboardHighlight = true;
                _anchor = -1;
                _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, value);
                _owner.Announce(_owner.Describe(value));
            }
            e.Handled = true;
        }
        else if ((e.Key == Key.Enter || e.Key == Key.Space) && index >= 0)
        {
            if (!_owner.Invoke(index) && _owner.Toggle(index))
            {
                _anchor = index;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && index >= 0)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, -1);
            e.Handled = true;
        }
    }

    /// <summary>The next part that is on from <paramref name="from"/> in <paramref name="direction"/>, wrapping round.</summary>
    private int Step(int from, int direction, bool fromEnd)
    {
        if (_owner is null)
        {
            return -1;
        }
        var count = _owner.Current.Count;
        if (count == 0)
        {
            return -1;
        }
        var at = fromEnd ? (direction > 0 ? -1 : count) : from;
        for (var n = 0; n < count; n++)
        {
            at = ((at + direction) % count + count) % count;
            if (_owner.SlotAt(at) is { Segment.IsHidden: false })
            {
                return at;
            }
        }
        return -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_owner is null || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var point = e.GetPosition(this);
        var over = point.Y <= BarHeight + 2 * HighlightLift ? HitTest(point.X) : -1;
        if (over >= 0 || !_keyboardHighlight)
        {
            _keyboardHighlight = false;
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, over);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_owner is not null && e.Pointer.Type != PointerType.Touch && !_keyboardHighlight)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, -1);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_owner is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var point = e.GetPosition(this);
        if (point.Y > BarHeight + 2 * HighlightLift)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        var index = HitTest(point.X);
        if (index >= 0)
        {
            _keyboardHighlight = false;
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, index);
            _owner.Invoke(index);
            e.Handled = true;
        }
        else if (e.Pointer.Type == PointerType.Touch)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, -1);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SplitBarTrackAutomationPeer(this);

    private sealed class SplitBarTrackAutomationPeer(SplitBarTrack owner) : ControlAutomationPeer(owner)
    {
        protected override string GetLocalizedControlTypeCore() => "split bar";

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
