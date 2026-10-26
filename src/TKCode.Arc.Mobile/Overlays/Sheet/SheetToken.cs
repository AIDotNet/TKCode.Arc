using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Sheet"/>: the grabber, the corner radius of the panel and the scrim color.</summary>
[ControlDesignToken]
internal sealed class SheetToken : AbstractControlDesignToken
{
    /// <summary>Color of the scrim behind the panel at full opacity.</summary>
    public Color ScrimColor { get; set; }

    /// <summary>Corner radius of the panel's top corners: 16.</summary>
    public CornerRadius PanelRadius { get; set; }

    /// <summary>Width of the grabber: 36.</summary>
    public double GrabberWidth { get; set; }

    /// <summary>Height of the grabber: 5.</summary>
    public double GrabberHeight { get; set; }

    /// <summary>Padding of the handle area (grabber and title) that a finger drags: 16 at the sides, 8 above, 8 below.</summary>
    public Thickness HandlePadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ScrimColor = isDarkMode ? Oklch.ToColor(0, 0, 0, 0.52) : Oklch.ToColor(0.16, 0, 0, 0.34);
        PanelRadius = new CornerRadius(16, 16, 0, 0);
        GrabberWidth = 36;
        GrabberHeight = 5;
        HandlePadding = new Thickness(16, 8);
    }
}
