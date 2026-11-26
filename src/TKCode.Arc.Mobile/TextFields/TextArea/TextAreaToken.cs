using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="TextArea"/>: content padding, border width, icon size and disabled opacity.</summary>
[ControlDesignToken(Identity = "MobileTextArea")]
internal sealed class TextAreaToken : AbstractControlDesignToken
{
    /// <summary>Padding around the text inside the filled box: 12 horizontally, 10 vertically.</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>Border thickness (transparent at rest, accent while focused): 1.</summary>
    public Thickness BorderWidth { get; set; }

    /// <summary>Width and height of the optional clear button glyph: 18.</summary>
    public double GlyphExtent { get; set; }

    /// <summary>Opacity while disabled: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        ContentPadding = new Thickness(12, 10);
        BorderWidth = new Thickness(1);
        GlyphExtent = 18;
        DisabledOpacity = 0.5;
    }
}
