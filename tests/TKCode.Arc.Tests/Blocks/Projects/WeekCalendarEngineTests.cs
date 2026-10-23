using System.Globalization;
using Avalonia.Media;
using TKCode.Arc.Gallery.Demos;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary>The week calendar's pure engines: ranges, time grid, overlap columns, all-day lanes, time zones, texts, palette.</summary>
public class WeekCalendarEngineTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Zh = CultureInfo.GetCultureInfo("zh-CN");
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    // Ranges, steps, cursor ---------------------------------------------------------------------------------------------

    [Fact]
    public void Views_Show_Their_Days()
    {
        WeekCalendarRange.DaysOf(WeekCalendarView.Week).ShouldBe(7);
        WeekCalendarRange.DaysOf(WeekCalendarView.WorkWeek).ShouldBe(5);
        WeekCalendarRange.DaysOf(WeekCalendarView.Day).ShouldBe(1);
        WeekCalendarRange.DaysOf(WeekCalendarView.ThreeDay).ShouldBe(3);
        WeekCalendarRange.DaysOf(WeekCalendarView.Agenda).ShouldBe(7);
        WeekCalendarRange.StepOf(WeekCalendarView.Week).ShouldBe(7);
        WeekCalendarRange.StepOf(WeekCalendarView.WorkWeek).ShouldBe(7);
        WeekCalendarRange.StepOf(WeekCalendarView.Agenda).ShouldBe(7);
        WeekCalendarRange.StepOf(WeekCalendarView.Day).ShouldBe(1);
        WeekCalendarRange.StepOf(WeekCalendarView.ThreeDay).ShouldBe(3);
    }

    [Theory]
    [InlineData(WeekCalendarView.Week, true, WeekCalendarView.ThreeDay)]
    [InlineData(WeekCalendarView.WorkWeek, true, WeekCalendarView.ThreeDay)]
    [InlineData(WeekCalendarView.Day, true, WeekCalendarView.Day)]
    [InlineData(WeekCalendarView.ThreeDay, true, WeekCalendarView.ThreeDay)]
    [InlineData(WeekCalendarView.Agenda, true, WeekCalendarView.Agenda)]
    [InlineData(WeekCalendarView.Week, false, WeekCalendarView.Week)]
    [InlineData(WeekCalendarView.WorkWeek, false, WeekCalendarView.WorkWeek)]
    [InlineData(WeekCalendarView.Day, false, WeekCalendarView.Day)]
    [InlineData(WeekCalendarView.ThreeDay, false, WeekCalendarView.Week)]
    [InlineData(WeekCalendarView.Agenda, false, WeekCalendarView.Week)]
    public void Narrow_Calendars_Map_The_Views(WeekCalendarView view, bool compact, WeekCalendarView shown) =>
        WeekCalendarRange.Effective(view, compact).ShouldBe(shown);

    [Fact]
    public void The_View_Switch_Offers_Three_Views_Per_Width()
    {
        WeekCalendarRange.Offered(false).ShouldBe([WeekCalendarView.Week, WeekCalendarView.WorkWeek, WeekCalendarView.Day]);
        WeekCalendarRange.Offered(true).ShouldBe([WeekCalendarView.ThreeDay, WeekCalendarView.Day, WeekCalendarView.Agenda]);
    }

    [Fact]
    public void Ranges_Follow_The_View_And_The_First_Day_Of_The_Week()
    {
        var thursday = D(2026, 9, 24);
        WeekCalendarRange.For(WeekCalendarView.Week, thursday, DayOfWeek.Monday).ShouldBe(new WeekCalendarDays(D(2026, 9, 21), 7));
        WeekCalendarRange.For(WeekCalendarView.Agenda, thursday, DayOfWeek.Monday).ShouldBe(new WeekCalendarDays(D(2026, 9, 21), 7));
        WeekCalendarRange.For(WeekCalendarView.WorkWeek, thursday, DayOfWeek.Monday).ShouldBe(new WeekCalendarDays(D(2026, 9, 21), 5));
        WeekCalendarRange.For(WeekCalendarView.Day, thursday, DayOfWeek.Monday).ShouldBe(new WeekCalendarDays(thursday, 1));
        WeekCalendarRange.For(WeekCalendarView.ThreeDay, thursday, DayOfWeek.Monday).ShouldBe(new WeekCalendarDays(thursday, 3));
        WeekCalendarRange.For(WeekCalendarView.Week, thursday, DayOfWeek.Sunday).Start.ShouldBe(D(2026, 9, 20));
        WeekCalendarRange.For(WeekCalendarView.Week, D(2026, 9, 27), DayOfWeek.Monday).Start.ShouldBe(D(2026, 9, 21));
        WeekCalendarRange.For(WeekCalendarView.Week, D(2026, 9, 27), DayOfWeek.Sunday).Start.ShouldBe(D(2026, 9, 27));
        new WeekCalendarDays(D(2026, 9, 21), 7).End.ShouldBe(D(2026, 9, 27));
        new WeekCalendarDays(D(2026, 9, 21), 7).ColumnOf(D(2026, 9, 24)).ShouldBe(3);
    }

    [Fact]
    public void Work_Weeks_Are_Monday_To_Friday_Whatever_The_First_Day()
    {
        // A Sunday-first week holding Sunday the 27th: Monday the 28th to Friday the 2nd.
        WeekCalendarRange.For(WeekCalendarView.WorkWeek, D(2026, 9, 27), DayOfWeek.Sunday).ShouldBe(new WeekCalendarDays(D(2026, 9, 28), 5));
        WeekCalendarRange.For(WeekCalendarView.WorkWeek, D(2026, 9, 26), DayOfWeek.Sunday).Start.ShouldBe(D(2026, 9, 21));
        WeekCalendarRange.For(WeekCalendarView.WorkWeek, D(2026, 9, 26), DayOfWeek.Saturday).Start.ShouldBe(D(2026, 9, 28));
        // Mid-week starts fall back to the Monday on or before the day.
        WeekCalendarRange.For(WeekCalendarView.WorkWeek, D(2026, 9, 24), DayOfWeek.Wednesday).Start.ShouldBe(D(2026, 9, 21));
        WeekCalendarRange.For(WeekCalendarView.WorkWeek, D(2026, 12, 31), DayOfWeek.Monday).ShouldBe(new WeekCalendarDays(D(2026, 12, 28), 5));
    }

    [Fact]
    public void Steps_Move_The_Range_And_The_Cursor_Together()
    {
        var week = new WeekCalendarDays(D(2026, 9, 21), 7);
        WeekCalendarRange.Step(WeekCalendarView.Week, week, D(2026, 9, 21), 1).ShouldBe((new WeekCalendarDays(D(2026, 9, 28), 7), D(2026, 9, 28)));
        WeekCalendarRange.Step(WeekCalendarView.Week, week, D(2026, 9, 24), -1).ShouldBe((new WeekCalendarDays(D(2026, 9, 14), 7), D(2026, 9, 17)));
        var work = new WeekCalendarDays(D(2026, 9, 21), 5);
        WeekCalendarRange.Step(WeekCalendarView.WorkWeek, work, D(2026, 9, 25), 1).ShouldBe((new WeekCalendarDays(D(2026, 9, 28), 5), D(2026, 10, 2)));
        // The day view does not skip weekends.
        var friday = new WeekCalendarDays(D(2026, 9, 25), 1);
        WeekCalendarRange.Step(WeekCalendarView.Day, friday, D(2026, 9, 25), 1).ShouldBe((new WeekCalendarDays(D(2026, 9, 26), 1), D(2026, 9, 26)));
        var three = new WeekCalendarDays(D(2026, 9, 24), 3);
        WeekCalendarRange.Step(WeekCalendarView.ThreeDay, three, D(2026, 9, 25), 1).ShouldBe((new WeekCalendarDays(D(2026, 9, 27), 3), D(2026, 9, 28)));
        WeekCalendarRange.Step(WeekCalendarView.ThreeDay, three, D(2026, 9, 24), -1).ShouldBe((new WeekCalendarDays(D(2026, 9, 21), 3), D(2026, 9, 21)));
        WeekCalendarRange.Step(WeekCalendarView.Agenda, week, D(2026, 9, 21), 1).Range.Start.ShouldBe(D(2026, 9, 28));
    }

    [Fact]
    public void The_Cursor_Moves_Inside_And_Brings_The_Next_Range_Past_The_Edge()
    {
        var week = new WeekCalendarDays(D(2026, 9, 21), 7);
        WeekCalendarRange.MoveCursor(WeekCalendarView.Week, week, D(2026, 9, 23), 1).ShouldBe((week, D(2026, 9, 24), 0));
        WeekCalendarRange.MoveCursor(WeekCalendarView.Week, week, D(2026, 9, 27), 1).ShouldBe((new WeekCalendarDays(D(2026, 9, 28), 7), D(2026, 9, 28), 1));
        WeekCalendarRange.MoveCursor(WeekCalendarView.Week, week, D(2026, 9, 21), -1).ShouldBe((new WeekCalendarDays(D(2026, 9, 14), 7), D(2026, 9, 20), -1));
        var work = new WeekCalendarDays(D(2026, 9, 21), 5);
        WeekCalendarRange.MoveCursor(WeekCalendarView.WorkWeek, work, D(2026, 9, 25), 1).ShouldBe((new WeekCalendarDays(D(2026, 9, 28), 5), D(2026, 9, 28), 1));
        WeekCalendarRange.MoveCursor(WeekCalendarView.WorkWeek, work, D(2026, 9, 21), -1).ShouldBe((new WeekCalendarDays(D(2026, 9, 14), 5), D(2026, 9, 18), -1));
        var three = new WeekCalendarDays(D(2026, 9, 24), 3);
        WeekCalendarRange.MoveCursor(WeekCalendarView.ThreeDay, three, D(2026, 9, 26), 1).ShouldBe((new WeekCalendarDays(D(2026, 9, 27), 3), D(2026, 9, 27), 1));
        var day = new WeekCalendarDays(D(2026, 9, 24), 1);
        WeekCalendarRange.MoveCursor(WeekCalendarView.Day, day, D(2026, 9, 24), -1).ShouldBe((new WeekCalendarDays(D(2026, 9, 23), 1), D(2026, 9, 23), -1));
    }

    [Fact]
    public void Settling_Keeps_A_Range_That_Holds_The_Day()
    {
        var three = new WeekCalendarDays(D(2026, 9, 24), 3);
        WeekCalendarRange.Settle(WeekCalendarView.ThreeDay, three, D(2026, 9, 26), DayOfWeek.Monday).ShouldBe(three);
        WeekCalendarRange.Settle(WeekCalendarView.ThreeDay, three, D(2026, 9, 28), DayOfWeek.Monday).ShouldBe(new WeekCalendarDays(D(2026, 9, 28), 3));
        var week = new WeekCalendarDays(D(2026, 9, 21), 7);
        WeekCalendarRange.Settle(WeekCalendarView.Week, week, D(2026, 9, 25), DayOfWeek.Monday).ShouldBe(week);
        WeekCalendarRange.Settle(WeekCalendarView.Week, week, D(2026, 10, 14), DayOfWeek.Monday).ShouldBe(new WeekCalendarDays(D(2026, 10, 12), 7));
        WeekCalendarRange.Settle(WeekCalendarView.Week, null, D(2026, 9, 25), DayOfWeek.Monday).ShouldBe(week);
        // A view that changed length starts over.
        WeekCalendarRange.Settle(WeekCalendarView.WorkWeek, week, D(2026, 9, 25), DayOfWeek.Monday).ShouldBe(new WeekCalendarDays(D(2026, 9, 21), 5));
        WeekCalendarRange.Clamp(new WeekCalendarDays(D(2026, 9, 21), 5), D(2026, 9, 26)).ShouldBe(D(2026, 9, 25));
        WeekCalendarRange.Clamp(new WeekCalendarDays(D(2026, 9, 21), 5), D(2026, 9, 20)).ShouldBe(D(2026, 9, 21));
    }

    [Fact]
    public void Today_Is_Disabled_While_Today_Is_Shown()
    {
        var week = new WeekCalendarDays(D(2026, 10, 19), 7);
        WeekCalendarRange.IsTodayShown(WeekCalendarView.Week, week, D(2026, 10, 22), DayOfWeek.Monday).ShouldBeTrue();
        WeekCalendarRange.IsTodayShown(WeekCalendarView.Week, week, D(2026, 10, 26), DayOfWeek.Monday).ShouldBeFalse();
        var work = new WeekCalendarDays(D(2026, 10, 19), 5);
        // On a Saturday the work week of this week is already "today's".
        WeekCalendarRange.IsTodayShown(WeekCalendarView.WorkWeek, work, D(2026, 10, 24), DayOfWeek.Monday).ShouldBeTrue();
        WeekCalendarRange.IsTodayShown(WeekCalendarView.WorkWeek, work, D(2026, 10, 27), DayOfWeek.Monday).ShouldBeFalse();
        WeekCalendarRange.IsTodayShown(WeekCalendarView.ThreeDay, new WeekCalendarDays(D(2026, 10, 23), 3), D(2026, 10, 22), DayOfWeek.Monday).ShouldBeFalse();
        WeekCalendarRange.Direction(D(2026, 10, 19), D(2026, 10, 26)).ShouldBe(1);
        WeekCalendarRange.Direction(D(2026, 10, 19), D(2026, 9, 1)).ShouldBe(-1);
        WeekCalendarRange.Direction(D(2026, 10, 19), D(2026, 10, 19)).ShouldBe(0);
    }

    // Time grid ------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(15, 18)]
    [InlineData(30, 24)]
    [InlineData(45, 37)]
    [InlineData(60, 50)]
    [InlineData(90, 76)]
    [InlineData(120, 102)]
    [InlineData(150, 128)]
    [InlineData(210, 180)]
    [InlineData(335, 288.33)]
    public void Event_Boxes_Match_Arc(int minutes, double height)
    {
        var grid = new WeekCalendarTimeGrid(0, 24, 52);
        var (top, h) = grid.EventBox(9 * 60, 9 * 60 + minutes);
        top.ShouldBe(9 * 52 + 1, 0.001);
        h.ShouldBe(height, 0.01);
    }

    [Fact]
    public void The_Grid_Maps_Minutes_And_Pixels()
    {
        var grid = new WeekCalendarTimeGrid(0, 24, 52);
        grid.BodyHeight.ShouldBe(1248);
        grid.YOf(13 * 60 + 30).ShouldBe(702);
        grid.MinuteAt(702).ShouldBe(13 * 60 + 30, 1e-9);
        grid.MinuteAt(-20).ShouldBe(0);
        grid.MinuteAt(5000).ShouldBe(24 * 60);
        // Arc's hover slot: y 725 is in the 1:30 PM half hour.
        grid.SlotAt(725).ShouldBe(13 * 60 + 30);
        var hours = new WeekCalendarTimeGrid(7, 20, 52);
        hours.BodyHeight.ShouldBe(13 * 52);
        hours.YOf(8 * 60).ShouldBe(52);
        hours.HourY(7).ShouldBe(0);
        hours.MinuteAt(0).ShouldBe(7 * 60);
        new WeekCalendarTimeGrid(30, 2, -1).ShouldBe(new WeekCalendarTimeGrid(23, 24, 52));
        WeekCalendarTimeGrid.SnapDown(13 * 60 + 14).ShouldBe(13 * 60);
        WeekCalendarTimeGrid.SnapDown(13 * 60 + 14, 30).ShouldBe(13 * 60);
        WeekCalendarTimeGrid.SnapNearest(13 * 60 + 7.4).ShouldBe(13 * 60);
        WeekCalendarTimeGrid.SnapNearest(13 * 60 + 7.5).ShouldBe(13 * 60 + 15);
    }

    [Fact]
    public void The_Grid_Opens_At_The_First_Events_Hour()
    {
        var grid = new WeekCalendarTimeGrid(0, 24, 52);
        grid.InitialOffset(8 * 60).ShouldBe(408);
        grid.InitialOffset(null).ShouldBe(408);
        grid.InitialOffset(9 * 60 + 30).ShouldBe(9 * 52 - 8);
        grid.InitialOffset(0).ShouldBe(0);
        new WeekCalendarTimeGrid(7, 20, 52).InitialOffset(8 * 60).ShouldBe(44);
        new WeekCalendarTimeGrid(7, 20, 52).InitialOffset(6 * 60).ShouldBe(0);
    }

    // Overlap columns (notes §4.2, every case) ------------------------------------------------------------------------------

    private static (double, double) M(double fromHours, double toHours) => (fromHours * 60, toHours * 60);

    [Fact]
    public void Three_Overlapping_Events_Take_A_Third_Each_And_A_Freed_Column_Is_Reused()
    {
        // A 13:30–14:15, B 14:00–15:00, C 14:00–14:30, D 14:30–15:15.
        var slots = WeekCalendarOverlapLayout.Layout([M(13.5, 14.25), M(14, 15), M(14, 14.5), M(14.5, 15.25)]);
        slots[0].ShouldBe(new WeekCalendarColumnSlot(0, 3, 1));
        slots[1].ShouldBe(new WeekCalendarColumnSlot(1, 3, 1));
        slots[2].ShouldBe(new WeekCalendarColumnSlot(2, 3, 1));
        slots[3].ShouldBe(new WeekCalendarColumnSlot(0, 3, 1));
        slots[3].Width.ShouldBe(1.0 / 3, 1e-9);
    }

    [Fact]
    public void Equal_Starts_Put_The_Longer_First_And_A_Touching_Event_Starts_A_New_Cluster()
    {
        // B shortened to 14:00–14:15: C (30 min) sorts before B; D only touches C's end and stands alone.
        var slots = WeekCalendarOverlapLayout.Layout([M(13.5, 14.25), M(14, 14.25), M(14, 14.5), M(14.5, 15.25)]);
        slots[0].ShouldBe(new WeekCalendarColumnSlot(0, 3, 1));
        slots[2].ShouldBe(new WeekCalendarColumnSlot(1, 3, 1));
        slots[1].ShouldBe(new WeekCalendarColumnSlot(2, 3, 1));
        slots[3].ShouldBe(new WeekCalendarColumnSlot(0, 1, 1));
        slots[3].Width.ShouldBe(1);
    }

    [Fact]
    public void An_Event_Widens_Over_Columns_That_Stay_Free()
    {
        // A 13:30–14:15, B 14:00–14:15, C 14:00–14:30, D 14:15–15:00, E 14:30–15:30.
        var slots = WeekCalendarOverlapLayout.Layout([M(13.5, 14.25), M(14, 14.25), M(14, 14.5), M(14.25, 15), M(14.5, 15.5)]);
        slots[3].ShouldBe(new WeekCalendarColumnSlot(0, 3, 1));
        slots[4].ShouldBe(new WeekCalendarColumnSlot(1, 3, 2));
        slots[4].Left.ShouldBe(1.0 / 3, 1e-9);
        slots[4].Width.ShouldBe(2.0 / 3, 1e-9);
    }

    [Fact]
    public void Two_Overlapping_Events_Share_Halves_And_Back_To_Back_Events_Stay_Full()
    {
        var pair = WeekCalendarOverlapLayout.Layout([M(14, 15), M(14, 14.5)]);
        pair[0].ShouldBe(new WeekCalendarColumnSlot(0, 2, 1));
        pair[1].ShouldBe(new WeekCalendarColumnSlot(1, 2, 1));
        var touching = WeekCalendarOverlapLayout.Layout([M(9.5, 10.5), M(10.5, 11.5)]);
        touching[0].ShouldBe(new WeekCalendarColumnSlot(0, 1, 1));
        touching[1].ShouldBe(new WeekCalendarColumnSlot(0, 1, 1));
        WeekCalendarOverlapLayout.Layout([]).ShouldBeEmpty();
        // Input order does not matter: the result follows it.
        var reversed = WeekCalendarOverlapLayout.Layout([M(14, 14.5), M(14, 15)]);
        reversed[1].ShouldBe(new WeekCalendarColumnSlot(0, 2, 1));
        reversed[0].ShouldBe(new WeekCalendarColumnSlot(1, 2, 1));
    }

    [Fact]
    public void A_Long_Event_Beside_Short_Ones_Keeps_Its_Column_And_The_Short_Ones_Widen_After_It()
    {
        // A 9–12 long; B 9–10; C 10–11 reuses B's column; D 11.5–12.5 overlaps A only.
        var slots = WeekCalendarOverlapLayout.Layout([M(9, 12), M(9, 10), M(10, 11), M(11.5, 12.5)]);
        slots[0].ShouldBe(new WeekCalendarColumnSlot(0, 2, 1));
        slots[1].ShouldBe(new WeekCalendarColumnSlot(1, 2, 1));
        slots[2].ShouldBe(new WeekCalendarColumnSlot(1, 2, 1));
        slots[3].ShouldBe(new WeekCalendarColumnSlot(1, 2, 1));
    }

    // All-day lanes ------------------------------------------------------------------------------------------------------

    [Fact]
    public void All_Day_Events_Pack_Into_Lanes_Longest_First()
    {
        var start = D(2026, 9, 21);
        // Arc's sample: a 3-day event from Wednesday, a 2-day and a 1-day from Thursday.
        var lanes = WeekCalendarAllDayLayout.Layout(
            [(D(2026, 9, 24), D(2026, 9, 25)), (D(2026, 9, 23), D(2026, 9, 26)), (D(2026, 9, 24), D(2026, 9, 26))], start, 7);
        lanes.LaneCount.ShouldBe(3);
        lanes.Placements[1].ShouldBe(new WeekCalendarLanePlacement(0, 2, 3));
        lanes.Placements[2].ShouldBe(new WeekCalendarLanePlacement(1, 3, 2));
        lanes.Placements[0].ShouldBe(new WeekCalendarLanePlacement(2, 3, 1));
        lanes.RowHeight(3, false).ShouldBe(80);
        lanes.Overflows(3).ShouldBeFalse();
        WeekCalendarAllDayLayout.BarTop(2).ShouldBe(52);
    }

    [Fact]
    public void All_Day_Events_Are_Clipped_To_The_Range_And_Free_Lanes_Are_Reused()
    {
        var start = D(2026, 9, 21);
        var lanes = WeekCalendarAllDayLayout.Layout(
            [(D(2026, 9, 18), D(2026, 9, 23)), (D(2026, 9, 26), D(2026, 10, 2)), (D(2026, 9, 23), D(2026, 9, 24)), (D(2026, 9, 1), D(2026, 9, 2))],
            start, 7);
        lanes.Placements[0].ShouldBe(new WeekCalendarLanePlacement(0, 0, 2));
        lanes.Placements[1].ShouldBe(new WeekCalendarLanePlacement(0, 5, 2));
        lanes.Placements[2].ShouldBe(new WeekCalendarLanePlacement(0, 2, 1));
        lanes.Placements[3].IsShown.ShouldBeFalse();
        lanes.LaneCount.ShouldBe(1);
        lanes.RowHeight(3, false).ShouldBe(32);
        WeekCalendarAllDayLayout.Layout([], start, 7).RowHeight(3, false).ShouldBe(32);
    }

    [Fact]
    public void A_Full_Row_Collapses_Into_More_Chips_Until_Expanded()
    {
        var start = D(2026, 9, 21);
        var wednesday = D(2026, 9, 23);
        var five = Enumerable.Range(0, 5).Select(_ => (wednesday, wednesday.AddDays(1))).ToList();
        five.Add((D(2026, 9, 22), D(2026, 9, 25)));
        var lanes = WeekCalendarAllDayLayout.Layout(five, start, 7);
        lanes.LaneCount.ShouldBe(6);
        lanes.Overflows(3).ShouldBeTrue();
        lanes.VisibleLanes(3, false).ShouldBe(3);
        lanes.VisibleLanes(3, true).ShouldBe(6);
        lanes.RowHeight(3, false).ShouldBe(80);
        lanes.RowHeight(3, true).ShouldBe(8 + 6 * 24);
        var hidden = lanes.HiddenPerColumn(3, false);
        hidden[2].ShouldBe(4);
        hidden[1].ShouldBe(0);
        hidden[3].ShouldBe(0);
        lanes.HiddenPerColumn(3, true).ShouldAllBe(n => n == 0);
        Enumerable.Range(0, 6).Count(i => lanes.IsBarVisible(i, 3, false)).ShouldBe(2);
        Enumerable.Range(0, 6).Count(i => lanes.IsBarVisible(i, 3, true)).ShouldBe(6);
        // No limit never collapses.
        lanes.Overflows(0).ShouldBeFalse();
    }

    // Time zones and texts -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(8, 0, "GMT+8")]
    [InlineData(5, 30, "GMT+5:30")]
    [InlineData(5, 45, "GMT+5:45")]
    [InlineData(0, 0, "GMT")]
    [InlineData(-3, -30, "GMT-3:30")]
    [InlineData(-5, 0, "GMT-5")]
    [InlineData(14, 0, "GMT+14")]
    public void Offsets_Read_Like_Short_Gmt_Labels(int hours, int minutes, string label) =>
        WeekCalendarTime.OffsetLabel(new TimeSpan(hours, minutes, 0)).ShouldBe(label);

    [Fact]
    public void Instants_Turn_Into_Wall_Clock_Times_Of_The_Zone()
    {
        var kolkata = TimeZoneInfo.CreateCustomTimeZone("test/kolkata", new TimeSpan(5, 30, 0), "Kolkata", "Kolkata");
        var newYork = TimeZoneInfo.CreateCustomTimeZone("test/ny", TimeSpan.FromHours(-4), "New York", "New York");
        var instant = new DateTimeOffset(2026, 10, 22, 2, 0, 0, TimeSpan.Zero);
        WeekCalendarTime.ToWall(instant, kolkata).ShouldBe(new DateTime(2026, 10, 22, 7, 30, 0));
        WeekCalendarTime.ToWall(instant, newYork).ShouldBe(new DateTime(2026, 10, 21, 22, 0, 0));
        WeekCalendarTime.OffsetLabel(WeekCalendarTime.OffsetOn(kolkata, D(2026, 10, 22))).ShouldBe("GMT+5:30");
        WeekCalendarTime.MinuteOfDay(new DateTime(2026, 10, 22, 13, 45, 0)).ShouldBe(13 * 60 + 45);
    }

    [Fact]
    public void English_Range_Titles_Merge_The_Shared_Month_And_Year()
    {
        WeekCalendarTime.RangeTitle(D(2026, 9, 21), D(2026, 9, 27), En).ShouldBe("Sep 21 to 27, 2026");
        WeekCalendarTime.RangeTitle(D(2026, 9, 28), D(2026, 10, 4), En).ShouldBe("Sep 28 to Oct 4, 2026");
        WeekCalendarTime.RangeTitle(D(2026, 12, 28), D(2027, 1, 3), En).ShouldBe("Dec 28, 2026 to Jan 3, 2027");
        WeekCalendarTime.RangeTitle(D(2026, 12, 28), D(2027, 1, 1), En).ShouldBe("Dec 28, 2026 to Jan 1, 2027");
        WeekCalendarTime.RangeTitle(D(2026, 9, 24), D(2026, 9, 26), En).ShouldBe("Sep 24 to 26, 2026");
        WeekCalendarTime.RangeTitle(D(2026, 11, 5), D(2026, 11, 5), En).ShouldBe("Thursday, November 5");
        WeekCalendarTime.DayTitle(D(2027, 1, 2), En).ShouldBe("Saturday, January 2");
    }

    [Fact]
    public void Chinese_Range_Titles_Use_Year_Month_Day()
    {
        WeekCalendarTime.RangeTitle(D(2026, 9, 21), D(2026, 9, 27), Zh).ShouldBe("2026年9月21日至27日");
        WeekCalendarTime.RangeTitle(D(2026, 9, 28), D(2026, 10, 4), Zh).ShouldBe("2026年9月28日至10月4日");
        WeekCalendarTime.RangeTitle(D(2026, 12, 28), D(2027, 1, 3), Zh).ShouldBe("2026年12月28日至2027年1月3日");
        WeekCalendarTime.DayTitle(D(2026, 11, 5), Zh).ShouldBe("11月5日 星期四");
        WeekCalendarTime.MonthTitle(2026, 9, Zh).ShouldBe("2026年9月");
        WeekCalendarTime.ShortDate(D(2026, 9, 21), Zh).ShouldBe("9月21日");
        WeekCalendarTime.WeekdayLetter(DayOfWeek.Monday, Zh).ShouldBe("一");
    }

    [Theory]
    [InlineData(9.5, 11.5, "9:30 – 11:30 AM")]
    [InlineData(12, 13, "12 – 1 PM")]
    [InlineData(10, 12, "10 AM – 12 PM")]
    [InlineData(9.5, 12, "9:30 AM – 12 PM")]
    [InlineData(11, 12.5, "11 AM – 12:30 PM")]
    [InlineData(12.5, 18 + 5 / 60.0, "12:30 – 6:05 PM")]
    [InlineData(22, 24, "10 PM – 12 AM")]
    public void English_Time_Ranges_Say_Am_Or_Pm_Once_When_Shared(double from, double to, string text) =>
        WeekCalendarTime.TimeRange(from * 60, to * 60, En).ShouldBe(text);

    [Fact]
    public void Times_Read_Short_Full_And_As_Hour_Labels()
    {
        WeekCalendarTime.ShortTime(9 * 60, En).ShouldBe("9 AM");
        WeekCalendarTime.ShortTime(13 * 60 + 30, En).ShouldBe("1:30 PM");
        WeekCalendarTime.ShortTime(12 * 60, En).ShouldBe("12 PM");
        WeekCalendarTime.ShortTime(0, En).ShouldBe("12 AM");
        WeekCalendarTime.FullTime(9 * 60, En).ShouldBe("9:00 AM");
        WeekCalendarTime.FullTime(22 * 60 + 2, En).ShouldBe("10:02 PM");
        WeekCalendarTime.HourLabel(1, En).ShouldBe("1 AM");
        WeekCalendarTime.HourLabel(12, En).ShouldBe("12 PM");
        WeekCalendarTime.HourLabel(23, En).ShouldBe("11 PM");
        WeekCalendarTime.ShortTime(13 * 60 + 30, Zh).ShouldBe("13:30");
        WeekCalendarTime.TimeRange(9.5 * 60, 11.5 * 60, Zh).ShouldBe("9:30 – 11:30");
        WeekCalendarTime.HourLabel(13, Zh).ShouldBe("13:00");
        WeekCalendarTime.WeekdayLetter(DayOfWeek.Thursday, En).ShouldBe("T");
        WeekCalendarTime.ShortWeekday(DayOfWeek.Monday, En).ShouldBe("Mon");
        WeekCalendarTime.MonthTitle(2026, 9, En).ShouldBe("September 2026");
        WeekCalendarTime.ShortDate(D(2026, 9, 21), En).ShouldBe("Sep 21");
        WeekCalendarTime.DayName(D(2026, 9, 21), En).ShouldBe("Monday, September 21");
    }

    // Palette (notes §3.8) --------------------------------------------------------------------------------------------------

    private static void Near(Color actual, string expected, int tolerance = 3)
    {
        var e = Color.Parse(expected);
        var off = Math.Max(Math.Abs(actual.R - e.R), Math.Max(Math.Abs(actual.G - e.G), Math.Abs(actual.B - e.B)));
        off.ShouldBeLessThanOrEqualTo(tolerance, $"{actual} vs {expected}");
    }

    [Theory]
    [InlineData(WeekCalendarColor.Blue, "#2c76d3", "#6ca7f5")]
    [InlineData(WeekCalendarColor.Green, "#15915c", "#5eca91")]
    [InlineData(WeekCalendarColor.Violet, "#7658cb", "#a793f4")]
    [InlineData(WeekCalendarColor.Amber, "#d48300", "#eeb154")]
    [InlineData(WeekCalendarColor.Rose, "#d3456c", "#f4839a")]
    [InlineData(WeekCalendarColor.Teal, "#0d9298", "#56c4ca")]
    [InlineData(WeekCalendarColor.Neutral, "#7a7a7a", "#9e9e9e")]
    public void Preset_Colors_Match_Both_Themes(WeekCalendarColor color, string light, string dark)
    {
        Near(WeekCalendarPalette.Base(color, false), light);
        Near(WeekCalendarPalette.Base(color, true), dark);
    }

    [Theory]
    [InlineData(WeekCalendarColor.Blue, false, "#e6effb", "#1d385c", "#edf4fc", "#79a7e5", "#4e6483")]
    [InlineData(WeekCalendarColor.Blue, true, "#29333f", "#bfd6f5", "#242b33", "#4e71a0", "#9cb1cc")]
    [InlineData(WeekCalendarColor.Green, false, "#e6f2ea", "#1a422d", "#eef6f1", "#78b793", "#4e6c5a")]
    [InlineData(WeekCalendarColor.Violet, true, "#322f3f", "#d4cef5", "#2a2933", "#7165a0", "#afaacc")]
    [InlineData(WeekCalendarColor.Amber, false, "#fbf0e6", "#5c3d18", "#fcf5ed", "#e5b075", "#82684d")]
    [InlineData(WeekCalendarColor.Rose, true, "#402d30", "#f7c8d0", "#342729", "#a15c69", "#cea5ab")]
    [InlineData(WeekCalendarColor.Teal, false, "#e6f2f2", "#1a4244", "#eef6f6", "#78b8bb", "#4e6c6e")]
    [InlineData(WeekCalendarColor.Neutral, true, "#313131", "#d1d1d1", "#292929", "#6c6c6c", "#acacac")]
    public void Derived_Colors_Match_Arc(WeekCalendarColor color, bool dark, string fill, string text, string pastFill, string pastBar,
                                         string pastText)
    {
        var colors = WeekCalendarPalette.Derive(WeekCalendarPalette.Base(color, dark), dark);
        Near(colors.Fill, fill);
        Near(colors.Text, text);
        Near(colors.PastFill, pastFill);
        Near(colors.PastBar, pastBar);
        Near(colors.PastText, pastText);
        colors.SubText.A.ShouldBe((byte)Math.Round(0.78 * 255));
        colors.SubText.R.ShouldBe(colors.Text.R);
    }

    [Fact]
    public void Custom_Colors_Mix_Like_Presets_And_Keep_Their_Base_In_Both_Themes()
    {
        var source = new WeekCalendarSource("x", "X", WeekCalendarColor.Custom) { CustomColor = Color.Parse("#e11d48") };
        var light = WeekCalendarPalette.Derive(WeekCalendarPalette.BaseOf(source, false), false);
        var dark = WeekCalendarPalette.Derive(WeekCalendarPalette.BaseOf(source, true), true);
        Near(light.Bar, "#e11d48", 0);
        Near(dark.Bar, "#e11d48", 0);
        Near(light.Fill, "#ffe8e8");
        Near(light.Text, "#631e26");
        Near(light.PastFill, "#ffefef");
        Near(light.PastBar, "#f57f86");
        Near(light.PastText, "#8b5154");
        Near(dark.Fill, "#3e2223");
        Near(dark.Text, "#f5aeaf");
        Near(dark.PastFill, "#331f20");
        Near(dark.PastBar, "#972938");
        Near(dark.PastText, "#cc8d8e");
        // A hover is a stronger mix than the rest fill.
        Oklch.FromColor(light.HoverFill).L.ShouldBeLessThan(Oklch.FromColor(light.Fill).L);
        // No custom color falls back to gray.
        Near(WeekCalendarPalette.BaseOf(new WeekCalendarSource("y", "Y", WeekCalendarColor.Custom), false), "#7a7a7a");
        Near(WeekCalendarPalette.BaseOf(null, true), "#9e9e9e");
    }

    // The day index -------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_Index_Splits_Events_At_Midnight_And_Leaves_Hidden_Calendars_Out()
    {
        var utc = TimeZoneInfo.Utc;
        var night = new WeekCalendarEvent("n", "Night train", new DateTimeOffset(2026, 10, 21, 22, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 22, 6, 30, 0, TimeSpan.Zero), "trips");
        var hidden = new WeekCalendarEvent("h", "Physio", new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), "health");
        var zero = new WeekCalendarEvent("z", "Reminder", new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero), "studio");
        var allDay = new WeekCalendarEvent("a", "Hotel", new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.FromHours(9)),
            new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.FromHours(9)), "trips") { IsAllDay = true };
        var index = WeekCalendarIndex.Build([night, hidden, zero, allDay], e => e.CalendarId != "health", utc);
        index.EventCount.ShouldBe(3);
        var wednesday = index.Timed(D(2026, 10, 21));
        wednesday.Count.ShouldBe(2);
        wednesday[0].Event.ShouldBe(zero);
        wednesday[0].EndMinute.ShouldBe(8 * 60 + 15);
        wednesday[1].StartMinute.ShouldBe(22 * 60);
        wednesday[1].EndMinute.ShouldBe(1440);
        wednesday[1].ContinuesAfter.ShouldBeTrue();
        var thursday = index.Timed(D(2026, 10, 22));
        thursday.Single().StartMinute.ShouldBe(0);
        thursday.Single().EndMinute.ShouldBe(6 * 60 + 30);
        thursday.Single().ContinuesBefore.ShouldBeTrue();
        // All-day dates are the dates as written, the end exclusive, whatever the zone.
        var entry = index.AllDay(new WeekCalendarDays(D(2026, 10, 19), 7)).Single();
        (entry.Start, entry.EndExclusive).ShouldBe((D(2026, 10, 21), D(2026, 10, 24)));
        entry.LastDay.ShouldBe(D(2026, 10, 23));
        index.CountOn(D(2026, 10, 21)).ShouldBe(3);
        index.CountOn(D(2026, 10, 23)).ShouldBe(1);
        var grid = new WeekCalendarTimeGrid(7, 20, 52);
        index.TimedInHours(D(2026, 10, 21), grid).Select(t => t.Segment.Event).ShouldBe([zero]);
        index.TimedInHours(D(2026, 10, 22), grid).ShouldBeEmpty();
        index.EarliestMinute(new WeekCalendarDays(D(2026, 10, 19), 7), new WeekCalendarTimeGrid(0, 24, 52)).ShouldBe(0);
        index.EarliestMinute(new WeekCalendarDays(D(2026, 10, 19), 7), grid).ShouldBe(8 * 60);
    }

    [Fact]
    public void The_Demo_Week_Has_Arcs_Density()
    {
        var monday = D(2026, 10, 19);
        var events = WeekCalendarDemoData.CreateEvents(monday, TimeZoneInfo.Utc);
        var index = WeekCalendarIndex.Build(events, _ => true, TimeZoneInfo.Utc);
        var week = new WeekCalendarDays(monday, 7);
        var timed = Enumerable.Range(0, 7).Sum(i => index.Timed(week.DayAt(i)).Count);
        timed.ShouldBeInRange(32, 38);
        index.AllDay(week).Count.ShouldBe(3);
        events.Select(e => e.Title).ShouldNotContain("Farmers market");
        events.Select(e => e.Title).ShouldNotContain("Leadership offsite");
    }
}
