using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// LiveCheckout in a window: the sample totals and their recalculation, plans and seats from the keyboard, the coupon flow,
/// the checks before paying, card and wallet payments, a declined card, the receipt, Start over, the layouts by the block's
/// own width, and the measured motion (the glide on the smooth spring, digits rolling straight).
/// </summary>
public class LiveCheckoutTests
{
    private static readonly Func<LiveCheckoutOrder, CancellationToken, Task<LiveCheckoutReceipt?>> InstantPay =
        LiveCheckoutSamples.CreatePaymentSimulator((_, _) => Task.CompletedTask, () => 482915);

    private static readonly Func<string, CancellationToken, Task<LiveCheckoutCouponResult>> InstantCoupons =
        LiveCheckoutSamples.CreateCouponValidator((_, _) => Task.CompletedTask);

    private static LiveCheckout Block() => new()
    {
        PayAsync = InstantPay,
        ValidateCouponAsync = InstantCoupons,
        Clock = new FixedClock()
    };

    private static Window Show(LiveCheckout block, double width = 1100, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new ScrollViewer { Content = block }, level);
        window.Width = width;
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

    private static T Part<T>(LiveCheckout block, string name) where T : Control => TestHelpers.Descendant<T>(block, name);

    private static void Click(Control button)
    {
        button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Settle();
    }

    private static object? Focused(Visual visual) => TopLevel.GetTopLevel(visual)!.FocusManager!.GetFocusedElement();

    [AvaloniaFact]
    public void TheSampleOrderAddsUpAsInTheNotes()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));

        block.Lines.Count.ShouldBe(2);
        block.Lines[0].Amount.ShouldBe(2150.40m);
        block.Lines[0].Detail.ShouldBe("8 seats × $22.40 × 12 months");
        block.Lines[1].Amount.ShouldBe(432.00m);
        block.Lines[1].Detail.ShouldBe("Billed yearly");
        block.Totals.ShouldBe(new LiveCheckoutTotals(2582.40m, 0m, 542.30m, 3124.70m));
        Part<TextBlock>(block, "PART_TaxLabel").Text.ShouldBe("VAT 21%");
    }

    [AvaloniaFact]
    public void EveryInputRecalculatesAndReportsTheTotals()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        var reported = new List<LiveCheckoutTotals>();
        block.TotalsChanged += (_, e) => reported.Add(e.Totals);

        block.Seats = 9;
        block.Totals.Subtotal.ShouldBe(9 * 22.40m * 12 + 432m);

        block.Billing = LiveCheckoutBilling.Monthly;
        block.Lines[0].Detail.ShouldBe("9 seats × $28.00 × 1 month");
        block.Lines[1].Detail.ShouldBe("Billed monthly");
        block.Totals.Subtotal.ShouldBe(9 * 28m + 45m);

        block.CountryCode = "US";
        block.Totals.Tax.ShouldBe(0m);
        Part<TextBlock>(block, "PART_TaxNotCharged").IsVisible.ShouldBeTrue();

        block.SelectedAddonIds.Add("sso");
        block.Lines.Select(l => l.Id).ShouldBe(new[] { "crew", "care", "sso" });

        block.Seats = 1;
        block.Lines[0].Detail.ShouldStartWith("1 seat ×");
        reported.Count.ShouldBeGreaterThanOrEqualTo(5);
        reported[^1].ShouldBe(block.Totals);
    }

    [AvaloniaFact]
    public void SeatsStayBetweenOneAndFiveHundredAndTheButtonsDisableAtTheEnds()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        var stepper = TestHelpers.Descendant<LiveCheckoutSeatStepper>(block);

        block.Seats = 0;
        block.Seats.ShouldBe(1);
        Settle();
        stepper.MinusButton!.IsEnabled.ShouldBeFalse();
        block.Seats = 900;
        block.Seats.ShouldBe(500);
        Settle();
        stepper.PlusButton!.IsEnabled.ShouldBeFalse();
        stepper.MinusButton.IsEnabled.ShouldBeTrue();

        Click(stepper.MinusButton);
        block.Seats.ShouldBe(499);
    }

    [AvaloniaFact]
    public void SeatBoxUpAndDownStepByOneAndShiftByTen()
    {
        var block = Block();
        var window = Show(block);
        using var _ = new WindowScope(window);
        var box = TestHelpers.Descendant<LiveCheckoutSeatStepper>(block).Box!;
        box.Focus(NavigationMethod.Tab);
        Settle();

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Settle();
        block.Seats.ShouldBe(9);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Shift);
        Settle();
        block.Seats.ShouldBe(1);
    }

    [AvaloniaFact]
    public void ArrowKeysMoveAndSelectPlansAndWrapAround()
    {
        var block = Block();
        var window = Show(block);
        using var _ = new WindowScope(window);
        var cards = block.GetVisualDescendants().OfType<LiveCheckoutPlanCard>().ToList();
        cards.Count.ShouldBe(3);
        cards[1].IsChecked.ShouldBe(true);
        cards[1].Focus(NavigationMethod.Tab);
        Settle();

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Settle();
        block.SelectedPlanId.ShouldBe("fleet");
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Settle();
        block.SelectedPlanId.ShouldBe("solo");
        Focused(block).ShouldBeSameAs(cards[0]);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Settle();
        block.SelectedPlanId.ShouldBe("fleet");
        block.Totals.Subtotal.ShouldBe(8 * 39.20m * 12 + 432m);
    }

    [AvaloniaFact]
    public void AnAcceptedCouponBecomesAChipAndADiscountRow()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        var applied = new List<string>();
        block.CouponApplied += (_, e) => applied.Add($"{e.Code} {e.Percent}");
        var slot = TestHelpers.Descendant<LiveCheckoutCouponSlot>(block);

        Click(slot.AddButton!);
        slot.State.ShouldBe(LiveCheckoutCouponState.Editing);
        slot.CodeField!.IsKeyboardFocusWithin.ShouldBeTrue();

        slot.CodeField!.Text = "spring15";
        Settle();
        slot.CodeField.Text.ShouldBe("SPRING15");
        Click(slot.ApplyButton!);
        TestHelpers.PumpUntil(() => block.CouponCode is not null).ShouldBeTrue();

        applied.ShouldBe(new[] { "SPRING15 15" });
        slot.State.ShouldBe(LiveCheckoutCouponState.Applied);
        block.Totals.Discount.ShouldBe(387.36m);
        block.Totals.Tax.ShouldBe(Math.Round((2582.40m - 387.36m) * 0.21m, 2, MidpointRounding.AwayFromZero));
        Part<LiveCheckoutCollapse>(block, "PART_DiscountRow").IsOpen.ShouldBeTrue();
        Focused(block).ShouldBeSameAs(slot.RemoveButton);

        Click(slot.RemoveButton!);
        block.CouponCode.ShouldBeNull();
        block.Totals.Discount.ShouldBe(0m);
        Part<LiveCheckoutCollapse>(block, "PART_DiscountRow").IsOpen.ShouldBeFalse();
        Focused(block).ShouldBeSameAs(slot.AddButton);
    }

    [AvaloniaFact]
    public void ARefusedCouponShowsItsMessageAndEscapeCloses()
    {
        var block = Block();
        var window = Show(block);
        using var _ = new WindowScope(window);
        var slot = TestHelpers.Descendant<LiveCheckoutCouponSlot>(block);
        Click(slot.AddButton!);
        slot.CodeField!.Text = "HELLO";
        Click(slot.ApplyButton!);
        TestHelpers.PumpUntil(() => slot.ErrorRow!.IsOpen).ShouldBeTrue();
        block.CouponCode.ShouldBeNull();
        slot.ErrorText.ShouldBe("No coupon called HELLO.");
        slot.CodeField.IsInvalid.ShouldBeTrue();

        slot.CodeField.Focus(NavigationMethod.Tab);
        Settle();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle();
        slot.State.ShouldBe(LiveCheckoutCouponState.Idle);
        Focused(block).ShouldBeSameAs(slot.AddButton);
    }

    [AvaloniaFact]
    public void PayingWithEmptyCardFieldsShowsEveryErrorAndFocusesTheCardNumber()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        var paid = false;
        block.Paid += (_, _) => paid = true;

        var task = block.SubmitAsync();
        Settle();
        task.IsCompleted.ShouldBeTrue();
        task.Result.ShouldBeFalse();
        paid.ShouldBeFalse();
        var card = Part<Input>(block, "PART_CardNumber");
        card.ErrorMessage.ShouldBe("Enter your card number");
        Part<Input>(block, "PART_Expiry").ErrorMessage.ShouldBe("Use the MM/YY format");
        Part<Input>(block, "PART_Cvc").ErrorMessage.ShouldBe("The 3 digits on the back");
        Part<Input>(block, "PART_Email").ErrorMessage.ShouldBeNull();
        card.IsKeyboardFocusWithin.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void FieldsAreCheckedOnlyAfterTheyWereEditedAndLeft()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        var email = Part<Input>(block, "PART_Email");
        email.Focus(NavigationMethod.Tab);
        email.Text = "mara@";
        Settle();
        email.ErrorMessage.ShouldBeNull();

        Part<Input>(block, "PART_Name").Focus(NavigationMethod.Tab);
        Settle();
        email.ErrorMessage.ShouldBe("Use an address like name@company.com");
        email.Text = "mara@lumen.dev";
        Settle();
        email.ErrorMessage.ShouldBeNull();
        block.Email.ShouldBe("mara@lumen.dev");
    }

    [AvaloniaFact]
    public void ChangingTheCountryChecksATouchedPostalCodeAgain()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        var postal = Part<Input>(block, "PART_Postal");
        postal.Focus(NavigationMethod.Tab);
        postal.Text = "1017 AB";
        Settle();
        block.PostalCode.ShouldBe("1017 AB");
        Part<Input>(block, "PART_City").Focus(NavigationMethod.Tab);
        Settle();
        postal.ErrorMessage.ShouldBeNull();
        block.CountryCode = "FR";
        Settle();
        postal.ErrorMessage.ShouldBe("Use five digits");

        block.CountryCode = "CA";
        Settle();
        postal.ErrorMessage.ShouldBe("Use the A1A 1A1 format");
    }

    [AvaloniaFact]
    public void TypingACardShapesItAndNamesTheNetwork()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        var card = Part<Input>(block, "PART_CardNumber");
        card.Text = "378282246310005";
        Settle();
        card.Text.ShouldBe("3782 822463 10005");
        var expiry = Part<Input>(block, "PART_Expiry");
        expiry.Text = "2";
        Settle();
        expiry.Text.ShouldBe("02/");
    }

    [AvaloniaFact]
    public void ACardPaymentShowsTheReceiptAndFocusesItsHeading()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        LiveCheckoutPaidEventArgs? paid = null;
        block.Paid += (_, e) => paid = e;

        block.FillTestCard();
        Part<Input>(block, "PART_CardNumber").Text.ShouldBe("4111 1111 1111 1111");
        var result = block.SubmitAsync();
        TestHelpers.PumpUntil(() => result.IsCompleted).ShouldBeTrue();
        Settle();

        result.Result.ShouldBeTrue();
        block.IsPaid.ShouldBeTrue();
        block.Classes.Contains(":paid").ShouldBeTrue();
        paid.ShouldNotBeNull();
        paid.Order.PaymentMethod.ShouldBe(LiveCheckoutPaymentMethod.Card);
        paid.Order.CardNumber.ShouldBe("4111111111111111");
        paid.Order.Totals.Total.ShouldBe(3124.70m);
        block.Receipt!.OrderNumber.ShouldBe("LW-482915");
        block.Receipt.PaymentText.ShouldBe("Novacard ending 1111");
        block.Receipt.Email.ShouldBe(LiveCheckoutSamples.Billing.Email);
        Part<TextBlock>(block, "PART_MetaDate").Text.ShouldBe("Nov 8, 2026");
        Part<TextBlock>(block, "PART_GrandLabel").Text.ShouldBe("Paid today");
        Part<TextBlock>(block, "PART_ReceiptText").Text.ShouldBe("Crew is now live for 8 seats.");
        Part<LiveCheckoutCollapse>(block, "PART_CouponRow").IsOpen.ShouldBeFalse();
        Part<LiveCheckoutHeading>(block, "PART_ReceiptTitle").IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void AWalletSkipsTheCardChecksAndNamesTheWalletOnTheReceipt()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        LiveCheckoutOrder? order = null;
        block.Paid += (_, e) => order = e.Order;
        var kite = block.GetVisualDescendants().OfType<LiveCheckoutButton>().First(b => b.Tag is LiveCheckoutWallet { Id: "kite" });

        Click(kite);
        TestHelpers.PumpUntil(() => block.IsPaid).ShouldBeTrue();
        order!.PaymentMethod.ShouldBe(LiveCheckoutPaymentMethod.Wallet);
        order.WalletId.ShouldBe("kite");
        order.CardNumber.ShouldBeEmpty();
        block.Receipt!.PaymentText.ShouldBe("Kite Wallet");
    }

    [AvaloniaFact]
    public void ADeclinedCardShowsTheMessageUntilTheNextEdit()
    {
        var block = Block();
        block.TestCard = LiveCheckoutSamples.DeclinedCard();
        using var _ = new WindowScope(Show(block));
        string? failure = null;
        block.PaymentFailed += (_, e) => failure = e.Message;

        block.FillTestCard();
        var result = block.SubmitAsync();
        TestHelpers.PumpUntil(() => result.IsCompleted).ShouldBeTrue();
        Settle();

        result.Result.ShouldBeFalse();
        block.IsPaid.ShouldBeFalse();
        block.IsProcessing.ShouldBeFalse();
        failure.ShouldBe(LiveCheckoutSamples.DeclineMessage);
        Part<LiveCheckoutCollapse>(block, "PART_AlertRow").IsOpen.ShouldBeTrue();
        Part<TextBlock>(block, "PART_Alert").Text.ShouldBe(LiveCheckoutSamples.DeclineMessage);

        Part<Input>(block, "PART_Cvc").Text = "738";
        Settle();
        Part<LiveCheckoutCollapse>(block, "PART_AlertRow").IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void ProcessingDisablesTheFormUntilTheHandlerAnswers()
    {
        var gate = new TaskCompletionSource<LiveCheckoutReceipt?>();
        var block = Block();
        block.PayAsync = (_, _) => gate.Task;
        using var _ = new WindowScope(Show(block));
        block.FillTestCard();

        var result = block.SubmitAsync();
        Settle();
        block.IsProcessing.ShouldBeTrue();
        Part<Input>(block, "PART_Email").IsEnabled.ShouldBeFalse();
        block.SubmitAsync().Result.ShouldBeFalse();

        gate.SetResult(null);
        TestHelpers.PumpUntil(() => result.IsCompleted).ShouldBeTrue();
        block.IsProcessing.ShouldBeFalse();
        block.IsPaid.ShouldBeTrue();
        block.Receipt!.OrderNumber!.Length.ShouldBe(6);
    }

    [AvaloniaFact]
    public void StartOverPutsEveryValueBackAndFocusesTheFirstWallet()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        var restarted = 0;
        block.Restarted += (_, _) => restarted++;
        block.Seats = 12;
        block.Billing = LiveCheckoutBilling.Monthly;
        block.SelectedAddonIds.Add("archive");
        block.CountryCode = "JP";
        block.PostalCode = "150-0001";
        block.FillTestCard();
        var result = block.SubmitAsync();
        TestHelpers.PumpUntil(() => result.IsCompleted).ShouldBeTrue();
        block.IsPaid.ShouldBeTrue();

        Click(Part<LiveCheckoutButton>(block, "PART_StartOver"));

        restarted.ShouldBe(1);
        block.IsPaid.ShouldBeFalse();
        block.Receipt.ShouldBeNull();
        block.Seats.ShouldBe(8);
        block.Billing.ShouldBe(LiveCheckoutBilling.Yearly);
        block.SelectedAddonIds.ShouldBe(new[] { "care" });
        block.CountryCode.ShouldBe("NL");
        block.PostalCode.ShouldBe(LiveCheckoutSamples.Billing.PostalCode);
        Part<Input>(block, "PART_CardNumber").Text.ShouldBeNullOrEmpty();
        Part<TextBlock>(block, "PART_GrandLabel").Text.ShouldBe("Due today");
        Part<LiveCheckoutCollapse>(block, "PART_CouponRow").IsOpen.ShouldBeTrue();
        block.Totals.Total.ShouldBe(3124.70m);
        var first = block.GetVisualDescendants().OfType<LiveCheckoutButton>().First(b => b.Tag is LiveCheckoutWallet);
        Focused(block).ShouldBeSameAs(first);
    }

    [AvaloniaFact]
    public void ReceiptPdfRaisesTheRequestOnce()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block));
        var requested = new List<string?>();
        block.ReceiptRequested += (_, e) => requested.Add(e.Receipt.OrderNumber);
        block.FillTestCard();
        var result = block.SubmitAsync();
        TestHelpers.PumpUntil(() => result.IsCompleted).ShouldBeTrue();

        var pdf = Part<LiveCheckoutButton>(block, "PART_ReceiptPdf");
        Click(pdf);
        requested.ShouldBe(new[] { "LW-482915" });
        Avalonia.Automation.AutomationProperties.GetName(pdf).ShouldBe("Downloaded");
    }

    [AvaloniaFact]
    public void TheLayoutFollowsTheBlocksOwnWidth()
    {
        var block = Block();
        var window = Show(block, 1100);
        using var _ = new WindowScope(window);
        block.IsNarrowLayout.ShouldBeFalse();
        block.Classes.Contains(":narrow").ShouldBeFalse();
        var summary = Part<Border>(block, "PART_Summary");
        summary.Bounds.Width.ShouldBe(380, 0.5);

        window.Width = 700;
        Settle();
        block.IsNarrowLayout.ShouldBeTrue();
        block.Classes.Contains(":narrow").ShouldBeTrue();
        block.Classes.Contains(":stacked").ShouldBeFalse();
        Part<LiveCheckoutCollapse>(block, "PART_BodyRow").IsOpen.ShouldBeFalse();

        block.IsSummaryOpen = true;
        Settle();
        Part<LiveCheckoutCollapse>(block, "PART_BodyRow").IsOpen.ShouldBeTrue();
        block.Classes.Contains(":summary-open").ShouldBeTrue();

        window.Width = 420;
        Settle();
        block.Classes.Contains(":stacked").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void ThePaidSummaryGlidesToTheCentreOnTheSmoothSpring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, 1100 + 48, ArcMotionLevel.Full);
        using var _ = new WindowScope(window);
        var summary = Part<Border>(block, "PART_Summary");
        var before = summary.TranslatePoint(default, window)!.Value;

        block.FillTestCard();
        var result = block.SubmitAsync();
        TestHelpers.PumpUntil(() => result.IsCompleted).ShouldBeTrue();
        block.IsPaid.ShouldBeTrue();

        // The layout is already centred; the transform carries the card from where it was.
        var start = block.GlideOffset;
        start.X.ShouldBeGreaterThan(100);
        // The sticky summary sat 24 px below the top of the scrolled view; the receipt starts at the top.
        start.Y.ShouldBe(24, 1);
        var layoutX = summary.TranslatePoint(default, window)!.Value.X - start.X;
        (layoutX + start.X).ShouldBe(before.X, 1);
        (layoutX + summary.Bounds.Width / 2).ShouldBe(block.TranslatePoint(new Point(block.Bounds.Width / 2, 0), window)!.Value.X, 1);

        // Smooth (critically damped, ω ≈ 12.5): about 54% left after 120 ms, under 2% after 500 ms, settled by 900 ms.
        clock.Advance(TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(1000.0 / 60));
        (block.GlideOffset.X / start.X).ShouldBeInRange(0.4, 0.7);
        clock.Advance(TimeSpan.FromMilliseconds(380), TimeSpan.FromMilliseconds(1000.0 / 60));
        (block.GlideOffset.X / start.X).ShouldBeLessThan(0.05);
        (block.GlideOffset.Y / start.Y).ShouldBeLessThan(0.05);
        clock.Advance(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000.0 / 60));
        block.GlideOffset.X.ShouldBe(0, 0.1);
    }

    [AvaloniaFact]
    public void DigitsRollStraightToTheNewValueWithoutWrapping()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var amount = new LiveCheckoutAmount { Value = 7m, Currency = "USD", FontSize = 14 };
        var window = TestHelpers.Show(amount, ArcMotionLevel.Full);
        using var _ = new WindowScope(window);
        Settle();
        amount.Positions[0].ShouldBe(7, 0.001);

        amount.Value = 0m;
        clock.Advance(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(1000.0 / 60));
        var mid = amount.Positions[0];
        mid.ShouldBeInRange(0.5, 6.9);
        clock.Advance(TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(1000.0 / 60));
        amount.Positions[0].ShouldBe(0, 0.01);

        // A new leading digit appears at once; the units keep rolling from their old value.
        amount.Value = 13m;
        amount.Positions[0].ShouldBe(1, 0.001);
        amount.Positions[1].ShouldBe(0, 0.2);
        clock.Advance(TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(1000.0 / 60));
        amount.Positions[1].ShouldBe(3, 0.01);
    }

    [AvaloniaFact]
    public void ReducedMotionShowsTheReceiptAtOnce()
    {
        var block = Block();
        using var _ = new WindowScope(Show(block, 1100, ArcMotionLevel.Reduced));
        block.FillTestCard();
        var result = block.SubmitAsync();
        TestHelpers.PumpUntil(() => result.IsCompleted).ShouldBeTrue();
        Settle();
        block.GlideOffset.ShouldBe(default);
        Part<Control>(block, "PART_Meta").Opacity.ShouldBe(1);
        Part<Control>(block, "PART_Form").IsHitTestVisible.ShouldBeFalse();
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class WindowScope(Window window) : IDisposable
    {
        public void Dispose() => window.Close();
    }
}
