using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiMessageActions"/>: geometry, the reveal rules (last, revealed, touch, streaming) and what hidden means, Copy and
/// its timed check, Retry, the exclusive and retractable ratings with their filled glyphs and toggle automation, the words,
/// and the pager slot.
/// </summary>
public class AiMessageActionsTests
{
    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static AiMessageActions Bar(Action<AiMessageActions>? configure = null)
    {
        var bar = new AiMessageActions
        {
            CopyText = "Copy",
            CopiedText = "Copied",
            RetryText = "Retry",
            GoodText = "Good response",
            BadText = "Bad response"
        };
        configure?.Invoke(bar);
        return bar;
    }

    private static AiVersionPager Pager() => new()
    {
        Count = 2,
        Index = 1,
        GroupName = (n, c) => $"Version {n} of {c}",
        PreviousText = "Previous version",
        NextText = "Next version"
    };

    private static StackPanel Group(AiMessageActions bar) => TestHelpers.Descendant<StackPanel>(bar, "PART_Group");

    [AvaloniaFact]
    public void Four_30px_Actions_Without_Gaps_Set_6px_Left_And_2px_Up()
    {
        using var rig = new AiReplyRig(Bar(b => b.IsLast = true));
        var bar = (AiMessageActions)rig.Content;
        bar.Margin.ShouldBe(new Thickness(-6, -2, 0, 0));
        var buttons = new Control[] { bar.CopyButton!, bar.RetryButton!, bar.GoodButton!, bar.BadButton! };
        buttons.ShouldAllBe(b => b.Bounds.Width == 30 && b.Bounds.Height == 30);
        for (var i = 1; i < buttons.Length; i++)
        {
            buttons[i].TranslatePoint(default, bar)!.Value.X.ShouldBe(30 * i, 0.01);
        }
        bar.Bounds.Height.ShouldBe(30);
        bar.CopyButton!.ShownGlyph.ShouldNotBeNull().Size.ShouldBe(16);
        bar.CopyButton.ShownGlyph.Data.ShouldBeSameAs(ArcIcons.Copy);
        bar.RetryButton!.ShownGlyph.ShouldNotBeNull().Size.ShouldBe(15);
        bar.RetryButton.ShownGlyph.Data.ShouldBeSameAs(ArcIcons.RotateCcw);
        bar.GoodButton!.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.ThumbsUp);
        bar.BadButton!.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.ThumbsDown);
        bar.CopyButton.CornerRadius.ShouldBe(new CornerRadius(10));
        TestHelpers.ColorOf(bar.RetryButton.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.TextMuted));
    }

    [AvaloniaFact]
    public void Names_And_Tooltips_Come_From_The_Owner_Tooltips_Below()
    {
        using var rig = new AiReplyRig(Bar(b => b.IsLast = true));
        var bar = (AiMessageActions)rig.Content;
        AutomationProperties.GetName(bar.CopyButton!).ShouldBe("Copy");
        AutomationProperties.GetName(bar.RetryButton!).ShouldBe("Retry");
        AutomationProperties.GetName(bar.GoodButton!).ShouldBe("Good response");
        AutomationProperties.GetName(bar.BadButton!).ShouldBe("Bad response");
        Tooltip.GetTip(bar.RetryButton!).ShouldBe("Retry");
        Tooltip.GetSide(bar.RetryButton!).ShouldBe(TooltipSide.Bottom);
        bar.RetryText = "Try again";
        rig.Settle();
        AutomationProperties.GetName(bar.RetryButton!).ShouldBe("Try again");
        ControlAutomationPeer.CreatePeerForElement(bar.RetryButton!).GetChildren().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Hidden_Actions_Keep_Their_Height_And_Leave_The_Tab_Order()
    {
        using var rig = new AiReplyRig(Bar());
        var bar = (AiMessageActions)rig.Content;
        bar.IsShown.ShouldBeFalse();
        bar.Bounds.Height.ShouldBe(30);
        Group(bar).Opacity.ShouldBe(0);
        bar.CopyButton!.IsHitTestVisible.ShouldBeFalse();
        KeyboardNavigation.GetIsTabStop(bar.CopyButton).ShouldBeFalse();
        KeyboardNavigation.GetIsTabStop(bar.BadButton!).ShouldBeFalse();

        bar.IsRevealed = true;
        rig.Settle();
        Group(bar).Opacity.ShouldBe(1);
        bar.CopyButton.IsHitTestVisible.ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(bar.CopyButton).ShouldBeTrue();
        bar.IsRevealed = false;
        bar.AlwaysVisible = true;
        rig.Settle();
        Group(bar).Opacity.ShouldBe(1);
        bar.AlwaysVisible = false;
        bar.IsLast = true;
        rig.Settle();
        Group(bar).Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Streaming_Hides_The_Whole_Bar_In_Place_Even_When_Last()
    {
        using var rig = new AiReplyRig(Bar(b =>
        {
            b.IsLast = true;
            b.IsStreaming = true;
            b.VersionPager = Pager();
        }));
        var bar = (AiMessageActions)rig.Content;
        bar.IsShown.ShouldBeFalse();
        TestHelpers.Descendant<StackPanel>(bar, "PART_Bar").Opacity.ShouldBe(0);
        bar.Bounds.Height.ShouldBe(30);
        bar.VersionPager!.IsHitTestVisible.ShouldBeFalse();
        KeyboardNavigation.GetIsTabStop(bar.CopyButton!).ShouldBeFalse();
        bar.IsStreaming = false;
        rig.Settle();
        TestHelpers.Descendant<StackPanel>(bar, "PART_Bar").Opacity.ShouldBe(1);
        Group(bar).Opacity.ShouldBe(1);
        bar.VersionPager.IsHitTestVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Pager_Comes_First_And_Stays_When_The_Actions_Hide()
    {
        var pager = Pager();
        using var rig = new AiReplyRig(Bar(b => b.VersionPager = pager));
        var bar = (AiMessageActions)rig.Content;
        pager.IsVisible.ShouldBeTrue();
        pager.TranslatePoint(default, bar)!.Value.X.ShouldBe(0, 0.01);
        // 4px after the pager's own margin of 4, then the actions.
        bar.CopyButton!.TranslatePoint(default, bar)!.Value.X.ShouldBe(pager.Bounds.Width + 4 + 4, 0.01);
        Group(bar).Opacity.ShouldBe(0);
        TestHelpers.Descendant<StackPanel>(bar, "PART_Bar").Opacity.ShouldBe(1);
        pager.IsHitTestVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Copy_Asks_The_Owner_Shows_A_Check_For_1_6s_Then_Turns_Back()
    {
        var time = new FakeTime();
        var bar = Bar(b => b.IsLast = true);
        bar.Time = time;
        var events = new List<string>();
        bar.CopyRequested += (_, _) => events.Add("copy");
        bar.Copied += (_, _) => events.Add("copied");
        using var rig = new AiReplyRig(bar);
        rig.Click(bar.CopyButton!);
        events.ShouldBe(["copy", "copied"]);
        bar.IsCopied.ShouldBeTrue();
        bar.Classes.ShouldContain(AiMessageActions.CopiedPseudoClass);
        bar.CopyButton!.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.Check);
        AutomationProperties.GetName(bar.CopyButton).ShouldBe("Copied");

        time.Now += TimeSpan.FromMilliseconds(1500);
        bar.Tick();
        bar.IsCopied.ShouldBeTrue();
        // A second copy starts the time again.
        rig.Click(bar.CopyButton);
        time.Now += TimeSpan.FromMilliseconds(1500);
        bar.Tick();
        bar.IsCopied.ShouldBeTrue();
        time.Now += TimeSpan.FromMilliseconds(100);
        bar.Tick();
        bar.IsCopied.ShouldBeFalse();
        rig.Settle();
        bar.CopyButton.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.Copy);
        AutomationProperties.GetName(bar.CopyButton).ShouldBe("Copy");
        events.Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Copied_Duration_Can_Change_And_Copied_Text_Falls_Back_To_Copy()
    {
        var time = new FakeTime();
        var bar = Bar(b =>
        {
            b.IsLast = true;
            b.CopiedText = null;
            b.CopiedDuration = TimeSpan.FromSeconds(3);
        });
        bar.Time = time;
        using var rig = new AiReplyRig(bar);
        bar.CopyButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        bar.IsCopied.ShouldBeTrue();
        AutomationProperties.GetName(bar.CopyButton).ShouldBe("Copy");
        time.Now += TimeSpan.FromSeconds(2);
        bar.Tick();
        bar.IsCopied.ShouldBeTrue();
        time.Now += TimeSpan.FromSeconds(1);
        bar.Tick();
        bar.IsCopied.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Retry_Raises_RetryRequested()
    {
        var bar = Bar(b => b.IsLast = true);
        var retries = 0;
        bar.RetryRequested += (_, _) => retries++;
        using var rig = new AiReplyRig(bar);
        rig.Click(bar.RetryButton!);
        retries.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Ratings_Are_Exclusive_A_Second_Press_Takes_Back_And_Fills_The_Glyph()
    {
        var bar = Bar(b => b.IsLast = true);
        var requests = new List<(AiChatFeedback Value, AiChatFeedback Previous)>();
        bar.FeedbackRequested += (_, e) => requests.Add((e.Value, e.Previous));
        using var rig = new AiReplyRig(bar);
        rig.Click(bar.GoodButton!);
        bar.Feedback.ShouldBe(AiChatFeedback.Up);
        bar.GoodButton!.IsPressedOn.ShouldBeTrue();
        bar.GoodButton.ShownGlyph!.IsFilled.ShouldBeTrue();
        TestHelpers.ColorOf(bar.GoodButton.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.Foreground));
        // Chosen means a filled glyph, not a fill behind it (once the pointer has left).
        rig.Window.MouseMove(new Point(700, 500), RawInputModifiers.None);
        rig.Settle();
        TestHelpers.ColorOf(bar.GoodButton.Background).A.ShouldBe((byte)0);
        TestHelpers.ColorOf(bar.GoodButton.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.Foreground));

        rig.Click(bar.BadButton!);
        bar.Feedback.ShouldBe(AiChatFeedback.Down);
        bar.GoodButton.IsPressedOn.ShouldBeFalse();
        bar.GoodButton.ShownGlyph!.IsFilled.ShouldBeFalse();
        bar.BadButton!.ShownGlyph!.IsFilled.ShouldBeTrue();

        rig.Click(bar.BadButton);
        bar.Feedback.ShouldBe(AiChatFeedback.None);
        bar.BadButton.IsPressedOn.ShouldBeFalse();
        requests.ShouldBe([(AiChatFeedback.Up, AiChatFeedback.None), (AiChatFeedback.Down, AiChatFeedback.Up), (AiChatFeedback.None, AiChatFeedback.Down)]);
    }

    [AvaloniaFact]
    public void Ratings_Follow_A_Feedback_Set_By_The_Owner_And_Report_Toggle_State()
    {
        using var rig = new AiReplyRig(Bar(b =>
        {
            b.IsLast = true;
            b.Feedback = AiChatFeedback.Down;
        }));
        var bar = (AiMessageActions)rig.Content;
        bar.BadButton!.IsPressedOn.ShouldBeTrue();
        var good = ControlAutomationPeer.CreatePeerForElement(bar.GoodButton!).GetProvider<IToggleProvider>().ShouldNotBeNull();
        var bad = ControlAutomationPeer.CreatePeerForElement(bar.BadButton).GetProvider<IToggleProvider>().ShouldNotBeNull();
        bad.ToggleState.ShouldBe(ToggleState.On);
        good.ToggleState.ShouldBe(ToggleState.Off);
        good.Toggle();
        rig.Settle();
        bar.Feedback.ShouldBe(AiChatFeedback.Up);
        good.ToggleState.ShouldBe(ToggleState.On);
        bad.ToggleState.ShouldBe(ToggleState.Off);
    }

    [AvaloniaFact]
    public void Hover_Fills_The_Button_Muted_With_A_Foreground_Glyph()
    {
        using var rig = new AiReplyRig(Bar(b => b.IsLast = true));
        var bar = (AiMessageActions)rig.Content;
        rig.Hover(bar.RetryButton!);
        bar.RetryButton!.Classes.ShouldContain(AiActionButton.HoverPseudoClass);
        TestHelpers.ColorOf(bar.RetryButton.Background).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(bar.RetryButton.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.Foreground));
    }

    [AvaloniaFact]
    public void Full_Motion_Fades_The_Actions_In_When_Revealed()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        using var rig = new AiReplyRig(Bar(), ArcMotionLevel.Full, clock);
        var bar = (AiMessageActions)rig.Content;
        bar.Classes.ShouldContain(ArcMotion.FullPseudoClass);
        var transitions = Group(bar).Transitions.ShouldNotBeNull();
        transitions.OfType<ArcDoubleTransition>().ShouldContain(t => t.Property == Visual.OpacityProperty && t.Duration == TimeSpan.FromMilliseconds(160));
        bar.CopyButton!.Transitions.ShouldNotBeNull().OfType<ArcTransformOperationsTransition>()
            .ShouldContain(t => t.Duration == ArcMotionPresets.CssSpring);
    }
}
