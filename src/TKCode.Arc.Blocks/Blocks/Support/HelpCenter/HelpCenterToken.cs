using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="HelpCenter"/> (an original TKCode design). Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class HelpCenterToken : AbstractControlDesignToken
{
    /// <summary>Padding inside the help center (28).</summary>
    public Thickness CenterPadding { get; set; }

    /// <summary>Below this width categories take one column and the table of contents hides (720).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Width of the table of contents beside an article (200).</summary>
    public double TocWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CenterPadding = new Thickness(28);
        NarrowWidth = 720;
        TocWidth = 200;
    }
}
