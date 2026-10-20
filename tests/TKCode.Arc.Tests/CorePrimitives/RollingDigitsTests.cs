using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Core <see cref="RollingDigits"/>: the wheel options Arc's number field uses.</summary>
public class RollingDigitsTests
{
    [AvaloniaFact]
    public void Wheels_Follow_The_Value_Unless_The_Short_Way_Is_Asked_For()
    {
        var follow = new RollingDigits { Value = 9, UseGrouping = false };
        var shortest = new RollingDigits { Value = 9, UseGrouping = false, IsShortestTurn = true };
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { follow, shortest } }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            // 9 → 2: following the value turns the ones wheel 7 steps back; the short way is 3 steps on (9, 0, 1, 2).
            follow.Value = 2;
            shortest.Value = 2;
            follow.WheelTargets[^1].ShouldBe(2);
            shortest.WheelTargets[^1].ShouldBe(12);
            // 2 → 9: 7 steps on, or 3 steps back.
            follow.Value = 9;
            shortest.Value = 9;
            follow.WheelTargets[^1].ShouldBe(9);
            shortest.WheelTargets[^1].ShouldBe(9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Instant_Changes_Jump_Without_Rolling()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var digits = new RollingDigits { Value = 1, IsInstant = true };
        var window = TestHelpers.Show(digits, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var requests = clock.RequestCount;
            digits.Value = 4;
            digits.WheelTargets[^1].ShouldBe(4);
            digits.DisplayText.ShouldBe("4");
            clock.RequestCount.ShouldBe(requests);
        }
        finally
        {
            window.Close();
        }
    }
}
