using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TextShimmerToken : AbstractControlDesignToken
{
    /// <summary>How much of the text color is mixed into muted text for the resting base (Arc: 12%).</summary>
    public double BaseMix { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BaseMix = 0.12;
    }
}
