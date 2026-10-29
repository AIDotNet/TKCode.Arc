
namespace TKCode.Arc;

/// <summary>Option geometry shared by every list in the Selects category.</summary>
[ControlDesignToken]
internal sealed class SelectItemToken : AbstractControlDesignToken
{
    /// <summary>Check glyph box (lucide-react size 16).</summary>
    public double CheckGlyphSize { get; set; }

    /// <summary>Lucide stroke width used by every select glyph (1.75).</summary>
    public double GlyphStrokeWidth { get; set; }

    /// <summary>Icon box before an option label (MorphSelect, 20px).</summary>
    public double OptionIconSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CheckGlyphSize = 16;
        GlyphStrokeWidth = 1.75;
        OptionIconSize = 20;
    }
}
