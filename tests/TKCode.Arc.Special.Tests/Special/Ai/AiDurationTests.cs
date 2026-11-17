namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The shared duration building blocks, with the values the components' own format tests use.</summary>
public class AiDurationTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(999, 0, 1)]
    [InlineData(1499, 1, 1)]
    [InlineData(1500, 1, 2)]
    [InlineData(14_450, 14, 14)]
    [InlineData(142_940, 142, 143)]
    [InlineData(-5000, 0, 0)]
    public void Whole_Seconds_Floor_And_Round_Half_Up(double ms, long floor, long rounded)
    {
        AiDuration.WholeSecondsFloor(TimeSpan.FromMilliseconds(ms)).ShouldBe(floor);
        AiDuration.WholeSecondsRounded(TimeSpan.FromMilliseconds(ms)).ShouldBe(rounded);
    }

    [Theory]
    [InlineData(759.5, 760)]
    [InlineData(1.4, 1)]
    [InlineData(2970, 2970)]
    public void Milliseconds_Round_Half_Away_From_Zero(double ms, long expected) =>
        AiDuration.WholeMilliseconds(TimeSpan.FromMilliseconds(ms)).ShouldBe(expected);

    [Theory]
    [InlineData(1499, 15)]
    [InlineData(1449, 14)]
    [InlineData(40, 0)]
    [InlineData(50, 1)]
    public void Tenths_Of_Milliseconds_Round_Half_Up(long ms, long tenths) =>
        AiDuration.TenthsOfMilliseconds(ms).ShouldBe(tenths);

    [Theory]
    [InlineData(0.067, 1)]
    [InlineData(26.382, 264)]
    [InlineData(65.3, 653)]
    public void Tenths_Of_Seconds_Round_Half_Away_From_Zero(double seconds, long tenths) =>
        AiDuration.TenthsOfSeconds(seconds).ShouldBe(tenths);

    [Fact]
    public void Seconds_And_Minutes_Have_Optional_Padding()
    {
        AiDuration.Seconds(23).ShouldBe("23s");
        AiDuration.MinutesSeconds(143).ShouldBe("2m 23s");
        AiDuration.MinutesSeconds(60).ShouldBe("1m 0s");
        AiDuration.MinutesSeconds(67, padSeconds: true).ShouldBe("1m 07s");
        AiDuration.MinutesSeconds(65, padSeconds: true).ShouldBe("1m 05s");
        AiDuration.HoursMinutes(3840).ShouldBe("1h 4m");
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(3, "0:03")]
    [InlineData(85, "1:25")]
    [InlineData(140, "2:20")]
    [InlineData(3727, "1:02:07")]
    public void Clock_Is_M_Ss_Then_H_Mm_Ss(long seconds, string expected) => AiDuration.Clock(seconds).ShouldBe(expected);

    [Fact]
    public void Tenths_Keep_The_Decimal_And_Pad_From_A_Minute_On()
    {
        AiDuration.Tenths(19).ShouldBe("1.9s");
        AiDuration.Tenths(30).ShouldBe("3.0s");
        AiDuration.Tenths(0).ShouldBe("0.0s");
        AiDuration.Tenths(264).ShouldBe("26.4s");
        AiDuration.MinutesTenths(653).ShouldBe("1m 05.3s");
        AiDuration.MinutesTenths(600).ShouldBe("1m 00.0s");
        AiDuration.Milliseconds(760).ShouldBe("760ms");
    }

    [Theory]
    [InlineData(14, "14 seconds")]
    [InlineData(1, "1 second")]
    [InlineData(60, "1 minute")]
    [InlineData(143, "2 minutes 23 seconds")]
    [InlineData(3840, "1 hour 4 minutes")]
    [InlineData(0, "0 seconds")]
    public void Spoken_Uses_Plurals(long seconds, string expected) => AiDuration.Spoken(seconds).ShouldBe(expected);

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(5000, "5s")]
    [InlineData(23_400, "23s")]
    [InlineData(67_000, "1m 07s")]
    public void Plan_Duration_Delegates_To_The_Shared_Pieces(double ms, string expected) =>
        AgentPlanFormat.Duration(TimeSpan.FromMilliseconds(ms)).ShouldBe(expected);

    [Theory]
    [InlineData(760, "760ms")]
    [InlineData(1200, "1.2s")]
    [InlineData(2970, "3.0s")]
    [InlineData(59_960, "1m 00s")]
    [InlineData(65_000, "1m 05s")]
    public void Tool_Call_Duration_Delegates_To_The_Shared_Pieces(double ms, string expected) =>
        ToolCallFormat.FormatDuration(TimeSpan.FromMilliseconds(ms)).ShouldBe(expected);
}
