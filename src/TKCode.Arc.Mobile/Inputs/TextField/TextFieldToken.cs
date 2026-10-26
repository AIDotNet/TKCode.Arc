using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="TextField"/> and <see cref="Searchbar"/>: a 44px field with a 1px border, an 18px icon and clear glyph.</summary>
[ControlDesignToken]
internal sealed class TextFieldToken : AbstractControlDesignToken
{
    /// <summary>Smallest height of the field: 44.</summary>
    public double FieldHeight { get; set; }

    /// <summary>Padding inside the field: 12 at the sides.</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>Border width (transparent until focused): 1.</summary>
    public Thickness BorderWidth { get; set; }

    /// <summary>Size of the icon and of the clear glyph: 18.</summary>
    public double GlyphExtent { get; set; }

    /// <summary>Margin after the icon: 8 on the right.</summary>
    public Thickness IconMargin { get; set; }

    /// <summary>Opacity when disabled: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FieldHeight = 44;
        ContentPadding = new Thickness(12, 0);
        BorderWidth = new Thickness(1);
        GlyphExtent = 18;
        IconMargin = new Thickness(0, 0, 8, 0);
        DisabledOpacity = 0.5;
    }
}
