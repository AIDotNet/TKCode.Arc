using System.Globalization;
using Avalonia;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary>The pure rules of week calendar editing (notes B §4): drawing, drafts, moves, resizes, auto-scroll, free hours,
/// popover placement, swipes, the touch hold, and the texts they produce.</summary>
public class WeekCalendarEditingEngineTests
{
    private static readonly WeekCalendarTimeGrid Day = new(0, 24, 52);

    private static double At(int hour, int minute) => hour * 60 + minute;

    // Drawing ------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(13, 7, 14, 40, 13, 0, 14, 45)]
    [InlineData(13, 10, 14, 34, 13, 0, 14, 30)]
    [InlineData(13, 16, 14, 20, 13, 15, 14, 15)]
    [InlineData(14, 20, 13, 10, 13, 15, 14, 30)]
    [InlineData(14, 6, 13, 57, 14, 0, 14, 15)]
    public void Drawing_Anchors_The_Pressed_Slot_And_Rounds_The_Moving_End(int ah, int am, int ph, int pm, int sh, int sm, int eh, int em)
    {
        var (start, end) = WeekCalendarEditMath.DrawRange(At(ah, am), At(ph, pm), Day);
        start.ShouldBe(sh * 60 + sm);
        end.ShouldBe(eh * 60 + em);
    }

    [Fact]
    public void Drawing_Lasts_15_Minutes_At_Least_And_Stays_In_The_Hours()
    {
        WeekCalendarEditMath.DrawRange(At(10, 5), At(10, 6), Day).ShouldBe((600, 615));
        WeekCalendarEditMath.DrawRange(At(10, 5), At(9, 59), Day).ShouldBe((600, 615));
        // 8px inside the bottom while auto-scrolling (≈ 11:51 PM) ends at 11:45 PM; past the bottom, midnight.
        WeekCalendarEditMath.DrawRange(At(23, 0), 24 * 60 - 8 / 52.0 * 60, Day).ShouldBe((1380, 1425));
        WeekCalendarEditMath.DrawRange(At(23, 0), 24 * 60, Day).ShouldBe((1380, 1440));
        WeekCalendarEditMath.DrawRange(At(0, 20), -30, Day).ShouldBe((0, 30));
        var work = new WeekCalendarTimeGrid(7, 20, 52);
        WeekCalendarEditMath.DrawRange(At(19, 50), At(21, 0), work).ShouldBe((1185, 1200));
    }

    [Theory]
    [InlineData(13, 33)]
    [InlineData(13, 41)]
    [InlineData(13, 48)]
    public void A_Click_Drafts_An_Hour_From_Its_Half_Hour_Slot(int hour, int minute)
    {
        WeekCalendarEditMath.ClickDraft(At(hour, minute), Day).ShouldBe((13 * 60 + 30, 14 * 60 + 30));
    }

    [Fact]
    public void A_Click_Near_The_End_Of_The_Hours_Is_Cut_There()
    {
        WeekCalendarEditMath.ClickDraft(At(23, 40), Day).ShouldBe((1410, 1440));
        WeekCalendarEditMath.ClickDraft(At(23, 59), Day).ShouldBe((1410, 1440));
        WeekCalendarEditMath.PressedSlot(At(8, 52), Day).ShouldBe((525, 540));
    }

    // Moving and resizing ------------------------------------------------------------------------------------------------

    [Fact]
    public void A_Move_Keeps_The_Grab_Offset_Rounds_To_15_Minutes_And_Stays_In_The_Day()
    {
        // 9:30 dragged 69 minutes later starts at 10:45 (round15(570 + 69)).
        WeekCalendarEditMath.MoveStart(570, 90, 69.2, Day).ShouldBe(645);
        // Less than half a slot does nothing.
        WeekCalendarEditMath.MoveStart(570, 90, 7, Day).ShouldBe(570);
        WeekCalendarEditMath.MoveStart(570, 90, 8, Day).ShouldBe(585);
        // Clamped: the top of the day, and the bottom (a 30 minute event ends at midnight at the latest).
        WeekCalendarEditMath.MoveStart(60, 60, -500, Day).ShouldBe(0);
        WeekCalendarEditMath.MoveStart(1300, 30, 500, Day).ShouldBe(1410);
        var work = new WeekCalendarTimeGrid(7, 20, 52);
        WeekCalendarEditMath.MoveStart(480, 60, -200, work).ShouldBe(420);
    }

    [Fact]
    public void A_Resize_Rounds_The_End_And_Keeps_15_Minutes()
    {
        WeekCalendarEditMath.ResizeEnd(570, 767.3, Day).ShouldBe(765);
        WeekCalendarEditMath.ResizeEnd(570, 400, Day).ShouldBe(585);
        WeekCalendarEditMath.ResizeEnd(570, 2000, Day).ShouldBe(1440);
        WeekCalendarEditMath.ResizeEnd(1430, 1440, Day).ShouldBe(1445);
    }

    // Auto-scroll --------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(60, 0)]
    [InlineData(44, 0)]
    [InlineData(40, 4)]
    [InlineData(30, 8)]
    [InlineData(20, 12)]
    [InlineData(10, 16)]
    [InlineData(5, 18)]
    [InlineData(0, 18)]
    [InlineData(-30, 18)]
    public void Auto_Scroll_Speeds_Up_Linearly_Toward_The_Edge(double distance, double step)
    {
        WeekCalendarEditMath.AutoScrollStep(distance).ShouldBe(step, 0.001);
        WeekCalendarEditMath.AutoScrollSpeed(distance).ShouldBe(step * 60, 0.01);
    }

    // Free hours ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void N_Finds_The_Next_Free_Hour_From_9_AM_In_Half_Hours()
    {
        // A packed morning and early afternoon: the first hour clear of every event starts at 3:30 PM.
        var monday = new (double, double)[]
        {
            (At(9, 0), At(9, 15)), (At(9, 30), At(11, 30)), (At(12, 0), At(13, 0)), (At(13, 30), At(14, 0)),
            (At(14, 0), At(15, 0)), (At(14, 0), At(14, 30)), (At(14, 30), At(15, 15)),
        };
        WeekCalendarEditMath.FreeStart(monday, Day).ShouldBe(15 * 60 + 30);
        // A trip until 6:05 PM: 6:30 PM.
        var wednesday = new (double, double)[]
        {
            (At(8, 0), At(8, 30)), (At(9, 0), At(9, 15)), (At(9, 30), At(10, 30)), (At(10, 30), At(11, 30)), (At(11, 0), At(12, 0)),
            (At(12, 30), At(18, 5)),
        };
        WeekCalendarEditMath.FreeStart(wednesday, Day).ShouldBe(18 * 60 + 30);
        // Only half an hour free at 9: the next whole hour.
        WeekCalendarEditMath.FreeStart(new (double, double)[] { (At(9, 0), At(9, 30)), (At(9, 30), At(11, 0)) }, Day).ShouldBe(11 * 60);
        WeekCalendarEditMath.FreeStart(Array.Empty<(double, double)>(), Day).ShouldBe(9 * 60);
        // Nothing free: the first hour shown.
        var work = new WeekCalendarTimeGrid(7, 20, 52);
        WeekCalendarEditMath.FreeStart(new (double, double)[] { (At(9, 0), At(19, 45)) }, work).ShouldBe(7 * 60);
        // The hours start after 9: the search starts with them.
        WeekCalendarEditMath.FreeStart(Array.Empty<(double, double)>(), new WeekCalendarTimeGrid(10, 18, 52)).ShouldBe(10 * 60);
    }

    // Popover placement --------------------------------------------------------------------------------------------------

    [Fact]
    public void Wide_Popovers_Sit_Beside_Their_Event_And_Grow_From_It()
    {
        var bounds = new Size(1440, 900);
        var size = new Size(320, 234);
        var monday = new Rect(340, 500, 152, 98);
        var right = WeekCalendarEditMath.PlacePopover(monday, size, bounds, 800, compact: false);
        right.Side.ShouldBe(WeekCalendarPopoverSide.Right);
        right.TopLeft.ShouldBe(new Point(502, 494));
        right.Origin.ShouldBe(new RelativePoint(0, 0, RelativeUnit.Relative));
        var saturday = new Rect(1150, 500, 152, 98);
        var left = WeekCalendarEditMath.PlacePopover(saturday, size, bounds, 800, compact: false);
        left.Side.ShouldBe(WeekCalendarPopoverSide.Left);
        left.TopLeft.X.ShouldBe(1150 - 10 - 320);
        left.Origin.ShouldBe(new RelativePoint(1, 0, RelativeUnit.Relative));
        // Near the bottom: pushed up to end 8 inside the grid; never above the calendar's top.
        WeekCalendarEditMath.PlacePopover(new Rect(340, 700, 152, 50), size, bounds, 800, false).TopLeft.Y.ShouldBe(800 - 8 - 234);
        WeekCalendarEditMath.PlacePopover(new Rect(340, -40, 152, 50), size, bounds, 800, false).TopLeft.Y.ShouldBe(0);
    }

    [Fact]
    public void Narrow_Popovers_Sit_Under_Or_Over_Their_Event_Inside_8px()
    {
        var bounds = new Size(390, 844);
        var size = new Size(320, 234);
        var rightDraft = new Rect(276, 300, 99, 50);
        var below = WeekCalendarEditMath.PlacePopover(rightDraft, size, bounds, 760, compact: true);
        below.Side.ShouldBe(WeekCalendarPopoverSide.Below);
        below.TopLeft.ShouldBe(new Point(62, 358));
        below.Origin.ShouldBe(new RelativePoint(0.5, 0, RelativeUnit.Relative));
        var leftDraft = new Rect(78, 300, 99, 50);
        WeekCalendarEditMath.PlacePopover(leftDraft, size, bounds, 760, true).TopLeft.X.ShouldBe(8);
        var low = new Rect(276, 600, 99, 50);
        var above = WeekCalendarEditMath.PlacePopover(low, size, bounds, 760, true);
        above.Side.ShouldBe(WeekCalendarPopoverSide.Above);
        above.TopLeft.Y.ShouldBe(600 - 8 - 234);
        above.Origin.ShouldBe(new RelativePoint(0.5, 1, RelativeUnit.Relative));
    }

    // Swipes and the touch hold ------------------------------------------------------------------------------------------

    [Fact]
    public void A_Swipe_Commits_Past_A_Fifth_Of_The_Pane_Or_On_A_Flick()
    {
        WeekCalendarEditMath.SwipeDirection(-55, 0, 296).ShouldBe(0);
        WeekCalendarEditMath.SwipeDirection(-60, 0, 296).ShouldBe(1);
        WeekCalendarEditMath.SwipeDirection(60, 0, 296).ShouldBe(-1);
        WeekCalendarEditMath.SwipeDirection(-30, -800, 296).ShouldBe(1);
        WeekCalendarEditMath.SwipeDirection(30, 800, 296).ShouldBe(-1);
        // A flick against the travel, or barely moving, springs back.
        WeekCalendarEditMath.SwipeDirection(-30, 800, 296).ShouldBe(0);
        WeekCalendarEditMath.SwipeDirection(-2, -900, 296).ShouldBe(0);
        WeekCalendarEditMath.SwipeDirection(-100, 0, 0).ShouldBe(0);
    }

    [Fact]
    public void A_Touch_Reads_As_Nothing_Then_Sideways_Or_Up_And_Down()
    {
        WeekCalendarEditMath.PanAxis(new Vector(5, 5)).ShouldBe(WeekCalendarPanAxis.None);
        WeekCalendarEditMath.PanAxis(new Vector(12, 3)).ShouldBe(WeekCalendarPanAxis.Horizontal);
        WeekCalendarEditMath.PanAxis(new Vector(-3, -12)).ShouldBe(WeekCalendarPanAxis.Vertical);
        WeekCalendarEditMath.PassedThreshold(new Vector(3.9, 0)).ShouldBeFalse();
        WeekCalendarEditMath.PassedThreshold(new Vector(3, 3)).ShouldBeTrue();
    }

    [Fact]
    public void A_Long_Press_Fires_After_380ms_Within_8px_Once()
    {
        var hold = new WeekCalendarHold();
        var start = TimeSpan.FromSeconds(10);
        hold.Press(new Point(100, 100), start);
        hold.IsPending.ShouldBeTrue();
        hold.Move(new Point(105, 104)).ShouldBeTrue();
        hold.IsDue(start + TimeSpan.FromMilliseconds(379)).ShouldBeFalse();
        hold.IsDue(start + TimeSpan.FromMilliseconds(380)).ShouldBeTrue();
        hold.TryFire(start + TimeSpan.FromMilliseconds(384)).ShouldBeTrue();
        hold.HasFired.ShouldBeTrue();
        hold.TryFire(start + TimeSpan.FromMilliseconds(500)).ShouldBeFalse();

        // Moving 10px first cancels it: the touch scrolls or swipes instead.
        hold.Press(new Point(100, 100), start);
        hold.Move(new Point(100, 110)).ShouldBeFalse();
        hold.TryFire(start + TimeSpan.FromSeconds(1)).ShouldBeFalse();
        hold.Press(new Point(100, 100), start);
        hold.Cancel();
        hold.IsDue(start + TimeSpan.FromSeconds(1)).ShouldBeFalse();
    }

    // Texts and time ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Lengths_People_And_Short_Days_Read_Naturally()
    {
        var labels = new WeekCalendarLabels();
        labels.Duration(105).ShouldBe("1 hr 45 min");
        labels.Duration(60).ShouldBe("1 hr");
        labels.Duration(45).ShouldBe("45 min");
        labels.Attendees("Iris Vale", 0).ShouldBe("Iris Vale");
        labels.Attendees("Iris Vale", 1).ShouldBe("Iris Vale and 1 other");
        labels.Attendees("Iris Vale", 4).ShouldBe("Iris Vale and 4 others");
        var day = new DateOnly(2026, 10, 24);
        WeekCalendarTime.ShortDayName(day, CultureInfo.GetCultureInfo("en-US")).ShouldBe("Sat, Oct 24");
        WeekCalendarTime.ShortDayName(day, CultureInfo.GetCultureInfo("zh-CN")).ShouldBe("10月24日 周六");
        labels.MovedAnnouncement("Kiln check", "Sat, Oct 24", "8:45 AM").ShouldBe("Kiln check moved to Sat, Oct 24, 8:45 AM");
        labels.UndoneAnnouncement("Kiln check deleted").ShouldBe("Reverted: Kiln check deleted");
    }

    [Fact]
    public void Wall_Times_Become_Instants_In_The_Calendars_Zone()
    {
        var berlin = TimeZoneInfo.CreateCustomTimeZone("test/berlin", TimeSpan.FromHours(1), "Berlin", "Berlin", "Berlin summer",
            new[]
            {
                TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2000, 1, 1), new DateTime(2099, 12, 31), TimeSpan.FromHours(1),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday)),
            });
        var summer = WeekCalendarTime.FromWall(new DateTime(2026, 7, 1, 9, 30, 0), berlin);
        summer.Offset.ShouldBe(TimeSpan.FromHours(2));
        summer.UtcDateTime.ShouldBe(new DateTime(2026, 7, 1, 7, 30, 0));
        // Written with the stored event's offset (UTC stays UTC).
        WeekCalendarTime.FromWall(new DateTime(2026, 7, 1, 9, 30, 0), berlin, TimeSpan.Zero).Offset.ShouldBe(TimeSpan.Zero);
        // 2:30 AM does not exist on the spring-forward night: it moves forward by the gap.
        var gap = WeekCalendarTime.FromWall(new DateTime(2026, 3, 29, 2, 30, 0), berlin);
        WeekCalendarTime.ToWall(gap, berlin).ShouldBe(new DateTime(2026, 3, 29, 3, 30, 0));
    }
}
