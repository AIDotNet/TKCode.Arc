using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>What the pointer rests on in the diagram (each shows a detail tag and slows the run).</summary>
internal enum ModelTrainingRegion
{
    None,
    TrainingSet,
    HeldOutSet,
    Layer0,
    Layer1,
    Layer2,
    Layer3,
    Layer4
}

/// <summary>
/// The drawn diagram of a <see cref="ModelTraining"/> (Arc Model training, design reference): the training and held-out
/// data as stacks of sheets (one bar per input feature), the network on 2.5D plates with S-curved connections whose
/// width, opacity and colour show how much each one has learned, signals riding the connections forward, the error
/// riding them back as a short dash, units lighting and ringing, the two class probabilities with bars, the layer names,
/// and a detail tag for the stack or layer under a mouse. Everything is a function of the owner's run time, so pausing
/// freezes it and a picked step shows the same picture every time. Frames allocate nothing: the layout and the curves
/// are solved when the width or the network changes, pens and brushes are cached, texts come from a cache.
/// </summary>
internal sealed class ModelTrainingStage : Control
{
    internal static readonly StyledProperty<IBrush?> SheetInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(SheetInk));

    internal static readonly StyledProperty<IBrush?> PlateBackInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(PlateBackInk));

    internal static readonly StyledProperty<IBrush?> PlateFaceInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(PlateFaceInk));

    internal static readonly StyledProperty<IBrush?> BorderInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(BorderInk));

    internal static readonly StyledProperty<IBrush?> StrongBorderInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(StrongBorderInk));

    internal static readonly StyledProperty<IBrush?> SubtleBorderInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(SubtleBorderInk));

    internal static readonly StyledProperty<IBrush?> TextInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(TextInk));

    internal static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(SecondaryInk));

    internal static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(MutedInk));

    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(AccentInk));

    internal static readonly StyledProperty<IBrush?> BackgroundInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(BackgroundInk));

    internal static readonly StyledProperty<IBrush?> SurfaceMutedInkProperty =
        AvaloniaProperty.Register<ModelTrainingStage, IBrush?>(nameof(SurfaceMutedInk));

    internal static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<ModelTrainingStage>();

    private static readonly double[] s_barShares = [0.78, 0.92, 0.56, 0.84, 0.66];

    private readonly ExplainerTextCache _text = new(512);
    private readonly ModelTrainingLayout _layout = new();
    private readonly Dictionary<int, IPen> _pens = new();
    private Pen[] _dashPens = [];
    private ModelTraining? _owner;
    private ModelTrainingRegion _hover;
    private ImmutableDashStyle? _heldOutDash;

    static ModelTrainingStage()
    {
        AffectsRender<ModelTrainingStage>(SheetInkProperty, PlateBackInkProperty, PlateFaceInkProperty, BorderInkProperty,
            StrongBorderInkProperty, SubtleBorderInkProperty, TextInkProperty, SecondaryInkProperty, MutedInkProperty,
            AccentInkProperty, BackgroundInkProperty, SurfaceMutedInkProperty, FontFamilyProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<ModelTrainingStage>(AutomationControlType.Image);
        ClipToBoundsProperty.OverrideDefaultValue<ModelTrainingStage>(false);
    }

    public ModelTrainingStage()
    {
        ActualThemeVariantChanged += (_, _) =>
        {
            ClearCaches();
            InvalidateVisual();
        };
    }

    public IBrush? SheetInk { get => GetValue(SheetInkProperty); set => SetValue(SheetInkProperty, value); }

    public IBrush? PlateBackInk { get => GetValue(PlateBackInkProperty); set => SetValue(PlateBackInkProperty, value); }

    public IBrush? PlateFaceInk { get => GetValue(PlateFaceInkProperty); set => SetValue(PlateFaceInkProperty, value); }

    public IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }

    public IBrush? StrongBorderInk { get => GetValue(StrongBorderInkProperty); set => SetValue(StrongBorderInkProperty, value); }

    public IBrush? SubtleBorderInk { get => GetValue(SubtleBorderInkProperty); set => SetValue(SubtleBorderInkProperty, value); }

    public IBrush? TextInk { get => GetValue(TextInkProperty); set => SetValue(TextInkProperty, value); }

    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }

    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    public IBrush? BackgroundInk { get => GetValue(BackgroundInkProperty); set => SetValue(BackgroundInkProperty, value); }

    public IBrush? SurfaceMutedInk { get => GetValue(SurfaceMutedInkProperty); set => SetValue(SurfaceMutedInkProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    internal ModelTrainingLayout Layout => _layout;

    internal ModelTrainingRegion Hover => _hover;

    /// <summary>Texts built so far (tests: a steady run stops growing this).</summary>
    internal int TextMisses => _text.Misses;

    internal void Attach(ModelTraining owner)
    {
        _owner = owner;
        NetworkChanged();
    }

    /// <summary>The layer sizes or the seed changed: lay out again.</summary>
    internal void NetworkChanged()
    {
        if (_owner is null)
        {
            return;
        }
        _layout.SetNetwork(_owner.LayerSizes, _owner.WeightSeed);
        _dashPens = [];
        InvalidateMeasure();
        InvalidateVisual();
    }

    internal void ClearCaches()
    {
        _text.Clear();
        _pens.Clear();
        _dashPens = [];
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        _layout.Solve(width);
        return new Size(width, _layout.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_layout.Solve(finalSize.Width))
        {
            _dashPens = [];
        }
        return new Size(finalSize.Width, _layout.Height);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SetHover(e.Pointer.Type == PointerType.Mouse ? HitTestRegion(e.GetPosition(this)) : ModelTrainingRegion.None);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(ModelTrainingRegion.None);
    }

    /// <summary>The stack or layer at <paramref name="point"/>.</summary>
    internal ModelTrainingRegion HitTestRegion(Point point)
    {
        if (_layout.TrainingHit.Contains(point))
        {
            return ModelTrainingRegion.TrainingSet;
        }
        if (_layout.HeldOutHit.Contains(point))
        {
            return ModelTrainingRegion.HeldOutSet;
        }
        for (var l = 0; l < _layout.LayerCount && l < 5; l++)
        {
            if (_layout.LayerHit(l).Contains(point))
            {
                return ModelTrainingRegion.Layer0 + l;
            }
        }
        return ModelTrainingRegion.None;
    }

    internal void SetHover(ModelTrainingRegion region)
    {
        if (region == _hover)
        {
            return;
        }
        _hover = region;
        _owner?.OnHoverChanged(region != ModelTrainingRegion.None);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_owner is null || _layout.Width <= 0)
        {
            return;
        }
        var t = _owner.RenderTime;
        var timeline = _owner.Timeline;
        var ink = new Inks(this, _owner.Accent);
        DrawStack(context, ink, timeline, t, heldOut: false);
        DrawStack(context, ink, timeline, t, heldOut: true);
        DrawPlates(context, ink);
        DrawEdges(context, ink, timeline, t);
        DrawBackward(context, ink, timeline, t);
        DrawForward(context, ink, timeline, t);
        DrawNodes(context, ink, timeline, t);
        DrawLabels(context, ink);
        DrawOutputs(context, ink, timeline, t);
        DrawFlight(context, ink, timeline, t);
        DrawTag(context, ink);
    }

    // ---- Data stacks ------------------------------------------------------------------------------------------------

    private void DrawStack(DrawingContext context, in Inks ink, ModelTrainingTimeline timeline, double t, bool heldOut)
    {
        var count = heldOut ? _layout.HeldOutSheets : _layout.TrainingSheets;
        var f = FlightOf(timeline, t, heldOut, out _);
        var hovered = _hover == (heldOut ? ModelTrainingRegion.HeldOutSet : ModelTrainingRegion.TrainingSet);
        // The hidden sheet behind the stack fades in while the others move up a slot.
        if (f > 0)
        {
            DrawSheet(context, ink, _layout.Sheet(heldOut, count - f), 1, f, heldOut, hovered);
        }
        for (var i = count - 1; i >= 1; i--)
        {
            DrawSheet(context, ink, _layout.Sheet(heldOut, i - f), 1, 1, heldOut, hovered);
        }
        if (f <= 0)
        {
            DrawSheet(context, ink, _layout.Sheet(heldOut, 0), 1, 1, heldOut, hovered);
        }
        var origin = heldOut ? _layout.HeldOutText : _layout.TrainingText;
        var owner = _owner!;
        var labels = owner.Labels;
        var name = heldOut ? labels.HeldOutSet : labels.TrainingSet;
        var meta = labels.SampleCount(heldOut ? owner.HeldOutSamples : owner.Samples, labels.Unit);
        DrawTextAtBaseline(context, name, ink.Text, origin.X, origin.Y, medium: true);
        DrawTextAtBaseline(context, meta, ink.Muted, origin.X, origin.Y + 15, medium: false);
    }

    /// <summary>The eased progress (0..1) of the stack's sheet flight at <paramref name="t"/>, 0 when none flies.</summary>
    private static double FlightOf(ModelTrainingTimeline timeline, double t, bool heldOut, out double linear)
    {
        var kind = heldOut ? ModelTrainingPassKind.HeldOutFlight : ModelTrainingPassKind.TrainingFlight;
        foreach (var pass in timeline.Passes)
        {
            if (pass.Kind == kind && t >= pass.Start && t < pass.End)
            {
                linear = pass.Progress(t);
                return ModelTrainingEase.InOutSine(linear);
            }
        }
        linear = 0;
        return 0;
    }

    private void DrawSheet(DrawingContext context, in Inks ink, Point center, double scale, double alpha, bool heldOut, bool hovered)
    {
        if (alpha <= 0.01)
        {
            return;
        }
        var w = _layout.SheetWidth * scale;
        var h = _layout.SheetHeight * scale;
        var rect = new Rect(center.X - w / 2, center.Y - h / 2, w, h);
        var radius = _layout.SheetRadius * scale;
        var strokeColor = hovered ? ArcColor.Lerp(ink.Border, ink.Accent, 0.6) : heldOut ? ink.StrongBorder : ink.Border;
        using (context.PushOpacity(alpha))
        {
            var pen = heldOut ? DashedPen(strokeColor) : Pen(strokeColor, 1, 1);
            context.DrawRectangle(_text.Brush(ink.Sheet), pen, new RoundedRect(rect, radius));
            var bars = Math.Clamp(_layout.Sizes[0], 1, 5);
            var inner = w - 22 * scale;
            var span = bars > 1 ? 0.5 * h / (bars - 1) : 0;
            var barHeight = 3.5 * scale;
            for (var b = 0; b < bars; b++)
            {
                var bar = new Rect(rect.X + 11 * scale, center.Y - 0.277 * h + b * span, inner * s_barShares[b], barHeight);
                context.DrawRectangle(_text.Brush(ink.Muted, b == 0 ? 0.75 : 0.45), null, new RoundedRect(bar, barHeight / 2));
            }
        }
    }

    private void DrawFlight(DrawingContext context, in Inks ink, ModelTrainingTimeline timeline, double t)
    {
        for (var s = 0; s < 2; s++)
        {
            var heldOut = s == 1;
            var f = FlightOf(timeline, t, heldOut, out var linear);
            if (f <= 0)
            {
                continue;
            }
            var start = _layout.Sheet(heldOut, 0);
            var target = _layout.Node(0, (_layout.Sizes[0] - 1) / 2);
            var d = target - start;
            // An arc that lifts a little first, then drops into the input layer.
            var c1 = new Point(start.X + d.X * 0.35, start.Y - Math.Max(14, Math.Abs(d.Y) * 0.2));
            var c2 = new Point(target.X - d.X * 0.2, target.Y - d.Y * 0.45);
            var p = Cubic(start, c1, c2, target, f);
            var scale = 1 - 0.66 * f;
            var alpha = linear < 0.55 ? 1 : Math.Clamp(1 - (linear - 0.55) / 0.33, 0, 1);
            DrawSheet(context, ink, p, scale, alpha, heldOut, hovered: false);
        }
    }

    private static Point Cubic(Point a, Point b, Point c, Point d, double u)
    {
        var v = 1 - u;
        var w0 = v * v * v;
        var w1 = 3 * v * v * u;
        var w2 = 3 * v * u * u;
        var w3 = u * u * u;
        return new Point(a.X * w0 + b.X * w1 + c.X * w2 + d.X * w3, a.Y * w0 + b.Y * w1 + c.Y * w2 + d.Y * w3);
    }

    // ---- Network ----------------------------------------------------------------------------------------------------

    private void DrawPlates(DrawingContext context, in Inks ink)
    {
        var border = Pen(ink.Border, 1, 1);
        for (var l = 0; l < _layout.LayerCount; l++)
        {
            var face = _layout.Plate(l);
            var back = face.Translate(_layout.PlateDepth);
            var radius = _layout.PlateWidth / 2;
            context.DrawRectangle(_text.Brush(ink.PlateBack), border, new RoundedRect(back, radius));
            var hovered = _hover == ModelTrainingRegion.Layer0 + l;
            var facePen = hovered ? Pen(ArcColor.Lerp(ink.Border, ink.Accent, 0.6), 1, 1) : border;
            context.DrawRectangle(_text.Brush(ink.PlateFace), facePen, new RoundedRect(face, radius));
        }
    }

    private void DrawEdges(DrawingContext context, in Inks ink, ModelTrainingTimeline timeline, double t)
    {
        Span<double> share = stackalloc double[8];
        for (var g = 0; g < Math.Min(8, _layout.LayerCount - 1); g++)
        {
            share[g] = timeline.LearnedShare(timeline.UpdatesAt(g, t));
        }
        foreach (var edge in _layout.Edges)
        {
            var s = share[Math.Min(edge.Gap, 7)];
            var k = edge.Learned * s;
            var width = edge.Learned > 0
                ? edge.StartWidth + (edge.LearnedWidth - edge.StartWidth) * k
                : edge.StartWidth * (1 - 0.18 * s);
            var alpha = Math.Clamp(0.053 + 0.413 * width, 0, 1);
            var color = ArcColor.Lerp(ink.Muted, ink.Accent, Quantize(0.8 * k, 20));
            context.DrawGeometry(null, Pen(color, Quantize(width, 20), alpha), edge.Path.Geometry);
        }
    }

    private void DrawBackward(DrawingContext context, in Inks ink, ModelTrainingTimeline timeline, double t)
    {
        EnsureDashPens(ink.Accent);
        foreach (var pass in timeline.Passes)
        {
            if (pass.Kind != ModelTrainingPassKind.Backward || !pass.IsActive(t))
            {
                continue;
            }
            var q = pass.Progress(t);
            var e = ModelTrainingEase.InOutSine(q);
            var alpha = 0.62 * Fade(q, 0.1, 0.12);
            var head = 1 - 1.2 * e;
            for (var i = 0; i < _layout.Edges.Count; i++)
            {
                var edge = _layout.Edges[i];
                if (edge.Gap != pass.Gap)
                {
                    continue;
                }
                var length = edge.Path.Length;
                if (edge.Path.ShowPart(_dashPens[i], Math.Max(0, head) * length, Math.Min(1, head + 0.2) * length))
                {
                    using (context.PushOpacity(alpha))
                    {
                        context.DrawGeometry(null, _dashPens[i], edge.Path.Geometry);
                    }
                }
            }
        }
    }

    private void DrawForward(DrawingContext context, in Inks ink, ModelTrainingTimeline timeline, double t)
    {
        var radius = _layout.IsNarrow ? 2.5 : 3;
        foreach (var pass in timeline.Passes)
        {
            if (pass.Kind != ModelTrainingPassKind.Forward || !pass.IsActive(t))
            {
                continue;
            }
            var q = pass.Progress(t);
            var e = ModelTrainingEase.InOutSine(q);
            var fade = Fade(q, 0.12, 0.16);
            var share = timeline.LearnedShare(timeline.UpdatesAt(pass.Gap, t));
            foreach (var edge in _layout.Edges)
            {
                if (edge.Gap != pass.Gap)
                {
                    continue;
                }
                var width = edge.Learned > 0
                    ? edge.StartWidth + (edge.LearnedWidth - edge.StartWidth) * edge.Learned * share
                    : edge.StartWidth;
                var alpha = Math.Clamp(0.15 + 0.413 * width, 0, 1) * fade;
                var p = edge.Path.PointAtFraction(e);
                context.DrawEllipse(_text.Brush(ink.Accent, alpha), null, p, radius, radius);
            }
        }
    }

    private void DrawNodes(DrawingContext context, in Inks ink, ModelTrainingTimeline timeline, double t)
    {
        var r = _layout.NodeRadius;
        var stroke = Pen(ink.NodeStroke, 1.25, 1);
        var nodeFill = _text.Brush(ink.Sheet);
        var winnerFloor = t >= timeline.FirstMeasure || t < ModelTrainingTimeline.ResetLength
            ? 0.85 * timeline.LearnedShare(timeline.UpdatesAt(_layout.LayerCount - 2, t))
            : 0;
        for (var l = 0; l < _layout.LayerCount; l++)
        {
            var act = ActOf(timeline, l, t);
            var (ringRadius, ringAlpha) = RingOf(timeline, l, t);
            var n = _layout.Sizes[l];
            for (var u = 0; u < n; u++)
            {
                var c = _layout.Node(l, u);
                if (ringAlpha > 0.01)
                {
                    context.DrawEllipse(null, Pen(ink.Accent, 1.25, ringAlpha), c, ringRadius, ringRadius);
                }
                context.DrawEllipse(nodeFill, stroke, c, r, r);
                var a = act;
                if (l == _layout.LayerCount - 1 && u == 0)
                {
                    a = Math.Max(a, winnerFloor);
                }
                if (a > 0.01)
                {
                    var inner = r - 2;
                    context.DrawEllipse(_text.Brush(ink.Accent, a), null, c, inner, inner);
                }
            }
        }
    }

    /// <summary>How lit layer <paramref name="layer"/>'s units are: a flash when a batch or a forward pass lands.</summary>
    private static double ActOf(ModelTrainingTimeline timeline, int layer, double t)
    {
        var act = 0.0;
        foreach (var pass in timeline.Passes)
        {
            double land;
            if (pass.Kind is ModelTrainingPassKind.TrainingFlight or ModelTrainingPassKind.HeldOutFlight)
            {
                if (layer != 0)
                {
                    continue;
                }
                land = pass.End;
            }
            else if (pass.Kind == ModelTrainingPassKind.Forward && pass.Gap + 1 == layer)
            {
                land = pass.End;
            }
            else
            {
                continue;
            }
            var d = t - land;
            if (d < 0 || d > 0.7)
            {
                continue;
            }
            var a = d < 0.1 ? 0.7 * d / 0.1 : 0.7 * (1 - (d - 0.1) / 0.55);
            act = Math.Max(act, a);
        }
        return Math.Max(0, act);
    }

    /// <summary>The ring around layer <paramref name="layer"/>'s units as the error passes through it.</summary>
    private (double Radius, double Alpha) RingOf(ModelTrainingTimeline timeline, int layer, double t)
    {
        var best = 2.0;
        foreach (var pass in timeline.Passes)
        {
            if (pass.Kind != ModelTrainingPassKind.Backward)
            {
                continue;
            }
            if (pass.Gap + 1 == layer)
            {
                best = Math.Min(best, Ring(t - pass.Start));
            }
            if (pass.Gap == 0 && layer == 0)
            {
                best = Math.Min(best, Ring(t - pass.End));
            }
        }
        if (best >= 1)
        {
            return (0, 0);
        }
        var r = _layout.NodeRadius;
        return (r + 7 * ModelTrainingEase.OutCubic(best), 0.57 * (1 - best));

        static double Ring(double d) => d < 0 || d > 0.55 ? 2 : d / 0.55;
    }

    private void DrawLabels(DrawingContext context, in Inks ink)
    {
        var labels = _owner!.Labels;
        for (var l = 0; l < _layout.LayerCount; l++)
        {
            var name = l == 0 ? labels.Input : l == _layout.LayerCount - 1 ? labels.Output : labels.Hidden;
            var layout = _text.Get(name, Face(false), 12, ink.Secondary);
            DrawLayout(context, layout, _layout.LayerX(l) - layout.WidthIncludingTrailingWhitespace / 2, _layout.LabelBaseline - layout.Baseline);
        }
    }

    private void DrawOutputs(DrawingContext context, in Inks ink, ModelTrainingTimeline timeline, double t)
    {
        var owner = _owner!;
        var p = timeline.WinnerProbability(t);
        var last = _layout.LayerCount - 1;
        var x = _layout.OutputTextX;
        var right = _layout.Width - 4;
        var outputs = Math.Min(2, _layout.Sizes[last]);
        for (var c = 0; c < outputs; c++)
        {
            var probability = c == 0 ? p : 1 - p;
            var lead = c == 0 ? Math.Clamp((p - 0.6) / 0.25, 0, 1) : 0;
            var node = _layout.Node(last, c);
            var nameColor = ArcColor.Lerp(ink.Secondary, ink.Text, lead);
            var valueColor = ArcColor.Lerp(ink.Muted, ink.Text, lead);
            var name = owner.ClassAt(c);
            var value = FormatProbability(probability);
            if (_layout.IsNarrow)
            {
                DrawTextAtBaseline(context, name, nameColor, x, node.Y - 4, medium: false);
                DrawTextAtBaseline(context, value, valueColor, x, node.Y + 11, medium: false);
                continue;
            }
            DrawTextAtBaseline(context, name, nameColor, x, node.Y - 5, medium: false);
            var valueLayout = _text.Get(value, Face(false), 12, valueColor);
            DrawLayout(context, valueLayout, right - valueLayout.WidthIncludingTrailingWhitespace, node.Y - 5 - valueLayout.Baseline);
            var track = new Rect(x, node.Y + 3, Math.Max(0, right - x), 3);
            context.DrawRectangle(_text.Brush(ink.SurfaceMuted), Pen(ink.SubtleBorder, 1, 1), new RoundedRect(track, 1.5));
            var fill = track.WithWidth(track.Width * probability);
            context.DrawRectangle(_text.Brush(ArcColor.Lerp(ink.Muted, ink.Accent, lead)), null, new RoundedRect(fill, 1.5));
        }
    }

    private static string FormatProbability(double p)
    {
        // Two decimals without allocating a new string for every frame: the 101 values are interned once.
        var i = Math.Clamp((int)Math.Round(p * 100), 0, 100);
        return s_probabilities[i];
    }

    private static readonly string[] s_probabilities = BuildProbabilities();

    private static string[] BuildProbabilities()
    {
        var values = new string[101];
        for (var i = 0; i <= 100; i++)
        {
            values[i] = (i / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
        return values;
    }

    // ---- Hover detail ------------------------------------------------------------------------------------------------

    private void DrawTag(DrawingContext context, in Inks ink)
    {
        if (_hover == ModelTrainingRegion.None || _owner is null)
        {
            return;
        }
        var text = _owner.DetailOf(_hover);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var layout = _text.Get(text, Face(true), 12, ink.Background);
        var width = layout.WidthIncludingTrailingWhitespace + 16;
        const double height = 21.6;
        double x;
        double y;
        switch (_hover)
        {
            case ModelTrainingRegion.TrainingSet:
                x = _layout.TrainingText.X;
                y = _layout.TrainingText.Y + 18;
                if (_layout.IsNarrow)
                {
                    y = _layout.TrainingText.Y + 20;
                }
                break;
            case ModelTrainingRegion.HeldOutSet:
                x = _layout.HeldOutText.X;
                y = _layout.IsNarrow ? _layout.HeldOutText.Y + 20 : _layout.HeldOutHit.Y - 8 - height;
                break;
            default:
                var l = _hover - ModelTrainingRegion.Layer0;
                var back = _layout.Plate(l).Translate(_layout.PlateDepth);
                y = back.Y - 8 - height;
                x = l == _layout.LayerCount - 1 ? back.Right - width : _layout.LayerX(l) - width / 2;
                break;
        }
        x = Math.Clamp(x, 0, Math.Max(0, _layout.Width - width));
        y = Math.Max(0, y);
        var pill = new Rect(x, y, width, height);
        context.DrawRectangle(_text.Brush(ink.Text), null, new RoundedRect(pill, height / 2));
        DrawLayout(context, layout, x + 8, y + (height - layout.Height) / 2);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------------

    private void DrawTextAtBaseline(DrawingContext context, string text, Color color, double x, double baseline, bool medium)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var layout = _text.Get(text, Face(medium), 12, color);
        DrawLayout(context, layout, x, baseline - layout.Baseline);
    }

    private static void DrawLayout(DrawingContext context, TextLayout layout, double x, double y) => layout.Draw(context, new Point(x, y));

    private Typeface Face(bool medium) => new(FontFamily, FontStyle.Normal, medium ? FontWeight.Medium : FontWeight.Normal);

    private IPen Pen(Color color, double thickness, double alpha)
    {
        var level = ExplainerTextCache.AlphaLevel(alpha);
        var key = HashCode.Combine(color, (int)Math.Round(thickness * 100), level);
        if (!_pens.TryGetValue(key, out var pen))
        {
            if (_pens.Count > 2048)
            {
                _pens.Clear();
            }
            pen = new ImmutablePen(_text.Brush(color, alpha), thickness, null, PenLineCap.Round);
            _pens[key] = pen;
        }
        return pen;
    }

    private IPen DashedPen(Color color)
    {
        _heldOutDash ??= new ImmutableDashStyle([3, 3], 0);
        var key = HashCode.Combine(color, -1);
        if (!_pens.TryGetValue(key, out var pen))
        {
            pen = new ImmutablePen(_text.Brush(color), 1, _heldOutDash, PenLineCap.Flat);
            _pens[key] = pen;
        }
        return pen;
    }

    private void EnsureDashPens(Color accent)
    {
        var count = _layout.Edges.Count;
        var thickness = _layout.IsNarrow ? 2 : 2.25;
        if (_dashPens.Length == count && count > 0 && _dashPens[0].Brush is ISolidColorBrush b && b.Color == accent
            && Math.Abs(_dashPens[0].Thickness - thickness) < 0.001)
        {
            return;
        }
        _dashPens = new Pen[count];
        var brush = new ImmutableSolidColorBrush(accent);
        for (var i = 0; i < count; i++)
        {
            _dashPens[i] = new Pen(brush, thickness, new DashStyle(new Avalonia.Collections.AvaloniaList<double>(), 0), PenLineCap.Round);
        }
    }

    private static double Fade(double q, double fadeIn, double fadeOut)
        => Math.Clamp(Math.Min(q / fadeIn, (1 - q) / fadeOut), 0, 1);

    private static double Quantize(double value, int steps) => Math.Round(value * steps) / steps;

    /// <summary>The theme colours this frame draws with.</summary>
    private readonly struct Inks
    {
        public Inks(ModelTrainingStage stage, IBrush? accent)
        {
            Sheet = ColorOf(stage.SheetInk, Colors.White);
            PlateBack = ColorOf(stage.PlateBackInk, Colors.WhiteSmoke);
            PlateFace = ColorOf(stage.PlateFaceInk, Colors.White);
            Border = ColorOf(stage.BorderInk, Colors.LightGray);
            StrongBorder = ColorOf(stage.StrongBorderInk, Colors.Gray);
            SubtleBorder = ColorOf(stage.SubtleBorderInk, Colors.Gainsboro);
            Text = ColorOf(stage.TextInk, Colors.Black);
            Secondary = ColorOf(stage.SecondaryInk, Colors.DimGray);
            Muted = ColorOf(stage.MutedInk, Colors.Gray);
            Accent = ColorOf(accent ?? stage.AccentInk, Colors.DimGray);
            Background = ColorOf(stage.BackgroundInk, Colors.White);
            SurfaceMuted = ColorOf(stage.SurfaceMutedInk, Colors.WhiteSmoke);
            // The unit outline sits between the strong border and the muted text on dark backgrounds (Arc: L .53).
            var dark = Background.R + Background.G + Background.B < 384;
            NodeStroke = dark ? ArcColor.Lerp(StrongBorder, Muted, 0.55) : StrongBorder;
        }

        public Color Sheet { get; }
        public Color PlateBack { get; }
        public Color PlateFace { get; }
        public Color Border { get; }
        public Color StrongBorder { get; }
        public Color SubtleBorder { get; }
        public Color Text { get; }
        public Color Secondary { get; }
        public Color Muted { get; }
        public Color Accent { get; }
        public Color Background { get; }
        public Color SurfaceMuted { get; }
        public Color NodeStroke { get; }

        private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;
    }
}
