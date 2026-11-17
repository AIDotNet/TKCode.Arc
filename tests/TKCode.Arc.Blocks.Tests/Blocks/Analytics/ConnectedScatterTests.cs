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

/// <summary>Connected scatter: the pure engine (axes, zone, keys, timeline, labels) and the block (headless).</summary>
public class ConnectedScatterTests
{
    private static readonly string[] s_months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    // Eighteen months from Jul 2024; x rises then falls, y rises; shift moves the whole path.
    private static ScatterSeries Channel(string name, string key, double shift = 0, bool notes = true)
    {
        var series = new ScatterSeries { Name = name, Key = key };
        for (var i = 0; i < 18; i++)
        {
            var m = 6 + i;
            var x = 800 + shift + 40 * i + 260 * Math.Sin(i / 4.0);
            var y = 3000 + shift * 2 + 90 * i + 150 * Math.Cos(i / 3.0);
            series.Points.Add(new ScatterPoint($"{s_months[m % 12]} {2024 + m / 12}", Math.Round(x), Math.Round(y)));
        }
        if (notes)
        {
            series.Notes.Add(new ScatterNote(6, "Pricing change", ScatterLabelSide.Top));
            series.Notes.Add(new ScatterNote(11, "New plan", ScatterLabelSide.Left));
        }
        return series;
    }

    private static ConnectedScatter Block(bool playOnAppear = false)
    {
        var block = new ConnectedScatter
        {
            Title = "Unit economics",
            TargetRatio = 3,
            PlayOnFirstAppear = playOnAppear,
            XAxis = new ScatterAxis("CAC", "Acquisition cost", 200, 1800, 200),
            YAxis = new ScatterAxis("LTV", "Customer value", 2000, 6000, 1000)
        };
        block.Series.Add(Channel("Field sales", "field"));
        block.Series.Add(Channel("Community", "community", 300));
        block.Series.Add(Channel("Resellers", "resellers", -200));
        return block;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off, double width = 1152)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width + 40;
        window.Height = 1000;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    #region Engine

    [Fact]
    public void Axis_Uses_The_Given_Range_And_Step_Exactly_And_Derives_The_Rest()
    {
        ConnectedScatterEngine.ResolveAxis(200, 1800, 200, 0, 1).ShouldBe((200, 1800, 200));
        var (min, max, step) = ConnectedScatterEngine.ResolveAxis(double.NaN, double.NaN, double.NaN, 2310, 4870);
        min.ShouldBeLessThanOrEqualTo(2310);
        max.ShouldBeGreaterThanOrEqualTo(4870);
        step.ShouldBeGreaterThan(0);
        ((max - min) / step).ShouldBeInRange(2, 6);
        ConnectedScatterEngine.Ticks(200, 1800, 200).Length.ShouldBe(9);
        ConnectedScatterEngine.Ticks(0, 1, 0.1)[3].ShouldBe(0.3);
    }

    [Fact]
    public void X_Ticks_Skip_Every_Other_One_And_Both_Ends_In_Compact_Widths()
    {
        ConnectedScatterEngine.XTickIndexes(9, false).ShouldBe([0, 1, 2, 3, 4, 5, 6, 7, 8]);
        // $200 … $1,800 gives $400 $800 $1,200 $1,600.
        ConnectedScatterEngine.XTickIndexes(9, true).ShouldBe([1, 3, 5, 7]);
    }

    [Fact]
    public void Default_Ticks_Are_Compact_For_Thousands_And_Full_Otherwise()
    {
        ConnectedScatterEngine.DefaultTick(2000, 1000, ConnectedScatterEngine.DefaultValue).ShouldBe("$2k");
        ConnectedScatterEngine.DefaultTick(1500, 500, ConnectedScatterEngine.DefaultValue).ShouldBe("$1,500");
        ConnectedScatterEngine.DefaultTick(1800, 200, ConnectedScatterEngine.DefaultValue).ShouldBe("$1,800");
    }

    [Fact]
    public void Target_Line_And_Healthy_Zone_Match_The_Measured_Geometry()
    {
        // 3x over $200–$1,800 × $2k–$6k: enters through the bottom at x = 666.7 and leaves through the right edge at y = 5400.
        ConnectedScatterEngine.TryTargetSegment(3, 200, 1800, 2000, 6000, out var from, out var to).ShouldBeTrue();
        from.X.ShouldBe(2000 / 3.0, 1e-9);
        from.Y.ShouldBe(2000, 1e-9);
        to.X.ShouldBe(1800, 1e-9);
        to.Y.ShouldBe(5400, 1e-9);
        var zone = ConnectedScatterEngine.HealthyPolygon(3, 200, 1800, 2000, 6000);
        zone.Length.ShouldBe(5);
        zone[0].ShouldBe(new Point(200, 6000));
        zone[1].ShouldBe(new Point(1800, 6000));
        // Every corner is on or above the line.
        zone.ShouldAllBe(p => p.Y >= 3 * p.X - 1e-6);
        // A line that misses the domain.
        ConnectedScatterEngine.TryTargetSegment(20, 200, 1800, 2000, 3000, out _, out _).ShouldBeFalse();
        ConnectedScatterEngine.HealthyPolygon(0.1, 200, 1800, 2000, 6000).Length.ShouldBe(4);
        ConnectedScatterEngine.HealthyPolygon(100, 200, 1800, 2000, 6000).Length.ShouldBe(0);
    }

    [Fact]
    public void Healthy_Is_Judged_On_The_Unrounded_Values_And_The_Ratio_Format_Has_One_Decimal()
    {
        ConnectedScatterEngine.IsHealthy(1000, 3000, 3).ShouldBeTrue();
        ConnectedScatterEngine.IsHealthy(1000, 2999.9, 3).ShouldBeFalse();
        ConnectedScatterEngine.DefaultRatio(4640 / 960.0).ShouldBe("4.8x");
        ConnectedScatterEngine.Ratio(0, 5).ShouldBe(double.NaN);
    }

    [Fact]
    public void Values_Between_Months_Are_Linear_And_The_Month_Rounds_Half_Up()
    {
        double[] values = [10, 20, 40];
        ConnectedScatterEngine.Interpolate(values, 0.5).ShouldBe(15);
        ConnectedScatterEngine.Interpolate(values, 1.75).ShouldBe(35);
        ConnectedScatterEngine.Interpolate(values, 9).ShouldBe(40);
        ConnectedScatterEngine.MonthOf(8.317, 18).ShouldBe(8);
        ConnectedScatterEngine.MonthOf(8.5, 18).ShouldBe(9);
        ConnectedScatterEngine.MonthOf(40, 18).ShouldBe(17);
        ConnectedScatterEngine.PositionAt(110, 10, 200, 11).ShouldBe(5, 1e-9);
        ConnectedScatterEngine.PositionAt(-50, 10, 200, 11).ShouldBe(0);
    }

    [Fact]
    public void Scrubber_Keys_Step_One_Three_Or_To_The_Ends_And_Round_Away_From_A_Fraction()
    {
        ConnectedScatterEngine.KeyTarget(Key.Right, 8, 18).ShouldBe(9);
        ConnectedScatterEngine.KeyTarget(Key.Up, 8, 18).ShouldBe(9);
        ConnectedScatterEngine.KeyTarget(Key.Left, 8, 18).ShouldBe(7);
        ConnectedScatterEngine.KeyTarget(Key.Down, 8, 18).ShouldBe(7);
        // Measured: 8.317 + → = 9; ← goes to 8.
        ConnectedScatterEngine.KeyTarget(Key.Right, 8.317, 18).ShouldBe(9);
        ConnectedScatterEngine.KeyTarget(Key.Left, 8.317, 18).ShouldBe(8);
        ConnectedScatterEngine.KeyTarget(Key.PageUp, 8, 18).ShouldBe(11);
        ConnectedScatterEngine.KeyTarget(Key.PageDown, 8, 18).ShouldBe(5);
        ConnectedScatterEngine.KeyTarget(Key.Home, 8, 18).ShouldBe(0);
        ConnectedScatterEngine.KeyTarget(Key.End, 8, 18).ShouldBe(17);
        // Clamped, never wrapping.
        ConnectedScatterEngine.KeyTarget(Key.Right, 17, 18).ShouldBe(17);
        ConnectedScatterEngine.KeyTarget(Key.PageDown, 1, 18).ShouldBe(0);
        ConnectedScatterEngine.KeyTarget(Key.A, 3, 18).ShouldBeNull();
    }

    [Fact]
    public void Hover_Finds_The_Nearest_Point_Within_Thirty_Pixels()
    {
        Point[] points = [new(100, 100), new(160, 100)];
        ConnectedScatterEngine.NearestPoint(points, new Point(129, 100)).ShouldBe(0);
        ConnectedScatterEngine.NearestPoint(points, new Point(131, 100)).ShouldBe(1);
        ConnectedScatterEngine.NearestPoint(points, new Point(130, 140)).ShouldBe(-1);
        ConnectedScatterEngine.NearestPoint(points, new Point(100, 129.5)).ShouldBe(0);
        ConnectedScatterEngine.NearestPoint(points, new Point(100, 130)).ShouldBe(-1);
    }

    [Fact]
    public void Short_Segments_Get_No_Arrow()
    {
        ConnectedScatterEngine.HasArrow(15.1).ShouldBeFalse();
        ConnectedScatterEngine.HasArrow(22.0).ShouldBeFalse();
        ConnectedScatterEngine.HasArrow(26.5).ShouldBeTrue();
    }

    [Fact]
    public void First_And_Last_Labels_Sit_On_The_Outer_Ends_Of_The_Path()
    {
        // The path starts going right: the first label is on the left. It ends going up-left: the last label is on the left too.
        ConnectedScatterEngine.FirstSide(new Point(100, 100), new Point(130, 98)).ShouldBe(ScatterLabelSide.Left);
        ConnectedScatterEngine.FirstSide(new Point(100, 100), new Point(98, 70)).ShouldBe(ScatterLabelSide.Bottom);
        ConnectedScatterEngine.LastSide(new Point(150, 150), new Point(120, 140), 700).ShouldBe(ScatterLabelSide.Left);
        ConnectedScatterEngine.LastSide(new Point(120, 140), new Point(150, 150), 700).ShouldBe(ScatterLabelSide.Right);
        // A narrow plot puts it above.
        ConnectedScatterEngine.LastSide(new Point(150, 150), new Point(120, 140), 314).ShouldBe(ScatterLabelSide.Top);
        ConnectedScatterEngine.LastSide(new Point(150, 150), new Point(120, 140), 334).ShouldBe(ScatterLabelSide.Left);
    }

    [Fact]
    public void Scrubber_Labels_Are_The_Quarters_The_Ends_And_Fewer_In_Narrow_Widths()
    {
        var points = Channel("s", "s", notes: false).Points;
        var wide = ConnectedScatterEngine.TimelineLabels(points, quarters: true);
        // Oct 2025 is two months from the last one, so it gives way to it.
        wide.Select(l => l.Text).ShouldBe(["Jul 2024", "Oct", "Jan 2025", "Apr", "Jul", "Dec 2025"]);
        wide.Select(l => l.Index).ShouldBe([0, 3, 6, 9, 12, 17]);
        var narrow = ConnectedScatterEngine.TimelineLabels(points, quarters: false);
        narrow.Select(l => l.Text).ShouldBe(["Jul 2024", "Jan 2025", "Dec 2025"]);
        // Months that do not read as dates still get labels.
        var weeks = new ScatterPoint[12];
        for (var i = 0; i < 12; i++)
        {
            weeks[i] = new ScatterPoint($"Week {i + 1}", i, i);
        }
        var fallback = ConnectedScatterEngine.TimelineLabels(weeks, true);
        fallback.Last().Text.ShouldBe("Week 12");
        fallback.Count.ShouldBeInRange(3, 8);
    }

    [Fact]
    public void Draw_In_Follows_The_In_Out_Curve_Over_1350ms()
    {
        var timeline = new ConnectedScatterTimeline();
        timeline.StartTween(0, 17, 1.35, ArcEasings.InOut);
        for (var i = 0; i < 40; i++)
        {
            timeline.Advance(0.0169);
        }
        // Half way in time is half way in distance; the middle runs fastest, the ends slowest.
        timeline.U.ShouldBe(8.5, 0.6);
        var early = new ConnectedScatterTimeline();
        early.StartTween(0, 17, 1.35, ArcEasings.InOut);
        early.Advance(0.1);
        early.U.ShouldBeLessThan(0.5);
        for (var i = 0; i < 100 && timeline.IsMoving; i++)
        {
            timeline.Advance(0.0169);
        }
        timeline.U.ShouldBe(17);
        timeline.TakeArrived().ShouldBeTrue();
        timeline.TakeArrived().ShouldBeFalse();
    }

    [Fact]
    public void Playback_Moves_One_Month_Per_240ms_And_A_Pause_Stays_Between_Months()
    {
        var timeline = new ConnectedScatterTimeline();
        timeline.StartLinear(17, 0.24);
        timeline.Advance(0.48);
        timeline.U.ShouldBe(2, 1e-9);
        timeline.Advance(0.1);
        timeline.U.ShouldBe(2.4167, 1e-3);
        timeline.Stop();
        timeline.IsMoving.ShouldBeFalse();
        timeline.U.ShouldBe(2.4167, 1e-3);
        timeline.StartLinear(17, 0.24);
        for (var i = 0; i < 400 && timeline.IsMoving; i++)
        {
            timeline.Advance(0.02);
        }
        timeline.U.ShouldBe(17);
        timeline.TakeArrived().ShouldBeTrue();
    }

    [Fact]
    public void Glide_Settles_On_The_Smooth_Spring_Without_Overshoot_And_Follow_Switches_To_Direct_Writes()
    {
        var timeline = new ConnectedScatterTimeline();
        timeline.Jump(9.4);
        timeline.GlideTo(9, AnalyticsSprings.Smooth);
        var min = 9.4;
        for (var i = 0; i < 100 && timeline.IsMoving; i++)
        {
            timeline.Advance(1 / 60.0);
            min = Math.Min(min, timeline.U);
        }
        min.ShouldBeGreaterThanOrEqualTo(9 - 1e-6);
        timeline.U.ShouldBe(9);
        timeline.TakeArrived().ShouldBeTrue();

        timeline.Jump(10);
        timeline.Follow(2, AnalyticsSprings.Smooth);
        timeline.Mode.ShouldBe(ScatterTimelineMode.Follow);
        // The head chases the pointer; ~650ms later it has caught up and writes 1:1.
        for (var i = 0; i < 60 && timeline.Mode == ScatterTimelineMode.Follow; i++)
        {
            timeline.Advance(1 / 60.0);
        }
        timeline.Mode.ShouldBe(ScatterTimelineMode.Direct);
        timeline.Follow(5.5, AnalyticsSprings.Smooth);
        timeline.U.ShouldBe(5.5);
        timeline.EndFollow();
        timeline.Mode.ShouldBe(ScatterTimelineMode.Idle);
    }

    #endregion

    #region Block

    [AvaloniaFact]
    public void Block_Rests_On_The_Last_Month_And_Shows_The_Readouts_The_Legend_And_The_Table()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            block.SettledIndex.ShouldBe(17);
            block.SelectedIndex.ShouldBe(17);
            block.Position.ShouldBe(17);
            var state = block.CurrentReadout;
            state.MonthName.ShouldBe("Month");
            state.MonthValue.ShouldBe("Dec 2025");
            state.MonthNote.ShouldBe("18 of 18");
            state.XName.ShouldBe("CAC");
            state.YName.ShouldBe("LTV");
            state.RatioName.ShouldBe("LTV to CAC");
            state.XNote.ShouldContain("since Jul 2024");
            state.XValue.ShouldStartWith("$");
            block.PlayButtonText!.Text.ShouldBe("Replay");
            block.Legend!.Items.Count.ShouldBe(3);
            block.Legend.Items[0].ShouldBe("Field sales, in time order");
            block.Legend.Items[1].ShouldBe("LTV = 3x CAC");
            block.Legend.Items[2].ShouldBe("Healthy zone");
            block.Table!.RowHeaders.Count.ShouldBe(18);
            block.Table.Caption.ShouldBe("Field sales: Acquisition cost and Customer value by month");
            block.Table.Rows[0][2].ShouldStartWith("LTV to CAC: ");
            block.EffectiveDescription.ShouldBe("Customer value against acquisition cost, Jul 2024 to Dec 2025.");
            AutomationProperties.GetName(block.Scrubber!).ShouldBe("Month");
            block.Scrubber!.ReportedIndex.ShouldBe(17);
            block.Scrubber.ValueText.ShouldContain("Dec 2025");
            block.Scrubber.Labels.Select(l => l.Text).ShouldBe(["Jul 2024", "Oct", "Jan 2025", "Apr", "Jul", "Dec 2025"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Readout_Status_Follows_The_Target_And_Is_Spoken_In_Words()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var series = block.ShownSeries!;
            for (var i = 0; i < 18; i++)
            {
                block.GoToMonth(i, animate: false);
                var point = series.Points[i];
                var healthy = point.Y >= 3 * point.X;
                block.CurrentReadout.IsHealthy.ShouldBe(healthy);
                block.CurrentReadout.RatioNote.ShouldBe(healthy ? "Above 3x target" : "Below 3x target");
                block.Announcer!.Announcement!.ShouldContain(healthy ? "above the 3x target" : "below the 3x target");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrubber_Keys_Move_The_Head_And_Raise_Month_Changed_Only_When_It_Settles()
    {
        var block = Block();
        var events = new List<int>();
        block.MonthChanged += (_, e) => events.Add(e.Index);
        var window = Show(block);
        try
        {
            block.Scrubber!.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(16);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(13);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(17);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(17);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            block.SelectedIndex.ShouldBe(17);
            events.ShouldBe([16, 13, 0, 0, 17, 17, 17]);
            block.PlayButtonText!.Text.ShouldBe("Replay");
            block.PlayButtonText.Text = "x";
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            block.PlayButtonText.Text.ShouldBe("Play");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_The_Selected_Index_Or_Calling_Go_To_Month_Moves_The_Head()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            block.SelectedIndex = 4;
            block.Position.ShouldBe(4);
            block.CurrentReadout.MonthValue.ShouldBe("Nov 2024");
            block.CurrentReadout.MonthNote.ShouldBe("5 of 18");
            block.GoToMonth(99, animate: false);
            block.Position.ShouldBe(17);
            block.GoToMonth(-3, animate: false);
            block.Position.ShouldBe(0);
            block.CurrentReadout.XNote.ShouldBe("Same as Jul 2024");
            block.CurrentReadout.YNote.ShouldBe("Same as Jul 2024");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Series_Changes_The_Data_The_Legend_And_Announces_The_Series()
    {
        var block = Block();
        var changes = new List<string?>();
        block.SeriesChanged += (_, e) => changes.Add(e.Series?.Name);
        var window = Show(block);
        try
        {
            var before = block.CurrentReadout.XValue;
            block.SelectSeries("community").ShouldBeTrue();
            block.SelectSeries("nope").ShouldBeFalse();
            Dispatcher.UIThread.RunJobs();
            block.SelectedSeriesKey.ShouldBe("community");
            block.CurrentReadout.XValue.ShouldNotBe(before);
            block.Legend!.Items[0].ShouldBe("Community, in time order");
            block.Table!.Caption.ShouldStartWith("Community:");
            block.Announcer!.Announcement.ShouldNotBeNull().ShouldStartWith("Community. Dec 2025:");
            changes.ShouldBe(["Community"]);
            block.SettledIndex.ShouldBe(17);
            block.Plot!.DisplayedX(17).ShouldBe(block.ShownSeries!.Points[17].X);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Series_Switch_Is_A_Segmented_Control_That_Selects_The_Series()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var switcher = TestHelpers.Descendant<SegmentedControl>(block);
            switcher.SelectedIndex.ShouldBe(0);
            switcher.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            block.SelectedSeriesKey.ShouldBe("resellers");
            block.ShownSeries!.Name.ShouldBe("Resellers");
            AutomationProperties.GetName(switcher).ShouldBe("Series");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Playback_At_Reduced_Motion_Jumps_A_Month_Every_960ms_And_Ends_On_The_Last_Month()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var events = new List<int>();
        block.MonthChanged += (_, e) => events.Add(e.Index);
        var window = Show(block, ArcMotionLevel.Reduced);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            block.GoToMonth(14, animate: false);
            events.Clear();
            block.Play();
            block.IsPlaying.ShouldBeTrue();
            block.PlayButtonText!.Text.ShouldBe("Pause");
            block.Plot!.HeadHaloScale.ShouldBe(1.0);
            clock.Advance(TimeSpan.FromMilliseconds(900));
            block.Position.ShouldBe(14);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            block.Position.ShouldBe(15);
            clock.Advance(TimeSpan.FromMilliseconds(970));
            block.Position.ShouldBe(16);
            clock.Advance(TimeSpan.FromMilliseconds(970));
            block.Position.ShouldBe(17);
            block.IsPlaying.ShouldBeFalse();
            block.PlayButtonText.Text.ShouldBe("Replay");
            events.ShouldBe([17]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Playback_At_Full_Motion_Is_Linear_Pauses_Between_Months_And_Replays_From_The_Start()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var events = new List<int>();
        block.MonthChanged += (_, e) => events.Add(e.Index);
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            block.PlayButtonText!.Text.ShouldBe("Replay");
            block.Replay();
            block.Position.ShouldBe(0);
            block.IsPlaying.ShouldBeTrue();
            block.Classes.Contains(ConnectedScatter.PlayingPseudoClass).ShouldBeTrue();
            for (var i = 0; i < 30; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(16));
            }
            // About 480ms in: two months at a constant pace.
            block.Position.ShouldBe(2, 0.3);
            block.Plot!.HeadHaloScale.ShouldBeGreaterThan(1.0);
            block.Pause();
            var stopped = block.Position;
            Math.Abs(stopped - Math.Round(stopped)).ShouldBeGreaterThan(0.001);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            block.Position.ShouldBe(stopped, 1e-9);
            block.IsPlaying.ShouldBeFalse();
            block.CurrentReadout.MonthValue.ShouldBe(block.ShownSeries!.Points[(int)Math.Floor(stopped + 0.5)].Label);
            events.ShouldBeEmpty();
            block.Play();
            for (var i = 0; i < 300 && block.IsPlaying; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(50));
            }
            block.Position.ShouldBe(17);
            events.ShouldBe([17]);
            block.PlayButtonText.Text.ShouldBe("Replay");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void First_Appearance_Runs_The_Head_Along_The_Path_And_Reveals_The_Rest_When_It_Lands()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block(playOnAppear: true);
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(16));
            block.Plot!.Reveal.ShouldBe(0);
            block.Position.ShouldBeLessThan(1);
            block.PlayButtonText!.Text.ShouldBe("Play");
            clock.Advance(TimeSpan.FromMilliseconds(660));
            block.IsDrawingIn.ShouldBeTrue();
            block.Position.ShouldBe(8.5, 1.2);
            block.Plot.Reveal.ShouldBe(0);
            for (var i = 0; i < 100 && block.IsDrawingIn; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(16));
            }
            block.IsDrawingIn.ShouldBeFalse();
            block.Position.ShouldBe(17);
            block.SettledIndex.ShouldBe(17);
            clock.Advance(TimeSpan.FromMilliseconds(16));
            block.Plot.Reveal.ShouldBeInRange(0.02, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(300));
            block.Plot.Reveal.ShouldBe(1);
            block.PlayButtonText.Text.ShouldBe("Replay");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_And_Off_Motion_Show_The_Finished_State_At_Once()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var block = Block(playOnAppear: true);
            var window = Show(block, level);
            try
            {
                block.IsAwaitingAppear.ShouldBeFalse();
                block.IsDrawingIn.ShouldBeFalse();
                block.Position.ShouldBe(17);
                block.Plot!.Reveal.ShouldBe(1);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Key_Glide_Uses_The_Smooth_Spring_And_Settles_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var events = new List<int>();
        block.MonthChanged += (_, e) => events.Add(e.Index);
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            block.GoToMonth(8);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            block.Position.ShouldBeInRange(9.5, 16.9);
            events.ShouldBeEmpty();
            clock.Advance(TimeSpan.FromMilliseconds(900));
            block.Position.ShouldBe(8);
            events.ShouldBe([8]);
            block.SelectedIndex.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_The_Scrubber_Chases_The_Pointer_And_Release_Snaps_To_The_Nearest_Month()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var events = new List<int>();
        block.MonthChanged += (_, e) => events.Add(e.Index);
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            var scrubber = block.Scrubber!;
            var track = scrubber.TranslatePoint(new Point(10 + (scrubber.Bounds.Width - 20) * 4.4 / 17, 22), window)!.Value;
            window.MouseDown(track, MouseButton.Left);
            scrubber.IsDragging.ShouldBeTrue();
            block.Plot!.HeadHaloScale.ShouldBe(1);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            block.Position.ShouldBeLessThan(17);
            block.Position.ShouldBeGreaterThan(4.4);
            for (var i = 0; i < 20; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(50));
            }
            // Caught up: 1:1 with the pointer.
            block.Position.ShouldBe(4.4, 0.02);
            scrubber.ThumbScale.ShouldBe(1.12, 0.01);
            events.ShouldBeEmpty();
            window.MouseUp(track, MouseButton.Left);
            for (var i = 0; i < 25; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(50));
            }
            block.Position.ShouldBe(4);
            events.ShouldBe([4]);
            block.SelectedIndex.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_A_Point_Shows_The_Card_And_Tapping_It_Moves_The_Head()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var plot = block.Plot!;
            var at = plot.PixelOf(5);
            var inWindow = plot.TranslatePoint(new Point(at.X + 6, at.Y + 4), window)!.Value;
            window.MouseMove(inWindow);
            plot.HoverIndex.ShouldBe(5);
            plot.Card.IsShown.ShouldBeTrue();
            plot.Card.Content!.Title.ShouldBe(block.ShownSeries!.Points[5].Label);
            plot.Card.Content.RatioName.ShouldBe("LTV to CAC");
            block.Position.ShouldBe(17);
            window.MouseMove(plot.TranslatePoint(new Point(2, 2), window)!.Value);
            plot.HoverIndex.ShouldBe(-1);
            plot.Card.IsShown.ShouldBeFalse();
            window.MouseDown(inWindow, MouseButton.Left);
            window.MouseUp(inWindow, MouseButton.Left);
            block.SelectedIndex.ShouldBe(5);
            block.Position.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Series_Morph_Springs_Every_Point_Together_And_Keeps_The_Head_Month()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            block.GoToMonth(9, animate: false);
            var plot = block.Plot!;
            var from = new[] { plot.DisplayedX(0), plot.DisplayedX(9), plot.DisplayedX(17) };
            var target = block.EffectiveSeries[1];
            block.SelectSeries("community");
            Dispatcher.UIThread.RunJobs();
            plot.IsMorphing.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(120));
            // Every point has left its place by a similar share of its way (no stagger).
            var shares = new[] { 0, 9, 17 }.Select((m, k) => (plot.DisplayedX(m) - from[k]) / (target.Points[m].X - from[k])).ToArray();
            shares.ShouldAllBe(s => s > 0.05 && s < 0.9);
            (shares.Max() - shares.Min()).ShouldBeLessThan(0.05);
            block.Position.ShouldBe(9);
            clock.Advance(TimeSpan.FromMilliseconds(1200));
            plot.IsMorphing.ShouldBeFalse();
            plot.DisplayedX(9).ShouldBe(target.Points[9].X, 0.01);
            block.CurrentReadout.XValue.ShouldBe(ConnectedScatterEngine.DefaultValue(target.Points[9].X));
            block.CurrentReadout.MonthValue.ShouldBe(target.Points[9].Label);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Series_Switch_At_Reduced_Motion_Replaces_The_Points_At_Once()
    {
        var block = Block();
        var window = Show(block, ArcMotionLevel.Reduced);
        try
        {
            block.SelectSeries("resellers");
            Dispatcher.UIThread.RunJobs();
            block.Plot!.IsMorphing.ShouldBeFalse();
            block.Plot.DisplayedY(3).ShouldBe(block.ShownSeries!.Points[3].Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_And_Notes_Follow_The_Width()
    {
        var wideBlock = Block();
        var window = Show(wideBlock);
        var wideArrows = 0;
        try
        {
            wideArrows = wideBlock.Plot!.Arrows.Take(17).Count(a => a);
            wideArrows.ShouldBeGreaterThan(5);
            wideBlock.Plot.Annotations.Select(a => a.Text).ShouldBe(["Jul 2024", "Dec 2025", "Pricing change", "New plan"], ignoreOrder: true);
            wideBlock.Plot.Annotations.Single(a => a.IsLast).Index.ShouldBe(17);
        }
        finally
        {
            window.Close();
        }

        var narrowBlock = Block();
        var host = new Border { Width = 420, Child = narrowBlock };
        var narrowWindow = Show(host);
        try
        {
            narrowBlock.Classes.Contains(ConnectedScatter.CompactPseudoClass).ShouldBeTrue();
            narrowBlock.Classes.Contains(ConnectedScatter.NarrowPseudoClass).ShouldBeTrue();
            narrowBlock.Plot!.Bounds.Height.ShouldBe(300);
            narrowBlock.Plot.Annotations.Count.ShouldBe(2);
            narrowBlock.Plot.Arrows.Take(17).Count(a => a).ShouldBeLessThan(wideArrows);
            narrowBlock.Scrubber!.Labels.Select(l => l.Text).ShouldBe(["Jul 2024", "Jan 2025", "Dec 2025"]);
        }
        finally
        {
            narrowWindow.Close();
        }
    }

    [AvaloniaFact]
    public void Layout_Follows_The_Blocks_Own_Width()
    {
        var wide = Block();
        var window = Show(wide);
        try
        {
            wide.Plot!.Bounds.Height.ShouldBe(380);
            wide.Readouts!.Bounds.Height.ShouldBeInRange(97, 100);
        }
        finally
        {
            window.Close();
        }

        var mid = Block();
        var midWindow = Show(new Border { Width = 640, Child = mid });
        try
        {
            mid.Plot!.Bounds.Height.ShouldBe(330);
            mid.Readouts!.Bounds.Height.ShouldBeInRange(197, 201);
            mid.Classes.Contains(ConnectedScatter.CompactPseudoClass).ShouldBeFalse();
        }
        finally
        {
            midWindow.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Target_The_Status_Zone_And_Labels_Disappear()
    {
        var block = Block();
        block.TargetRatio = double.NaN;
        var window = Show(block);
        try
        {
            block.Legend!.Items.Count.ShouldBe(1);
            block.CurrentReadout.HasRatio.ShouldBeFalse();
            block.CurrentReadout.RatioNote.ShouldBe(string.Empty);
            block.Announcer!.History.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Formatters_And_Words_Replace_The_Defaults()
    {
        var block = Block();
        block.ValueFormatter = v => $"{v:0} EUR";
        block.RatioFormatter = r => $"{r:0.00}×";
        block.MonthName = "Week";
        block.PlayText = "Run";
        var window = Show(block);
        try
        {
            block.GoToMonth(2, animate: false);
            block.CurrentReadout.XValue.ShouldEndWith(" EUR");
            block.CurrentReadout.RatioValue.ShouldEndWith("×");
            block.CurrentReadout.MonthName.ShouldBe("Week");
            block.CurrentReadout.RatioNote.ShouldBe("Above 3.00× target");
            block.PlayButtonText!.Text.ShouldBe("Run");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Series_Source_Wins_And_An_Empty_Block_Shows_Nothing_Without_Failing()
    {
        var empty = new ConnectedScatter { Title = "Nothing yet" };
        var window = Show(empty);
        try
        {
            empty.Classes.Contains(ConnectedScatter.EmptyPseudoClass).ShouldBeTrue();
            empty.SettledIndex.ShouldBe(-1);
            empty.Play();
            empty.IsPlaying.ShouldBeFalse();
            empty.Legend!.Items.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }

        var source = new Avalonia.Collections.AvaloniaList<ScatterSeries> { Channel("Own", "own") };
        var block = Block();
        block.SeriesSource = source;
        var second = Show(block);
        try
        {
            block.EffectiveSeries.Count.ShouldBe(1);
            block.ShownSeries!.Name.ShouldBe("Own");
            source.Add(Channel("Other", "other"));
            Dispatcher.UIThread.RunJobs();
            block.EffectiveSeries.Count.ShouldBe(2);
        }
        finally
        {
            second.Close();
        }
    }

    [AvaloniaFact]
    public void Colours_Come_From_Tokens_In_Both_Themes_And_Nothing_Throws_While_Rendering()
    {
        var block = Block();
        var window = Show(block, ArcMotionLevel.Off);
        try
        {
            foreach (var variant in new[] { Avalonia.Styling.ThemeVariant.Light, Avalonia.Styling.ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                Dispatcher.UIThread.RunJobs();
                block.SelectedIndex = 7;
                block.Plot!.InvalidateVisual();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                block.Position.ShouldBe(7);
            }
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
