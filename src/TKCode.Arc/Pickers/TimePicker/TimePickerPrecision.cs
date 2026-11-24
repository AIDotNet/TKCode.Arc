namespace TKCode.Arc;

/// <summary>The smallest part of the time a <see cref="TimePicker"/> shows and lets people choose.</summary>
public enum TimePickerPrecision
{
    /// <summary>Hours and minutes ("9:15 AM", "09:15"); a choice sets the seconds to zero (the default).</summary>
    Minute,

    /// <summary>Hours, minutes and seconds ("9:15:30 AM", "09:15:30"); the drop-down shows a seconds column.</summary>
    Second
}
