using Avalonia;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Core.Tests.Primitives;

public class LayeredGraphLayoutTests
{
    private static readonly Size Step = new(300, 60);
    private static readonly Size Action = new(200, 84);

    private static LayeredGraphEdge E(int source, int target) => new(source, target);

    /// <summary>Arc's HeroRelay workflow: trigger → lookup → branch → three actions.</summary>
    private static (Size[] Sizes, LayeredGraphEdge[] Edges) Relay()
        => ([Step, Step, Step, Action, Action, Action], [E(0, 1), E(1, 2), E(2, 3), E(2, 4), E(2, 5)]);

    [Fact]
    public void Reproduces_Arcs_Workflow_Layout()
    {
        var (sizes, edges) = Relay();
        var layout = LayeredGraphLayout.Arrange(sizes, edges);
        // hero-relay.tsx LAYOUTS.wide: steps at x 170 every 112, actions at 0 / 220 / 440, y 356.
        layout.Positions.ShouldBe([
            new Point(170, 0), new Point(170, 112), new Point(170, 224),
            new Point(0, 356), new Point(220, 356), new Point(440, 356)
        ]);
        layout.Size.ShouldBe(new Size(640, 440));
        layout.Ranks.ShouldBe([0, 1, 2, 3, 3, 3]);
        layout.Orders.ShouldBe([0, 0, 0, 0, 1, 2]);
        layout.RankCount.ShouldBe(4);
        layout.Crossings.ShouldBe(0);
        layout.ReversedEdges.ShouldBeEmpty();
    }

    [Fact]
    public void Left_To_Right_Swaps_The_Axes()
    {
        var sizes = new[] { new Size(120, 40), new Size(120, 40), new Size(120, 40) };
        var layout = LayeredGraphLayout.Arrange(sizes, [E(0, 1), E(0, 2)],
            new LayeredGraphLayoutOptions { Direction = LayeredGraphDirection.LeftToRight });
        // The source fans out: 120 wide + 72; its two targets stack 20 apart and center on it.
        layout.Positions.ShouldBe([new Point(0, 30), new Point(192, 0), new Point(192, 60)]);
        layout.Size.ShouldBe(new Size(312, 100));
    }

    [Fact]
    public void Breaks_Cycles_By_Reversing_Back_Edges()
    {
        // 0 → 1 → 2 → 0 plus 2 → 3: the closing edge is laid out reversed.
        var sizes = Enumerable.Repeat(Step, 4).ToArray();
        var layout = LayeredGraphLayout.Arrange(sizes, [E(0, 1), E(1, 2), E(2, 0), E(2, 3)]);
        layout.ReversedEdges.ShouldBe([2]);
        layout.Ranks.ShouldBe([0, 1, 2, 3]);
    }

    [Fact]
    public void Ignores_Self_Loops_And_Repeated_Edges_And_Rejects_Bad_Indices()
    {
        var sizes = new[] { Step, Step };
        var layout = LayeredGraphLayout.Arrange(sizes, [E(0, 0), E(0, 1), E(0, 1)]);
        layout.Ranks.ShouldBe([0, 1]);
        layout.ReversedEdges.ShouldBeEmpty();
        Should.Throw<ArgumentOutOfRangeException>(() => LayeredGraphLayout.Arrange(sizes, [E(0, 2)]));
    }

    [Fact]
    public void Empty_Graph_And_Isolated_Nodes()
    {
        var empty = LayeredGraphLayout.Arrange([], []);
        empty.Positions.ShouldBeEmpty();
        empty.Size.ShouldBe(default);

        var row = LayeredGraphLayout.Arrange([Action, Action, Action], []);
        row.Positions.ShouldBe([new Point(0, 0), new Point(220, 0), new Point(440, 0)]);
        row.RankCount.ShouldBe(1);
    }

    [Fact]
    public void A_Source_Feeding_A_Deep_Node_Sits_Next_To_It()
    {
        // 0 → 1 → 2 → 3, and 4 → 3: node 4 belongs on rank 2, not on top.
        var sizes = Enumerable.Repeat(Action, 5).ToArray();
        var layout = LayeredGraphLayout.Arrange(sizes, [E(0, 1), E(1, 2), E(2, 3), E(4, 3)]);
        layout.Ranks.ShouldBe([0, 1, 2, 3, 2]);
    }

    [Fact]
    public void Sweeps_Remove_Avoidable_Crossings()
    {
        // Input order crosses: 0 → 3 and 1 → 2 (with both rows in index order the edges form an X).
        var sizes = Enumerable.Repeat(Action, 4).ToArray();
        var layout = LayeredGraphLayout.Arrange(sizes, [E(0, 3), E(1, 2)]);
        layout.Crossings.ShouldBe(0);
        // The order of the lower row follows the upper row.
        (layout.Positions[3].X < layout.Positions[2].X).ShouldBeTrue();
    }

    [Fact]
    public void Long_Edges_Get_Room_And_Keep_A_Rank_Free_Of_Overlap()
    {
        // A diamond with a long bypass: 0 → 1 → 2 → 3 and 0 → 3.
        var sizes = Enumerable.Repeat(Action, 4).ToArray();
        var layout = LayeredGraphLayout.Arrange(sizes, [E(0, 1), E(1, 2), E(2, 3), E(0, 3)]);
        layout.Ranks.ShouldBe([0, 1, 2, 3]);
        layout.Crossings.ShouldBe(0);
        AssertNoOverlap(layout, sizes, 20);
    }

    [Fact]
    public void Random_Graphs_Have_No_Overlaps_And_Are_Deterministic()
    {
        var random = new Random(7);
        for (var round = 0; round < 20; round++)
        {
            var n = random.Next(2, 40);
            var sizes = Enumerable.Range(0, n).Select(_ => new Size(random.Next(80, 320), random.Next(40, 100))).ToArray();
            var edges = Enumerable.Range(0, n * 3 / 2).Select(_ => E(random.Next(n), random.Next(n))).ToArray();
            var options = new LayeredGraphLayoutOptions
            {
                Direction = round % 2 == 0 ? LayeredGraphDirection.TopToBottom : LayeredGraphDirection.LeftToRight
            };
            var layout = LayeredGraphLayout.Arrange(sizes, edges, options);
            LayeredGraphLayout.Arrange(sizes, edges, options).Positions.ShouldBe(layout.Positions);
            AssertNoOverlap(layout, sizes, 20, options.Direction);
            // Every edge points down a rank once cycles are broken; a repeated edge follows its first copy.
            for (var i = 0; i < edges.Length; i++)
            {
                var (s, t) = (edges[i].Source, edges[i].Target);
                if (s == t)
                {
                    continue;
                }
                var first = Array.FindIndex(edges, e => e.Source == s && e.Target == t);
                var (from, to) = layout.ReversedEdges.Contains(first) ? (t, s) : (s, t);
                (layout.Ranks[from] < layout.Ranks[to]).ShouldBeTrue($"round {round}, edge {i} ({s} → {t})");
            }
            layout.Positions.Min(p => p.X).ShouldBe(0, 1e-9);
            layout.Positions.Min(p => p.Y).ShouldBe(0, 1e-9);
        }
    }

    [Fact]
    public void Five_Hundred_Nodes_Lay_Out_Quickly()
    {
        var random = new Random(11);
        const int n = 500;
        var sizes = Enumerable.Repeat(Step, n).ToArray();
        // A layered DAG: each node links to one or two later nodes nearby.
        var edges = new List<LayeredGraphEdge>();
        for (var v = 1; v < n; v++)
        {
            edges.Add(E(random.Next(Math.Max(0, v - 12), v), v));
            if (random.NextDouble() < 0.6)
            {
                edges.Add(E(random.Next(Math.Max(0, v - 30), v), v));
            }
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var layout = LayeredGraphLayout.Arrange(sizes, edges);
        watch.Stop();
        layout.Positions.Count.ShouldBe(n);
        AssertNoOverlap(layout, sizes, 20);
        // Generous bound for loaded CI machines; the target is well under 150ms.
        watch.ElapsedMilliseconds.ShouldBeLessThan(3000);
    }

    private static void AssertNoOverlap(LayeredGraphLayoutResult layout, IReadOnlyList<Size> sizes, double gap,
                                        LayeredGraphDirection direction = LayeredGraphDirection.TopToBottom)
    {
        for (var a = 0; a < sizes.Count; a++)
        {
            for (var b = a + 1; b < sizes.Count; b++)
            {
                if (layout.Ranks[a] != layout.Ranks[b])
                {
                    continue;
                }
                var (startA, endA, startB, endB) = direction == LayeredGraphDirection.TopToBottom
                    ? (layout.Positions[a].X, layout.Positions[a].X + sizes[a].Width, layout.Positions[b].X, layout.Positions[b].X + sizes[b].Width)
                    : (layout.Positions[a].Y, layout.Positions[a].Y + sizes[a].Height, layout.Positions[b].Y, layout.Positions[b].Y + sizes[b].Height);
                var apart = Math.Max(startB - endA, startA - endB);
                apart.ShouldBeGreaterThanOrEqualTo(gap - 1e-6, $"nodes {a} and {b} on rank {layout.Ranks[a]}");
            }
        }
    }
}
