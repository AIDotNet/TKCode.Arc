using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class CarouselToken : AbstractControlDesignToken
{
    /// <summary>Gap between the slides and the control row (<c>--space-4</c>).</summary>
    public double RowGap { get; set; }

    /// <summary>Gap between the controls in the row (<c>--space-2</c>; 4px when narrow).</summary>
    public double ControlsGap { get; set; }

    public double CompactControlsGap { get; set; }

    /// <summary>Viewport focus ring: 2px outline 4px outside the slides (<c>--focus-outline-offset: 4px</c>).</summary>
    public Thickness ViewportRingMargin { get; set; }

    /// <summary>Panel radius 26 + 6.</summary>
    public CornerRadius ViewportRingRadius { get; set; }

    /// <summary>The play / pause glyph (15px, filled).</summary>
    public double PlayGlyphSize { get; set; }

    /// <summary>The play / pause glyph slot (16px).</summary>
    public double PlayGlyphSlot { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RowGap = 16;
        ControlsGap = 8;
        CompactControlsGap = 4;
        ViewportRingMargin = new Thickness(-6);
        ViewportRingRadius = new CornerRadius(32);
        PlayGlyphSize = 15;
        PlayGlyphSlot = 16;
    }
}
