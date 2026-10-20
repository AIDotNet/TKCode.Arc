using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Progress;

public class CountdownTests
{
    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Largest_Unit_Absorbs_The_Rest()
    {
        var seconds = (long)new TimeSpan(2, 3, 4, 5).TotalSeconds;
        CountdownTime.Split(seconds, CountdownUnits.All).Select(p => p.Value).ShouldBe([2L, 3, 4, 5]);
        CountdownTime.Split(seconds, CountdownUnits.Hours | CountdownUnits.Minutes).Select(p => p.Value).ShouldBe([51L, 4]);
        CountdownTime.Split(seconds, CountdownUnits.None).Length.ShouldBe(4);
    }

    [Fact]
    public void Wheels_Turn_The_Short_Way()
    {
        CountdownTime.BaseFor(CountdownUnits.Minutes, largest: false, length: 2, index: 0).ShouldBe(6);
        CountdownTime.BaseFor(CountdownUnits.Hours, largest: false, length: 2, index: 0).ShouldBe(3);
        CountdownTime.BaseFor(CountdownUnits.Minutes, largest: true, length: 2, index: 0).ShouldBe(10);
        CountdownTime.BaseFor(CountdownUnits.Seconds, largest: false, length: 2, index: 1).ShouldBe(10);
        CountdownTime.ShortestTurn(5, 4, 10).ShouldBe(-1);
        CountdownTime.ShortestTurn(0, 9, 10).ShouldBe(-1);
        // 00 → 59: the tens wheel of six steps once down.
        CountdownTime.ShortestTurn(0, 5, 6).ShouldBe(-1);
        CountdownTime.ShortestTurn(3, 6, 10).ShouldBe(3);
    }

    [Fact]
    public void Spoken_Text_Leaves_Out_Seconds_Above_A_Minute()
    {
        CountdownTime.Spoken(3725, CountdownUnits.All).ShouldBe("1 hour, 2 minutes");
        CountdownTime.Spoken(45, CountdownUnits.All).ShouldBe("45 seconds");
        CountdownTime.Spoken(86_401 + 60, CountdownUnits.All).ShouldBe("1 day, 1 minute");
    }

    [Fact]
    public void Targets_Resolve_Without_The_Device_Time_Zone()
    {
        Countdown.ResolveTarget("2026-10-06T10:00:00-07:00").ShouldBe(new DateTimeOffset(2026, 10, 6, 17, 0, 0, TimeSpan.Zero));
        Countdown.ResolveTarget("2026-10-06T10:00").ShouldBe(new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test+02", TimeSpan.FromHours(2), "Test", "Test");
        Countdown.ResolveTarget("2026-10-06 10:00", zone).UtcDateTime.ShouldBe(new DateTime(2026, 10, 6, 8, 0, 0));
    }

    [Fact]
    public void Large_Digits_Follow_The_Container()
    {
        Countdown.LargeDigitSize(320).ShouldBe(40);
        Countdown.LargeDigitSize(100).ShouldBe(28);
        Countdown.LargeDigitSize(1200).ShouldBe(72);
        Countdown.DisplaySeconds(TimeSpan.FromMilliseconds(1001)).ShouldBe(2);
    }

    [AvaloniaFact]
    public void Counts_To_The_Target_And_Completes_Once()
    {
        var clock = new FakeClock();
        var countdown = new Countdown { TimeProvider = clock, Target = clock.Now.AddSeconds(65), Width = 360 };
        var completed = 0;
        countdown.Completed += (_, _) => completed++;
        var window = TestHelpers.Show(countdown);
        try
        {
            var face = TestHelpers.Descendant<CountdownFace>(countdown);
            countdown.Remaining.ShouldBe(TimeSpan.FromSeconds(65));
            face.DisplayedDigits.ShouldBe("00000105");
            AutomationProperties.GetName(countdown).ShouldBe("Time remaining");
            AutomationProperties.GetHelpText(countdown).ShouldBe("1 minute");

            clock.Now = clock.Now.AddSeconds(30);
            countdown.Tick();
            face.DisplayedDigits.ShouldBe("00000035");

            clock.Now = clock.Now.AddSeconds(40);
            countdown.Tick();
            countdown.IsCompleted.ShouldBeTrue();
            countdown.Classes.ShouldContain(Countdown.CompletePseudoClass);
            completed.ShouldBe(1);
            AutomationProperties.GetHelpText(countdown).ShouldBe("Live now");
            countdown.Tick();
            completed.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Passed_Target_Shows_Finished_Without_Firing()
    {
        var clock = new FakeClock();
        var countdown = new Countdown { TimeProvider = clock, Target = clock.Now.AddSeconds(-5), Variant = CountdownVariant.Compact };
        var completed = 0;
        countdown.Completed += (_, _) => completed++;
        var window = TestHelpers.Show(countdown);
        try
        {
            countdown.IsCompleted.ShouldBeTrue();
            completed.ShouldBe(0);
            countdown.Classes.ShouldContain(Countdown.CompactPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Duration_Countdowns_Pause_And_Resume()
    {
        var countdown = new Countdown { Duration = TimeSpan.FromMinutes(5), Units = CountdownUnits.Minutes | CountdownUnits.Seconds };
        var window = TestHelpers.Show(countdown);
        try
        {
            countdown.Remaining.ShouldBeGreaterThan(TimeSpan.FromMinutes(4.9));
            TestHelpers.Descendant<CountdownFace>(countdown).DisplayedDigits.ShouldBe("0500");
            countdown.Pause();
            countdown.IsPaused.ShouldBeTrue();
            var paused = countdown.Remaining;
            Thread.Sleep(30);
            countdown.Tick();
            countdown.Remaining.ShouldBe(paused);
            countdown.Resume();
            Thread.Sleep(30);
            countdown.Tick();
            countdown.Remaining.ShouldBeLessThan(paused);
        }
        finally
        {
            window.Close();
        }
    }
}
