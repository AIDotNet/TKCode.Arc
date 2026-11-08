using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// PricingCalculator: defaults from the samples, the slider's key model and step buttons, the picked plan and its cap,
/// billing (keys, announcements, the yearly offer), the trial flow with a stepped delay, feature rows, the digit roll
/// rule, the badge FLIP, the chasing frame, the stacked fold and reduced motion.
/// </summary>
public class PricingCalculatorTests
{
    private static PricingCalculatorCard Card(PricingCalculator calc, string id) => calc.Cards.First(c => c.Plan.Id == id);

    [AvaloniaFact]
    public void DefaultsShowTheSamplesAtTwentySeatsYearly()
    {
        var calc = new PricingCalculator();
        TestHelpers.Show(calc);
        Assert.Equal(3, calc.Cards.Count);
        Assert.Equal(20, calc.Seats);
        Assert.Equal(PricingCalculatorBilling.Yearly, calc.Billing);
        Assert.Equal("studio", calc.RecommendedPlan!.Id);
        Assert.Equal("studio", calc.SelectedPlan!.Id);
        Assert.Equal(4800, calc.Total);
        Assert.Equal(1200, calc.YearlySavings);
        Assert.Equal(1, Card(calc, "solo").AreaIndex);
        Assert.True(Card(calc, "solo").IsUnavailable);
        Assert.True(Card(calc, "studio").IsSelected);
        Assert.True(Card(calc, "studio").IsRecommended);
        Assert.Equal("Try Studio free", calc.TrialButton!.Content);
        Assert.Equal("$4,800", calc.TotalDigits!.DisplayText);
        Assert.Equal(3, calc.Marks!.Children.Count);
    }

    [AvaloniaFact]
    public void SliderKeysMoveByStop()
    {
        var calc = new PricingCalculator();
        var window = TestHelpers.Show(calc);
        calc.Slider!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.Equal(25, calc.Seats);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal(30, calc.Seats);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        Assert.Equal(50, calc.Seats);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        Assert.Equal(30, calc.Seats);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Assert.Equal(20, calc.Seats);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Assert.Equal(300, calc.Seats);
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Assert.Equal(1, calc.Seats);
        Assert.Equal("1 seat", Avalonia.Automation.AutomationProperties.GetHelpText(calc.Slider));
    }

    [AvaloniaFact]
    public void StepButtonsClampAtTheEnds()
    {
        var calc = new PricingCalculator { SeatIndex = 21 };
        var seats = new List<(int, int)>();
        calc.SeatsChanged += (_, e) => seats.Add((e.OldSeats, e.NewSeats));
        var window = TestHelpers.Show(calc);
        calc.PlusButton!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(300, calc.Seats);
        Assert.Equal([(200, 300)], seats);
        calc.SeatIndex = 0;
        calc.MinusButton!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(1, calc.Seats);
    }

    [AvaloniaFact]
    public void PickedPlanHoldsAndCappedPlanGoesBackToItsCap()
    {
        var calc = new PricingCalculator();
        var picks = new List<(string, int)>();
        calc.PlanPicked += (_, e) => picks.Add((e.Plan.Id, e.Seats));
        TestHelpers.Show(calc);

        calc.PickPlan("scale");
        calc.SetSeats(12);
        Assert.Equal("scale", calc.SelectedPlan!.Id);
        Assert.Equal("studio", calc.RecommendedPlan!.Id);

        // Solo is over its cap at 12 seats: its button reads "Switch to 8 seats" and moves the seats back.
        Assert.Equal("Switch to 8 seats", Avalonia.Automation.AutomationProperties.GetName(Card(calc, "solo").ActionButton!));
        Card(calc, "solo").ActionButton!.Focus(NavigationMethod.Tab);
        var window = (Window)TopLevel.GetTopLevel(calc)!;
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(8, calc.Seats);
        Assert.Equal("solo", calc.SelectedPlan!.Id);
        Assert.Equal("solo", calc.PickedPlanId);
        Assert.True(Card(calc, "solo").ActionButton!.IsFocused);
        Assert.Equal([("scale", 20), ("solo", 8)], picks);

        // Over the cap the selection falls back to the recommendation, the pick is remembered.
        calc.SetSeats(10);
        Assert.Equal("studio", calc.SelectedPlan!.Id);
        Assert.Equal("solo", calc.PickedPlanId);
        calc.SetSeats(8);
        Assert.Equal("solo", calc.SelectedPlan!.Id);
        Assert.Contains("Solo picked", calc.Announcements);
    }

    [AvaloniaFact]
    public void BillingSwitchKeysAnnounceAndOfferYearly()
    {
        var calc = new PricingCalculator();
        var changes = new List<PricingCalculatorBilling>();
        calc.BillingChanged += (_, e) => changes.Add(e.NewBilling);
        var window = TestHelpers.Show(calc);
        calc.BillingSwitch!.YearlyOption!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.Equal(PricingCalculatorBilling.Monthly, calc.Billing);
        Assert.True(calc.BillingSwitch.MonthlyOption!.IsFocused);
        Assert.Equal("Billed monthly", calc.Announcements[^1]);
        Assert.Equal(500, calc.Total);
        Assert.Equal(1200, calc.YearlySavings);
        Assert.Equal(1, calc.SavingsPanel!.SelectedIndex);
        Assert.Equal("Keep $1,200 with annual billing", calc.SwitchYearlyButton!.Content);
        // Only the chosen option is a tab stop.
        Assert.True(calc.BillingSwitch.MonthlyOption.IsTabStop);
        Assert.False(calc.BillingSwitch.YearlyOption.IsTabStop);

        calc.SwitchYearlyButton.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(PricingCalculatorBilling.Yearly, calc.Billing);
        Assert.Equal(0, calc.SavingsPanel.SelectedIndex);
        Assert.Equal("Billed annually, prices are per seat per month", calc.Announcements[^1]);
        Assert.Equal([PricingCalculatorBilling.Monthly, PricingCalculatorBilling.Yearly], changes);
    }

    [AvaloniaFact]
    public void TrialRunsStartsAndResets()
    {
        var gates = new List<TaskCompletionSource>();
        var calc = new PricingCalculator();
        calc.DelayAsync = (_, _) =>
        {
            var gate = new TaskCompletionSource();
            gates.Add(gate);
            return gate.Task;
        };
        PricingCalculatorTrialRequest? started = null;
        calc.TrialStarted += (_, e) => started = e.Request;
        TestHelpers.Show(calc);

        var run = calc.StartTrialAsync();
        Assert.Equal(PricingCalculatorTrialState.Starting, calc.TrialState);
        Assert.True(calc.TrialButton!.IsLoading);
        // A second start while running is ignored.
        Assert.False(calc.StartTrialAsync().Result);

        gates[0].SetResult();
        Dispatcher.UIThread.RunJobs();
        Assert.True(run.IsCompleted && run.Result);
        Assert.Equal(PricingCalculatorTrialState.Started, calc.TrialState);
        Assert.False(calc.TrialButton.IsLoading);
        Assert.Equal("Studio trial is live for 20 seats.", calc.Announcements[^1]);
        Assert.Equal(new PricingCalculatorTrialRequest(calc.SelectedPlan!, 20, PricingCalculatorBilling.Yearly, 4800), started);
        Assert.False(calc.StartTrialAsync().Result);

        gates[1].SetResult();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PricingCalculatorTrialState.Idle, calc.TrialState);
        Assert.Equal("Try Studio free", calc.TrialButton.Content);
    }

    [AvaloniaFact]
    public void FailedTrialGoesBackToIdleAndSaysSo()
    {
        var calc = new PricingCalculator { TrialHandler = (_, _) => Task.FromResult(false) };
        TestHelpers.Show(calc);
        Assert.False(calc.StartTrialAsync().Result);
        Assert.Equal(PricingCalculatorTrialState.Idle, calc.TrialState);
        Assert.Equal(new PricingCalculatorLabels().AnnounceTrialFailed, calc.Announcements[^1]);
    }

    [AvaloniaFact]
    public void FeatureRowsFollowSeatsAndBilling()
    {
        var calc = new PricingCalculator();
        TestHelpers.Show(calc);
        var studio = Card(calc, "studio").Features!;
        Assert.Equal(["All of Solo", "500 GB file storage", "Single sign-on with Google", "Kickoff workshop with our team"], studio.Texts);
        calc.SetSeats(30);
        calc.Billing = PricingCalculatorBilling.Monthly;
        Assert.Equal(["All of Solo", "750 GB file storage", "Single sign-on with Google", "Volume rate, $3 off every seat"], studio.Texts);
        calc.SetSeats(50);
        Assert.Contains("Dedicated account partner", Card(calc, "scale").Features!.Texts);
    }

    [AvaloniaFact]
    public void CustomLabelsCurrencyAndPlans()
    {
        var plan = new PricingCalculatorPlan { Id = "one", Name = "One", MonthlySeatPrice = 10, RecommendFromSeats = 1 };
        plan.Features.Add(new PricingCalculatorFeature("Everything"));
        var calc = new PricingCalculator
        {
            SeatStops = [1, 2, 3],
            SeatIndex = 2,
            Currency = "EUR",
            Labels = new PricingCalculatorLabels { StartTrial = name => $"Start {name}" }
        };
        calc.Plans.Add(plan);
        TestHelpers.Show(calc);
        Assert.Single(calc.Cards);
        Assert.Equal(3, calc.Seats);
        Assert.Equal(288, calc.Total);
        Assert.Equal("€288", calc.TotalDigits!.DisplayText);
        Assert.Equal("Start One", calc.TrialButton!.Content);
    }

    [AvaloniaFact]
    public void StackedLayoutFoldsUnselectedCards()
    {
        var calc = new PricingCalculator { Width = 700 };
        TestHelpers.Show(calc);
        Assert.True(calc.Classes.Contains(PricingCalculator.StackedPseudoClass));
        Assert.True(calc.PlansPanel!.IsStacked);
        Assert.False(Card(calc, "solo").IsExpanded);
        Assert.True(Card(calc, "studio").IsExpanded);
        Assert.False(Card(calc, "scale").IsExpanded);
        calc.PickPlan("scale");
        Assert.True(Card(calc, "scale").IsExpanded);
        Assert.False(Card(calc, "studio").IsExpanded);
        Assert.Equal(PricingCalculatorTotalsMode.TwoRows, calc.TotalsPanel!.Mode);
    }

    [AvaloniaFact]
    public void CompactLayoutStretchesTheSwitchAndStacksTotals()
    {
        var calc = new PricingCalculator { Width = 480 };
        TestHelpers.Show(calc);
        Assert.Equal(PricingCalculatorTotalsMode.Column, calc.TotalsPanel!.Mode);
        Assert.True(calc.BillingSwitch!.IsUniform);
        Assert.True(calc.ControlsPanel!.IsWrapped);
    }

    [AvaloniaFact]
    public void FrameChasesTheSelectedCardOnTheGlideSpring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var calc = new PricingCalculator { Width = 1200 };
        TestHelpers.Show(calc, ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(1000.0 / 60));
        var frame = calc.PlansPanel!.Frame;
        Assert.Equal(Card(calc, "studio").Bounds.Inflate(0.5), frame.CurrentRect);

        calc.PickPlan("scale");
        Dispatcher.UIThread.RunJobs();
        calc.UpdateLayout();
        clock.Advance(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1000.0 / 60));
        var target = Card(calc, "scale").Bounds.Inflate(0.5);
        var mid = frame.CurrentRect;
        Assert.True(mid.X > Card(calc, "studio").Bounds.X && mid.X < target.X, $"mid {mid.X}");
        clock.Advance(TimeSpan.FromSeconds(1.5), TimeSpan.FromMilliseconds(1000.0 / 60));
        Assert.Equal(target.X, frame.CurrentRect.X, 1);
    }

    [AvaloniaFact]
    public void BadgeFlipsFromTheOldCard()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var calc = new PricingCalculator { Width = 1200, SeatIndex = 3 };
        TestHelpers.Show(calc, ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(1000.0 / 60));
        Assert.Equal("solo", calc.RecommendedPlan!.Id);

        calc.SetSeats(5);
        calc.UpdateLayout();
        var studio = Card(calc, "studio");
        // The badge starts where Solo's badge was: one column (≈ card width + gap) to the left.
        Assert.True(studio.BadgeOffset.X < -300, $"offset {studio.BadgeOffset.X}");
        Assert.Equal(0, studio.BadgeOffset.Y, 1);
        clock.Advance(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(1000.0 / 60));
        Assert.True(studio.BadgeOffset.X is < -10 and > -300);
        clock.Advance(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(1000.0 / 60));
        Assert.Equal(0, studio.BadgeOffset.X, 1);
        Assert.False(Card(calc, "solo").Badge!.IsVisible);
    }

    [AvaloniaFact]
    public void ReducedMotionSnapsTheFrameAndBadge()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var calc = new PricingCalculator { Width = 1200, SeatIndex = 3 };
        TestHelpers.Show(calc, ArcMotionLevel.Reduced);
        clock.Advance(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(1000.0 / 60));
        calc.SetSeats(5);
        calc.UpdateLayout();
        Assert.Equal(default, Card(calc, "studio").BadgeOffset);
        Assert.Equal(Card(calc, "studio").Bounds.Inflate(0.5), calc.PlansPanel!.Frame.CurrentRect);
    }
}

/// <summary>The odometer's roll rule, interruption and blur (notes 3.2 and 6.1).</summary>
public class PricingCalculatorDigitsTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(1000.0 / 60);

    [AvaloniaFact]
    public void WheelsTurnInTheDirectionTheNumberMoved()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var digits = new PricingCalculatorDigits { Value = 3840, Prefix = "$", FontSize = 28 };
        TestHelpers.Show(digits, ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromMilliseconds(100), Step);
        Assert.Equal([3.0, 8, 4, 0], digits.WheelTargets);

        // Up: every wheel moves forward (new − old) mod 10 steps: 3→4 (+1), 8→2 (+4), 4→0 (+6), 0→0.
        digits.Value = 4200;
        Assert.Equal([4.0, 12, 10, 0], digits.WheelTargets);
        clock.Advance(TimeSpan.FromSeconds(1.2), Step);
        Assert.Equal([4.0, 12, 10, 0], digits.WheelPositions.Select(p => Math.Round(p, 2)).ToArray());

        // Down: every wheel moves back (old − new) mod 10 steps: 4,200 → 1,152: 4→1 (−3), 2→1 (−1), 0→5 (−5), 0→2 (−8).
        digits.Value = 1152;
        Assert.Equal([1.0, 11, 5, -8], digits.WheelTargets);
        Assert.Equal("$1,152", digits.DisplayText);
    }

    [AvaloniaFact]
    public void ARollInterruptedKeepsGoingWithItsSpeed()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var digits = new PricingCalculatorDigits { Value = 20, FontSize = 18 };
        TestHelpers.Show(digits, ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromMilliseconds(100), Step);
        digits.Value = 25;
        clock.Advance(TimeSpan.FromMilliseconds(120), Step);
        var ones = digits.WheelPositions[1];
        Assert.InRange(ones, 0.5, 5);
        digits.Value = 30;
        // The ones wheel carries on forward to 10 (a whole turn), it does not go back to 0.
        Assert.Equal(10, digits.WheelTargets[1]);
        var previous = ones;
        for (var i = 0; i < 40; i++)
        {
            clock.Advance(Step, Step);
            var now = digits.WheelPositions[1];
            Assert.True(now >= previous - 1e-6, "the wheel never turns back");
            previous = now;
        }
        clock.Advance(TimeSpan.FromSeconds(1), Step);
        Assert.Equal(10, digits.WheelPositions[1], 2);
    }

    [AvaloniaFact]
    public void GlyphsBlurWithTheirDistanceAndNewColumnsGrow()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var digits = new PricingCalculatorDigits { Value = 960, FontSize = 14 };
        TestHelpers.Show(digits, ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromMilliseconds(100), Step);
        Assert.All(digits.GlyphBlurs, b => Assert.Equal(0, b));
        var narrow = digits.DesiredSize.Width;

        digits.Value = 1200;
        clock.Advance(TimeSpan.FromMilliseconds(120), Step);
        Assert.Contains(digits.GlyphBlurs, b => b is > 0 and <= 1.0001);
        // The new thousands column and its separator grow in.
        Assert.Contains(digits.Presences, p => p is > 0 and < 1);
        clock.Advance(TimeSpan.FromSeconds(1.5), Step);
        digits.UpdateLayout();
        Assert.All(digits.GlyphBlurs, b => Assert.Equal(0, b));
        Assert.True(digits.DesiredSize.Width > narrow);
    }

    [AvaloniaFact]
    public void ReducedMotionJumps()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var digits = new PricingCalculatorDigits { Value = 20, FontSize = 18 };
        TestHelpers.Show(digits, ArcMotionLevel.Reduced);
        digits.Value = 25;
        Assert.Equal(5, digits.WheelPositions[1]);
        Assert.All(digits.Presences, p => Assert.Equal(1, p));
    }
}
