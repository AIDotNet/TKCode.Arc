using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="Radio"/>: a 22px circle with a 10px dot, a 44px row.</summary>
[ControlDesignToken]
internal sealed class RadioToken : AbstractControlDesignToken
{
    /// <summary>Smallest height and width of the row: 44.</summary>
    public double TouchTarget { get; set; }

    /// <summary>Width and height of the circle: 22.</summary>
    public double CircleSize { get; set; }

    /// <summary>Border width of the circle: 1.5.</summary>
    public Thickness CircleBorderWidth { get; set; }

    /// <summary>Width and height of the dot: 10.</summary>
    public double DotSize { get; set; }

    /// <summary>Margin between the circle and the label: 12 on the left of the label.</summary>
    public Thickness LabelMargin { get; set; }

    /// <summary>Opacity while a finger is on it: 0.6.</summary>
    public double PressedOpacity { get; set; }

    /// <summary>Opacity when disabled: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TouchTarget = 44;
        CircleSize = 22;
        CircleBorderWidth = new Thickness(1.5);
        DotSize = 10;
        LabelMargin = new Thickness(12, 0, 0, 0);
        PressedOpacity = 0.6;
        DisabledOpacity = 0.5;
    }
}
