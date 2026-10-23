using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ProjectBoard"/> (Arc Project board, design reference, measured at 1440 / 700 / 390): the header,
/// the filter switch, the Add button, the sprint line, the cards and their arrow buttons. Column and slot geometry lives in
/// <c>ProjectBoardLayout</c>; colors, shadows and type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ProjectBoardToken : AbstractControlDesignToken
{
    /// <summary>Header padding 18 / 20 / 16 (narrow 14 / 16 / 12); 12 between wrapped rows, 16 beside the title, 10 between actions.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness NarrowHeaderPadding { get; set; }

    public double HeaderRowGap { get; set; }

    public double HeaderColumnGap { get; set; }

    public double ActionGap { get; set; }

    /// <summary>Every text runs at −0.14 letter spacing (Inter −0.01em).</summary>
    public double TextTracking { get; set; }

    /// <summary>The title 16/17.6 over the progress sentence 14/19.6, 2 apart.</summary>
    public double TitleLineHeight { get; set; }

    public double SubtitleLineHeight { get; set; }

    public Thickness SubtitleMargin { get; set; }

    /// <summary>The sprint line: 2 high, 20 in from the sides and 16 above the columns (narrow 16 / 12).</summary>
    public Thickness ProgressMargin { get; set; }

    public Thickness NarrowProgressMargin { get; set; }

    /// <summary>The team avatars: 24 faces in 2px surface rings (28 boxes) 22 apart, 10 before the filter.</summary>
    public double TeamAvatarDiameter { get; set; }

    public Thickness TeamMargin { get; set; }

    public double TeamAvatarRing { get; set; }

    public double TeamAvatarOverlap { get; set; }

    /// <summary>The filter track: 2 of padding, radius 11; buttons 28 high (narrow 36), 10 to the sides (narrow 12), radius 9.</summary>
    public Thickness FilterPadding { get; set; }

    public CornerRadius FilterRadius { get; set; }

    public double FilterButtonHeight { get; set; }

    public double NarrowFilterButtonHeight { get; set; }

    public Thickness FilterButtonPadding { get; set; }

    public Thickness NarrowFilterButtonPadding { get; set; }

    public CornerRadius FilterButtonRadius { get; set; }

    public CornerRadius FilterFocusRadius { get; set; }

    /// <summary>Add task: 32 high (narrow 40), padding 10 / 12, radius 10, Plus 16 with 6 to the label; compact: a 40 square.</summary>
    public double AddHeight { get; set; }

    public double NarrowAddHeight { get; set; }

    public Thickness AddPadding { get; set; }

    public CornerRadius AddRadius { get; set; }

    public CornerRadius AddFocusRadius { get; set; }

    public double AddGap { get; set; }

    public double AddGlyphSize { get; set; }

    public Thickness AddMargin { get; set; }

    /// <summary>Lucide stroke of every board glyph (1.75).</summary>
    public double GlyphStroke { get; set; }

    /// <summary>Hovered Add button opacity (.88) and disabled arrow opacity (.3).</summary>
    public double AddHoverOpacity { get; set; }

    public double DisabledOpacity { get; set; }

    /// <summary>A card: 12 of padding in a 1px border, radius 11.</summary>
    public Thickness CardPadding { get; set; }

    public CornerRadius CardRadius { get; set; }

    /// <summary>Area, due and comment count 12/16.8; the title 14/15.4 medium, 6 above and 12 below.</summary>
    public double CardSmallLineHeight { get; set; }

    public double CardTitleLineHeight { get; set; }

    public Thickness CardTitleMargin { get; set; }

    /// <summary>8 between the area and the due label, and after the owner avatar.</summary>
    public Thickness DueMargin { get; set; }

    public Thickness OwnerMargin { get; set; }

    /// <summary>The bottom row: 26 high (narrow 24), 8 between avatar, comments and buttons; owner avatar 22.</summary>
    public double CardFooterHeight { get; set; }

    public double NarrowCardFooterHeight { get; set; }

    public double CardFooterGap { get; set; }

    public double OwnerDiameter { get; set; }

    /// <summary>The comment count: MessageCircle 14, 4 to the number.</summary>
    public double CommentGlyphSize { get; set; }

    public double CommentGap { get; set; }

    /// <summary>The hovered card's shadow (0 1 3 black 5%) and the transparent shadow it fades from.</summary>
    public BoxShadows CardHoverShadow { get; set; }

    public BoxShadows CardRestShadow { get; set; }

    /// <summary>Arrow buttons: 26 squares, radius 8, 2 apart, arrows 14; narrow: 40 squares, radius 12, the pair −8 out (touch size, same card height).</summary>
    public double MoveButtonSize { get; set; }

    public double NarrowMoveButtonSize { get; set; }

    public CornerRadius MoveButtonRadius { get; set; }

    public CornerRadius NarrowMoveButtonRadius { get; set; }

    public CornerRadius MoveFocusRadius { get; set; }

    public CornerRadius NarrowMoveFocusRadius { get; set; }

    public Thickness NarrowMovesMargin { get; set; }

    public double MoveGap { get; set; }

    public double MoveGlyphSize { get; set; }

    /// <summary>Column heading: 24 of content and 4 under it, 6 to the sides, 8 between name and count; name 14/15.4 medium.</summary>
    public Thickness ColumnHeadingMargin { get; set; }

    public double ColumnHeadingHeight { get; set; }

    public double ColumnHeadingGap { get; set; }

    public double ColumnHeadingLineHeight { get; set; }

    /// <summary>The empty text sits in the slot's content box (6 in) with 20 of padding, centred.</summary>
    public Thickness EmptyMargin { get; set; }

    /// <summary>The framed board's corner radius (24).</summary>
    public CornerRadius FramedRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderPadding = new Thickness(20, 18, 20, 16);
        NarrowHeaderPadding = new Thickness(16, 14, 16, 12);
        HeaderRowGap = 12;
        HeaderColumnGap = 16;
        ActionGap = 10;
        TextTracking = -0.14;
        TitleLineHeight = 17.6;
        SubtitleLineHeight = 19.6;
        SubtitleMargin = new Thickness(0, 2, 0, 0);
        ProgressMargin = new Thickness(20, 0, 20, 16);
        NarrowProgressMargin = new Thickness(16, 0, 16, 12);
        TeamAvatarDiameter = 24;
        TeamMargin = new Thickness(2, 0, 12, 0);
        TeamAvatarRing = 2;
        TeamAvatarOverlap = 2;
        FilterPadding = new Thickness(2);
        FilterRadius = new CornerRadius(11);
        FilterButtonHeight = 28;
        NarrowFilterButtonHeight = 36;
        FilterButtonPadding = new Thickness(10, 0);
        NarrowFilterButtonPadding = new Thickness(12, 0);
        FilterButtonRadius = new CornerRadius(9);
        FilterFocusRadius = new CornerRadius(13);
        AddHeight = 32;
        NarrowAddHeight = 40;
        AddPadding = new Thickness(10, 0, 12, 0);
        AddRadius = new CornerRadius(10);
        AddFocusRadius = new CornerRadius(14);
        AddGap = 6;
        AddGlyphSize = 16;
        AddMargin = new Thickness(10, 0, 0, 0);
        GlyphStroke = 1.75;
        AddHoverOpacity = 0.88;
        DisabledOpacity = 0.3;
        CardPadding = new Thickness(12);
        CardRadius = new CornerRadius(11);
        CardSmallLineHeight = 16.8;
        CardTitleLineHeight = 15.4;
        CardTitleMargin = new Thickness(0, 6, 0, 12);
        DueMargin = new Thickness(8, 0, 0, 0);
        OwnerMargin = new Thickness(0, 0, 8, 0);
        CardFooterHeight = 26;
        NarrowCardFooterHeight = 24;
        CardFooterGap = 8;
        OwnerDiameter = 22;
        CommentGlyphSize = 14;
        CommentGap = 4;
        CardHoverShadow = BoxShadows.Parse("0 1 3 0 #0D000000");
        CardRestShadow = BoxShadows.Parse("0 1 3 0 #00000000");
        MoveButtonSize = 26;
        NarrowMoveButtonSize = 40;
        MoveButtonRadius = new CornerRadius(8);
        NarrowMoveButtonRadius = new CornerRadius(12);
        MoveFocusRadius = new CornerRadius(12);
        NarrowMoveFocusRadius = new CornerRadius(16);
        NarrowMovesMargin = new Thickness(-8);
        MoveGap = 2;
        MoveGlyphSize = 14;
        ColumnHeadingMargin = new Thickness(6, 0, 6, 4);
        ColumnHeadingHeight = 24;
        ColumnHeadingGap = 8;
        ColumnHeadingLineHeight = 15.4;
        EmptyMargin = new Thickness(26, 54, 26, 26);
        FramedRadius = new CornerRadius(24);
    }
}
