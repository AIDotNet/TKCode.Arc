using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The session-length dial of the control center's Focus detail (Arc Control center, design reference): a 164px ring
/// (150 when compact) where one full turn is 120 minutes. A faint track with 24 ticks (a long one every 45°), an accent
/// arc from 12 o'clock to the value and a 22px knob on the track; the number and "min" in the middle with the end time
/// under them. Dragging turns it relative to where it was grabbed (a tap changes nothing), the angle is unwrapped so it
/// keeps counting across 12 o'clock, past 5 and 120 the knob resists (c = 90°, factor 0.4) while the arc and the number
/// hold, and a release snaps to the nearest 5 minutes. Keys: arrows ±5, Page Up / Down ±15, Home 5, End 120.
/// <para><see cref="RangeBase.Value"/> is the committed value (a multiple of 5); <see cref="KnobMinutes"/> is drawn.</para>
/// <para>Why code: the knob follows the pointer and snaps on springs that keep velocity when grabbed again.</para>
/// </summary>
internal sealed class ControlCenterDial : RangeBase
{
    public static readonly StyledProperty<IBrush?> TrackInkProperty =
        AvaloniaProperty.Register<ControlCenterDial, IBrush?>(nameof(TrackInk));

    public static readonly StyledProperty<IBrush?> MajorTickInkProperty =
        AvaloniaProperty.Register<ControlCenterDial, IBrush?>(nameof(MajorTickInk));

    public static readonly StyledProperty<IBrush?> MinorTickInkProperty =
        AvaloniaProperty.Register<ControlCenterDial, IBrush?>(nameof(MinorTickInk));

    public static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<ControlCenterDial, IBrush?>(nameof(AccentInk));

    public static readonly StyledProperty<IBrush?> KnobInkProperty =
        AvaloniaProperty.Register<ControlCenterDial, IBrush?>(nameof(KnobInk));

    public static readonly StyledProperty<BoxShadows> KnobShadowProperty =
        AvaloniaProperty.Register<ControlCenterDial, BoxShadows>(nameof(KnobShadow));

    public static readonly StyledProperty<IBrush?> FocusInkProperty =
        AvaloniaProperty.Register<ControlCenterDial, IBrush?>(nameof(FocusInk));

    public static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<ControlCenterDial, IBrush?>(nameof(SecondaryInk));

    public static readonly StyledProperty<FontFamily> NumberFontFamilyProperty =
        AvaloniaProperty.Register<ControlCenterDial, FontFamily>(nameof(NumberFontFamily), FontFamily.Default);

    private const double ViewBox = 200;
    private const double TrackRadius = 94;
    private const double KnobDiameter = 22;

    private readonly MotionValue<double> _knob;
    private readonly DragTracker _drag;
    private readonly EllipseGeometry _circle = new();
    private readonly PathGeometry _arc = new();
    private readonly PathFigure _figure = new() { IsClosed = false, IsFilled = false };
    private readonly ArcSegment _segment = new() { SweepDirection = SweepDirection.Clockwise };
    private readonly Run _number = new();
    private readonly Run _unit = new();
    private readonly TextBlock _numberText;
    private readonly TextBlock _untilText;
    private readonly StackPanel _center;
    private Pen? _trackPen;
    private Pen? _arcPen;
    private Pen? _majorPen;
    private Pen? _minorPen;
    private Pen? _knobPen;
    private Pen? _focusPen;
    private double _penScale = double.NaN;
    private Size _geometrySize;
    private double _geometryArc = double.NaN;
    private double _lastAngle;
    private double _raw;
    private int _shownNumber = -1;
    private bool _userChange;

    static ControlCenterDial()
    {
        AffectsRender<ControlCenterDial>(TrackInkProperty, MajorTickInkProperty, MinorTickInkProperty, AccentInkProperty,
            KnobInkProperty, KnobShadowProperty, FocusInkProperty);
        FocusableProperty.OverrideDefaultValue<ControlCenterDial>(true);
        MinimumProperty.OverrideDefaultValue<ControlCenterDial>(ControlCenterMath.DialMinimum);
        MaximumProperty.OverrideDefaultValue<ControlCenterDial>(ControlCenterMath.DialMaximum);
    }

    public ControlCenterDial()
    {
        _figure.Segments!.Add(_segment);
        _arc.Figures!.Add(_figure);
        _knob = new MotionValue<double>(this, ControlCenterMath.DialMinimum, MotionVectors.Double);
        _knob.Changed += _ => OnKnobMoved();
        _drag = new DragTracker(this, new DragOptions { Threshold = 2 });
        _drag.Pressed += (_, e) => OnPressed(e);
        _drag.Moved += (_, e) => OnDragged(e);
        _drag.Completed += (_, _) => OnDragEnded();
        _drag.Cancelled += (_, _) => OnDragEnded();

        _numberText = new TextBlock
        {
            Inlines = [_number, _unit],
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFeatures = [FontFeature.Parse("tnum")]
        };
        _untilText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFeatures = [FontFeature.Parse("tnum")],
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _center = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Children = { _numberText, _untilText }
        };
        _number.FontSize = 36;
        _number.LetterSpacing = -1.08;
        _unit.FontSize = 14;
        VisualChildren.Add(_center);
        LogicalChildren.Add(_center);
        Cursor = new Cursor(StandardCursorType.Hand);
        OnKnobMoved();
    }

    public IBrush? TrackInk { get => GetValue(TrackInkProperty); set => SetValue(TrackInkProperty, value); }
    public IBrush? MajorTickInk { get => GetValue(MajorTickInkProperty); set => SetValue(MajorTickInkProperty, value); }
    public IBrush? MinorTickInk { get => GetValue(MinorTickInkProperty); set => SetValue(MinorTickInkProperty, value); }
    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }
    public IBrush? KnobInk { get => GetValue(KnobInkProperty); set => SetValue(KnobInkProperty, value); }
    public BoxShadows KnobShadow { get => GetValue(KnobShadowProperty); set => SetValue(KnobShadowProperty, value); }
    public IBrush? FocusInk { get => GetValue(FocusInkProperty); set => SetValue(FocusInkProperty, value); }
    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }
    public FontFamily NumberFontFamily { get => GetValue(NumberFontFamilyProperty); set => SetValue(NumberFontFamilyProperty, value); }

    /// <summary>The minutes the knob is drawn at (rubber-banded past the ends while dragging).</summary>
    public double KnobMinutes => _knob.Value;

    /// <summary>The number in the middle now.</summary>
    public int ShownNumber => _shownNumber;

    /// <summary>The line under the number ("Until 12:14 PM").</summary>
    public string? UntilText
    {
        get => _untilText.Text;
        set => _untilText.Text = value;
    }

    /// <summary>The unit after the number ("min").</summary>
    public string? UnitText
    {
        get => _unit.Text;
        set => _unit.Text = value is null ? null : " " + value;
    }

    /// <summary>True while the knob is being dragged.</summary>
    public bool IsDragging => _drag.IsDragging;

    /// <summary>The shown number changed (it counts through every minute while the knob springs or is dragged).</summary>
    public event Action<int>? ShownNumberChanged;

    /// <summary>The user changed the committed value (a drag step, a release or a key).</summary>
    public event Action<int>? UserChanged;

    /// <summary>The committed value as a whole number of minutes.</summary>
    public int IntValue => (int)Math.Round(Value);

    /// <summary>Puts the knob on the value at once (opening the detail).</summary>
    public void SyncKnob() => _knob.Jump(Value);

    /// <summary>Springs the knob to a value set by code, such as a new mode's default.</summary>
    public void AnimateTo(int minutes)
    {
        SetCurrentValue(ValueProperty, (double)minutes);
        _ = _knob.AnimateTo(minutes, MotionSpec.Spring(ControlCenterMath.QuickSpring));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlCenterSliderPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty && !_userChange && !_drag.IsDragging && VisualRoot is null)
        {
            _knob.Jump(Value);
        }
        else if (change.Property == TrackInkProperty || change.Property == MajorTickInkProperty ||
                 change.Property == MinorTickInkProperty || change.Property == AccentInkProperty ||
                 change.Property == FocusInkProperty)
        {
            _penScale = double.NaN;
        }
        else if (change.Property == SecondaryInkProperty)
        {
            _unit.Foreground = SecondaryInk;
            _untilText.Foreground = SecondaryInk;
        }
        else if (change.Property == NumberFontFamilyProperty)
        {
            _number.FontFamily = NumberFontFamily;
        }
    }

    private void SetFromUser(int value)
    {
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

    private Point Center => new(Bounds.Width / 2, Bounds.Height / 2);

    private bool TryLocal(Point rootPoint, out Point local)
    {
        local = default;
        if (TopLevel.GetTopLevel(this) is not { } top || top.TranslatePoint(rootPoint, this) is not { } p)
        {
            return false;
        }
        local = p;
        return true;
    }

    private void OnPressed(DragGestureEventArgs e)
    {
        // Grab wherever the finger lands: the turn is measured from here, and a spring in flight continues from where it is.
        _knob.Stop();
        _raw = _knob.Value;
        if (TryLocal(e.Start, out var local))
        {
            _lastAngle = ControlCenterMath.AngleOf(local, Center);
        }
    }

    private void OnDragged(DragGestureEventArgs e)
    {
        if (!TryLocal(e.Position, out var local))
        {
            return;
        }
        var angle = ControlCenterMath.AngleOf(local, Center);
        _raw += ControlCenterMath.AngleDelta(_lastAngle, angle) / ControlCenterMath.DegreesPerMinute;
        _lastAngle = angle;
        _knob.Jump(ControlCenterMath.DialKnobMinutes(_raw));
        SetFromUser(ControlCenterMath.SnapMinutes(_raw));
    }

    private void OnDragEnded()
    {
        var target = ControlCenterMath.SnapMinutes(_raw);
        SetFromUser(target);
        // Inside the range the snap is quick; coming back from the overshoot uses the slower layout spring.
        var inside = _raw >= ControlCenterMath.DialMinimum && _raw <= ControlCenterMath.DialMaximum;
        _ = _knob.AnimateTo(target, MotionSpec.Spring(inside ? ControlCenterMath.QuickSpring : ControlCenterMath.LayoutSpring));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && ControlCenterMath.DialKey(IntValue, e.Key) is { } next)
        {
            e.Handled = true;
            SetFromUser(next);
            _ = _knob.AnimateTo(next, MotionSpec.Spring(ControlCenterMath.QuickSpring));
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

    private void OnKnobMoved()
    {
        UpdateGeometry();
        InvalidateVisual();
        var number = ControlCenterMath.DialNumber(_knob.Value);
        if (number != _shownNumber)
        {
            _shownNumber = number;
            _number.Text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ShownNumberChanged?.Invoke(number);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _center.Measure(availableSize);
        var size = double.IsNaN(Width) ? 164 : Width;
        return new Size(size, size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _center.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _penScale = double.NaN;
        UpdateGeometry();
    }

    /// <summary>Updates the circle and the arc in place (outside the render pass): a drag moves one end point and allocates nothing.</summary>
    private void UpdateGeometry()
    {
        var size = Bounds.Size;
        if (size.Width <= 0)
        {
            return;
        }
        var k = Math.Min(size.Width, size.Height) / ViewBox;
        var radius = TrackRadius * k;
        var center = Center;
        if (_geometrySize != size)
        {
            _circle.Center = center;
            _circle.RadiusX = radius;
            _circle.RadiusY = radius;
            _geometrySize = size;
            _geometryArc = double.NaN;
        }
        var arc = ControlCenterMath.DialArc(_knob.Value);
        if (_geometryArc.Equals(arc) || arc >= 0.9999)
        {
            return;
        }
        var to = arc * 2 * Math.PI;
        _figure.StartPoint = new Point(center.X, center.Y - radius);
        _segment.Point = new Point(center.X + radius * Math.Sin(to), center.Y - radius * Math.Cos(to));
        _segment.Size = new Size(radius, radius);
        _segment.IsLargeArc = arc > 0.5;
        _geometryArc = arc;
    }

    private void EnsurePens(double k)
    {
        if (_penScale.Equals(k))
        {
            return;
        }
        _penScale = k;
        _trackPen = TrackInk is null ? null : new Pen(TrackInk, 4 * k);
        _arcPen = AccentInk is null ? null : new Pen(AccentInk, 4 * k, lineCap: PenLineCap.Round);
        _majorPen = MajorTickInk is null ? null : new Pen(MajorTickInk, 2 * k, lineCap: PenLineCap.Round);
        _minorPen = MinorTickInk is null ? null : new Pen(MinorTickInk, 1.5 * k, lineCap: PenLineCap.Round);
        _knobPen = AccentInk is null ? null : new Pen(AccentInk, 3);
        _focusPen = FocusInk is null ? null : new Pen(FocusInk, 2);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || _geometrySize != size)
        {
            return;
        }
        var k = Math.Min(size.Width, size.Height) / ViewBox;
        EnsurePens(k);
        var center = Center;
        context.DrawEllipse(Brushes.Transparent, null, center, size.Width / 2, size.Height / 2);
        if (_trackPen is { } track)
        {
            context.DrawGeometry(null, track, _circle);
        }
        for (var i = 0; i < 24; i++)
        {
            var major = i % 3 == 0;
            var pen = major ? _majorPen : _minorPen;
            if (pen is null)
            {
                continue;
            }
            var a = i * 15 * Math.PI / 180;
            var (sin, cos) = Math.SinCos(a);
            var inner = (major ? 76 : 80) * k;
            var outer = 86 * k;
            context.DrawLine(pen, new Point(center.X + inner * sin, center.Y - inner * cos),
                new Point(center.X + outer * sin, center.Y - outer * cos));
        }
        if (_arcPen is { } arcPen)
        {
            context.DrawGeometry(null, arcPen, ControlCenterMath.DialArc(_knob.Value) >= 0.9999 ? _circle : _arc);
        }
        // The knob sits on the track at the drawn minutes (3° per minute, past 12 o'clock while overshooting).
        var knobAngle = _knob.Value * ControlCenterMath.DegreesPerMinute * Math.PI / 180;
        var (ks, kc) = Math.SinCos(knobAngle);
        var r = TrackRadius * k;
        var knobCenter = new Point(center.X + r * ks, center.Y - r * kc);
        var half = KnobDiameter / 2;
        var knobRect = new RoundedRect(new Rect(knobCenter.X - half, knobCenter.Y - half, KnobDiameter, KnobDiameter), half);
        context.DrawRectangle(KnobInk, null, knobRect, KnobShadow);
        if (_knobPen is { } knobPen)
        {
            context.DrawEllipse(null, knobPen, knobCenter, half - 1.5, half - 1.5);
        }
        if (_focusPen is { } focus && IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawEllipse(null, focus, center, size.Width / 2 + 3, size.Height / 2 + 3);
        }
    }
}
