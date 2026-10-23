using Avalonia;
using Avalonia.Headless.XUnit;
using TKCode.Arc.Gallery.Demos;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="GalaxyField"/>: rotation and its pause, drift, the per-dot springs of a grouping change (spec 6.2), dimming
/// (4.6) and the zero-allocation frame.
/// </summary>
public class CustomerGalaxyFieldTests
{
    private const double Frame = 1 / 60.0;

    private static (GalaxyModel Model, GalaxyGroupSet Plan) Data()
    {
        var model = GalaxyModel.Build(CustomerGalaxyDemoData.Accounts, GalaxyHealthState.CreateDefaults().ToList());
        var plan = model.BuildSet("plan", CustomerGalaxyDemoData.PlanGrouping().Groups, "Other");
        plan.EnsureGeometry(model, Enumerable.Repeat(new Size(130, GalaxyLayout.LabelHeight), plan.Count).ToList());
        return (model, plan);
    }

    [AvaloniaFact]
    public void The_Layout_Turns_Clockwise_At_0_028_Radians_Per_Second()
    {
        var (model, _) = Data();
        var field = new GalaxyField();
        field.Load(model, null);
        var start = field.Angle;
        for (var i = 0; i < 600; i++)
        {
            field.Step(Frame, animate: true, rotate: true, spinTarget: 1, GalaxyLayout.RotationSpeed).ShouldBeTrue();
        }
        (field.Angle - start).ShouldBe(0.28, 1e-6);
    }

    [AvaloniaFact]
    public void The_Pointer_On_The_Stage_Eases_The_Spin_To_Rest_And_Leaving_Brings_It_Back()
    {
        var (model, _) = Data();
        var field = new GalaxyField();
        field.Load(model, null);
        // τ ≈ 0.39 s to rest (rate 2.55/s).
        for (var i = 0; i < 23; i++)
        {
            field.Step(Frame, true, true, 0, GalaxyLayout.RotationSpeed);
        }
        field.Spin.ShouldBe(Math.Exp(-23 * Frame * GalaxyField.SpinDecayRate), 0.01);
        var moving = true;
        for (var i = 0; i < 300 && moving; i++)
        {
            moving = field.Step(Frame, true, true, 0, GalaxyLayout.RotationSpeed);
        }
        moving.ShouldBeFalse("nothing moves once the spin is at rest: the stage stops asking for frames");
        field.Spin.ShouldBe(0);
        // Back to full speed with τ ≈ 0.37 s.
        for (var i = 0; i < 22; i++)
        {
            field.Step(Frame, true, true, 1, GalaxyLayout.RotationSpeed);
        }
        field.Spin.ShouldBe(1 - Math.Exp(-22 * Frame * GalaxyField.SpinRecoverRate), 0.01);
    }

    [AvaloniaFact]
    public void The_Overview_Is_A_Tilted_Disc_Whose_Front_Dots_Are_Larger()
    {
        var (model, _) = Data();
        var field = new GalaxyField();
        field.Load(model, null);
        for (var i = 0; i < model.Count; i++)
        {
            field.Target(i, out var x, out var y, out var r);
            var rho = Math.Sqrt(x * x + Math.Pow(y / GalaxyLayout.Squash, 2));
            (Math.Abs(y) <= GalaxyLayout.Squash * rho + 1e-9).ShouldBeTrue();
            r.ShouldBe(model.Radius[i] * GalaxyLayout.DepthScale(y / GalaxyLayout.Squash), 1e-9);
        }
    }

    [AvaloniaFact]
    public void Dots_Drift_Slowly()
    {
        var (model, _) = Data();
        var field = new GalaxyField();
        field.Load(model, null);
        var outer = Enumerable.Range(0, model.Count).OrderByDescending(i => model.Rho[i]).First();
        field.Target(outer, out var x0, out var y0, out _);
        // Without rotation, half a minute of drift moves an outer dot less than about 20 px.
        field.DriftClock += 30;
        field.Target(outer, out var x1, out var y1, out _);
        var moved = Math.Sqrt((x1 - x0) * (x1 - x0) + Math.Pow((y1 - y0) / GalaxyLayout.Squash, 2));
        moved.ShouldBeGreaterThan(0.5);
        moved.ShouldBeLessThan(25);
    }

    [AvaloniaFact]
    public void A_Grouping_Change_Springs_Every_Dot_Into_Its_Slot_As_Measured()
    {
        var (model, plan) = Data();
        var field = new GalaxyField();
        field.Load(model, null);
        var n = model.Count;
        var startX = (double[])field.X.Clone();
        var startY = (double[])field.Y.Clone();
        field.MorphTo(plan, animate: true);
        field.IsMorphing.ShouldBeTrue();
        var tx = new double[n];
        var ty = new double[n];
        for (var i = 0; i < n; i++)
        {
            field.Target(i, out tx[i], out ty[i], out _);
        }
        var distance = new double[n];
        var path = new double[n];
        var t90 = Enumerable.Repeat(double.NaN, n).ToArray();
        var t98 = Enumerable.Repeat(double.NaN, n).ToArray();
        var start = Enumerable.Repeat(double.NaN, n).ToArray();
        var peak = new double[n];
        var peakAt = new double[n];
        var px = (double[])startX.Clone();
        var py = (double[])startY.Clone();
        for (var i = 0; i < n; i++)
        {
            distance[i] = Math.Sqrt(Math.Pow(tx[i] - startX[i], 2) + Math.Pow(ty[i] - startY[i], 2));
        }
        double maxSwell = 0;
        var time = 0.0;
        for (var frame = 0; frame < 240 && field.IsMorphing; frame++)
        {
            field.Step(Frame, animate: true, rotate: false, spinTarget: 1, GalaxyLayout.RotationSpeed);
            time += Frame;
            for (var i = 0; i < n; i++)
            {
                var dx = field.X[i] - px[i];
                var dy = field.Y[i] - py[i];
                var step = Math.Sqrt(dx * dx + dy * dy);
                if (step > 1e-9 && double.IsNaN(start[i]))
                {
                    start[i] = time - Frame;
                }
                path[i] += step;
                var speed = step / Frame;
                if (speed > peak[i])
                {
                    peak[i] = speed;
                    peakAt[i] = time;
                }
                var left = Math.Sqrt(Math.Pow(tx[i] - field.X[i], 2) + Math.Pow(ty[i] - field.Y[i], 2));
                if (double.IsNaN(t90[i]) && left <= 0.1 * distance[i])
                {
                    t90[i] = time;
                }
                if (double.IsNaN(t98[i]) && left <= 0.02 * distance[i])
                {
                    t98[i] = time;
                }
                px[i] = field.X[i];
                py[i] = field.Y[i];
                maxSwell = Math.Max(maxSwell, field.R[i] - model.Radius[i]);
            }
        }
        field.IsMorphing.ShouldBeFalse("every dot settles within 4 s");
        time.ShouldBeLessThan(3.6);
        var far = Enumerable.Range(0, n).Where(i => distance[i] > 100).ToArray();
        far.Length.ShouldBeGreaterThan(500);
        double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            return sorted[sorted.Length / 2];
        }
        // About 70% start at once (wandering slowly), the rest 140–330 ms later.
        var prompt = far.Count(i => start[i] <= Frame + 1e-9) / (double)far.Length;
        prompt.ShouldBeInRange(0.62, 0.78);
        far.Where(i => start[i] > Frame + 1e-9).All(i => start[i] >= 0.12 && start[i] <= 0.36).ShouldBeTrue();
        // 90% of the way in 0.5–0.9 s, 98% in about 1.0–1.3 s (spec 6.2).
        Median(far.Select(i => t90[i])).ShouldBeInRange(0.45, 0.9);
        Median(far.Select(i => t98[i])).ShouldBeInRange(0.75, 1.35);
        // Peak speed about 230–310 ms after the switch for dots that set off at once; 700–1100 px/s for 250 px of travel.
        Median(far.Where(i => start[i] <= Frame + 1e-9).Select(i => peakAt[i])).ShouldBeInRange(0.2, 0.36);
        Median(far.Select(i => peak[i] / distance[i])).ShouldBeInRange(2.6, 4.6);
        // Curved, spiralling paths: 1.1–1.6 times the straight line.
        Median(far.Select(i => path[i] / distance[i])).ShouldBeInRange(1.05, 1.6);
        // Flying dots swell by up to 0.3 px.
        maxSwell.ShouldBeInRange(0.15, GalaxyField.SwellSize + 0.05);
    }

    [AvaloniaFact]
    public void Without_Animation_A_Grouping_Change_Lands_At_Once()
    {
        var (model, plan) = Data();
        var field = new GalaxyField();
        field.Load(model, null);
        field.MorphTo(plan, animate: false);
        field.IsMorphing.ShouldBeFalse();
        for (var i = 0; i < model.Count; i += 37)
        {
            field.Target(i, out var x, out var y, out var r);
            field.X[i].ShouldBe(x);
            field.Y[i].ShouldBe(y);
            field.R[i].ShouldBe(r);
        }
    }

    [AvaloniaFact]
    public void An_Interrupted_Morph_Keeps_Its_Speed()
    {
        var (model, plan) = Data();
        var field = new GalaxyField();
        field.Load(model, null);
        field.MorphTo(plan, animate: true);
        for (var i = 0; i < 21; i++)
        {
            field.Step(Frame, true, false, 1, 0);
        }
        var x = (double[])field.X.Clone();
        var y = (double[])field.Y.Clone();
        field.MorphTo(null, animate: true);
        field.Step(Frame, true, false, 1, 0);
        // Dots in full flight keep moving on the first frame instead of holding still for a new launch.
        var moved = Enumerable.Range(0, model.Count).Count(i => Math.Abs(field.X[i] - x[i]) + Math.Abs(field.Y[i] - y[i]) > 3);
        moved.ShouldBeGreaterThan(model.Count / 3);
    }

    [AvaloniaFact]
    public void Dimming_Fades_To_One_Seventh_In_About_A_Quarter_Second_In_Seven_Levels()
    {
        var (model, plan) = Data();
        var field = new GalaxyField();
        field.Load(model, plan);
        field.DimAllBut(0, animate: true);
        field.IsDimming.ShouldBeTrue();
        var levels = new HashSet<int>();
        var time = 0.0;
        while (field.IsDimming && time < 2)
        {
            field.Step(Frame, true, false, 1, 0);
            time += Frame;
            levels.Add(field.DimLevels[1]);
            field.DimLevels[0].ShouldBe(GalaxyLayout.DimLevels - 1);
        }
        field.DimLevels[1].ShouldBe(0);
        // Rate 15/s: within 2e-3 of 1/7 after about 0.4 s; the level reaches the bottom after about 0.26 s.
        time.ShouldBeInRange(0.25, 0.5);
        levels.Count.ShouldBeGreaterThanOrEqualTo(5);
        field.DimAllBut(-1, animate: false);
        field.DimLevels.ShouldAllBe(level => level == GalaxyLayout.DimLevels - 1);
    }

    [AvaloniaFact]
    public void Frames_Allocate_Nothing()
    {
        var (model, plan) = Data();
        var field = new GalaxyField();
        field.Load(model, null);
        for (var i = 0; i < 30; i++)
        {
            field.Step(Frame, true, true, 1, GalaxyLayout.RotationSpeed);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 120; i++)
        {
            field.Step(Frame, true, true, 1, GalaxyLayout.RotationSpeed);
        }
        GC.GetAllocatedBytesForCurrentThread().ShouldBe(before, "rotating overview");
        field.MorphTo(plan, animate: true);
        field.Step(Frame, true, true, 1, GalaxyLayout.RotationSpeed);
        field.DimAllBut(1, animate: true);
        field.Step(Frame, true, true, 1, GalaxyLayout.RotationSpeed);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++)
        {
            field.Step(Frame, true, true, 1, GalaxyLayout.RotationSpeed);
        }
        GC.GetAllocatedBytesForCurrentThread().ShouldBe(before, "morph and dimming");
    }
}
