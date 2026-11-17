using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Finance;

/// <summary>
/// <see cref="BudgetVariance"/> on an invented budget (two windows, three departments): sums and the shared scale, variance
/// kinds and sentences, the period switch, disclosure with its events and keyboard, the stacked layout and the motion levels.
/// </summary>
public class BudgetVarianceTests
{
    private static BudgetPeriod[] Periods() =>
    [
        new("month", "Month", "March 2026", "in March"),
        new("quarter", "Quarter", "January to March 2026", "in the first quarter"),
    ];

    private static BudgetDepartment[] Departments()
    {
        var research = new BudgetDepartment("research", "Research", "Ines Duarte");
        research.Lines.Add(new BudgetLine("lab", "Lab supplies").With("month", 40_000, 46_000).With("quarter", 120_000, 118_000));
        research.Lines.Add(new BudgetLine("staff", "Staff").With("month", 200_000, 198_000).With("quarter", 600_000, 597_000));
        var support = new BudgetDepartment("support", "Support", "Kofi Mensah");
        support.Lines.Add(new BudgetLine("tools", "Help desk tools").With("month", 10_000, 9_000).With("quarter", 30_000, 27_000));
        var facilities = new BudgetDepartment("facilities", "Facilities", "Rosa Lind");
        facilities.Lines.Add(new BudgetLine("rent", "Rent").With("month", 50_000, 50_100).With("quarter", 150_000, 165_000));
        return [research, support, facilities];
    }

    private sealed class Rig : IDisposable
    {
        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 1000, Action<BudgetVariance>? configure = null)
        {
            Block = new BudgetVariance { SelectedPeriod = "month" };
            Block.Periods.AddRange(Periods());
            Block.Departments.AddRange(Departments());
            configure?.Invoke(Block);
            Block.PeriodChanged += (_, e) => PeriodEvents.Add(e.NewPeriod);
            Block.DepartmentToggled += (_, e) => ToggleEvents.Add((e.Department.Id, e.IsExpanded));
            Host = new Border { Width = width, Child = Block, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 1200;
            Window.Height = 900;
            Pump();
        }

        public BudgetVariance Block { get; }
        public Border Host { get; }
        public Window Window { get; }
        public List<string?> PeriodEvents { get; } = new();
        public List<(string Id, bool Open)> ToggleEvents { get; } = new();

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
        }

        public List<BudgetVarianceRowButton> Buttons => Block.GetVisualDescendants().OfType<BudgetVarianceRowButton>().ToList();

        public List<BudgetBulletBar> Bars => Block.GetVisualDescendants().OfType<BudgetBulletBar>().ToList();

        public FinanceAnnouncer Announcer => TestHelpers.Descendant<FinanceAnnouncer>(Block);

        public void Dispose() => Window.Close();
    }

    [AvaloniaFact]
    public void Departments_And_The_Total_Are_Summed_From_The_Lines()
    {
        using var rig = new Rig();
        var research = rig.Block.Departments[0];
        research.In("month").ShouldBe((240_000, 244_000));
        rig.Block.TotalIn("month").ShouldBe((300_000, 303_100));
        rig.Block.TotalIn("quarter").ShouldBe((900_000, 907_000));
        // A window a line has no entry for counts as zero.
        research.Lines[0].In("year").ShouldBe((0, 0));
    }

    [AvaloniaFact]
    public void The_Shared_Scale_Fits_The_Largest_Figure_Of_Any_Window()
    {
        using var rig = new Rig();
        // The largest ratio is the lab line in March, 46/40 = 1.15, so the floor of 1.2 applies.
        rig.Block.ScaleMaximum.ShouldBe(1.2, 1e-9);
        rig.Block.Departments[1].Lines[0].Amounts[1].Actual = 41_000;
        rig.Block.Departments.Move(1, 2);
        rig.Pump();
        // 41/30 = 1.367 rounds up to 1.4, and the budget tick sits at 1/1.4 on every bar.
        rig.Block.ScaleMaximum.ShouldBe(1.4, 1e-9);
        rig.Bars.ShouldAllBe(b => Math.Abs(b.Budget - 1 / 1.4) < 1e-9);
    }

    [AvaloniaFact]
    public void Variance_Kinds_Use_The_On_Plan_Threshold()
    {
        using var rig = new Rig();
        rig.Block.KindOf(100, 100.4).ShouldBe(BudgetVarianceKind.OnPlan);
        rig.Block.KindOf(100, 101).ShouldBe(BudgetVarianceKind.Over);
        rig.Block.KindOf(100, 99).ShouldBe(BudgetVarianceKind.Under);
        rig.Block.KindOf(0, 5).ShouldBe(BudgetVarianceKind.Over);
        rig.Block.OnPlanThreshold = 0.02;
        rig.Block.KindOf(100, 101).ShouldBe(BudgetVarianceKind.OnPlan);
    }

    [AvaloniaFact]
    public void Each_Department_Button_Reads_A_Full_Sentence()
    {
        using var rig = new Rig();
        var name = AutomationProperties.GetName(rig.Buttons[0]);
        name.ShouldBe("Research: spent $244.0k of a $240.0k budget in March; $4.0k, 1.7%, over budget, unfavorable.");
        AutomationProperties.GetName(rig.Buttons[1])!.ShouldContain("under budget, favorable");
        AutomationProperties.GetName(rig.Buttons[2])!.ShouldContain("on plan");
    }

    [AvaloniaFact]
    public void Switching_The_Period_Updates_Bars_Description_And_Announces_The_Total()
    {
        using var rig = new Rig();
        var description = TestHelpers.Descendant<TextBlock>(rig.Block, BudgetVariance.DescriptionPart);
        description.Text.ShouldBe("March 2026");
        var totalBar = rig.Bars[0];
        totalBar.Fraction.ShouldBe(303_100 / 300_000.0 / 1.2, 1e-9);

        rig.Block.SelectedPeriod = "quarter";
        rig.Pump();
        description.Text.ShouldBe("January to March 2026");
        totalBar.Fraction.ShouldBe(907_000 / 900_000.0 / 1.2, 1e-9);
        rig.PeriodEvents.ShouldBe(["quarter"]);
        rig.Announcer.Announcement!.ShouldStartWith("Quarter. Total spend: spent $907.0k of a $900.0k budget in the first quarter");
        AutomationProperties.GetName(rig.Buttons[2])!.ShouldContain("over budget");
    }

    [AvaloniaFact]
    public void The_Period_Switch_Drives_The_Selected_Period()
    {
        using var rig = new Rig();
        var switcher = TestHelpers.Descendant<SegmentedControl>(rig.Block, BudgetVariance.PeriodsPart);
        switcher.SelectedValue.ShouldBe("month");
        switcher.SelectedValue = "quarter";
        rig.Pump();
        rig.Block.SelectedPeriod.ShouldBe("quarter");
    }

    [AvaloniaFact]
    public void Opening_A_Department_Closes_The_Open_One_And_Reports_It_First()
    {
        using var rig = new Rig(configure: b => b.ExpandedDepartment = "research");
        rig.Buttons[0].IsExpanded.ShouldBeTrue();
        rig.Block.ToggleDepartment("support");
        rig.Pump();
        rig.Block.ExpandedDepartment.ShouldBe("support");
        rig.ToggleEvents.ShouldBe([("research", false), ("support", true)]);
        rig.Buttons[0].IsExpanded.ShouldBeFalse();
        rig.Buttons[1].IsExpanded.ShouldBeTrue();
        rig.Block.ToggleDepartment("support");
        rig.Block.ExpandedDepartment.ShouldBeNull();
        rig.ToggleEvents[^1].ShouldBe(("support", false));
    }

    [AvaloniaFact]
    public void Arrows_Home_And_End_Move_Between_Departments_And_Enter_Toggles()
    {
        using var rig = new Rig();
        rig.Buttons[0].Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        rig.Buttons[1].IsFocused.ShouldBeTrue();
        rig.Window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        rig.Buttons[2].IsFocused.ShouldBeTrue();
        rig.Window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        rig.Buttons[0].IsFocused.ShouldBeTrue();
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        rig.Buttons[0].IsFocused.ShouldBeTrue();
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Pump();
        rig.Block.ExpandedDepartment.ShouldBe("research");
    }

    [AvaloniaFact]
    public void Below_The_Table_Threshold_Rows_Stack_Their_Charts()
    {
        using var rig = new Rig(width: 600);
        rig.Block.IsStacked.ShouldBeTrue();
        rig.Block.Classes.Contains(BudgetVariance.StackedPseudoClass).ShouldBeTrue();
        var panels = rig.Block.GetVisualDescendants().OfType<BudgetVarianceRowPanel>().ToList();
        panels.ShouldAllBe(p => !p.IsTable);
        // The chart spans the row under the figures.
        var bar = rig.Bars[0];
        var panel = panels.First(p => p.Children.Contains(bar));
        bar.Bounds.Width.ShouldBe(panel.Bounds.Width, 0.5);
        bar.Bounds.Y.ShouldBeGreaterThan(0);

        rig.Host.Width = 1000;
        rig.Pump();
        rig.Block.IsStacked.ShouldBeFalse();
        panels.ShouldAllBe(p => p.IsTable);
    }

    [AvaloniaFact]
    public void Budget_Ticks_Line_Up_Down_The_Table()
    {
        using var rig = new Rig(configure: b => b.ExpandedDepartment = "research");
        var ticks = rig.Bars.Select(b => b.TranslatePoint(new Avalonia.Point(b.Bounds.Width * b.Budget, 0), rig.Block)!.Value.X).ToList();
        ticks.Count.ShouldBe(1 + 3 + 4);
        ticks.ShouldAllBe(x => Math.Abs(x - ticks[0]) < 0.5);
    }

    [AvaloniaFact]
    public void Numbers_Count_On_A_Smooth_Spring_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        var actual = rig.Block.GetVisualDescendants().OfType<FinanceCountingText>().First(t => t.ShownText == "$303.1k" && t.IsEffectivelyVisible);
        rig.Block.SelectedPeriod = "quarter";
        rig.Pump();
        clock.Advance(TimeSpan.FromMilliseconds(80));
        actual.ShownValue.ShouldBeInRange(303_200, 906_000);
        var peak = actual.ShownValue;
        for (var i = 0; i < 60; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            peak = Math.Max(peak, actual.ShownValue);
        }
        // Critically damped: the count lands on the new total and never passes it.
        clock.Advance(TimeSpan.FromMilliseconds(600));
        actual.ShownValue.ShouldBe(907_000, 1);
        peak.ShouldBeLessThanOrEqualTo(907_000 + 1e-6);
        actual.ShownText.ShouldBe("$907.0k");
    }

    [AvaloniaFact]
    public void Without_Motion_Numbers_Jump_To_The_New_Window()
    {
        using var rig = new Rig();
        rig.Block.SelectedPeriod = "quarter";
        rig.Pump();
        rig.Block.GetVisualDescendants().OfType<FinanceCountingText>().ShouldContain(t => t.ShownText == "$907.0k");
    }

    [AvaloniaFact]
    public void Labels_Replace_The_Built_In_Text()
    {
        using var rig = new Rig(configure: b => b.Labels = new BudgetVarianceLabels { Total = "All teams", Over = "above" });
        rig.Block.GetVisualDescendants().OfType<TextBlock>().ShouldContain(t => t.Text == "All teams");
        rig.Block.GetVisualDescendants().OfType<FinanceCountingText>().ShouldContain(t => t.ShownText.EndsWith(" above"));
    }

    [Fact]
    public void Compact_Amounts_Shorten_To_K_And_M()
    {
        FinanceFormat.Compact(4_010_000, "$").ShouldBe("$4.01M");
        FinanceFormat.Compact(993_600, "$").ShouldBe("$993.6k");
        FinanceFormat.Compact(999_960, "$").ShouldBe("$1.00M");
        FinanceFormat.Compact(820, "$").ShouldBe("$820");
        FinanceFormat.SignedCompact(-3_900, "$").ShouldBe("−$3.9k");
        FinanceFormat.SignedCompact(48_900, "€").ShouldBe("+€48.9k");
        FinanceFormat.SignedPercent(-0.003).ShouldBe("−0.3%");
        FinanceFormat.SignedPercent(-0.0001).ShouldBe("+0.0%");
    }
}
