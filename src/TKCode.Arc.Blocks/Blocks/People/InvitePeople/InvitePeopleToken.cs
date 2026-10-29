using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="InvitePeople"/>, measured from the People group's invite form (design reference only, ADR 0025),
/// and the accent-independent tints it derives from the neutrals. Colors that follow the accent or the danger color, the
/// type ramp and the fonts come from ArcDesign tokens. Paddings are Avalonia thicknesses (left, top, right, bottom).
/// </summary>
[ControlDesignToken]
internal sealed class InvitePeopleToken : AbstractControlDesignToken
{
    /// <summary>The framed card is this wide at most (600).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The framed card's corner radius (16).</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>The body's padding (20 on the sides and the top) and its compact form (16).</summary>
    public Thickness BodyPadding { get; set; }

    public Thickness CompactBodyPadding { get; set; }

    /// <summary>The pending section's padding (20, 12, 20, 8) and its compact form.</summary>
    public Thickness PendingPadding { get; set; }

    public Thickness CompactPendingPadding { get; set; }

    /// <summary>At or below this width of the control the paddings tighten from 20 to 16 (480).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Below this content width the pending rows stack their status under the name and suggestions drop the title (440).</summary>
    public double NarrowContentWidth { get; set; }

    /// <summary>The block's running text: 13 on an 18.2 line, letter spacing −0.13.</summary>
    public double CopySize { get; set; }

    public double CopyLineHeight { get; set; }

    public double CopyLetterSpacing { get; set; }

    /// <summary>Small text: 12 on a 16.8 line.</summary>
    public double SmallSize { get; set; }

    public double SmallLineHeight { get; set; }

    /// <summary>The title: 16 on a 17.6 line, letter spacing −0.48; 2 between it and the description.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    /// <summary>The pending heading: 13 on a 14.3 line, letter spacing −0.39.</summary>
    public double HeadingLineHeight { get; set; }

    public double HeadingLetterSpacing { get; set; }

    /// <summary>Space between the sections (16) and between the field and the hint (6).</summary>
    public double SectionGap { get; set; }

    public double HintGap { get; set; }

    /// <summary>The field's corner radius (18) and its inner padding (4).</summary>
    public CornerRadius FieldRadius { get; set; }

    public Thickness FieldPadding { get; set; }

    /// <summary>The error panel: padding (14, 12), radius 18, 8 below the hint.</summary>
    public Thickness ErrorsPadding { get; set; }

    public CornerRadius ErrorsRadius { get; set; }

    public Thickness ErrorsMargin { get; set; }

    /// <summary>The seat meter's height (6).</summary>
    public double MeterHeight { get; set; }

    /// <summary>The over-limit line's height (36).</summary>
    public double OverLineHeight { get; set; }

    /// <summary>The send button: 36 high, padding (14, 0, 16, 0).</summary>
    public double SendHeight { get; set; }

    public Thickness SendPadding { get; set; }

    /// <summary>The hover and highlight tint: the foreground at 6% (both themes).</summary>
    public Color HoverTint { get; set; }

    /// <summary>The marked chip's fill: the foreground at 7% over the muted surface.</summary>
    public Color ArmedFill { get; set; }

    /// <summary>An invalid chip: the danger color at about 8% over the surface, a 32% danger outline, an 80% danger ×.</summary>
    public Color ErrorChipFill { get; set; }

    public Color ErrorChipStroke { get; set; }

    public Color ErrorChipGlyph { get; set; }

    /// <summary>The error panel's fill: the danger color at about 6% over the surface.</summary>
    public Color ErrorPanelFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardMaxWidth = 600;
        CardRadius = new CornerRadius(16);
        BodyPadding = new Thickness(20, 20, 20, 0);
        CompactBodyPadding = new Thickness(16, 16, 16, 0);
        PendingPadding = new Thickness(20, 12, 20, 8);
        CompactPendingPadding = new Thickness(16, 12, 16, 8);
        CompactWidth = 480;
        NarrowContentWidth = 440;
        CopySize = 13;
        CopyLineHeight = 18.2;
        CopyLetterSpacing = -0.13;
        SmallSize = 12;
        SmallLineHeight = 16.8;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.48;
        HeadingLineHeight = 14.3;
        HeadingLetterSpacing = -0.39;
        SectionGap = 16;
        HintGap = 6;
        FieldRadius = new CornerRadius(18);
        FieldPadding = new Thickness(4);
        ErrorsPadding = new Thickness(14, 12);
        ErrorsRadius = new CornerRadius(18);
        ErrorsMargin = new Thickness(0, 8, 0, 0);
        MeterHeight = 6;
        OverLineHeight = 36;
        SendHeight = 36;
        SendPadding = new Thickness(14, 0, 16, 0);
        HoverTint = Oklch.WithAlpha(n.Foreground, 0.06);
        ArmedFill = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, 0.07);
        ErrorChipFill = Oklch.MixOklab(n.Surface, n.Danger, 0.08);
        ErrorChipStroke = Oklch.WithAlpha(n.Danger, 0.32);
        ErrorChipGlyph = Oklch.WithAlpha(n.Danger, 0.8);
        ErrorPanelFill = Oklch.MixOklab(n.Surface, n.Danger, 0.06);
    }
}
