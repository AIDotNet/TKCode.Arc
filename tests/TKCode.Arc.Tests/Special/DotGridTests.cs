using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The dot grid: the field's layout, the lens, ripples, settling, pointer handling and motion levels.</summary>
public class DotGridTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static DotGridSimulation Field(double width = 720, double height = 420, double gap = 22)
    {
        var field = new DotGridSimulation();
        field.Build(width, height, gap);
        return field;
    }

    private static void Run(DotGridSimulation field, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
        {
            field.Step(1.0 / 60);
        }
    }

    private static int Nearest(DotGridSimulation field, double x, double y)
    {
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < field.Count; i++)
        {
            var d = Math.Pow(field.HomeX(i) - x, 2) + Math.Pow(field.HomeY(i) - y, 2);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        return best;
    }

    [Fact]
    public void Grid_fits_the_field_and_is_centred()
    {
        var field = Field();
        // 720 / 22 → 32 gaps, 33 columns; 420 / 22 → 19 gaps, 20 rows.
        field.Columns.ShouldBe(33);
        field.Rows.ShouldBe(20);
        field.Count.ShouldBe(660);
        var left = field.HomeX(0);
        var right = field.HomeX(field.Columns - 1);
        (left + right).ShouldBe(720, 0.01);
        (field.HomeY(0) + field.HomeY(field.Count - 1)).ShouldBe(420, 0.01);
    }

    [Fact]
    public void Denser_gap_builds_more_dots_and_tiny_gaps_are_clamped()
    {
        Field(gap: 14).Count.ShouldBeGreaterThan(1500);
        var tiny = Field(gap: 1);
        tiny.Gap.ShouldBe(DotGridSimulation.MinimumGap);
    }

    [Fact]
    public void Lens_pushes_dots_outward_and_lights_them()
    {
        var field = Field();
        field.PointerMoved(360, 210);
        Run(field, 1.5);
        field.LensPower.ShouldBe(1);
        // A dot about a third of the radius away is pushed out by about the strength.
        var index = Nearest(field, 360 + 50, 210);
        var pushed = field.X(index) - field.HomeX(index);
        pushed.ShouldBeInRange(12, 17);
        field.Glow(index).ShouldBeGreaterThan(0.3);
        // Dots beyond the radius stay put and dark.
        var far = Nearest(field, 60, 40);
        field.X(far).ShouldBe(field.HomeX(far), 0.05);
        field.Glow(far).ShouldBe(0, 0.01);
    }

    [Fact]
    public void Field_settles_after_the_pointer_leaves()
    {
        var field = Field();
        field.PointerMoved(360, 210);
        Run(field, 1);
        field.PointerLeft();
        var moving = true;
        for (var i = 0; i < 600 && moving; i++)
        {
            moving = field.Step(1.0 / 60);
        }
        moving.ShouldBeFalse();
        field.LensPower.ShouldBe(0);
        var index = Nearest(field, 410, 210);
        field.X(index).ShouldBe(field.HomeX(index), 0.1);
    }

    [Fact]
    public void Ripple_travels_at_620_px_per_second_and_ends_after_its_life()
    {
        var field = Field();
        field.AddRipple(360, 210).ShouldBeTrue();
        Run(field, 0.25);
        // After 0.25s the crest is about 155px out: dots there are lit, dots at the origin are not.
        var crest = Nearest(field, 360 + 154, 210);
        field.Glow(crest).ShouldBeGreaterThan(0.25);
        field.Glow(Nearest(field, 360, 210)).ShouldBeLessThan(0.15);
        Run(field, 1.4);
        field.RippleCount.ShouldBe(0);
    }

    [Fact]
    public void At_most_six_ripples_overlap()
    {
        var field = Field();
        for (var i = 0; i < 9; i++)
        {
            field.AddRipple(100 + i * 10, 100);
        }
        field.RippleCount.ShouldBe(DotGridSimulation.MaxRipples);
    }

    [Fact]
    public void Without_motion_dots_stay_home_but_the_lens_still_lights_them()
    {
        var field = Field();
        field.CanMove = false;
        field.AddRipple(360, 210).ShouldBeFalse();
        field.PointerMoved(360, 210);
        Run(field, 1);
        var index = Nearest(field, 410, 210);
        field.X(index).ShouldBe(field.HomeX(index));
        field.Glow(index).ShouldBeGreaterThan(0.3);
    }

    [AvaloniaFact]
    public void Control_builds_its_field_and_follows_the_pointer()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var grid = new DotGrid { Width = 440, Height = 300, Content = new Button { Content = "Go" } };
        var window = TestHelpers.Show(grid, ArcMotionLevel.Full);
        window.Width = 500;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        var field = grid.Field.ShouldNotBeNull();
        field.Simulation.Count.ShouldBeGreaterThan(0);
        grid.Focusable.ShouldBeFalse();
        field.IsHitTestVisible.ShouldBeFalse();

        var origin = grid.TranslatePoint(new Point(220, 150), window)!.Value;
        window.MouseMove(origin, RawInputModifiers.None);
        frames.Advance(TimeSpan.FromSeconds(0.6), Frame);
        Dispatcher.UIThread.RunJobs();
        field.Simulation.PointerInside.ShouldBeTrue();
        field.Simulation.LensPower.ShouldBeGreaterThan(0.9);

        window.MouseDown(origin, MouseButton.Left);
        window.MouseUp(origin, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        field.Simulation.RippleCount.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Ripples_can_be_turned_off()
    {
        var grid = new DotGrid { Width = 400, Height = 300, IsRippleEnabled = false };
        var window = TestHelpers.Show(grid, ArcMotionLevel.Full);
        grid.Ripple(new Point(100, 100));
        grid.Field!.Simulation.RippleCount.ShouldBe(0);
        grid.IsRippleEnabled = true;
        grid.Ripple(new Point(100, 100));
        grid.Field.Simulation.RippleCount.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_never_ripples()
    {
        var grid = new DotGrid { Width = 400, Height = 300 };
        var window = TestHelpers.Show(grid, ArcMotionLevel.Reduced);
        grid.Ripple(new Point(100, 100));
        grid.Field!.Simulation.RippleCount.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Gap_change_rebuilds_the_grid()
    {
        var grid = new DotGrid { Width = 400, Height = 300 };
        var window = TestHelpers.Show(grid);
        var before = grid.Field!.Simulation.Count;
        grid.Gap = 32;
        grid.Field.Simulation.Count.ShouldBeLessThan(before);
        window.Close();
    }

    [AvaloniaFact]
    public void Field_is_decoration_and_content_keeps_its_semantics()
    {
        var button = new Button { Content = "Get started" };
        var grid = new DotGrid { Width = 400, Height = 300, Content = button };
        var window = TestHelpers.Show(grid);
        ControlAutomationPeer.CreatePeerForElement(grid).IsControlElement().ShouldBeFalse();
        ControlAutomationPeer.CreatePeerForElement(button).GetName().ShouldBe("Get started");
        window.Close();
    }
}
