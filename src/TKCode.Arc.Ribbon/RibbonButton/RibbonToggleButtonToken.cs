using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of a <see cref="RibbonToggleButton"/> on top of <see cref="RibbonButtonToken"/>: the outline drawn while it is on.</summary>
[ControlDesignToken]
internal sealed class RibbonToggleButtonToken : AbstractControlDesignToken
{
    /// <summary>Outline of a command that is on (accent colored, drawn over the fill so the size does not change): 1.</summary>
    public Thickness CheckedBorderThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CheckedBorderThickness = new Thickness(1);
    }
}
