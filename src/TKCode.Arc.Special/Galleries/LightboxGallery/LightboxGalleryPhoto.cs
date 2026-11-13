using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One picture of the lightbox, laid out at the largest size that fits (its own proportions) and centred. It loads the
/// full picture and, until that arrives, shows the thumbnail. It draws the picture itself: while a
/// <see cref="SharedElementScope"/> stretches it between a square tile and its own shape, it draws the crop that the
/// stretch turns back into an undistorted picture, so the flight looks like the tile's crop opening up rather than a
/// squashed photo.
/// </summary>
internal sealed class LightboxGalleryPhoto : Control
{
    private readonly ArcImage _full;
    private readonly ArcImage _preview;
    private double _hintRatio = 1;
    private double _flightRadius;

    public LightboxGalleryPhoto()
    {
        // The loaders are laid out over the photo (so they decode for its size) but never drawn.
        _full = new ArcImage { Stretch = Stretch.Uniform, Opacity = 0, IsHitTestVisible = false };
        _preview = new ArcImage { Stretch = Stretch.Uniform, Opacity = 0, IsHitTestVisible = false };
        foreach (var image in (ReadOnlySpan<ArcImage>)[_preview, _full])
        {
            image.PropertyChanged += OnImagePropertyChanged;
            VisualChildren.Add(image);
            LogicalChildren.Add(image);
        }
    }

    /// <summary>True when this photo is the flying one (the middle slot); the neighbours are not keyed.</summary>
    internal bool IsFlightTarget
    {
        set => SharedElement.SetKey(this, value ? LightboxGallery.FlightKey : null);
    }

    /// <summary>The picture being shown (the full one once it has loaded).</summary>
    internal IImage? Picture => _full.CurrentImage ?? _preview.CurrentImage;

    internal bool IsFullLoaded => _full.IsImageLoaded;

    internal void SetSources(ImageSource? full, ImageSource? preview, double ratioHint)
    {
        _hintRatio = ratioHint > 0 && double.IsFinite(ratioHint) ? ratioHint : 1;
        _full.Source = full;
        _preview.Source = preview is not null && !ReferenceEquals(preview, full) ? preview : null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnImagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ArcImage.CurrentImageProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private double Ratio
    {
        get
        {
            var size = Picture?.Size ?? default;
            return size.Width > 0 && size.Height > 0 ? size.Width / size.Height : _hintRatio;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var ratio = Ratio;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 800;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : width / ratio;
        var fitted = width / height > ratio ? new Size(height * ratio, height) : new Size(width, width / ratio);
        _full.Measure(fitted);
        _preview.Measure(fitted);
        return fitted;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var box = new Rect(finalSize);
        _full.Arrange(box);
        _preview.Arrange(box);
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        if (Picture is not { } picture || picture.Size.Width <= 0 || picture.Size.Height <= 0)
        {
            context.FillRectangle(Brushes.Transparent, bounds);
            return;
        }
        // The box may be stretched by a flight (sx, sy): the crop has the proportions the box has on screen.
        var (sx, sy) = Stretched();
        var screenRatio = bounds.Width * sx / (bounds.Height * sy);
        var natural = picture.Size;
        var crop = natural.Width / natural.Height > screenRatio
            ? new Size(natural.Height * screenRatio, natural.Height)
            : new Size(natural.Width, natural.Width / screenRatio);
        var source = new Rect((natural.Width - crop.Width) / 2, (natural.Height - crop.Height) / 2, crop.Width, crop.Height);
        if (picture is Avalonia.Media.Imaging.Bitmap bitmap && bitmap.PixelSize.Width > 0)
        {
            // Bitmaps take their source rectangle in pixels.
            var kx = bitmap.PixelSize.Width / natural.Width;
            var ky = bitmap.PixelSize.Height / natural.Height;
            source = new Rect(source.X * kx, source.Y * ky, source.Width * kx, source.Height * ky);
        }
        if (_flightRadius > 0.01)
        {
            // Corners stay round on screen under the stretch.
            var radii = new Vector(_flightRadius / sx, _flightRadius / sy);
            using (context.PushClip(new RoundedRect(bounds, radii, radii, radii, radii)))
            {
                context.DrawImage(picture, source, bounds);
            }
            return;
        }
        context.DrawImage(picture, source, bounds);
    }

    /// <summary>The on-screen corner radius while flying (the tile's radius melting to square corners).</summary>
    internal void SetFlightRadius(double radius)
    {
        radius = Math.Max(0, radius);
        if (Math.Abs(radius - _flightRadius) < 0.01)
        {
            return;
        }
        _flightRadius = radius;
        InvalidateVisual();
    }

    private (double X, double Y) Stretched()
    {
        if (RenderTransform is not { } transform)
        {
            return (1, 1);
        }
        var m = transform.Value;
        var sx = Math.Sqrt(m.M11 * m.M11 + m.M12 * m.M12);
        var sy = Math.Sqrt(m.M21 * m.M21 + m.M22 * m.M22);
        return sx > 1e-6 && sy > 1e-6 ? (sx, sy) : (1, 1);
    }

    /// <summary>Redraws for a new flight stretch.</summary>
    internal void OnFlightMoved() => InvalidateVisual();
}
