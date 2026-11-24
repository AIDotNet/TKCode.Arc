namespace TKCode.Arc;

/// <summary>How a <see cref="TimePicker"/> lays out its drop-down.</summary>
public enum TimePickerDropDownLayout
{
    /// <summary>
    /// <see cref="List"/> at <see cref="TimePickerPrecision.Minute"/> precision, <see cref="Columns"/> at
    /// <see cref="TimePickerPrecision.Second"/> precision (the default).
    /// </summary>
    Auto,

    /// <summary>One scrolling list of every time on the <see cref="TimePicker.MinuteStep"/> grid (1440 / step rows).</summary>
    List,

    /// <summary>
    /// One column per part: hours, minutes (every <see cref="TimePicker.MinuteStep"/>), seconds (every
    /// <see cref="TimePicker.SecondStep"/>, at second precision) and AM / PM on a 12-hour clock. A choice in a column
    /// applies that part at once and keeps the drop-down open.
    /// </summary>
    Columns
}
