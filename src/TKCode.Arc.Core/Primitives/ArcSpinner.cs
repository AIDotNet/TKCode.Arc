using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P21 loading ring: a 1.5px ring with one open quarter, spinning once every 0.7s (linear).
/// Spins on a Core <see cref="LiveLoop"/> only while attached, visible and in view at full motion, keeping its phase
/// when it pauses; static under reduced motion. The ring geometry and pen are cached, so a frame allocates nothing.
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

    /// <summary>True while frames are being delivered (attached, visible, in view, full motion).</summary>
    public bool IsSpinning => _loop.IsRunning;

    private readonly LiveLoop _loop;
    private double _angle;
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
        _loop = new LiveLoop(this, Step);
        _loop.Start();
    }

    private bool Step(double seconds)
    {
        _angle = (_angle + seconds / TurnSeconds * 360) % 360;
        InvalidateVisual();
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == ThicknessProperty)
        {
            _pen = null;
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
