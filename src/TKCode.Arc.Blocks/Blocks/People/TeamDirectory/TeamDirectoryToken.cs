using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TeamDirectory"/>, measured from Arc Team directory (design reference only, ADR 0025), and the
/// accent-independent row hover tint it derives from the neutrals. Colors, radii of shared shapes, the type ramp and the
/// fonts come from ArcDesign tokens in the theme. Thicknesses are Avalonia order (left, top, right, bottom). Breakpoints
/// follow the block's own width: narrow at or below <see cref="NarrowWidth"/>, stacked at or below
/// <see cref="StackedWidth"/>, compact at or below <see cref="CompactWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class TeamDirectoryToken : AbstractControlDesignToken
{
    /// <summary>The framed card's corner radius (16).</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Breakpoints of the block's width: 640 (icon-only copy, tighter profile), 500 (one column), 400 (header stacks, counts hide).</summary>
    public double NarrowWidth { get; set; }

    public double StackedWidth { get; set; }

    public double CompactWidth { get; set; }

    /// <summary>The block's running text: 13 on an 18.2 line, letter spacing −0.13; small text 12 on 16.8.</summary>
    public double CopySize { get; set; }

    public double CopyLineHeight { get; set; }

    public double CopyLetterSpacing { get; set; }

    public double SmallLineHeight { get; set; }

    /// <summary>The header: padding (20, 16, 20, 12), from 500 down (16, 14, 16, 10); 10 between title and search when compact.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness StackedHeaderPadding { get; set; }

    public double HeaderGap { get; set; }

    public double CompactHeaderGap { get; set; }

    /// <summary>The heading: 16 on 17.6, letter spacing −0.48; the count 8 after it.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double TitleCountGap { get; set; }

    /// <summary>The search box: 240 × 32, padding (11, 0, 4, 0), 8 between its parts, a 16 glass, a 24 clear slot with a 14 cross; strokes 1.75.</summary>
    public double SearchWidth { get; set; }

    public double SearchHeight { get; set; }

    public Thickness SearchPadding { get; set; }

    public double SearchGap { get; set; }

    public double SearchGlyphSize { get; set; }

    public double ClearSlotSize { get; set; }

    public double ClearGlyphSize { get; set; }

    public double GlyphStroke { get; set; }

    /// <summary>The team filter: padding (16, 0, 16, 12), from 500 down (12, 0, 12, 12); buttons 28 high, 10 inline padding, 2 apart, 6 before the count.</summary>
    public Thickness FilterBarPadding { get; set; }

    public Thickness StackedFilterBarPadding { get; set; }

    public double FilterHeight { get; set; }

    public Thickness FilterPadding { get; set; }

    public double FilterGap { get; set; }

    public double FilterCountGap { get; set; }

    /// <summary>The list column: max(196, 36% of the block); the body is at least 432 high in two columns.</summary>
    public double ListMinWidth { get; set; }

    public double ListFraction { get; set; }

    public double BodyMinHeight { get; set; }

    /// <summary>The list: padding 8; rows 52 high, 2 apart, radius 10, padding (10, 8), 12 between avatar and text, 1 between the lines.</summary>
    public Thickness ListPadding { get; set; }

    public double RowHeight { get; set; }

    public double RowGap { get; set; }

    public CornerRadius RowRadius { get; set; }

    public Thickness RowPadding { get; set; }

    public double RowContentGap { get; set; }

    public double RowTextGap { get; set; }

    /// <summary>A hovered row: the muted surface at 60%.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The no-match message: padding (10, 16), 8 between the line and the button; the underline 3 below the text.</summary>
    public Thickness EmptyPadding { get; set; }

    public double EmptyGap { get; set; }

    public double UnderlineOffset { get; set; }

    /// <summary>The 1px underline placed 3 below the baseline of 13 / 18.2 text (0.4 above the line box's bottom).</summary>
    public Thickness UnderlineMargin { get; set; }

    /// <summary>The profile: padding 24, 20 from 640 down, (16, 20) from 500 down; 16 between its parts; 14 between avatar and name.</summary>
    public Thickness DetailPadding { get; set; }

    public Thickness NarrowDetailPadding { get; set; }

    public Thickness StackedDetailPadding { get; set; }

    public double DetailGap { get; set; }

    public double ProfileAvatarGap { get; set; }

    /// <summary>The profile name: 18 on 19.8, letter spacing −0.18; the bio at most 426.5 wide.</summary>
    public double NameLineHeight { get; set; }

    public double NameLetterSpacing { get; set; }

    public double BioMaxWidth { get; set; }

    /// <summary>Profile fields: 40 high, label column 96 (76 from 640 down, 84 from 400 down), 12 between columns, 6 inside a value.</summary>
    public double FieldHeight { get; set; }

    public double LabelWidth { get; set; }

    public double NarrowLabelWidth { get; set; }

    public double CompactLabelWidth { get; set; }

    public double FieldGap { get; set; }

    /// <summary>The gap after a field's label and before the copy button, as margins (12).</summary>
    public Thickness LabelMargin { get; set; }

    public Thickness CopyMargin { get; set; }

    public double InlineGap { get; set; }

    public double StatusDotSize { get; set; }

    /// <summary>The copy button: 28 high (28 wide icon-only), padding (10, 0), a 14 glyph (the check's stroke 2), a label at least 38 wide; pressed scale .97.</summary>
    public double CopyHeight { get; set; }

    public Thickness CopyPadding { get; set; }

    public double CopyGlyphSize { get; set; }

    public double CheckStroke { get; set; }

    public double CopyLabelMinWidth { get; set; }

    public double PressedScale { get; set; }

    /// <summary>The empty profile area: 4 between its two lines.</summary>
    public double NoneGap { get; set; }

    /// <summary>How far an incoming profile rises (4).</summary>
    public double ProfileRise { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardRadius = new CornerRadius(16);
        NarrowWidth = 640;
        StackedWidth = 500;
        CompactWidth = 400;
        CopySize = 13;
        CopyLineHeight = 18.2;
        CopyLetterSpacing = -0.13;
        SmallLineHeight = 16.8;
        HeaderPadding = new Thickness(20, 16, 20, 12);
        StackedHeaderPadding = new Thickness(16, 14, 16, 10);
        HeaderGap = 16;
        CompactHeaderGap = 10;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.48;
        TitleCountGap = 8;
        SearchWidth = 240;
        SearchHeight = 32;
        SearchPadding = new Thickness(11, 0, 4, 0);
        SearchGap = 8;
        SearchGlyphSize = 16;
        ClearSlotSize = 24;
        ClearGlyphSize = 14;
        GlyphStroke = 1.75;
        FilterBarPadding = new Thickness(16, 0, 16, 12);
        StackedFilterBarPadding = new Thickness(12, 0, 12, 12);
        FilterHeight = 28;
        FilterPadding = new Thickness(10, 0);
        FilterGap = 2;
        FilterCountGap = 6;
        ListMinWidth = 196;
        ListFraction = 0.36;
        BodyMinHeight = 432;
        ListPadding = new Thickness(8);
        RowHeight = 52;
        RowGap = 2;
        RowRadius = new CornerRadius(10);
        RowPadding = new Thickness(10, 8);
        RowContentGap = 12;
        RowTextGap = 1;
        RowHoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.6);
        EmptyPadding = new Thickness(10, 16);
        EmptyGap = 8;
        UnderlineOffset = 3;
        UnderlineMargin = new Thickness(0, 0, 0, 0.4);
        DetailPadding = new Thickness(24);
        NarrowDetailPadding = new Thickness(20);
        StackedDetailPadding = new Thickness(16, 20);
        DetailGap = 16;
        ProfileAvatarGap = 14;
        NameLineHeight = 19.8;
        NameLetterSpacing = -0.18;
        BioMaxWidth = 426.5;
        FieldHeight = 40;
        LabelWidth = 96;
        NarrowLabelWidth = 76;
        CompactLabelWidth = 84;
        FieldGap = 12;
        LabelMargin = new Thickness(0, 0, 12, 0);
        CopyMargin = new Thickness(12, 0, 0, 0);
        InlineGap = 6;
        StatusDotSize = 6;
        CopyHeight = 28;
        CopyPadding = new Thickness(10, 0);
        CopyGlyphSize = 14;
        CheckStroke = 2;
        CopyLabelMinWidth = 38;
        PressedScale = 0.97;
        NoneGap = 4;
        ProfileRise = 4;
    }
}
