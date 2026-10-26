using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TimePickerToken : AbstractControlDesignToken
{
    /// <summary>Gap between the clock icon, the value and the chevron (Arc: <c>gap: 9px</c>).</summary>
    public double AffixGap { get; set; }

    /// <summary>The list sits 6px under the trigger with a 5px inset.</summary>
    public double PopupGap { get; set; }
    public Thickness ListInset { get; set; }

    /// <summary>Options: 36px rows, 12px inline inset, corners of the panel radius minus the 5px inset, 6px chosen dot.</summary>
    public double OptionHeight { get; set; }
    public double OptionInset { get; set; }
    public double OptionRadius { get; set; }
    public double DotDiameter { get; set; }

    /// <summary>Line height of the value copy (14px × 1.4).</summary>
    public double ValueLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        AffixGap = 9;
        PopupGap = 6;
        ListInset = new Thickness(5);
        OptionHeight = 36;
        OptionInset = 12;
        OptionRadius = 21;
        DotDiameter = 6;
        ValueLineHeight = 14 * 1.4;
    }
}
