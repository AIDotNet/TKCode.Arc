using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The seat slider of <see cref="PricingCalculator"/> (Arc pricing-calculator, design reference, notes 3.1 and 4): its value
/// is the index of a seat stop, stops evenly spaced along the track, so dragging, clicking the track and the keys all snap
/// to a stop. A 6px round-ended track filled up to the thumb (no animation: the fill follows the value at once), a 22px
/// thumb (surface, 1px border, resting shadow) whose centre runs from 11px in at each end. Keys: ←/↓ one stop down,
/// →/↑ one up, PageDown/PageUp two (10% of the range), Home/End the first and last stop. The keyboard focus ring is a
/// 2px outline 2px outside the slider.
/// </summary>
internal sealed class PricingCalculatorSeatSlider : RangeBase
{
    /// <summary>Defines the <see cref="FillBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<PricingCalculatorSeatSlider, IBrush?>(nameof(FillBrush));

    /// <summary>Defines the <see cref="TrackBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<PricingCalculatorSeatSlider, IBrush?>(nameof(TrackBrush));

    /// <summary>Defines the <see cref="ThumbBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ThumbBrushProperty =
        AvaloniaProperty.Register<PricingCalculatorSeatSlider, IBrush?>(nameof(ThumbBrush));

    /// <summary>Defines the <see cref="ThumbBorderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ThumbBorderBrushProperty =
        AvaloniaProperty.Register<PricingCalculatorSeatSlider, IBrush?>(nameof(ThumbBorderBrush));

    /// <summary>Defines the <see cref="ThumbShadow"/> property.</summary>
    public static readonly StyledProperty<BoxShadows> ThumbShadowProperty =
        AvaloniaProperty.Register<PricingCalculatorSeatSlider, BoxShadows>(nameof(ThumbShadow));

    /// <summary>Defines the <see cref="FocusBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<PricingCalculatorSeatSlider, IBrush?>(nameof(FocusBrush));

    /// <summary>Defines the <see cref="TrackThickness"/> property.</summary>
    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<PricingCalculatorSeatSlider, double>(nameof(TrackThickness), 6);

    /// <summary>Defines the <see cref="ThumbSize"/> property.</summary>
    public static readonly StyledProperty<double> ThumbSizeProperty =
        AvaloniaProperty.Register<PricingCalculatorSeatSlider, double>(nameof(ThumbSize), 22);

    private bool _dragging;
    private Pen? _borderPen;
    private Pen? _focusPen;

    static PricingCalculatorSeatSlider()
    {
        FocusableProperty.OverrideDefaultValue<PricingCalculatorSeatSlider>(true);
        SmallChangeProperty.OverrideDefaultValue<PricingCalculatorSeatSlider>(1);
        LargeChangeProperty.OverrideDefaultValue<PricingCalculatorSeatSlider>(2);
        AffectsRender<PricingCalculatorSeatSlider>(ValueProperty, MaximumProperty, FillBrushProperty, TrackBrushProperty, ThumbBrushProperty,
            ThumbBorderBrushProperty, ThumbShadowProperty, FocusBrushProperty, TrackThicknessProperty, ThumbSizeProperty);
        AffectsMeasure<PricingCalculatorSeatSlider>(ThumbSizeProperty);
    }

    public PricingCalculatorSeatSlider()
    {
        Minimum = 0;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>The filled part of the track.</summary>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <summary>The rest of the track.</summary>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    /// <summary>The thumb's fill.</summary>
    public IBrush? ThumbBrush { get => GetValue(ThumbBrushProperty); set => SetValue(ThumbBrushProperty, value); }

    /// <summary>The thumb's 1px outline.</summary>
    public IBrush? ThumbBorderBrush { get => GetValue(ThumbBorderBrushProperty); set => SetValue(ThumbBorderBrushProperty, value); }

    /// <summary>The thumb's shadow.</summary>
    public BoxShadows ThumbShadow { get => GetValue(ThumbShadowProperty); set => SetValue(ThumbShadowProperty, value); }

    /// <summary>The keyboard focus ring.</summary>
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    /// <summary>Thickness of the track.</summary>
    public double TrackThickness { get => GetValue(TrackThicknessProperty); set => SetValue(TrackThicknessProperty, value); }

    /// <summary>Diameter of the thumb (and the slider's height).</summary>
    public double ThumbSize { get => GetValue(ThumbSizeProperty); set => SetValue(ThumbSizeProperty, value); }

    /// <summary>The stop index (an integer between 0 and <see cref="RangeBase.Maximum"/>).</summary>
    public int Index => (int)Math.Round(Value);

    /// <summary>The x of the thumb's centre for stop <paramref name="index"/> on a slider <paramref name="width"/> wide.</summary>
    public static double ThumbCenter(double width, double thumb, int index, double maximum)
    {
        var inner = Math.Max(0, width - thumb);
        return thumb / 2 + (maximum <= 0 ? 0 : inner * index / maximum);
    }

    /// <summary>The stop nearest the x <paramref name="x"/> on a slider <paramref name="width"/> wide.</summary>
    public static int IndexAt(double width, double thumb, double x, double maximum)
    {
        var inner = Math.Max(1, width - thumb);
        var ratio = Math.Clamp((x - thumb / 2) / inner, 0, 1);
        return (int)Math.Round(ratio * Math.Max(0, maximum), MidpointRounding.AwayFromZero);
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, ThumbSize);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFocusedProperty)
        {
            InvalidateVisual();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SeatPeer(this);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !IsEnabled)
        {
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
        Focus(NavigationMethod.Pointer);
        SetFromPointer(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            SetFromPointer(e.GetPosition(this).X);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private void SetFromPointer(double x)
        => SetCurrentValue(ValueProperty, (double)IndexAt(Bounds.Width, ThumbSize, x, Maximum));

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled)
        {
            return;
        }
        double? next = e.Key switch
        {
            Key.Left or Key.Down => Index - SmallChange,
            Key.Right or Key.Up => Index + SmallChange,
            Key.PageDown => Index - LargeChange,
            Key.PageUp => Index + LargeChange,
            Key.Home => Minimum,
            Key.End => Maximum,
            _ => null
        };
        if (next is { } value)
        {
            SetCurrentValue(ValueProperty, Math.Clamp(Math.Round(value), Minimum, Maximum));
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        // The whole box takes the pointer, not only the painted track.
        context.FillRectangle(Brushes.Transparent, bounds);
        var thumb = ThumbSize;
        var cy = bounds.Height / 2;
        var track = TrackThickness;
        var center = ThumbCenter(bounds.Width, thumb, Index, Maximum);
        var trackRect = new Rect(0, cy - track / 2, bounds.Width, track);
        context.DrawRectangle(TrackBrush, null, new RoundedRect(trackRect, track / 2));
        if (center > 0)
        {
            context.DrawRectangle(FillBrush, null, new RoundedRect(new Rect(0, trackRect.Y, center, track), track / 2));
        }
        var border = ThumbBorderBrush is { } b ? _borderPen is { } p && ReferenceEquals(p.Brush, b) ? p : _borderPen = new Pen(b, 1) : null;
        var thumbRect = new Rect(center - thumb / 2, cy - thumb / 2, thumb, thumb);
        context.DrawRectangle(ThumbBrush, border, new RoundedRect(thumbRect.Deflate(0.5), thumb / 2), ThumbShadow);
        if (IsFocused && Classes.Contains(":focus-visible") && FocusBrush is { } focus)
        {
            var pen = _focusPen is { } fp && ReferenceEquals(fp.Brush, focus) ? fp : _focusPen = new Pen(focus, 2);
            context.DrawRectangle(null, pen, new RoundedRect(bounds.Inflate(3), 4));
        }
    }

    private sealed class SeatPeer(PricingCalculatorSeatSlider owner) : RangeBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string GetClassNameCore() => "Slider";
    }
}

/// <summary>
/// One plan mark under the seat slider: 12px text, muted, in the foreground while its plan is recommended (color 240 ms on
/// the standard curve, in the theme). Every mark but the first has a 1×11 tick 7px before its text.
/// </summary>
internal sealed class PricingCalculatorMarkLabel : TextBlock
{
    /// <summary>Defines the <see cref="TickBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<PricingCalculatorMarkLabel, IBrush?>(nameof(TickBrush));

    private bool _hasTick;

    static PricingCalculatorMarkLabel()
    {
        AffectsRender<PricingCalculatorMarkLabel>(TickBrushProperty);
    }

    protected override Type StyleKeyOverride => typeof(PricingCalculatorMarkLabel);

    /// <summary>The stop index the mark sits at.</summary>
    public int Index { get; set; }

    /// <summary>The tick color.</summary>
    public IBrush? TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }

    /// <summary>True: the mark has a tick before its text (every mark but the first).</summary>
    public bool HasTick
    {
        get => _hasTick;
        set
        {
            _hasTick = value;
            Padding = new Thickness(value ? 7 : 0, 0, 0, 0);
            InvalidateVisual();
        }
    }

    /// <summary>True while the mark's plan is recommended.</summary>
    public bool IsOn
    {
        set => PseudoClasses.Set(":on", value);
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        base.RenderTextLayout(context, origin);
        if (_hasTick && TickBrush is { } tick)
        {
            context.FillRectangle(tick, new Rect(0, 3, 1, 11));
        }
    }
}

/// <summary>
/// Lays the plan marks under the slider: a mark at stop i starts at i/max of the thumb's travel (the first one at the
/// track's start), and is pulled back inside when it would run past the right edge.
/// </summary>
internal sealed class PricingCalculatorSeatMarks : Panel
{
    /// <summary>The last stop index.</summary>
    public int Maximum { get; set; } = 1;

    /// <summary>The thumb's diameter (the travel is the width less one thumb).</summary>
    public double ThumbSize { get; set; } = 22;

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var travel = Math.Max(0, finalSize.Width - ThumbSize);
        foreach (var child in Children)
        {
            var index = child is PricingCalculatorMarkLabel mark ? mark.Index : 0;
            var x = Maximum <= 0 ? 0 : travel * index / Maximum;
            var width = child.DesiredSize.Width;
            x = Math.Max(0, Math.Min(x, finalSize.Width - width));
            child.Arrange(new Rect(x, 0, width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}
