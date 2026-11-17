using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// AiSpark: sizes per variant, status and self-play, the pill's labels and shimmer, the accessible name and live region,
/// the pose math (seamless loops, gather, shake, weight hinting) and the mark's motion driven frame by frame: blends that
/// start exactly where the mark is, the done gather and spin glide, the error shake and tone, reduced and no motion, the
/// particles and the loop that stops once a resting status has settled.
/// </summary>
public class AiSparkTests
{
    private static (AiSpark Spark, AiSparkMark Mark, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off,
        Action<AiSpark>? setup = null)
    {
        var spark = new AiSpark();
        setup?.Invoke(spark);
        var window = TestHelpers.Show(spark, level);
        Dispatcher.UIThread.RunJobs();
        return (spark, spark.Mark!, window);
    }

    private static double[] Snapshot(AiSparkMark mark) => mark.Pose.ToArray();

    private static double MaxDifference(double[] a, double[] b)
    {
        var max = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            max = Math.Max(max, Math.Abs(a[i] - b[i]));
        }
        return max;
    }

    private static void Run(AiSparkMark mark, double seconds, double frame = 1.0 / 60)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += frame)
        {
            mark.Advance(frame);
        }
    }

    [AvaloniaFact]
    public void Defaults_SelfPlayIdle_Accent_24px()
    {
        var (spark, mark, _) = Open();
        spark.Status.ShouldBeNull();
        spark.EffectiveStatus.ShouldBe(AiSparkStatus.Idle);
        mark.IsSelfPlaying.ShouldBeTrue();
        spark.Variant.ShouldBe(AiSparkVariant.Mark);
        spark.Tone.ShouldBe(AiSparkTone.Accent);
        spark.Speed.ShouldBe(1);
        spark.Twinkle.ShouldBeTrue();
        spark.Announce.ShouldBeTrue();
        mark.Bounds.Width.ShouldBe(24);
        mark.Bounds.Height.ShouldBe(24);
        spark.Classes.Contains(":idle").ShouldBeTrue();
        spark.Classes.Contains(":mark").ShouldBeTrue();
        spark.Focusable.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Sizes_FollowVariantAndSize()
    {
        var (spark, mark, _) = Open(setup: s => s.Variant = AiSparkVariant.Avatar);
        spark.ResolvedSize.ShouldBe(32);
        mark.IsTile.ShouldBeTrue();
        spark.Variant = AiSparkVariant.Pill;
        Dispatcher.UIThread.RunJobs();
        spark.ResolvedSize.ShouldBe(16);
        mark.IsTile.ShouldBeFalse();
        spark.Size = 96;
        Dispatcher.UIThread.RunJobs();
        mark.Width.ShouldBe(96);
        spark.Classes.Contains(":pill").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Pill_Is28High_WithLabelForEachStatus()
    {
        var (spark, _, _) = Open(setup: s =>
        {
            s.Variant = AiSparkVariant.Pill;
            s.Status = AiSparkStatus.Idle;
        });
        var frame = TestHelpers.Descendant<Border>(spark, AiSpark.FramePart);
        frame.Bounds.Height.ShouldBe(28);
        var expected = new Dictionary<AiSparkStatus, string>
        {
            [AiSparkStatus.Idle] = "Ready",
            [AiSparkStatus.Thinking] = "Thinking",
            [AiSparkStatus.Generating] = "Writing",
            [AiSparkStatus.Done] = "Done",
            [AiSparkStatus.Error] = "Couldn't finish"
        };
        foreach (var (status, text) in expected)
        {
            spark.Status = status;
            Dispatcher.UIThread.RunJobs();
            spark.LabelText.ShouldBe(text);
            spark.LabelTextPart!.Text.ShouldBe(text);
            spark.LabelTextPart.IsShimmering.ShouldBe(status is AiSparkStatus.Thinking or AiSparkStatus.Generating);
        }
    }

    [AvaloniaFact]
    public void Pill_CustomLabel_KeepsItsText_AndShimmersOnlyWhileBusy()
    {
        var (spark, _, _) = Open(setup: s =>
        {
            s.Variant = AiSparkVariant.Pill;
            s.Status = AiSparkStatus.Thinking;
            s.Label = "Searching 4 sources";
        });
        var first = spark.LabelTextPart!;
        first.Text.ShouldBe("Searching 4 sources");
        first.IsShimmering.ShouldBeTrue();
        spark.Status = AiSparkStatus.Done;
        Dispatcher.UIThread.RunJobs();
        // The same text is not swapped out; it only stops shimmering.
        spark.LabelTextPart.ShouldBeSameAs(first);
        first.IsShimmering.ShouldBeFalse();
        AutomationProperties.GetName(spark).ShouldBe("Searching 4 sources");
    }

    [AvaloniaFact]
    public void Labels_CanBeReplaced()
    {
        var (spark, _, _) = Open(setup: s =>
        {
            s.Variant = AiSparkVariant.Pill;
            s.Status = AiSparkStatus.Generating;
        });
        spark.Labels = new AiSparkLabels { Writing = "Rédaction", WritingPhrase = "L'IA rédige" };
        Dispatcher.UIThread.RunJobs();
        spark.LabelTextPart!.Text.ShouldBe("Rédaction");
        spark.Variant = AiSparkVariant.Mark;
        AutomationProperties.GetName(spark).ShouldBe("L'IA rédige");
    }

    [AvaloniaFact]
    public void Status_SetsEffectiveStatusAndPseudoClasses()
    {
        var (spark, mark, _) = Open(setup: s => s.Status = AiSparkStatus.Thinking);
        spark.EffectiveStatus.ShouldBe(AiSparkStatus.Thinking);
        mark.IsSelfPlaying.ShouldBeFalse();
        spark.Classes.Contains(":thinking").ShouldBeTrue();
        spark.Classes.Contains(":busy").ShouldBeTrue();
        spark.Status = AiSparkStatus.Error;
        spark.EffectiveStatus.ShouldBe(AiSparkStatus.Error);
        spark.Classes.Contains(":error").ShouldBeTrue();
        spark.Classes.Contains(":busy").ShouldBeFalse();
        spark.Classes.Contains(":thinking").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Accessibility_LiveRegionWhenAnnouncing_ImageOtherwise()
    {
        var (spark, _, _) = Open(setup: s => s.Status = AiSparkStatus.Thinking);
        AutomationProperties.GetLiveSetting(spark).ShouldBe(AutomationLiveSetting.Polite);
        AutomationProperties.GetName(spark).ShouldBe("AI is thinking");
        spark.Status = AiSparkStatus.Generating;
        AutomationProperties.GetName(spark).ShouldBe("AI is writing");

        spark.Announce = false;
        AutomationProperties.GetLiveSetting(spark).ShouldBe(AutomationLiveSetting.Off);
        AutomationProperties.GetControlTypeOverride(spark).ShouldBe(AutomationControlType.Image);
        AutomationProperties.GetName(spark).ShouldBe("AI is writing");

        spark.AccessibleName = "Assistant";
        AutomationProperties.GetName(spark).ShouldBe("Assistant");
    }

    [AvaloniaFact]
    public void Accessibility_SelfPlayNeverSpeaks()
    {
        var (spark, _, _) = Open();
        AutomationProperties.GetLiveSetting(spark).ShouldBe(AutomationLiveSetting.Off);
        AutomationProperties.GetName(spark).ShouldBe("AI is ready");
    }

    [AvaloniaFact]
    public void SelfPlay_StepsThroughTheSequence()
    {
        var (spark, mark, _) = Open(ArcMotionLevel.Full);
        Run(mark, 2.5);
        spark.EffectiveStatus.ShouldBe(AiSparkStatus.Thinking);
        Run(mark, 3.6);
        spark.EffectiveStatus.ShouldBe(AiSparkStatus.Generating);
        Run(mark, 4.2);
        spark.EffectiveStatus.ShouldBe(AiSparkStatus.Done);
        Run(mark, 2.4);
        spark.EffectiveStatus.ShouldBe(AiSparkStatus.Idle);
        // Setting a status takes over; clearing it starts again from idle.
        spark.Status = AiSparkStatus.Error;
        Run(mark, 5);
        spark.EffectiveStatus.ShouldBe(AiSparkStatus.Error);
        spark.Status = null;
        spark.EffectiveStatus.ShouldBe(AiSparkStatus.Idle);
    }

    [AvaloniaFact]
    public void Pose_LoopsAreSeamless()
    {
        Span<double> a = stackalloc double[AiSparkPose.Count];
        Span<double> b = stackalloc double[AiSparkPose.Count];
        foreach (var (status, period) in new[]
                 {
                     (AiSparkStatus.Idle, AiSparkPose.IdlePeriod),
                     (AiSparkStatus.Thinking, AiSparkPose.ThinkingPeriod),
                     (AiSparkStatus.Generating, AiSparkPose.BeatPeriod)
                 })
        {
            foreach (var t in new[] { 0.0, 0.37, 1.21 })
            {
                AiSparkPose.Write(status, true, false, t, a);
                AiSparkPose.Write(status, true, false, t + period * 3, b);
                for (var i = 0; i < AiSparkPose.Count; i++)
                {
                    b[i].ShouldBe(a[i], 1e-9);
                }
            }
        }
    }

    [AvaloniaFact]
    public void Pose_ThinkingCrestTravelsAroundTheRing()
    {
        Span<double> pose = stackalloc double[AiSparkPose.Count];
        // At t = 0 the crest is at 12 o'clock; a quarter turn later it has reached the 3 o'clock ray.
        AiSparkPose.Write(AiSparkStatus.Thinking, true, false, 0, pose);
        pose[AiSparkPose.Primary].ShouldBeGreaterThan(pose[AiSparkPose.Primary + 1]);
        AiSparkPose.Write(AiSparkStatus.Thinking, true, false, AiSparkPose.ThinkingPeriod / 4, pose);
        pose[AiSparkPose.Primary + 1].ShouldBeGreaterThan(pose[AiSparkPose.Primary]);
    }

    [AvaloniaFact]
    public void Pose_HeartbeatFiresTheVerticalAxisFirst()
    {
        Span<double> pose = stackalloc double[AiSparkPose.Count];
        AiSparkPose.Write(AiSparkStatus.Generating, true, false, 0, pose);
        pose[AiSparkPose.Primary].ShouldBeGreaterThan(pose[AiSparkPose.Primary + 1]);
        // The dub is weaker than the lub.
        AiSparkPose.Beat(0.17, 0).ShouldBeLessThan(AiSparkPose.Beat(0, 0));
        AiSparkPose.Beat(0.17, 0).ShouldBeGreaterThan(0.5);
    }

    [AvaloniaFact]
    public void Pose_StillPosesDoNotMove_AndErrorIsShorter()
    {
        Span<double> a = stackalloc double[AiSparkPose.Count];
        Span<double> b = stackalloc double[AiSparkPose.Count];
        foreach (var status in Enum.GetValues<AiSparkStatus>())
        {
            AiSparkPose.Write(status, true, true, 0, a);
            AiSparkPose.Write(status, true, true, 2.3, b);
            a.ToArray().ShouldBe(b.ToArray());
        }
        AiSparkPose.Write(AiSparkStatus.Error, true, true, 0, a);
        a[AiSparkPose.Primary].ShouldBeLessThan(1);
        a[AiSparkPose.DiagonalTone].ShouldBeLessThan(1);
        AiSparkPose.Write(AiSparkStatus.Idle, false, false, 1.7, b);
        b.ToArray().ShouldAllBe(v => v == 1);
    }

    [AvaloniaFact]
    public void Pose_GatherAndShakeEnvelopes()
    {
        AiSparkPose.Gather(0).ShouldBe(0);
        AiSparkPose.Gather(AiSparkPose.GatherIn).ShouldBe(1, 1e-9);
        AiSparkPose.Gather(AiSparkPose.GatherIn + AiSparkPose.GatherOut).ShouldBe(0);
        // Back to rest without overshoot: never negative, only falling after the peak.
        var last = 1.0;
        for (var t = AiSparkPose.GatherIn; t < AiSparkPose.GatherIn + AiSparkPose.GatherOut; t += 0.01)
        {
            var g = AiSparkPose.Gather(t);
            g.ShouldBeGreaterThanOrEqualTo(0);
            g.ShouldBeLessThanOrEqualTo(last + 1e-12);
            last = g;
        }
        AiSparkPose.Shake(0.05).ShouldNotBe(0);
        AiSparkPose.Shake(AiSparkPose.ShakeDuration).ShouldBe(0);
        Math.Abs(AiSparkPose.Shake(0.4)).ShouldBeLessThan(Math.Abs(AiSparkPose.Shake(0.05)));
    }

    [AvaloniaFact]
    public void Weight_IsHeavierForSmallMarks()
    {
        AiSparkPose.Weight(32).ShouldBe(1, 1e-9);
        AiSparkPose.Weight(16).ShouldBeGreaterThan(AiSparkPose.Weight(24));
        AiSparkPose.Weight(12).ShouldBeLessThanOrEqualTo(1.5);
        AiSparkPose.Weight(120).ShouldBeGreaterThanOrEqualTo(0.92);
    }

    [AvaloniaFact]
    public void Blend_StartsExactlyWhereTheMarkIs()
    {
        var (spark, mark, _) = Open(ArcMotionLevel.Full, s => s.Status = AiSparkStatus.Thinking);
        Run(mark, 1.3);
        var before = Snapshot(mark);
        spark.Status = AiSparkStatus.Generating;
        mark.IsBlending.ShouldBeTrue();
        MaxDifference(Snapshot(mark), before).ShouldBeLessThan(1e-9);
        // One frame later it has moved only a little: no jump.
        mark.Advance(1.0 / 60);
        MaxDifference(Snapshot(mark), before).ShouldBeLessThan(0.05);
        // An interruption mid-blend starts from the blended pose too.
        Run(mark, 0.15);
        var mid = Snapshot(mark);
        spark.Status = AiSparkStatus.Idle;
        MaxDifference(Snapshot(mark), mid).ShouldBeLessThan(1e-9);
        Run(mark, 1.2);
        mark.IsBlending.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Done_GathersThenSettles_AndTheSpinGlidesHomeWithFallingSpeed()
    {
        var (spark, mark, _) = Open(ArcMotionLevel.Full, s => s.Status = AiSparkStatus.Generating);
        Run(mark, 1.1);
        mark.SpinSpeedNow.ShouldBeGreaterThan(40);
        spark.Status = AiSparkStatus.Done;
        var last = mark.SpinSpeedNow;
        var peak = 0.0;
        for (var i = 0; i < 360; i++)
        {
            mark.Advance(1.0 / 60);
            peak = Math.Max(peak, mark.GatherNow);
            mark.SpinSpeedNow.ShouldBeLessThanOrEqualTo(last + 1e-9);
            last = mark.SpinSpeedNow;
        }
        peak.ShouldBeGreaterThan(0.95);
        mark.GatherNow.ShouldBe(0);
        mark.SpinSpeedNow.ShouldBe(0);
        (mark.Angle % 90).ShouldBe(0, 1e-6);
        Run(mark, 1);
        mark.NeedsFrames().ShouldBeFalse();
        Snapshot(mark).ShouldAllBe(v => Math.Abs(v - 1) < 1e-3);
    }

    [AvaloniaFact]
    public void Error_ShakesOnce_AndEasesToTheMutedDangerTone()
    {
        var (spark, mark, _) = Open(ArcMotionLevel.Full, s => s.Status = AiSparkStatus.Idle);
        var accent = mark.RayColor;
        spark.Status = AiSparkStatus.Error;
        mark.Advance(0.05);
        mark.ShakeNow.ShouldNotBe(0);
        mark.ErrorMix.ShouldBeInRange(0.01, 0.99);
        Run(mark, 0.5);
        mark.ShakeNow.ShouldBe(0);
        mark.ErrorMix.ShouldBe(1);
        mark.RayColor.ShouldNotBe(accent);
        Run(mark, 1);
        mark.NeedsFrames().ShouldBeFalse();
        mark.Pose[AiSparkPose.Primary].ShouldBeLessThan(0.8);
        // Recovering eases the tone back.
        spark.Status = AiSparkStatus.Idle;
        Run(mark, 0.5);
        mark.ErrorMix.ShouldBe(0);
        mark.RayColor.ShouldBe(accent);
    }

    [AvaloniaFact]
    public void Loop_RunsWhileAnimating_StopsWhenPausedOrStill()
    {
        var (spark, mark, _) = Open(ArcMotionLevel.Full, s => s.Status = AiSparkStatus.Thinking);
        mark.NeedsFrames().ShouldBeTrue();
        mark.IsLoopActive.ShouldBeTrue();
        spark.IsPaused = true;
        mark.IsLoopActive.ShouldBeFalse();
        spark.IsPaused = false;
        mark.IsLoopActive.ShouldBeTrue();

        spark.Twinkle = false;
        spark.Status = AiSparkStatus.Idle;
        // The spin glides home (at most 3s), then nothing moves.
        Run(mark, 4);
        mark.NeedsFrames().ShouldBeFalse();
        spark.Twinkle = true;
        mark.NeedsFrames().ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Paused_FreezesThePhase()
    {
        var (spark, mark, _) = Open(ArcMotionLevel.Full, s => s.Status = AiSparkStatus.Thinking);
        Run(mark, 0.7);
        var angle = mark.Angle;
        spark.IsPaused = true;
        mark.IsLoopActive.ShouldBeFalse();
        // No frames arrive while paused; the angle and pose stay where they were.
        mark.Angle.ShouldBe(angle);
    }

    [AvaloniaFact]
    public void Speed_MultipliesTheSpin()
    {
        var (_, slow, _) = Open(ArcMotionLevel.Full, s => s.Status = AiSparkStatus.Thinking);
        var (_, fast, _) = Open(ArcMotionLevel.Full, s =>
        {
            s.Status = AiSparkStatus.Thinking;
            s.Speed = 2;
        });
        Run(slow, 2);
        Run(fast, 2);
        slow.SpinSpeedNow.ShouldBe(AiSparkPose.ThinkingSpin, 1);
        fast.SpinSpeedNow.ShouldBe(AiSparkPose.ThinkingSpin * 2, 2);
    }

    [AvaloniaFact]
    public void Particles_ShowWhileWritingFrom40px()
    {
        var (_, big, _) = Open(ArcMotionLevel.Full, s =>
        {
            s.Status = AiSparkStatus.Generating;
            s.Size = 48;
        });
        Run(big, 0.5);
        big.ParticleOpacity.ShouldBe(1);
        var (_, small, _) = Open(ArcMotionLevel.Full, s =>
        {
            s.Status = AiSparkStatus.Generating;
            s.Size = 24;
        });
        Run(small, 0.5);
        small.ParticleOpacity.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Reduced_StillPoses_NoSpin_200msBlend()
    {
        var (spark, mark, _) = Open(ArcMotionLevel.Reduced, s => s.Status = AiSparkStatus.Idle);
        spark.Status = AiSparkStatus.Generating;
        Run(mark, 1);
        mark.Angle.ShouldBe(0);
        mark.ParticleOpacity.ShouldBe(0);
        var settled = Snapshot(mark);
        Run(mark, 0.5);
        Snapshot(mark).ShouldBe(settled);
        mark.NeedsFrames().ShouldBeFalse();

        spark.Status = AiSparkStatus.Error;
        mark.Advance(0.1);
        mark.ShakeNow.ShouldBe(0);
        mark.IsBlending.ShouldBeTrue();
        mark.Advance(0.11);
        mark.IsBlending.ShouldBeFalse();
        mark.ErrorMix.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Off_ChangesAtOnce()
    {
        var (spark, mark, _) = Open(ArcMotionLevel.Off, s => s.Status = AiSparkStatus.Idle);
        spark.Status = AiSparkStatus.Error;
        mark.IsBlending.ShouldBeFalse();
        mark.ErrorMix.ShouldBe(1);
        mark.Pose[AiSparkPose.Primary].ShouldBe(0.74, 1e-9);
        mark.NeedsFrames().ShouldBeFalse();
        mark.IsLoopActive.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Tones_PickTheirInk_AndMarkBrushOverrides()
    {
        var (spark, mark, _) = Open(setup: s => s.Status = AiSparkStatus.Idle);
        mark.RayColor.ShouldBe(TestHelpers.ColorOf(mark.AccentInk));
        spark.Tone = AiSparkTone.Muted;
        mark.RayColor.ShouldBe(TestHelpers.ColorOf(mark.MutedInk));
        spark.Tone = AiSparkTone.Inherit;
        spark.Foreground = new SolidColorBrush(Color.FromRgb(10, 20, 30));
        Dispatcher.UIThread.RunJobs();
        mark.RayColor.ShouldBe(Color.FromRgb(10, 20, 30));
        spark.MarkBrush = new SolidColorBrush(Color.FromRgb(200, 100, 0));
        mark.RayColor.ShouldBe(Color.FromRgb(200, 100, 0));
    }

    [AvaloniaFact]
    public void Avatar_DrawsATintedTile()
    {
        var (_, mark, _) = Open(setup: s =>
        {
            s.Variant = AiSparkVariant.Avatar;
            s.Status = AiSparkStatus.Idle;
        });
        mark.IsTile.ShouldBeTrue();
        mark.TileColor.A.ShouldBeLessThan((byte)80);
        mark.TileColor.A.ShouldBeGreaterThan((byte)0);
    }
}
