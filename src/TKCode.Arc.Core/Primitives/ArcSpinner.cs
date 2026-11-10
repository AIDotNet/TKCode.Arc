using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P21 loading ring: a 1.5px ring with one open quarter, spinning once every 0.7s (linear).
/// Runs on a Core <see cref="LiveLoop"/> only while attached, visible and in view, keeping its phase when it pauses.
/// At full motion it spins; under reduced motion it rests at its angle and breathes in opacity
/// (<see cref="ArcBusyPulse"/>) so it still reads as busy; with motion off it is still. The ring geometry and pen are
/// cached, so a frame allocates nothing.
/// </summary>
public class ArcSpinner : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ArcSpinner>();

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ArcSpinner, double>(nameof(Thickness), 1.5);

    /// <summary>One turn in seconds (Arc: 0.7s).</summary>
    private const double TurnSeconds = 0.7;

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>Current rotation in degrees.</summary>
    public double Angle => _angle;

    /// <summary>True while the ring turns (frames delivered at full motion: attached, visible, in view).</summary>
    public bool IsSpinning => _loop.IsRunning && _full;

    /// <summary>True while the resting ring breathes in opacity instead of turning (reduced motion).</summary>
    public bool IsPulsing => _loop.IsRunning && !_full;

    /// <summary>The ring's opacity now: 1 unless it is pulsing under reduced motion.</summary>
    public double PulseOpacity => IsPulsing ? ArcBusyPulse.Opacity(_pulse) : 1;

    private readonly LiveLoop _loop;
    private double _angle;
    private double _pulse;
    private bool _full = true;
    private StreamGeometry? _ring;
    private Size _ringSize;
    private double _ringThickness;
    private Pen? _pen;

    static ArcSpinner()
    {
        AffectsRender<ArcSpinner>(ForegroundProperty, ThicknessProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ArcSpinner>(false);
        WidthProperty.OverrideDefaultValue<ArcSpinner>(16);
        HeightProperty.OverrideDefaultValue<ArcSpinner>(16);
    }

    public ArcSpinner()
    {
        // An infinite Avalonia Animation cannot be started with RunAsync (it throws), and a loop must pause when unseen.
        // It also runs under reduced motion, where it breathes instead of turning (a busy ring must not freeze).
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _loop.RunningChanged += OnRunningChanged;
        // The motion level can come from the theme's resources: re-read it when they change.
        ResourcesChanged += (_, _) => RefreshLevel();
        _loop.Start();
    }

    private bool Step(double seconds)
    {
        if (_full)
        {
            _angle = (_angle + seconds / TurnSeconds * 360) % 360;
        }
        else
        {
            _pulse = ArcBusyPulse.Advance(_pulse, seconds);
        }
        InvalidateVisual();
        return true;
    }

    private void OnRunningChanged(bool running)
    {
        if (running)
        {
            RefreshLevel();
        }
        InvalidateVisual();
    }

    // Read once per level change, not per frame (the level lookup walks up to the resource root).
    private void RefreshLevel()
    {
        var full = ArcMotion.IsFull(this);
        if (full == _full)
        {
            return;
        }
        _full = full;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == ThicknessProperty)
        {
            _pen = null;
        }
        else if (change.Property == ArcMotion.LevelProperty || change.Property == ArcMotion.TokenLevelProperty)
        {
            RefreshLevel();
        }
    }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        if (brush is null)
        {
            return;
        }
        var t = Thickness;
        var size = Bounds.Size;
        if (_ring is null || _ringSize != size || _ringThickness != t)
        {
            _ring = CreateRing(size, t);
            _ringSize = size;
            _ringThickness = t;
        }
        _pen ??= new Pen(brush, t);
        var center = new Point(size.Width / 2, size.Height / 2);
        var opacity = PulseOpacity;
        using (opacity < 1 ? context.PushOpacity(opacity) : default(DrawingContext.PushedState?))
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
                                     Matrix.CreateRotation(_angle * Math.PI / 180.0) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawGeometry(null, _pen, _ring);
        }
    }

    private static StreamGeometry CreateRing(Size size, double thickness)
    {
        var radius = (Math.Min(size.Width, size.Height) - thickness) / 2;
        var center = new Point(size.Width / 2, size.Height / 2);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Three quarters of the ring; the right quarter is open (border-right-color: transparent).
            var start = new Point(center.X + radius * Math.Cos(Math.PI / 4), center.Y + radius * Math.Sin(Math.PI / 4));
            var end = new Point(center.X + radius * Math.Cos(-Math.PI / 4), center.Y + radius * Math.Sin(-Math.PI / 4));
            ctx.BeginFigure(start, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, true, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        return geometry;
    }
}
