using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CoverFlow"/>: the perspective, the spacing of the covers and their reflection. Text colors,
/// corner radii and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CoverFlowToken : AbstractControlDesignToken
{
    /// <summary>Distance from the eye to the covers, in px: smaller is a stronger perspective.</summary>
    public double Perspective { get; set; }

    /// <summary>Distance between two covers on a side, in px.</summary>
    public double SideSpacing { get; set; }

    /// <summary>Distance from the centred cover to its first neighbour, as a share of the cover's width.</summary>
    public double CenterGap { get; set; }

    /// <summary>How large a turned cover is next to the centred one.</summary>
    public double SideScale { get; set; }

    /// <summary>Height of the reflection, as a share of the cover's height.</summary>
    public double ReflectionHeight { get; set; }

    /// <summary>Space between a cover and its reflection.</summary>
    public double ReflectionGap { get; set; }

    /// <summary>Opacity at the top of the reflection.</summary>
    public double ReflectionOpacity { get; set; }

    /// <summary>The shade laid over a turned cover, which deepens with its angle.</summary>
    public Color ShadeColor { get; set; }

    /// <summary>The most a turned cover is shaded (0 to 1).</summary>
    public double MaxShade { get; set; }

    /// <summary>Space around the stage: above the covers and between the stage and the caption.</summary>
    public Thickness StagePadding { get; set; }

    /// <summary>Space between the title and the subtitle.</summary>
    public double CaptionSpacing { get; set; }

    /// <summary>Margin of the keyboard focus ring.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Perspective = 650;
        SideSpacing = 52;
        CenterGap = 0.62;
        SideScale = 0.86;
        ReflectionHeight = 0.32;
        ReflectionGap = 3;
        ReflectionOpacity = isDarkMode ? 0.32 : 0.26;
        ShadeColor = Color.FromRgb(0, 0, 0);
        MaxShade = isDarkMode ? 0.45 : 0.3;
        StagePadding = new Thickness(0, 28, 0, 12);
        CaptionSpacing = 2;
        FocusRingMargin = new Thickness(-3);
    }
}
