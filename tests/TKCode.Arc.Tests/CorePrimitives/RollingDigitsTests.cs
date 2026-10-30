using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.Blocks.Showpieces;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Core <see cref="RollingDigits"/>: the wheel options Arc's number field uses.</summary>
public class RollingDigitsTests
{
    [AvaloniaFact]
    public void Wheels_Follow_The_Value_Unless_The_Short_Way_Is_Asked_For()
    {
        var follow = new RollingDigits { Value = 9, UseGrouping = false };
        var shortest = new RollingDigits { Value = 9, UseGrouping = false, IsShortestTurn = true };
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { follow, shortest } }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            // 9 → 2: following the value turns the ones wheel 7 steps back; the short way is 3 steps on (9, 0, 1, 2).
            follow.Value = 2;
            shortest.Value = 2;
            follow.WheelTargets[^1].ShouldBe(2);
            shortest.WheelTargets[^1].ShouldBe(12);
            // 2 → 9: 7 steps on, or 3 steps back.
            follow.Value = 9;
            shortest.Value = 9;
            follow.WheelTargets[^1].ShouldBe(9);
            shortest.WheelTargets[^1].ShouldBe(9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Instant_Changes_Jump_Without_Rolling()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var digits = new RollingDigits { Value = 1, IsInstant = true };
        var window = TestHelpers.Show(digits, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var requests = clock.RequestCount;
            digits.Value = 4;
            digits.WheelTargets[^1].ShouldBe(4);
            digits.DisplayText.ShouldBe("4");
            clock.RequestCount.ShouldBe(requests);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Changes 1 → 3 under the frame clock and returns where the ones wheel is after each 1/60 s frame (frame n sits n/60 s
    /// into the motion, give or take the clock's tick rounding).
    /// </summary>
    private static List<double> SampleOnesWheel(RollingDigits digits, ArcMotionLevel level, int frames, out int requestsDuringChange)
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        digits.Value = 1;
        digits.UseGrouping = false;
        var window = TestHelpers.Show(digits, level);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var before = clock.RequestCount;
            digits.Value = 3;
            requestsDuringChange = clock.RequestCount - before;
            var samples = new List<double> { digits.WheelPositions[^1] };
            for (var i = 0; i < frames; i++)
            {
                clock.Advance(TimeSpan.FromSeconds(1.0 / 60.0));
                samples.Add(digits.WheelPositions[^1]);
            }
            return samples;
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wheel_Spring_Still_Drives_Changes_When_No_Wheel_Motion_Is_Set()
    {
        // Source and behavior compatible: the default is null and the wheel follows WheelSpring (smooth by default).
        new RollingDigits().WheelMotion.ShouldBeNull();
        new RollingDigits().WheelSpring.ShouldBe(ArcSpring.Smooth);
        foreach (var preset in new[] { ArcSpring.Smooth, ArcSpring.Quick })
        {
            var samples = SampleOnesWheel(new RollingDigits { WheelSpring = preset }, ArcMotionLevel.Full, 30, out _);
            samples[0].ShouldBe(1);
            for (var n = 1; n <= 30; n++)
            {
                var (x, _) = SpringSolver.Evaluate(ArcMotionPresets.Get(preset), 1 - 3, 0, n / 60.0);
                samples[n].ShouldBe(3 + x, 1e-4, $"{preset} frame {n}");
            }
        }
    }

    [AvaloniaFact]
    public void Wheel_Motion_Takes_Any_Spring()
    {
        var spring = SpringSpec.FromVisual(0.28, 0);
        var samples = SampleOnesWheel(new RollingDigits { WheelSpring = ArcSpring.Quick, WheelMotion = MotionSpec.Spring(spring) },
            ArcMotionLevel.Full, 30, out _);
        for (var n = 1; n <= 30; n++)
        {
            var (x, _) = SpringSolver.Evaluate(spring, -2, 0, n / 60.0);
            samples[n].ShouldBe(3 + x, 1e-4, $"frame {n}");
        }
        // It is not the preset it replaces.
        var (quick, _) = SpringSolver.Evaluate(ArcMotionPresets.Quick, -2, 0, 6 / 60.0);
        Math.Abs(samples[6] - (3 + quick)).ShouldBeGreaterThan(0.01);
    }

    [AvaloniaFact]
    public void Wheel_Motion_Takes_A_Timed_Ease_And_Lands_On_Time()
    {
        var easing = new Avalonia.Animation.Easings.CubicEaseOut();
        var duration = TimeSpan.FromMilliseconds(240);
        var samples = SampleOnesWheel(new RollingDigits { WheelMotion = MotionSpec.Tween(duration, easing) }, ArcMotionLevel.Full, 20,
            out _);
        // 240ms is 14.4 frames: every frame before it follows the curve, the 15th lands on the digit and stays.
        for (var n = 1; n <= 14; n++)
        {
            samples[n].ShouldBe(1 + 2 * easing.Ease(n / 60.0 / duration.TotalSeconds), 1e-4, $"frame {n}");
        }
        samples[15].ShouldBe(3);
        samples[20].ShouldBe(3);
    }

    [AvaloniaFact]
    public void Wheel_Motion_Still_Jumps_Under_Reduced_And_Off_Motion()
    {
        // MotionValue runs tweens under reduced motion; the wheel must still jump, as it does with springs.
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var tween = MotionSpec.Tween(TimeSpan.FromMilliseconds(600), ArcEasings.Standard);
            var samples = SampleOnesWheel(new RollingDigits { WheelMotion = tween }, level, 2, out var requests);
            samples.ShouldAllBe(p => p == 3, level.ToString());
            requests.ShouldBe(0, level.ToString());
        }
    }

    [AvaloniaFact]
    public void Wheel_Motion_Is_Ignored_By_Instant_Changes_And_The_First_Reveal()
    {
        var slow = MotionSpec.Tween(TimeSpan.FromSeconds(1), ArcEasings.Standard);
        var instant = SampleOnesWheel(new RollingDigits { IsInstant = true, WheelMotion = slow }, ArcMotionLevel.Full, 1, out var requests);
        instant.ShouldAllBe(p => p == 3);
        requests.ShouldBe(0);

        // The reveal keeps its own staggered spring (visual duration 0.48s) whatever the wheel motion is.
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var digits = new RollingDigits { IsArmed = true, Value = 7, WheelMotion = slow };
        var window = TestHelpers.Show(digits, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            digits.IsArmed = false;
            for (var i = 0; i < 6; i++)
            {
                clock.Advance(TimeSpan.FromSeconds(1.0 / 60.0));
            }
            var (x, _) = SpringSolver.Evaluate(SpringSpec.FromVisual(0.48, 0), -7, 0, 6 / 60.0);
            digits.WheelPositions[^1].ShouldBe(7 + x, 1e-4);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Falloff_None_Keeps_Glyphs_Solid_And_Only_Clips()
    {
        // The default does not change.
        new RollingDigits().Falloff.ShouldBe(RollingDigitsFalloff.Linear);
        ((int)RollingDigitsFalloff.None).ShouldBe(2);
        RollingDigits.GlyphOpacity(RollingDigitsFalloff.None, 0).ShouldBe(1);
        RollingDigits.GlyphOpacity(RollingDigitsFalloff.None, 0.6).ShouldBe(1);
        RollingDigits.GlyphOpacity(RollingDigitsFalloff.Linear, 0.6).ShouldBe(0.4, 1e-12);
        RollingDigits.GlyphOpacity(RollingDigitsFalloff.Eased, 0.5).ShouldBe(1 - 1.1 * Math.Pow(0.5, 1.5), 1e-12);
        // Without fade a glyph is visible as far as the line box reaches: a 1.2em line with a 1em pitch shows 1.2 steps.
        RollingDigits.GlyphReach(RollingDigitsFalloff.None, 24, 20).ShouldBe(1.2, 1e-12);
        RollingDigits.GlyphReach(RollingDigitsFalloff.None, 24, 30).ShouldBe(1);
        RollingDigits.GlyphReach(RollingDigitsFalloff.Linear, 24, 20).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Falloff_None_Rolls_And_Renders()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var digits = new RollingDigits { Value = 9, UseGrouping = false, Falloff = RollingDigitsFalloff.None, GlyphPitch = 1.2 };
        var window = TestHelpers.Show(digits, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            digits.Value = 0;
            // A 0–9 strip: 9 → 0 rolls back through 8…1.
            digits.WheelTargets[^1].ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1));
            digits.DisplayText.ShouldBe("0");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Glyphs_Are_Drawn_From_Cached_Layouts_In_The_Foreground()
    {
        // Prefix, separators and suffix are the glyphs a FormattedText of each drew, in Foreground; a new Foreground
        // redraws the same glyph runs; a steady frame allocates nothing (drawing a FormattedText laid it out again).
        var digits = new RollingDigits { Value = 1234.5, Decimals = 1, Prefix = "$", Suffix = " MRR", FontSize = 20, Foreground = Brushes.Teal };
        var window = TestHelpers.Show(digits);
        try
        {
            var typeface = new Typeface(digits.FontFamily, FontStyle.Normal, digits.FontWeight);
            FormattedText Formatted(string text) =>
                new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 20, Brushes.Black);
            var drawn = ShowpieceTextRecording.Record(dc => digits.Render(dc));
            drawn.ShouldAllBe(r => ReferenceEquals(r.Brush, Brushes.Teal));
            var glyphs = ShowpieceTextRecording.Glyphs(dc => digits.Render(dc)).Select(g => g[(g.IndexOf(' ') + 1)..]).ToList();
            foreach (var literal in new[] { "$", ",", ".", " MRR" })
            {
                var expected = ShowpieceTextRecording.Glyphs(dc => dc.DrawText(Formatted(literal), default)).Single();
                glyphs.ShouldContain(expected[(expected.IndexOf(' ') + 1)..], literal);
            }
            // The prefix sits at the left edge, where DrawText put it.
            drawn[0].Transform.M31.ShouldBe(ShowpieceTextRecording.Record(dc => dc.DrawText(Formatted("$"), default))[0].Transform.M31, 1e-9);

            digits.Foreground = Brushes.Crimson;
            var recolored = ShowpieceTextRecording.Record(dc => digits.Render(dc));
            recolored.Select(r => r.Run).ShouldBe(drawn.Select(r => r.Run));
            recolored.ShouldAllBe(r => ReferenceEquals(r.Brush, Brushes.Crimson));

            using var bitmap = new RenderTargetBitmap(new PixelSize(300, 60));
            using var context = bitmap.CreateDrawingContext();
            for (var i = 0; i < 3; i++)
            {
                digits.Render(context);
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 50; i++)
            {
                digits.Render(context);
            }
            ((GC.GetAllocatedBytesForCurrentThread() - before) / 50.0).ShouldBeLessThan(256);
        }
        finally
        {
            window.Close();
        }
    }
}
