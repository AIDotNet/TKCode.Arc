using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Arc's EyeMorph (P15 + P29): one Lucide eye that a slash draws across, cutting the outline beneath it,
/// instead of swapping two icons. The slash draws on the standard tween while its opacity (and the cut) follows
/// the instant tween. Drawn in <see cref="Render"/> with cached geometry, so the animation allocates nothing.
/// </summary>
internal sealed class PasswordEyeGlyph : Control
{
    public static readonly StyledProperty<bool> IsSlashedProperty =
        AvaloniaProperty.Register<PasswordEyeGlyph, bool>(nameof(IsSlashed));

    /// <summary>Slash inset in the 24px view box: 2 for PasswordField (M2 2l20 20), 3 for PasswordStrength (M3 3l18 18).</summary>
    public static readonly StyledProperty<double> SlashInsetProperty =
        AvaloniaProperty.Register<PasswordEyeGlyph, double>(nameof(SlashInset), 2);

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<PasswordEyeGlyph, double>(nameof(Size), 18);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<PasswordEyeGlyph>();

    public bool IsSlashed { get => GetValue(IsSlashedProperty); set => SetValue(IsSlashedProperty, value); }
    public double SlashInset { get => GetValue(SlashInsetProperty); set => SetValue(SlashInsetProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Drawn length of the slash, 0 to 1.</summary>
    internal double SlashLength => _length?.Value ?? (IsSlashed ? 1 : 0);

    private const int Steps = 48;
    private const double StrokeWidth = 1.75;
    private const double CutWidth = 5;

    private static readonly Geometry s_eye = Geometry.Parse(
        "M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0 M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z");

    private static readonly Dictionary<(double Inset, int Step), (Geometry Band, Geometry Outside)> s_cuts = new();

    private MotionValue<double>? _length;
    private MotionValue<double>? _opacity;
    private IPen? _pen;
    private IBrush? _penBrush;

    static PasswordEyeGlyph()
    {
        AffectsMeasure<PasswordEyeGlyph>(SizeProperty);
        AffectsRender<PasswordEyeGlyph>(ForegroundProperty, SlashInsetProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<PasswordEyeGlyph>(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSlashedProperty)
        {
            var target = IsSlashed ? 1.0 : 0.0;
            _length ??= CreateValue(1 - target);
            _opacity ??= CreateValue(1 - target);
            if (!IsLoaded || !ArcMotion.IsFull(this))
            {
                _length.Jump(target);
                _opacity.Jump(target);
                return;
            }
            _ = _length.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            _ = _opacity.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        if (scale <= 0)
        {
            return;
        }
        var pen = PenFor(brush);
        var length = Math.Clamp(_length?.Value ?? (IsSlashed ? 1 : 0), 0, 1);
        var opacity = Math.Clamp(_opacity?.Value ?? (IsSlashed ? 1 : 0), 0, 1);
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            if (length <= 0.001 || opacity <= 0.001)
            {
                context.DrawGeometry(null, pen, s_eye);
                return;
            }
            var step = (int)Math.Round(length * Steps);
            var (band, outside) = Cut(SlashInset, step);
            using (context.PushGeometryClip(outside))
            {
                context.DrawGeometry(null, pen, s_eye);
            }
            if (opacity < 0.999)
            {
                // The mask fades with the slash, so the outline under it comes back as the slash leaves.
                using (context.PushGeometryClip(band))
                using (context.PushOpacity(1 - opacity))
                {
                    context.DrawGeometry(null, pen, s_eye);
                }
            }
            var inset = SlashInset;
            var end = inset + (24 - 2 * inset) * step / Steps;
            using (context.PushOpacity(opacity))
            {
                context.DrawLine(pen, new Point(inset, inset), new Point(end, end));
            }
        }
    }

    private IPen PenFor(IBrush brush)
    {
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _penBrush = brush;
            _pen = new ImmutablePen(brush.ToImmutable(), StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        return _pen;
    }

    /// <summary>The cut band (the slash widened to 5 units with round caps) and everything outside it, per step.</summary>
    private static (Geometry Band, Geometry Outside) Cut(double inset, int step)
    {
        if (s_cuts.TryGetValue((inset, step), out var cached))
        {
            return cached;
        }
        var start = new Point(inset, inset);
        var length = (24 - 2 * inset) * step / Steps;
        var end = new Point(inset + length, inset + length);
        var half = CutWidth / 2;
        var n = new Vector(-Math.Sqrt(0.5), Math.Sqrt(0.5)) * half;
        var band = new StreamGeometry();
        using (var g = band.Open())
        {
            g.BeginFigure(start + n, true);
            g.LineTo(end + n);
            g.ArcTo(end - n, new Size(half, half), 0, false, SweepDirection.CounterClockwise);
            g.LineTo(start - n);
            g.ArcTo(start + n, new Size(half, half), 0, false, SweepDirection.CounterClockwise);
            g.EndFigure(true);
        }
        var outside = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(-2, -2, 28, 28)), band);
        var result = ((Geometry)band, (Geometry)outside);
        s_cuts[(inset, step)] = result;
        return result;
    }
}
