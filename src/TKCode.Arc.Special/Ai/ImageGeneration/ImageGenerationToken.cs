using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ImageGeneration"/>: the card, the chips, the 2 × 2 grid and its tiles, the progress pill, the
/// selection ring and badge, and the footer. Colors, fonts and the focus ring come from the ArcDesign tokens in the theme;
/// the pill's scrim is a fixed dark glass that reads on any image.
/// </summary>
[ControlDesignToken]
internal sealed class ImageGenerationToken : AbstractControlDesignToken
{
    /// <summary>The card fills its container up to this width.</summary>
    public double CardMaxWidth { get; set; }

    public CornerRadius CardCornerRadius { get; set; }

    public Thickness CardPadding { get; set; }

    /// <summary>The space between the prompt, the chips, the grid and the footer.</summary>
    public double SectionGap { get; set; }

    /// <summary>
    /// The prompt box's smallest height: three lines of prompt (3 × 19.6) plus the field's 12px padding and 1px border on
    /// each side, rounded up (Arc's textarea default of 110px leaves the two-line sample prompt in a mostly empty box).
    /// </summary>
    public double PromptMinHeight { get; set; }

    /// <summary>A chip: 32 high, pill shaped, 10 in from the sides, a 16px mark, 13px text.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipGap { get; set; }

    public double ChipMarkSize { get; set; }

    public double ChipFontSize { get; set; }

    /// <summary>The space before a chip's text (it springs open on the chosen model).</summary>
    public Thickness ChipTextMargin { get; set; }

    /// <summary>A suggested prompt in the empty grid: the chip's height and font, 14 in from the sides.</summary>
    public Thickness SuggestionPadding { get; set; }

    /// <summary>The aspect chip's shape glyph fits in a square this size.</summary>
    public double ShapeSize { get; set; }

    /// <summary>The grid: the gap between tiles, its height cap and the tiles' corners.</summary>
    public double GridGap { get; set; }

    public double GridMaxHeight { get; set; }

    public CornerRadius TileCornerRadius { get; set; }

    /// <summary>The tile's keyboard focus ring, 3px outside it.</summary>
    public CornerRadius TileFocusCornerRadius { get; set; }

    public Thickness TileFocusMargin { get; set; }

    /// <summary>The aspect chip's shape outline: its corners and line.</summary>
    public CornerRadius ShapeCornerRadius { get; set; }

    public Thickness ShapeThickness { get; set; }

    /// <summary>The selection ring's width and the check badge's size.</summary>
    public Thickness SelectionThickness { get; set; }

    public double BadgeSize { get; set; }

    /// <summary>The progress pill in a running tile's corner: its padding, ring and percentage.</summary>
    public Thickness PillPadding { get; set; }

    public double PillGap { get; set; }

    public double RingSize { get; set; }

    public double RingThickness { get; set; }

    public double PercentFontSize { get; set; }

    /// <summary>The pill's glass and its text: black at 55% and white, on light and dark images alike.</summary>
    public Color ScrimColor { get; set; }

    public Color ScrimForeground { get; set; }

    /// <summary>The ring's track on the glass.</summary>
    public Color ScrimTrack { get; set; }

    /// <summary>A failed or stopped tile: the glyph and the two lines under it.</summary>
    public double StatusGlyphSize { get; set; }

    public double StatusFontSize { get; set; }

    /// <summary>The footer's status line and its row height.</summary>
    public double FooterHeight { get; set; }

    public double FooterFontSize { get; set; }

    /// <summary>The space between the three actions.</summary>
    public double ActionGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 640;
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(16);
        SectionGap = 12;
        PromptMinHeight = 85;
        ChipHeight = 32;
        ChipPadding = new Thickness(10, 0);
        ChipGap = 4;
        ChipMarkSize = 16;
        ChipFontSize = 13;
        ChipTextMargin = new Thickness(6, 0, 0, 0);
        SuggestionPadding = new Thickness(14, 0);
        ShapeSize = 12;
        GridGap = 8;
        GridMaxHeight = 420;
        TileCornerRadius = new CornerRadius(12);
        TileFocusCornerRadius = new CornerRadius(15);
        TileFocusMargin = new Thickness(-3);
        ShapeCornerRadius = new CornerRadius(2);
        ShapeThickness = new Thickness(1.5);
        SelectionThickness = new Thickness(2);
        BadgeSize = 22;
        PillPadding = new Thickness(6, 3, 8, 3);
        PillGap = 6;
        RingSize = 14;
        RingThickness = 2;
        PercentFontSize = 11;
        ScrimColor = Color.FromArgb(140, 0, 0, 0);
        ScrimForeground = Colors.White;
        ScrimTrack = Color.FromArgb(70, 255, 255, 255);
        StatusGlyphSize = 18;
        StatusFontSize = 12;
        FooterHeight = 36;
        FooterFontSize = 13;
        ActionGap = 8;
    }
}
