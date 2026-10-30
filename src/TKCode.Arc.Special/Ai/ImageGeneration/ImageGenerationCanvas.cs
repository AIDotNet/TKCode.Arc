using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The picture of one tile, drawn in one control: the quiet placeholder grid, the pixel mosaic as it resolves, the soft
/// light band over the unresolved blocks, the finished image (cropped to cover) and the upscale sweep. The tile writes
/// its state here and invalidates; every brush and bitmap is built beforehand, so a frame allocates nothing.
/// </summary>
internal sealed class ImageGenerationCanvas : Control
{
    /// <summary>The empty tile's fill.</summary>
    public static readonly StyledProperty<IBrush?> PlaceholderBrushProperty =
        AvaloniaProperty.Register<ImageGenerationCanvas, IBrush?>(nameof(PlaceholderBrush));

    /// <summary>The placeholder grid's lines.</summary>
    public static readonly StyledProperty<IBrush?> CellBrushProperty =
        AvaloniaProperty.Register<ImageGenerationCanvas, IBrush?>(nameof(CellBrush));

    /// <summary>The light band's widest part, as a share of the diagonal.</summary>
    internal const double ShimmerWidth = 0.18;

    /// <summary>The light band's strongest white.</summary>
    internal const double ShimmerStrength = 0.24;

    /// <summary>White at 16 steps of alpha for the light band and the sweep (built once).</summary>
    private static readonly ImmutableSolidColorBrush[] s_light = BuildLight();

    private static readonly RenderOptions s_nearest = new() { BitmapInterpolationMode = BitmapInterpolationMode.None };
    private static readonly RenderOptions s_smooth = new() { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };

    static ImageGenerationCanvas()
    {
        AffectsRender<ImageGenerationCanvas>(PlaceholderBrushProperty, CellBrushProperty);
        ClipToBoundsProperty.OverrideDefaultValue<ImageGenerationCanvas>(true);
    }

    public IBrush? PlaceholderBrush { get => GetValue(PlaceholderBrushProperty); set => SetValue(PlaceholderBrushProperty, value); }

    public IBrush? CellBrush { get => GetValue(CellBrushProperty); set => SetValue(CellBrushProperty, value); }

    /// <summary>The shape the tile shows (height over width); the placeholder grid follows it.</summary>
    public double Aspect { get; set; } = 1;

    /// <summary>The mosaic the picture resolves from, or null (the placeholder grid until the image arrives).</summary>
    public ImageGenerationMosaic? Mosaic { get; set; }

    /// <summary>The finished image, or null.</summary>
    public Bitmap? Image { get; set; }

    /// <summary>How far the picture has resolved, 0 to 1 (the tile's one progress value).</summary>
    public double Progress { get; set; }

    /// <summary>How much of the image shows over the placeholder when it arrived without a mosaic, 0 to 1.</summary>
    public double Reveal { get; set; } = 1;

    /// <summary>Where the light band is on its loop, 0 to 1; negative draws no band.</summary>
    public double ShimmerPhase { get; set; } = -1;

    /// <summary>Where the upscale sweep is, 0 to 1; negative draws no sweep.</summary>
    public double Sweep { get; set; } = -1;

    /// <summary>The upscaled image the sweep reveals behind it, once it has arrived (else the image itself).</summary>
    public Bitmap? SweepImage { get; set; }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        // Fill the whole tile so it is hit everywhere.
        context.FillRectangle(PlaceholderBrush ?? Brushes.Transparent, bounds);
        var mosaic = Mosaic;
        var progress = Math.Clamp(Progress, 0, 1);
        var (stage, local) = ImageGenerationMosaic.Phase(progress);
        if (Image is { } image && (mosaic is null || progress >= 1))
        {
            if (Reveal < 1)
            {
                DrawPlaceholder(context, bounds);
                using (context.PushOpacity(Math.Clamp(Reveal, 0, 1)))
                {
                    DrawCover(context, image, bounds);
                }
            }
            else if (Sweep >= 0 && Sweep < 1 && mosaic is not null)
            {
                DrawSweep(context, image, mosaic, bounds);
            }
            else
            {
                DrawCover(context, image, bounds);
            }
            return;
        }
        if (mosaic is null)
        {
            DrawPlaceholder(context, bounds);
            if (ShimmerPhase >= 0)
            {
                DrawShimmer(context, bounds, ImageGenerationMosaic.CoarseColumns, ImageGenerationMosaic.CoarseRows(Aspect), ShimmerPhase, 1);
            }
            return;
        }
        DrawMosaic(context, mosaic, bounds, stage, local);
        if (ShimmerPhase >= 0)
        {
            var level = mosaic.Levels[Math.Min(stage, ImageGenerationMosaic.LevelCount - 1)];
            // The band fades out as detail arrives.
            var strength = 1 - Smooth(0.55, 1, progress);
            if (strength > 0.01)
            {
                DrawShimmer(context, bounds, level.Columns, level.Rows, ShimmerPhase, strength);
            }
        }
    }

    private void DrawMosaic(DrawingContext context, ImageGenerationMosaic mosaic, Rect bounds, int stage, double local)
    {
        if (stage == 0)
        {
            // The coarse blocks fade in one by one from the centre, in the seeded order.
            var coarse = mosaic.Levels[0];
            var count = coarse.Colors.Length;
            var cellWidth = bounds.Width / coarse.Columns;
            var cellHeight = bounds.Height / coarse.Rows;
            for (var i = 0; i < count; i++)
            {
                var appear = (double)mosaic.CoarseRank[i] / count * 0.75;
                var alpha = Math.Clamp((local - appear) / 0.25, 0, 1);
                if (alpha <= 0)
                {
                    continue;
                }
                var col = i % coarse.Columns;
                var row = i / coarse.Columns;
                // Blocks overlap their neighbours by a hair so no seam shows between them.
                var cell = new Rect(Math.Floor(col * cellWidth), Math.Floor(row * cellHeight),
                    Math.Ceiling(cellWidth) + 1, Math.Ceiling(cellHeight) + 1);
                IBrush brush = coarse.Brushes is { } brushes ? brushes[i] : Brushes.Gray;
                if (alpha >= 1)
                {
                    context.FillRectangle(brush, cell);
                }
                else
                {
                    using (context.PushOpacity(alpha))
                    {
                        context.FillRectangle(brush, cell);
                    }
                }
            }
            return;
        }
        // Each later step lands a level twice as fine exactly over the one before it; the last crossfades to the image.
        DrawLevel(context, mosaic.Levels[stage - 1], bounds);
        if (stage < ImageGenerationMosaic.LevelCount)
        {
            var alpha = Smooth(0, 0.55, local);
            if (alpha > 0)
            {
                using (context.PushOpacity(alpha))
                {
                    DrawLevel(context, mosaic.Levels[stage], bounds);
                }
            }
        }
        else if (Image is { } image)
        {
            var alpha = Smooth(0, 0.85, local);
            if (alpha > 0)
            {
                using (context.PushOpacity(alpha))
                {
                    DrawCover(context, image, bounds);
                }
            }
        }
    }

    private static void DrawLevel(DrawingContext context, ImageGenerationMosaicLevel level, Rect bounds, bool smooth = false)
    {
        if (level.Bitmap is { } bitmap)
        {
            using (context.PushRenderOptions(smooth ? s_smooth : s_nearest))
            {
                context.DrawImage(bitmap, new Rect(0, 0, level.Columns, level.Rows), bounds);
            }
            return;
        }
        // No bitmaps on this platform: the coarse level's brushes stand in.
        if (level.Brushes is { } brushes)
        {
            var cellWidth = bounds.Width / level.Columns;
            var cellHeight = bounds.Height / level.Rows;
            for (var i = 0; i < brushes.Length; i++)
            {
                context.FillRectangle(brushes[i], new Rect(bounds.X + i % level.Columns * cellWidth, bounds.Y + i / level.Columns * cellHeight,
                    cellWidth + 1, cellHeight + 1));
            }
        }
    }

    /// <summary>The quiet grid an empty or waiting tile shows: the coarse level's lines.</summary>
    private void DrawPlaceholder(DrawingContext context, Rect bounds)
    {
        if (CellBrush is not { } brush)
        {
            return;
        }
        var cols = ImageGenerationMosaic.CoarseColumns;
        var rows = ImageGenerationMosaic.CoarseRows(Aspect);
        for (var col = 1; col < cols; col++)
        {
            context.FillRectangle(brush, new Rect(Math.Round(bounds.Width * col / cols), 0, 1, bounds.Height));
        }
        for (var row = 1; row < rows; row++)
        {
            context.FillRectangle(brush, new Rect(0, Math.Round(bounds.Height * row / rows), bounds.Width, 1));
        }
    }

    /// <summary>
    /// A soft light band moving diagonally across the tile, drawn on the resolving grid's blocks (each block takes the band's
    /// strength at its centre, so the band is stepped like the mosaic). Only the blocks inside the band are drawn.
    /// </summary>
    private static void DrawShimmer(DrawingContext context, Rect bounds, int cols, int rows, double phase, double strength)
    {
        var center = -ShimmerWidth + phase * (1 + 2 * ShimmerWidth);
        var cellWidth = bounds.Width / cols;
        var cellHeight = bounds.Height / rows;
        for (var row = 0; row < rows; row++)
        {
            var v = (row + 0.5) / rows;
            // The columns the band can reach on this row: |(u + v) / 2 − center| < width.
            var first = Math.Max(0, (int)Math.Floor(((center - ShimmerWidth) * 2 - v) * cols));
            var last = Math.Min(cols - 1, (int)Math.Ceiling(((center + ShimmerWidth) * 2 - v) * cols));
            for (var col = first; col <= last; col++)
            {
                var u = (col + 0.5) / cols;
                var distance = Math.Abs((u + v) / 2 - center) / ShimmerWidth;
                if (distance >= 1)
                {
                    continue;
                }
                var falloff = 1 - distance;
                var alpha = ShimmerStrength * strength * falloff * falloff;
                var step = (int)Math.Round(alpha / ShimmerStrength * (s_light.Length - 1));
                if (step <= 0)
                {
                    continue;
                }
                context.FillRectangle(s_light[Math.Min(step, s_light.Length - 1)],
                    new Rect(Math.Floor(col * cellWidth), Math.Floor(row * cellHeight), Math.Ceiling(cellWidth) + 1, Math.Ceiling(cellHeight) + 1));
            }
        }
    }

    /// <summary>
    /// The upscale sweep: a narrow band of crisp pixels moves left to right; behind it the image is sharp (the upscaled one
    /// once it has arrived), ahead of it soft (the finest mosaic level drawn smooth).
    /// </summary>
    private void DrawSweep(DrawingContext context, Bitmap image, ImageGenerationMosaic mosaic, Rect bounds)
    {
        var band = Math.Max(14, bounds.Width * 0.07);
        var x = -band + Math.Clamp(Sweep, 0, 1) * (bounds.Width + 2 * band);
        var left = Math.Clamp(x - band / 2, 0, bounds.Width);
        var right = Math.Clamp(x + band / 2, 0, bounds.Width);
        var finest = mosaic.Levels[ImageGenerationMosaic.LevelCount - 1];
        if (left > 0)
        {
            using (context.PushClip(new Rect(0, 0, left, bounds.Height)))
            {
                DrawCover(context, SweepImage ?? image, bounds);
            }
        }
        if (right > left)
        {
            using (context.PushClip(new Rect(left, 0, right - left, bounds.Height)))
            {
                DrawLevel(context, finest, bounds);
                context.FillRectangle(s_light[3], bounds);
            }
        }
        if (right < bounds.Width)
        {
            using (context.PushClip(new Rect(right, 0, bounds.Width - right, bounds.Height)))
            {
                DrawLevel(context, finest, bounds, smooth: true);
            }
        }
    }

    /// <summary>Draws the image to cover the bounds, cropped about its centre (the source rectangle is in pixels).</summary>
    private static void DrawCover(DrawingContext context, Bitmap image, Rect bounds)
    {
        var pixels = image.PixelSize;
        if (pixels.Width <= 0 || pixels.Height <= 0)
        {
            return;
        }
        double sourceWidth = pixels.Width;
        var sourceHeight = sourceWidth * bounds.Height / bounds.Width;
        if (sourceHeight > pixels.Height)
        {
            sourceHeight = pixels.Height;
            sourceWidth = sourceHeight * bounds.Width / bounds.Height;
        }
        var source = new Rect((pixels.Width - sourceWidth) / 2, (pixels.Height - sourceHeight) / 2, sourceWidth, sourceHeight);
        using (context.PushRenderOptions(s_smooth))
        {
            context.DrawImage(image, source, bounds);
        }
    }

    internal static double Smooth(double edge0, double edge1, double x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static ImmutableSolidColorBrush[] BuildLight()
    {
        var brushes = new ImmutableSolidColorBrush[16];
        for (var i = 0; i < brushes.Length; i++)
        {
            var alpha = (byte)Math.Round(ShimmerStrength * 255 * i / (brushes.Length - 1));
            brushes[i] = new ImmutableSolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));
        }
        return brushes;
    }

    /// <summary>Whether the band may move: full motion only (reduced keeps the stepped mosaic and drops the loop).</summary>
    internal bool AllowsShimmer => ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
}
