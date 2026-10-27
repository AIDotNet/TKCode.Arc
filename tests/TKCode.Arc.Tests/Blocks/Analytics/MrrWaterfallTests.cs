using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>MRR waterfall: the block's behaviour (headless): numbers, selection, keys, preview, switches, motion, breakpoints.</summary>
public class MrrWaterfallTests
{
    private static MrrMovement Move(MrrMovementKind kind, string label, string shortLabel, double amount, int count, params (string Name, double Amount)[] accounts)
    {
        var movement = new MrrMovement { Kind = kind, Label = label, ShortLabel = shortLabel, CompactLabel = shortLabel[..Math.Min(4, shortLabel.Length)], Amount = amount, AccountCount = count };
        foreach (var (name, value) in accounts)
        {
            movement.Accounts.Add(new MrrAccount(name, value, "detail of " + name));
        }
        return movement;
    }

    private static MrrPeriod Q2()
    {
        var period = new MrrPeriod
        {
            Key = "q2", Label = "Q2", LongLabel = "Q2 2026", Caption = "Apr 1 – Jun 30, 2026", StartDateText = "Apr 1", EndDateText = "Jun 30",
            StartingMrr = 286_400, StartingAccounts = 892, EndingAccounts = 948
        };
        period.Movements.Add(Move(MrrMovementKind.New, "New business", "New", 39_800, 116, ("A", 6_000), ("B", 5_000), ("C", 4_000)));
        period.Movements.Add(Move(MrrMovementKind.Expansion, "Expansion", "Expansion", 21_670, 88, ("A", 2_000)));
        period.Movements.Add(Move(MrrMovementKind.Reactivation, "Reactivation", "Reactivation", 4_820, 19));
        period.Movements.Add(Move(MrrMovementKind.Contraction, "Contraction", "Contraction", 9_470, 41));
        period.Movements.Add(Move(MrrMovementKind.Churn, "Churn", "Churn", 18_460, 79, ("D", 3_000)));
        period.StartingBreakdown.Add(new MrrAccount("Business", 100_000, null, 100));
        period.StartingBreakdown.Add(new MrrAccount("Team", 186_400, null, 792));
        period.EndingBreakdown.Add(new MrrAccount("Business", 120_000, null, 120));
        period.EndingBreakdown.Add(new MrrAccount("Team", 204_760, null, 828));
        return period;
    }

    private static MrrPeriod Jun()
    {
        var period = new MrrPeriod
        {
            Key = "jun", Label = "Jun", LongLabel = "June 2026", Caption = "Jun 1 – 30, 2026", StartDateText = "Jun 1", EndDateText = "Jun 30",
            StartingMrr = 308_120, StartingAccounts = 924, EndingAccounts = 948
        };
        period.Movements.Add(Move(MrrMovementKind.New, "New business", "New", 15_370, 40, ("B", 3_000), ("E", 2_500)));
        period.Movements.Add(Move(MrrMovementKind.Expansion, "Expansion", "Expansion", 6_110, 28));
        period.Movements.Add(Move(MrrMovementKind.Reactivation, "Reactivation", "Reactivation", 2_280, 8));
        period.Movements.Add(Move(MrrMovementKind.Contraction, "Contraction", "Contraction", 2_350, 13));
        period.Movements.Add(Move(MrrMovementKind.Churn, "Churn", "Churn", 4_770, 24));
        return period;
    }

    private static MrrWaterfall Block(params MrrPeriod[] periods)
    {
        var block = new MrrWaterfall { Title = "Recurring revenue bridge", Scope = "All tiers", MaxNamedAccounts = 2 };
        block.Periods.AddRange(periods);
        return block;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off, double width = 1152)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width + 40;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Block_Shows_The_Headline_The_Statistics_And_The_Default_Selection()
    {
        var block = Block(Jun(), Q2());
        var window = Show(block);
        try
        {
            block.SelectedPeriod!.Key.ShouldBe("q2");
            block.SelectedIndex.ShouldBe(1);
            block.EndingMrr.ShouldBe(324_760);
            block.NetChange.ShouldBe(38_360);
            block.NetChangeRatio!.Value.ShouldBe(38_360 / 286_400.0, 1e-12);
            block.Headline!.Value.FullText.ShouldBe("$324,760");
            block.Headline.Delta.Text.ShouldBe("+$38,360 +13.4%");
            block.Headline.Delta.Direction.ShouldBe(AnalyticsDirection.Up);
            block.Headline.Delta.Context.ShouldBe("since Apr 1");
            block.Headline.Label.Text.ShouldBe("Ending MRR on Jun 30");
            block.Stats!.Retention.Value.FullText.ShouldBe("97.8%");
            block.Stats.QuickRatio.Value.FullText.ShouldBe("2.37");
            block.Stats.LogoChurn.Value.FullText.ShouldBe("8.9%");
            block.NetRevenueRetention!.Value.ShouldBe(280_140.0 / 286_400, 1e-12);
            block.Plot!.BarCount.ShouldBe(7);
            block.Plot.SelectedIndex.ShouldBe(1);
            block.Plot.Axis.TargetMin.ShouldBe(240_000);
            block.Plot.Axis.TargetMax.ShouldBe(360_000);
            block.Plot.CurrentLabel(0).ShouldBe("$286.4k");
            block.Plot.CurrentLabel(1).ShouldBe("+$39.8k");
            block.Plot.CurrentLabel(4).ShouldBe("−$9.5k");
            block.Plot.CurrentLabel(6).ShouldBe("$324.8k");
            AutomationProperties.GetName(block.Plot)!.ShouldContain("Q2 2026");
            block.Table!.Rows.Count.ShouldBe(8);
            block.Table.Caption.ShouldBe("Q2 2026");
            var switches = window.GetVisualDescendantsOfType<SegmentedControl>().ToList();
            switches.Count.ShouldBe(2);
            switches[0].ItemCount.ShouldBe(2);
            AutomationProperties.GetName(switches[0]).ShouldBe("Period");
            AutomationProperties.GetName(switches[1]).ShouldBe("Show values as");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Panel_Lists_The_Largest_Accounts_Then_The_Remainder_And_The_Tiers_For_Totals()
    {
        var block = Block(Q2());
        var window = Show(block);
        try
        {
            var panel = block.Panel!;
            panel.Title.Text.ShouldBe("New business");
            panel.Total.FullText.ShouldBe("+$39,800");
            panel.Count.FullText.ShouldBe("116");
            panel.Rows.Select(r => r.NameText).ShouldBe(["A", "B", "Remaining accounts"]);
            panel.Rows[0].Amount.FullText.ShouldBe("+$6,000");
            panel.Rows[2].Amount.FullText.ShouldBe($"+${39_800 - 11_000:N0}");
            panel.Rows[2].Detail.Text.ShouldBe("114 accounts");
            panel.Rows[0].Bar.DisplayValue.ShouldBe(6_000.0 / 39_800, 1e-9);
            panel.Swatch.Tone.ShouldBe(MrrTone.Growth);

            block.Select(0).ShouldBeTrue();
            panel.Title.Text.ShouldBe("Starting MRR");
            panel.Total.FullText.ShouldBe("$286,400");
            panel.Rows.Select(r => r.NameText).ShouldBe(["Team", "Business"]);
            panel.Rows.Any(r => r.Key == "__rest").ShouldBeFalse();
            panel.Swatch.Tone.ShouldBe(MrrTone.Total);

            block.Select(5);
            panel.Title.Text.ShouldBe("Churn");
            panel.Total.FullText.ShouldBe("−$18,460");
            panel.Swatch.Tone.ShouldBe(MrrTone.Decrease);
            panel.Rows[0].Amount.FullText.ShouldBe("−$3,000");
            AutomationProperties.GetName(panel).ShouldBe("Accounts behind churn");
            block.Select(7).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_And_Wrap_The_Selection_And_Announce_The_Bar()
    {
        var block = Block(Q2());
        var events = new List<MrrSelectionChangedEventArgs>();
        block.SelectionChanged += (_, e) => events.Add(e);
        var window = Show(block);
        try
        {
            block.Plot!.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(6);
            block.Plot.SelectedIndex.ShouldBe(6);
            block.Announcer!.Announcement.ShouldStartWith("Ending MRR, $324,760, 948 paying accounts at the end");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(6);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(1);
            block.Announcer.Announcement.ShouldBe("New, +$39,800, 116 accounts");
            events.Select(e => e.NewIndex).ShouldBe([0, 6, 0, 6, 0, 1]);
            events[^1].Movement!.Kind.ShouldBe(MrrMovementKind.New);
            events[0].Movement.ShouldBeNull();
            events[^1].OldIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Previews_A_Bar_Without_Selecting_It_And_Leaving_Restores_The_Selection()
    {
        var block = Block(Q2());
        var window = Show(block);
        try
        {
            var plot = block.Plot!;
            var band = MrrWaterfallEngine.Band(plot.Bounds.Width, 7);
            var point = plot.TranslatePoint(new Point(MrrWaterfallEngine.PlotLeft + 3.5 * band, 100), window)!.Value;
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            block.HoveredIndex.ShouldBe(3);
            plot.HoverIndex.ShouldBe(3);
            plot.ActiveIndex.ShouldBe(3);
            block.SelectedIndex.ShouldBe(1);
            block.Panel!.Title.Text.ShouldBe("Reactivation");
            block.Panel.Hint.ShouldNotBeNull();
            window.MouseMove(new Point(5, 5));
            Dispatcher.UIThread.RunJobs();
            block.HoveredIndex.ShouldBe(-1);
            block.Panel.Title.Text.ShouldBe("New business");
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            block.SelectedIndex.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_The_Period_Keeps_The_Selected_Bar_And_Raises_The_Event()
    {
        var block = Block(Jun(), Q2());
        var changes = new List<MrrPeriodChangedEventArgs>();
        block.PeriodChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            block.Select(4);
            var switchControl = window.GetVisualDescendantsOfType<SegmentedControl>().First();
            switchControl.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            block.SelectedPeriod!.Key.ShouldBe("jun");
            block.SelectedIndex.ShouldBe(4);
            block.Plot!.LevelAt(0).ShouldBe(308_120);
            block.Plot.Axis.TargetMin.ShouldBe(MrrWaterfallEngine.Ticks([308_120, 323_490, 329_600, 331_880, 329_530, 324_760]).Min);
            block.Panel!.Title.Text.ShouldBe("Contraction");
            changes.Single().NewPeriod!.Key.ShouldBe("jun");
            block.Announcer!.Announcement.ShouldBe("June 2026: MRR moved from $308,120 to $324,760, up 5.4%");
            block.SelectPeriod("Q2").ShouldBeTrue();
            block.SelectedPeriod.Key.ShouldBe("q2");
            block.SelectPeriod("nope").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Percent_Mode_Rescales_Levels_Ticks_Labels_And_The_Panel()
    {
        var block = Block(Q2());
        var modes = new List<MrrValueModeChangedEventArgs>();
        block.ValueModeChanged += (_, e) => modes.Add(e);
        var window = Show(block);
        try
        {
            var unitSwitch = window.GetVisualDescendantsOfType<SegmentedControl>().Last();
            unitSwitch.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            block.ValueMode.ShouldBe(MrrValueMode.PercentOfStart);
            modes.Single().NewMode.ShouldBe(MrrValueMode.PercentOfStart);
            var plot = block.Plot!;
            plot.LevelAt(0).ShouldBe(100, 1e-9);
            plot.Axis.TargetMin.ShouldBe(85);
            plot.Axis.TargetMax.ShouldBe(125);
            plot.CurrentLabel(0).ShouldBe("100.0%");
            plot.CurrentLabel(1).ShouldBe("+13.9%");
            plot.CurrentLabel(4).ShouldBe("−3.3%");
            block.Panel!.Total.FullText.ShouldBe("+13.90%");
            block.Panel.Rows[0].Amount.FullText.ShouldBe("+2.09%");
            // The headline stays in currency.
            block.Headline!.Value.FullText.ShouldBe("$324,760");
            AutomationProperties.GetName(plot)!.ShouldContain("+13.9%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Period_Switch_Springs_Levels_And_Axis_And_Staggers_The_Panel_Rows()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block(Jun(), Q2());
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = block.Plot!;
            var panel = block.Panel!;
            panel.Rows.Count.ShouldBe(3);
            var oldMin = plot.Axis.DomainMin;
            block.SelectedPeriod = block.Periods[0];
            Dispatcher.UIThread.RunJobs();
            // A leaves where it stood (120ms), then everything else has sprung part of the way on Smooth.
            clock.Advance(TimeSpan.FromMilliseconds(50));
            panel.LeavingRows.Count.ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            panel.LeavingRows.Count.ShouldBe(0);
            plot.LevelAt(0).ShouldBeInRange(286_400 + 1, 308_120 - 1);
            plot.Axis.DomainMin.ShouldBeGreaterThan(oldMin);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            plot.LevelAt(0).ShouldBe(308_120, 0.5);
            plot.Axis.DomainMin.ShouldBe(plot.Axis.TargetMin, 0.5);
            plot.IsMoving.ShouldBeFalse();
            panel.Rows.Select(r => r.NameText).ShouldBe(["B", "E", "Remaining accounts"]);
            panel.Rows.All(r => r.Fade.Value >= 0.999).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selection_Slides_The_Marker_On_A_Fast_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block(Q2());
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = block.Plot!;
            var band = MrrWaterfallEngine.Band(plot.Bounds.Width, 7);
            var from = plot.MarkerX;
            from.ShouldBe(MrrWaterfallEngine.BarLeft(48 + band, band));
            block.Select(2);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(120));
            plot.MarkerX.ShouldBeGreaterThan(from + 5);
            plot.MarkerX.ShouldBeLessThan(MrrWaterfallEngine.BarLeft(48 + 2 * band, band));
            clock.Advance(TimeSpan.FromMilliseconds(600));
            plot.MarkerX.ShouldBe(MrrWaterfallEngine.BarLeft(48 + 2 * band, band), 0.05);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_And_Off_Motion_Land_At_Once()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var block = Block(Jun(), Q2());
            var window = Show(block, level);
            try
            {
                block.SelectedPeriod = block.Periods[0];
                Dispatcher.UIThread.RunJobs();
                clock.Advance(TimeSpan.FromMilliseconds(20));
                block.Plot!.LevelAt(0).ShouldBe(308_120);
                block.Panel!.LeavingRows.Count.ShouldBe(0);
                block.Panel.Rows.All(r => r.Fade.Value >= 0.999 && r.Slide.Value == 0).ShouldBeTrue();
                block.Select(5);
                clock.Advance(TimeSpan.FromMilliseconds(20));
                block.Plot.MarkerX.ShouldBe(MrrWaterfallEngine.BarLeft(48 + 5 * MrrWaterfallEngine.Band(block.Plot.Bounds.Width, 7), MrrWaterfallEngine.Band(block.Plot.Bounds.Width, 7)));
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Blocks_Own_Width()
    {
        var block = Block(Q2());
        var window = Show(block, width: 1152);
        try
        {
            block.Classes.Contains(":stacked").ShouldBeFalse();
            block.Classes.Contains(":compact-summary").ShouldBeFalse();
            block.Plot!.IsCompactBand.ShouldBeFalse();
            window.Width = 740;
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(":stacked").ShouldBeTrue();
            block.Classes.Contains(":compact-summary").ShouldBeFalse();
            window.Width = 520;
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(":compact-summary").ShouldBeTrue();
            block.Plot.IsCompactBand.ShouldBeTrue();
            block.Classes.Contains(":compact-axis").ShouldBeTrue();
            block.Plot.Bounds.Height.ShouldBe(MrrWaterfallEngine.ChartHeight(block.Plot.Bounds.Width));
            // Short labels have no currency symbol.
            block.Plot.CurrentLabel(1).ShouldBe("+39.8k");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Shrinking_Period_Points_Down_And_No_Periods_Is_Empty()
    {
        var down = Q2();
        down.Movements[0].Amount = 2_000;
        down.Movements[1].Amount = 1_000;
        var block = Block(down);
        var window = Show(block);
        try
        {
            block.NetChange.ShouldBeLessThan(0);
            block.Headline!.Delta.Direction.ShouldBe(AnalyticsDirection.Down);
            block.Headline.Delta.Tone.ShouldBe(AnalyticsTone.Bad);
            block.Headline.Delta.Text.ShouldStartWith("−$");
        }
        finally
        {
            window.Close();
        }

        var empty = new MrrWaterfall();
        var emptyWindow = Show(empty);
        try
        {
            empty.Classes.Contains(":empty").ShouldBeTrue();
            empty.Plot!.BarCount.ShouldBe(0);
            empty.Select(0).ShouldBeFalse();
        }
        finally
        {
            emptyWindow.Close();
        }
    }

    [AvaloniaFact]
    public void Periods_And_Labels_Can_Change_After_The_First_Show()
    {
        var block = Block(Q2());
        var window = Show(block);
        try
        {
            block.Periods.Add(Jun());
            Dispatcher.UIThread.RunJobs();
            window.GetVisualDescendantsOfType<SegmentedControl>().First().ItemCount.ShouldBe(2);
            block.IsPeriodSwitchVisible = false;
            window.GetVisualDescendantsOfType<SegmentedControl>().First().IsVisible.ShouldBeFalse();
            block.Labels.Hint = "Choose a bar";
            Dispatcher.UIThread.RunJobs();
            block.Panel!.Hint.ShouldNotBeNull();
            block.CurrencySymbol = "€";
            Dispatcher.UIThread.RunJobs();
            block.Headline!.Value.FullText.ShouldBe("€324,760");
        }
        finally
        {
            window.Close();
        }
    }
}
