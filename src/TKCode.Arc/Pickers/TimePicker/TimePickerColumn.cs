using TKCode.Arc.Motion;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>The part of the time a <see cref="TimePickerColumn"/> picks.</summary>
internal enum TimePickerColumnKind
{
    Hour,
    Minute,
    Second,
    Meridiem
}

/// <summary>One column of the TimePicker's column layout: a scroll viewer over a drawn list of the part's values.</summary>
internal sealed class TimePickerColumn
{
    public TimePickerColumn(TimePickerColumnKind kind, ScrollViewer scroll, TimePickerList list)
    {
        Kind = kind;
        Scroll = scroll;
        List = list;
    }

    public TimePickerColumnKind Kind { get; }

    public ScrollViewer Scroll { get; }

    public TimePickerList List { get; }

    /// <summary>The spring that glides the column to keep its active row in view (created on first use).</summary>
    public MotionValue<double>? ScrollMotion { get; set; }
}
