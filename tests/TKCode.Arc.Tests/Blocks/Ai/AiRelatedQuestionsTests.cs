using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiRelatedQuestions"/>: the heading, the hairline-separated 48px rows with their arrows, hover (colors and the
/// arrow's slide at each motion level), invoking by pointer and keys, the tab order, live questions (rows reused with their
/// focus), the plain appearance, a list without a heading, wrapping, the hairline's color in both themes and automation.
/// </summary>
public class AiRelatedQuestionsTests
{
    private static double Y(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.Y;

    private static double X(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.X;

    /// <summary>Questions short enough for one line in the headless test font (its advances are wider than a real font's).</summary>
    private static AvaloniaList<string> Short() => new() { "Which checks block?", "Faster builds?", "Flaky tests?" };

    [AvaloniaFact]
    public void A_Heading_Then_48px_Rows_Between_Hairlines()
    {
        using var rig = new AiAnswerActionsRig(AiAnswerActionsSamples.RelatedQuestions(r => r.Questions = Short()));
        var list = (AiRelatedQuestions)rig.Content;
        var heading = TestHelpers.Descendant<TextBlock>(list, "PART_Heading");
        heading.IsVisible.ShouldBeTrue();
        heading.Text.ShouldBe("Related");
        heading.FontSize.ShouldBe(14);
        heading.LineHeight.ShouldBe(15.4);
        heading.FontWeight.ShouldBe(FontWeight.Medium);
        heading.LetterSpacing.ShouldBe(-0.42);
        heading.Margin.ShouldBe(new Thickness(0, 0, 0, 6));
        var top = TestHelpers.Descendant<AiActionsHairline>(list, "PART_TopLine");
        Y(top, list).ShouldBe(15.4 + 6, 1);
        top.Bounds.Height.ShouldBe(1);
        var rows = list.Rows;
        rows.Select(r => r.Question).ShouldBe(Short());
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].Bounds.Height.ShouldBe(49);
            Y(rows[i], list).ShouldBe(15.4 + 6 + 1 + 49 * i, 1);
            TestHelpers.Descendant<AiActionsHairline>(rows[i], "PART_Line").IsVisible.ShouldBeTrue();
        }
        var text = TestHelpers.Descendant<TextBlock>(rows[0], "PART_Text");
        var arrow = TestHelpers.Descendant<ArcGlyph>(rows[0], "PART_Arrow");
        X(text, rows[0]).ShouldBe(2, 0.01);
        (rows[0].Bounds.Width - X(arrow, rows[0]) - 16).ShouldBe(2, 0.01);
        arrow.Data.ShouldBeSameAs(ArcIcons.ArrowRight);
        arrow.Size.ShouldBe(16);
        text.FontSize.ShouldBe(16);
        text.LineHeight.ShouldBe(22.4);
        TestHelpers.ColorOf(text.Foreground).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.TextSecondary));
        TestHelpers.ColorOf(arrow.Foreground).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.TextMuted));
        rows[0].CornerRadius.ShouldBe(new CornerRadius(10));
        TestHelpers.ColorOf(top.Brush).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.BorderSubtle));
    }

    [AvaloniaFact]
    public void Hover_Turns_Both_Foreground_And_Slides_The_Arrow_3px()
    {
        using var rig = new AiAnswerActionsRig(AiAnswerActionsSamples.RelatedQuestions());
        var list = (AiRelatedQuestions)rig.Content;
        var row = list.Rows[1];
        rig.Hover(row);
        row.Classes.ShouldContain(AiRelatedQuestionRow.HoverPseudoClass);
        var text = TestHelpers.Descendant<TextBlock>(row, "PART_Text");
        var arrow = TestHelpers.Descendant<ArcGlyph>(row, "PART_Arrow");
        TestHelpers.ColorOf(text.Foreground).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.Foreground));
        TestHelpers.ColorOf(arrow.Foreground).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.Foreground));
        arrow.RenderTransform!.Value.M31.ShouldBe(3, 0.01);
        // No fill behind a hovered row.
        TestHelpers.ColorOf(row.Background).A.ShouldBe((byte)0);
        rig.MoveTo(new Point(2, 2));
        arrow.RenderTransform!.Value.M31.ShouldBe(0, 0.01);
    }

    [AvaloniaFact]
    public void Full_Motion_Slides_The_Arrow_On_The_Css_Spring_And_Reduced_Keeps_It_Still()
    {
        var clock = new ManualFrameClock();
        using (var rig = new AiAnswerActionsRig(AiAnswerActionsSamples.RelatedQuestions(), ArcMotionLevel.Full, clock))
        {
            var list = (AiRelatedQuestions)rig.Content;
            var arrow = TestHelpers.Descendant<ArcGlyph>(list.Rows[0], "PART_Arrow");
            var text = TestHelpers.Descendant<TextBlock>(list.Rows[0], "PART_Text");
            var slide = arrow.Transitions!.OfType<ArcTransformOperationsTransition>().Single();
            slide.Property.ShouldBe(Visual.RenderTransformProperty);
            slide.Duration.ShouldBe(ArcMotionPresets.CssSpring);
            arrow.Transitions!.OfType<ArcBrushTransition>().Single().Duration.ShouldBe(ArcMotionPresets.Fast);
            text.Transitions!.OfType<ArcBrushTransition>().Single().Duration.ShouldBe(ArcMotionPresets.Fast);
        }
        using (var rig = new AiAnswerActionsRig(AiAnswerActionsSamples.RelatedQuestions(), ArcMotionLevel.Reduced, clock))
        {
            var list = (AiRelatedQuestions)rig.Content;
            var arrow = TestHelpers.Descendant<ArcGlyph>(list.Rows[0], "PART_Arrow");
            arrow.Transitions!.OfType<ArcTransformOperationsTransition>().ShouldBeEmpty();
            arrow.Transitions!.OfType<ArcBrushTransition>().Single().Duration.ShouldBe(ArcMotionPresets.Instant);
            rig.Hover(list.Rows[0]);
            arrow.RenderTransform!.Value.M31.ShouldBe(0, 0.01);
        }
    }

    [AvaloniaFact]
    public void A_Click_Enter_Or_Space_Asks_The_Question()
    {
        var list = AiAnswerActionsSamples.RelatedQuestions();
        var asked = new List<AiRelatedQuestionEventArgs>();
        list.QuestionInvoked += (_, e) => asked.Add(e);
        using var rig = new AiAnswerActionsRig(list);
        rig.Click(list.Rows[2]);
        asked.Single().Question.ShouldBe(AiAnswerActionsSamples.Related[2]);
        asked.Single().Index.ShouldBe(2);
        list.Rows[0].Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.Space);
        asked.Count.ShouldBe(3);
        asked[1].Index.ShouldBe(0);
        asked[2].Index.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Every_Row_Is_A_Tab_Stop_With_An_Inset_Ring()
    {
        var list = AiAnswerActionsSamples.RelatedQuestions();
        using var rig = new AiAnswerActionsRig(list);
        rig.Before.Focus(NavigationMethod.Tab);
        foreach (var row in list.Rows)
        {
            rig.Key(PhysicalKey.Tab);
            rig.Focused.ShouldBeSameAs(row);
        }
        var ring = TestHelpers.Descendant<Border>(list.Rows[^1], "PART_FocusRing");
        ring.IsVisible.ShouldBeTrue();
        ring.Margin.ShouldBe(default);
        ring.BorderThickness.ShouldBe(new Thickness(2));
        rig.Key(PhysicalKey.Tab);
        rig.Focused.ShouldBeSameAs(rig.After);
    }

    [AvaloniaFact]
    public void Questions_Follow_The_Collection_And_Rows_Keep_Their_Focus()
    {
        var questions = new AvaloniaList<string>(AiAnswerActionsSamples.Related);
        var list = AiAnswerActionsSamples.RelatedQuestions(r => r.Questions = questions);
        using var rig = new AiAnswerActionsRig(list);
        var first = list.Rows[0];
        first.Focus(NavigationMethod.Tab);
        questions[0] = "What changes for release branches?";
        rig.Settle();
        list.Rows[0].ShouldBeSameAs(first);
        first.Question.ShouldBe("What changes for release branches?");
        first.IsFocused.ShouldBeTrue();
        AutomationProperties.GetName(first).ShouldBe("What changes for release branches?");
        questions.RemoveAt(2);
        questions.Add("  ");
        rig.Settle();
        list.Rows.Count.ShouldBe(2);
        list.Questions = null;
        rig.Settle();
        list.Rows.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Plain_Rows_Have_No_Hairlines_And_No_Heading_Names_The_List()
    {
        var list = AiAnswerActionsSamples.RelatedQuestions(r =>
        {
            r.Heading = null;
            r.ListName = "Suggested questions";
            r.Appearance = AiRelatedQuestionsAppearance.Plain;
            r.Questions = Short();
        });
        using var rig = new AiAnswerActionsRig(list);
        TestHelpers.Descendant<TextBlock>(list, "PART_Heading").IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<AiActionsHairline>(list, "PART_TopLine").IsVisible.ShouldBeFalse();
        list.Rows.ShouldAllBe(r => !TestHelpers.Descendant<AiActionsHairline>(r, "PART_Line").IsVisible);
        list.Rows[0].Bounds.Height.ShouldBe(48);
        Y(list.Rows[0], list).ShouldBe(0, 0.01);
        AutomationProperties.GetName(list).ShouldBe("Suggested questions");
        list.Appearance = AiRelatedQuestionsAppearance.List;
        rig.Settle();
        list.Rows.ShouldAllBe(r => TestHelpers.Descendant<AiActionsHairline>(r, "PART_Line").IsVisible);
        TestHelpers.Descendant<AiActionsHairline>(list, "PART_TopLine").IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Long_Question_Wraps_And_The_Row_Grows()
    {
        var list = AiAnswerActionsSamples.RelatedQuestions();
        using var rig = new AiAnswerActionsRig(list, width: 240);
        var row = list.Rows[0];
        var text = TestHelpers.Descendant<TextBlock>(row, "PART_Text");
        text.Bounds.Height.ShouldBeGreaterThan(22.4 * 1.5);
        row.Bounds.Height.ShouldBe(text.Bounds.Height + 24 + 1, 0.5);
        var arrow = TestHelpers.Descendant<ArcGlyph>(row, "PART_Arrow");
        // The arrow stays centered on the question.
        (Y(arrow, row) + 8).ShouldBe(Y(text, row) + text.Bounds.Height / 2, 0.5);
    }

    [AvaloniaFact]
    public void The_Group_Is_Named_By_Its_Heading_And_Rows_By_Their_Questions()
    {
        var list = AiAnswerActionsSamples.RelatedQuestions();
        using var rig = new AiAnswerActionsRig(list);
        var peer = ControlAutomationPeer.CreatePeerForElement(list);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        peer.GetName().ShouldBe("Related");
        var row = ControlAutomationPeer.CreatePeerForElement(list.Rows[0]);
        row.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        row.GetName().ShouldBe(AiAnswerActionsSamples.Related[0]);
        row.GetChildren().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task The_Hairline_Is_BorderSubtle_In_Light_And_Border_In_Dark()
    {
        var list = AiAnswerActionsSamples.RelatedQuestions();
        using var rig = new AiAnswerActionsRig(list);
        var line = TestHelpers.Descendant<AiActionsHairline>(list, "PART_TopLine");
        line.Kind.ShouldBe(ArcDesignTokenKind.BorderSubtle);
        try
        {
            await TestHelpers.ApplyAsync(TestHelpers.Dark);
            rig.Settle();
            line.Kind.ShouldBe(ArcDesignTokenKind.Border);
            TestHelpers.ColorOf(line.Brush).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.Border));
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
            rig.Settle();
        }
        line.Kind.ShouldBe(ArcDesignTokenKind.BorderSubtle);
    }
}
