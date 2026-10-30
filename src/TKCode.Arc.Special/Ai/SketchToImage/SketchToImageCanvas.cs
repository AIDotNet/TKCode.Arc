using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The painting surface of a <see cref="SketchToImage"/>: blank paper (its background) at a fixed 3:2 ratio of its width,
/// with six layers stacked on it: the ink (brush blocks, pencil lines, pen and brush tips), two pre-blurred passes of the
/// photo, a static film grain, a warm light and the sharp photo. The blur is set once per layer; a frame only writes the
/// layers' opacities and redraws the ink, so the photo layers stay with the compositor. The canvas is one image for
/// assistive technology; its layers are hidden from it.
/// </summary>
internal sealed class SketchToImageCanvas : Panel
{
    /// <summary>The pencil, pen tip and brush outline color (the accent, or the foreground for the neutral tone).</summary>
    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<SketchToImageCanvas, IBrush?>(nameof(InkBrush));

    /// <summary>The pencil's width on screen in px (it stays this crisp whatever the canvas scale).</summary>
    public static readonly StyledProperty<double> PencilWidthProperty =
        AvaloniaProperty.Register<SketchToImageCanvas, double>(nameof(PencilWidth), 1.5);

    /// <summary>The pen tip's diameter on screen in px.</summary>
    public static readonly StyledProperty<double> TipSizeProperty =
        AvaloniaProperty.Register<SketchToImageCanvas, double>(nameof(TipSize), 7);

    /// <summary>The blur of the first, softest pass of the photo (px).</summary>
    internal const double SoftBlur = 10;

    /// <summary>The blur of the second, more detailed pass (px).</summary>
    internal const double DetailBlur = 4;

    private readonly SketchToImageInk _ink = new();
    private readonly SketchToImagePhoto _soft = new() { Bleed = SoftBlur * 2 };
    private readonly SketchToImagePhoto _detail = new() { Bleed = DetailBlur * 2 };
    private readonly SketchToImageGrain _grain = new();
    private readonly SketchToImageLight _light = new();
    private readonly SketchToImagePhoto _photo = new();
    private double _t = -1;
    private double _fade = -1;

    static SketchToImageCanvas()
    {
        ClipToBoundsProperty.OverrideDefaultValue<SketchToImageCanvas>(true);
        IsHitTestVisibleProperty.OverrideDefaultValue<SketchToImageCanvas>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<SketchToImageCanvas>(AutomationControlType.Image);
    }

    public SketchToImageCanvas()
    {
        _soft.Effect = ArcBlur.Get(SoftBlur);
        _detail.Effect = ArcBlur.Get(DetailBlur);
        foreach (var layer in new Control[] { _ink, _soft, _detail, _grain, _light, _photo })
        {
            AutomationProperties.SetAccessibilityView(layer, AccessibilityView.Raw);
            layer.IsHitTestVisible = false;
            Children.Add(layer);
        }
        _ink.Owner = this;
    }

    /// <inheritdoc cref="InkBrushProperty"/>
    public IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    /// <inheritdoc cref="PencilWidthProperty"/>
    public double PencilWidth { get => GetValue(PencilWidthProperty); set => SetValue(PencilWidthProperty, value); }

    /// <inheritdoc cref="TipSizeProperty"/>
    public double TipSize { get => GetValue(TipSizeProperty); set => SetValue(TipSizeProperty, value); }

    /// <summary>The scene ready to draw.</summary>
    public SketchToImagePrepared? Prepared { get; private set; }

    /// <summary>The finished picture that overrides the scene's own.</summary>
    public IImage? Image { get; private set; }

    /// <summary>The position drawn (0 blank, 1 the photo).</summary>
    public double T => _t;

    /// <summary>The layer opacities last applied (tests read them).</summary>
    internal SketchToImageLayers Layers { get; private set; }

    internal SketchToImageInk Ink => _ink;

    internal SketchToImagePhoto SoftPass => _soft;

    internal SketchToImagePhoto DetailPass => _detail;

    internal SketchToImagePhoto Photo => _photo;

    internal Control GrainLayer => _grain;

    internal Control LightLayer => _light;

    public void SetScene(SketchToImagePrepared prepared, IImage? image)
    {
        Prepared = prepared;
        Image = image ?? prepared.Scene.Image;
        _ink.Prepared = prepared;
        _light.Light = prepared.Light;
        foreach (var photo in new[] { _soft, _detail, _photo })
        {
            photo.Prepared = prepared;
            photo.Image = Image;
            photo.InvalidateVisual();
        }
        _light.InvalidateVisual();
        _ink.InvalidateVisual();
    }

    /// <summary>Shows the timeline at <paramref name="t"/> with <paramref name="fade"/> of the canvas showing. Allocates nothing.</summary>
    public void Show(double t, double fade)
    {
        if (t == _t && fade == _fade)
        {
            return;
        }
        var inkChanged = t != _t;
        _t = t;
        _fade = fade;
        var layers = SketchToImageTimeline.LayersAt(t);
        Layers = layers;
        _ink.Opacity = fade;
        _soft.Opacity = layers.SoftPass * fade;
        _detail.Opacity = layers.DetailPass * fade;
        _grain.Opacity = layers.Grain * fade;
        _light.Opacity = layers.Light * fade;
        _photo.Opacity = layers.Photo * fade;
        // Hidden layers leave the render pass altogether (the blurred ones cost the most).
        _soft.IsVisible = _soft.Opacity > 0.001;
        _detail.IsVisible = _detail.Opacity > 0.001;
        _grain.IsVisible = _grain.Opacity > 0.001;
        _light.IsVisible = _light.Opacity > 0.001;
        _photo.IsVisible = _photo.Opacity > 0.001;
        if (inkChanged)
        {
            _ink.T = t;
            _ink.LinesOpacity = layers.Lines;
            _ink.InvalidateVisual();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InkBrushProperty || change.Property == PencilWidthProperty || change.Property == TipSizeProperty)
        {
            _ink.InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 520;
        var size = new Size(width, width * SketchToImageScene.CanvasHeight / SketchToImageScene.CanvasWidth);
        foreach (var child in Children)
        {
            child.Measure(size);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        foreach (var child in Children)
        {
            child.Arrange(rect);
        }
        return finalSize;
    }
}

/// <summary>
/// The ink layer: finished regions in their flat colors, the region being painted revealed through the round brush's
/// sweep (the sweep drawn with a pen in the shape's color, clipped by the shape: only one mask is live), the pencil strokes
/// trimmed to their progress, and the pen and brush tips. Every geometry and pen is cached; the pencil pen is rebuilt only
/// when the scale or the ink color changes.
/// </summary>
internal sealed class SketchToImageInk : Control
{
    private Pen? _pencil;
    private IBrush? _pencilBrush;
    private double _pencilWidth;
    private Pen? _outline;

    public SketchToImageCanvas? Owner { get; set; }

    public SketchToImagePrepared? Prepared { get; set; }

    public double T { get; set; }

    public double LinesOpacity { get; set; } = 1;

    /// <summary>Where the pen tip was last drawn and how far it was lifted (tests read it).</summary>
    internal (Point Position, double Lift, double Alpha)? PenTip { get; private set; }

    /// <summary>Where the brush tip was last drawn (tests read it).</summary>
    internal Point? BrushTip { get; private set; }

    public override void Render(DrawingContext context)
    {
        PenTip = null;
        BrushTip = null;
        var prepared = Prepared;
        var owner = Owner;
        if (prepared is null || owner is null || Bounds.Width <= 0 || T <= 0)
        {
            return;
        }
        var scale = Bounds.Width / SketchToImageScene.CanvasWidth;
        var ink = owner.InkBrush ?? Brushes.Gray;
        var width = owner.PencilWidth / scale;
        if (_pencil is null || !ReferenceEquals(_pencilBrush, ink) || Math.Abs(_pencilWidth - width) > 1e-6)
        {
            _pencil = new Pen(ink, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _outline = new Pen(ink, width * 0.8);
            _pencilBrush = ink;
            _pencilWidth = width;
        }
        var sketch = SketchToImageTimeline.Local(T, 0, SketchToImageTimeline.SketchEnd);
        var block = SketchToImageTimeline.Local(T, SketchToImageTimeline.SketchEnd, SketchToImageTimeline.BlockEnd);
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            if (block > 0)
            {
                DrawBlocks(context, prepared, block);
            }
            if (LinesOpacity > 0.003)
            {
                using (context.PushOpacity(LinesOpacity))
                {
                    DrawLines(context, prepared, sketch);
                }
            }
            if (T < SketchToImageTimeline.SketchEnd)
            {
                DrawPenTip(context, prepared, sketch, owner.TipSize / 2 / scale, ink);
            }
            else if (block > 0 && T < SketchToImageTimeline.BlockEnd)
            {
                DrawBrushTip(context, prepared, block);
            }
        }
    }

    private static void DrawBlocks(DrawingContext context, SketchToImagePrepared prepared, double block)
    {
        foreach (var region in prepared.Regions)
        {
            if (block >= region.End)
            {
                using (context.PushClip(region.Swept))
                {
                    foreach (var shape in region.Shapes)
                    {
                        if (shape.Flat is not null)
                        {
                            context.DrawGeometry(shape.Flat, null, shape.Geometry);
                        }
                    }
                }
                continue;
            }
            if (block <= region.Start)
            {
                return;
            }
            var reach = SketchToImageTimeline.InOut(SketchToImageTimeline.Local(block, region.Start, region.End)) * region.Sweep.Length;
            foreach (var shape in region.Shapes)
            {
                if (shape.Brush is null)
                {
                    continue;
                }
                using (context.PushGeometryClip(shape.Geometry))
                {
                    region.Sweep.Draw(context, shape.Brush, reach);
                }
            }
            return;
        }
    }

    private void DrawLines(DrawingContext context, SketchToImagePrepared prepared, double sketch)
    {
        var pen = _pencil!;
        foreach (var stroke in prepared.Strokes)
        {
            if (sketch >= stroke.End)
            {
                context.DrawGeometry(null, pen, stroke.Path.Full);
                continue;
            }
            if (sketch > stroke.Start)
            {
                var reach = SketchToImageTimeline.InOut(SketchToImageTimeline.Local(sketch, stroke.Start, stroke.End)) * stroke.Path.Length;
                stroke.Path.Draw(context, pen, reach);
            }
            return;
        }
    }

    /// <summary>
    /// The pen tip follows the stroke head; between strokes it lifts (shrinks to 60% and dims) while it travels to the next
    /// stroke's start, and after the last stroke it lifts away and fades.
    /// </summary>
    private void DrawPenTip(DrawingContext context, SketchToImagePrepared prepared, double sketch, double radius, IBrush ink)
    {
        var strokes = prepared.Strokes;
        if (strokes.Count == 0)
        {
            return;
        }
        Point position;
        double lift;
        var alpha = 1.0;
        var last = strokes[^1];
        if (sketch >= last.End)
        {
            var away = SketchToImageTimeline.Local(sketch, last.End, 1);
            position = last.Path.End;
            lift = away;
            alpha = 1 - away;
        }
        else
        {
            var stroke = strokes[0];
            foreach (var candidate in strokes)
            {
                stroke = candidate;
                if (sketch < candidate.End)
                {
                    break;
                }
            }
            if (sketch < stroke.Start)
            {
                var u = SketchToImageTimeline.Local(sketch, stroke.TravelStart, stroke.Start);
                var eased = SketchToImageTimeline.InOut(u);
                position = new Point(
                    stroke.From.X + (stroke.Path.Start.X - stroke.From.X) * eased,
                    stroke.From.Y + (stroke.Path.Start.Y - stroke.From.Y) * eased);
                lift = Math.Sin(Math.PI * u);
            }
            else
            {
                var reach = SketchToImageTimeline.InOut(SketchToImageTimeline.Local(sketch, stroke.Start, stroke.End)) * stroke.Path.Length;
                position = stroke.Path.PointAt(reach);
                lift = 0;
            }
        }
        alpha *= 1 - 0.55 * lift;
        PenTip = (position, lift, alpha);
        if (alpha <= 0.01)
        {
            return;
        }
        var r = radius * (1 - 0.4 * lift);
        using (context.PushOpacity(alpha))
        {
            context.DrawEllipse(ink, null, position, r, r);
        }
    }

    /// <summary>The round brush head at the sweep's head in the region's color, travelling (and lifting) between regions.</summary>
    private void DrawBrushTip(DrawingContext context, SketchToImagePrepared prepared, double block)
    {
        var regions = prepared.Regions;
        if (regions.Count == 0)
        {
            return;
        }
        var region = regions[^1];
        Point position;
        double lift;
        var alpha = 1.0;
        if (block >= region.End)
        {
            var away = SketchToImageTimeline.Local(block, region.End, 1);
            position = region.Sweep.End;
            lift = away;
            alpha = 1 - away;
        }
        else
        {
            foreach (var candidate in regions)
            {
                region = candidate;
                if (block < candidate.End)
                {
                    break;
                }
            }
            if (block < region.Start)
            {
                var u = SketchToImageTimeline.Local(block, region.TravelStart, region.Start);
                var eased = SketchToImageTimeline.InOut(u);
                position = new Point(
                    region.From.X + (region.Sweep.Start.X - region.From.X) * eased,
                    region.From.Y + (region.Sweep.Start.Y - region.From.Y) * eased);
                lift = Math.Sin(Math.PI * u);
            }
            else
            {
                var reach = SketchToImageTimeline.InOut(SketchToImageTimeline.Local(block, region.Start, region.End)) * region.Sweep.Length;
                position = region.Sweep.PointAt(reach);
                lift = 0;
            }
        }
        alpha *= 1 - 0.5 * lift;
        BrushTip = position;
        if (alpha <= 0.01)
        {
            return;
        }
        var r = region.BrushWidth * 0.3 * (1 - 0.35 * lift);
        using (context.PushOpacity(alpha))
        {
            context.DrawEllipse(region.Tip, _outline, position, r, r);
        }
    }
}

/// <summary>
/// One layer of the finished picture: the app's image stretched to fill, or the scene's shapes in their finished brushes.
/// A blurred pass draws a little past its edges (<see cref="Bleed"/>) so the blur does not pull blank paper in. It is
/// drawn once per size; frames only change its opacity.
/// </summary>
internal sealed class SketchToImagePhoto : Control
{
    public SketchToImagePrepared? Prepared { get; set; }

    public IImage? Image { get; set; }

    /// <summary>How far past each edge the layer draws (px).</summary>
    public double Bleed { get; set; }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var target = bounds.Inflate(Bleed);
        if (Image is { } image)
        {
            var size = image.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                return;
            }
            // Uniform to fill: cover the target, centered, cropped by the canvas.
            var k = Math.Max(target.Width / size.Width, target.Height / size.Height);
            var w = size.Width * k;
            var h = size.Height * k;
            context.DrawImage(image, new Rect(target.Center.X - w / 2, target.Center.Y - h / 2, w, h));
            return;
        }
        var prepared = Prepared;
        if (prepared is null)
        {
            return;
        }
        var matrix = Matrix.CreateScale(target.Width / SketchToImageScene.CanvasWidth, target.Height / SketchToImageScene.CanvasHeight)
                     * Matrix.CreateTranslation(target.X, target.Y);
        using (context.PushTransform(matrix))
        {
            foreach (var region in prepared.Regions)
            {
                foreach (var shape in region.Shapes)
                {
                    if (shape.Finish is not null)
                    {
                        context.DrawGeometry(shape.Finish, null, shape.Geometry);
                    }
                }
            }
        }
    }
}

/// <summary>
/// A static film grain: a few thousand fine light and dark specks laid out once from a fixed seed into two cached
/// geometries on the 600 × 400 canvas. Drawn once per size; only its opacity changes.
/// </summary>
internal sealed class SketchToImageGrain : Control
{
    private static readonly Lazy<(Geometry Light, Geometry Dark)> s_specks = new(Build);
    private static readonly IBrush s_light = new ImmutableSolidColorBrush(Color.FromArgb(70, 255, 248, 236));
    private static readonly IBrush s_dark = new ImmutableSolidColorBrush(Color.FromArgb(56, 24, 18, 30));

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }
        var scale = Bounds.Width / SketchToImageScene.CanvasWidth;
        var (light, dark) = s_specks.Value;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawGeometry(s_light, null, light);
            context.DrawGeometry(s_dark, null, dark);
        }
    }

    private static (Geometry, Geometry) Build()
    {
        var random = new Random(20261029);
        return (Specks(random, 2400), Specks(random, 2400));
    }

    private static Geometry Specks(Random random, int count)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            for (var i = 0; i < count; i++)
            {
                var x = random.NextDouble() * SketchToImageScene.CanvasWidth;
                var y = random.NextDouble() * SketchToImageScene.CanvasHeight;
                var s = 0.6 + random.NextDouble() * 0.7;
                g.BeginFigure(new Point(x, y), true);
                g.LineTo(new Point(x + s, y));
                g.LineTo(new Point(x + s, y + s));
                g.LineTo(new Point(x, y + s));
                g.EndFigure(true);
            }
        }
        return geometry;
    }
}

/// <summary>A soft warm light in the scene's light source: a large radial glow and a faint warm wash over the whole canvas.</summary>
internal sealed class SketchToImageLight : Control
{
    private static readonly IBrush s_glow = new ImmutableRadialGradientBrush(
    [
        new ImmutableGradientStop(0, Color.FromArgb(150, 255, 214, 150)),
        new ImmutableGradientStop(0.45, Color.FromArgb(60, 255, 190, 120)),
        new ImmutableGradientStop(1, Color.FromArgb(0, 255, 180, 110)),
    ], radius: 0.5);

    private static readonly IBrush s_wash = new ImmutableSolidColorBrush(Color.FromArgb(22, 255, 170, 90));

    public Point Light { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }
        var scale = Bounds.Width / SketchToImageScene.CanvasWidth;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawRectangle(s_wash, null, new Rect(0, 0, SketchToImageScene.CanvasWidth, SketchToImageScene.CanvasHeight));
            context.DrawEllipse(s_glow, null, Light, 300, 300);
        }
    }
}
