using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>Pure engines of Blocks/Analytics/_Shared: seed, ticks, scales, curves, formats, colours, axis, readout placement.</summary>
public class AnalyticsSharedTests
{
    private const double Eps = 1e-9;

    // Seed ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Seed_Is_Mulberry32()
    {
        var seed = new AnalyticsSeed(1);
        seed.NextDouble().ShouldBe(0.6270739405881613, 1e-15);
        seed.NextDouble().ShouldBe(0.002735721180215478, 1e-15);
        seed.NextDouble().ShouldBe(0.5274470399599522, 1e-15);
    }

    // Ticks ---------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1374, 1500, 500)]
    [InlineData(1448, 1500, 500)]
    [InlineData(63, 75, 25)]
    [InlineData(67, 75, 25)]
    [InlineData(431, 600, 200)]
    [InlineData(8855, 15000, 5000)]
    public void Fixed_Rows_From_Zero_Match_Metrics_Dashboard(double max, double top, double step)
    {
        var ticks = AnalyticsTicks.FixedRowsFromZero(max);
        ticks.Min.ShouldBe(0);
        ticks.Max.ShouldBe(top);
        ticks.Step.ShouldBe(step);
        ticks.Ticks.Length.ShouldBe(4);
    }

    [Theory]
    [InlineData(4.1, 4.86)]
    [InlineData(37.2, 42.7)]
    [InlineData(4.2, 5.2)]
    public void Fixed_Rows_Always_Cover_The_Data(double min, double max)
    {
        var ticks = AnalyticsTicks.FixedRows(min, max);
        ticks.Ticks.Length.ShouldBe(4);
        ticks.Min.ShouldBeLessThanOrEqualTo(min);
        ticks.Max.ShouldBeGreaterThanOrEqualTo(max);
        AnalyticsTicks.Ladder.ShouldContain(ticks.Step / Math.Pow(10, Math.Floor(Math.Log10(ticks.Step))), 1e-9);
    }

    [Fact]
    public void Padded_Rows_Reproduce_Metric_Explorer()
    {
        var ticks = AnalyticsTicks.PaddedRows(2170, 2330);
        ticks.Min.ShouldBe(2150);
        ticks.Max.ShouldBe(2350);
        ticks.Step.ShouldBe(50);
        ticks.Ticks.Length.ShouldBe(5);
        var fine = AnalyticsTicks.PaddedRows(1.225, 1.295);
        fine.Step.ShouldBe(0.02);
        fine.Min.ShouldBeLessThanOrEqualTo(1.225);
        fine.Max.ShouldBeGreaterThanOrEqualTo(1.295);
    }

    [Fact]
    public void Waterfall_Ticks_Extend_Below_And_Step_Near_A_Sixth()
    {
        var ticks = AnalyticsTicks.Waterfall(380_000, 440_000);
        ticks.Step.ShouldBe(20_000);
        ticks.Min.ShouldBe(340_000);
        ticks.Max.ShouldBe(460_000);
        ticks.Ticks.Length.ShouldBeInRange(7, 9);
    }

    [Theory]
    [InlineData(400, 500, 100)]
    [InlineData(1000, 1250, 250)]
    [InlineData(2000, 2500, 500)]
    public void Headroom_Ticks_Match_Uptime(double max, double top, double step)
    {
        var ticks = AnalyticsTicks.Headroom(max);
        ticks.Max.ShouldBe(top);
        ticks.Step.ShouldBe(step);
        ticks.Ticks.Length.ShouldBeInRange(4, 6);
    }

    [Fact]
    public void Nice_Ticks_Give_Four_Or_Five_From_Zero()
    {
        var ticks = AnalyticsTicks.Nice(0, 1374);
        ticks.Max.ShouldBe(1500);
        ticks.Ticks.ShouldBe([0, 500, 1000, 1500]);
        AnalyticsTicks.Nice(0, 0.3).Ticks.ShouldAllBe(t => t == AnalyticsTicks.Clean(t));
    }

    [Fact]
    public void Fit_Height_Keeps_The_Domain_And_Drops_Ticks_At_The_Top()
    {
        var ticks = AnalyticsTicks.FitHeight(-45_500, 124_500, 228);
        ticks.Min.ShouldBe(-45_500);
        ticks.Max.ShouldBe(124_500);
        ticks.Ticks.ShouldContain(0);
        ticks.Ticks.Length.ShouldBeLessThanOrEqualTo(7);
        ticks.Ticks.ShouldAllBe(t => t >= -45_500 && t <= 124_500);
    }

    [Fact]
    public void Min_Mid_Max_And_Decimals()
    {
        AnalyticsTicks.MinMidMax(10, 20).Ticks.ShouldBe([20, 15, 10]);
        AnalyticsTicks.DecimalsFor(0.25).ShouldBe(2);
        AnalyticsTicks.DecimalsFor(2.5).ShouldBe(1);
        AnalyticsTicks.DecimalsFor(500).ShouldBe(0);
        AnalyticsTicks.NiceNearest(16_500).ShouldBe(20_000);
        AnalyticsTicks.NiceCeiling(143.7).ShouldBe(200);
        AnalyticsTicks.NiceCeiling(42.4, AnalyticsTicks.FineLadder).ShouldBe(50);
    }

    // Scales ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Point_Scale_Switches_Half_Way()
    {
        var x = new AnalyticsPointScale(31, 0, 600);
        x.Map(10).ShouldBe(200);
        x.NearestIndex(209).ShouldBe(10);
        x.NearestIndex(211).ShouldBe(11);
        x.NearestIndex(-50).ShouldBe(0);
        x.NearestIndex(9000).ShouldBe(30);
        new AnalyticsBandScale(7, 48, 822).IndexAt(100).ShouldBe(0);
        new AnalyticsLinearScale(0, 1500, 200, 0).Map(750).ShouldBe(100);
        AnalyticsPixels.Crisp(10.2).ShouldBe(10.5);
    }

    // Curves ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Monotone_Tangents_Are_D3_MonotoneX()
    {
        var peak = new Point[] { new(0, 0), new(1, 1), new(2, 0) };
        var t = new double[3];
        AnalyticsCurves.MonotoneTangents(peak, t);
        t.ShouldBe([1.5, 0, -1.5]);

        var rising = new Point[] { new(0, 0), new(1, 1), new(2, 3) };
        AnalyticsCurves.MonotoneTangents(rising, t);
        t[1].ShouldBe(1.5, Eps);
        t[0].ShouldBe(0.75, Eps);
        t[2].ShouldBe(2.25, Eps);
    }

    [Fact]
    public void Harmonic_Tangents_Match_Metrics_Dashboard()
    {
        var rising = new Point[] { new(0, 0), new(1, 1), new(2, 3) };
        var t = new double[3];
        AnalyticsCurves.HarmonicTangents(rising, t);
        t[1].ShouldBe(4.0 / 3, Eps);
        t[0].ShouldBe(1, Eps);
        t[2].ShouldBe(2, Eps);
        // Hermite through the points: exact at the knots.
        AnalyticsCurves.ValueAt(rising, t, 1).ShouldBe(1, Eps);
        AnalyticsCurves.ValueAt(rising, t, 0.5).ShouldBeInRange(0, 1);
    }

    [Fact]
    public void Catmull_Rom_Controls_Duplicate_The_Ends()
    {
        var points = new Point[] { new(0, 0), new(1, 1), new(2, 0) };
        var (c1, c2) = AnalyticsCurves.CatmullRomControls(points, 0);
        c1.X.ShouldBe(1.0 / 6, Eps);
        c1.Y.ShouldBe(1.0 / 6, Eps);
        c2.X.ShouldBe(2.0 / 3, Eps);
        c2.Y.ShouldBe(1, Eps);
    }

    [Fact]
    public void Catmull_Rom_Arc_Length_Table()
    {
        var path = new AnalyticsCatmullRom();
        path.SetPoints([new(0, 0), new(1, 0), new(2, 0), new(3, 0)]);
        path.Length.ShouldBe(3, 1e-6);
        path.LengthAt(3).ShouldBe(3, 1e-6);
        path.UAtLength(1.5).ShouldBe(1.5, 1e-3);
        path.PointAt(1).ShouldBe(new Point(1, 0));
        path.LengthAt(path.UAtLength(2.2)).ShouldBe(2.2, 1e-6);
        path.TangentAt(1.5).X.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Linear_Resample_Keeps_The_Ends()
    {
        var into = new double[5];
        AnalyticsCurves.ResampleLinear([0, 10], into);
        into.ShouldBe([0, 2.5, 5, 7.5, 10]);
        var one = new double[3];
        AnalyticsCurves.ResampleLinear([4], one);
        one.ShouldBe([4, 4, 4]);
    }

    [Fact]
    public void Geometry_Builders_Cover_Their_Points()
    {
        var points = new Point[] { new(0, 50), new(50, 10), new(100, 30) };
        foreach (var curve in Enum.GetValues<AnalyticsCurve>())
        {
            var line = AnalyticsGeometry.Line(points, curve);
            line.Bounds.Left.ShouldBe(0, 0.01);
            line.Bounds.Right.ShouldBe(100, 0.01);
            var area = AnalyticsGeometry.Area(points, 100, curve);
            area.Bounds.Bottom.ShouldBe(100, 0.01);
            AnalyticsGeometry.Band(points, points.Select(p => new Point(p.X, p.Y + 20)).ToArray(), curve).Bounds.Bottom.ShouldBe(70, 0.5);
        }
        // Monotone never overshoots the data's range.
        AnalyticsGeometry.Line(points, AnalyticsCurve.Monotone).Bounds.Top.ShouldBeGreaterThanOrEqualTo(10 - 0.01);
        AnalyticsGeometry.RoundedRect(new Rect(0, 0, 20, 40), 3, 3, 0, 0).Bounds.ShouldBe(new Rect(0, 0, 20, 40));
        AnalyticsGeometry.Ribbon(0, 0, 10, 100, 40, 50).Bounds.Bottom.ShouldBe(50, 0.01);
    }

    // Formats ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Formats_Use_The_True_Minus()
    {
        AnalyticsFormat.Number(-1250).ShouldBe("−1,250");
        AnalyticsFormat.Number(-0.04, 1).ShouldBe("0.0");
        AnalyticsFormat.Currency(-13_800).ShouldBe("−$13,800");
        AnalyticsFormat.Currency(55_800, signed: true).ShouldBe("+$55,800");
        AnalyticsFormat.WithTrueMinus("-$5 and a-b").ShouldBe("−$5 and a-b");
    }

    [Theory]
    [InlineData(63_300, 3, false, "$63.3k")]
    [InlineData(2_561_000, 3, false, "$2.56M")]
    [InlineData(120_400, 2, true, "$120K")]
    [InlineData(1_640_000, 2, true, "$1.6M")]
    [InlineData(999_960, 3, false, "$1.00M")]
    [InlineData(-25_000, 3, false, "−$25.0k")]
    [InlineData(950, 3, false, "$950")]
    [InlineData(99_960, 3, false, "$100k")]
    public void Compact_Currency_At_Significant_Digits(double value, int digits, bool upper, string expected)
        => AnalyticsFormat.Compact(value, digits, "$", upper).ShouldBe(expected);

    [Fact]
    public void Compact_Fixed_And_Trimmed()
    {
        AnalyticsFormat.CompactFixed(412_380, 1, "$").ShouldBe("$412.4k");
        AnalyticsFormat.CompactFixed(1500, 1, upperCaseThousands: true, trimZeros: true).ShouldBe("1.5K");
        AnalyticsFormat.CompactFixed(15_000, 1, upperCaseThousands: true, trimZeros: true).ShouldBe("15K");
        AnalyticsFormat.CompactFixed(-13_800, 1, "$", signed: true).ShouldBe("−$13.8k");
        AnalyticsFormat.CompactFixed(55_800, 1, "$", signed: true).ShouldBe("+$55.8k");
        AnalyticsFormat.CompactFixed(950, 1, "$").ShouldBe("$950");
        AnalyticsFormat.Compact(25_000, 3, "$", trimZeros: true).ShouldBe("$25k");
    }

    [Fact]
    public void Percent_Points_And_Changes()
    {
        AnalyticsFormat.Percent(4.3, 1, signed: true).ShouldBe("+4.3%");
        AnalyticsFormat.Percent(-4.8, 1, signed: true).ShouldBe("−4.8%");
        AnalyticsFormat.Percent(0.04, 0, signed: true).ShouldBe("0%");
        AnalyticsFormat.Change(0.117).ShouldBe("11.7%");
        AnalyticsFormat.Change(-0.034, signed: true).ShouldBe("−3.4%");
        AnalyticsFormat.Points(0.1).ShouldBe("0.1 pts");
        AnalyticsFormat.Points(-0.25, 2, signed: true).ShouldBe("−0.25 pts");
        AnalyticsFormat.Ordinal(1).ShouldBe("1st");
        AnalyticsFormat.Ordinal(12).ShouldBe("12th");
        AnalyticsFormat.Ordinal(22).ShouldBe("22nd");
        AnalyticsFormat.Date(new DateOnly(2026, 10, 18)).ShouldBe("Oct 18");
    }

    [Fact]
    public void Tone_Follows_Direction_And_Lower_Is_Better()
    {
        AnalyticsFormat.Tone(11.7).ShouldBe(AnalyticsTone.Good);
        AnalyticsFormat.Tone(-3).ShouldBe(AnalyticsTone.Bad);
        AnalyticsFormat.Tone(-0.7, lowerIsBetter: true).ShouldBe(AnalyticsTone.Good);
        AnalyticsFormat.Tone(0.04, decimals: 1).ShouldBe(AnalyticsTone.Flat);
        AnalyticsFormat.Tone(double.NaN).ShouldBe(AnalyticsTone.Flat);
    }

    [Fact]
    public void Number_Format_Parts_Agree_With_Its_Text()
    {
        var compact = AnalyticsNumberFormat.Compact(3, "$");
        compact.Parts(-2_561_000).ShouldBe(("−$", 2.56, 2, "M"));
        compact.Format(-2_561_000).ShouldBe("−$2.56M");
        AnalyticsNumberFormat.Percent(1).Parts(4.8).ShouldBe(("", 4.8, 1, "%"));
        AnalyticsNumberFormat.Points(1).Parts(-0.7).ShouldBe(("−", 0.7, 1, " pts"));
        AnalyticsNumberFormat.Currency(0, "$", signed: true).Parts(55_800).ShouldBe(("+$", 55_800, 0, ""));
        AnalyticsNumberFormat.CompactFixed(1, "$").Parts(412_380).ShouldBe(("$", 412.4, 1, "k"));
        AnalyticsNumberFormat.Number(0, " ms").Format(1250).ShouldBe("1,250 ms");
    }

    // Colours ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Oklab_Mix_Matches_Measured_Bars()
    {
        var accent = Oklch.ToColor(0.33, 0, 0);
        var surface = Oklch.ToColor(1, 0, 0);
        Oklch.FromColor(AnalyticsColors.Mix(accent, 0.08, surface)).L.ShouldBe(0.9464, 0.004);
        var ramp = AnalyticsColors.Ramp(accent, surface, AnalyticsColors.CohortSteps);
        ramp.Length.ShouldBe(6);
        for (var i = 1; i < ramp.Length; i++)
        {
            Oklch.FromColor(ramp[i]).L.ShouldBeLessThan(Oklch.FromColor(ramp[i - 1]).L);
        }
        AnalyticsColors.HorizonStackedAlpha(4, 4).ShouldBe(0.92, 1e-9);
        AnalyticsColors.HorizonStackedAlpha(2, 2).ShouldBe(0.92, 1e-9);
        AnalyticsColors.EvenWeights(3, 0.2, 0.8).ShouldBe([0.2, 0.5, 0.8], 1e-12);
    }

    // Tweens, springs, axis ---------------------------------------------------------------------------------------------

    [Fact]
    public void Tween_Runs_From_The_Current_Value()
    {
        var tween = new AnalyticsTween(0);
        tween.Start(1, TimeSpan.FromMilliseconds(200), AnalyticsEasings.Standard, TimeSpan.FromMilliseconds(100));
        tween.Advance(0.05).ShouldBeTrue();
        tween.Value.ShouldBe(0);
        tween.Advance(0.15);
        tween.Value.ShouldBeInRange(0.01, 0.99);
        tween.Advance(1).ShouldBeFalse();
        tween.Value.ShouldBe(1);
    }

    [Fact]
    public void Spring_Channel_Settles_And_Keeps_Velocity()
    {
        var channel = new AnalyticsSpringChannel(0);
        channel.SpringTo(100, AnalyticsSprings.Smooth);
        channel.Step(0.1);
        channel.Value.ShouldBeInRange(1, 99);
        var velocity = channel.Velocity;
        channel.SpringTo(50, AnalyticsSprings.Smooth);
        channel.Velocity.ShouldBe(velocity);
        for (var i = 0; i < 200 && channel.Step(1 / 60.0); i++)
        {
        }
        channel.Value.ShouldBe(50);
        channel.IsMoving.ShouldBeFalse();
        AnalyticsSprings.Expand.Stiffness.ShouldBe(SpringSpec.FromVisual(0.5, 0).Stiffness);
        AnalyticsSprings.MarkerSnap.Stiffness.ShouldBe(1100);
    }

    [Fact]
    public void Value_Axis_Fades_Ticks_By_Value_And_Rides_The_Domain()
    {
        var axis = new AnalyticsValueAxis();
        axis.Set(AnalyticsTicks.FixedRowsFromZero(1374), ArcMotionLevel.Full);
        axis.IsMoving.ShouldBeFalse();
        axis.TickCount.ShouldBe(4);
        axis.DomainMax.ShouldBe(1500);

        axis.Set(AnalyticsTicks.FixedRowsFromZero(2700), ArcMotionLevel.Full); // 0, 1000, 2000, 3000
        axis.IsMoving.ShouldBeTrue();
        axis.Advance(0.03);
        Tick(axis, 0).Opacity.ShouldBe(1);       // kept
        Tick(axis, 1000).Opacity.ShouldBe(1);    // kept
        Tick(axis, 2000).Opacity.ShouldBe(0);    // entering: still inside its 60ms delay
        Tick(axis, 500).IsLeaving.ShouldBeTrue();
        Tick(axis, 500).Opacity.ShouldBeLessThan(1);
        axis.DomainMax.ShouldBeInRange(1500, 3000);
        axis.Advance(0.2);
        Enumerable.Range(0, axis.TickCount).Select(i => axis[i].Value).ShouldNotContain(500); // faded out (160ms) and removed
        axis.Advance(2);
        axis.IsMoving.ShouldBeFalse();
        axis.TickCount.ShouldBe(4);
        axis.DomainMax.ShouldBe(3000);
        Tick(axis, 3000).Opacity.ShouldBe(1);
    }

    [Fact]
    public void Value_Axis_Under_Reduced_Motion_Jumps_The_Domain_And_Fades_Out_Only()
    {
        var axis = new AnalyticsValueAxis();
        axis.Set(AnalyticsTicks.FixedRowsFromZero(1374), ArcMotionLevel.Reduced);
        axis.Set(AnalyticsTicks.FixedRowsFromZero(2700), ArcMotionLevel.Reduced);
        axis.DomainMax.ShouldBe(3000);
        Tick(axis, 2000).Opacity.ShouldBe(1);
        Tick(axis, 500).IsLeaving.ShouldBeTrue();
        axis.Set(AnalyticsTicks.FixedRowsFromZero(1374), ArcMotionLevel.Off);
        axis.TickCount.ShouldBe(4);
        axis.IsMoving.ShouldBeFalse();
    }

    private static AnalyticsAxisTick Tick(AnalyticsValueAxis axis, double value)
    {
        for (var i = 0; i < axis.TickCount; i++)
        {
            if (axis[i].Value == value)
            {
                return axis[i];
            }
        }
        throw new InvalidOperationException($"no tick {value}");
    }

    // Readout placement and keyboard --------------------------------------------------------------------------------------

    [Fact]
    public void Readout_Placement_Rules()
    {
        var bounds = new Rect(0, 0, 600, 228);
        var card = new Size(204, 140);
        AnalyticsReadout.Beside(100, 14, card, bounds).X.ShouldBe(114);
        AnalyticsReadout.Beside(500, 14, card, bounds).X.ShouldBe(500 - 14 - 204);
        AnalyticsReadout.Beside(100, 200, card, bounds).Y.ShouldBe(228 - 140);
        AnalyticsReadout.CenteredX(10, 96, 0, 600).ShouldBe(0);
        AnalyticsReadout.CenteredX(590, 96, 0, 600).ShouldBe(504);
        AnalyticsReadout.CenteredX(300, 96, 0, 600).ShouldBe(252);
        AnalyticsReadout.OppositeHalfY(40, 0, 200, 52).ShouldBe(200 - 6 - 52);
        AnalyticsReadout.OppositeHalfY(160, 0, 200, 52).ShouldBe(6);
        AnalyticsReadout.AboveOrBelow(new Point(300, 100), new Size(176, 60), bounds).Y.ShouldBe(26);
        AnalyticsReadout.AboveOrBelow(new Point(300, 30), new Size(176, 60), bounds).Y.ShouldBe(44);
        AnalyticsReadout.NearPointer(new Point(500, 200), new Size(212, 106), new Rect(0, 0, 600, 300)).ShouldBe(new Point(500 - 14 - 212, 200 - 16 - 106));
    }

    [Fact]
    public void Readout_Appears_In_Place_And_Follows_On_Its_Spring()
    {
        var readout = new AnalyticsReadout { FollowSpring = AnalyticsSprings.ReadoutGlide, EnterScale = 0.97 };
        readout.Show(new Point(100, 14));
        readout.Position.ShouldBe(new Point(100, 14));
        readout.Opacity.ShouldBe(0);
        readout.Advance(0.06);
        readout.Opacity.ShouldBeInRange(0.01, 0.99);
        readout.Show(new Point(300, 14));
        readout.Advance(0.05);
        readout.Position.X.ShouldBeInRange(101, 299);
        readout.Advance(2);
        readout.Position.X.ShouldBe(300);
        readout.Opacity.ShouldBe(1);
        readout.Hide();
        readout.IsShown.ShouldBeFalse();
        readout.IsVisible.ShouldBeTrue();
        readout.Advance(1);
        readout.IsVisible.ShouldBeFalse();
        readout.Show(new Point(10, 10), ArcMotionLevel.Off);
        readout.Opacity.ShouldBe(1);
    }

    [Fact]
    public void Keyboard_Steps_From_The_End_When_There_Is_No_Cursor()
    {
        AnalyticsKeyboard.Step(Key.Right, -1, 30).ShouldBe(29);
        AnalyticsKeyboard.Step(Key.Left, -1, 30).ShouldBe(28);
        AnalyticsKeyboard.Step(Key.Right, 29, 30).ShouldBe(29);
        AnalyticsKeyboard.Step(Key.Right, 6, 7, wrap: true).ShouldBe(0);
        AnalyticsKeyboard.Step(Key.Left, 0, 7, wrap: true).ShouldBe(6);
        AnalyticsKeyboard.Step(Key.Home, 12, 30).ShouldBe(0);
        AnalyticsKeyboard.Step(Key.End, 12, 30).ShouldBe(29);
        AnalyticsKeyboard.Step(Key.PageDown, 80, 90, page: 7).ShouldBe(87);
        AnalyticsKeyboard.Step(Key.PageDown, 85, 90, page: 7).ShouldBe(89);
        AnalyticsKeyboard.Step(Key.PageUp, 10, 90).ShouldBeNull();
        AnalyticsKeyboard.Step(Key.Up, 3, 10, vertical: false).ShouldBeNull();
        AnalyticsKeyboard.Step(Key.Escape, 3, 10).ShouldBeNull();
    }
}
