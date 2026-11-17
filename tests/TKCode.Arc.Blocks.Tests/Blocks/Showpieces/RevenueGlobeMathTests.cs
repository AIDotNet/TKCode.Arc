using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// The measured curves of the Revenue globe (spec sections 4 and 6): projection, land lattice, dot tables, arcs, rings,
/// flights, the replay speed profile and the ledger.
/// </summary>
public class RevenueGlobeMathTests
{
    private static double Rad(double degrees) => ShowpieceProjection.Radians(degrees);

    private static double Deg(double radians) => ShowpieceProjection.Degrees(radians);

    // Projection (4.1) -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Projection_Is_Orthographic_With_The_Spec_Formula()
    {
        var view = new ShowpieceSphereView(Rad(-65), Rad(22), 179.36, new Point(331.5, 218.5));
        foreach (var (lat, lon) in new[] { (39.74, -104.99), (51.5, -0.1), (-22.9, -43.2), (0.0, -65.0) })
        {
            var depth = view.Project(lat, lon, out var p);
            var phi = Rad(lat);
            var dl = Rad(lon) - Rad(-65);
            var pc = Rad(22);
            p.X.ShouldBe(331.5 + 179.36 * Math.Cos(phi) * Math.Sin(dl), 1e-9);
            p.Y.ShouldBe(218.5 - 179.36 * (Math.Sin(phi) * Math.Cos(pc) - Math.Cos(phi) * Math.Sin(pc) * Math.Cos(dl)), 1e-9);
            depth.ShouldBe(Math.Sin(phi) * Math.Sin(pc) + Math.Cos(phi) * Math.Cos(pc) * Math.Cos(dl), 1e-9);
        }
        // The view center projects onto the canvas center with depth 1.
        view.Project(22, -65, out var center).ShouldBe(1, 1e-9);
        center.ShouldBe(new Point(331.5, 218.5));
    }

    [Theory]
    [InlineData(663, 437.46, 179.36)]
    [InlineData(990, 580, 237.8)]
    [InlineData(356, 342, 140.2)]
    public void Radius_Is_41_Percent_Of_The_Smaller_Side(double width, double height, double radius)
    {
        (RevenueGlobeMath.RadiusFactor * Math.Min(width, height)).ShouldBe(radius, 0.1);
    }

    // Land (4.3, 11.5) --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Default_Land_Samples_The_Natural_Earth_Mask_On_Fibonacci_Lattices()
    {
        var land = RevenueGlobeLand.Default;
        // About 29% of a 12,800 point lattice is land; the sea keeps the rest of a 2,050 point lattice.
        land.LandCount.ShouldBeInRange(3300, 4200);
        land.OceanCount.ShouldBeInRange(1300, 1650);
        var points = land.LandPoints.Span;
        for (var i = 0; i < points.Length; i += 3)
        {
            var length = Math.Sqrt(points[i] * points[i] + points[i + 1] * points[i + 1] + points[i + 2] * points[i + 2]);
            length.ShouldBe(1, 1e-5);
        }
    }

    [Fact]
    public void Land_Mask_Knows_Land_From_Sea()
    {
        using var stream = typeof(RevenueGlobeLand).Assembly.GetManifestResourceStream(RevenueGlobeLand.MaskResourceName);
        stream.ShouldNotBeNull();
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        var mask = new RevenueGlobeLandMask(bytes);
        mask.IsLand(39.74, -104.99).ShouldBeTrue("Denver");
        mask.IsLand(23, 10).ShouldBeTrue("Sahara");
        mask.IsLand(-25, 134).ShouldBeTrue("Australia");
        mask.IsLand(30, -40).ShouldBeFalse("Atlantic");
        mask.IsLand(0, -150).ShouldBeFalse("Pacific");
        Should.Throw<ArgumentException>(() =>
        {
            _ = new RevenueGlobeLandMask(new byte[8]);
        });
    }

    [Theory]
    [InlineData(RevenueGlobeLand.DefaultLandLattice, 1.70)]
    [InlineData(RevenueGlobeLand.DefaultOceanLattice, 4.25)]
    public void Lattice_Neighbours_Are_As_Far_Apart_As_Measured(int n, double nearestDegrees)
    {
        // Nearest neighbour of points away from the poles (where Fibonacci lattices are regular).
        var points = Enumerable.Range(0, n).Select(i => RevenueGlobeLand.LatticePoint(i, n)).ToArray();
        var distances = new List<double>();
        for (var i = n / 4; i < 3 * n / 4; i += n / 40)
        {
            var best = double.MaxValue;
            for (var j = 0; j < n; j++)
            {
                if (j == i)
                {
                    continue;
                }
                var dot = points[i].X * points[j].X + points[i].Y * points[j].Y + points[i].Z * points[j].Z;
                best = Math.Min(best, Math.Acos(Math.Clamp(dot, -1, 1)));
            }
            distances.Add(Deg(best));
        }
        distances.Average().ShouldBe(nearestDegrees, nearestDegrees * 0.06);
    }

    // Dots (4.3, 4.4) ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Dot_Tables_Match_The_Eight_Measured_Buckets()
    {
        double[] radius = [0.76, 0.80, 0.84, 0.88, 0.92, 0.96, 1.00, 1.04];
        double[] land = [0.04, 0.11, 0.19, 0.26, 0.34, 0.41, 0.49, 0.56];
        double[] sea = [0.01, 0.04, 0.07, 0.10, 0.12, 0.15, 0.18, 0.21];
        for (var b = 0; b < 8; b++)
        {
            RevenueGlobeMath.LandDotRadius(b).ShouldBe(radius[b], 1e-9);
            RevenueGlobeMath.LandDotAlpha(b).ShouldBe(land[b], 1e-9);
            RevenueGlobeMath.OceanDotAlpha(b).ShouldBe(sea[b], 1e-9);
        }
        RevenueGlobeMath.Bucket(0.01).ShouldBe(0);
        RevenueGlobeMath.Bucket(0.5).ShouldBe(4);
        RevenueGlobeMath.Bucket(0.95).ShouldBe(7);
        RevenueGlobeMath.Bucket(1).ShouldBe(7);
        // ×1.08 and ×1.30 zoom: the smallest dot grows to 0.83 / 0.99 px.
        (RevenueGlobeMath.LandDotRadius(0) * 1.08).ShouldBe(0.82, 0.01);
        (RevenueGlobeMath.LandDotRadius(0) * 1.30).ShouldBe(0.99, 0.01);
    }

    [Fact]
    public void City_Dots_Grow_With_Revenue_And_Fade_With_Depth()
    {
        RevenueGlobeMath.CityDotRadius(100, 100).ShouldBe(3.4, 1e-9);
        RevenueGlobeMath.CityDotRadius(0, 100).ShouldBe(1.8, 1e-9);
        RevenueGlobeMath.CityDotRadius(25, 100).ShouldBe(2.6, 1e-9);
        RevenueGlobeMath.CityDotAlpha(0).ShouldBe(0.18, 1e-9);
        RevenueGlobeMath.CityDotAlpha(1).ShouldBe(0.72, 1e-9);
        RevenueGlobeMath.HqAlpha(1).ShouldBe(1, 1e-9);
        RevenueGlobeMath.HqAlpha(0).ShouldBe(0.36, 1e-9);
        RevenueGlobeMath.CityRingAlpha(1).ShouldBe(0.97, 1e-9);
        RevenueGlobeMath.CityRingAlpha(0.63).ShouldBe(0.70, 0.01);
    }

    // Arcs (4.5, 6.8) -------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, 0.047)]
    [InlineData(37, 0.090)]
    [InlineData(55, 0.113)]
    [InlineData(93, 0.165)]
    public void Arc_Lift_Grows_With_The_Distance(double degrees, double lift)
    {
        RevenueGlobeMath.ArcLift(Rad(degrees)).ShouldBe(lift, 0.003);
        RevenueGlobeMath.ArcLiftAt(Rad(degrees), 0).ShouldBe(0, 1e-12);
        RevenueGlobeMath.ArcLiftAt(Rad(degrees), 0.5).ShouldBe(lift, 0.003);
    }

    [Theory]
    [InlineData(5, 1.0)]
    [InlineData(37, 1.2)]
    [InlineData(81, 1.45)]
    public void Flight_Takes_970ms_Plus_5_9ms_Per_Degree(double degrees, double seconds)
    {
        RevenueGlobeMath.FlightDuration(Rad(degrees)).ShouldBe(seconds, 0.04);
    }

    [Fact]
    public void Arc_Weight_Follows_The_Amount()
    {
        RevenueGlobeMath.ArcWeight(0).ShouldBe(0.09);
        RevenueGlobeMath.ArcWeight(100).ShouldBe(0.5, 1e-9);
        RevenueGlobeMath.ArcWeight(10_000).ShouldBe(1);
        // Line width 1 + w, head radius 2 + 0.9 w (1.25 ↔ 2.23, 2.0 ↔ 2.9).
        (2 + 0.9 * 0.25).ShouldBe(2.225, 1e-9);
    }

    [Fact]
    public void Arcs_Are_57_Points_On_The_Great_Circle_Lifted_By_Sine()
    {
        var from = ShowpieceProjection.LatLonToUnit(42.36, -71.06);
        var to = ShowpieceProjection.LatLonToUnit(39.74, -104.99);
        var lifted = new float[RevenueGlobeMath.ArcPoints * 3];
        var ground = new float[RevenueGlobeMath.ArcPoints * 3];
        RevenueGlobeMath.BuildArc(from, to, lifted, ground, out var theta);
        theta.ShouldBe(RevenueGlobeMath.Angle(from, to), 1e-12);
        var normal = from.Cross(to).Normalized();
        for (var i = 0; i < RevenueGlobeMath.ArcPoints; i++)
        {
            var g = new ShowpieceVec3(ground[i * 3], ground[i * 3 + 1], ground[i * 3 + 2]);
            var l = new ShowpieceVec3(lifted[i * 3], lifted[i * 3 + 1], lifted[i * 3 + 2]);
            g.Length.ShouldBe(1, 1e-5);
            Math.Abs(g.Dot(normal)).ShouldBeLessThan(1e-5, "no cross-track drift");
            l.Length.ShouldBe(1 + RevenueGlobeMath.ArcLiftAt(theta, i / 56.0), 1e-5);
        }
        new ShowpieceVec3(ground[0], ground[1], ground[2]).Dot(from).ShouldBe(1, 1e-6);
        new ShowpieceVec3(ground[168], ground[169], ground[170]).Dot(to).ShouldBe(1, 1e-6);
    }

    [Fact]
    public void Back_Side_Tiers_Are_Quarters_Of_The_Alpha()
    {
        RevenueGlobeMath.ArcTier(0.3).ShouldBe(4);
        RevenueGlobeMath.ArcTier(0).ShouldBe(4);
        RevenueGlobeMath.ArcTier(-0.05).ShouldBe(3);
        RevenueGlobeMath.ArcTier(-0.15).ShouldBe(2);
        RevenueGlobeMath.ArcTier(-0.6).ShouldBe(1);
        (RevenueGlobeMath.ArcAlpha / 4).ShouldBe(0.23, 1e-9);
        (RevenueGlobeMath.StillArcAlpha * 3 / 4).ShouldBe(0.525, 1e-9);
    }

    [Fact]
    public void Head_Progress_Is_An_Ease_In_Out()
    {
        RevenueGlobeMath.EaseInOut(0).ShouldBe(0);
        RevenueGlobeMath.EaseInOut(1).ShouldBe(1);
        RevenueGlobeMath.EaseInOut(0.5).ShouldBe(0.5, 1e-6);
        // cubic-bezier(.65,0,.35,1) against the averaged frame samples of 6.8, which were timed from the frame after the
        // launch: with that 2% (about one frame) offset the curve fits them within 0.04 (rms 0.018).
        double[] t = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9];
        double[] measured = [0.02, 0.07, 0.16, 0.33, 0.57, 0.79, 0.90, 0.96, 0.99];
        for (var i = 0; i < t.Length; i++)
        {
            RevenueGlobeMath.EaseInOut(t[i] + 0.02).ShouldBe(measured[i], 0.04);
        }
    }

    [Fact]
    public void Arc_Lifetime_Is_Flight_Hold_And_Tail()
    {
        const double duration = 1.2;
        RevenueGlobeCanvas.ArcSpan(0.6, duration, out var tail, out var head, out var fade, out var flying);
        flying.ShouldBeTrue();
        tail.ShouldBe(0);
        head.ShouldBe(28, 0.01);
        fade.ShouldBe(1);
        RevenueGlobeCanvas.ArcSpan(duration + 0.3, duration, out tail, out head, out fade, out flying);
        flying.ShouldBeFalse();
        (tail, head, fade).ShouldBe((0.0, 56.0, 1.0));
        // Halfway through the tail: half the arc left, alpha × (0.4 + 0.6 × 0.5).
        RevenueGlobeCanvas.ArcSpan(duration + 0.35 + 0.435 / 2, duration, out tail, out head, out fade, out _);
        tail.ShouldBe(28, 0.01);
        fade.ShouldBe(0.7, 0.001);
    }

    [Fact]
    public void Rings_Open_Like_Ease_Out_Expo_And_Fade()
    {
        // Arrival ring: 3.1° (≈ 9.6 px at R 179.36), 1 s; launch ring 1.3° (≈ 4.1 px), 0.8 s.
        var arrival = 179.36 * Math.Sin(RevenueGlobeMath.ArrivalRingRadius);
        arrival.ShouldBe(9.7, 0.15);
        (arrival * RevenueGlobeMath.EaseOutExpo(0.2)).ShouldBe(7.2, 0.15);
        (arrival * RevenueGlobeMath.EaseOutExpo(0.4)).ShouldBe(9.0, 0.15);
        (0.84 * RevenueGlobeMath.RingFade(0.2)).ShouldBe(0.60, 0.01);
        (0.84 * RevenueGlobeMath.RingFade(0.4)).ShouldBe(0.39, 0.01);
        var launch = 179.36 * Math.Sin(RevenueGlobeMath.LaunchRingRadius);
        launch.ShouldBe(4.07, 0.05);
        (launch * RevenueGlobeMath.EaseOutExpo(0.2 / 0.8)).ShouldBe(3.36, 0.05);
        (launch * RevenueGlobeMath.EaseOutExpo(0.4 / 0.8)).ShouldBe(3.96, 0.05);
        (0.45 * RevenueGlobeMath.RingFade(0.2 / 0.8)).ShouldBe(0.29, 0.01);
    }

    // Flights (6.3) ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Flight_Progress_Is_A_Critically_Damped_Spring_Of_Omega_6_2()
    {
        var progress = new ShowpieceSpringChannel(0, 1e-4, 1e-3);
        progress.SpringTo(1, ShowpieceSprings.GlobeFly);
        var samples = new (double Time, double Value)[]
        {
            (0.067, 0.07), (0.134, 0.21), (0.201, 0.37), (0.268, 0.51), (0.335, 0.63), (0.402, 0.72), (0.536, 0.85),
            (0.669, 0.92), (0.800, 0.96), (1.066, 0.99)
        };
        var t = 0.0;
        foreach (var (time, value) in samples)
        {
            progress.Step(time - t);
            t = time;
            progress.Value.ShouldBe(value, 0.02, $"at {time}s");
        }
    }

    [Fact]
    public void Flight_Follows_The_Great_Circle_And_Dips_The_Zoom()
    {
        var path = new RevenueGlobeFlightPath(Rad(-65), Rad(22), 1, Rad(8.54), Rad(47.38), 1.3);
        var start = path.Evaluate(0);
        start.Yaw!.Value.ShouldBe(Rad(-65), 1e-9);
        start.Pitch!.Value.ShouldBe(Rad(22), 1e-9);
        start.Zoom!.Value.ShouldBe(1, 1e-9);
        var from = ShowpieceProjection.LatLonToUnit(22, -65);
        var to = ShowpieceProjection.LatLonToUnit(47.38, 8.54);
        var normal = from.Cross(to).Normalized();
        for (var s = 0.1; s < 1; s += 0.1)
        {
            var pose = path.Evaluate(s);
            var u = ShowpieceProjection.LatLonToUnit(Deg(pose.Pitch!.Value), Deg(pose.Yaw!.Value));
            Math.Abs(u.Dot(normal)).ShouldBeLessThan(1e-9, "the view moves on the great circle");
        }
        var end = path.Evaluate(1);
        ShowpieceProjection.WrapAngle(end.Yaw!.Value - Rad(8.54)).ShouldBe(0, 1e-9);
        end.Pitch!.Value.ShouldBe(Rad(47.38), 1e-9);
        end.Zoom!.Value.ShouldBe(1.3, 1e-9);
        // Zoom dip 0.052 × θ at the middle: 61° → 5.5%, 139° → 12.6% below the straight line.
        var sixtyOne = new RevenueGlobeFlightPath(0, 0, 1, Rad(61), 0, 1);
        (1 - sixtyOne.ZoomAt(0.5)).ShouldBe(0.0554, 0.001);
        var long139 = new RevenueGlobeFlightPath(0, 0, 1, Rad(139), 0, 1);
        (1 - long139.ZoomAt(0.5)).ShouldBe(0.126, 0.002);
    }

    [Fact]
    public void Long_Flights_Bulge_Toward_The_Pole()
    {
        // From the home view (22° N, −65°) to a city across the Pacific, the camera passes far north.
        var path = new RevenueGlobeFlightPath(Rad(-65), Rad(22), 1, Rad(135.5), Rad(34.69), 1.3);
        var highest = Enumerable.Range(0, 21).Select(i => Deg(path.Evaluate(i / 20.0).Pitch!.Value)).Max();
        highest.ShouldBeGreaterThan(50);
        // The longitude stays continuous (no 360° jump between steps).
        var previous = path.Evaluate(0).Yaw!.Value;
        for (var i = 1; i <= 40; i++)
        {
            var yaw = path.Evaluate(i / 40.0).Yaw!.Value;
            Math.Abs(yaw - previous).ShouldBeLessThan(Math.PI / 2);
            previous = yaw;
        }
    }

    [Fact]
    public void Centroid_Weights_By_Revenue()
    {
        var a = ShowpieceProjection.LatLonToUnit(0, 0);
        var b = ShowpieceProjection.LatLonToUnit(0, 90);
        var center = RevenueGlobeMath.Centroid([a, b], [1, 1])!.Value;
        Deg(RevenueGlobeMath.ToLatLon(center).Longitude).ShouldBe(45, 1e-6);
        var heavy = RevenueGlobeMath.Centroid([a, b], [3, 1])!.Value;
        Deg(RevenueGlobeMath.ToLatLon(heavy).Longitude).ShouldBeLessThan(45);
        RevenueGlobeMath.Centroid([a, b], [0, 0])!.Value.Length.ShouldBe(1, 1e-9);
        RevenueGlobeMath.Centroid([], []).ShouldBeNull();
    }

    // Camera (6.2) -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Dragging_Down_200px_Rubber_Bands_The_Tilt_To_About_75_Degrees()
    {
        var camera = new ShowpieceOrbitCamera
        {
            PitchMin = Rad(-65),
            PitchMax = Rad(65),
            PitchRubberBandDimension = Rad(25),
            PitchRubberBandFactor = 0.77,
            PitchReturnSpring = ShowpieceSprings.PitchHome,
            YawPerPixel = -1 / 179.36,
            PitchPerPixel = 1 / 179.36
        };
        camera.JumpTo(new ShowpieceOrbitPose { Yaw = 0, Pitch = Rad(22), Zoom = 1 });
        camera.BeginDrag();
        camera.DragBy(new Vector(0, 200));
        Deg(camera.Pitch).ShouldBe(74.9, 1);
        // 1px = 1/R rad: dragging right 100px turns the center 31.9° west.
        camera.DragBy(new Vector(100, 0));
        Deg(camera.Yaw).ShouldBe(-31.94, 0.05);
    }

    // Playback (6.4) -----------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0.2, 950)]
    [InlineData(0.6, 1220)]
    [InlineData(1.6, 1470)]
    public void Replay_Speed_Ramps_Up_As_Measured(double seconds, double speed)
    {
        RevenueGlobePlayback.RampSpeed(0, 1500, seconds).ShouldBe(speed, 50);
    }

    [Fact]
    public void Replay_Plays_The_Day_And_Lands_Exactly_On_The_Present()
    {
        var playback = new RevenueGlobePlayback();
        playback.SetNow(16.9 * 3600);
        playback.Replay();
        playback.State.ShouldBe(RevenueGlobePlaybackState.Replaying);
        playback.Time.ShouldBe(0);
        var seconds = 0.0;
        var wentLive = false;
        while (!wentLive && seconds < 120)
        {
            playback.SetNow(playback.Now + 1 / 60.0);
            wentLive = playback.Advance(1 / 60.0);
            seconds += 1 / 60.0;
        }
        wentLive.ShouldBeTrue();
        playback.State.ShouldBe(RevenueGlobePlaybackState.Live);
        playback.Time.ShouldBe(playback.Now);
        // 16.9 hours at 1500× is 40.6 s; the ramp and the brake add about a second (measured ≈ 41.5 s).
        seconds.ShouldBe(41.5, 1.0);
    }

    [Fact]
    public void Go_Live_Fast_Forwards_On_A_Trapezoid()
    {
        RevenueGlobePlayback.FastForwardDuration(9 / 3600.0).ShouldBe(0.6, 0.01);
        RevenueGlobePlayback.FastForwardDuration(15.8).ShouldBe(1.94, 0.01);
        RevenueGlobePlayback.FastForwardDuration(40).ShouldBe(2.0);
        RevenueGlobePlayback.Trapezoid(0, 0.2, 0.2).ShouldBe(0);
        RevenueGlobePlayback.Trapezoid(1, 0.2, 0.2).ShouldBe(1, 1e-12);
        RevenueGlobePlayback.Trapezoid(0.5, 0.2, 0.2).ShouldBe(0.5, 1e-12);
        var last = 0.0;
        for (var u = 0.01; u <= 1; u += 0.01)
        {
            var s = RevenueGlobePlayback.Trapezoid(u, 0.4 / 1.5, 0.35 / 1.5);
            s.ShouldBeGreaterThanOrEqualTo(last);
            last = s;
        }

        var playback = new RevenueGlobePlayback();
        playback.SetNow(15 * 3600);
        playback.Seek(3600, continueReplay: false);
        playback.State.ShouldBe(RevenueGlobePlaybackState.Paused);
        playback.GoLive(animate: true);
        playback.IsFastForwarding.ShouldBeTrue();
        var duration = RevenueGlobePlayback.FastForwardDuration(14);
        var elapsed = 0.0;
        while (!playback.Advance(1 / 60.0))
        {
            elapsed += 1 / 60.0;
            elapsed.ShouldBeLessThan(3);
        }
        elapsed.ShouldBe(duration, 0.05);
        playback.IsLive.ShouldBeTrue();
    }

    [Fact]
    public void Pause_Play_Seek_And_Scrub_Change_The_State()
    {
        var playback = new RevenueGlobePlayback();
        playback.SetNow(10 * 3600);
        playback.IsLive.ShouldBeTrue();
        playback.Pause();
        playback.State.ShouldBe(RevenueGlobePlaybackState.Paused);
        playback.SetNow(10 * 3600 + 60);
        playback.Time.ShouldBe(10 * 3600, "a paused playhead stays");
        playback.Play();
        playback.State.ShouldBe(RevenueGlobePlaybackState.Replaying);
        playback.Seek(20 * 3600, continueReplay: true);
        playback.IsLive.ShouldBeTrue("a seek past the present goes live");
        playback.BeginScrub();
        playback.ScrubTo(-50);
        playback.Time.ShouldBe(0);
        playback.State.ShouldBe(RevenueGlobePlaybackState.Scrubbing);
        playback.EndScrub(14000, continueReplay: true);
        playback.State.ShouldBe(RevenueGlobePlaybackState.Replaying);
        playback.Velocity.ShouldBe(14000);
        playback.Advance(1.0);
        // The release speed eases down toward the replay speed.
        playback.Velocity.ShouldBeLessThan(14000);
        playback.Velocity.ShouldBeGreaterThan(1500);
    }

    // Ledger (2.2, 6.4) ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Ledger_Answers_Totals_At_Any_Moment()
    {
        var ledger = new RevenueGlobeLedger(2);
        ledger.Build([(100.0, 0, 10.0), (50.0, 1, 5.0), (3000.0, 0, 20.0), (7300.0, 1, 1.0), (7300.0, -1, 2.0)], 2);
        ledger.Count.ShouldBe(5);
        ledger.TimeOf(0).ShouldBe(50, "sorted by time");
        ledger.CountAt(49).ShouldBe(0);
        ledger.CountAt(100).ShouldBe(2);
        ledger.RevenueAt(3000).ShouldBe(35);
        ledger.RevenueAt(1e9).ShouldBe(38);
        ledger.CityRevenueAt(0, 3000).ShouldBe(30);
        ledger.CityCountAt(1, 8000).ShouldBe(2);
        ledger.CityRevenueAt(5, 8000).ShouldBe(0);
        // Last hour at 7300: the payments in (3700, 7300].
        ledger.RevenueBetween(7300 - 3600, 7300).ShouldBe(3);
        ledger.Bins[0].ShouldBe(2);
        ledger.Bins[3].ShouldBe(1);
        ledger.Bins[8].ShouldBe(2);
        ledger.MaxBin.ShouldBe(2);
        ledger.Append(7299, 0, 1).ShouldBeFalse("out of order");
        ledger.Append(7400, 0, 1).ShouldBeTrue();
        ledger.CityRevenueAt(0, 1e9).ShouldBe(31);
        RevenueGlobeLedger.BarHeight(2, 2).ShouldBe(36);
        RevenueGlobeLedger.BarHeight(0, 2).ShouldBe(4);
        RevenueGlobeLedger.BarHeight(1, 0).ShouldBe(0);
    }
}
