using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The ink fluid: the solver's grids, ink transport, fading and tint hand-off, composing, and the control.</summary>
public class InkFluidTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static InkFluidSolver Solver(double aspect = 760.0 / 440, InkFluidQuality quality = InkFluidQuality.Medium)
    {
        var solver = new InkFluidSolver();
        var (cells, inkCells) = InkFluidCanvas.CellsFor(quality);
        solver.Resize(aspect, cells, inkCells);
        return solver;
    }

    private static void Run(InkFluidSolver solver, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
        {
            solver.Step(1.0 / 60);
        }
    }

    [Theory]
    [InlineData(InkFluidQuality.Low, 96, 192)]
    [InlineData(InkFluidQuality.Medium, 128, 256)]
    [InlineData(InkFluidQuality.High, 192, 384)]
    public void Quality_sets_the_short_side_of_both_grids(InkFluidQuality quality, int cells, int inkCells)
    {
        var wide = Solver(2, quality);
        wide.Rows.ShouldBe(cells);
        wide.Columns.ShouldBe(cells * 2);
        wide.InkRows.ShouldBe(inkCells);
        var tall = Solver(0.5, quality);
        tall.Columns.ShouldBe(cells);
        tall.Rows.ShouldBe(cells * 2);
    }

    [Fact]
    public void Ink_fades_over_a_couple_of_seconds()
    {
        var solver = Solver();
        solver.Splat(0.5, 0.5, 0, 0, 1, 0, 0.05);
        var start = solver.TotalInk();
        start.ShouldBeGreaterThan(0);
        Run(solver, 1);
        solver.TotalInk().ShouldBeInRange(start * 0.25, start * 0.5);
        Run(solver, 3);
        solver.TotalInk().ShouldBeLessThan(start * 0.03);
    }

    [Fact]
    public void Accent_ink_hands_itself_to_the_neutral_share()
    {
        var solver = Solver();
        solver.Splat(0.5, 0.5, 0, 0, 0, 1, 0.05);
        solver.TotalAccent().ShouldBe(solver.TotalInk(), 1e-6);
        Run(solver, 1);
        var accent = solver.TotalAccent();
        var neutral = solver.TotalInk() - accent;
        neutral.ShouldBeGreaterThan(0.15 * accent);
    }

    [Fact]
    public void Force_carries_ink_downstream_and_stays_divergence_free_enough()
    {
        var solver = Solver();
        solver.Splat(0.3, 0.5, 300, 0, 1, 0, 0.04);
        Run(solver, 0.4);
        var pixels = new int[solver.InkColumns * solver.InkRows];
        solver.Compose(pixels, Colors.Black, Colors.Black, 1);
        // Ink has moved right of where it was dropped.
        double weighted = 0, total = 0;
        for (var j = 0; j < solver.InkRows; j++)
        {
            for (var i = 0; i < solver.InkColumns; i++)
            {
                var alpha = (pixels[j * solver.InkColumns + i] >> 24) & 0xFF;
                weighted += alpha * (i + 0.5) / solver.InkColumns;
                total += alpha;
            }
        }
        (weighted / total).ShouldBeGreaterThan(0.33);
        solver.MaxSpeed().ShouldBeLessThan(1000);
    }

    [Fact]
    public void Compose_writes_premultiplied_pixels_capped_by_strength()
    {
        var solver = Solver();
        solver.Splat(0.5, 0.5, 0, 0, 20, 0, 0.05);
        var pixels = new int[solver.InkColumns * solver.InkRows];
        solver.Compose(pixels, Color.FromRgb(200, 100, 50), Colors.Black, 0.3);
        var center = pixels[solver.InkRows / 2 * solver.InkColumns + solver.InkColumns / 2];
        var alpha = (center >> 24) & 0xFF;
        alpha.ShouldBeInRange(70, 78);
        var red = (center >> 16) & 0xFF;
        red.ShouldBeLessThanOrEqualTo(alpha);
        // Away from the ink the pixels stay clear.
        pixels[0].ShouldBe(0);
    }

    [AvaloniaFact]
    public void Control_runs_its_canvas_and_draws_ink()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var ink = new InkFluid { Width = 760, Height = 440, Content = new Button { Content = "Get started" } };
        var window = TestHelpers.Show(ink, ArcMotionLevel.Full);
        window.Width = 800;
        window.Height = 500;
        Dispatcher.UIThread.RunJobs();
        var canvas = ink.Canvas.ShouldNotBeNull();
        canvas.Solver.Rows.ShouldBe(128);
        canvas.IsHitTestVisible.ShouldBeFalse();
        ink.Focusable.ShouldBeFalse();

        // The ambient stir starts 1.2s after the last move and keeps the ink alive.
        frames.Advance(TimeSpan.FromSeconds(2), Frame);
        Dispatcher.UIThread.RunJobs();
        canvas.Loop.IsRunning.ShouldBeTrue();
        canvas.Solver.TotalInk().ShouldBeGreaterThan(0);
        canvas.Picture.ShouldNotBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void Pointer_strokes_add_ink()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var ink = new InkFluid { Width = 760, Height = 440, IsAmbient = false };
        var window = TestHelpers.Show(ink, ArcMotionLevel.Full);
        window.Width = 800;
        window.Height = 500;
        Dispatcher.UIThread.RunJobs();
        var canvas = ink.Canvas!;
        canvas.Solver.TotalInk().ShouldBe(0);
        for (var i = 0; i < 12; i++)
        {
            window.MouseMove(ink.TranslatePoint(new Point(100 + i * 30, 200), window)!.Value, RawInputModifiers.None);
            frames.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
        }
        canvas.Solver.TotalInk().ShouldBeGreaterThan(0);
        canvas.Solver.TotalAccent().ShouldBeGreaterThan(0);

        // Without the ambient stir the loop stops once the ink has faded.
        frames.Advance(TimeSpan.FromSeconds(12), Frame);
        Dispatcher.UIThread.RunJobs();
        canvas.Loop.IsActive.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Non_interactive_ink_ignores_the_pointer()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var ink = new InkFluid { Width = 400, Height = 300, IsAmbient = false, IsInteractive = false };
        var window = TestHelpers.Show(ink, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        for (var i = 0; i < 8; i++)
        {
            window.MouseMove(ink.TranslatePoint(new Point(50 + i * 30, 150), window)!.Value, RawInputModifiers.None);
            frames.Advance(Frame, Frame);
        }
        ink.Canvas!.Solver.TotalInk().ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_composes_one_still_frame()
    {
        var ink = new InkFluid { Width = 400, Height = 300 };
        var window = TestHelpers.Show(ink, ArcMotionLevel.Reduced);
        Dispatcher.UIThread.RunJobs();
        var canvas = ink.Canvas!;
        canvas.Loop.IsRunning.ShouldBeFalse();
        canvas.Picture.ShouldNotBeNull();
        canvas.Solver.TotalInk().ShouldBeGreaterThan(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Quality_change_rebuilds_the_grids()
    {
        var ink = new InkFluid { Width = 400, Height = 300 };
        var window = TestHelpers.Show(ink);
        ink.Canvas!.Solver.Rows.ShouldBe(128);
        ink.Quality = InkFluidQuality.Low;
        ink.Canvas.Solver.Rows.ShouldBe(96);
        window.Close();
    }

    [AvaloniaFact]
    public void Ink_is_decoration()
    {
        var ink = new InkFluid { Width = 400, Height = 300 };
        var window = TestHelpers.Show(ink);
        ControlAutomationPeer.CreatePeerForElement(ink).IsControlElement().ShouldBeFalse();
        window.Close();
    }
}
