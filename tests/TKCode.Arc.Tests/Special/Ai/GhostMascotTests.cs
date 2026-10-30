using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// GhostMascot: size and the compact drawing, status and self-play with its event, the accessible label and live region,
/// colors per tone, the pose math (blend, hem, swirl, writing cycle, sparkle, wobble, spring) and the motion driven frame by
/// frame: blends that start where the ghost is and keep its velocity, phases that only grow, the gaze, reduced and no
/// motion, and the loop that pauses.
/// </summary>
public class GhostMascotTests
{
    private const double Frame = 1.0 / 60;

    private static (GhostMascot Ghost, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off, Action<GhostMascot>? setup = null)
    {
        var ghost = new GhostMascot();
        setup?.Invoke(ghost);
        var window = TestHelpers.Show(ghost, level);
        Dispatcher.UIThread.RunJobs();
        return (ghost, window);
    }

    private static void Run(GhostMascot ghost, double seconds)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += Frame)
        {
            ghost.Advance(Frame);
        }
    }

    // Every pose value but the eye spring's stiffness, which switches at once by design.
    private static double[] Snapshot(GhostMascot ghost) =>
        ghost.Pose.ToArray().Where((_, i) => i != GhostMascotPose.GazeStiffness).ToArray();

    private static double MaxDifference(double[] a, double[] b)
    {
        var max = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            max = Math.Max(max, Math.Abs(a[i] - b[i]));
        }
        return max;
    }

    [AvaloniaFact]
    public void Defaults_SelfPlayIdle_96px_Accent()
    {
        var (ghost, _) = Open();
        ghost.Status.ShouldBeNull();
        ghost.EffectiveStatus.ShouldBe(GhostMascotStatus.Idle);
        ghost.IsSelfPlaying.ShouldBeTrue();
        ghost.Size.ShouldBe(96);
        ghost.Bounds.Width.ShouldBe(96);
        ghost.Bounds.Height.ShouldBe(96);
        ghost.Tone.ShouldBe(GhostMascotTone.Accent);
        ghost.ShowGround.ShouldBeTrue();
        ghost.Announce.ShouldBeFalse();
        ghost.IsPaused.ShouldBeFalse();
        ghost.AssistantName.ShouldBe("Assistant");
        ghost.Focusable.ShouldBeFalse();
        ghost.IsCompact.ShouldBeFalse();
        ghost.Layout.ShouldBeSameAs(GhostMascotLayout.Regular);
        ghost.Classes.Contains(":idle").ShouldBeTrue();
        ghost.Classes.Contains(":compact").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Size_IsAFixedSquare_AndUnder56UsesTheCompactDrawing()
    {
        var (ghost, _) = Open(setup: g => g.Size = 24);
        ghost.Bounds.Width.ShouldBe(24);
        ghost.Bounds.Height.ShouldBe(24);
        ghost.IsCompact.ShouldBeTrue();
        ghost.Layout.ShouldBeSameAs(GhostMascotLayout.Compact);
        ghost.Classes.Contains(":compact").ShouldBeTrue();
        GhostMascotLayout.Compact.HasGround.ShouldBeFalse();
        GhostMascotLayout.Compact.EyeScale.ShouldBeGreaterThan(1);
        GhostMascotLayout.Compact.Stroke.ShouldBeGreaterThan(1);

        ghost.Size = 56;
        Dispatcher.UIThread.RunJobs();
        ghost.IsCompact.ShouldBeFalse();
        ghost.Bounds.Width.ShouldBe(56);

        ghost.Size = double.NaN;
        ghost.Size.ShouldBe(96);
    }

    [AvaloniaFact]
    public void Status_SetsEffectiveStatus_PseudoClasses_AndLabel()
    {
        var (ghost, _) = Open(setup: g => g.AssistantName = "Atlas");
        ghost.Status = GhostMascotStatus.Thinking;
        ghost.EffectiveStatus.ShouldBe(GhostMascotStatus.Thinking);
        ghost.IsSelfPlaying.ShouldBeFalse();
        ghost.Classes.Contains(":thinking").ShouldBeTrue();
        ghost.Classes.Contains(":idle").ShouldBeFalse();
        AutomationProperties.GetName(ghost).ShouldBe("Atlas, thinking");
        AutomationProperties.GetControlTypeOverride(ghost).ShouldBe(AutomationControlType.Image);
        AutomationProperties.GetLiveSetting(ghost).ShouldBe(AutomationLiveSetting.Off);

        foreach (var (status, word) in new[]
                 {
                     (GhostMascotStatus.Listening, "listening"), (GhostMascotStatus.Writing, "writing"),
                     (GhostMascotStatus.Done, "done"), (GhostMascotStatus.Error, "error"), (GhostMascotStatus.Idle, "idle")
                 })
        {
            ghost.Status = status;
            AutomationProperties.GetName(ghost).ShouldBe("Atlas, " + word);
            ghost.Classes.Contains(":" + status.ToString().ToLowerInvariant()).ShouldBeTrue();
        }

        ghost.Announce = true;
        AutomationProperties.GetLiveSetting(ghost).ShouldBe(AutomationLiveSetting.Polite);
        ghost.AccessibleName = "Your assistant";
        AutomationProperties.GetName(ghost).ShouldBe("Your assistant");
    }

    [AvaloniaFact]
    public void Labels_TranslateTheWordsAndThePattern()
    {
        var labels = new GhostMascotLabels { Thinking = "denkt nach", LabelFormat = "{0} {1}" };
        labels.LabelFor("Atlas", GhostMascotStatus.Thinking).ShouldBe("Atlas denkt nach");
        new GhostMascotLabels().LabelFor("Atlas", GhostMascotStatus.Done).ShouldBe("Atlas, done");
        new GhostMascotLabels { LabelFormat = "{5}" }.LabelFor("Atlas", GhostMascotStatus.Idle).ShouldBe("Atlas, idle");

        var (ghost, _) = Open(setup: g =>
        {
            g.Status = GhostMascotStatus.Thinking;
            g.Labels = labels;
        });
        AutomationProperties.GetName(ghost).ShouldBe("Assistant denkt nach");
    }

    [AvaloniaFact]
    public void SelfPlay_StepsThroughEveryState_AndRaisesStatusChanged()
    {
        var (ghost, _) = Open();
        var seen = new List<GhostMascotStatus>();
        ghost.StatusChanged += (_, e) =>
        {
            e.RoutedEvent.ShouldBe(GhostMascot.StatusChangedEvent);
            seen.Add(e.Status);
        };
        var total = 0.0;
        for (var i = 0; i < GhostMascotSamples.Count; i++)
        {
            total += GhostMascotSamples.SecondsAt(i);
        }
        Run(ghost, total + 0.05);
        seen.ShouldBe([
            GhostMascotStatus.Listening, GhostMascotStatus.Thinking, GhostMascotStatus.Writing, GhostMascotStatus.Done,
            GhostMascotStatus.Error, GhostMascotStatus.Idle
        ]);
        ghost.EffectiveStatus.ShouldBe(GhostMascotStatus.Idle);

        // A set status stops the self-play and raises nothing.
        seen.Clear();
        ghost.Status = GhostMascotStatus.Writing;
        Run(ghost, 10);
        seen.ShouldBeEmpty();
        ghost.EffectiveStatus.ShouldBe(GhostMascotStatus.Writing);

        // Unset again: it starts over from idle.
        ghost.Status = null;
        ghost.EffectiveStatus.ShouldBe(GhostMascotStatus.Idle);
    }

    [AvaloniaFact]
    public void NoMotion_SwitchesInPlace_ToTheStillPose()
    {
        var (ghost, _) = Open(setup: g => g.Status = GhostMascotStatus.Idle);
        ghost.IsStill.ShouldBeTrue();
        ghost.IsLoopActive.ShouldBeFalse();
        ghost.Status = GhostMascotStatus.Thinking;
        ghost.IsBlending.ShouldBeFalse();
        ghost.Pose[GhostMascotPose.Swirl].ShouldBe(1);
        ghost.Pose[GhostMascotPose.Opacity].ShouldBe(0.68);
        // Representative marks: a half drawn swirl, half a written line, the sparkle at full size.
        ghost.SwirlNow.ShouldBe((0, 0.5));
        ghost.WritingNow.Written.ShouldBe(0.5, 1e-9);
        ghost.WritingNow.PenLift.ShouldBe(0, 1e-9);
        ghost.SparkleGrowNow.ShouldBe(1);
        ghost.FloatOffset.ShouldBe(0);
        ghost.Gaze.Y.ShouldBe(-1);
        ghost.Status = GhostMascotStatus.Error;
        ghost.WobbleNow.ShouldBe(0);
        ghost.Pose[GhostMascotPose.Droop].ShouldBe(1);
    }

    [AvaloniaFact]
    public void ReducedMotion_LoopsOnlyToSelfPlay_AndRepaintsTheStillPose()
    {
        var (ghost, _) = Open(ArcMotionLevel.Reduced, g => g.Status = GhostMascotStatus.Thinking);
        ghost.IsStill.ShouldBeTrue();
        ghost.NeedsFrames().ShouldBeFalse();
        ghost.IsLoopActive.ShouldBeFalse();
        ghost.Status = GhostMascotStatus.Done;
        ghost.IsBlending.ShouldBeFalse();
        ghost.Pose[GhostMascotPose.Sparkle].ShouldBe(1);

        ghost.Status = null;
        ghost.NeedsFrames().ShouldBeTrue();
        ghost.IsLoopActive.ShouldBeTrue();
        var phase = ghost.FloatPhase;
        Run(ghost, 3.1);
        ghost.EffectiveStatus.ShouldBe(GhostMascotStatus.Listening);
        ghost.FloatPhase.ShouldBe(phase);
        ghost.IsBlending.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void FullMotion_StatusChangeBlendsFromTheFrameOnScreen_Over400ms()
    {
        var (ghost, _) = Open(ArcMotionLevel.Full, g => g.Status = GhostMascotStatus.Idle);
        ghost.IsStill.ShouldBeFalse();
        ghost.NeedsFrames().ShouldBeTrue();
        Run(ghost, 0.5);
        var before = Snapshot(ghost);
        ghost.Status = GhostMascotStatus.Thinking;
        ghost.IsBlending.ShouldBeTrue();
        // No jump: one frame later the pose is still next to where it was.
        ghost.Advance(Frame);
        MaxDifference(before, Snapshot(ghost)).ShouldBeLessThan(0.6);
        Run(ghost, 0.2 - Frame);
        var mid = ghost.Pose[GhostMascotPose.Opacity];
        mid.ShouldBeLessThan(1);
        mid.ShouldBeGreaterThan(0.68);
        Run(ghost, 0.21);
        ghost.IsBlending.ShouldBeFalse();
        ghost.Pose[GhostMascotPose.Opacity].ShouldBe(0.68, 1e-9);
        ghost.Pose[GhostMascotPose.Swirl].ShouldBe(1, 1e-9);
    }

    [AvaloniaFact]
    public void FullMotion_InterruptedBlend_KeepsValueAndVelocity()
    {
        var (ghost, _) = Open(ArcMotionLevel.Full, g => g.Status = GhostMascotStatus.Idle);
        Run(ghost, 0.3);
        ghost.Status = GhostMascotStatus.Done;
        Run(ghost, 0.15);
        var a = ghost.Pose[GhostMascotPose.Lift];
        ghost.Advance(Frame);
        var b = ghost.Pose[GhostMascotPose.Lift];
        var slope = b - a;
        slope.ShouldBeLessThan(0);
        // Interrupt mid-blend: the next frame continues in the same direction at about the same speed.
        ghost.Status = GhostMascotStatus.Error;
        ghost.Advance(Frame);
        var c = ghost.Pose[GhostMascotPose.Lift];
        (c - b).ShouldBeLessThan(0);
        Math.Abs((c - b) - slope).ShouldBeLessThan(Math.Abs(slope) * 0.5);
        Run(ghost, 0.5);
        ghost.Pose[GhostMascotPose.Lift].ShouldBe(4, 1e-9);
    }

    [AvaloniaFact]
    public void FullMotion_PhasesOnlyGrow_AndRatesBlendWithoutJumps()
    {
        var (ghost, _) = Open(ArcMotionLevel.Full, g => g.Status = GhostMascotStatus.Idle);
        Run(ghost, 0.5);
        ghost.Status = GhostMascotStatus.Writing;
        var lastFloat = ghost.FloatPhase;
        var lastWrite = ghost.WritePhase;
        var lastRate = ghost.Pose[GhostMascotPose.FloatRate];
        for (var i = 0; i < 60; i++)
        {
            ghost.Advance(Frame);
            ghost.FloatPhase.ShouldBeGreaterThan(lastFloat);
            ghost.WritePhase.ShouldBeGreaterThan(lastWrite);
            Math.Abs(ghost.Pose[GhostMascotPose.FloatRate] - lastRate).ShouldBeLessThan(0.02);
            lastFloat = ghost.FloatPhase;
            lastWrite = ghost.WritePhase;
            lastRate = ghost.Pose[GhostMascotPose.FloatRate];
        }
    }

    [AvaloniaFact]
    public void FullMotion_HemRipplesInPlace()
    {
        var (ghost, _) = Open(ArcMotionLevel.Full, g => g.Status = GhostMascotStatus.Idle);
        var geometry = ghost.BodyGeometry;
        var figure = geometry.Figures![0];
        figure.Segments!.Count.ShouldBe(16);
        var hem = (BezierSegment)figure.Segments[6];
        var before = hem.Point3;
        Run(ghost, 0.4);
        ghost.BodyGeometry.ShouldBeSameAs(geometry);
        geometry.Figures[0].Segments![6].ShouldBeSameAs(hem);
        hem.Point3.ShouldNotBe(before);
        // The hem runs from right to left in equal steps, near the hem line.
        for (var i = 4; i < 13; i++)
        {
            var segment = (BezierSegment)figure.Segments[i];
            var next = (BezierSegment)figure.Segments[i + 1];
            next.Point3.X.ShouldBeLessThan(segment.Point3.X);
            segment.Point3.Y.ShouldBeInRange(70, 86);
        }
    }

    [AvaloniaFact]
    public void FullMotion_Gaze_FollowsThePenWhileWriting_AndDriftsUpWhileThinking()
    {
        var (ghost, _) = Open(ArcMotionLevel.Full, g => g.Status = GhostMascotStatus.Writing);
        Run(ghost, 1.0);
        ghost.Gaze.X.ShouldBeGreaterThan(0.2);
        ghost.Gaze.Y.ShouldBeGreaterThan(0.2);

        ghost.Status = GhostMascotStatus.Thinking;
        Run(ghost, 0.2);
        // A slow drift: still well short of the top after 200ms.
        ghost.Gaze.Y.ShouldBeGreaterThan(-0.6);
        Run(ghost, 2.0);
        ghost.Gaze.Y.ShouldBeLessThan(-0.8);
    }

    [AvaloniaFact]
    public void FullMotion_DoneGrowsTheSparkle_AndErrorWobblesOnce()
    {
        var (ghost, _) = Open(ArcMotionLevel.Full, g => g.Status = GhostMascotStatus.Writing);
        Run(ghost, 0.2);
        ghost.Status = GhostMascotStatus.Done;
        ghost.SparkleGrowNow.ShouldBe(0, 1e-9);
        Run(ghost, 0.16);
        ghost.SparkleGrowNow.ShouldBeGreaterThan(0.6);
        Run(ghost, 0.2);
        ghost.SparkleGrowNow.ShouldBe(1);

        ghost.Status = GhostMascotStatus.Error;
        ghost.WobbleNow.ShouldBe(0);
        var peak = 0.0;
        for (var t = 0.0; t < 1.3; t += Frame)
        {
            ghost.Advance(Frame);
            peak = Math.Max(peak, Math.Abs(ghost.WobbleNow));
        }
        peak.ShouldBeGreaterThan(2);
        ghost.WobbleNow.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Paused_StopsTheLoop_AndResumeKeepsTheFrame()
    {
        var (ghost, _) = Open(ArcMotionLevel.Full, g => g.Status = GhostMascotStatus.Idle);
        ghost.IsLoopActive.ShouldBeTrue();
        ghost.IsPaused = true;
        ghost.IsLoopActive.ShouldBeFalse();
        var clock = ghost.Clock;
        Dispatcher.UIThread.RunJobs();
        ghost.Clock.ShouldBe(clock);
        ghost.IsPaused = false;
        ghost.IsLoopActive.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Tones_PickTheThemeInks_AndCustomColorsOverride()
    {
        var accent = Color.Parse("#3366FF");
        var raised = Color.Parse("#FAFAFA");
        var foreground = Color.Parse("#111111");
        var background = Color.Parse("#FFFFFF");
        var (ghost, _) = Open(setup: g => g.Status = GhostMascotStatus.Idle);
        ghost.AccentInk = new SolidColorBrush(accent);
        ghost.AccentForegroundInk = new SolidColorBrush(Colors.White);
        ghost.RaisedInk = new SolidColorBrush(raised);
        ghost.ForegroundInk = new SolidColorBrush(foreground);
        ghost.BackgroundInk = new SolidColorBrush(background);

        ghost.BodyColor.ShouldBe(accent);
        ghost.EyeColor.ShouldBe(Colors.White);
        ghost.MarkColor.ShouldBe(accent);
        ghost.HasHairline.ShouldBeFalse();

        ghost.Tone = GhostMascotTone.Sheet;
        ghost.BodyColor.ShouldBe(raised);
        ghost.EyeColor.ShouldBe(foreground);
        ghost.MarkColor.ShouldBe(accent);
        ghost.HasHairline.ShouldBeTrue();

        ghost.Tone = GhostMascotTone.Ink;
        ghost.BodyColor.ShouldBe(foreground);
        ghost.EyeColor.ShouldBe(background);

        var violet = Color.Parse("#7747FF");
        ghost.SheetBrush = new SolidColorBrush(violet);
        ghost.BodyColor.ShouldBe(violet);
        ghost.HasHairline.ShouldBeFalse();
        // The eyes become a dark mix of the sheet.
        Oklch.FromColor(ghost.EyeColor).L.ShouldBeLessThan(Oklch.FromColor(violet).L);
        ghost.EyeBrush = Brushes.White;
        ghost.EyeColor.ShouldBe(Colors.White);
        ghost.MarkBrush = Brushes.Orange;
        ghost.MarkColor.ShouldBe(Colors.Orange);
    }

    [AvaloniaFact]
    public void Theme_HandsTheInksAndTokensToTheGhost()
    {
        var (ghost, _) = Open();
        ghost.AccentInk.ShouldNotBeNull();
        ghost.RaisedInk.ShouldNotBeNull();
        ghost.CompactSize.ShouldBe(56);
        ghost.HairlineWidth.ShouldBe(1);
    }

    [Fact]
    public void Blend_IsAnEaseInOutFromRest_AndKeepsTheStartVelocity()
    {
        GhostMascotPose.Blend(0, 0, 1, 0.5).X.ShouldBe(0.5, 1e-9);
        GhostMascotPose.Blend(0, 0, 1, 0).V.ShouldBe(0, 1e-9);
        GhostMascotPose.Blend(0, 0, 1, 1).ShouldBe((1, 0));
        GhostMascotPose.Blend(0, 0, 1, 0.999).V.ShouldBe(0, 0.05);
        GhostMascotPose.Blend(2, 3, 5, 0).ShouldBe((2, 3));
        // The start slope is the given velocity.
        var (x, _) = GhostMascotPose.Blend(0, 3, 0, 0.001);
        (x / (0.001 * GhostMascotPose.BlendDuration)).ShouldBe(3, 0.05);
    }

    [Fact]
    public void Spring_IsCriticallyDamped_NoOvershootFromRest()
    {
        double x = 0, v = 0;
        for (var i = 0; i < 300; i++)
        {
            (x, v) = GhostMascotPose.Spring(x, v, 1, 30, Frame);
            x.ShouldBeLessThanOrEqualTo(1 + 1e-9);
        }
        x.ShouldBe(1, 1e-6);
        // Stable at long frames too.
        (x, _) = GhostMascotPose.Spring(0, 0, 1, 40, 0.1);
        x.ShouldBeInRange(0, 1);
    }

    [Fact]
    public void Swirl_DrawsInThenErases_AndIsEmptyAtTheWrap()
    {
        var (tail0, head0) = GhostMascotPose.SwirlWindow(0);
        (head0 - tail0).ShouldBe(0);
        var (tail1, head1) = GhostMascotPose.SwirlWindow(0.9999);
        (head1 - tail1).ShouldBeLessThan(0.001);
        var (tail, head) = GhostMascotPose.SwirlWindow(0.5);
        (head - tail).ShouldBeGreaterThan(0.5);
        var early = GhostMascotPose.SwirlWindow(0.2);
        early.Tail.ShouldBe(0);
        early.Head.ShouldBeGreaterThan(0);
        // The spiral runs from its outer end inward.
        var center = new Point(0, 0);
        var outer = GhostMascot.SwirlPoint(center, 10, 0);
        var inner = GhostMascot.SwirlPoint(center, 10, 1);
        Math.Sqrt(outer.X * outer.X + outer.Y * outer.Y).ShouldBe(10, 1e-9);
        Math.Sqrt(inner.X * inner.X + inner.Y * inner.Y).ShouldBe(2, 1e-9);
    }

    [Fact]
    public void Writing_WritesWordByWord_LiftsBetweenWords_ErasesAndWrapsEmpty()
    {
        GhostMascotPose.Writing(0).Written.ShouldBe(0);
        GhostMascotPose.Writing(0.9999).Written.ShouldBeLessThan(0.001);
        // In the first word the pen is down; in the gap after it the pen lifts.
        var inWord = GhostMascotPose.Writing(0.1 * GhostMascotPose.WriteShare);
        inWord.PenLift.ShouldBe(0);
        inWord.Pen.ShouldBe(0.1, 1e-9);
        var inGap = GhostMascotPose.Writing(0.3 * GhostMascotPose.WriteShare);
        inGap.PenLift.ShouldBeGreaterThan(1);
        // At the end of writing the whole line is there; the erase then glides the pen back, lifted.
        GhostMascotPose.Writing(GhostMascotPose.WriteShare - 1e-6).Written.ShouldBe(1, 1e-4);
        var erasing = GhostMascotPose.Writing(GhostMascotPose.WriteShare + (1 - GhostMascotPose.WriteShare) / 2);
        erasing.Written.ShouldBe(0.5, 1e-9);
        erasing.Pen.ShouldBe(0.5, 1e-9);
        erasing.PenLift.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Hem_SlopeMatchesTheWave()
    {
        const double h = 1e-5;
        foreach (var x in new[] { 24.0, 37.5, 50, 71 })
        {
            var (y, slope) = GhostMascotPose.Hem(x, 78, 1, 0.37);
            var (y2, _) = GhostMascotPose.Hem(x + h, 78, 1, 0.37);
            ((y2 - y) / h).ShouldBe(slope, 1e-3);
            Math.Abs(y - 78).ShouldBeLessThanOrEqualTo(3.5 + 1e-9);
        }
        GhostMascotPose.Hem(40, 78, 0, 0.2).ShouldBe((78, 0));
    }

    [Fact]
    public void SparkleAndWobble_TimeShapes()
    {
        GhostMascotPose.SparkleGrow(0).ShouldBe(0);
        GhostMascotPose.SparkleGrow(GhostMascotPose.SparkleGrowDuration).ShouldBe(1);
        GhostMascotPose.SparkleGrow(GhostMascotPose.SparkleGrowDuration / 2).ShouldBeGreaterThan(0.5);
        // Softened at the start: a tiny value in the first frame, then a real swing, then rest by 1.2s.
        Math.Abs(GhostMascotPose.Wobble(0.005)).ShouldBeLessThan(0.3);
        Math.Abs(GhostMascotPose.Wobble(0.11)).ShouldBeGreaterThan(2);
        GhostMascotPose.Wobble(GhostMascotPose.WobbleDuration).ShouldBe(0);
        Math.Abs(GhostMascotPose.Wobble(1.15)).ShouldBeLessThan(0.05);
        GhostMascotPose.BlinkClosure(GhostMascotPose.BlinkDuration / 2).ShouldBe(1, 1e-9);
        GhostMascotPose.BlinkClosure(GhostMascotPose.BlinkDuration).ShouldBe(0);
    }

    [Fact]
    public void Poses_GiveEachStatusItsMarks()
    {
        Span<double> pose = stackalloc double[GhostMascotPose.Count];
        GhostMascotPose.Write(GhostMascotStatus.Thinking, pose);
        pose[GhostMascotPose.Swirl].ShouldBe(1);
        pose[GhostMascotPose.Opacity].ShouldBeLessThan(1);
        pose[GhostMascotPose.Lift].ShouldBeLessThan(0);
        GhostMascotPose.Write(GhostMascotStatus.Writing, pose);
        pose[GhostMascotPose.Pen].ShouldBe(1);
        pose[GhostMascotPose.Swirl].ShouldBe(0);
        GhostMascotPose.Write(GhostMascotStatus.Done, pose);
        pose[GhostMascotPose.Sparkle].ShouldBe(1);
        pose[GhostMascotPose.Happy].ShouldBe(1);
        GhostMascotPose.Write(GhostMascotStatus.Listening, pose);
        pose[GhostMascotPose.Scale].ShouldBeGreaterThan(1);
        pose[GhostMascotPose.EyeSize].ShouldBeGreaterThan(1);
        GhostMascotPose.Write(GhostMascotStatus.Error, pose);
        pose[GhostMascotPose.Droop].ShouldBe(1);
        pose[GhostMascotPose.Lift].ShouldBeGreaterThan(0);
        GhostMascotPose.Write(GhostMascotStatus.Idle, pose);
        pose[GhostMascotPose.Blink].ShouldBe(1);
        pose[GhostMascotPose.Glance].ShouldBe(1);
    }
}
