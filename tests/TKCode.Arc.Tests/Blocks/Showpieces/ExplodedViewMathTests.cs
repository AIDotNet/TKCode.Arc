using Avalonia;
using Avalonia.Input;
using TKCode.Arc.Gallery.Demos;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// The Exploded view's timeline, projection, fit and layout against the numbers measured on Arc's preview
/// (design/components/blocks/showpieces/exploded-view.md sections 4 and 8).
/// </summary>
public class ExplodedViewMathTests
{
    private static readonly Size Layer = new(280, 620);

    // The measured anchor sits at y = +265.9 from the layer center: 44.1 above the bottom edge.
    private static double MeasuredAnchor(int _) => 44.1;

    [Fact]
    public void Smoothstep_Clamps_And_Eases()
    {
        ExplodedViewProgress.Smoothstep(-1).ShouldBe(0);
        ExplodedViewProgress.Smoothstep(0.5).ShouldBe(0.5);
        ExplodedViewProgress.Smoothstep(0.25).ShouldBe(0.15625, 1e-12);
        ExplodedViewProgress.Smoothstep(2).ShouldBe(1);
    }

    [Fact]
    public void Tilt_Leads_The_Spread_Opening_And_Trails_It_Closing()
    {
        ExplodedViewProgress.Tilt(0.04).ShouldBe(0);
        ExplodedViewProgress.Tilt(0.13).ShouldBe(0.5, 1e-9);
        ExplodedViewProgress.Tilt(0.22).ShouldBe(1);
        ExplodedViewProgress.Tilt(0.84).ShouldBe(1);
        ExplodedViewProgress.Tilt(0.91).ShouldBe(0.5, 1e-9);
        ExplodedViewProgress.Tilt(0.98).ShouldBe(0, 1e-12);
        ExplodedViewProgress.Spread(0.08).ShouldBe(0);
        // Measured at p 0.2: spread .649 (fit error < 0.002).
        ExplodedViewProgress.Spread(0.2).ShouldBe(0.649, 0.002);
        ExplodedViewProgress.Spread(0.28).ShouldBe(1);
        ExplodedViewProgress.Spread(0.80).ShouldBe(1);
        ExplodedViewProgress.Spread(0.875).ShouldBe(0.5, 1e-9);
        ExplodedViewProgress.Spread(0.95).ShouldBe(0, 1e-12);
        // Opening: tipped before separating. Closing: joined before standing up.
        ExplodedViewProgress.Tilt(0.1).ShouldBeGreaterThan(ExplodedViewProgress.Spread(0.1));
        ExplodedViewProgress.Tilt(0.9).ShouldBeGreaterThan(ExplodedViewProgress.Spread(0.9));
        ExplodedViewProgress.Tilt(0.02).ShouldBe(0);
        ExplodedViewProgress.Tilt(0.99).ShouldBe(0);
    }

    [Fact]
    public void Labels_Draw_Bottom_Up_At_The_Measured_Rates()
    {
        // Measured at p 0.2 (spread .649): .903 / .584 / .266 / 0 / 0.
        double[] expected = [0.903, 0.584, 0.266, 0, 0];
        for (var k = 0; k < 5; k++)
        {
            ExplodedViewProgress.LabelDraw(k, 5, 0.649).ShouldBe(expected[k], 0.005);
        }
        ExplodedViewProgress.LabelDraw(0, 5, 0.45).ShouldBe(0);
        ExplodedViewProgress.LabelDraw(4, 5, 0.95).ShouldBe(1, 1e-9);
        ExplodedViewProgress.LabelDraw(4, 5, 0.94).ShouldBeLessThan(1);
        // Any count: the top label always finishes at 0.95.
        ExplodedViewProgress.LabelDraw(2, 3, 0.95).ShouldBe(1, 1e-9);
        ExplodedViewProgress.LabelDraw(7, 8, 0.95).ShouldBe(1, 1e-9);
        ExplodedViewProgress.LabelDraw(0, 1, 0.67).ShouldBe(1, 1e-9);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.2799, 0)]
    [InlineData(0.28, 1)]
    [InlineData(0.3839, 1)]
    [InlineData(0.384, 2)]
    [InlineData(0.488, 3)]
    [InlineData(0.592, 4)]
    [InlineData(0.696, 5)]
    [InlineData(0.7999, 5)]
    [InlineData(0.80, 6)]
    [InlineData(1.0, 6)]
    public void Stops_Split_The_Layer_Range_Evenly(double progress, int stop)
    {
        ExplodedViewProgress.StopAt(progress, 5).ShouldBe(stop);
    }

    [Fact]
    public void Stop_Targets_Are_The_Middles_Of_Their_Shares()
    {
        ExplodedViewProgress.StopProgress(0, 5).ShouldBe(0);
        double[] expected = [0.332, 0.436, 0.540, 0.644, 0.748];
        for (var k = 1; k <= 5; k++)
        {
            ExplodedViewProgress.StopProgress(k, 5).ShouldBe(expected[k - 1], 1e-9);
            ExplodedViewProgress.StopAt(ExplodedViewProgress.StopProgress(k, 5), 5).ShouldBe(k);
        }
        ExplodedViewProgress.StopProgress(6, 5).ShouldBe(1);
        ExplodedViewProgress.StopCount(5).ShouldBe(7);
        // Other counts share [0.28, 0.80) evenly.
        ExplodedViewProgress.StopAt(0.28 + 0.52 / 3 * 2.5, 3).ShouldBe(3);
        ExplodedViewProgress.StopAt(0.5, 8).ShouldBe((int)((0.5 - 0.28) / 0.065) + 1);
        ExplodedViewProgress.StopAt(0.5, 0).ShouldBe(0);
        ExplodedViewProgress.StopAt(0.9, 0).ShouldBe(1);
    }

    [Fact]
    public void Reduced_Motion_Pose_Is_Flat_At_The_Ends_And_Apart_At_Every_Layer()
    {
        ExplodedViewProgress.Pose(0.2, 5, discrete: true).ShouldBe((0, 0));
        ExplodedViewProgress.Pose(0.3, 5, discrete: true).ShouldBe((1, 1));
        ExplodedViewProgress.Pose(0.79, 5, discrete: true).ShouldBe((1, 1));
        ExplodedViewProgress.Pose(0.85, 5, discrete: true).ShouldBe((0, 0));
        var continuous = ExplodedViewProgress.Pose(0.2, 5, discrete: false);
        continuous.Tilt.ShouldBe(ExplodedViewProgress.Tilt(0.2));
        continuous.Spread.ShouldBe(ExplodedViewProgress.Spread(0.2));
    }

    [Fact]
    public void Depths_Center_On_The_Middle_Layer_With_A_Half_Pixel_Rest_Step()
    {
        double[] wide = [-272, -135.5, 1, 137.5, 274];
        double[] tall = [-300, -149.5, 1, 151.5, 302];
        for (var i = 0; i < 5; i++)
        {
            ExplodedViewProgress.LayerDepth(i, 5, 136, 1).ShouldBe(wide[i], 1e-9);
            ExplodedViewProgress.LayerDepth(i, 5, 150, 1).ShouldBe(tall[i], 1e-9);
            ExplodedViewProgress.LayerDepth(i, 5, 136, 0).ShouldBe(0.5 * i, 1e-9);
        }
    }

    [Fact]
    public void The_Anchor_Projects_Where_Arc_Drew_It()
    {
        // Fully apart (60°, −40°, s .689, t (−87.28, −16.37)) on a 743 × 629 stage: layer 2's anchor at (484.17, 338.39).
        var fit = new ExplodedFit(0.9131, 0.689, new Vector(-87.28, -16.37));
        var projected = ExplodedViewGeometry.Project(140, 265.9, 1, 60, -40, 2400);
        var stage = ExplodedViewGeometry.ToStage(projected, new Point(743 / 2.0, 629 / 2.0), fit, 1);
        stage.X.ShouldBe(484.17, 0.3);
        stage.Y.ShouldBe(338.39, 0.3);
    }

    [Fact]
    public void A_Layer_Matrix_Is_The_Exact_Projection_Of_Its_Points()
    {
        var origin = new Point(300, 250);
        var matrix = ExplodedViewGeometry.LayerMatrix(Layer, 137.5, 60, -40, 2400, 0.689, origin);
        foreach (var (u, v) in new[] { (0.0, 0.0), (280.0, 0.0), (280.0, 620.0), (0.0, 620.0), (140.0, 575.9), (37.0, 211.0) })
        {
            var expected = ExplodedViewGeometry.Project(u - 140, v - 310, 137.5, 60, -40, 2400);
            var actual = ShowpieceProjection.Apply(matrix, new Point(u, v));
            actual.X.ShouldBe(origin.X + 0.689 * expected.X, 1e-6);
            actual.Y.ShouldBe(origin.Y + 0.689 * expected.Y, 1e-6);
        }
        // Flat: a plain scale about the center (plus the resting depth step's tiny perspective).
        var flat = ExplodedViewGeometry.LayerMatrix(Layer, 0, 0, 0, 2400, 0.9131, origin);
        var corner = ShowpieceProjection.Apply(flat, new Point(0, 0));
        corner.X.ShouldBe(origin.X - 140 * 0.9131, 1e-9);
        corner.Y.ShouldBe(origin.Y - 310 * 0.9131, 1e-9);
    }

    [Fact]
    public void Exploded_Bounds_Match_Both_Compositions()
    {
        var wide = ExplodedViewGeometry.MeasureExploded(Layer, 5, ExplodedComposition.Wide, 2400, MeasuredAnchor);
        wide.MinX.ShouldBe(-307.7, 0.15);
        wide.MaxX.ShouldBe(344.5, 0.15);
        wide.MinY.ShouldBe(-377.9, 0.15);
        wide.MaxY.ShouldBe(425.5, 0.15);
        wide.Height.ShouldBe(803.4, 0.2);
        wide.CenterY.ShouldBe(23.76, 0.1);
        wide.MaxAnchorX.ShouldBe(308.4, 0.15);

        var tall = ExplodedViewGeometry.MeasureExploded(Layer, 5, ExplodedComposition.Tall, 2400, MeasuredAnchor);
        tall.MinX.ShouldBe(-230.6, 0.15);
        tall.MaxX.ShouldBe(279.1, 0.15);
        tall.MinY.ShouldBe(-399.6, 0.15);
        tall.MaxY.ShouldBe(454.1, 0.15);
        tall.Height.ShouldBe(853.8, 0.2);
        tall.CenterY.ShouldBe(27.26, 0.1);
        tall.MaxAnchorX.ShouldBe(256.7, 0.15);
    }

    [Theory]
    // Stage W × H, composition, widest label (G − 3.5) → flat scale, exploded scale, offset (NaN: not recorded).
    [InlineData(743, 629, false, 170.5, 0.9131, 0.689, -87.28, -16.37)]
    [InlineData(932, 790, false, 170.5, 1.1, 0.8653, -87.35, -20.56)]
    [InlineData(932, 618, false, 170.5, 0.8971, 0.6769, double.NaN, double.NaN)]
    [InlineData(610, 790, false, 170.5, 1.1, 0.6482, double.NaN, double.NaN)]
    [InlineData(810, 552, false, 170.5, 0.8013, 0.6046, double.NaN, double.NaN)]
    [InlineData(504, 535, true, 107.5, 0.7766, 0.5514, -62.73, -15.03)]
    [InlineData(358, 467, true, 107.5, 0.6779, 0.4627, -61.57, -12.61)]
    public void The_Fit_Reproduces_The_Measured_Scales(double w, double h, bool tall, double label, double flat, double exploded,
                                                       double tx, double ty)
    {
        var composition = tall ? ExplodedComposition.Tall : ExplodedComposition.Wide;
        var bounds = ExplodedViewGeometry.MeasureExploded(Layer, 5, composition, 2400, MeasuredAnchor);
        var fit = ExplodedViewGeometry.Fit(new Size(w, h), Layer, bounds, label);
        fit.FlatScale.ShouldBe(flat, 0.001);
        fit.ExplodedScale.ShouldBe(exploded, 0.001);
        if (!double.IsNaN(tx))
        {
            fit.ExplodedOffset.X.ShouldBe(tx, 0.15);
            fit.ExplodedOffset.Y.ShouldBe(ty, 0.15);
        }
        // Scale and offset follow the tilt, not the separation.
        fit.ScaleAt(0).ShouldBe(fit.FlatScale);
        fit.ScaleAt(1).ShouldBe(fit.ExplodedScale);
        fit.OffsetAt(0).ShouldBe(default);
    }

    [Fact]
    public void The_Fit_Keeps_The_Stack_And_Labels_Inside_The_Stage()
    {
        foreach (var (w, h) in new[] { (743.0, 629.0), (358.0, 467.0), (1200.0, 400.0), (300.0, 900.0) })
        {
            var composition = w < 560 && h > w ? ExplodedComposition.Tall : ExplodedComposition.Wide;
            var bounds = ExplodedViewGeometry.MeasureExploded(Layer, 5, composition, 2400, MeasuredAnchor);
            var fit = ExplodedViewGeometry.Fit(new Size(w, h), Layer, bounds, 150);
            var left = w / 2 + fit.ExplodedOffset.X + bounds.MinX * fit.ExplodedScale;
            var right = w / 2 + fit.ExplodedOffset.X + bounds.MaxAnchorX * fit.ExplodedScale + 153.5;
            var top = h / 2 + fit.ExplodedOffset.Y + bounds.MinY * fit.ExplodedScale;
            var bottom = h / 2 + fit.ExplodedOffset.Y + bounds.MaxY * fit.ExplodedScale;
            left.ShouldBeGreaterThanOrEqualTo(0);
            right.ShouldBeLessThanOrEqualTo(w + 1e-9);
            top.ShouldBeGreaterThanOrEqualTo(0);
            bottom.ShouldBeLessThanOrEqualTo(h + 1e-9);
            // Horizontally centered on stack + labels; vertically on the stack.
            (left + right).ShouldBe(w, 1e-6);
            (top + bottom).ShouldBe(h, 1e-6);
        }
    }

    [Theory]
    [InlineData(1312, 2, 64, 64, 64, 52)]
    [InlineData(990, 2, 49.5, 49.5, 49.5, 49.5)]
    [InlineData(880, 2, 44, 44, 44, 44)]
    [InlineData(879.9, 1, 40, 40, 35.196, 43.995)]
    [InlineData(792, 1, 39.6, 39.6, 31.68, 39.6)]
    [InlineData(630, 1, 31.5, 31.5, 25.2, 31.5)]
    [InlineData(560, 1, 28, 28, 22.4, 28)]
    [InlineData(504, 0, 25.2, 25.2, 20.16, 28)]
    [InlineData(448, 0, 22.4, 24, 20, 28)]
    [InlineData(358, 0, 20, 24, 20, 28)]
    public void Metrics_Follow_The_Container_Width(double width, int breakpoint, double side, double introTop, double footerBottom,
                                                    double title)
    {
        // Breakpoint: 0 compact, 1 medium, 2 wide.
        var m = ExplodedViewLayout.Metrics(width);
        m.Breakpoint.ShouldBe((ExplodedViewBreakpoint)breakpoint);
        m.Side.ShouldBe(side, 1e-9);
        m.IntroTop.ShouldBe(introTop, 1e-9);
        m.FooterBottom.ShouldBe(footerBottom, 1e-9);
        m.TitleSize.ShouldBe(title, 1e-9);
    }

    [Fact]
    public void Medium_Layout_Puts_The_Caption_Beside_The_Stops()
    {
        // Measured at 630 × 790: intro 118.9 (31.5 + 87.4), caption 82.6, stage 563.3, stops 252 wide at (346.5, 720.8).
        var m = ExplodedViewLayout.Metrics(630);
        var r = ExplodedViewLayout.Arrange(m, new Size(630, 790), 87.4, 82.6, 7);
        r.Stage.ShouldBe(new Rect(0, 118.9, 630, 563.3), new RectTolerance(0.01));
        r.Stops.ShouldBe(new Rect(346.5, 720.8, 252, 44), new RectTolerance(0.01));
        r.Caption.ShouldBe(new Rect(31.5, 682.2, 283, 82.6), new RectTolerance(0.01));
        r.Intro.X.ShouldBe(31.5);
        r.Intro.Width.ShouldBe(481);
        r.StopWidth.ShouldBe(36);
    }

    [Fact]
    public void Wide_Layout_Puts_The_Stage_Beside_A_380_Column()
    {
        // Measured at 1312 × 790: stops at (56, 682), caption 96 tall at y 566, stage (380, 0, 932, 790).
        var m = ExplodedViewLayout.Metrics(1312);
        var r = ExplodedViewLayout.Arrange(m, new Size(1312, 790), 132.4, 96, 7);
        r.Stage.ShouldBe(new Rect(380, 0, 932, 790), new RectTolerance(0.01));
        r.Stops.ShouldBe(new Rect(56, 682, 252, 44), new RectTolerance(0.01));
        r.Caption.ShouldBe(new Rect(64, 566, 316, 96), new RectTolerance(0.01));
        r.Intro.ShouldBe(new Rect(64, 64, 316, 132.4), new RectTolerance(0.01));
    }

    [Fact]
    public void Compact_Layout_Shares_The_Stop_Row_Above_The_Caption()
    {
        // Measured at 358 × 740.7: stops (12, 588.7, 334 × 44) with 47.7 px buttons, caption at y 636.7, stage 467.1 tall.
        var m = ExplodedViewLayout.Metrics(358);
        var r = ExplodedViewLayout.Arrange(m, new Size(358, 740.7), 97.6, 84, 7);
        r.Stops.ShouldBe(new Rect(12, 588.7, 334, 44), new RectTolerance(0.01));
        r.StopWidth.ShouldBe(334 / 7.0, 1e-9);
        r.Caption.ShouldBe(new Rect(20, 636.7, 318, 84), new RectTolerance(0.01));
        r.Stage.ShouldBe(new Rect(0, 121.6, 358, 467.1), new RectTolerance(0.01));
    }

    [Theory]
    [InlineData(Key.Down, KeyModifiers.None, 0, 40)]
    [InlineData(Key.Up, KeyModifiers.None, 100, 60)]
    [InlineData(Key.Up, KeyModifiers.None, 10, 0)]
    [InlineData(Key.PageDown, KeyModifiers.None, 0, 818)]
    [InlineData(Key.Space, KeyModifiers.None, 0, 818)]
    [InlineData(Key.Space, KeyModifiers.Shift, 1000, 182)]
    [InlineData(Key.PageUp, KeyModifiers.None, 1000, 182)]
    [InlineData(Key.End, KeyModifiers.None, 10, 2574)]
    [InlineData(Key.Home, KeyModifiers.None, 900, 0)]
    [InlineData(Key.PageDown, KeyModifiers.None, 2500, 2574)]
    public void Frame_Keys_Scroll_Like_The_Browser(Key key, KeyModifiers modifiers, double from, double to)
    {
        // The reference frame: 858 px viewport over a 3432 px track (2574 px to scroll).
        ExplodedViewScrollViewer.KeyTarget(key, modifiers, from, 858, 2574).ShouldBe(to);
    }

    [Fact]
    public void Side_Keys_Do_Not_Scroll()
    {
        ExplodedViewScrollViewer.KeyTarget(Key.Left, KeyModifiers.None, 100, 858, 2574).ShouldBeNull();
        ExplodedViewScrollViewer.KeyTarget(Key.Right, KeyModifiers.None, 100, 858, 2574).ShouldBeNull();
        ExplodedViewScrollViewer.KeyTarget(Key.A, KeyModifiers.None, 100, 858, 2574).ShouldBeNull();
    }

    [Fact]
    public void Stop_Glides_Grow_With_The_Distance()
    {
        // Chromium's smooth scroll over 1658 px took about 670 ms.
        ExplodedView.GlideDuration(1658).TotalMilliseconds.ShouldBe(670, 15);
        ExplodedView.GlideDuration(40).TotalMilliseconds.ShouldBe(250);
        ExplodedView.GlideDuration(10000).TotalMilliseconds.ShouldBe(700);
    }

    [Fact]
    public void The_Sample_Week_Is_Deterministic_And_Follows_Its_Recipe()
    {
        var week = ExplodedViewDemoData.Generate();
        week.ShouldBeEquivalentTo(ExplodedViewDemoData.Generate());
        week.Bars.Length.ShouldBe(7);
        week.Bars[5].ShouldBe(92);
        week.Bars.Max().ShouldBe(92);
        week.Bars.Min().ShouldBeGreaterThanOrEqualTo(14);
        week.TotalKm.ShouldBe(Math.Round(week.Bars.Sum() / 92.0 * 9.4, 1));
        week.ChangePercent.ShouldBeInRange(4, 12);
        week.SessionKm.ShouldBe([Math.Round(week.TotalKm * 0.16, 1), Math.Round(week.TotalKm * 0.21, 1), Math.Round(week.TotalKm * 0.38, 1)]);
        ExplodedViewDemoData.Generate(1).Bars.ShouldNotBe(week.Bars);
    }

    private sealed class RectTolerance(double tolerance) : IEqualityComparer<Rect>
    {
        public bool Equals(Rect a, Rect b) =>
            Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance &&
            Math.Abs(a.Width - b.Width) <= tolerance && Math.Abs(a.Height - b.Height) <= tolerance;

        public int GetHashCode(Rect obj) => 0;
    }
}
