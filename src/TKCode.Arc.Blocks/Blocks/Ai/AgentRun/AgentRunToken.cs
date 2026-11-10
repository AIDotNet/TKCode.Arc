using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="AgentRun"/>. Colors, radii and fonts come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AgentRunToken : AbstractControlDesignToken
{
    /// <summary>The widest the run grows (720).</summary>
    public double MaxWidth { get; set; }

    /// <summary>Padding of the card's sections (20 on the sides).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>The narrow card's section padding (16 on the sides).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>At or below this width the facts move under the state line (480).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The timeline scrolls past this height (420).</summary>
    public double TimelineMaxHeight { get; set; }

    /// <summary>The width of the marker column of the timeline (28).</summary>
    public double RailWidth { get; set; }

    /// <summary>The diameter of a step's marker (24).</summary>
    public double MarkerSize { get; set; }

    /// <summary>The glyph inside a marker (13).</summary>
    public double MarkerGlyphSize { get; set; }

    /// <summary>The width of the line joining the markers (1.5).</summary>
    public double RailThickness { get; set; }

    /// <summary>The minimum height of a step's header row (32).</summary>
    public double StepHeaderHeight { get; set; }

    /// <summary>Padding of a step's header (8 on the sides).</summary>
    public Thickness StepHeaderPadding { get; set; }

    /// <summary>Space under each step (6).</summary>
    public Thickness StepMargin { get; set; }

    /// <summary>Padding of a code block in a step's detail (12, 10).</summary>
    public Thickness CodePadding { get; set; }

    /// <summary>The height of the step progress bar (4) and the gap between its segments (3).</summary>
    public double ProgressHeight { get; set; }

    /// <inheritdoc cref="ProgressHeight"/>
    public double ProgressGap { get; set; }

    /// <summary>How far a new step rises while it fades in (6).</summary>
    public double EnterOffset { get; set; }

    /// <summary>How often the clock is redrawn while the run works (250 ms; it shows whole seconds).</summary>
    public TimeSpan ClockInterval { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxWidth = 720;
        SectionPadding = new Thickness(20, 16);
        CompactSectionPadding = new Thickness(16, 14);
        CompactWidth = 480;
        TimelineMaxHeight = 420;
        RailWidth = 28;
        MarkerSize = 24;
        MarkerGlyphSize = 13;
        RailThickness = 1.5;
        StepHeaderHeight = 32;
        StepHeaderPadding = new Thickness(8, 4);
        StepMargin = new Thickness(0, 0, 0, 6);
        CodePadding = new Thickness(12, 10);
        ProgressHeight = 4;
        ProgressGap = 3;
        EnterOffset = 6;
        ClockInterval = TimeSpan.FromMilliseconds(250);
    }
}
