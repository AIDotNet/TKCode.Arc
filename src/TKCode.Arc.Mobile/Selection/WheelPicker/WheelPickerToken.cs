namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="WheelPicker"/>: row height and selection band corners.</summary>
[ControlDesignToken]
internal sealed class WheelPickerToken : AbstractControlDesignToken
{
    /// <summary>Height of each wheel row in px: 40.</summary>
    public double ItemHeight { get; set; }

    /// <summary>Horizontal margin around the center selection highlight band: 8.</summary>
    public double BandInset { get; set; }

    /// <summary>Opacity while disabled: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        ItemHeight = 40;
        BandInset = 8;
        DisabledOpacity = 0.5;
    }
}
