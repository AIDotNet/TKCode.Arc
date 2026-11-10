using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawn part of a <see cref="ModelTraining"/>: readouts (step, loss, learning rate), the data's batch cards, the
/// network (4 inputs, 5 hidden units, 3 classes; each connection's thickness and color show its weight), the prediction
/// bars, the loss curve and the phase rail. Wide (from 600px) the prediction and the curve stand in a column on the
/// right; narrower they sit side by side under the network. Drawn from the owner's scene at the current story time, so
/// pausing freezes it and stepping lands on the same picture. Frames allocate nothing: positions are solved when the size
/// changes, the curve's geometry when a point is added, and texts come from a cache.
/// </summary>
internal sealed class ModelTrainingStage : Control
{
    internal static readonly StyledProperty<IBrush?> ForwardInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(ForwardInk));

    internal static readonly StyledProperty<IBrush?> BackwardInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(BackwardInk));

    internal static readonly StyledProperty<IBrush?> NegativeInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(NegativeInk));

    internal static readonly StyledProperty<IBrush?> TargetInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(TargetInk));

    internal static readonly StyledProperty<IBrush?> NodeFillProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(NodeFill));

    internal static readonly StyledProperty<IBrush?> NodeBorderProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(NodeBorder));

    internal static readonly StyledProperty<IBrush?> TrackInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(TrackInk));

    internal static readonly StyledProperty<IBrush?> TextInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(TextInk));

    internal static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(SecondaryInk));

    internal static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(MutedInk));

    internal static readonly StyledProperty<IBrush?> AccentSubtleProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(AccentSubtle));

    internal static readonly StyledProperty<CornerRadius> CardRadiusProperty =
        AvaloniaProperty.Register<ModelTrainingStage, CornerRadius>(nameof(CardRadius), new CornerRadius(8));

    internal static readonly StyledProperty<CornerRadius> PillRadiusProperty =
        AvaloniaProperty.Register<ModelTrainingStage, CornerRadius>(nameof(PillRadius), new CornerRadius(999));

    internal static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<ModelTrainingStage>();

    /// <summary>The width from which the prediction and the curve stand in a column on the right.</summary>
    public const double WideWidth = 600;

    private const double ReadoutHeight = 40;
    private const double RailHeight = 30;
    private const double NodeRadius = 9;

    private readonly ExplainerTextCache _text = new(512);
    private readonly Dictionary<(IBrush, double, bool), IPen> _pens = new();
    private readonly List<Point> _nodes = new();
    private readonly List<(int From, int To)> _edges = new();
    private ModelTraining? _owner;
    private IReadOnlyList<double> _losses = [];
    private int _count;
    private Rect _data;
    private Rect _network;
    private Rect _prediction;
    private Rect _chart;
    private Rect _rail;
    private double _layoutWidth = -1;
    private bool _wide;
    private Geometry? _curve;
    private int _curvePoints = -1;
    private int _stepShown = -1;
    private string? _stepText;
    private double _lossShown = double.NaN;
    private string? _lossText;
    private double _rateShown = double.NaN;
    private string? _rateText;

    static ModelTrainingStage()
    {
        AffectsRender<ModelTrainingStage>(ForwardInkProperty, BackwardInkProperty, NegativeInkProperty, TargetInkProperty, NodeFillProperty,
            NodeBorderProperty, TrackInkProperty, TextInkProperty, SecondaryInkProperty, MutedInkProperty, AccentSubtleProperty,
            CardRadiusProperty, PillRadiusProperty);
        AffectsMeasure<ModelTrainingStage>(FontFamilyProperty);
    }

    public ModelTrainingStage()
    {
        ClipToBounds = false;
        ActualThemeVariantChanged += (_, _) =>
        {
            _text.Clear();
            _pens.Clear();
            InvalidateVisual();
        };
    }

    public IBrush? ForwardInk { get => GetValue(ForwardInkProperty); set => SetValue(ForwardInkProperty, value); }

    public IBrush? BackwardInk { get => GetValue(BackwardInkProperty); set => SetValue(BackwardInkProperty, value); }

    public IBrush? NegativeInk { get => GetValue(NegativeInkProperty); set => SetValue(NegativeInkProperty, value); }

    public IBrush? TargetInk { get => GetValue(TargetInkProperty); set => SetValue(TargetInkProperty, value); }

    public IBrush? NodeFill { get => GetValue(NodeFillProperty); set => SetValue(NodeFillProperty, value); }

    public IBrush? NodeBorder { get => GetValue(NodeBorderProperty); set => SetValue(NodeBorderProperty, value); }

    public IBrush? TrackInk { get => GetValue(TrackInkProperty); set => SetValue(TrackInkProperty, value); }

    public IBrush? TextInk { get => GetValue(TextInkProperty); set => SetValue(TextInkProperty, value); }

    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }

    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }

    public IBrush? AccentSubtle { get => GetValue(AccentSubtleProperty); set => SetValue(AccentSubtleProperty, value); }

    public CornerRadius CardRadius { get => GetValue(CardRadiusProperty); set => SetValue(CardRadiusProperty, value); }

    public CornerRadius PillRadius { get => GetValue(PillRadiusProperty); set => SetValue(PillRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>True while the prediction and the curve stand on the right.</summary>
    public bool IsWide => _wide;

    /// <summary>The network's node centers, inputs first (tests).</summary>
    public IReadOnlyList<Point> Nodes => _nodes;

    /// <summary>The loss curve's area (tests).</summary>
    public Rect ChartRect => _chart;

    /// <summary>The words a screen reader hears for the stage now.</summary>
    public string Summary { get; private set; } = string.Empty;

    public void Attach(ModelTraining owner)
    {
        _owner = owner;
        Changed();
    }

    public void SetRun(int count, IReadOnlyList<double> losses)
    {
        _count = count;
        _losses = losses;
        _curvePoints = -1;
        InvalidateVisual();
    }

    public void OnLabelsChanged()
    {
        _stepText = null;
        _lossText = null;
        _rateText = null;
        Changed();
    }

    public void Changed()
    {
        UpdateSummary();
        InvalidateVisual();
    }

    public void Tick() => InvalidateVisual();

    private void UpdateSummary()
    {
        if (_owner is null)
        {
            return;
        }
        var scene = _owner.Scene;
        var labels = _owner.Labels;
        var text = scene.Iteration == 0
            ? labels.ReadyCaption(_owner.StepCount)
            : labels.StepReadout(scene.Iteration, _owner.StepCount);
        if (scene.Phase is > ModelTrainingPhase.Ready and < ModelTrainingPhase.Done)
        {
            var names = labels.PhaseNames;
            var index = (int)scene.Phase - 1;
            if (index < names.Length)
            {
                text += ", " + names[index];
            }
        }
        if (!double.IsNaN(scene.Loss))
        {
            text += string.Create(CultureInfo.InvariantCulture, $", loss {scene.Loss:0.00}");
        }
        Summary = text;
        AutomationProperties.SetName(this, text);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 680;
        var body = width >= WideWidth ? 214 : 180 + 14 + 120;
        return new Size(width, ReadoutHeight + body + 16 + RailHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _layoutWidth) > 0.5)
        {
            Solve(finalSize.Width);
        }
        return finalSize;
    }

    private void Solve(double width)
    {
        _layoutWidth = width;
        _wide = width >= WideWidth;
        var top = ReadoutHeight;
        double bodyHeight;
        if (_wide)
        {
            bodyHeight = 214;
            _data = new Rect(0, top, 84, bodyHeight);
            var right = 210.0;
            _prediction = new Rect(width - right, top, right, 84);
            _chart = new Rect(width - right, top + 98, right, bodyHeight - 98);
            _network = new Rect(_data.Right + 24, top, Math.Max(80, width - right - 24 - _data.Right - 24), bodyHeight);
        }
        else
        {
            bodyHeight = 180 + 14 + 120;
            _data = new Rect(0, top, 64, 180);
            _network = new Rect(_data.Right + 16, top, Math.Max(80, width - _data.Right - 16), 180);
            var half = (width - 14) / 2;
            _prediction = new Rect(0, top + 194, half, 120);
            _chart = new Rect(half + 14, top + 194, half, 120);
        }
        _rail = new Rect(0, top + bodyHeight + 16, width, RailHeight);
        // The network: three columns of nodes, spread evenly, each column centered.
        _nodes.Clear();
        _edges.Clear();
        var sizes = ModelTraining.LayerSizes;
        var inner = _network.Deflate(new Thickness(NodeRadius + 2, 22, NodeRadius + 2, 8));
        for (var layer = 0; layer < sizes.Length; layer++)
        {
            var x = inner.X + inner.Width * layer / (sizes.Length - 1);
            var spacing = Math.Min(40, inner.Height / Math.Max(1, sizes[layer]));
            var startY = inner.Center.Y - spacing * (sizes[layer] - 1) / 2;
            for (var i = 0; i < sizes[layer]; i++)
            {
                _nodes.Add(new Point(x, startY + i * spacing));
            }
        }
        var offset = 0;
        for (var layer = 0; layer < sizes.Length - 1; layer++)
        {
            var next = offset + sizes[layer];
            for (var a = 0; a < sizes[layer]; a++)
            {
                for (var b = 0; b < sizes[layer + 1]; b++)
                {
                    _edges.Add((offset + a, next + b));
                }
            }
            offset = next;
        }
        _curvePoints = -1;
    }

    /// <summary>The layer of a node index.</summary>
    private static int LayerOf(int node)
    {
        var sizes = ModelTraining.LayerSizes;
        var sum = 0;
        for (var layer = 0; layer < sizes.Length; layer++)
        {
            sum += sizes[layer];
            if (node < sum)
            {
                return layer;
            }
        }
        return sizes.Length - 1;
    }

    private IPen? Pen(IBrush? brush, double thickness, bool dashed = false)
    {
        if (brush is null)
        {
            return null;
        }
        thickness = Math.Round(thickness * 4) / 4;
        if (!_pens.TryGetValue((brush, thickness, dashed), out var pen))
        {
            pen = dashed
                ? new ImmutablePen(brush.ToImmutable(), thickness, new ImmutableDashStyle([2, 3], 0), PenLineCap.Round)
                : new ImmutablePen(brush.ToImmutable(), thickness, null, PenLineCap.Round, PenLineJoin.Round);
            _pens[(brush, thickness, dashed)] = pen;
        }
        return pen;
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;

    /// <summary>A soft bump: 1 at 0, 0 beyond ±<paramref name="width"/>.</summary>
    private static double Bump(double x, double width)
    {
        var d = Math.Abs(x) / width;
        return d >= 1 ? 0 : 0.5 + 0.5 * Math.Cos(d * Math.PI);
    }

    public override void Render(DrawingContext context)
    {
        if (_owner is null || _layoutWidth < 0)
        {
            return;
        }
        var scene = _owner.Scene;
        var now = _owner.StoryTime;
        var animates = _owner.Animates;
        var typeface = new Typeface(FontFamily);
        var progress = animates ? Math.Clamp((now - scene.PhaseStart) / Math.Max(0.001, scene.PhaseDuration), 0, 1) : 1;
        DrawReadouts(context, scene, typeface);
        DrawData(context, scene, progress, animates, typeface);
        DrawNetwork(context, scene, now, progress, animates, typeface);
        DrawPrediction(context, scene, now, animates, typeface);
        DrawChart(context, scene, now, animates, typeface);
        DrawRail(context, scene, typeface);
    }

    private void DrawReadouts(DrawingContext context, ModelTrainingScene scene, Typeface typeface)
    {
        var labels = _owner!.Labels;
        var text = ColorOf(TextInk);
        var muted = ColorOf(MutedInk);
        var step = Math.Max(1, scene.Iteration);
        if (_stepText is null || step != _stepShown)
        {
            _stepShown = step;
            _stepText = labels.StepReadout(scene.Iteration, _owner.StepCount);
        }
        if (_lossText is null || !scene.Loss.Equals(_lossShown))
        {
            _lossShown = scene.Loss;
            _lossText = double.IsNaN(scene.Loss)
                ? labels.LossCurve + " –"
                : string.Create(CultureInfo.InvariantCulture, $"{labels.LossCurve} {scene.Loss:0.000}");
        }
        if (_rateText is null || !_owner.LearningRate.Equals(_rateShown))
        {
            _rateShown = _owner.LearningRate;
            _rateText = labels.RateReadout(_owner.LearningRate);
        }
        var x = 0.0;
        var stepLayout = _text.Get(_stepText, typeface, 15, text);
        stepLayout.Draw(context, new Point(x, 6));
        x += stepLayout.Width + 24;
        var lossLayout = _text.Get(_lossText, typeface, 15, text);
        lossLayout.Draw(context, new Point(x, 6));
        x += lossLayout.Width + 24;
        var rate = _text.Get(_rateText, typeface, 12, muted);
        if (x + rate.Width <= _layoutWidth)
        {
            rate.Draw(context, new Point(x, 9));
        }
    }

    private void DrawData(DrawingContext context, ModelTrainingScene scene, double progress, bool animates, Typeface typeface)
    {
        var labels = _owner!.Labels;
        var muted = ColorOf(MutedInk);
        _text.GetTrimmed(labels.Data, typeface, 12, muted, _data.Width).Draw(context, new Point(_data.X, _data.Y));
        var used = scene.Iteration - (scene.Phase == ModelTrainingPhase.Batch && progress < 1 ? 1 : 0);
        if (scene.Finished)
        {
            used = _count;
        }
        var left = Math.Max(0, _count - used);
        var cardWidth = _data.Width - 12;
        var cardHeight = _wide ? 40.0 : 34.0;
        var radius = Math.Min(CardRadius.TopLeft, cardHeight / 2);
        var shown = Math.Min(4, left);
        var baseY = _data.Y + 24;
        for (var i = shown - 1; i >= 0; i--)
        {
            var rect = new Rect(_data.X + i * 4, baseY + i * 6, cardWidth, cardHeight);
            context.DrawRectangle(NodeFill, Pen(NodeBorder, 1), rect, radius, radius);
            if (i == 0)
            {
                DrawCardFace(context, rect, used + 1, typeface);
            }
        }
        // The batch in flight: the top card slides into the input layer and fades as it arrives.
        if (scene.Phase == ModelTrainingPhase.Batch && animates && progress < 1 && _nodes.Count > 0)
        {
            var eased = ArcEasings.InOut.Ease(progress);
            var from = new Point(_data.X, baseY);
            var to = new Point(_nodes[0].X - cardWidth / 2, _network.Center.Y - cardHeight / 2);
            var rect = new Rect(from.X + (to.X - from.X) * eased, from.Y + (to.Y - from.Y) * eased, cardWidth, cardHeight);
            using (context.PushOpacity(1 - Math.Max(0, eased - 0.6) / 0.4))
            {
                context.DrawRectangle(NodeFill, Pen(ForwardInk, 1.5), rect, radius, radius);
                DrawCardFace(context, rect, scene.Iteration, typeface);
            }
        }
    }

    private void DrawCardFace(DrawingContext context, Rect rect, int batch, Typeface typeface)
    {
        if (batch < 1 || batch > _count)
        {
            return;
        }
        var label = _text.GetTrimmed(_owner!.Labels.BatchName(batch), typeface, 11, ColorOf(SecondaryInk), Math.Max(10, rect.Width - 12));
        label.Draw(context, new Point(rect.X + 7, rect.Y + 4));
        // Four examples as small dots.
        for (var i = 0; i < 4; i++)
        {
            var c = new Point(rect.X + 10 + i * 9, rect.Bottom - 9);
            context.DrawEllipse(i == ModelTraining.TargetClass ? TargetInk : MutedInk, null, c, 2.5, 2.5);
        }
    }

    private void DrawNetwork(DrawingContext context, ModelTrainingScene scene, double now, double progress, bool animates, Typeface typeface)
    {
        var labels = _owner!.Labels;
        _text.GetTrimmed(labels.Model, typeface, 12, ColorOf(MutedInk), _network.Width).Draw(context, new Point(_network.X, _network.Y));
        if (_nodes.Count == 0)
        {
            return;
        }
        var updating = scene.Phase == ModelTrainingPhase.Update;
        var mix = updating ? (animates ? ArcEasings.Standard.Ease(progress) : 1) : 1;
        var before = scene.Before;
        var after = scene.After;
        // Forward: a wave from the inputs (0) to the outputs (2); backward: the other way.
        var forward = scene.Phase == ModelTrainingPhase.Forward && animates && progress < 1;
        var backward = scene.Phase == ModelTrainingPhase.Backward && animates && progress < 1;
        var wave = forward ? progress * 2.4 - 0.2 : backward ? 2.2 - progress * 2.4 : double.NaN;
        for (var i = 0; i < _edges.Count && i < after.Length; i++)
        {
            var (a, b) = _edges[i];
            var w = before.Length == after.Length ? before[i] + (after[i] - before[i]) * mix : after[i];
            var magnitude = Math.Min(1, Math.Abs(w));
            var ink = w >= 0 ? ForwardInk : NegativeInk;
            var pa = _nodes[a];
            var pb = _nodes[b];
            using (context.PushOpacity(0.18 + 0.5 * magnitude))
            {
                context.DrawLine(Pen(ink, 0.5 + 2.5 * magnitude)!, pa, pb);
            }
            if (!double.IsNaN(wave))
            {
                // The signal on this connection: a dot that runs along it as the wave crosses its layer.
                var layer = LayerOf(a);
                var t = Math.Clamp(forward ? wave - layer : layer + 1 - wave, 0, 1);
                if (t > 0 && t < 1)
                {
                    var p = forward ? t : 1 - t;
                    var at = new Point(pa.X + (pb.X - pa.X) * p, pa.Y + (pb.Y - pa.Y) * p);
                    using (context.PushOpacity(0.35 + 0.65 * magnitude))
                    {
                        context.DrawEllipse(forward ? ForwardInk : BackwardInk, null, at, 2.5, 2.5);
                    }
                }
            }
            if (updating && animates && progress < 1)
            {
                // The update: each connection glows once as it changes.
                var glow = Bump(progress - 0.5, 0.5) * Math.Min(1, Math.Abs(after[i] - before[i]) * 4);
                if (glow > 0.02)
                {
                    using (context.PushOpacity(glow * 0.6))
                    {
                        context.DrawLine(Pen(BackwardInk, 1.5)!, pa, pb);
                    }
                }
            }
        }
        for (var n = 0; n < _nodes.Count; n++)
        {
            var center = _nodes[n];
            context.DrawEllipse(NodeFill, Pen(NodeBorder, 1), center, NodeRadius, NodeRadius);
            var layer = LayerOf(n);
            var lit = double.IsNaN(wave) ? 0 : Bump(wave - layer, 0.55);
            if (scene.Phase == ModelTrainingPhase.Batch && layer == 0 && animates)
            {
                lit = Math.Max(lit, Bump(progress - 1, 0.35));
            }
            if (scene.Phase == ModelTrainingPhase.Loss && layer == 2 && n - (_nodes.Count - 3) == ModelTraining.TargetClass)
            {
                lit = 1;
            }
            if (lit > 0.01)
            {
                var ink = backward ? BackwardInk : scene.Phase == ModelTrainingPhase.Loss ? TargetInk : ForwardInk;
                using (context.PushOpacity(lit))
                {
                    context.DrawEllipse(ink, null, center, NodeRadius - 3, NodeRadius - 3);
                }
            }
        }
    }

    /// <summary>The probabilities of the three classes for a loss: the right one gets e^−loss, the others share the rest.</summary>
    private static double ClassProbability(int index, double loss)
    {
        var p = ModelTraining.TargetProbability(loss);
        if (index == ModelTraining.TargetClass)
        {
            return p;
        }
        return (1 - p) * (index == 0 ? 0.6 : 0.4);
    }

    private void DrawPrediction(DrawingContext context, ModelTrainingScene scene, double now, bool animates, Typeface typeface)
    {
        var labels = _owner!.Labels;
        var muted = ColorOf(MutedInk);
        _text.GetTrimmed(labels.Prediction, typeface, 12, muted, _prediction.Width).Draw(context, new Point(_prediction.X, _prediction.Y));
        var loss = scene.Loss;
        var grow = double.IsNaN(loss) ? 0 : animates ? ArcEasings.Standard.Ease(Math.Clamp((now - scene.LossAt) / 0.5, 0, 1)) : 1;
        var top = _prediction.Y + 22;
        var rowHeight = _wide ? 20.0 : 26.0;
        var trackX = _prediction.X + 18;
        var trackWidth = Math.Max(20, _prediction.Width - 18 - 52);
        for (var i = 0; i < 3; i++)
        {
            var y = top + i * rowHeight;
            var name = i switch
            {
                0 => "A",
                1 => "B",
                _ => "C"
            };
            _text.Get(name, typeface, 11, muted).Draw(context, new Point(_prediction.X, y));
            var track = new Rect(trackX, y + 4, trackWidth, 7);
            var r = Math.Min(PillRadius.TopLeft, track.Height / 2);
            context.DrawRectangle(TrackInk, null, track, r, r);
            if (!double.IsNaN(loss))
            {
                var p = ClassProbability(i, loss);
                var fill = new Rect(track.X, track.Y, Math.Max(track.Height, track.Width * p * grow), track.Height);
                using (context.PushOpacity(i == ModelTraining.TargetClass ? 1 : 0.55))
                {
                    context.DrawRectangle(i == ModelTraining.TargetClass ? TargetInk : ForwardInk, null, fill, r, r);
                }
            }
            if (i == ModelTraining.TargetClass)
            {
                _text.Get(labels.Target, typeface, 11, ColorOf(TargetInk)).Draw(context, new Point(track.Right + 6, y));
            }
        }
    }

    private void DrawChart(DrawingContext context, ModelTrainingScene scene, double now, bool animates, Typeface typeface)
    {
        var labels = _owner!.Labels;
        var muted = ColorOf(MutedInk);
        _text.GetTrimmed(labels.LossCurve, typeface, 12, muted, _chart.Width).Draw(context, new Point(_chart.X, _chart.Y));
        var plot = new Rect(_chart.X, _chart.Y + 20, _chart.Width, Math.Max(10, _chart.Height - 24));
        context.DrawLine(Pen(TrackInk, 1)!, plot.BottomLeft, plot.BottomRight);
        context.DrawLine(Pen(TrackInk, 1, dashed: true)!, plot.TopLeft, plot.TopRight);
        if (_count < 2 || _losses.Count < _count)
        {
            return;
        }
        var max = 0.0;
        for (var i = 0; i < _count; i++)
        {
            max = Math.Max(max, _losses[i]);
        }
        max = Math.Max(0.5, max * 1.1);
        var plotted = Math.Min(scene.Plotted, _count);
        if (plotted != _curvePoints)
        {
            _curvePoints = plotted;
            _curve = null;
            if (plotted >= 2)
            {
                var geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(PointOf(plot, 0, max), false);
                    for (var i = 1; i < plotted; i++)
                    {
                        g.LineTo(PointOf(plot, i, max));
                    }
                    g.EndFigure(false);
                }
                _curve = geometry;
            }
        }
        if (_curve is not null)
        {
            context.DrawGeometry(null, Pen(ForwardInk, 2), _curve);
        }
        for (var i = 0; i < plotted; i++)
        {
            var newest = i == plotted - 1;
            var appear = newest && animates ? Math.Clamp((now - scene.PlottedAt) / 0.3, 0, 1) : 1;
            var radius = newest ? 3.5 + (1 - appear) * 3 : 2.5;
            using (context.PushOpacity(appear))
            {
                context.DrawEllipse(newest ? ForwardInk : SecondaryInk, null, PointOf(plot, i, max), radius, radius);
            }
        }
    }

    private Point PointOf(Rect plot, int index, double max)
        => new(plot.X + 4 + (plot.Width - 8) * index / Math.Max(1, _count - 1), plot.Bottom - plot.Height * Math.Clamp(_losses[index] / max, 0, 1));

    private void DrawRail(DrawingContext context, ModelTrainingScene scene, Typeface typeface)
    {
        var names = _owner!.Labels.PhaseNames;
        var gap = 6.0;
        var width = (_rail.Width - gap * 4) / 5;
        var current = scene.Phase is > ModelTrainingPhase.Ready and < ModelTrainingPhase.Done ? (int)scene.Phase - 1 : -1;
        for (var i = 0; i < 5; i++)
        {
            var rect = new Rect(_rail.X + i * (width + gap), _rail.Y, width, _rail.Height);
            var r = Math.Min(PillRadius.TopLeft, rect.Height / 2);
            var isCurrent = i == current;
            var isDone = scene.Finished || (current >= 0 && i < current);
            if (isCurrent)
            {
                context.DrawRectangle(AccentSubtle, Pen(ForwardInk, 1), rect, r, r);
            }
            else
            {
                context.DrawRectangle(null, Pen(NodeBorder, 1), rect, r, r);
            }
            var name = i < names.Length ? names[i] : string.Empty;
            var color = ColorOf(isCurrent ? TextInk : isDone ? SecondaryInk : MutedInk);
            var layout = _text.GetTrimmed(name, typeface, 12, color, Math.Max(10, rect.Width - 12));
            layout.Draw(context, new Point(rect.Center.X - layout.Width / 2, rect.Center.Y - layout.Height / 2));
        }
    }
}
