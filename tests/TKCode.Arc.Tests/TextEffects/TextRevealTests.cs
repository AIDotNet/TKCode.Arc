using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.TextEffects;

public class TextRevealTests
{
    [Fact]
    public void Reveal_Stagger_Stays_Under_The_Considered_Duration()
    {
        TextReveal.StepFor(5).ShouldBe(0.04, 1e-9);
        TextReveal.StepFor(24).ShouldBe(0.02, 1e-9);
        var delays = TextReveal.ComputeDelays("one two\nthree", 0.1);
        delays.Length.ShouldBe(3);
        delays[0].ShouldBe(0.1, 1e-9);
        delays[1].ShouldBe(0.14, 1e-9);
        // Position 2 plus one forced line: 0.1 + 2 × 0.04 + 0.04.
        delays[2].ShouldBe(0.22, 1e-9);
    }

    [Fact]
    public void Balanced_Wrapping_Evens_Out_Lines()
    {
        double[] words = [50, 50, 50, 50];
        TextEffectsWordLayout.CountLines(words, 5, 210).ShouldBe(2);
        var balanced = TextEffectsWordLayout.BalanceWidths(words, 5, 210);
        balanced.ShouldBeLessThan(160);
        TextEffectsWordLayout.CountLines(words, 5, balanced).ShouldBe(2);
        TextEffectsWordLayout.BalanceWidths(words, 5, 1000).ShouldBe(1000);
    }

    [AvaloniaFact]
    public void Text_Reveal_Shows_At_Rest_Without_Motion()
    {
        var reveal = new TextReveal { Text = "Interfaces that feel considered from the first frame" };
        var window = TestHelpers.Show(reveal);
        try
        {
            reveal.IsRevealing.ShouldBeFalse();
            reveal.IsWaiting.ShouldBeFalse();
            AutomationProperties.GetName(reveal).ShouldBe("Interfaces that feel considered from the first frame");
            AutomationProperties.GetHeadingLevel(reveal).ShouldBe(2);
            reveal.Bounds.Height.ShouldBeGreaterThan(reveal.FontSize * 1.1);
            var ch = new Avalonia.Media.FormattedText("0", System.Globalization.CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight,
                new Avalonia.Media.Typeface(reveal.FontFamily, Avalonia.Media.FontStyle.Normal, reveal.FontWeight), reveal.FontSize, null).WidthIncludingTrailingWhitespace;
            reveal.Bounds.Width.ShouldBeLessThanOrEqualTo(24 * ch + 1);

            reveal.Text = "Line one\nLine two";
            reveal.Level = TextLevel.Paragraph;
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(reveal).ShouldBe("Line one Line two");
            AutomationProperties.GetHeadingLevel(reveal).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Reveal_Plays_Once_With_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var reveal = new TextReveal { Text = "A quieter way to ship", Level = TextLevel.H3 };
        var window = TestHelpers.Show(reveal, ArcMotionLevel.Full);
        try
        {
            reveal.IsRevealing.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            reveal.IsRevealing.ShouldBeFalse();
            reveal.Replay();
            reveal.IsRevealing.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
