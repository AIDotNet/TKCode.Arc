using System.Globalization;

namespace TKCode.Arc.Tests.Blocks.Calendars;

/// <summary>The month calendar's pure rules: the grid of a month, lanes and "N more", the day summary and the formats.</summary>
public class MonthCalendarModelTests
{
    private static readonly DateOnly Nov = new(2026, 11, 1);

    private static MonthCalendarEvent Timed(string title, DateOnly day, int h, int m = 0, int eh = -1, int em = 0) =>
        new(title, title, "c", day, new TimeOnly(h, m), eh < 0 ? null : new TimeOnly(eh, em));

    private static MonthCalendarEvent AllDay(string title, DateOnly first, DateOnly? last = null) =>
        new(title, title, "c", first) { EndDate = last };

    [Fact]
    public void A_Month_Has_As_Many_Weeks_As_It_Spans()
    {
        var (first, weeks) = MonthCalendarModel.GridOf(Nov, DayOfWeek.Monday);
        first.ShouldBe(new DateOnly(2026, 10, 26));
        weeks.ShouldBe(6);
        (first, weeks) = MonthCalendarModel.GridOf(Nov, DayOfWeek.Sunday);
        first.ShouldBe(new DateOnly(2026, 11, 1));
        weeks.ShouldBe(5);
        MonthCalendarModel.GridOf(new DateOnly(2027, 2, 1), DayOfWeek.Monday).Weeks.ShouldBe(4);
    }

    [Fact]
    public void Columns_Drop_Weekends_When_Asked()
    {
        MonthCalendarModel.Columns(new DateOnly(2026, 11, 2), true).Count.ShouldBe(7);
        var days = MonthCalendarModel.Columns(new DateOnly(2026, 11, 2), false);
        days.Count.ShouldBe(5);
        days.ShouldAllBe(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday);
    }

    [Theory]
    [InlineData(132.8, 4)]
    [InlineData(159, 6)]
    [InlineData(119, 4)]
    [InlineData(40, 1)]
    public void Lanes_Follow_The_Row_Height(double height, int lanes)
    {
        MonthCalendarModel.LaneCount(height).ShouldBe(lanes);
    }

    [Fact]
    public void Bars_Take_Lanes_Before_Timed_Events_And_The_Overflow_Folds_Into_More()
    {
        var week = MonthCalendarModel.Columns(new DateOnly(2026, 11, 9), true);
        var thu = new DateOnly(2026, 11, 12);
        var events = new List<MonthCalendarEvent>
        {
            Timed("Sync", thu, 9, 15, 9, 30),
            Timed("Review", thu, 10, 0, 10, 45),
            AllDay("Planning", thu, thu.AddDays(1)),
            Timed("Lunch", thu, 12, 30, 13, 30),
            Timed("Research", thu, 14, 0, 15, 30),
            Timed("Ship", thu, 16, 30, 17, 0),
        };
        var layout = MonthCalendarModel.Pack(week, events, 4);
        var shown = layout.Items.Where(i => i.StartColumn <= 3 && i.EndColumn >= 3).ToList();
        shown.Count.ShouldBe(3);
        shown.First(i => i.Lane == 0).Event.Title.ShouldBe("Planning");
        shown.First(i => i.Lane == 1).Event.Title.ShouldBe("Sync");
        shown.First(i => i.Lane == 2).Event.Title.ShouldBe("Review");
        var more = layout.More.ShouldHaveSingleItem();
        more.Column.ShouldBe(3);
        more.Lane.ShouldBe(3);
        more.Count.ShouldBe(3);
        // The two-day bar still covers Friday, where nothing overflows.
        layout.Items.Single(i => i.Event.Title == "Planning").EndColumn.ShouldBe(4);
    }

    [Fact]
    public void A_Bar_Across_Weeks_Is_Cut_Into_Segments_With_Square_Ends()
    {
        var bar = AllDay("Trip", new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 9));
        var first = MonthCalendarModel.Pack(MonthCalendarModel.Columns(new DateOnly(2026, 11, 2), true), [bar], 4).Items.Single();
        first.StartColumn.ShouldBe(5);
        first.EndColumn.ShouldBe(6);
        first.ClipStart.ShouldBeFalse();
        first.ClipEnd.ShouldBeTrue();
        var second = MonthCalendarModel.Pack(MonthCalendarModel.Columns(new DateOnly(2026, 11, 9), true), [bar], 4).Items.Single();
        second.StartColumn.ShouldBe(0);
        second.EndColumn.ShouldBe(0);
        second.ClipStart.ShouldBeTrue();
        second.ClipEnd.ShouldBeFalse();
    }

    [Fact]
    public void Earlier_And_Longer_Bars_Get_The_Upper_Lanes()
    {
        var week = MonthCalendarModel.Columns(new DateOnly(2026, 11, 23), true);
        var a = AllDay("Span", new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 25));
        var b = AllDay("Conference", new DateOnly(2026, 11, 24), new DateOnly(2026, 11, 26));
        var c = AllDay("Holiday", new DateOnly(2026, 11, 26));
        var layout = MonthCalendarModel.Pack(week, [c, b, a], 6);
        layout.Items.Single(i => i.Event == a).Lane.ShouldBe(0);
        layout.Items.Single(i => i.Event == b).Lane.ShouldBe(1);
        layout.Items.Single(i => i.Event == c).Lane.ShouldBe(0);
    }

    [Fact]
    public void Free_Gaps_Are_An_Hour_Or_More_Inside_The_Working_Hours()
    {
        (TimeOnly, TimeOnly) B(int h, int m, int eh, int em) => (new TimeOnly(h, m), new TimeOnly(eh, em));
        var gaps = MonthCalendarModel.FreeGaps([B(9, 15, 9, 30), B(10, 0, 10, 45), B(12, 30, 13, 30), B(14, 0, 15, 30), B(16, 30, 17, 0)],
            new TimeOnly(9, 0), new TimeOnly(18, 0), TimeSpan.FromHours(1));
        gaps.ShouldBe([B(10, 45, 12, 30), B(15, 30, 16, 30), B(17, 0, 18, 0)]);
        // A 45 minute gap is not offered.
        MonthCalendarModel.FreeGaps([B(9, 0, 11, 0), B(11, 45, 18, 0)], new TimeOnly(9, 0), new TimeOnly(18, 0), TimeSpan.FromHours(1))
            .ShouldBeEmpty();
    }

    [Fact]
    public void The_Day_Summary_Counts_Booked_Time_Without_Unanswered_Invitations()
    {
        var day = new DateOnly(2026, 11, 18);
        var invite = Timed("Panel", day, 10, 0, 11, 0);
        invite.Rsvp = MonthCalendarRsvp.Pending;
        var declined = Timed("Skipped", day, 12, 0, 13, 0);
        declined.Rsvp = MonthCalendarRsvp.No;
        var summary = MonthCalendarModel.Summarize(day, [invite, declined, Timed("Webinar", day, 15, 0, 16, 0)],
            new TimeOnly(9, 0), new TimeOnly(18, 0), TimeSpan.FromHours(1));
        summary.EventCount.ShouldBe(3);
        summary.Booked.ShouldBe(TimeSpan.FromHours(1));
        summary.Free.ShouldBe(TimeSpan.FromHours(7));
        summary.Schedule.Count(e => e.IsGap).ShouldBe(3);
        summary.Schedule[0].IsGap.ShouldBeTrue();
        summary.Schedule[1].Event.ShouldBe(invite);
    }

    [Theory]
    [InlineData(9, 30, "9:30a")]
    [InlineData(10, 0, "10a")]
    [InlineData(16, 0, "4p")]
    [InlineData(12, 0, "12p")]
    [InlineData(0, 15, "12:15a")]
    public void Chip_Times_Are_Compact(int h, int m, string text)
    {
        MonthCalendarModel.ChipTime(new TimeOnly(h, m), CultureInfo.GetCultureInfo("en-US")).ShouldBe(text);
    }

    [Fact]
    public void Chip_Times_Use_24_Hours_Where_The_Culture_Does()
    {
        MonthCalendarModel.ChipTime(new TimeOnly(16, 5), CultureInfo.GetCultureInfo("de-DE")).ShouldBe("16:05");
    }

    [Theory]
    [InlineData(15, "15m")]
    [InlineData(60, "1h")]
    [InlineData(90, "1h 30m")]
    [InlineData(225, "3h 45m")]
    [InlineData(0, "0m")]
    public void Durations_Are_Short(int minutes, string text)
    {
        MonthCalendarModel.Duration(TimeSpan.FromMinutes(minutes)).ShouldBe(text);
    }
}
