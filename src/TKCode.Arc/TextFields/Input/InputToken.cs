using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class InputToken : AbstractControlDesignToken
{
    /// <summary>Arc input: <c>.input:disabled { opacity: .5 }</c> (Textarea uses 0.6).</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DisabledOpacity = 0.5;
    }
}
