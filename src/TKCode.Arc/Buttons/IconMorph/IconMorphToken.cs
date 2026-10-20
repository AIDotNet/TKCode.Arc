using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class IconMorphToken : AbstractControlDesignToken
{
    /// <summary>Rendered size of the icon (Arc default 24px).</summary>
    public double GlyphExtent { get; set; }

    /// <summary>Stroke width on the 24-unit grid (Arc default 1.75).</summary>
    public double GlyphStrokeWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GlyphExtent = 24;
        GlyphStrokeWidth = 1.75;
    }
}
