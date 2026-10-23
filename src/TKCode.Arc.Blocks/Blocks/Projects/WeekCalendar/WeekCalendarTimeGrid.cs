namespace TKCode.Arc;

/// <summary>
/// Geometry of the time grid (notes §3.4–3.6): the hours shown, the pixels per hour, minutes ↔ y, event boxes and the
/// snapping the editing gestures use. y = 0 is the top of <see cref="StartHour"/>. Pure; no UI.
/// </summary>
internal readonly record struct WeekCalendarTimeGrid
{
    /// <summary>Arc's hour height (52px, the same at every width).</summary>
    public const double DefaultHourHeight = 52;

    /// <summary>The shortest event box (a 15 minute event).</summary>
    public const double MinEventHeight = 18;

    /// <summary>The slot the editing gestures snap to.</summary>
    public const int SnapMinutes = 15;

    /// <summary>The hover slot of an empty grid (half an hour).</summary>
    public const int SlotMinutes = 30;

    /// <summary>Creates the geometry; the hours are clamped to 0–24 with at least one hour shown.</summary>
    public WeekCalendarTimeGrid(int startHour, int endHour, double hourHeight)
    {
        StartHour = Math.Clamp(startHour, 0, 23);
        EndHour = Math.Clamp(endHour, StartHour + 1, 24);
        HourHeight = hourHeight > 0 && double.IsFinite(hourHeight) ? hourHeight : DefaultHourHeight;
    }

    /// <summary>The first hour shown.</summary>
    public int StartHour { get; }

    /// <summary>The hour the grid ends at (exclusive).</summary>
    public int EndHour { get; }

    /// <summary>Pixels per hour.</summary>
    public double HourHeight { get; }

    /// <summary>The first minute of the day shown.</summary>
    public int StartMinute => StartHour * 60;

    /// <summary>The minute of the day the grid ends at.</summary>
    public int EndMinute => EndHour * 60;

    /// <summary>The height of the grid's body.</summary>
    public double BodyHeight => (EndHour - StartHour) * HourHeight;

    /// <summary>The y of a minute of the day.</summary>
    public double YOf(double minuteOfDay) => (minuteOfDay - StartMinute) / 60.0 * HourHeight;

    /// <summary>The minute of the day at <paramref name="y"/>, kept inside the hours shown.</summary>
    public double MinuteAt(double y) => Math.Clamp(StartMinute + y / HourHeight * 60.0, StartMinute, EndMinute);

    /// <summary>The y of an hour line.</summary>
    public double HourY(int hour) => (hour - StartHour) * HourHeight;

    /// <summary>
    /// The box of an event from <paramref name="startMinute"/> to <paramref name="endMinute"/>: one pixel below its start
    /// and two pixels short of its length (so stacked events keep a gap), at least <see cref="MinEventHeight"/>.
    /// </summary>
    public (double Top, double Height) EventBox(double startMinute, double endMinute)
    {
        var top = YOf(startMinute) + 1;
        var height = Math.Max(MinEventHeight, (endMinute - startMinute) / 60.0 * HourHeight - 2);
        return (top, height);
    }

    /// <summary>The half-hour slot under <paramref name="y"/>: its first minute.</summary>
    public int SlotAt(double y) => SnapDown((int)Math.Floor(MinuteAt(y)), SlotMinutes);

    /// <summary>Rounds <paramref name="minutes"/> down to a multiple of <paramref name="step"/>.</summary>
    public static int SnapDown(int minutes, int step = SnapMinutes) => (int)Math.Floor(minutes / (double)step) * step;

    /// <summary>Rounds <paramref name="minutes"/> to the nearest multiple of <paramref name="step"/> (halves round up).</summary>
    public static int SnapNearest(double minutes, int step = SnapMinutes) => (int)Math.Floor(minutes / step + 0.5) * step;

    /// <summary>
    /// The scroll offset that opens the grid at the hour of <paramref name="earliestMinute"/> (the first timed event in
    /// view), 8px above its line; 8 AM without one (Arc).
    /// </summary>
    public double InitialOffset(int? earliestMinute)
    {
        var minute = earliestMinute ?? 8 * 60;
        var hour = Math.Clamp(minute / 60, StartHour, EndHour);
        return Math.Max(0, HourY(hour) - 8);
    }
}
