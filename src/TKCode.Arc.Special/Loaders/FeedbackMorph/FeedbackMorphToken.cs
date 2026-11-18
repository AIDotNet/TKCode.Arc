using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="FeedbackMorph"/> (Arc Feedback morph, design reference) and the few tints it derives from the
/// neutral palette: a 40 px button with a 16 px glyph, a 352 px panel (12 px padding and row gap) with a 28 px title row,
/// 44 px face buttons on 52 px columns, an 80 px note and a 32 px action row, a 40 px thank-you pill, and the three
/// shadows of the button, the panel and the pill. Text colors, the accent send button, the success and danger inks, the
/// radii and the fonts come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class FeedbackMorphToken : AbstractControlDesignToken
{
    /// <summary>Height of the button and of the thank-you pill (40).</summary>
    public double PillHeight { get; set; }

    /// <summary>Space inside the button: 16 before the glyph, 18 after the label.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>Space inside the thank-you pill: 12 before the check, 18 after the words.</summary>
    public Thickness SentPadding { get; set; }

    /// <summary>Gap between a pill's glyph and its text (8).</summary>
    public double PillGap { get; set; }

    /// <summary>Size of the button's glyph (16).</summary>
    public double PillGlyphSize { get; set; }

    /// <summary>Size of the thank-you check (22).</summary>
    public double SentGlyphSize { get; set; }

    /// <summary>Gap between the words of the thank-you text (one space at 13 px).</summary>
    public double WordGap { get; set; }

    /// <summary>Text size of the button, the thank-you pill, the note and the send button (13).</summary>
    public double LabelFontSize { get; set; }

    /// <summary>Line height of the 13 px texts (18).</summary>
    public double LabelLineHeight { get; set; }

    /// <summary>Text size of the panel's question (15).</summary>
    public double TitleFontSize { get; set; }

    /// <summary>Line height of the panel's question (21).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Text size of the caption under the faces and of the status line (12).</summary>
    public double SmallFontSize { get; set; }

    /// <summary>Line height of the caption (18).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Width of the open panel (352), capped by the viewport less <see cref="ViewportMargin"/> on each side.</summary>
    public double PanelWidth { get; set; }

    /// <summary>Room the panel keeps from each side of the viewport (16).</summary>
    public double ViewportMargin { get; set; }

    /// <summary>Space between the panel's edge and its rows (12).</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>Gap between the panel's rows (12).</summary>
    public double RowGap { get; set; }

    /// <summary>Height of the title row (28).</summary>
    public double HeaderHeight { get; set; }

    /// <summary>Space before the question (4).</summary>
    public Thickness TitlePadding { get; set; }

    /// <summary>Side of the round close button (28).</summary>
    public double CloseSize { get; set; }

    /// <summary>Size of the close glyph (16).</summary>
    public double CloseGlyphSize { get; set; }

    /// <summary>Height of the row of faces (52).</summary>
    public double MoodRowHeight { get; set; }

    /// <summary>Side of a face button (44, the touch target).</summary>
    public double MoodButtonSize { get; set; }

    /// <summary>Size of a face (24).</summary>
    public double MoodGlyphSize { get; set; }

    /// <summary>Side of the round highlight behind the picked face (48).</summary>
    public double MoodHighlightSize { get; set; }

    /// <summary>Scale of the picked face (1.14).</summary>
    public double MoodSelectedScale { get; set; }

    /// <summary>Scale of the other faces once one is picked (0.92).</summary>
    public double MoodRestScale { get; set; }

    /// <summary>Gap between the faces and the caption (4).</summary>
    public double CaptionGap { get; set; }

    /// <summary>Height of the note (80).</summary>
    public double FieldHeight { get; set; }

    /// <summary>Space inside the note (12 across, 10 down).</summary>
    public Thickness FieldPadding { get; set; }

    /// <summary>Line height of the note (20).</summary>
    public double FieldLineHeight { get; set; }

    /// <summary>Height of the action row and of the send button (32).</summary>
    public double ActionsHeight { get; set; }

    /// <summary>Space inside the send button (16 across).</summary>
    public Thickness SendPadding { get; set; }

    /// <summary>Gap between the send button's text and its glyph (6).</summary>
    public double SendGlyphGap { get; set; }

    /// <summary>Size of the send, spinner and alert glyphs (16).</summary>
    public double SmallGlyphSize { get; set; }

    /// <summary>Opacity of the send button while there is nothing to send (0.45).</summary>
    public double EmptySendOpacity { get; set; }

    /// <summary>Opacity of the faces while the note is being sent (0.5).</summary>
    public double BusyMoodsOpacity { get; set; }

    /// <summary>Space before the status line (4).</summary>
    public Thickness StatusPadding { get; set; }

    /// <summary>How far the keyboard focus ring sits outside a pill or a round button (2 px gap, 2 px ring).</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>The surface fill: the raised surface, opaque (Arc lets 18% through behind a 24 px backdrop blur, which reads the same).</summary>
    public Color SurfaceFill { get; set; }

    /// <summary>The note's fill: the muted surface at 72%.</summary>
    public Color FieldFill { get; set; }

    /// <summary>The round highlight behind the picked face: the foreground at 8%.</summary>
    public Color HighlightFill { get; set; }

    /// <summary>The thank-you check's disc: the success ink at 14%.</summary>
    public Color SuccessDisc { get; set; }

    /// <summary>The button's resting shadow.</summary>
    public BoxShadows PillShadow { get; set; }

    /// <summary>The open panel's shadow.</summary>
    public BoxShadows PanelShadow { get; set; }

    /// <summary>The thank-you pill's shadow.</summary>
    public BoxShadows SentShadow { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        PillHeight = 40;
        PillPadding = new Thickness(16, 0, 18, 0);
        SentPadding = new Thickness(12, 0, 18, 0);
        PillGap = 8;
        PillGlyphSize = 16;
        SentGlyphSize = 22;
        WordGap = 3.6;
        LabelFontSize = 13;
        LabelLineHeight = 18;
        TitleFontSize = 15;
        TitleLineHeight = 21;
        SmallFontSize = 12;
        SmallLineHeight = 18;
        PanelWidth = 352;
        ViewportMargin = 16;
        PanelPadding = new Thickness(12);
        RowGap = 12;
        HeaderHeight = 28;
        TitlePadding = new Thickness(4, 0, 0, 0);
        CloseSize = 28;
        CloseGlyphSize = 16;
        MoodRowHeight = 52;
        MoodButtonSize = 44;
        MoodGlyphSize = 24;
        MoodHighlightSize = 48;
        MoodSelectedScale = 1.14;
        MoodRestScale = 0.92;
        CaptionGap = 4;
        FieldHeight = 80;
        FieldPadding = new Thickness(12, 10);
        FieldLineHeight = 20;
        ActionsHeight = 32;
        SendPadding = new Thickness(16, 0);
        SendGlyphGap = 6;
        SmallGlyphSize = 16;
        EmptySendOpacity = 0.45;
        BusyMoodsOpacity = 0.5;
        StatusPadding = new Thickness(4, 0, 0, 0);
        FocusRingMargin = new Thickness(-4);
        SurfaceFill = n.SurfaceRaised;
        FieldFill = Oklch.WithAlpha(n.SurfaceMuted, 0.72);
        HighlightFill = Oklch.WithAlpha(n.Foreground, 0.08);
        SuccessDisc = Oklch.WithAlpha(n.Success, 0.14);
        PillShadow = isDarkMode
            ? BoxShadows.Parse("0 1 2 0 #2E000000")
            : BoxShadows.Parse("0 1 2 0 #09000000");
        PanelShadow = isDarkMode
            ? BoxShadows.Parse("0 22 55 0 #57000000")
            : BoxShadows.Parse("0 20 48 0 #1B000000, 0 3 10 0 #0B000000");
        SentShadow = isDarkMode
            ? BoxShadows.Parse("0 8 22 0 #38000000")
            : BoxShadows.Parse("0 6 18 0 #11000000, 0 1 3 0 #09000000");
    }
}
