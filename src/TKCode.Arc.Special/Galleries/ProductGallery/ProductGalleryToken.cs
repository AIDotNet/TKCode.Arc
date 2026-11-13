using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ProductGallery"/>. Its colors, corner radii and shadows come from the ArcDesign tokens in the
/// theme.
/// </summary>
[ControlDesignToken]
internal sealed class ProductGalleryToken : AbstractControlDesignToken
{
    /// <summary>Side of a square thumbnail.</summary>
    public double ThumbSize { get; set; }

    /// <summary>Space between two thumbnails.</summary>
    public double ThumbSpacing { get; set; }

    /// <summary>Space between the stage and the thumbnail strip.</summary>
    public Thickness StripMargin { get; set; }

    /// <summary>Space between the strip and the stage when the strip runs down the left side.</summary>
    public Thickness StripMarginLeft { get; set; }

    /// <summary>Space between two pictures while they slide past each other.</summary>
    public double SlideGap { get; set; }

    /// <summary>Width of the ring around the selected thumbnail.</summary>
    public Thickness ThumbRingThickness { get; set; }

    /// <summary>Space between the stage's edge and the overlaid buttons and counter.</summary>
    public Thickness OverlayMargin { get; set; }

    /// <summary>Padding of the image counter pill.</summary>
    public Thickness CounterPadding { get; set; }

    /// <summary>Margin of the keyboard focus ring (outside the face it surrounds).</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Opacity of a thumbnail that is not selected.</summary>
    public double IdleThumbOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ThumbSize = 64;
        ThumbSpacing = 8;
        StripMargin = new Thickness(0, 12, 0, 0);
        StripMarginLeft = new Thickness(0, 0, 12, 0);
        SlideGap = 16;
        ThumbRingThickness = new Thickness(2);
        OverlayMargin = new Thickness(12);
        CounterPadding = new Thickness(10, 4);
        FocusRingMargin = new Thickness(-3);
        IdleThumbOpacity = 0.72;
    }
}
