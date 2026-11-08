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

    [Theory]
    [InlineData(6.4)]
    [InlineData(1 / 6.4)]
    public void A_very_wide_field_costs_no_more_than_the_aspect_limit(double aspect)
    {
        var limit = Solver(2.5);
        var field = Solver(aspect);
        (field.Columns * field.Rows).ShouldBeLessThanOrEqualTo((int)(limit.Columns * limit.Rows * 1.05));
        (field.InkColumns * field.InkRows).ShouldBeLessThanOrEqualTo((int)(limit.InkColumns * limit.InkRows * 1.05));
        // The cells stay square.
        ((double)field.Columns / field.Rows).ShouldBe(aspect, 0.1 * Math.Max(aspect, 1));
    }

    [Fact]
    public void Faded_ink_reaches_exactly_zero()
    {
        // Fading alone never gets there: the field would stay live and sink into denormals.
        var solver = Solver();
        solver.Splat(0.5, 0.5, 100, 50, 1, 1, 0.08);
        Run(solver, 15);
        solver.TotalInk().ShouldBe(0);
    }

    [Fact]
    public void A_fast_jet_carries_ink_outside_the_rows_it_started_in()
    {
        var solver = Solver();
        solver.Splat(0.5, 0.2, 0, 900, 1, 0, 0.03);
        BandInk(solver, 0.6, 1).ShouldBe(0);
        for (var i = 0; i < 12; i++)
        {
            solver.Splat(0.5, 0.2, 0, 900, 1, 0, 0.03);
            solver.Step(1.0 / 60);
        }
        Run(solver, 0.3);
        BandInk(solver, 0.6, 1).ShouldBeGreaterThan(0);
    }

    private static int BandInk(InkFluidSolver solver, double from, double to)
    {
        var pixels = new int[solver.InkColumns * solver.InkRows];
        solver.Compose(pixels, Colors.Black, Colors.Black, 1);
        var sum = 0;
        for (var j = (int)(from * solver.InkRows); j < (int)(to * solver.InkRows); j++)
        {
            for (var i = 0; i < solver.InkColumns; i++)
            {
                sum += (pixels[j * solver.InkColumns + i] >> 24) & 0xFF;
            }
        }
        return sum;
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 1.0)]
    public void Ink_in_a_corner_is_composed(double x, double y)
    {
        var solver = Solver();
        solver.Splat(x, y, 0, 0, 5, 5, 0.05);
        var pixels = new int[solver.InkColumns * solver.InkRows];
        solver.Compose(pixels, Colors.Black, Colors.Red, 1);
        var column = x > 0 ? solver.InkColumns - 1 : 0;
        var row = y > 0 ? solver.InkRows - 1 : 0;
        ((pixels[row * solver.InkColumns + column] >> 24) & 0xFF).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Clearing_leaves_a_clear_picture()
    {
        var solver = Solver();
        solver.Splat(0.5, 0.5, 100, 0, 1, 1, 0.05);
        Run(solver, 0.2);
        solver.Clear();
        var pixels = new int[solver.InkColumns * solver.InkRows];
        Array.Fill(pixels, -1);
        solver.Compose(pixels, Colors.Black, Colors.Black, 1);
        pixels.ShouldAllBe(p => p == 0);
    }

    private static int Steps(InkFluidPacer pacer, double frame, double cost, int frames)
    {
        var steps = 0;
        for (var i = 0; i < frames; i++)
        {
            if (pacer.TryStep(frame, out _))
            {
                pacer.Stepped(cost);
                steps++;
            }
        }
        return steps;
    }

    [Theory]
    [InlineData(0.0055, 60)] // Regression: any step cost used to skip the next frame, so a cheap step still ran at 30 Hz.
    [InlineData(0.012, 30)]
    [InlineData(0.030, 15)]
    public void Steps_at_60Hz_follow_their_cost(double cost, int perSecond)
    {
        Steps(new InkFluidPacer(), 1.0 / 60, cost, 60).ShouldBeInRange(perSecond - 1, perSecond + 1);
    }

    [Fact]
    public void A_fast_display_steps_at_most_near_60Hz()
    {
        Steps(new InkFluidPacer(), 1.0 / 120, 0.001, 120).ShouldBeInRange(59, 61);
    }

    [Fact]
    public void The_steps_simulate_the_time_that_went_by()
    {
        var pacer = new InkFluidPacer();
        var simulated = 0.0;
        for (var i = 0; i < 120; i++)
        {
            if (pacer.TryStep(1.0 / 120, out var dt))
            {
                simulated += dt;
                pacer.Stepped(0.001);
            }
        }
        simulated.ShouldBe(1, 0.02);
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

    [AvaloniaTheory]
    [InlineData(InkFluidQuality.Medium)]
    [InlineData(InkFluidQuality.High)]
    public void Running_ink_on_the_real_frame_clock_lets_the_dispatcher_go_idle(InkFluidQuality quality)
    {
        // Regression: on the real frame clock a simulation step that costs more than a frame was followed straight away by
        // the next one (each frame's picture is committed, and the finished commit asks for another frame), so the UI
        // thread never went idle and Dispatcher.RunJobs never returned: the gallery smoke test hung on this page.
        RunJobsWithWatchdog(quality, ArcMotionLevel.Full).ShouldBeTrue("the dispatcher never went idle while the ink ran");
    }

    [AvaloniaFact]
    public void Running_ink_in_the_gallery_page_lets_the_dispatcher_go_idle()
    {
        var entry = TKCode.Arc.Gallery.Catalog.GalleryCatalog.AllEntries.First(e => e.ClassName == nameof(InkFluid));
        var page = TKCode.Arc.Gallery.Pages.GalleryPages.Create(entry);
        var host = new Window { Content = page, Width = 1000, Height = 800 };
        RunJobsWithWatchdog(host).ShouldBeTrue("the dispatcher never went idle on the InkFluid gallery page");
    }

    private static bool RunJobsWithWatchdog(InkFluidQuality quality, ArcMotionLevel level)
    {
        var ink = new InkFluid { Width = 760, Height = 440, Quality = quality };
        var window = new Window { Content = ink, Width = 800, Height = 500 };
        ArcMotion.SetLevel(window, level);
        var idle = RunJobsWithWatchdog(window);
        ink.Canvas.ShouldNotBeNull().Solver.Columns.ShouldBeGreaterThan(0);
        return idle;
    }

    /// <summary>
    /// Shows <paramref name="window"/> and drains the dispatcher. A runaway frame loop would keep RunJobs busy forever,
    /// so a dispatcher timer (timers are promoted between jobs, inside RunJobs) closes the window after a while: that
    /// stops the loop and the test fails instead of hanging. Returns whether the dispatcher went idle on its own.
    /// </summary>
    private static bool RunJobsWithWatchdog(Window window)
    {
        var timedOut = false;
        // Above render priority: a runaway frame loop keeps a render job at the head of the queue.
        var watchdog = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(15) };
        watchdog.Tick += (_, _) =>
        {
            watchdog.Stop();
            timedOut = true;
            window.Close();
        };
        watchdog.Start();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            // A few more passes: the loop keeps running, and every pass must still go idle.
            for (var i = 0; i < 3 && !timedOut; i++)
            {
                Thread.Sleep(20);
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            watchdog.Stop();
            if (!timedOut)
            {
                window.Close();
            }
        }
        return !timedOut;
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

        // Without the ambient stir the loop stops once the ink has faded. Where a step costs more than half a frame (a debug
        // build) the canvas paces itself and skips frames, and each step simulates at most 1/30s, so allow a minute of frames.
        for (var second = 0; second < 60 && canvas.Loop.IsActive; second++)
        {
            frames.Advance(TimeSpan.FromSeconds(1), Frame);
            Dispatcher.UIThread.RunJobs();
        }
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
    public void Reduced_motion_reuses_the_still_frame_until_its_shape_changes()
    {
        var ink = new InkFluid { Width = 400, Height = 300 };
        var window = TestHelpers.Show(ink, ArcMotionLevel.Reduced);
        Dispatcher.UIThread.RunJobs();
        var canvas = ink.Canvas!;
        var builds = canvas.StillBuilds;
        builds.ShouldBeGreaterThan(0);

        // A theme change sets both brushes; the intensity only changes the picture.
        ink.InkBrush = Brushes.Red;
        ink.AccentInkBrush = Brushes.Blue;
        ink.Intensity = 0.9;
        canvas.StillBuilds.ShouldBe(builds);

        ink.Tint = 0.1;
        canvas.StillBuilds.ShouldBe(builds + 1);
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
