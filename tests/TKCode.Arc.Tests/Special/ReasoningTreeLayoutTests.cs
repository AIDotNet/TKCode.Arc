using Avalonia;
using Avalonia.Headless.XUnit;
using static TKCode.Arc.Tests.Special.ReasoningTreeSearchTests;

namespace TKCode.Arc.Tests.Special;

/// <summary>The wide and narrow layouts of <see cref="ReasoningTree"/> against the rows measured on the reference preview.</summary>
public class ReasoningTreeLayoutTests
{
    private static double[] Tops(ReasoningTreeModel model, Rect[] rects, ReasoningTreeResult result, int depth) =>
        model.Nodes.Where(n => n.Depth == depth && result.IsShown(n.Index))
             .Select(n => Math.Round(rects[n.Index].Y, 2))
             .OrderBy(y => y)
             .ToArray();

    private static (ReasoningTreeModel Model, ReasoningTreeResult[] Results, double Height) Measured()
    {
        var model = Model(MeasuredShape());
        var results = new[] { 1, 2, 3 }.Select(b => ReasoningTreeSearch.Run(model, b)).ToArray();
        return (model, results, ReasoningTreeLayout.WideHeight(model, results));
    }

    [AvaloniaFact]
    public void Columns_Follow_The_Measured_Shares()
    {
        var (x0, w0) = ReasoningTreeLayout.Column(0, 694);
        x0.ShouldBe(0);
        w0.ShouldBe(131.86, 0.01);
        ReasoningTreeLayout.Column(1, 694).X.ShouldBe(163.09, 0.01);
        ReasoningTreeLayout.Column(2, 694).X.ShouldBe(350.47, 0.01);
        ReasoningTreeLayout.Column(3, 694).X.ShouldBe(537.85, 0.01);
        ReasoningTreeLayout.Column(3, 694).Width.ShouldBe(156.15, 0.01);
        ReasoningTreeLayout.Column(0, 620).Width.ShouldBe(117.8, 0.01);
        ReasoningTreeLayout.Column(1, 620).X.ShouldBe(145.7, 0.01);
        ReasoningTreeLayout.Column(2, 620).X.ShouldBe(313.1, 0.01);
        ReasoningTreeLayout.Column(3, 620).X.ShouldBe(480.5, 0.01);
        ReasoningTreeLayout.Column(1, 620).Width.ShouldBe(139.5, 0.01);
        ReasoningTreeLayout.IsWide(620).ShouldBeTrue();
        ReasoningTreeLayout.IsWide(618).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Wide_Stage_Fits_The_Tallest_Column_Of_Any_Beam()
    {
        var (_, _, height) = Measured();
        height.ShouldBe(394);
        var single = Model([T("only", 0.5)]);
        ReasoningTreeLayout.WideHeight(single, [ReasoningTreeSearch.Run(single, 2)]).ShouldBe(30 + 84);
    }

    [AvaloniaFact]
    public void Wide_Rows_Match_The_Measured_Beam_Of_One()
    {
        var (model, results, height) = Measured();
        var rects = ReasoningTreeLayout.Wide(model, results[0], 694, height);
        rects[0].Y.ShouldBe(170);
        rects[0].Height.ShouldBe(84);
        Tops(model, rects, results[0], 1).ShouldBe([123, 185, 247]);
        Tops(model, rects, results[0], 2).ShouldBe([92, 154]);
        Tops(model, rects, results[0], 3).ShouldBe([61, 123]);
    }

    [AvaloniaFact]
    public void Wide_Rows_Match_The_Measured_Beam_Of_Two()
    {
        var (model, results, height) = Measured();
        var rects = ReasoningTreeLayout.Wide(model, results[1], 694, height);
        rects[0].Y.ShouldBe(170);
        Tops(model, rects, results[1], 1).ShouldBe([123, 185, 247]);
        Tops(model, rects, results[1], 2).ShouldBe([92, 154, 216, 278]);
        Tops(model, rects, results[1], 3).ShouldBe([61, 123, 247, 309]);
    }

    [AvaloniaFact]
    public void Wide_Rows_Match_The_Measured_Beam_Of_Three()
    {
        var (model, results, height) = Measured();
        var rects = ReasoningTreeLayout.Wide(model, results[2], 694, height);
        rects[0].Y.ShouldBe(170);
        // Depth 2 is clamped into one block, so depth 1 is centred again on its children.
        Tops(model, rects, results[2], 1).ShouldBe([61, 185, 309]);
        Tops(model, rects, results[2], 2).ShouldBe([30, 92, 154, 216, 278, 340]);
        // Depth 3 is clamped too, but centring depth 2 again would collide with its neighbours: it stays.
        Tops(model, rects, results[2], 3).ShouldBe([30, 92, 154, 216, 278, 340]);
        var thought = model.Nodes.First(n => n.Depth == 2);
        rects[thought.Index].Width.ShouldBe(156.15, 0.01);
        rects[thought.Index].Height.ShouldBe(54);
    }

    [AvaloniaFact]
    public void A_Wide_Edge_Is_An_S_Curve_From_Parent_To_Child()
    {
        var path = new ExplainerPath();
        ReasoningTreeLayout.WideEdge(path, new Rect(0, 170, 131.86, 84), new Rect(163.09, 123, 156.15, 54));
        path.Start.ShouldBe(new Point(131.86, 212));
        path.End.X.ShouldBe(163.09, 0.001);
        path.End.Y.ShouldBe(150, 0.001);
        path.PointAtFraction(0.5).Y.ShouldBe(181, 0.5);
        var chain = new[] { new Rect(0, 170, 100, 84), new Rect(150, 100, 100, 54), new Rect(300, 60, 100, 54) };
        ReasoningTreeLayout.WidePath(path, chain);
        path.Start.ShouldBe(new Point(100, 212));
        path.End.ShouldBe(new Point(300, 87));
        // It runs under the middle card from its left to its right edge.
        path.Points.ShouldContain(new Point(250, 127));
    }

    [AvaloniaFact]
    public void The_Narrow_Outline_Reserves_A_Row_Per_Shown_Thought()
    {
        var (model, results, _) = Measured();
        results.Select(ReasoningTreeLayout.NarrowHeight).ShouldBe([412, 612, 812]);
        var order = ReasoningTreeLayout.OutlineOrder(model, results[1]);
        order.Select(i => model.Nodes[i].Label).ShouldBe(
            ["Question?", "a", "a1", "a1a", "a1b", "a2", "b", "c", "c1", "c2", "c2a", "c2b"]);
        var row = ReasoningTreeLayout.NarrowRect(model.Nodes[order[3]], 2, 504);
        row.ShouldBe(new Rect(48, 168, 456, 44));
        ReasoningTreeLayout.NarrowRect(model.Root, 0, 504).ShouldBe(new Rect(0, 0, 504, 62));
    }

    [AvaloniaFact]
    public void The_Narrow_Path_Runs_Down_The_Elbows()
    {
        var path = new ExplainerPath();
        var chain = new[] { new Rect(0, 0, 300, 62), new Rect(16, 118, 284, 44), new Rect(32, 168, 268, 44) };
        ReasoningTreeLayout.NarrowPath(path, chain);
        path.Start.ShouldBe(new Point(9, 62));
        path.End.X.ShouldBe(32, 0.001);
        path.End.Y.ShouldBe(190, 0.001);
        path.Points.ShouldContain(new Point(9, 140 - ReasoningTreeLayout.ElbowRadius));
        path.Points.ShouldContain(new Point(25, 140));
    }
}
