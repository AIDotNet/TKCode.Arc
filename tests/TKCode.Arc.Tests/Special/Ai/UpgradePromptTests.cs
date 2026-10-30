using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The invented Pro plan the upgrade prompt tests offer (the package ships none): $20 a month or $16 billed yearly, 2,000 AI credits.</summary>
internal static class UpgradePromptTestPlan
{
    /// <summary>The Atlas mark: Lucide <c>sparkles</c> in violet.</summary>
    private static readonly IBrush s_atlas = new ImmutableSolidColorBrush(Color.FromRgb(0x7C, 0x5C, 0xE6));

    /// <summary>The Atlas Vision mark: Lucide <c>eye</c> in teal.</summary>
    private static readonly IBrush s_vision = new ImmutableSolidColorBrush(Color.FromRgb(0x0E, 0x95, 0x8A));

    /// <summary>Creates the sample Pro plan (a new instance each time, so a page can change one part).</summary>
    public static UpgradePromptPlan CreatePlan() => new()
    {
        Name = "Pro",
        MonthlyPrice = 20,
        YearlyPrice = 16,
        Currency = "USD",
        Limit = 2000,
        Features =
        [
            new UpgradePromptFeature("credits", "2,000 AI credits every month", ArcIcons.Zap),
            new UpgradePromptFeature("model", "Atlas 3 Pro, the most capable model", ArcIcons.Sparkles, s_atlas),
            new UpgradePromptFeature("queue", "Priority queue, no waiting at peak times", ArcIcons.Timer)
        ],
        Limits =
        [
            new UpgradePromptLimit("AI credits", "2,000 a month"),
            new UpgradePromptLimit("Models", "Atlas 3 Pro", ArcIcons.Sparkles, s_atlas),
            new UpgradePromptLimit("Image input", "Atlas Vision", ArcIcons.Eye, s_vision),
            new UpgradePromptLimit("Queue", "Priority")
        ]
    };
}

/// <summary>
/// UpgradePrompt: the sample offer it opens on, the billing toggle and the rolling price, the upgrade (simulated, app
/// supplied, failing, cancelled on unmount), Maybe later and See Pro again, Continue, the size kept across states, inert
/// panels, focus handed to the new state's button, the narrow layout, the labels and the pure number logic.
/// </summary>
public class UpgradePromptTests
{
    /// <summary>A prompt with the test plan at 50 of 50 AI credits, resetting Nov 1 (unless <paramref name="withPlan"/> is false).</summary>
    private static (UpgradePrompt Prompt, Window Window) Open(Action<UpgradePrompt>? setup = null, double width = 480,
        ArcMotionLevel level = ArcMotionLevel.Off, bool withPlan = true)
    {
        var prompt = new UpgradePrompt { SimulatedDuration = TimeSpan.Zero, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (withPlan)
        {
            prompt.Plan = UpgradePromptTestPlan.CreatePlan();
            prompt.Used = 50;
            prompt.Limit = 50;
            prompt.ResetsOn = "Nov 1";
        }
        setup?.Invoke(prompt);
        var window = TestHelpers.Show(prompt, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (prompt, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    private static void Upgrade(UpgradePrompt prompt, UpgradePromptState expected = UpgradePromptState.Success)
    {
        _ = prompt.UpgradeAsync();
        TestHelpers.PumpUntil(() => prompt.State == expected).ShouldBeTrue();
        Pump();
    }

    [AvaloniaFact]
    public void Without_a_plan_the_card_is_empty_and_offers_nothing()
    {
        var (prompt, _) = Open(withPlan: false);
        prompt.Plan.ShouldBeNull();
        prompt.Used.ShouldBe(0);
        prompt.Limit.ShouldBe(0);
        prompt.ResetsOn.ShouldBe(string.Empty);
        prompt.IsEmpty.ShouldBeTrue();
        prompt.State.ShouldBe(UpgradePromptState.Offer);
        prompt.TitlePart!.Text.ShouldBe("No plan to offer");
        prompt.DescriptionPart!.Text.ShouldBe(string.Empty);
        prompt.MeterPart!.IsEffectivelyVisible.ShouldBeFalse();
        prompt.PriceGroupPart!.IsEffectivelyVisible.ShouldBeFalse();
        prompt.UpgradeButton!.Content.ShouldBe("Upgrade");
        prompt.UpgradeButton.IsEnabled.ShouldBeFalse();
        prompt.LaterButton!.IsEnabled.ShouldBeFalse();
        AutomationProperties.GetName(prompt).ShouldBe("No plan to offer");
        _ = prompt.UpgradeAsync();
        prompt.Dismiss();
        Pump();
        prompt.State.ShouldBe(UpgradePromptState.Offer);

        // The app's plan arrives: the offer shows.
        prompt.Plan = UpgradePromptTestPlan.CreatePlan();
        prompt.Used = 50;
        prompt.Limit = 50;
        Pump();
        prompt.IsEmpty.ShouldBeFalse();
        prompt.MeterPart.IsEffectivelyVisible.ShouldBeTrue();
        prompt.UpgradeButton.Content.ShouldBe("Upgrade to Pro");
        prompt.UpgradeButton.IsEnabled.ShouldBeTrue();
        prompt.TitlePart.Text.ShouldBe("You've used all 50 AI credits");
    }

    [AvaloniaFact]
    public void Opens_on_the_offer_at_the_limit()
    {
        var (prompt, _) = Open();
        prompt.State.ShouldBe(UpgradePromptState.Offer);
        prompt.TitlePart!.Text.ShouldBe("You've used all 50 AI credits");
        prompt.DescriptionPart!.Text.ShouldBe("Upgrade to Pro for 2,000 AI credits a month, or wait until they reset on Nov 1.");
        prompt.MeterCountPart!.Text.ShouldBe("50 / 50");
        prompt.StatusTextPart!.Text.ShouldBe("Limit reached");
        AutomationProperties.GetName(prompt.MeterPart!).ShouldBe("50 of 50 AI credits used");
        prompt.BarPart!.Target.ShouldBe(1);
        prompt.PricePart!.Value.ShouldBe(16);
        prompt.PricePart.Prefix.ShouldBe("$");
        prompt.BillingNotePart!.Text.ShouldBe("$192 billed yearly");
        AutomationProperties.GetName(prompt.PriceGroupPart!).ShouldBe("$16 a month, $192 billed yearly");
        prompt.TogglePart!.Value.ShouldBe("yearly");
        prompt.FeaturesPart!.Children.Count.ShouldBe(3);
        prompt.UpgradeButton!.Content.ShouldBe("Upgrade to Pro");
        prompt.LaterButton!.Content.ShouldBe("Maybe later");
        AutomationProperties.GetName(prompt).ShouldBe("Upgrade to Pro");
    }

    [AvaloniaFact]
    public void Below_the_limit_the_title_and_status_say_what_is_left()
    {
        var (prompt, _) = Open(p => p.Used = 45);
        prompt.TitlePart!.Text.ShouldBe("Only 5 AI credits left");
        prompt.StatusTextPart!.Text.ShouldBe("5 AI credits left");
        prompt.BarPart!.Target.ShouldBe(0.9, 1e-9);
    }

    [AvaloniaFact]
    public void Title_and_description_overrides_replace_the_offer_words()
    {
        var (prompt, _) = Open(p =>
        {
            p.Unit = "seats";
            p.Used = 3;
            p.Limit = 3;
            p.Title = "You've used all 3 seats";
            p.Description = "Invite the rest of your team.";
        });
        prompt.TitlePart!.Text.ShouldBe("You've used all 3 seats");
        prompt.DescriptionPart!.Text.ShouldBe("Invite the rest of your team.");
        AutomationProperties.GetName(prompt.MeterPart!).ShouldBe("3 of 3 seats used");
    }

    [AvaloniaFact]
    public void The_toggle_switches_the_interval_and_rolls_the_price()
    {
        var (prompt, _) = Open();
        var raised = new List<UpgradePromptInterval>();
        prompt.IntervalChanged += (_, e) => raised.Add(e.Interval);

        prompt.TogglePart!.Value = "monthly";
        Pump();

        prompt.Interval.ShouldBe(UpgradePromptInterval.Monthly);
        raised.ShouldBe([UpgradePromptInterval.Monthly]);
        prompt.PricePart!.Value.ShouldBe(20);
        prompt.BillingNotePart!.Text.ShouldBe("Billed monthly, cancel any time");
        prompt.CurrentSelection.Amount.ShouldBe(20m);
    }

    [AvaloniaFact]
    public void Arrow_keys_on_the_toggle_switch_between_monthly_and_yearly()
    {
        var (prompt, window) = Open();
        var raised = new List<UpgradePromptInterval>();
        prompt.IntervalChanged += (_, e) => raised.Add(e.Interval);
        var yearly = prompt.TogglePart!.ContainerFromIndex(1)!;
        yearly.Focus(NavigationMethod.Tab);
        Pump();

        Press(window, PhysicalKey.ArrowLeft);
        prompt.Interval.ShouldBe(UpgradePromptInterval.Monthly);

        Press(window, PhysicalKey.ArrowRight);
        prompt.Interval.ShouldBe(UpgradePromptInterval.Yearly);
        raised.ShouldBe([UpgradePromptInterval.Monthly, UpgradePromptInterval.Yearly]);
    }

    [AvaloniaFact]
    public void Setting_the_interval_moves_the_toggle_without_raising_the_event()
    {
        var (prompt, _) = Open();
        var raised = 0;
        prompt.IntervalChanged += (_, _) => raised++;
        prompt.Interval = UpgradePromptInterval.Monthly;
        Pump();
        prompt.TogglePart!.Value.ShouldBe("monthly");
        prompt.PricePart!.Value.ShouldBe(20);
        raised.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_yearly_badge_derives_from_the_two_prices()
    {
        var (prompt, _) = Open();
        var options = prompt.TogglePart!.ItemsSource!.Cast<BillingToggleOption>().ToList();
        options[0].Label.ShouldBe("Monthly");
        options[1].Badge.ShouldBe("Save 20%");
        options[1].ActiveBadge.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_simulated_upgrade_lands_on_success_with_the_new_limit()
    {
        var (prompt, _) = Open();
        UpgradePromptSelection? upgraded = null;
        prompt.Upgraded += (_, e) => upgraded = e.Selection;

        Upgrade(prompt);

        upgraded.ShouldNotBeNull();
        upgraded.Plan.Name.ShouldBe("Pro");
        upgraded.Interval.ShouldBe(UpgradePromptInterval.Yearly);
        upgraded.Amount.ShouldBe(192m);
        upgraded.Currency.ShouldBe("USD");
        prompt.TitlePart!.Text.ShouldBe("You're on Pro");
        prompt.MeterCountPart!.Text.ShouldBe("50 / 2,000");
        prompt.StatusTextPart!.Text.ShouldBe("1,950 AI credits left");
        prompt.BarPart!.Target.ShouldBe(0.025, 1e-9);
        prompt.LimitsPart!.Children.Count.ShouldBe(4);
        prompt.ChargedNotePart!.Text.ShouldBe("Simulated upgrade: nothing was charged.");
        prompt.StatusRegion!.Text.ShouldBe("Upgraded to Pro. You now have 2,000 AI credits a month.");
        prompt.BodyPanels!.Active.ShouldBe(UpgradePrompt.SuccessPanel);
        prompt.FooterPanels!.Active.ShouldBe(UpgradePrompt.SuccessPanel);
    }

    [AvaloniaFact]
    public void Processing_keeps_Upgrade_focusable_and_turns_the_rest_off()
    {
        var gate = new TaskCompletionSource();
        var (prompt, _) = Open(p => p.UpgradeHandler = (_, _) => gate.Task);
        UpgradePromptSelection? seen = null;
        prompt.UpgradeHandler = (selection, _) =>
        {
            seen = selection;
            return gate.Task;
        };

        _ = prompt.UpgradeAsync();
        Pump();

        prompt.State.ShouldBe(UpgradePromptState.Processing);
        seen!.Amount.ShouldBe(192m);
        prompt.UpgradeButton!.IsLoading.ShouldBeTrue();
        prompt.UpgradeButton.IsEnabled.ShouldBeTrue();
        prompt.UpgradeButton.Content.ShouldBe("Upgrading…");
        prompt.LaterButton!.IsEnabled.ShouldBeFalse();
        prompt.TogglePart!.IsEnabled.ShouldBeFalse();
        prompt.StatusRegion!.Text.ShouldBe("Upgrading to Pro…");

        gate.SetResult();
        TestHelpers.PumpUntil(() => prompt.State == UpgradePromptState.Success).ShouldBeTrue();
        Pump();
        prompt.ChargedNotePart!.Text.ShouldBe("Charged $192 today. A receipt is on its way.");
    }

    [AvaloniaFact]
    public void Without_a_handler_the_announcement_says_nothing_is_charged()
    {
        var (prompt, _) = Open(p => p.SimulatedDuration = TimeSpan.FromSeconds(30));
        _ = prompt.UpgradeAsync();
        Pump();
        prompt.StatusRegion!.Text.ShouldBe("Simulating the upgrade to Pro. Nothing will be charged.");
        prompt.Reset();
        Pump();
        prompt.State.ShouldBe(UpgradePromptState.Offer);
    }

    [AvaloniaFact]
    public void A_failed_upgrade_shows_its_message_and_Try_again_retries()
    {
        var attempts = 0;
        var (prompt, _) = Open(p => p.UpgradeHandler = (_, _) =>
        {
            attempts++;
            return attempts == 1 ? Task.FromException(new InvalidOperationException("Your card was declined. Try another card.")) : Task.CompletedTask;
        });
        Exception? failure = null;
        prompt.UpgradeFailed += (_, e) => failure = e.Error;

        Upgrade(prompt, UpgradePromptState.Error);

        prompt.ErrorMessage.ShouldBe("Your card was declined. Try another card.");
        failure.ShouldBeOfType<InvalidOperationException>();
        prompt.ErrorTextPart!.Text.ShouldBe("Your card was declined. Try another card.");
        prompt.ErrorLinePart!.Opacity.ShouldBe(1);
        prompt.BillingNotePart!.Opacity.ShouldBe(0);
        prompt.UpgradeButton!.Content.ShouldBe("Try again");
        prompt.StatusRegion!.Text.ShouldBe("Upgrade failed. Your card was declined. Try another card.");

        Upgrade(prompt);
        prompt.ErrorMessage.ShouldBeNull();
        prompt.ErrorLinePart.Opacity.ShouldBe(0);
        attempts.ShouldBe(2);
    }

    [AvaloniaFact]
    public void An_error_without_a_message_uses_the_generic_one()
    {
        var (prompt, _) = Open(p => p.UpgradeHandler = (_, _) => Task.FromException(new Exception(" ")));
        Upgrade(prompt, UpgradePromptState.Error);
        prompt.ErrorMessage.ShouldBe("The upgrade didn't go through. Try again.");
    }

    [AvaloniaFact]
    public void Maybe_later_shows_the_reminder_and_See_Pro_again_returns()
    {
        var (prompt, _) = Open();
        var dismissed = 0;
        prompt.Dismissed += (_, _) => dismissed++;

        prompt.LaterButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();

        dismissed.ShouldBe(1);
        prompt.State.ShouldBe(UpgradePromptState.Reminder);
        prompt.TitlePart!.Text.ShouldBe("No problem");
        prompt.DescriptionPart!.Text.ShouldBe("Your AI credits reset on Nov 1.");
        prompt.StatusRegion!.Text.ShouldBe("Upgrade dismissed. Your limit resets on Nov 1.");
        prompt.SeeAgainButton!.Content.ShouldBe("See Pro again");

        prompt.SeeAgainButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
        prompt.State.ShouldBe(UpgradePromptState.Offer);
        prompt.TitlePart.Text.ShouldBe("You've used all 50 AI credits");
    }

    [AvaloniaFact]
    public void Continue_left_unhandled_resets_to_the_offer()
    {
        var (prompt, _) = Open();
        Upgrade(prompt);
        var continued = 0;
        prompt.ContinueRequested += (_, _) => continued++;

        prompt.ContinueButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();

        continued.ShouldBe(1);
        prompt.State.ShouldBe(UpgradePromptState.Offer);
        prompt.MeterCountPart!.Text.ShouldBe("50 / 50");
        prompt.BarPart!.Target.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Continue_handled_keeps_the_success_state()
    {
        var (prompt, _) = Open();
        Upgrade(prompt);
        prompt.ContinueRequested += (_, e) => e.Handled = true;

        prompt.ContinueButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();

        prompt.State.ShouldBe(UpgradePromptState.Success);
    }

    [AvaloniaFact]
    public void The_card_keeps_its_size_in_every_state()
    {
        var (prompt, _) = Open(p => p.UpgradeHandler = (_, _) => Task.FromException(new InvalidOperationException("Declined.")));
        var size = prompt.Bounds.Size;
        size.Height.ShouldBeGreaterThan(0);

        Upgrade(prompt, UpgradePromptState.Error);
        prompt.Bounds.Size.ShouldBe(size);

        prompt.Dismiss();
        Pump();
        prompt.State.ShouldBe(UpgradePromptState.Reminder);
        prompt.Bounds.Size.ShouldBe(size);

        prompt.ShowOffer();
        prompt.UpgradeHandler = (_, _) => Task.CompletedTask;
        Upgrade(prompt);
        prompt.Bounds.Size.ShouldBe(size);
    }

    [AvaloniaFact]
    public void Inactive_panels_are_inert_and_hidden()
    {
        var (prompt, _) = Open();
        var success = prompt.BodyPanels!.Panels[UpgradePrompt.SuccessPanel];
        success.IsHitTestVisible.ShouldBeFalse();
        success.Opacity.ShouldBe(0);
        KeyboardNavigation.GetTabNavigation(success).ShouldBe(KeyboardNavigationMode.None);
        AutomationProperties.GetAccessibilityView(success).ShouldBe(AccessibilityView.Raw);
        var offer = prompt.BodyPanels.Panels[UpgradePrompt.OfferPanel];
        offer.IsHitTestVisible.ShouldBeTrue();
        offer.Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Focus_moves_to_the_new_states_button()
    {
        var (prompt, _) = Open();
        prompt.UpgradeButton!.Focus(NavigationMethod.Tab);
        Upgrade(prompt);
        prompt.ContinueButton!.IsFocused.ShouldBeTrue();

        prompt.ContinueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
        prompt.UpgradeButton.IsFocused.ShouldBeTrue();

        prompt.LaterButton!.Focus(NavigationMethod.Tab);
        prompt.Dismiss();
        Pump();
        prompt.SeeAgainButton!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Leaving_the_tree_cancels_a_running_upgrade()
    {
        var cancelled = false;
        var (prompt, window) = Open(p => p.UpgradeHandler = async (_, token) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                throw;
            }
        });
        _ = prompt.UpgradeAsync();
        Pump();
        prompt.State.ShouldBe(UpgradePromptState.Processing);

        window.Content = null;
        TestHelpers.PumpUntil(() => cancelled).ShouldBeTrue();
        Pump();
        prompt.State.ShouldBe(UpgradePromptState.Offer);
    }

    [AvaloniaFact]
    public void Narrow_cards_drop_the_padding_and_stack_the_toggle()
    {
        var (prompt, window) = Open(width: 360);
        prompt.Classes.Contains(UpgradePrompt.NarrowPseudoClass).ShouldBeTrue();
        prompt.Padding.ShouldBe(new Thickness(20));

        window.Width = 230;
        Pump();
        prompt.PriceRowPart!.IsStacked.ShouldBeTrue();
        prompt.LaterButton!.Bounds.Width.ShouldBeGreaterThan(0);
        prompt.UpgradeButton!.Bounds.X.ShouldBeGreaterThan(prompt.LaterButton.Bounds.X);

        window.Width = 600;
        Pump();
        prompt.Classes.Contains(UpgradePrompt.NarrowPseudoClass).ShouldBeFalse();
        // Headless glyphs are wider than real ones, so assert the rule rather than a layout at one width.
        var row = prompt.PriceRowPart;
        var sideBySide = prompt.PriceGroupPart!.DesiredSize.Width + row.Gap + prompt.TogglePart!.DesiredSize.Width;
        row.IsStacked.ShouldBe(sideBySide > row.Bounds.Width);
        prompt.Bounds.Width.ShouldBe(440);
    }

    [AvaloniaFact]
    public void A_custom_plan_and_labels_show_their_words()
    {
        var plan = new UpgradePromptPlan
        {
            Name = "Team",
            MonthlyPrice = 9.5m,
            YearlyPrice = 8m,
            Currency = "EUR",
            Limit = 25,
            Features = [new UpgradePromptFeature("seats", "Up to 25 seats")]
        };
        var (prompt, _) = Open(p =>
        {
            p.Plan = plan;
            p.Interval = UpgradePromptInterval.Monthly;
            p.Labels = new UpgradePromptLabels { MaybeLater = "Not now", Upgrade = name => $"Go {name}" };
        });
        prompt.LaterButton!.Content.ShouldBe("Not now");
        prompt.UpgradeButton!.Content.ShouldBe("Go Team");
        prompt.PricePart!.Prefix.ShouldBe("€");
        prompt.PricePart.Decimals.ShouldBe(2);
        prompt.PricePart.Value.ShouldBe(9.5);
        prompt.FeaturesPart!.Children.Count.ShouldBe(1);
        AutomationProperties.GetName(prompt).ShouldBe("Upgrade to Team");
    }

    [AvaloniaFact]
    public void The_bar_waits_for_the_card_to_scroll_into_view_under_full_motion()
    {
        var (prompt, _) = Open(level: ArcMotionLevel.Full);
        TestHelpers.PumpUntil(() => prompt.BarPart!.Target > 0).ShouldBeTrue();
        prompt.BarPart!.Target.ShouldBe(1);
        TestHelpers.PumpUntil(() => prompt.BarPart.Shown >= 0.999).ShouldBeTrue();
    }

    [Fact]
    public void Format_derives_the_saving_and_the_charge()
    {
        var plan = UpgradePromptTestPlan.CreatePlan();
        UpgradePromptFormat.SavingPercent(plan).ShouldBe(20);
        UpgradePromptFormat.SavingPerYear(plan).ShouldBe(48m);
        UpgradePromptFormat.Charge(plan, UpgradePromptInterval.Yearly).ShouldBe(192m);
        UpgradePromptFormat.Charge(plan, UpgradePromptInterval.Monthly).ShouldBe(20m);
        UpgradePromptFormat.PerMonth(plan, UpgradePromptInterval.Yearly).ShouldBe(16m);
        UpgradePromptFormat.Decimals(plan).ShouldBe(0);
        UpgradePromptFormat.Money(192m, "USD").ShouldBe("$192");
        UpgradePromptFormat.Money(9.5m, "GBP").ShouldBe("£9.50");
        UpgradePromptFormat.Money(10m, "CHF").ShouldBe("CHF 10");
        UpgradePromptFormat.Count(1950).ShouldBe("1,950");
        UpgradePromptFormat.Count(12.5).ShouldBe("12.5");
        UpgradePromptFormat.Fill(50, 50).ShouldBe(1);
        UpgradePromptFormat.Fill(60, 50).ShouldBe(1);
        UpgradePromptFormat.Fill(50, 0).ShouldBe(1);
        UpgradePromptFormat.Fill(50, 2000).ShouldBe(0.025, 1e-12);
        UpgradePromptFormat.IsAtLimit(50, 50).ShouldBeTrue();
        UpgradePromptFormat.IsAtLimit(49, 50).ShouldBeFalse();
        UpgradePromptFormat.Left(60, 50).ShouldBe(0);

        var noSaving = new UpgradePromptPlan { MonthlyPrice = 10, YearlyPrice = 10 };
        UpgradePromptFormat.SavingPercent(noSaving).ShouldBe(0);
        UpgradePromptFormat.SavingPerYear(noSaving).ShouldBe(0m);
    }

    [Fact]
    public void Panel_index_follows_the_state()
    {
        UpgradePrompt.PanelOf(UpgradePromptState.Offer).ShouldBe(UpgradePrompt.OfferPanel);
        UpgradePrompt.PanelOf(UpgradePromptState.Processing).ShouldBe(UpgradePrompt.OfferPanel);
        UpgradePrompt.PanelOf(UpgradePromptState.Error).ShouldBe(UpgradePrompt.OfferPanel);
        UpgradePrompt.PanelOf(UpgradePromptState.Success).ShouldBe(UpgradePrompt.SuccessPanel);
        UpgradePrompt.PanelOf(UpgradePromptState.Reminder).ShouldBe(UpgradePrompt.ReminderPanel);
    }

    [AvaloniaFact]
    public void The_price_rolls_on_a_bounce_free_0_28s_spring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (prompt, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            var spring = SpringSpec.FromVisual(0.28, 0);
            var price = prompt.PricePart!;
            price.WheelMotion.ShouldBe(MotionSpec.Spring(spring));
            prompt.TogglePart!.Value = "monthly";
            Dispatcher.UIThread.RunJobs();
            // $16 → $20: the ones wheel turns on from 6 to 10 (reading 0), on the spec's spring frame by frame.
            price.WheelTargets[^1].ShouldBe(10);
            for (var n = 1; n <= 24; n++)
            {
                clock.Advance(TimeSpan.FromSeconds(1.0 / 60.0));
                var (x, _) = SpringSolver.Evaluate(spring, -4, 0, n / 60.0);
                price.WheelPositions[^1].ShouldBe(10 + x, 1e-3, $"frame {n}");
            }
        }
        finally
        {
            window.Close();
        }
    }
}
