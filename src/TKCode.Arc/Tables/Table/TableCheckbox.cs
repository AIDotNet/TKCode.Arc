using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Arc's table checkbox, drawn by its owner: an 18px box whose accent fill grows from 0.6 on a snappy spring,
/// a check that draws itself (path length) and morphs into the mixed dash (they share three points), a hover
/// border and a press scale. Arc builds it from a hidden native input; here the row or header cell owns focus.
/// </summary>
internal sealed class TableCheckbox
{
    public const double Extent = 18;
    private const double Radius = 5;
    private static readonly Point[] s_check = [new(4.25, 9.25), new(7.25, 12.25), new(13.75, 5.75)];
    private static readonly Point[] s_dash = [new(4.75, 9), new(9, 9), new(13.25, 9)];

    private readonly Visual _owner;
    private MotionValue<double>? _scale;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _length;
    private MotionValue<double>? _morph;
    private MotionValue<double>? _hover;
    private MotionValue<double>? _press;
    private Color _penColor;
    private IPen? _borderPen;

    public TableCheckbox(Visual owner) => _owner = owner;

    public bool IsChecked { get; private set; }
    public bool IsMixed { get; private set; }

    private bool IsOn => IsChecked || IsMixed;

    public void Set(bool isChecked, bool isMixed, bool animate)
    {
        if (isChecked == IsChecked && isMixed == IsMixed)
        {
            return;
        }
        IsChecked = isChecked;
        IsMixed = isMixed;
        var on = IsOn ? 1.0 : 0.0;
        if (!animate || !ArcMotion.IsFull(_owner))
        {
            // Arc's reduced motion uses duration 0 for the whole box.
            _scale?.Jump(IsOn ? 1 : 0.6);
            _opacity?.Jump(on);
            _length?.Jump(on);
            _morph?.Jump(isMixed ? 1 : 0);
            _owner.InvalidateVisual();
            return;
        }
        _scale ??= Create(IsOn ? 0.6 : 1);
        _opacity ??= Create(1 - on);
        _length ??= Create(1 - on);
        _morph ??= Create(isMixed ? 0 : 1);
        _ = _scale.AnimateTo(IsOn ? 1 : 0.6, MotionSpec.Spring(ArcSpring.Snappy));
        _ = _opacity.AnimateTo(on, MotionSpec.Tween(IsOn ? ArcMotionPresets.Instant : ArcMotionPresets.Fast, ArcEasings.Standard));
        _ = _length.AnimateTo(on, MotionSpec.Spring(ArcSpring.Snappy));
        _ = _morph.AnimateTo(isMixed ? 1 : 0, MotionSpec.Spring(ArcSpring.Morph));
    }

    /// <summary>Hover over the select cell (mouse only): the resting border darkens to text-muted.</summary>
    public void SetHover(bool hover)
    {
        _hover ??= Create(0);
        var spec = ArcMotion.GetEffectiveLevel(_owner) == ArcMotionLevel.Full
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Instant;
        _ = _hover.AnimateTo(hover ? 1 : 0, spec);
    }

    public void SetPressed(bool pressed)
    {
        if (!ArcMotion.IsFull(_owner))
        {
            _press?.Jump(1);
            return;
        }
        _press ??= Create(1);
        _ = _press.AnimateTo(pressed ? 0.95 : 1, pressed
            ? MotionSpec.Tween(TimeSpan.FromMilliseconds(110), ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
    }

    /// <summary>Snaps every motion to the current state (rows are recycled).</summary>
    public void Reset(bool isChecked, bool isMixed)
    {
        IsChecked = isChecked;
        IsMixed = isMixed;
        var on = IsOn ? 1.0 : 0.0;
        _scale?.Jump(IsOn ? 1 : 0.6);
        _opacity?.Jump(on);
        _length?.Jump(on);
        _morph?.Jump(isMixed ? 1 : 0);
        _hover?.Jump(0);
        _press?.Jump(1);
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(_owner, initial, MotionVectors.Double);
        value.Changed += _ => _owner.InvalidateVisual();
        return value;
    }

    public void Render(DrawingContext context, Point origin, TablePalette palette, bool focusVisible)
    {
        var on = IsOn ? 1.0 : 0.0;
        var opacity = Math.Clamp(_opacity?.Value ?? on, 0, 1);
        var scale = _scale?.Value ?? (IsOn ? 1 : 0.6);
        var length = Math.Clamp(_length?.Value ?? on, 0, 1);
        var morph = _morph?.Value ?? (IsMixed ? 1 : 0);
        var hover = _hover?.Value ?? 0;
        var press = _press?.Value ?? 1;
        var box = new Rect(origin, new Size(Extent, Extent));
        var center = box.Center;
        using var pressTransform = press < 0.9999
            ? context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(press, press) *
                                    Matrix.CreateTranslation(center.X, center.Y))
            : default(DrawingContext.PushedState?);
        var border = TablesBDrawing.Lerp(
            TablesBDrawing.Lerp(palette.BorderStrong, palette.TextMuted, hover * (1 - opacity)), palette.ControlOn, opacity);
        if (_borderPen is null || border != _penColor)
        {
            _penColor = border;
            _borderPen = new ImmutablePen(new ImmutableSolidColorBrush(border), 1);
        }
        context.DrawRectangle(palette.SurfaceBrush, _borderPen, box.Deflate(0.5), Radius - 0.5, Radius - 0.5);
        if (opacity > 0.001)
        {
            using (context.PushOpacity(opacity))
            {
                using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) *
                                             Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y)))
                {
                    context.DrawRectangle(palette.ControlOnBrush, null, box, Radius, Radius);
                }
                if (length > 0.001)
                {
                    DrawMark(context, origin, palette.GlyphPen, length, Math.Clamp(morph, 0, 1.2));
                }
            }
        }
        if (focusVisible && palette.FocusPen is { } focus)
        {
            context.DrawRectangle(null, focus, box.Inflate(3), Radius + 3, Radius + 3);
        }
    }

    private static void DrawMark(DrawingContext context, Point origin, IPen pen, double length, double morph)
    {
        var p0 = Mix(s_check[0], s_dash[0], morph) + origin;
        var p1 = Mix(s_check[1], s_dash[1], morph) + origin;
        var p2 = Mix(s_check[2], s_dash[2], morph) + origin;
        var first = Distance(p0, p1);
        var second = Distance(p1, p2);
        var drawn = (first + second) * length;
        if (drawn <= first)
        {
            context.DrawLine(pen, p0, Mix(p0, p1, first > 0 ? drawn / first : 1));
            return;
        }
        context.DrawLine(pen, p0, p1);
        context.DrawLine(pen, p1, Mix(p1, p2, second > 0 ? (drawn - first) / second : 1));
    }

    private static Point Mix(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static double Distance(Point a, Point b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
