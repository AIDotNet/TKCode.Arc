using Avalonia;
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
/// CreditTopUp: defaults from the sample data, the selection model (packs ⇄ slider, ring fades off-pack), keyboard,
/// auto top-up and the health rule, the purchase timeline (processing → done → idle, error → retry), the status line
/// priority, card cycling, breakpoints, the chart readout and the balance odometer (laps, new places, interruptions).
/// </summary>
public class CreditTopUpTests
{
    /// <summary>Waits the test releases by hand (simulated charge, done dwell, notice dwell).</summary>
    private sealed class Gate
    {
        private readonly List<(TimeSpan Time, TaskCompletionSource Done)> _waits = new();

        public int Count => _waits.Count;

        public IReadOnlyList<TimeSpan> Times => _waits.Select(w => w.Time).ToList();

        public Task Wait(TimeSpan time, CancellationToken token)
        {
            var done = new TaskCompletionSource();
            token.Register(() => done.TrySetCanceled(token));
            _waits.Add((time, done));
            return done.Task;
        }

        /// <summary>Releases the oldest wait of <paramref name="time"/>.</summary>
        public void Release(TimeSpan time)
        {
            var index = _waits.FindIndex(w => w.Time == time);
            index.ShouldBeGreaterThanOrEqualTo(0, $"no wait of {time}");
            var wait = _waits[index];
            _waits.RemoveAt(index);
            wait.Done.TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static readonly TimeSpan Charge = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan DoneDwell = TimeSpan.FromMilliseconds(2400);
    private static readonly TimeSpan NoticeDwell = TimeSpan.FromMilliseconds(2600);

    private static (CreditTopUp Card, Window Window, Gate Gate) Show(double width = 520, ArcMotionLevel level = ArcMotionLevel.Off,
        Action<CreditTopUp>? configure = null)
    {
        var gate = new Gate();
        var card = new CreditTopUp { Delay = gate.Wait };
        configure?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (card, window, gate);
    }

    private static T Part<T>(CreditTopUp card, string name) where T : Control =>
        card.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static string? SwapText(CreditTopUp card, string name)
    {
        var swap = Part<ArcContentSwap>(card, name);
        return swap.Content switch
        {
            TextBlock text => text.Text,
            Panel panel => panel.Children.OfType<TextBlock>().LastOrDefault()?.Text,
            _ => null
        };
    }

    [AvaloniaFact]
    public void Defaults_Come_From_The_Samples()
    {
        var (card, _, _) = Show();
        card.Balance.ShouldBe(1_260);
        card.Health.ShouldBe(CreditTopUpHealth.RunningLow);
        card.AverageDailyUsage!.Value.ShouldBe(440.46, 0.01);
        card.DaysLeft.ShouldBe(2);
        card.SelectedCredits.ShouldBe(6_000);
        card.CurrentQuote.Total.ShouldBe(6_600);
        card.PackGroup!.SelectedIndex.ShouldBe(1);
        card.IsSimulated.ShouldBeTrue();
        card.StatusLine.ShouldBe((CreditTopUp.StatusKind.Resting, "Test mode. Credits don't expire."));
        SwapText(card, CreditTopUp.DaysLeftPart).ShouldBe("≈ 2 days at 440/day");
        SwapText(card, CreditTopUp.HealthPart).ShouldBe("Low balance");
        SwapText(card, CreditTopUp.ChartLabelPart).ShouldBe("Past 14 days");
        SwapText(card, CreditTopUp.BonusLinePart).ShouldBeNull(); // a rolling figure, not a text block
        card.TotalFigure!.DisplayText.ShouldBe("6,600 credits");
        card.PriceFigure!.DisplayText.ShouldBe("$48.00");
        Part<TextBlock>(card, CreditTopUp.RatePart).Text.ShouldBe("$8 per 1,000");
        card.ThresholdGroup!.IsEnabled.ShouldBeFalse();
        card.ThresholdGroup.SelectedIndex.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Packs_And_Slider_Share_One_Amount()
    {
        var (card, _, _) = Show();
        var slider = Part<Slider>(card, CreditTopUp.SliderPart);
        slider.Value.ShouldBe(6_000);

        card.PackGroup!.Options[2].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        card.SelectedCredits.ShouldBe(24_000);
        slider.Value.ShouldBe(24_000);
        card.CurrentQuote.Bonus.ShouldBe(3_600);

        // Off every pack: no pack is selected and the ring is gone; the bonus line falls back below the first tier.
        slider.Value = 4_500;
        card.SelectedCredits.ShouldBe(4_500);
        card.PackGroup.SelectedIndex.ShouldBe(-1);
        card.PackGroup.Indicator.CurrentOpacity.ShouldBe(0);
        SwapText(card, CreditTopUp.BonusLinePart).ShouldBe("Bonus from 6,000");

        slider.Value = 1_500;
        card.PackGroup.SelectedIndex.ShouldBe(0);
        card.PackGroup.Indicator.CurrentOpacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Selected_Credits_Snap_To_The_Range()
    {
        var (card, _, _) = Show();
        card.SelectedCredits = 6_250;
        card.SelectedCredits.ShouldBe(6_500);
        card.SelectedCredits = 99_000;
        card.SelectedCredits.ShouldBe(30_000);
        Part<Slider>(card, CreditTopUp.SliderPart).LargeChange.ShouldBe(3_000);
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_The_Pack_And_Wrap()
    {
        var (card, window, _) = Show();
        card.PackGroup!.Options[1].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        card.SelectedCredits.ShouldBe(24_000);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        card.SelectedCredits.ShouldBe(1_500);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        card.SelectedCredits.ShouldBe(24_000);
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        card.SelectedCredits.ShouldBe(1_500);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        card.SelectedCredits.ShouldBe(24_000);
        card.PackGroup.Options[2].IsFocused.ShouldBeTrue();
        // Roving tab stop: only the selected pack.
        KeyboardNavigation.GetIsTabStop(card.PackGroup.Options[2]).ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(card.PackGroup.Options[0]).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Auto_TopUp_Enables_The_Thresholds_Reports_And_Rejudges_Health()
    {
        var (card, _, gate) = Show();
        var events = new List<CreditTopUpAutoSettings>();
        card.AutoTopUpChanged += (_, e) => events.Add(e.Settings);
        var toggle = Part<Switch>(card, CreditTopUp.AutoSwitchPart);
        toggle.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        card.IsAutoTopUpEnabled.ShouldBeTrue();
        card.ThresholdGroup!.IsEnabled.ShouldBeTrue();
        events.ShouldHaveSingleItem().ShouldBe(new CreditTopUpAutoSettings(true, 1_500, 6_600));
        card.StatusLine.ShouldBe((CreditTopUp.StatusKind.Notice, "Auto refill below 1,500 credits"));

        card.ThresholdGroup.Options[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        card.AutoTopUpThreshold.ShouldBe(750);
        card.Health.ShouldBe(CreditTopUpHealth.Healthy);
        SwapText(card, CreditTopUp.HealthPart).ShouldBe("On track");
        events[^1].ShouldBe(new CreditTopUpAutoSettings(true, 750, 6_600));
        card.StatusLine.Text.ShouldBe("Auto refill below 750 credits");

        // The notice leaves after 2.6s (the first notice's wait was superseded).
        gate.Release(NoticeDwell);
        gate.Release(NoticeDwell);
        card.StatusLine.Kind.ShouldBe(CreditTopUp.StatusKind.Resting);

        toggle.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        card.StatusLine.ShouldBe((CreditTopUp.StatusKind.Notice, "Auto refill paused"));
        card.ThresholdGroup.IsEnabled.ShouldBeFalse();
        // The threshold still judges health while auto top-up is off.
        card.Health.ShouldBe(CreditTopUpHealth.Healthy);
    }

    [AvaloniaFact]
    public void Thresholds_Ignore_Picks_While_Auto_Is_Off()
    {
        var (card, _, _) = Show();
        card.ThresholdGroup!.Options[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        card.AutoTopUpThreshold.ShouldBe(1_500);
    }

    [AvaloniaFact]
    public void Simulated_Purchase_Runs_Processing_Done_Idle()
    {
        var (card, _, gate) = Show();
        var purchased = new List<CreditTopUpPurchasedEventArgs>();
        card.Purchased += (_, e) => purchased.Add(e);
        var buy = Part<Button>(card, CreditTopUp.BuyPart);

        _ = card.PurchaseAsync();
        card.Phase.ShouldBe(CreditTopUpPhase.Processing);
        buy.IsLoading.ShouldBeTrue();
        card.StatusLine.ShouldBe((CreditTopUp.StatusKind.Charging, "Charging Nimbus ••3108"));
        gate.Times.ShouldContain(Charge);

        // A second press while charging does nothing, and changing the amount does not change the order.
        _ = card.PurchaseAsync();
        gate.Count.ShouldBe(1);
        card.SelectedCredits = 1_500;

        gate.Release(Charge);
        card.Phase.ShouldBe(CreditTopUpPhase.Done);
        card.Balance.ShouldBe(1_260 + 6_600);
        card.BalanceFigure!.DisplayText.ShouldBe("7,860");
        card.Health.ShouldBe(CreditTopUpHealth.Healthy);
        card.DaysLeft.ShouldBe(17);
        purchased.ShouldHaveSingleItem();
        purchased[0].Order.ShouldBe(new CreditTopUpOrder(6_000, 600, 6_600, 48m, "USD", "card_nimbus_3108"));
        purchased[0].Balance.ShouldBe(7_860);
        card.StatusLine.ShouldBe((CreditTopUp.StatusKind.Done, "6,600 credits added. Test mode, no charge."));
        buy.IsLoading.ShouldBeFalse();

        gate.Release(DoneDwell);
        card.Phase.ShouldBe(CreditTopUpPhase.Idle);
        card.StatusLine.Kind.ShouldBe(CreditTopUp.StatusKind.Resting);
    }

    [AvaloniaFact]
    public void A_Failed_Charge_Shows_The_Message_And_Retries()
    {
        var calls = 0;
        var (card, _, _) = Show(configure: c => c.PurchaseHandler = (_, _) =>
        {
            calls++;
            return calls == 1 ? Task.FromException(new InvalidOperationException("Card declined. Try another card.")) : Task.CompletedTask;
        });
        var failures = new List<string>();
        card.PurchaseFailed += (_, e) => failures.Add(e.Message);
        card.StatusLine.Text.ShouldBe("Credits don't expire.");

        _ = card.PurchaseAsync();
        Dispatcher.UIThread.RunJobs();
        card.Phase.ShouldBe(CreditTopUpPhase.Error);
        card.ErrorMessage.ShouldBe("Card declined. Try another card.");
        card.StatusLine.ShouldBe((CreditTopUp.StatusKind.Error, "Card declined. Try another card."));
        Part<Button>(card, CreditTopUp.BuyPart).Content.ShouldBe("Retry");
        failures.ShouldHaveSingleItem();
        card.Balance.ShouldBe(1_260);

        _ = card.PurchaseAsync();
        Dispatcher.UIThread.RunJobs();
        card.Phase.ShouldBe(CreditTopUpPhase.Done);
        card.Balance.ShouldBe(7_860);
        card.StatusLine.Text.ShouldBe("6,600 credits added.");
    }

    [AvaloniaFact]
    public void Changing_The_Amount_Clears_An_Error()
    {
        var (card, _, _) = Show(configure: c => c.PurchaseHandler = (_, _) => Task.FromException(new InvalidOperationException("Nope")));
        _ = card.PurchaseAsync();
        Dispatcher.UIThread.RunJobs();
        card.Phase.ShouldBe(CreditTopUpPhase.Error);
        card.SelectedCredits = 24_000;
        card.Phase.ShouldBe(CreditTopUpPhase.Idle);
        card.ErrorMessage.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Change_Cycles_The_Cards()
    {
        var (card, _, _) = Show();
        var changed = new List<string>();
        card.PaymentMethodChanged += (_, e) => changed.Add(e.PaymentMethod.Id);
        var change = Part<Button>(card, CreditTopUp.ChangePart);
        change.IsVisible.ShouldBeTrue();
        Avalonia.Automation.AutomationProperties.GetName(change).ShouldBe("Change card, 2 saved");

        card.ChangePaymentMethod();
        card.SelectedPaymentMethodId.ShouldBe("card_orbit_7720");
        card.ChangePaymentMethod();
        card.SelectedPaymentMethodId.ShouldBe("card_nimbus_3108");
        changed.ShouldBe(["card_orbit_7720", "card_nimbus_3108"]);

        card.PaymentMethods = [CreditTopUpSamples.PaymentMethods()[1]];
        change.IsVisible.ShouldBeFalse();
        card.PaymentMethods = [];
        card.SelectedPaymentMethod.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Card_Width()
    {
        var (card, window, _) = Show(width: 600);
        HasPseudo(card, ":narrow").ShouldBeFalse();
        window.Width = 440;
        Dispatcher.UIThread.RunJobs();
        HasPseudo(card, ":narrow").ShouldBeTrue();
        HasPseudo(card, ":compact").ShouldBeFalse();
        card.Chart!.IsStretched.ShouldBeTrue();
        window.Width = 410;
        Dispatcher.UIThread.RunJobs();
        HasPseudo(card, ":compact").ShouldBeTrue();
        window.Width = 470;
        Dispatcher.UIThread.RunJobs();
        HasPseudo(card, ":narrow").ShouldBeFalse();
        card.Chart.IsStretched.ShouldBeFalse();
    }

    private static bool HasPseudo(Control control, string name) => control.Classes.Contains(name);

    [AvaloniaFact]
    public void Pointing_At_A_Day_Reads_It_Out()
    {
        var (card, _, _) = Show();
        card.Chart!.Point(12);
        SwapText(card, CreditTopUp.ChartLabelPart).ShouldBe("Nov 14: 529");
        card.Chart.Point(13);
        SwapText(card, CreditTopUp.ChartLabelPart).ShouldBe("Today: 362");
        card.Chart.Point(-1);
        SwapText(card, CreditTopUp.ChartLabelPart).ShouldBe("Past 14 days");
        Avalonia.Automation.AutomationProperties.GetName(card.Chart)
            .ShouldBe("Daily usage over the past 14 days, averaging 440 credits a day.");
    }

    [AvaloniaFact]
    public void No_Usage_Hides_The_Chart_And_Days_Left()
    {
        var (card, _, _) = Show(configure: c => c.Usage = null);
        Part<Grid>(card, CreditTopUp.ChartBoxPart).IsVisible.ShouldBeFalse();
        Part<ArcContentSwap>(card, CreditTopUp.DaysLeftPart).IsVisible.ShouldBeFalse();
        card.DaysLeft.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Culture_And_Currency_Format_Every_Amount()
    {
        var (card, _, _) = Show(configure: c =>
        {
            c.CultureName = "de-DE";
            c.Pricing = new CreditTopUpPricing { UnitPrice = 0.008m, Currency = "EUR", BonusTiers = CreditTopUpPricing.Default.BonusTiers };
        });
        Part<TextBlock>(card, CreditTopUp.RatePart).Text!.Replace('\u00A0', ' ').ShouldBe("8 € per 1.000");
        card.PriceFigure!.DisplayText.Replace('\u00A0', ' ').ShouldBe("48,00 €");
        card.TotalFigure!.DisplayText.ShouldBe("6.600 credits");
    }

    [AvaloniaFact]
    public void Balance_Odometer_Laps_And_Opens_New_Places()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, _, _) = Show(level: ArcMotionLevel.Full, configure: c => c.Balance = 840);
        var odometer = card.BalanceFigure!;
        odometer.Targets.ShouldBe([8, 4, 0]);

        card.Balance = 1_840;
        // The new leading place shows its digit and opens; every shared place takes one lap.
        odometer.Targets.ShouldBe([1, 18, 14, 10]);
        odometer.Openness[0].ShouldBe(0, 0.001);
        clock.Advance(TimeSpan.FromMilliseconds(450), TimeSpan.FromMilliseconds(1000.0 / 60));
        odometer.Positions[1].ShouldBeInRange(15.0, 17.99);
        clock.Advance(TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(1000.0 / 60));
        odometer.Positions.ShouldBe([1, 18, 14, 10]);
        odometer.Openness.ShouldAllBe(o => Math.Abs(o - 1) < 0.001);
        odometer.DisplayText.ShouldBe("1,840");
    }

    [AvaloniaFact]
    public void Odometer_Converges_After_Interrupted_Rolls()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, _, _) = Show(level: ArcMotionLevel.Full, configure: c => c.Balance = 9_500);
        var odometer = card.BalanceFigure!;
        // Crossing 9,999 ⇄ 10,000 again and again mid-roll (Arc left a place stuck at width 0 here).
        for (var i = 0; i < 6; i++)
        {
            card.Balance = i % 2 == 0 ? 10_500 : 9_500;
            clock.Advance(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(1000.0 / 60));
        }
        card.Balance = 23_000;
        clock.Advance(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(1000.0 / 60));
        TestHelpers.PumpUntil(() => !odometer.IsMoving, 3000).ShouldBeTrue();
        odometer.DisplayText.ShouldBe("23,000");
        odometer.Openness.Count.ShouldBe(6); // 2, 3, comma, 0, 0, 0
        odometer.Openness.ShouldAllBe(o => Math.Abs(o - 1) < 0.001);
        odometer.Positions.Select(p => (int)Math.Round(((p % 10) + 10) % 10)).ShouldBe([2, 3, 0, 0, 0]);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Figures()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, _, _) = Show(level: ArcMotionLevel.Reduced, configure: c => c.Balance = 840);
        card.Balance = 23_840;
        var odometer = card.BalanceFigure!;
        odometer.Positions.ShouldBe(odometer.Targets);
        odometer.Openness.ShouldAllBe(o => Math.Abs(o - 1) < 0.001);
    }

    [AvaloniaFact]
    public void Ring_Glides_Between_Packs_And_Fades_Off_Them()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, _, _) = Show(level: ArcMotionLevel.Full);
        var ring = card.PackGroup!.Indicator;
        var start = ring.CurrentRect.X;
        var end = card.PackGroup.Options[2].Bounds.X;
        card.SelectedCredits = 24_000;
        clock.Advance(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(1000.0 / 60));
        ring.CurrentRect.X.ShouldBeInRange(start + 5, end - 5);
        ring.CurrentOpacity.ShouldBe(1, 0.01);
        clock.Advance(TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(1000.0 / 60));
        ring.CurrentRect.X.ShouldBe(end - 0.25, 0.01);

        // Off every pack: the ring fades where it is.
        card.SelectedCredits = 20_000;
        clock.Advance(TimeSpan.FromMilliseconds(33), TimeSpan.FromMilliseconds(1000.0 / 60));
        ring.CurrentOpacity.ShouldBeInRange(0.05, 0.95);
        ring.CurrentRect.X.ShouldBe(end - 0.25, 0.01);
        clock.Advance(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(1000.0 / 60));
        ring.CurrentOpacity.ShouldBe(0, 0.01);

        // Back on a pack from none: it lands in place and fades in, no travel.
        card.SelectedCredits = 1_500;
        clock.Advance(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(1000.0 / 60));
        ring.CurrentRect.X.ShouldBe(card.PackGroup.Options[0].Bounds.X - 0.25, 0.01);
        ring.CurrentOpacity.ShouldBeLessThan(0.5);
    }
}
