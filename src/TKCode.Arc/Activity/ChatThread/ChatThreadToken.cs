using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ChatThread"/> (Arc chat-thread.module.css) and the accent-independent bubble fill. Colors that
/// follow the accent (your own bubbles, chosen reactions) come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ChatThreadToken : AbstractControlDesignToken
{
    /// <summary>Content padding: <c>12px 16px 14px</c>.</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>Under 420px: <c>12px 12px 14px</c>.</summary>
    public Thickness ContentPaddingNarrow { get; set; }

    /// <summary>Width under which the thread uses its narrow layout (Arc's 420px media query).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>A message following another of the same group: 2px above.</summary>
    public Thickness RowMargin { get; set; }

    /// <summary>The first message of a group: 14px above.</summary>
    public Thickness GroupMargin { get; set; }

    /// <summary>The first message under a day separator: 10px above.</summary>
    public Thickness AfterDayMargin { get; set; }

    /// <summary>The author's gutter (28px) beside other people's messages.</summary>
    public double GutterWidth { get; set; }

    /// <summary>Gap between the gutter and the message stack (8px).</summary>
    public double RowGap { get; set; }

    /// <summary>Message stack width limit as a share of the row (78%; 84% when narrow) and in px (34rem).</summary>
    public double StackMaxRatio { get; set; }

    /// <summary>Message stack width share under 420px (84%).</summary>
    public double StackMaxRatioNarrow { get; set; }

    /// <summary>Message stack width cap (34rem = 544px).</summary>
    public double StackMaxWidth { get; set; }

    /// <summary>Gap between the parts of a message stack (meta, line, reactions, receipts): 4px.</summary>
    public double StackGap { get; set; }

    /// <summary>Meta line (author and time): <c>0 12px</c>, 2px below.</summary>
    public Thickness MetaMargin { get; set; }

    /// <summary>Gap between author and time (6px).</summary>
    public double MetaGap { get; set; }

    /// <summary>Gap between the bubble and the add-reaction button (6px).</summary>
    public double LineGap { get; set; }

    /// <summary>Author portrait in the gutter (28px).</summary>
    public double FaceSize { get; set; }

    /// <summary>Bubble radius (18px).</summary>
    public double BubbleRadius { get; set; }

    /// <summary>The typing bubble's corners (a group of its own: 18px all round).</summary>
    public CornerRadius TypingRadius { get; set; }

    /// <summary>A bubble's corners on the author's side inside a group (6px).</summary>
    public double BubbleTightRadius { get; set; }

    /// <summary>Bubble padding: <c>8px 13px 9px</c>.</summary>
    public Thickness BubblePadding { get; set; }

    /// <summary>Bubble text line height (14px × Arc's 1.4 body leading).</summary>
    public double BubbleLineHeight { get; set; }

    /// <summary>Space between Markdown blocks inside a bubble.</summary>
    public double BubbleBlockSpacing { get; set; }

    /// <summary>Other people's bubbles: SurfaceMuted in light mode, SurfaceRaised in dark mode (Arc's dark override).</summary>
    public Color BubbleFill { get; set; }

    /// <summary>A failed message's bubble opacity (0.6).</summary>
    public double FailedOpacity { get; set; }

    /// <summary>Image gallery width (240px), tile gap (3px) and radii (16px outside, 6px between tiles).</summary>
    public double GalleryWidth { get; set; }

    /// <summary>Gap between gallery tiles (3px).</summary>
    public double GalleryGap { get; set; }

    /// <summary>Outer radius of the gallery and of a lone picture (16px).</summary>
    public double GalleryRadius { get; set; }

    /// <summary>Radius of tiles inside a multi-picture gallery (6px).</summary>
    public double GalleryTileRadius { get; set; }

    /// <summary>The "+N" scrim over the fourth tile: <c>oklch(0% 0 0 / .45)</c>.</summary>
    public Color MoreScrim { get; set; }

    /// <summary>File row padding: <c>9px 14px 9px 11px</c>.</summary>
    public Thickness FilePadding { get; set; }

    /// <summary>File row radius (16px).</summary>
    public CornerRadius FileRadius { get; set; }

    /// <summary>Gap between a file row's icon and its text (10px).</summary>
    public double FileGap { get; set; }

    /// <summary>Reactions row: <c>margin -2px -4px; padding 2px 4px 2px 8px</c> (mirrored for your own messages).</summary>
    public Thickness ReactionsMargin { get; set; }

    /// <summary>Reactions row padding beside other people's messages.</summary>
    public Thickness ReactionsPadding { get; set; }

    /// <summary>Reactions row padding beside your own messages.</summary>
    public Thickness ReactionsPaddingMine { get; set; }

    /// <summary>Receipts line: <c>padding 1px 6px 0</c>, min height 16px, gap 6px.</summary>
    public Thickness ReceiptsPadding { get; set; }

    /// <summary>Gap between the status and the readers (6px).</summary>
    public double ReceiptsGap { get; set; }

    /// <summary>Reader portraits (16px) overlapping by 4px with a 2px surface ring.</summary>
    public double ReaderSize { get; set; }

    /// <summary>Overlap between reader portraits (-4px).</summary>
    public double ReaderOverlap { get; set; }

    /// <summary>Retry button: <c>2px 8px</c>, 4px between icon and label.</summary>
    public Thickness RetryPadding { get; set; }

    /// <summary>Day separator: <c>20px 4px 4px</c> (first row: 4px on top); lines 12px from the label.</summary>
    public Thickness DayPadding { get; set; }

    /// <summary>The first day separator's padding.</summary>
    public Thickness DayPaddingFirst { get; set; }

    /// <summary>Gap between the day label and its lines (12px).</summary>
    public double DayGap { get; set; }

    /// <summary>Typing bubble height (36px) and padding (0 14px).</summary>
    public double TypingHeight { get; set; }

    /// <summary>Typing bubble padding.</summary>
    public Thickness TypingPadding { get; set; }

    /// <summary>Add-reaction button (28px).</summary>
    public double ReactButtonSize { get; set; }

    /// <summary>Picker emoji buttons (34px) with 19px emoji, 2px apart in a 4px padded pill, 6px from the line.</summary>
    public double EmojiButtonSize { get; set; }

    /// <summary>Picker emoji size (19px).</summary>
    public double EmojiFontSize { get; set; }

    /// <summary>Picker padding (4px).</summary>
    public Thickness PickerPadding { get; set; }

    /// <summary>Picker gap between emoji (2px).</summary>
    public double PickerGap { get; set; }

    /// <summary>Picker distance from the line (6px).</summary>
    public double PickerOffset { get; set; }

    /// <summary>Jump pill: 32px high, <c>0 14px 0 11px</c>, 6px gap, 12px above the bottom.</summary>
    public double PillHeight { get; set; }

    /// <summary>Jump pill padding.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>Jump pill content gap (6px).</summary>
    public double PillGap { get; set; }

    /// <summary>Jump pill bottom margin (12px).</summary>
    public Thickness PillMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        ContentPadding = new Thickness(16, 12, 16, 14);
        ContentPaddingNarrow = new Thickness(12, 12, 12, 14);
        NarrowWidth = 420;
        RowMargin = new Thickness(0, 2, 0, 0);
        GroupMargin = new Thickness(0, 14, 0, 0);
        AfterDayMargin = new Thickness(0, 10, 0, 0);
        GutterWidth = 28;
        RowGap = 8;
        StackMaxRatio = 0.78;
        StackMaxRatioNarrow = 0.84;
        StackMaxWidth = 544;
        StackGap = 4;
        MetaMargin = new Thickness(12, 0, 12, 2);
        MetaGap = 6;
        LineGap = 6;
        FaceSize = 28;
        BubbleRadius = 18;
        BubbleTightRadius = 6;
        TypingRadius = new CornerRadius(18);
        BubblePadding = new Thickness(13, 8, 13, 9);
        BubbleLineHeight = 19.6;
        BubbleBlockSpacing = 8;
        BubbleFill = isDarkMode ? neutrals.SurfaceRaised : neutrals.SurfaceMuted;
        FailedOpacity = 0.6;
        GalleryWidth = 240;
        GalleryGap = 3;
        GalleryRadius = 16;
        GalleryTileRadius = 6;
        MoreScrim = Color.FromArgb(115, 0, 0, 0);
        FilePadding = new Thickness(11, 9, 14, 9);
        FileRadius = new CornerRadius(16);
        FileGap = 10;
        ReactionsMargin = new Thickness(-4, -2, -4, -2);
        ReactionsPadding = new Thickness(8, 2, 4, 2);
        ReactionsPaddingMine = new Thickness(4, 2, 8, 2);
        ReceiptsPadding = new Thickness(6, 1, 6, 0);
        ReceiptsGap = 6;
        ReaderSize = 16;
        ReaderOverlap = -4;
        RetryPadding = new Thickness(8, 2);
        DayPadding = new Thickness(4, 20, 4, 4);
        DayPaddingFirst = new Thickness(4, 4, 4, 4);
        DayGap = 12;
        TypingHeight = 36;
        TypingPadding = new Thickness(14, 0);
        ReactButtonSize = 28;
        EmojiButtonSize = 34;
        EmojiFontSize = 19;
        PickerPadding = new Thickness(4);
        PickerGap = 2;
        PickerOffset = 6;
        PillHeight = 32;
        PillPadding = new Thickness(11, 0, 14, 0);
        PillGap = 6;
        PillMargin = new Thickness(0, 0, 0, 12);
    }
}
