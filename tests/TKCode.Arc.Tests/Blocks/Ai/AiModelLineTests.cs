using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiModelLine"/>: "[mark] Name · tokens · time" geometry, type and colors, the stopped line, missing parts and
/// their dots, the provider's mark, live model changes, wrapping and trimming at narrow widths, and what it reads as.
/// </summary>
public class AiModelLineTests
{
    private static double X(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.X;

    private static AiModelLine Line(Action<AiModelLine>? configure = null)
    {
        var line = AiAnswerActionsSamples.ModelLine(configure);
        line.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        return line;
    }

    [AvaloniaFact]
    public void Mark_Name_And_Figures_Sit_8px_Apart_With_Dots()
    {
        using var rig = new AiAnswerActionsRig(Line());
        var line = (AiModelLine)rig.Content;
        var mark = TestHelpers.Descendant<ModelMark>(line, "PART_Mark");
        var name = TestHelpers.Descendant<TextBlock>(line, "PART_Name");
        var tokensDot = TestHelpers.Descendant<Ellipse>(line, "PART_TokensDot");
        var tokens = TestHelpers.Descendant<TextBlock>(line, "PART_TokensText");
        var durationDot = TestHelpers.Descendant<Ellipse>(line, "PART_DurationDot");
        var duration = TestHelpers.Descendant<TextBlock>(line, "PART_DurationText");
        mark.Size.ShouldBe(14);
        mark.Mark.ShouldBeSameAs(AiAnswerActionsSamples.StarMark);
        mark.MarkColor.ShouldBeSameAs(AiAnswerActionsSamples.Orange);
        X(mark, line).ShouldBe(0, 0.01);
        X(name, line).ShouldBe(14 + 8, 0.01);
        X(tokensDot, line).ShouldBe(X(name, line) + name.Bounds.Width + 8, 0.01);
        X(tokens, line).ShouldBe(X(tokensDot, line) + 3 + 8, 0.01);
        X(durationDot, line).ShouldBe(X(tokens, line) + tokens.Bounds.Width + 8, 0.01);
        X(duration, line).ShouldBe(X(durationDot, line) + 3 + 8, 0.01);
        tokensDot.Bounds.Size.ShouldBe(new Size(3, 3));
        tokensDot.Opacity.ShouldBe(0.7);
        TestHelpers.ColorOf(tokensDot.Fill).ShouldBe(ArcTokens.GetColor(line, ArcDesignTokenKind.TextMuted));
        name.Text.ShouldBe("Lumen Pro 4");
        tokens.Text.ShouldBe("812 tokens");
        duration.Text.ShouldBe("18.6s");
        name.FontSize.ShouldBe(12);
        name.LineHeight.ShouldBe(16.8);
        TestHelpers.ColorOf(name.Foreground).ShouldBe(ArcTokens.GetColor(line, ArcDesignTokenKind.TextSecondary));
        TestHelpers.ColorOf(tokens.Foreground).ShouldBe(ArcTokens.GetColor(line, ArcDesignTokenKind.TextMuted));
        tokens.FontFeatures.ShouldNotBeNull().ShouldContain(f => f.Tag == "tnum");
        duration.FontFeatures.ShouldNotBeNull().ShouldContain(f => f.Tag == "tnum");
        line.Bounds.Height.ShouldBe(16.8, 0.5);
    }

    [AvaloniaFact]
    public void A_Stopped_Version_Shows_Only_The_Model()
    {
        using var rig = new AiAnswerActionsRig(Line(l => l.IsStopped = true));
        var line = (AiModelLine)rig.Content;
        line.Classes.ShouldContain(AiModelLine.StoppedPseudoClass);
        TestHelpers.Descendant<StackPanel>(line, "PART_Tokens").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<StackPanel>(line, "PART_Duration").IsVisible.ShouldBeFalse();
        AutomationProperties.GetName(line).ShouldBe("Lumen Pro 4");
        line.IsStopped = false;
        rig.Settle();
        TestHelpers.Descendant<StackPanel>(line, "PART_Tokens").IsVisible.ShouldBeTrue();
        AutomationProperties.GetName(line).ShouldBe("Lumen Pro 4, 812 tokens, 18.6s");
    }

    [AvaloniaFact]
    public void Missing_Parts_Leave_No_Stray_Dot()
    {
        using var rig = new AiAnswerActionsRig(Line(l => l.TokensText = null));
        var line = (AiModelLine)rig.Content;
        TestHelpers.Descendant<StackPanel>(line, "PART_Tokens").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<Ellipse>(line, "PART_DurationDot").IsVisible.ShouldBeTrue();
        line.Model = null;
        rig.Settle();
        TestHelpers.Descendant<DockPanel>(line, "PART_Model").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<Ellipse>(line, "PART_DurationDot").IsVisible.ShouldBeFalse();
        X(TestHelpers.Descendant<TextBlock>(line, "PART_DurationText"), line).ShouldBe(0, 0.01);
        AutomationProperties.GetName(line).ShouldBe("18.6s");
        line.TokensText = "90 tokens";
        rig.Settle();
        TestHelpers.Descendant<Ellipse>(line, "PART_TokensDot").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<Ellipse>(line, "PART_DurationDot").IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Model_Without_A_Mark_Uses_Its_Providers_Or_None()
    {
        var provider = new AiModelProvider { Id = "atlas", Name = "Atlas", Mark = AiAnswerActionsSamples.RingsMark };
        var model = new AiModel { Id = "plain", Name = "Plain 1", ProviderId = "atlas" };
        using var rig = new AiAnswerActionsRig(Line(l =>
        {
            l.Model = model;
            l.Providers = new AvaloniaList<AiModelProvider> { provider };
        }));
        var line = (AiModelLine)rig.Content;
        var mark = TestHelpers.Descendant<ModelMark>(line, "PART_Mark");
        mark.Mark.ShouldBeSameAs(AiAnswerActionsSamples.RingsMark);
        line.Providers = null;
        rig.Settle();
        mark.IsVisible.ShouldBeFalse();
        X(TestHelpers.Descendant<TextBlock>(line, "PART_Name"), line).ShouldBe(0, 0.01);
    }

    [AvaloniaFact]
    public void A_Change_To_The_Model_Shows_At_Once()
    {
        var model = AiAnswerActionsSamples.LumenPro();
        using var rig = new AiAnswerActionsRig(Line(l => l.Model = model));
        var line = (AiModelLine)rig.Content;
        model.Name = "Lumen Pro 4.1";
        rig.Settle();
        TestHelpers.Descendant<TextBlock>(line, "PART_Name").Text.ShouldBe("Lumen Pro 4.1");
        AutomationProperties.GetName(line).ShouldStartWith("Lumen Pro 4.1");
        line.Model = AiAnswerActionsSamples.Atlas();
        rig.Settle();
        TestHelpers.Descendant<ModelMark>(line, "PART_Mark").Mark.ShouldBeSameAs(AiAnswerActionsSamples.RingsMark);
        // The old model is no longer followed.
        model.Name = "Changed";
        rig.Settle();
        TestHelpers.Descendant<TextBlock>(line, "PART_Name").Text.ShouldBe("Atlas 2.1");
    }

    [AvaloniaFact]
    public void Narrow_The_Parts_Wrap_4px_Apart_And_A_Long_Name_Trims()
    {
        using var rig = new AiAnswerActionsRig(new Border { Width = 150, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Child = Line() });
        var line = TestHelpers.Descendant<AiModelLine>(rig.Content);
        var tokens = TestHelpers.Descendant<StackPanel>(line, "PART_Tokens");
        var duration = TestHelpers.Descendant<StackPanel>(line, "PART_Duration");
        // The name and the tokens do not fit together in 150: the tokens go to the next line.
        tokens.TranslatePoint(default, line)!.Value.Y.ShouldBe(16.8 + 4, 0.5);
        line.Bounds.Height.ShouldBeGreaterThan(16.8 * 2);
        duration.TranslatePoint(default, line)!.Value.Y.ShouldBeGreaterThanOrEqualTo(16.8 + 4 - 0.5);

        line.Model = new AiModel { Name = "A model name far too long to fit in the narrow line", Mark = AiAnswerActionsSamples.StarMark };
        rig.Settle();
        var name = TestHelpers.Descendant<TextBlock>(line, "PART_Name");
        name.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
        TestHelpers.Descendant<DockPanel>(line, "PART_Model").Bounds.Width.ShouldBeLessThanOrEqualTo(150.5);
    }

    [AvaloniaFact]
    public void It_Reads_As_One_Line_Of_Text()
    {
        using var rig = new AiAnswerActionsRig(Line());
        var line = (AiModelLine)rig.Content;
        var peer = ControlAutomationPeer.CreatePeerForElement(line);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
        peer.GetName().ShouldBe("Lumen Pro 4, 812 tokens, 18.6s");
        peer.GetChildren().ShouldBeEmpty();
        line.SpokenText.ShouldBe("Lumen Pro 4, 812 tokens, 18.6s");
    }
}
