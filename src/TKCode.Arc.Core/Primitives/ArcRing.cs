using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// A circular progress ring: an optional full track and an arc that starts at <see cref="StartAngle"/> (12 o'clock)
/// and runs clockwise to <see cref="Value"/> (0–1). Used for a reply's progress around a send button, an upload chip,
/// a character counter and an undo countdown (Arc AI blocks). The drawn value follows <see cref="Value"/> at once, or
/// chases it frame by frame (<see cref="ChaseRate"/>) on a Core <see cref="LiveLoop"/> at full motion only; reduced
/// motion and Off show the value at once. The geometry and pens are cached, so a frame allocates nothing.
/// </summary>
public class ArcRing : Control
{
    /// <summary>The arc's brush.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ArcRing>();

    /// <summary>The full circle under the arc; null draws no track.</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ArcRing, IBrush?>(nameof(TrackBrush));

    /// <summary>Progress from 0 (empty) to 1 (full circle); values outside are clamped.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcRing, double>(nameof(Value), coerce: (_, v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1));

    /// <summary>Stroke width of the track and the arc.</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ArcRing, double>(nameof(Thickness), 2);

    /// <summary>Round caps on the arc's ends (default) or flat ones.</summary>
    public static readonly StyledProperty<bool> IsRoundCapProperty =
        AvaloniaProperty.Register<ArcRing, bool>(nameof(IsRoundCap), true);

    /// <summary>Where the arc starts, in degrees clockwise from 3 o'clock; −90 is 12 o'clock.</summary>
    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<ArcRing, double>(nameof(StartAngle), -90);

    /// <summary>
    /// 0 (default) draws <see cref="Value"/> at once. Above 0, the drawn value moves this fraction of the remaining
    /// distance every 1/60s (frame-rate independent), so it trails a value that climbs in steps (Arc's reply ring
    /// uses about 0.15).
    /// </summary>
    public static readonly StyledProperty<double> ChaseRateProperty =
        AvaloniaProperty.Register<ArcRing, double>(nameof(ChaseRate), coerce: (_, v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1));

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <inheritdoc cref="TrackBrushProperty"/>
    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="ThicknessProperty"/>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <inheritdoc cref="IsRoundCapProperty"/>
    public bool IsRoundCap
    {
        get => GetValue(IsRoundCapProperty);
        set => SetValue(IsRoundCapProperty, value);
    }

    /// <inheritdoc cref="StartAngleProperty"/>
    public double StartAngle
    {
        get => GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    /// <inheritdoc cref="ChaseRateProperty"/>
    public double ChaseRate
    {
        get => GetValue(ChaseRateProperty);
        set => SetValue(ChaseRateProperty, value);
    }

    /// <summary>The value currently drawn (trails <see cref="Value"/> while it chases).</summary>
    public double DisplayedValue => _displayed;

    /// <summary>True while the drawn value is chasing <see cref="Value"/>.</summary>
    public bool IsChasing => _loop.IsRunning;

    private const double RestDistance = 0.0005;

    private readonly LiveLoop _loop;
    private double _displayed;
    private Pen? _arcPen;
    private Pen? _trackPen;
    private readonly EllipseGeometry _circle = new();
    private readonly PathGeometry _arc = new();
    private readonly PathFigure _figure = new() { IsClosed = false, IsFilled = false };
    private readonly ArcSegment _segment = new() { SweepDirection = SweepDirection.Clockwise };
    private Size _geometrySize;
    private double _geometryThickness = double.NaN;
    private double _geometryStart = double.NaN;
    private double _geometryValue = double.NaN;

    static ArcRing()
    {
        AffectsRender<ArcRing>(ForegroundProperty, TrackBrushProperty, ThicknessProperty, IsRoundCapProperty, StartAngleProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ArcRing>(false);
        WidthProperty.OverrideDefaultValue<ArcRing>(16);
        HeightProperty.OverrideDefaultValue<ArcRing>(16);
    }

    public ArcRing()
    {
        _figure.Segments!.Add(_segment);
        _arc.Figures!.Add(_figure);
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += OnRunningChanged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == ChaseRateProperty)
        {
            Follow();
        }
        else if (change.Property == ForegroundProperty || change.Property == ThicknessProperty || change.Property == IsRoundCapProperty)
        {
            _arcPen = null;
            _trackPen = null;
            UpdateGeometry();
        }
        else if (change.Property == StartAngleProperty)
        {
            UpdateGeometry();
        }
        else if (change.Property == TrackBrushProperty)
        {
            _trackPen = null;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Follow();
    }

    /// <summary>Shows the value at once, or starts the chase when it may run.</summary>
    private void Follow()
    {
        var target = Value;
        if (Math.Abs(target - _displayed) < RestDistance)
        {
            _loop.Stop();
            Show(target);
            return;
        }
        if (ChaseRate <= 0 || !this.IsAttachedToVisualTree() || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _loop.Stop();
            Show(target);
            return;
        }
        _loop.Start();
    }

    private bool Step(double seconds)
    {
        var target = Value;
        var keep = 1 - Math.Pow(1 - ChaseRate, seconds * 60);
        var next = _displayed + (target - _displayed) * keep;
        if (Math.Abs(target - next) < RestDistance)
        {
            Show(target);
            return false;
        }
        Show(next);
        return true;
    }

    private void OnRunningChanged(bool running)
    {
        // Paused by the motion level (reduced or off): the chase is decoration, so show the value where it is.
        if (!running && _loop.IsActive && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _loop.Stop();
            Show(Value);
        }
    }

    private void Show(double value)
    {
        if (value.Equals(_displayed))
        {
            return;
        }
        _displayed = value;
        UpdateGeometry();
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateGeometry();
    }

    /// <summary>
    /// Updates the cached circle and arc in place (never during the render pass): a chase moves one end point and
    /// rebuilds no geometry objects.
    /// </summary>
    private void UpdateGeometry()
    {
        var size = Bounds.Size;
        var thickness = Thickness;
        if (size.Width <= thickness || size.Height <= thickness)
        {
            return;
        }
        var radius = (Math.Min(size.Width, size.Height) - thickness) / 2;
        var center = new Point(size.Width / 2, size.Height / 2);
        if (_geometrySize != size || !_geometryThickness.Equals(thickness))
        {
            _circle.Center = center;
            _circle.RadiusX = radius;
            _circle.RadiusY = radius;
            _geometrySize = size;
            _geometryThickness = thickness;
            _geometryValue = double.NaN;
        }
        var value = _displayed;
        var start = StartAngle;
        if (value <= 0.0001 || value >= 0.9999 || (_geometryValue.Equals(value) && _geometryStart.Equals(start)))
        {
            return;
        }
        var from = start * Math.PI / 180;
        var to = (start + value * 360) * Math.PI / 180;
        _figure.StartPoint = new Point(center.X + radius * Math.Cos(from), center.Y + radius * Math.Sin(from));
        _segment.Point = new Point(center.X + radius * Math.Cos(to), center.Y + radius * Math.Sin(to));
        _segment.Size = new Size(radius, radius);
        _segment.IsLargeArc = value > 0.5;
        _geometryValue = value;
        _geometryStart = start;
    }

    public override void Render(DrawingContext context)
    {
        var thickness = Thickness;
        if (_geometrySize.Width <= thickness || _geometrySize.Height <= thickness || _geometrySize != Bounds.Size)
        {
            return;
        }
        if (TrackBrush is { } track)
        {
            _trackPen ??= new Pen(track, thickness);
            context.DrawGeometry(null, _trackPen, _circle);
        }
        var value = _displayed;
        if (value <= 0.0001 || Foreground is not { } brush)
        {
            return;
        }
        _arcPen ??= new Pen(brush, thickness, lineCap: IsRoundCap ? PenLineCap.Round : PenLineCap.Flat);
        context.DrawGeometry(null, _arcPen, value >= 0.9999 ? _circle : _arc);
    }
}
