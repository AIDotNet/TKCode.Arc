using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ProductDetail"/>.</summary>
[ControlDesignToken]
internal sealed class ProductDetailToken : AbstractControlDesignToken
{
    /// <summary>Space inside the block's card.</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>Width of the picture column beside the information.</summary>
    public double GalleryWidth { get; set; }

    /// <summary>Space around the picture column beside the information (the gap is on its right).</summary>
    public Thickness GalleryMargin { get; set; }

    /// <summary>Space around the picture column when it sits above the information.</summary>
    public Thickness GalleryStackedMargin { get; set; }

    /// <summary>Block width below which the pictures sit above the information.</summary>
    public double CompactThreshold { get; set; }

    /// <summary>Side of a thumbnail.</summary>
    public double ThumbSize { get; set; }

    /// <summary>Diameter of a color swatch.</summary>
    public double SwatchSize { get; set; }

    /// <summary>Smallest width of a size button.</summary>
    public double SizeButtonWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(24);
        GalleryWidth = 400;
        GalleryMargin = new Thickness(0, 0, 32, 0);
        GalleryStackedMargin = new Thickness(0, 0, 0, 24);
        CompactThreshold = 820;
        ThumbSize = 68;
        SwatchSize = 30;
        SizeButtonWidth = 52;
    }
}
