using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One row the graph can show: a commit of the history, or the merge / squash commit a preview adds.</summary>
internal sealed class BranchGraphItem
{
    public required int Index { get; init; }

    /// <summary>The commit (null for the merge and squash commits the previews add).</summary>
    public BranchGraphCommit? Commit { get; init; }

    /// <summary>The commit's position in the history, oldest first (-1 for the added commits).</summary>
    public int CommitIndex { get; init; } = -1;

    public required string Message { get; init; }

    public required string Hash { get; init; }

    /// <summary>The hash after a rebase (pull request commits only).</summary>
    public string? RebasedHash { get; set; }

    public string? Body { get; init; }

    public required string Author { get; init; }

    public IImage? Avatar { get; init; }

    public Color? AuthorColor { get; init; }

    public required string Date { get; init; }

    public int? Additions { get; init; }

    public int? Deletions { get; init; }

    public int? Files { get; init; }

    /// <summary>The branch whose color the commit carries.</summary>
    public required int ColorIndex { get; init; }

    /// <summary>The branch the commit was made on.</summary>
    public required string Branch { get; init; }

    /// <summary>A merge commit: a hollow ring and the merge icon on its row.</summary>
    public bool IsMerge { get; init; }

    /// <summary>One of the pull request's commits (they travel in the rebase and squash previews).</summary>
    public bool IsPullRequest { get; init; }

    public BranchGraphCommitKind Kind { get; init; }
}

/// <summary>
/// A drawn edge: from a parent to a child, its color, and where it bends (<c>MergeIn</c>: the edge joins its child from
/// another lane and bends just below the child; a branch bends near its parent).
/// </summary>
internal readonly record struct BranchGraphEdgeSpec(int Parent, int Child, bool MergeIn, int ColorIndex);

/// <summary>A branch pill on a row.</summary>
internal readonly record struct BranchGraphPill(string Text, int ColorIndex);

/// <summary>What one strategy shows: the row and lane of every item (row -1 = not shown), pills, edges and words.</summary>
internal sealed class BranchGraphLayout
{
    public required BranchGraphStrategy Strategy { get; init; }

    public required int[] Row { get; init; }

    public required int[] Lane { get; init; }

    /// <summary>The hash each item shows.</summary>
    public required string[] Hash { get; init; }

    /// <summary>The branch each item counts as (hovering a commit lights its branch).</summary>
    public required string[] Group { get; init; }

    public required BranchGraphPill[][] Pills { get; init; }

    public required BranchGraphEdgeSpec[] Edges { get; init; }

    public required int RowCount { get; init; }

    /// <summary>The items top to bottom.</summary>
    public required int[] RowItems { get; init; }

    public required string Summary { get; init; }

    public bool Shows(int item) => item >= 0 && item < Row.Length && Row[item] >= 0;
}

/// <summary>
/// The four previews of one history: Before (the history as given, newest on top), Merge (a merge commit on top joins
/// the pull request's branch), Rebase (the pull request's commits replayed on the target's tip with new hashes) and Squash
/// (the pull request folded into one commit). Built once per data set; pure and deterministic.
/// </summary>
internal sealed class BranchGraphModel
{
    private BranchGraphModel(BranchGraphItem[] items, BranchGraphLayout[] layouts, int laneCount, string target, string? source,
        int[] pullRequest, int mergeItem, int squashItem, int ahead, int behind)
    {
        Items = items;
        Layouts = layouts;
        LaneCount = laneCount;
        Target = target;
        Source = source;
        PullRequest = pullRequest;
        MergeItem = mergeItem;
        SquashItem = squashItem;
        Ahead = ahead;
        Behind = behind;
    }

    public BranchGraphItem[] Items { get; }

    /// <summary>The layouts by <see cref="BranchGraphStrategy"/>.</summary>
    public BranchGraphLayout[] Layouts { get; }

    public int LaneCount { get; }

    /// <summary>The branch the pull request lands on.</summary>
    public string Target { get; }

    /// <summary>The pull request's branch (null when the history has none).</summary>
    public string? Source { get; }

    /// <summary>The pull request's items, oldest first.</summary>
    public int[] PullRequest { get; }

    /// <summary>The merge commit the Merge preview adds (-1 without a pull request).</summary>
    public int MergeItem { get; }

    /// <summary>The commit the Squash preview adds (-1 without a pull request).</summary>
    public int SquashItem { get; }

    /// <summary>Commits on the pull request's branch that the target does not have.</summary>
    public int Ahead { get; }

    /// <summary>Commits on the target that the pull request's branch does not have.</summary>
    public int Behind { get; }

    public int CommitCount => Items.Count(i => i.Commit is not null);

    public BranchGraphLayout this[BranchGraphStrategy strategy] => Layouts[(int)strategy];

    public static BranchGraphModel Build(IReadOnlyList<BranchGraphCommit> history, BranchGraphLabels labels, string? title, int number,
        string? sourceBranch, string? targetBranch)
    {
        var plan = BranchGraphPlan.Build(history);
        var nodes = plan.Nodes;
        var n = nodes.Length;
        var target = !string.IsNullOrWhiteSpace(targetBranch) && plan.Branches.Contains(targetBranch.Trim())
            ? targetBranch.Trim()
            : plan.Branches.Length > 0 ? plan.Branches[0] : "main";
        var targetIndex = Math.Max(0, Array.IndexOf(plan.Branches, target));
        var mergedHeads = MergedHeads(plan);
        var source = PickSource(plan, target, sourceBranch, mergedHeads);
        var items = new List<BranchGraphItem>(n + 2);
        for (var i = 0; i < n; i++)
        {
            var node = nodes[i];
            var c = node.Commit;
            items.Add(new BranchGraphItem
            {
                Index = i,
                Commit = c,
                CommitIndex = i,
                Message = string.IsNullOrEmpty(c.Message) ? node.Id : c.Message,
                Hash = string.IsNullOrWhiteSpace(c.Hash) ? node.Id : c.Hash.Trim(),
                Body = string.IsNullOrWhiteSpace(c.Body) ? null : c.Body,
                Author = c.Author ?? string.Empty,
                Avatar = c.AuthorAvatar,
                AuthorColor = c.AuthorColor,
                Date = c.Date ?? string.Empty,
                Additions = c.Additions,
                Deletions = c.Deletions,
                Files = c.Files,
                ColorIndex = node.BranchIndex,
                Branch = node.Branch,
                IsMerge = node.Kind == BranchGraphCommitKind.Merge,
                IsPullRequest = source is not null && node.Branch == source,
                Kind = node.Kind
            });
        }
        var pr = items.Where(i => i.IsPullRequest).Select(i => i.Index).ToArray();
        var sourceIndex = source is null ? -1 : Array.IndexOf(plan.Branches, source);
        var baseTip = plan.HeadOf(target, n);
        var mergeItem = -1;
        var squashItem = -1;
        var ahead = 0;
        var behind = 0;
        if (pr.Length > 0 && baseTip >= 0 && source is not null)
        {
            var sourceTip = pr[^1];
            var tipCommit = items[sourceTip];
            var prTitle = string.IsNullOrWhiteSpace(title) ? source : title;
            var adds = Sum(pr.Select(i => items[i].Additions));
            var dels = Sum(pr.Select(i => items[i].Deletions));
            var files = Sum(pr.Select(i => items[i].Files));
            mergeItem = items.Count;
            items.Add(new BranchGraphItem
            {
                Index = mergeItem,
                Message = labels.MergeMessage(number, source),
                Hash = HashOf("merge", source, items[baseTip].Hash, tipCommit.Hash),
                Author = tipCommit.Author,
                Avatar = tipCommit.Avatar,
                AuthorColor = tipCommit.AuthorColor,
                Date = labels.JustNow,
                Additions = adds,
                Deletions = dels,
                Files = files,
                ColorIndex = targetIndex,
                Branch = target,
                IsMerge = true,
                Kind = BranchGraphCommitKind.Merge
            });
            squashItem = items.Count;
            items.Add(new BranchGraphItem
            {
                Index = squashItem,
                Message = labels.SquashMessage(prTitle, number),
                Hash = HashOf("squash", source, items[baseTip].Hash, tipCommit.Hash),
                Author = tipCommit.Author,
                Avatar = tipCommit.Avatar,
                AuthorColor = tipCommit.AuthorColor,
                Date = labels.JustNow,
                Additions = adds,
                Deletions = dels,
                Files = files,
                ColorIndex = sourceIndex,
                Branch = target,
                Kind = BranchGraphCommitKind.Commit
            });
            foreach (var i in pr)
            {
                items[i].RebasedHash = HashOf("rebase", items[i].Hash, items[baseTip].Hash, string.Empty);
            }
            var tipHistory = plan.AncestorsOf(sourceTip);
            var baseHistory = plan.AncestorsOf(baseTip);
            var branchPoint = nodes[pr[0]].Parents.Length > 0 ? nodes[pr[0]].Parents[0] : -1;
            var pointHistory = branchPoint >= 0 ? plan.AncestorsOf(branchPoint) : new bool[n];
            for (var i = 0; i < n; i++)
            {
                if (tipHistory[i] && !baseHistory[i])
                {
                    ahead++;
                }
                if (baseHistory[i] && !pointHistory[i])
                {
                    behind++;
                }
            }
        }
        var all = items.ToArray();
        var model = new BuildContext(plan, all, target, targetIndex, source, sourceIndex, pr, baseTip, mergeItem, squashItem, mergedHeads, labels, ahead, behind);
        var layouts = new[]
        {
            model.Before(),
            model.Merge(),
            model.Rebase(),
            model.Squash()
        };
        return new BranchGraphModel(all, layouts, Math.Max(1, plan.LaneCount), target, source, pr, mergeItem, squashItem, ahead, behind);
    }

    private static int? Sum(IEnumerable<int?> values)
    {
        int? total = null;
        foreach (var v in values)
        {
            if (v is { } value)
            {
                total = (total ?? 0) + value;
            }
        }
        return total;
    }

    /// <summary>A stable seven-digit hex hash for a commit the preview invents.</summary>
    internal static string HashOf(string kind, string a, string b, string c)
    {
        var hash = 14695981039346656037UL;
        foreach (var part in new[] { kind, a, b, c })
        {
            foreach (var ch in part)
            {
                hash ^= ch;
                hash *= 1099511628211UL;
            }
            hash ^= 0x1F;
            hash *= 1099511628211UL;
        }
        return (hash & 0xFFFFFFF).ToString("x7");
    }

    /// <summary>Branch heads that a merge commit already took in (their branch shows no pill).</summary>
    private static bool[] MergedHeads(BranchGraphPlan plan)
    {
        var merged = new bool[plan.Nodes.Length];
        foreach (var node in plan.Nodes)
        {
            for (var k = 1; k < node.Parents.Length; k++)
            {
                merged[node.Parents[k]] = true;
            }
        }
        return merged;
    }

    private static string? PickSource(BranchGraphPlan plan, string target, string? requested, bool[] mergedHeads)
    {
        if (!string.IsNullOrWhiteSpace(requested) && requested.Trim() != target && plan.Branches.Contains(requested.Trim()))
        {
            return requested.Trim();
        }
        // The branch with the newest head that has not been merged yet.
        string? best = null;
        var bestHead = -1;
        foreach (var branch in plan.Branches)
        {
            if (branch == target)
            {
                continue;
            }
            var head = plan.HeadOf(branch, plan.Nodes.Length);
            if (head > bestHead && !mergedHeads[head])
            {
                best = branch;
                bestHead = head;
            }
        }
        return best;
    }

    private sealed class BuildContext(
        BranchGraphPlan plan,
        BranchGraphItem[] items,
        string target,
        int targetIndex,
        string? source,
        int sourceIndex,
        int[] pr,
        int baseTip,
        int mergeItem,
        int squashItem,
        bool[] mergedHeads,
        BranchGraphLabels labels,
        int ahead,
        int behind)
    {
        private int TargetLane => baseTip >= 0 ? plan.Nodes[baseTip].Lane : 0;

        private bool HasPullRequest => pr.Length > 0 && mergeItem >= 0;

        private int[] HistoryNewestFirst() => Enumerable.Range(0, plan.Nodes.Length).Reverse().ToArray();

        private BranchGraphEdgeSpec[] PlanEdges() => plan.Edges.Select(e =>
        {
            var mergeIn = e.Kind == BranchGraphEdgeKind.MergeIn;
            return new BranchGraphEdgeSpec(e.Parent, e.Child, mergeIn, mergeIn ? items[e.Parent].ColorIndex : items[e.Child].ColorIndex);
        }).ToArray();

        /// <summary>The pills of the history as given: each unmerged branch on its newest commit, then each tag.</summary>
        private List<BranchGraphPill>[] BasePills()
        {
            var pills = new List<BranchGraphPill>[items.Length];
            for (var i = 0; i < pills.Length; i++)
            {
                pills[i] = new List<BranchGraphPill>();
            }
            // The target first, then the other branches in their order.
            var order = plan.Branches.OrderBy(b => b == target ? 0 : 1).ToArray();
            foreach (var branch in order)
            {
                var head = plan.HeadOf(branch, plan.Nodes.Length);
                if (head >= 0 && !mergedHeads[head])
                {
                    pills[head].Add(new BranchGraphPill(branch, Array.IndexOf(plan.Branches, branch)));
                }
            }
            return pills;
        }

        private static void AddTags(List<BranchGraphPill>[] pills, BranchGraphItem[] items)
        {
            foreach (var item in items)
            {
                if (item.Commit?.Tag is { Length: > 0 } tag)
                {
                    pills[item.Index].Add(new BranchGraphPill(tag, item.ColorIndex));
                }
            }
        }

        private static void Move(List<BranchGraphPill>[] pills, string branch, int to, bool first)
        {
            BranchGraphPill? found = null;
            foreach (var list in pills)
            {
                var index = list.FindIndex(p => p.Text == branch);
                if (index >= 0)
                {
                    found = list[index];
                    list.RemoveAt(index);
                }
            }
            if (found is { } pill && to >= 0)
            {
                if (first)
                {
                    pills[to].Insert(0, pill);
                }
                else
                {
                    pills[to].Add(pill);
                }
            }
        }

        private BranchGraphLayout Make(BranchGraphStrategy strategy, IReadOnlyList<int> rows, Func<int, int> lane, Func<int, string> hash,
            Func<int, string> group, List<BranchGraphPill>[] pills, IEnumerable<BranchGraphEdgeSpec> edges, string summary)
        {
            var row = Enumerable.Repeat(-1, items.Length).ToArray();
            for (var r = 0; r < rows.Count; r++)
            {
                row[rows[r]] = r;
            }
            var lanes = new int[items.Length];
            var hashes = new string[items.Length];
            var groups = new string[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                lanes[i] = lane(i);
                hashes[i] = hash(i);
                groups[i] = group(i);
            }
            return new BranchGraphLayout
            {
                Strategy = strategy,
                Row = row,
                Lane = lanes,
                Hash = hashes,
                Group = groups,
                Pills = pills.Select(p => p.ToArray()).ToArray(),
                Edges = edges.Where(e => row[e.Parent] >= 0 && row[e.Child] >= 0).ToArray(),
                RowCount = rows.Count,
                RowItems = rows.ToArray(),
                Summary = summary
            };
        }

        private int PlanLane(int i) => i < plan.Nodes.Length ? plan.Nodes[i].Lane : TargetLane;

        private string BeforeSummary() => HasPullRequest
            ? labels.BeforeSummary(source!, target, ahead, behind)
            : labels.HistorySummary(plan.Nodes.Length, plan.Branches.Length);

        public BranchGraphLayout Before()
        {
            var pills = BasePills();
            AddTags(pills, items);
            return Make(BranchGraphStrategy.Before, HistoryNewestFirst(), PlanLane, i => items[i].Hash, i => items[i].Branch, pills,
                PlanEdges(), BeforeSummary());
        }

        public BranchGraphLayout Merge()
        {
            if (!HasPullRequest)
            {
                return Retag(Before(), BranchGraphStrategy.Merge);
            }
            var pills = BasePills();
            Move(pills, target, mergeItem, first: true);
            AddTags(pills, items);
            var rows = new List<int> { mergeItem };
            rows.AddRange(HistoryNewestFirst());
            var edges = PlanEdges().ToList();
            edges.Add(new BranchGraphEdgeSpec(baseTip, mergeItem, false, targetIndex));
            edges.Add(new BranchGraphEdgeSpec(pr[^1], mergeItem, true, sourceIndex));
            return Make(BranchGraphStrategy.Merge, rows, PlanLane, i => items[i].Hash, i => items[i].Branch, pills, edges,
                labels.MergeSummary(pr.Length, target));
        }

        public BranchGraphLayout Rebase()
        {
            if (!HasPullRequest)
            {
                return Retag(Before(), BranchGraphStrategy.Rebase);
            }
            var isPr = new bool[items.Length];
            foreach (var i in pr)
            {
                isPr[i] = true;
            }
            var pills = BasePills();
            Move(pills, target, pr[^1], first: true);
            Move(pills, source!, pr[^1], first: false);
            AddTags(pills, items);
            var rows = new List<int>();
            rows.AddRange(Enumerable.Reverse(pr));
            rows.AddRange(HistoryNewestFirst().Where(i => !isPr[i]));
            var edges = PlanEdges().Where(e => !(isPr[e.Child] && !isPr[e.Parent])).ToList();
            edges.Add(new BranchGraphEdgeSpec(baseTip, pr[0], false, sourceIndex));
            return Make(BranchGraphStrategy.Rebase, rows, i => isPr[i] ? TargetLane : PlanLane(i),
                i => isPr[i] ? items[i].RebasedHash ?? items[i].Hash : items[i].Hash, i => isPr[i] ? target : items[i].Branch, pills,
                edges, labels.RebaseSummary(pr.Length, target));
        }

        public BranchGraphLayout Squash()
        {
            if (!HasPullRequest)
            {
                return Retag(Before(), BranchGraphStrategy.Squash);
            }
            var isPr = new bool[items.Length];
            foreach (var i in pr)
            {
                isPr[i] = true;
            }
            var pills = BasePills();
            Move(pills, target, squashItem, first: true);
            Move(pills, source!, -1, first: false);
            AddTags(pills, items);
            var rows = new List<int> { squashItem };
            rows.AddRange(HistoryNewestFirst().Where(i => !isPr[i]));
            var edges = PlanEdges().Where(e => !isPr[e.Child] && !isPr[e.Parent]).ToList();
            edges.Add(new BranchGraphEdgeSpec(baseTip, squashItem, false, sourceIndex));
            return Make(BranchGraphStrategy.Squash, rows, PlanLane, i => items[i].Hash, i => items[i].Branch, pills, edges,
                labels.SquashSummary(pr.Length, target));
        }

        private static BranchGraphLayout Retag(BranchGraphLayout layout, BranchGraphStrategy strategy) => new()
        {
            Strategy = strategy,
            Row = layout.Row,
            Lane = layout.Lane,
            Hash = layout.Hash,
            Group = layout.Group,
            Pills = layout.Pills,
            Edges = layout.Edges,
            RowCount = layout.RowCount,
            RowItems = layout.RowItems,
            Summary = layout.Summary
        };
    }
}
