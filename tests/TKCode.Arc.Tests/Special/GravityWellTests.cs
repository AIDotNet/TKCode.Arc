using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The gravity well: the grid layout, the field, shockwaves, keyboard, pointer and motion levels.</summary>
public class GravityWellTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    /// <summary>A 7 × 6 grid of 64px tiles 8 apart (centres every 72px).</summary>
    private static GravityWellSimulation Field()
    {
        var xs = new double[42];
        var ys = new double[42];
        for (var i = 0; i < 42; i++)
        {
            xs[i] = 32 + i % 7 * 72;
            ys[i] = 32 + i / 7 * 72;
        }
        var field = new GravityWellSimulation();
        field.SetTiles(xs, ys);
        return field;
    }

    private static bool Run(GravityWellSimulation field, double seconds)
    {
        var moving = false;
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
        {
            moving = field.Step(1.0 / 60);
        }
        return moving;
    }

    [Fact]
    public void Tiles_near_the_well_lean_in_shrink_and_dim()
    {
        var field = Field();
        // The well between tiles 9 (176, 104) and 10 (248, 104).
        field.Aim(212, 104, snap: true);
        Run(field, 1.5);
        field.Presence.ShouldBe(1);
        field.OffsetX(9).ShouldBeGreaterThan(5);
        field.OffsetX(10).ShouldBeLessThan(-5);
        field.Scale(9).ShouldBeLessThan(0.75);
        field.Opacity(9).ShouldBeLessThan(0.8);
        field.ZIndex(9).ShouldBeLessThan(field.ZIndex(41));
        // The far corner barely feels it.
        field.Scale(41).ShouldBeGreaterThan(0.98);
        field.ZIndex(41).ShouldBe(10);
    }

    [Fact]
    public void Well_trails_the_pointer_without_overshooting()
    {
        var field = Field();
        field.Aim(32, 32, snap: true);
        Run(field, 0.5);
        field.Aim(392, 32, snap: false);
        var furthest = 0.0;
        for (var i = 0; i < 90; i++)
        {
            field.Step(1.0 / 60);
            furthest = Math.Max(furthest, field.WellX);
        }
        furthest.ShouldBeLessThanOrEqualTo(392.5);
        field.WellX.ShouldBe(392, 1);
    }

    [Fact]
    public void Everything_comes_to_rest_after_release()
    {
        var field = Field();
        field.Aim(212, 104, snap: true);
        Run(field, 1);
        field.Release();
        var moving = true;
        for (var i = 0; i < 900 && moving; i++)
        {
            moving = field.Step(1.0 / 60);
        }
        moving.ShouldBeFalse();
        field.Scale(9).ShouldBe(1, 0.001);
        field.OffsetX(9).ShouldBe(0, 0.05);
    }

    [Fact]
    public void Shockwave_reaches_tiles_at_900_px_per_second_and_rings_them_once()
    {
        var field = Field();
        field.Pulse(32, 32);
        // After 0.1s the front is 90px out: tile 1 (72px away) is hit, tile 3 (216px away) is not yet.
        Run(field, 0.1);
        field.Ring(1).ShouldBeGreaterThan(0.5);
        field.Ring(3).ShouldBe(0);
        Run(field, 0.25);
        field.Ring(3).ShouldBeGreaterThan(0);
        // The wave ends once it has passed the last tile, and the rings fade out.
        Run(field, 1);
        field.WaveCount.ShouldBe(0);
        Run(field, 2);
        field.Ring(1).ShouldBe(0);
    }

    [Fact]
    public void Shockwave_kicks_tiles_away_from_its_origin()
    {
        var field = Field();
        field.Pulse(32, 32);
        Run(field, 0.12);
        field.OffsetX(1).ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Without_motion_tiles_stay_and_a_shockwave_only_rings()
    {
        var field = Field();
        field.CanMove = false;
        field.Aim(212, 104, snap: true);
        field.Pulse(212, 104);
        field.Step(1.0 / 60);
        field.WaveCount.ShouldBe(0);
        field.Ring(41).ShouldBeGreaterThan(0);
        field.Scale(9).ShouldBe(1);
        field.OffsetX(9).ShouldBe(0);
    }

    [Theory]
    [InlineData(600, 0, 56, 8, 9)]
    [InlineData(600, 7, 56, 8, 7)]
    [InlineData(100, 0, 56, 8, 1)]
    [InlineData(64, 0, 56, 8, 1)]
    public void Columns_fit_the_width(double width, int columns, double min, double gap, int expected) =>
        GravityWellPanel.ColumnsFor(width, columns, min, gap).ShouldBe(expected);

    private static (Window Window, GravityWell Well) Show(ArcMotionLevel level = ArcMotionLevel.Full, int count = 42)
    {
        var well = new GravityWell { Width = 560, Columns = 7 };
        for (var i = 0; i < count; i++)
        {
            well.Items.Add(new Border());
        }
        var window = TestHelpers.Show(well, level);
        window.Width = 640;
        window.Height = 640;
        Dispatcher.UIThread.RunJobs();
        return (window, well);
    }

    [AvaloniaFact]
    public void Lays_out_square_tiles_in_fixed_columns()
    {
        var (window, well) = Show();
        var panel = well.Panel.ShouldNotBeNull();
        panel.ColumnCount.ShouldBe(7);
        // (560 − 6 × 8) / 7 = 73.14
        panel.TileSize.ShouldBe(73.14, 0.01);
        panel.Children.Count.ShouldBe(42);
        panel.Children[0].Bounds.Height.ShouldBe(panel.Children[0].Bounds.Width, 0.01);
        panel.DesiredSize.Height.ShouldBe(6 * 73.14 + 5 * 8, 0.5);
        well.Simulation.Count.ShouldBe(42);
        window.Close();
    }

    [AvaloniaFact]
    public void Is_a_named_focusable_group()
    {
        var (window, well) = Show();
        well.Focusable.ShouldBeTrue();
        var peer = ControlAutomationPeer.CreatePeerForElement(well);
        peer.GetName().ShouldBe("Gravity well");
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        peer.GetLocalizedControlType().ShouldBe("gravity well");
        well.Label = "Team";
        peer.GetName().ShouldBe("Team");
        window.Close();
    }

    [AvaloniaFact]
    public void Keyboard_moves_the_well_and_pulses()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var (window, well) = Show();
        var pulses = new List<Point>();
        well.Pulsed += (_, e) => pulses.Add(e.Origin);
        well.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        well.CurrentIndex.ShouldBe(0);

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        well.CurrentIndex.ShouldBe(8);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        well.CurrentIndex.ShouldBe(7);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        well.CurrentIndex.ShouldBe(41);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        well.CurrentIndex.ShouldBe(41);
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        well.CurrentIndex.ShouldBe(0);

        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        pulses.Count.ShouldBe(2);
        var tile = well.Panel!.Children[0].Bounds;
        pulses[0].ShouldBe(tile.Center);

        frames.Advance(TimeSpan.FromSeconds(0.5), Frame);
        Dispatcher.UIThread.RunJobs();
        well.Simulation.Presence.ShouldBeGreaterThan(0.9);
        window.Close();
    }

    [AvaloniaFact]
    public void Click_pulses_from_the_pointer()
    {
        var (window, well) = Show();
        var pulses = new List<Point>();
        well.Pulsed += (_, e) => pulses.Add(e.Origin);
        var point = well.TranslatePoint(new Point(100, 60), window)!.Value;
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        pulses.Count.ShouldBe(1);
        pulses[0].X.ShouldBe(100, 0.5);
        pulses[0].Y.ShouldBe(60, 0.5);
        well.Simulation.WaveCount.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Moving_the_pointer_bends_the_tiles()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var (window, well) = Show();
        var center = well.Panel!.Children[17].Bounds.Center;
        window.MouseMove(well.Panel.TranslatePoint(center, window)!.Value, RawInputModifiers.None);
        frames.Advance(TimeSpan.FromSeconds(1), Frame);
        Dispatcher.UIThread.RunJobs();
        var tile = (GravityWellTile)well.Panel.Children[17];
        tile.Placement.M11.ShouldBeLessThan(0.8);
        tile.Opacity.ShouldBeLessThan(0.8);
        tile.ZIndex.ShouldBeLessThan(10);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_keeps_the_grid_still()
    {
        var (window, well) = Show(ArcMotionLevel.Reduced);
        var point = well.TranslatePoint(new Point(200, 200), window)!.Value;
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        TestHelpers.PumpUntil(() => well.Simulation.WaveCount == 0, 2000);
        var tile = (GravityWellTile)well.Panel!.Children[17];
        tile.Placement.ShouldBe(Matrix.Identity);
        window.Close();
    }
}
