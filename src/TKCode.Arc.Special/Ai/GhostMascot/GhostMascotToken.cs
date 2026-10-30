namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent mixes of <see cref="GhostMascot"/>: below <see cref="CompactSize"/> (56px) the compact
/// drawing takes over; the form shade, the floating ground shadow and the sheet tone's hairline are drawn at these
/// strengths. The sheet, eye and mark colors themselves come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class GhostMascotToken : AbstractControlDesignToken
{
    /// <summary>The size (px) under which the compact drawing is used (56).</summary>
    public double CompactSize { get; set; }

    /// <summary>The opacity of the form shade, a darker tone of the sheet clipped to it (0.16 light, 0.2 dark).</summary>
    public double ShadeOpacity { get; set; }

    /// <summary>The opacity of the ground shadow at rest (0.14 light, 0.32 dark).</summary>
    public double GroundOpacity { get; set; }

    /// <summary>The width in px of the sheet tone's hairline edge (1).</summary>
    public double HairlineWidth { get; set; }

    /// <summary>How far a custom sheet color is mixed toward black for its eyes (0.72).</summary>
    public double EyeDarkMix { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CompactSize = 56;
        ShadeOpacity = isDarkMode ? 0.2 : 0.16;
        GroundOpacity = isDarkMode ? 0.32 : 0.14;
        HairlineWidth = 1;
        EyeDarkMix = 0.72;
    }
}
