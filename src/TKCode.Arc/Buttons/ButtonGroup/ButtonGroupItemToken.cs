using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ButtonGroupItemToken : AbstractControlDesignToken
{
    /// <summary>Segment height and minimum width inside the 1px group border: the control height minus 2 (42 / 34px).</summary>
    public double SegmentExtentMD { get; set; }
    public double SegmentExtentSM { get; set; }

    /// <summary>Inline padding (14 / 12px).</summary>
    public Thickness SegmentInsetMD { get; set; }
    public Thickness SegmentInsetSM { get; set; }

    /// <summary>Gap between icon and label (space-2 / 6px).</summary>
    public double LabelGapMD { get; set; }
    public double LabelGapSM { get; set; }

    /// <summary>Space a reserved label keeps for the icon: 16px glyph plus the gap.</summary>
    public double SizerLeadingMD { get; set; }
    public double SizerLeadingSM { get; set; }

    /// <summary>Hairline divider insets: top and bottom 11 / 9px (straddling the seam), sides 10px when vertical.</summary>
    public Thickness DividerMarginMD { get; set; }
    public Thickness DividerMarginSM { get; set; }
    public Thickness DividerMarginVertical { get; set; }

    /// <summary>Solid group: hairline dividers (background 20% into foreground).</summary>
    public Color SolidDividerColor { get; set; }

    /// <summary>Content of a disabled segment (it stays focusable).</summary>
    public double InertContentOpacity { get; set; }

    /// <summary>Clip overflow while an unreserved label springs the slot (Arc: inset −space-3).</summary>
    public double MorphOverflow { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SegmentExtentMD = 42;
        SegmentExtentSM = 34;
        SegmentInsetMD = new Thickness(14, 0);
        SegmentInsetSM = new Thickness(12, 0);
        LabelGapMD = 8;
        LabelGapSM = 6;
        SizerLeadingMD = 24;
        SizerLeadingSM = 22;
        DividerMarginMD = new Thickness(-0.5, 11, 0, 11);
        DividerMarginSM = new Thickness(-0.5, 9, 0, 9);
        DividerMarginVertical = new Thickness(10, -0.5, 10, 0);
        SolidDividerColor = Oklch.MixOklab(ArcPalette.Neutrals(isDarkMode).Foreground, ArcPalette.Neutrals(isDarkMode).Background, 0.20);
        InertContentOpacity = 0.4;
        MorphOverflow = 12;
    }
}
