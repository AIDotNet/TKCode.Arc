using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the accent-independent derived colors of <see cref="ProfilePage"/>.</summary>
[ControlDesignToken]
internal sealed class ProfilePageToken : AbstractControlDesignToken
{
    /// <summary>Cover height on a page at least 1000 wide.</summary>
    public double CoverHeight { get; set; }

    /// <summary>Cover height between 640 and 999.</summary>
    public double CoverHeightMedium { get; set; }

    /// <summary>Cover height under 640.</summary>
    public double CoverHeightCompact { get; set; }

    /// <summary>Avatar diameter (half of it rides over the cover's bottom edge).</summary>
    public double AvatarDiameter { get; set; }

    /// <summary>Avatar diameter under 640.</summary>
    public double AvatarDiameterCompact { get; set; }

    /// <summary>The surface-colored ring around the avatar.</summary>
    public Thickness AvatarRing { get; set; }

    /// <summary>The avatar pulled up over the cover by half its size.</summary>
    public Thickness AvatarOverlap { get; set; }

    public Thickness AvatarOverlapCompact { get; set; }

    /// <summary>Font size of the avatar's initials.</summary>
    public double AvatarInitialsSize { get; set; }

    public double AvatarInitialsSizeCompact { get; set; }

    /// <summary>The widest the identity row and the body grow; they stay centered beyond it.</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>Side padding of the identity row.</summary>
    public Thickness IdentityInset { get; set; }

    public Thickness IdentityInsetCompact { get; set; }

    /// <summary>Gap between the avatar column and the buttons.</summary>
    public double IdentityColumnGap { get; set; }

    /// <summary>Gap between the avatar row and the name.</summary>
    public double IdentityRowGap { get; set; }

    /// <summary>The buttons sit this far above the avatar's bottom edge.</summary>
    public Thickness ActionsInset { get; set; }

    /// <summary>Padding of the body (about column and tabs).</summary>
    public Thickness BodyInset { get; set; }

    public Thickness BodyInsetCompact { get; set; }

    /// <summary>Width of the about column beside the tabs.</summary>
    public double AboutWidth { get; set; }

    /// <summary>Gap between the about column and the tabs.</summary>
    public double ColumnGap { get; set; }

    /// <summary>Gap between the about block and the tabs when they stack.</summary>
    public double StackedGap { get; set; }

    /// <summary>Line height of the 28px name.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLineHeightCompact { get; set; }

    /// <summary>Letter spacing of the name (−0.02em).</summary>
    public double TitleLetterSpacing { get; set; }

    public double TitleLetterSpacingCompact { get; set; }

    /// <summary>Line height of 14px body text.</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Line height of 12px small text.</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Height of the pill buttons (Share, Edit profile, Cancel, Save).</summary>
    public double PillHeight { get; set; }

    /// <summary>Padding of the pill buttons.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>Gap between a pill button's icon and label.</summary>
    public double PillGap { get; set; }

    /// <summary>The primary pill's hover fill: 86% foreground, 14% surface.</summary>
    public Color PrimaryHoverBackground { get; set; }

    /// <summary>The primary pill's label (white on light, a near-black on dark).</summary>
    public Color PrimaryForeground { get; set; }

    /// <summary>Height of a tab (label baseline 9px below its top).</summary>
    public double TabHeight { get; set; }

    /// <summary>Space above a tab's label.</summary>
    public Thickness TabPadding { get; set; }

    /// <summary>Gap between tabs.</summary>
    public double TabGap { get; set; }

    /// <summary>Thickness of the selected tab's underline.</summary>
    public double UnderlineThickness { get; set; }

    /// <summary>Space above the tab content.</summary>
    public Thickness PanelInset { get; set; }

    /// <summary>Gap between activity groups.</summary>
    public double ActivityGroupGap { get; set; }

    /// <summary>Padding of an activity row.</summary>
    public Thickness ActivityRowPadding { get; set; }

    /// <summary>Space under a group heading.</summary>
    public Thickness GroupHeadingInset { get; set; }

    /// <summary>Gap between project cards.</summary>
    public double CardGap { get; set; }

    /// <summary>Corner radius of a project card.</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Height of a project card's cover.</summary>
    public double CardCoverHeight { get; set; }

    /// <summary>Padding of a project card's text.</summary>
    public Thickness CardBodyInset { get; set; }

    /// <summary>Diameter of a collaborator face.</summary>
    public double FaceDiameter { get; set; }

    /// <summary>How far each face overlaps the previous one.</summary>
    public Thickness FaceOverlap { get; set; }

    /// <summary>Height of the star button.</summary>
    public double StarHeight { get; set; }

    /// <summary>Padding of the star button.</summary>
    public Thickness StarPadding { get; set; }

    /// <summary>Width of the edit sheet (never wider than the page).</summary>
    public double SheetWidth { get; set; }

    /// <summary>The scrim over the page while the sheet is open (the same in both themes).</summary>
    public Color ScrimFill { get; set; }

    /// <summary>Padding of the sheet's header.</summary>
    public Thickness SheetHeadInset { get; set; }

    /// <summary>Padding of the sheet's fields.</summary>
    public Thickness SheetBodyInset { get; set; }

    /// <summary>Padding of the sheet's footer.</summary>
    public Thickness SheetFootInset { get; set; }

    /// <summary>Height of a text field.</summary>
    public double FieldHeight { get; set; }

    /// <summary>Corner radius of a text field.</summary>
    public CornerRadius FieldRadius { get; set; }

    /// <summary>Height of the bio field.</summary>
    public double BioHeight { get; set; }

    /// <summary>Diameter of the sheet's close button.</summary>
    public double CloseDiameter { get; set; }

    /// <summary>Corner radius of the save-glow behind a changed field.</summary>
    public CornerRadius GlowRadius { get; set; }

    /// <summary>How far the save-glow reaches past the field.</summary>
    public double GlowSpread { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CoverHeight = 240;
        CoverHeightMedium = 200;
        CoverHeightCompact = 128;
        AvatarDiameter = 104;
        AvatarDiameterCompact = 84;
        AvatarRing = new Thickness(4);
        AvatarOverlap = new Thickness(0, -52, 0, 0);
        AvatarOverlapCompact = new Thickness(0, -42, 0, 0);
        AvatarInitialsSize = 34;
        AvatarInitialsSizeCompact = 28;
        ContentMaxWidth = 1120;
        IdentityInset = new Thickness(28, 0);
        IdentityInsetCompact = new Thickness(20, 0);
        IdentityColumnGap = 16;
        IdentityRowGap = 12;
        ActionsInset = new Thickness(0, 0, 0, 4);
        BodyInset = new Thickness(28, 24, 28, 32);
        BodyInsetCompact = new Thickness(20, 20, 20, 24);
        AboutWidth = 248;
        ColumnGap = 40;
        StackedGap = 24;
        TitleLineHeight = 30.8;
        TitleLineHeightCompact = 24.2;
        TitleLetterSpacing = -0.56;
        TitleLetterSpacingCompact = -0.44;
        BodyLineHeight = 19.6;
        SmallLineHeight = 16.8;
        PillHeight = 34;
        PillPadding = new Thickness(14, 0);
        PillGap = 6;
        PrimaryHoverBackground = Oklch.MixOklab(n.Foreground, n.Surface, 0.14);
        PrimaryForeground = isDarkMode ? Oklch.ToColor(0.19, 0, 0) : Colors.White;
        TabHeight = 38;
        TabPadding = new Thickness(0, 9, 0, 0);
        TabGap = 20;
        UnderlineThickness = 2;
        PanelInset = new Thickness(0, 16, 0, 0);
        ActivityGroupGap = 20;
        ActivityRowPadding = new Thickness(0, 10);
        GroupHeadingInset = new Thickness(0, 0, 0, 4);
        CardGap = 12;
        CardRadius = new CornerRadius(12);
        CardCoverHeight = 96;
        CardBodyInset = new Thickness(14, 12);
        FaceDiameter = 22;
        FaceOverlap = new Thickness(-7, 0, 0, 0);
        StarHeight = 28;
        StarPadding = new Thickness(8, 0, 10, 0);
        SheetWidth = 420;
        ScrimFill = Oklch.ToColor(0.15, 0, 0, 0.28);
        SheetHeadInset = new Thickness(20, 14, 12, 10);
        SheetBodyInset = new Thickness(20, 6, 20, 20);
        SheetFootInset = new Thickness(20, 12);
        FieldHeight = 36;
        FieldRadius = new CornerRadius(10);
        BioHeight = 116;
        CloseDiameter = 32;
        GlowRadius = new CornerRadius(6);
        GlowSpread = 3;
    }
}
