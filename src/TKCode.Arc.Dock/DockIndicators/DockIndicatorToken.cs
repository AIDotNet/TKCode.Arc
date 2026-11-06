namespace TKCode.Arc;

/// <summary>
/// Geometry of the drag indicators of a <see cref="DockLayout"/> (self-designed, spec section 7): 36px compass buttons
/// 6px apart with 18px glyphs, edge buttons 8px in from the layout's edges, a preview with a 1.5px accent outline over the
/// accent tint at 55%, a drag ghost at 92% that trails the pointer by 12px, and a 2px insertion line in tab strips. Colors,
/// radii and shadows come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class DockIndicatorToken : AbstractControlDesignToken
{
    /// <summary>A compass or edge button: 36.</summary>
    public double ButtonSize { get; set; }

    /// <summary>Space between neighbouring compass buttons: 6.</summary>
    public double ButtonGap { get; set; }

    /// <summary>The buttons' glyphs: 18.</summary>
    public double GlyphSize { get; set; }

    /// <summary>Distance of an edge button from the layout's edge: 8.</summary>
    public double EdgeInset { get; set; }

    /// <summary>Outline of the drop preview: 1.5.</summary>
    public double PreviewStroke { get; set; }

    /// <summary>Opacity of the preview's accent tint: 0.55.</summary>
    public double PreviewFillOpacity { get; set; }

    /// <summary>Opacity of the drag ghost: 0.92.</summary>
    public double GhostOpacity { get; set; }

    /// <summary>How far the ghost sits right of and below the pointer: 12.</summary>
    public double GhostOffset { get; set; }

    /// <summary>Width of the insertion line shown in a tab strip: 2.</summary>
    public double InsertionLineWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ButtonSize = 36;
        ButtonGap = 6;
        GlyphSize = 18;
        EdgeInset = 8;
        PreviewStroke = 1.5;
        PreviewFillOpacity = 0.55;
        GhostOpacity = 0.92;
        GhostOffset = 12;
        InsertionLineWidth = 2;
    }
}
