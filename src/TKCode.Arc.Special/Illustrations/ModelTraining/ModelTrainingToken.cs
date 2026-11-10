using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ModelTraining"/>: the header's line heights, the caption's reserved lines, the space around the
/// stage and the footer's gaps. Colors, radii and type come from the ArcDesign tokens in the theme; the diagram is laid
/// out from the measured width.
/// </summary>
[ControlDesignToken]
internal sealed class ModelTrainingToken : AbstractControlDesignToken
{
    /// <summary>The title's line height (17.6) and the header row's height (22.4).</summary>
    public double TitleLineHeight { get; set; }

    public double HeaderRowHeight { get; set; }

    /// <summary>The caption sits 4 under the title and always keeps two lines (39.2).</summary>
    public Thickness CaptionMargin { get; set; }

    public double CaptionMinHeight { get; set; }

    /// <summary>Space between the header and the stage, and between the stage and the footer (20).</summary>
    public double SectionGap { get; set; }

    /// <summary>The footer: buttons 6 apart, the speed words 12 before the control.</summary>
    public double ButtonGap { get; set; }

    public double SpeedGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TitleLineHeight = 17.6;
        HeaderRowHeight = 22.4;
        CaptionMargin = new Thickness(0, 4, 0, 0);
        CaptionMinHeight = 39.2;
        SectionGap = 20;
        ButtonGap = 6;
        SpeedGap = 12;
    }
}
