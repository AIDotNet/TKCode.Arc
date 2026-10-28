using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawing layer of <see cref="DotGrid"/>: it owns the <see cref="DotGridSimulation"/>, rebuilds it when its size
/// or the gap changes, runs it on a <see cref="LiveLoop"/> while anything moves and draws every frame. Resting dots go
/// into one geometry; lit dots are drawn one by one, growing and taking the active color.
/// </summary>
internal sealed class DotGridField : Control
{
    /// <summary>A dot whose glow is below this is drawn with the resting dots.</summary>
    private const double GlowThreshold = 0.04;

    // The edge fade: opaque in the middle 40% of an ellipse that reaches 70% of the field out from its centre.
    private static readonly IImmutableBrush FadeMask = new ImmutableRadialGradientBrush(
        [new ImmutableGradientStop(0.4, Colors.Black), new ImmutableGradientStop(1, Colors.Transparent)],
        radiusX: new RelativeScalar(0.7, RelativeUnit.Relative),
        radiusY: new RelativeScalar(0.7, RelativeUnit.Relative));

    private readonly DotGridSimulation _simulation = new();
    private readonly LiveLoop _loop;
    private IImmutableBrush _dotBrush = new ImmutableSolidColorBrush(Colors.Gray);
    private IImmutableBrush _activeBrush = new ImmutableSolidColorBrush(Colors.Gray);
    private double _dotSize = 1.4;
    private double _gap = 22;
    private bool _fade = true;

    public DotGridField()
    {
        IsHitTestVisible = false;
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
    }

    internal DotGridSimulation Simulation => _simulation;

    internal LiveLoop Loop => _loop;

    /// <summary>Distance between dots; the grid is rebuilt when it changes.</summary>
    public double Gap
    {
        get => _gap;
        set
        {
            if (_gap.Equals(value))
            {
                return;
            }
            _gap = value;
            Rebuild();
        }
    }

    public double DotSize
    {
        get => _dotSize;
        set
        {
            _dotSize = Math.Max(0, value);
            InvalidateVisual();
        }
    }

    public bool Fade
    {
        get => _fade;
        set
        {
            _fade = value;
            InvalidateVisual();
        }
    }

    public void SetBrushes(IBrush? dot, IBrush? active)
    {
        _dotBrush = Freeze(dot, Colors.Gray);
        _activeBrush = Freeze(active, Colors.Gray);
        InvalidateVisual();
    }

    /// <summary>Asks for frames: something changed that will move or light dots.</summary>
    public void Wake()
    {
        _simulation.CanMove = ArcMotion.IsFull(this);
        _loop.Start();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _simulation.CanMove = ArcMotion.IsFull(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ArcMotion.LevelProperty)
        {
            // Under reduced motion every dot goes back home at once and only the lens tint stays.
            _simulation.CanMove = ArcMotion.IsFull(this);
            if (!_simulation.CanMove)
            {
                _simulation.Rest();
                InvalidateVisual();
            }
        }
    }

    private void Rebuild()
    {
        _simulation.Build(Bounds.Width, Bounds.Height, _gap);
        InvalidateVisual();
    }

    private bool Step(double dt)
    {
        var moving = _simulation.Step(dt);
        InvalidateVisual();
        return moving;
    }

    public override void Render(DrawingContext context)
    {
        var simulation = _simulation;
        if (simulation.Count == 0 || _dotSize <= 0)
        {
            return;
        }
        using (_fade ? context.PushOpacityMask(FadeMask, new Rect(Bounds.Size)) : default(DrawingContext.PushedState?))
        {
            DrawDots(context, simulation);
        }
    }

    private void DrawDots(DrawingContext context, DotGridSimulation simulation)
    {
        var size = _dotSize;
        var geometry = new StreamGeometry();
        var lit = 0;
        using (var path = geometry.Open())
        {
            for (var i = 0; i < simulation.Count; i++)
            {
                if (simulation.Glow(i) > GlowThreshold)
                {
                    lit++;
                    continue;
                }
                AddDot(path, simulation.X(i), simulation.Y(i), size);
            }
        }
        context.DrawGeometry(_dotBrush, null, geometry);
        if (lit == 0)
        {
            return;
        }
        for (var i = 0; i < simulation.Count; i++)
        {
            var glow = simulation.Glow(i);
            if (glow <= GlowThreshold)
            {
                continue;
            }
            var center = new Point(simulation.X(i), simulation.Y(i));
            // The resting color fades out under the growing active dot, so a dot lights up instead of switching.
            if (glow < 0.5)
            {
                using (context.PushOpacity(1 - glow * 2))
                {
                    context.DrawEllipse(_dotBrush, null, center, size, size);
                }
            }
            var radius = size * (1 + glow);
            using (context.PushOpacity(0.4 + 0.6 * glow))
            {
                context.DrawEllipse(_activeBrush, null, center, radius, radius);
            }
        }
    }

    private static void AddDot(StreamGeometryContext path, double x, double y, double radius)
    {
        var arc = new Size(radius, radius);
        path.BeginFigure(new Point(x + radius, y), true);
        path.ArcTo(new Point(x - radius, y), arc, 0, false, SweepDirection.Clockwise);
        path.ArcTo(new Point(x + radius, y), arc, 0, false, SweepDirection.Clockwise);
        path.EndFigure(true);
    }

    private static IImmutableBrush Freeze(IBrush? brush, Color fallback) => brush switch
    {
        IImmutableBrush immutable => immutable,
        ISolidColorBrush solid => new ImmutableSolidColorBrush(solid.Color, solid.Opacity),
        not null => brush.ToImmutable(),
        _ => new ImmutableSolidColorBrush(fallback)
    };
}
