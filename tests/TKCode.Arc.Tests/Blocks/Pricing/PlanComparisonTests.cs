using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// PlanComparison (port of Arc's free plan-comparison): the width tiers, the yearly saving, the counts, billing,
/// the differences filter, selection with its rail, announcement, event and command, and the motion of prices, rows and
/// the rail under a manual clock.
/// </summary>
public class PlanComparisonTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Calls { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Calls.Add(parameter);
    }

    private static Window Show(PlanComparison block, double width = 1100, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(block, level);
        window.Width = width;
        window.Height = 1000;
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

    private static void Click(Window window, Control control)
    {
        Settle();
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    #region Pure rules

    [Theory]
    [InlineData(1058, 0)]
    [InlineData(721, 0)]
    [InlineData(720, 1)]
    [InlineData(684, 1)]
    [InlineData(541, 1)]
    [InlineData(540, 2)]
    [InlineData(504, 2)]
    [InlineData(341, 2)]
    [InlineData(340, 3)]
    [InlineData(328, 3)]
    public void Size_Tiers_Follow_Arcs_Thresholds(double width, int expected)
        => PlanComparisonMath.SizeFor(width).ShouldBe((PlanComparisonSize)expected);

    [Fact]
    public void Yearly_Saving_Is_The_Largest_Of_Any_Plan()
    {
        // Team 13 → 10 saves 23.1%, Studio 27 → 22 saves 18.5%: "save up to 23%" as Arc writes it.
        PlanComparisonMath.MaxYearlySaving(PlanComparisonSamples.Plans()).ShouldBe(23);
        PlanComparisonMath.MaxYearlySaving([new PlanComparisonPlan("A", null, 10, 12)]).ShouldBe(0);
        PlanComparisonMath.MaxYearlySaving([new PlanComparisonPlan("A", null, 0, 0)]).ShouldBe(0);
        PlanComparisonMath.MaxYearlySaving([new PlanComparisonPlan("A", null, 20, 15)]).ShouldBe(25);
    }

    [Fact]
    public void Counts_Round_Like_Arc()
    {
        PlanComparisonMath.CountDecimals(13, 10).ShouldBe(0);
        PlanComparisonMath.CountDecimals(13, 9.5m).ShouldBe(2);
        PlanComparisonMath.CountValue(13, 10, 0, 0).ShouldBe(13);
        PlanComparisonMath.CountValue(13, 10, 0.5, 0).ShouldBe(12);
        PlanComparisonMath.CountValue(13, 10, 0.84, 0).ShouldBe(10);
        PlanComparisonMath.CountValue(13, 10, 1, 0).ShouldBe(10);
        PlanComparisonMath.CountValue(10m, 9.5m, 0.5, 2).ShouldBe(9.75m);
    }

    [Fact]
    public void Two_Sample_Features_Are_Shared()
    {
        var features = PlanComparisonSamples.Features();
        features.Count.ShouldBe(8);
        PlanComparisonMath.VisibleCount(features, false).ShouldBe(8);
        PlanComparisonMath.VisibleCount(features, true).ShouldBe(6);
        features.Where(f => f.IsShared).Select(f => f.Label).ShouldBe(["Active projects", "Source exports"]);
    }

    #endregion

    #region Defaults and data

    [AvaloniaFact]
    public void Zero_Configuration_Shows_The_Sample_Offering()
    {
        var block = new PlanComparison();
        var window = Show(block);
        try
        {
            block.Size.ShouldBe(PlanComparisonSize.Large);
            block.PriceTexts.Select(t => t.Text).ShouldBe(["$13", "$27"]);
            block.SelectButtons.Select(b => b.Content as string).ShouldBe(["Select Team", "Select Studio"]);
            block.Rows.Count.ShouldBe(8);
            block.Rows.All(r => r.IsVisible).ShouldBeTrue();
            block.FeatureCountText!.Text.ShouldBe("8 of 8 features");
            block.BillingNoteText!.Text.ShouldBe("Billed month to month");
            block.VisibleFeatureCount.ShouldBe(8);
            block.Rail!.HasTarget.ShouldBeFalse();
            // Every row but the last draws its divider.
            block.Rows.Select(r => r.HasDivider).ShouldBe([true, true, true, true, true, true, true, false]);
            // The plan columns get Arc's 1fr each beside the 1.1fr feature column.
            var cells = block.PlanCells;
            cells[0].Bounds.Width.ShouldBe(cells[1].Bounds.Width, 1);
            cells[0].Bounds.X.ShouldBeGreaterThan(cells[0].Bounds.Width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_Plans_And_Features_Replace_The_Samples()
    {
        var block = new PlanComparison { Currency = "EUR" };
        block.Plans.Add(new PlanComparisonPlan("Solo", "One seat", 9, 7));
        block.Plans.Add(new PlanComparisonPlan("Crew", "Up to ten", 19, 15));
        block.Plans.Add(new PlanComparisonPlan("Fleet", "Any size", 39, 31));
        block.Features.Add(new PlanComparisonFeature("Boards", true, "Unlimited", "Unlimited", "Unlimited"));
        block.Features.Add(new PlanComparisonFeature("Exports", false, "Not included", "Included", "Included"));
        var window = Show(block);
        try
        {
            block.PriceTexts.Select(t => t.Text).ShouldBe(["€9", "€19", "€39"]);
            block.Rows.Count.ShouldBe(2);
            block.FeatureCountText!.Text.ShouldBe("2 of 2 features");
            block.PlanCells.Count.ShouldBe(3);

            block.PlansSource = [new PlanComparisonPlan("Only", null, 5, 4)];
            Settle();
            block.PriceTexts.Select(t => t.Text).ShouldBe(["€5"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Localize_Text_And_Culture()
    {
        var block = new PlanComparison
        {
            Currency = "EUR",
            Labels = new PlanComparisonLabels
            {
                Culture = CultureInfo.GetCultureInfo("de-DE"),
                Select = name => $"{name} wählen",
                FeatureCount = (shown, total) => $"{shown} von {total}"
            }
        };
        block.Plans.Add(new PlanComparisonPlan("Team", null, 1234.5m, 1000));
        var window = Show(block);
        try
        {
            block.PriceTexts[0].Text.ShouldBe("€1.234,50");
            block.SelectButtons[0].Content.ShouldBe("Team wählen");
            block.FeatureCountText!.Text.ShouldBe("8 von 8");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Price_Change_On_A_Plan_Reaches_The_Block()
    {
        var plan = new PlanComparisonPlan("Team", null, 13, 10);
        var block = new PlanComparison();
        block.Plans.Add(plan);
        var window = Show(block);
        try
        {
            plan.MonthlyPrice = 15;
            Settle();
            block.PriceTexts[0].Text.ShouldBe("$15");
            plan.Name = "Crew";
            Settle();
            block.SelectButtons[0].Content.ShouldBe("Select Crew");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Billing and filter

    [AvaloniaFact]
    public void Yearly_Billing_Shows_Yearly_Prices_And_The_Saving()
    {
        var block = new PlanComparison();
        var events = new List<PlanComparisonBillingChangedEventArgs>();
        block.BillingChanged += (_, e) => events.Add(e);
        var window = Show(block);
        try
        {
            var segment = block.GetVisualDescendants().OfType<SegmentedItem>().First(s => (s.Content as string) == "Yearly");
            Click(window, segment);
            block.Billing.ShouldBe(PlanComparisonBilling.Yearly);
            block.PriceTexts.Select(t => t.Text).ShouldBe(["$10", "$22"]);
            block.BillingNoteText!.Text.ShouldBe("Billed yearly, save up to 23%");
            events.Count.ShouldBe(1);
            events[0].OldBilling.ShouldBe(PlanComparisonBilling.Monthly);
            events[0].NewBilling.ShouldBe(PlanComparisonBilling.Yearly);

            block.Billing = PlanComparisonBilling.Monthly;
            Settle();
            block.PriceTexts.Select(t => t.Text).ShouldBe(["$13", "$27"]);
            block.GetVisualDescendants().OfType<SegmentedControl>().First().SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Differences_Only_Hides_The_Shared_Rows()
    {
        var block = new PlanComparison();
        var window = Show(block);
        try
        {
            var toggle = block.GetVisualDescendants().OfType<Switch>().First();
            Click(window, toggle);
            block.IsDifferencesOnly.ShouldBeTrue();
            block.Rows.Where(r => !r.IsVisible).Select(r => r.Feature.Label).ShouldBe(["Active projects", "Source exports"]);
            block.FeatureCountText!.Text.ShouldBe("6 of 8 features");
            block.VisibleFeatureCount.ShouldBe(6);
            block.Rows.Last().HasDivider.ShouldBeFalse();

            block.Reset();
            Settle();
            block.Rows.All(r => r.IsVisible).ShouldBeTrue();
            toggle.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Selection

    [AvaloniaFact]
    public void Selecting_A_Plan_Tints_It_Announces_It_And_Reports_It()
    {
        var command = new RecordingCommand();
        var block = new PlanComparison { SelectCommand = command };
        var events = new List<PlanComparisonSelectionChangedEventArgs>();
        block.SelectedPlanChanged += (_, e) => events.Add(e);
        var window = Show(block);
        try
        {
            var team = block.EffectivePlans[0];
            Click(window, block.SelectButtons[0]);
            block.SelectedPlan.ShouldBeSameAs(team);
            block.SelectButtons[0].Content.ShouldBe("Selected");
            block.SelectButtons[0].Variant.ShouldBe(ButtonVariant.Primary);
            block.SelectButtons[1].Variant.ShouldBe(ButtonVariant.Secondary);
            block.PlanCells[0].Classes.Contains(":selected").ShouldBeTrue();
            block.PlanCells[1].Classes.Contains(":selected").ShouldBeFalse();
            block.Announcer!.Announcement.ShouldBe("Team selected");
            events.Single().IsUserAction.ShouldBeTrue();
            events.Single().NewPlan.ShouldBeSameAs(team);
            command.Calls.ShouldBe([team]);
            block.Rail!.HasTarget.ShouldBeTrue();
            block.Rail.CurrentRect.X.ShouldBe(block.PlanCells[0].Bounds.X, 0.5);
            block.Rail.CurrentRect.Width.ShouldBe(block.PlanCells[0].Bounds.Width, 0.5);

            // Pressing the chosen plan again clears the choice and the rail goes at once.
            Click(window, block.SelectButtons[0]);
            block.SelectedPlan.ShouldBeNull();
            block.SelectButtons[0].Content.ShouldBe("Select Team");
            block.Announcer.Announcement.ShouldBeNull();
            block.Rail.CurrentOpacity.ShouldBe(0);
            command.Calls.Count.ShouldBe(2);
            command.Calls[1].ShouldBeNull();

            // The app selects: event, no command.
            block.Select(block.EffectivePlans[1]);
            Settle();
            events.Last().IsUserAction.ShouldBeFalse();
            command.Calls.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_On_A_Select_Button_Selects_Its_Plan()
    {
        var block = new PlanComparison();
        var window = Show(block);
        try
        {
            block.SelectButtons[1].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            block.SelectedPlan.ShouldBeSameAs(block.EffectivePlans[1]);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Layout

    [AvaloniaTheory]
    [InlineData(1100, 0, true)]
    [InlineData(686, 1, true)]
    [InlineData(506, 2, false)]
    [InlineData(330, 3, false)]
    public void Layout_Follows_The_Blocks_Own_Width(double width, int tier, bool featureColumn)
    {
        var size = (PlanComparisonSize)tier;
        var block = new PlanComparison();
        var window = Show(block, width);
        try
        {
            block.Size.ShouldBe(size);
            block.Classes.Contains(":compact").ShouldBe(size != PlanComparisonSize.Large);
            block.Classes.Contains(":narrow").ShouldBe(!featureColumn);
            var cells = block.PlanCells;
            // Narrow: the plans share the width and the first plan starts at the block's edge.
            if (featureColumn)
            {
                cells[0].Bounds.X.ShouldBeGreaterThan(0);
            }
            else
            {
                cells[0].Bounds.X.ShouldBe(0);
                cells[0].Bounds.Width.ShouldBe(cells[1].Bounds.Width, 1);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Mode_Drops_The_Card()
    {
        var block = new PlanComparison { IsFramed = false };
        var window = Show(block, 1300);
        try
        {
            block.Classes.Contains(":flat").ShouldBeTrue();
            var frame = TestHelpers.Descendant<Border>(block, "PART_Frame");
            frame.BorderThickness.ShouldBe(new Thickness(0));
            frame.Bounds.Width.ShouldBe(1300, 1);
            block.IsFramed = true;
            Settle();
            frame.Bounds.Width.ShouldBe(1060, 1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void Prices_Count_To_The_New_Period_In_240ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var block = new PlanComparison();
        var window = Show(block, level: ArcMotionLevel.Full);
        try
        {
            block.Billing = PlanComparisonBilling.Yearly;
            clock.Advance(TimeSpan.FromMilliseconds(34), Frame);
            var mid = block.PriceTexts[1].Text;
            mid.ShouldNotBe("$27");
            mid.ShouldNotBe("$22");
            // Screen readers get the final price at once.
            Avalonia.Automation.AutomationProperties.GetName(block.PriceTexts[1]).ShouldBe("$22");
            clock.Advance(TimeSpan.FromMilliseconds(260), Frame);
            block.PriceTexts.Select(t => t.Text).ShouldBe(["$10", "$22"]);

            // Interrupted halfway, the count continues from the number on screen.
            block.Billing = PlanComparisonBilling.Monthly;
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            var shown = block.PriceTexts[1].Text;
            block.Billing = PlanComparisonBilling.Yearly;
            clock.Advance(Frame, Frame);
            int.Parse(block.PriceTexts[1].Text!.TrimStart('$')).ShouldBeLessThanOrEqualTo(int.Parse(shown!.TrimStart('$')));
            clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
            block.PriceTexts[1].Text.ShouldBe("$22");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shared_Rows_Collapse_On_The_Smooth_Spring_And_Come_Back()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var block = new PlanComparison();
        var window = Show(block, level: ArcMotionLevel.Full);
        try
        {
            var shared = block.Rows[0];
            block.IsDifferencesOnly = true;
            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            shared.IsVisible.ShouldBeTrue();
            shared.Reveal.ShouldBeInRange(0.05, 0.95);
            shared.Opacity.ShouldBeLessThan(0.6);
            clock.Advance(TimeSpan.FromMilliseconds(1500), Frame);
            shared.IsVisible.ShouldBeFalse();
            block.FeatureCountText!.Text.ShouldBe("6 of 8 features");

            block.IsDifferencesOnly = false;
            clock.Advance(TimeSpan.FromMilliseconds(30), Frame);
            shared.IsVisible.ShouldBeTrue();
            // The fade waits 40ms; the height has started.
            shared.Opacity.ShouldBe(0, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(1000), Frame);
            shared.Reveal.ShouldBe(1, 0.001);
            shared.Opacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_Rows_And_Prices()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var block = new PlanComparison();
        var window = Show(block, level: ArcMotionLevel.Reduced);
        try
        {
            block.IsDifferencesOnly = true;
            block.Billing = PlanComparisonBilling.Yearly;
            clock.Advance(Frame, Frame);
            Settle();
            block.Rows[0].IsVisible.ShouldBeFalse();
            block.PriceTexts[0].Text.ShouldBe("$10");
            block.FeatureCountText!.Text.ShouldBe("6 of 8 features");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Rail_Appears_In_Place_And_Glides_To_The_Next_Plan()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var block = new PlanComparison();
        var window = Show(block, level: ArcMotionLevel.Full);
        try
        {
            var team = block.PlanCells[0].Bounds;
            var studio = block.PlanCells[1].Bounds;
            block.Select(block.EffectivePlans[0]);
            Settle();
            block.Rail!.CurrentRect.X.ShouldBe(team.X, 0.5);
            block.Rail.CurrentOpacity.ShouldBe(1);

            block.Select(block.EffectivePlans[1]);
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            block.Rail.CurrentRect.X.ShouldBeInRange(team.X + 1, studio.X - 1);
            clock.Advance(TimeSpan.FromMilliseconds(1000), Frame);
            block.Rail.CurrentRect.X.ShouldBe(studio.X, 0.5);
            block.Rail.CurrentRect.Height.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
