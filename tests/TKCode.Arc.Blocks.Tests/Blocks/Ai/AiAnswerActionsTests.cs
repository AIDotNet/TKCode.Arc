using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiAnswerActions"/>: the toolbar's geometry and words, the model line beside or under it, Copy and the link
/// with their timed checks, the exclusive and retractable ratings (up at once, down after a reason), the answer-again menu,
/// the version pager, the busy rule and the way back from it, keyboard order, automation.
/// </summary>
public class AiAnswerActionsTests
{
    private static AiAnswerActions Bar(Action<AiAnswerActions>? configure = null) =>
        AiAnswerActionsSamples.Bar(b =>
        {
            b.ModelLine = AiAnswerActionsSamples.ModelLine();
            configure?.Invoke(b);
        });

    private static double X(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.X;

    private static double Y(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.Y;

    #region Geometry and words

    [AvaloniaFact]
    public void The_Toolbar_Is_36px_Circles_2px_Apart_Set_10px_Left()
    {
        using var rig = new AiAnswerActionsRig(Bar());
        var bar = (AiAnswerActions)rig.Content;
        var buttons = new Control[] { bar.CopyButton!, bar.RegenerateButton!, bar.GoodButton!, bar.BadButton!, bar.LinkButton! };
        buttons.ShouldAllBe(b => b.Bounds.Width == 36 && b.Bounds.Height == 36);
        for (var i = 0; i < buttons.Length; i++)
        {
            X(buttons[i], bar).ShouldBe(-10 + 38 * i, 0.01);
        }
        bar.Bounds.Height.ShouldBe(36);
        bar.CopyButton!.CornerRadius.ShouldBe(new CornerRadius(ArcTokens.Get<CornerRadius>(bar, ArcDesignTokenKind.RadiusPill).TopLeft));
        bar.CopyButton.ShownGlyph.ShouldNotBeNull().Size.ShouldBe(16);
        bar.CopyButton.ShownGlyph.Data.ShouldBeSameAs(ArcIcons.Copy);
        bar.RegenerateButton!.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.RotateCcw);
        bar.GoodButton!.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.ThumbsUp);
        bar.BadButton!.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.ThumbsDown);
        bar.LinkButton!.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.Link2);
        TestHelpers.ColorOf(bar.CopyButton.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.TextSecondary));
        bar.CopyButton.ShownGlyph.IsFilled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Names_And_Tooltips_Come_From_The_Owner_Tooltips_Above()
    {
        using var rig = new AiAnswerActionsRig(Bar());
        var bar = (AiAnswerActions)rig.Content;
        AutomationProperties.GetName(bar.CopyButton!).ShouldBe("Copy answer");
        AutomationProperties.GetName(bar.RegenerateButton!).ShouldBe("Answer again");
        AutomationProperties.GetName(bar.GoodButton!).ShouldBe("Good answer");
        AutomationProperties.GetName(bar.BadButton!).ShouldBe("Bad answer");
        AutomationProperties.GetName(bar.LinkButton!).ShouldBe("Copy link to this answer");
        Tooltip.GetTip(bar.RegenerateButton!).ShouldBe("Answer again");
        Tooltip.GetSide(bar.CopyButton!).ShouldBe(TooltipSide.Top);
        AutomationProperties.GetName(bar.Toolbar!).ShouldBe("Answer actions");
        ControlAutomationPeer.CreatePeerForElement(bar.Toolbar!).GetAutomationControlType().ShouldBe(AutomationControlType.ToolBar);
        ControlAutomationPeer.CreatePeerForElement(bar.CopyButton!).GetChildren().ShouldBeEmpty();
        bar.CopyText = "Copy";
        rig.Settle();
        AutomationProperties.GetName(bar.CopyButton!).ShouldBe("Copy");
        Tooltip.GetTip(bar.CopyButton!).ShouldBe("Copy");
    }

    [AvaloniaFact]
    public void Hover_Fills_SurfaceMuted_And_Turns_The_Glyph_Foreground()
    {
        using var rig = new AiAnswerActionsRig(Bar());
        var bar = (AiAnswerActions)rig.Content;
        var copy = bar.CopyButton!;
        TestHelpers.ColorOf(copy.Background).A.ShouldBe((byte)0);
        rig.Hover(copy);
        copy.Classes.ShouldContain(AiAnswerActionButton.HoverPseudoClass);
        TestHelpers.ColorOf(copy.Background).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(copy.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.Foreground));
        rig.MoveTo(new Point(2, 2));
        copy.Classes.ShouldNotContain(AiAnswerActionButton.HoverPseudoClass);
    }

    [AvaloniaFact]
    public void A_Press_Darkens_The_Fill_With_Half_Border()
    {
        using var rig = new AiAnswerActionsRig(Bar());
        var bar = (AiAnswerActions)rig.Content;
        var copy = bar.CopyButton!;
        var press = TestHelpers.Descendant<Border>(copy, "PART_Press");
        press.Opacity.ShouldBe(0);
        TestHelpers.ColorOf(press.Background).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.Border));
        var point = rig.CenterOf(copy);
        rig.Window.MouseMove(point, RawInputModifiers.None);
        rig.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        rig.Settle();
        press.Opacity.ShouldBe(0.5);
        TestHelpers.ColorOf(copy.Background).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.SurfaceMuted));
        rig.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        rig.Settle();
        press.Opacity.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Model_Line_Sits_Right_On_The_Row_Or_Moves_4px_Under_The_Toolbar()
    {
        using (var rig = new AiAnswerActionsRig(Bar()))
        {
            var bar = (AiAnswerActions)rig.Content;
            var line = bar.ModelLine!;
            bar.LayoutPanel!.IsWrapped.ShouldBeFalse();
            (X(line, bar) + line.Bounds.Width).ShouldBe(bar.Bounds.Width, 0.5);
            (Y(line, bar) + line.Bounds.Height / 2).ShouldBe(18, 0.5);
            bar.Bounds.Height.ShouldBe(36);
        }
        using (var narrow = new AiAnswerActionsRig(Bar(), width: 300))
        {
            var bar = (AiAnswerActions)narrow.Content;
            var line = bar.ModelLine!;
            bar.LayoutPanel!.IsWrapped.ShouldBeTrue();
            X(line, bar).ShouldBe(0, 0.01);
            Y(line, bar).ShouldBe(36 + 4, 0.01);
            bar.Bounds.Height.ShouldBe(40 + line.Bounds.Height, 0.01);
        }
    }

    [AvaloniaFact]
    public void Without_A_Model_Line_The_Toolbar_Stands_Alone()
    {
        using var rig = new AiAnswerActionsRig(AiAnswerActionsSamples.Bar());
        var bar = (AiAnswerActions)rig.Content;
        bar.ModelLine.ShouldBeNull();
        bar.LayoutPanel!.IsWrapped.ShouldBeFalse();
        bar.Bounds.Height.ShouldBe(36);
    }

    #endregion

    #region Copy and link

    [AvaloniaFact]
    public void Copy_Asks_The_Owner_Then_Shows_A_Check_For_1_6s()
    {
        var time = new AiAnswerFakeTime();
        var bar = Bar(b => b.Time = time);
        var events = new List<string>();
        bar.CopyRequested += (_, _) => events.Add("copy");
        bar.Copied += (_, _) => events.Add("copied");
        using var rig = new AiAnswerActionsRig(bar);
        rig.Click(bar.CopyButton!);
        events.ShouldBe(["copy", "copied"]);
        bar.IsCopied.ShouldBeTrue();
        bar.Classes.ShouldContain(AiAnswerActions.CopiedPseudoClass);
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
        AutomationProperties.GetName(bar.CopyButton).ShouldBe("Copy answer");
        events.Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public void The_Link_Shows_Only_With_Share_And_Its_Check_Waits_For_The_Owner()
    {
        var time = new AiAnswerFakeTime();
        var bar = Bar(b =>
        {
            b.Time = time;
            b.HasShare = false;
        });
        var requests = 0;
        bar.LinkRequested += (_, _) => requests++;
        using var rig = new AiAnswerActionsRig(bar);
        bar.LinkButton!.IsVisible.ShouldBeFalse();
        // The pager and the toolbar close up: nothing takes the hidden button's place.
        bar.Toolbar!.Bounds.Width.ShouldBe(4 * 36 + 3 * 2, 0.01);

        bar.HasShare = true;
        rig.Settle();
        bar.LinkButton.IsVisible.ShouldBeTrue();
        rig.Click(bar.LinkButton);
        requests.ShouldBe(1);
        bar.IsLinkCopied.ShouldBeFalse();
        bar.ShowLinkCopied();
        bar.IsLinkCopied.ShouldBeTrue();
        bar.LinkButton.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.Check);
        AutomationProperties.GetName(bar.LinkButton).ShouldBe("Link copied");
        time.Now += TimeSpan.FromSeconds(1.6);
        bar.Tick();
        bar.IsLinkCopied.ShouldBeFalse();
        bar.LinkButton.ShownGlyph!.Data.ShouldBeSameAs(ArcIcons.Link2);

        bar.ShowLinkCopied();
        bar.ClearLinkCopied();
        bar.IsLinkCopied.ShouldBeFalse();
        AutomationProperties.GetName(bar.LinkButton).ShouldBe("Copy link to this answer");
    }

    [AvaloniaFact]
    public void Copy_And_Link_Checks_Run_On_Their_Own_Times()
    {
        var time = new AiAnswerFakeTime();
        var bar = Bar(b => b.Time = time);
        using var rig = new AiAnswerActionsRig(bar);
        rig.Click(bar.CopyButton!);
        time.Now += TimeSpan.FromSeconds(1);
        bar.ShowLinkCopied();
        time.Now += TimeSpan.FromSeconds(0.6);
        bar.Tick();
        bar.IsCopied.ShouldBeFalse();
        bar.IsLinkCopied.ShouldBeTrue();
        time.Now += TimeSpan.FromSeconds(1);
        bar.Tick();
        bar.IsLinkCopied.ShouldBeFalse();
    }

    #endregion

    #region Ratings

    [AvaloniaFact]
    public void Good_Rates_At_Once_Fills_And_Offers_To_Take_It_Back()
    {
        var bar = Bar();
        var events = new List<AiAnswerActionsFeedbackEventArgs>();
        bar.FeedbackChanged += (_, e) => events.Add(e);
        using var rig = new AiAnswerActionsRig(bar);
        rig.Click(bar.GoodButton!);
        bar.Feedback.ShouldBe(AiAnswerFeedbackValue.Up);
        events.Count.ShouldBe(1);
        events[0].Value.ShouldBe(AiAnswerFeedbackValue.Up);
        events[0].Previous.ShouldBe(AiAnswerFeedbackValue.None);
        events[0].Reason.ShouldBeNull();
        bar.GoodButton!.IsPressedOn.ShouldBeTrue();
        bar.GoodButton.Classes.ShouldContain(AiAnswerRatingButton.OnPseudoClass);
        bar.GoodButton.ShownGlyph!.IsFilled.ShouldBeTrue();
        TestHelpers.ColorOf(bar.GoodButton.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.Foreground));
        Tooltip.GetTip(bar.GoodButton).ShouldBe("Remove feedback");
        AutomationProperties.GetName(bar.GoodButton).ShouldBe("Good answer");

        // Pressing it again takes it back.
        rig.Click(bar.GoodButton);
        bar.Feedback.ShouldBe(AiAnswerFeedbackValue.None);
        events.Count.ShouldBe(2);
        events[1].Value.ShouldBe(AiAnswerFeedbackValue.None);
        events[1].Previous.ShouldBe(AiAnswerFeedbackValue.Up);
        bar.GoodButton.ShownGlyph!.IsFilled.ShouldBeFalse();
        Tooltip.GetTip(bar.GoodButton).ShouldBe("Good answer");
    }

    [AvaloniaFact]
    public void Bad_Asks_Why_First_And_Rates_Down_When_A_Reason_Is_Chosen()
    {
        var bar = Bar(b => b.Feedback = AiAnswerFeedbackValue.Up);
        var events = new List<AiAnswerActionsFeedbackEventArgs>();
        bar.FeedbackChanged += (_, e) => events.Add(e);
        using var rig = new AiAnswerActionsRig(bar);
        rig.Click(bar.BadButton!);
        var picker = bar.ReasonPicker!;
        picker.IsOpen.ShouldBeTrue();
        bar.BadButton!.IsExpanded.ShouldBeTrue();
        bar.BadButton.Classes.ShouldContain(AiAnswerActionButton.ExpandedPseudoClass);
        // Nothing is rated yet: the previous rating stands.
        events.ShouldBeEmpty();
        bar.Feedback.ShouldBe(AiAnswerFeedbackValue.Up);

        rig.Click(picker.ReasonButtons[3]);
        events.Count.ShouldBe(1);
        events[0].Value.ShouldBe(AiAnswerFeedbackValue.Down);
        events[0].Reason.ShouldBe("Too long");
        events[0].Previous.ShouldBe(AiAnswerFeedbackValue.Up);
        bar.Feedback.ShouldBe(AiAnswerFeedbackValue.Down);
        bar.GoodButton!.IsPressedOn.ShouldBeFalse();
        bar.BadButton.IsPressedOn.ShouldBeTrue();
        Tooltip.GetTip(bar.BadButton).ShouldBe("Remove feedback");
        picker.IsConfirming.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Closing_The_Reasons_Without_One_Changes_Nothing()
    {
        var bar = Bar(b => b.Feedback = AiAnswerFeedbackValue.Up);
        var events = 0;
        bar.FeedbackChanged += (_, _) => events++;
        using var rig = new AiAnswerActionsRig(bar);
        bar.BadButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        bar.ReasonPicker!.IsOpen.ShouldBeTrue();
        rig.Key(PhysicalKey.Escape);
        bar.ReasonPicker.IsOpen.ShouldBeFalse();
        bar.BadButton.IsExpanded.ShouldBeFalse();
        events.ShouldBe(0);
        bar.Feedback.ShouldBe(AiAnswerFeedbackValue.Up);
        bar.BadButton.IsFocused.ShouldBeTrue();

        // A press on the thumbs down again closes it the same way.
        rig.Click(bar.BadButton);
        bar.ReasonPicker.IsOpen.ShouldBeTrue();
        rig.Click(bar.BadButton);
        bar.ReasonPicker.IsOpen.ShouldBeFalse();
        events.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Pressing_A_Chosen_Down_Takes_It_Back_Without_Asking()
    {
        var bar = Bar(b => b.Feedback = AiAnswerFeedbackValue.Down);
        var events = new List<AiAnswerActionsFeedbackEventArgs>();
        bar.FeedbackChanged += (_, e) => events.Add(e);
        using var rig = new AiAnswerActionsRig(bar);
        bar.BadButton!.IsPressedOn.ShouldBeTrue();
        rig.Click(bar.BadButton);
        bar.ReasonPicker!.IsOpen.ShouldBeFalse();
        bar.Feedback.ShouldBe(AiAnswerFeedbackValue.None);
        events.Single().Value.ShouldBe(AiAnswerFeedbackValue.None);
        events.Single().Previous.ShouldBe(AiAnswerFeedbackValue.Down);
    }

    [AvaloniaFact]
    public void Without_Reasons_Bad_Rates_At_Once()
    {
        var bar = Bar(b => b.FeedbackReasons = null);
        var events = new List<AiAnswerActionsFeedbackEventArgs>();
        bar.FeedbackChanged += (_, e) => events.Add(e);
        using var rig = new AiAnswerActionsRig(bar);
        rig.Click(bar.BadButton!);
        bar.ReasonPicker!.IsOpen.ShouldBeFalse();
        bar.Feedback.ShouldBe(AiAnswerFeedbackValue.Down);
        events.Single().Reason.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Feedback_Set_By_The_Owner_Shows_Without_An_Event()
    {
        var bar = Bar();
        var events = 0;
        bar.FeedbackChanged += (_, _) => events++;
        using var rig = new AiAnswerActionsRig(bar);
        bar.Feedback = AiAnswerFeedbackValue.Down;
        rig.Settle();
        bar.BadButton!.IsPressedOn.ShouldBeTrue();
        bar.GoodButton!.IsPressedOn.ShouldBeFalse();
        bar.Feedback = AiAnswerFeedbackValue.Up;
        rig.Settle();
        bar.BadButton.IsPressedOn.ShouldBeFalse();
        bar.GoodButton.IsPressedOn.ShouldBeTrue();
        events.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Ratings_Are_Toggles_And_The_Popup_Buttons_Expand()
    {
        using var rig = new AiAnswerActionsRig(Bar());
        var bar = (AiAnswerActions)rig.Content;
        var good = ControlAutomationPeer.CreatePeerForElement(bar.GoodButton!);
        good.GetProvider<IToggleProvider>().ShouldNotBeNull().ToggleState.ShouldBe(ToggleState.Off);
        good.GetProvider<IExpandCollapseProvider>().ShouldBeNull();
        good.GetProvider<IToggleProvider>()!.Toggle();
        rig.Settle();
        bar.Feedback.ShouldBe(AiAnswerFeedbackValue.Up);
        good.GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.On);

        var bad = ControlAutomationPeer.CreatePeerForElement(bar.BadButton!);
        bad.GetProvider<IToggleProvider>().ShouldNotBeNull();
        var badExpand = bad.GetProvider<IExpandCollapseProvider>().ShouldNotBeNull();
        badExpand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        badExpand.Expand();
        rig.Settle();
        bar.ReasonPicker!.IsOpen.ShouldBeTrue();
        badExpand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        badExpand.Collapse();
        rig.Settle();
        bar.ReasonPicker.IsOpen.ShouldBeFalse();

        var regenerate = ControlAutomationPeer.CreatePeerForElement(bar.RegenerateButton!);
        regenerate.GetProvider<IToggleProvider>().ShouldBeNull();
        var menuExpand = regenerate.GetProvider<IExpandCollapseProvider>().ShouldNotBeNull();
        menuExpand.Expand();
        rig.Settle();
        bar.Menu!.IsOpen.ShouldBeTrue();
        menuExpand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        menuExpand.Collapse();
        rig.Settle();
        bar.Menu.IsOpen.ShouldBeFalse();
        ControlAutomationPeer.CreatePeerForElement(bar.CopyButton!).GetProvider<IExpandCollapseProvider>().ShouldBeNull();
    }

    #endregion

    #region Answer again

    [AvaloniaFact]
    public void Answer_Again_Opens_On_Press_And_Reports_The_Mode()
    {
        var bar = Bar();
        var chosen = new List<AiRegenerateChosenEventArgs>();
        bar.RegenerateChosen += (_, e) => chosen.Add(e);
        using var rig = new AiAnswerActionsRig(bar);
        var point = rig.CenterOf(bar.RegenerateButton!);
        rig.Window.MouseMove(point, RawInputModifiers.None);
        rig.Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        rig.Settle();
        // Radix opens a menu on the press, not the release.
        bar.Menu!.IsOpen.ShouldBeTrue();
        bar.RegenerateButton!.IsExpanded.ShouldBeTrue();
        rig.Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        rig.Settle();
        bar.Menu.IsOpen.ShouldBeTrue();
        // A second press on the button closes it; a third opens it again.
        rig.Click(bar.RegenerateButton);
        bar.Menu.IsOpen.ShouldBeFalse();
        rig.Click(bar.RegenerateButton);
        bar.Menu.IsOpen.ShouldBeTrue();
        rig.Click(bar.Menu.Rows[1]);
        bar.Menu.IsOpen.ShouldBeFalse();
        bar.RegenerateButton.IsExpanded.ShouldBeFalse();
        chosen.Single().Mode.ShouldBe(AiAnswerMode.Concise);
        chosen.Single().Model.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Switch_Model_Reports_The_Model()
    {
        var bar = Bar();
        var chosen = new List<AiRegenerateChosenEventArgs>();
        bar.RegenerateChosen += (_, e) => chosen.Add(e);
        using var rig = new AiAnswerActionsRig(bar);
        bar.RegenerateButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.ArrowDown);
        bar.Menu!.IsOpen.ShouldBeTrue();
        bar.Menu.HighlightedRow.ShouldBeSameAs(bar.Menu.Rows[0]);
        rig.Key(PhysicalKey.End);
        rig.Key(PhysicalKey.ArrowRight);
        bar.Menu.IsSubmenuOpen.ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.Enter);
        chosen.Single().Mode.ShouldBe(AiAnswerMode.SwitchModel);
        chosen.Single().Model!.Name.ShouldBe("Lumen Mini 4");
        bar.RegenerateButton.IsFocused.ShouldBeTrue();
        bar.RegenerateButton.Classes.ShouldContain(":focus-visible");
    }

    [AvaloniaFact]
    public void One_Popup_At_A_Time()
    {
        using var rig = new AiAnswerActionsRig(Bar());
        var bar = (AiAnswerActions)rig.Content;
        rig.Click(bar.BadButton!);
        bar.ReasonPicker!.IsOpen.ShouldBeTrue();
        rig.Click(bar.RegenerateButton!);
        bar.ReasonPicker.IsOpen.ShouldBeFalse();
        bar.Menu!.IsOpen.ShouldBeTrue();
        rig.Click(bar.BadButton!);
        bar.Menu.IsOpen.ShouldBeFalse();
        bar.ReasonPicker.IsOpen.ShouldBeTrue();
    }

    #endregion

    #region Versions

    [AvaloniaFact]
    public void The_Pager_Shows_From_Two_Versions_Behind_A_Line()
    {
        var bar = Bar(b => b.VersionCount = 1);
        var moves = new List<AiVersionPagerEventArgs>();
        bar.VersionChanged += (_, e) => moves.Add(e);
        using var rig = new AiAnswerActionsRig(bar);
        bar.Pager!.IsVisible.ShouldBeFalse();
        bar.VersionCount = 2;
        bar.VersionIndex = 1;
        rig.Settle();
        var pager = bar.Pager;
        pager.IsVisible.ShouldBeTrue();
        // 8px after the link button, the 1 × 16 line, 6px, then the 30px arrows.
        var divider = TestHelpers.Descendant<Border>(pager, "PART_Divider");
        divider.Bounds.Size.ShouldBe(new Size(1, 16));
        (X(divider, bar) - (X(bar.LinkButton!, bar) + 36 + 2)).ShouldBe(8, 0.01);
        pager.PreviousButton!.Bounds.Size.ShouldBe(new Size(30, 30));
        (X(pager.PreviousButton, bar) - X(divider, bar) - 1).ShouldBe(6, 0.01);
        AutomationProperties.GetName(pager).ShouldBe("Version 2 of 2");
        pager.NextButton!.IsEffectivelyEnabled.ShouldBeFalse();
        pager.NextButton.Opacity.ShouldBe(0.5);

        rig.Click(pager.PreviousButton);
        bar.VersionIndex.ShouldBe(0);
        moves.Single().Index.ShouldBe(0);
        moves.Single().Count.ShouldBe(2);
        AutomationProperties.GetName(pager).ShouldBe("Version 1 of 2");

        // The owner moves it too (no event for that).
        bar.VersionIndex = 1;
        rig.Settle();
        pager.Index.ShouldBe(1);
        moves.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Another_Version_Closes_Open_Popups()
    {
        using var rig = new AiAnswerActionsRig(Bar(b =>
        {
            b.VersionCount = 2;
            b.VersionIndex = 1;
        }));
        var bar = (AiAnswerActions)rig.Content;
        rig.Click(bar.BadButton!);
        bar.ReasonPicker!.IsOpen.ShouldBeTrue();
        bar.VersionIndex = 0;
        rig.Settle();
        bar.ReasonPicker.IsOpen.ShouldBeFalse();
    }

    #endregion

    #region Busy

    [AvaloniaFact]
    public void Busy_Keeps_The_Height_Shows_Nothing_And_Takes_No_Input()
    {
        var bar = Bar(b => b.VersionCount = 2);
        var events = 0;
        bar.CopyRequested += (_, _) => events++;
        bar.FeedbackChanged += (_, _) => events++;
        using var rig = new AiAnswerActionsRig(bar);
        rig.Click(bar.RegenerateButton!);
        bar.Menu!.IsOpen.ShouldBeTrue();
        var height = bar.Bounds.Height;

        bar.IsBusy = true;
        rig.Settle();
        bar.Classes.ShouldContain(AiAnswerActions.BusyPseudoClass);
        bar.Menu.IsOpen.ShouldBeFalse();
        bar.LayoutPanel!.Opacity.ShouldBe(0);
        bar.LayoutPanel.IsHitTestVisible.ShouldBeFalse();
        KeyboardNavigation.GetTabNavigation(bar.LayoutPanel).ShouldBe(KeyboardNavigationMode.None);
        bar.Bounds.Height.ShouldBe(height);
        // Nothing to Tab into: from the button before, focus goes straight past the bar.
        rig.Before.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.Focused.ShouldBeSameAs(rig.After);
        // A click reaches nothing, and an action that still has focus does nothing.
        rig.Click(bar.CopyButton!);
        bar.CopyButton!.Focus();
        rig.Key(PhysicalKey.Enter);
        events.ShouldBe(0);

        bar.IsBusy = false;
        rig.Settle();
        bar.LayoutPanel.Opacity.ShouldBe(1);
        bar.LayoutPanel.IsHitTestVisible.ShouldBeTrue();
        rig.Before.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.Focused.ShouldBeSameAs(bar.CopyButton);
    }

    [AvaloniaFact]
    public void Back_From_Busy_Fades_In_150ms_Rising_4px_In_200ms()
    {
        var clock = new ManualFrameClock();
        var bar = Bar(b => b.IsBusy = true);
        using var rig = new AiAnswerActionsRig(bar, ArcMotionLevel.Full, clock);
        var layout = bar.LayoutPanel!;
        layout.Opacity.ShouldBe(0);
        bar.IsBusy = false;
        rig.Settle();
        var rise = (Avalonia.Media.TranslateTransform)layout.RenderTransform!;
        layout.Opacity.ShouldBeLessThan(0.05);
        rise.Y.ShouldBe(4, 0.01);
        rig.Frames(17);
        layout.Opacity.ShouldBeInRange(0.05, 0.95);
        rise.Y.ShouldBeInRange(0.5, 3.95);
        rig.Frames(150);
        layout.Opacity.ShouldBe(1, 0.001);
        rig.Frames(50);
        rise.Y.ShouldBe(0, 0.001);

        // Busy again: gone at once.
        bar.IsBusy = true;
        rig.Settle();
        layout.Opacity.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Back_In_120ms_Without_Rising()
    {
        var clock = new ManualFrameClock();
        var bar = Bar(b => b.IsBusy = true);
        using var rig = new AiAnswerActionsRig(bar, ArcMotionLevel.Reduced, clock);
        bar.IsBusy = false;
        rig.Settle();
        var rise = (Avalonia.Media.TranslateTransform)bar.LayoutPanel!.RenderTransform!;
        rise.Y.ShouldBe(0);
        rig.Frames(50);
        bar.LayoutPanel.Opacity.ShouldBeInRange(0.2, 0.7);
        rig.Frames(84);
        bar.LayoutPanel.Opacity.ShouldBe(1, 0.001);
        rise.Y.ShouldBe(0);
    }

    #endregion

    #region Keyboard

    [AvaloniaFact]
    public void Every_Action_Is_Its_Own_Tab_Stop_In_Order()
    {
        using var rig = new AiAnswerActionsRig(Bar(b =>
        {
            b.VersionCount = 3;
            b.VersionIndex = 1;
        }));
        var bar = (AiAnswerActions)rig.Content;
        rig.Before.Focus(NavigationMethod.Tab);
        var order = new List<IInputElement?>();
        for (var i = 0; i < 8; i++)
        {
            rig.Key(PhysicalKey.Tab);
            order.Add(rig.Focused);
        }
        order.ShouldBe(new IInputElement?[]
        {
            bar.CopyButton, bar.RegenerateButton, bar.GoodButton, bar.BadButton, bar.LinkButton, bar.Pager!.PreviousButton,
            bar.Pager.NextButton, rig.After
        });
        // The keyboard ring shows on the focused action.
        rig.Before.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        TestHelpers.Descendant<Border>(bar.CopyButton!, "PART_FocusRing").IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<Border>(bar.CopyButton!, "PART_FocusRing").Margin.ShouldBe(new Thickness(-4));
    }

    [AvaloniaFact]
    public void Escape_From_The_Menu_Returns_Focus_With_Its_Ring()
    {
        using var rig = new AiAnswerActionsRig(Bar());
        var bar = (AiAnswerActions)rig.Content;
        bar.RegenerateButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        bar.Menu!.IsOpen.ShouldBeTrue();
        bar.Menu.HighlightedRow.ShouldBeSameAs(bar.Menu.Rows[0]);
        bar.Menu.Rows[0].IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.Escape);
        bar.Menu.IsOpen.ShouldBeFalse();
        bar.RegenerateButton.IsFocused.ShouldBeTrue();
        bar.RegenerateButton.Classes.ShouldContain(":focus-visible");
    }

    #endregion
}
