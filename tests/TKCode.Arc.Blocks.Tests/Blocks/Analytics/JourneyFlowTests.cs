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

/// <summary>Journey flow (headless): layout by width, tracing, selection, keyboard, segments, motion.</summary>
public class JourneyFlowTests
{
    private static readonly string[] SegmentNames = ["All", "Search", "Social"];

    private static readonly double[][] SegmentValues =
    [
        JourneyFlowEngineTests.All,
        [3140, 4910, 2400, 740, 1520, 880, 470, 1050, 40, 840],
        [1120, 5230, 690, 430, 330, 360, 140, 190, 25, 335],
    ];

    private static JourneyFlow Create(double width = 1200)
    {
        var flow = new JourneyFlow { Width = width, Title = "Trial to paid" };
        foreach (var key in JourneyFlowEngineTests.StageKeys)
        {
            flow.Stages.Add(new JourneyStage(key, char.ToUpperInvariant(key[0]) + key[1..], key[..2]));
        }
        foreach (var (key, stage, kind) in JourneyFlowEngineTests.NodeDefs)
        {
            flow.Nodes.Add(new JourneyNode(key, stage, char.ToUpperInvariant(key[0]) + key[1..], kind, $"About {key}"));
        }
        for (var s = 0; s < SegmentNames.Length; s++)
        {
            var segment = new JourneySegment(SegmentNames[s].ToLowerInvariant(), SegmentNames[s]);
            for (var i = 0; i < JourneyFlowEngineTests.Pairs.Length; i++)
            {
                segment.Links.Add(new JourneyLink(JourneyFlowEngineTests.Pairs[i].Source, JourneyFlowEngineTests.Pairs[i].Target, SegmentValues[s][i]));
            }
            flow.Segments.Add(segment);
        }
        return flow;
    }

    private static Window Show(JourneyFlow flow, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(flow, level);
        window.Width = Math.Max(1300, flow.Width + 40);
        window.Height = 1200;
        Pump();
        return window;
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static Point InWindow(JourneyFlowChart chart, Point point, Window window) => chart.TranslatePoint(point, window) ?? default;

    [AvaloniaFact]
    public void Wide_Block_Puts_The_Panel_Beside_A_404_High_Chart()
    {
        var flow = Create(1200);
        var window = Show(flow);
        try
        {
            ((IPseudoClasses)flow.Classes).Contains(JourneyFlow.WidePseudoClass).ShouldBeTrue();
            var chart = flow.Chart!;
            chart.Bounds.Width.ShouldBe(1200 - 280 - 40);
            chart.Bounds.Height.ShouldBe(404);
            chart.CurrentLayout.ColumnX[4].ShouldBe(880 - 8);
            chart.Buttons.Count.ShouldBe(9);
            chart.Buttons.Count(b => b.IsTabStop).ShouldBe(1);
            chart.Buttons[0].IsTabStop.ShouldBeTrue();
            flow.SegmentSwitch!.IsVisible.ShouldBeTrue();
            flow.SegmentSwitch.SelectedIndex.ShouldBe(0);
            flow.SegmentSwitch.Items!.Count.ShouldBe(3);
            flow.Visitors.ShouldBe(16790);
            flow.GoalTotal.ShouldBe(1025);

            var texts = flow.PanelTexts;
            texts.Title.ShouldBe("All");
            texts.Big.ShouldBe("1,025");
            texts.Share.ShouldBe("6.1%");
            texts.Caption.ShouldBe("of visitors reached the goal");
            texts.First.ShouldBe("Biggest drop-offs");
            texts.Second.ShouldBe("Plan");
            flow.FirstRows.Count.ShouldBe(2);
            flow.FirstRows[0].Label.ShouldBe("Left");
            flow.FirstRows[0].ShareText.ShouldBe("68.3%");
            flow.FirstRows[1].Label.ShouldBe("Idle");
            flow.SecondRows[0].Label.ShouldBe("Subscribed");
            flow.SecondRows[0].ShareText.ShouldBe("25.6%");

            flow.Table!.RowCount.ShouldBe(10);
            flow.Table.Caption.ShouldBe("Flows for All");
            flow.Table.CellText(-1, 2).ShouldBe("Users");
            flow.Table.CellText(0, 0).ShouldBe("Arrived");
            flow.Table.CellText(0, 2).ShouldBe("5,320");
            flow.Table.CellText(0, 3).ShouldBe("31.7%");
            AutomationProperties.GetName(chart.Buttons[1]).ShouldBe("Started: 5,320 users, 31.7% of visitors. About started.");
            AutomationProperties.GetHelpText(chart).ShouldStartWith("All. Landing: Arrived 16,790 users.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(860, true, false, false, 372)]
    [InlineData(1100, true, false, false, 404)]
    [InlineData(859, false, false, false, 404)]
    [InlineData(700, false, false, false, 372)]
    [InlineData(559, false, true, false, 372)]
    [InlineData(479, false, true, true, 340)]
    [InlineData(390, false, true, true, 340)]
    public void Breakpoints_Follow_The_Blocks_Own_Width(double width, bool wide, bool compact, bool tight, double chartHeight)
    {
        var flow = Create(width);
        var window = Show(flow);
        try
        {
            var classes = (IPseudoClasses)flow.Classes;
            classes.Contains(JourneyFlow.WidePseudoClass).ShouldBe(wide);
            classes.Contains(JourneyFlow.CompactPseudoClass).ShouldBe(compact);
            classes.Contains(JourneyFlow.TightPseudoClass).ShouldBe(tight);
            var chart = flow.Chart!;
            var expectedWidth = wide ? width - 280 - 40 : width - (compact ? 32 : 40);
            chart.Bounds.Width.ShouldBe(expectedWidth);
            chart.Bounds.Height.ShouldBe(chartHeight);
            chart.IsTight.ShouldBe(tight);
            TestHelpers.Descendant<JourneyFlowGroupsPanel>(flow).Columns.ShouldBe(!wide && !compact ? 2 : 1);
            TestHelpers.Descendant<JourneyFlowHeaderPanel>(flow).IsStretched.ShouldBe(compact);
            flow.SegmentSwitch!.IsStretched.ShouldBe(compact);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Over_A_Node_Or_Ribbon_Traces_Its_Cohort_With_A_Tooltip()
    {
        var flow = Create(1200);
        var window = Show(flow);
        var traces = new List<JourneyTraceChangedEventArgs>();
        flow.TraceChanged += (_, e) => traces.Add(e);
        try
        {
            var chart = flow.Chart!;
            var layout = chart.CurrentLayout;
            var started = layout.Nodes[1];
            window.MouseMove(InWindow(chart, started.Center, window), RawInputModifiers.None);
            Pump();
            chart.TraceNode.ShouldBe(1);
            flow.HoveredNode.ShouldBe(flow.Nodes[1]);
            ((IPseudoClasses)flow.Classes).Contains(JourneyFlow.TracingPseudoClass).ShouldBeTrue();
            traces[^1].Node.ShouldBe(flow.Nodes[1]);
            chart.TraceFraction(0).ShouldBe(1);
            chart.TraceFraction(1).ShouldBe(0);
            chart.TraceFraction(6).ShouldBe(1);
            chart.DimProgress.ShouldBe(1);
            chart.LabelOpacity(2).ShouldBe(0.35);
            chart.LabelOpacity(3).ShouldBe(1);
            chart.Readout.IsShown.ShouldBeTrue();
            chart.Readout.Content!.Title.ShouldBe("Started");
            chart.Readout.Content.Value.ShouldBe("5,320 users");
            chart.Readout.Content.Rows![0].Label.ShouldBe("Share of arrived");
            chart.Readout.Content.Rows[0].Value.ShouldBe("31.7%");
            chart.Readout.Content.Rows[1].Label.ShouldBe("Share of visitors");
            // Top-left at pointer + (14, 16).
            chart.Readout.Position.X.ShouldBe(started.Center.X + 14, 0.01);
            chart.Readout.Position.Y.ShouldBe(started.Center.Y + 16, 0.01);

            // The label area counts as the node too.
            window.MouseMove(InWindow(chart, new Point(started.X + 40, started.Y - 20), window), RawInputModifiers.None);
            Pump();
            chart.TraceNode.ShouldBe(1);

            // A ribbon: learned → drifting, half way along.
            var model = chart.Model;
            var l = 5;
            var x0 = layout.LinkSourceX(model, l);
            var x1 = layout.LinkTargetX(model, l);
            var y = (layout.LinkSourceY[l] + layout.LinkTargetY[l]) / 2 + layout.LinkThickness[l] / 2;
            window.MouseMove(InWindow(chart, new Point((x0 + x1) / 2, y), window), RawInputModifiers.None);
            Pump();
            chart.TraceLink.ShouldBe(5);
            flow.HoveredLink.ShouldBe(flow.Segments[0].Links[5]);
            chart.Readout.Content!.Title.ShouldBe("Learned → Drifting");
            chart.Readout.Content.Rows![0].Label.ShouldBe("Share of learned");
            chart.TraceFraction(9).ShouldBe(1);
            chart.TraceFraction(4).ShouldBe(0);

            // Far right: the tooltip flips to the left of the pointer.
            var goal = layout.Nodes[7];
            window.MouseMove(InWindow(chart, goal.Center, window), RawInputModifiers.None);
            Pump();
            chart.Readout.Position.X.ShouldBe(goal.Center.X - 14 - 212, 0.01);
            chart.Readout.Content!.Rows![0].Label.ShouldBe("Share of learned");

            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            Pump();
            chart.TraceNode.ShouldBe(-1);
            chart.TraceLink.ShouldBe(-1);
            chart.Readout.IsVisible.ShouldBeFalse();
            flow.HoveredNode.ShouldBeNull();
            chart.DimProgress.ShouldBe(0);
            traces[^1].Node.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Node_Selects_It_And_The_Panel_Shows_Where_Its_People_Came_From_And_Went()
    {
        var flow = Create(1200);
        var window = Show(flow);
        var changes = new List<JourneyNodeChangedEventArgs>();
        flow.SelectedNodeChanged += (_, e) => changes.Add(e);
        object? commandParameter = "unset";
        flow.NodeSelectedCommand = new TestCommand(p => commandParameter = p);
        try
        {
            var chart = flow.Chart!;
            var layout = chart.CurrentLayout;
            void Click(Point p)
            {
                var at = InWindow(chart, p, window);
                window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
                Pump();
            }

            Click(layout.Nodes[0].Center);
            flow.SelectedNode.ShouldBe(flow.Nodes[0]);
            commandParameter.ShouldBe(flow.Nodes[0]);
            chart.Buttons[0].IsChecked.ShouldBe(true);
            ((IPseudoClasses)flow.Classes).Contains(JourneyFlow.HasSelectionPseudoClass).ShouldBeTrue();
            var texts = flow.PanelTexts;
            texts.Title.ShouldBe("Arrived");
            texts.Note.ShouldBe("About arrived");
            texts.Big.ShouldBe("16,790");
            texts.Share.ShouldBe("100.0%");
            texts.First.ShouldBe("Arrived from");
            flow.FirstRows[0].State.ShouldBe(JourneyFlowRowState.Placeholder);
            flow.FirstRows[0].Label.ShouldBe("The journey begins here");
            flow.FirstRows[1].State.ShouldBe(JourneyFlowRowState.Empty);
            flow.SecondRows[0].Label.ShouldBe("Started");
            flow.SecondRows[0].ShareText.ShouldBe("31.7%");
            flow.SecondRows[1].CountText.ShouldBe("11,470");

            // Pointer elsewhere: the selection stays traced.
            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            Pump();
            chart.TraceNode.ShouldBe(0);
            chart.Readout.IsShown.ShouldBeFalse();

            // A ribbon click changes nothing; the same node again clears; empty space clears.
            var model = chart.Model;
            Click(new Point((layout.LinkSourceX(model, 1) + layout.LinkTargetX(model, 1)) / 2,
                (layout.LinkSourceY[1] + layout.LinkTargetY[1]) / 2 + layout.LinkThickness[1] / 2));
            flow.SelectedNode.ShouldBe(flow.Nodes[0]);
            Click(layout.Nodes[0].Center);
            flow.SelectedNode.ShouldBeNull();
            commandParameter.ShouldBeNull();
            Click(layout.Nodes[8].Center);
            flow.SelectedNode.ShouldBe(flow.Nodes[8]);
            flow.SecondRows[0].Label.ShouldBe("The journey stops here");
            Click(new Point(100, layout.Height - 40));
            flow.SelectedNode.ShouldBeNull();
            changes.Count.ShouldBe(4);
            changes[^1].OldNode.ShouldBe(flow.Nodes[8]);
            flow.PanelTexts.Title.ShouldBe("All");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Through_The_Nodes_Previews_Them_And_Selects()
    {
        var flow = Create(1200);
        var window = Show(flow);
        try
        {
            var chart = flow.Chart!;
            chart.FocusNode(0);
            Pump();
            chart.Buttons[0].IsFocused.ShouldBeTrue();
            chart.TraceNode.ShouldBe(0);
            chart.Readout.IsShown.ShouldBeTrue();
            // Anchored beside the node: (x + 8 + 14, y − 8).
            chart.Readout.Position.ShouldBe(new Point(22, 32));

            // → from the tall entry node: the lower node of the next stage has the nearer centre.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            window.FocusManager!.GetFocusedElement().ShouldBe(chart.Buttons[2]);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Pump();
            chart.Buttons[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            chart.Buttons[2].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            chart.Buttons[2].IsFocused.ShouldBeTrue(); // no wrap
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            chart.Buttons[4].IsFocused.ShouldBeTrue(); // nearest centre: the lower exit
            chart.Buttons.Count(b => b.IsTabStop).ShouldBe(1);
            chart.Buttons[4].IsTabStop.ShouldBeTrue();
            flow.HoveredNode.ShouldBe(flow.Nodes[4]);

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            flow.SelectedNode.ShouldBe(flow.Nodes[4]);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Pump();
            chart.Buttons[8].IsFocused.ShouldBeTrue();
            // The last stage flips the anchored tooltip to the left.
            chart.Readout.Position.X.ShouldBe(chart.CurrentLayout.Nodes[8].X - 14 - 212, 0.01);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            flow.SelectedNode.ShouldBeNull();
            chart.Buttons[8].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Pump();
            chart.Buttons[0].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clear_Button_Clears_And_Returns_Focus_To_The_Node()
    {
        var flow = Create(1200);
        var window = Show(flow);
        try
        {
            flow.SelectNode("hooked").ShouldBeTrue();
            Pump();
            var clear = TestHelpers.Descendant<JourneyFlowClearButton>(flow);
            clear.IsVisible.ShouldBeTrue();
            clear.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            flow.SelectedNode.ShouldBeNull();
            clear.IsVisible.ShouldBeFalse();
            flow.Chart!.Buttons[5].IsFocused.ShouldBeTrue();
            flow.SelectNode("nobody").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Segments_Morphs_Counts_On_The_Measured_Spring_With_Labels_A_Beat_Later()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var flow = Create(1200);
        var window = Show(flow, ArcMotionLevel.Full);
        var segmentEvents = new List<JourneySegmentChangedEventArgs>();
        flow.SelectedSegmentChanged += (_, e) => segmentEvents.Add(e);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var chart = flow.Chart!;
            flow.SelectNode("started");
            clock.Advance(TimeSpan.FromMilliseconds(400));
            var from = chart.CurrentValues[1];
            flow.SelectedSegment = flow.Segments[2];
            Dispatcher.UIThread.RunJobs();
            segmentEvents.Count.ShouldBe(1);
            flow.Announcer!.Announcement.ShouldBe("Social: 6,350 visits and 165 reached the goal.");
            flow.Visitors.ShouldBe(6350);
            chart.TargetValues[1].ShouldBe(5230);
            chart.CurrentValues[1].ShouldBe(from);

            clock.Advance(TimeSpan.FromMilliseconds(30));
            chart.CurrentValues[1].ShouldNotBe(from);
            chart.LabelValue(2).ShouldBe(11470); // labels wait about 65ms
            clock.Advance(TimeSpan.FromMilliseconds(160));
            var progress = (chart.CurrentValues[1] - from) / (5230 - from);
            progress.ShouldBe(0.48, 0.08);
            chart.LabelValue(2).ShouldBeGreaterThan(5230);
            chart.LabelValue(2).ShouldBeLessThan(11470);
            // Every frame re-lays the chart out: the first stage keeps the full plot height minus a gap.
            chart.CurrentLayout.Nodes[0].Height.ShouldBe(286, 1e-6);

            clock.Advance(TimeSpan.FromMilliseconds(2000));
            chart.CurrentValues[1].ShouldBe(5230);
            chart.LabelValue(2).ShouldBe(5230);
            chart.IsLooping.ShouldBeFalse();
            // The selection is kept across segments and traced with the new counts.
            flow.SelectedNode.ShouldBe(flow.Nodes[1]);
            chart.TraceNode.ShouldBe(1);
            flow.PanelTexts.Big.ShouldBe("1,120");
            flow.Table!.Caption.ShouldBe("Flows for Social");
            flow.SegmentSwitch!.SelectedIndex.ShouldBe(2);

            // Reduced motion: the next switch lands at once.
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Dispatcher.UIThread.RunJobs();
            flow.SelectedSegment = flow.Segments[1];
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(20));
            chart.CurrentValues[1].ShouldBe(4910);
            chart.LabelValue(2).ShouldBe(4910);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Trace_Grows_On_A_Quick_Spring_And_The_Rest_Fades_In_160ms()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var flow = Create(1200);
        var window = Show(flow, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var chart = flow.Chart!;
            window.MouseMove(InWindow(chart, chart.CurrentLayout.Nodes[5].Center, window), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chart.TraceNode.ShouldBe(5);
            chart.TraceFraction(4).ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(90));
            chart.TraceFraction(4).ShouldBe(0.5, 0.15);   // measured: about half at 90ms
            chart.DimProgress.ShouldBeInRange(0.3, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            chart.DimProgress.ShouldBe(1);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            chart.TraceFraction(4).ShouldBe(1);
            chart.TraceFraction(2).ShouldBe(2540.0 / 4000, 1e-6);
            chart.IsLooping.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flow_Table_Toggles_And_Formatters_Apply_Everywhere()
    {
        var flow = Create(1200);
        flow.ValueFormatter = v => $"{v / 1000:0.0}k";
        flow.TableColumnHeaders = ["From", "To", "Learners", "Share"];
        var window = Show(flow);
        try
        {
            var button = TestHelpers.Descendant<JourneyFlowTableButton>(flow);
            button.IsExpanded.ShouldBeFalse();
            ((Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(button)).ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            flow.IsTableExpanded.ShouldBeTrue();
            button.IsExpanded.ShouldBeTrue();
            flow.Table!.CellText(0, 2).ShouldBe("5.3k");
            flow.Table.CellText(-1, 2).ShouldBe("Learners");
            flow.PanelTexts.Big.ShouldBe("1.0k");
            flow.Chart!.Readout.ShouldNotBeNull();

            var cohort = flow.GetCohort(flow.Nodes[5]);
            cohort[flow.Segments[0].Links[2]].ShouldBe(2540, 1e-6);
            cohort[flow.Segments[0].Links[3]].ShouldBe(0);
            var linkCohort = flow.GetCohort(flow.Segments[0].Links[5]);
            linkCohort[flow.Segments[0].Links[9]].ShouldBe(1375, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void One_Segment_Hides_The_Switch_And_Rolling_Text_Splits_Formatted_Numbers()
    {
        var flow = Create(900);
        flow.Segments.RemoveAt(2);
        flow.Segments.RemoveAt(1);
        var window = Show(flow);
        try
        {
            flow.SegmentSwitch!.IsVisible.ShouldBeFalse();
            JourneyFlowRollingText.TrySplit("$1,234.5k", System.Globalization.CultureInfo.GetCultureInfo("en-US"),
                out var prefix, out var value, out var decimals, out var suffix).ShouldBeTrue();
            prefix.ShouldBe("$");
            value.ShouldBe(1234.5);
            decimals.ShouldBe(1);
            suffix.ShouldBe("k");
            JourneyFlowRollingText.TrySplit("none", System.Globalization.CultureInfo.InvariantCulture, out _, out _, out _, out _).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class TestCommand(Action<object?> execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
