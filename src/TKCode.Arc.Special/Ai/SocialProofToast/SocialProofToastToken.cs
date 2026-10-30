using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SocialProofToast"/>: the fixed toast height, how far covered toasts step back and shrink, how
/// far the stack fans out, the entrance travel, the corner placement and the parts of a toast. Colors, radii, shadows and
/// fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SocialProofToastToken : AbstractControlDesignToken
{
    /// <summary>Every toast's height (64px), so nothing resizes between states.</summary>
    public double CardHeight { get; set; }

    /// <summary>The space between toasts when the stack fans out.</summary>
    public double FanGap { get; set; }

    /// <summary>How far each covered toast steps back (10px).</summary>
    public double StackStep { get; set; }

    /// <summary>How much each covered toast shrinks (5%).</summary>
    public double StackScaleStep { get; set; }

    /// <summary>How far a new toast rises as it fades in (16px).</summary>
    public double EnterOffset { get; set; }

    /// <summary>The scale a dismissed toast shrinks to as it fades.</summary>
    public double ExitScale { get; set; }

    /// <summary>The stack's width in a corner (360px).</summary>
    public double CornerWidth { get; set; }

    /// <summary>The distance from the window's edges in a corner (24px).</summary>
    public double CornerInset { get; set; }

    /// <summary>The gutters of a corner stack on a narrow window, where it spans the width (16px).</summary>
    public double CompactInset { get; set; }

    /// <summary>The window width under which a corner stack spans the width (480px).</summary>
    public double CompactBreakpoint { get; set; }

    /// <summary>The toast's padding around the mark, the text and the dismiss button.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The mark slot (an Avatar's medium size, 36px).</summary>
    public double SlotSize { get; set; }

    /// <summary>A company logo's size inside its slot (18px).</summary>
    public double LogoSize { get; set; }

    /// <summary>The space between the mark, the text and the dismiss button.</summary>
    public Thickness TextMargin { get; set; }

    /// <summary>The first line: 14/20.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The second line: 12/16.</summary>
    public double MetaFontSize { get; set; }

    public double MetaLineHeight { get; set; }

    /// <summary>The verified badge: its glyph size and the space after it.</summary>
    public double BadgeGlyphSize { get; set; }

    public double BadgeGap { get; set; }

    /// <summary>The space between the meta and the verified badge that follows it.</summary>
    public double BadgeSpacing { get; set; }

    /// <summary>The dismiss button (24px) and its glyph (14px).</summary>
    public double DismissSize { get; set; }

    public double DismissGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardHeight = 64;
        FanGap = 8;
        StackStep = 10;
        StackScaleStep = 0.05;
        EnterOffset = 16;
        ExitScale = 0.96;
        CornerWidth = 360;
        CornerInset = 24;
        CompactInset = 16;
        CompactBreakpoint = 480;
        CardPadding = new Thickness(14, 0, 10, 0);
        SlotSize = 36;
        LogoSize = 18;
        TextMargin = new Thickness(12, 0, 8, 0);
        TitleLineHeight = 20;
        MetaFontSize = 12;
        MetaLineHeight = 16;
        BadgeGlyphSize = 12;
        BadgeGap = 4;
        BadgeSpacing = 8;
        DismissSize = 24;
        DismissGlyphSize = 14;
    }
}
