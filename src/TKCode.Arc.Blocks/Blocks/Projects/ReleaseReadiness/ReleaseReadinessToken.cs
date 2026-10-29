using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ReleaseReadiness"/>, measured from the Projects group's release gate (design reference only,
/// ADR 0017), and the one accent-independent tint it derives from the neutrals. Colors that follow the accent, the type
/// ramp's sizes and the fonts come from ArcDesign tokens in the theme. Paddings are Avalonia thicknesses (left, top,
/// right, bottom); the compact ones apply at or below <see cref="CompactWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class ReleaseReadinessToken : AbstractControlDesignToken
{
    /// <summary>The framed card is this wide at most (560).</summary>
    public double CardWidth { get; set; }

    /// <summary>The framed card's corner radius (16; tighter than the panel radius).</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>At or below this width of the control the side paddings tighten from 20 to 16 (388).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The block's running text: 13 on an 18.2 line, letter spacing −0.13.</summary>
    public double CopySize { get; set; }

    public double CopyLineHeight { get; set; }

    public double CopyLetterSpacing { get; set; }

    /// <summary>The title: 16 on a 17.6 line, letter spacing −0.16; 2 between it and the subtitle.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double SubtitleGap { get; set; }

    /// <summary>Header padding (20, 16, 20, 12) and its compact form (16, 16, 16, 12); 16 between the titles and the owners.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    public Thickness OwnersMargin { get; set; }

    /// <summary>The owners' avatars: 28 wide, overlapping by 6, cut apart by a 2px surface ring.</summary>
    public double AvatarDiameter { get; set; }

    public double AvatarOverlap { get; set; }

    public double AvatarRing { get; set; }

    /// <summary>Progress padding (20, 0, 20, 16) and its compact form; 8 between the counts and the bar, 12 between the two counts.</summary>
    public Thickness ProgressPadding { get; set; }

    public Thickness CompactProgressPadding { get; set; }

    public double ProgressGap { get; set; }

    public Thickness CountMargin { get; set; }

    /// <summary>The bar's height (4).</summary>
    public double BarHeight { get; set; }

    /// <summary>Row padding (20, 12) and its compact form (16, 12).</summary>
    public Thickness RowPadding { get; set; }

    public Thickness CompactRowPadding { get; set; }

    /// <summary>
    /// The checkbox's 44px hit area spills out of the row's padding: 13 to the left and 12 above, so its 18px box lines up
    /// with the padding edge and the hit area reaches the row's top.
    /// </summary>
    public Thickness CheckboxMargin { get; set; }

    /// <summary>Where the row's text starts inside the checkbox: 55 from the row's edge like a 31 + 4 column grid, 13 from its top.</summary>
    public Thickness RowCopyMargin { get; set; }

    /// <summary>Between a row's title and its description (1); the description is 12 on a 16.8 line.</summary>
    public double RowCopyGap { get; set; }

    public double DescriptionLineHeight { get; set; }

    /// <summary>The row's hover tint: the muted surface mixed 55% into the surface.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>Footer padding (20, 12) and its compact form; 16 between the switch and the actions, 12 between wrapped lines, 8 between the buttons.</summary>
    public Thickness FooterPadding { get; set; }

    public Thickness CompactFooterPadding { get; set; }

    public double FooterColumnGap { get; set; }

    public double FooterRowGap { get; set; }

    public double ActionsGap { get; set; }

    /// <summary>The button's glyph (16, stroke 2) and the gap between it and the label (8).</summary>
    public double GlyphSize { get; set; }

    public double GlyphStroke { get; set; }

    public double GlyphGap { get; set; }

    /// <summary>The release button while its action runs, as Arc's disabled look (0.52).</summary>
    public double BusyOpacity { get; set; }

    /// <summary>The status line under the footer: padding (20, 0, 20, 14) and its compact form; 12 on a 16.8 line.</summary>
    public Thickness MessagePadding { get; set; }

    public Thickness CompactMessagePadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardWidth = 560;
        CardRadius = new CornerRadius(16);
        CompactWidth = 388;
        CopySize = 13;
        CopyLineHeight = 18.2;
        CopyLetterSpacing = -0.13;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.16;
        SubtitleGap = 2;
        HeaderPadding = new Thickness(20, 16, 20, 12);
        CompactHeaderPadding = new Thickness(16, 16, 16, 12);
        OwnersMargin = new Thickness(16, 0, 0, 0);
        AvatarDiameter = 28;
        AvatarOverlap = 6;
        AvatarRing = 2;
        ProgressPadding = new Thickness(20, 0, 20, 16);
        CompactProgressPadding = new Thickness(16, 0, 16, 16);
        ProgressGap = 8;
        CountMargin = new Thickness(12, 0, 0, 0);
        BarHeight = 4;
        RowPadding = new Thickness(20, 12);
        CompactRowPadding = new Thickness(16, 12);
        CheckboxMargin = new Thickness(-13, -12, 0, 0);
        // The checkbox puts its text 45 from its hit area's left (44 + 1) and 12.2 from its top; Arc's label starts at
        // 48 (13 + 31 + 4) and 13 (12 + 1).
        RowCopyMargin = new Thickness(3, 0.8, 0, 0);
        RowCopyGap = 1;
        DescriptionLineHeight = 16.8;
        RowHoverBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.55);
        FooterPadding = new Thickness(20, 12);
        CompactFooterPadding = new Thickness(16, 12);
        FooterColumnGap = 16;
        FooterRowGap = 12;
        ActionsGap = 8;
        GlyphSize = 16;
        GlyphStroke = 2;
        GlyphGap = 8;
        BusyOpacity = 0.52;
        MessagePadding = new Thickness(20, 0, 20, 14);
        CompactMessagePadding = new Thickness(16, 0, 16, 14);
    }
}
