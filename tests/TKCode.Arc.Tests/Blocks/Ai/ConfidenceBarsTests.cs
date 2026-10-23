using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ConfidenceBars"/>: heights against the measured numbers, colors per state, the strip's geometry, the height
/// spring and the color fade at full motion, jumps below it, rebuilding on a new length, and staying out of automation.
/// </summary>
public class ConfidenceBarsTests
{
    private static readonly ConfidenceBarItem Pending = new(null, ConfidenceBarState.Pending);

    private static IReadOnlyList<ConfidenceBarItem> Pendings(int count) => Enumerable.Repeat(Pending, count).ToArray();

    private static (AiReplyRig Rig, ConfidenceBars Bars) Stage(IReadOnlyList<ConfidenceBarItem> items, ArcMotionLevel level = ArcMotionLevel.Off,
        ManualFrameClock? clock = null)
    {
        var bars = new ConfidenceBars { Items = items };
        return (new AiReplyRig(bars, level, clock), bars);
    }

    private static ConfidenceBarsStrip Strip(ConfidenceBars bars) => bars.Strip.ShouldNotBeNull();

    [AvaloniaFact]
    public void Heights_Follow_The_Measured_Numbers()
    {
        // Arc: 99% → 17.7, 98% → 17.5, 93% → 16.2, 72% → 10.7, 64% → 8.6.
        ConfidenceBars.HeightOf(new(0.99, ConfidenceBarState.Ok)).ShouldBe(17.74, 0.001);
        ConfidenceBars.HeightOf(new(0.98, ConfidenceBarState.Ok)).ShouldBe(17.48, 0.001);
        ConfidenceBars.HeightOf(new(0.93, ConfidenceBarState.Ok)).ShouldBe(16.18, 0.001);
        ConfidenceBars.HeightOf(new(0.72, ConfidenceBarState.Check)).ShouldBe(10.72, 0.001);
        ConfidenceBars.HeightOf(new(0.64, ConfidenceBarState.Check)).ShouldBe(8.64, 0.001);
        ConfidenceBars.HeightOf(new(1, ConfidenceBarState.Accepted)).ShouldBe(18);
        // A pending field is a 3px stub whatever it carries; a very low value never goes below it.
        ConfidenceBars.HeightOf(Pending).ShouldBe(3);
        ConfidenceBars.HeightOf(new(0.99, ConfidenceBarState.Pending)).ShouldBe(3);
        ConfidenceBars.HeightOf(new(0.1, ConfidenceBarState.Check)).ShouldBe(3);
        // A value without a confidence (typed by the person) stands full.
        ConfidenceBars.HeightOf(new(null, ConfidenceBarState.Ok)).ShouldBe(18);
    }

    [AvaloniaFact]
    public void The_Strip_Is_18_High_With_4px_Bars_3px_Apart()
    {
        var (rig, bars) = Stage(Pendings(10));
        using var _ = rig;
        var strip = Strip(bars);
        strip.Count.ShouldBe(10);
        strip.Bounds.Height.ShouldBe(18);
        strip.Bounds.Width.ShouldBe(10 * 4 + 9 * 3);
        bars.Items = Pendings(1);
        rig.Settle();
        strip.Bounds.Width.ShouldBe(4);
        bars.Items = [];
        rig.Settle();
        strip.Count.ShouldBe(0);
        strip.Bounds.Width.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Colors_Follow_The_State()
    {
        IReadOnlyList<ConfidenceBarItem> items =
        [
            Pending, new(0.99, ConfidenceBarState.Ok), new(0.64, ConfidenceBarState.Check), new(0.97, ConfidenceBarState.Accepted)
        ];
        var (rig, bars) = Stage(items);
        using var _ = rig;
        var strip = Strip(bars);
        Color Token(ArcDesignTokenKind kind) => ArcTokens.GetColor(bars, kind);
        strip.ColorAt(0).ShouldBe(Token(ArcDesignTokenKind.Border));
        strip.ColorAt(1).ShouldBe(Oklch.MixOklab(Token(ArcDesignTokenKind.Surface), Token(ArcDesignTokenKind.Success), 0.55));
        strip.ColorAt(2).ShouldBe(Token(ArcDesignTokenKind.Warning));
        strip.ColorAt(3).ShouldBe(Token(ArcDesignTokenKind.Success));
        strip.HeightAt(0).ShouldBe(3);
        strip.HeightAt(1).ShouldBe(17.74, 0.001);
        strip.HeightAt(2).ShouldBe(8.64, 0.001);
        // The sure color sits between the surface and the success color.
        var ok = strip.ColorAt(1);
        ok.ShouldNotBe(Token(ArcDesignTokenKind.Success));
        ok.ShouldNotBe(Token(ArcDesignTokenKind.Surface));
    }

    [AvaloniaFact]
    public void Full_Motion_Springs_The_Height_Up_And_Down_And_Fades_The_Color_In_160ms()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (rig, bars) = Stage(Pendings(3), ArcMotionLevel.Full, clock);
        using var _ = rig;
        var strip = Strip(bars);
        bars.Items = [new(0.99, ConfidenceBarState.Ok), Pending, Pending];
        rig.Settle();
        strip.HeightAt(0).ShouldBe(3, "the first frame has not run");
        rig.Frames(100);
        // On the Smooth spring (critically damped, no overshoot): rising, not there yet.
        var mid = strip.HeightAt(0);
        mid.ShouldBeInRange(4, 17.5);
        var color = strip.ColorAt(0);
        color.ShouldNotBe(ArcTokens.GetColor(bars, ArcDesignTokenKind.Border));
        rig.Frames(100);
        strip.HeightAt(0).ShouldBeGreaterThan(mid);
        strip.ColorAt(0).ShouldBe(bars.ColorOf(ConfidenceBarState.Ok), "160ms in");
        rig.Frames(500);
        strip.HeightAt(0).ShouldBe(17.74, 0.01);
        strip.HeightAt(0).ShouldBeLessThanOrEqualTo(17.74 + 1e-6);
        // The others did not move.
        strip.HeightAt(1).ShouldBe(3);

        // Clearing for another run: the bar falls on the same spring.
        bars.Items = Pendings(3);
        rig.Settle();
        rig.Frames(100);
        strip.HeightAt(0).ShouldBeInRange(3.5, 17);
        rig.Frames(600);
        strip.HeightAt(0).ShouldBe(3, 0.01);
        strip.ColorAt(0).ShouldBe(bars.ColorOf(ConfidenceBarState.Pending));
    }

    [AvaloniaFact]
    public void Accepting_Recolors_Without_Moving()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        IReadOnlyList<ConfidenceBarItem> sure = [new(0.99, ConfidenceBarState.Ok), new(0.64, ConfidenceBarState.Check)];
        var (rig, bars) = Stage(sure, ArcMotionLevel.Full, clock);
        using var _ = rig;
        var strip = Strip(bars);
        bars.Items = [new(0.99, ConfidenceBarState.Accepted), new(0.64, ConfidenceBarState.Check)];
        rig.Settle();
        strip.StateAt(0).ShouldBe(ConfidenceBarState.Accepted);
        rig.Frames(67);
        var success = bars.ColorOf(ConfidenceBarState.Accepted);
        var ok = bars.ColorOf(ConfidenceBarState.Ok);
        var halfway = strip.ColorAt(0);
        halfway.ShouldNotBe(ok);
        halfway.ShouldNotBe(success);
        strip.HeightAt(0).ShouldBe(17.74, 0.001);
        rig.Frames(120);
        strip.ColorAt(0).ShouldBe(success);
        strip.ColorAt(1).ShouldBe(bars.ColorOf(ConfidenceBarState.Check));
    }

    [AvaloniaFact]
    public void Reduced_And_Off_Jump()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var (rig, bars) = Stage(Pendings(2), level, clock);
            using var _ = rig;
            var strip = Strip(bars);
            bars.Items = [new(0.64, ConfidenceBarState.Check), new(0.99, ConfidenceBarState.Accepted)];
            rig.Settle();
            strip.HeightAt(0).ShouldBe(8.64, 0.001);
            strip.HeightAt(1).ShouldBe(17.74, 0.001);
            strip.ColorAt(0).ShouldBe(bars.ColorOf(ConfidenceBarState.Check));
            strip.ColorAt(1).ShouldBe(bars.ColorOf(ConfidenceBarState.Accepted));
        }
    }

    [AvaloniaFact]
    public void A_List_Of_Another_Length_Starts_Over_Without_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (rig, bars) = Stage(Pendings(10), ArcMotionLevel.Full, clock);
        using var _ = rig;
        var strip = Strip(bars);
        bars.Items = [new(0.99, ConfidenceBarState.Ok), new(0.71, ConfidenceBarState.Check), Pending];
        rig.Settle();
        strip.Count.ShouldBe(3);
        strip.HeightAt(0).ShouldBe(17.74, 0.001);
        strip.HeightAt(1).ShouldBe(10.46, 0.001);
        strip.ColorAt(1).ShouldBe(bars.ColorOf(ConfidenceBarState.Check));
        rig.Settle();
        strip.Bounds.Width.ShouldBe(3 * 4 + 2 * 3);
    }

    [AvaloniaFact]
    public void A_Theme_Change_Recolors_At_Once()
    {
        var (rig, bars) = Stage([new(0.99, ConfidenceBarState.Ok)]);
        using var _ = rig;
        var strip = Strip(bars);
        bars.SuccessBrush = new SolidColorBrush(Colors.Lime);
        bars.SurfaceBrush = new SolidColorBrush(Colors.Black);
        strip.ColorAt(0).ShouldBe(Oklch.MixOklab(Colors.Black, Colors.Lime, 0.55));
    }

    [AvaloniaFact]
    public void The_Bars_Are_Decorative()
    {
        var (rig, bars) = Stage(Pendings(4));
        using var _ = rig;
        AutomationProperties.GetAccessibilityView(bars).ShouldBe(AccessibilityView.Raw);
        var peer = ControlAutomationPeer.CreatePeerForElement(bars);
        peer.IsContentElement().ShouldBeFalse();
        peer.IsControlElement().ShouldBeFalse();
        peer.GetChildren().ShouldBeEmpty();
        bars.Focusable.ShouldBeFalse();
        bars.IsHitTestVisible.ShouldBeFalse();
    }
}
