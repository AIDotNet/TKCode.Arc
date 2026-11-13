using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The interaction constants of editing, measured on Arc's preview (notes B §4): gesture thresholds, the resize handle,
/// auto-scroll, the popover gaps and the swipe rule. Pure constants.
/// </summary>
internal static class WeekCalendarEditMetrics
{
    /// <summary>A press becomes a drag (draw, move, resize) once the pointer is this far from where it went down.</summary>
    public const double DragThreshold = 4;

    /// <summary>A touch held this long without moving starts drawing or moving (Arc ≈ 380ms).</summary>
    public static readonly TimeSpan LongPressDelay = TimeSpan.FromMilliseconds(380);

    /// <summary>The finger may wander this far before a long press gives way to a scroll or a swipe.</summary>
    public const double LongPressTolerance = 8;

    /// <summary>The resize handle: 7px inside the bottom of an event block and 3px below it.</summary>
    public const double ResizeHandleInside = 7;

    public const double ResizeHandleOutside = 3;

    /// <summary>The auto-scroll zone at the top and bottom of the visible hours.</summary>
    public const double AutoScrollZone = 44;

    /// <summary>The fastest auto-scroll step, in pixels per 60 Hz frame (1080 px/s).</summary>
    public const double AutoScrollMaxStep = 18;

    /// <summary>Everything snaps to 15 minutes and lasts at least that long.</summary>
    public const int SnapMinutes = WeekCalendarTimeGrid.SnapMinutes;

    /// <summary>A click, a tap, N and New event draft an hour.</summary>
    public const int DraftMinutes = 60;

    /// <summary>N and New event look for a free hour from 9 AM in half-hour steps.</summary>
    public const int DraftSearchStart = 9 * 60;

    public const int DraftSearchStep = 30;

    /// <summary>Popovers sit 10 beside the event, their top 6 above its top, 8 inside the bottom of the grid.</summary>
    public const double PopoverGap = 10;

    public const double PopoverTopOffset = -6;

    public const double PopoverEdgeMargin = 8;

    /// <summary>Below 640 popovers sit 8 under (or over) the event.</summary>
    public const double CompactPopoverGap = 8;

    /// <summary>The time pill of a drag sits 26 above the block (21.6 high, ≈ 4 clear of it).</summary>
    public const double DragLabelOffset = 26;

    public const double DragLabelHeight = 21.6;

    /// <summary>A swipe changes the days past 20% of the pane's width, or on a flick faster than this (px/s).</summary>
    public const double SwipeCommitFraction = 0.2;

    public const double SwipeFlickSpeed = 500;

    /// <summary>The undo toast sits 16 above the bottom of the frame; it rises 12 and drops 8.</summary>
    public const double ToastBottom = 16;

    public const double ToastRise = 12;

    public const double ToastDrop = 8;

    /// <summary>A draft scrolled into view lands this far below the top of the visible hours (Arc).</summary>
    public const double ScrollIntoViewMargin = 24;
}

/// <summary>Which side of its event a popover took.</summary>
internal enum WeekCalendarPopoverSide
{
    /// <summary>To the right of the event (wide calendars).</summary>
    Right,

    /// <summary>To the left, when the right side has no room.</summary>
    Left,

    /// <summary>Under the event (narrow calendars).</summary>
    Below,

    /// <summary>Over the event, when there is no room under it.</summary>
    Above,
}

/// <summary>Where a popover goes and the corner it grows from (the edge facing its event).</summary>
internal readonly record struct WeekCalendarPopoverPlacement(Point TopLeft, WeekCalendarPopoverSide Side)
{
    /// <summary>The transform origin: the top corner facing the event, or the centre of the edge facing it.</summary>
    public RelativePoint Origin => Side switch
    {
        WeekCalendarPopoverSide.Left => new RelativePoint(1, 0, RelativeUnit.Relative),
        WeekCalendarPopoverSide.Below => new RelativePoint(0.5, 0, RelativeUnit.Relative),
        WeekCalendarPopoverSide.Above => new RelativePoint(0.5, 1, RelativeUnit.Relative),
        _ => new RelativePoint(0, 0, RelativeUnit.Relative),
    };
}

/// <summary>How a touch that has moved is read before a long press fired.</summary>
internal enum WeekCalendarPanAxis
{
    /// <summary>Still within the tolerance.</summary>
    None,

    /// <summary>Mostly sideways: a swipe between days (narrow calendars).</summary>
    Horizontal,

    /// <summary>Mostly up or down: the grid scrolls.</summary>
    Vertical,
}

/// <summary>
/// The rules of editing (notes B §4), measured on Arc and kept pure so they are tested without a window: drawing from an
/// anchor slot, the one-hour draft of a click, moving with the grab offset, resizing, the auto-scroll speed, the free hour
/// N drafts at, popover placement and the swipe rule. Minutes are minutes of the day (0–1440).
/// </summary>
internal static class WeekCalendarEditMath
{
    private const int Step = WeekCalendarEditMetrics.SnapMinutes;

    /// <summary>
    /// The range drawn from a press at <paramref name="anchorMinute"/> to the pointer at <paramref name="pointerMinute"/>:
    /// the pressed 15-minute slot is the anchor, the moving end rounds to 15 minutes. Dragging down starts at the top of the
    /// pressed slot; dragging up ends at its bottom; a short drag either way is the pressed slot (15 minutes). Kept inside the
    /// hours shown.
    /// </summary>
    public static (int Start, int End) DrawRange(double anchorMinute, double pointerMinute, WeekCalendarTimeGrid grid)
    {
        var anchor = WeekCalendarTimeGrid.SnapDown((int)Math.Floor(anchorMinute), Step);
        anchor = Math.Clamp(anchor, grid.StartMinute, grid.EndMinute - Step);
        var current = Math.Clamp(WeekCalendarTimeGrid.SnapNearest(pointerMinute, Step), grid.StartMinute, grid.EndMinute);
        return (Math.Min(anchor, current), Math.Max(anchor + Step, current));
    }

    /// <summary>
    /// A click or tap on empty time: an hour from the half-hour slot under the pointer (the one its hover shows; Arc gave
    /// 1:30–2:30 for clicks at 1:33, 1:41 and 1:48), kept inside the hours shown.
    /// </summary>
    public static (int Start, int End) ClickDraft(double minute, WeekCalendarTimeGrid grid, int length = WeekCalendarEditMetrics.DraftMinutes)
    {
        var start = Math.Clamp(WeekCalendarTimeGrid.SnapDown((int)Math.Floor(minute), WeekCalendarTimeGrid.SlotMinutes), grid.StartMinute,
            grid.EndMinute - Step);
        var end = Math.Min(grid.EndMinute, start + Math.Max(Step, length));
        return (start, end);
    }

    /// <summary>A long press on empty time: the pressed 15-minute slot.</summary>
    public static (int Start, int End) PressedSlot(double minute, WeekCalendarTimeGrid grid) => DrawRange(minute, minute, grid);

    /// <summary>
    /// The new start of an event moved by <paramref name="deltaMinutes"/> (the pointer's travel, which keeps the grab
    /// offset): rounded to 15 minutes and kept so the event stays inside the hours shown.
    /// </summary>
    public static double MoveStart(double originalStart, double duration, double deltaMinutes, WeekCalendarTimeGrid grid)
    {
        var start = (double)WeekCalendarTimeGrid.SnapNearest(originalStart + deltaMinutes, Step);
        var latest = Math.Max(grid.StartMinute, grid.EndMinute - Math.Max(0, duration));
        return Math.Clamp(start, grid.StartMinute, latest);
    }

    /// <summary>The new end of an event resized from its bottom edge: the pointer rounded to 15 minutes, 15 after the start at least.</summary>
    public static double ResizeEnd(double start, double pointerMinute, WeekCalendarTimeGrid grid)
    {
        var end = (double)WeekCalendarTimeGrid.SnapNearest(pointerMinute, Step);
        var earliest = start + Step;
        return Math.Clamp(end, earliest, Math.Max(earliest, grid.EndMinute));
    }

    /// <summary>
    /// The auto-scroll step for a pointer <paramref name="distance"/> px inside the edge (negative: past it), in pixels per
    /// 60 Hz frame: min(18, 20 − 0.4·d) inside the 44px zone, 18 past the edge, 0 outside the zone.
    /// </summary>
    public static double AutoScrollStep(double distance)
    {
        if (double.IsNaN(distance) || distance >= WeekCalendarEditMetrics.AutoScrollZone)
        {
            return 0;
        }
        return Math.Min(WeekCalendarEditMetrics.AutoScrollMaxStep, 20 - 0.4 * Math.Max(0, distance));
    }

    /// <summary>The auto-scroll speed in px/s (the per-frame step at 60 frames a second, applied with the real frame time).</summary>
    public static double AutoScrollSpeed(double distance) => AutoScrollStep(distance) * 60;

    /// <summary>
    /// The start of the next free hour of a day for N and New event: from 9 AM (or the first hour shown) in half-hour steps,
    /// the first start where <paramref name="length"/> minutes overlap no event, jumping past each event that is in the way
    /// (its end rounded up to the half hour). Without one, the first hour shown.
    /// </summary>
    public static int FreeStart(IEnumerable<(double Start, double End)> busy, WeekCalendarTimeGrid grid,
                                int searchFrom = WeekCalendarEditMetrics.DraftSearchStart, int length = WeekCalendarEditMetrics.DraftMinutes,
                                int step = WeekCalendarEditMetrics.DraftSearchStep)
    {
        var events = busy.ToList();
        var t = CeilTo(Math.Max(searchFrom, grid.StartMinute), step);
        while (t + length <= grid.EndMinute)
        {
            var blockedUntil = double.NegativeInfinity;
            foreach (var (start, end) in events)
            {
                if (start < t + length && end > t)
                {
                    blockedUntil = Math.Max(blockedUntil, end);
                }
            }
            if (double.IsNegativeInfinity(blockedUntil))
            {
                return t;
            }
            var next = CeilTo(blockedUntil, step);
            t = next > t ? next : t + step;
        }
        return grid.StartMinute;
    }

    private static int CeilTo(double minutes, int step) => (int)Math.Ceiling(minutes / step - 1e-9) * step;

    /// <summary>
    /// Places a popover of <paramref name="size"/> beside <paramref name="anchor"/> (both in the calendar's coordinates,
    /// whose size is <paramref name="bounds"/>). Wide calendars: to the right, 10 apart, or to the left when the right has
    /// no room; the top 6 above the event's, pushed up to end 8 inside the bottom of the grid (<paramref name="bottom"/>).
    /// Narrow calendars: centred under the event, 8 apart, kept 8 inside the sides, or over it when there is no room under.
    /// Never above the calendar's top.
    /// </summary>
    public static WeekCalendarPopoverPlacement PlacePopover(Rect anchor, Size size, Size bounds, double bottom, bool compact)
    {
        var margin = WeekCalendarEditMetrics.PopoverEdgeMargin;
        var limit = bottom - margin;
        if (!compact)
        {
            var side = WeekCalendarPopoverSide.Right;
            var x = anchor.Right + WeekCalendarEditMetrics.PopoverGap;
            if (x + size.Width > bounds.Width)
            {
                side = WeekCalendarPopoverSide.Left;
                x = Math.Max(0, anchor.Left - WeekCalendarEditMetrics.PopoverGap - size.Width);
            }
            var y = anchor.Top + WeekCalendarEditMetrics.PopoverTopOffset;
            if (y + size.Height > limit)
            {
                y = limit - size.Height;
            }
            return new WeekCalendarPopoverPlacement(new Point(x, Math.Max(0, y)), side);
        }
        var cx = anchor.Center.X - size.Width / 2;
        var maxX = bounds.Width - margin - size.Width;
        cx = maxX < margin ? margin : Math.Clamp(cx, margin, maxX);
        var gap = WeekCalendarEditMetrics.CompactPopoverGap;
        var below = anchor.Bottom + gap;
        if (below + size.Height <= limit)
        {
            return new WeekCalendarPopoverPlacement(new Point(cx, below), WeekCalendarPopoverSide.Below);
        }
        var above = anchor.Top - gap - size.Height;
        if (above >= 0)
        {
            return new WeekCalendarPopoverPlacement(new Point(cx, above), WeekCalendarPopoverSide.Above);
        }
        // Neither fits: under the event, pushed up to the bottom of the grid.
        return new WeekCalendarPopoverPlacement(new Point(cx, Math.Max(0, Math.Min(below, limit - size.Height))),
            WeekCalendarPopoverSide.Below);
    }

    /// <summary>
    /// The days a swipe ends on: +1 (later days; the finger moved left), −1 (earlier) or 0 (spring back). It commits past
    /// 20% of the pane's width, or on a flick faster than 500 px/s in the direction it moved.
    /// </summary>
    public static int SwipeDirection(double travel, double velocity, double paneWidth)
    {
        if (!(paneWidth > 0))
        {
            return 0;
        }
        if (Math.Abs(travel) > WeekCalendarEditMetrics.SwipeCommitFraction * paneWidth)
        {
            return travel < 0 ? 1 : -1;
        }
        if (Math.Abs(velocity) > WeekCalendarEditMetrics.SwipeFlickSpeed && Math.Sign(velocity) == Math.Sign(travel) &&
            Math.Abs(travel) >= WeekCalendarEditMetrics.DragThreshold)
        {
            return velocity < 0 ? 1 : -1;
        }
        return 0;
    }

    /// <summary>How a touch that moved by <paramref name="delta"/> is read before a long press: within 8px nothing yet.</summary>
    public static WeekCalendarPanAxis PanAxis(Vector delta)
    {
        if (Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) <= WeekCalendarEditMetrics.LongPressTolerance)
        {
            return WeekCalendarPanAxis.None;
        }
        return Math.Abs(delta.X) > Math.Abs(delta.Y) ? WeekCalendarPanAxis.Horizontal : WeekCalendarPanAxis.Vertical;
    }

    /// <summary>True once a press has moved far enough to be a drag.</summary>
    public static bool PassedThreshold(Vector delta) =>
        Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) >= WeekCalendarEditMetrics.DragThreshold;
}

/// <summary>
/// The long press of a touch (Arc: ≈ 380ms within 8px): it fires once the finger has stayed put long enough; moving further
/// first cancels it, leaving the touch to scroll or swipe. Time is passed in, so it is tested without a timer.
/// </summary>
internal sealed class WeekCalendarHold
{
    private Point _start;
    private TimeSpan _pressedAt;
    private bool _armed;
    private bool _fired;

    /// <summary>True between a press and its release while the hold has neither fired nor been cancelled.</summary>
    public bool IsPending => _armed && !_fired;

    /// <summary>True once the hold fired (until the next press).</summary>
    public bool HasFired => _fired;

    /// <summary>A finger went down at <paramref name="point"/>.</summary>
    public void Press(Point point, TimeSpan at)
    {
        _start = point;
        _pressedAt = at;
        _armed = true;
        _fired = false;
    }

    /// <summary>The finger moved; returns false (and cancels) once it is more than 8px from where it went down.</summary>
    public bool Move(Point point)
    {
        if (!IsPending)
        {
            return false;
        }
        if (WeekCalendarEditMath.PanAxis(point - _start) != WeekCalendarPanAxis.None)
        {
            _armed = false;
            return false;
        }
        return true;
    }

    /// <summary>True when the hold is due at <paramref name="now"/>.</summary>
    public bool IsDue(TimeSpan now) => IsPending && now - _pressedAt >= WeekCalendarEditMetrics.LongPressDelay;

    /// <summary>Fires the hold if it is due; returns true the one time it fires.</summary>
    public bool TryFire(TimeSpan now)
    {
        if (!IsDue(now))
        {
            return false;
        }
        _fired = true;
        return true;
    }

    /// <summary>The finger lifted or the gesture was taken over.</summary>
    public void Cancel()
    {
        _armed = false;
    }
}
