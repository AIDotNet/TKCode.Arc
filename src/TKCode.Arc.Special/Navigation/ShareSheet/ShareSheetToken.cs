using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ShareSheet"/>: the Share button, the panel and its rows, the sent confirmation and the bottom
/// sheet on narrow screens, plus the few neutral tints derived from the palette. Accent-dependent colors, radii, shadows
/// and fonts come from the ArcDesign tokens in the theme. Values measured from Arc's Share sheet (design reference).
/// </summary>
[ControlDesignToken]
internal sealed class ShareSheetToken : AbstractControlDesignToken
{
    /// <summary>Height of the Share button.</summary>
    public double TriggerHeight { get; set; }

    /// <summary>Padding of the Share button (start 14, end 16).</summary>
    public Thickness TriggerPadding { get; set; }

    /// <summary>Gap between a glyph and its label (Share button, channel buttons).</summary>
    public double GlyphGap { get; set; }

    /// <summary>Size of the 16 px glyphs (Share, link, access, channels, close).</summary>
    public double GlyphSize { get; set; }

    /// <summary>Fill of the Share button and the Send button under the pointer.</summary>
    public Color ButtonHoverColor { get; set; }

    /// <summary>Width of the panel (it is never wider than the viewport less 32 px).</summary>
    public double PanelWidth { get; set; }

    /// <summary>Width of the sent confirmation.</summary>
    public double SentWidth { get; set; }

    /// <summary>Padding around the panel's content when it floats.</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>Padding around the panel's content in the bottom sheet (under the grabber).</summary>
    public Thickness SheetPanelPadding { get; set; }

    /// <summary>Padding of the sent confirmation.</summary>
    public Thickness SentPadding { get; set; }

    /// <summary>Corner radius of the floating panel.</summary>
    public double PanelRadius { get; set; }

    /// <summary>Corner radius of the floating confirmation.</summary>
    public double SentRadius { get; set; }

    /// <summary>Gap between the panel's sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>Height of the title row.</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Indent of the title, the small headings and the hint.</summary>
    public Thickness TextIndent { get; set; }

    /// <summary>Size of the round close button.</summary>
    public double CloseSize { get; set; }

    /// <summary>Size of the round close button in the bottom sheet (a larger touch target).</summary>
    public double SheetCloseSize { get; set; }

    /// <summary>Corner radius of the card that holds the link and the access level.</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Fill of the link card (the muted surface, slightly see-through).</summary>
    public Color CardColor { get; set; }

    /// <summary>Height of the link row.</summary>
    public double LinkRowHeight { get; set; }

    /// <summary>Padding of the link row (start 14, end 6).</summary>
    public Thickness LinkRowPadding { get; set; }

    /// <summary>Gap between a row's glyph, text and trailing part.</summary>
    public double RowGap { get; set; }

    /// <summary>Height of the copy button.</summary>
    public double CopyHeight { get; set; }

    /// <summary>Padding of the copy button.</summary>
    public Thickness CopyPadding { get; set; }

    /// <summary>Gap between the copy glyph and its label.</summary>
    public double CopyGap { get; set; }

    /// <summary>Size of the copy glyph.</summary>
    public double CopyGlyphSize { get; set; }

    /// <summary>Minimum height of the access row.</summary>
    public double AccessRowHeight { get; set; }

    /// <summary>Padding of the access row.</summary>
    public Thickness AccessRowPadding { get; set; }

    /// <summary>Padding of the open access list.</summary>
    public Thickness AccessListPadding { get; set; }

    /// <summary>Gap between access options.</summary>
    public double OptionGap { get; set; }

    /// <summary>Padding of an access option.</summary>
    public Thickness OptionPadding { get; set; }

    /// <summary>Corner radius of an access option.</summary>
    public CornerRadius OptionRadius { get; set; }

    /// <summary>Fill of the chosen access option.</summary>
    public Color OptionSelectedColor { get; set; }

    /// <summary>Fill of an access option, a person or a channel under the pointer, and of a channel while it runs.</summary>
    public Color HoverColor { get; set; }

    /// <summary>Gap between two lines of text (a label and its description).</summary>
    public double LineGap { get; set; }

    /// <summary>Height of a channel button.</summary>
    public double ChannelHeight { get; set; }

    /// <summary>Padding of a channel button.</summary>
    public Thickness ChannelPadding { get; set; }

    /// <summary>Gap between channel buttons.</summary>
    public double ChannelGap { get; set; }

    /// <summary>Margin of the hairline under the channels.</summary>
    public Thickness DividerMargin { get; set; }

    /// <summary>Gap between the "send to" heading and its chips row.</summary>
    public double SendToGap { get; set; }

    /// <summary>Height of the chips row.</summary>
    public double ChipsRowHeight { get; set; }

    /// <summary>Padding at the ends of the chips row.</summary>
    public Thickness ChipsPadding { get; set; }

    /// <summary>Height of a chip.</summary>
    public double ChipHeight { get; set; }

    /// <summary>Padding inside a chip.</summary>
    public Thickness ChipPadding { get; set; }

    /// <summary>Gap between chips and between a chip's parts.</summary>
    public double ChipGap { get; set; }

    /// <summary>Size of a chip's avatar.</summary>
    public double ChipAvatarSize { get; set; }

    /// <summary>Fill of a chip.</summary>
    public Color ChipColor { get; set; }

    /// <summary>Size of a chip's round Remove button.</summary>
    public double RemoveSize { get; set; }

    /// <summary>Size of the glyph in a chip's Remove button.</summary>
    public double RemoveGlyphSize { get; set; }

    /// <summary>Gap between recent people.</summary>
    public double PeopleGap { get; set; }

    /// <summary>Padding of a person's button.</summary>
    public Thickness PersonPadding { get; set; }

    /// <summary>Gap between a person's avatar and their name.</summary>
    public double PersonGap { get; set; }

    /// <summary>Corner radius of a person's button.</summary>
    public CornerRadius PersonRadius { get; set; }

    /// <summary>Size of a recent person's avatar.</summary>
    public double PersonAvatarSize { get; set; }

    /// <summary>Where the ring around a picked avatar sits (2 px gap in the panel's color, then the ring).</summary>
    public Thickness PickedRingMargin { get; set; }

    /// <summary>Width of the ring around a picked avatar.</summary>
    public Thickness PickedRingThickness { get; set; }

    /// <summary>Size of the check badge on a picked avatar, its panel-colored ring included.</summary>
    public double BadgeOuterSize { get; set; }

    /// <summary>How far the badge reaches past the avatar's corner.</summary>
    public Thickness BadgeMargin { get; set; }

    /// <summary>The panel-colored ring around the badge.</summary>
    public Thickness BadgePadding { get; set; }

    /// <summary>Size of the check in the badge.</summary>
    public double BadgeGlyphSize { get; set; }

    /// <summary>Height of the Send button.</summary>
    public double SendHeight { get; set; }

    /// <summary>Fill of the Send button while nobody is picked.</summary>
    public Color SendEmptyColor { get; set; }

    /// <summary>Size of the glyph before the error line.</summary>
    public double ErrorGlyphSize { get; set; }

    /// <summary>Gap between the error glyph and its text.</summary>
    public double ErrorGap { get; set; }

    /// <summary>Distance a digit travels when the count rolls.</summary>
    public double DigitRoll { get; set; }

    /// <summary>Size of the avatars on the confirmation.</summary>
    public double SentAvatarSize { get; set; }

    /// <summary>How far the confirmation's avatars overlap.</summary>
    public double SentAvatarOverlap { get; set; }

    /// <summary>Width of the panel-colored ring around the confirmation's avatars and badge.</summary>
    public double SentRing { get; set; }

    /// <summary>Size of the confirmation's success badge, its panel-colored ring included.</summary>
    public double SentBadgeOuterSize { get; set; }

    /// <summary>How far the success badge reaches past the last avatar's corner.</summary>
    public Thickness SentBadgeMargin { get; set; }

    /// <summary>The panel-colored ring around the success badge.</summary>
    public Thickness SentBadgePadding { get; set; }

    /// <summary>Size of the check in the success badge.</summary>
    public double SentBadgeGlyphSize { get; set; }

    /// <summary>Gap between the confirmation's lines.</summary>
    public double SentLineGap { get; set; }

    /// <summary>Extra space above the confirmation's title.</summary>
    public Thickness SentTitleMargin { get; set; }

    /// <summary>Extra space above the confirmation's buttons.</summary>
    public Thickness SentActionsMargin { get; set; }

    /// <summary>Gap between Undo and Done.</summary>
    public double SentButtonGap { get; set; }

    /// <summary>Height of the confirmation's buttons.</summary>
    public double SentButtonHeight { get; set; }

    /// <summary>The widest viewport that still gets the bottom sheet instead of the floating panel.</summary>
    public double SheetBreakpoint { get; set; }

    /// <summary>Space between the bottom sheet and the viewport's sides and bottom.</summary>
    public Thickness SheetMargin { get; set; }

    /// <summary>Corner radius of the bottom sheet.</summary>
    public CornerRadius SheetRadius { get; set; }

    /// <summary>Height of the grabber row at the top of the bottom sheet.</summary>
    public double GrabberRowHeight { get; set; }

    /// <summary>Width of the grabber.</summary>
    public double GrabberWidth { get; set; }

    /// <summary>Height of the grabber.</summary>
    public double GrabberHeight { get; set; }

    /// <summary>A slow drag down past this distance closes the bottom sheet.</summary>
    public double SheetDismissDistance { get; set; }

    /// <summary>A flick down faster than this (px/s) closes the bottom sheet.</summary>
    public double SheetDismissVelocity { get; set; }

    /// <summary>Margin of the keyboard focus ring around a part (negative: outside it).</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Corner radius of the focus ring around an access option.</summary>
    public CornerRadius OptionFocusRadius { get; set; }

    /// <summary>Corner radius of the focus ring around a person.</summary>
    public CornerRadius PersonFocusRadius { get; set; }

    /// <summary>Margin of the focus ring drawn inside the access row (the card clips it).</summary>
    public Thickness InsetFocusRingMargin { get; set; }

    /// <summary>Corner radius of the focus ring inside the access row.</summary>
    public CornerRadius InsetFocusRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        TriggerHeight = 36;
        TriggerPadding = new Thickness(14, 0, 16, 0);
        GlyphGap = 8;
        GlyphSize = 16;
        ButtonHoverColor = Oklch.MixOklab(n.Foreground, n.Background, 0.14);
        PanelWidth = 384;
        SentWidth = 304;
        PanelPadding = new Thickness(8);
        SheetPanelPadding = new Thickness(10, 0, 10, 10);
        SentPadding = new Thickness(16, 20, 16, 14);
        PanelRadius = 28;
        SentRadius = 26;
        SectionGap = 12;
        HeaderHeight = 36;
        TextIndent = new Thickness(8, 0, 0, 0);
        CloseSize = 32;
        SheetCloseSize = 36;
        CardRadius = new CornerRadius(20);
        CardColor = Oklch.WithAlpha(n.SurfaceMuted, 0.7);
        LinkRowHeight = 48;
        LinkRowPadding = new Thickness(14, 0, 6, 0);
        RowGap = 10;
        CopyHeight = 34;
        CopyPadding = new Thickness(12, 0);
        CopyGap = 6;
        CopyGlyphSize = 14;
        AccessRowHeight = 52;
        AccessRowPadding = new Thickness(14, 6);
        AccessListPadding = new Thickness(6, 0, 6, 6);
        OptionGap = 2;
        OptionPadding = new Thickness(8);
        OptionRadius = new CornerRadius(14);
        OptionSelectedColor = Oklch.WithAlpha(n.Foreground, 0.04);
        HoverColor = Oklch.WithAlpha(n.Foreground, 0.05);
        LineGap = 1;
        ChannelHeight = 40;
        ChannelPadding = new Thickness(8, 0);
        ChannelGap = 6;
        DividerMargin = new Thickness(6, 2);
        SendToGap = 6;
        ChipsRowHeight = 36;
        ChipsPadding = new Thickness(4, 0);
        ChipHeight = 32;
        ChipPadding = new Thickness(4, 0);
        ChipGap = 6;
        ChipAvatarSize = 24;
        ChipColor = Oklch.WithAlpha(n.Foreground, 0.07);
        RemoveSize = 24;
        RemoveGlyphSize = 14;
        PeopleGap = 4;
        PersonPadding = new Thickness(2, 8, 2, 6);
        PersonGap = 6;
        PersonRadius = new CornerRadius(18);
        PersonAvatarSize = 40;
        PickedRingMargin = new Thickness(-4);
        PickedRingThickness = new Thickness(2);
        BadgeOuterSize = 20;
        BadgeMargin = new Thickness(0, 0, -5, -5);
        BadgePadding = new Thickness(2);
        BadgeGlyphSize = 10;
        SendHeight = 44;
        SendEmptyColor = Oklch.WithAlpha(n.Foreground, 0.08);
        ErrorGlyphSize = 14;
        ErrorGap = 6;
        DigitRoll = 14;
        SentAvatarSize = 44;
        SentAvatarOverlap = 12;
        SentRing = 3;
        SentBadgeOuterSize = 28;
        SentBadgeMargin = new Thickness(0, 0, -9, -7);
        SentBadgePadding = new Thickness(3);
        SentBadgeGlyphSize = 14;
        SentLineGap = 4;
        SentTitleMargin = new Thickness(0, 10, 0, 0);
        SentActionsMargin = new Thickness(0, 14, 0, 0);
        SentButtonGap = 8;
        SentButtonHeight = 40;
        SheetBreakpoint = 520;
        SheetMargin = new Thickness(8);
        SheetRadius = new CornerRadius(30);
        GrabberRowHeight = 22;
        GrabberWidth = 36;
        GrabberHeight = 4;
        SheetDismissDistance = 120;
        SheetDismissVelocity = 600;
        FocusRingMargin = new Thickness(-4);
        OptionFocusRadius = new CornerRadius(18);
        PersonFocusRadius = new CornerRadius(22);
        InsetFocusRingMargin = new Thickness(2);
        InsetFocusRadius = new CornerRadius(12);
    }
}
