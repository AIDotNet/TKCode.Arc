namespace TKCode.Arc;

/// <summary>One picture of a <see cref="ProductGallery"/>: the full image, an optional smaller thumbnail and its description.</summary>
public sealed class ProductGalleryImage
{
    /// <summary>The picture shown on the stage.</summary>
    public ImageSource? Source { get; set; }

    /// <summary>A smaller copy for the thumbnail strip; null uses <see cref="Source"/>.</summary>
    public ImageSource? Thumbnail { get; set; }

    /// <summary>What the picture shows, read by assistive technology ("Side view, brushed steel").</summary>
    public string? Description { get; set; }
}
