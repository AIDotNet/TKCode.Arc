using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The editor's token estimate: splitting the formatted line into words and a rolling number, the wheels' settings
/// (clipped at the line, quick spring, en-US grouping), lines whose number cannot roll, the muted 12 / 16.8 look and
/// what screen readers get.
/// </summary>
public class PromptTokenCountTests
{
    private static readonly Func<int, string> Tokens = n => n == 1 ? "1 token" : $"{n:N0} tokens";

    private static (PromptTokenCount Count, Window Window) Show(int count, Func<int, string>? format, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var tokens = new PromptTokenCount { Count = count, Format = format };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { tokens } }, level);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (tokens, window);
    }

    [Theory]
    [InlineData("112 tokens", "", "112", " tokens")]
    [InlineData("1,234 tokens", "", "1,234", " tokens")]
    [InlineData("Tokens: 12", "Tokens: ", "12", "")]
    [InlineData("约 1 234 个词元", "约 ", "1 234", " 个词元")]
    [InlineData("12, 13", "", "12", ", 13")]
    [InlineData("no number", "no number", "", "")]
    public void Split_Finds_The_First_Number_With_Its_Group_Separators(string text, string prefix, string number, string suffix)
    {
        PromptTokenCount.Split(text).ShouldBe((prefix, number, suffix));
    }

    [Theory]
    [InlineData(1234, "1,234", true)]
    [InlineData(1234, "1234", false)]
    [InlineData(12, "12", false)]
    [InlineData(1234, "1.234", null)]
    [InlineData(1234, "1 234", null)]
    [InlineData(1234, "", null)]
    [InlineData(-3, "3", null)]
    public void The_Wheels_Roll_Only_Numbers_They_Write_The_Same_Way(int count, string number, bool? grouping)
    {
        PromptTokenCount.RollingFormat(count, number).ShouldBe(grouping);
    }

    [AvaloniaFact]
    public void The_Number_Rolls_And_The_Words_Follow_Four_Pixels_After_It()
    {
        var (tokens, window) = Show(112, Tokens);
        try
        {
            tokens.IsRolling.ShouldBeTrue();
            tokens.DisplayText.ShouldBe("112 tokens");
            var digits = tokens.Digits!;
            digits.IsVisible.ShouldBeTrue();
            digits.Value.ShouldBe(112);
            digits.UseGrouping.ShouldBeFalse();
            digits.DisplayText.ShouldBe("112");
            tokens.WordsBlock!.Text.ShouldBe("tokens");
            tokens.WordsBlock.IsVisible.ShouldBeTrue();
            tokens.WordsBlock.Margin.ShouldBe(new Thickness(PromptTokenCount.WordGap, 0, 0, 0));
            tokens.PrefixBlock!.IsVisible.ShouldBeFalse();
            // The words start 4px after the number.
            tokens.WordsBlock.Bounds.Left.ShouldBe(digits.Bounds.Right + 4, 0.01);

            tokens.Count = 1234;
            tokens.DisplayText.ShouldBe("1,234 tokens");
            digits.Value.ShouldBe(1234);
            digits.UseGrouping.ShouldBeTrue();
            digits.DisplayText.ShouldBe("1,234");

            tokens.Count = 1;
            tokens.WordsBlock.Text.ShouldBe("token");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Wheels_Are_Cut_At_The_Line_And_Turn_On_The_Quick_Spring()
    {
        var (tokens, window) = Show(40, Tokens);
        try
        {
            var digits = tokens.Digits!;
            digits.Falloff.ShouldBe(RollingDigitsFalloff.None);
            digits.WheelSpring.ShouldBe(ArcSpring.Quick);
            digits.CultureName.ShouldBe("en-US");
            digits.FontSize.ShouldBe(12);
            TestHelpers.ColorOf(digits.Foreground).ShouldBe(ArcTokens.GetColor(tokens, ArcDesignTokenKind.TextMuted));
            TestHelpers.ColorOf(tokens.WordsBlock!.Foreground).ShouldBe(ArcTokens.GetColor(tokens, ArcDesignTokenKind.TextMuted));
            tokens.WordsBlock.LineHeight.ShouldBe(16.8);
            tokens.WordsBlock.FontSize.ShouldBe(12);
            tokens.Padding.ShouldBe(new Thickness(0, 0, 12, 0));
            tokens.HorizontalAlignment.ShouldBe(Avalonia.Layout.HorizontalAlignment.Right);
            // Arc's line box: 16.8 high (laid out to whole pixels).
            tokens.Bounds.Height.ShouldBe(16.8, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_Motion_A_Change_Rolls_The_Wheels_And_Off_Jumps()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (tokens, window) = Show(41, Tokens, ArcMotionLevel.Full);
        try
        {
            var digits = tokens.Digits!;
            tokens.Count = 43;
            // The ones wheel heads for 3 from 1 (two steps up) and is on its way after a few frames.
            digits.WheelTargets[^1].ShouldBe(3);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            digits.DisplayText.ShouldBe("43");
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Format_Only_The_Number_Shows()
    {
        var (tokens, window) = Show(7, null);
        try
        {
            tokens.DisplayText.ShouldBe("7");
            tokens.IsRolling.ShouldBeTrue();
            tokens.WordsBlock!.IsVisible.ShouldBeFalse();
            tokens.PrefixBlock!.IsVisible.ShouldBeFalse();
            tokens.Digits!.Value.ShouldBe(7);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Words_Before_The_Number_Keep_Their_Place()
    {
        var (tokens, window) = Show(12, n => $"Tokens: {n}");
        try
        {
            tokens.PrefixBlock!.Text.ShouldBe("Tokens:");
            tokens.PrefixBlock.IsVisible.ShouldBeTrue();
            tokens.PrefixBlock.Margin.ShouldBe(new Thickness(0, 0, PromptTokenCount.WordGap, 0));
            tokens.WordsBlock!.IsVisible.ShouldBeFalse();
            tokens.Digits!.Bounds.Left.ShouldBe(tokens.PrefixBlock.Bounds.Right + 4, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Number_The_Wheels_Cannot_Write_Shows_The_Line_As_Plain_Text()
    {
        var (tokens, window) = Show(1234, n => $"{n:#,0} Token".Replace(',', '.'));
        try
        {
            tokens.IsRolling.ShouldBeFalse();
            tokens.Digits!.IsVisible.ShouldBeFalse();
            tokens.PrefixBlock!.Text.ShouldBe("1.234 Token");
            tokens.PrefixBlock.IsVisible.ShouldBeTrue();
            tokens.WordsBlock!.IsVisible.ShouldBeFalse();

            // A count it can write rolls again.
            tokens.Format = Tokens;
            tokens.IsRolling.ShouldBeTrue();
            tokens.Digits.IsVisible.ShouldBeTrue();
            tokens.PrefixBlock.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_The_Whole_Line()
    {
        var (tokens, window) = Show(112, Tokens);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(tokens);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
            peer.GetName().ShouldBe("112 tokens");
            peer.GetChildren().ShouldBeEmpty();
            tokens.Count = 113;
            peer.GetName().ShouldBe("113 tokens");
            AutomationProperties.GetName(tokens).ShouldBe("113 tokens");
            tokens.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
