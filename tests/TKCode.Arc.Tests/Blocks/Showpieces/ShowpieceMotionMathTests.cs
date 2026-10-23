using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>Springs, glides, wheel factors and the van Wijk path, against the numbers measured on the Showpieces.</summary>
public class ShowpieceMotionMathTests
{
    /// <summary>Progress of a spring released from rest toward 1 after <paramref name="t"/> seconds.</summary>
    private static double Progress(SpringSpec spec, double t) => 1 + SpringSolver.Evaluate(spec, -1, 0, t).Displacement;

    [Fact]
    public void Critical_Springs_Have_Unit_Damping_Ratio()
    {
        var spec = ShowpieceSprings.Critical(25);
        spec.Stiffness.ShouldBe(625);
        spec.Damping.ShouldBe(50);
        spec.DampingRatio.ShouldBe(1, 1e-12);
        ShowpieceSprings.CameraFollow.ShouldBe(spec);
        Should.Throw<ArgumentOutOfRangeException>(() => ShowpieceSprings.Critical(0));
    }

    [Fact]
    public void Presets_Carry_The_Measured_Constants()
    {
        ShowpieceSprings.ScrollFollow.Stiffness.ShouldBe(330);
        ShowpieceSprings.ScrollFollow.Damping.ShouldBe(60);
        ShowpieceSprings.ScrollFollow.DampingRatio.ShouldBe(1.65, 0.01);
        // Customer galaxy: zoom ω 10.3 ≈ FromVisual(0.51, 0); flights ω 5.7 ≈ FromVisual(0.92, 0); recenter ω 5.
        ShowpieceSprings.ZoomFollow.NaturalFrequency.ShouldBe(SpringSpec.FromVisual(0.51, 0).NaturalFrequency, 0.05);
        ShowpieceSprings.FlyFollow.NaturalFrequency.ShouldBe(SpringSpec.FromVisual(0.92, 0).NaturalFrequency, 0.05);
        ShowpieceSprings.Recenter.NaturalFrequency.ShouldBe(5, 1e-12);
        // Revenue globe: flight k 38.4 / c 12.4 (ω ≈ 6.2, critical), pitch home ω 3, zoom clear ω 8.
        ShowpieceSprings.GlobeFly.NaturalFrequency.ShouldBe(6.2, 0.01);
        ShowpieceSprings.GlobeFly.DampingRatio.ShouldBe(1, 0.01);
        ShowpieceSprings.PitchHome.NaturalFrequency.ShouldBe(3, 1e-12);
        ShowpieceSprings.ZoomClear.NaturalFrequency.ShouldBe(8, 1e-12);
        // Activity terrain fits.
        ShowpieceSprings.OrbitFlyIn.ShouldBe(SpringSpec.FromPhysical(21.7, 9.24));
        ShowpieceSprings.OrbitExit.ShouldBe(SpringSpec.FromPhysical(34.8, 11.9));
        ShowpieceSprings.OrbitReset.ShouldBe(SpringSpec.FromPhysical(31.2, 11.3));
        ShowpieceSprings.OrbitTopView.ShouldBe(SpringSpec.FromPhysical(28.3, 10.8));
        ShowpieceSprings.OrbitZoom.ShouldBe(SpringSpec.FromPhysical(223, 30.7));
        ShowpieceSprings.CardFollow.ShouldBe(SpringSpec.FromPhysical(138, 20.6));
        ShowpieceSprings.CardFollow.DampingRatio.ShouldBe(0.88, 0.01);
        // Layout morph.
        ShowpieceSprings.MorphProgress.ShouldBe(SpringSpec.FromPhysical(100, 20));
        ShowpieceSprings.MorphLift.ShouldBe(SpringSpec.FromPhysical(500, 49));
    }

    [Fact]
    public void Preset_Curves_Match_The_Frame_Captures()
    {
        // Terrain fly-in: 50 / 90 / 99% at 358 / 825 / 1400ms.
        Progress(ShowpieceSprings.OrbitFlyIn, 0.358).ShouldBe(0.5, 0.02);
        Progress(ShowpieceSprings.OrbitFlyIn, 0.825).ShouldBe(0.9, 0.02);
        Progress(ShowpieceSprings.OrbitFlyIn, 1.4).ShouldBe(0.99, 0.01);
        // Terrain zoom: 50% at 114ms, 90% at 269ms.
        Progress(ShowpieceSprings.OrbitZoom, 0.114).ShouldBe(0.5, 0.03);
        Progress(ShowpieceSprings.OrbitZoom, 0.269).ShouldBe(0.9, 0.03);
        // Exploded view scroll smoothing: 50 / 90 / 95 / 99% at 132 / 392 / 504 / 764ms.
        Progress(ShowpieceSprings.ScrollFollow, 0.132).ShouldBe(0.5, 0.03);
        Progress(ShowpieceSprings.ScrollFollow, 0.392).ShouldBe(0.9, 0.02);
        Progress(ShowpieceSprings.ScrollFollow, 0.504).ShouldBe(0.95, 0.02);
        Progress(ShowpieceSprings.ScrollFollow, 0.764).ShouldBe(0.99, 0.01);
        // Layout morph card progress: 100ms 0.27, 200ms 0.59, 400ms 0.90, 800ms 0.995.
        Progress(ShowpieceSprings.MorphProgress, 0.1).ShouldBe(0.27, 0.02);
        Progress(ShowpieceSprings.MorphProgress, 0.2).ShouldBe(0.59, 0.02);
        Progress(ShowpieceSprings.MorphProgress, 0.4).ShouldBe(0.90, 0.02);
        Progress(ShowpieceSprings.MorphProgress, 0.8).ShouldBe(0.995, 0.01);
        // Semantic zoom camera follow: 95% at about 180ms.
        Progress(ShowpieceSprings.CameraFollow, 0.19).ShouldBe(0.95, 0.02);
        // Globe flight: 0.51 at 268ms, 0.92 at 669ms.
        Progress(ShowpieceSprings.GlobeFly, 0.268).ShouldBe(0.51, 0.03);
        Progress(ShowpieceSprings.GlobeFly, 0.669).ShouldBe(0.92, 0.02);
    }

    [Fact]
    public void Spring_Channel_Steps_Exactly_And_Keeps_Velocity_On_Retarget()
    {
        var coarse = new ShowpieceSpringChannel(0, 1e-6, 1e-6);
        var fine = new ShowpieceSpringChannel(0, 1e-6, 1e-6);
        coarse.SpringTo(100, ShowpieceSprings.CameraFollow);
        fine.SpringTo(100, ShowpieceSprings.CameraFollow);
        coarse.Step(0.1);
        for (var i = 0; i < 10; i++)
        {
            fine.Step(0.01);
        }
        fine.Value.ShouldBe(coarse.Value, 1e-9);
        fine.Velocity.ShouldBe(coarse.Velocity, 1e-9);
        var value = fine.Value;
        var velocity = fine.Velocity;
        velocity.ShouldBeGreaterThan(0);
        fine.SpringTo(-50, ShowpieceSprings.FlyFollow);
        fine.Value.ShouldBe(value);
        fine.Velocity.ShouldBe(velocity);
        fine.IsMoving.ShouldBeTrue();
        // Runs to rest at the target.
        for (var i = 0; i < 600 && fine.Step(1 / 60.0); i++)
        {
        }
        fine.IsMoving.ShouldBeFalse();
        fine.Value.ShouldBe(-50);
        fine.Velocity.ShouldBe(0);
    }

    [Fact]
    public void Frame_Factors_Convert_To_The_Measured_Time_Constants()
    {
        ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.PanFrameFactor).ShouldBe(0.274, 0.002);
        ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.OrbitFrameFactor).ShouldBe(0.31, 0.005);
        ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.GlobeFrameFactor).ShouldBe(1.27, 0.03);
        ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.HoverFrameFactor).ShouldBe(0.09, 0.005);
        ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.DimFrameFactor).ShouldBe(0.14, 0.005);
        ShowpieceInertia.FrameFactorFromTimeConstant(ShowpieceInertia.TimeConstantFromFrameFactor(0.941)).ShouldBe(0.941, 1e-12);
        // e^(−3.65 t): Semantic zoom's 0.941 per frame.
        (1 / ShowpieceInertia.TimeConstantFromFrameFactor(0.941)).ShouldBe(3.65, 0.01);
    }

    [Fact]
    public void Glides_Travel_The_Measured_Distances()
    {
        // Semantic zoom: a 290px/s release glides about 81px (v·τ = 79.5).
        var panTau = ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.PanFrameFactor);
        ShowpieceInertia.GlideDistance(290, panTau).ShouldBe(81, 3);
        // Activity terrain: 303px/s at 0.318°/px → 96.6°/s glides about 30° more.
        var orbitTau = ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.OrbitFrameFactor);
        ShowpieceInertia.GlideDistance(303 * 0.318, orbitTau).ShouldBe(30, 1);

        var glide = new ShowpieceInertia(panTau, 3);
        glide.Start(290);
        var travelled = 0.0;
        var frames = 0;
        while (glide.IsActive && frames < 1000)
        {
            var v = glide.Velocity;
            travelled += glide.Step(1 / 60.0);
            if (frames == 0)
            {
                // One 60Hz frame scales the speed by the measured factor.
                (glide.Velocity / v).ShouldBe(0.941, 1e-9);
            }
            frames++;
        }
        travelled.ShouldBe(290 * panTau, 3);
        // Stops under 3px/s: about 1.3s.
        (frames / 60.0).ShouldBe(1.3, 0.15);
        // Too slow to glide at all.
        glide.Start(2);
        glide.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void First_Order_Approach_Follows_Its_Time_Constant()
    {
        var x = ShowpieceInertia.Approach(0, 1, 0.096, 0.096);
        x.ShouldBe(1 - Math.Exp(-1), 1e-12);
        ShowpieceInertia.Approach(0.9995, 1, 0.1, 0.01).ShouldBe(1);
    }

    [Fact]
    public void Wheel_Factors_Match_The_Web_Measurements()
    {
        // One notch (web deltaY −100, Avalonia Delta.Y +1) zooms ×1.246 = exp(0.0022·100).
        ShowpieceWheel.ZoomFactor(1, ShowpieceWheel.WheelSensitivity).ShouldBe(1.2461, 1e-4);
        ShowpieceWheel.ZoomFactor(-1, ShowpieceWheel.WheelSensitivity).ShouldBe(1 / 1.2461, 1e-4);
        ShowpieceWheel.ZoomFactorFromWebDelta(-1000, ShowpieceWheel.WheelSensitivity).ShouldBe(9.03, 0.01);
        // Ctrl / pinch: exp(−deltaY·0.012), −100 → ×3.3.
        ShowpieceWheel.ZoomFactorFromWebDelta(-100, ShowpieceWheel.PinchSensitivity).ShouldBe(3.32, 0.01);
        // Activity terrain Ctrl + wheel: exp(−deltaY·0.01), −20 → ×1.2214.
        ShowpieceWheel.ZoomFactorFromWebDelta(-20, ShowpieceWheel.OrbitPinchSensitivity).ShouldBe(1.2214, 1e-4);
        ShowpieceWheel.WebDeltaY(1).ShouldBe(-100);
        // A trackpad pinch of +10% at the terrain's sensitivity is exactly ×1.1.
        ShowpieceWheel.MagnifyFactor(0.1, ShowpieceWheel.OrbitPinchSensitivity).ShouldBe(1.1, 1e-12);
    }

    [Fact]
    public void Wheel_Devices_Are_Told_Apart_By_Notches_And_Bursts()
    {
        ShowpieceWheel.Classify(new Vector(0, 1)).ShouldBe(ShowpieceWheelDevice.Notched);
        ShowpieceWheel.Classify(new Vector(0, -2)).ShouldBe(ShowpieceWheelDevice.Notched);
        ShowpieceWheel.Classify(new Vector(0, 0.25)).ShouldBe(ShowpieceWheelDevice.Precise);
        ShowpieceWheel.Classify(new Vector(0.5, 1)).ShouldBe(ShowpieceWheelDevice.Precise);

        var classifier = new ShowpieceWheelClassifier();
        var t = TimeSpan.Zero;
        classifier.Classify(new Vector(0, 1), t).ShouldBe(ShowpieceWheelDevice.Notched);
        // A fractional event marks the whole burst as a trackpad, even a later whole-number delta.
        classifier.Classify(new Vector(0, 0.4), t + TimeSpan.FromMilliseconds(16)).ShouldBe(ShowpieceWheelDevice.Precise);
        classifier.Classify(new Vector(0, 1), t + TimeSpan.FromMilliseconds(32)).ShouldBe(ShowpieceWheelDevice.Precise);
        // After a rest the next burst starts fresh.
        classifier.Classify(new Vector(0, 1), t + TimeSpan.FromMilliseconds(500)).ShouldBe(ShowpieceWheelDevice.Notched);
    }

    [Fact]
    public void Van_Wijk_Path_Has_The_Closed_Form_Length_And_Endpoints()
    {
        var rho = Math.Sqrt(2);
        // Pure pan between equal widths: S = 2·asinh(ρ²d / 2w) / ρ (d = w → 1.2465).
        var pan = VanWijkPath.Create(new Point(0, 0), 1, new Point(1, 0), 1);
        pan.S.ShouldBe(2 * Math.Asinh(rho * rho / 2) / rho, 1e-9);
        pan.S.ShouldBe(1.2465, 1e-4);
        // Pure zoom: S = ln(w1/w0)/ρ (negative when zooming in).
        var zoom = VanWijkPath.Create(new Point(5, 5), 100, new Point(5, 5), 100 / 1.5876);
        zoom.S.ShouldBe(Math.Log(1 / 1.5876) / rho, 1e-9);
        zoom.Length.ShouldBe(-zoom.S);
        // Endpoints are exact.
        var path = VanWijkPath.Create(new Point(0, 0), 600, new Point(900, 300), 60);
        path.At(0).Center.ShouldBe(new Point(0, 0));
        path.At(0).Width.ShouldBe(600);
        path.At(1).Center.ShouldBe(new Point(900, 300));
        path.At(1).Width.ShouldBe(60);
        // Monotone travel along the straight line between the centers.
        var previous = -1.0;
        for (var i = 0; i <= 20; i++)
        {
            var (c, w) = path.At(i / 20.0);
            w.ShouldBeGreaterThan(0);
            (c.Y / 300 - c.X / 900).ShouldBe(0, 1e-9);
            c.X.ShouldBeGreaterThanOrEqualTo(previous - 1e-9);
            previous = c.X;
        }
    }

    [Fact]
    public void Van_Wijk_Zooms_Out_First_On_Long_Trips()
    {
        var path = VanWijkPath.Create(new Point(0, 0), 100, new Point(2000, 0), 100);
        var widest = 0.0;
        for (var i = 0; i <= 50; i++)
        {
            widest = Math.Max(widest, path.At(i / 50.0).Width);
        }
        widest.ShouldBeGreaterThan(300);
        path.At(0.5).Width.ShouldBe(widest, 1);
    }

    [Fact]
    public void Flight_Duration_Is_460_Plus_380_S_And_Fits_The_Measured_Flights()
    {
        VanWijkPath.DurationFor(0).TotalMilliseconds.ShouldBe(460, 1e-9);
        VanWijkPath.DurationFor(1.96).TotalMilliseconds.ShouldBe(460 + 380 * 1.96, 1e-9);
        // Semantic zoom 6.1: (S, ms) of seven flights; the fit's rms is about 55ms.
        var measured = new (double S, double Ms)[] { (0.52, 570), (0.68, 770), (1.06, 850), (1.16, 880), (1.96, 1120), (2.79, 1530) };
        var sum = 0.0;
        foreach (var (s, ms) in measured)
        {
            var error = VanWijkPath.DurationFor(s).TotalMilliseconds - ms;
            sum += error * error;
        }
        Math.Sqrt(sum / measured.Length).ShouldBeLessThan(60);
    }

    [Fact]
    public void Rubber_Band_Reproduces_The_Measured_Stretches()
    {
        // Semantic zoom pan: overflow 10 / 20 / 50 / 100 px on a 663px stage → 5.45 / 10.8 / 26.4 / 50.8.
        RubberBand.Apply(10, 663).ShouldBe(5.45, 0.05);
        RubberBand.Apply(20, 663).ShouldBe(10.8, 0.05);
        RubberBand.Apply(50, 663).ShouldBe(26.4, 0.05);
        RubberBand.Apply(100, 663).ShouldBe(50.8, 0.05);
        // Semantic zoom ln z: −0.055 → −0.026, −0.11 → −0.048 (dimension 0.2).
        RubberBand.Apply(-0.055, 0.2).ShouldBe(-0.026, 0.002);
        RubberBand.Apply(-0.11, 0.2).ShouldBe(-0.048, 0.003);
    }
}
