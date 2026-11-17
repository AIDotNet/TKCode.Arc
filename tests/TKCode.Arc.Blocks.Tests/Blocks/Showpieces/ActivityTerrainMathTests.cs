using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// The terrain's measured numbers (Arc Activity terrain, design reference; spec sections 4, 6 and 8): the fit to the
/// canvas and its pitch table, label thinning, wave delays, spring timings, the color math and the derived summary.
/// </summary>
public class ActivityTerrainMathTests
{
    private const double Frame = 1 / 240.0;

    [Theory]
    [InlineData(623, 292, 802.0)]   // the preview: height-limited, f 801 measured
    [InlineData(1028, 460, 1328.4)] // spec 8: f 1329
    [InlineData(706, 436, 1003.9)]  // spec 8: f 1004
    [InlineData(770, 356, 1002.6)]  // spec 8: f 1004
    public void Rest_Focal_Follows_The_Measured_Rule(double width, double height, double expected) =>
        ActivityTerrainMath.RestFocal(new Size(width, height), narrow: false).ShouldBe(expected, 0.5);

    [Theory]
    [InlineData(416, 280, 649.4)]
    [InlineData(326, 280, 495.6)]
    [InlineData(560, 280, 661.7)] // height-limited at 280
    public void Narrow_Focal_Uses_The_Yaw_50_Rule(double width, double height, double expected) =>
        ActivityTerrainMath.RestFocal(new Size(width, height), narrow: true).ShouldBe(expected, 0.6);

    [Fact]
    public void Rest_Fit_Puts_The_Target_Where_Arc_Does()
    {
        var fit = ActivityTerrainMath.PerspectiveFit(new Size(623, 292), ActivityTerrainMath.Radians(30), narrow: false);
        (fit.Scale * ActivityTerrainMath.Distance).ShouldBe(801, 1.5);
        fit.Offset.X.ShouldBe(16.31, 0.3);
        fit.Offset.Y.ShouldBe(-24.83, 0.3);
    }

    [Theory]
    [InlineData(39.5, 768, 15.1, -27.0)]
    [InlineData(48.9, 697, 12.3, -31.3)]
    [InlineData(58.4, 621, 9.2, -28.5)]
    [InlineData(62.2, 587, 7.8, -25.3)]
    [InlineData(65.7, 559, 6.4, -21.7)]
    [InlineData(69.4, 527, 4.4, -15.9)]
    [InlineData(84, 499, 1.2, -4.5)]
    [InlineData(10, 869, 20.4, 10.5)]
    public void Pitch_Table_Reproduces_The_Measured_Fit(double pitch, double focal, double dx, double dy)
    {
        var fit = ActivityTerrainMath.PerspectiveFit(new Size(623, 292), ActivityTerrainMath.Radians(pitch), narrow: false);
        (fit.Scale * ActivityTerrainMath.Distance).ShouldBe(focal, 2);
        fit.Offset.X.ShouldBe(dx, 0.5);
        fit.Offset.Y.ShouldBe(dy, 0.5);
    }

    [Fact]
    public void Pitch_Table_Is_Smooth_Between_Measurements()
    {
        var size = new Size(623, 292);
        var previous = ActivityTerrainMath.PerspectiveFit(size, ActivityTerrainMath.Radians(10), false).Scale;
        for (var degrees = 10.5; degrees <= 84; degrees += 0.5)
        {
            var scale = ActivityTerrainMath.PerspectiveFit(size, ActivityTerrainMath.Radians(degrees), false).Scale;
            // Monotone: the focal length only shrinks as the camera looks down (no overshoot between the knots).
            scale.ShouldBeLessThanOrEqualTo(previous + 1e-9);
            previous = scale;
        }
    }

    [Fact]
    public void Top_View_Fits_The_Floor_At_The_Measured_Scale()
    {
        var fit = ActivityTerrainMath.TopFit(new Size(623, 292));
        fit.Scale.ShouldBe(21.46, 0.05);
        (fit.Scale * 2 * ActivityTerrainMath.FloorHalfX).ShouldBe(527.9, 1);
        (fit.Scale * 2 * ActivityTerrainMath.FloorHalfZ).ShouldBe(163.1, 1);
        fit.Offset.ShouldBe(default);
    }

    [Fact]
    public void Plot_Height_Is_44_Percent_Clamped()
    {
        ActivityTerrainMath.PlotHeight(663).ShouldBe(291.72, 0.01);
        ActivityTerrainMath.PlotHeight(630).ShouldBe(280);
        ActivityTerrainMath.PlotHeight(990).ShouldBe(435.6, 0.01);
        ActivityTerrainMath.PlotHeight(1312).ShouldBe(460);
        ActivityTerrainMath.PlotHeight(448).ShouldBe(280);
    }

    [Theory]
    [InlineData(12.35, false, 3)] // f 801: Mon / Thu / Sun (two days only 24.7px)
    [InlineData(15.4, false, 2)]  // f 1004: Mon / Wed / Fri / Sun
    [InlineData(20, false, 2)]    // f 1329
    [InlineData(34.6, false, 1)]  // zoom 2.8: every day
    [InlineData(8.6, false, 6)]   // zoom 0.7: Mon / Sun
    [InlineData(29.5, false, 2)]  // pitch 10°: every other day
    [InlineData(21.5, true, 1)]   // top view: all seven
    public void Day_Labels_Thin_To_Keep_Their_Spacing(double pxPerDay, bool top, int step) =>
        ActivityTerrainMath.DayLabelStep(pxPerDay, top).ShouldBe(step);

    [Theory]
    [InlineData(68.8, true)]
    [InlineData(65.7, true)]
    [InlineData(50.6, false)]
    [InlineData(51.5, false)]
    [InlineData(41.8, false)]
    [InlineData(48, false)]
    public void Hour_Labels_Shorten_Below_58px(double pxPerThreeHours, bool longLabels) =>
        ActivityTerrainMath.UsesLongHourLabels(pxPerThreeHours).ShouldBe(longLabels);

    [Fact]
    public void Wave_Delays_Sweep_From_The_Back_Left()
    {
        ActivityTerrainMath.MetricDelay(0, 0).ShouldBe(0);
        ActivityTerrainMath.MetricDelay(6, 23).ShouldBe(0.3193, 0.001); // the last bar ≈ 320ms
        ActivityTerrainMath.MetricDelay(1, 0).ShouldBe(0.0122, 1e-9);
        ActivityTerrainMath.MetricDelay(0, 1).ShouldBe(0.0107, 1e-9);
        ActivityTerrainMath.IntroDelay(0, 0).ShouldBe(0.110, 1e-9);
        ActivityTerrainMath.IntroDelay(6, 23).ShouldBe(0.110 + 6 * 0.0216 + 23 * 0.026, 1e-9);
    }

    [Fact]
    public void Bar_Heights_Are_Linear_In_The_Value()
    {
        ActivityTerrainMath.BarHeight(4745, 4745).ShouldBe(5.384, 1e-9);
        ActivityTerrainMath.BarHeight(145, 4745).ShouldBe(0.1645, 0.001); // the measured 0.166 for the smallest bar
        ActivityTerrainMath.BarHeight(0, 0).ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(SpringTimings))]
    public void Springs_Match_The_Measured_Timings(string name, double half, double ninety)
    {
        var spec = name switch
        {
            "bar" => ActivityTerrainMath.BarSpring,
            "lift" => ActivityTerrainMath.LiftSpring,
            "dim" => ActivityTerrainMath.DimSpring,
            "intro" => ActivityTerrainMath.IntroCameraSpring,
            "fly" => ShowpieceSprings.OrbitFlyIn,
            _ => ShowpieceSprings.CardFollow
        };
        var (t50, t90) = Crossings(spec);
        t50.ShouldBe(half, 0.02);
        t90.ShouldBe(ninety, 0.03);
    }

    public static IEnumerable<object[]> SpringTimings() =>
    [
        ["bar", 0.133, 0.314],
        ["lift", 0.108, 0.256],
        ["dim", 0.072, 0.176],
        ["intro", 0.603, 1.38],
        ["fly", 0.358, 0.825],
        ["card", 0.134, 0.281]
    ];

    private static (double Half, double Ninety) Crossings(SpringSpec spec)
    {
        var channel = new ShowpieceSpringChannel(0, 1e-6, 1e-6);
        channel.SpringTo(1, spec);
        double t = 0, half = double.NaN, ninety = double.NaN;
        while (t < 5 && double.IsNaN(ninety))
        {
            channel.Step(Frame);
            t += Frame;
            if (double.IsNaN(half) && channel.Value >= 0.5)
            {
                half = t;
            }
            if (channel.Value >= 0.9)
            {
                ninety = t;
            }
        }
        return (half, ninety);
    }

    [Fact]
    public void Default_View_Puts_Sunday_Midnight_Nearest_And_Lowest()
    {
        var fit = ActivityTerrainMath.PerspectiveFit(new Size(623, 292), ActivityTerrainMath.Radians(30), false);
        var view = new ShowpieceOrbitView(ActivityTerrainMath.Radians(32), ActivityTerrainMath.Radians(30), 42,
            ActivityTerrainMath.RestTarget, fit.Scale * 42, new Point(311.5 + fit.Offset.X, 146 + fit.Offset.Y));
        var corners = new[]
        {
            new ShowpieceVec3(-12.3, 0, 3.8), // Sun 00:00 corner
            new ShowpieceVec3(12.3, 0, 3.8),
            new ShowpieceVec3(-12.3, 0, -3.8),
            new ShowpieceVec3(12.3, 0, -3.8)
        };
        var points = corners.Select(c =>
        {
            view.Project(c, out var p, out var depth).ShouldBeTrue();
            return (p, depth);
        }).ToArray();
        points[0].depth.ShouldBe(points.Min(x => x.depth));
        points[0].p.Y.ShouldBe(points.Max(x => x.p.Y));
        // Mon 00:00 (left) is left of Mon 23:00 (right) on screen.
        points[2].p.X.ShouldBeLessThan(points[3].p.X);
    }

    [Fact]
    public void Camera_Position_Inverts_The_Orbit_View()
    {
        var target = new ShowpieceVec3(-2.5, 1, 1);
        var view = new ShowpieceOrbitView(0.7, 0.4, 42, target, 900, new Point(300, 150));
        var camera = ActivityTerrainMath.CameraPosition(0.7, 0.4, 42, target);
        view.DepthOf(camera).ShouldBe(0, 1e-9);
        (camera - target).Length.ShouldBe(42, 1e-9);
        var rest = ActivityTerrainMath.CameraPosition(ActivityTerrainMath.Radians(32), ActivityTerrainMath.Radians(30), 42, ActivityTerrainMath.RestTarget);
        rest.X.ShouldBe(-19.27, 0.05);
        rest.Z.ShouldBe(30.84, 0.05);
    }

    [Fact]
    public void Oklab_Round_Trips_And_Mixes_Linearly()
    {
        foreach (var hex in new[] { "#D5D5D5", "#3D3D3D", "#CF1E2E", "#F9F9F9", "#353535" })
        {
            var color = Color.Parse(hex);
            ActivityTerrainOklab.FromColor(color).ToColor().ShouldBe(color);
        }
        var low = ActivityTerrainOklab.FromColor(Color.Parse("#F8F8F8")).Mix(ActivityTerrainOklab.FromColor(Color.Parse("#C4C4C4")), 0.5);
        low.L.ShouldBe(0.899, 0.003); // spec 4.3: the low end of the light scale
        var tone = ActivityTerrainOklab.FromColor(Color.Parse("#353535"));
        tone.L.ShouldBe(0.33, 0.01);
        low.Mix(tone, 0.5).L.ShouldBe((low.L + tone.L) / 2, 1e-12);
        // Sides: every channel scaled (left 0.905, right 0.890).
        var top = ActivityTerrainOklab.FromColor(Color.Parse("#D5D5D5"));
        top.Scale(ActivityTerrainMath.LeftShade).L.ShouldBe(top.L * 0.905, 1e-12);
        // The measured smallest bar: top #D5D5D5 → left side ≈ #B8B8B8 / right ≈ #B4B4B4 (one step).
        Math.Abs(top.Scale(ActivityTerrainMath.LeftShade).ToColor().R - 0xB8).ShouldBeLessThanOrEqualTo(2);
        Math.Abs(top.Scale(ActivityTerrainMath.RightShade).ToColor().R - 0xB4).ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public void Summary_Derives_Totals_Peaks_And_The_Busiest_Day()
    {
        var values = new double[168];
        var previous = new double[168];
        for (var i = 0; i < 168; i++)
        {
            values[i] = 10;
            previous[i] = 8;
        }
        values[2 * 24 + 14] = 500; // Wed 14:00
        values[1 * 24 + 9] = 300;  // Tue 09:00
        values[4 * 24 + 9] = 300;  // Fri 09:00 (same value: Tue first)
        values[6 * 24 + 3] = 0;
        var summary = ActivityTerrainSummary.Create(values, previous, 3, new ActivityTerrainWorkingHours());
        summary.Total.ShouldBe(164 * 10 + 500 + 300 + 300); // Sun 03:00 is zero
        summary.PreviousTotal.ShouldBe(168 * 8);
        summary.Change!.Value.ShouldBe(summary.Total / (168.0 * 8) - 1, 1e-12);
        summary.Peak.ShouldBe(new ActivityTerrainCell(2, 14));
        summary.Peaks.ShouldBe([new ActivityTerrainCell(2, 14), new ActivityTerrainCell(1, 9), new ActivityTerrainCell(4, 9)]);
        summary.BusiestDay.ShouldBe(2);
        summary.DayTotals[2].ShouldBe(23 * 10 + 500);
        // Working hours: Mon–Fri 09:00–17:59 over the week's total.
        var working = 5 * 9 * 10.0 + (500 - 10) + (300 - 10) + (300 - 10);
        summary.WorkingShare.ShouldBe(working / summary.Total, 1e-12);
        summary.ShareOfDay(new ActivityTerrainCell(2, 14)).ShouldBe(500.0 / (23 * 10 + 500), 1e-12);
        summary.ChangeAt(new ActivityTerrainCell(0, 0))!.Value.ShouldBe(0.25, 1e-12);
        summary.DayMax(2).ShouldBe(500);
    }

    [Fact]
    public void Summary_Handles_Missing_And_Empty_Data()
    {
        var empty = ActivityTerrainSummary.Create(null, null, 3, null);
        empty.IsEmpty.ShouldBeTrue();
        empty.Change.ShouldBeNull();
        empty.Peaks.Length.ShouldBe(3);
        var shortList = ActivityTerrainSummary.Create([1, 2, double.NaN, -4], null, 2, null);
        shortList.Total.ShouldBe(3);
        shortList.Peak.ShouldBe(new ActivityTerrainCell(0, 1));
        shortList.PreviousAt(new ActivityTerrainCell(0, 0)).ShouldBeNull();
    }

    [Fact]
    public void Working_Hours_And_Cells_Index_The_Week()
    {
        new ActivityTerrainCell(2, 14).Index.ShouldBe(62);
        ActivityTerrainCell.FromIndex(167).ShouldBe(new ActivityTerrainCell(6, 23));
        new ActivityTerrainCell(7, 0).IsValid.ShouldBeFalse();
        var hours = new ActivityTerrainWorkingHours();
        hours.Contains(new ActivityTerrainCell(0, 9)).ShouldBeTrue();
        hours.Contains(new ActivityTerrainCell(4, 17)).ShouldBeTrue();
        hours.Contains(new ActivityTerrainCell(4, 18)).ShouldBeFalse();
        hours.Contains(new ActivityTerrainCell(5, 12)).ShouldBeFalse();
        var grid = new double[7, 24];
        grid[6, 23] = 9;
        ActivityTerrainMetric.Flatten(grid)[167].ShouldBe(9);
    }
}
