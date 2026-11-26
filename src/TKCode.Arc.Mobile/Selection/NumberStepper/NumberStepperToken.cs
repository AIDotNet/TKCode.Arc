using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="NumberStepper"/>: 44px touch buttons, value label width, icon size and disabled opacity.</summary>
[ControlDesignToken]
internal sealed class NumberStepperToken : AbstractControlDesignToken
{
    /// <summary>Minimum width and height of the minus and plus buttons: 44.</summary>
    public double TouchTarget { get; set; }

    /// <summary>Minimum width of the value readout between the buttons: 44.</summary>
    public double ValueMinWidth { get; set; }

    /// <summary>Padding around the value readout: 8 horizontally.</summary>
    public Thickness ValuePadding { get; set; }

    /// <summary>Width and height of the minus and plus glyphs: 16.</summary>
    public double IconExtent { get; set; }

    /// <summary>Opacity of the stepper or a boundary-disabled button: 0.4.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        TouchTarget = 44;
        ValueMinWidth = 44;
        ValuePadding = new Thickness(8, 0);
        IconExtent = 16;
        DisabledOpacity = 0.4;
    }
}
