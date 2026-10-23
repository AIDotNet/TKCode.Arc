using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The story clock of the explainer base class: play, pause, speed, step, end, loop and announcements.</summary>
public class ExplainerTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    /// <summary>A story with beats at 0.5, 1 (stop), 2 (stop) and 3 seconds.</summary>
    private sealed class Probe : Explainer
    {
        public List<string> Ran { get; } = new();
        public int Resets { get; private set; }
        public int Finishes { get; private set; }

        private protected override void BuildStory(ExplainerScript script)
        {
            script.At(0.5, () => Ran.Add("a"));
            script.At(1, () => { Ran.Add("b"); AnnounceBeat("beat b"); }, isStop: true);
            script.At(2, () => { Ran.Add("c"); AnnounceBeat("beat c"); }, isStop: true);
            script.At(3, () => Ran.Add("d"), isStop: true);
        }

        private protected override void ResetScene()
        {
            Ran.Clear();
            Resets++;
        }

        private protected override void OnFinished() => Finishes++;

        public double Time => Now;

        public void Say(string text) => AnnounceBeat(text);

        protected override Size MeasureOverride(Size availableSize) => new(200, 100);
    }

    private static (Probe Probe, Window Window) Open(ManualFrameClock clock, ArcMotionLevel level = ArcMotionLevel.Full, bool autoPlay = false)
    {
        var probe = new Probe { AutoPlay = autoPlay };
        var window = TestHelpers.Show(probe, level);
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        return (probe, window);
    }

    [AvaloniaFact]
    public void Playing_Runs_The_Beats_And_Pausing_Freezes_The_Clock()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (probe, window) = Open(clock);
        try
        {
            probe.Time.ShouldBe(0);
            probe.Play();
            clock.Advance(TimeSpan.FromSeconds(1.2), Frame);
            probe.Ran.ShouldBe(["a", "b"]);
            probe.Pause();
            var held = probe.Time;
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            probe.Time.ShouldBe(held);
            probe.Ran.ShouldBe(["a", "b"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Speed_Scales_The_Clock_And_Zero_Holds_It_Still()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (probe, window) = Open(clock);
        try
        {
            probe.Speed = 2;
            probe.Play();
            clock.Advance(TimeSpan.FromSeconds(0.6), Frame);
            probe.Time.ShouldBeInRange(1.1, 1.3);
            probe.Speed = 0;
            var held = probe.Time;
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            probe.Time.ShouldBe(held);
            probe.Speed = 9;
            probe.Speed.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Step_Plays_To_The_Next_Stop_And_Announces_It()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (probe, window) = Open(clock);
        try
        {
            probe.StepForward();
            probe.IsStepping.ShouldBeTrue();
            probe.IsPlaying.ShouldBeFalse();
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            probe.IsStepping.ShouldBeFalse();
            probe.Time.ShouldBe(1);
            probe.Ran.ShouldBe(["a", "b"]);
            probe.StepForward();
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            probe.Time.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_Reduced_Motion_A_Step_Applies_At_Once()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (probe, window) = Open(clock, ArcMotionLevel.Reduced);
        try
        {
            probe.Restart();
            probe.Pause();
            probe.StepForward();
            probe.Time.ShouldBe(1);
            probe.IsStepping.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_End_Stops_The_Story_And_Play_Starts_It_Over()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (probe, window) = Open(clock);
        try
        {
            var raised = 0;
            probe.Finished += (_, _) => raised++;
            probe.Play();
            clock.Advance(TimeSpan.FromSeconds(3.5), Frame);
            probe.IsFinished.ShouldBeTrue();
            probe.IsPlaying.ShouldBeFalse();
            probe.CanStep.ShouldBeFalse();
            probe.Classes.ShouldContain(Explainer.FinishedPseudoClass);
            raised.ShouldBe(1);
            probe.Finishes.ShouldBe(1);
            probe.Play();
            probe.IsFinished.ShouldBeFalse();
            probe.Ran.ShouldBeEmpty();
            probe.Time.ShouldBe(0);
            probe.IsPlaying.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Looping_Story_Starts_Again_After_Its_Delay()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (probe, window) = Open(clock);
        try
        {
            probe.IsLooping = true;
            probe.Play();
            clock.Advance(TimeSpan.FromSeconds(3.2), Frame);
            probe.IsFinished.ShouldBeTrue();
            probe.IsPlaying.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(4.2), Frame);
            probe.IsFinished.ShouldBeFalse();
            probe.Time.ShouldBeLessThan(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Beats_Are_Announced_Only_When_The_Reader_Drives()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var probe = new Probe { AutoPlay = false };
        var announcer = new ExplainerAnnouncer();
        var panel = new Panel { Children = { probe, announcer } };
        var window = TestHelpers.Show(panel, ArcMotionLevel.Full);
        try
        {
            // The probe has no template, so give it the live region by hand.
            probe.Announcer = announcer;
            probe.Play();
            clock.Advance(TimeSpan.FromSeconds(1.2), Frame);
            announcer.History.ShouldBeEmpty();
            probe.Pause();
            probe.StepForward();
            clock.Advance(TimeSpan.FromSeconds(1.2), Frame);
            announcer.History.ShouldBe(["beat c"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Starts_When_Seen_And_Reduced_Motion_Opens_On_The_End()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (full, window) = Open(clock, ArcMotionLevel.Full, autoPlay: true);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            full.IsPlaying.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
        var (reduced, window2) = Open(clock, ArcMotionLevel.Reduced, autoPlay: true);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            reduced.IsPlaying.ShouldBeFalse();
            reduced.IsFinished.ShouldBeTrue();
            reduced.Ran.ShouldBe(["a", "b", "c", "d"]);
        }
        finally
        {
            window2.Close();
        }
    }

    /// <summary>A probe whose data changes before it is first seen.</summary>
    private sealed class Rebuilding : Explainer
    {
        public int Builds { get; private set; }

        private protected override void BuildStory(ExplainerScript script)
        {
            Builds++;
            script.At(1, () => { }, isStop: true);
        }

        private protected override void ResetScene()
        {
        }

        public void DataChanged() => Rebuild();

        protected override Size MeasureOverride(Size availableSize) => new(200, 100);
    }

    [AvaloniaFact]
    public void A_Rebuild_Before_The_Card_Is_Seen_Keeps_Autoplay()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var probe = new Rebuilding();
        var host = new Panel();
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            host.Children.Add(probe);
            probe.DataChanged();
            probe.IsPlaying.ShouldBeFalse();
            // The first-sight check runs after the next layout pass.
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
            probe.Builds.ShouldBeGreaterThanOrEqualTo(2);
            probe.IsPlaying.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stepping_To_The_End_Moves_Keyboard_Focus_To_Restart()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var probe = new Probe
        {
            AutoPlay = false,
            Template = new Avalonia.Controls.Templates.FuncControlTemplate<Probe>((_, scope) => new StackPanel
            {
                Children =
                {
                    new ExplainerIconButton { Name = Explainer.StepButtonPart }.RegisterInNameScope(scope),
                    new ExplainerIconButton { Name = Explainer.RestartButtonPart }.RegisterInNameScope(scope),
                    new ExplainerIconButton { Name = Explainer.PlayButtonPart }.RegisterInNameScope(scope),
                }
            })
        };
        var window = TestHelpers.Show(probe, ArcMotionLevel.Reduced);
        try
        {
            var step = TestHelpers.Descendant<ExplainerIconButton>(probe, Explainer.StepButtonPart);
            var restart = TestHelpers.Descendant<ExplainerIconButton>(probe, Explainer.RestartButtonPart);
            var play = TestHelpers.Descendant<ExplainerIconButton>(probe, Explainer.PlayButtonPart);
            // Reduced motion opens on the finished story: start over and hold at the start.
            Avalonia.Automation.AutomationProperties.GetName(play).ShouldBe("Play again");
            probe.Restart();
            probe.Pause();
            Avalonia.Automation.AutomationProperties.GetName(play).ShouldBe("Play");
            step.Focus(Avalonia.Input.NavigationMethod.Tab);
            for (var i = 0; i < 3; i++)
            {
                probe.StepForward();
            }
            probe.IsFinished.ShouldBeTrue();
            step.IsEnabled.ShouldBeFalse();
            restart.IsKeyboardFocusWithin.ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(play).ShouldBe("Play again");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Path_Measures_Its_Length_And_Shows_A_Part_With_A_Dash()
    {
        var path = new ExplainerPath().MoveTo(new Point(0, 0)).LineTo(new Point(100, 0)).ArcTo(new Point(100, 50), 50, 0, 180);
        path.Length.ShouldBe(100 + Math.PI * 50, 0.5);
        path.PointAt(50).ShouldBe(new Point(50, 0));
        var bottom = path.PointAt(path.Length);
        bottom.X.ShouldBe(100, 0.01);
        bottom.Y.ShouldBe(100, 0.01);
        var side = path.PointAt(100 + Math.PI * 25);
        side.X.ShouldBe(150, 0.1);
        side.Y.ShouldBe(50, 0.1);
        path.TangentAt(10).ShouldBe(new Vector(1, 0));
        var pen = new Pen(Brushes.Black, 2);
        path.ShowPart(pen, 20, 60).ShouldBeTrue();
        var dash = (DashStyle)pen.DashStyle!;
        dash.Dashes![0].ShouldBe(20);
        path.ShowPart(pen, 30, 30).ShouldBeFalse();
    }
}
