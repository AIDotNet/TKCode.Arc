using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.AudioVideo;

/// <summary>The voice orb's model against the measured numbers, its simulated levels, colors and the control.</summary>
public class VoiceOrbTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static void Run(VoiceOrbModel model, double seconds, double level = 0)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += 1.0 / 60)
        {
            model.Step(1.0 / 60, level);
        }
    }

    [Fact]
    public void Idle_orb_breathes_around_0_302_of_the_size_every_5_03_seconds()
    {
        var model = new VoiceOrbModel();
        model.SetState(VoiceOrbState.Idle, false);
        model.Snap();
        var min = double.MaxValue;
        var max = double.MinValue;
        var mean = 0.0;
        var n = 0;
        for (var i = 0; i < 302; i++)
        {
            model.Step(1.0 / 60, 0);
            var r = model.MeanRadius(200);
            min = Math.Min(min, r);
            max = Math.Max(max, r);
            mean += r;
            n++;
        }
        (mean / n).ShouldBe(60.4, 0.25);
        (max - min).ShouldBe(3.4, 0.15);
        VoiceOrbModel.BreathPeriod.ShouldBe(5.027, 0.001);
    }

    [Fact]
    public void Listening_to_thinking_follows_the_fitted_spring_without_overshoot()
    {
        var model = new VoiceOrbModel();
        model.SetState(VoiceOrbState.Listening, false);
        model.Snap();
        model.SetState(VoiceOrbState.Thinking, false);
        double At(double seconds)
        {
            Run(model, seconds);
            return model[VoiceOrbModel.Radius] * 200;
        }
        // Measured (seq1 #1, from 62.5 to 55): 100 ms 60.5, 300 ms 57.3, 500 ms 55.9, 800 ms 55.1 (99 %).
        At(0.1).ShouldBe(60.7, 0.6);
        At(0.2).ShouldBe(57.9, 0.7);
        At(0.2).ShouldBe(56.3, 0.6);
        var settled = At(0.3);
        settled.ShouldBe(55.4, 0.4);
        var lowest = double.MaxValue;
        for (var i = 0; i < 120; i++)
        {
            model.Step(1.0 / 60, 0);
            lowest = Math.Min(lowest, model[VoiceOrbModel.Radius] * 200);
        }
        lowest.ShouldBeGreaterThan(54.99);
        VoiceOrbModel.ShapeSpring.NaturalFrequency.ShouldBe(7.42, 0.05);
        VoiceOrbModel.ShapeSpring.DampingRatio.ShouldBe(1.15, 0.01);
    }

    [Fact]
    public void Interrupting_a_state_change_keeps_the_springs_velocity()
    {
        var model = new VoiceOrbModel();
        model.SetState(VoiceOrbState.Listening, false);
        model.Snap();
        model.SetState(VoiceOrbState.Thinking, false);
        Run(model, 0.15);
        var velocity = model.VelocityOf(VoiceOrbModel.Radius);
        velocity.ShouldBeLessThan(-0.01);
        model.SetState(VoiceOrbState.Speaking, false);
        model.VelocityOf(VoiceOrbModel.Radius).ShouldBe(velocity);
    }

    [Theory]
    [InlineData(VoiceOrbState.Idle, false, 60.4)]
    [InlineData(VoiceOrbState.Listening, false, 62)]
    [InlineData(VoiceOrbState.Thinking, false, 55)]
    [InlineData(VoiceOrbState.Speaking, false, 62)]
    [InlineData(VoiceOrbState.Listening, true, 59)]
    public void Each_state_rests_at_its_measured_radius(VoiceOrbState state, bool muted, double radius)
    {
        var targets = VoiceOrbModel.TargetsOf(state, muted);
        (targets[VoiceOrbModel.Radius] * 200).ShouldBe(radius, 0.5);
    }

    [Fact]
    public void Halo_strength_per_state_follows_the_measured_ratios()
    {
        double Halo(VoiceOrbState s, bool m) => VoiceOrbModel.TargetsOf(s, m)[VoiceOrbModel.Halo];
        // Alpha 6 px outside the rim: idle .09, listening .15, thinking .16, speaking .21, muted .05.
        Halo(VoiceOrbState.Listening, false).ShouldBe(0.15 / 0.09, 0.05);
        Halo(VoiceOrbState.Thinking, false).ShouldBe(0.16 / 0.09, 0.05);
        Halo(VoiceOrbState.Speaking, false).ShouldBe(0.21 / 0.09, 0.05);
        Halo(VoiceOrbState.Listening, true).ShouldBe(0.05 / 0.09, 0.02);
    }

    [Fact]
    public void Halo_alpha_halves_every_0_04_size_and_is_gone_by_0_47()
    {
        VoiceOrb.HaloAlpha(0.30).ShouldBe(0.086, 0.01);
        VoiceOrb.HaloAlpha(0.34).ShouldBe(0.051, 0.012);
        VoiceOrb.HaloAlpha(0.38).ShouldBe(0.024, 0.008);
        VoiceOrb.HaloAlpha(0.42).ShouldBe(0.008, 0.006);
        VoiceOrb.HaloAlpha(0.47).ShouldBe(0, 0.0005);
    }

    [Fact]
    public void Thinking_band_turns_clockwise_at_160_degrees_per_second()
    {
        var model = new VoiceOrbModel();
        var start = model.BandAngle;
        Run(model, 1);
        ((model.BandAngle - start) * 180 / Math.PI).ShouldBe(160, 1);
    }

    [Fact]
    public void Ripple_rings_are_born_4px_out_and_fade_by_19px()
    {
        var model = new VoiceOrbModel();
        model.SetState(VoiceOrbState.Listening, false);
        model.Snap();
        var offsets = new List<(double Offset, double Alpha)>();
        for (var i = 0; i < 200; i++)
        {
            model.Step(1.0 / 60, 0);
            offsets.Add(model.RippleRing(0, 200));
        }
        offsets.Min(o => o.Offset).ShouldBe(4, 0.4);
        offsets.Max(o => o.Offset).ShouldBe(19, 0.3);
        // The farther out, the fainter (after the short fade-in at birth).
        var late = offsets.Where(o => o.Offset > 15).Max(o => o.Alpha);
        var early = offsets.Where(o => o.Offset is > 5 and < 8).Max(o => o.Alpha);
        late.ShouldBeLessThan(early);
        // No rings in other states.
        model.SetState(VoiceOrbState.Thinking, false);
        model.Snap();
        model.RippleRing(0, 200).Alpha.ShouldBe(0);
    }

    [Fact]
    public void A_louder_voice_swells_the_orb_and_speeds_the_ripples()
    {
        var quiet = new VoiceOrbModel();
        var loud = new VoiceOrbModel();
        foreach (var m in new[] { quiet, loud })
        {
            m.SetState(VoiceOrbState.Listening, false);
            m.Snap();
        }
        Run(quiet, 1, 0);
        Run(loud, 1, 0.9);
        (loud.MeanRadius(200) - quiet.MeanRadius(200)).ShouldBeInRange(4, 6);
        loud.RipplePhase.ShouldBeGreaterThan(quiet.RipplePhase + 0.3);
    }

    [Fact]
    public void Simulated_voice_is_deterministic_and_speech_like()
    {
        var a = new VoiceOrbSimulatedLevels(3);
        var b = new VoiceOrbSimulatedLevels(3);
        var peaks = 0;
        var previous = 0.0;
        var rising = false;
        var silent = 0;
        for (var t = 0.0; t < 10; t += 1.0 / 60)
        {
            var level = a.ReadInput(t);
            level.ShouldBe(b.ReadInput(t));
            level.ShouldBeInRange(0, 1);
            if (level < 0.05)
            {
                silent++;
            }
            if (rising && level < previous)
            {
                peaks++;
            }
            rising = level > previous;
            previous = level;
        }
        // About one syllable every 150-350 ms with breaths in between.
        peaks.ShouldBeInRange(20, 50);
        silent.ShouldBeGreaterThan(20);
        new VoiceOrbSimulatedLevels(4).ReadInput(1.3).ShouldNotBe(a.ReadInput(1.3));
    }

    [Fact]
    public void Tint2_is_derived_lighter_and_softer()
    {
        var gray = VoiceOrb.DeriveTint2(Color.FromRgb(51, 51, 51));
        gray.R.ShouldBeInRange((byte)135, (byte)165);
        var red = VoiceOrb.DeriveTint2(Colors.Red);
        // Red turns toward orange and lightens (measured: bright veins (255, 189, 91)).
        red.G.ShouldBeGreaterThan((byte)100);
        red.R.ShouldBeGreaterThan(red.B);
        var (tint, tint2) = VoiceOrb.ResolveColors(null, null, Colors.Blue, false);
        tint.ShouldBe(Colors.Blue);
        tint2.ShouldNotBe(Colors.Blue);
        VoiceOrb.ResolveColors(Colors.Green, Colors.Blue, Colors.Red, false).ShouldBe((Colors.Green, Colors.Blue));
    }

    [Fact]
    public void Outline_has_96_points_close_to_the_mean_radius()
    {
        var model = new VoiceOrbModel();
        model.SetState(VoiceOrbState.Speaking, false);
        model.Snap();
        Run(model, 0.5, 0.5);
        Span<double> radii = stackalloc double[VoiceOrbModel.Points];
        model.Outline(200, radii);
        var mean = model.MeanRadius(200);
        foreach (var r in radii)
        {
            Math.Abs(r - mean).ShouldBeLessThan(mean * 0.05);
        }
    }

    [Fact]
    public void Texture_is_opaque_and_follows_the_tints()
    {
        var model = new VoiceOrbModel();
        model.PoseStill();
        var texture = new VoiceOrbTexture();
        texture.Ensure(VoiceOrbTexture.SideFor(200));
        texture.Side.ShouldBe(72);
        var palette = new VoiceOrbTexture.Palette(Color.FromRgb(46, 46, 46), Color.FromRgb(150, 150, 150), Colors.White, Colors.White, 0.28, 0.12, 0.08);
        texture.Render(model, palette);
        var min = 255;
        var max = 0;
        foreach (var p in texture.Pixels)
        {
            // Texels outside the outline's reach are skipped (left transparent; the clip hides them).
            if (p == 0)
            {
                continue;
            }
            ((uint)p >> 24).ShouldBe(255u);
            var g = (p >> 8) & 0xFF;
            min = Math.Min(min, g);
            max = Math.Max(max, g);
        }
        // Light theme, neutral accent: darkest ≈ 47, brightest veins ≈ 145-160 (measured).
        min.ShouldBeInRange(25, 60);
        max.ShouldBeInRange(120, 200);
    }

    [AvaloniaFact]
    public void Orb_is_208_by_default_and_named_after_its_state()
    {
        var orb = new VoiceOrb();
        var window = TestHelpers.Show(orb);
        try
        {
            orb.Bounds.Width.ShouldBe(208);
            orb.Focusable.ShouldBeFalse();
            AutomationProperties.GetName(orb).ShouldBe("Voice assistant is idle");
            var peer = ControlAutomationPeer.CreatePeerForElement(orb);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Image);
            orb.State = VoiceOrbState.Listening;
            AutomationProperties.GetName(orb).ShouldBe("Listening");
            orb.IsMuted = true;
            AutomationProperties.GetName(orb).ShouldBe("Muted");
            orb.State = VoiceOrbState.Speaking;
            AutomationProperties.GetName(orb).ShouldBe("Speaking");
            orb.Label = "Assistant";
            AutomationProperties.GetName(orb).ShouldBe("Assistant");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_full_motion_the_orb_rests_on_a_still_frame()
    {
        var orb = new VoiceOrb { Size = 200, State = VoiceOrbState.Thinking };
        var window = TestHelpers.Show(orb, ArcMotionLevel.Reduced);
        try
        {
            orb.IsLooping.ShouldBeFalse();
            orb.Texture.Side.ShouldBeGreaterThan(0);
            // Still frames sit on the targets: thinking is drawn in at 0.275.
            orb.Model[VoiceOrbModel.Radius].ShouldBe(0.275, 1e-6);
            orb.Model.BandStrength.ShouldBe(1, 1e-6);
            orb.State = VoiceOrbState.Listening;
            orb.Model[VoiceOrbModel.Radius].ShouldBe(0.31, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_full_motion_the_loop_runs_and_word_pulses_swell_the_speaking_orb()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var orb = new VoiceOrb { Size = 200, State = VoiceOrbState.Speaking, OutputLevel = double.NaN };
        var window = TestHelpers.Show(orb, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(0.3), Frame);
            orb.IsLooping.ShouldBeTrue();
            orb.Model.State.ShouldBe(VoiceOrbState.Speaking);
            // Quiet output first, then a word.
            orb.OutputLevel = 0;
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            var before = orb.Model.MeanRadius(200);
            orb.OutputLevel = double.NaN;
            orb.PulseOutput(0.8);
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            // A word lifts the speaking orb 2-3.5 px within a few frames.
            (orb.Model.MeanRadius(200) - before).ShouldBeInRange(1.8, 4.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Host_levels_override_the_simulation()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var orb = new VoiceOrb { Size = 200, State = VoiceOrbState.Listening, InputLevel = 0 };
        var window = TestHelpers.Show(orb, ArcMotionLevel.Full);
        try
        {
            // The still pose shown before the loop starts carries a moderate level, which falls away within a second.
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            orb.Model.Envelope.ShouldBe(0, 0.002);
            orb.InputLevel = 1;
            clock.Advance(TimeSpan.FromSeconds(0.2), Frame);
            orb.Model.Envelope.ShouldBeGreaterThan(0.95);
            orb.IsMuted = true;
            clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
            // Muted ignores the input and goes gray.
            orb.Model.Envelope.ShouldBeLessThan(0.02);
            orb.Model.SaturationFactor.ShouldBe(0.35, 0.03);
        }
        finally
        {
            window.Close();
        }
    }
}
