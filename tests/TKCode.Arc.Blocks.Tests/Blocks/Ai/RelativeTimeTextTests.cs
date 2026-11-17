namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The version menu's relative times: days under a week, last week, then whole weeks; words from the caller.</summary>
public class RelativeTimeTextTests
{
    private static readonly DateTimeOffset Now = PromptMenuSamples.Now;

    private static string Say(TimeSpan ago) =>
        RelativeTimeText.Format(Now - ago, Now, PromptMenuSamples.DaysAgo, "last week", PromptMenuSamples.WeeksAgo);

    [Theory]
    [InlineData(0, "today")]
    [InlineData(0.9, "today")]
    [InlineData(1, "yesterday")]
    [InlineData(3, "3 days ago")]
    [InlineData(6.99, "6 days ago")]
    [InlineData(7, "last week")]
    [InlineData(13.5, "last week")]
    [InlineData(14, "2 weeks ago")]
    [InlineData(20.9, "2 weeks ago")]
    [InlineData(21, "3 weeks ago")]
    [InlineData(29, "4 weeks ago")]
    [InlineData(400, "57 weeks ago")]
    public void Days_Under_A_Week_Then_Last_Week_Then_Whole_Weeks(double days, string expected) =>
        Say(TimeSpan.FromDays(days)).ShouldBe(expected);

    [Fact]
    public void A_Moment_In_The_Future_Counts_As_Today()
    {
        Say(TimeSpan.FromHours(-5)).ShouldBe("today");
        RelativeTimeText.DaysBetween(Now.AddDays(2), Now).ShouldBe(0);
    }

    [Fact]
    public void Offsets_Do_Not_Matter_Only_The_Instants()
    {
        var when = new DateTimeOffset(2026, 10, 20, 20, 0, 0, TimeSpan.FromHours(9)); // 11:00 UTC
        RelativeTimeText.DaysBetween(when, Now).ShouldBe(3);
    }

    [Fact]
    public void Missing_Words_Give_Empty_Text()
    {
        RelativeTimeText.Format(Now.AddDays(-2), Now, null, null, null).ShouldBe(string.Empty);
        RelativeTimeText.Format(Now.AddDays(-9), Now, null, null, null).ShouldBe(string.Empty);
        RelativeTimeText.Format(Now.AddDays(-30), Now, null, null, null).ShouldBe(string.Empty);
    }

    [Fact]
    public void The_Words_Get_The_Counts()
    {
        var days = new List<int>();
        var weeks = new List<int>();
        RelativeTimeText.Format(Now.AddDays(-5), Now, d => { days.Add(d); return ""; }, "", w => { weeks.Add(w); return ""; });
        RelativeTimeText.Format(Now.AddDays(-35), Now, d => { days.Add(d); return ""; }, "", w => { weeks.Add(w); return ""; });
        days.ShouldBe([5]);
        weeks.ShouldBe([5]);
    }
}
