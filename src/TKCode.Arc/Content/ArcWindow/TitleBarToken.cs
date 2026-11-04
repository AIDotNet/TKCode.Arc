using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TitleBarToken : AbstractControlDesignToken
{
    /// <summary>The bar's height; the window asks the system for a title bar of the same height.</summary>
    public double Height { get; set; }

    public Thickness Padding { get; set; }

    /// <summary>The smallest gap between the left, center and right sections.</summary>
    public double SectionSpacing { get; set; }

    /// <summary>Between the icon, the title and the left content.</summary>
    public double ItemSpacing { get; set; }

    /// <summary>Between the right content and the caption buttons, and between two caption buttons.</summary>
    public double ButtonSpacing { get; set; }

    /// <summary>Width and height of minimize, maximize and close.</summary>
    public double CaptionButtonSize { get; set; }

    /// <summary>The minimize and close glyphs.</summary>
    public double CaptionGlyphSize { get; set; }

    /// <summary>The maximize square is drawn smaller than the other glyphs so it reads at the same weight.</summary>
    public double MaximizeGlyphSize { get; set; }

    /// <summary>The restore glyph (two squares).</summary>
    public double RestoreGlyphSize { get; set; }

    public double IconSize { get; set; }

    public double TitleFontSize { get; set; }

    /// <summary>
    /// The room kept for the macOS traffic lights, after the bar's left padding and before the item spacing: with them the
    /// bar's content starts 82 px from the left edge, clear of the lights.
    /// </summary>
    public double SystemButtonsWidth { get; set; }

    /// <summary>The hairline between the right content and the caption buttons.</summary>
    public double DividerHeight { get; set; }

    /// <summary>The line under the bar.</summary>
    public Thickness BorderThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Height = 40;
        Padding = new Thickness(14, 0, 6, 0);
        SectionSpacing = 12;
        ItemSpacing = 8;
        ButtonSpacing = 4;
        CaptionButtonSize = 30;
        CaptionGlyphSize = 15;
        MaximizeGlyphSize = 13;
        RestoreGlyphSize = 14;
        IconSize = 18;
        TitleFontSize = 13;
        SystemButtonsWidth = 60;
        DividerHeight = 16;
        BorderThickness = new Thickness(0, 0, 0, 1);
    }
}
