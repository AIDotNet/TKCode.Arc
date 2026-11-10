using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// What the four mascots cost per frame: the frame step (pose, springs, outline points) allocates next to nothing in
/// every status, and a settled AI mascot stops asking for redraws between blinks. Set MASCOT_COST_REPORT to a file path
/// to also write the measured bytes and time per frame.
/// </summary>
public class MascotFrameCostTests
{
    private const double Frame = 1.0 / 60;
    private const int Frames = 600;

    // A brush is replaced when a face's shade changes by a whole color step, so a few bytes per frame are expected; a
    // path edited in place cost thousands.
    private const double StepBudget = 96;

    [AvaloniaFact]
    public void FrameSteps_AllocateNextToNothing()
    {
        var report = new StringBuilder();
        foreach (var status in Enum.GetValues<AiMascotStatus>())
        {
            var mascot = new AiMascot { Status = status };
            Measure(report, $"AiMascot/{status}", mascot, () => mascot.CanvasPartControl!.Step(Frame)).ShouldBeLessThan(StepBudget);
        }
        foreach (var status in Enum.GetValues<GhostMascotStatus>())
        {
            var mascot = new GhostMascot { Status = status };
            Measure(report, $"Ghost/{status}", mascot, () => mascot.Advance(Frame)).ShouldBeLessThan(StepBudget);
        }
        foreach (var status in Enum.GetValues<CubeMascotStatus>())
        {
            var mascot = new CubeMascot { Status = status };
            Measure(report, $"Cube/{status}", mascot, () => mascot.Advance(Frame)).ShouldBeLessThan(StepBudget);
        }
        foreach (var status in Enum.GetValues<PixelMascotStatus>())
        {
            var mascot = new PixelMascot { Status = status };
            Measure(report, $"Pixel/{status}", mascot, () => mascot.AdvanceTicks(1)).ShouldBeLessThan(StepBudget);
        }
        if (Environment.GetEnvironmentVariable("MASCOT_COST_REPORT") is { Length: > 0 } path)
        {
            report.AppendLine($"InOut.Ease(1) = {ArcEasings.InOut.Ease(1):R}");
            report.AppendLine($"one arc: edit in place {EditArcInPlace():F0} B, new path {NewArcPath():F0} B");
            File.WriteAllText(path, report.ToString());
        }
    }

    [AvaloniaFact]
    public void AiMascot_SettledError_DrawsOnlyWhenItBlinks()
    {
        var mascot = new AiMascot { Status = AiMascotStatus.Error, FollowPointer = AiMascotPointerFollow.Never };
        TestHelpers.Show(mascot, ArcMotionLevel.Full);
        var canvas = mascot.CanvasPartControl!;
        // The shake, the blend out of the still pose and the eye spring settle within a few seconds.
        Tick(canvas, 360);
        var before = canvas.FramesRequested;
        Tick(canvas, 600);
        var drawn = canvas.FramesRequested - before;
        // Ten seconds hold two to five blinks of 160ms (about ten frames each), not 600 redraws.
        drawn.ShouldBeGreaterThan(0);
        drawn.ShouldBeLessThan(120);

        // A live state still draws every frame.
        mascot.Status = AiMascotStatus.Thinking;
        Tick(canvas, 60);
        before = canvas.FramesRequested;
        Tick(canvas, 60);
        // At least one per step here (the render tick steps the loop as well).
        (canvas.FramesRequested - before).ShouldBeGreaterThanOrEqualTo(60);
    }

    private static void Tick(AiMascotCanvas canvas, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            canvas.Step(Frame);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    // Returns the bytes one frame step allocates (averaged).
    private static double Measure(StringBuilder report, string name, Control control, Action step)
    {
        var window = TestHelpers.Show(control, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        for (var i = 0; i < 120; i++)
        {
            step();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < Frames; i++)
        {
            step();
        }
        watch.Stop();
        var stepBytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)Frames;
        var stepMicros = watch.Elapsed.TotalMicroseconds / Frames;

        before = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        for (var i = 0; i < Frames; i++)
        {
            step();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        watch.Stop();
        var tickBytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)Frames;
        var tickMicros = watch.Elapsed.TotalMicroseconds / Frames;
        report.AppendLine($"{name,-22} step {stepBytes,8:F0} B {stepMicros,7:F2} us | step+render {tickBytes,8:F0} B {tickMicros,7:F2} us");
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return stepBytes;
    }

    // What moving the end of a one-arc path costs each way (the dials and rings keep editing in place: see the report).
    private static double EditArcInPlace()
    {
        var segment = new ArcSegment { Size = new Size(10, 10) };
        var figure = new PathFigure { IsClosed = false, IsFilled = false };
        figure.Segments!.Add(segment);
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        for (var i = 0; i < 100; i++)
        {
            segment.Point = new Point(i, 1);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            segment.Point = new Point(i, 2);
        }
        return (GC.GetAllocatedBytesForCurrentThread() - before) / 1000.0;
    }

    private static double NewArcPath()
    {
        Geometry? last = null;
        for (var pass = 0; pass < 2; pass++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                var geometry = new StreamGeometry();
                using (var path = geometry.Open())
                {
                    path.BeginFigure(new Point(0, 0), false);
                    path.ArcTo(new Point(i, 2), new Size(10, 10), 0, false, SweepDirection.Clockwise);
                    path.EndFigure(false);
                }
                last = geometry;
            }
            if (pass == 1)
            {
                GC.KeepAlive(last);
                return (GC.GetAllocatedBytesForCurrentThread() - before) / 1000.0;
            }
        }
        return 0;
    }
}
