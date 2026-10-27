using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>Cohort retention: the engine (numbers, geometry, morph slices, keys) and the block (headless).</summary>
public class CohortRetentionTests
{
    // Three cohorts: 100 / 200 / 50 started; 3, 2 and 1 months measured.
    private static RetentionCohort[] Triangle() =>
    [
        new("Jan 2026", 100, [80, 60, 50], "Jan ’26"),
        new("Feb 2026", 200, [150, 100], "Feb ’26"),
        new("Mar 2026", 50, [45], "Mar ’26"),
    ];

    private static RetentionCohort[] Shifted() =>
    [
        new("Jan 2026", 100, [40, 30, 20], "Jan ’26"),
        new("Feb 2026", 200, [190, 180], "Feb ’26"),
        new("Mar 2026", 50, [10], "Mar ’26"),
    ];

    private static CohortRetentionModel Model() => new(Triangle(), 0);

    // Twelve cohorts like the reference (12 − r months each), rates falling from ~0.8.
    private static RetentionCohort[] Twelve(double shift = 0)
    {
        var cohorts = new RetentionCohort[12];
        for (var r = 0; r < 12; r++)
        {
            var retained = new int[12 - r];
            for (var m = 0; m < retained.Length; m++)
            {
                retained[m] = (int)Math.Round(400 * Math.Clamp(0.4 + 0.38 * Math.Exp(-0.3 * m) + shift, 0, 1));
            }
            cohorts[r] = new RetentionCohort($"C{r}", 400, retained);
        }
        return cohorts;
    }

    #region Engine

    [Fact]
    public void Model_Weights_The_Average_By_Size_Over_The_Cohorts_That_Reached_The_Month()
    {
        var model = Model();
        model.Rows.ShouldBe(3);
        model.Months.ShouldBe(3);
        model.Rate(new CohortCell(0, 0)).ShouldBe(0.8, 1e-12);
        model.Rate(new CohortCell(1, 1)).ShouldBe(0.5, 1e-12);
        // Month 1: (80 + 150 + 45) / (100 + 200 + 50); month 2: (60 + 100) / 300; month 3 only the first cohort.
        model.AverageRates[0].ShouldBe(275.0 / 350, 1e-12);
        model.AverageRates[1].ShouldBe(160.0 / 300, 1e-12);
        model.AverageRates[2].ShouldBe(0.5, 1e-12);
        model.AverageCoverage.ShouldBe([3, 2, 1]);
        model.AverageLength.ShouldBe(3);
        model.TotalSize.ShouldBe(350);
        model.Contains(new CohortCell(2, 1)).ShouldBeFalse();
        model.Contains(CohortCell.Average(2)).ShouldBeTrue();
        model.RetainedAt(CohortCell.Average(1)).ShouldBe(160);
        model.SizeAt(CohortCell.Average(1)).ShouldBe(300);
        new CohortRetentionModel(Triangle(), 6).Months.ShouldBe(6);
        CohortRetentionModel.Empty.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Compare_Change_Is_Newest_Minus_Oldest_Among_Cohorts_That_Reached_The_Month()
    {
        var model = Model();
        // Month 1, n = 1: 0.9 (Mar) − 0.8 (Jan).
        model.CompareChange(0, 1)!.Value.ShouldBe(0.1, 1e-12);
        // Month 2 has two cohorts: n = 3 takes both on each side.
        model.CompareChange(1, 3)!.Value.ShouldBe(0, 1e-12);
        model.CompareChange(1, 1)!.Value.ShouldBe(0.5 - 0.6, 1e-12);
        model.CompareChange(5, 3).ShouldBeNull();
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.199, 0)]
    [InlineData(0.20, 1)]
    [InlineData(0.349, 1)]
    [InlineData(0.50, 3)]
    [InlineData(0.798, 4)]
    [InlineData(0.80, 5)]
    [InlineData(1.0, 5)]
    public void Steps_Are_Closed_At_The_Bottom(double rate, int step)
        => CohortRetentionModel.Step(rate, CohortRetentionPlotOptions.DefaultThresholds).ShouldBe(step);

    [Fact]
    public void Wide_Layout_Matches_The_Measured_Svg()
    {
        var layout = new CohortRetentionLayout(1112, 12, 12, true, false, 136, 30, 28, 5, 10);
        layout.ColumnWidth.ShouldBe(976.0 / 12, 1e-9);
        layout.X0(0).ShouldBe(137);
        layout.X1(0).ShouldBe(216);
        layout.Cx(0).ShouldBe(176.5);
        layout.Cx(1).ShouldBe(258);
        layout.Cx(2).ShouldBe(339.5);
        layout.Cx(11).ShouldBe(1071.5);
        for (var i = 0; i < 11; i++)
        {
            (layout.X0(i + 1) - layout.X1(i)).ShouldBe(2);
        }
        layout.CellTop(0, false).ShouldBe(1);
        layout.CellTop(11, false).ShouldBe(331);
        layout.CellTop(0, true).ShouldBe(371);
        layout.RowCenter(0, true).ShouldBe(385);
        layout.Height.ShouldBe(430);
        layout.AxisY.ShouldBe(421);
        layout.PlotBottom.ShouldBe(390);
        layout.YOf(1).ShouldBe(10);
        layout.YOf(0.5).ShouldBe(200);
        Enumerable.Range(0, 5).Select(layout.GridY).ShouldBe([10.5, 105.5, 200.5, 295.5, 390.5]);
    }

    [Fact]
    public void Compact_Layout_Matches_The_Measured_Svg()
    {
        var layout = new CohortRetentionLayout(500, 12, 12, true, true, 56, 24, 22, 3, 8);
        layout.CellTop(0, true).ShouldBe(297);
        layout.Height.ShouldBe(350);
        layout.AxisY.ShouldBe(341);
        layout.PlotBottom.ShouldBe(310);
        Enumerable.Range(0, 5).Select(layout.GridY).ShouldBe([10.5, 85.5, 160.5, 235.5, 310.5]);
        layout.YOf(0).ShouldBe(310);
    }

    [Fact]
    public void Heatmap_Hit_Test_Finds_Measured_Cells_Only()
    {
        var model = Model();
        var layout = new CohortRetentionLayout(436, 3, 3, true, false, 136, 30, 28, 5, 10);
        layout.HitHeatmap(new Point(layout.Cx(0), 15), model).ShouldBe(new CohortCell(0, 0));
        layout.HitHeatmap(new Point(layout.Cx(1), 45), model).ShouldBe(new CohortCell(1, 1));
        // March has one month only; the label column and the gap above the average clear the cursor.
        layout.HitHeatmap(new Point(layout.Cx(1), 75), model).ShouldBeNull();
        layout.HitHeatmap(new Point(60, 15), model).ShouldBeNull();
        layout.HitHeatmap(new Point(layout.Cx(0), 95), model).ShouldBeNull();
        layout.HitHeatmap(new Point(layout.Cx(2), 105), model).ShouldBe(CohortCell.Average(2));
    }

    [Fact]
    public void Curves_Hit_Test_Takes_The_Nearest_Month_Then_The_Nearest_Point()
    {
        var model = Model();
        var layout = new CohortRetentionLayout(436, 3, 3, true, false, 136, 30, 28, 5, 10);
        double Y(CohortCell c) => layout.YOf(model.Rate(c));
        // Month 1 points: Jan 0.8, Feb 0.75, Mar 0.9, average 0.786.
        layout.HitCurves(new Point(layout.Cx(0) + 10, layout.YOf(0.9)), model, Y).ShouldBe(new CohortCell(2, 0));
        layout.HitCurves(new Point(layout.Cx(0), layout.YOf(0.74)), model, Y).ShouldBe(new CohortCell(1, 0));
        layout.HitCurves(new Point(layout.Cx(1), layout.YOf(0.535)), model, Y).ShouldBe(CohortCell.Average(1));
        // Past the last column: the last month; in the label column: nothing.
        layout.HitCurves(new Point(435, layout.YOf(0.5)), model, Y)!.Value.MonthIndex.ShouldBe(2);
        layout.HitCurves(new Point(100, 100), model, Y).ShouldBeNull();
    }

    [Fact]
    public void Card_Goes_Right_Of_The_Box_Flips_Left_And_Clamps_Vertically()
    {
        var card = new Size(200, 124);
        var right = CohortRetentionLayout.CardPosition(new Rect(500, 150, 81, 30), card, 1112, 430, 9);
        right.X.ShouldBe(590);
        right.Y.ShouldBe(165 - 62);
        var flipped = CohortRetentionLayout.CardPosition(new Rect(1000, 0, 81, 30), card, 1112, 430, 9);
        flipped.X.ShouldBe(1000 - 9 - 200);
        flipped.Y.ShouldBe(0);
        CohortRetentionLayout.CardPosition(new Rect(136, 370, 81, 30), card, 1112, 430, 9).Y.ShouldBe(306);
        CohortRetentionCard.Height.ShouldBe(124);
    }

    [Fact]
    public void Morph_Slices_Follow_The_Joint_Fit_And_Reverse_By_Themselves()
    {
        CohortRetentionMorph.Size(0.24, 0).ShouldBe(0);
        CohortRetentionMorph.Size(0.54, 0).ShouldBe(1);
        CohortRetentionMorph.Y(0.40, 0).ShouldBe(0);
        CohortRetentionMorph.Y(0.78, 0).ShouldBe(1);
        // Cell (0, 11): y over [.56, .90]; average month 12 (key 23): [.72, 1].
        CohortRetentionMorph.Y(0.40 + 0.0139 * 11, 11).ShouldBe(0, 1e-9);
        CohortRetentionMorph.Y(0.90, 11).ShouldBe(1, 0.02);
        CohortRetentionMorph.Y(0.72, 23).ShouldBe(0, 0.01);
        CohortRetentionMorph.Y(1, 23).ShouldBe(1);
        CohortRetentionMorph.Labels(0.24).ShouldBe(1);
        CohortRetentionMorph.Labels(0.54).ShouldBe(0);
        CohortRetentionMorph.Curves(0.56).ShouldBe(0);
        CohortRetentionMorph.Curves(0.92).ShouldBe(1);
        CohortRetentionMorph.Grid(0.72).ShouldBe(0);
        // Diagonal: same key, same progress; later keys trail.
        CohortRetentionMorph.Key(new CohortCell(0, 11), 12).ShouldBe(CohortRetentionMorph.Key(new CohortCell(11, 0), 12));
        CohortRetentionMorph.Key(CohortCell.Average(11), 12).ShouldBe(23);
        CohortRetentionMorph.Y(0.6, 0).ShouldBeGreaterThan(CohortRetentionMorph.Y(0.6, 12));
        // Going back (p falling from 1) the last keys leave first, and a cell moves before it grows.
        CohortRetentionMorph.Y(0.95, 23).ShouldBeLessThan(1);
        CohortRetentionMorph.Y(0.95, 0).ShouldBe(1);
        CohortRetentionMorph.Size(0.6, 0).ShouldBe(1);
        CohortRetentionMorph.Y(0.6, 0).ShouldBeLessThan(1);
        CohortRetentionMorph.TextOpacity(0.35).ShouldBe(0);
    }

    [Fact]
    public void Keyboard_Moves_Inside_The_Triangle()
    {
        var model = Model();
        model.Move(null, Key.Right, true).ShouldBe(new CohortCell(0, 0));
        model.Move(new CohortCell(0, 0), Key.Left, true).ShouldBe(new CohortCell(0, 0));
        model.Move(new CohortCell(0, 2), Key.Right, true).ShouldBe(new CohortCell(0, 2));
        model.Move(new CohortCell(1, 0), Key.End, true).ShouldBe(new CohortCell(1, 1));
        model.Move(new CohortCell(1, 1), Key.Home, true).ShouldBe(new CohortCell(1, 0));
        // Down keeps the month when it exists, else takes the row's last month; from the newest cohort to the average.
        model.Move(new CohortCell(0, 2), Key.Down, true).ShouldBe(new CohortCell(1, 1));
        model.Move(new CohortCell(2, 0), Key.Down, true).ShouldBe(CohortCell.Average(0));
        model.Move(new CohortCell(2, 0), Key.Down, false).ShouldBe(new CohortCell(2, 0));
        model.Move(CohortCell.Average(2), Key.Up, true).ShouldBe(new CohortCell(2, 0));
        model.Move(CohortCell.Average(2), Key.Down, true).ShouldBe(CohortCell.Average(2));
        model.Move(new CohortCell(0, 1), Key.Up, true).ShouldBe(new CohortCell(0, 1));
        model.Move(new CohortCell(0, 1), Key.Enter, true).ShouldBeNull();
    }

    #endregion

    #region Block

    private static CohortRetention Block(params RetentionSegment[] segments)
    {
        var block = new CohortRetention { Title = "Retention", Subtitle = "Test data" };
        block.Segments.AddRange(segments);
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
    public void Block_Shows_The_Headline_Numbers_And_Both_Switches()
    {
        var block = Block(new RetentionSegment("All", Triangle()), new RetentionSegment("Other", Shifted()));
        block.CompareCohortCount = 1;
        var window = Show(block);
        try
        {
            block.SelectedSegment!.Label.ShouldBe("All");
            block.FirstMetric!.Value.ShouldBe(275.0 / 350, 1e-12);
            block.SecondMetric.ShouldBeNull();
            block.TotalSize.ShouldBe(350);
            var metrics = block.Metrics!;
            metrics.First.Rolling.FullText.ShouldBe("78.6%");
            metrics.First.Delta.Text.ShouldBe("+10.0 pts");
            metrics.First.ContextText.ShouldBe("newest 1 vs oldest 1 cohorts");
            metrics.Total.Rolling.FullText.ShouldBe("350");
            metrics.Total.ContextText.ShouldBe("Jan 2026 to Mar 2026");
            AutomationProperties.GetName(metrics.First).ShouldStartWith("Month 1 retention, 78.6%, up");

            var switches = window.GetVisualDescendantsOfType<SegmentedControl>().ToList();
            switches.Count.ShouldBe(2);
            switches[0].ItemCount.ShouldBe(2);
            switches[0].IsVisible.ShouldBeTrue();
            switches[1].ItemCount.ShouldBe(2);
            AutomationProperties.GetName(switches[1]).ShouldBe("Chart view");
            block.Classes.Contains(":heatmap").ShouldBeTrue();
            block.Table!.RowHeaders.ShouldBe(["Jan 2026", "Feb 2026", "Mar 2026", "Average"]);
            block.Table.Rows[2][2].ShouldBe("Month 2: not measured yet");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Segment_Switch_Changes_The_Data_Raises_The_Event_And_Announces()
    {
        var other = new RetentionSegment("Other", Shifted(), "the other group");
        var block = Block(new RetentionSegment("All", Triangle()), other);
        var changes = new List<CohortRetentionSegmentChangedEventArgs>();
        block.SegmentChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            var segments = window.GetVisualDescendantsOfType<SegmentedControl>().First();
            segments.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            block.SelectedSegment.ShouldBe(other);
            changes.Count.ShouldBe(1);
            changes[0].NewSegment.ShouldBe(other);
            block.FirstMetric!.Value.ShouldBe(240.0 / 350, 1e-12);
            block.Plot!.DisplayedRate(new CohortCell(0, 0)).ShouldBe(0.4, 1e-9);
            block.Announcer!.Announcement!.ShouldStartWith("Showing the other group. Month 1 retention: 68.6%.");
            block.SelectSegment("All").ShouldBeTrue();
            block.SelectedSegment!.Label.ShouldBe("All");
            block.SelectSegment("nope").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void View_Switch_Morphs_At_Once_Without_Motion_And_Announces()
    {
        var block = Block(new RetentionSegment("All", Triangle()));
        var views = new List<CohortRetentionViewChangedEventArgs>();
        block.ViewChanged += (_, e) => views.Add(e);
        var window = Show(block);
        try
        {
            var viewSwitch = window.GetVisualDescendantsOfType<SegmentedControl>().Last();
            viewSwitch.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            block.View.ShouldBe(CohortRetentionView.Curves);
            block.Plot!.MorphProgress.ShouldBe(1);
            block.Legend!.View.ShouldBe(CohortRetentionView.Curves);
            block.Classes.Contains(":curves").ShouldBeTrue();
            views.Single().NewView.ShouldBe(CohortRetentionView.Curves);
            block.Announcer!.Announcement.ShouldBe(block.CurvesAnnouncement);
            // Only one segment: its switch is hidden.
            window.GetVisualDescendantsOfType<SegmentedControl>().First().IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Move_The_Cursor_Escape_Clears_And_Enter_Invokes()
    {
        var block = Block(new RetentionSegment("All", Triangle()));
        var cursors = new List<CohortCellEventArgs>();
        var invoked = new List<CohortCellEventArgs>();
        block.CursorChanged += (_, e) => cursors.Add(e);
        block.CellInvoked += (_, e) => invoked.Add(e);
        var window = Show(block);
        try
        {
            var plot = block.Plot!;
            plot.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            block.CursorCell.ShouldBe(new CohortCell(0, 0));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            block.CursorCell.ShouldBe(new CohortCell(1, 1));
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            block.CursorCell.ShouldBe(CohortCell.Average(0));
            cursors[^1].CoveredCohorts.ShouldBe(3);
            cursors[^1].Size.ShouldBe(350);
            block.Announcer!.Announcement.ShouldBe("Average of 3 cohorts, month 1: 78.6% still active.");
            plot.Card.IsShown.ShouldBeTrue();
            plot.Card.Content!.SecondValue.ShouldBe("3 cohorts");

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.Single().Cell.ShouldBe(CohortCell.Average(0));

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            block.CursorCell.ShouldBeNull();
            plot.Card.IsShown.ShouldBeFalse();
            plot.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Sets_The_Cursor_Dims_Other_Rows_And_Columns_And_Shows_The_Card()
    {
        var block = Block(new RetentionSegment("All", Triangle()));
        var window = Show(block);
        try
        {
            var plot = block.Plot!;
            var layout = plot.CurrentLayout;
            var local = new Point(layout.Cx(1), layout.RowCenter(1, false));
            window.MouseMove(plot.TranslatePoint(local, window)!.Value, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.CursorCell.ShouldBe(new CohortCell(1, 1));
            plot.DisplayedOpacity(new CohortCell(1, 0)).ShouldBe(1);
            plot.DisplayedOpacity(new CohortCell(0, 1)).ShouldBe(1);
            plot.DisplayedOpacity(new CohortCell(0, 0)).ShouldBe(0.28);
            plot.DisplayedOpacity(CohortCell.Average(1)).ShouldBe(1);
            plot.DisplayedOpacity(CohortCell.Average(0)).ShouldBe(0.28);
            var card = plot.Card;
            card.IsShown.ShouldBeTrue();
            card.Content!.Title.ShouldBe("Feb 2026 cohort");
            card.Content.Value.ShouldBe("50.0%");
            card.Content.FirstValue.ShouldBe("100 of 200");
            // 50% against the month's average 53.3%: −3.3 pts, bad.
            card.Content.SecondValue.ShouldBe("−3.3 pts");
            card.Content.SecondTone.ShouldBe(AnalyticsTone.Bad);
            card.Position.X.ShouldBe(layout.X1(1) + 1 + 9);

            window.MouseMove(plot.TranslatePoint(new Point(20, 20), window)!.Value, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.CursorCell.ShouldBeNull();
            plot.DisplayedOpacity(new CohortCell(0, 0)).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Segment_Switch_Rolls_Values_In_A_Diagonal_Wave_And_Rewashes_The_Steps()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block(new RetentionSegment("All", Twelve()), new RetentionSegment("High", Twelve(0.2)));
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = block.Plot!;
            var first = new CohortCell(0, 0);
            var last = CohortCell.Average(11);
            var start = plot.DisplayedRate(first);
            var lastStart = plot.DisplayedRate(last);
            block.SelectSegment("High");
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            // Key 0 has started; key 23 waits 207ms.
            plot.DisplayedRate(first).ShouldBeGreaterThan(start + 0.01);
            plot.DisplayedRate(last).ShouldBe(lastStart, 1e-9);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            plot.DisplayedRate(first).ShouldBe(Math.Min(1, 0.98), 0.01);
            plot.DisplayedRate(last).ShouldBe(0.4 + 0.38 * Math.Exp(-3.3) + 0.2, 0.01);
            plot.DisplayedStep(first).ShouldBe(5);
            plot.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void View_Morph_Runs_On_The_Master_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block(new RetentionSegment("All", Twelve()));
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = block.Plot!;
            block.View = CohortRetentionView.Curves;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(150));
            plot.MorphProgress.ShouldBeInRange(0.2, 0.6);
            clock.Advance(TimeSpan.FromMilliseconds(1600));
            plot.MorphProgress.ShouldBe(1, 0.002);
            block.Legend!.SwapProgress.ShouldBe(1);

            // Reduced motion: the switch back lands at once.
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            block.View = CohortRetentionView.Heatmap;
            Dispatcher.UIThread.RunJobs();
            plot.MorphProgress.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Blocks_Own_Width()
    {
        var block = Block(new RetentionSegment("All", Twelve()), new RetentionSegment("B", Twelve(0.1)));
        var host = new Border { Width = 420, Child = block };
        var window = Show(host);
        try
        {
            var classes = block.Classes;
            classes.Contains(":narrow").ShouldBeTrue();
            classes.Contains(":stacked").ShouldBeTrue();
            classes.Contains(":compact").ShouldBeTrue();
            block.Plot!.IsCompact.ShouldBeTrue();
            block.Plot.Bounds.Height.ShouldBe(350);
            block.Metrics!.IsStacked.ShouldBeTrue();
            block.Metrics.First.ValueSize.ShouldBe(22);

            host.Width = 1152;
            Dispatcher.UIThread.RunJobs();
            classes.Contains(":narrow").ShouldBeFalse();
            classes.Contains(":compact").ShouldBeFalse();
            block.Plot.Bounds.Height.ShouldBe(430);
            block.Metrics.First.ValueSize.ShouldBe(28);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Block_Shows_Dashes_And_Disables_The_View_Switch()
    {
        var block = new CohortRetention { Title = "Retention" };
        var window = Show(block);
        try
        {
            block.Classes.Contains(":empty").ShouldBeTrue();
            block.FirstMetric.ShouldBeNull();
            AutomationProperties.GetName(block.Metrics!.First).ShouldBe("Month 1 retention, —");
            window.GetVisualDescendantsOfType<SegmentedControl>().Last().IsEnabled.ShouldBeFalse();
            block.Plot!.Bounds.Height.ShouldBe(160);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}

internal static class CohortRetentionTestExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Visual root) where T : Visual
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>();
}
