using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="AiInlineEdit"/>. Colors, radii and fonts come from ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class AiInlineEditToken : AbstractControlDesignToken
{
    /// <summary>The widest the editor grows (680).</summary>
    public double MaxWidth { get; set; }

    /// <summary>Padding of the paragraph (24, 20).</summary>
    public Thickness DocumentPadding { get; set; }

    /// <summary>Padding of the bar under the paragraph (16, 12).</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Line height of the paragraph (26, room for the change buttons).</summary>
    public double LineHeight { get; set; }

    /// <summary>The size of a change's accept and reject buttons (20) and their glyphs (12).</summary>
    public double ChangeButtonSize { get; set; }

    /// <inheritdoc cref="ChangeButtonSize"/>
    public double ChangeGlyphSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxWidth = 680;
        DocumentPadding = new Thickness(24, 20);
        BarPadding = new Thickness(16, 12);
        LineHeight = 26;
        ChangeButtonSize = 20;
        ChangeGlyphSize = 12;
    }
}
