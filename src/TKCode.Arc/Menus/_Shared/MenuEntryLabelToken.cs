using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class MenuEntryLabelToken : AbstractControlDesignToken
{
    /// <summary>Group label inset, aligned with the row text (account rows use an 8px inset).</summary>
    public Thickness LabelInset { get; set; }
    public Thickness AccountLabelInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LabelInset = new Thickness(11, 8, 11, 4);
        AccountLabelInset = new Thickness(8, 6, 8, 4);
    }
}
