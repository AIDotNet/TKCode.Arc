using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The accessible peer of the dial and the meters: a slider with a range value.</summary>
internal sealed class ControlCenterSliderPeer(RangeBase owner) : RangeBaseAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
}

/// <summary>
/// A segmented level meter of the control center (Arc Control center, design reference): 28 rounded segments in a faint
/// track, the same segments in the text color (every other one at 88% for a fine stripe) clipped continuously at the
/// level, so the clip cuts through a segment. Grab anywhere and it follows the finger 1:1 from the grab; a tap jumps to
/// the tapped value when released; past either end the track stretches up to 6px toward the finger while the value holds;
/// the track swells to 1.18× its height while held. Keys: arrows one step, Page Up / Down four steps, Home, End.
/// <para><see cref="RangeBase.Value"/> is the committed value (whole steps); the drawn level is
/// <see cref="DisplayedValue"/>, which follows drags continuously and springs to each new value.</para>
/// <para>Why code: the level, swell and stretch are pointer-driven springs that keep velocity when retargeted.</para>
/// </summary>
internal sealed class ControlCenterMeter : RangeBase
{
    public static readonly StyledProperty<IBrush?> TrackInkProperty =
        AvaloniaProperty.Register<ControlCenterMeter, IBrush?>(nameof(TrackInk));

    public static readonly StyledProperty<IBrush?> FillInkProperty =
        AvaloniaProperty.Register<ControlCenterMeter, IBrush?>(nameof(FillInk));

    public static readonly StyledProperty<IBrush?> FocusInkProperty =
        AvaloniaProperty.Register<ControlCenterMeter, IBrush?>(nameof(FocusInk));

    private readonly MotionValue<double> _shown;
    private readonly MotionValue<double> _swell;
    private readonly MotionValue<double> _stretch;
    private readonly DragTracker _drag;
    private IBrush? _fill;
    private IBrush? _fillAlt;
    private IBrush? _track;
    private Pen? _focusPen;
    private double _grab;
    private bool _userChange;
    private bool _held;

    static ControlCenterMeter()
    {
        AffectsRender<ControlCenterMeter>(TrackInkProperty, FillInkProperty, FocusInkProperty);
        FocusableProperty.OverrideDefaultValue<ControlCenterMeter>(true);
        HeightProperty.OverrideDefaultValue<ControlCenterMeter>(30);
    }

    public ControlCenterMeter()
    {
        _shown = new MotionValue<double>(this, 0, MotionVectors.Double);
        _shown.Changed += v =>
        {
            InvalidateVisual();
            DisplayedChanged?.Invoke(v);
        };
        _swell = new MotionValue<double>(this, 1, MotionVectors.Double);
        _swell.Changed += _ => InvalidateVisual();
        _stretch = new MotionValue<double>(this, 0, MotionVectors.Double);
        _stretch.Changed += _ => InvalidateVisual();
        _drag = new DragTracker(this, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 3 });
        _drag.Pressed += (_, _) => OnPressed();
        _drag.Moved += (_, e) => OnDragged(e);
        _drag.Completed += (_, _) => OnDragEnded();
        _drag.Cancelled += (_, _) => OnDragEnded();
        _drag.Released += (_, e) => OnTapped(e);
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    public IBrush? TrackInk { get => GetValue(TrackInkProperty); set => SetValue(TrackInkProperty, value); }
    public IBrush? FillInk { get => GetValue(FillInkProperty); set => SetValue(FillInkProperty, value); }
    public IBrush? FocusInk { get => GetValue(FocusInkProperty); set => SetValue(FocusInkProperty, value); }

    /// <summary>The size of one step (1 for the volume, 5 for the text size); values are whole steps from the minimum.</summary>
    public int Step { get; set; } = 1;

    /// <summary>The level drawn now (continuous while dragging, springing after a change).</summary>
    public double DisplayedValue => _shown.Value;

    /// <summary>The track's vertical swell now (1 at rest, 1.18 while held).</summary>
    public double Swell => _swell.Value;

    /// <summary>The track's stretch past an end now, in pixels (negative toward the start).</summary>
    public double Stretch => _stretch.Value;

    /// <summary>True between a press and its release.</summary>
    public bool IsHeld => _held;

    /// <summary>The drawn level changed (every frame of a spring or drag).</summary>
    public event Action<double>? DisplayedChanged;

    /// <summary>The user changed the committed value (a drag step, a tap or a key).</summary>
    public event Action<int>? UserChanged;

    /// <summary>The committed value as a whole number.</summary>
    public int IntValue => (int)Math.Round(Value);

    /// <summary>Snaps the drawn level to the value (no animation); used when the meter is first shown.</summary>
    public void SyncDisplayed() => _shown.Jump(Value);

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlCenterSliderPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty && !_userChange && !_drag.IsDragging)
        {
            if (VisualRoot is null)
            {
                _shown.Jump(Value);
            }
            else
            {
                _ = _shown.AnimateTo(Value, MotionSpec.Spring(ControlCenterMath.QuickSpring));
            }
        }
        else if (change.Property == FillInkProperty)
        {
            _fill = FillInk;
            _fillAlt = FillInk is ISolidColorBrush solid
                ? new ImmutableSolidColorBrush(solid.Color, 0.88 * solid.Opacity)
                : FillInk;
        }
        else if (change.Property == TrackInkProperty)
        {
            _track = TrackInk;
        }
        else if (change.Property == FocusInkProperty)
        {
            _focusPen = FocusInk is null ? null : new Pen(FocusInk, 2);
        }
    }

    private void SetFromUser(int value)
    {
        value = Math.Clamp(value, (int)Minimum, (int)Maximum);
        if (value == IntValue)
        {
            return;
        }
        _userChange = true;
        try
        {
            SetCurrentValue(ValueProperty, (double)value);
        }
        finally
        {
            _userChange = false;
        }
        UserChanged?.Invoke(value);
    }

    private void Hold(bool held)
    {
        _held = held;
        // The swell is a pointer cue: reduced motion keeps the track flat.
        var target = held && ArcMotion.IsFull(this) ? ControlCenterMath.MeterSwell : 1;
        _ = _swell.AnimateTo(target, MotionSpec.Spring(ControlCenterMath.QuickSpring));
    }

    private void OnPressed()
    {
        _shown.Stop();
        _grab = _shown.Value;
        Hold(true);
    }

    private void OnDragged(DragGestureEventArgs e)
    {
        var width = Bounds.Width;
        var raw = ControlCenterMath.MeterDragValue(_grab, e.Offset.X, width, Minimum, Maximum);
        var range = Maximum - Minimum;
        var clamped = Math.Clamp(raw, Minimum, Maximum);
        var overflowPx = range <= 0 ? 0 : (raw - clamped) / range * width;
        _stretch.Jump(ControlCenterMath.MeterStretch(overflowPx));
        _shown.Jump(clamped);
        SetFromUser(ControlCenterMath.Snap(clamped, (int)Minimum, (int)Maximum, Step));
    }

    private void OnDragEnded()
    {
        Hold(false);
        _ = _stretch.AnimateTo(0, MotionSpec.Spring(ControlCenterMath.LayoutSpring));
        _ = _shown.AnimateTo(Value, MotionSpec.Spring(ControlCenterMath.QuickSpring));
    }

    private void OnTapped(DragGestureEventArgs e)
    {
        Hold(false);
        // A press that moved past the threshold across the axis was a scroll, not a tap.
        if (Math.Abs(e.Offset.X) > 3 || Math.Abs(e.Offset.Y) > 3)
        {
            return;
        }
        if (TopLevel.GetTopLevel(this) is not { } top || top.TranslatePoint(e.Start, this) is not { } local)
        {
            return;
        }
        var value = ControlCenterMath.MeterValueAt(local.X, Bounds.Width, Minimum, Maximum);
        SetFromUser(ControlCenterMath.Snap(value, (int)Minimum, (int)Maximum, Step));
        _ = _shown.AnimateTo(Value, MotionSpec.Spring(ControlCenterMath.QuickSpring));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None &&
            ControlCenterMath.MeterKey(IntValue, e.Key, (int)Minimum, (int)Maximum, Step) is { } next)
        {
            e.Handled = true;
            SetFromUser(next);
            _ = _shown.AnimateTo(Value, MotionSpec.Spring(ControlCenterMath.QuickSpring));
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, width, height));
        var stretch = _stretch.Value;
        var swell = _swell.Value;
        // Scale about the center, then shift by half the stretch: the far edge reaches toward the finger, the near one stays.
        var matrix = Matrix.CreateTranslation(-width / 2, -height / 2) *
                     Matrix.CreateScale(1 + Math.Abs(stretch) / width, swell) *
                     Matrix.CreateTranslation(width / 2 + stretch / 2, height / 2);
        var segment = ControlCenterMath.SegmentWidth(width);
        var pitch = segment + ControlCenterMath.MeterGap;
        using (context.PushTransform(matrix))
        {
            if (_track is { } track)
            {
                for (var i = 0; i < ControlCenterMath.MeterSegments; i++)
                {
                    context.DrawRectangle(track, null, new Rect(i * pitch, 0, segment, height), 2, 2);
                }
            }
            var filled = ControlCenterMath.MeterFraction(_shown.Value, Minimum, Maximum) * width;
            if (filled > 0 && _fill is { } fill)
            {
                using (context.PushClip(new Rect(0, 0, filled, height)))
                {
                    for (var i = 0; i < ControlCenterMath.MeterSegments && i * pitch < filled; i++)
                    {
                        context.DrawRectangle(i % 2 == 1 ? _fillAlt : fill, null, new Rect(i * pitch, 0, segment, height), 2, 2);
                    }
                }
            }
        }
        if (_focusPen is { } pen && IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, pen, new Rect(-3, -3, width + 6, height + 6), 5, 5);
        }
    }
}
