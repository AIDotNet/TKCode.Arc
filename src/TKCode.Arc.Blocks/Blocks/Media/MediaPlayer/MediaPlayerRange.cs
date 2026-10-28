using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The player's seek and volume slider (Arc Media player, design reference): a 4px rounded track filled up to a 12px dot,
/// with a gap ring of the surface color around the dot so the fill and the rest of the track stop short of it. The dot and
/// its ring grow by <see cref="DragScale"/> while it is pressed. It behaves like a native range input: a press anywhere
/// jumps to that spot and dragging follows the pointer; the arrows move one <see cref="RangeBase.SmallChange"/>, Page Up
/// and Page Down a tenth of the range (snapped to the step), Home and End to the ends. Self-drawn because the ring has to
/// cut the track exactly where the dot is on every frame of a drag.
/// </summary>
internal sealed class MediaPlayerRange : RangeBase
{
    /// <summary>The filled part of the track.</summary>
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<MediaPlayerRange, IBrush?>(nameof(FillBrush));

    /// <summary>The rest of the track.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<MediaPlayerRange, IBrush?>(nameof(TrackBrush));

    /// <summary>The dot.</summary>
    public static readonly StyledProperty<IBrush?> ThumbBrushProperty =
        AvaloniaProperty.Register<MediaPlayerRange, IBrush?>(nameof(ThumbBrush));

    /// <summary>The gap ring around the dot: the color of the surface behind the slider.</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<MediaPlayerRange, IBrush?>(nameof(RingBrush));

    /// <summary>The keyboard focus ring.</summary>
    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<MediaPlayerRange, IBrush?>(nameof(FocusBrush));

    /// <summary>The track's height (4).</summary>
    public static readonly StyledProperty<double> TrackHeightProperty =
        AvaloniaProperty.Register<MediaPlayerRange, double>(nameof(TrackHeight), 4);

    /// <summary>The dot's diameter (12).</summary>
    public static readonly StyledProperty<double> ThumbSizeProperty =
        AvaloniaProperty.Register<MediaPlayerRange, double>(nameof(ThumbSize), 12);

    /// <summary>The width of the ring around the dot (3).</summary>
    public static readonly StyledProperty<double> ThumbGapProperty =
        AvaloniaProperty.Register<MediaPlayerRange, double>(nameof(ThumbGap), 3);

    /// <summary>The dot's scale while pressed (1.2).</summary>
    public static readonly StyledProperty<double> DragScaleProperty =
        AvaloniaProperty.Register<MediaPlayerRange, double>(nameof(DragScale), 1.2);

    /// <summary>Whether the slider is being dragged.</summary>
    public static readonly DirectProperty<MediaPlayerRange, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<MediaPlayerRange, bool>(nameof(IsDragging), o => o.IsDragging);

    private MotionValue<double>? _scale;
    private bool _dragging;
    private Pen? _focusPen;
    private IBrush? _focusPenBrush;

    static MediaPlayerRange()
    {
        FocusableProperty.OverrideDefaultValue<MediaPlayerRange>(true);
        AffectsRender<MediaPlayerRange>(ValueProperty, MinimumProperty, MaximumProperty, FillBrushProperty, TrackBrushProperty,
            ThumbBrushProperty, RingBrushProperty, FocusBrushProperty, TrackHeightProperty, ThumbSizeProperty, ThumbGapProperty);
    }

    /// <summary>Raised when the person changes the value (pointer or keyboard), never for values set from code.</summary>
    public event Action<double>? UserChanged;

    /// <summary>Raised when a drag starts (true) and ends (false).</summary>
    public event Action<bool>? DraggingChanged;

    /// <inheritdoc cref="FillBrushProperty"/>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <inheritdoc cref="TrackBrushProperty"/>
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    /// <inheritdoc cref="ThumbBrushProperty"/>
    public IBrush? ThumbBrush { get => GetValue(ThumbBrushProperty); set => SetValue(ThumbBrushProperty, value); }

    /// <inheritdoc cref="RingBrushProperty"/>
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }

    /// <inheritdoc cref="FocusBrushProperty"/>
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    /// <inheritdoc cref="TrackHeightProperty"/>
    public double TrackHeight { get => GetValue(TrackHeightProperty); set => SetValue(TrackHeightProperty, value); }

    /// <inheritdoc cref="ThumbSizeProperty"/>
    public double ThumbSize { get => GetValue(ThumbSizeProperty); set => SetValue(ThumbSizeProperty, value); }

    /// <inheritdoc cref="ThumbGapProperty"/>
    public double ThumbGap { get => GetValue(ThumbGapProperty); set => SetValue(ThumbGapProperty, value); }

    /// <inheritdoc cref="DragScaleProperty"/>
    public double DragScale { get => GetValue(DragScaleProperty); set => SetValue(DragScaleProperty, value); }

    /// <inheritdoc cref="IsDraggingProperty"/>
    public bool IsDragging
    {
        get => _dragging;
        private set
        {
            if (SetAndRaise(IsDraggingProperty, ref _dragging, value))
            {
                PseudoClasses.Set(":dragging", value);
                AnimateScale(value ? DragScale : 1);
                DraggingChanged?.Invoke(value);
            }
        }
    }

    /// <summary>A tenth of the range snapped to the step, at least one step (what Page Up and Page Down move).</summary>
    public double PageChange
    {
        get
        {
            var step = SmallChange;
            var page = (Maximum - Minimum) / 10;
            if (step <= 0)
            {
                return page;
            }
            return Math.Max(step, Math.Round(page / step) * step);
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 120 : 0, Math.Max(ThumbSize * DragScale, 18));

    protected override AutomationPeer OnCreateAutomationPeer() => new RangePeer(this);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Pointer.Capture(this);
        e.Handled = true;
        Focus(NavigationMethod.Pointer);
        IsDragging = true;
        SetFromUser(ValueAt(e.GetPosition(this).X));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            SetFromUser(ValueAt(e.GetPosition(this).X));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            e.Pointer.Capture(null);
            IsDragging = false;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        IsDragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled || e.Handled)
        {
            return;
        }
        double? next = e.Key switch
        {
            Key.Left or Key.Down => Value - SmallChange,
            Key.Right or Key.Up => Value + SmallChange,
            Key.PageDown => Value - PageChange,
            Key.PageUp => Value + PageChange,
            Key.Home => Minimum,
            Key.End => Maximum,
            _ => null,
        };
        if (next is { } value)
        {
            SetFromUser(value);
            e.Handled = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFocusedProperty || change.Property == IsEnabledProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        IsDragging = false;
        _scale?.Jump(1);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        // Hits anywhere in the slider's box, like a native range input.
        context.FillRectangle(Brushes.Transparent, new Rect(bounds.Size));
        var radius = ThumbSize / 2;
        var scale = _scale?.Value ?? 1;
        var center = new Point(ThumbCenterX(bounds.Width), bounds.Height / 2);
        var track = TrackHeight;
        var trackTop = (bounds.Height - track) / 2;
        var corner = track / 2;
        if (TrackBrush is { } trackBrush)
        {
            context.DrawRectangle(trackBrush, null, new RoundedRect(new Rect(0, trackTop, bounds.Width, track), corner));
        }
        if (FillBrush is { } fillBrush && center.X > 0)
        {
            context.DrawRectangle(fillBrush, null, new RoundedRect(new Rect(0, trackTop, center.X, track), corner));
        }
        var outer = (radius + ThumbGap) * scale;
        if (RingBrush is { } ring)
        {
            context.DrawEllipse(ring, null, center, outer, outer);
        }
        if (ThumbBrush is { } thumb)
        {
            context.DrawEllipse(thumb, null, center, radius * scale, radius * scale);
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusBrush is { } focus)
        {
            if (_focusPen is null || !ReferenceEquals(_focusPenBrush, focus))
            {
                _focusPen = new Pen(focus, 2);
                _focusPenBrush = focus;
            }
            context.DrawRectangle(null, _focusPen, new RoundedRect(new Rect(bounds.Size).Inflate(1), 6));
        }
    }

    private double ThumbCenterX(double width)
    {
        var radius = ThumbSize / 2;
        var span = Math.Max(0, width - ThumbSize);
        var range = Maximum - Minimum;
        var fraction = range > 0 ? Math.Clamp((Value - Minimum) / range, 0, 1) : 0;
        return radius + span * fraction;
    }

    private double ValueAt(double x)
    {
        var span = Math.Max(1, Bounds.Width - ThumbSize);
        var fraction = Math.Clamp((x - ThumbSize / 2) / span, 0, 1);
        return Minimum + fraction * (Maximum - Minimum);
    }

    private void SetFromUser(double value)
    {
        var step = SmallChange;
        if (step > 0)
        {
            value = Minimum + Math.Round((value - Minimum) / step) * step;
        }
        value = Math.Clamp(value, Minimum, Maximum);
        if (value == Value)
        {
            return;
        }
        SetCurrentValue(ValueProperty, value);
        UserChanged?.Invoke(value);
    }

    private void AnimateScale(double target)
    {
        _scale ??= CreateScale();
        if (ArcMotion.IsFull(this))
        {
            _ = _scale.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            _scale.Jump(target);
        }
    }

    private MotionValue<double> CreateScale()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private sealed class RangePeer(MediaPlayerRange owner) : RangeBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string GetClassNameCore() => "Slider";
    }
}
