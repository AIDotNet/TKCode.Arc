using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="WorkflowZoomControls"/>, the same as the library's other zoom bars: a 34px pill with 2px padding
/// and gaps, radius 10; 28px buttons with radius 8 and 16px glyphs; a 46px readout. Colors, shadow and type come from
/// the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class WorkflowZoomControlsToken : AbstractControlDesignToken
{
    /// <summary>Inner padding of the bar: 2.</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Gap between the bar's buttons: 2.</summary>
    public double ItemGap { get; set; }

    /// <summary>The bar's corners: 10.</summary>
    public CornerRadius BarRadius { get; set; }

    /// <summary>A square button: 28.</summary>
    public double ButtonExtent { get; set; }

    /// <summary>A button's corners: 8.</summary>
    public CornerRadius ButtonRadius { get; set; }

    /// <summary>The glyphs: 16, 1.75 stroke in the 24 unit box.</summary>
    public double ButtonGlyphSize { get; set; }

    /// <summary>The readout's width: 46.</summary>
    public double ReadoutWidth { get; set; }

    /// <summary>A button that cannot act: 0.35 opacity.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Keyboard focus ring: 2px, 2px outside the button, radius 12.</summary>
    public Thickness FocusRingMargin { get; set; }

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
        DisabledOpacity = 0.35;
        FocusRingMargin = new Thickness(-4);
        FocusRingRadius = new CornerRadius(12);
    }
}
