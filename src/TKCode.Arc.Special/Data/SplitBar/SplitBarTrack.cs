using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The drawn bar of a <see cref="SplitBar"/>: one rounded piece per part with a small gap between them, over the empty
/// track. It is the bar's single tab stop: Left / Right / Home / End move the highlight, Enter or Space invokes the
/// highlighted part, Escape clears it. Its accessible name is the whole split, or the highlighted part.
/// </summary>
internal sealed class SplitBarTrack : Control
{
    internal static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(TrackBrush));

    internal static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(FocusBrush));

    internal static readonly StyledProperty<IBrush?> Series1Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Series1));

    internal static readonly StyledProperty<IBrush?> Series2Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Series2));

    internal static readonly StyledProperty<IBrush?> Series3Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Series3));

    internal static readonly StyledProperty<IBrush?> Series4Property =
        AvaloniaProperty.Register<SplitBarTrack, IBrush?>(nameof(Series4));

    internal static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(BarHeight), 12);

    internal static readonly StyledProperty<double> SegmentGapProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(SegmentGap), 3);

    internal static readonly StyledProperty<CornerRadius> SegmentRadiusProperty =
        AvaloniaProperty.Register<SplitBarTrack, CornerRadius>(nameof(SegmentRadius), new CornerRadius(6));

    internal static readonly StyledProperty<double> HighlightLiftProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(HighlightLift), 2);

    internal static readonly StyledProperty<double> DimOpacityProperty =
        AvaloniaProperty.Register<SplitBarTrack, double>(nameof(DimOpacity), 0.35);

    private readonly List<Rect> _layout = new();
    private SplitBar? _owner;
    private IPen? _focusPen;
    private IBrush? _focusPenBrush;

    static SplitBarTrack()
    {
        FocusableProperty.OverrideDefaultValue<SplitBarTrack>(true);
        FocusAdornerProperty.OverrideDefaultValue<SplitBarTrack>(null);
        AffectsRender<SplitBarTrack>(TrackBrushProperty, FocusBrushProperty, Series1Property, Series2Property, Series3Property,
            Series4Property, SegmentGapProperty, SegmentRadiusProperty, DimOpacityProperty);
        AffectsMeasure<SplitBarTrack>(BarHeightProperty, HighlightLiftProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SplitBarTrack>(AutomationControlType.Image);
    }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    public IBrush? Series1 { get => GetValue(Series1Property); set => SetValue(Series1Property, value); }

    public IBrush? Series2 { get => GetValue(Series2Property); set => SetValue(Series2Property, value); }

    public IBrush? Series3 { get => GetValue(Series3Property); set => SetValue(Series3Property, value); }

    public IBrush? Series4 { get => GetValue(Series4Property); set => SetValue(Series4Property, value); }

    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }

    public CornerRadius SegmentRadius { get => GetValue(SegmentRadiusProperty); set => SetValue(SegmentRadiusProperty, value); }

    /// <summary>How far the highlighted part grows above and below the bar.</summary>
    public double HighlightLift { get => GetValue(HighlightLiftProperty); set => SetValue(HighlightLiftProperty, value); }

    /// <summary>The opacity of the parts that are not highlighted.</summary>
    public double DimOpacity { get => GetValue(DimOpacityProperty); set => SetValue(DimOpacityProperty, value); }

    /// <summary>The color of the part at <paramref name="index"/> when its segment has none: series 1 to 4, then again, lighter.</summary>
    public IBrush? SeriesBrush(int index) => (index % 4) switch
    {
        0 => Series1,
        1 => Series2,
        2 => Series3,
        _ => Series4
    };

    /// <summary>The opacity the default color takes at <paramref name="index"/> (later rounds are lighter).</summary>
    public static double SeriesOpacity(int index) => (index / 4) switch
    {
        0 => 1,
        1 => 0.62,
        _ => 0.38
    };

    public void Attach(SplitBar owner)
    {
        _owner = owner;
        InvalidateVisual();
    }

    /// <summary>The drawn rectangle of each visible part, in order (pointer hit tests and tests).</summary>
    internal List<Rect> PartRects()
    {
        var rects = new List<Rect>();
        if (_owner is null)
        {
            return rects;
        }
        Layout();
        var slots = _owner.Slots;
        for (var i = 0; i < slots.Count && i < _layout.Count; i++)
        {
            if (!slots[i].IsLeaving)
            {
                rects.Add(_layout[i]);
            }
        }
        return rects;
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(0, BarHeight + 2 * HighlightLift);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole bar takes pointer input, gaps included.
        context.FillRectangle(Brushes.Transparent, bounds);
        var bar = BarRect();
        if (bar.Width <= 0)
        {
            return;
        }
        var radius = Math.Min(SegmentRadius.TopLeft, bar.Height / 2);
        if (TrackBrush is { } track)
        {
            context.DrawRectangle(track, null, bar, radius, radius);
        }
        if (_owner is not null)
        {
            var wall = _owner.Wall;
            var dimOpacity = DimOpacity;
            var highlighted = _owner.HighlightedIndex;
            Rect? focusRect = null;
            var visible = 0;
            Layout();
            var slots = _owner.Slots;
            for (var i = 0; i < slots.Count && i < _layout.Count; i++)
            {
                var slot = slots[i];
                var rect = _layout[i];
                var isHighlighted = !slot.IsLeaving && visible == highlighted;
                if (!slot.IsLeaving)
                {
                    visible++;
                }
                var brush = slot.Segment.Brush ?? SeriesBrush(slot.ColorIndex);
                if (brush is null || rect.Width <= 0.25)
                {
                    continue;
                }
                var dim = Math.Clamp(slot.Dim.Value(wall), 0, 1);
                var opacity = (1 - dim * (1 - dimOpacity)) * (slot.Segment.Brush is null ? SeriesOpacity(slot.ColorIndex) : 1);
                var drawn = isHighlighted ? rect.Inflate(new Thickness(0, HighlightLift)) : rect;
                var r = Math.Min(radius + (isHighlighted ? HighlightLift : 0), Math.Min(drawn.Width, drawn.Height) / 2);
                using (context.PushOpacity(opacity))
                {
                    context.DrawRectangle(brush, null, drawn, r, r);
                }
                if (isHighlighted)
                {
                    focusRect = drawn;
                }
            }
            if (IsFocused && Classes.Contains(":focus-visible") && FocusPen() is { } pen)
            {
                var ring = (focusRect ?? bar).Inflate(3);
                var rr = Math.Min(radius + 3, ring.Height / 2);
                context.DrawRectangle(null, pen, ring, rr, rr);
            }
        }
    }

    /// <summary>The bar itself (without the highlight's room above and below).</summary>
    internal Rect BarRect() => new(0, HighlightLift, Math.Max(0, Bounds.Width), BarHeight);

    /// <summary>Lays the parts out left to right with their current widths into a reused list; gaps only between parts that show.</summary>
    private void Layout()
    {
        _layout.Clear();
        if (_owner is null)
        {
            return;
        }
        var bar = BarRect();
        var slots = _owner.Slots;
        var wall = _owner.Wall;
        var gap = SegmentGap;
        // Gaps take room from the parts in proportion to how present each part is (a part growing in opens its gap).
        double presence = 0;
        double shares = 0;
        for (var i = 0; i < slots.Count; i++)
        {
            var share = Math.Max(0, slots[i].Share.Value(wall));
            shares += share;
            presence += Math.Min(1, share * 40);
        }
        var gaps = Math.Max(0, presence - 1) * gap;
        if (_owner.RemainderShare > 0.0005)
        {
            gaps += gap;
        }
        var scale = Math.Max(0, bar.Width - gaps);
        var overflow = shares > 1 ? 1 / shares : 1;
        var x = bar.X;
        for (var i = 0; i < slots.Count; i++)
        {
            var share = Math.Max(0, slots[i].Share.Value(wall)) * overflow;
            var width = share * scale;
            var rect = new Rect(x, bar.Y, Math.Min(width, Math.Max(0, bar.Right - x)), bar.Height);
            _layout.Add(rect);
            x += width + Math.Min(1, share * 40) * gap;
        }
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

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_owner is { HighlightedIndex: < 0 } owner && e.NavigationMethod == NavigationMethod.Tab && owner.Current.Count > 0)
        {
            owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, 0);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_owner is not null && !IsPointerOver)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, -1);
        }
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_owner is null || e.Handled)
        {
            return;
        }
        var count = _owner.Current.Count;
        if (count == 0)
        {
            return;
        }
        var index = _owner.HighlightedIndex;
        int? next = e.Key switch
        {
            Key.Right or Key.Down => Math.Min(count - 1, index + 1),
            Key.Left or Key.Up => Math.Max(0, index < 0 ? 0 : index - 1),
            Key.Home => 0,
            Key.End => count - 1,
            _ => null
        };
        if (next is { } value)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, value);
            e.Handled = true;
        }
        else if ((e.Key == Key.Enter || e.Key == Key.Space) && index >= 0)
        {
            _owner.Invoke(index);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && index >= 0)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, -1);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_owner is null || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, HitTest(e.GetPosition(this).X));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_owner is not null && !IsFocused)
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
        var index = HitTest(e.GetPosition(this).X);
        if (index >= 0)
        {
            _owner.SetCurrentValue(SplitBar.HighlightedIndexProperty, index);
            _owner.Invoke(index);
            e.Handled = true;
        }
    }

    /// <summary>The visible part under <paramref name="x"/>, or the nearest one; -1 over the empty rest.</summary>
    internal int HitTest(double x)
    {
        var rects = PartRects();
        for (var i = 0; i < rects.Count; i++)
        {
            var half = SegmentGap / 2;
            if (x >= rects[i].Left - half && x <= rects[i].Right + half)
            {
                return i;
            }
        }
        return -1;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}
