using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// CubeMascot: size and the compact drawing, the accessible label and live region, self-play and its event, the pose math
/// (blinks, shake, dip, printed lines, face visibility), the 3D projection (hull, visible faces, shading order) and the
/// motion driven frame by frame: blends that start from the frame on screen, the thinking quarter turns that keep turning
/// forward, the writing lines, the error shake, reduced motion, pausing and the loop that stops once an error settles.
/// </summary>
public class CubeMascotTests
{
    private static (CubeMascot Cube, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off, Action<CubeMascot>? setup = null)
    {
        var cube = new CubeMascot();
        setup?.Invoke(cube);
        var window = TestHelpers.Show(cube, level);
        Dispatcher.UIThread.RunJobs();
        return (cube, window);
    }

    private static void Run(CubeMascot cube, double seconds, double frame = 1.0 / 60)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += frame)
        {
            cube.Advance(frame);
        }
    }

    private static double MaxDifference(double[] a, ReadOnlySpan<double> b)
    {
        var max = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            max = Math.Max(max, Math.Abs(a[i] - b[i]));
        }
        return max;
    }

    [AvaloniaFact]
    public void Defaults_SelfPlayIdle_96px_NoFocus()
    {
        var (cube, _) = Open();
        cube.Status.ShouldBeNull();
        cube.EffectiveStatus.ShouldBe(CubeMascotStatus.Idle);
        cube.IsSelfPlaying.ShouldBeTrue();
        cube.Size.ShouldBe(96);
        cube.ShowsGround.ShouldBeTrue();
        cube.Announce.ShouldBeFalse();
        cube.IsPaused.ShouldBeFalse();
        cube.AssistantName.ShouldBe("Assistant");
        cube.Bounds.Width.ShouldBe(96);
        cube.Bounds.Height.ShouldBe(96);
        cube.Focusable.ShouldBeFalse();
        cube.IsCompact.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Size_IsAFixedSquare_AndCompactBelow56()
    {
        var (cube, _) = Open(setup: c => c.Size = 24);
        cube.Bounds.Width.ShouldBe(24);
        cube.Bounds.Height.ShouldBe(24);
        cube.IsCompact.ShouldBeTrue();
        cube.Size = 56;
        Dispatcher.UIThread.RunJobs();
        cube.Bounds.Width.ShouldBe(56);
        cube.IsCompact.ShouldBeFalse();
        cube.Size = double.NaN;
        cube.Size.ShouldBe(96);
    }

    [AvaloniaFact]
    public void Theme_GivesAccentBodyAndForegroundFace()
    {
        var (cube, _) = Open();
        cube.BodyBrush.ShouldNotBeNull();
        cube.FaceBrush.ShouldNotBeNull();
        cube.LineBrush.ShouldNotBeNull();
        TestHelpers.ColorOf(cube.BodyBrush).ShouldBe(ArcTokens.GetColor(cube, ArcDesignTokenKind.Accent));
        cube.InkColor.ShouldBe(ArcTokens.GetColor(cube, ArcDesignTokenKind.AccentForeground));
    }

    [AvaloniaFact]
    public void Shading_TopIsLightest_SideIsDarkest_FrontIsTheBody()
    {
        var (cube, _) = Open(setup: c =>
        {
            c.Status = CubeMascotStatus.Error;
            c.BodyBrush = new SolidColorBrush(Color.Parse("#4F6BFF"));
        });
        var scene = cube.Scene;
        scene.Facing[CubeMascotScene.Top].ShouldBeGreaterThan(0);
        scene.Facing[CubeMascotScene.Front].ShouldBeGreaterThan(0);
        scene.Facing[CubeMascotScene.Right].ShouldBeGreaterThan(0);
        scene.Facing[CubeMascotScene.Left].ShouldBeLessThan(0);
        scene.Facing[CubeMascotScene.Back].ShouldBeLessThan(0);
        var top = Oklch.FromColor(cube.FaceColor(CubeMascotScene.Top)).L;
        var front = Oklch.FromColor(cube.FaceColor(CubeMascotScene.Front)).L;
        var side = Oklch.FromColor(cube.FaceColor(CubeMascotScene.Right)).L;
        top.ShouldBeGreaterThan(front);
        front.ShouldBeGreaterThan(side);
        // Error stands still, so the front face sits exactly on its anchor: the body color.
        cube.FaceColor(CubeMascotScene.Front).ShouldBe(Color.Parse("#4F6BFF"));
        cube.SideBrush = Brushes.Black;
        cube.FaceColor(CubeMascotScene.Right).ShouldBe(Colors.Black);
    }

    [AvaloniaFact]
    public void Scene_SilhouetteIsAHexagon_AndOnlyTheFrontSideCarriesTheFace()
    {
        var scene = new CubeMascotScene();
        scene.Update(CubeMascotPose.HomeYaw, 0, 0, 0, 0, 40, new Avalonia.Point(50, 50));
        scene.HullCount.ShouldBe(6);
        scene.SideAngles[CubeMascotScene.Front].ShouldBe(0, 1e-9);
        scene.SideAngles[CubeMascotScene.Left].ShouldBe(-90, 1e-9);
        scene.SideAngles[CubeMascotScene.Right].ShouldBe(90, 1e-9);
        CubeMascotPose.FaceVisibility(scene.SideAngles[CubeMascotScene.Front]).ShouldBe(1);
        CubeMascotPose.FaceVisibility(scene.SideAngles[CubeMascotScene.Right]).ShouldBe(0);
        CubeMascotPose.FaceVisibility(scene.SideAngles[CubeMascotScene.Left]).ShouldBe(0);
        // A quarter turn later the left side is in front: same silhouette, the face moved over.
        scene.Update(CubeMascotPose.HomeYaw + 90, 0, 0, 0, 0, 40, new Avalonia.Point(50, 50));
        scene.HullCount.ShouldBe(6);
        scene.SideAngles[CubeMascotScene.Left].ShouldBe(0, 1e-9);
        CubeMascotPose.FaceVisibility(scene.SideAngles[CubeMascotScene.Left]).ShouldBe(1);
        CubeMascotPose.FaceVisibility(scene.SideAngles[CubeMascotScene.Front]).ShouldBe(0);
        // Face on, the silhouette is a 4-sided outline.
        scene.Update(0, 0, -CubeMascotPose.CameraPitch, 0, 0, 40, new Avalonia.Point(50, 50));
        scene.HullCount.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Label_IsNameAndStatus_AndFollowsOverrides()
    {
        var (cube, _) = Open(setup: c =>
        {
            c.Status = CubeMascotStatus.Thinking;
            c.AssistantName = "Atlas";
        });
        AutomationProperties.GetName(cube).ShouldBe("Atlas, thinking");
        AutomationProperties.GetControlTypeOverride(cube).ShouldBe(AutomationControlType.Image);
        AutomationProperties.GetLiveSetting(cube).ShouldBe(AutomationLiveSetting.Off);
        cube.Status = CubeMascotStatus.Writing;
        AutomationProperties.GetName(cube).ShouldBe("Atlas, writing");
        cube.Announce = true;
        AutomationProperties.GetLiveSetting(cube).ShouldBe(AutomationLiveSetting.Polite);
        cube.Labels = new CubeMascotLabels { Writing = "schreibt", LabelFormat = "{0} {1}" };
        AutomationProperties.GetName(cube).ShouldBe("Atlas schreibt");
        cube.AccessibleName = "Drafting your reply";
        AutomationProperties.GetName(cube).ShouldBe("Drafting your reply");
    }

    [AvaloniaFact]
    public void Labels_WordsAndBrokenFormat()
    {
        var labels = new CubeMascotLabels();
        labels.WordFor(CubeMascotStatus.Idle).ShouldBe("idle");
        labels.WordFor(CubeMascotStatus.Done).ShouldBe("done");
        labels.WordFor(CubeMascotStatus.Error).ShouldBe("error");
        labels.LabelFor("Atlas", CubeMascotStatus.Done).ShouldBe("Atlas, done");
        labels.LabelFormat = "{0} {5}";
        labels.LabelFor("Atlas", CubeMascotStatus.Done).ShouldBe("Atlas, done");
    }

    [AvaloniaFact]
    public void SelfPlay_StepsThroughEveryState_AndRaisesStatusChanged()
    {
        var (cube, _) = Open(ArcMotionLevel.Full);
        var seen = new List<CubeMascotStatus>();
        cube.StatusChanged += (_, e) => seen.Add(e.Status);
        Run(cube, 2.7);
        cube.EffectiveStatus.ShouldBe(CubeMascotStatus.Thinking);
        Run(cube, 3.9 + 4.32 + 2.6 + 1.8);
        seen.ShouldBe([CubeMascotStatus.Thinking, CubeMascotStatus.Writing, CubeMascotStatus.Done, CubeMascotStatus.Error,
            CubeMascotStatus.Idle]);
        AutomationProperties.GetName(cube).ShouldBe("Assistant, idle");
    }

    [AvaloniaFact]
    public void SettingStatus_DoesNotRaiseStatusChanged()
    {
        var (cube, _) = Open(ArcMotionLevel.Full);
        var raised = 0;
        cube.StatusChanged += (_, _) => raised++;
        cube.Status = CubeMascotStatus.Writing;
        Run(cube, 6);
        raised.ShouldBe(0);
        cube.EffectiveStatus.ShouldBe(CubeMascotStatus.Writing);
    }

    [AvaloniaFact]
    public void StatusChange_BlendsFromTheFrameOnScreen_Over420ms()
    {
        var (cube, _) = Open(ArcMotionLevel.Full, c => c.Status = CubeMascotStatus.Idle);
        Run(cube, 1.3);
        var before = cube.Pose.ToArray();
        cube.Status = CubeMascotStatus.Writing;
        cube.IsBlending.ShouldBeTrue();
        // The first frame is the pose that was on screen: no jump.
        MaxDifference(before, cube.Pose).ShouldBeLessThan(1e-9);
        cube.Advance(1.0 / 60);
        MaxDifference(before, cube.Pose).ShouldBeLessThan(0.05);
        Run(cube, 0.2);
        cube.IsBlending.ShouldBeTrue();
        cube.Pose[CubeMascotPose.Pitch].ShouldBeGreaterThan(2);
        cube.Pose[CubeMascotPose.Pitch].ShouldBeLessThan(9);
        Run(cube, 0.25);
        cube.IsBlending.ShouldBeFalse();
        cube.Pose[CubeMascotPose.Ink].ShouldBe(1);
        cube.Pose[CubeMascotPose.Pitch].ShouldBeGreaterThanOrEqualTo(9);
    }

    [AvaloniaFact]
    public void Thinking_HoldsThenTurnsAQuarterTurn_AndTheFaceMovesToTheArrivingSide()
    {
        var (cube, _) = Open(ArcMotionLevel.Full, c => c.Status = CubeMascotStatus.Idle);
        cube.Status = CubeMascotStatus.Thinking;
        Run(cube, 0.45);
        cube.IsTurning.ShouldBeFalse();
        cube.SideVisibility[CubeMascotScene.Front].ShouldBeGreaterThan(0.9);
        Run(cube, 0.1);
        cube.IsTurning.ShouldBeTrue();
        Run(cube, 0.35);
        // Mid turn: rising and leaning, the leaving face fading, the arriving one not yet there.
        cube.Pose[CubeMascotPose.Lift].ShouldBeGreaterThan(0.05);
        cube.Pose[CubeMascotPose.Roll].ShouldBeLessThan(-4);
        cube.Yaw.ShouldBe(CubeMascotPose.HomeYaw + 45, 8);
        Run(cube, 0.5);
        cube.IsTurning.ShouldBeFalse();
        cube.Yaw.ShouldBe(CubeMascotPose.HomeYaw + 90, 1e-6);
        cube.SideVisibility[CubeMascotScene.Left].ShouldBeGreaterThan(0.9);
        cube.SideVisibility[CubeMascotScene.Front].ShouldBe(0);
        // The next turn comes after another 0.5s hold.
        Run(cube, 0.35);
        cube.IsTurning.ShouldBeFalse();
        Run(cube, 0.15);
        cube.IsTurning.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void LeavingThinkingMidTurn_KeepsTurningForward()
    {
        var (cube, _) = Open(ArcMotionLevel.Full, c => c.Status = CubeMascotStatus.Thinking);
        Run(cube, 0.9);
        cube.IsTurning.ShouldBeTrue();
        var yaw = cube.Yaw;
        cube.Status = CubeMascotStatus.Done;
        var last = yaw;
        for (var i = 0; i < 40; i++)
        {
            cube.Advance(1.0 / 60);
            if (cube.IsTurning)
            {
                cube.Yaw.ShouldBeGreaterThanOrEqualTo(last - 1e-9);
                last = cube.Yaw;
            }
        }
        cube.IsTurning.ShouldBeFalse();
        cube.Yaw.ShouldBe(CubeMascotPose.HomeYaw + 90, 1e-6);
        // No new turn starts outside thinking.
        Run(cube, 2);
        cube.IsTurning.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Writing_PrintsLinesThatFadeInAndOut()
    {
        var (cube, _) = Open(ArcMotionLevel.Full, c => c.Status = CubeMascotStatus.Writing);
        Run(cube, 1.6);
        var lines = cube.LineOpacities.ToArray();
        lines.Count(o => o > 0.05).ShouldBeGreaterThanOrEqualTo(2);
        lines.ShouldAllBe(o => o <= 0.7 + 1e-9);
        cube.ShowsGround = false;
        cube.LineOpacities.ToArray().ShouldAllBe(o => o == 0);
    }

    [AvaloniaFact]
    public void Compact_DrawsNoLines()
    {
        var (cube, _) = Open(ArcMotionLevel.Full, c =>
        {
            c.Status = CubeMascotStatus.Writing;
            c.Size = 40;
        });
        Run(cube, 1.6);
        cube.IsCompact.ShouldBeTrue();
        cube.LineOpacities.ToArray().ShouldAllBe(o => o == 0);
    }

    [AvaloniaFact]
    public void Error_ShakesOnce_ThenTheLoopStops()
    {
        var (cube, _) = Open(ArcMotionLevel.Full, c => c.Status = CubeMascotStatus.Idle);
        cube.Status = CubeMascotStatus.Error;
        cube.Advance(0.05);
        Math.Abs(cube.ShakeNow).ShouldBeGreaterThan(0.01);
        cube.Pose[CubeMascotPose.Brow].ShouldBeGreaterThan(0);
        Run(cube, 0.6);
        cube.ShakeNow.ShouldBe(0);
        cube.IsBlending.ShouldBeFalse();
        cube.Pose[CubeMascotPose.Brow].ShouldBe(1);
        cube.Pose[CubeMascotPose.Mouth].ShouldBeLessThan(0);
        cube.NeedsFrames().ShouldBeFalse();
        cube.Advance(1.0 / 60).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Done_DipsAndSquints_WithASparkle()
    {
        var (cube, _) = Open(ArcMotionLevel.Full, c => c.Status = CubeMascotStatus.Idle);
        cube.Status = CubeMascotStatus.Done;
        Run(cube, 0.2);
        cube.DipNow.ShouldBeLessThan(0);
        Run(cube, 0.8);
        cube.DipNow.ShouldBe(0);
        cube.Pose[CubeMascotPose.EyeOpen].ShouldBe(0, 1e-9);
        cube.Pose[CubeMascotPose.Squint].ShouldBe(1, 1e-9);
        cube.Pose[CubeMascotPose.Sparkle].ShouldBe(1, 1e-9);
    }

    [AvaloniaFact]
    public void ReducedMotion_StillPosesSwitchInPlace_NothingLoops()
    {
        var (cube, _) = Open(ArcMotionLevel.Reduced, c => c.Status = CubeMascotStatus.Idle);
        cube.IsLooping.ShouldBeFalse();
        cube.NeedsFrames().ShouldBeFalse();
        cube.Status = CubeMascotStatus.Done;
        cube.IsBlending.ShouldBeFalse();
        cube.Pose[CubeMascotPose.Squint].ShouldBe(1);
        cube.Pose[CubeMascotPose.Sparkle].ShouldBe(1);
        cube.Status = CubeMascotStatus.Thinking;
        cube.Yaw.ShouldBe(CubeMascotPose.HomeYaw);
        cube.Pose[CubeMascotPose.GazeY].ShouldBeLessThan(0);
        cube.ShakeNow.ShouldBe(0);
        cube.Status = CubeMascotStatus.Writing;
        cube.LineOpacities.ToArray().Count(o => o > 0).ShouldBe(3);
    }

    [AvaloniaFact]
    public void Paused_StopsTheLoop_AndKeepsTheFrame()
    {
        var (cube, _) = Open(ArcMotionLevel.Full, c => c.Status = CubeMascotStatus.Idle);
        cube.IsLooping.ShouldBeTrue();
        cube.IsPaused = true;
        cube.IsLooping.ShouldBeFalse();
        cube.IsPaused = false;
        cube.IsLooping.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Pose_PureMath()
    {
        // The shake reaches rest with zero velocity.
        CubeMascotPose.Shake(0).ShouldBe(0);
        CubeMascotPose.Shake(CubeMascotPose.ShakeDuration).ShouldBe(0);
        var h = 1e-4;
        var end = CubeMascotPose.ShakeDuration - h;
        Math.Abs(CubeMascotPose.Shake(end) / h).ShouldBeLessThan(0.01);
        // The dip goes down and comes back without overshoot.
        for (var t = 0.0; t <= CubeMascotPose.DipDuration; t += 0.01)
        {
            CubeMascotPose.Dip(t).ShouldBeLessThanOrEqualTo(0);
        }
        // Blinks take 170ms and close fully in the middle.
        CubeMascotPose.Blink(0.085).ShouldBe(1, 1e-9);
        CubeMascotPose.Blink(0.2).ShouldBe(0);
        // A line is invisible at both ends of its life and changes length only by index.
        CubeMascotPose.Line(0).Opacity.ShouldBe(0);
        CubeMascotPose.Line(CubeMascotPose.LineLife).Opacity.ShouldBe(0);
        CubeMascotPose.Line(0.7).Opacity.ShouldBeGreaterThan(0.9);
        for (var i = 0; i < 40; i++)
        {
            CubeMascotPose.LineLength(i).ShouldBeInRange(0.3, 0.72);
            CubeMascotPose.LineLength(i).ShouldBe(CubeMascotPose.LineLength(i));
        }
        // The quarter turn eases in and out.
        CubeMascotPose.EaseInOutCubic(0).ShouldBe(0);
        CubeMascotPose.EaseInOutCubic(0.5).ShouldBe(0.5, 1e-9);
        CubeMascotPose.EaseInOutCubic(1).ShouldBe(1);
        // The idle sway never hides or closes the face.
        for (var a = -8.0; a <= 8; a += 1)
        {
            CubeMascotPose.FaceVisibility(a).ShouldBe(1);
            CubeMascotPose.FaceEyes(a).ShouldBe(1);
        }
    }

    [AvaloniaFact]
    public void Samples_CoverEveryStatus()
    {
        var statuses = Enumerable.Range(0, CubeMascotSamples.Count).Select(CubeMascotSamples.StatusAt).ToArray();
        statuses.ShouldBe([CubeMascotStatus.Idle, CubeMascotStatus.Thinking, CubeMascotStatus.Writing, CubeMascotStatus.Done,
            CubeMascotStatus.Error]);
        CubeMascotSamples.SecondsAt(1).ShouldBe(3.9);
        CubeMascotSamples.StatusAt(CubeMascotSamples.Count).ShouldBe(CubeMascotStatus.Idle);
    }
}
