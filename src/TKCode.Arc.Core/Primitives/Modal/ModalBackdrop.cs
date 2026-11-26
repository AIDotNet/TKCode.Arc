using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TKCode.Arc.Primitives;

/// <summary>
/// The blurred backdrop behind the modals of a window: a snapshot of the content under the overlay layer, taken at half
/// size, blurred once and drawn scaled up over that content (at the bottom of the overlay layer, under every modal).
/// <see cref="ModalStack"/> fades it in and out with the scrim through <see cref="Visual.Opacity"/>.
/// </summary>
/// <remarks>
/// A blur set as the content's <see cref="Visual.Effect"/> re-blurs the whole window on every frame: on a Retina Metal
/// window each frame takes several window-sized scratch textures (3072x2048, about 24 MB each), and the driver keeps
/// them after the modal closes. The snapshot costs one half-size render and one small blur when the modal opens,
/// about 1.5 MB that is released when it closes, and nothing per frame. The content behind the modal is frozen while
/// it is blurred; <see cref="ModalStack"/> takes a new snapshot when the content is resized.
/// </remarks>
internal sealed class ModalBackdrop : Control
{
    /// <summary>Snapshot size relative to the content size in DIPs (a quarter of the pixels on a Retina display).</summary>
    internal const double SnapshotScale = 0.5;

    private readonly RenderTargetBitmap _blurred;
    private readonly Rect _sourceRect;

    private ModalBackdrop(RenderTargetBitmap blurred, Rect sourceRect)
    {
        _blurred = blurred;
        _sourceRect = sourceRect;
        IsHitTestVisible = false;
        Focusable = false;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.MediumQuality);
    }

    /// <summary>The size of the content the snapshot was taken from.</summary>
    public Size ContentSize { get; private init; }

    /// <summary>The blur radius (content DIPs) the snapshot was blurred with.</summary>
    public double Radius { get; private init; }

    /// <summary>
    /// Snapshots <paramref name="content"/> and blurs it by <paramref name="radius"/> DIPs; null when the content has no
    /// size or the platform cannot render to a bitmap (the modal then opens without a blur).
    /// </summary>
    public static ModalBackdrop? Capture(Visual content, double radius)
    {
        var size = content.Bounds.Size;
        if (size.Width < 1 || size.Height < 1 || radius <= 0)
        {
            return null;
        }
        var width = Math.Max(1, (int)Math.Ceiling(size.Width * SnapshotScale));
        var height = Math.Max(1, (int)Math.Ceiling(size.Height * SnapshotScale));
        var blurRadius = radius * SnapshotScale;
        // Room for the blur kernel on every side, filled by stretching the edge pixels outward: a blur fades to
        // transparent past its input, which would let the sharp content show through along the window edges.
        var margin = (int)Math.Ceiling(blurRadius * 2) + 2;
        RenderTargetBitmap? blurred = null;
        try
        {
            using var snapshot = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96 * SnapshotScale, 96 * SnapshotScale));
            snapshot.Render(content);

            var source = new EdgeExtendedImage(snapshot, width, height, margin) { Effect = ArcBlur.Get(blurRadius) };
            var full = new Size(width + 2 * margin, height + 2 * margin);
            source.Measure(full);
            source.Arrange(new Rect(full));
            blurred = new RenderTargetBitmap(new PixelSize(width + 2 * margin, height + 2 * margin), new Vector(96, 96));
            blurred.Render(source);
        }
        catch (Exception)
        {
            blurred?.Dispose();
            return null;
        }
        return new ModalBackdrop(blurred, new Rect(margin, margin, width, height)) { ContentSize = size, Radius = radius };
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            context.DrawImage(_blurred, _sourceRect, new Rect(Bounds.Size));
        }
    }

    /// <summary>Releases the blurred bitmap (its GPU surface).</summary>
    public void Release() => _blurred.Dispose();

    /// <summary>
    /// The snapshot drawn at its pixel size inside a margin that repeats its edge pixels: the input of the one-time
    /// blur. Rendered into a bitmap only, never mounted.
    /// </summary>
    private sealed class EdgeExtendedImage(Bitmap snapshot, int width, int height, int margin) : Control
    {
        public override void Render(DrawingContext context)
        {
            // Source rectangles of DrawImage are in the bitmap's pixels (not its DIPs, which are larger by
            // 1 / SnapshotScale); this control renders at 96 DPI, so its DIPs are the pixels of the blurred bitmap.
            static Rect Src(double x, double y, double w, double h) => new(x, y, w, h);
            double m = margin, w = width, h = height;

            context.DrawImage(snapshot, Src(0, 0, w, h), new Rect(m, m, w, h));
            // Edges: the outermost row or column stretched across the margin.
            context.DrawImage(snapshot, Src(0, 0, w, 1), new Rect(m, 0, w, m));
            context.DrawImage(snapshot, Src(0, h - 1, w, 1), new Rect(m, m + h, w, m));
            context.DrawImage(snapshot, Src(0, 0, 1, h), new Rect(0, m, m, h));
            context.DrawImage(snapshot, Src(w - 1, 0, 1, h), new Rect(m + w, m, m, h));
            // Corners: the corner pixel.
            context.DrawImage(snapshot, Src(0, 0, 1, 1), new Rect(0, 0, m, m));
            context.DrawImage(snapshot, Src(w - 1, 0, 1, 1), new Rect(m + w, 0, m, m));
            context.DrawImage(snapshot, Src(0, h - 1, 1, 1), new Rect(0, m + h, m, m));
            context.DrawImage(snapshot, Src(w - 1, h - 1, 1, 1), new Rect(m + w, m + h, m, m));
        }
    }
}
