using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P15: a 24x24 stroked glyph that draws itself from 0 to its full length (checkmarks, ticks, connectors).
/// Set <see cref="Progress"/> directly, or call <see cref="Play"/>.
/// </summary>
public class StrokeDrawGlyph : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<StrokeDrawGlyph, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<StrokeDrawGlyph, double>(nameof(Progress), 1);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<StrokeDrawGlyph, double>(nameof(StrokeThickness), 1.75);

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<StrokeDrawGlyph, double>(nameof(Size), 16);

    /// <summary>When true, the stroke draws in automatically each time the glyph is attached.</summary>
    public static readonly StyledProperty<bool> DrawOnAttachProperty =
        AvaloniaProperty.Register<StrokeDrawGlyph, bool>(nameof(DrawOnAttach));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<StrokeDrawGlyph>();

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public bool DrawOnAttach { get => GetValue(DrawOnAttachProperty); set => SetValue(DrawOnAttachProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private MotionValue<double>? _progress;

    static StrokeDrawGlyph()
    {
        AffectsMeasure<StrokeDrawGlyph>(SizeProperty);
        AffectsRender<StrokeDrawGlyph>(DataProperty, ProgressProperty, StrokeThicknessProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<StrokeDrawGlyph>(false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DrawOnAttach)
        {
            Progress = 0;
            _ = Play(TimeSpan.FromSeconds(0.5), ArcEasings.Standard, TimeSpan.FromSeconds(0.05));
        }
    }

    /// <summary>Draws the stroke from 0 to 1. Under reduced motion the stroke appears at once.</summary>
    public Task Play(TimeSpan duration, Easing easing, TimeSpan delay = default)
    {
        _progress ??= CreateValue();
        _progress.Jump(Progress);
        return _progress.AnimateTo(1, MotionSpec.Tween(duration, easing, delay));
    }

    private MotionValue<double> CreateValue()
    {
        var value = new MotionValue<double>(this, Progress, MotionVectors.Double);
        value.Changed += v => Progress = v;
        return value;
    }

    private Pen? _pen;
    private IBrush? _penBrush;

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var data = Data;
        var brush = Foreground;
        var progress = Math.Clamp(Progress, 0, 1);
        if (data is null || brush is null || progress <= 0)
        {
            return;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || _pen.Thickness != StrokeThickness)
        {
            _pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
        }
        var pen = _pen;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            if (progress >= 1)
            {
                context.DrawGeometry(null, pen, data);
                return;
            }
            var length = data.ContourLength;
            if (length > 0 && data.TryGetSegment(0, length * progress, true, out var segment) && segment is not null)
            {
                context.DrawGeometry(null, pen, segment);
            }
        }
    }
}
