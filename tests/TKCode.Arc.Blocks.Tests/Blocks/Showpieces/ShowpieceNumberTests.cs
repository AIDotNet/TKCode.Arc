using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="ShowpieceNumber"/>: formats, per-character stagger, place-value columns, screen reader text.</summary>
public class ShowpieceNumberTests
{
    private static List<double> Digits(ShowpieceNumber number) => number.Wheels.Select(w => w.Value).ToList();

    [AvaloniaFact]
    public void Formats_Roll_Digits_Only()
    {
        var number = new ShowpieceNumber { Value = 6_050_000, Format = ShowpieceNumberFormat.CompactCurrency };
        var window = TestHelpers.Show(number);
        try
        {
            number.DisplayText.ShouldBe("$6.05M");
            Digits(number).ShouldBe(new[] { 6.0, 0, 5 });
            AutomationProperties.GetName(number).ShouldBe("$6.05M");
            number.Bounds.Width.ShouldBeGreaterThan(0);

            number.Format = ShowpieceNumberFormat.Percent;
            number.Decimals = 1;
            number.IsSignShown = true;
            number.Value = 0.086;
            number.DisplayText.ShouldBe("+8.6%");
            Digits(number).ShouldBe(new[] { 8.0, 6 });

            number.Format = ShowpieceNumberFormat.Number;
            number.Decimals = 0;
            number.IsSignShown = false;
            number.Suffix = " accounts";
            number.Value = 1687;
            number.DisplayText.ShouldBe("1,687 accounts");

            number.Suffix = null;
            number.Formatter = v => $"Sep {v:0}";
            number.Value = 23;
            number.DisplayText.ShouldBe("Sep 23");
            Digits(number).ShouldBe(new[] { 2.0, 3 });
            // Wheels default to Arc's strips: no fade, one line per glyph.
            number.Wheels[0].Falloff.ShouldBe(RollingDigitsFalloff.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changes_Stagger_26ms_Per_Character()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var number = new ShowpieceNumber { Value = 1234 };
        var window = TestHelpers.Show(number, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            number.DisplayText.ShouldBe("1,234");
            number.Value = 5678;
            // The screen reader text is final at once.
            number.DisplayText.ShouldBe("5,678");
            // "5,678": the 5 (index 0) starts now; 6, 7, 8 (indices 2, 3, 4) wait 52, 78, 104ms.
            Digits(number).ShouldBe(new[] { 5.0, 2, 3, 4 });
            number.PendingCount.ShouldBe(3);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            Digits(number).ShouldBe(new[] { 5.0, 6, 3, 4 });
            clock.Advance(TimeSpan.FromMilliseconds(20));
            Digits(number).ShouldBe(new[] { 5.0, 6, 7, 4 });
            clock.Advance(TimeSpan.FromMilliseconds(30));
            Digits(number).ShouldBe(new[] { 5.0, 6, 7, 8 });
            number.PendingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Applies_Every_Digit_At_Once()
    {
        var number = new ShowpieceNumber { Value = 1234 };
        var window = TestHelpers.Show(number, ArcMotionLevel.Reduced);
        try
        {
            number.Value = 5678;
            Digits(number).ShouldBe(new[] { 5.0, 6, 7, 8 });
            number.PendingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Columns_Are_Keyed_By_Place_Value_And_Units_Swap()
    {
        var number = new ShowpieceNumber { Value = 999 };
        var window = TestHelpers.Show(number);
        try
        {
            var ones = number.Wheels[^1];
            number.Value = 1000;
            number.DisplayText.ShouldBe("1,000");
            number.Wheels.Count.ShouldBe(4);
            number.Wheels[^1].ShouldBeSameAs(ones);
            Digits(number).ShouldBe(new[] { 1.0, 0, 0, 0 });

            number.Format = ShowpieceNumberFormat.CompactCurrency;
            number.Value = 870_000;
            number.DisplayText.ShouldBe("$870K");
            number.Value = 1_200_000;
            number.DisplayText.ShouldBe("$1.20M");
            Digits(number).ShouldBe(new[] { 1.0, 2, 0 });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Read_The_Final_Text_Only()
    {
        var number = new ShowpieceNumber { Value = 2_000_000, Format = ShowpieceNumberFormat.CompactCurrency };
        var window = TestHelpers.Show(number);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(number);
            peer.GetName().ShouldBe("$2.00M");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
            peer.GetChildren().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Digits_And_Symbols_Draw_The_Glyphs_FormattedText_Drew()
    {
        var number = new ShowpieceNumber
        {
            Value = 2_512_340, Format = ShowpieceNumberFormat.Currency, FontSize = 24, Foreground = Brushes.Teal
        };
        var window = TestHelpers.Show(number);
        try
        {
            var typeface = new Typeface(number.FontFamily, FontStyle.Normal, number.FontWeight);
            // The symbol and separators, "$", "," and ",", in the foreground, glyph for glyph what a FormattedText of each drew.
            var symbols = ShowpieceTextRecording.Record(dc => number.Render(dc));
            symbols.ShouldNotBeEmpty();
            symbols.ShouldAllBe(r => ReferenceEquals(r.Brush, Brushes.Teal));
            var expected = new[] { "$", ",", "," }
                .SelectMany(t => GlyphsOnly(dc => dc.DrawText(Formatted(t, typeface, 24, CultureInfo.CurrentCulture), default)))
                .ToList();
            GlyphsOnly(dc => number.Render(dc)).ShouldBe(expected);

            // Each wheel at rest: its digit on the reading line and the neighbors its line box shows, centered in the
            // tabular cell, exactly where the FormattedText glyphs were drawn.
            number.Wheels.Count.ShouldBe(7);
            foreach (var wheel in number.Wheels)
            {
                var digits = Enumerable.Range(0, 10)
                    .Select(d => Formatted(d.ToString(CultureInfo.InvariantCulture), typeface, 24, CultureInfo.InvariantCulture)).ToArray();
                var cell = digits.Max(g => g.WidthIncludingTrailingWhitespace);
                var pitch = wheel.FontSize * wheel.GlyphPitch;
                var reach = RollingDigits.GlyphReach(wheel.Falloff, digits[0].Height, pitch);
                var reference = ShowpieceTextRecording.Glyphs(dc =>
                {
                    for (var d = 0; d < 10; d++)
                    {
                        var offset = ((((d - wheel.Value) % 10) + 15) % 10) - 5;
                        if (Math.Abs(offset) < reach)
                        {
                            dc.DrawText(digits[d], new Point((cell - digits[d].WidthIncludingTrailingWhitespace) / 2, offset * pitch));
                        }
                    }
                });
                var drawn = ShowpieceTextRecording.Record(dc => wheel.Render(dc));
                drawn.ShouldAllBe(r => ReferenceEquals(r.Brush, Brushes.Teal));
                ShowpieceTextRecording.Glyphs(dc => wheel.Render(dc)).ShouldBe(reference);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Foreground_Redraws_Without_Laying_Out_Again()
    {
        var number = new ShowpieceNumber { Value = 1687, Suffix = " accounts", Foreground = Brushes.Black };
        var window = TestHelpers.Show(number);
        try
        {
            var wheel = number.Wheels[0];
            var symbols = ShowpieceTextRecording.Record(dc => number.Render(dc));
            var digits = ShowpieceTextRecording.Record(dc => wheel.Render(dc));
            number.Foreground = Brushes.Crimson;
            var symbolsAfter = ShowpieceTextRecording.Record(dc => number.Render(dc));
            var digitsAfter = ShowpieceTextRecording.Record(dc => wheel.Render(dc));
            symbolsAfter.Select(r => r.Run).ShouldBe(symbols.Select(r => r.Run));
            digitsAfter.Select(r => r.Run).ShouldBe(digits.Select(r => r.Run));
            symbolsAfter.ShouldAllBe(r => ReferenceEquals(r.Brush, Brushes.Crimson));
            digitsAfter.ShouldAllBe(r => ReferenceEquals(r.Brush, Brushes.Crimson));
            // A font change lays the glyphs out again.
            number.FontSize = 20;
            Dispatcher.UIThread.RunJobs();
            ShowpieceTextRecording.Record(dc => wheel.Render(dc)).Select(r => r.Run).ShouldNotBe(digits.Select(r => r.Run));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Steady_Frame_Of_A_Number_Allocates_Nothing()
    {
        var number = new ShowpieceNumber { Value = 2_512_340, Format = ShowpieceNumberFormat.Currency, Suffix = " MRR" };
        var window = TestHelpers.Show(number);
        try
        {
            var visuals = new List<Visual> { number };
            visuals.AddRange(number.Wheels);
            using var bitmap = new RenderTargetBitmap(new PixelSize(400, 100));
            using var context = bitmap.CreateDrawingContext();
            for (var frame = 0; frame < 5; frame++)
            {
                foreach (var visual in visuals)
                {
                    visual.Render(context);
                }
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < 50; frame++)
            {
                foreach (var visual in visuals)
                {
                    visual.Render(context);
                }
            }
            // Drawing a FormattedText laid its glyph out again every time (about 1–2 KB per glyph per frame).
            ((GC.GetAllocatedBytesForCurrentThread() - before) / 50.0).ShouldBeLessThan(256);
        }
        finally
        {
            window.Close();
        }
    }

    private static FormattedText Formatted(string text, Typeface typeface, double size, CultureInfo culture) =>
        new(text, culture, FlowDirection.LeftToRight, typeface, size, Brushes.Black);

    /// <summary>Glyph runs without their position (glyph indices and advances).</summary>
    private static List<string> GlyphsOnly(Action<DrawingContext> draw) =>
        ShowpieceTextRecording.Glyphs(draw).Select(g => g[(g.IndexOf(' ') + 1)..]).ToList();
}
