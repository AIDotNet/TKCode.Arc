using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TeamShowcase"/> and the one color it derives from the neutrals: the bio panel's translucent
/// surface. Radii, fonts and every color that follows the accent come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class TeamShowcaseToken : AbstractControlDesignToken
{
    /// <summary>The section's padding (24 80).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>The section's padding below the compact width (20 56).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>The section's padding below the narrow width (16 40).</summary>
    public Thickness NarrowSectionPadding { get; set; }

    /// <summary>The content is at most this wide (1120).</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The heading column is at most this wide (640).</summary>
    public double HeaderMaxWidth { get; set; }

    /// <summary>The space between the eyebrow, the title and the description (12).</summary>
    public double HeaderGap { get; set; }

    /// <summary>The title's line height (44; 34 when compact).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The title's line height below the compact width.</summary>
    public double CompactTitleLineHeight { get; set; }

    /// <summary>The description's line height (28).</summary>
    public double DescriptionLineHeight { get; set; }

    /// <summary>The space above the filter (0 32 0 0).</summary>
    public Thickness FilterMargin { get; set; }

    /// <summary>The space above the grid (0 40 0 0; 0 28 0 0 when compact).</summary>
    public Thickness GridMargin { get; set; }

    /// <summary>The space above the grid below the compact width.</summary>
    public Thickness CompactGridMargin { get; set; }

    /// <summary>The space between columns (20; 14 when narrow).</summary>
    public double ColumnGap { get; set; }

    /// <summary>The space between columns below the narrow width.</summary>
    public double NarrowColumnGap { get; set; }

    /// <summary>The space between rows (32; 24 when compact).</summary>
    public double RowGap { get; set; }

    /// <summary>The space between rows below the compact width.</summary>
    public double CompactRowGap { get; set; }

    /// <summary>The space around the name (0 14 0 2).</summary>
    public Thickness NameMargin { get; set; }

    /// <summary>The bio panel's distance from the portrait's edges (8).</summary>
    public Thickness BioMargin { get; set; }

    /// <summary>The bio panel's padding (14 12).</summary>
    public Thickness BioPadding { get; set; }

    /// <summary>The bio panel's surface: the raised surface at 94% so the portrait shows faintly through it.</summary>
    public Color BioBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        SectionPadding = new Thickness(24, 80);
        CompactSectionPadding = new Thickness(20, 56);
        NarrowSectionPadding = new Thickness(16, 40);
        ContentMaxWidth = 1120;
        HeaderMaxWidth = 640;
        HeaderGap = 12;
        TitleLineHeight = 44;
        CompactTitleLineHeight = 34;
        DescriptionLineHeight = 28;
        FilterMargin = new Thickness(0, 32, 0, 0);
        GridMargin = new Thickness(0, 40, 0, 0);
        CompactGridMargin = new Thickness(0, 28, 0, 0);
        ColumnGap = 20;
        NarrowColumnGap = 14;
        RowGap = 32;
        CompactRowGap = 24;
        NameMargin = new Thickness(0, 14, 0, 2);
        BioMargin = new Thickness(8);
        BioPadding = new Thickness(14, 12);
        BioBackground = Oklch.WithAlpha(n.SurfaceRaised, 0.94);
    }
}
