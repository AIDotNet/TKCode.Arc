using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SpeculativeDecoding"/> (Arc Speculative decoding, design reference) around its drawn stage: the
/// card insets and section gaps (wide and narrow), the header, the statistics row and the controls row, plus the
/// ghost-chip text color, which mixes neutrals only. The lanes, chips, cells and answer lines are measured geometry kept
/// in <c>SpeculativeDecodingStage</c>; colors that follow the accent (the two model colors) come from the series tokens
/// in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SpeculativeDecodingToken : AbstractControlDesignToken
{
    /// <summary>The card's inset: 24 (16 below 560 px).</summary>
    public Thickness CardInset { get; set; }

    public Thickness NarrowCardInset { get; set; }

    /// <summary>Space between the header, the stage and the footer: 20 (16 narrow).</summary>
    public double SectionGap { get; set; }

    public double NarrowSectionGap { get; set; }

    /// <summary>The footer's top rule is followed by 20 (16 narrow) before the statistics.</summary>
    public Thickness FooterInset { get; set; }

    public Thickness NarrowFooterInset { get; set; }

    /// <summary>Between the title block and the buttons (16), between the buttons (8), under the title (2).</summary>
    public double HeaderGap { get; set; }

    public double ActionGap { get; set; }

    public double TitleGap { get; set; }

    /// <summary>The title: 16/21.6.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The status line: 14/19.6; narrow cards reserve two lines (39.2) so wrapping never moves the lanes.</summary>
    public double StatusLineHeight { get; set; }

    public double NarrowStatusMinHeight { get; set; }

    /// <summary>Statistics: three equal columns 16 apart (12 narrow), value 22/26.4 then 2 then the 12/16.8 label.</summary>
    public double StatColumnGap { get; set; }

    public double NarrowStatColumnGap { get; set; }

    public double StatValueLineHeight { get; set; }

    public double StatLabelGap { get; set; }

    public double StatLabelLineHeight { get; set; }

    /// <summary>Space between the statistics and the controls (16).</summary>
    public double FooterGap { get; set; }

    /// <summary>The controls row: the slider is at most 320 wide, 24 left of the switch; narrow cards stack them 12 apart.</summary>
    public double SliderMaxWidth { get; set; }

    public double ControlColumnGap { get; set; }

    public double ControlRowGap { get; set; }

    /// <summary>The text of a chip the draft has proposed and the target has not checked: 12% foreground into muted text.</summary>
    public Color GhostText { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardInset = new Thickness(24);
        NarrowCardInset = new Thickness(16);
        SectionGap = 20;
        NarrowSectionGap = 16;
        FooterInset = new Thickness(0, 20, 0, 0);
        NarrowFooterInset = new Thickness(0, 16, 0, 0);
        HeaderGap = 16;
        ActionGap = 8;
        TitleGap = 2;
        TitleLineHeight = 21.6;
        StatusLineHeight = 19.6;
        NarrowStatusMinHeight = 39.2;
        StatColumnGap = 16;
        NarrowStatColumnGap = 12;
        StatValueLineHeight = 26.4;
        StatLabelGap = 2;
        StatLabelLineHeight = 16.8;
        FooterGap = 16;
        SliderMaxWidth = 320;
        ControlColumnGap = 24;
        ControlRowGap = 12;
        GhostText = Oklch.MixOklab(n.TextMuted, n.Foreground, 0.12);
    }
}
