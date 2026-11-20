using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The loss chart of a <see cref="ModelTraining"/> (Arc Model training, design reference: 112 high, two dashed grid lines
/// at y 10 and 51, the axis at 92, the epochs from x 2 to the width − 10, the line 1.75 in the accent drawn up to the
/// head, a 3.5 head with a background ring, and a hollow validation point that fades in once the evaluation passes).
/// The loss scale tops out a little above the first loss. Drawn from the owner's run time; frames allocate nothing.
/// </summary>
internal sealed class ModelTrainingChart : Control
{
    internal static readonly StyledProperty<IBrush?> GridInkProperty =
        AvaloniaProperty.Register<ModelTrainingChart, IBrush?>(nameof(GridInk));

    internal static readonly StyledProperty<IBrush?> AxisInkProperty =
        AvaloniaProperty.Register<ModelTrainingChart, IBrush?>(nameof(AxisInk));

    internal static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<ModelTrainingChart, IBrush?>(nameof(MutedInk));

    internal static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<ModelTrainingChart, IBrush?>(nameof(SecondaryInk));

    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<ModelTrainingChart, IBrush?>(nameof(AccentInk));

    internal static readonly StyledProperty<IBrush?> BackgroundInkProperty =
        AvaloniaProperty.Register<ModelTrainingChart, IBrush?>(nameof(BackgroundInk));

    internal static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<ModelTrainingChart>();

    private const int Samples = 72;
    private const double Left = 2;
    private const double Axis = 92;
    private const double Top = 10;

    private readonly ExplainerTextCache _text = new(64);
    private readonly ExplainerPath _curve = new();
    private readonly double[] _lengths = new double[Samples + 1];
    private Pen? _linePen;
    private IPen? _gridPen;
    private IPen? _axisPen;
    private ModelTraining? _owner;
    private double _builtWidth;
    private int _builtVersion = -1;

    static ModelTrainingChart()
    {
        AffectsRender<ModelTrainingChart>(GridInkProperty, AxisInkProperty, MutedInkProperty, SecondaryInkProperty, AccentInkProperty,
            BackgroundInkProperty, FontFamilyProperty);
    }

    public ModelTrainingChart()
    {
        ActualThemeVariantChanged += (_, _) =>
        {
            _text.Clear();
            _linePen = null;
            _gridPen = null;
            _axisPen = null;
            InvalidateVisual();
        };
    }

    public IBrush? GridInk { get => GetValue(GridInkProperty); set => SetValue(GridInkProperty, value); }

    public IBrush? AxisInk { get => GetValue(AxisInkProperty); set => SetValue(AxisInkProperty, value); }

    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }

    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    public IBrush? BackgroundInk { get => GetValue(BackgroundInkProperty); set => SetValue(BackgroundInkProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    internal void Attach(ModelTraining owner)
    {
        _owner = owner;
        _builtVersion = -1;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsFinite(availableSize.Width) ? availableSize.Width : 232, 112);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_owner is null || Bounds.Width <= 20)
        {
            return;
        }
        var width = Bounds.Width;
        var right = width - 10;
        var timeline = _owner.Timeline;
        var t = _owner.RenderTime;
        var muted = ColorOf(MutedInk, Colors.Gray);
        var accent = ColorOf(_owner.Accent ?? AccentInk, Colors.DimGray);
        var background = ColorOf(BackgroundInk, Colors.White);

        _gridPen ??= new ImmutablePen(_text.Brush(ColorOf(GridInk, Colors.Gainsboro)), 1, new ImmutableDashStyle([2, 3], 0));
        _axisPen ??= new ImmutablePen(_text.Brush(ColorOf(AxisInk, Colors.LightGray)), 1);
        context.DrawLine(_gridPen, new Point(Left, Top + 0.5), new Point(right, Top + 0.5));
        context.DrawLine(_gridPen, new Point(Left, 51.5), new Point(right, 51.5));
        context.DrawLine(_axisPen, new Point(Left, Axis + 0.5), new Point(right, Axis + 0.5));

        var face = new Typeface(FontFamily);
        var first = _text.Get("1", face, 12, muted);
        first.Draw(context, new Point(Left, 108 - first.Baseline));
        var last = _text.Get(_owner.EpochText(timeline.Epochs), face, 12, muted);
        last.Draw(context, new Point(right - last.WidthIncludingTrailingWhitespace, 108 - last.Baseline));

        EnsureCurve(width, timeline);
        if (!timeline.IsMeasured(t))
        {
            return;
        }
        var head = timeline.HeadAt(t);
        var reveal = LengthAt(head);
        if (_linePen is null || _linePen.Brush is not ISolidColorBrush { } lineBrush || lineBrush.Color != accent)
        {
            _linePen = new Pen(new ImmutableSolidColorBrush(accent), 1.75, new DashStyle(new AvaloniaList<double>(), 0), PenLineCap.Round, PenLineJoin.Round);
        }
        if (_curve.ShowPart(_linePen, 0, Math.Max(0.02, reveal)))
        {
            context.DrawGeometry(null, _linePen, _curve.Geometry);
        }
        var validation = timeline.ValidationAlpha(t);
        var headPoint = PointAt(head, right, timeline);
        if (validation < 1)
        {
            using (context.PushOpacity(1 - validation))
            {
                context.DrawEllipse(_text.Brush(accent), new ImmutablePen(_text.Brush(background), 2), headPoint, 3.5, 3.5);
            }
        }
        if (validation > 0)
        {
            using (context.PushOpacity(validation))
            {
                var y = Y(timeline.ValidationLoss, timeline);
                var secondary = ColorOf(SecondaryInk, Colors.DimGray);
                context.DrawEllipse(_text.Brush(background), new ImmutablePen(_text.Brush(secondary), 1.5), new Point(right, y), 3.5, 3.5);
                var label = _text.Get(_owner.Labels.Validation(timeline.ValidationLoss), face, 12, muted);
                label.Draw(context, new Point(right - 8 - label.WidthIncludingTrailingWhitespace, y - 7 - label.Baseline));
            }
        }
    }

    private void EnsureCurve(double width, ModelTrainingTimeline timeline)
    {
        if (Math.Abs(width - _builtWidth) < 0.1 && _builtVersion == _owner!.TimelineVersion)
        {
            return;
        }
        _builtWidth = width;
        _builtVersion = _owner!.TimelineVersion;
        var right = width - 10;
        _curve.Clear();
        var previous = default(Point);
        var total = 0.0;
        for (var i = 0; i <= Samples; i++)
        {
            var p = PointAt(i / (double)Samples, right, timeline);
            if (i == 0)
            {
                _curve.MoveTo(p);
            }
            else
            {
                _curve.LineTo(p);
                var d = p - previous;
                total += Math.Sqrt(d.X * d.X + d.Y * d.Y);
            }
            _lengths[i] = total;
            previous = p;
        }
    }

    /// <summary>The curve's length from its start to the share <paramref name="x"/> of the training.</summary>
    private double LengthAt(double x)
    {
        var f = Math.Clamp(x, 0, 1) * Samples;
        var i = Math.Min((int)f, Samples - 1);
        return _lengths[i] + (_lengths[i + 1] - _lengths[i]) * (f - i);
    }

    private static Point PointAt(double x, double right, ModelTrainingTimeline timeline)
        => new(Left + (right - Left) * Math.Clamp(x, 0, 1), Y(timeline.LossAt(x), timeline));

    private static double Y(double loss, ModelTrainingTimeline timeline)
        => Axis - (Axis - Top) * Math.Clamp(loss / Math.Max(1e-6, timeline.ChartMax), 0, 1.05);

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;
}
