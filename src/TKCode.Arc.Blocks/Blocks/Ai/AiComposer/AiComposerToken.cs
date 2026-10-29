using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="AiComposer"/>, measured from the AI composer of the AI group (design reference only,
/// ADR 0015), and the accent-independent tints it derives from the neutrals. Colors that follow the accent, radii and
/// fonts come from ArcDesign tokens in the theme. The breakpoints apply to the composer's own width.
/// </summary>
[ControlDesignToken]
internal sealed class AiComposerToken : AbstractControlDesignToken
{
    /// <summary>Space between the prompt surface's border and its content (10).</summary>
    public Thickness SurfacePadding { get; set; }

    /// <summary>The same space in a tiny composer (8).</summary>
    public Thickness TinySurfacePadding { get; set; }

    /// <summary>Height of the toolbar row (36).</summary>
    public double ToolbarHeight { get; set; }

    /// <summary>Space between the text area and the toolbar (2 above the toolbar).</summary>
    public Thickness ToolbarMargin { get; set; }

    /// <summary>Space between the toolbar's controls (4).</summary>
    public double ToolbarGap { get; set; }

    /// <summary>Space between the toolbar's controls in a tiny composer (2).</summary>
    public double TinyToolbarGap { get; set; }

    /// <summary>The plus button: a 36px circle.</summary>
    public double ButtonDiameter { get; set; }

    /// <summary>The plus button's icon (20, stroke 1.75).</summary>
    public double ButtonGlyphSize { get; set; }

    /// <summary>Stroke of the toolbar icons (1.75).</summary>
    public double GlyphStroke { get; set; }

    /// <summary>The surface border under the pointer: BorderStrong mixed 45% into Border (Arc: L 0.883 in light).</summary>
    public Color SurfaceHoverBorder { get; set; }

    /// <summary>The plus button under the pointer: Foreground at 6%.</summary>
    public Color ButtonHoverBackground { get; set; }

    /// <summary>The plus button pressed or with its menu open: Foreground at 10%.</summary>
    public Color ButtonPressedBackground { get; set; }

    /// <summary>Below this width of the composer the tool toggles keep only their icons (680).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Below this width the model pill drops the effort (560).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Below this width the model pill keeps only its mark and the counter only its ring (400).</summary>
    public double TinyWidth { get; set; }

    /// <summary>The drag cover sits this far inside the composer (6).</summary>
    public Thickness DropCoverMargin { get; set; }

    /// <summary>The drag cover's corner radius (20: the surface's 26 minus the inset).</summary>
    public double DropCoverRadius { get; set; }

    /// <summary>The drag cover's tint: Background at 92%.</summary>
    public Color DropCoverBackground { get; set; }

    /// <summary>The drag cover's upload icon (24).</summary>
    public double DropGlyphSize { get; set; }

    /// <summary>
    /// The "/" and "@" list's left edge relative to the trigger character (−12: Arc puts it 7px from the composer's edge
    /// plus the trigger's x in the text, whose first character sits 19px in).
    /// </summary>
    public double SuggestionShift { get; set; }

    /// <summary>Space between the plus button and its menu (Arc ≈ 7.5).</summary>
    public double MenuGap { get; set; }

    /// <summary>How long a finished upload shows its check before the remove button (0.9s).</summary>
    public TimeSpan UploadDoneHold { get; set; }

    /// <summary>The chat's single row (<see cref="AiComposerLayout.Inline"/>): space inside the frame (7).</summary>
    public Thickness InlineSurfacePadding { get; set; }

    /// <summary>The chat's frame corners (24).</summary>
    public CornerRadius InlineCornerRadius { get; set; }

    /// <summary>Space between the paperclip and the text (4, after the paperclip; none when it is hidden).</summary>
    public Thickness InlineAttachMargin { get; set; }

    /// <summary>Space between the text and the send button (4, before the button).</summary>
    public Thickness InlineSendMargin { get; set; }

    /// <summary>The paperclip: a 34px circle.</summary>
    public double InlineButtonDiameter { get; set; }

    /// <summary>The paperclip's icon (18).</summary>
    public double InlineButtonGlyphSize { get; set; }

    /// <summary>The chat's text area padding (6 above and below, 4 at the sides): one 24px line makes it 36 high.</summary>
    public Thickness InlineEditorPadding { get; set; }

    /// <summary>The chat's text area grows to this many lines (7 × 24 + 12 = 180), then scrolls.</summary>
    public int InlineMaxLines { get; set; }

    /// <summary>The drag cover's corners in the chat's row (18: the frame's 24 minus the 6 inset).</summary>
    public double InlineDropCoverRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        SurfacePadding = new Thickness(10);
        TinySurfacePadding = new Thickness(8);
        ToolbarHeight = 36;
        ToolbarMargin = new Thickness(0, 2, 0, 0);
        ToolbarGap = 4;
        TinyToolbarGap = 2;
        ButtonDiameter = 36;
        ButtonGlyphSize = 20;
        GlyphStroke = 1.75;
        SurfaceHoverBorder = Oklch.MixOklab(n.Border, n.BorderStrong, 0.45);
        ButtonHoverBackground = Oklch.WithAlpha(n.Foreground, 0.06);
        ButtonPressedBackground = Oklch.WithAlpha(n.Foreground, 0.10);
        CompactWidth = 680;
        NarrowWidth = 560;
        TinyWidth = 400;
        DropCoverMargin = new Thickness(6);
        DropCoverRadius = 20;
        DropCoverBackground = Oklch.WithAlpha(n.Background, 0.92);
        DropGlyphSize = 24;
        SuggestionShift = -12;
        MenuGap = 7.5;
        UploadDoneHold = TimeSpan.FromMilliseconds(900);
        InlineSurfacePadding = new Thickness(7);
        InlineCornerRadius = new CornerRadius(24);
        InlineAttachMargin = new Thickness(0, 0, 4, 0);
        InlineSendMargin = new Thickness(4, 0, 0, 0);
        InlineButtonDiameter = 34;
        InlineButtonGlyphSize = 18;
        InlineEditorPadding = new Thickness(4, 6);
        InlineMaxLines = 7;
        InlineDropCoverRadius = 18;
    }
}
