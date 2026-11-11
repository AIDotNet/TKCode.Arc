namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class WindowCaptionButtonsToken : AbstractControlDesignToken
{
    /// <summary>Width and height of minimize, maximize and close.</summary>
    public double ButtonSize { get; set; }

    /// <summary>Between two buttons.</summary>
    public double ButtonSpacing { get; set; }

    /// <summary>The minimize and close glyphs.</summary>
    public double GlyphSize { get; set; }

    /// <summary>The maximize square is drawn smaller than the other glyphs so it reads at the same weight.</summary>
    public double MaximizeGlyphSize { get; set; }

    /// <summary>The restore glyph (two squares).</summary>
    public double RestoreGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ButtonSize = 30;
        ButtonSpacing = 4;
        GlyphSize = 15;
        MaximizeGlyphSize = 13;
        RestoreGlyphSize = 14;
    }
}
