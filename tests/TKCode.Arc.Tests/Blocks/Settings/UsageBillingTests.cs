using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Settings;

/// <summary>
/// UsageBilling: overage and cost per meter, the totals and the projection under a fixed clock, the limit's threshold
/// states, recalculation when a meter changes, the days drawn, and the limit setting's event.
/// </summary>
public class UsageBillingTests
{
    /// <summary>A clock stopped at one instant (UTC), so "today" is fixed.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly DateTimeOffset October20 = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static UsageBillingMeter ApiCalls() => new()
    {
        Name = "API calls", Unit = "calls", Used = 1_240_000, Included = 1_000_000, UnitPrice = 0.40m, PriceUnit = 10_000
    };

    private static UsageBillingMeter Storage() => new()
    {
        Name = "Storage", Unit = "GB", Used = 182, Included = 250, UnitPrice = 0.10m
    };

    private static UsageBillingMeter BuildMinutes() => new()
    {
        Name = "Build minutes", Unit = "min", Used = 3_420, Included = 3_000, UnitPrice = 0.008m
    };

    /// <summary>The §1.6 period: Oct 1–31, $90 base fee, three meters, a $250 limit, seen on Oct 20.</summary>
    private static UsageBilling Larkspur(DateTimeOffset? now = null)
    {
        var block = new UsageBilling
        {
            PeriodStart = new DateTime(2026, 10, 1),
            PeriodEnd = new DateTime(2026, 10, 31),
            BaseFee = 90,
            SpendLimit = 250,
            IsSpendLimitEnabled = true,
            Clock = new FixedClock(now ?? October20)
        };
        block.Meters.Add(ApiCalls());
        block.Meters.Add(Storage());
        block.Meters.Add(BuildMinutes());
        return block;
    }

    private static Window ShowWide(UsageBilling block)
    {
        var window = TestHelpers.Show(block);
        window.Width = 900;
        window.Height = 1600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void A_Meter_Prices_Only_What_Goes_Past_The_Included_Amount()
    {
        var api = ApiCalls();
        api.Overage.ShouldBe(240_000);
        api.Cost.ShouldBe(9.60m);

        var storage = Storage();
        storage.Overage.ShouldBe(0);
        storage.Cost.ShouldBe(0m);

        var build = BuildMinutes();
        build.PriceUnit.ShouldBe(1);
        build.Overage.ShouldBe(420);
        build.Cost.ShouldBe(3.36m);

        // A price unit that is not positive counts as one unit.
        build.PriceUnit = 0;
        build.PriceUnit.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Meter_Announces_Its_Computed_Values_When_Usage_Changes()
    {
        var api = ApiCalls();
        var changed = new List<string?>();
        api.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        api.Used += 50_000;

        changed.ShouldBe([nameof(UsageBillingMeter.Used), nameof(UsageBillingMeter.Overage), nameof(UsageBillingMeter.Cost)]);
        api.Cost.ShouldBe(11.60m);
    }

    [AvaloniaFact]
    public void Totals_Add_The_Base_Fee_To_Every_Meters_Cost()
    {
        var block = Larkspur();
        block.MeteredTotal.ShouldBe(12.96m);
        block.CurrentTotal.ShouldBe(102.96m);
    }

    [AvaloniaFact]
    public void The_Projection_Scales_The_Metered_Total_By_The_Share_Of_The_Period_Begun()
    {
        // Oct 20 is day 20 of 31: 90 + 12.96 × 31 / 20.
        Larkspur().ProjectedTotal.ShouldBe(110.088m);

        // Before the period starts the share is one day; after it ends the projection is what was spent.
        Larkspur(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero)).ProjectedTotal.ShouldBe(90m + 12.96m * 31);
        Larkspur(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero)).ProjectedTotal.ShouldBe(102.96m);
    }

    [AvaloniaFact]
    public void The_Summary_Reads_The_Period_And_The_Days_Left()
    {
        var block = Larkspur();
        var window = ShowWide(block);
        try
        {
            var summary = TestHelpers.Descendant<SettingsGroup>(block, UsageBilling.SummaryGroupPart);
            summary.Header.ShouldBe("This billing period");
            summary.Description.ShouldBe("Oct 1 – Oct 31, 2026 · 11 days left");
            TestHelpers.Descendant<TextBlock>(block, UsageBilling.ProjectedPart).Text.ShouldBe("Projected $110.09");
            var spent = TestHelpers.Descendant<AnimatedCounter>(block, UsageBilling.SpentPart);
            spent.Value.ShouldBe(102.96, 1e-9);
            spent.Prefix.ShouldBe("$");
            spent.Decimals.ShouldBe(2);

            block.Clock = new FixedClock(new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero));
            summary.Description.ShouldBe("Oct 1 – Oct 31, 2026 · Last day");
            block.Clock = new FixedClock(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
            summary.Description.ShouldBe("Oct 1 – Oct 31, 2026 · Period ended");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Limit_Status_Follows_The_Alert_Threshold_And_The_Limit()
    {
        var block = Larkspur();
        var window = ShowWide(block);
        try
        {
            var bar = TestHelpers.Descendant<UsageBillingBar>(block, UsageBilling.LimitBarPart);
            var percent = TestHelpers.Descendant<TextBlock>(block, UsageBilling.LimitPercentPart);
            block.LimitStatus.ShouldBe(UsageBillingLimitStatus.Ok);
            bar.Marker.ShouldBe(0.8, 1e-9);
            percent.Text.ShouldBe("41%");
            TestHelpers.Descendant<TextBlock>(block, UsageBilling.LimitCaptionPart).Text.ShouldBe("$102.96 of $250.00 limit");

            // $102.96 of $120 is 86%: past the 80% alert.
            block.SpendLimit = 120;
            block.LimitStatus.ShouldBe(UsageBillingLimitStatus.Near);
            block.Classes.Contains(UsageBilling.LimitNearPseudoClass).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(bar.FillBrush).ShouldBe(ArcTokens.GetColor(block, DesignTokens.ArcDesignTokenKind.Warning));
            TestHelpers.Descendant<Badge>(block, UsageBilling.StatusBadgePart).Content.ShouldBe("Near limit");

            block.AlertThreshold = 0.9;
            block.LimitStatus.ShouldBe(UsageBillingLimitStatus.Ok);

            block.SpendLimit = 100;
            block.LimitStatus.ShouldBe(UsageBillingLimitStatus.Reached);
            block.Classes.Contains(UsageBilling.LimitReachedPseudoClass).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(bar.FillBrush).ShouldBe(ArcTokens.GetColor(block, DesignTokens.ArcDesignTokenKind.Danger));
            bar.FillEnd.ShouldBe(1);

            block.IsSpendLimitEnabled = false;
            block.LimitStatus.ShouldBe(UsageBillingLimitStatus.None);
            block.Classes.Contains(UsageBilling.NoLimitPseudoClass).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            bar.IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(block, UsageBilling.LimitCaptionPart).Text.ShouldBe("No spending limit");
            TestHelpers.Descendant<Avalonia.Controls.Button>(block, UsageBilling.SetLimitPart).IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_A_Meter_Updates_The_Totals_The_Row_And_Its_Bar()
    {
        var block = Larkspur();
        var window = ShowWide(block);
        try
        {
            var api = block.Meters[0];
            api.Used += 50_000;

            block.MeteredTotal.ShouldBe(14.96m);
            block.CurrentTotal.ShouldBe(104.96m);
            block.ProjectedTotal.ShouldBe(90m + 14.96m * 31 / 20);
            TestHelpers.Descendant<TextBlock>(block, UsageBilling.UsageTotalPart).Text.ShouldBe("$14.96");

            var charges = block.DescendantsOf<UsageBillingMeterCharge>();
            charges[0].Amount.ShouldBe("$11.60");
            charges[0].Rate.ShouldBe("$0.40 / 10K calls");
            charges[1].Amount.ShouldBe("Included");
            charges[1].IsIncluded.ShouldBeTrue();
            charges[2].Rate.ShouldBe("$0.008 / min");

            // The bar spans the 1.29M used: the included million first, the overage after it.
            var bar = block.DescendantsOf<UsageBillingBar>().First(b => b.Classes.Contains("meter"));
            bar.FillEnd.ShouldBe(1);
            bar.Split.ShouldBe(1_000_000 / 1_290_000.0, 1e-9);
            AutomationProperties.GetName(bar).ShouldBe("API calls: 1.3M of 1M calls included, 290K over");

            var rows = block.DescendantsOf<SettingsRow>().Where(r => r.Content is UsageBillingMeterCharge).ToList();
            rows[0].Description.ShouldBe("1.3M of 1M calls included");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Meters_Added_Or_Removed_Change_The_Rows_And_The_Totals()
    {
        var block = Larkspur();
        var window = ShowWide(block);
        try
        {
            var usage = TestHelpers.Descendant<SettingsGroup>(block, UsageBilling.UsageGroupPart);
            usage.Items.OfType<SettingsRow>().Count().ShouldBe(3);
            var firstRow = usage.Items.OfType<SettingsRow>().First();

            var api = block.Meters[0];
            block.Meters.RemoveAt(0);
            block.MeteredTotal.ShouldBe(3.36m);
            usage.Items.OfType<SettingsRow>().Count().ShouldBe(2);

            // A removed meter is no longer observed.
            api.Used = 5_000_000;
            block.MeteredTotal.ShouldBe(3.36m);

            block.Meters.Add(new UsageBillingMeter { Name = "Seats", Unit = "seats", Used = 7, Included = 6, UnitPrice = 15 });
            block.MeteredTotal.ShouldBe(18.36m);
            usage.Items.OfType<SettingsRow>().Select(r => r.Header).ShouldBe(["Storage", "Build minutes", "Seats"]);
            usage.Items.OfType<SettingsRow>().ShouldNotContain(firstRow);

            // Pay as you go: nothing included, every unit is charged and there is no bar to compare against.
            block.Meters.Add(new UsageBillingMeter { Name = "Messages", Unit = "messages", Used = 2_000, UnitPrice = 0.01m });
            block.MeteredTotal.ShouldBe(38.36m);
            var messages = usage.Items.OfType<SettingsRow>().Last();
            messages.Description.ShouldBe("2K messages");
            usage.Items.OfType<UsageBillingBar>().Last().IsVisible.ShouldBeFalse();

            block.Meters.Clear();
            usage.IsVisible.ShouldBeFalse();
            block.CurrentTotal.ShouldBe(90m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_The_Days_Through_Today_Are_Drawn()
    {
        var block = Larkspur();
        var days = new AvaloniaList<double>(Enumerable.Repeat(5.0, 21));
        block.DailySpend = days;
        var window = ShowWide(block);
        try
        {
            var chart = TestHelpers.Descendant<BarChart>(block, UsageBilling.ChartPart);
            var points = chart.ItemsSource!.Cast<ChartPoint>().ToList();
            points.Count.ShouldBe(20);
            points[0].X.ShouldBe(new DateTime(2026, 10, 1));
            points[^1].X.ShouldBe(new DateTime(2026, 10, 20));
            chart.Period.ShouldBe("Oct 1 – Oct 20, 2026");
            chart.ValueFormatter!(new ChartValueContext(745, 3, null, null)).ShouldBe("$7.45");
            TestHelpers.Descendant<SettingsGroup>(block, UsageBilling.DailyGroupPart).IsVisible.ShouldBeTrue();

            // An observable list redraws as it changes.
            days[19] = 7;
            chart.ItemsSource!.Cast<ChartPoint>().Last().Value.ShouldBe(700);

            // A later clock draws more of the days.
            block.Clock = new FixedClock(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
            chart.ItemsSource!.Cast<ChartPoint>().Count().ShouldBe(21);

            block.DailySpend = null;
            TestHelpers.Descendant<SettingsGroup>(block, UsageBilling.DailyGroupPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Switch_Turns_The_Limit_Off_Collapses_The_Editor_And_Reports_It()
    {
        var block = Larkspur();
        var window = ShowWide(block);
        try
        {
            var events = new List<UsageBillingLimitChangedEventArgs>();
            block.SpendLimitChanged += (_, e) => events.Add(e);
            var toggle = TestHelpers.Descendant<Switch>(block, UsageBilling.LimitSwitchPart);
            var editor = TestHelpers.Descendant<SpringSizeDecorator>(block, "PART_LimitEditor");
            toggle.IsChecked.ShouldBe(true);
            editor.IsExpanded.ShouldBeTrue();
            KeyboardNavigation.GetTabNavigation(editor).ShouldBe(KeyboardNavigationMode.Continue);
            AutomationProperties.GetName(toggle).ShouldBe("Spending limit");

            toggle.IsChecked = false;

            block.IsSpendLimitEnabled.ShouldBeFalse();
            editor.IsExpanded.ShouldBeFalse();
            // The collapsed field keeps no Tab stop.
            KeyboardNavigation.GetTabNavigation(editor).ShouldBe(KeyboardNavigationMode.None);
            events.Count.ShouldBe(1);
            events[0].IsEnabled.ShouldBeFalse();
            events[0].OldLimit.ShouldBe(250m);
            events[0].NewLimit.ShouldBe(250m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Limit_Field_Edits_The_Limit_And_Reports_An_Edit_Once()
    {
        var block = Larkspur();
        var window = ShowWide(block);
        try
        {
            var events = new List<UsageBillingLimitChangedEventArgs>();
            block.SpendLimitChanged += (_, e) => events.Add(e);
            var input = TestHelpers.Descendant<MoneyInput>(block, UsageBilling.LimitInputPart);
            input.Value.ShouldBe(250m);

            // Set from code: reported at once.
            block.SpendLimit = 300;
            input.Value.ShouldBe(300m);
            events.Count.ShouldBe(1);
            events[0].OldLimit.ShouldBe(250m);
            events[0].NewLimit.ShouldBe(300m);
            events[0].IsEnabled.ShouldBeTrue();

            // Edited in the field: one report when the field lets go, from the value before the edit.
            input.Focus();
            Dispatcher.UIThread.RunJobs();
            // Typing sets the field's current value, as each keystroke would.
            input.SetCurrentValue(MoneyInput.ValueProperty, 3m);
            input.SetCurrentValue(MoneyInput.ValueProperty, 32m);
            input.SetCurrentValue(MoneyInput.ValueProperty, 320m);
            block.SpendLimit.ShouldBe(320m);
            events.Count.ShouldBe(1);

            TestHelpers.Descendant<Switch>(block, UsageBilling.LimitSwitchPart).Focus();
            Dispatcher.UIThread.RunJobs();
            events.Count.ShouldBe(2);
            events[1].OldLimit.ShouldBe(300m);
            events[1].NewLimit.ShouldBe(320m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Set_A_Limit_Turns_The_Limit_On()
    {
        var block = Larkspur();
        block.IsSpendLimitEnabled = false;
        var window = ShowWide(block);
        try
        {
            var button = TestHelpers.Descendant<Avalonia.Controls.Button>(block, UsageBilling.SetLimitPart);
            button.IsVisible.ShouldBeTrue();
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            block.IsSpendLimitEnabled.ShouldBeTrue();
            block.LimitStatus.ShouldBe(UsageBillingLimitStatus.Ok);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Amounts_Follow_The_Currency()
    {
        var block = Larkspur();
        block.Currency = "CHF";
        var window = ShowWide(block);
        try
        {
            var spent = TestHelpers.Descendant<AnimatedCounter>(block, UsageBilling.SpentPart);
            spent.Prefix.ShouldBeNull();
            spent.Suffix.ShouldBe(" CHF");
            TestHelpers.Descendant<TextBlock>(block, UsageBilling.UsageTotalPart).Text.ShouldBe("12.96 CHF");

            block.Currency = "EUR";
            spent.Prefix.ShouldBe("€");
            spent.Suffix.ShouldBeNull();
            block.DescendantsOf<UsageBillingMeterCharge>()[0].Amount.ShouldBe("€9.60");
            var input = TestHelpers.Descendant<MoneyInput>(block, UsageBilling.LimitInputPart);
            input.Currency.ShouldBe("EUR");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Block_Is_Collected_While_Its_Meters_And_Days_Live_On()
    {
        var meters = new System.Collections.ObjectModel.ObservableCollection<UsageBillingMeter> { ApiCalls(), Storage() };
        var shared = BuildMinutes();
        var days = new AvaloniaList<double> { 1.5, 2.25, 3 };
        var withList = TestHelpers.ShowAndClose(() => new UsageBilling { Clock = new FixedClock(October20), Meters = meters, DailySpend = days });
        // A meter the host keeps, in the block's own list.
        var withShared = TestHelpers.ShowAndClose(() =>
        {
            var block = new UsageBilling { Clock = new FixedClock(October20) };
            block.Meters.Add(shared);
            return block;
        });
        TestHelpers.Collect(withList, withShared);
        withList.IsAlive.ShouldBeFalse();
        withShared.IsAlive.ShouldBeFalse();
        meters.Add(BuildMinutes());
        meters[0].Used += 1_000;
        shared.Used += 1_000;
        days.Add(4);
    }

    [AvaloniaFact]
    public void A_Block_Put_Back_Totals_What_Changed_While_It_Was_Away()
    {
        var meters = new System.Collections.ObjectModel.ObservableCollection<UsageBillingMeter> { ApiCalls(), Storage(), BuildMinutes() };
        var days = new AvaloniaList<double> { 4, 5, 6 };
        var block = new UsageBilling
        {
            PeriodStart = new DateTime(2026, 10, 1),
            PeriodEnd = new DateTime(2026, 10, 31),
            BaseFee = 90,
            Clock = new FixedClock(October20),
            Meters = meters,
            DailySpend = days
        };
        var host = new Border { Child = block };
        var window = TestHelpers.Show(host);
        window.Width = 900;
        window.Height = 1600;
        Dispatcher.UIThread.RunJobs();
        try
        {
            block.MeteredTotal.ShouldBe(12.96m);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            // API calls: 1.29M calls, 290k over at $0.40 per 10k = $11.60; build minutes leave.
            meters[0].Used += 50_000;
            meters.RemoveAt(2);
            days.Add(7);

            host.Child = block;
            Dispatcher.UIThread.RunJobs();
            block.MeteredTotal.ShouldBe(11.60m);
            block.CurrentTotal.ShouldBe(101.60m);
            TestHelpers.Descendant<BarChart>(block, UsageBilling.ChartPart).ItemsSource!.Cast<object>().Count().ShouldBe(4);

            // Back in the window, it listens again: storage goes 50 GB over at $0.10.
            meters[1].Used = 300;
            block.MeteredTotal.ShouldBe(16.60m);
            days.Add(8);
            TestHelpers.Descendant<BarChart>(block, UsageBilling.ChartPart).ItemsSource!.Cast<object>().Count().ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Block_Stacks_The_Limit_Under_The_Spend()
    {
        var block = Larkspur();
        var window = ShowWide(block);
        try
        {
            block.Classes.Contains(UsageBilling.NarrowPseudoClass).ShouldBeFalse();
            var column = TestHelpers.Descendant<StackPanel>(block, "PART_LimitColumn");
            Grid.GetRow(column).ShouldBe(0);

            window.Width = 420;
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(UsageBilling.NarrowPseudoClass).ShouldBeTrue();
            Grid.GetRow(column).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}

internal static class UsageBillingTestExtensions
{
    public static List<T> DescendantsOf<T>(this Visual root) where T : Visual =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>().ToList();
}
