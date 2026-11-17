using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiMetricLine"/>: the time format, placeholders, the timing color, the rolling speed and cost (reserved width,
/// four decimals, first value without a roll), the layout of items, dots and wrapping, and what screen readers hear.
/// </summary>
public class AiMetricLineTests
{
    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    private static T Part<T>(AiMetricLine line, string name) where T : Control => TestHelpers.Descendant<T>(line, name);

    [AvaloniaFact]
    public void Time_Has_Two_Decimals_Under_10s_One_Under_100s_Then_Minutes()
    {
        AiMetricLine.FormatTime(TimeSpan.Zero).ShouldBe("0.00s");
        AiMetricLine.FormatTime(TimeSpan.FromMilliseconds(80)).ShouldBe("0.08s");
        AiMetricLine.FormatTime(TimeSpan.FromMilliseconds(1834)).ShouldBe("1.83s");
        AiMetricLine.FormatTime(TimeSpan.FromMilliseconds(1835)).ShouldBe("1.84s");
        AiMetricLine.FormatTime(TimeSpan.FromMilliseconds(9994)).ShouldBe("9.99s");
        // Rounding up to the limit moves to the next form.
        AiMetricLine.FormatTime(TimeSpan.FromMilliseconds(9996)).ShouldBe("10.0s");
        AiMetricLine.FormatTime(TimeSpan.FromMilliseconds(12440)).ShouldBe("12.4s");
        AiMetricLine.FormatTime(TimeSpan.FromMilliseconds(99940)).ShouldBe("99.9s");
        AiMetricLine.FormatTime(TimeSpan.FromMilliseconds(99960)).ShouldBe("1:39");
        AiMetricLine.FormatTime(TimeSpan.FromSeconds(102.7)).ShouldBe("1:42");
        AiMetricLine.FormatTime(TimeSpan.FromSeconds(3725)).ShouldBe("62:05");
        AiMetricLine.FormatTime(TimeSpan.FromSeconds(-1)).ShouldBe("0.00s");
    }

    [AvaloniaFact]
    public void Speed_Is_A_Whole_Number_And_Cost_Has_Four_Decimals()
    {
        AiMetricLine.SpeedValue(152.4).ShouldBe(152);
        AiMetricLine.SpeedValue(152.5).ShouldBe(153);
        AiMetricLine.SpeedValue(null).ShouldBeNull();
        AiMetricLine.SpeedValue(double.NaN).ShouldBeNull();
        AiMetricLine.SpeedValue(double.PositiveInfinity).ShouldBeNull();
        AiMetricLine.SpeedValue(-3).ShouldBeNull();
        AiMetricLine.FormatCost(0.0041m).ShouldBe("$0.0041");
        AiMetricLine.FormatCost(0m).ShouldBe("$0.0000");
        AiMetricLine.FormatCost(1234.56789m).ShouldBe("$1,234.5679");
        AiMetricLine.FormatCost(null).ShouldBeNull();
    }

    [AvaloniaFact]
    public void Line_Is_12px_Muted_With_Values_In_Foreground()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Metrics(TimeSpan.FromMilliseconds(1830), speed: 152, cost: 0.0041m));
        var line = (AiMetricLine)rig.Content;
        line.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(line.Foreground).ShouldBe(Token(line, ArcDesignTokenKind.TextMuted));
        Part<TextBlock>(line, "PART_FirstTokenLabel").Text.ShouldBe("First token");
        var time = Part<TextBlock>(line, "PART_FirstTokenValue");
        time.Text.ShouldBe("1.83s");
        time.FontFeatures.ShouldNotBeNull().ShouldContain(f => f.Tag == "tnum");
        TestHelpers.ColorOf(time.Foreground).ShouldBe(Token(line, ArcDesignTokenKind.Foreground));
        var speed = Part<RollingDigits>(line, AiMetricLine.SpeedPart);
        speed.IsVisible.ShouldBeTrue();
        speed.DisplayText.ShouldBe("152");
        TestHelpers.ColorOf(speed.Foreground).ShouldBe(Token(line, ArcDesignTokenKind.Foreground));
        speed.Falloff.ShouldBe(RollingDigitsFalloff.None);
        Part<TextBlock>(line, "PART_SpeedUnit").Text.ShouldBe("tok/s");
        TestHelpers.ColorOf(Part<TextBlock>(line, "PART_SpeedUnit").Foreground).ShouldBe(Token(line, ArcDesignTokenKind.TextMuted));
        var cost = Part<RollingDigits>(line, AiMetricLine.CostPart);
        cost.DisplayText.ShouldBe("$0.0041");
        cost.Decimals.ShouldBe(AiMetricLine.CostDecimals);
        TestHelpers.ColorOf(cost.Foreground).ShouldBe(Token(line, ArcDesignTokenKind.Foreground));
        Part<TextBlock>(line, "PART_SpeedPlaceholder").IsVisible.ShouldBeFalse();
        Part<TextBlock>(line, "PART_CostPlaceholder").IsVisible.ShouldBeFalse();
        foreach (var dot in new[] { "PART_SpeedDot", "PART_CostDot" })
        {
            Part<TextBlock>(line, dot).Text.ShouldBe("·");
            TestHelpers.ColorOf(Part<TextBlock>(line, dot).Foreground).ShouldBe(Token(line, ArcDesignTokenKind.TextMuted));
        }
    }

    [AvaloniaFact]
    public void Items_Are_8px_Apart_With_A_Dot_8px_Before_Each_Value()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Metrics(TimeSpan.FromMilliseconds(1830), speed: 152, cost: 0.0041m));
        var line = (AiMetricLine)rig.Content;
        double X(Control part) => part.TranslatePoint(default, line)!.Value.X;
        var label = Part<TextBlock>(line, "PART_FirstTokenLabel");
        var time = Part<TextBlock>(line, "PART_FirstTokenValue");
        (X(time) - label.Bounds.Width).ShouldBe(5, 0.01);
        var speedItem = Part<StackPanel>(line, "PART_SpeedItem");
        (X(speedItem) - (X(time) + time.Bounds.Width)).ShouldBe(8, 0.01);
        var dot = Part<TextBlock>(line, "PART_SpeedDot");
        var cell = Part<Panel>(line, "PART_SpeedCell");
        (X(cell) - (X(dot) + dot.Bounds.Width)).ShouldBe(8, 0.01);
        var unit = Part<TextBlock>(line, "PART_SpeedUnit");
        (X(unit) - (X(cell) + cell.Bounds.Width)).ShouldBe(3, 0.01);
        var costItem = Part<StackPanel>(line, "PART_CostItem");
        (X(costItem) - (X(unit) + unit.Bounds.Width)).ShouldBe(8, 0.01);
        // One 16.8 line (layout rounds it to whole pixels).
        line.Bounds.Height.ShouldBe(16.8, 0.5);
    }

    [AvaloniaFact]
    public void Speed_Cell_Keeps_Three_Digits_So_The_Unit_Stays()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Metrics(TimeSpan.FromSeconds(1), speed: 99, cost: 0.001m));
        var line = (AiMetricLine)rig.Content;
        var unit = Part<TextBlock>(line, "PART_SpeedUnit");
        var reserve = Part<TextBlock>(line, "PART_SpeedReserve");
        reserve.Opacity.ShouldBe(0);
        var cell = Part<Panel>(line, "PART_SpeedCell");
        cell.Bounds.Width.ShouldBe(reserve.Bounds.Width, 0.01);
        var x99 = unit.TranslatePoint(default, line)!.Value.X;
        line.TokensPerSecond = 152;
        rig.Settle();
        unit.TranslatePoint(default, line)!.Value.X.ShouldBe(x99, 0.01);
        line.TokensPerSecond = null;
        rig.Settle();
        unit.TranslatePoint(default, line)!.Value.X.ShouldBe(x99, 0.01);
    }

    [AvaloniaFact]
    public void Missing_Values_Show_The_Placeholder()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Metrics());
        var line = (AiMetricLine)rig.Content;
        Part<TextBlock>(line, "PART_FirstTokenValue").Text.ShouldBe("–");
        Part<RollingDigits>(line, AiMetricLine.SpeedPart).IsVisible.ShouldBeFalse();
        var speed = Part<TextBlock>(line, "PART_SpeedPlaceholder");
        speed.IsVisible.ShouldBeTrue();
        speed.Text.ShouldBe("–");
        TestHelpers.ColorOf(speed.Foreground).ShouldBe(Token(line, ArcDesignTokenKind.Foreground));
        Part<TextBlock>(line, "PART_SpeedUnit").Text.ShouldBe("tok/s");
        Part<RollingDigits>(line, AiMetricLine.CostPart).IsVisible.ShouldBeFalse();
        Part<TextBlock>(line, "PART_CostPlaceholder").IsVisible.ShouldBeTrue();
        line.SpokenText.ShouldBe("First token –, Speed – tok/s, Cost –");

        line.Placeholder = "n/a";
        rig.Settle();
        Part<TextBlock>(line, "PART_FirstTokenValue").Text.ShouldBe("n/a");
        speed.Text.ShouldBe("n/a");
    }

    [AvaloniaFact]
    public void Timing_Turns_The_Time_Secondary_And_Back()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Metrics(TimeSpan.Zero, timing: true, cost: 0.0001m));
        var line = (AiMetricLine)rig.Content;
        var time = Part<TextBlock>(line, "PART_FirstTokenValue");
        line.Classes.ShouldContain(AiMetricLine.TimingPseudoClass);
        time.Text.ShouldBe("0.00s");
        TestHelpers.ColorOf(time.Foreground).ShouldBe(Token(line, ArcDesignTokenKind.TextSecondary));
        // The owner updates the time; the part only shows it.
        line.FirstToken = TimeSpan.FromMilliseconds(180);
        rig.Settle();
        time.Text.ShouldBe("0.18s");
        line.FirstToken = TimeSpan.FromMilliseconds(820);
        line.IsTiming = false;
        rig.Settle();
        line.Classes.ShouldNotContain(AiMetricLine.TimingPseudoClass);
        time.Text.ShouldBe("0.82s");
        TestHelpers.ColorOf(time.Foreground).ShouldBe(Token(line, ArcDesignTokenKind.Foreground));
    }

    [AvaloniaFact]
    public void Timing_Color_Changes_Over_240ms_At_Full_Motion_And_120ms_Reduced()
    {
        foreach (var (level, duration) in new[] { (ArcMotionLevel.Full, 240.0), (ArcMotionLevel.Reduced, 120.0) })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            using var rig = new AiReplyRig(ArenaStatusSamples.Metrics(TimeSpan.Zero, cost: 0.0001m), level, clock);
            var line = (AiMetricLine)rig.Content;
            var time = Part<TextBlock>(line, "PART_FirstTokenValue");
            var transition = time.Transitions.ShouldNotBeNull().OfType<ArcBrushTransition>().Single(t => t.Property == TextBlock.ForegroundProperty);
            transition.Duration.TotalMilliseconds.ShouldBe(duration);
        }
        using (var off = new AiReplyRig(ArenaStatusSamples.Metrics(TimeSpan.Zero)))
        {
            (Part<TextBlock>((AiMetricLine)off.Content, "PART_FirstTokenValue").Transitions?.Count ?? 0).ShouldBe(0);
        }
    }

    [AvaloniaFact]
    public void First_Value_Appears_As_It_Is_And_Later_Values_Roll()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using var rig = new AiReplyRig(ArenaStatusSamples.Metrics(TimeSpan.FromSeconds(0.5), cost: 0.0001m), ArcMotionLevel.Full, clock);
        var line = (AiMetricLine)rig.Content;
        var speed = Part<RollingDigits>(line, AiMetricLine.SpeedPart);
        line.TokensPerSecond = 145;
        rig.Settle();
        speed.IsVisible.ShouldBeTrue();
        speed.IsInstant.ShouldBeFalse();
        speed.DisplayText.ShouldBe("145");
        // No roll from an old value: the wheels are already where the digits are.
        speed.WheelTargets.ShouldBe([1.0, 4, 5]);
        line.TokensPerSecond = 153;
        line.CostUsd = 0.0016m;
        rig.Settle();
        rig.Frames(34);
        var cost = Part<RollingDigits>(line, AiMetricLine.CostPart);
        cost.DisplayText.ShouldBe("$0.0016");
        speed.DisplayText.ShouldBe("153");
        // Rolling (the ones go 5 → 3 the way the value moved: up, through 6…9, 0…3).
        speed.WheelTargets[^1].ShouldBe(13);
        rig.Frames(700);
        // A new round clears the speed; the next first value appears without rolling.
        line.TokensPerSecond = null;
        rig.Settle();
        speed.IsVisible.ShouldBeFalse();
        line.TokensPerSecond = 88;
        rig.Settle();
        speed.DisplayText.ShouldBe("88");
    }

    [AvaloniaFact]
    public void Screen_Readers_Hear_The_Whole_Line_With_Names()
    {
        using var rig = new AiReplyRig(ArenaStatusSamples.Metrics(TimeSpan.FromMilliseconds(1830), speed: 152.2, cost: 0.0041m));
        var line = (AiMetricLine)rig.Content;
        line.SpokenText.ShouldBe("First token 1.83s, Speed 152 tok/s, Cost $0.0041");
        AutomationProperties.GetName(line).ShouldBe(line.SpokenText);
        var peer = ControlAutomationPeer.CreatePeerForElement(line);
        peer.GetName().ShouldBe(line.SpokenText);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
        peer.GetChildren().ShouldBeEmpty();
        line.SpeedName = null;
        line.CostName = null;
        rig.Settle();
        line.SpokenText.ShouldBe("First token 1.83s, 152 tok/s, $0.0041");
    }

    [AvaloniaFact]
    public void A_Narrow_Line_Wraps_2px_Apart()
    {
        var line = ArenaStatusSamples.Metrics(TimeSpan.FromMilliseconds(1830), speed: 152, cost: 0.0041m);
        using var rig = new AiReplyRig(new Border { Width = 150, Child = line, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left });
        var first = Part<StackPanel>(line, "PART_FirstTokenItem");
        var cost = Part<StackPanel>(line, "PART_CostItem");
        cost.TranslatePoint(default, line)!.Value.Y.ShouldBeGreaterThanOrEqualTo(first.Bounds.Height + 2 - 0.01);
        line.Bounds.Height.ShouldBeGreaterThan(30);
    }

    [AvaloniaFact]
    public void Reduced_Jumps_The_Digits()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using var rig = new AiReplyRig(ArenaStatusSamples.Metrics(TimeSpan.FromSeconds(0.5), speed: 140, cost: 0.0010m), ArcMotionLevel.Reduced, clock);
        var line = (AiMetricLine)rig.Content;
        line.TokensPerSecond = 160;
        rig.Settle();
        rig.Frames(17);
        Part<RollingDigits>(line, AiMetricLine.SpeedPart).DisplayText.ShouldBe("160");
    }
}
