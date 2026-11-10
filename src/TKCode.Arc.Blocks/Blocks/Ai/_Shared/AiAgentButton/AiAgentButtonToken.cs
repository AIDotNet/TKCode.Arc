using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="AiAgentButton"/>, the pressable rows and cards of the agent blocks.</summary>
[ControlDesignToken]
internal sealed class AiAgentButtonToken : AbstractControlDesignToken
{
    /// <summary>The keyboard focus ring sits this far outside the face (3).</summary>
    public Thickness RingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RingMargin = new Thickness(-3);
    }
}
