namespace TKCode.Arc;

/// <summary>One photo of a <see cref="PhotoGrid"/>.</summary>
public sealed class PhotoGridPhoto
{
    /// <summary>The picture.</summary>
    public ImageSource? Source { get; set; }

    /// <summary>
    /// The picture's width divided by its height. Giving it lets the grid lay the photo out before it has loaded; 0 (the
    /// default) uses the loaded picture's own proportions, and 3:2 until then.
    /// </summary>
    public double AspectRatio { get; set; }

    /// <summary>What the photo shows, read by assistive technology.</summary>
    public string? Description { get; set; }
}
