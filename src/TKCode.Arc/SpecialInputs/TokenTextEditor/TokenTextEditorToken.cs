using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TokenTextEditorToken : AbstractControlDesignToken
{
    /// <summary>Arc composer textarea: <c>line-height: 24px</c> (16px text); the editor grows in steps of it.</summary>
    public double EditorLineHeight { get; set; }

    /// <summary>Arc composer textarea and mirror: <c>padding: 8px</c>.</summary>
    public Thickness EditorPadding { get; set; }

    /// <summary>Arc <c>letter-spacing: -0.01em</c> of the 16px text.</summary>
    public double EditorLetterSpacing { get; set; }

    /// <summary>Token <c>mark</c>: <c>border-radius: 6px</c>.</summary>
    public double TokenCornerRadius { get; set; }

    /// <summary>Token <c>box-shadow: 0 0 0 2px</c> in the fill color: the box grows 2px without moving any glyph.</summary>
    public double TokenOutset { get; set; }

    /// <summary>Token padding around the glyphs (none in the composer; the outset does the work).</summary>
    public Thickness TokenPadding { get; set; }

    /// <summary>Default token fill: <c>color-mix(in oklab, var(--accent) 12%, transparent)</c>.</summary>
    public double TokenFillOpacity { get; set; }

    /// <summary>Selection: <c>color-mix(in oklab, var(--accent) 24%, transparent)</c>.</summary>
    public double SelectionOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        EditorLineHeight = 24;
        EditorPadding = new Thickness(8);
        EditorLetterSpacing = -0.16;
        TokenCornerRadius = 6;
        TokenOutset = 2;
        TokenPadding = default;
        TokenFillOpacity = 0.12;
        SelectionOpacity = 0.24;
    }
}
