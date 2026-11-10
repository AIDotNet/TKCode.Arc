using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>Sand text: the grains' paths, and the control's states, triggers, keyboard, events and motion levels.</summary>
public class SandTextTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static SandTextGrains Grains(SandTextDrift drift = SandTextDrift.Right, int max = 10_000)
    {
        var grains = new SandTextGrains();
        // A 100 x 20 "letter" box with a hole in the middle.
        grains.Build(new Rect(0, 0, 100, 20), 2, p => p.X < 40 || p.X > 60, drift, 100, max);
        return grains;
    }

    // --- grains ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Grains_cover_only_the_inside_of_the_letters()
    {
        var grains = Grains();
        grains.Count.ShouldBe(40 / 2 * 10 * 2);
        for (var i = 0; i < grains.Count; i++)
        {
            var home = grains.Home(i);
            (home.X + 1 < 40 || home.X + 1 > 60).ShouldBeTrue();
        }
    }

    [Fact]
    public void Grains_coarsen_to_stay_within_the_budget()
    {
        var grains = Grains(max: 100);
        grains.Count.ShouldBeLessThanOrEqualTo(100);
        grains.Count.ShouldBeGreaterThan(40);
        grains.Size.ShouldBeGreaterThan(2);
    }

    [Fact]
    public void Grains_sit_home_at_the_start_and_have_faded_at_the_end()
    {
        var grains = Grains();
        for (var i = 0; i < grains.Count; i++)
        {
            grains.Place(i, 0, out var x, out var y, out var opacity, out _);
            new Point(x, y).ShouldBe(grains.Home(i));
            opacity.ShouldBe(1);
            grains.Place(i, SandTextGrains.Span, out _, out _, out opacity, out _);
            opacity.ShouldBe(0, 1e-9);
        }
    }

    [Theory]
    [InlineData(SandTextDrift.Right)]
    [InlineData(SandTextDrift.Left)]
    [InlineData(SandTextDrift.Up)]
    public void Wind_carries_grains_its_way_and_the_windward_side_lets_go_first(SandTextDrift drift)
    {
        var grains = Grains(drift);
        var t = SandTextGrains.Span * 0.8;
        var first = -1;
        var last = -1;
        for (var i = 0; i < grains.Count; i++)
        {
            grains.Place(i, t, out var x, out var y, out _, out _);
            var home = grains.Home(i);
            var travel = drift switch
            {
                SandTextDrift.Left => home.X - x,
                SandTextDrift.Up => home.Y - y,
                _ => x - home.X
            };
            if (grains.Flight(i, t) > 0.2)
            {
                travel.ShouldBeGreaterThan(0);
            }
            if (first < 0 || grains.Order(i) < grains.Order(first))
            {
                first = i;
            }
            if (last < 0 || grains.Order(i) > grains.Order(last))
            {
                last = i;
            }
        }
        grains.Flight(first, SandTextGrains.Span * 0.3).ShouldBeGreaterThan(grains.Flight(last, SandTextGrains.Span * 0.3));
    }

    [Fact]
    public void No_grain_has_let_go_ahead_of_the_front()
    {
        var grains = Grains();
        for (var step = 0; step <= 20; step++)
        {
            var t = SandTextGrains.Span * step / 20;
            var front = SandTextGrains.Front(t);
            for (var i = 0; i < grains.Count; i++)
            {
                if (grains.Order(i) > front)
                {
                    grains.Flight(i, t).ShouldBe(0);
                }
            }
        }
    }

    // --- control ---------------------------------------------------------------------------------------------------

    private static SandText Text(SandTextTrigger trigger = SandTextTrigger.Click) => new()
    {
        Text = "Drift",
        Trigger = trigger,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
    };

    [AvaloniaFact]
    public void Measures_its_text_and_is_named_by_it()
    {
        var text = Text();
        var window = TestHelpers.Show(text);
        text.Bounds.Width.ShouldBeGreaterThan(0);
        text.Bounds.Height.ShouldBeGreaterThan(0);
        AutomationProperties.GetName(text).ShouldBe("Drift");
        ControlAutomationPeer.CreatePeerForElement(text).GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        text.Grains.Count.ShouldBeGreaterThan(0);
        window.Close();
    }

    [AvaloniaFact]
    public void A_first_state_shows_without_motion()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var text = new SandText { Text = "Gone", IsDissolved = true };
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        text.Progress.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Click_blows_the_text_away_over_the_duration_and_raises_Dissolved()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var text = Text();
        text.Duration = TimeSpan.FromSeconds(1);
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        var dissolved = 0;
        text.Dissolved += (_, _) => dissolved++;
        var point = text.TranslatePoint(new Point(4, 4), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        text.IsDissolved.ShouldBeTrue();
        frames.Advance(TimeSpan.FromSeconds(0.5), Frame);
        text.Progress.ShouldBeInRange(0.4, 0.6);
        dissolved.ShouldBe(0);
        frames.Advance(TimeSpan.FromSeconds(0.6), Frame);
        text.Progress.ShouldBe(1);
        dissolved.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Gathering_half_way_turns_around_from_where_it_is()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var text = Text();
        text.Duration = TimeSpan.FromSeconds(1);
        var window = TestHelpers.Show(text, ArcMotionLevel.Full);
        var gathered = 0;
        text.Gathered += (_, _) => gathered++;
        text.Dissolve();
        frames.Advance(TimeSpan.FromSeconds(0.4), Frame);
        var mid = text.Progress;
        text.Gather();
        frames.Advance(TimeSpan.FromSeconds(0.1), Frame);
        text.Progress.ShouldBeLessThan(mid);
        frames.Advance(TimeSpan.FromSeconds(0.5), Frame);
        text.Progress.ShouldBe(0);
        gathered.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Enter_and_Space_toggle_and_Escape_gathers()
    {
        var text = Text();
        var window = TestHelpers.Show(text);
        text.Focusable.ShouldBeTrue();
        text.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        text.IsDissolved.ShouldBeTrue();
        text.Progress.ShouldBe(1);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        text.IsDissolved.ShouldBeFalse();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        text.IsDissolved.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Hover_blows_away_on_enter_and_gathers_on_leave()
    {
        var text = Text(SandTextTrigger.Hover);
        var window = TestHelpers.Show(text);
        var inside = text.TranslatePoint(new Point(4, 4), window)!.Value;
        window.MouseMove(inside, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        text.IsDissolved.ShouldBeTrue();
        window.MouseMove(new Point(399, 299), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        text.IsDissolved.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Without_a_trigger_it_is_plain_text_that_code_still_moves()
    {
        var text = Text(SandTextTrigger.None);
        var window = TestHelpers.Show(text);
        text.Focusable.ShouldBeFalse();
        ControlAutomationPeer.CreatePeerForElement(text).GetAutomationControlType().ShouldBe(AutomationControlType.Text);
        text.Toggle();
        text.Progress.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_fades_quickly_instead_of_crumbling()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var text = Text();
        text.Duration = TimeSpan.FromSeconds(2);
        var window = TestHelpers.Show(text, ArcMotionLevel.Reduced);
        text.Dissolve();
        frames.Advance(TimeSpan.FromSeconds(0.3), Frame);
        text.Progress.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Automation_invoke_toggles()
    {
        var text = Text();
        var window = TestHelpers.Show(text);
        var peer = ControlAutomationPeer.CreatePeerForElement(text);
        ((IInvokeProvider)peer).Invoke();
        text.IsDissolved.ShouldBeTrue();
        peer.GetHelpText().ShouldBe("Blown away");
        window.Close();
    }
}
