using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Special;

/// <summary>The pure beam search of <see cref="ReasoningTree"/>: survivors, answers, ties, seeded scores and odd shapes.</summary>
public class ReasoningTreeSearchTests
{
    /// <summary>A thought with a score and children (labels are placeholders; only the numbers matter).</summary>
    internal static ReasoningThought T(string label, double? score, params ReasoningThought[] children)
    {
        var thought = new ReasoningThought(label, score);
        thought.Children.AddRange(children);
        return thought;
    }

    /// <summary>
    /// The score structure measured on the reference preview (notes 2b.5): depth 1 [0.82, 0.38, 0.74], depth 2
    /// [0.86, 0.31] | [0.34, 0.49] | [0.66, 0.79], depth 3 under each of those two leaves.
    /// </summary>
    internal static ReasoningThought[] MeasuredShape() =>
    [
        T("a", 0.82,
            T("a1", 0.86, T("a1a", 0.90), T("a1b", 0.71)),
            T("a2", 0.31, T("a2a", 0.27), T("a2b", 0.22))),
        T("b", 0.38,
            T("b1", 0.34, T("b1a", 0.30), T("b1b", 0.41)),
            T("b2", 0.49, T("b2a", 0.52), T("b2b", 0.58))),
        T("c", 0.74,
            T("c1", 0.66, T("c1a", 0.76), T("c1b", 0.81)),
            T("c2", 0.79, T("c2a", 0.94), T("c2b", 0.87))),
    ];

    internal static ReasoningTreeModel Model(IEnumerable<ReasoningThought> thoughts, int seed = 7) =>
        ReasoningTreeModel.Build("Question?", null, thoughts, seed);

    private static string[] Labels(ReasoningTreeModel model, IEnumerable<int> indexes) =>
        indexes.Select(i => model.Nodes[i].Label).ToArray();

    [AvaloniaFact]
    public void The_Snapshot_Reads_Three_Depths_In_Document_Order()
    {
        var model = Model(MeasuredShape());
        model.ThoughtCount.ShouldBe(21);
        model.Root.Depth.ShouldBe(0);
        Labels(model, model.Root.Children).ShouldBe(["a", "b", "c"]);
        model.Nodes[1].Label.ShouldBe("a");
        model.Nodes[2].Label.ShouldBe("a1");
        model.Nodes[3].Label.ShouldBe("a1a");
        model.Nodes.Max(n => n.Depth).ShouldBe(3);
        model.QuestionDetail.ShouldBe("Question?");
    }

    [AvaloniaFact]
    public void A_Greedy_Beam_Settles_For_The_Weaker_Answer()
    {
        var model = Model(MeasuredShape());
        var result = ReasoningTreeSearch.Run(model, 1);
        result.FinalDepth.ShouldBe(3);
        Labels(model, result.Depths[0].Kept).ShouldBe(["a"]);
        result.Depths[0].Candidates.Length.ShouldBe(3);
        Labels(model, result.Depths[1].Candidates).ShouldBe(["a1", "a2"]);
        Labels(model, result.Depths[1].Kept).ShouldBe(["a1"]);
        Labels(model, result.Depths[2].Candidates).ShouldBe(["a1a", "a1b"]);
        result.Depths[2].IsFinal.ShouldBeTrue();
        model.Nodes[result.Answer].Score.ShouldBe(0.90);
        Labels(model, result.Path).ShouldBe(["a", "a1", "a1a"]);
        result.ShownThoughtCount.ShouldBe(7);
    }

    [AvaloniaFact]
    public void A_Beam_Of_Two_Finds_The_Better_Answer()
    {
        var model = Model(MeasuredShape());
        var result = ReasoningTreeSearch.Run(model, 2);
        Labels(model, result.Depths[0].Kept).ShouldBe(["a", "c"]);
        Labels(model, result.Depths[0].Pruned).ShouldBe(["b"]);
        Labels(model, result.Depths[1].Candidates).ShouldBe(["a1", "a2", "c1", "c2"]);
        Labels(model, result.Depths[1].Kept).ShouldBe(["a1", "c2"]);
        Labels(model, result.Depths[2].Candidates).ShouldBe(["a1a", "a1b", "c2a", "c2b"]);
        model.Nodes[result.Answer].Score.ShouldBe(0.94);
        Labels(model, result.Path).ShouldBe(["c", "c2", "c2a"]);
        Labels(model, result.Depths[2].Pruned).ShouldBe(["a1a", "a1b", "c2b"]);
        result.ShownThoughtCount.ShouldBe(11);
    }

    [AvaloniaFact]
    public void A_Beam_Of_Three_Keeps_Every_First_Thought()
    {
        var model = Model(MeasuredShape());
        var result = ReasoningTreeSearch.Run(model, 3);
        Labels(model, result.Depths[0].Kept).ShouldBe(["a", "b", "c"]);
        result.Depths[0].Pruned.ShouldBeEmpty();
        result.Depths[1].Candidates.Length.ShouldBe(6);
        Labels(model, result.Depths[1].Kept).ShouldBe(["a1", "c1", "c2"]);
        Labels(model, result.Depths[2].Candidates).ShouldBe(["a1a", "a1b", "c1a", "c1b", "c2a", "c2b"]);
        model.Nodes[result.Answer].Score.ShouldBe(0.94);
        result.ShownThoughtCount.ShouldBe(15);
    }

    [AvaloniaFact]
    public void The_Sample_Story_Rewards_A_Wider_Beam()
    {
        var model = ReasoningTreeModel.Build(ReasoningTreeSamples.Question, ReasoningTreeSamples.QuestionDetail,
            ReasoningTreeSamples.CreateThoughts(), 7);
        model.ThoughtCount.ShouldBe(21);
        string Answer(int beam) => model.Nodes[ReasoningTreeSearch.Run(model, beam).Answer].Label;
        Answer(1).ShouldBe("Done by Sunday");
        Answer(2).ShouldBe("Small region first");
        Answer(3).ShouldBe("Small region first");
        var two = ReasoningTreeSearch.Run(model, 2);
        Labels(model, two.Depths[0].Kept).ShouldBe(["Copy in batches", "Mirror, then switch"]);
        Labels(model, two.Depths[1].Kept).ShouldBe(["Oldest albums first", "Switch by region"]);
        Labels(model, two.Path).ShouldBe(["Mirror, then switch", "Switch by region", "Small region first"]);
        var three = ReasoningTreeSearch.Run(model, 3);
        Labels(model, three.Depths[1].Kept).ShouldBe(["Oldest albums first", "Dual-write a week", "Switch by region"]);
    }

    [AvaloniaFact]
    public void The_Build_Time_Demo_Finds_A_Different_Answer_For_Each_Beam()
    {
        // The score shape of the gallery's BuildTime demo (its docs promise 0.84, 0.89 and 0.95).
        var model = Model(
        [
            T("cache", 0.78, T("pin", 0.81, T("weekly", 0.84), T("branch", 0.66)), T("warm", 0.52, T("prefetch", 0.48), T("mirror", 0.57))),
            T("split", 0.71, T("duration", 0.77, T("rebalance", 0.89), T("fixed", 0.72)), T("folder", 0.62, T("merge", 0.58), T("big", 0.75))),
            T("changed", 0.66, T("hash", 0.79, T("shared", 0.95), T("local", 0.61)), T("stamps", 0.35, T("touch", 0.30), T("ignore", 0.41))),
        ]);
        new[] { 1, 2, 3 }.Select(b => model.Nodes[ReasoningTreeSearch.Run(model, b).Answer].Score).ShouldBe([0.84, 0.89, 0.95]);
    }

    [AvaloniaFact]
    public void Ties_Keep_The_Thought_That_Comes_First()
    {
        var model = Model([T("x", 0.5), T("y", 0.7), T("z", 0.5), T("w", 0.5)]);
        var result = ReasoningTreeSearch.Run(model, 2);
        result.FinalDepth.ShouldBe(1);
        // No survivor has children, so depth 1 is the last depth and picks one answer.
        result.Depths[0].IsFinal.ShouldBeTrue();
        model.Nodes[result.Answer].Label.ShouldBe("y");
        Labels(model, ReasoningTreeSearch.Top(model, model.Root.Children, 2)).ShouldBe(["x", "y"]);
        Labels(model, ReasoningTreeSearch.Top(model, model.Root.Children, 3)).ShouldBe(["x", "y", "z"]);
    }

    [AvaloniaFact]
    public void Missing_Scores_Are_Seeded_Deterministically()
    {
        ReasoningThought[] Shape() =>
        [
            T("Cache at the edge", null, T("Purge on publish", null), T("Serve stale", null)),
            T("Add a read replica", null, T("Route reports", null)),
            T("Bigger database", null),
        ];
        var first = Model(Shape(), 7);
        var again = Model(Shape(), 7);
        var other = Model(Shape(), 8);
        for (var i = 1; i < first.Nodes.Count; i++)
        {
            first.Nodes[i].IsSeeded.ShouldBeTrue();
            first.Nodes[i].Score.ShouldBe(again.Nodes[i].Score);
            first.Nodes[i].Score.ShouldBeInRange(0.05, 0.99);
            Math.Round(first.Nodes[i].Score, 2).ShouldBe(first.Nodes[i].Score);
        }
        first.Nodes.Skip(1).Select(n => n.Score).ShouldNotBe(other.Nodes.Skip(1).Select(n => n.Score));
        // The formula: 0.18 + 0.78 · mulberry32(seed ^ fnv1a(path key)), two decimals.
        var expected = Math.Round(0.18 + 0.78 * ReasoningTreeSearch.Mulberry32(7u ^ ReasoningTreeSearch.Fnv1A("Cache at the edge/Serve stale")), 2,
            MidpointRounding.AwayFromZero);
        first.Nodes.Single(n => n.Label == "Serve stale").Score.ShouldBe(expected);
        ReasoningTreeSearch.Fnv1A("").ShouldBe(2166136261u);
        ReasoningTreeSearch.Fnv1A("a").ShouldBe(0xE40C292Cu);
    }

    [AvaloniaFact]
    public void A_Key_Replaces_The_Label_In_The_Seed()
    {
        var keyed = Model([new ReasoningThought("Old name") { Key = "k" }]);
        var renamed = Model([new ReasoningThought("New name") { Key = "k" }]);
        keyed.Nodes[1].Score.ShouldBe(renamed.Nodes[1].Score);
    }

    [AvaloniaFact]
    public void Scores_Are_Clamped_And_Counted_In_Hundredths()
    {
        var model = Model([T("hi", 1.4), T("lo", -0.2), T("odd", 0.075)]);
        model.Nodes[1].Score.ShouldBe(0.99);
        model.Nodes[2].Score.ShouldBe(0);
        model.Nodes[3].Hundredths.ShouldBe(8);
        new ReasoningTreeLabels().FormatScore(0.07).ShouldBe("0.07");
        new ReasoningTreeLabels().FormatScore(0.9).ShouldBe("0.90");
    }

    [AvaloniaFact]
    public void Odd_Shapes_End_The_Search_Early()
    {
        var empty = ReasoningTreeSearch.Run(Model([]), 2);
        empty.FinalDepth.ShouldBe(0);
        empty.Answer.ShouldBe(-1);
        empty.Path.ShouldBeEmpty();

        // When no survivor of a depth has children, that depth is the last one and picks the answer.
        var flat = Model([T("a", 0.4, T("a1", 0.9)), T("b", 0.6), T("c", 0.5)]);
        var two = ReasoningTreeSearch.Run(flat, 2);
        two.FinalDepth.ShouldBe(1);
        flat.Nodes[two.Answer].Label.ShouldBe("b");
        var three = ReasoningTreeSearch.Run(flat, 3);
        three.FinalDepth.ShouldBe(2);
        flat.Nodes[three.Answer].Label.ShouldBe("a1");
        three.Path.Length.ShouldBe(2);

        // Thoughts deeper than depth 3 are ignored.
        var deep = Model([T("a", 0.5, T("b", 0.5, T("c", 0.5, T("d", 0.5))))]);
        deep.ThoughtCount.ShouldBe(3);
        ReasoningTreeSearch.Run(deep, 9).BeamWidth.ShouldBe(3);
    }
}
