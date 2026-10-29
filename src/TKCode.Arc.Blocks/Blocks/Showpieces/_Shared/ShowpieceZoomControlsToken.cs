using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ShowpieceZoomControls"/> (Arc Customer galaxy / Semantic zoom, design reference): a 34px bar
/// with 2px padding and gaps, 1px border, radius 10; 28px buttons with radius 8 and 16px glyphs; a 46px readout (38px
/// compact). Colors, shadow and type come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ShowpieceZoomControlsToken : AbstractControlDesignToken
{
    /// <summary>Inner padding of the bar (2px).</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Gap between the bar's items (2px).</summary>
    public double ItemGap { get; set; }

    /// <summary>Bar corner radius (10px).</summary>
    public CornerRadius BarRadius { get; set; }

    /// <summary>Square button size (28px).</summary>
    public double ButtonExtent { get; set; }

    /// <summary>Button corner radius (8px).</summary>
    public CornerRadius ButtonRadius { get; set; }

    /// <summary>Glyph size (16px; stroke 1.75 in the 24px viewBox).</summary>
    public double ButtonGlyphSize { get; set; }

    /// <summary>Readout width (46px).</summary>
    public double ReadoutWidth { get; set; }

    /// <summary>Readout width in compact containers (38px).</summary>
    public double CompactReadoutWidth { get; set; }

    /// <summary>Opacity of a disabled Fit button (0.35).</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Keyboard focus ring: 2px, 2px outside the button.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>Focus ring radius (button radius + 4).</summary>
    public CornerRadius FocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarPadding = new Thickness(2);
        ItemGap = 2;
        BarRadius = new CornerRadius(10);
        ButtonExtent = 28;
        ButtonRadius = new CornerRadius(8);
        ButtonGlyphSize = 16;
        ReadoutWidth = 46;
        CompactReadoutWidth = 38;
        DisabledOpacity = 0.35;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(12);
    }
}
