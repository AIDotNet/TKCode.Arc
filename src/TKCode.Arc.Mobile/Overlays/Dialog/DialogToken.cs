using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Dialog"/>: the scrim color, the panel's width and padding.</summary>
[ControlDesignToken]
internal sealed class DialogToken : AbstractControlDesignToken
{
    /// <summary>Color of the scrim behind the panel at full opacity.</summary>
    public Color ScrimColor { get; set; }

    /// <summary>The widest the panel gets: 320.</summary>
    public double PanelMaxWidth { get; set; }

    /// <summary>Space kept free between the panel and the edges of the page: 24.</summary>
    public Thickness PanelMargin { get; set; }

    /// <summary>Padding inside the panel: 20 at the sides, 20 above, 12 below.</summary>
    public Thickness PanelPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ScrimColor = isDarkMode ? Oklch.ToColor(0, 0, 0, 0.52) : Oklch.ToColor(0.16, 0, 0, 0.34);
        PanelMaxWidth = 320;
        PanelMargin = new Thickness(24);
        PanelPadding = new Thickness(20, 20, 20, 12);
    }
}
