using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="OrbitMenu"/>: the trigger, the actions and the arc they fan out on. Colors, radii and shadows
/// come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class OrbitMenuToken : AbstractControlDesignToken
{
    /// <summary>Diameter of the round trigger.</summary>
    public double TriggerSize { get; set; }

    /// <summary>Size of the trigger's glyph.</summary>
    public double TriggerGlyphSize { get; set; }

    /// <summary>Diameter of an action.</summary>
    public double ActionSize { get; set; }

    /// <summary>Size of an action's glyph.</summary>
    public double ActionGlyphSize { get; set; }

    /// <summary>Distance from the trigger's center to each action's center when <see cref="OrbitMenu.Radius"/> is not set.</summary>
    public double OrbitRadius { get; set; }

    /// <summary>Margin of the trigger's keyboard focus ring (negative: outside the trigger).</summary>
    public Thickness TriggerFocusRingMargin { get; set; }

    /// <summary>Scale an action starts from while it travels out of the trigger.</summary>
    public double LaunchScale { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TriggerSize = 52;
        TriggerGlyphSize = 22;
        ActionSize = 44;
        ActionGlyphSize = 18;
        OrbitRadius = 88;
        TriggerFocusRingMargin = new Thickness(-4);
        LaunchScale = 0.4;
    }
}
