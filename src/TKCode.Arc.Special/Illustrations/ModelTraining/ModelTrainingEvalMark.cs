using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The 24 px mark of a <see cref="ModelTraining"/>'s evaluation row (Arc Model training, design reference): a circle of
/// radius 10.5 that shows a turning accent arc while the held-out data runs, then fills faintly with the success colour
/// and draws a check (or, when the score stays under the target, fills with the danger colour and draws a cross).
/// </summary>
internal sealed class ModelTrainingEvalMark : Control
{
    internal static readonly StyledProperty<IBrush?> TrackInkProperty =
        AvaloniaProperty.Register<ModelTrainingEvalMark, IBrush?>(nameof(TrackInk));

    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<ModelTrainingEvalMark, IBrush?>(nameof(AccentInk));

    internal static readonly StyledProperty<IBrush?> SuccessInkProperty =
        AvaloniaProperty.Register<ModelTrainingEvalMark, IBrush?>(nameof(SuccessInk));

    internal static readonly StyledProperty<IBrush?> DangerInkProperty =
        AvaloniaProperty.Register<ModelTrainingEvalMark, IBrush?>(nameof(DangerInk));

    internal static readonly StyledProperty<IBrush?> BorderInkProperty =
        AvaloniaProperty.Register<ModelTrainingEvalMark, IBrush?>(nameof(BorderInk));

    private readonly ExplainerPath _spin = new();
    private readonly ExplainerPath _check = new();
    private Pen? _spinPen;
    private Pen? _checkPen;
    private ModelTraining? _owner;

    static ModelTrainingEvalMark()
    {
        AffectsRender<ModelTrainingEvalMark>(TrackInkProperty, AccentInkProperty, SuccessInkProperty, DangerInkProperty, BorderInkProperty);
    }

    public ModelTrainingEvalMark()
    {
        var center = new Point(12, 12);
        _spin.MoveTo(ExplainerPath.OnCircle(center, 10.5, 0)).ArcTo(center, 10.5, 0, 359.9, 4);
        _check.MoveTo(new Point(7.5, 12.4)).LineTo(new Point(10.6, 15.4)).LineTo(new Point(16.5, 9));
        ActualThemeVariantChanged += (_, _) =>
        {
            _spinPen = null;
            _checkPen = null;
            InvalidateVisual();
        };
    }

    public IBrush? TrackInk { get => GetValue(TrackInkProperty); set => SetValue(TrackInkProperty, value); }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    public IBrush? SuccessInk { get => GetValue(SuccessInkProperty); set => SetValue(SuccessInkProperty, value); }

    public IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }

    public IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }

    internal void Attach(ModelTraining owner)
    {
        _owner = owner;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(24, 24);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_owner is null)
        {
            return;
        }
        var timeline = _owner.Timeline;
        var t = _owner.RenderTime;
        var state = timeline.EvalStateAt(t);
        var center = new Point(12, 12);
        var track = ColorOf(TrackInk, Colors.Gray);
        if (state == 2)
        {
            var passed = timeline.PassesTarget;
            var tone = ColorOf(passed ? SuccessInk : DangerInk, Colors.Green);
            var ring = ArcColor.Lerp(tone, ColorOf(BorderInk, Colors.LightGray), 0.5);
            context.DrawEllipse(new ImmutableSolidColorBrush(tone, 0.12), new ImmutablePen(new ImmutableSolidColorBrush(ring), 1.25), center, 10.5, 10.5);
            if (_checkPen is null || _checkPen.Brush is not ISolidColorBrush { } b || b.Color != tone)
            {
                _checkPen = new Pen(new ImmutableSolidColorBrush(tone), 1.75, new DashStyle(new AvaloniaList<double>(), 0), PenLineCap.Round, PenLineJoin.Round);
            }
            var drawn = Math.Max(0.05, timeline.CheckAt(t));
            if (passed)
            {
                if (_check.ShowPart(_checkPen, 0, drawn * _check.Length))
                {
                    context.DrawGeometry(null, _checkPen, _check.Geometry);
                }
                return;
            }
            // Below the target: a cross, its two strokes drawn one after the other.
            var crossPen = new ImmutablePen(new ImmutableSolidColorBrush(tone), 1.75, null, PenLineCap.Round);
            var first = Math.Clamp(drawn * 2, 0, 1);
            context.DrawLine(crossPen, new Point(8.5, 8.5), new Point(8.5 + 7 * first, 8.5 + 7 * first));
            var second = Math.Clamp(drawn * 2 - 1, 0, 1);
            if (second > 0)
            {
                context.DrawLine(crossPen, new Point(15.5, 8.5), new Point(15.5 - 7 * second, 8.5 + 7 * second));
            }
            return;
        }
        context.DrawEllipse(null, new ImmutablePen(new ImmutableSolidColorBrush(track), 1.25), center, 10.5, 10.5);
        if (state == 1)
        {
            var accent = ColorOf(_owner.Accent ?? AccentInk, Colors.DimGray);
            if (_spinPen is null || _spinPen.Brush is not ISolidColorBrush { } s || s.Color != accent)
            {
                _spinPen = new Pen(new ImmutableSolidColorBrush(accent), 1.75, new DashStyle(new AvaloniaList<double>(), 0), PenLineCap.Round);
            }
            var turn = (t * 1.1) % 1;
            var start = turn * _spin.Length;
            var arc = 0.26 * _spin.Length;
            // A quarter arc turning a little over once a second.
            if (_spin.ShowPart(_spinPen, start, Math.Min(_spin.Length, start + arc)))
            {
                context.DrawGeometry(null, _spinPen, _spin.Geometry);
            }
        }
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;
}
