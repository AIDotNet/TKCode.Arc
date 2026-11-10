namespace TKCode.Arc;

/// <summary>How an edge leaves its parent: straight down its lane, out to a new branch, or into a merge.</summary>
internal enum BranchGraphEdgeKind
{
    /// <summary>Parent and child share a lane.</summary>
    Straight,

    /// <summary>The child starts a branch: the edge leaves the parent's row and turns into the child's lane.</summary>
    BranchOff,

    /// <summary>The child joins another lane: the edge runs down the parent's lane and turns in just before the child.</summary>
    MergeIn
}

/// <summary>One commit as planned: its lane, its parents and what it does.</summary>
internal sealed class BranchGraphNode
{
    public required BranchGraphCommit Commit { get; init; }

    public required int Index { get; init; }

    public required string Id { get; init; }

    public required string Branch { get; init; }

    public int Lane { get; set; }

    /// <summary>The branch's position among the branches (its color).</summary>
    public int BranchIndex { get; set; }

    public int[] Parents { get; set; } = [];

    public BranchGraphCommitKind Kind { get; set; }

    /// <summary>For a branch's first commit, the branch it left; for a merge, the branch merged in.</summary>
    public string? Other { get; set; }
}

/// <summary>An edge from a parent to a child.</summary>
internal readonly record struct BranchGraphEdge(int Parent, int Child, BranchGraphEdgeKind Kind);

/// <summary>
/// Lays out a history: every branch gets a lane when its first commit appears (main, the first branch, keeps lane 0; a new
/// branch takes the lowest free lane) and gives it back once it has been merged and has no later commits, so short-lived
/// branches reuse lanes. Parents default to the previous commit on the same branch, or the previous commit overall for a
/// branch's first commit. Pure and deterministic.
/// </summary>
internal sealed class BranchGraphPlan
{
    private BranchGraphPlan(BranchGraphNode[] nodes, BranchGraphEdge[] edges, int lanes, string[] branches, int merges)
    {
        Nodes = nodes;
        Edges = edges;
        LaneCount = lanes;
        Branches = branches;
        MergeCount = merges;
    }

    public BranchGraphNode[] Nodes { get; }

    public BranchGraphEdge[] Edges { get; }

    public int LaneCount { get; }

    /// <summary>The branches in the order they first appear.</summary>
    public string[] Branches { get; }

    public int MergeCount { get; }

    public static BranchGraphPlan Empty { get; } = new([], [], 1, [], 0);

    public static BranchGraphPlan Build(IReadOnlyList<BranchGraphCommit> commits)
    {
        var nodes = new BranchGraphNode[commits.Count];
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        var lastOnBranch = new Dictionary<string, int>(StringComparer.Ordinal);
        var branchOrder = new List<string>();
        for (var i = 0; i < commits.Count; i++)
        {
            var commit = commits[i];
            var id = string.IsNullOrWhiteSpace(commit.Id) ? "#" + (i + 1) : commit.Id.Trim();
            var branch = string.IsNullOrWhiteSpace(commit.Branch) ? "main" : commit.Branch.Trim();
            var node = new BranchGraphNode { Commit = commit, Index = i, Id = id, Branch = branch };
            var parents = new List<int>();
            foreach (var part in (commit.Parents ?? string.Empty).Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (byId.TryGetValue(part, out var p) && !parents.Contains(p))
                {
                    parents.Add(p);
                }
            }
            if (parents.Count == 0)
            {
                if (lastOnBranch.TryGetValue(branch, out var previous))
                {
                    parents.Add(previous);
                }
                else if (i > 0)
                {
                    parents.Add(i - 1);
                }
            }
            node.Parents = parents.Take(2).ToArray();
            if (!branchOrder.Contains(branch))
            {
                branchOrder.Add(branch);
            }
            node.BranchIndex = branchOrder.IndexOf(branch);
            var isFirstOnBranch = !lastOnBranch.ContainsKey(branch);
            if (node.Parents.Length == 2)
            {
                node.Kind = BranchGraphCommitKind.Merge;
            }
            else if (isFirstOnBranch && node.Parents.Length == 1)
            {
                node.Kind = BranchGraphCommitKind.Branch;
            }
            else
            {
                node.Kind = BranchGraphCommitKind.Commit;
            }
            nodes[i] = node;
            byId.TryAdd(id, i);
            lastOnBranch[branch] = i;
        }
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node.Kind == BranchGraphCommitKind.Branch)
            {
                node.Other = nodes[node.Parents[0]].Branch;
                if (node.Other == node.Branch)
                {
                    node.Kind = BranchGraphCommitKind.Commit;
                    node.Other = null;
                }
            }
            else if (node.Kind == BranchGraphCommitKind.Merge)
            {
                node.Other = nodes[node.Parents[1]].Branch;
            }
        }
        // Lanes: a branch takes one at its first commit and frees it after the merge that ends it.
        var lanes = new Dictionary<string, int>(StringComparer.Ordinal);
        var busy = new List<bool>();
        var laneCount = 1;
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (!lanes.TryGetValue(node.Branch, out var lane))
            {
                lane = lanes.Count == 0 ? 0 : busy.FindIndex(1, b => !b) is var free && free >= 1 ? free : Math.Max(1, busy.Count);
                while (busy.Count <= lane)
                {
                    busy.Add(false);
                }
                busy[lane] = true;
                lanes[node.Branch] = lane;
                laneCount = Math.Max(laneCount, lane + 1);
            }
            node.Lane = lane;
            if (node.Kind == BranchGraphCommitKind.Merge)
            {
                var merged = nodes[node.Parents[1]].Branch;
                if (merged != node.Branch && lastOnBranch[merged] < i && lanes.TryGetValue(merged, out var mergedLane) && mergedLane != 0)
                {
                    busy[mergedLane] = false;
                    lanes.Remove(merged);
                }
            }
        }
        var edges = new List<BranchGraphEdge>();
        foreach (var node in nodes)
        {
            for (var k = 0; k < node.Parents.Length; k++)
            {
                var parent = nodes[node.Parents[k]];
                var kind = parent.Lane == node.Lane
                    ? BranchGraphEdgeKind.Straight
                    : k == 0 && node.Kind == BranchGraphCommitKind.Branch ? BranchGraphEdgeKind.BranchOff : BranchGraphEdgeKind.MergeIn;
                edges.Add(new BranchGraphEdge(parent.Index, node.Index, kind));
            }
        }
        var merges = nodes.Count(n => n.Kind == BranchGraphCommitKind.Merge);
        return new BranchGraphPlan(nodes, edges.ToArray(), laneCount, branchOrder.ToArray(), merges);
    }

    /// <summary>The commit at <paramref name="index"/> and every commit it descends from.</summary>
    public bool[] AncestorsOf(int index)
    {
        var seen = new bool[Nodes.Length];
        if (index < 0 || index >= Nodes.Length)
        {
            return seen;
        }
        var stack = new Stack<int>();
        stack.Push(index);
        while (stack.Count > 0)
        {
            var i = stack.Pop();
            if (seen[i])
            {
                continue;
            }
            seen[i] = true;
            foreach (var p in Nodes[i].Parents)
            {
                stack.Push(p);
            }
        }
        return seen;
    }

    /// <summary>The newest commit of each branch among the first <paramref name="revealed"/> commits (the branch labels).</summary>
    public int HeadOf(string branch, int revealed)
    {
        for (var i = Math.Min(revealed, Nodes.Length) - 1; i >= 0; i--)
        {
            if (Nodes[i].Branch == branch)
            {
                return i;
            }
        }
        return -1;
    }
}
