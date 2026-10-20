using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The two marks of the checkbox share three points (Arc's <c>checkPath</c> and <c>dashPath</c> in an 18px box),
/// so the check morphs into the dash and back by moving its points instead of swapping shapes.
/// </summary>
internal static class CheckboxPathMorph
{
    public const int PointCount = 3;

    private static readonly Point[] s_check = [new(4.25, 9.25), new(7.25, 12.25), new(13.75, 5.75)];
    private static readonly Point[] s_dash = [new(4.75, 9), new(9, 9), new(13.25, 9)];

    /// <summary>Point <paramref name="index"/> of the mark at morph progress <paramref name="t"/> (0 = check, 1 = dash).</summary>
    public static Point PointAt(int index, double t)
    {
        var a = s_check[index];
        var b = s_dash[index];
        return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>
    /// Draws the first <paramref name="progress"/> of the polyline's length (SVG <c>pathLength</c>). Two line segments
    /// with round caps reproduce the round join, and nothing is allocated per frame.
    /// </summary>
    public static void DrawPartial(DrawingContext context, IPen pen, double morph, double progress)
    {
        if (progress <= 0.001)
        {
            return;
        }
        var p0 = PointAt(0, morph);
        var p1 = PointAt(1, morph);
        var p2 = PointAt(2, morph);
        var first = Distance(p0, p1);
        var second = Distance(p1, p2);
        var drawn = Math.Min(1, progress) * (first + second);
        if (drawn <= first)
        {
            context.DrawLine(pen, p0, Lerp(p0, p1, first <= 0 ? 1 : drawn / first));
            return;
        }
        context.DrawLine(pen, p0, p1);
        context.DrawLine(pen, p1, Lerp(p1, p2, second <= 0 ? 1 : (drawn - first) / second));
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}

/// <summary>
/// The painted inside of the checkbox square: the <c>ControlOn</c> fill that grows from 0.6 on a snappy spring, and the
/// mark that draws itself (P15) and morphs between check and dash (P16). Done in code because a path morph and a
/// partial stroke have no ControlTheme equivalent; four motion values drive one <see cref="Render"/>.
/// </summary>
internal sealed class CheckboxMark : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<CheckboxMark, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> GlyphProperty =
        AvaloniaProperty.Register<CheckboxMark, IBrush?>(nameof(Glyph));

    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<CheckboxMark, double>(nameof(Radius), 5);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CheckboxMark, double>(nameof(StrokeThickness), 1.75);

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    /// <summary>Box edge of the mark geometry (Arc draws in an 18x18 viewBox).</summary>
    public const double BoxSize = 18;

    private readonly MotionValue<double> _fillScale;
    private readonly MotionValue<double> _fade;
    private readonly MotionValue<double> _draw;
    private readonly MotionValue<double> _morph;
    private Pen? _pen;
    private CheckboxState _state;

    static CheckboxMark()
    {
        AffectsRender<CheckboxMark>(FillProperty, GlyphProperty, RadiusProperty, StrokeThicknessProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CheckboxMark>(false);
    }

    public CheckboxMark()
    {
        _fillScale = Create(0.6);
        _fade = Create(0);
        _draw = Create(0);
        _morph = Create(0);
    }

    public CheckboxState State => _state;

    // Read by tests.
    internal double FillScale => _fillScale.Value;
    internal double FadeValue => _fade.Value;
    internal double DrawProgress => _draw.Value;
    internal double MorphProgress => _morph.Value;

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>Moves to <paramref name="state"/>. Reduced and Off apply every change at once (Arc: duration 0).</summary>
    public void Apply(CheckboxState state, bool animate)
    {
        _state = state;
        var on = state != CheckboxState.Unchecked;
        var fill = on ? 1.0 : 0.6;
        var fade = on ? 1.0 : 0.0;
        var morph = state == CheckboxState.Indeterminate ? 1.0 : 0.0;
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _fillScale.Jump(fill);
            _fade.Jump(fade);
            _draw.Jump(fade);
            _morph.Jump(morph);
            return;
        }
        _ = _fillScale.AnimateTo(fill, MotionSpec.Spring(ArcSpring.Snappy));
        _ = _draw.AnimateTo(fade, MotionSpec.Spring(ArcSpring.Snappy));
        _ = _morph.AnimateTo(morph, MotionSpec.Spring(ArcSpring.Morph));
        _ = _fade.AnimateTo(fade, MotionSpec.Tween(on ? ArcMotionPresets.Instant : ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlyphProperty || change.Property == StrokeThicknessProperty)
        {
            _pen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(BoxSize, BoxSize);

    public override void Render(DrawingContext context)
    {
        var fade = Math.Clamp(_fade.Value, 0, 1);
        if (fade <= 0.001)
        {
            return;
        }
        var scale = Math.Max(0, _fillScale.Value);
        if (Fill is { } fill && scale > 0)
        {
            var edge = BoxSize * scale;
            var offset = (BoxSize - edge) / 2;
            using (context.PushOpacity(fade))
            {
                context.DrawRectangle(fill, null, new RoundedRect(new Rect(offset, offset, edge, edge), Radius * scale));
            }
        }
        if (Glyph is { } glyph)
        {
            _pen ??= new Pen(glyph, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            using (context.PushOpacity(fade))
            {
                CheckboxPathMorph.DrawPartial(context, _pen, _morph.Value, _draw.Value);
            }
        }
    }
}
