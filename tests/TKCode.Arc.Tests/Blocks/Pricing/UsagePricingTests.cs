using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// UsagePricing: defaults from the sample, the recommendation and its four reasons, the breakdown, yearly billing, the
/// slider keyboard model (PageUp / PageDown = 3 stops), the trial (reset on plan change, undo, async handler), the
/// breakpoint, the live region and the motion parts (digit wheels with blur, FLIP list, meter fills).
/// </summary>
public class UsagePricingTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (UsagePricing Block, Window Window) Show(Action<UsagePricing>? setup = null, double width = 1200,
        ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var block = new UsagePricing();
        setup?.Invoke(block);
        var window = TestHelpers.Show(block, level);
        window.Width = width;
        window.Height = 1000;
        Pump();
        return (block, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static T Part<T>(UsagePricing block, string name) where T : Control => TestHelpers.Descendant<T>(block, name);

    private static InputElement ThumbOf(Slider slider)
        => slider.GetVisualDescendants().OfType<InputElement>().First(x => x.Focusable && !ReferenceEquals(x, slider));

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    [AvaloniaFact]
    public void Defaults_show_the_sample_estimate()
    {
        var (block, _) = Show();
        Assert.Equal(16, block.Seats);
        Assert.Equal(12_000_000, block.Usage);
        Assert.Equal("Studio", block.RecommendedPlan?.Name);
        Assert.Equal(UsagePricingReason.BothAbove, block.Reason);
        Assert.Equal(436m, block.EstimatedTotal);
        Assert.Equal(960m, block.YearlySaving);
        Assert.Equal("Studio", Part<TextMorph>(block, UsagePricing.PlanNamePart).Text);
        Assert.Equal("$436", Part<UsagePricingDigits>(block, UsagePricing.PricePart).DisplayText);
        Assert.Equal("$436", Part<UsagePricingDigits>(block, UsagePricing.TotalPart).DisplayText);
        Assert.Equal("$416", Part<UsagePricingDigits>(block, UsagePricing.SeatsCostPart).DisplayText);
        Assert.Equal("$960", Part<UsagePricingDigits>(block, UsagePricing.SavingPart).DisplayText);
        Assert.Equal("16 × $26", Part<TextBlock>(block, UsagePricing.SeatsDetailPart).Text);
        Assert.Equal("12M used, 4M extra at $5 per million", Part<TextBlock>(block, UsagePricing.UsageDetailPart).Text);
        Assert.Equal("Suggested beyond 4 seats and 1.5M calls", Part<ArcContentSwap>(block, UsagePricing.ReasonPart).Content);
        Assert.Equal("Try Studio", Part<Button>(block, UsagePricing.ActionPart).Content);
        Assert.Equal(1, Part<UsagePricingMeter>(block, UsagePricing.MeterPart).CurrentIndex);
        Assert.Equal(["Spark", "Studio", "Fleet"], Part<UsagePricingMeter>(block, UsagePricing.MeterPart).Names);
        var features = Part<UsagePricingFeatureList>(block, UsagePricing.FeaturesPart);
        Assert.Equal(["links", "trail", "history", "rate", "help"], features.Rows.Select(r => r.Key));
        Assert.Equal(10 + 5 * 36, features.TargetHeight);
        Assert.Equal("Included in Studio", AutomationProperties.GetName(features));
    }

    [AvaloniaFact]
    public void Sliders_step_through_the_stops_with_plan_marks()
    {
        var (block, _) = Show();
        var seats = Part<Slider>(block, UsagePricing.SeatSliderPart);
        var usage = Part<Slider>(block, UsagePricing.UsageSliderPart);
        Assert.Equal(22, seats.Maximum);
        Assert.Equal(26, usage.Maximum);
        Assert.Equal(10, seats.Value);
        Assert.Equal("16", seats.FormatValue(10));
        Assert.Equal("1.5M", usage.FormatValue(6));
        Assert.Equal([(4.0, "Studio"), (15.0, "Fleet")], seats.Marks.Select(m => (m.Value, m.Label!)));
        Assert.Equal([(7.0, "Studio"), (17.0, "Fleet")], usage.Marks.Select(m => (m.Value, m.Label!)));
        Assert.Equal("Team size", seats.Label);
    }

    [AvaloniaFact]
    public void Keyboard_moves_one_three_or_all_stops()
    {
        var (block, window) = Show();
        var slider = Part<Slider>(block, UsagePricing.SeatSliderPart);
        ThumbOf(slider).Focus(NavigationMethod.Tab);
        Pump();
        Press(window, PhysicalKey.ArrowRight);
        Assert.Equal(11, block.SeatIndex);
        Assert.Equal(20, block.Seats);
        Press(window, PhysicalKey.PageUp);
        Assert.Equal(14, block.SeatIndex);
        Press(window, PhysicalKey.PageDown);
        Assert.Equal(11, block.SeatIndex);
        Press(window, PhysicalKey.ArrowLeft);
        Assert.Equal(10, block.SeatIndex);
        Press(window, PhysicalKey.End);
        Assert.Equal(22, block.SeatIndex);
        Assert.Equal("Fleet", block.RecommendedPlan?.Name);
        Press(window, PhysicalKey.Home);
        Assert.Equal(0, block.SeatIndex);
    }

    [AvaloniaFact]
    public void Index_setters_move_the_sliders_and_coerce()
    {
        var (block, _) = Show();
        block.UsageIndex = 99;
        Pump();
        Assert.Equal(26, block.UsageIndex);
        Assert.Equal(26, Part<Slider>(block, UsagePricing.UsageSliderPart).Value);
        block.SeatIndex = -4;
        Assert.Equal(0, block.SeatIndex);
    }

    [AvaloniaFact]
    public void Four_reasons_and_announcements()
    {
        var (block, _) = Show();
        var announcer = Part<SiteAnnouncer>(block, UsagePricing.AnnouncerPart);
        var changes = new List<UsagePricingRecommendationChangedEventArgs>();
        block.RecommendationChanged += (_, e) => changes.Add(e);

        block.SeatIndex = 2; // 3 seats, 12M: still Studio, only the usage is above Spark.
        Pump();
        Assert.Equal("Studio", block.RecommendedPlan?.Name);
        Assert.Equal(UsagePricingReason.UsageAbove, block.Reason);
        Assert.Equal("Suggested beyond 1.5M calls a month", Part<ArcContentSwap>(block, UsagePricing.ReasonPart).Content);
        Assert.Single(changes);
        Assert.Same(changes[0].OldPlan, changes[0].NewPlan);
        Assert.Equal("Studio. Suggested beyond 1.5M calls a month.", announcer.Announcement);

        block.UsageIndex = 4; // 3 seats, 800k: Spark.
        Pump();
        Assert.Equal("Spark", block.RecommendedPlan?.Name);
        Assert.Equal(UsagePricingReason.WithinLimits, block.Reason);
        Assert.Equal("Fits up to 4 seats and 1.5M calls a month", Part<ArcContentSwap>(block, UsagePricing.ReasonPart).Content);

        block.SeatIndex = 15; // 42 seats, 800k: Fleet, seats only.
        Pump();
        Assert.Equal("Fleet", block.RecommendedPlan?.Name);
        Assert.Equal(UsagePricingReason.SeatsAbove, block.Reason);
        Assert.Equal("Fleet. Suggested beyond 36 seats.", announcer.Announcement);
        Assert.Equal(3, changes.Count);
        Assert.Equal("Spark", changes[2].OldPlan?.Name);

        block.SeatIndex = 10; // 16 seats, 800k: Studio, seats only.
        Pump();
        Assert.Equal(UsagePricingReason.SeatsAbove, block.Reason);
        Assert.Equal("Suggested beyond 4 seats", Part<ArcContentSwap>(block, UsagePricing.ReasonPart).Content);

        // A change that moves neither plan nor reason says nothing new.
        var count = announcer.History.Count;
        block.SeatIndex = 11;
        Pump();
        Assert.Equal(count, announcer.History.Count);
    }

    [AvaloniaFact]
    public void Usage_inside_the_plan_is_bundled()
    {
        var (block, _) = Show(b => { b.SeatIndex = 1; b.UsageIndex = 4; });
        Assert.Equal("Spark", block.RecommendedPlan?.Name);
        var value = Part<ArcContentSwap>(block, UsagePricing.UsageValuePart);
        Assert.Equal("Bundled", Assert.IsType<TextBlock>(value.Content).Text);
        Assert.Equal("800k of 1M bundled", Part<TextBlock>(block, UsagePricing.UsageDetailPart).Text);
        Assert.Equal(28m, block.EstimatedTotal);
        block.UsageIndex = 6; // 1.5M: still Spark, 500k over at $9 per million.
        Pump();
        Assert.Equal("Spark", block.RecommendedPlan?.Name);
        Assert.IsType<UsagePricingDigits>(value.Content);
        Assert.Equal(32.5m, block.EstimatedTotal);
        Assert.Equal("$33", Part<UsagePricingDigits>(block, UsagePricing.PricePart).DisplayText);
    }

    [AvaloniaFact]
    public void Yearly_billing_adds_the_discount_row()
    {
        var (block, _) = Show();
        var row = Part<SpringSizeDecorator>(block, UsagePricing.YearlyRowPart);
        Assert.False(row.IsExpanded);
        Assert.Equal(0, row.Bounds.Height);
        block.BillingPeriod = UsagePricingPeriod.Yearly;
        Pump();
        Assert.True(row.IsExpanded);
        Assert.True(row.Bounds.Height > 40);
        Assert.True(block.Classes.Contains(UsagePricing.YearlyPseudoClass));
        Assert.Equal(356m, block.EstimatedTotal);
        Assert.Equal("−$80", Part<UsagePricingDigits>(block, UsagePricing.YearlyOffPart).DisplayText);
        Assert.Equal("16 × $5 off per seat", Part<TextBlock>(block, UsagePricing.YearlyDetailPart).Text);
        Assert.True(Part<UsagePricingSavingMark>(block, UsagePricing.SavingMarkPart).IsApplied);
        Assert.Equal(1, Part<SegmentedControl>(block, UsagePricing.PeriodPart).SelectedIndex);
        var billed = Part<ArcContentSwap>(block, UsagePricing.BilledPart);
        var panel = Assert.IsType<StackPanel>(billed.Content);
        Assert.Equal("$4,032", panel.Children.OfType<UsagePricingDigits>().Single().DisplayText);
        Assert.Equal(" per year for seats, calls monthly", panel.Children.OfType<TextBlock>().Last().Text);
        block.BillingPeriod = UsagePricingPeriod.Monthly;
        Pump();
        Assert.Equal("Charged monthly, calls included", billed.Content);
        Assert.False(row.IsExpanded);
    }

    [AvaloniaFact]
    public void Segmented_switch_sets_the_period()
    {
        var (block, _) = Show();
        var raised = new List<UsagePricingSelectionEventArgs>();
        block.SelectionChanged += (_, e) => raised.Add(e);
        Part<SegmentedControl>(block, UsagePricing.PeriodPart).SelectedIndex = 1;
        Pump();
        Assert.Equal(UsagePricingPeriod.Yearly, block.BillingPeriod);
        Assert.Equal(UsagePricingPeriod.Yearly, Assert.Single(raised).Period);
        Assert.Equal(356m, raised[0].Total);
        Assert.Equal("Billing period", AutomationProperties.GetName(Part<SegmentedControl>(block, UsagePricing.PeriodPart)));
    }

    [AvaloniaFact]
    public void Trial_starts_survives_same_plan_and_resets_on_plan_change()
    {
        var (block, window) = Show();
        var started = 0;
        var undone = 0;
        block.TrialStarted += (_, _) => started++;
        block.TrialUndone += (_, _) => undone++;
        var action = Part<Button>(block, UsagePricing.ActionPart);
        action.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        Assert.True(block.IsTrialStarted);
        Assert.Equal(1, started);
        Assert.Equal(ButtonVariant.Secondary, action.Variant);
        Assert.True(block.Classes.Contains(UsagePricing.TrialPseudoClass));
        var note = Part<ArcContentSwap>(block, UsagePricing.NotePart);
        Assert.IsType<StackPanel>(note.Content);

        // Pressing again does nothing.
        Press(window, PhysicalKey.Enter);
        Assert.Equal(1, started);

        block.SeatIndex = 11; // still Studio
        Pump();
        Assert.True(block.IsTrialStarted);

        block.SeatIndex = 15; // Fleet
        Pump();
        Assert.False(block.IsTrialStarted);
        Assert.Equal(1, undone);
        Assert.Equal(ButtonVariant.Primary, action.Variant);
        Assert.Equal("Try Fleet", action.Content);
        Assert.Equal("Free for 14 days, then billed as shown", note.Content);
    }

    [AvaloniaFact]
    public void Undo_turns_the_trial_off_and_focuses_the_button()
    {
        var (block, window) = Show();
        Assert.True(block.StartTrialAsync().Result);
        Pump();
        var undo = TestHelpers.Descendant<UsagePricingUndoButton>(block);
        Assert.Equal("Undo", undo.Content);
        undo.Focus(NavigationMethod.Tab);
        Press(window, PhysicalKey.Enter);
        Assert.False(block.IsTrialStarted);
        Assert.True(Part<Button>(block, UsagePricing.ActionPart).IsFocused);
    }

    [AvaloniaFact]
    public void Trial_handler_runs_and_can_fail()
    {
        var gate = new TaskCompletionSource();
        UsagePricingTrialRequest? request = null;
        var (block, _) = Show(b => b.TrialHandler = (r, _) =>
        {
            request = r;
            return gate.Task;
        });
        var failed = 0;
        block.TrialFailed += (_, _) => failed++;
        var run = block.StartTrialAsync();
        Pump();
        Assert.True(block.IsTrialBusy);
        Assert.True(Part<Button>(block, UsagePricing.ActionPart).IsLoading);
        Assert.Equal("Studio", request?.Plan.Name);
        Assert.Equal(16, request?.Seats);
        Assert.Equal(436m, request?.Total);
        gate.SetResult();
        Assert.True(TestHelpers.PumpUntil(() => run.IsCompleted));
        Assert.True(run.Result);
        Assert.True(block.IsTrialStarted);
        Assert.False(block.IsTrialBusy);

        block.UndoTrial();
        block.TrialHandler = UsagePricingSamples.SimulatedTrial(TimeSpan.FromSeconds(1), _ => true, (_, _) => Task.CompletedTask);
        var second = block.StartTrialAsync();
        Assert.True(TestHelpers.PumpUntil(() => second.IsCompleted));
        Assert.False(second.Result);
        Assert.False(block.IsTrialStarted);
        Assert.Equal(1, failed);
    }

    [AvaloniaFact]
    public void Narrow_width_stacks_the_card_between_controls_and_breakdown()
    {
        var (block, window) = Show(width: 600);
        Assert.True(block.Classes.Contains(UsagePricing.StackedPseudoClass));
        var card = Part<Border>(block, UsagePricing.CardPart);
        var breakdown = Part<StackPanel>(block, "PART_Breakdown");
        var controls = Part<StackPanel>(block, "PART_Controls");
        Assert.True(card.Bounds.Top > controls.Bounds.Bottom);
        Assert.True(breakdown.Bounds.Top > card.Bounds.Bottom);
        window.Width = 1000;
        Pump();
        Assert.False(block.Classes.Contains(UsagePricing.StackedPseudoClass));
        Assert.Equal(392, card.Bounds.Width, 1);
        Assert.Equal(0, card.Bounds.Top, 1);
        Assert.True(UsagePricingLayout.Stacks(779.4, 780));
        Assert.False(UsagePricingLayout.Stacks(780, 780));
    }

    [AvaloniaFact]
    public void Inline_plans_replace_the_sample()
    {
        var (block, _) = Show(b =>
        {
            b.SeatStops = [1, 5, 10, 50];
            b.UsageStops = [1_000, 10_000];
            b.SeatIndex = 2;
            b.UsageIndex = 0;
            b.Currency = "EUR";
            b.Plans.Add(new UsagePricingPlan("Solo", 9, 1, 5_000, 1, 5, 5_000) { Features = { new UsagePricingFeature("a", "Alpha", "1") } });
            b.Plans.Add(new UsagePricingPlan("Crew", 19, 3, 50_000, 1, null, null) { Features = { new UsagePricingFeature("b", "Beta") } });
        });
        Assert.Equal("Crew", block.RecommendedPlan?.Name);
        Assert.Equal(190m, block.EstimatedTotal);
        Assert.Equal("€190", Part<UsagePricingDigits>(block, UsagePricing.PricePart).DisplayText);
        Assert.Equal(["b"], Part<UsagePricingFeatureList>(block, UsagePricing.FeaturesPart).Rows.Select(r => r.Key));
        block.Plans[1].SeatPrice = 20;
        Pump();
        Assert.Equal(200m, block.EstimatedTotal);
    }

    [AvaloniaFact]
    public void Custom_labels_split_around_the_amount()
    {
        var (block, _) = Show(b => b.Labels = new UsagePricingLabels { Saving = a => $"Save {a} every year", SeatsRow = p => $"Seats on {p}" });
        Assert.Equal("Save ", Part<TextBlock>(block, UsagePricing.SavingBeforePart).Text);
        Assert.Equal(" every year", Part<TextBlock>(block, UsagePricing.SavingAfterPart).Text);
        Assert.Equal("Seats on ", Part<TextBlock>(block, UsagePricing.SeatsRowBeforePart).Text);
        Assert.False(Part<TextBlock>(block, UsagePricing.SeatsRowAfterPart).IsVisible);
    }

    [AvaloniaFact]
    public void Digits_roll_with_blur_and_settle_sharp()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var digits = new UsagePricingDigits { FontSize = 52, MaxBlur = 2, Value = 980, Format = v => UsagePricingMath.Money(v, "USD", System.Globalization.CultureInfo.GetCultureInfo("en-US")) };
        TestHelpers.Show(digits, ArcMotionLevel.Full);
        Pump();
        Assert.Equal("$980", digits.DisplayText);
        Assert.Equal(0, digits.CurrentMaxBlur);
        digits.Value = 1_000;
        Pump();
        // Places keyed by value: hundreds 9 -> 0 and tens 8 -> 0 turn up (one and two steps), a thousands column grows.
        Assert.Equal([1, 10, 10, 0], digits.WheelTargets);
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
        Pump();
        Assert.True(digits.CurrentMaxBlur > 0);
        Assert.True(digits.CurrentMaxBlur <= 2);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        Pump();
        Assert.Equal(0, digits.CurrentMaxBlur);
        Assert.Equal("$1,000", digits.DisplayText);
        Assert.Equal([1, 10, 10, 0], digits.WheelPositions.Select(p => Math.Round(p, 3)));

        // Going down turns the wheels down.
        digits.Value = 990;
        Pump();
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        Pump();
        Assert.Equal("$990", digits.DisplayText);
        Assert.Equal([9, 9, 0], digits.WheelPositions.Select(p => Math.Round(p, 3)));
    }

    [AvaloniaFact]
    public void Feature_list_slides_kept_lines_and_springs_its_height()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (block, _) = Show(b => b.SeatIndex = 14, level: ArcMotionLevel.Full);
        var list = Part<UsagePricingFeatureList>(block, UsagePricing.FeaturesPart);
        var links = list.Rows.Single(r => r.Key == "links");
        block.SeatIndex = 15; // Studio -> Fleet: two new lines on top.
        Pump();
        Assert.Equal(7, list.Rows.Count);
        Assert.Same(links, list.Rows[2]);
        Assert.Equal(-72, links.Shift.Y, 1);
        Assert.Equal(10 + 7 * 36, list.TargetHeight);
        Assert.True(list.CurrentHeight < list.TargetHeight);
        var sso = list.Rows[0];
        Assert.True(sso.Opacity < 0.5);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        Pump();
        Assert.Equal(0, links.Shift.Y, 1);
        Assert.Equal(1, sso.Opacity, 2);
        Assert.Equal(list.TargetHeight, list.CurrentHeight, 1);
        var meter = Part<UsagePricingMeter>(block, UsagePricing.MeterPart);
        Assert.Equal([1, 1, 1], meter.Fills.Select(f => Math.Round(f, 2)));

        block.SeatIndex = 14; // back: the two lines leave but stay painted while they fade.
        Pump();
        Assert.Equal(5, list.Rows.Count);
        Assert.Equal(7, list.PaintedRows.Count());
        Assert.Equal(72, links.Shift.Y, 1);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        Pump();
        Assert.True(TestHelpers.PumpUntil(() => list.PaintedRows.Count() == 5));
        Assert.Equal([1, 1, 0], meter.Fills.Select(f => Math.Round(f, 2)));
    }

    [AvaloniaFact]
    public void Reduced_motion_jumps_layout_but_fades_new_lines()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (block, _) = Show(b => b.SeatIndex = 14, level: ArcMotionLevel.Reduced);
        var list = Part<UsagePricingFeatureList>(block, UsagePricing.FeaturesPart);
        block.SeatIndex = 15;
        Pump();
        Assert.Equal(list.TargetHeight, list.CurrentHeight);
        Assert.All(list.Rows.Skip(2), r => Assert.Equal(0, r.Shift.Y));
        Assert.True(list.Rows[0].Opacity < 1);
        clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
        Pump();
        Assert.Equal(1, list.Rows[0].Opacity, 2);
        Assert.Equal(0, list.Rows[0].Shift.Y);
    }

    [AvaloniaFact]
    public void Saving_mark_opens_and_draws_its_check()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (block, _) = Show(level: ArcMotionLevel.Full);
        var mark = Part<UsagePricingSavingMark>(block, UsagePricing.SavingMarkPart);
        Assert.Equal(0, mark.Bounds.Width);
        block.BillingPeriod = UsagePricingPeriod.Yearly;
        Pump();
        clock.Advance(TimeSpan.FromMilliseconds(60), Frame);
        Pump();
        Assert.True(mark.OpenAmount is > 0 and < 1);
        Assert.Equal(0, mark.StrokeProgress, 2);
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        Pump();
        Assert.Equal(1, mark.OpenAmount, 2);
        Assert.Equal(1, mark.StrokeProgress, 2);
        Assert.Equal(UsagePricingSavingMark.OpenWidth, mark.Bounds.Width, 1);
        block.BillingPeriod = UsagePricingPeriod.Monthly;
        Pump();
        clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
        Pump();
        Assert.Equal(0, mark.OpenAmount, 2);
    }
}
