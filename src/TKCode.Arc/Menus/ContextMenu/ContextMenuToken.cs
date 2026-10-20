using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ContextMenuToken : AbstractControlDesignToken
{
    /// <summary>Click and keyboard opens place the menu 12px in from the target's left edge and 8px below it.</summary>
    public double AnchorInset { get; set; }
    public double AnchorGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        AnchorInset = 12;
        AnchorGap = 8;
    }
}
