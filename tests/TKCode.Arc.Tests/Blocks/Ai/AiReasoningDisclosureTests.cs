using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiReasoningDisclosure"/>: the head row and paragraph geometry, the Thinking → Thought label swap and its width,
/// toggling by pointer, keys and automation, the open-while-thinking and no-reasoning rules, and the collapse motion at each level.
/// </summary>
public class AiReasoningDisclosureTests
{
    private static AiReasoningDisclosure Create(bool thinking = false, string? reasoning = AiReplySamples.Reasoning, bool expanded = false) => new()
    {
        Lead = AiReplySamples.ModelMark(),
        IsThinking = thinking,
        ThinkingText = AiReplySamples.Thinking,
        ThoughtText = thinking ? null : AiReplySamples.Thought,
        Reasoning = reasoning,
        IsExpanded = expanded
    };

    private static AiCollapsible Body(AiReasoningDisclosure d) => d.Body.ShouldNotBeNull();

    private static AiReasoningButton Button(AiReasoningDisclosure d) => d.Button.ShouldNotBeNull();

    [AvaloniaFact]
    public void Head_Row_Is_The_Lead_Cell_Then_The_Button_28px_High()
    {
        using var rig = new AiReplyRig(Create());
        var d = (AiReasoningDisclosure)rig.Content;
        var lead = TestHelpers.Descendant<ContentPresenter>(d, "PART_Lead");
        lead.Bounds.Width.ShouldBe(22);
        lead.Bounds.Height.ShouldBe(22);
        var button = Button(d);
        button.Bounds.Height.ShouldBe(28);
        button.TranslatePoint(default, d)!.Value.X.ShouldBe(28, 0.01);
        button.Padding.ShouldBe(new Thickness(6, 0));
        var chevron = TestHelpers.Descendant<ArcGlyph>(button, "PART_Chevron");
        chevron.Size.ShouldBe(14);
        chevron.StrokeThickness.ShouldBe(1.75);
        TestHelpers.ColorOf(chevron.Foreground).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.TextMuted));
        TestHelpers.ColorOf(button.Foreground).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.TextSecondary));
        // Closed: nothing under the head.
        d.Bounds.Height.ShouldBe(28);
        Body(d).IsMounted.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Without_A_Lead_The_Button_Starts_The_Row()
    {
        var d = Create();
        d.Lead = null;
        using var rig = new AiReplyRig(d);
        Button(d).TranslatePoint(default, d)!.Value.X.ShouldBe(0, 0.01);
    }

    [AvaloniaFact]
    public void Thinking_Shows_The_Shimmer_And_Thought_Shows_Plain_Text()
    {
        var d = Create(thinking: true, reasoning: null);
        using var rig = new AiReplyRig(d);
        var button = Button(d);
        var shimmer = button.Label.ShouldBeOfType<TextShimmer>();
        shimmer.Text.ShouldBe("Thinking");
        shimmer.IsActive.ShouldBeTrue();
        AutomationProperties.GetName(button).ShouldBe("Thinking");
        d.Classes.ShouldContain(AiReasoningDisclosure.ThinkingPseudoClass);

        d.IsThinking = false;
        d.ThoughtText = "Thought for 4s";
        rig.Settle();
        var text = button.Label.ShouldBeOfType<TextBlock>();
        text.Text.ShouldBe("Thought for 4s");
        text.LineHeight.ShouldBe(19.6);
        AutomationProperties.GetName(button).ShouldBe("Thought for 4s");
        // Only the new label remains once Off motion swapped it.
        TestHelpers.Descendant<ArcContentSwap>(button, "PART_Label").GetVisualChildrenCount().ShouldBe(1);
    }

    [AvaloniaFact]
    public void Thought_Text_Missing_Keeps_The_Thinking_Words_Without_The_Shimmer()
    {
        var d = Create(thinking: true, reasoning: null);
        using var rig = new AiReplyRig(d);
        d.IsThinking = false;
        rig.Settle();
        Button(d).Label.ShouldBeOfType<TextBlock>().Text.ShouldBe("Thinking");
    }

    [AvaloniaFact]
    public void A_Press_Opens_And_Closes_The_Reasoning_Two_Way()
    {
        var d = Create();
        var changes = new List<bool>();
        d.PropertyChanged += (_, e) =>
        {
            if (e.Property == AiReasoningDisclosure.IsExpandedProperty)
            {
                changes.Add(e.GetNewValue<bool>());
            }
        };
        using var rig = new AiReplyRig(d);
        rig.Click(Button(d));
        d.IsExpanded.ShouldBeTrue();
        Body(d).IsMounted.ShouldBeTrue();
        Button(d).Classes.ShouldContain(AiReasoningButton.ExpandedPseudoClass);
        TestHelpers.Descendant<SelectableTextBlock>(d, "PART_Reasoning").Text.ShouldBe(AiReplySamples.Reasoning);

        rig.Click(Button(d));
        d.IsExpanded.ShouldBeFalse();
        Body(d).IsMounted.ShouldBeFalse();
        changes.ShouldBe([true, false]);
    }

    [AvaloniaFact]
    public void Paragraph_Sits_10px_In_6px_Under_The_Row_With_A_Rule_And_16px_To_The_Text()
    {
        var d = Create(expanded: true);
        using var rig = new AiReplyRig(d);
        var body = Body(d);
        body.IsMounted.ShouldBeTrue();
        var rule = TestHelpers.Descendant<Border>(d, "PART_Rule");
        var origin = rule.TranslatePoint(default, d)!.Value;
        origin.X.ShouldBe(10, 0.01);
        origin.Y.ShouldBe(28 + 6, 0.01);
        rule.BorderThickness.ShouldBe(new Thickness(1, 0, 0, 0));
        rule.Padding.ShouldBe(new Thickness(16, 2, 0, 2));
        TestHelpers.ColorOf(rule.BorderBrush).ShouldBe(ArcTokens.GetColor(d, ArcDesignTokenKind.Border));
        var text = TestHelpers.Descendant<SelectableTextBlock>(d, "PART_Reasoning");
        text.TranslatePoint(default, d)!.Value.X.ShouldBe(27, 0.01);
        text.FontSize.ShouldBe(14);
        text.LineHeight.ShouldBe(22.4);
        TestHelpers.ColorOf(text.Foreground).ShouldBe(ArcTokens.GetColor(d, ArcDesignTokenKind.TextSecondary));
        // Row, 6px gap, the paragraph box, 2px after it.
        d.Bounds.Height.ShouldBe(28 + 6 + rule.Bounds.Height + 2, 0.01);
        body.Clip.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Enter_And_Space_Toggle()
    {
        var d = Create();
        using var rig = new AiReplyRig(d);
        Button(d).Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        d.IsExpanded.ShouldBeTrue();
        rig.Key(PhysicalKey.Space);
        d.IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Automation_Reports_And_Drives_The_Expanded_State()
    {
        var d = Create();
        using var rig = new AiReplyRig(d);
        var peer = ControlAutomationPeer.CreatePeerForElement(Button(d));
        var expand = peer.GetProvider<IExpandCollapseProvider>().ShouldNotBeNull();
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        expand.Expand();
        rig.Settle();
        d.IsExpanded.ShouldBeTrue();
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        expand.Collapse();
        rig.Settle();
        d.IsExpanded.ShouldBeFalse();
        peer.GetName().ShouldBe("Thought for 3s");
        peer.GetChildren().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Opened_While_Thinking_Shows_Nothing_Until_Reasoning_Arrives()
    {
        var d = Create(thinking: true, reasoning: null);
        using var rig = new AiReplyRig(d);
        Button(d).IsEffectivelyEnabled.ShouldBeTrue();
        rig.Click(Button(d));
        d.IsExpanded.ShouldBeTrue();
        Button(d).Classes.ShouldContain(AiReasoningButton.ExpandedPseudoClass);
        // No reasoning yet: no gap, no empty rule.
        Body(d).IsMounted.ShouldBeFalse();
        d.Bounds.Height.ShouldBe(28);

        d.Reasoning = "Look at the restore step first.";
        rig.Settle();
        Body(d).IsMounted.ShouldBeTrue();
        d.Bounds.Height.ShouldBeGreaterThan(28 + 6);

        // Streaming reasoning grows the open paragraph at once.
        var before = d.Bounds.Height;
        d.Reasoning += string.Concat(Enumerable.Repeat(" Then the compile step, which depends on the restored packages.", 8));
        rig.Settle();
        d.Bounds.Height.ShouldBeGreaterThan(before);
    }

    [AvaloniaFact]
    public void Without_Reasoning_A_Finished_Reply_Shows_A_Plain_Label()
    {
        var d = Create(reasoning: null);
        using var rig = new AiReplyRig(d);
        var button = Button(d);
        button.IsEffectivelyEnabled.ShouldBeFalse();
        button.Classes.ShouldContain(AiReasoningButton.StaticPseudoClass);
        TestHelpers.Descendant<ArcGlyph>(button, "PART_Chevron").IsVisible.ShouldBeFalse();
        rig.Click(button);
        d.IsExpanded.ShouldBeFalse();
        ControlAutomationPeer.CreatePeerForElement(button).GetProvider<IExpandCollapseProvider>()!.ExpandCollapseState
            .ShouldBe(ExpandCollapseState.LeafNode);

        // Reasoning arriving later makes it a disclosure again.
        d.Reasoning = AiReplySamples.Reasoning;
        rig.Settle();
        button.IsEffectivelyEnabled.ShouldBeTrue();
        TestHelpers.Descendant<ArcGlyph>(button, "PART_Chevron").IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Hover_And_Open_Turn_The_Text_Foreground_Without_A_Fill()
    {
        var d = Create();
        using var rig = new AiReplyRig(d);
        var button = Button(d);
        rig.Hover(button);
        button.Classes.ShouldContain(AiReasoningButton.HoverPseudoClass);
        TestHelpers.ColorOf(button.Foreground).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.Foreground));
        TestHelpers.ColorOf(button.Background).A.ShouldBe((byte)0);
        rig.Window.MouseMove(new Point(600, 500), RawInputModifiers.None);
        rig.Settle();
        TestHelpers.ColorOf(button.Foreground).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.TextSecondary));
        d.IsExpanded = true;
        rig.Settle();
        TestHelpers.ColorOf(button.Foreground).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.Foreground));
        var chevron = TestHelpers.Descendant<ArcGlyph>(button, "PART_Chevron");
        chevron.RenderTransform.ShouldNotBeNull().Value.M12.ShouldBe(1, 0.001);
    }

    [AvaloniaFact]
    public void Full_Motion_Springs_The_Height_And_Fades_The_Paragraph_Then_Unmounts_It()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var d = Create();
        using var rig = new AiReplyRig(d, ArcMotionLevel.Full, clock);
        var body = Body(d);
        d.IsExpanded = true;
        rig.Settle();
        body.IsMounted.ShouldBeTrue();
        rig.Frames(50);
        var natural = TestHelpers.Descendant<Border>(d, "PART_Rule").Bounds.Height;
        body.DisplayedHeight.ShouldBeGreaterThan(0);
        body.DisplayedHeight.ShouldBeLessThan(natural);
        body.DisplayedOpacity.ShouldBeGreaterThan(0);
        body.DisplayedOpacity.ShouldBeLessThan(1);
        // While it moves the box clips; the 6px gap is there at once.
        body.Clip.ShouldNotBeNull();
        body.Bounds.Height.ShouldBe(6 + 2 + body.DisplayedHeight, 1);
        // The content is at full strength after 160ms; the spring settles in about half a second.
        rig.Frames(150);
        body.DisplayedOpacity.ShouldBe(1);
        rig.Frames(700);
        body.DisplayedHeight.ShouldBe(natural, 0.5);
        body.Clip.ShouldBeNull();

        d.IsExpanded = false;
        rig.Settle();
        rig.Frames(100);
        body.DisplayedOpacity.ShouldBe(0, 0.001);
        body.IsMounted.ShouldBeTrue();
        rig.Frames(900);
        body.IsMounted.ShouldBeFalse();
        d.Bounds.Height.ShouldBe(28);
    }

    [AvaloniaFact]
    public void Reopening_Mid_Close_Turns_Around_From_Where_It_Is()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var d = Create(expanded: true);
        using var rig = new AiReplyRig(d, ArcMotionLevel.Full, clock);
        var body = Body(d);
        var natural = body.DisplayedHeight;
        natural.ShouldBeGreaterThan(20);
        d.IsExpanded = false;
        rig.Settle();
        rig.Frames(100);
        var mid = body.DisplayedHeight;
        mid.ShouldBeLessThan(natural);
        mid.ShouldBeGreaterThan(0);
        d.IsExpanded = true;
        rig.Settle();
        rig.Frames(17);
        body.IsMounted.ShouldBeTrue();
        // Still near where it was (the spring keeps going from its place), and it ends open.
        body.DisplayedHeight.ShouldBe(mid, 6);
        rig.Frames(900);
        body.DisplayedHeight.ShouldBe(natural, 0.5);
        body.DisplayedOpacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Height_And_Only_Fades()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var d = Create();
        using var rig = new AiReplyRig(d, ArcMotionLevel.Reduced, clock);
        var body = Body(d);
        d.IsExpanded = true;
        rig.Settle();
        rig.Frames(17);
        var natural = TestHelpers.Descendant<Border>(d, "PART_Rule").Bounds.Height;
        body.DisplayedHeight.ShouldBe(natural, 0.01);
        body.DisplayedOpacity.ShouldBeLessThan(1);
        rig.Frames(120);
        body.DisplayedOpacity.ShouldBe(1);

        d.IsExpanded = false;
        rig.Settle();
        rig.Frames(50);
        // Fading out in place first.
        body.DisplayedHeight.ShouldBe(natural, 0.01);
        body.IsMounted.ShouldBeTrue();
        rig.Frames(120);
        body.IsMounted.ShouldBeFalse();
        // Reduced: the shimmer of a thinking label stands still.
        d.IsThinking = true;
        rig.Settle();
        rig.Frames(50);
        Button(d).Label.ShouldBeOfType<TextShimmer>().IsActive.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Off_Motion_Opens_And_Closes_At_Once()
    {
        var d = Create();
        using var rig = new AiReplyRig(d);
        d.IsExpanded = true;
        rig.Settle();
        var body = Body(d);
        body.IsMoving.ShouldBeFalse();
        body.DisplayedOpacity.ShouldBe(1);
        d.IsExpanded = false;
        rig.Settle();
        body.IsMounted.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Full_Motion_Width_Follows_The_New_Label_And_Settles_On_It()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var d = Create(thinking: true, reasoning: null);
        using var rig = new AiReplyRig(d, ArcMotionLevel.Full, clock);
        var size = TestHelpers.Descendant<SpringSizeDecorator>(Button(d), "PART_LabelSize");
        var before = size.Bounds.Width;
        d.IsThinking = false;
        d.ThoughtText = "Thought for 12s";
        rig.Settle();
        rig.Frames(50);
        size.IsMoving.ShouldBeTrue();
        size.Bounds.Width.ShouldBeGreaterThan(before);
        rig.Frames(700);
        size.IsMoving.ShouldBeFalse();
        var label = Button(d).Label.ShouldBeOfType<TextBlock>();
        size.Bounds.Width.ShouldBe(label.DesiredSize.Width, 0.6);
        // The old label is gone once it faded out (the swap removes it from a continuation).
        rig.Drain(() => TestHelpers.Descendant<ArcContentSwap>(Button(d), "PART_Label").GetVisualChildrenCount() == 1).ShouldBeTrue();
    }
}

file static class VisualCountExtensions
{
    public static int GetVisualChildrenCount(this Visual visual) => Avalonia.VisualTree.VisualExtensions.GetVisualChildren(visual).Count();
}
