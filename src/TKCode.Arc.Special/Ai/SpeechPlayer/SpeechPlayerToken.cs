using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SpeechPlayer"/>: the card, the author line, the reply's type and paragraph spacing, the word
/// highlight, how dim the words still to come are, the 32px dock (36px on touch) and its buttons, the progress bar and
/// the width below which the total time steps aside. Colors and fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SpeechPlayerToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows; it fills narrower containers.</summary>
    public double MaxCardWidth { get; set; }

    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card's padding (plain drops it).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The author line: its height, the mark, the name's size and the space under it.</summary>
    public double AuthorHeight { get; set; }

    public double AuthorMarkSize { get; set; }

    public double AuthorFontSize { get; set; }

    public Thickness AuthorMargin { get; set; }

    /// <summary>The reply: 15/24, with this much space between paragraphs.</summary>
    public double ReplyFontSize { get; set; }

    public double ReplyLineHeight { get; set; }

    public double ParagraphGap { get; set; }

    /// <summary>The space between the reply and the dock.</summary>
    public Thickness DockMargin { get; set; }

    /// <summary>How opaque the words still to come are while reading (body contrast is kept).</summary>
    public double UpcomingOpacity { get; set; }

    /// <summary>The spoken word's highlight: its radius and how far it reaches past the word.</summary>
    public CornerRadius HighlightCornerRadius { get; set; }

    public Thickness HighlightOutset { get; set; }

    /// <summary>The glide between words at 1× (shorter at 1.5× and 2× so it lands before the next word).</summary>
    public TimeSpan HighlightGlide { get; set; }

    /// <summary>The dock: the action row and the player share this one slot (taller on touch).</summary>
    public double DockHeight { get; set; }

    public double DockTouchHeight { get; set; }

    /// <summary>Square icon buttons and the play button (and their touch sizes).</summary>
    public double IconButtonSize { get; set; }

    public double IconButtonTouchSize { get; set; }

    public double IconSize { get; set; }

    /// <summary>Text buttons in the dock (Read aloud, speed, voice).</summary>
    public Thickness TextButtonPadding { get; set; }

    public double DockFontSize { get; set; }

    /// <summary>The space between the dock's items.</summary>
    public double DockGap { get; set; }

    /// <summary>The times either side of the bar.</summary>
    public double TimeFontSize { get; set; }

    public double TimeMinWidth { get; set; }

    /// <summary>The progress bar: the track at rest and on hover, focus or drag, and the thumb.</summary>
    public double TrackThickness { get; set; }

    public double TrackActiveThickness { get; set; }

    public double ThumbSize { get; set; }

    /// <summary>Below this width the total time steps aside so the bar keeps room.</summary>
    public double NarrowWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxCardWidth = 640;
        CardCornerRadius = new CornerRadius(16);
        CardPadding = new Thickness(18, 16, 14, 10);
        AuthorHeight = 20;
        AuthorMarkSize = 16;
        AuthorFontSize = 13;
        AuthorMargin = new Thickness(0, 0, 0, 8);
        ReplyFontSize = 15;
        ReplyLineHeight = 24;
        ParagraphGap = 12;
        DockMargin = new Thickness(-6, 10, 0, 0);
        UpcomingOpacity = 0.58;
        HighlightCornerRadius = new CornerRadius(5);
        HighlightOutset = new Thickness(3, 1);
        HighlightGlide = TimeSpan.FromMilliseconds(150);
        DockHeight = 32;
        DockTouchHeight = 36;
        IconButtonSize = 28;
        IconButtonTouchSize = 36;
        IconSize = 16;
        TextButtonPadding = new Thickness(8, 0);
        DockFontSize = 13;
        DockGap = 4;
        TimeFontSize = 12;
        TimeMinWidth = 30;
        TrackThickness = 3;
        TrackActiveThickness = 5;
        ThumbSize = 12;
        NarrowWidth = 440;
    }
}
