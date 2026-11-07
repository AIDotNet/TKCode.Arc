using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A slider drawn over the picture (the volume slider, and the base of <see cref="VideoSeekBar"/>): a rounded track filled
/// up to a round thumb that shows only while hovered, dragged or focused (spec section 7). It behaves like a native range
/// input, as the Blocks MediaPlayer's range it is copied from (internal there, ADR 0036 decision 5): a press anywhere jumps
/// there and dragging follows the pointer; the arrows move one <see cref="RangeBase.SmallChange"/>, Page Up / Page Down a
/// tenth (subclasses change that), Home / End to the ends. The track height and the thumb's opacity are styled
/// properties so the theme animates them under <c>:motion-full</c>. Screen readers get <see cref="ValueText"/> as the
/// slider's item status.
/// </summary>
internal class VideoRange : RangeBase
{
    /// <summary>The filled part of the track.</summary>
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<VideoRange, IBrush?>(nameof(FillBrush));

    /// <summary>The rest of the track.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<VideoRange, IBrush?>(nameof(TrackBrush));

    /// <summary>The thumb.</summary>
    public static readonly StyledProperty<IBrush?> ThumbBrushProperty =
        AvaloniaProperty.Register<VideoRange, IBrush?>(nameof(ThumbBrush));

    /// <summary>The keyboard focus ring.</summary>
    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<VideoRange, IBrush?>(nameof(FocusBrush));

    /// <summary>The track's height (4; 6 while active on the seek bar).</summary>
    public static readonly StyledProperty<double> TrackHeightProperty =
        AvaloniaProperty.Register<VideoRange, double>(nameof(TrackHeight), 4);

    /// <summary>The thumb's diameter (12).</summary>
    public static readonly StyledProperty<double> ThumbSizeProperty =
        AvaloniaProperty.Register<VideoRange, double>(nameof(ThumbSize), 12);

    /// <summary>The thumb's opacity (0 at rest, 1 while hovered, dragged or focused).</summary>
    public static readonly StyledProperty<double> ThumbOpacityProperty =
        AvaloniaProperty.Register<VideoRange, double>(nameof(ThumbOpacity), 1);

    /// <summary>Whether the slider is being dragged.</summary>
    public static readonly DirectProperty<VideoRange, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<VideoRange, bool>(nameof(IsDragging), o => o.IsDragging);

    /// <summary>The value as screen readers read it ("Volume 60%", "1 minute 5 seconds of 3 minutes 15 seconds").</summary>
    public static readonly StyledProperty<string?> ValueTextProperty =
        AvaloniaProperty.Register<VideoRange, string?>(nameof(ValueText));

    private bool _dragging;
    private Pen? _focusPen;
    private IBrush? _focusPenBrush;

    static VideoRange()
    {
        FocusableProperty.OverrideDefaultValue<VideoRange>(true);
        AffectsRender<VideoRange>(ValueProperty, MinimumProperty, MaximumProperty, FillBrushProperty, TrackBrushProperty,
            ThumbBrushProperty, FocusBrushProperty, TrackHeightProperty, ThumbSizeProperty, ThumbOpacityProperty);
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

    /// <inheritdoc cref="FocusBrushProperty"/>
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    /// <inheritdoc cref="TrackHeightProperty"/>
    public double TrackHeight { get => GetValue(TrackHeightProperty); set => SetValue(TrackHeightProperty, value); }

    /// <inheritdoc cref="ThumbSizeProperty"/>
    public double ThumbSize { get => GetValue(ThumbSizeProperty); set => SetValue(ThumbSizeProperty, value); }

    /// <inheritdoc cref="ThumbOpacityProperty"/>
    public double ThumbOpacity { get => GetValue(ThumbOpacityProperty); set => SetValue(ThumbOpacityProperty, value); }

    /// <inheritdoc cref="ValueTextProperty"/>
    public string? ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }

    /// <inheritdoc cref="IsDraggingProperty"/>
    public bool IsDragging
    {
        get => _dragging;
        private set
        {
            if (SetAndRaise(IsDraggingProperty, ref _dragging, value))
            {
                PseudoClasses.Set(":dragging", value);
                DraggingChanged?.Invoke(value);
            }
        }
    }

    /// <summary>A tenth of the range snapped to the step, at least one step.</summary>
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

    /// <summary>The fraction (0..1) of the range the value is at.</summary>
    protected double Fraction
    {
        get
        {
            var range = Maximum - Minimum;
            return range > 0 ? Math.Clamp((Value - Minimum) / range, 0, 1) : 0;
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 120 : 0, Math.Max(ThumbSize, 16));

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
            e.Handled = true;
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
        if (!IsEnabled || e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        double? next = e.Key switch
        {
            Key.Left or Key.Down => Value - SmallChange,
            Key.Right or Key.Up => Value + SmallChange,
            Key.PageDown => PageTarget(-1),
            Key.PageUp => PageTarget(1),
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

    /// <summary>Where Page Up (<paramref name="direction"/> 1) and Page Down (-1) go: a tenth of the range by default.</summary>
    protected virtual double PageTarget(int direction) => Value + direction * PageChange;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFocusedProperty || change.Property == IsEnabledProperty)
        {
            InvalidateVisual();
        }
        else if (change.Property == ValueTextProperty)
        {
            AutomationProperties.SetItemStatus(this, ValueText);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        IsDragging = false;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        // Hits anywhere in the slider's box, like a native range input.
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var thumbX = ThumbX(size.Width);
        var track = TrackHeight;
        var trackRect = new Rect(0, (size.Height - track) / 2, size.Width, track);
        RenderTrack(context, trackRect, thumbX);
        var opacity = Math.Clamp(ThumbOpacity, 0, 1);
        if (ThumbBrush is { } thumb && opacity > 0)
        {
            var radius = ThumbSize / 2;
            using (context.PushOpacity(opacity))
            {
                context.DrawEllipse(thumb, null, new Point(thumbX, size.Height / 2), radius, radius);
            }
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusBrush is { } focus)
        {
            if (_focusPen is null || !ReferenceEquals(_focusPenBrush, focus))
            {
                _focusPen = new Pen(focus, 2);
                _focusPenBrush = focus;
            }
            context.DrawRectangle(null, _focusPen, new RoundedRect(new Rect(size).Inflate(new Thickness(4, 1)), 6));
        }
    }

    /// <summary>Draws the track and its fill up to <paramref name="thumbX"/>.</summary>
    protected virtual void RenderTrack(DrawingContext context, Rect track, double thumbX)
    {
        var corner = track.Height / 2;
        if (TrackBrush is { } trackBrush)
        {
            context.DrawRectangle(trackBrush, null, new RoundedRect(track, corner));
        }
        if (FillBrush is { } fillBrush && thumbX > track.X)
        {
            context.DrawRectangle(fillBrush, null, new RoundedRect(track.WithWidth(thumbX - track.X), corner));
        }
    }

    /// <summary>Whether values set by the person snap to <see cref="RangeBase.SmallChange"/> (the volume; not the seek bar).</summary>
    protected virtual bool SnapsToStep => true;

    /// <summary>The thumb's centre: the track spans the whole width and the fill ends at the thumb.</summary>
    protected double ThumbX(double width) => Fraction * width;

    /// <summary>The value under <paramref name="x"/>.</summary>
    protected double ValueAt(double x)
    {
        var width = Math.Max(1, Bounds.Width);
        var fraction = Math.Clamp(x / width, 0, 1);
        return Minimum + fraction * (Maximum - Minimum);
    }

    /// <summary>Sets the value as the person did (snapped to the step, clamped) and raises <see cref="UserChanged"/>.</summary>
    protected void SetFromUser(double value)
    {
        var step = SmallChange;
        if (SnapsToStep && step > 0 && !_dragging)
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

    private sealed class RangePeer(VideoRange owner) : RangeBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string GetClassNameCore() => "Slider";
    }
}
