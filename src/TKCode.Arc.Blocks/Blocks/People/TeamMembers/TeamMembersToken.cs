using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TeamMembers"/>, measured from the People group's members page (design reference only, ADR 0025),
/// and the two accent-independent tints it derives from the neutrals. Colors that follow the accent, the type ramp and the
/// fonts come from ArcDesign tokens in the theme. Thicknesses are (left, top, right, bottom); "compact" values apply at or
/// below <see cref="StackWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class TeamMembersToken : AbstractControlDesignToken
{
    /// <summary>The framed card's corner radius (16).</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>At or below this width the last-active column moves under the email (640).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>At or below this width the header and the invite row stack and paddings tighten (600).</summary>
    public double StackWidth { get; set; }

    /// <summary>At or below this width a pending invite's buttons move under the address (480).</summary>
    public double ActionsBelowWidth { get; set; }

    /// <summary>At or below this width a member row splits into two tiers (420).</summary>
    public double TwoTierWidth { get; set; }

    /// <summary>The block's running text: 13 on an 18.2 line, letter spacing −0.13.</summary>
    public double CopySize { get; set; }

    public double CopyLineHeight { get; set; }

    public double CopyLetterSpacing { get; set; }

    /// <summary>The title: 16 on a 17.6 line, −0.16; 2 above the description.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double DescriptionGap { get; set; }

    /// <summary>Small text (emails, last active, details, footer): 12 on a 16.8 line.</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Header padding (20, 16) and compact (16, 14); 24 between the titles and the seat meter, 12 when stacked.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    public double HeaderGap { get; set; }

    public double StackedHeaderGap { get; set; }

    /// <summary>The seat meter: 180 wide, 6 between the text and the 4px bar.</summary>
    public double SeatMeterWidth { get; set; }

    public double SeatMeterGap { get; set; }

    public double SeatBarHeight { get; set; }

    /// <summary>Invite form padding (20, 16) and compact (16, 14); 6 under the label, 8 between the field, the picker and the button.</summary>
    public Thickness FormPadding { get; set; }

    public Thickness CompactFormPadding { get; set; }

    public double LabelGap { get; set; }

    public double InviteRowGap { get; set; }

    /// <summary>The invite field: at least 36 tall, radius 18, inner padding (4, 3), 4 between chips.</summary>
    public double FieldMinHeight { get; set; }

    public CornerRadius FieldRadius { get; set; }

    public Thickness FieldPadding { get; set; }

    public double ChipGap { get; set; }

    /// <summary>The text input inside the field: 26 tall, 6 on each side, at least 72 wide before it wraps.</summary>
    public double InputHeight { get; set; }

    public Thickness InputInset { get; set; }

    public double InputMinWidth { get; set; }

    /// <summary>A chip: 26 tall, padding (9, 0, 3, 0), 2 before its 20px remove button with a 12px glyph.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipInnerGap { get; set; }

    public double ChipRemoveSize { get; set; }

    public double ChipGlyphSize { get; set; }

    /// <summary>The field-style role picker: padding (14, 0, 12, 0), 8 before a 14px chevron.</summary>
    public Thickness RoleFieldPadding { get; set; }

    public double RoleFieldGap { get; set; }

    public double ChevronSize { get; set; }

    /// <summary>The row's role trigger: 32 tall, padding (10, 0, 6, 0), 4 before the chevron, radius 8.</summary>
    public double RoleGhostHeight { get; set; }

    public Thickness RoleGhostPadding { get; set; }

    public double RoleGhostGap { get; set; }

    public CornerRadius RoleGhostRadius { get; set; }

    /// <summary>The owner's role text sits 25 from the column's right edge (in line with the other rows' role names).</summary>
    public Thickness OwnerTextMargin { get; set; }

    /// <summary>The message under the field: 8 above it, a 14px icon 6 before the 12 / 18 text.</summary>
    public double MessageTopGap { get; set; }

    public double MessageIconSize { get; set; }

    public double MessageIconGap { get; set; }

    public double MessageLineHeight { get; set; }

    /// <summary>Section heads: members (20, 12, 20, 4), pending (20, 16, 20, 4), compact with 16 sides; 8 before the count.</summary>
    public Thickness MembersHeadPadding { get; set; }

    public Thickness PendingHeadPadding { get; set; }

    public Thickness CompactMembersHeadPadding { get; set; }

    public Thickness CompactPendingHeadPadding { get; set; }

    public double HeadCountGap { get; set; }

    public double HeadingLineHeight { get; set; }

    public double HeadingLetterSpacing { get; set; }

    /// <summary>The search: 240 × 32 beside the heading; full width on its own line (12 below the heading) when stacked.</summary>
    public double SearchWidth { get; set; }

    public double SearchHeight { get; set; }

    public double HeadRowGap { get; set; }

    /// <summary>8 under the last list.</summary>
    public double ListsBottom { get; set; }

    /// <summary>Row padding (20, 8) and compact (16, 8); a 36px avatar 12 before the text; 12 between columns.</summary>
    public Thickness RowPadding { get; set; }

    public Thickness CompactRowPadding { get; set; }

    public double AvatarSize { get; set; }

    public double AvatarGap { get; set; }

    public double ColumnGap { get; set; }

    /// <summary>Columns of a wide member row: last active 128, role 112, remove 32; narrow rows give the role 104.</summary>
    public double ActiveColumnWidth { get; set; }

    public double RoleColumnWidth { get; set; }

    public double NarrowRoleColumnWidth { get; set; }

    public double RemoveSize { get; set; }

    /// <summary>The remove glyph (16, stroke 1.75).</summary>
    public double RowGlyphSize { get; set; }

    public double RowGlyphStroke { get; set; }

    /// <summary>1 between the lines of a row's text.</summary>
    public double IdentityGap { get; set; }

    /// <summary>8 between the "You" mark and the name.</summary>
    public double YouGap { get; set; }

    /// <summary>4 between a pending row's buttons; 8 above them when they sit under the address.</summary>
    public double ActionsGap { get; set; }

    public double ActionsBelowGap { get; set; }

    /// <summary>The members empty row (52 tall) and the pending empty row's padding (20, 8).</summary>
    public double EmptyRowHeight { get; set; }

    public Thickness EmptyPendingPadding { get; set; }

    public Thickness CompactEmptyPendingPadding { get; set; }

    /// <summary>Footer padding (20, 10) and compact (16, 10).</summary>
    public Thickness FooterPadding { get; set; }

    public Thickness CompactFooterPadding { get; set; }

    /// <summary>The role menu: 6 below its trigger, padding 6, radius 20; items 48 tall, padding (10, 6, 36, 6), radius 10; a 16px check 12 from the right.</summary>
    public double MenuOffset { get; set; }

    public Thickness MenuPadding { get; set; }

    public CornerRadius MenuRadius { get; set; }

    public double MenuItemMinHeight { get; set; }

    public Thickness MenuItemPadding { get; set; }

    public CornerRadius MenuItemRadius { get; set; }

    public double MenuCheckSize { get; set; }

    public Thickness MenuCheckMargin { get; set; }

    /// <summary>A row's hover tint: the muted surface mixed 55% into the surface.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>A row asking to confirm a removal: the danger color mixed 6% into the surface.</summary>
    public Color ConfirmBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardRadius = new CornerRadius(16);
        NarrowWidth = 640;
        StackWidth = 600;
        ActionsBelowWidth = 480;
        TwoTierWidth = 420;
        CopySize = 13;
        CopyLineHeight = 18.2;
        CopyLetterSpacing = -0.13;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.16;
        DescriptionGap = 2;
        SmallLineHeight = 16.8;
        HeaderPadding = new Thickness(20, 16);
        CompactHeaderPadding = new Thickness(16, 14);
        HeaderGap = 24;
        StackedHeaderGap = 12;
        SeatMeterWidth = 180;
        SeatMeterGap = 6;
        SeatBarHeight = 4;
        FormPadding = new Thickness(20, 16);
        CompactFormPadding = new Thickness(16, 14);
        LabelGap = 6;
        InviteRowGap = 8;
        FieldMinHeight = 36;
        FieldRadius = new CornerRadius(18);
        FieldPadding = new Thickness(4, 3);
        ChipGap = 4;
        InputHeight = 26;
        InputInset = new Thickness(6, 0);
        InputMinWidth = 72;
        ChipHeight = 26;
        ChipPadding = new Thickness(9, 0, 3, 0);
        ChipInnerGap = 2;
        ChipRemoveSize = 20;
        ChipGlyphSize = 12;
        RoleFieldPadding = new Thickness(14, 0, 12, 0);
        RoleFieldGap = 8;
        ChevronSize = 14;
        RoleGhostHeight = 32;
        RoleGhostPadding = new Thickness(10, 0, 6, 0);
        RoleGhostGap = 4;
        RoleGhostRadius = new CornerRadius(8);
        OwnerTextMargin = new Thickness(0, 0, 25, 0);
        MessageTopGap = 8;
        MessageIconSize = 14;
        MessageIconGap = 6;
        MessageLineHeight = 18;
        MembersHeadPadding = new Thickness(20, 12, 20, 4);
        PendingHeadPadding = new Thickness(20, 16, 20, 4);
        CompactMembersHeadPadding = new Thickness(16, 12, 16, 4);
        CompactPendingHeadPadding = new Thickness(16, 16, 16, 4);
        HeadCountGap = 8;
        HeadingLineHeight = 14.3;
        HeadingLetterSpacing = -0.39;
        SearchWidth = 240;
        SearchHeight = 32;
        HeadRowGap = 12;
        ListsBottom = 8;
        RowPadding = new Thickness(20, 8);
        CompactRowPadding = new Thickness(16, 8);
        AvatarSize = 36;
        AvatarGap = 12;
        ColumnGap = 12;
        ActiveColumnWidth = 128;
        RoleColumnWidth = 112;
        NarrowRoleColumnWidth = 104;
        RemoveSize = 32;
        RowGlyphSize = 16;
        RowGlyphStroke = 1.75;
        // Arc: 18.2 + 1 + 16.8 = 36. Layout rounding lifts the 18.2 line to 19, which already holds the 1px gap.
        IdentityGap = 0;
        YouGap = 8;
        ActionsGap = 4;
        ActionsBelowGap = 8;
        EmptyRowHeight = 52;
        EmptyPendingPadding = new Thickness(20, 8);
        CompactEmptyPendingPadding = new Thickness(16, 8);
        FooterPadding = new Thickness(20, 10);
        CompactFooterPadding = new Thickness(16, 10);
        MenuOffset = 6;
        MenuPadding = new Thickness(6);
        MenuRadius = new CornerRadius(20);
        MenuItemMinHeight = 48;
        MenuItemPadding = new Thickness(10, 6, 36, 6);
        MenuItemRadius = new CornerRadius(10);
        MenuCheckSize = 16;
        MenuCheckMargin = new Thickness(0, 0, 12, 0);
        RowHoverBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.55);
        ConfirmBackground = Oklch.MixOklab(n.Surface, n.Danger, 0.06);
    }
}
