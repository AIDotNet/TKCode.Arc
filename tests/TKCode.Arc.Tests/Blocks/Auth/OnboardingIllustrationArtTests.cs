using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

public class OnboardingIllustrationArtTests
{
    private static Window Show(OnboardingIllustration block, ArcMotionLevel level) => OnboardingIllustrationTests.Show(block, level: level);

    private static (double X, double Y) Center(OnboardingArtScene scene, int slot) => (scene.Rects[slot].X, scene.Rects[slot].Y);

    [AvaloniaFact]
    public void Scenes_Use_The_Same_Twelve_Shapes()
    {
        OnboardingScene[] all = [OnboardingScene.Folder, OnboardingScene.Files, OnboardingScene.Team, OnboardingScene.Badge];
        foreach (var scene in all.Select(OnboardingArtScenes.Get))
        {
            scene.Rects.Length.ShouldBe(7);
            scene.Lines.Length.ShouldBe(3);
            scene.Lines.ShouldAllBe(l => l.Length == 4);
            scene.Trail.Length.ShouldBe(6);
            scene.Centers.Length.ShouldBe(7);
            scene.Shapes.Length.ShouldBe(7);
        }
        // Only the badge has the check; the trail shows in the files and team scenes.
        all.Select(s => OnboardingArtScenes.Get(s).HasCheck).ShouldBe([false, false, false, true]);
        all.Select(s => OnboardingArtScenes.Get(s).TrailOpacity).ShouldBe([0.0, 1, 1, 0]);
        // The note's lines are turned with the note (−14°) around its center (62, 42).
        double[] noteLine = [45.513, 33.743, 70.740, 27.453];
        OnboardingArtScenes.Files.Lines[0].Zip(noteLine).ShouldAllBe(p => Math.Abs(p.First - p.Second) < 0.002);
        // The badge's middle ray points straight up, from radius 61 to 72.
        OnboardingArtScenes.Badge.Lines[1].ShouldBe([160, 31, 160, 20]);
        OnboardingArtScenes.Badge.Lines[0][0].ShouldBe(125.012, 0.001);
        // Three waiting pages and the note are hidden in the empty box; three shapes fold away in the badge.
        OnboardingArtScenes.Folder.Rects.Select(r => r.Opacity).ShouldBe([1.0, 1, 0, 0, 0, 0, 1]);
        OnboardingArtScenes.Badge.Rects.Select(r => r.Opacity).ShouldBe([1.0, 1, 1, 0, 0, 0, 1]);
    }

    [AvaloniaFact]
    public void Delay_Tables_Run_Forward_And_Backward()
    {
        Enumerable.Range(0, 7).Select(i => Math.Round(OnboardingArt.RectDelay(i, true), 3)).ShouldBe([0, .03, .06, .09, .12, .15, .18]);
        Enumerable.Range(0, 7).Select(i => Math.Round(OnboardingArt.RectDelay(i, false), 3)).ShouldBe([.18, .15, .12, .09, .06, .03, 0]);
        Enumerable.Range(0, 3).Select(i => OnboardingArt.LineDelay(i, true)).ShouldBe([.12, .16, .2]);
        Enumerable.Range(0, 3).Select(i => OnboardingArt.LineDelay(i, false)).ShouldBe([.2, .16, .12]);
    }

    /// <summary>
    /// Runs frames one by one until <paramref name="seconds"/> of engine time passed since <paramref name="switchAt"/>,
    /// and returns when each probe first changed (engine seconds after the switch; NaN when it never did).
    /// </summary>
    private static double[] FirstChanges(ManualFrameClock clock, OnboardingArt art, double switchAt, double seconds, params Func<double[]>[] probes)
    {
        var start = probes.Select(p => p().ToArray()).ToArray();
        var first = Enumerable.Repeat(double.NaN, probes.Length).ToArray();
        for (var frame = 0; frame < 400 && art.Clock - switchAt < seconds; frame++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60));
            for (var i = 0; i < probes.Length; i++)
            {
                if (double.IsNaN(first[i]) && !probes[i]().SequenceEqual(start[i]))
                {
                    first[i] = art.Clock - switchAt;
                }
            }
        }
        return first;
    }

    private static Func<double[]> CenterProbe(OnboardingArt art, int slot) => () => [art.CenterOf(slot).X, art.CenterOf(slot).Y];

    /// <summary>Starts within one capped frame (34ms) after its delay.</summary>
    private static void StartsAt(double actual, double delay) => actual.ShouldBeInRange(delay - 1e-6, delay + 0.035);

    [AvaloniaFact]
    public void A_Switch_From_Rest_Staggers_The_Shapes_Forward()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            var art = block.Art!;
            clock.Advance(TimeSpan.FromSeconds(1));
            art.IsBusy.ShouldBeFalse();
            block.Next();
            Dispatcher.UIThread.RunJobs();
            art.IsBusy.ShouldBeTrue();
            var switchAt = art.Clock;
            var first = FirstChanges(clock, art, switchAt, 0.3,
                CenterProbe(art, 2), CenterProbe(art, 3), CenterProbe(art, 4), CenterProbe(art, 5),
                () => art.LineOf(0), () => art.LineOf(1), () => art.LineOf(2),
                () => art.TrailPoints, () => [art.TrailOpacity], () => [art.OpacityOf(2)]);
            // C, D, E, F 30ms apart from 60ms (A and B do not change); the trail's curve at 100ms, its dots at 160ms;
            // the lines at 120, 160 and 200ms; C fades in with its move.
            StartsAt(first[0], .06);
            StartsAt(first[1], .09);
            StartsAt(first[2], .12);
            StartsAt(first[3], .15);
            StartsAt(first[4], .12);
            StartsAt(first[5], .16);
            StartsAt(first[6], .2);
            StartsAt(first[7], .1);
            StartsAt(first[8], .16);
            StartsAt(first[9], .06);

            // C's center is about 99% there half a second after it started (critically damped, ω ≈ 13.1).
            while (art.Clock - switchAt < .56)
            {
                clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60));
            }
            var progress = (art.CenterOf(2).Y - 6) / (78 - 6.0);
            progress.ShouldBeInRange(0.97, 0.999);

            clock.Advance(TimeSpan.FromSeconds(2));
            art.IsBusy.ShouldBeFalse();
            var files = OnboardingArtScenes.Files;
            for (var i = 0; i < 7; i++)
            {
                art.CenterOf(i).ShouldBe(Center(files, i));
                art.ShapeOf(i).ShouldBe(files.Shapes[i]);
                art.OpacityOf(i).ShouldBe(files.Rects[i].Opacity);
            }
            art.TrailPoints.ShouldBe(files.Trail);
            art.TrailOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Going_Back_Staggers_From_The_Last_Shape()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration { CurrentIndex = 1 };
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            var art = block.Art!;
            clock.Advance(TimeSpan.FromSeconds(1));
            block.Back();
            Dispatcher.UIThread.RunJobs();
            var switchAt = art.Clock;
            var first = FirstChanges(clock, art, switchAt, 0.3,
                CenterProbe(art, 5), CenterProbe(art, 4), CenterProbe(art, 3), CenterProbe(art, 2),
                () => art.LineOf(2), () => art.LineOf(1), () => art.LineOf(0),
                () => art.TrailPoints, () => [art.TrailOpacity]);
            // F first (30ms), then E, D, C; lines L3 first (120ms) and L1 last (200ms); the trail fades out at once.
            StartsAt(first[0], .03);
            StartsAt(first[1], .06);
            StartsAt(first[2], .09);
            StartsAt(first[3], .12);
            StartsAt(first[4], .12);
            StartsAt(first[5], .16);
            StartsAt(first[6], .2);
            StartsAt(first[7], .1);
            StartsAt(first[8], 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Quick_Second_Switch_Moves_Every_Shape_At_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            var art = block.Art!;
            clock.Advance(TimeSpan.FromSeconds(1));
            block.Next();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var files = OnboardingArtScenes.Files;
            block.Next();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(20));
            // G (180ms in a staggered switch) and the last line (200ms) already move.
            art.CenterOf(6).ShouldNotBe(Center(files, 6));
            art.LineOf(2).ShouldNotBe(OnboardingArtScenes.Folder.Lines[2]);
            art.LineOf(2).ShouldNotBe(files.Lines[2]);

            clock.Advance(TimeSpan.FromSeconds(3));
            art.IsBusy.ShouldBeFalse();
            var team = OnboardingArtScenes.Team;
            art.CenterOf(6).ShouldBe(Center(team, 6));

            // At rest again: the next switch staggers.
            block.Next();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(20));
            art.CenterOf(6).ShouldBe(Center(team, 6));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Check_Draws_In_After_The_Badge_Lands_And_Undraws_At_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration { CurrentIndex = 2 };
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            var art = block.Art!;
            clock.Advance(TimeSpan.FromSeconds(1));
            art.CheckDrawn.ShouldBe(0);
            block.Next();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(300));
            art.CheckDrawn.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(150));
            art.CheckOpacity.ShouldBe(1);
            art.CheckDrawn.ShouldBeInRange(0.01, 0.99);
            clock.Advance(TimeSpan.FromSeconds(2));
            art.CheckDrawn.ShouldBe(1);
            art.IsBusy.ShouldBeFalse();

            block.Back();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(84));
            art.CheckDrawn.ShouldBeInRange(0.01, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            art.CheckDrawn.ShouldBe(0);
            art.CheckOpacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Colors_Cross_Over_When_A_Shape_Changes_Ink()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration { CurrentIndex = 1 };
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            var art = block.Art!;
            clock.Advance(TimeSpan.FromSeconds(1));
            var shade = ArcTokens.GetColor(block, ArcDesignTokenKind.Border);
            var paper = ArcTokens.GetColor(block, ArcDesignTokenKind.SurfaceRaised);
            var muted = ArcTokens.GetColor(block, ArcDesignTokenKind.TextMuted);
            var accent = ArcTokens.GetColor(block, ArcDesignTokenKind.Accent);
            art.FillOf(0).ShouldBe(shade);
            art.StrokeOf(5).ShouldBe(accent);
            art.LineColorOf(1).ShouldBe(muted);

            // Files → Team: the box's back turns into a person (paper), the note into a head; the second line is highlighted.
            block.Next();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(150));
            art.FillOf(0).ShouldNotBe(shade);
            art.FillOf(0).ShouldNotBe(paper);
            clock.Advance(TimeSpan.FromSeconds(1));
            art.FillOf(0).ShouldBe(paper);
            art.StrokeOf(5).ShouldBe(ArcTokens.GetColor(block, ArcDesignTokenKind.Foreground));
            art.LineColorOf(1).ShouldBe(accent);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Mouse_Shifts_The_Shapes_By_Depth_Under_Full_Motion_Only()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            var art = block.Art!;
            var stage = TestHelpers.Descendant<Panel>(block, OnboardingIllustration.StagePart);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var corner = stage.TranslatePoint(new Point(stage.Bounds.Width - 0.5, 0.5), window)!.Value;
            window.MouseMove(corner, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(2));
            art.Sway.X.ShouldBe(1, 0.02);
            art.Sway.Y.ShouldBe(-1, 0.02);
            // A (depth .4) moves 6 × .4 right and 4 × .4 up; the front of the box (depth 1) the full 6 and 4.
            art.DrawnCenterOf(0).X.ShouldBe(200 + 6 * .4 * art.Sway.X, 0.001);
            art.DrawnCenterOf(0).Y.ShouldBe(60 + 4 * .4 * art.Sway.Y, 0.001);
            art.DrawnCenterOf(6).X.ShouldBe(160 + 6 * art.Sway.X, 0.001);
            art.IsBusy.ShouldBeFalse();

            // Leaving the stage brings everything back.
            var body = TestHelpers.Descendant<Border>(block, "PART_Body");
            window.MouseMove(body.TranslatePoint(new Point(10, 10), window)!.Value, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(2));
            art.Sway.ShouldBe((0.0, 0.0));

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Dispatcher.UIThread.RunJobs();
            art.PointAt(1, 1);
            clock.Advance(TimeSpan.FromSeconds(1));
            art.Sway.ShouldBe((0.0, 0.0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_The_Whole_Scene()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = new OnboardingIllustration { CurrentIndex = 2 };
        var window = Show(block, ArcMotionLevel.Reduced);
        try
        {
            var art = block.Art!;
            clock.Advance(TimeSpan.FromSeconds(0.5));
            block.Next();
            Dispatcher.UIThread.RunJobs();
            var badge = OnboardingArtScenes.Badge;
            art.IsCrossfading.ShouldBeTrue();
            // No morph: the new scene is in place at once, the check fully drawn.
            for (var i = 0; i < 7; i++)
            {
                art.CenterOf(i).ShouldBe(Center(badge, i));
            }
            art.CheckDrawn.ShouldBe(1);
            clock.Advance(TimeSpan.FromMilliseconds(300));
            art.IsCrossfading.ShouldBeFalse();
            art.IsBusy.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Off_Switches_At_Once()
    {
        var block = new OnboardingIllustration();
        var window = Show(block, ArcMotionLevel.Off);
        try
        {
            var art = block.Art!;
            block.GoTo(3);
            Dispatcher.UIThread.RunJobs();
            art.IsBusy.ShouldBeFalse();
            art.IsCrossfading.ShouldBeFalse();
            art.CenterOf(2).ShouldBe(Center(OnboardingArtScenes.Badge, 2));
            art.CheckDrawn.ShouldBe(1);
            art.TrailOpacity.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}
