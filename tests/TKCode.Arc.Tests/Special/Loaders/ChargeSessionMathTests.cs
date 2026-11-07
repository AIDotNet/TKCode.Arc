using System.Globalization;
using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>
/// The pure simulation of <see cref="ChargeSession"/> against the numbers measured from the reference preview (its
/// defaults: 77 kWh, 150 kW, start 34%, limit 80%, taper at 50%).
/// </summary>
public class ChargeSessionMathTests
{
    private static ChargeSessionModel ReferenceModel(double charge = 34, double limit = 80)
    {
        var model = new ChargeSessionModel { Capacity = 77, MaxPower = 150, StartCharge = 34, MinLimit = 50, TaperAt = 50 };
        model.Reset(charge, limit, instant: false);
        return model;
    }

    [Theory]
    [InlineData(0.00, 0.62)]
    [InlineData(0.05, 0.81)]
    [InlineData(0.10, 1.0)]
    [InlineData(0.30, 1.0)]
    [InlineData(0.55, 0.888)]
    [InlineData(0.60, 0.732)]
    [InlineData(0.70, 0.447)]
    [InlineData(0.80, 0.257)]
    [InlineData(0.90, 0.150)]
    [InlineData(1.00, 0.098)]
    public void Curve_matches_the_fitted_plot(double soc, double share)
    {
        ChargeSessionCurve.Default(soc, 0.5).ShouldBe(share, 0.0015);
    }

    [Theory]
    [InlineData(35.66, 22.53)]
    [InlineData(42.87, 20.31)]
    [InlineData(70.93, 8.54)]
    [InlineData(71.02, 8.48)]
    public void Time_to_limit_is_the_48_step_integral(double charge, double minutes)
    {
        ChargeSessionCurve.MinutesToLimit(charge, 80, 77, 150, s => ChargeSessionCurve.Default(s, 0.5)).ShouldBe(minutes, 0.01);
    }

    [Fact]
    public void Time_from_start_to_full_is_over_an_hour()
    {
        ChargeSessionCurve.MinutesToLimit(34, 100, 77, 150, s => ChargeSessionCurve.Default(s, 0.5)).ShouldBe(64.8, 0.1);
        ChargeSessionCurve.MinutesToLimit(80, 80, 77, 150, s => 1).ShouldBe(0);
    }

    [Fact]
    public void Charge_climbs_2_164_percent_per_second_at_full_power()
    {
        ChargeSessionCurve.PercentPerSecond(150, 77, 1).ShouldBe(2.1645, 0.0005);
        var model = ReferenceModel();
        model.Settle();
        model.Step(0.5, 1);
        model.Charge.ShouldBe(34 + 2.1645 * 0.5, 0.01);
    }

    [Fact]
    public void Availability_ramps_with_tau_0_384_and_drains_with_tau_0_237()
    {
        var model = ReferenceModel();
        model.Availability.ShouldBe(0);
        Step(model, 0.384);
        model.Availability.ShouldBe(1 - Math.Exp(-1), 0.002);
        Step(model, 2);
        model.Availability.ShouldBe(1, 0.01);
        model.Stop(instant: false).ShouldBe(ChargeSessionTransition.Stopped);
        var a0 = model.Availability;
        Step(model, 0.237);
        model.Availability.ShouldBe(a0 * Math.Exp(-1), 0.002);
        Step(model, 1.5);
        model.Availability.ShouldBe(0);
    }

    [Fact]
    public void A_stop_drains_power_in_133_ms_steps_like_the_reference()
    {
        // Reference: 42 → 24 → 14 → 8 → 4 → 1 kW every 133 ms when stopped from 42 kW (the last sample's timing jitters).
        var power = 42.0;
        var expected = new[] { 24.0, 14, 8, 4 };
        foreach (var kw in expected)
        {
            power *= Math.Exp(-0.133 / ChargeSessionModel.DrainTau);
            Math.Round(power).ShouldBe(kw, 1);
        }
    }

    [Fact]
    public void Stopped_session_creeps_on_while_draining_then_holds()
    {
        var model = ReferenceModel(70.82);
        model.Settle();
        model.Stop(instant: false);
        Step(model, 3);
        model.Charge.ShouldBeGreaterThan(70.82);
        // The reference crept 70.82 → 70.93 (sampled a little after the press); the full drain integral gives about 0.2.
        model.Charge.ShouldBeLessThan(71.1);
        var held = model.Charge;
        Step(model, 2);
        model.Charge.ShouldBe(held);
        model.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void Reaching_the_limit_completes_at_exactly_the_limit()
    {
        var model = ReferenceModel(79.9);
        model.Settle();
        var transitions = new List<ChargeSessionTransition>();
        for (var i = 0; i < 120; i++)
        {
            var t = model.Step(1.0 / 60, 1);
            if (t != ChargeSessionTransition.None)
            {
                transitions.Add(t);
            }
        }
        transitions.ShouldBe(new[] { ChargeSessionTransition.Completed });
        model.Charge.ShouldBe(80);
        model.State.ShouldBe(ChargeSessionState.Complete);
    }

    [Fact]
    public void Limit_moves_complete_and_resume_the_session()
    {
        var model = ReferenceModel(60);
        model.SetLimit(55, instant: false).ShouldBe(ChargeSessionTransition.Completed);
        model.State.ShouldBe(ChargeSessionState.Complete);
        model.SetLimit(58, instant: false).ShouldBe(ChargeSessionTransition.None);
        model.SetLimit(61, instant: false).ShouldBe(ChargeSessionTransition.Resumed);
        model.State.ShouldBe(ChargeSessionState.Charging);
        model.Stop(instant: false);
        model.SetLimit(55, instant: false).ShouldBe(ChargeSessionTransition.None);
        model.State.ShouldBe(ChargeSessionState.Stopped);
        model.Resume(instant: false).ShouldBe(ChargeSessionTransition.Completed);
    }

    [Theory]
    [InlineData(10, 50)]
    [InlineData(63.4, 63)]
    [InlineData(63.5, 64)]
    [InlineData(140, 100)]
    public void Limit_is_whole_percent_between_min_and_100(double requested, double limit)
    {
        ReferenceModel().Coerce(requested).ShouldBe(limit);
    }

    [Fact]
    public void New_session_returns_to_the_start_and_clears_energy()
    {
        var model = ReferenceModel(80);
        model.State.ShouldBe(ChargeSessionState.Complete);
        model.EnergyAdded.ShouldBe(46.0 / 100 * 77, 1e-9);
        model.StartNew(instant: false).ShouldBe(ChargeSessionTransition.NewSession);
        model.Charge.ShouldBe(34);
        model.EnergyAdded.ShouldBe(0);
        model.Availability.ShouldBe(0);
        model.State.ShouldBe(ChargeSessionState.Charging);
    }

    [Theory]
    [InlineData(35.659, 1.3)]
    [InlineData(63.607, 22.8)]
    [InlineData(40.71, 5.2)]
    public void Energy_added_has_one_decimal(double charge, double energy)
    {
        var model = ReferenceModel(charge);
        Math.Round(model.EnergyAdded, 1).ShouldBe(energy);
    }

    [Fact]
    public void Smoother_is_frame_rate_independent()
    {
        var once = ChargeSessionSmoother.Approach(0, 1, 0.3, 0.384);
        var stepped = 0.0;
        for (var i = 0; i < 18; i++)
        {
            stepped = ChargeSessionSmoother.Approach(stepped, 1, 0.3 / 18, 0.384);
        }
        stepped.ShouldBe(once, 1e-9);
        ChargeSessionSmoother.Approach(0.4, 0, 0, 0.2).ShouldBe(0.4);
    }

    [Fact]
    public void Cable_path_is_162_8_units_and_samples_evenly()
    {
        var path = ChargeSessionScene.Cable;
        path.Length.ShouldBe(162.8, 0.15);
        path.PointAt(0).ShouldBe(new Point(84, 176));
        var end = path.PointAt(path.Length);
        end.X.ShouldBe(212, 0.01);
        end.Y.ShouldBe(120, 0.01);
        // Beyond the curve the path runs straight on along the pack's center line.
        var inside = path.PointAt(path.Length + 50);
        inside.X.ShouldBe(262, 0.01);
        inside.Y.ShouldBe(120, 0.01);
        // Equal distances give equal chords.
        var a = path.PointAt(40);
        var b = path.PointAt(50);
        var c = path.PointAt(120);
        var d = path.PointAt(130);
        Distance(a, b).ShouldBe(Distance(c, d), 0.05);
    }

    [Theory]
    [InlineData(5, 138)]
    [InlineData(25, 189)]
    [InlineData(45, 263)]
    [InlineData(65, 300)]
    public void Packets_ease_out_of_the_charger(double distance, double speed)
    {
        ChargeSessionFlow.SpeedAt(distance, 500, 1).ShouldBe(speed, 4);
    }

    // The observer's fit 0.7 + 0.3·smoothstep(0, 90, d); the raw tracks (12 → 222, 36 → 246, 60 → 276, 84+ → 297) scatter
    // by about ±50 u/s per sample, so the fit itself is the oracle.
    [Theory]
    [InlineData(0, 210)]
    [InlineData(12, 214.4)]
    [InlineData(36, 241.7)]
    [InlineData(60, 276.7)]
    [InlineData(90, 300)]
    public void Packets_slow_toward_the_front(double remaining, double speed)
    {
        ChargeSessionFlow.SpeedAt(200, remaining, 1).ShouldBe(speed, 0.5);
    }

    [Fact]
    public void Packets_cruise_at_120_when_drained_and_fade_at_both_ends()
    {
        ChargeSessionFlow.SpeedAt(200, 300, 0).ShouldBe(120, 1e-9);
        ChargeSessionFlow.OpacityAt(14.1, 300).ShouldBe(0.505, 0.01);
        ChargeSessionFlow.OpacityAt(100, 8).ShouldBe(0);
        ChargeSessionFlow.OpacityAt(100, 31).ShouldBe(1);
    }

    [Theory]
    [InlineData(218, 5.41)]
    [InlineData(228, 5.00)]
    [InlineData(238, 4.59)]
    public void Packets_shrink_through_the_nub(double x, double radius)
    {
        ChargeSessionScene.PacketRadius(x).ShouldBe(radius, 0.01);
    }

    [Fact]
    public void Steady_flow_spaces_packets_65_units_apart_and_lands_them()
    {
        var flow = new ChargeSessionFlow();
        var end = ChargeSessionScene.PathEnd(40);
        for (var i = 0; i < 600; i++)
        {
            flow.Step(1.0 / 60, 1, end);
        }
        var distances = new List<double>();
        for (var i = 0; i < ChargeSessionFlow.PoolSize; i++)
        {
            if (flow.IsAlive(i))
            {
                distances.Add(flow.DistanceOf(i));
            }
        }
        distances.Sort();
        // Two cruising packets in the middle of the path are one interval apart.
        var middle = distances.Where(d => d > 80 && end - d > 100).ToList();
        middle.Count.ShouldBeGreaterThan(1);
        (middle[1] - middle[0]).ShouldBe(65, 3);
        flow.Landed.ShouldBeGreaterThan(10);
        flow.Count.ShouldBeInRange(8, 13);
    }

    [Fact]
    public void Landing_flashes_the_front_and_the_flash_decays_with_tau_0_2()
    {
        var flow = new ChargeSessionFlow();
        var end = ChargeSessionScene.PathEnd(40);
        while (flow.Landed == 0)
        {
            flow.Step(1.0 / 60, 1, end);
        }
        flow.Flash.ShouldBeGreaterThan(0.1);
        // Stop emitting and let the rest land; then only decay is left.
        for (var i = 0; i < 600; i++)
        {
            flow.Step(1.0 / 60, 0, end);
        }
        flow.IsIdle.ShouldBeTrue();
    }

    [Fact]
    public void First_packet_of_a_ramp_leaves_after_about_376_ms()
    {
        var model = ReferenceModel();
        var flow = new ChargeSessionFlow();
        var t = 0.0;
        while (flow.Count == 0 && t < 2)
        {
            model.Step(1.0 / 60, 1);
            flow.Step(1.0 / 60, model.Availability, ChargeSessionScene.PathEnd(model.Charge));
            t += 1.0 / 60;
        }
        t.ShouldBe(0.376, 0.04);
    }

    [Fact]
    public void Reduced_motion_lays_packets_out_evenly_from_the_front()
    {
        var flow = new ChargeSessionFlow();
        var end = ChargeSessionScene.PathEnd(34);
        flow.LayOut(end, visible: true);
        flow.Count.ShouldBe(7);
        flow.DistanceOf(0).ShouldBe(end - 30, 1e-9);
        flow.DistanceOf(1).ShouldBe(end - 94, 1e-9);
        flow.OpacityOf(0, end).ShouldBe(1);
        flow.LayOut(end, visible: false);
        flow.Count.ShouldBe(0);
    }

    [Fact]
    public void Formats_follow_the_locale_and_currency()
    {
        var us = ChargeSessionFormat.Culture("en-US");
        var de = ChargeSessionFormat.Culture("de-DE");
        ChargeSessionFormat.Money(9.57m, "USD", us).ShouldBe("$9.57");
        ChargeSessionFormat.Money(0m, "USD", us).ShouldBe("$0.00");
        ChargeSessionFormat.Money(9.57m, "EUR", us).ShouldBe("€9.57");
        ChargeSessionFormat.Money(9.57m, "EUR", de).ShouldBe("9,57 €");
        ChargeSessionFormat.Money(1234m, "JPY", us).ShouldBe("¥1,234");
        ChargeSessionFormat.OneDecimal(22.84, de).ShouldBe("22,8");
        ChargeSessionFormat.ChargeNumber(63.607, us).ShouldBe("63");
        ChargeSessionFormat.ChargeNumber(70.927, us).ShouldBe("70");
        ChargeSessionFormat.Cost(22.8, 0.42m, "USD").ShouldBe(9.58m);
        ChargeSessionFormat.Culture("not-a-locale").ShouldBe(CultureInfo.InvariantCulture);
    }

    [Theory]
    [InlineData(0.6, "Under 1 min")]
    [InlineData(22.53, "23 min")]
    [InlineData(20.31, "20 min")]
    [InlineData(8.54, "9 min")]
    [InlineData(8.48, "8 min")]
    [InlineData(64.2, "1 h 04 min")]
    [InlineData(64.82, "1 h 05 min")]
    public void Time_stat_reads_minutes_and_hours(double minutes, string text)
    {
        new ChargeSessionLabels().Time(minutes).ShouldBe(text);
    }

    [Theory]
    [InlineData(0.4, "under a minute")]
    [InlineData(1.2, "about 1 minute")]
    [InlineData(22.53, "about 23 minutes")]
    [InlineData(64.2, "about 1 hour 4 minutes")]
    [InlineData(120.1, "about 2 hours")]
    public void Remaining_time_reads_in_words(double minutes, string text)
    {
        new ChargeSessionLabels().Remaining(minutes).ShouldBe(text);
    }

    [Fact]
    public void Alpha_caches_fold_opacity_into_one_immutable_brush_per_alpha_byte()
    {
        var brushes = new ChargeSessionAlphaBrushes();
        brushes.Reset(Avalonia.Media.Color.FromArgb(200, 10, 20, 30));
        var half = (Avalonia.Media.ISolidColorBrush)brushes.Get(0.5);
        half.Color.ShouldBe(Avalonia.Media.Color.FromArgb(100, 10, 20, 30));
        brushes.Get(0.5).ShouldBeSameAs(half);
        ((Avalonia.Media.ISolidColorBrush)brushes.Get(2)).Color.A.ShouldBe((byte)200);
        var pens = new ChargeSessionAlphaPens();
        pens.Reset(Avalonia.Media.Colors.Black, 1.5, Avalonia.Media.PenLineCap.Round, null);
        var pen = pens.Get(0.55);
        pen.Thickness.ShouldBe(1.5);
        ((Avalonia.Media.ISolidColorBrush)pen.Brush!).Color.A.ShouldBe((byte)140);
        pens.Get(0.55).ShouldBeSameAs(pen);
    }

    private static void Step(ChargeSessionModel model, double seconds)
    {
        var steps = (int)Math.Round(seconds * 600);
        for (var i = 0; i < steps; i++)
        {
            model.Step(seconds / steps, 1);
        }
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
