using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class AccordionToken : AbstractControlDesignToken
{
    /// <summary>The list draws a 1px rule above the first row (each row draws its own below), as a filled strip of this thickness.</summary>
    public Thickness ListBorderThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ListBorderThickness = new Thickness(0, 1, 0, 0);
    }
}
