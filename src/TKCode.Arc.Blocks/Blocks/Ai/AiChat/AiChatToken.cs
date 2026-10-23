using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="AiChat"/>, measured from the AI chat of the AI group (design reference only, ADR
/// 0015). Colors, radii and fonts come from ArcDesign tokens in the theme. The breakpoints and the side paddings follow
/// the chat's own width, not the window's.
/// </summary>
[ControlDesignToken]
internal sealed class AiChatToken : AbstractControlDesignToken
{
    /// <summary>Height of the header, its 1px bottom edge included (52).</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Space at the header's right edge (10); the left one follows the thread's side padding.</summary>
    public double HeaderEndPadding { get; set; }

    /// <summary>Space before the model pill and the new chat button (2 on the left).</summary>
    public Thickness HeaderControlMargin { get; set; }

    /// <summary>The header's pills and the new chat button are this high (34).</summary>
    public double HeaderControlHeight { get; set; }

    /// <summary>The model pill's padding in a compact chat (0 6 0 8).</summary>
    public Thickness CompactModelPillPadding { get; set; }

    /// <summary>The new chat button's icon (18, stroke 1.75).</summary>
    public double IconGlyphSize { get; set; }

    /// <summary>Stroke of the header icons (1.75).</summary>
    public double GlyphStroke { get; set; }

    /// <summary>Space above the first turn (24).</summary>
    public double ThreadTopPadding { get; set; }

    /// <summary>The same in a narrow chat (18).</summary>
    public double NarrowThreadTopPadding { get; set; }

    /// <summary>Space below the last turn (12).</summary>
    public double ThreadBottomPadding { get; set; }

    /// <summary>The thread's side padding is this share of the chat's width (4.5%) ...</summary>
    public double ThreadSideRatio { get; set; }

    /// <summary>... but at least this (16) ...</summary>
    public double ThreadSideMin { get; set; }

    /// <summary>... and at most this (28).</summary>
    public double ThreadSideMax { get; set; }

    /// <summary>The header's left padding is the thread's side padding less this (10) ...</summary>
    public double HeaderSideOffset { get; set; }

    /// <summary>... but at least this (6).</summary>
    public double HeaderSideMin { get; set; }

    /// <summary>The composer dock's side padding is the thread's less this (8) ...</summary>
    public double DockSideOffset { get; set; }

    /// <summary>... but at least this (8).</summary>
    public double DockSideMin { get; set; }

    /// <summary>Space above the composer (6).</summary>
    public double DockTopPadding { get; set; }

    /// <summary>Space below the composer (12).</summary>
    public double DockBottomPadding { get; set; }

    /// <summary>Space between turns (28).</summary>
    public double TurnGap { get; set; }

    /// <summary>The same in a narrow chat (24).</summary>
    public double NarrowTurnGap { get; set; }

    /// <summary>Space between the person's row and the reply (14).</summary>
    public double TurnBodyGap { get; set; }

    /// <summary>Space between the parts of a reply (10).</summary>
    public double ReplyGap { get; set; }

    /// <summary>Space between tool chips (6).</summary>
    public double ToolGap { get; set; }

    /// <summary>The model mark of a reply (16, in a 22px cell).</summary>
    public double ReplyMarkSize { get; set; }

    /// <summary>At or below this width the chat is narrow (480).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>At or below this width the chat is compact (360).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Another conversation rises this far while it fades in (4; out 100ms ease-out, in 160ms standard).</summary>
    public double PaneEnterRise { get; set; }

    /// <summary>A long conversation first shows this many of its newest turns; older ones are made as the person scrolls up (30).</summary>
    public int InitialTurns { get; set; }

    /// <summary>How many older turns are made at a time (20).</summary>
    public int EarlierTurns { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderHeight = 52;
        HeaderEndPadding = 10;
        HeaderControlMargin = new Thickness(2, 0, 0, 0);
        HeaderControlHeight = 34;
        CompactModelPillPadding = new Thickness(8, 0, 6, 0);
        IconGlyphSize = 18;
        GlyphStroke = 1.75;
        ThreadTopPadding = 24;
        NarrowThreadTopPadding = 18;
        ThreadBottomPadding = 12;
        ThreadSideRatio = 0.045;
        ThreadSideMin = 16;
        ThreadSideMax = 28;
        HeaderSideOffset = 10;
        HeaderSideMin = 6;
        DockSideOffset = 8;
        DockSideMin = 8;
        DockTopPadding = 6;
        DockBottomPadding = 12;
        TurnGap = 28;
        NarrowTurnGap = 24;
        TurnBodyGap = 14;
        ReplyGap = 10;
        ToolGap = 6;
        ReplyMarkSize = 16;
        NarrowWidth = 480;
        CompactWidth = 360;
        PaneEnterRise = 4;
        InitialTurns = 30;
        EarlierTurns = 20;
    }
}
