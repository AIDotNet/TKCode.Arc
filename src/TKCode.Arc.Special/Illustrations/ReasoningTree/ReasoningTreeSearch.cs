namespace TKCode.Arc;

/// <summary>One node of a <see cref="ReasoningTreeModel"/>: the question (index 0) or a thought, with its effective score.</summary>
internal sealed class ReasoningTreeNodeData
{
    public ReasoningTreeNodeData(int index, int parent, int depth, int siblingIndex, string label, string text, double score,
        bool isSeeded, ReasoningThought? thought)
    {
        Index = index;
        Parent = parent;
        Depth = depth;
        SiblingIndex = siblingIndex;
        Label = label;
        Text = text;
        Score = score;
        IsSeeded = isSeeded;
        Thought = thought;
    }

    /// <summary>The node's index in <see cref="ReasoningTreeModel.Nodes"/> (document order, depth first; 0 = question).</summary>
    public int Index { get; }

    /// <summary>The parent's index (-1 for the question).</summary>
    public int Parent { get; }

    /// <summary>0 for the question, 1 to 3 for thoughts.</summary>
    public int Depth { get; }

    /// <summary>The place among the parent's children (0-based).</summary>
    public int SiblingIndex { get; }

    /// <summary>The short text on the node.</summary>
    public string Label { get; }

    /// <summary>The full text (the label when the thought has none).</summary>
    public string Text { get; }

    /// <summary>The score the search uses: the thought's own (clamped to 0..0.99) or the seeded one.</summary>
    public double Score { get; }

    /// <summary>True when the score was seeded because the thought has none.</summary>
    public bool IsSeeded { get; }

    /// <summary>The thought this node was read from (null for the question).</summary>
    public ReasoningThought? Thought { get; }

    /// <summary>The children's indexes in document order.</summary>
    public List<int> Children { get; } = new();

    /// <summary>The score as the integer the node counts to (0 to 99).</summary>
    public int Hundredths => ReasoningTreeSearch.Hundredths(Score);
}

/// <summary>
/// A snapshot of the question and its thoughts, read to depth 3, with every score settled (own or seeded). The search
/// and the layouts are pure functions of it, so a story replays exactly the same way.
/// </summary>
internal sealed class ReasoningTreeModel
{
    /// <summary>The deepest depth the tree reads.</summary>
    public const int MaxDepth = 3;

    private ReasoningTreeModel(List<ReasoningTreeNodeData> nodes, string question, string detail)
    {
        Nodes = nodes;
        Question = question;
        QuestionDetail = detail;
    }

    /// <summary>The nodes in document order (depth first); index 0 is the question.</summary>
    public IReadOnlyList<ReasoningTreeNodeData> Nodes { get; }

    /// <summary>The short question.</summary>
    public string Question { get; }

    /// <summary>The full question.</summary>
    public string QuestionDetail { get; }

    /// <summary>The question node.</summary>
    public ReasoningTreeNodeData Root => Nodes[0];

    /// <summary>The number of thoughts (nodes without the question).</summary>
    public int ThoughtCount => Nodes.Count - 1;

    /// <summary>Reads <paramref name="thoughts"/> (depth 1) and their children to depth 3, seeding missing scores.</summary>
    public static ReasoningTreeModel Build(string question, string? detail, IEnumerable<ReasoningThought> thoughts, int seed)
    {
        var nodes = new List<ReasoningTreeNodeData>
        {
            new(0, -1, 0, 0, question, string.IsNullOrWhiteSpace(detail) ? question : detail, 1, false, null)
        };
        var sibling = 0;
        foreach (var thought in thoughts)
        {
            if (thought is null)
            {
                continue;
            }
            Add(nodes, thought, nodes[0], 1, sibling++, string.Empty, seed);
        }
        return new ReasoningTreeModel(nodes, question, nodes[0].Text);
    }

    private static void Add(List<ReasoningTreeNodeData> nodes, ReasoningThought thought, ReasoningTreeNodeData parent, int depth,
        int sibling, string parentKey, int seed)
    {
        var label = thought.Label ?? string.Empty;
        var text = string.IsNullOrWhiteSpace(thought.Text) ? label : thought.Text!;
        var key = string.IsNullOrEmpty(parentKey) ? thought.Key ?? label : parentKey + "/" + (thought.Key ?? label);
        var own = thought.Score;
        var seeded = own is not { } value || double.IsNaN(value);
        var score = seeded ? ReasoningTreeSearch.SeededScore(seed, key) : ReasoningTreeSearch.ClampScore(own!.Value);
        var node = new ReasoningTreeNodeData(nodes.Count, parent.Index, depth, sibling, label, text, score, seeded, thought);
        nodes.Add(node);
        parent.Children.Add(node.Index);
        if (depth >= MaxDepth)
        {
            return;
        }
        var childSibling = 0;
        foreach (var child in thought.Children)
        {
            if (child is null || ReferenceEquals(child, thought))
            {
                continue;
            }
            Add(nodes, child, node, depth + 1, childSibling++, key, seed);
        }
    }
}

/// <summary>One depth of a search: the candidates it proposed, the ones it kept and the ones it pruned (document order).</summary>
internal sealed class ReasoningTreeDepth
{
    public ReasoningTreeDepth(int depth, int[] candidates, int[] kept, int[] pruned, bool isFinal)
    {
        Depth = depth;
        Candidates = candidates;
        Kept = kept;
        Pruned = pruned;
        IsFinal = isFinal;
    }

    /// <summary>1 to 3.</summary>
    public int Depth { get; }

    /// <summary>The children of the previous depth's survivors, in document order.</summary>
    public int[] Candidates { get; }

    /// <summary>The survivors in document order (on the final depth: the answer alone).</summary>
    public int[] Kept { get; }

    /// <summary>The candidates that did not survive, in document order.</summary>
    public int[] Pruned { get; }

    /// <summary>True for the last depth, which picks the answer instead of keeping a beam.</summary>
    public bool IsFinal { get; }
}

/// <summary>The outcome of a beam search over a <see cref="ReasoningTreeModel"/>.</summary>
internal sealed class ReasoningTreeResult
{
    public ReasoningTreeResult(int beamWidth, IReadOnlyList<ReasoningTreeDepth> depths, int answer, int[] path, bool[] shown)
    {
        BeamWidth = beamWidth;
        Depths = depths;
        Answer = answer;
        Path = path;
        _shown = shown;
    }

    private readonly bool[] _shown;

    /// <summary>The beam width (1 to 3).</summary>
    public int BeamWidth { get; }

    /// <summary>The depths the search ran, first to last (empty for a tree without thoughts).</summary>
    public IReadOnlyList<ReasoningTreeDepth> Depths { get; }

    /// <summary>The answer's node index, or -1 when there is none.</summary>
    public int Answer { get; }

    /// <summary>The path from the first depth to the answer (node indexes; empty without an answer).</summary>
    public int[] Path { get; }

    /// <summary>The last depth the search reached (0 without thoughts).</summary>
    public int FinalDepth => Depths.Count;

    /// <summary>True when the node appears during this search (the question and every candidate).</summary>
    public bool IsShown(int index) => index >= 0 && index < _shown.Length && _shown[index];

    /// <summary>The number of thoughts that appear (without the question).</summary>
    public int ShownThoughtCount
    {
        get
        {
            var count = 0;
            for (var i = 1; i < _shown.Length; i++)
            {
                if (_shown[i])
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>The depth record for <paramref name="depth"/> (1-based), or null.</summary>
    public ReasoningTreeDepth? DepthOf(int depth) => depth >= 1 && depth <= Depths.Count ? Depths[depth - 1] : null;
}

/// <summary>
/// The search behind <see cref="ReasoningTree"/>, as pure functions: plain beam search over at most three depths and the
/// seeded scores of thoughts without one.
/// <list type="bullet">
/// <item>Every depth proposes the children of the previous depth's survivors in document order and keeps the best
/// <c>beamWidth</c>; ties keep the thought that comes first in the document.</item>
/// <item>The last depth keeps only its best thought: the answer. The last depth is depth 3, or an earlier one whose
/// survivors have no children (the search cannot go deeper).</item>
/// <item>A thought without a score gets <c>0.18 + 0.78 · mulberry32(seed XOR fnv1a(pathKey))</c> rounded to two
/// decimals, where the path key joins the <c>Key ?? Label</c> of the thought and its ancestors with "/".</item>
/// </list>
/// </summary>
internal static class ReasoningTreeSearch
{
    /// <summary>The narrowest and widest beams.</summary>
    public const int MinBeamWidth = 1;

    public const int MaxBeamWidth = 3;

    /// <summary>The highest score a thought can show (0.99, so the digits never read 1.00).</summary>
    public const double MaxScore = 0.99;

    /// <summary>Runs the search with <paramref name="beamWidth"/> (clamped to 1..3).</summary>
    public static ReasoningTreeResult Run(ReasoningTreeModel model, int beamWidth)
    {
        beamWidth = Math.Clamp(beamWidth, MinBeamWidth, MaxBeamWidth);
        var nodes = model.Nodes;
        var shown = new bool[nodes.Count];
        shown[0] = true;
        var depths = new List<ReasoningTreeDepth>();
        var frontier = new List<int> { 0 };
        var answer = -1;
        for (var depth = 1; depth <= ReasoningTreeModel.MaxDepth; depth++)
        {
            var candidates = new List<int>();
            foreach (var parent in frontier)
            {
                candidates.AddRange(nodes[parent].Children);
            }
            if (candidates.Count == 0)
            {
                break;
            }
            foreach (var candidate in candidates)
            {
                shown[candidate] = true;
            }
            var best = Top(model, candidates, beamWidth);
            var isFinal = depth == ReasoningTreeModel.MaxDepth || best.All(k => nodes[k].Children.Count == 0);
            int[] kept;
            if (isFinal)
            {
                answer = Top(model, candidates, 1)[0];
                kept = [answer];
            }
            else
            {
                kept = best;
            }
            var pruned = candidates.Where(c => Array.IndexOf(kept, c) < 0).ToArray();
            depths.Add(new ReasoningTreeDepth(depth, candidates.ToArray(), kept, pruned, isFinal));
            if (isFinal)
            {
                break;
            }
            frontier = kept.ToList();
        }
        var path = new List<int>();
        for (var node = answer; node > 0; node = nodes[node].Parent)
        {
            path.Insert(0, node);
        }
        return new ReasoningTreeResult(beamWidth, depths, answer, path.ToArray(), shown);
    }

    /// <summary>
    /// The best <paramref name="count"/> of <paramref name="candidates"/> by score (ties: earlier in the list first),
    /// returned in the order of the list.
    /// </summary>
    public static int[] Top(ReasoningTreeModel model, IReadOnlyList<int> candidates, int count)
    {
        var order = new int[candidates.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        // A stable ranking: higher score first, then the earlier position.
        Array.Sort(order, (a, b) =>
        {
            var byScore = model.Nodes[candidates[b]].Score.CompareTo(model.Nodes[candidates[a]].Score);
            return byScore != 0 ? byScore : a.CompareTo(b);
        });
        var take = Math.Min(Math.Max(count, 0), order.Length);
        var chosen = order.Take(take).ToArray();
        Array.Sort(chosen);
        return chosen.Select(i => candidates[i]).ToArray();
    }

    /// <summary>The best candidate by score (ties: the earlier one), or -1 for an empty list.</summary>
    public static int Best(ReasoningTreeModel model, IReadOnlyList<int> candidates)
    {
        var best = -1;
        foreach (var candidate in candidates)
        {
            if (best < 0 || model.Nodes[candidate].Score > model.Nodes[best].Score)
            {
                best = candidate;
            }
        }
        return best;
    }

    /// <summary>A score clamped to 0..0.99 (NaN reads as 0).</summary>
    public static double ClampScore(double score) => double.IsNaN(score) ? 0 : Math.Clamp(score, 0, MaxScore);

    /// <summary>The score as hundredths, the integer the digits count to.</summary>
    public static int Hundredths(double score) => (int)Math.Round(ClampScore(score) * 100, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The seeded score of a thought without one: <c>0.18 + 0.78 · mulberry32(seed XOR fnv1a(pathKey))</c>, rounded to two
    /// decimals and kept within 0.05..0.99.
    /// </summary>
    public static double SeededScore(int seed, string pathKey)
    {
        var state = unchecked((uint)seed ^ Fnv1A(pathKey));
        var unit = Mulberry32(state);
        var score = Math.Round(0.18 + 0.78 * unit, 2, MidpointRounding.AwayFromZero);
        return Math.Clamp(score, 0.05, MaxScore);
    }

    /// <summary>32-bit FNV-1a over the UTF-16 code units of <paramref name="text"/>.</summary>
    public static uint Fnv1A(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash ^= c;
            hash = unchecked(hash * 16777619u);
        }
        return hash;
    }

    /// <summary>The first output of the mulberry32 generator seeded with <paramref name="state"/>, in [0, 1).</summary>
    public static double Mulberry32(uint state)
    {
        unchecked
        {
            var a = state + 0x6D2B79F5u;
            var t = a;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }
}
