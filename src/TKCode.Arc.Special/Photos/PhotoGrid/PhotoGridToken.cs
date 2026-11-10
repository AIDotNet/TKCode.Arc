using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="PhotoGrid"/>. Its colors and corner radii come from the ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class PhotoGridToken : AbstractControlDesignToken
{
    /// <summary>Space between photos, both ways.</summary>
    public double PhotoSpacing { get; set; }

    /// <summary>The height a justified row aims for before it is stretched to the full width.</summary>
    public double TargetRowHeight { get; set; }

    /// <summary>The narrowest a masonry column gets before the grid drops a column.</summary>
    public double MinColumnWidth { get; set; }

    /// <summary>The tallest the selected photo is drawn on its own row.</summary>
    public double SelectedMaxHeight { get; set; }

    /// <summary>Width of the accent ring around the selected photo.</summary>
    public Thickness SelectedRingThickness { get; set; }

    /// <summary>Margin of a photo's keyboard focus ring.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PhotoSpacing = 6;
        TargetRowHeight = 200;
        MinColumnWidth = 220;
        SelectedMaxHeight = 520;
        SelectedRingThickness = new Thickness(2);
        FocusRingMargin = new Thickness(-3);
    }
}
