namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="DateWheelPicker"/>: maximum width of the trailing readout and sheet bar height.</summary>
[ControlDesignToken]
internal sealed class DateWheelPickerToken : AbstractControlDesignToken
{
    /// <summary>Maximum width of the formatted date/time value at the row's end: 180.</summary>
    public double ValueMaxWidth { get; set; }

    /// <summary>Height of the Cancel / Title / Done bar at the top of the picker sheet: 44.</summary>
    public double HeaderHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        ValueMaxWidth = 180;
        HeaderHeight = 44;
    }
}
