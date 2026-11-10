using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SupportWidget"/> (an original TKCode design). Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class SupportWidgetToken : AbstractControlDesignToken
{
    /// <summary>Width of the open panel (380).</summary>
    public double PanelWidth { get; set; }

    /// <summary>Height of the open panel (560).</summary>
    public double PanelHeight { get; set; }

    /// <summary>Diameter of the round launcher (56).</summary>
    public double LauncherSize { get; set; }

    /// <summary>Space between the widget and the corner it sits in (20).</summary>
    public Thickness CornerMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PanelWidth = 380;
        PanelHeight = 560;
        LauncherSize = 56;
        CornerMargin = new Thickness(20);
    }
}
