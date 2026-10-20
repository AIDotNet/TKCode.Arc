using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of the Activity reaction chips (CommentThread and ChatThread share Arc's 26px reaction pill). Colors come
/// from ArcDesign tokens in the theme; the accent mixes of a chosen chip are drawn as accent layers at these opacities.
/// </summary>
[ControlDesignToken]
internal sealed class ActivityReactionChipToken : AbstractControlDesignToken
{
    /// <summary>Chip height (26px).</summary>
    public double ChipHeight { get; set; }

    /// <summary>Inner padding: <c>0 9px 0 7px</c>.</summary>
    public Thickness ChipPadding { get; set; }

    /// <summary>Gap between the emoji and the count (5px).</summary>
    public double ChipContentGap { get; set; }

    /// <summary>Gap between chips in a row and between wrapped rows (4px).</summary>
    public double ChipGap { get; set; }

    /// <summary>Comment emoji size (inherits the chip's 12px text).</summary>
    public double CommentEmojiSize { get; set; }

    /// <summary>Chat emoji size (14px).</summary>
    public double ChatEmojiSize { get; set; }

    /// <summary>Comment chosen border: <c>color-mix(accent 45%, border)</c>, an accent ring at 45% over the border.</summary>
    public double CommentRingOpacity { get; set; }

    /// <summary>Chat chosen border: <c>color-mix(accent 28%, transparent)</c>.</summary>
    public double ChatRingOpacity { get; set; }

    /// <summary>Chat chosen fill: <c>color-mix(accent 8%, surface)</c>.</summary>
    public double ChatFillOpacity { get; set; }

    /// <summary>
    /// Emoji face: the platform color emoji fonts first, so "❤️" is the color glyph rather than the body font's text
    /// heart (browsers prefer the emoji presentation; Skia takes the first font that has the code point).
    /// </summary>
    public FontFamily EmojiFontFamily { get; set; } = FontFamily.Default;

    /// <summary>Focus ring outset (-4px) around the pill.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ChipHeight = 26;
        ChipPadding = new Thickness(7, 0, 9, 0);
        ChipContentGap = 5;
        ChipGap = 4;
        CommentEmojiSize = 12;
        ChatEmojiSize = 14;
        CommentRingOpacity = 0.45;
        ChatRingOpacity = 0.28;
        ChatFillOpacity = 0.08;
        EmojiFontFamily = new FontFamily("Apple Color Emoji, Segoe UI Emoji, Noto Color Emoji, $Default");
        FocusRingMargin = new Thickness(-4);
    }
}
