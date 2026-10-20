using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class PopoverToken : AbstractControlDesignToken
{
    /// <summary>Gap between the trigger and the panel (Arc sideOffset 6).</summary>
    public double SideOffset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SideOffset = 6;
    }
}
