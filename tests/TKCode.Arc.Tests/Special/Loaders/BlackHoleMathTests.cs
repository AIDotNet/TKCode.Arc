using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>The black hole's measured curves (notes sections 3.3, 4.1, 6.1, 6.2) and the CPU painter's passes.</summary>
public class BlackHoleMathTests
{
    private const double R = 5.47;

    private static BlackHoleHoleState Hole(double t) => BlackHoleMath.Hole(t, R, 0.4, 90, 0.6);

    [Fact]
    public void The_hole_opens_with_a_ten_percent_overshoot()
    {
        Hole(0).CoreRadius.ShouldBe(0);
        Hole(15).CoreRadius.ShouldBe(0);
        // Measured (dpx / 2): 40 ms 2.75, 73 ms 4.95, 106 ms 5.9, 123–140 ms 6.0.
        Hole(40).CoreRadius.ShouldBe(2.75, 0.25);
        Hole(73).CoreRadius.ShouldBe(4.95, 0.3);
        Hole(106).CoreRadius.ShouldBe(5.9, 0.15);
        Hole(130).CoreRadius.ShouldBe(6.02, 0.05);
        var peak = Enumerable.Range(15, 190).Max(t => Hole(t).CoreRadius);
        (peak / R).ShouldBe(1.1, 0.01);
        Hole(206).CoreRadius.ShouldBe(R, 0.01);
    }

    [Fact]
    public void The_core_grows_forty_percent_while_eating_then_collapses()
    {
        Hole(240).CoreRadius.ShouldBe(R, 0.01);
        Hole(660).CoreRadius.ShouldBe(R * 1.2, 0.01);
        Hole(1080).CoreRadius.ShouldBe(7.66, 0.01);
        Hole(1100).CoreRadius.ShouldBe(7.66, 0.01);
        // Ease-in: still most of its size half way, gone after 95 ms.
        Hole(1157).CoreRadius.ShouldBeGreaterThan(7.66 * 0.75);
        Hole(1205).CoreRadius.ShouldBe(0);
    }

    [Fact]
    public void The_shockwave_eases_out_to_ninety_and_fades_quadratically()
    {
        Hole(1130).ShockOpacity.ShouldBe(0);
        // p = 0.1: radius 90·(1 − 0.9³), opacity 0.6·0.81 (measured 0.44 at the first sample).
        var early = Hole(1156);
        early.ShockRadius.ShouldBe(90 * (1 - Math.Pow(0.9, 3)), 0.01);
        early.ShockOpacity.ShouldBe(0.6 * 0.81, 0.001);
        Hole(1220).ShockOpacity.ShouldBe(0.6 * 0.25, 0.001);
        Hole(1299).ShockRadius.ShouldBe(90, 0.1);
        Hole(1300).ShockOpacity.ShouldBe(0);
    }

    [Fact]
    public void Points_start_by_distance_and_fall_with_ease_in_sine()
    {
        BlackHoleMath.Stagger(200).ShouldBe(1.84);
        BlackHoleMath.FallStart(0, 1.84).ShouldBe(110);
        BlackHoleMath.FallStart(100, 1.84).ShouldBe(294);
        BlackHoleMath.FallFactor(200, 294).ShouldBe(1);
        BlackHoleMath.FallFactor(294 + 305, 294).ShouldBe(Math.Cos(Math.PI / 4), 1e-9);
        BlackHoleMath.FallFactor(294 + 610, 294).ShouldBe(0);
        // A farther item list slows the stagger so its last point still lands by 0.9 of the duration.
        var stagger = BlackHoleMath.Stagger(262);
        stagger.ShouldBeLessThan(1.84);
        (BlackHoleMath.FallStart(262, stagger) + BlackHoleMath.FallLength).ShouldBe(1170, 0.001);
    }

    [Fact]
    public void Points_follow_a_clockwise_logarithmic_spiral()
    {
        // Start at the right of the hole; shrinking by e turns 0.88 rad (50°) clockwise (y down).
        var (x, y) = BlackHoleMath.Spiral(100, 0, 1 / Math.E);
        Math.Sqrt(x * x + y * y).ShouldBe(100 / Math.E, 1e-9);
        Math.Atan2(y, x).ShouldBe(0.88, 1e-9);
        y.ShouldBeGreaterThan(0);
        // 235 → 10 DIP turns about 2.8 rad.
        var (x2, y2) = BlackHoleMath.Spiral(235, 0, 10 / 235.0);
        var turned = Math.Atan2(y2, x2);
        (turned < 0 ? turned + 2 * Math.PI : turned).ShouldBe(2.78, 0.02);
        BlackHoleMath.Spiral(50, 1, 1).ShouldBe((50 * Math.Cos(1), 50 * Math.Sin(1)));
        BlackHoleMath.Spiral(50, 1, 0).ShouldBe((0, 0));
    }

    [Fact]
    public void The_lens_pushes_out_beyond_the_einstein_ring_and_mirrors_inside()
    {
        var e = 4 * R;
        // Measured with θE ≈ 21.9: β = 20 → shown at 34.5; β = 95 → 100.5; β = 185 → 187.5.
        ImageFor(20, e).ShouldBe(34.5, 0.6);
        ImageFor(95, e).ShouldBe(100.5, 1.0);
        ImageFor(185, e).ShouldBe(187.5, 0.3);
        // Inside the ring the source lies on the other side.
        var (sx, _) = BlackHoleMath.LensSource(10, 0, e);
        sx.ShouldBeLessThan(0);
        BlackHoleMath.LensSource(10, 5, 0).ShouldBe((10, 5));
    }

    /// <summary>The image radius θ of a source at β: the positive root of θ² − βθ − θE² = 0.</summary>
    private static double ImageFor(double beta, double einstein)
    {
        var theta = (beta + Math.Sqrt(beta * beta + 4 * einstein * einstein)) / 2;
        var (src, _) = BlackHoleMath.LensSource(theta, 0, einstein);
        src.ShouldBe(beta, 1e-9);
        return theta;
    }

    [Fact]
    public void A_release_plays_the_swallow_backwards_a_little_faster()
    {
        BlackHoleMath.SwallowTimeForRelease(0, 1300, 0.92).ShouldBe(1300);
        BlackHoleMath.SwallowTimeForRelease(598, 1300, 0.92).ShouldBe(650, 0.01);
        BlackHoleMath.SwallowTimeForRelease(1196, 1300, 0.92).ShouldBe(0, 0.01);
        BlackHoleMath.SwallowTimeForRelease(5000, 1300, 0.92).ShouldBe(0);
    }

    [Fact]
    public void Calls_queue_one_after_another()
    {
        var queue = new BlackHoleQueue();
        var first = new TaskCompletionSource();
        var order = new List<int>();
        var a = queue.Enqueue(async () => { order.Add(1); await first.Task; order.Add(2); });
        var b = queue.Enqueue(() => { order.Add(3); return Task.CompletedTask; });
        queue.Pending.ShouldBe(2);
        order.ShouldBe([1]);
        b.IsCompleted.ShouldBeFalse();
        first.SetResult();
        a.IsCompleted.ShouldBeTrue();
        b.IsCompleted.ShouldBeTrue();
        order.ShouldBe([1, 2, 3]);
        queue.Pending.ShouldBe(0);
        // A failed call does not stop the ones after it.
        var failed = queue.Enqueue(() => Task.FromException(new InvalidOperationException()));
        var after = queue.Enqueue(() => Task.CompletedTask);
        failed.IsFaulted.ShouldBeTrue();
        after.IsCompletedSuccessfully.ShouldBeTrue();
    }

    private static BlackHoleLook Look => new(5.5, 0.4, 4, 10.5, 0.16, 2, Color.FromArgb(145, 0x2A, 0x2A, 0x2A), 90, 6, 0.6, Color.FromRgb(0x34, 0x34, 0x34));

    /// <summary>A 200 × 100 content: white rest, and a 100 × 20 opaque gray item at (50, 40).</summary>
    private static BlackHoleRenderer Painter(double scale = 1)
    {
        var w = (int)(200 * scale);
        var h = (int)(100 * scale);
        var r = new BlackHoleRenderer(w, h, scale, redFirst: false);
        Array.Fill(r.Rest, 0xFFFFFFFFu);
        for (var y = (int)(40 * scale); y < (int)(60 * scale); y++)
        {
            for (var x = (int)(50 * scale); x < (int)(150 * scale); x++)
            {
                r.Items[y * w + x] = 0xFF808080u;
            }
        }
        r.BuildMesh([new Rect(50, 40, 100, 20)], new Point(100, 50), Look);
        return r;
    }

    [Fact]
    public void The_mesh_is_a_five_dip_grid_over_the_items()
    {
        var r = Painter();
        r.VertexCount.ShouldBe(21 * 5);
        r.TriangleCount.ShouldBe(20 * 4 * 2);
        r.Center.ShouldBe(new Point(100, 50));
        // The corner (50, 40) is 50.99 DIP away and starts at 110 + 1.84·50.99 ms.
        r.VertexAt(0, 100).X.ShouldBe(50, 1e-9);
        r.VertexAt(0, 100).Y.ShouldBe(40, 1e-9);
        var later = r.VertexAt(0, 110 + 1.84 * Math.Sqrt(50 * 50 + 10 * 10) + 305);
        Math.Sqrt((later.X - 100) * (later.X - 100) + (later.Y - 50) * (later.Y - 50)).ShouldBe(Math.Sqrt(2600) * Math.Cos(Math.PI / 4), 0.01);
    }

    [Fact]
    public void The_first_frame_is_the_content_as_it_was()
    {
        var r = Painter();
        r.Render(0);
        r.Output[10 * 200 + 10].ShouldBe(0xFFFFFFFFu);
        r.Output[50 * 200 + 60].ShouldBe(0xFF808080u);
        r.Output[50 * 200 + 140].ShouldBe(0xFF808080u);
    }

    [Fact]
    public void Mid_swallow_the_hole_is_black_and_the_items_have_moved()
    {
        var r = Painter();
        r.Render(660);
        r.LastHole.CoreRadius.ShouldBe(5.5 * 1.2, 0.01);
        // The core is opaque black.
        (r.Output[50 * 200 + 100] & 0x00FFFFFFu).ShouldBe(0u);
        (r.Output[50 * 200 + 100] >> 24).ShouldBe(255u);
        // The item's far ends have left their places (white rest content shows there, lensed).
        var end = r.Output[50 * 200 + 52];
        (end & 0xFF).ShouldBeGreaterThan(0xC0u);
        // The halo darkens the rest near the hole.
        var near = r.Output[50 * 200 + 100 + 6 + 8] & 0xFF;
        near.ShouldBeLessThan(0xF8u);
        r.LastFrameMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void After_the_collapse_only_the_shockwave_remains()
    {
        var r = Painter();
        r.Render(1220);
        r.LastHole.CoreRadius.ShouldBe(0);
        var ring = r.LastHole.ShockRadius;
        ring.ShouldBeGreaterThan(40);
        // On the ring the white rest is darkened; between centre and ring it is untouched; the items are gone.
        var onRing = r.Output[50 * 200 + 100 + (int)Math.Round(ring)] & 0xFF;
        onRing.ShouldBeLessThan(0xF0u);
        r.Output[50 * 200 + 100 + 10].ShouldBe(0xFFFFFFFFu);
        r.Output[50 * 200 + 60].ShouldBe(0xFFFFFFFFu);
    }

    [Fact]
    public void Pixel_blending_is_premultiplied()
    {
        BlackHoleRenderer.Over(0x80000000u, 0xFFFFFFFFu).ShouldBe(0xFF7F7F7Fu);
        BlackHoleRenderer.Over(0xFF102030u, 0xFFFFFFFFu).ShouldBe(0xFF102030u);
        BlackHoleRenderer.Lerp(0xFF000000u, 0xFFFFFFFFu, 128).ShouldBe(0xFF7F7F7Fu);
    }
    private static int MaxChannelDiff(uint a, uint b)
    {
        var max = 0;
        for (var shift = 0; shift < 32; shift += 8)
        {
            max = Math.Max(max, Math.Abs((int)((a >> shift) & 0xFF) - (int)((b >> shift) & 0xFF)));
        }
        return max;
    }

    [Theory]
    [InlineData(1.0, 40)]
    [InlineData(1.0, 664)]
    [InlineData(2.0, 1080)]
    [InlineData(2.0, 1180)]
    public void The_hole_table_matches_the_exact_radial_color(double scale, double t)
    {
        var r = Painter(scale);
        r.Render(t);
        var worst = 0;
        for (var rp = 0.0; rp < 140 * scale; rp += 0.0137)
        {
            worst = Math.Max(worst, MaxChannelDiff(r.HoleLutAt((float)rp), r.HoleColorAt(rp)));
        }
        worst.ShouldBeLessThanOrEqualTo(2);
        // Blended with the core: opaque black inside, nothing far out.
        r.HoleBlendAt(0, 0xFFFFFFFFu).ShouldBe(0xFF000000u);
        r.HoleBlendAt((float)(200 * scale), 0xFFFFFFFFu).ShouldBe(0xFFFFFFFFu);
    }

    [Theory]
    [InlineData(1.0, 1160)]
    [InlineData(2.0, 1220)]
    public void The_shockwave_table_matches_the_exact_ring(double scale, double t)
    {
        var r = Painter(scale);
        r.Render(t);
        var worst = 0;
        for (var rp = 0.0; rp < 110 * scale; rp += 0.0113)
        {
            worst = Math.Max(worst, MaxChannelDiff(r.ShockLutAt((float)rp), r.ShockColorAt(rp)));
        }
        worst.ShouldBeLessThanOrEqualTo(2);
        r.ShockLutAt((float)(r.LastHole.ShockRadius * scale)).ShouldNotBe(0u);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(900)]
    [InlineData(1150)]
    public void Each_band_gets_exactly_its_triangles_in_mesh_order(double t)
    {
        var r = Painter(2);
        r.Render(t);
        for (var band = 0; band < r.Bands; band++)
        {
            var y0 = band * 8;
            var y1 = y0 + 8;
            var expected = new List<int>();
            for (var i = 0; i < r.TriangleCount; i++)
            {
                var (live, minY, maxY) = r.TriangleRows(i);
                // Rows floor(minY)..ceil(maxY) meet the band.
                if (live && Math.Floor(minY) < y1 && Math.Ceiling(maxY) >= y0)
                {
                    expected.Add(i);
                }
            }
            r.BinnedTriangles(band).ToArray().ShouldBe(expected.ToArray(), $"band {band} at {t} ms");
        }
    }

    [Fact]
    public void The_lens_is_interpolated_only_where_the_error_stays_under_a_twentieth_of_a_pixel()
    {
        BlackHoleRenderer.CanInterpolateLens(30, 30).ShouldBeFalse();
        BlackHoleRenderer.CanInterpolateLens(100, 61.6f).ShouldBeFalse();
        BlackHoleRenderer.CanInterpolateLens(400, 61.6f).ShouldBeTrue();
        // Check the bound numerically: wherever it allows a tile, bilinear corners stay within 0.05 px of the exact map.
        var worst = 0.0;
        foreach (var e in new[] { 5.0, 11.0, 22.0, 44.0, 61.6 })
        {
            for (var tx = -60; tx < 60; tx++)
            {
                for (var ty = -40; ty < 40; ty++)
                {
                    double x0 = tx * 8, y0 = ty * 8;
                    var nx = Math.Clamp(0, x0, x0 + 8);
                    var ny = Math.Clamp(0, y0, y0 + 8);
                    if (!BlackHoleRenderer.CanInterpolateLens((float)Math.Sqrt(nx * nx + ny * ny), (float)e))
                    {
                        continue;
                    }
                    (double X, double Y) Map(double px, double py) => BlackHoleMath.LensSource(px, py, e);
                    var c00 = Map(x0 + 0.5, y0 + 0.5);
                    var c10 = Map(x0 + 7.5, y0 + 0.5);
                    var c01 = Map(x0 + 0.5, y0 + 7.5);
                    var c11 = Map(x0 + 7.5, y0 + 7.5);
                    for (var j = 0; j < 8; j++)
                    {
                        for (var i = 0; i < 8; i++)
                        {
                            double u = i / 7.0, v = j / 7.0;
                            var ix = (1 - u) * (1 - v) * c00.X + u * (1 - v) * c10.X + (1 - u) * v * c01.X + u * v * c11.X;
                            var iy = (1 - u) * (1 - v) * c00.Y + u * (1 - v) * c10.Y + (1 - u) * v * c01.Y + u * v * c11.Y;
                            var exact = Map(x0 + 0.5 + i, y0 + 0.5 + j);
                            worst = Math.Max(worst, Math.Max(Math.Abs(ix - exact.X), Math.Abs(iy - exact.Y)));
                        }
                    }
                }
            }
        }
        worst.ShouldBeLessThan(0.05);
    }

    [Fact]
    public void Frames_are_double_buffered()
    {
        var r = Painter();
        r.Render(300);
        var first = r.Output;
        r.Render(400);
        r.Output.ShouldNotBeSameAs(first);
        r.Render(500);
        r.Output.ShouldBeSameAs(first);
    }

    [Fact]
    public void The_worker_paints_the_same_frames_as_the_ui_thread()
    {
        var sync = Painter(2);
        var async = Painter(2);
        using var worker = new BlackHoleFrameWorker(async);
        foreach (var t in new[] { 100.0, 497, 830, 1163 })
        {
            sync.Render(t);
            worker.Start(t);
            worker.Wait().ShouldBeTrue();
            async.Output.ShouldBe(sync.Output);
        }
        worker.Painted.ShouldBe(4);
    }
}
