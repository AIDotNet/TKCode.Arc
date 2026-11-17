using Avalonia;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>Journey flow's pure engine: structure, Sankey layout, cohort tracing (measured rules, design/notes journey-flow).</summary>
public class JourneyFlowEngineTests
{
    // Invented test journey (same shape as the measured preview: 5 stages, 1 + 2 + 2 + 2 + 2 nodes, 10 links).
    internal static readonly string[] StageKeys = ["landing", "trial", "lesson", "week", "plan"];

    internal static readonly (string Key, string Stage, JourneyNodeKind Kind)[] NodeDefs =
    [
        ("arrived", "landing", JourneyNodeKind.Step),
        ("started", "trial", JourneyNodeKind.Step),
        ("left", "trial", JourneyNodeKind.Exit),
        ("learned", "lesson", JourneyNodeKind.Step),
        ("idle", "lesson", JourneyNodeKind.Exit),
        ("hooked", "week", JourneyNodeKind.Step),
        ("drifting", "week", JourneyNodeKind.Exit),
        ("subscribed", "plan", JourneyNodeKind.Goal),
        ("lapsed", "plan", JourneyNodeKind.Exit),
    ];

    internal static readonly (string Source, string Target)[] Pairs =
    [
        ("arrived", "started"), ("arrived", "left"),
        ("started", "learned"), ("started", "idle"),
        ("learned", "hooked"), ("learned", "drifting"),
        ("hooked", "subscribed"), ("hooked", "lapsed"),
        ("drifting", "subscribed"), ("drifting", "lapsed"),
    ];

    internal static readonly double[] All = [5320, 11470, 4000, 1320, 2540, 1460, 940, 1600, 85, 1375];

    internal static JourneyFlowModel Model() => JourneyFlowModel.Build(
        StageKeys,
        NodeDefs.Select(n => ((string?)n.Key, (string?)n.Stage, n.Kind)).ToList(),
        Pairs.Select(p => ((string?)p.Source, (string?)p.Target)).ToList(),
        out _, out _);

    private static JourneyFlowLayout Layout(double width, double[]? values = null)
    {
        var layout = new JourneyFlowLayout();
        JourneyFlowEngine.Layout(Model(), values ?? All, width, JourneyFlowEngine.ChartHeight(width), layout);
        return layout;
    }

    [Theory]
    [InlineData(832, 404)]
    [InlineData(725, 404)]
    [InlineData(720, 404)]
    [InlineData(716, 372)]
    [InlineData(602, 372)]
    [InlineData(481, 372)]
    [InlineData(480, 372)]
    [InlineData(477, 340)]
    [InlineData(300, 340)]
    public void Chart_Height_Has_Three_Tiers_By_Width(double width, double height)
        => JourneyFlowEngine.ChartHeight(width).ShouldBe(height);

    [Theory]
    [InlineData(832, new double[] { 0, 206, 412, 618, 824 })]
    [InlineData(602, new double[] { 0, 149, 297, 446, 594 })]
    [InlineData(770, new double[] { 0, 191, 381, 572, 762 })]
    [InlineData(735, new double[] { 0, 182, 364, 545, 727 })]
    public void Columns_Are_Rounded_Half_Up_And_Touch_Both_Edges(double width, double[] expected)
    {
        var layout = Layout(width);
        layout.ColumnX.ShouldBe(expected);
        layout.Nodes[0].X.ShouldBe(0);
        layout.Nodes[8].Right.ShouldBe(width);
    }

    [Theory]
    [InlineData(832, 286)]
    [InlineData(602, 254)]
    [InlineData(400, 222)]
    public void Scale_Comes_From_The_Fullest_Two_Node_Stage_So_The_Entry_Node_Is_PlotHeight_Minus_A_Gap(double width, double entryHeight)
    {
        // Measured: the entry node is plotH − 44 high (404 → 286, 372 → 254, 340 → 222) because the second stage holds
        // everyone in two nodes 44 apart, and k = min over stages of (plotH − 44·(m − 1)) / total.
        var layout = Layout(width);
        layout.Top.ShouldBe(40);
        layout.Bottom.ShouldBe(layout.Height - 34);
        layout.Nodes[0].Height.ShouldBe(entryHeight, 1e-9);
        layout.Scale.ShouldBe(entryHeight / 16790, 1e-12);
        layout.BaselineY.ShouldBe(layout.Height - 23.5);
        layout.StageNameBaseline.ShouldBe(layout.Height - 6);
    }

    [Fact]
    public void Nodes_Stack_From_The_Top_44_Apart_And_Every_Stage_Is_Top_Aligned()
    {
        var layout = Layout(832);
        var k = layout.Scale;
        foreach (var top in new[] { 0, 1, 3, 5, 7 })
        {
            layout.Nodes[top].Y.ShouldBe(40);
        }
        layout.Nodes[1].Height.ShouldBe(5320 * k, 1e-9);
        layout.Nodes[2].Y.ShouldBe(40 + 5320 * k + 44, 1e-9);
        layout.Nodes[2].Bottom.ShouldBe(370, 1e-9); // the second stage fills the plot exactly: 40 + 330
        layout.Nodes[4].Y.ShouldBe(40 + 4000 * k + 44, 1e-9);
        // Goal 940 + 85 on top of the last stage, lapsed 1600 + 1375 under it.
        layout.Nodes[7].Height.ShouldBe(1025 * k, 1e-9);
        layout.Nodes[8].Y.ShouldBe(40 + 1025 * k + 44, 1e-9);
        layout.Nodes.ShouldAllBe(r => Math.Abs(r.Width - 8) < 1e-9);
    }

    [Fact]
    public void Node_Totals_Are_The_Larger_Of_Inflow_And_Outflow()
    {
        var totals = new double[9];
        JourneyFlowEngine.NodeTotals(Model(), All, totals);
        totals.ShouldBe([16790, 5320, 11470, 4000, 1320, 2540, 1460, 1025, 2975]);
        JourneyFlowEngine.Visitors(Model(), totals).ShouldBe(16790);
        JourneyFlowEngine.GoalTotal(Model(), totals).ShouldBe(1025);
    }

    [Fact]
    public void Link_Ends_Stack_In_The_Order_Of_The_Other_End_So_Only_The_Last_Stage_Crosses()
    {
        var model = Model();
        var layout = Layout(832);
        var k = layout.Scale;
        // Out of "hooked": subscribed (top) then lapsed; into "subscribed": hooked then drifting.
        layout.LinkSourceY[6].ShouldBe(layout.Nodes[5].Y, 1e-9);
        layout.LinkSourceY[7].ShouldBe(layout.Nodes[5].Y + 940 * k, 1e-9);
        layout.LinkTargetY[6].ShouldBe(layout.Nodes[7].Y, 1e-9);
        layout.LinkTargetY[8].ShouldBe(layout.Nodes[7].Y + 940 * k, 1e-9);
        // drifting → subscribed leaves the lower node and ends on the upper one: the one crossing.
        layout.LinkSourceY[8].ShouldBeGreaterThan(layout.LinkTargetY[8]);
        layout.LinkThickness[1].ShouldBe(11470 * k, 1e-9);
        layout.LinkSourceX(model, 0).ShouldBe(8);
        layout.LinkTargetX(model, 0).ShouldBe(206);
    }

    [Fact]
    public void Model_Orders_Links_By_Source_Then_Target_Place_And_Drops_Links_That_Skip_A_Stage()
    {
        var model = JourneyFlowModel.Build(
            StageKeys,
            NodeDefs.Select(n => ((string?)n.Key, (string?)n.Stage, n.Kind)).ToList(),
            [("arrived", "left"), ("arrived", "started"), ("arrived", "learned"), ("arrived", "started"), ("ghost", "left")],
            out var nodeIndex, out var linkIndex);
        model.LinkCount.ShouldBe(2);
        model.LinkTarget[0].ShouldBe(1); // started is above left
        linkIndex.ShouldBe([1, 0, -1, 0, -1]);
        nodeIndex.ShouldBe([0, 1, 2, 3, 4, 5, 6, 7, 8]);
        model.StageNodes[1].ShouldBe([1, 2]);
        model.NodeRank[2].ShouldBe(1);
    }

    [Fact]
    public void Node_Cohort_Takes_Its_Flows_In_Full_And_Mixes_Proportionally_Up_And_Down()
    {
        var model = Model();
        var cohort = new double[10];
        // Second-stage step: everything downstream, nothing to the first-stage exit (measured on the preview).
        JourneyFlowEngine.NodeCohort(model, All, 1, cohort);
        cohort.ShouldBe([5320, 0, 4000, 1320, 2540, 1460, 940, 1600, 85, 1375]);

        // "hooked": its outflows in full; upstream each node passes on the cohort's share of its outflow.
        JourneyFlowEngine.NodeCohort(model, All, 5, cohort);
        cohort[4].ShouldBe(2540);
        cohort[2].ShouldBe(2540, 1e-9);   // learned passes 2540 / 4000 of its inflow
        cohort[0].ShouldBe(2540, 1e-9);   // started passes 2540 / 5320
        cohort[6].ShouldBe(940);
        cohort[7].ShouldBe(1600);
        cohort[8].ShouldBe(0);
        cohort[3].ShouldBe(0);

        // The goal: 940 from hooked and 85 from drifting, mixed upstream.
        JourneyFlowEngine.NodeCohort(model, All, 7, cohort);
        cohort[4].ShouldBe(940, 1e-9);
        cohort[5].ShouldBe(85, 1e-9);
        cohort[2].ShouldBe(1025, 1e-9);
        cohort[0].ShouldBe(1025, 1e-9);
        cohort[7].ShouldBe(0);
        JourneyFlowEngine.NodeCohortTotal(model, cohort, 3).ShouldBe(1025, 1e-9);
        JourneyFlowEngine.NodeCohortTotal(model, cohort, 4).ShouldBe(0);
    }

    [Fact]
    public void Link_Cohort_Starts_From_The_Link_And_Spreads_Both_Ways()
    {
        var model = Model();
        var cohort = new double[10];
        JourneyFlowEngine.LinkCohort(model, All, 5, cohort); // learned → drifting
        cohort[5].ShouldBe(1460);
        cohort[8].ShouldBe(85, 1e-9);
        cohort[9].ShouldBe(1375, 1e-9);
        cohort[2].ShouldBe(1460, 1e-9);
        cohort[0].ShouldBe(1460, 1e-9);
        cohort[4].ShouldBe(0);
        cohort[6].ShouldBe(0);
        cohort[1].ShouldBe(0);
    }

    [Fact]
    public void Share_Base_Is_The_Nearest_Earlier_Node_That_Everyone_In_The_Stage_Came_Through()
    {
        var model = Model();
        var totals = new double[9];
        JourneyFlowEngine.NodeTotals(model, All, totals);
        JourneyFlowEngine.ShareBaseNode(model, totals, 0).ShouldBe(-1);
        JourneyFlowEngine.ShareBaseNode(model, totals, 1).ShouldBe(0);
        JourneyFlowEngine.ShareBaseNode(model, totals, 2).ShouldBe(1);
        JourneyFlowEngine.ShareBaseNode(model, totals, 3).ShouldBe(3);
        // Measured: the fourth and the fifth stage both name the third stage's step.
        JourneyFlowEngine.ShareBaseNode(model, totals, 4).ShouldBe(3);
    }

    [Fact]
    public void Drop_Offs_Are_Terminal_Exits_Before_The_Last_Stage_Largest_First()
    {
        var model = Model();
        var totals = new double[9];
        JourneyFlowEngine.NodeTotals(model, All, totals);
        var drops = JourneyFlowEngine.DropOffs(model, All, totals, 2);
        drops.Select(d => d.Node).ShouldBe([2, 4]); // left, went quiet; drifting still flows on, so it is not listed
        drops[0].Share.ShouldBe(11470.0 / 16790, 1e-12);
        drops[1].Share.ShouldBe(1320.0 / 5320, 1e-12);
        JourneyFlowEngine.DropOffs(model, All, totals, 1).Count.ShouldBe(1);
    }

    [Fact]
    public void Arrow_Keys_Across_Stages_Pick_The_Nearest_Centre()
    {
        var model = Model();
        var layout = Layout(832);
        JourneyFlowEngine.Nearest(model, layout, 2, 1).ShouldBe(4);  // the lower exit to the lower exit
        JourneyFlowEngine.Nearest(model, layout, 1, 1).ShouldBe(3);
        JourneyFlowEngine.Nearest(model, layout, 0, 1).ShouldBe(2);  // the tall entry's centre is nearer the lower node
        JourneyFlowEngine.Nearest(model, layout, 4, -1).ShouldBe(1);  // the upper step's centre is nearer
        JourneyFlowEngine.Nearest(model, layout, 0, -1).ShouldBe(-1);
        JourneyFlowEngine.Nearest(model, layout, 8, 1).ShouldBe(-1);
    }

    [Theory]
    [InlineData(832, (int)JourneyLabelDensity.Full)]
    [InlineData(604, (int)JourneyLabelDensity.Full)]
    [InlineData(600, (int)JourneyLabelDensity.Full)]
    [InlineData(590, (int)JourneyLabelDensity.NoShare)]
    [InlineData(447, (int)JourneyLabelDensity.NoShare)]
    [InlineData(427, (int)JourneyLabelDensity.Compact)]
    public void Labels_Lose_The_Share_Then_The_Top_Names_As_The_Chart_Narrows(double width, int density)
        => JourneyFlowEngine.Density(width).ShouldBe((JourneyLabelDensity)density);

    [Fact]
    public void Ribbon_Edges_Are_Horizontal_S_Curves_And_Hits_Widen_By_Two_Pixels()
    {
        // Control points at the midpoint: half way along x the edge is half way down, and the ends are flat.
        JourneyFlowEngine.RibbonParameter(0, 200, 100).ShouldBe(0.5, 1e-6);
        JourneyFlowEngine.RibbonEdgeY(40, 140, 0.5).ShouldBe(90, 1e-9);
        JourneyFlowEngine.RibbonEdgeY(40, 140, 0).ShouldBe(40);
        var t = JourneyFlowEngine.RibbonParameter(0, 200, 20);
        JourneyFlowEngine.RibbonEdgeY(40, 140, t).ShouldBeLessThan(45);
        JourneyFlowEngine.RibbonContains(0, 40, 60, 200, 140, 160, new Point(100, 100)).ShouldBeTrue();
        JourneyFlowEngine.RibbonContains(0, 40, 60, 200, 140, 160, new Point(100, 88.5)).ShouldBeTrue();  // 1.5px above: slop
        JourneyFlowEngine.RibbonContains(0, 40, 60, 200, 140, 160, new Point(100, 87)).ShouldBeFalse();
        JourneyFlowEngine.RibbonContains(0, 40, 60, 200, 140, 160, new Point(203, 150)).ShouldBeFalse();
    }

    [Fact]
    public void Hit_Boxes_Match_The_Measured_Buttons()
    {
        var node = new Rect(206, 40, 8, 90);
        JourneyFlowEngine.NodeHitRect(node).ShouldBe(new Rect(200, 37, 20, 96));
        JourneyFlowEngine.LabelHitRect(node, false).ShouldBe(new Rect(200, 4, 92, 33));
        JourneyFlowEngine.LabelHitRect(new Rect(824, 40, 8, 20), true).ShouldBe(new Rect(746, 4, 92, 33));
    }

    [Fact]
    public void Count_Spring_Matches_The_Measured_Morph()
    {
        // Measured k ≈ 70, c ≈ 16.4: about half way at 190ms, 90% at 460ms, 99% at 780ms, no overshoot.
        var spec = JourneyFlowChart.CountSpring;
        double Progress(double t) => 1 - SpringSolver.Evaluate(spec, 1, 0, t).Displacement;
        Progress(0.19).ShouldBe(0.5, 0.04);
        Progress(0.46).ShouldBe(0.9, 0.02);
        Progress(0.78).ShouldBe(0.99, 0.01);
        for (var t = 0.0; t < 2; t += 0.01)
        {
            Progress(t).ShouldBeLessThanOrEqualTo(1.0001); // ζ ≈ 0.98: no visible overshoot
        }
    }

    [Fact]
    public void Zero_Counts_Lay_Out_Flat_Without_Errors()
    {
        var layout = Layout(832, new double[10]);
        layout.Scale.ShouldBe(0);
        layout.Nodes.ShouldAllBe(r => r.Height == 0);
        layout.Nodes[2].Y.ShouldBe(84);
    }
}
