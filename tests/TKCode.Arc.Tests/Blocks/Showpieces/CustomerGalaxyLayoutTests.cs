using Avalonia;
using Avalonia.Headless.XUnit;
using TKCode.Arc.Gallery.Demos;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="GalaxyLayout"/> and <see cref="GalaxyModel"/>: the measured formulas of Arc's Customer galaxy (spec 4.2–4.7,
/// section 8), the spiral, the cluster packing and arrangement, and the seeded demo data (section 11).
/// </summary>
public class CustomerGalaxyLayoutTests
{
    [Theory]
    [InlineData(990, 1.18)]
    [InlineData(1870, 1.51)]
    [InlineData(4200, 2.11)]
    [InlineData(92000, 8.79)]
    [InlineData(111, 0.74)]
    [InlineData(49, 0.74)]
    [InlineData(0, 0.74)]
    public void Dot_Radius_Is_The_Square_Root_Fit_With_A_Floor(double mrr, double radius)
    {
        GalaxyLayout.DotRadius(mrr).ShouldBe(radius, 0.01);
    }

    [Fact]
    public void Front_Dots_Are_Drawn_About_Twelve_Percent_Larger()
    {
        GalaxyLayout.DepthScale(0).ShouldBe(1);
        GalaxyLayout.DepthScale(180).ShouldBe(1.12, 0.005);
        GalaxyLayout.DepthScale(-180).ShouldBe(0.88, 0.005);
    }

    [Theory]
    [InlineData(862, 448.24)]
    [InlineData(808, 420.16)]
    [InlineData(628, 360)]
    [InlineData(619, 380)]
    [InlineData(574, 380)]
    [InlineData(356, 380)]
    public void Narrow_Stage_Height_Follows_The_Width(double width, double height)
    {
        GalaxyLayout.StageHeight(width).ShouldBe(height, 0.01);
    }

    [Fact]
    public void Side_Stage_Is_Sixteen_By_Nine_Up_To_560_And_Never_Shorter_Than_The_Panel()
    {
        GalaxyLayout.SideStageHeight(1022, 522).ShouldBe(560);
        GalaxyLayout.SideStageHeight(700, 514).ShouldBe(514);
        GalaxyLayout.SideStageHeight(900, 300).ShouldBe(900 * 9.0 / 16);
        GalaxyLayout.SideStageHeight(400, 200).ShouldBe(360);
    }

    [Theory]
    [InlineData(663, 360, 1.00)]
    [InlineData(628, 360, 1.00)]
    [InlineData(538, 380, 1.06)]
    [InlineData(446, 380, 1.00)]
    [InlineData(356, 380, 0.77)]
    [InlineData(700, 514, 1.49)]
    [InlineData(1022, 560, 1.64)]
    public void Overview_Fit_Matches_The_Measured_Scales(double width, double height, double scale)
    {
        GalaxyLayout.AllFit(new Size(width, height), GalaxyLayout.ReferenceCoreRadius).ShouldBe(scale, 0.02);
    }

    [Fact]
    public void Overview_Sits_Seven_Pixels_Above_Center_At_Fit()
    {
        var fit = GalaxyLayout.AllFit(new Size(663, 360), GalaxyLayout.ReferenceCoreRadius);
        var content = GalaxyLayout.AllContent(GalaxyLayout.ReferenceCoreRadius, fit);
        // The camera centers the content: the galaxy's center (world 0, 0) lands 7 px above the stage center.
        (content.Center.Y * fit).ShouldBe(7, 0.01);
        // At fit the content plus the 14 px pan insets fits the stage, so any drag is out of bounds.
        (content.Width * fit + 28).ShouldBeLessThan(663);
        (content.Height * fit + 28).ShouldBeLessThan(360);
    }

    [Fact]
    public void A_Flight_Frames_Disc_Gap_And_Label_In_Three_Quarters_Of_The_Height()
    {
        var center = new Point(40, -30);
        var (view, scale) = GalaxyLayout.ClusterFlight(center, 60, new Size(663, 360));
        (2 * 60 * scale + GalaxyLayout.LabelGap + GalaxyLayout.LabelHeight).ShouldBe(0.75 * 360, 0.01);
        // The disc's center sits half a label (23.7 px) below the stage center.
        ((center.Y - view.Y) * scale).ShouldBe(23.7, 0.01);
        view.X.ShouldBe(center.X);
        // Wide clusters on narrow stages are limited by the width.
        var (_, narrow) = GalaxyLayout.ClusterFlight(center, 150, new Size(300, 700));
        (2 * 150 * narrow).ShouldBe(0.85 * 300, 0.01);
    }

    [Fact]
    public void Dimming_Quantizes_To_Seven_Levels()
    {
        GalaxyLayout.DimLevel(1).ShouldBe(6);
        GalaxyLayout.DimLevel(GalaxyLayout.DimFactor).ShouldBe(0);
        GalaxyLayout.DimLevel(0.43).ShouldBe(2);
        GalaxyLayout.DimLevel(0.57).ShouldBe(3);
        GalaxyLayout.DimFactor.ShouldBe(0.143, 0.001);
        Enumerable.Range(0, 101).Select(i => GalaxyLayout.DimLevel(i / 100.0)).Distinct().Count().ShouldBe(7);
    }

    [Fact]
    public void Clusters_Pack_Largest_First_Without_Overlap_At_The_Measured_Density()
    {
        var seed = new ShowpieceSeed(7);
        var radii = Enumerable.Range(0, 300).Select(i => GalaxyLayout.DotRadius(60000 / (1 + i * 0.4))).ToArray();
        var x = new double[radii.Length];
        var y = new double[radii.Length];
        var outer = GalaxyLayout.Pack(radii, seed, x, y);
        // The largest sits in the middle; distance from the center grows with rank.
        Math.Sqrt(x[0] * x[0] + y[0] * y[0]).ShouldBeLessThan(0.5);
        Math.Sqrt(x[^1] * x[^1] + y[^1] * y[^1]).ShouldBeGreaterThan(outer * 0.8);
        // Dots cover a little over half of the disc.
        var area = radii.Sum(r => r * r);
        (area / (outer * outer)).ShouldBeInRange(0.40, 0.62);
        // The ten largest never overlap.
        for (var i = 0; i < 10; i++)
        {
            for (var j = i + 1; j < 10; j++)
            {
                var d = Math.Sqrt((x[i] - x[j]) * (x[i] - x[j]) + (y[i] - y[j]) * (y[i] - y[j]));
                d.ShouldBeGreaterThan((radii[i] + radii[j]) * 0.9, $"{i} and {j}");
            }
        }
    }

    [Fact]
    public void Clusters_Arrange_Around_The_Largest_Without_Touching()
    {
        double[] radii = [64, 58, 50, 45, 40, 36];
        var labels = Enumerable.Repeat(new Size(140, 44), radii.Length).ToArray();
        var centers = new Point[radii.Length];
        GalaxyLayout.Arrange(radii, labels, 11, 26, 9, centers);
        centers[0].ShouldBe(default(Point));
        // Second to the left, third to the right on the same line; fourth lower left; fifth upper right; sixth upper left.
        centers[1].X.ShouldBeLessThan(0);
        centers[1].Y.ShouldBe(0, 0.001);
        centers[2].X.ShouldBeGreaterThan(0);
        centers[2].Y.ShouldBe(0, 0.001);
        (centers[3].X < 0 && centers[3].Y > 0).ShouldBeTrue();
        (centers[4].X > 0 && centers[4].Y < 0).ShouldBeTrue();
        (centers[5].X < 0 && centers[5].Y < 0).ShouldBeTrue();
        for (var i = 0; i < radii.Length; i++)
        {
            for (var j = i + 1; j < radii.Length; j++)
            {
                var d = Math.Sqrt(Math.Pow(centers[i].X - centers[j].X, 2) + Math.Pow(centers[i].Y - centers[j].Y, 2));
                d.ShouldBeGreaterThanOrEqualTo(radii[i] + radii[j] + 26 - 0.01);
                var a = GalaxyLayout.LabelRect(centers[i], radii[i], labels[i], 11);
                var b = GalaxyLayout.LabelRect(centers[j], radii[j], labels[j], 11);
                a.Inflate(12.9).Intersects(b.Inflate(12.9)).ShouldBeFalse($"labels {i} and {j}");
            }
        }
    }

    [Fact]
    public void Group_Fit_Keeps_The_Insets()
    {
        Point[] centers = [new(0, 0), new(-170, 0), new(170, 0), new(-130, 170)];
        double[] radii = [64, 58, 48, 50];
        var labels = Enumerable.Repeat(new Size(130, GalaxyLayout.LabelHeight), 4).ToArray();
        var stage = new Size(663, 360);
        var fit = GalaxyLayout.GroupFit(centers, radii, labels, stage, out var content);
        var box = GalaxyLayout.Bounds(centers, radii, labels, fit);
        var i = GalaxyLayout.GroupFrameInsets;
        // One side is full: the height (20 above, 56 below) or the width (24 a side).
        var fullHeight = Math.Abs(box.Height * fit - (stage.Height - i.Top - i.Bottom)) < 0.5;
        var fullWidth = Math.Abs(box.Width * fit - (stage.Width - i.Left - i.Right)) < 0.5;
        (fullHeight || fullWidth).ShouldBeTrue();
        // The view's center at fit is the safe area's: the box's top lands 20 px from the stage top when height-limited.
        var top = (box.Top - content.Center.Y) * fit + stage.Height / 2;
        if (fullHeight)
        {
            top.ShouldBe(i.Top, 0.5);
        }
    }

    [AvaloniaFact]
    public void The_Spiral_Is_Deterministic_With_A_Core_Two_Trailing_Arms_And_A_Halo()
    {
        var accounts = CustomerGalaxyDemoData.Accounts;
        var a = GalaxyModel.Build(accounts, GalaxyHealthState.CreateDefaults().ToList());
        var b = GalaxyModel.Build(accounts, GalaxyHealthState.CreateDefaults().ToList());
        a.Rho.ShouldBe(b.Rho);
        a.Theta.ShouldBe(b.Theta);
        // The reference galaxy's core is about 95 units across its radius.
        a.CoreRadius.ShouldBeInRange(80, 105);
        var core = (int)Math.Round(a.Count * GalaxyLayout.CoreShare);
        // Core: the largest accounts, inside the core radius; the largest in the middle.
        a.Rho[0].ShouldBeLessThan(1);
        Enumerable.Range(0, core).Count(i => a.Rho[i] <= a.CoreRadius + 2).ShouldBeGreaterThan((int)(core * 0.98));
        // Bigger dots sit nearer the center: the mean radius of a dot falls with distance.
        double MeanRadius(double from, double to) =>
            Enumerable.Range(0, a.Count).Where(i => a.Rho[i] >= from && a.Rho[i] < to).Average(i => a.Radius[i]);
        MeanRadius(0, 20).ShouldBeGreaterThan(MeanRadius(60, 80));
        MeanRadius(60, 80).ShouldBeGreaterThan(MeanRadius(100, 120));
        MeanRadius(100, 120).ShouldBeGreaterThan(MeanRadius(160, 400) * 0.99);
        // Arms trail: for arm dots, angle + twist·(radius − core) clusters around two values 180° apart.
        var size = a.CoreRadius / GalaxyLayout.ReferenceCoreRadius;
        var phases = Enumerable.Range(core, a.Count - core)
                               .Where(i => a.Rho[i] > a.CoreRadius + 10 && a.Rho[i] < a.CoreRadius + 100 * size)
                               .Select(i => ShowpieceProjection.WrapAngle(2 * (a.Theta[i] + GalaxyLayout.ArmTwist * (a.Rho[i] - a.CoreRadius) / size)))
                               .ToArray();
        // Doubling the angle folds the two arms onto one direction: most phases agree.
        var meanX = phases.Average(Math.Cos);
        var meanY = phases.Average(Math.Sin);
        Math.Sqrt(meanX * meanX + meanY * meanY).ShouldBeGreaterThan(0.6);
        // A few accounts scatter as a halo beyond the arms' width.
        a.Rho.Max().ShouldBeLessThan(300 * size);
    }

    [AvaloniaFact]
    public void The_Demo_Data_Follows_The_Recipe()
    {
        var accounts = CustomerGalaxyDemoData.Accounts;
        accounts.Count.ShouldBe(2400);
        CustomerGalaxyDemoData.Create().Select(x => x.Mrr).ShouldBe(accounts.Select(x => x.Mrr));
        var total = accounts.Sum(x => x.Mrr);
        total.ShouldBeInRange(5_800_000, 6_200_000);
        double Share(string plan) => accounts.Where(x => x.Groups["plan"] == plan).Sum(x => x.Mrr) / total;
        Share("corporate").ShouldBeInRange(0.40, 0.50);
        Share("business").ShouldBeInRange(0.28, 0.38);
        Share("team").ShouldBeInRange(0.13, 0.21);
        Share("solo").ShouldBeInRange(0.02, 0.06);
        accounts.Count(x => x.Groups["plan"] == "solo").ShouldBe(1350);
        (accounts.Count(x => x.Health == "steady") / 2400.0).ShouldBeInRange(0.66, 0.74);
        accounts.Select(x => x.Name).Distinct().Count().ShouldBe(2400);
        accounts.Select(x => x.Id).Distinct().Count().ShouldBe(2400);
    }

    [AvaloniaFact]
    public void Group_Sets_Total_And_Sort_By_Mrr()
    {
        var model = GalaxyModel.Build(CustomerGalaxyDemoData.Accounts, GalaxyHealthState.CreateDefaults().ToList());
        var plan = model.BuildSet("plan", CustomerGalaxyDemoData.PlanGrouping().Groups, "Other");
        plan.Groups.Select(g => g.Key).ShouldBe(["corporate", "business", "team", "solo"]);
        plan.Groups.Sum(g => g.Mrr).ShouldBe(model.TotalMrr, 0.01);
        plan.Groups.Sum(g => g.AccountCount).ShouldBe(2400);
        foreach (var group in plan.Groups)
        {
            group.HealthMrr.Sum().ShouldBe(group.Mrr, 0.01);
            group.AtRiskShare.ShouldBe(group.HealthMrr[3] / group.Mrr, 1e-9);
            group.GrowthMrr.ShouldBe(group.HealthMrr[0], 1e-9);
        }
        // The health set is grouped by state and labelled from the states.
        model.HealthSet.Groups.Select(g => g.Label).ShouldContain("At risk");
        model.HealthSet.Groups[0].Key.ShouldBe("steady");
        // Members are listed largest first.
        foreach (var members in plan.Members)
        {
            for (var i = 1; i < members.Length; i++)
            {
                model.Mrr[members[i - 1]].ShouldBeGreaterThanOrEqualTo(model.Mrr[members[i]]);
            }
        }
        // Accounts without a value go to "Other"; definitions nobody uses are dropped.
        var extra = new GalaxyAccount { Name = "Loner", Mrr = 10, Health = "steady" };
        var withOther = GalaxyModel.Build([.. CustomerGalaxyDemoData.Accounts, extra], GalaxyHealthState.CreateDefaults().ToList())
            .BuildSet("plan", [new GalaxyGroupDefinition { Key = "nobody" }, .. CustomerGalaxyDemoData.PlanGrouping().Groups], "Other");
        withOther.Groups.Select(g => g.Label).ShouldContain("Other");
        withOther.Groups.Select(g => g.Key).ShouldNotContain("nobody");
    }
}
