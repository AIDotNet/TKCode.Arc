// Ported from vscode's src/vs/editor/test/common/model/intervalTree.test.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// The oracle, the operation script and the red-black invariant checker used by the ported tests.

using Shouldly;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tests;

/// <summary>A plain interval as the oracle sees it.</summary>
internal sealed class TestInterval
{
    internal TestInterval(int start, int end)
    {
        Start = start;
        End = end;
    }

    internal int Start { get; set; }
    internal int End { get; set; }

    public override bool Equals(object? obj) =>
        obj is TestInterval other && Start == other.Start && End == other.End;

    public override int GetHashCode() => HashCode.Combine(Start, End);

    public override string ToString() => $"{Start}->{End}";
}

/// <summary>The sorted list of intervals the tree is checked against.</summary>
internal sealed class IntervalOracle
{
    private readonly List<TestInterval> _intervals = new List<TestInterval>();

    internal IReadOnlyList<TestInterval> Intervals => _intervals;

    internal TestInterval Insert(TestInterval interval)
    {
        _intervals.Add(interval);
        _intervals.Sort(static (a, b) =>
        {
            if (a.Start == b.Start)
            {
                return a.End - b.End;
            }
            return a.Start - b.Start;
        });
        return interval;
    }

    internal void Delete(TestInterval interval)
    {
        for (int i = 0, len = _intervals.Count; i < len; i++)
        {
            if (ReferenceEquals(_intervals[i], interval))
            {
                _intervals.RemoveAt(i);
                return;
            }
        }
    }

    internal List<TestInterval> Search(TestInterval interval)
    {
        List<TestInterval> result = new List<TestInterval>();
        for (int i = 0, len = _intervals.Count; i < len; i++)
        {
            TestInterval candidate = _intervals[i];
            if (candidate.Start <= interval.End && candidate.End >= interval.Start)
            {
                result.Add(candidate);
            }
        }
        return result;
    }
}

internal enum TestOperationType
{
    Insert,
    Delete,
    Change,
    Search,
}

/// <summary>One step of a generated tree script.</summary>
internal sealed class TestOperation
{
    private TestOperation(TestOperationType type, int id, int begin, int end)
    {
        Type = type;
        Id = id;
        Begin = begin;
        End = end;
    }

    internal TestOperationType Type { get; }
    internal int Id { get; }
    internal int Begin { get; }
    internal int End { get; }

    internal static TestOperation Insert(int begin, int end) => new TestOperation(TestOperationType.Insert, 0, begin, end);

    internal static TestOperation Delete(int id) => new TestOperation(TestOperationType.Delete, id, 0, 0);

    internal static TestOperation Change(int id, int begin, int end) => new TestOperation(TestOperationType.Change, id, begin, end);

    internal static TestOperation Search(int begin, int end) => new TestOperation(TestOperationType.Search, 0, begin, end);

    public override string ToString()
    {
        switch (Type)
        {
            case TestOperationType.Insert:
                return $"insert {Begin}->{End}";
            case TestOperationType.Delete:
                return $"delete {Id}";
            case TestOperationType.Change:
                return $"change {Id} to {Begin}->{End}";
            default:
                return $"search {Begin}->{End}";
        }
    }
}

/// <summary>Applies a script to a tree and to the oracle after every step.</summary>
internal sealed class IntervalTreeTestState
{
    private readonly IntervalOracle _oracle = new IntervalOracle();
    private readonly IntervalTree _tree = new IntervalTree();
    private readonly List<IntervalNode?> _treeNodes = new List<IntervalNode?>();
    private readonly List<TestInterval?> _oracleNodes = new List<TestInterval?>();
    private int _lastNodeId = -1;

    internal void AcceptOp(TestOperation op)
    {
        switch (op.Type)
        {
            case TestOperationType.Insert:
            {
                int nodeId = ++_lastNodeId;
                IntervalNode node = new IntervalNode(null, op.Begin, op.End);
                _treeNodes.Add(node);
                _tree.Insert(node);
                _oracleNodes.Add(_oracle.Insert(new TestInterval(op.Begin, op.End)));
                break;
            }
            case TestOperationType.Delete:
            {
                _tree.Delete(_treeNodes[op.Id]!);
                _oracle.Delete(_oracleNodes[op.Id]!);

                _treeNodes[op.Id] = null;
                _oracleNodes[op.Id] = null;
                break;
            }
            case TestOperationType.Change:
            {
                _tree.Delete(_treeNodes[op.Id]!);
                _treeNodes[op.Id]!.Reset(0, op.Begin, op.End, null);
                _tree.Insert(_treeNodes[op.Id]!);

                _oracle.Delete(_oracleNodes[op.Id]!);
                _oracleNodes[op.Id]!.Start = op.Begin;
                _oracleNodes[op.Id]!.End = op.End;
                _oracle.Insert(_oracleNodes[op.Id]!);
                break;
            }
            default:
            {
                List<IntervalNode> actualNodes = _tree.IntervalSearch(op.Begin, op.End, 0, false, false, 0, false);
                List<TestInterval> actual = new List<TestInterval>(actualNodes.Count);
                for (int i = 0, len = actualNodes.Count; i < len; i++)
                {
                    actual.Add(new TestInterval(actualNodes[i].CachedAbsoluteStart, actualNodes[i].CachedAbsoluteEnd));
                }
                List<TestInterval> expected = _oracle.Search(new TestInterval(op.Begin, op.End));
                actual.ShouldBe(expected);
                return;
            }
        }

        IntervalTreeAssert.AssertTreeInvariants(_tree);

        List<IntervalNode> ordered = _tree.GetAllInOrder();
        List<TestInterval> actualOrdered = new List<TestInterval>(ordered.Count);
        for (int i = 0, len = ordered.Count; i < len; i++)
        {
            actualOrdered.Add(new TestInterval(ordered[i].CachedAbsoluteStart, ordered[i].CachedAbsoluteEnd));
        }
        actualOrdered.ShouldBe(_oracle.Intervals);
    }

    internal int GetExistingNodeId(int index)
    {
        int currIndex = -1;
        for (int i = 0; i < _treeNodes.Count; i++)
        {
            if (_treeNodes[i] is null)
            {
                continue;
            }
            currIndex++;
            if (currIndex == index)
            {
                return i;
            }
        }
        throw new InvalidOperationException("unexpected");
    }
}

internal static class IntervalTreeTestRunner
{
    internal static void TestIntervalTree(IReadOnlyList<TestOperation> ops)
    {
        IntervalTreeTestState state = new IntervalTreeTestState();
        for (int i = 0, len = ops.Count; i < len; i++)
        {
            state.AcceptOp(ops[i]);
        }
    }
}

/// <summary>
/// Checks the red-black and augmented-interval invariants the upstream test suite asserts.
/// </summary>
internal static class IntervalTreeAssert
{
    internal static void AssertTreeInvariants(IntervalTree T)
    {
        IntervalNode sentinel = T.Sentinel;
        IntervalTreeNodeMetadata.GetNodeColor(sentinel).ShouldBe(IntervalNodeColor.Black);
        ReferenceEquals(sentinel.Parent, sentinel).ShouldBeTrue();
        ReferenceEquals(sentinel.Left, sentinel).ShouldBeTrue();
        ReferenceEquals(sentinel.Right, sentinel).ShouldBeTrue();
        sentinel.Start.ShouldBe(0);
        sentinel.End.ShouldBe(0);
        sentinel.Delta.ShouldBe(0);
        ReferenceEquals(T.Root.Parent, sentinel).ShouldBeTrue();
        AssertValidTree(T);
    }

    private static int Depth(IntervalTree T, IntervalNode n)
    {
        if (ReferenceEquals(n, T.Sentinel))
        {
            // The leafs are black
            return 1;
        }
        Depth(T, n.Left).ShouldBe(Depth(T, n.Right));
        return (IntervalTreeNodeMetadata.GetNodeColor(n) == IntervalNodeColor.Black ? 1 : 0) + Depth(T, n.Left);
    }

    private static void AssertValidNode(IntervalTree T, IntervalNode n, int delta)
    {
        if (ReferenceEquals(n, T.Sentinel))
        {
            return;
        }

        IntervalNode l = n.Left;
        IntervalNode r = n.Right;

        if (IntervalTreeNodeMetadata.GetNodeColor(n) == IntervalNodeColor.Red)
        {
            IntervalTreeNodeMetadata.GetNodeColor(l).ShouldBe(IntervalNodeColor.Black);
            IntervalTreeNodeMetadata.GetNodeColor(r).ShouldBe(IntervalNodeColor.Black);
        }

        int expectedMaxEnd = n.End;
        if (!ReferenceEquals(l, T.Sentinel))
        {
            IntervalTree.IntervalCompare(l.Start + delta, l.End + delta, n.Start + delta, n.End + delta).ShouldBeLessThanOrEqualTo(0);
            expectedMaxEnd = Math.Max(expectedMaxEnd, l.MaxEnd);
        }
        if (!ReferenceEquals(r, T.Sentinel))
        {
            IntervalTree.IntervalCompare(n.Start + delta, n.End + delta, r.Start + delta + n.Delta, r.End + delta + n.Delta).ShouldBeLessThanOrEqualTo(0);
            expectedMaxEnd = Math.Max(expectedMaxEnd, r.MaxEnd + n.Delta);
        }
        n.MaxEnd.ShouldBe(expectedMaxEnd);

        AssertValidNode(T, l, delta);
        AssertValidNode(T, r, delta + n.Delta);
    }

    private static void AssertValidTree(IntervalTree T)
    {
        if (ReferenceEquals(T.Root, T.Sentinel))
        {
            return;
        }
        IntervalTreeNodeMetadata.GetNodeColor(T.Root).ShouldBe(IntervalNodeColor.Black);
        Depth(T, T.Root.Left).ShouldBe(Depth(T, T.Root.Right));
        AssertValidNode(T, T.Root, 0);
    }
}
