using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One cover of a <see cref="CoverFlow"/>: the picture cropped into a rounded face, a shade that deepens as it turns
/// away, and its reflection on the floor, drawn under the face from the same decoded picture. The stage writes its
/// perspective transform, shade and opacity every frame; the cover itself never lays out again.
/// </summary>
internal sealed class CoverFlowCover : Control
{
    // A fixed fade from the top of the reflection to nothing: built once, shared by every cover.
    private static readonly IBrush s_reflectionMask = new ImmutableLinearGradientBrush(
        [new ImmutableGradientStop(0, Colors.White), new ImmutableGradientStop(1, Colors.Transparent)],
        startPoint: new RelativePoint(0.5, 0, RelativeUnit.Relative),
        endPoint: new RelativePoint(0.5, 1, RelativeUnit.Relative));

    private readonly Border _face;
    private readonly ArcImage _image;
    private readonly Border _shade;
    private double _reflectionHeight;
    private double _reflectionGap;
    private double _reflectionOpacity;
    private double _radius;

    public CoverFlowCover()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        RenderTransform = Transform;
        _image = new ArcImage { Stretch = Stretch.UniformToFill };
        _image.PropertyChanged += (_, e) =>
        {
            if (e.Property == ArcImage.CurrentImageProperty)
            {
                InvalidateVisual();
            }
        };
        _face = new Border { ClipToBounds = true, Child = _image }.BindCornerRadius(ArcDesignTokenKind.RadiusStep8);
        ArcTokens.Bind(_face, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        _face.PropertyChanged += (_, e) =>
        {
            if (e.Property == Border.CornerRadiusProperty)
            {
                _radius = _face.CornerRadius.TopLeft;
                InvalidateVisual();
            }
        };
        _shade = new Border { Opacity = 0 }.BindCornerRadius(ArcDesignTokenKind.RadiusStep8);
        foreach (var child in (ReadOnlySpan<Control>)[_face, _shade])
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
    }

    /// <summary>The perspective transform the stage writes.</summary>
    internal MatrixTransform Transform { get; } = new();

    /// <summary>The stage's latest matrix (kept for hit testing).</summary>
    internal Matrix Projection;

    /// <summary>How far the cover is from the centre, in covers (the stage's latest frame).</summary>
    internal double Distance;

    internal int Index { get; set; }

    internal ImageSource? Source
    {
        get => _image.Source;
        set => _image.Source = value;
    }

    internal bool IsImageLoaded => _image.IsImageLoaded;

    /// <summary>Sets the darkness of the shade over the face (0 to 1).</summary>
    internal void SetShade(double amount)
    {
        if (Math.Abs(_shade.Opacity - amount) > 0.002)
        {
            _shade.Opacity = amount;
        }
    }

    internal void SetShadeBrush(IBrush? brush) => _shade.Background = brush;

    /// <summary>Sets the reflection: its height as a share of the cover's, the gap above it and its opacity (0 hides it).</summary>
    internal void SetReflection(double height, double gap, double opacity)
    {
        if (height == _reflectionHeight && gap == _reflectionGap && opacity == _reflectionOpacity)
        {
            return;
        }
        _reflectionHeight = height;
        _reflectionGap = gap;
        _reflectionOpacity = opacity;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _face.Measure(availableSize);
        _shade.Measure(availableSize);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var box = new Rect(finalSize);
        _face.Arrange(box);
        _shade.Arrange(box);
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (_reflectionOpacity <= 0 || _reflectionHeight <= 0 || size.Width <= 0 || size.Height <= 0 ||
            _image.CurrentImage is not { } picture || picture.Size.Width <= 0 || picture.Size.Height <= 0)
        {
            return;
        }
        // The face upside down under the cover, cut to the reflection's height and faded toward the floor.
        var height = size.Height * _reflectionHeight;
        var area = new Rect(0, size.Height + _reflectionGap, size.Width, height);
        var natural = picture.Size;
        var ratio = size.Width / size.Height;
        var crop = natural.Width / natural.Height > ratio
            ? new Size(natural.Height * ratio, natural.Height)
            : new Size(natural.Width, natural.Width / ratio);
        // The reflected strip is the bottom of the cover's crop.
        var share = height / size.Height;
        var source = new Rect((natural.Width - crop.Width) / 2, (natural.Height + crop.Height) / 2 - crop.Height * share,
            crop.Width, crop.Height * share);
        if (picture is Avalonia.Media.Imaging.Bitmap bitmap && bitmap.PixelSize.Width > 0)
        {
            var kx = bitmap.PixelSize.Width / natural.Width;
            var ky = bitmap.PixelSize.Height / natural.Height;
            source = new Rect(source.X * kx, source.Y * ky, source.Width * kx, source.Height * ky);
        }
        var radius = new Vector(_radius, _radius);
        using (context.PushOpacity(_reflectionOpacity))
        using (context.PushOpacityMask(s_reflectionMask, area))
        using (context.PushClip(new RoundedRect(area, radius, radius, radius, radius)))
        using (context.PushTransform(new Matrix(1, 0, 0, -1, 0, 2 * area.Y + area.Height)))
        {
            context.DrawImage(picture, source, area);
        }
    }
}
