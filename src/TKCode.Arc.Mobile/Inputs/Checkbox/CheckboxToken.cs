using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Checkbox"/>: a 22px box with 6px corners, a 44px row, a 16px check.</summary>
[ControlDesignToken]
internal sealed class CheckboxToken : AbstractControlDesignToken
{
    /// <summary>Smallest height of the row: 44.</summary>
    public double TouchTarget { get; set; }

    /// <summary>Width and height of the box: 22.</summary>
    public double BoxSize { get; set; }

    /// <summary>Corner radius of the box: 6.</summary>
    public CornerRadius BoxRadius { get; set; }

    /// <summary>Border width of the box: 1.5.</summary>
    public Thickness BoxBorderWidth { get; set; }

    /// <summary>Size of the check and the dash: 16.</summary>
    public double CheckExtent { get; set; }

    /// <summary>Margin between the box and the label: 12 on the left of the label.</summary>
    public Thickness LabelMargin { get; set; }

    /// <summary>Opacity while a finger is on it: 0.6.</summary>
    public double PressedOpacity { get; set; }

    /// <summary>Opacity when disabled: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TouchTarget = 44;
        BoxSize = 22;
        BoxRadius = new CornerRadius(6);
        BoxBorderWidth = new Thickness(1.5);
        CheckExtent = 16;
        LabelMargin = new Thickness(12, 0, 0, 0);
        PressedOpacity = 0.6;
        DisabledOpacity = 0.5;
    }
}
