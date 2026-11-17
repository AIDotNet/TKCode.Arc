using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.AudioVideo;

/// <summary>The now playing player's pure logic against the measured numbers: layouts, formats, pull, springs, peaks, clock, level bars.</summary>
public class NowPlayingMathTests
{
    [Fact]
    public void Layout_at_336_matches_the_measured_mini_and_full_rects()
    {
        var l = NowPlayingLayout.Compute(336, false);
        l.MiniHeight.ShouldBe(64);
        l.FullHeight.ShouldBe(410.8, 1e-9);
        // Inner coordinates (inside the 1px border): the measured shell rects minus one.
        l.MiniArt.ShouldBe(new Rect(10, 10, 44, 44));
        l.MiniMain.Width.ShouldBe(133, 1e-9);
        l.MiniMain.X.ShouldBe(64);
        l.MiniLevels.Width.ShouldBe(13);
        l.MiniLevels.Height.ShouldBe(14);
        l.MiniPlay.ShouldBe(new Rect(234, 12, 40, 40));
        l.MiniNext.ShouldBe(new Rect(284, 12, 40, 40));
        l.MiniProgress.ShouldBe(new Rect(18, 62, 298, 2));
        l.FullArt.ShouldBe(new Rect(12, 11, 310, 192));
        l.Collapse.ShouldBe(new Rect(278, 21, 34, 34));
        l.Info.ShouldBe(new Rect(16, 215, 302, 46));
        l.Wave.Width.ShouldBe(302);
        l.Wave.Height.ShouldBe(36);
        l.Previous.X.ShouldBe(69, 1e-9);
        l.FullPlay.X.ShouldBe(141, 1e-9);
        l.FullPlay.Y.ShouldBe(343.8, 1e-9);
        l.FullPlay.Width.ShouldBe(52);
        l.FullNext.X.ShouldBe(221, 1e-9);
        l.FullNext.Y.ShouldBe(347.8, 1e-9);
    }

    [Fact]
    public void Compact_mini_bar_drops_next_and_narrow_shells_shrink_the_artwork_only()
    {
        var compact = NowPlayingLayout.Compute(312, true);
        compact.MiniNext.ShouldBe(default);
        compact.MiniPlay.X.ShouldBe(310 - 10 - 40);
        compact.FullNext.Width.ShouldBe(44);

        var narrow = NowPlayingLayout.Compute(272, true);
        narrow.FullArt.Width.ShouldBe(246);
        narrow.FullArt.Height.ShouldBe(192);
        narrow.FullHeight.ShouldBe(410.8, 1e-9);
        NowPlayingLayout.ShellWidthFor(500).ShouldBe(336);
        NowPlayingLayout.ShellWidthFor(290).ShouldBe(290);
        NowPlayingLayout.ShellWidthFor(double.PositiveInfinity).ShouldBe(336);
    }

    [Fact]
    public void Progress_radius_and_interpolation_follow_the_shell_height()
    {
        var l = NowPlayingLayout.Compute(336, false);
        l.Progress(64).ShouldBe(0);
        l.Progress(410.8).ShouldBe(1, 1e-9);
        NowPlayingLayout.RadiusAt(0).ShouldBe(22);
        NowPlayingLayout.RadiusAt(1).ShouldBe(30);
        var half = NowPlayingLayout.Lerp(l.MiniArt, l.FullArt, 0.5);
        half.Width.ShouldBe(177);
        half.Height.ShouldBe(118);
    }

    [Fact]
    public void Times_use_m_ss_and_a_minus_sign_for_the_remaining_time()
    {
        NowPlayingFormat.Clock(0).ShouldBe("0:00");
        NowPlayingFormat.Clock(62).ShouldBe("1:02");
        NowPlayingFormat.Clock(176).ShouldBe("2:56");
        NowPlayingFormat.Clock(3665).ShouldBe("61:05");
        NowPlayingFormat.Elapsed(62.97).ShouldBe("1:02");
        NowPlayingFormat.Remaining(0, 214, "−").ShouldBe("−3:34");
        NowPlayingFormat.Remaining(29.9, 214, "−").ShouldBe("−3:05");
        NowPlayingFormat.Remaining(214, 214, "−").ShouldBe("−0:00");
        NowPlayingFormat.ValueText(0, 214, "{0} of {1}").ShouldBe("0:00 of 3:34");
        NowPlayingFormat.ValueText(67.5, 231, "{0} of {1}").ShouldBe("1:07 of 3:51");
        NowPlayingFormat.ValueText(187, 188, "{0} of {1}").ShouldBe("3:07 of 3:08");
    }

    [Fact]
    public void Pull_follows_at_0_6_down_and_rubber_bands_up()
    {
        NowPlayingPull.Offset(2).ShouldBe(1.2, 1e-9);
        NowPlayingPull.Offset(130).ShouldBe(78, 1e-9);
        NowPlayingPull.Offset(-10).ShouldBe(-1.98, 0.005);
        NowPlayingPull.Offset(-50).ShouldBe(-9.52, 0.005);
        NowPlayingPull.Offset(-100).ShouldBe(-18.18, 0.005);
        NowPlayingPull.Offset(-200).ShouldBe(-33.33, 0.005);
        NowPlayingPull.Offset(-250).ShouldBe(-40, 0.005);
    }

    [Fact]
    public void Release_closes_past_70_or_on_a_downward_flick()
    {
        NowPlayingPull.ShouldClose(NowPlayingPull.Offset(116), 0).ShouldBeFalse();
        NowPlayingPull.ShouldClose(NowPlayingPull.Offset(118), 0).ShouldBeTrue();
        NowPlayingPull.ShouldClose(NowPlayingPull.Offset(14), 580).ShouldBeFalse();
        NowPlayingPull.ShouldClose(NowPlayingPull.Offset(16), 700).ShouldBeTrue();
        // An upward pull never closes, however fast.
        NowPlayingPull.ShouldClose(NowPlayingPull.Offset(-40), 900).ShouldBeFalse();
    }

    [Fact]
    public void Springs_are_the_fitted_constants()
    {
        NowPlayingMotion.Expand.NaturalFrequency.ShouldBe(11.38, 0.05);
        NowPlayingMotion.Expand.DampingRatio.ShouldBe(0.9, 1e-9);
        NowPlayingMotion.Collapse.NaturalFrequency.ShouldBe(11.38, 0.05);
        NowPlayingMotion.Collapse.DampingRatio.ShouldBe(1, 1e-9);
        NowPlayingMotion.Firm.NaturalFrequency.ShouldBe(20, 1e-9);
        NowPlayingMotion.Firm.DampingRatio.ShouldBe(1, 1e-9);
        NowPlayingPull.ReturnSpring.DampingRatio.ShouldBe(0.75, 1e-9);
        NowPlayingPull.ReturnSpring.NaturalFrequency.ShouldBe(13.09, 0.05);
    }

    [Fact]
    public void Peaks_have_the_measured_shape_and_are_stable_per_id()
    {
        var a = NowPlayingPeaks.Generate("low-orbit");
        a.Length.ShouldBe(54);
        NowPlayingPeaks.Generate("low-orbit").ShouldBe(a);
        NowPlayingPeaks.Generate("riverglass").ShouldNotBe(a);
        foreach (var id in new[] { "low-orbit", "saltwind-road", "paper-lanterns", "steel-and-fern", "riverglass" })
        {
            var p = NowPlayingPeaks.Generate(id);
            p.Min().ShouldBeGreaterThanOrEqualTo(0.16);
            p.Max().ShouldBeLessThanOrEqualTo(0.98);
            p.Average().ShouldBeInRange(0.42, 0.66);
            var ends = (p.Take(5).Average() + p.TakeLast(5).Average()) / 2;
            var middle = p.Skip(20).Take(14).Average();
            ends.ShouldBeLessThan(0.4);
            middle.ShouldBeGreaterThan(0.55);
        }
    }

    [Fact]
    public void Peaks_of_another_length_are_resampled()
    {
        var r = NowPlayingPeaks.Resample([0, 1], 5);
        r.ShouldBe([0, 0.25, 0.5, 0.75, 1]);
        NowPlayingPeaks.Resample([2, -1], 2).ShouldBe([1, 0]);
    }

    [Fact]
    public void Clock_moves_reports_the_end_and_ignores_corrections_while_frozen()
    {
        var clock = new NowPlayingClock();
        clock.Load(10);
        clock.Advance(4).ShouldBeFalse();
        clock.Position.ShouldBe(4);
        clock.Correct(7).ShouldBeTrue();
        clock.Position.ShouldBe(7);
        clock.IsFrozen = true;
        clock.Advance(1).ShouldBeFalse();
        clock.Correct(2).ShouldBeFalse();
        clock.Position.ShouldBe(7);
        clock.IsFrozen = false;
        clock.Advance(5).ShouldBeTrue();
        clock.Position.ShouldBe(10);
        clock.Seek(-3);
        clock.Position.ShouldBe(0);
    }

    [Fact]
    public void Level_bars_swing_between_a_quarter_and_full_height_back_and_forth()
    {
        NowPlayingLevels.DanceScale(0, 0).ShouldBe(0.25, 1e-9);
        NowPlayingLevels.DanceScale(0, 0.9).ShouldBe(1, 1e-9);
        NowPlayingLevels.DanceScale(0, 1.8).ShouldBe(0.25, 1e-9);
        NowPlayingLevels.DanceScale(0, 0.45).ShouldBe(0.625, 1e-6);
        // Bar two starts 300ms into its 640ms sweep, bar three 500ms into its 1100ms one.
        NowPlayingLevels.DanceScale(1, 0.34).ShouldBe(1, 1e-9);
        NowPlayingLevels.DanceScale(2, 0.6).ShouldBe(1, 1e-9);
        for (var t = 0.0; t < 3; t += 0.05)
        {
            for (var i = 0; i < 3; i++)
            {
                NowPlayingLevels.DanceScale(i, t).ShouldBeInRange(0.25, 1.0);
            }
        }
    }
}
