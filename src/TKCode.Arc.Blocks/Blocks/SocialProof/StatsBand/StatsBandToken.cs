using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of the <see cref="StatsBand"/> block (stats-band.module.css).</summary>
[ControlDesignToken]
internal sealed class StatsBandToken : AbstractControlDesignToken
{
    /// <summary>The band's content is at most 1120 wide.</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The height of a visual (56).</summary>
    public double VisualHeight { get; set; }

    /// <summary>The space above a visual (32).</summary>
    public Thickness VisualMargin { get; set; }

    /// <summary>The space under the number (20).</summary>
    public Thickness FigureMargin { get; set; }

    /// <summary>The space between the label and the caption (2).</summary>
    public Thickness CaptionMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ContentMaxWidth = 1120;
        VisualHeight = 56;
        VisualMargin = new Thickness(0, 32, 0, 0);
        FigureMargin = new Thickness(0, 0, 0, 20);
        CaptionMargin = new Thickness(0, 2, 0, 0);
    }
}
