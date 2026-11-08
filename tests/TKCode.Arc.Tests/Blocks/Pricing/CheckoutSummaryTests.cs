using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// The checkout summary: the arithmetic and money text against the observed numbers, the promo code field (blank codes,
/// refusals, acceptance, removal, focus), the pay state machine (processing, paid, error, retry, cancel, lock) and the
/// motion helpers (shake keyframes, line heights, viewport padding).
/// </summary>
public class CheckoutSummaryTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>A delay the test completes by hand.</summary>
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _pending = new();

        public int Count => _pending.Count;

        public List<TimeSpan> Requested { get; } = new();

        public Task Wait(TimeSpan span, CancellationToken token)
        {
            Requested.Add(span);
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => tcs.TrySetCanceled(token));
            _pending.Add(tcs);
            return tcs.Task;
        }

        public void Release()
        {
            var all = _pending.ToArray();
            _pending.Clear();
            foreach (var tcs in all)
            {
                tcs.TrySetResult();
            }
            Settle();
        }
    }

    private static CheckoutSummary Sample(ManualDelay delay, Func<bool>? fail = null)
    {
        var summary = new CheckoutSummary
        {
            Item = CheckoutSummarySamples.CreateItem(),
            Tax = CheckoutSummarySamples.CreateTax(),
            Labels = CheckoutSummarySamples.Labels,
            CouponValidator = CheckoutSummarySamples.CreateCouponValidator(delay.Wait),
            PayAsync = CheckoutSummarySamples.CreatePayHandler(fail, delay.Wait)
        };
        summary.Adjustments.Add(CheckoutSummarySamples.CreateAdjustment());
        return summary;
    }

    private static Window Show(CheckoutSummary summary, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(summary, level);
        window.Width = 520;
        window.Height = 900;
        Settle();
        return window;
    }

    private static void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static CheckoutSummaryCouponField Field(CheckoutSummary s) => TestHelpers.Descendant<CheckoutSummaryCouponField>(s, CheckoutSummary.CouponPart);

    private static CheckoutSummaryLines Lines(CheckoutSummary s) => TestHelpers.Descendant<CheckoutSummaryLines>(s, CheckoutSummary.LinesPart);

    private static CheckoutSummaryPayButton Pay(CheckoutSummary s) => TestHelpers.Descendant<CheckoutSummaryPayButton>(s, CheckoutSummary.PayPart);

    private static CheckoutSummaryCouponInput Input(CheckoutSummary s) => TestHelpers.Descendant<CheckoutSummaryCouponInput>(s, CheckoutSummaryCouponField.InputPart);

    private static CheckoutSummaryButton Remove(CheckoutSummary s) => TestHelpers.Descendant<CheckoutSummaryButton>(s, CheckoutSummaryCouponField.RemovePart);

    private static string[] OpenKeys(CheckoutSummary s) => Lines(s).OpenRows.Select(r => r.Key).ToArray();

    private static void Click(AvaloniaButton button)
    {
        button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Settle();
    }

    // ---- arithmetic and money ----

    [Fact]
    public void Math_matches_the_reference_numbers()
    {
        // Reference shape: 96 with a −25% line, 8.5% tax, a 10% code on the adjusted subtotal.
        var plain = CheckoutSummaryMath.Compute(96m, [-24m], null, 0.085m);
        Assert.Equal(72m, plain.Adjusted);
        Assert.Equal(6.12m, plain.Tax);
        Assert.Equal(78.12m, plain.Total);
        var coded = CheckoutSummaryMath.Compute(96m, [-24m], CheckoutSummaryCouponResult.ValidPercent("X", 0.10m), 0.085m);
        Assert.Equal(7.20m, coded.Coupon);
        Assert.Equal(5.51m, coded.Tax);
        Assert.Equal(70.31m, coded.Total);
    }

    [Fact]
    public void Math_matches_the_sample_numbers()
    {
        var plain = CheckoutSummaryMath.Compute(120m, [-30m], null, 0.075m);
        Assert.Equal(6.75m, plain.Tax);
        Assert.Equal(96.75m, plain.Total);
        var hello = CheckoutSummaryMath.Compute(120m, [-30m], CheckoutSummarySamples.Check("hello15"), 0.075m);
        Assert.Equal(13.50m, hello.Coupon);
        Assert.Equal(5.74m, hello.Tax);
        Assert.Equal(82.24m, hello.Total);
        var friend = CheckoutSummaryMath.Compute(120m, [-30m], CheckoutSummarySamples.Check(" friend5 "), 0.075m);
        Assert.Equal(5m, friend.Coupon);
        Assert.Equal(6.38m, friend.Tax);
        Assert.Equal(91.38m, friend.Total);
    }

    [Fact]
    public void Math_never_goes_below_zero_and_skips_tax_without_a_rate()
    {
        var big = CheckoutSummaryMath.Compute(10m, [], CheckoutSummaryCouponResult.Valid("BIG", 50m), 0.1m);
        Assert.Equal(10m, big.Coupon);
        Assert.Equal(0m, big.Total);
        var noRate = CheckoutSummaryMath.Compute(40m, [5m], null, null);
        Assert.Equal(0m, noRate.Tax);
        Assert.Equal(45m, noRate.Total);
    }

    [Fact]
    public void Money_uses_a_true_minus_and_one_decimal_rule()
    {
        Assert.Equal("−$24.00", CheckoutSummaryMoney.Format(-24m, "USD", EnUs, 2));
        Assert.Equal("$1,234.50", CheckoutSummaryMoney.Format(1234.5m, "USD", EnUs, 2));
        Assert.Equal(0, CheckoutSummaryMoney.DecimalsFor("USD", [120m, -30m, 90m]));
        Assert.Equal(2, CheckoutSummaryMoney.DecimalsFor("USD", [120m, 6.75m]));
        Assert.Equal(0, CheckoutSummaryMoney.DecimalsFor("JPY", [120.5m]));
        Assert.Equal("7.5%", CheckoutSummaryMoney.Percent(0.075m, EnUs));
        Assert.Equal("8.5%", CheckoutSummaryMoney.Percent(0.085m, EnUs));
        Assert.Equal("20%", CheckoutSummaryMoney.Percent(0.2m, EnUs));
        Assert.Equal("€9.00", CheckoutSummaryMoney.Format(9m, "EUR", EnUs, 2));
        Assert.Equal("CHF 12", CheckoutSummaryMoney.Format(12m, "CHF", EnUs, 0));
        var shape = CheckoutSummaryMoney.ShapeOf("USD", EnUs, 2);
        Assert.Equal("$", shape.Prefix);
        Assert.Equal(string.Empty, shape.Suffix);
    }

    [Fact]
    public void Shake_follows_the_keyframes()
    {
        Assert.Equal(0, CheckoutSummaryMotion.ShakeAt(0), 3);
        Assert.Equal(-6, CheckoutSummaryMotion.ShakeAt(0.2), 3);
        Assert.Equal(6, CheckoutSummaryMotion.ShakeAt(0.4), 3);
        Assert.Equal(-4, CheckoutSummaryMotion.ShakeAt(0.6), 3);
        Assert.Equal(3, CheckoutSummaryMotion.ShakeAt(0.8), 3);
        Assert.Equal(0, CheckoutSummaryMotion.ShakeAt(1), 3);
        Assert.Equal(360, CheckoutSummaryMotion.ShakeDuration.TotalMilliseconds);
    }

    [Fact]
    public void Padding_follows_the_viewport_like_the_reference()
    {
        Assert.Equal(28, CheckoutSummaryCard.PaddingFor(1440, 20, 28, 0.05));
        Assert.Equal(28, CheckoutSummaryCard.PaddingFor(640, 20, 28, 0.05));
        Assert.Equal(26, CheckoutSummaryCard.PaddingFor(520, 20, 28, 0.05), 6);
        Assert.Equal(20, CheckoutSummaryCard.PaddingFor(360, 20, 28, 0.05));
        Assert.Equal(28, CheckoutSummaryCard.PaddingFor(double.NaN, 20, 28, 0.05));
    }

    [Fact]
    public void Smooth_spring_is_arc_smooth()
    {
        Assert.Equal(ArcMotionPresets.Smooth, CheckoutSummaryMotion.Smooth);
        Assert.Equal(2 * Math.PI / (0.4 * 1.2), CheckoutSummaryMotion.Smooth.NaturalFrequency, 2);
        Assert.Equal(1, CheckoutSummaryMotion.Smooth.DampingRatio, 3);
    }

    // ---- the control ----

    [AvaloniaFact]
    public void Shows_the_breakdown_total_and_pay_label()
    {
        var delay = new ManualDelay();
        var s = Sample(delay);
        Show(s);
        Assert.Equal(["subtotal", "line-annual", "tax"], OpenKeys(s));
        Assert.Equal(96.75m, s.Total);
        Assert.Equal("$96.75", s.FormattedTotal);
        Assert.Equal("Pay $96.75", Pay(s).IdleLabel);
        Assert.Equal("Order summary", AutomationProperties.GetName(s));
        var amounts = Lines(s).OpenRows.Select(r => r.Amount ?? string.Empty).ToArray();
        Assert.Equal(["$120.00", "−$30.00", "$6.75"], amounts);
        Assert.Contains(CheckoutSummaryLineRow.DiscountPseudoClass, Lines(s).OpenRows.ElementAt(1).Classes);
        Assert.Equal("Sales tax (7.5%)", Lines(s).OpenRows.Last().Label);
        Assert.Equal(96.75, Lines(s).TotalDigits!.Value, 3);
        Assert.Equal("$", Lines(s).TotalDigits!.Prefix);
        Assert.Equal(2, Lines(s).TotalDigits!.Decimals);
        Assert.Equal("Total $96.75", AutomationProperties.GetName(Lines(s).TotalDigits!));
    }

    [AvaloniaFact]
    public void Whole_amounts_drop_the_decimals()
    {
        var s = new CheckoutSummary { Item = new CheckoutSummaryItem { Name = "Seat", Price = 40m } };
        s.Adjustments.Add(new CheckoutSummaryLine { Id = "fee", Label = "Setup", Amount = 10m });
        Show(s);
        Assert.Equal("$50", s.FormattedTotal);
        Assert.False(Field(s).IsVisible);
    }

    [AvaloniaFact]
    public void Blank_code_does_nothing_and_keeps_focus()
    {
        var delay = new ManualDelay();
        var s = Sample(delay);
        var window = Show(s);
        Input(s).Focus();
        Input(s).Text = "   ";
        Settle();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle();
        Assert.Equal(0, delay.Count);
        Assert.False(Field(s).IsBusy);
        Assert.True(Input(s).IsFocused);
    }

    [AvaloniaFact]
    public void Input_turns_letters_into_capitals()
    {
        var s = Sample(new ManualDelay());
        Show(s);
        Input(s).Text = "hello15";
        Settle();
        Assert.Equal("HELLO15", Input(s).Text);
    }

    [AvaloniaFact]
    public void Refused_code_shows_an_error_that_typing_clears()
    {
        var delay = new ManualDelay();
        var s = Sample(delay);
        var rejected = new List<string>();
        s.CouponRejected += (_, e) => rejected.Add(e.Message ?? string.Empty);
        Show(s);
        Input(s).Text = "NOPE";
        _ = s.ApplyCouponAsync("NOPE");
        Settle();
        Assert.True(Field(s).IsBusy);
        Assert.Equal(CheckoutSummarySamples.CouponCheckDelay, delay.Requested.Single());
        Assert.False(TestHelpers.Descendant<CheckoutSummaryButton>(s, CheckoutSummaryCouponField.ApplyPart).IsEnabled);
        delay.Release();
        Assert.False(Field(s).IsBusy);
        Assert.Equal("We couldn't find that code. Try HELLO15.", Field(s).ErrorMessage);
        Assert.Equal(["We couldn't find that code. Try HELLO15."], rejected);
        Assert.Contains(CheckoutSummaryCouponField.InvalidPseudoClass, Field(s).Classes);
        Assert.Equal(Field(s).ErrorMessage, AutomationProperties.GetHelpText(Input(s)));
        Assert.True(TestHelpers.Descendant<CheckoutSummaryCollapse>(s, CheckoutSummaryCouponField.ErrorHostPart).IsOpen);
        Assert.Null(s.AppliedCoupon);
        Input(s).Text = "NOPE2";
        Settle();
        Assert.Null(Field(s).ErrorMessage);
        Assert.DoesNotContain(CheckoutSummaryCouponField.InvalidPseudoClass, Field(s).Classes);
        Assert.False(TestHelpers.Descendant<CheckoutSummaryCollapse>(s, CheckoutSummaryCouponField.ErrorHostPart).IsOpen);
    }

    [AvaloniaFact]
    public void Accepted_code_adds_the_line_updates_totals_and_moves_focus_to_remove()
    {
        var delay = new ManualDelay();
        var s = Sample(delay);
        var applied = new List<string>();
        s.CouponApplied += (_, e) => applied.Add(e.Code);
        var window = Show(s);
        Input(s).Focus();
        Input(s).Text = "  hello15 ";
        Settle();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle();
        delay.Release();
        Assert.Equal(["HELLO15"], applied);
        Assert.Equal("HELLO15", s.AppliedCoupon?.Code);
        Assert.Equal(["subtotal", "line-annual", "coupon", "tax"], OpenKeys(s));
        Assert.Equal(82.24m, s.Total);
        Assert.Equal(13.50m, s.CouponDiscount);
        Assert.Equal("Pay $82.24", Pay(s).IdleLabel);
        var couponRow = Lines(s).OpenRows.Single(r => r.Key == "coupon");
        Assert.Equal("Hello, 15% off", couponRow.Label);
        Assert.Equal("−$13.50", couponRow.Amount);
        Assert.True(Remove(s).IsFocused);
        Assert.Equal("Remove code HELLO15", AutomationProperties.GetName(Remove(s)));
        Assert.Contains(CheckoutSummary.CouponAppliedPseudoClass, s.Classes);
    }

    [AvaloniaFact]
    public void Removing_the_code_restores_totals_and_focuses_the_input()
    {
        var delay = new ManualDelay();
        var s = Sample(delay);
        var removed = new List<string>();
        s.CouponRemoved += (_, e) => removed.Add(e.Code);
        var window = Show(s);
        _ = s.ApplyCouponAsync("FRIEND5");
        Settle();
        delay.Release();
        Assert.Equal(91.38m, s.Total);
        Remove(s).Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle();
        Assert.Equal(["FRIEND5"], removed);
        Assert.Null(s.AppliedCoupon);
        Assert.Equal(96.75m, s.Total);
        Assert.Equal(["subtotal", "line-annual", "tax"], OpenKeys(s));
        Assert.Equal(string.Empty, Input(s).Text ?? string.Empty);
        Assert.True(Input(s).IsFocused);
    }

    [AvaloniaFact]
    public void Pay_walks_through_processing_to_paid_and_locks_the_code()
    {
        var delay = new ManualDelay();
        var s = Sample(delay);
        var states = new List<CheckoutSummaryPayState>();
        s.PayStateChanged += (_, e) => states.Add(e.NewState);
        Show(s);
        Click(Pay(s));
        Assert.Equal(CheckoutSummaryPayState.Processing, s.PayState);
        Assert.Equal(CheckoutSummarySamples.PaymentDelay, delay.Requested.Single());
        Assert.False(Pay(s).IsEnabled);
        Assert.Equal("Processing", AutomationProperties.GetName(Pay(s)));
        Assert.True(s.IsCouponLocked);
        Assert.False(Input(s).IsEnabled);
        delay.Release();
        Assert.Equal(CheckoutSummaryPayState.Paid, s.PayState);
        Assert.Equal("Paid", AutomationProperties.GetName(Pay(s)));
        Assert.Contains(CheckoutSummaryPayButton.PaidPseudoClass, Pay(s).Classes);
        Assert.False(Pay(s).IsEnabled);
        Assert.True(s.IsCouponLocked);
        Assert.Equal([CheckoutSummaryPayState.Processing, CheckoutSummaryPayState.Paid], states);
        s.ResetPayState();
        Assert.Equal(CheckoutSummaryPayState.Idle, s.PayState);
        Assert.False(s.IsCouponLocked);
        Assert.True(Pay(s).IsEnabled);
    }

    [AvaloniaFact]
    public void Failed_payment_shows_the_error_and_try_again_retries()
    {
        var delay = new ManualDelay();
        var fail = true;
        var s = Sample(delay, () => fail);
        Exception? error = null;
        s.PayStateChanged += (_, e) => error ??= e.Error;
        Show(s);
        Click(Pay(s));
        delay.Release();
        Assert.Equal(CheckoutSummaryPayState.Error, s.PayState);
        Assert.NotNull(error);
        Assert.Equal("Try again", AutomationProperties.GetName(Pay(s)));
        Assert.True(Pay(s).IsEnabled);
        Assert.False(s.IsCouponLocked);
        Assert.True(TestHelpers.Descendant<CheckoutSummaryCollapse>(s, CheckoutSummary.PayErrorHostPart).IsOpen);
        Assert.Equal(s.Labels.PayFailed, TestHelpers.Descendant<TextBlock>(s, CheckoutSummary.PayErrorPart).Text);
        fail = false;
        Click(Pay(s));
        Assert.Equal(CheckoutSummaryPayState.Processing, s.PayState);
        Assert.False(TestHelpers.Descendant<CheckoutSummaryCollapse>(s, CheckoutSummary.PayErrorHostPart).IsOpen);
        delay.Release();
        Assert.Equal(CheckoutSummaryPayState.Paid, s.PayState);
    }

    [AvaloniaFact]
    public void Pay_requested_can_cancel_and_a_missing_handler_leaves_the_outcome_to_the_host()
    {
        var s = new CheckoutSummary { Item = CheckoutSummarySamples.CreateItem() };
        var cancel = true;
        s.PayRequested += (_, e) =>
        {
            Assert.Equal(120m, e.Total);
            e.Cancel = cancel;
        };
        Show(s);
        Click(Pay(s));
        Assert.Equal(CheckoutSummaryPayState.Idle, s.PayState);
        cancel = false;
        Click(Pay(s));
        Assert.Equal(CheckoutSummaryPayState.Processing, s.PayState);
        s.PayState = CheckoutSummaryPayState.Paid;
        Settle();
        Assert.Equal("Paid", AutomationProperties.GetName(Pay(s)));
    }

    [AvaloniaFact]
    public void Disabled_pay_does_not_start()
    {
        var delay = new ManualDelay();
        var s = Sample(delay);
        s.IsPayEnabled = false;
        Show(s);
        Assert.False(Pay(s).IsEnabled);
        _ = s.StartPaymentAsync();
        Settle();
        Assert.Equal(CheckoutSummaryPayState.Idle, s.PayState);
        Assert.Equal(0, delay.Count);
    }

    [AvaloniaFact]
    public void Tax_without_a_rate_shows_its_note_and_stays_out_of_the_total()
    {
        var s = new CheckoutSummary
        {
            Item = new CheckoutSummaryItem { Name = "Pass", Price = 49.5m },
            Tax = new CheckoutSummaryTax { Label = "Tax" }
        };
        Show(s);
        var tax = Lines(s).OpenRows.Last();
        Assert.Equal("Calculated at checkout", tax.Amount);
        Assert.Contains(CheckoutSummaryLineRow.PendingPseudoClass, tax.Classes);
        Assert.Equal(49.5m, s.Total);
    }

    [AvaloniaFact]
    public void Adjustment_changes_update_the_breakdown()
    {
        var s = Sample(new ManualDelay());
        Show(s);
        s.Adjustments.Add(new CheckoutSummaryLine { Id = "credit", Label = "Referral credit", Amount = -10m });
        Settle();
        Assert.Equal(["subtotal", "line-annual", "line-credit", "tax"], OpenKeys(s));
        Assert.Equal(86.00m, s.Total); // 80 + 6.00 tax
        s.Adjustments[0].Amount = -20m;
        Settle();
        Assert.Equal(96.75m, s.Total); // 90 + 6.75
        s.Adjustments.RemoveAt(1);
        Settle();
        Assert.Equal(["subtotal", "line-annual", "tax"], OpenKeys(s));
    }

    [AvaloniaFact]
    public void New_line_opens_on_the_smooth_spring_and_the_label_swaps()
    {
        var clock = new ManualFrameClock();
        using var clockScope = clock.Install();
        var delay = new ManualDelay();
        var s = Sample(delay);
        Show(s, ArcMotionLevel.Full);
        _ = s.ApplyCouponAsync("HELLO15");
        Settle();
        delay.Release();
        var host = Lines(s).OpenRows.Single(r => r.Key == "coupon").GetVisualParent() as CheckoutSummaryCollapse;
        Assert.NotNull(host);
        Settle();
        clock.Advance(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1000.0 / 60));
        Settle();
        Assert.InRange(host!.Fraction, 0.05, 0.95);
        // Two faces live during the label swap.
        Assert.Equal(2, Pay(s).Swap.GetVisualChildren().Count());
        clock.Advance(TimeSpan.FromMilliseconds(900), TimeSpan.FromMilliseconds(1000.0 / 60));
        Settle();
        Assert.Equal(1, host.Fraction, 3);
        Assert.True(TestHelpers.PumpUntil(() => Pay(s).Swap.GetVisualChildren().Count() == 1, 3000));
    }

    [AvaloniaFact]
    public void Reduced_motion_opens_lines_at_once()
    {
        var delay = new ManualDelay();
        var s = Sample(delay);
        Show(s, ArcMotionLevel.Reduced);
        _ = s.ApplyCouponAsync("HELLO15");
        Settle();
        delay.Release();
        Settle();
        var host = (CheckoutSummaryCollapse)Lines(s).OpenRows.Single(r => r.Key == "coupon").GetVisualParent()!;
        Assert.Equal(1, host.Fraction, 3);
    }
}
