using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry, timing and the accent-independent cell colors of <see cref="RolesPermissions"/>, measured from the People
/// group's roles matrix (design reference only, ADR 0025). Widths have three tiers chosen by the block's content width:
/// wide (760 and up), narrow (below 760) and compact (below 540). Paddings are Avalonia thicknesses (left, top, right,
/// bottom).
/// </summary>
[ControlDesignToken]
internal sealed class RolesPermissionsToken : AbstractControlDesignToken
{
    /// <summary>The content widths below which the narrow (760) and compact (540) tiers apply.</summary>
    public double NarrowWidth { get; set; }

    public double CompactWidth { get; set; }

    /// <summary>The label column's minimum width per tier (248 / 200 / 148); it takes the space the roles leave.</summary>
    public double LabelMinWidth { get; set; }

    public double NarrowLabelMinWidth { get; set; }

    public double CompactLabelMinWidth { get; set; }

    /// <summary>A role column's width per tier (100 / 92 / 80).</summary>
    public double RoleWidth { get; set; }

    public double NarrowRoleWidth { get; set; }

    public double CompactRoleWidth { get; set; }

    /// <summary>The framed card's radius (16) and the flat form's padding (32, or 16 compact).</summary>
    public CornerRadius CardRadius { get; set; }

    public Thickness FlatPadding { get; set; }

    public Thickness CompactFlatPadding { get; set; }

    /// <summary>Header padding (20, 16) and compact (16, 14); 2 between the title and the subtitle; 24 / 12 between wrapped parts.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    public double SubtitleGap { get; set; }

    /// <summary>Running text: 13 on an 18.2 line, letter spacing −0.13; the title 16 on a 17.6 line, −0.16.</summary>
    public double CopySize { get; set; }

    public double CopyLineHeight { get; set; }

    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    /// <summary>The header row (56), a group row (44 + 1 border) and the minimum permission row (39 + 1 border).</summary>
    public double HeaderRowHeight { get; set; }

    public double GroupRowHeight { get; set; }

    public double PermissionRowHeight { get; set; }

    /// <summary>Label insets: the caption (20 left, 12 bottom; 16 compact), group rows (20), permission rows (44; 40 compact) and 12 on the right.</summary>
    public double LabelInset { get; set; }

    public double CompactLabelInset { get; set; }

    public double PermissionInset { get; set; }

    public double CompactPermissionInset { get; set; }

    public double LabelEndInset { get; set; }

    /// <summary>A permission name: 13 on a 16.9 line (12 compact), 8 above and below when it wraps.</summary>
    public double PermissionLineHeight { get; set; }

    public double PermissionVerticalInset { get; set; }

    /// <summary>A role header button: 6 narrower than its column, 44 high, radius 10, 4 side padding, 1 between its lines.</summary>
    public double RoleButtonInset { get; set; }

    public double RoleButtonHeight { get; set; }

    public double RoleButtonRadius { get; set; }

    /// <summary>The group button: 28 high, radius 8, 6 / 8 side padding, 8 between chevron, name and count.</summary>
    public double GroupButtonHeight { get; set; }

    public double GroupButtonRadius { get; set; }

    /// <summary>A cell's hit area (40 × 32, radius 8), its box (16, radius 5) and the unsaved dot (5, 5 right of the box).</summary>
    public double CellWidth { get; set; }

    public double CellHeight { get; set; }

    public double CellRadius { get; set; }

    public double BoxSize { get; set; }

    public double BoxRadius { get; set; }

    public double DotSize { get; set; }

    /// <summary>The column band: 4 inside the matrix top and bottom, radius 10.</summary>
    public double BandInset { get; set; }

    public double BandRadius { get; set; }

    /// <summary>The review bar: 600 wide at most, 54 high, radius 14; 16 from the sides and bottom (12 compact); its slot opens to 76.</summary>
    public double BarMaxWidth { get; set; }

    public double BarHeight { get; set; }

    public CornerRadius BarRadius { get; set; }

    public Thickness BarMargin { get; set; }

    public Thickness CompactBarMargin { get; set; }

    public Thickness BarPadding { get; set; }

    public double BarSlotHeight { get; set; }

    /// <summary>Footer padding (20, 12; 16 compact), 20 between legends and 6 between wrapped lines.</summary>
    public Thickness FooterPadding { get; set; }

    public Thickness CompactFooterPadding { get; set; }

    /// <summary>The group ripple step (60ms per row inside the group) and the table-wide revert step (30ms per row).</summary>
    public TimeSpan GroupRippleStep { get; set; }

    public TimeSpan TableRippleStep { get; set; }

    /// <summary>How long the bar reads "saved" before it closes (1600).</summary>
    public TimeSpan SavedHold { get; set; }

    /// <summary>Box colors: off border, hover fill and border, on fill (and its check), mixed fill, locked fill (and check).</summary>
    public Color BoxOffBorder { get; set; }

    public Color BoxHoverFill { get; set; }

    public Color BoxHoverBorder { get; set; }

    public Color BoxOnFill { get; set; }

    public Color BoxOnGlyph { get; set; }

    public Color BoxMixedFill { get; set; }

    public Color BoxLockedFill { get; set; }

    /// <summary>The column band and the hovered row tint: the foreground at 1.2% / 2% (1.8% / 3.5% dark).</summary>
    public Color BandFill { get; set; }

    public Color RowHoverFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        NarrowWidth = 760;
        CompactWidth = 540;
        LabelMinWidth = 248;
        NarrowLabelMinWidth = 200;
        CompactLabelMinWidth = 148;
        RoleWidth = 100;
        NarrowRoleWidth = 92;
        CompactRoleWidth = 80;
        CardRadius = new CornerRadius(16);
        FlatPadding = new Thickness(32);
        CompactFlatPadding = new Thickness(16);
        HeaderPadding = new Thickness(20, 16);
        CompactHeaderPadding = new Thickness(16, 14);
        SubtitleGap = 2;
        CopySize = 13;
        CopyLineHeight = 18.2;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.16;
        HeaderRowHeight = 56;
        GroupRowHeight = 45;
        PermissionRowHeight = 40;
        LabelInset = 20;
        CompactLabelInset = 16;
        PermissionInset = 44;
        CompactPermissionInset = 40;
        LabelEndInset = 12;
        PermissionLineHeight = 16.9;
        PermissionVerticalInset = 8;
        RoleButtonInset = 6;
        RoleButtonHeight = 44;
        RoleButtonRadius = 10;
        GroupButtonHeight = 28;
        GroupButtonRadius = 8;
        CellWidth = 40;
        CellHeight = 32;
        CellRadius = 8;
        BoxSize = 16;
        BoxRadius = 5;
        DotSize = 5;
        BandInset = 4;
        BandRadius = 10;
        BarMaxWidth = 600;
        BarHeight = 54;
        BarRadius = new CornerRadius(14);
        BarMargin = new Thickness(16, 0, 16, 16);
        CompactBarMargin = new Thickness(12, 0, 12, 16);
        BarPadding = new Thickness(16, 8, 8, 8);
        BarSlotHeight = 76;
        FooterPadding = new Thickness(20, 12);
        CompactFooterPadding = new Thickness(16, 12);
        GroupRippleStep = TimeSpan.FromMilliseconds(60);
        TableRippleStep = TimeSpan.FromMilliseconds(30);
        SavedHold = TimeSpan.FromMilliseconds(1600);
        if (isDarkMode)
        {
            BoxOffBorder = Oklch.ToColor(0.523, 0, 0);
            BoxHoverFill = Oklch.ToColor(0.25, 0, 0);
            BoxHoverBorder = n.TextMuted;
            BoxOnFill = Oklch.ToColor(0.93, 0, 0);
            BoxOnGlyph = n.Background;
            BoxMixedFill = Oklch.ToColor(0.301, 0, 0);
            BoxLockedFill = Oklch.ToColor(0.501, 0, 0);
            BandFill = Oklch.WithAlpha(n.Foreground, 0.018);
            RowHoverFill = Oklch.WithAlpha(n.Foreground, 0.035);
        }
        else
        {
            BoxOffBorder = Oklch.ToColor(0.673, 0, 0);
            BoxHoverFill = Oklch.ToColor(0.9745, 0, 0);
            BoxHoverBorder = n.TextMuted;
            BoxOnFill = Oklch.ToColor(0.23, 0, 0);
            BoxOnGlyph = Colors.White;
            BoxMixedFill = Oklch.ToColor(0.908, 0, 0);
            BoxLockedFill = Oklch.ToColor(0.692, 0, 0);
            BandFill = Oklch.WithAlpha(n.Foreground, 0.012);
            RowHoverFill = Oklch.WithAlpha(n.Foreground, 0.02);
        }
    }
}
