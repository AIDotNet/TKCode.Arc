namespace TKCode.Arc;

/// <summary>One picture of a <see cref="LightboxGallery"/>.</summary>
public sealed class LightboxGalleryImage
{
    /// <summary>The full picture, shown in the lightbox.</summary>
    public ImageSource? Source { get; set; }

    /// <summary>A smaller copy for the grid tile, also shown in the lightbox until the full picture arrives; null uses <see cref="Source"/>.</summary>
    public ImageSource? Thumbnail { get; set; }

    /// <summary>A line shown under the picture in the lightbox.</summary>
    public string? Caption { get; set; }

    /// <summary>What the picture shows, read by assistive technology; null reads the <see cref="Caption"/>.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The picture's width divided by its height, if known. It shapes the lightbox before the picture has loaded; 0 waits
    /// for the picture.
    /// </summary>
    public double AspectRatio { get; set; }
}
