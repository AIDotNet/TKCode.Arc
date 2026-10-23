using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ModelArena"/>, measured from the model arena of the AI group (design reference only, ADR 0015;
/// spec section 3). Colors, radii and fonts come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ModelArenaToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (800).</summary>
    public double MaxWidth { get; set; }

    /// <summary>The header (52 high; 8 10 0 20 inside).</summary>
    public double HeadHeight { get; set; }

    /// <summary>Space inside the header.</summary>
    public Thickness HeadPadding { get; set; }

    /// <summary>Space around the prompt row (6 20 14).</summary>
    public Thickness PromptPadding { get; set; }

    /// <summary>The prompt box (48 high).</summary>
    public double FieldHeight { get; set; }

    /// <summary>Between the prompt box and the chips (10).</summary>
    public double ChipsGap { get; set; }

    /// <summary>The sample prompt chips (28 high).</summary>
    public double ChipHeight { get; set; }

    /// <summary>A pane's header (48 high; 4 12 0 20 inside).</summary>
    public double PaneHeadHeight { get; set; }

    /// <summary>Space inside a pane's header.</summary>
    public Thickness PaneHeadPadding { get; set; }

    /// <summary>A pane's answer area (272 high, fixed).</summary>
    public double AnswerHeight { get; set; }

    /// <summary>Space inside the answer area (8 20 18).</summary>
    public Thickness AnswerPadding { get; set; }

    /// <summary>A pane's metrics row (39 high; 8 20 14 inside).</summary>
    public double MetricsHeight { get; set; }

    /// <summary>Space inside the metrics row.</summary>
    public Thickness MetricsPadding { get; set; }

    /// <summary>Space inside the vote bar (12 12 12 20).</summary>
    public Thickness VoteBarPadding { get; set; }

    /// <summary>A vote row's height (36).</summary>
    public double VoteRowHeight { get; set; }

    /// <summary>Below this container width the panes are two tabs (560).</summary>
    public double NarrowBreakpoint { get; set; }

    /// <summary>Below this container width the layout is compact (480).</summary>
    public double CompactBreakpoint { get; set; }

    /// <summary>The standings popover's width (320).</summary>
    public double PopoverWidth { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxWidth = 800;
        HeadHeight = 52;
        HeadPadding = new Thickness(20, 8, 10, 0);
        PromptPadding = new Thickness(20, 6, 20, 14);
        FieldHeight = 48;
        ChipsGap = 10;
        ChipHeight = 28;
        PaneHeadHeight = 48;
        PaneHeadPadding = new Thickness(20, 4, 12, 0);
        AnswerHeight = 272;
        AnswerPadding = new Thickness(20, 8, 20, 18);
        MetricsHeight = 39;
        MetricsPadding = new Thickness(20, 8, 20, 14);
        VoteBarPadding = new Thickness(20, 12, 12, 12);
        VoteRowHeight = 36;
        NarrowBreakpoint = 560;
        CompactBreakpoint = 480;
        PopoverWidth = 320;
    }
}
