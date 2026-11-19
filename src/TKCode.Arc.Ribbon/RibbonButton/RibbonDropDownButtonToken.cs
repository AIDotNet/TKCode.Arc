using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of a <see cref="RibbonDropDownButton"/> on top of <see cref="RibbonButtonToken"/>: the chevron and the menu gap.</summary>
[ControlDesignToken]
internal sealed class RibbonDropDownButtonToken : AbstractControlDesignToken
{
    /// <summary>The chevron that marks a command with a menu: 12.</summary>
    public double ChevronSize { get; set; }

    /// <summary>Space before the chevron of a medium or small command: 2.</summary>
    public Thickness ChevronMargin { get; set; }

    /// <summary>Gap between the command and its menu: 4.</summary>
    public double MenuGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ChevronSize = 12;
        ChevronMargin = new Thickness(2, 0, 0, 0);
        MenuGap = 4;
    }
}
