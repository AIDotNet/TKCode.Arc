using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ScrollStory"/>: paddings, the step slots, the gap between the columns and the stage's shape.
/// Radii, fonts and colors come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ScrollStoryToken : AbstractControlDesignToken
{
    /// <summary>The section's padding (24 80).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>The section's padding below the compact width (16 48).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>The content is at most this wide (1120).</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The heading column is at most this wide (640).</summary>
    public double HeaderMaxWidth { get; set; }

    /// <summary>The space between the eyebrow, the title and the description (12).</summary>
    public double HeaderGap { get; set; }

    /// <summary>The space between the heading and the story (0 0 0 32).</summary>
    public Thickness HeaderMargin { get; set; }

    /// <summary>The title's line height at 36 (44).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The title's line height at 28, below the compact width (34).</summary>
    public double CompactTitleLineHeight { get; set; }

    /// <summary>Each step's slot is at least this tall, so steps pass the reading line one by one (380).</summary>
    public double StepMinHeight { get; set; }

    /// <summary>The space between the steps and the stage (56).</summary>
    public double ColumnGap { get; set; }

    /// <summary>The space between stacked steps (40).</summary>
    public double StackedGap { get; set; }

    /// <summary>The stage's height as a share of its width (0.8).</summary>
    public double StageAspect { get; set; }

    /// <summary>A stacked step's own picture is this tall (200).</summary>
    public double InlineSceneHeight { get; set; }

    /// <summary>The step title's line height at 28 (34).</summary>
    public double StepTitleLineHeight { get; set; }

    /// <summary>The step body's line height (27).</summary>
    public double StepBodyLineHeight { get; set; }

    /// <summary>A step's opacity while another step is active (0.35).</summary>
    public double InactiveOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SectionPadding = new Thickness(24, 80);
        CompactSectionPadding = new Thickness(16, 48);
        ContentMaxWidth = 1120;
        HeaderMaxWidth = 640;
        HeaderGap = 12;
        HeaderMargin = new Thickness(0, 0, 0, 32);
        TitleLineHeight = 44;
        CompactTitleLineHeight = 34;
        StepMinHeight = 380;
        ColumnGap = 56;
        StackedGap = 40;
        StageAspect = 0.8;
        InlineSceneHeight = 200;
        StepTitleLineHeight = 34;
        StepBodyLineHeight = 27;
        InactiveOpacity = 0.35;
    }
}
