using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The self-drawn canvas of an <see cref="ImageReveal"/>: the surface tone and its queued shimmer, the soft preview
/// (the chain's 6 to 48px copies smoothly upscaled, blurred and crossfaded by progress, with a slow drift), fine film grain
/// below the band, the sharp image above the band and the band itself (the near-sharp copy and the image through
/// feathered opacity masks), the swap snapshot and the completion sheen. It holds no state of its own: the owner hands it
/// an <see cref="ImageRevealEngine"/> pose and the chain, and every frame is a few image draws, one masked strip and a
/// tiled grain fill, with no allocation.
/// </summary>
internal sealed class ImageRevealCanvas : Control
{
    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<ImageRevealCanvas, IBrush?>(nameof(SurfaceBrush));

    public static readonly StyledProperty<IBrush?> ShimmerBrushProperty =
        AvaloniaProperty.Register<ImageRevealCanvas, IBrush?>(nameof(ShimmerBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<ImageRevealCanvas, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<double> BandRatioProperty =
        AvaloniaProperty.Register<ImageRevealCanvas, double>(nameof(BandRatio), 0.24);

    public static readonly StyledProperty<double> GrainOpacityProperty =
        AvaloniaProperty.Register<ImageRevealCanvas, double>(nameof(GrainOpacity), 1);

    /// <summary>The soft copies' blur in px at a 480px wide canvas, softest first (capped by <see cref="ArcBlur.MaxRadius"/>).</summary>
    private static readonly double[] s_softBlur = [10, 8, 5, 3];

    /// <summary>The near-sharp copy: opaque at the band's sharp edge, gone at its soft edge.</summary>
    private static readonly IBrush s_nearMask = Mask(1);

    /// <summary>The sharp image inside the band: opaque at the sharp edge, gone 60% down the band.</summary>
    private static readonly IBrush s_sharpMask = Mask(0.6);

    private static readonly IBrush s_sheen = new ImmutableLinearGradientBrush(
        [
            new ImmutableGradientStop(0, Color.FromArgb(0, 255, 255, 255)),
            new ImmutableGradientStop(0.5, Color.FromArgb(84, 255, 255, 255)),
            new ImmutableGradientStop(1, Color.FromArgb(0, 255, 255, 255))
        ],
        opacity: 1, transform: null, transformOrigin: null, spreadMethod: GradientSpreadMethod.Pad,
        startPoint: new RelativePoint(0, 0.2, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0.8, RelativeUnit.Relative));

    private static readonly RenderOptions s_smooth = new() { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };

    private IBrush? _shimmer;
    private double _scaling = 1;

    static ImageRevealCanvas()
    {
        AffectsRender<ImageRevealCanvas>(SurfaceBrushProperty, CornerRadiusProperty, BandRatioProperty, GrainOpacityProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ImageRevealCanvas>(false);
        FocusableProperty.OverrideDefaultValue<ImageRevealCanvas>(false);
    }

    /// <summary>The calm surface tone under everything.</summary>
    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }

    /// <summary>The color of the queued shimmer's light.</summary>
    public IBrush? ShimmerBrush { get => GetValue(ShimmerBrushProperty); set => SetValue(ShimmerBrushProperty, value); }

    /// <summary>The canvas's corner radius (it clips every layer).</summary>
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>The band's height as a share of the canvas's height.</summary>
    public double BandRatio { get => GetValue(BandRatioProperty); set => SetValue(BandRatioProperty, value); }

    /// <summary>The grain's strength (multiplies the tile's alpha).</summary>
    public double GrainOpacity { get => GetValue(GrainOpacityProperty); set => SetValue(GrainOpacityProperty, value); }

    /// <summary>The pose to draw.</summary>
    internal ImageRevealEngine? Engine { get; set; }

    /// <summary>The copies of the current image.</summary>
    internal ImageRevealChain? Chain { get; set; }

    /// <summary>The previous frame, crossfading out after a photo swap.</summary>
    internal Bitmap? Snapshot { get; set; }

    /// <summary>The snapshot's opacity.</summary>
    internal double SnapshotOpacity { get; set; }

    /// <summary>Seeds the grain's offsets and the preview's drift.</summary>
    internal int Seed { get; set; } = 1;

    /// <summary>The device pixels per DIP (capped at 2, as the original caps its canvas).</summary>
    internal double Scaling => _scaling;

    /// <summary>How many frames were drawn (tests read it).</summary>
    internal int Frames { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShimmerBrushProperty)
        {
            _shimmer = null;
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scaling = Math.Clamp(TopLevel.GetTopLevel(this)?.RenderScaling ?? 1, 1, 2);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        Frames++;
        using (context.PushClip(new RoundedRect(bounds, CornerRadius)))
        {
            if (SurfaceBrush is { } surface)
            {
                context.FillRectangle(surface, bounds);
            }
            if (Engine is not { } pose)
            {
                return;
            }
            var level = ArcMotion.GetEffectiveLevel(this);
            var full = level == ArcMotionLevel.Full;
            DrawShimmer(context, bounds, pose, full);
            var chain = Chain;
            var bandHeight = Math.Max(1, BandRatio * bounds.Height);
            var bandTop = pose.Reveal * (bounds.Height + bandHeight) - bandHeight;
            var resolved = pose.Reveal >= 1 && pose.Focus >= 1;
            if (chain is not null && !resolved)
            {
                DrawSoft(context, bounds, pose, chain, full);
                DrawGrain(context, bounds, pose, full ? Math.Max(0, bandTop + bandHeight * 0.5) : 0, full);
            }
            if (chain is not null && pose.Reveal > 0 && pose.Focus > 0.001)
            {
                DrawSharp(context, bounds, pose, chain, full, bandTop, bandHeight);
            }
            if (Snapshot is { } snapshot && SnapshotOpacity > 0.001)
            {
                using (context.PushOpacity(SnapshotOpacity))
                {
                    context.DrawImage(snapshot, new Rect(0, 0, snapshot.PixelSize.Width, snapshot.PixelSize.Height), bounds);
                }
            }
            if (pose.Sheen >= 0 && full)
            {
                var width = bounds.Width * 0.55;
                var t = pose.Sheen;
                var eased = t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
                var x = -width + (bounds.Width + width) * eased;
                using (context.PushOpacity(Math.Sin(Math.PI * t)))
                {
                    context.FillRectangle(s_sheen, new Rect(x, 0, width, bounds.Height));
                }
            }
        }
    }

    /// <summary>The queued look's one broad, soft light: it starts and ends fully off canvas, so the loop has no seam.</summary>
    private void DrawShimmer(DrawingContext context, Rect bounds, ImageRevealEngine pose, bool full)
    {
        var visible = 1 - pose.Preview * pose.Dim;
        if (!full || visible <= 0.001 || pose.Status is ImageRevealStatus.Error || pose.IsBlank)
        {
            return;
        }
        _shimmer ??= ShimmerOf(ShimmerBrush);
        if (_shimmer is null)
        {
            return;
        }
        var width = bounds.Width * 1.1;
        var x = -width + (bounds.Width + width) * pose.ShimmerPhase;
        using (context.PushOpacity(visible))
        {
            context.FillRectangle(_shimmer, new Rect(x, 0, width, bounds.Height));
        }
    }

    /// <summary>
    /// The soft preview: the copy for the current point of the chain and the next one over it by the remainder, each
    /// smoothly upscaled and blurred, drawn a little larger than the canvas (so the blur never shows an edge) and drifting
    /// very slowly while it is unresolved.
    /// </summary>
    private void DrawSoft(DrawingContext context, Rect bounds, ImageRevealEngine pose, ImageRevealChain chain, bool full)
    {
        var opacity = pose.Preview * pose.Dim;
        if (opacity <= 0.001)
        {
            return;
        }
        var unresolved = full ? 1 - pose.Reveal : 0;
        var t = pose.Time;
        var phase = Seed * 1.618;
        var scale = 1.08 + 0.02 * unresolved * Math.Sin(t * 0.11 + phase);
        var dx = Math.Sin(t * 0.21 + phase) * 0.014 * bounds.Width * unresolved;
        var dy = Math.Cos(t * 0.17 + phase * 1.3) * 0.014 * bounds.Height * unresolved;
        var width = bounds.Width * scale;
        var height = bounds.Height * scale;
        var dest = new Rect((bounds.Width - width) / 2 + dx, (bounds.Height - height) / 2 + dy, width, height);
        var count = chain.Soft.Length;
        var point = pose.Reveal * (count - 1);
        var index = Math.Clamp((int)Math.Floor(point), 0, count - 1);
        var rest = point - index;
        var blurScale = bounds.Width / 480;
        using (context.PushOpacity(opacity))
        using (context.PushRenderOptions(s_smooth))
        {
            DrawCopy(context, chain.Soft[index], dest, s_softBlur[index] * blurScale);
            if (rest > 0.01 && index + 1 < count)
            {
                using (context.PushOpacity(rest))
                {
                    DrawCopy(context, chain.Soft[index + 1], dest, s_softBlur[index + 1] * blurScale);
                }
            }
        }
    }

    private static void DrawCopy(DrawingContext context, Bitmap copy, Rect dest, double blur)
    {
        var source = new Rect(0, 0, copy.PixelSize.Width, copy.PixelSize.Height);
        if (ArcBlur.Get(blur) is { } effect)
        {
            using (context.PushEffect(effect, dest))
            {
                context.DrawImage(copy, source, dest);
            }
            return;
        }
        context.DrawImage(copy, source, dest);
    }

    /// <summary>
    /// Fine monochrome grain over the soft part (below the band), one tile pixel per device pixel. Under full motion the
    /// tile jumps to a new seeded offset 24 times a second; otherwise it stays still.
    /// </summary>
    private void DrawGrain(DrawingContext context, Rect bounds, ImageRevealEngine pose, double top, bool full)
    {
        var opacity = pose.Preview * pose.Dim * Math.Sqrt(Math.Max(0, 1 - pose.Reveal)) * GrainOpacity;
        if (opacity <= 0.01 || top >= bounds.Height || ImageRevealGrain.Tile is not { } tile)
        {
            return;
        }
        var frame = full ? (int)(pose.Time * 24) : 0;
        var hash = (uint)(frame * 0x9E3779B1) ^ (uint)(Seed * 0x85EBCA77);
        hash ^= hash >> 15;
        hash *= 0x2C1B3C6D;
        hash ^= hash >> 12;
        var side = ImageRevealGrain.Side;
        var ox = (int)(hash % (uint)side);
        var oy = (int)((hash >> 8) % (uint)side);
        var s = _scaling;
        var deviceWidth = bounds.Width * s;
        var deviceHeight = bounds.Height * s;
        var deviceTop = Math.Floor(top * s);
        var source = new Rect(0, 0, side, side);
        using (context.PushClip(new Rect(0, top, bounds.Width, bounds.Height - top)))
        using (context.PushOpacity(opacity))
        using (context.PushTransform(Matrix.CreateScale(1 / s, 1 / s)))
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
        {
            var startY = deviceTop - ((deviceTop + oy) % side);
            for (var y = startY; y < deviceHeight; y += side)
            {
                for (double x = -ox; x < deviceWidth; x += side)
                {
                    context.DrawImage(tile, source, new Rect(x, y, side, side));
                }
            }
        }
    }

    /// <summary>
    /// The sharp layers, blurred back out and faded as <see cref="ImageRevealEngine.Focus"/> falls. Full motion: the image
    /// above the band, then the band (the near copy and the image through feathered masks). Reduced motion and Off: no
    /// travelling band, the near copy and then the image crossfade in by progress.
    /// </summary>
    private static void DrawSharp(DrawingContext context, Rect bounds, ImageRevealEngine pose, ImageRevealChain chain, bool full,
        double bandTop, double bandHeight)
    {
        var near = chain.Near;
        var nearSource = new Rect(0, 0, near.PixelSize.Width, near.PixelSize.Height);
        var effect = pose.Focus < 1 ? ArcBlur.Get((1 - pose.Focus) * 8) : null;
        using (context.PushOpacity(pose.Focus))
        using (context.PushRenderOptions(s_smooth))
        using (effect is null ? default(DrawingContext.PushedState?) : context.PushEffect(effect, bounds))
        {
            if (!full)
            {
                var r = pose.Reveal;
                using (context.PushOpacity(SmoothStep(0, 0.6, r)))
                {
                    context.DrawImage(near, nearSource, bounds);
                }
                using (context.PushOpacity(SmoothStep(0.35, 1, r)))
                {
                    context.DrawImage(chain.Source, chain.Crop, bounds);
                }
                return;
            }
            if (bandTop > 0)
            {
                using (context.PushClip(new Rect(0, 0, bounds.Width, Math.Min(bandTop, bounds.Height))))
                {
                    context.DrawImage(chain.Source, chain.Crop, bounds);
                }
            }
            var band = new Rect(0, bandTop, bounds.Width, bandHeight);
            var visible = band.Intersect(bounds);
            if (visible.Height <= 0)
            {
                return;
            }
            using (context.PushClip(visible))
            {
                using (context.PushOpacityMask(s_nearMask, band))
                {
                    context.DrawImage(near, nearSource, bounds);
                }
                using (context.PushOpacityMask(s_sharpMask, band))
                {
                    context.DrawImage(chain.Source, chain.Crop, bounds);
                }
            }
        }
    }

    private static double SmoothStep(double from, double to, double value)
    {
        var t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static IBrush Mask(double end) => new ImmutableLinearGradientBrush(
        [
            new ImmutableGradientStop(0, Colors.Black),
            new ImmutableGradientStop(end, Colors.Transparent),
            new ImmutableGradientStop(1, Colors.Transparent)
        ],
        opacity: 1, transform: null, transformOrigin: null, spreadMethod: GradientSpreadMethod.Pad,
        startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));

    private static IBrush? ShimmerOf(IBrush? brush)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return null;
        }
        var c = solid.Color;
        return new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Color.FromArgb(0, c.R, c.G, c.B)),
                new ImmutableGradientStop(0.5, Color.FromArgb((byte)(c.A * solid.Opacity), c.R, c.G, c.B)),
                new ImmutableGradientStop(1, Color.FromArgb(0, c.R, c.G, c.B))
            ],
            opacity: 1, transform: null, transformOrigin: null, spreadMethod: GradientSpreadMethod.Pad,
            startPoint: new RelativePoint(0, 0.35, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0.65, RelativeUnit.Relative));
    }
}
