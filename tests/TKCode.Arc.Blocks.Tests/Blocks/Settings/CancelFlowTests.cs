using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Settings;

/// <summary>
/// The cancel flow: Continue waits for a reason, the steps follow the offers, accepting and confirming end the flow with
/// their events, Back and Reset return, and the keyboard model (Enter, Alt+Left, focus on the new title) holds.
/// </summary>
public class CancelFlowTests
{
    private static readonly CancelFlowOffer Discount = new()
    {
        Title = "Take 40% off for 3 months",
        Description = "Your next three invoices are $54 instead of $90.",
        AcceptLabel = "Claim 40% off",
        AcceptedTitle = "40% off is on"
    };

    private static CancelFlow Flow(bool withOffer = true)
    {
        var flow = new CancelFlow { PlanName = "Studio", AccessEndsOn = new DateTime(2026, 11, 1) };
        flow.Reasons.Add(new CancelFlowReason("Too expensive", "The price is more than it's worth.") { Offer = withOffer ? Discount : null });
        flow.Reasons.Add(new CancelFlowReason("Not using it enough"));
        flow.Reasons.Add(new CancelFlowReason("Missing a feature I need") { AsksForDetails = true, DetailsPrompt = "What were you missing?" });
        flow.Losses.Add("Shared projects become read-only");
        flow.Losses.Add("Custom domains stop working");
        return flow;
    }

    private static Window Show(CancelFlow flow)
    {
        var window = TestHelpers.Show(flow);
        window.Width = 640;
        window.Height = 900;
        Settle();
        return window;
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Part<T>(CancelFlow flow, string name) where T : Control => TestHelpers.Descendant<T>(flow, name);

    private static bool HasPart(CancelFlow flow, string name) => flow.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name);

    private static void Click(CancelFlow flow, string name)
    {
        Part<AvaloniaButton>(flow, name).RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Settle();
    }

    private static void Choose(CancelFlow flow, int index)
    {
        flow.SelectedReason = flow.Reasons.ElementAt(index);
        Settle();
    }

    private static void Confirm(CancelFlow flow)
    {
        Part<HoldToConfirm>(flow, CancelFlow.HoldButtonPart).RaiseEvent(new RoutedEventArgs(HoldToConfirm.ConfirmedEvent));
        Settle();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Settle();
    }

    [AvaloniaFact]
    public void Continue_Is_Disabled_Until_A_Reason_Is_Chosen()
    {
        var flow = Flow();
        var window = Show(flow);
        try
        {
            flow.Step.ShouldBe(CancelFlowStep.Reason);
            var next = Part<AvaloniaButton>(flow, CancelFlow.ContinueButtonPart);
            next.IsEnabled.ShouldBeFalse();
            Part<SpringSizeDecorator>(flow, CancelFlow.FeedbackHostPart).IsExpanded.ShouldBeFalse();

            Choose(flow, 1);
            next.IsEnabled.ShouldBeTrue();
            Part<SpringSizeDecorator>(flow, CancelFlow.FeedbackHostPart).IsExpanded.ShouldBeTrue();
            Part<RadioGroup>(flow, CancelFlow.ReasonsPart).SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_A_Row_Sets_The_Reason_And_Clicking_Continue_Moves_On()
    {
        var flow = Flow(withOffer: false);
        var window = Show(flow);
        try
        {
            var group = Part<RadioGroup>(flow, CancelFlow.ReasonsPart);
            group.SelectedIndex = 0;
            flow.SelectedReason.ShouldBe(flow.Reasons.First());

            var next = Part<AvaloniaButton>(flow, CancelFlow.ContinueButtonPart);
            Settle();
            var point = next.TranslatePoint(new Point(next.Bounds.Width / 2, next.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
            flow.Step.ShouldBe(CancelFlowStep.Confirm);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Reason_With_An_Offer_Goes_Through_Three_Steps()
    {
        var flow = Flow();
        var steps = new List<CancelFlowStep>();
        flow.StepChanged += (_, e) => steps.Add(e.NewStep);
        var window = Show(flow);
        try
        {
            flow.StepCount.ShouldBe(3);
            Part<TextBlock>(flow, CancelFlow.StepCounterPart).Text.ShouldBe("Step 1 of 3");

            Choose(flow, 0);
            Click(flow, CancelFlow.ContinueButtonPart);
            flow.Step.ShouldBe(CancelFlowStep.Offer);
            Part<TextBlock>(flow, CancelFlow.OfferTitlePart).Text.ShouldBe(Discount.Title);
            Part<AvaloniaButton>(flow, CancelFlow.AcceptButtonPart).Content.ShouldBe("Claim 40% off");
            Part<TextBlock>(flow, CancelFlow.StepCounterPart).Text.ShouldBe("Step 2 of 3");
            Part<Border>(flow, CancelFlow.ReasonSegmentPart).Classes.ShouldContain("done");
            Part<Border>(flow, CancelFlow.OfferSegmentPart).Classes.ShouldContain("current");

            Click(flow, CancelFlow.DeclineButtonPart);
            flow.Step.ShouldBe(CancelFlowStep.Confirm);
            Part<TextBlock>(flow, CancelFlow.StepCounterPart).Text.ShouldBe("Step 3 of 3");
            Part<TextBlock>(flow, CancelFlow.ConfirmTitlePart).Text.ShouldBe("Cancel your Studio plan?");
            Part<TextBlock>(flow, CancelFlow.ConfirmDescriptionPart).Text.ShouldBe("You keep access until Nov 1, 2026. After that:");
            Part<ItemsControl>(flow, CancelFlow.LossesPart).ItemCount.ShouldBe(2);

            steps.ShouldBe([CancelFlowStep.Offer, CancelFlowStep.Confirm]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_An_Offer_The_Flow_Skips_To_The_Confirmation_With_Two_Segments()
    {
        var flow = Flow(withOffer: false);
        var window = Show(flow);
        try
        {
            flow.StepCount.ShouldBe(2);
            Part<Border>(flow, CancelFlow.OfferSegmentPart).IsVisible.ShouldBeFalse();
            Part<Grid>(flow, CancelFlow.StepBarPart).ColumnDefinitions.Count.ShouldBe(2);

            Choose(flow, 0);
            Click(flow, CancelFlow.ContinueButtonPart);
            flow.Step.ShouldBe(CancelFlowStep.Confirm);
            Part<TextBlock>(flow, CancelFlow.StepCounterPart).Text.ShouldBe("Step 2 of 2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Default_Offer_Applies_To_Reasons_Without_One()
    {
        var flow = Flow(withOffer: false);
        flow.DefaultOffer = new CancelFlowOffer { Title = "Talk to us first" };
        var window = Show(flow);
        try
        {
            Choose(flow, 1);
            Click(flow, CancelFlow.ContinueButtonPart);
            flow.Step.ShouldBe(CancelFlowStep.Offer);
            Part<TextBlock>(flow, CancelFlow.OfferTitlePart).Text.ShouldBe("Talk to us first");
            Part<AvaloniaButton>(flow, CancelFlow.AcceptButtonPart).Content.ShouldBe("Accept offer");
            // An offer without an icon shows the gift.
            Part<ArcGlyph>(flow, CancelFlow.OfferIconPart).Data.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Accepting_The_Offer_Retains_The_Plan_And_Reports_It()
    {
        var flow = Flow();
        CancelFlowOfferEventArgs? accepted = null;
        var kept = 0;
        flow.OfferAccepted += (_, e) => accepted = e;
        flow.KeepPlanRequested += (_, _) => kept++;
        var window = Show(flow);
        try
        {
            Choose(flow, 0);
            Click(flow, CancelFlow.ContinueButtonPart);
            Click(flow, CancelFlow.AcceptButtonPart);

            flow.Step.ShouldBe(CancelFlowStep.Retained);
            accepted.ShouldNotBeNull();
            accepted.Reason.ShouldBe(flow.Reasons.First());
            accepted.Offer.ShouldBe(Discount);
            Part<TextBlock>(flow, CancelFlow.ResultTitlePart).Text.ShouldBe("40% off is on");
            Part<AvaloniaButton>(flow, CancelFlow.DoneButtonPart).IsVisible.ShouldBeTrue();
            Part<AvaloniaButton>(flow, CancelFlow.ReactivateButtonPart).IsVisible.ShouldBeFalse();
            // The step bar is gone on the last steps.
            Part<PresenceHost>(flow, CancelFlow.HeaderHostPart).IsPresent.ShouldBeFalse();

            Click(flow, CancelFlow.DoneButtonPart);
            kept.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Holding_To_Confirm_Cancels_With_The_Reason_And_Feedback()
    {
        var flow = Flow(withOffer: false);
        CancelFlowCancelEventArgs? canceled = null;
        flow.CancellationConfirmed += (_, e) => canceled = e;
        var window = Show(flow);
        try
        {
            Choose(flow, 2);
            Part<Textarea>(flow, CancelFlow.FeedbackPart).Text = "Two-way calendar sync";
            flow.Feedback.ShouldBe("Two-way calendar sync");
            Click(flow, CancelFlow.ContinueButtonPart);
            var hold = Part<HoldToConfirm>(flow, CancelFlow.HoldButtonPart);
            hold.Label.ShouldBe("Hold to cancel plan");
            hold.Tone.ShouldBe(HoldToConfirmTone.Danger);

            Confirm(flow);
            flow.Step.ShouldBe(CancelFlowStep.Canceled);
            canceled.ShouldNotBeNull();
            canceled.Reason.Title.ShouldBe("Missing a feature I need");
            canceled.Feedback.ShouldBe("Two-way calendar sync");
            Part<TextBlock>(flow, CancelFlow.ResultTitlePart).Text.ShouldBe("Your plan is canceled");
            Part<TextBlock>(flow, CancelFlow.ResultDescriptionPart).Text
                .ShouldBe("You can keep using Studio until Nov 1, 2026. We sent a confirmation to your email.");
            // Below full motion the check is simply there.
            Part<StrokeDrawGlyph>(flow, CancelFlow.ResultGlyphPart).Progress.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Back_Returns_Through_The_Offer_And_Alt_Left_Does_The_Same()
    {
        var flow = Flow();
        var window = Show(flow);
        try
        {
            flow.GoBack().ShouldBeFalse();
            Choose(flow, 0);
            Click(flow, CancelFlow.ContinueButtonPart);
            Click(flow, CancelFlow.DeclineButtonPart);
            flow.Step.ShouldBe(CancelFlowStep.Confirm);

            Click(flow, CancelFlow.BackButtonPart);
            flow.Step.ShouldBe(CancelFlowStep.Offer);

            Part<TextBlock>(flow, CancelFlow.OfferTitlePart).Focus();
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Alt);
            flow.Step.ShouldBe(CancelFlowStep.Reason);
            flow.SelectedReason.ShouldBe(flow.Reasons.First());
            Part<PresenceHost>(flow, CancelFlow.BackHostPart).IsPresent.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Back_From_The_Confirmation_Skips_A_Missing_Offer()
    {
        var flow = Flow(withOffer: false);
        var window = Show(flow);
        try
        {
            Choose(flow, 0);
            Click(flow, CancelFlow.ContinueButtonPart);
            flow.GoBack().ShouldBeTrue();
            flow.Step.ShouldBe(CancelFlowStep.Reason);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Clears_The_Answers_And_Returns_To_The_First_Step()
    {
        var flow = Flow(withOffer: false);
        var window = Show(flow);
        try
        {
            Choose(flow, 0);
            flow.Feedback = "Too pricey for a team of six";
            Click(flow, CancelFlow.ContinueButtonPart);
            Confirm(flow);
            flow.Step.ShouldBe(CancelFlowStep.Canceled);

            flow.Reset();
            Settle();
            flow.Step.ShouldBe(CancelFlowStep.Reason);
            flow.SelectedReason.ShouldBeNull();
            flow.Feedback.ShouldBeNull();
            Part<RadioGroup>(flow, CancelFlow.ReasonsPart).SelectedIndex.ShouldBe(-1);
            Part<Textarea>(flow, CancelFlow.FeedbackPart).Text.ShouldBeNullOrEmpty();
            Part<AvaloniaButton>(flow, CancelFlow.ContinueButtonPart).IsEnabled.ShouldBeFalse();

            // The confirmation starts fresh the next time.
            Choose(flow, 0);
            Click(flow, CancelFlow.ContinueButtonPart);
            Part<HoldToConfirm>(flow, CancelFlow.HoldButtonPart).IsConfirmed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reactivate_Resets_Unless_The_Request_Is_Handled()
    {
        var flow = Flow(withOffer: false);
        var handle = true;
        var requests = 0;
        flow.ReactivateRequested += (_, e) =>
        {
            requests++;
            e.Handled = handle;
        };
        var window = Show(flow);
        try
        {
            Choose(flow, 0);
            Click(flow, CancelFlow.ContinueButtonPart);
            Confirm(flow);

            Click(flow, CancelFlow.ReactivateButtonPart);
            requests.ShouldBe(1);
            flow.Step.ShouldBe(CancelFlowStep.Canceled);

            handle = false;
            Click(flow, CancelFlow.ReactivateButtonPart);
            requests.ShouldBe(2);
            flow.Step.ShouldBe(CancelFlowStep.Reason);
            flow.SelectedReason.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keep_My_Plan_Asks_The_Host_To_Close()
    {
        var flow = Flow();
        var kept = 0;
        flow.KeepPlanRequested += (_, _) => kept++;
        var window = Show(flow);
        try
        {
            Click(flow, CancelFlow.KeepButtonPart);
            kept.ShouldBe(1);
            flow.Step.ShouldBe(CancelFlowStep.Reason);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Chooses_A_Row_First_And_Continues_From_The_Chosen_One()
    {
        var flow = Flow();
        var window = Show(flow);
        try
        {
            var group = Part<RadioGroup>(flow, CancelFlow.ReasonsPart);
            var row = group.ContainerFromIndex(0)!;
            row.Focus(NavigationMethod.Tab);
            Settle();

            Press(window, PhysicalKey.Enter);
            flow.SelectedReason.ShouldBe(flow.Reasons.First());
            flow.Step.ShouldBe(CancelFlowStep.Reason);

            Press(window, PhysicalKey.Enter);
            flow.Step.ShouldBe(CancelFlowStep.Offer);

            // Focus is on the offer's title now; Enter accepts the offer.
            Press(window, PhysicalKey.Enter);
            flow.Step.ShouldBe(CancelFlowStep.Retained);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Held_Enter_Does_Not_Press_The_Next_Steps_Button()
    {
        var flow = Flow();
        var window = Show(flow);
        try
        {
            Choose(flow, 0);
            Part<AvaloniaButton>(flow, CancelFlow.ContinueButtonPart).Focus(NavigationMethod.Tab);
            Settle();

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            flow.Step.ShouldBe(CancelFlowStep.Offer);

            // Auto-repeat arrives as more key-downs on the new step's title.
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            flow.Step.ShouldBe(CancelFlowStep.Offer);

            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Press(window, PhysicalKey.Enter);
            flow.Step.ShouldBe(CancelFlowStep.Retained);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_In_The_Feedback_Field_Adds_A_Line()
    {
        var flow = Flow();
        var window = Show(flow);
        try
        {
            Choose(flow, 2);
            var field = Part<Textarea>(flow, CancelFlow.FeedbackPart);
            field.Focus(NavigationMethod.Tab).ShouldBeTrue();
            Settle();
            TopLevel.GetTopLevel(flow)!.FocusManager!.GetFocusedElement().ShouldBeSameAs(field);
            Press(window, PhysicalKey.Enter);
            flow.Step.ShouldBe(CancelFlowStep.Reason);

            // This reason has no offer, so Ctrl+Enter continues straight to the confirmation.
            Press(window, PhysicalKey.Enter, RawInputModifiers.Control);
            flow.Step.ShouldBe(CancelFlowStep.Confirm);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Moves_To_The_New_Steps_Title()
    {
        var flow = Flow(withOffer: false);
        var window = Show(flow);
        try
        {
            Choose(flow, 0);
            var next = Part<AvaloniaButton>(flow, CancelFlow.ContinueButtonPart);
            next.Focus(NavigationMethod.Tab);
            Settle();
            Press(window, PhysicalKey.Enter);
            flow.Step.ShouldBe(CancelFlowStep.Confirm);

            var title = Part<TextBlock>(flow, CancelFlow.ConfirmTitlePart);
            title.IsFocused.ShouldBeTrue();
            KeyboardNavigation.GetIsTabStop(title).ShouldBeFalse();
            AutomationProperties.GetLiveSetting(title).ShouldBe(AutomationLiveSetting.Polite);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Reasons_The_Default_Reasons_Are_Offered_And_The_Field_Asks_Follow_Ups()
    {
        var flow = new CancelFlow { PlanName = "Studio" };
        var window = Show(flow);
        try
        {
            var group = Part<RadioGroup>(flow, CancelFlow.ReasonsPart);
            group.ItemCount.ShouldBe(5);
            AutomationProperties.GetName(group).ShouldBe("Why are you canceling?");
            Part<TextBlock>(flow, CancelFlow.ReasonDescriptionPart).Text.ShouldBe("Your answer helps us improve Studio.");

            flow.SelectedReason = flow.Labels.DefaultReasons[0];
            Settle();
            Part<Textarea>(flow, CancelFlow.FeedbackPart).Label.ShouldBe("Anything else we should know?");
            flow.SelectedReason = flow.Labels.DefaultReasons[2];
            Settle();
            Part<Textarea>(flow, CancelFlow.FeedbackPart).Label.ShouldBe("What were you missing?");
            group.SelectedIndex.ShouldBe(2);

            // Without offers anywhere the bar has two segments.
            flow.StepCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bound_Reasons_And_Losses_Follow_Their_Collections()
    {
        var reasons = new AvaloniaList<CancelFlowReason> { new("Too expensive") };
        var losses = new AvaloniaList<string>();
        var flow = new CancelFlow { Reasons = reasons, Losses = losses };
        var window = Show(flow);
        try
        {
            var group = Part<RadioGroup>(flow, CancelFlow.ReasonsPart);
            group.ItemCount.ShouldBe(1);
            reasons.Add(new CancelFlowReason("Something else"));
            Settle();
            group.ItemCount.ShouldBe(2);

            Choose(flow, 0);
            Click(flow, CancelFlow.ContinueButtonPart);
            Part<Border>(flow, CancelFlow.LossesBoxPart).IsVisible.ShouldBeFalse();
            Part<TextBlock>(flow, CancelFlow.ConfirmDescriptionPart).Text.ShouldBe("Your access ends when you confirm.");
            losses.Add("Shared projects become read-only");
            Settle();
            Part<Border>(flow, CancelFlow.LossesBoxPart).IsVisible.ShouldBeTrue();
            Part<TextBlock>(flow, CancelFlow.ConfirmDescriptionPart).Text.ShouldBe("Your access ends when you confirm. After that:");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Replace_The_Built_In_Text()
    {
        var flow = Flow();
        flow.Labels = new CancelFlowLabels { ReasonTitle = "Waarom zeg je op?", Continue = "Doorgaan" };
        var window = Show(flow);
        try
        {
            Part<TextBlock>(flow, CancelFlow.ReasonTitlePart).Text.ShouldBe("Waarom zeg je op?");
            Part<AvaloniaButton>(flow, CancelFlow.ContinueButtonPart).Content.ShouldBe("Doorgaan");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Flow_Stacks_Its_Buttons_And_Plain_Drops_The_Frame()
    {
        var flow = Flow();
        var window = Show(flow);
        try
        {
            flow.IsNarrow.ShouldBeFalse();
            flow.Width = 360;
            Settle();
            flow.IsNarrow.ShouldBeTrue();
            flow.Classes.Contains(CancelFlow.NarrowPseudoClass).ShouldBeTrue();
            DockPanel.GetDock(Part<AvaloniaButton>(flow, CancelFlow.ContinueButtonPart)).ShouldBe(Dock.Top);

            flow.Appearance = CancelFlowAppearance.Plain;
            Settle();
            flow.BorderThickness.ShouldBe(new Thickness(0));
            flow.Padding.ShouldBe(new Thickness(0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Quick_Return_During_A_Slide_Reuses_The_Step_View()
    {
        var flow = Flow();
        var window = TestHelpers.Show(flow, Motion.ArcMotionLevel.Full);
        window.Width = 640;
        window.Height = 900;
        try
        {
            Settle();
            var reasonView = Part<Control>(flow, CancelFlow.ReasonStepPart);
            Choose(flow, 0);
            Click(flow, CancelFlow.ContinueButtonPart);
            // Back at once, while the reason step is still sliding out.
            flow.GoBack().ShouldBeTrue();
            Settle();
            flow.Step.ShouldBe(CancelFlowStep.Reason);
            Part<Control>(flow, CancelFlow.ReasonStepPart).ShouldBeSameAs(reasonView);
        }
        finally
        {
            window.Close();
        }
    }
}
