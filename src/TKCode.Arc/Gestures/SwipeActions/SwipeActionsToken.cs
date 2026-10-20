using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SwipeActionsToken : AbstractControlDesignToken
{
    /// <summary>The list is pulled down 1px, so the last row's separator tucks under the frame.</summary>
    public Thickness ListInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ListInset = new Thickness(0, 0, 0, -1);
    }
}
