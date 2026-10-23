namespace TKCode.Arc;

/// <summary>The views of a <see cref="WeekCalendar"/>.</summary>
public enum WeekCalendarView
{
    /// <summary>Seven days, from <see cref="WeekCalendar.FirstDayOfWeek"/>.</summary>
    Week,

    /// <summary>Monday to Friday of the week.</summary>
    WorkWeek,

    /// <summary>One day.</summary>
    Day,

    /// <summary>Three days from the chosen day; the week views of a narrow calendar.</summary>
    ThreeDay,

    /// <summary>The week as a list grouped by day; offered in narrow calendars in place of the work week.</summary>
    Agenda,
}

/// <summary>The palette color of a <see cref="WeekCalendarSource"/>.</summary>
public enum WeekCalendarColor
{
    /// <summary>Blue.</summary>
    Blue,

    /// <summary>Green.</summary>
    Green,

    /// <summary>Violet.</summary>
    Violet,

    /// <summary>Amber.</summary>
    Amber,

    /// <summary>Rose.</summary>
    Rose,

    /// <summary>Teal.</summary>
    Teal,

    /// <summary>Gray; also used for events whose calendar is not listed.</summary>
    Neutral,

    /// <summary><see cref="WeekCalendarSource.CustomColor"/>, mixed with the theme the same way as the preset colors.</summary>
    Custom,
}

/// <summary>What moved a <see cref="WeekCalendar"/> to another range of days.</summary>
public enum WeekCalendarRangeChangeReason
{
    /// <summary>The previous or next button, or <see cref="WeekCalendar.Previous"/> / <see cref="WeekCalendar.Next"/>.</summary>
    Step,

    /// <summary>The Today button, the T key or <see cref="WeekCalendar.GoToToday"/>.</summary>
    Today,

    /// <summary>A day picked in the month in the sidebar.</summary>
    MiniMonth,

    /// <summary>The arrow keys moved past the first or last day.</summary>
    Keyboard,

    /// <summary>The view changed (a day view of the chosen day, the week around it).</summary>
    ViewSwitch,

    /// <summary>A horizontal swipe on a touch screen.</summary>
    Swipe,

    /// <summary><see cref="WeekCalendar.Date"/> or <see cref="WeekCalendar.GoTo"/> from code.</summary>
    Programmatic,

    /// <summary>The calendar became narrow or wide and the days shown changed with the view.</summary>
    Resize,
}
