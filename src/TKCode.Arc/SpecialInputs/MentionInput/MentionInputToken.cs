using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class MentionInputToken : AbstractControlDesignToken
{
    /// <summary>Arc <c>.textarea</c> / <c>.backdrop</c>: <c>line-height: 24px</c>; the field grows in steps of it.</summary>
    public double EditorLineHeight { get; set; }

    /// <summary>Arc <c>padding: 11px 14px</c> shared by the editor and its mirror.</summary>
    public Thickness EditorPadding { get; set; }

    /// <summary>Arc <c>letter-spacing: var(--tracking-body)</c> (-0.01em of the 16px text).</summary>
    public double EditorLetterSpacing { get; set; }

    /// <summary>Arc <c>.root[data-disabled] { opacity: .55 }</c>.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Channel token: <c>color-mix(in oklab, var(--foreground) 7%, transparent)</c>.</summary>
    public Color ChannelTokenFill { get; set; }

    /// <summary>Suggestion highlight: <c>color-mix(in oklab, var(--foreground) 6.5%, transparent)</c>.</summary>
    public Color SuggestionHighlightFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        EditorLineHeight = 24;
        EditorPadding = new Thickness(14, 11);
        EditorLetterSpacing = -0.16;
        DisabledOpacity = 0.55;
        ChannelTokenFill = Oklch.WithAlpha(n.Foreground, 0.07);
        SuggestionHighlightFill = Oklch.WithAlpha(n.Foreground, 0.065);
    }
}
