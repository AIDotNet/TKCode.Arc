// Ported from vscode's src/vs/editor/test/common/model/intervalTree.test.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// The stickiness table for nodeAcceptEdit.

using Shouldly;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class IntervalTreeEditTests
{
    private static void AssertNodeAcceptEdit(
        string message,
        int nodeStart,
        int nodeEnd,
        TrackedRangeStickiness nodeStickiness,
        int start,
        int end,
        int textLength,
        bool forceMoveMarkers,
        int expectedNodeStart,
        int expectedNodeEnd)
    {
        IntervalNode node = new IntervalNode(string.Empty, nodeStart, nodeEnd);
        IntervalTreeNodeMetadata.SetNodeStickiness(node, nodeStickiness);
        IntervalTree.NodeAcceptEdit(node, start, end, textLength, forceMoveMarkers);
        node.Start.ShouldBe(expectedNodeStart, message);
        node.End.ShouldBe(expectedNodeEnd, message);
    }

    [Fact]
    public void NodeAcceptEdit()
    {
        // A. collapsed decoration
        {
            // no-op
            AssertNodeAcceptEdit("A.000", 0, 0, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 0, 0, 0, false, 0, 0);
            AssertNodeAcceptEdit("A.001", 0, 0, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 0, 0, 0, false, 0, 0);
            AssertNodeAcceptEdit("A.002", 0, 0, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 0, 0, 0, false, 0, 0);
            AssertNodeAcceptEdit("A.003", 0, 0, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 0, 0, 0, false, 0, 0);
            AssertNodeAcceptEdit("A.004", 0, 0, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 0, 0, 0, true, 0, 0);
            AssertNodeAcceptEdit("A.005", 0, 0, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 0, 0, 0, true, 0, 0);
            AssertNodeAcceptEdit("A.006", 0, 0, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 0, 0, 0, true, 0, 0);
            AssertNodeAcceptEdit("A.007", 0, 0, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 0, 0, 0, true, 0, 0);
            // insertion
            AssertNodeAcceptEdit("A.008", 0, 0, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 0, 0, 1, false, 0, 1);
            AssertNodeAcceptEdit("A.009", 0, 0, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 0, 0, 1, false, 1, 1);
            AssertNodeAcceptEdit("A.010", 0, 0, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 0, 0, 1, false, 0, 0);
            AssertNodeAcceptEdit("A.011", 0, 0, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 0, 0, 1, false, 1, 1);
            AssertNodeAcceptEdit("A.012", 0, 0, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 0, 0, 1, true, 1, 1);
            AssertNodeAcceptEdit("A.013", 0, 0, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 0, 0, 1, true, 1, 1);
            AssertNodeAcceptEdit("A.014", 0, 0, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 0, 0, 1, true, 1, 1);
            AssertNodeAcceptEdit("A.015", 0, 0, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 0, 0, 1, true, 1, 1);
        }

        // B. non collapsed decoration
        {
            // no-op
            AssertNodeAcceptEdit("B.000", 0, 5, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 0, 0, 0, false, 0, 5);
            AssertNodeAcceptEdit("B.001", 0, 5, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 0, 0, 0, false, 0, 5);
            AssertNodeAcceptEdit("B.002", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 0, 0, 0, false, 0, 5);
            AssertNodeAcceptEdit("B.003", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 0, 0, 0, false, 0, 5);
            AssertNodeAcceptEdit("B.004", 0, 5, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 0, 0, 0, true, 0, 5);
            AssertNodeAcceptEdit("B.005", 0, 5, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 0, 0, 0, true, 0, 5);
            AssertNodeAcceptEdit("B.006", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 0, 0, 0, true, 0, 5);
            AssertNodeAcceptEdit("B.007", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 0, 0, 0, true, 0, 5);
            // insertion at start
            AssertNodeAcceptEdit("B.008", 0, 5, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 0, 0, 1, false, 0, 6);
            AssertNodeAcceptEdit("B.009", 0, 5, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 0, 0, 1, false, 1, 6);
            AssertNodeAcceptEdit("B.010", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 0, 0, 1, false, 0, 6);
            AssertNodeAcceptEdit("B.011", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 0, 0, 1, false, 1, 6);
            AssertNodeAcceptEdit("B.012", 0, 5, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 0, 0, 1, true, 1, 6);
            AssertNodeAcceptEdit("B.013", 0, 5, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 0, 0, 1, true, 1, 6);
            AssertNodeAcceptEdit("B.014", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 0, 0, 1, true, 1, 6);
            AssertNodeAcceptEdit("B.015", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 0, 0, 1, true, 1, 6);
            // insertion in middle
            AssertNodeAcceptEdit("B.016", 0, 5, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 2, 2, 1, false, 0, 6);
            AssertNodeAcceptEdit("B.017", 0, 5, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 2, 2, 1, false, 0, 6);
            AssertNodeAcceptEdit("B.018", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 2, 2, 1, false, 0, 6);
            AssertNodeAcceptEdit("B.019", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 2, 2, 1, false, 0, 6);
            AssertNodeAcceptEdit("B.020", 0, 5, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 2, 2, 1, true, 0, 6);
            AssertNodeAcceptEdit("B.021", 0, 5, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 2, 2, 1, true, 0, 6);
            AssertNodeAcceptEdit("B.022", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 2, 2, 1, true, 0, 6);
            AssertNodeAcceptEdit("B.023", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 2, 2, 1, true, 0, 6);
            // insertion at end
            AssertNodeAcceptEdit("B.024", 0, 5, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 5, 1, false, 0, 6);
            AssertNodeAcceptEdit("B.025", 0, 5, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 5, 1, false, 0, 5);
            AssertNodeAcceptEdit("B.026", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 5, 1, false, 0, 5);
            AssertNodeAcceptEdit("B.027", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 5, 1, false, 0, 6);
            AssertNodeAcceptEdit("B.028", 0, 5, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 5, 1, true, 0, 6);
            AssertNodeAcceptEdit("B.029", 0, 5, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 5, 1, true, 0, 6);
            AssertNodeAcceptEdit("B.030", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 5, 1, true, 0, 6);
            AssertNodeAcceptEdit("B.031", 0, 5, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 5, 1, true, 0, 6);

            // replace with larger text until start
            AssertNodeAcceptEdit("B.032", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 5, 2, false, 5, 11);
            AssertNodeAcceptEdit("B.033", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 5, 2, false, 6, 11);
            AssertNodeAcceptEdit("B.034", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 5, 2, false, 5, 11);
            AssertNodeAcceptEdit("B.035", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 5, 2, false, 6, 11);
            AssertNodeAcceptEdit("B.036", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 5, 2, true, 6, 11);
            AssertNodeAcceptEdit("B.037", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 5, 2, true, 6, 11);
            AssertNodeAcceptEdit("B.038", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 5, 2, true, 6, 11);
            AssertNodeAcceptEdit("B.039", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 5, 2, true, 6, 11);
            // replace with smaller text until start
            AssertNodeAcceptEdit("B.040", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 3, 5, 1, false, 4, 9);
            AssertNodeAcceptEdit("B.041", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 3, 5, 1, false, 4, 9);
            AssertNodeAcceptEdit("B.042", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 3, 5, 1, false, 4, 9);
            AssertNodeAcceptEdit("B.043", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 3, 5, 1, false, 4, 9);
            AssertNodeAcceptEdit("B.044", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 3, 5, 1, true, 4, 9);
            AssertNodeAcceptEdit("B.045", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 3, 5, 1, true, 4, 9);
            AssertNodeAcceptEdit("B.046", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 3, 5, 1, true, 4, 9);
            AssertNodeAcceptEdit("B.047", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 3, 5, 1, true, 4, 9);

            // replace with larger text select start
            AssertNodeAcceptEdit("B.048", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 6, 3, false, 5, 11);
            AssertNodeAcceptEdit("B.049", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 6, 3, false, 5, 11);
            AssertNodeAcceptEdit("B.050", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 6, 3, false, 5, 11);
            AssertNodeAcceptEdit("B.051", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 6, 3, false, 5, 11);
            AssertNodeAcceptEdit("B.052", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 6, 3, true, 7, 11);
            AssertNodeAcceptEdit("B.053", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 6, 3, true, 7, 11);
            AssertNodeAcceptEdit("B.054", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 6, 3, true, 7, 11);
            AssertNodeAcceptEdit("B.055", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 6, 3, true, 7, 11);
            // replace with smaller text select start
            AssertNodeAcceptEdit("B.056", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 6, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.057", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 6, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.058", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 6, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.059", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 6, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.060", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 6, 1, true, 5, 9);
            AssertNodeAcceptEdit("B.061", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 6, 1, true, 5, 9);
            AssertNodeAcceptEdit("B.062", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 6, 1, true, 5, 9);
            AssertNodeAcceptEdit("B.063", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 6, 1, true, 5, 9);

            // replace with larger text from start
            AssertNodeAcceptEdit("B.064", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 6, 2, false, 5, 11);
            AssertNodeAcceptEdit("B.065", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 6, 2, false, 5, 11);
            AssertNodeAcceptEdit("B.066", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 6, 2, false, 5, 11);
            AssertNodeAcceptEdit("B.067", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 6, 2, false, 5, 11);
            AssertNodeAcceptEdit("B.068", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 6, 2, true, 7, 11);
            AssertNodeAcceptEdit("B.069", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 6, 2, true, 7, 11);
            AssertNodeAcceptEdit("B.070", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 6, 2, true, 7, 11);
            AssertNodeAcceptEdit("B.071", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 6, 2, true, 7, 11);
            // replace with smaller text from start
            AssertNodeAcceptEdit("B.072", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 7, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.073", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 7, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.074", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 7, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.075", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 7, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.076", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 7, 1, true, 6, 9);
            AssertNodeAcceptEdit("B.077", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 7, 1, true, 6, 9);
            AssertNodeAcceptEdit("B.078", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 7, 1, true, 6, 9);
            AssertNodeAcceptEdit("B.079", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 7, 1, true, 6, 9);

            // replace with larger text to end
            AssertNodeAcceptEdit("B.080", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 10, 2, false, 5, 11);
            AssertNodeAcceptEdit("B.081", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 10, 2, false, 5, 10);
            AssertNodeAcceptEdit("B.082", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 10, 2, false, 5, 10);
            AssertNodeAcceptEdit("B.083", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 10, 2, false, 5, 11);
            AssertNodeAcceptEdit("B.084", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 10, 2, true, 5, 11);
            AssertNodeAcceptEdit("B.085", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 10, 2, true, 5, 11);
            AssertNodeAcceptEdit("B.086", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 10, 2, true, 5, 11);
            AssertNodeAcceptEdit("B.087", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 10, 2, true, 5, 11);
            // replace with smaller text to end
            AssertNodeAcceptEdit("B.088", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 8, 10, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.089", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 8, 10, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.090", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 8, 10, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.091", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 8, 10, 1, false, 5, 9);
            AssertNodeAcceptEdit("B.092", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 8, 10, 1, true, 5, 9);
            AssertNodeAcceptEdit("B.093", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 8, 10, 1, true, 5, 9);
            AssertNodeAcceptEdit("B.094", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 8, 10, 1, true, 5, 9);
            AssertNodeAcceptEdit("B.095", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 8, 10, 1, true, 5, 9);

            // replace with larger text select end
            AssertNodeAcceptEdit("B.096", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 11, 3, false, 5, 10);
            AssertNodeAcceptEdit("B.097", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 11, 3, false, 5, 10);
            AssertNodeAcceptEdit("B.098", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 11, 3, false, 5, 10);
            AssertNodeAcceptEdit("B.099", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 11, 3, false, 5, 10);
            AssertNodeAcceptEdit("B.100", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 11, 3, true, 5, 12);
            AssertNodeAcceptEdit("B.101", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 11, 3, true, 5, 12);
            AssertNodeAcceptEdit("B.102", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 11, 3, true, 5, 12);
            AssertNodeAcceptEdit("B.103", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 11, 3, true, 5, 12);
            // replace with smaller text select end
            AssertNodeAcceptEdit("B.104", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 11, 1, false, 5, 10);
            AssertNodeAcceptEdit("B.105", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 11, 1, false, 5, 10);
            AssertNodeAcceptEdit("B.106", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 11, 1, false, 5, 10);
            AssertNodeAcceptEdit("B.107", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 11, 1, false, 5, 10);
            AssertNodeAcceptEdit("B.108", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 11, 1, true, 5, 10);
            AssertNodeAcceptEdit("B.109", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 11, 1, true, 5, 10);
            AssertNodeAcceptEdit("B.110", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 11, 1, true, 5, 10);
            AssertNodeAcceptEdit("B.111", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 11, 1, true, 5, 10);

            // replace with larger text from end
            AssertNodeAcceptEdit("B.112", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 10, 11, 3, false, 5, 10);
            AssertNodeAcceptEdit("B.113", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 10, 11, 3, false, 5, 10);
            AssertNodeAcceptEdit("B.114", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 10, 11, 3, false, 5, 10);
            AssertNodeAcceptEdit("B.115", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 10, 11, 3, false, 5, 10);
            AssertNodeAcceptEdit("B.116", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 10, 11, 3, true, 5, 13);
            AssertNodeAcceptEdit("B.117", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 10, 11, 3, true, 5, 13);
            AssertNodeAcceptEdit("B.118", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 10, 11, 3, true, 5, 13);
            AssertNodeAcceptEdit("B.119", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 10, 11, 3, true, 5, 13);
            // replace with smaller text from end
            AssertNodeAcceptEdit("B.120", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 10, 12, 1, false, 5, 10);
            AssertNodeAcceptEdit("B.121", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 10, 12, 1, false, 5, 10);
            AssertNodeAcceptEdit("B.122", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 10, 12, 1, false, 5, 10);
            AssertNodeAcceptEdit("B.123", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 10, 12, 1, false, 5, 10);
            AssertNodeAcceptEdit("B.124", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 10, 12, 1, true, 5, 11);
            AssertNodeAcceptEdit("B.125", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 10, 12, 1, true, 5, 11);
            AssertNodeAcceptEdit("B.126", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 10, 12, 1, true, 5, 11);
            AssertNodeAcceptEdit("B.127", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 10, 12, 1, true, 5, 11);

            // delete until start
            AssertNodeAcceptEdit("B.128", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 5, 0, false, 4, 9);
            AssertNodeAcceptEdit("B.129", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 5, 0, false, 4, 9);
            AssertNodeAcceptEdit("B.130", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 5, 0, false, 4, 9);
            AssertNodeAcceptEdit("B.131", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 5, 0, false, 4, 9);
            AssertNodeAcceptEdit("B.132", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 5, 0, true, 4, 9);
            AssertNodeAcceptEdit("B.133", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 5, 0, true, 4, 9);
            AssertNodeAcceptEdit("B.134", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 5, 0, true, 4, 9);
            AssertNodeAcceptEdit("B.135", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 5, 0, true, 4, 9);

            // delete select start
            AssertNodeAcceptEdit("B.136", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 6, 0, false, 4, 8);
            AssertNodeAcceptEdit("B.137", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 6, 0, false, 4, 8);
            AssertNodeAcceptEdit("B.138", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 6, 0, false, 4, 8);
            AssertNodeAcceptEdit("B.139", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 6, 0, false, 4, 8);
            AssertNodeAcceptEdit("B.140", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 4, 6, 0, true, 4, 8);
            AssertNodeAcceptEdit("B.141", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 4, 6, 0, true, 4, 8);
            AssertNodeAcceptEdit("B.142", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 4, 6, 0, true, 4, 8);
            AssertNodeAcceptEdit("B.143", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 4, 6, 0, true, 4, 8);

            // delete from start
            AssertNodeAcceptEdit("B.144", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 6, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.145", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 6, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.146", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 6, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.147", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 6, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.148", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 6, 0, true, 5, 9);
            AssertNodeAcceptEdit("B.149", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 6, 0, true, 5, 9);
            AssertNodeAcceptEdit("B.150", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 6, 0, true, 5, 9);
            AssertNodeAcceptEdit("B.151", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 6, 0, true, 5, 9);

            // delete to end
            AssertNodeAcceptEdit("B.152", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 10, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.153", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 10, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.154", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 10, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.155", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 10, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.156", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 10, 0, true, 5, 9);
            AssertNodeAcceptEdit("B.157", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 10, 0, true, 5, 9);
            AssertNodeAcceptEdit("B.158", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 10, 0, true, 5, 9);
            AssertNodeAcceptEdit("B.159", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 10, 0, true, 5, 9);

            // delete select end
            AssertNodeAcceptEdit("B.160", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 11, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.161", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 11, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.162", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 11, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.163", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 11, 0, false, 5, 9);
            AssertNodeAcceptEdit("B.164", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 9, 11, 0, true, 5, 9);
            AssertNodeAcceptEdit("B.165", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 9, 11, 0, true, 5, 9);
            AssertNodeAcceptEdit("B.166", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 9, 11, 0, true, 5, 9);
            AssertNodeAcceptEdit("B.167", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 9, 11, 0, true, 5, 9);

            // delete from end
            AssertNodeAcceptEdit("B.168", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 10, 11, 0, false, 5, 10);
            AssertNodeAcceptEdit("B.169", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 10, 11, 0, false, 5, 10);
            AssertNodeAcceptEdit("B.170", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 10, 11, 0, false, 5, 10);
            AssertNodeAcceptEdit("B.171", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 10, 11, 0, false, 5, 10);
            AssertNodeAcceptEdit("B.172", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 10, 11, 0, true, 5, 10);
            AssertNodeAcceptEdit("B.173", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 10, 11, 0, true, 5, 10);
            AssertNodeAcceptEdit("B.174", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 10, 11, 0, true, 5, 10);
            AssertNodeAcceptEdit("B.175", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 10, 11, 0, true, 5, 10);

            // replace with larger text entire
            AssertNodeAcceptEdit("B.176", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 10, 3, false, 5, 8);
            AssertNodeAcceptEdit("B.177", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 10, 3, false, 5, 8);
            AssertNodeAcceptEdit("B.178", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 10, 3, false, 5, 8);
            AssertNodeAcceptEdit("B.179", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 10, 3, false, 5, 8);
            AssertNodeAcceptEdit("B.180", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 10, 3, true, 8, 8);
            AssertNodeAcceptEdit("B.181", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 10, 3, true, 8, 8);
            AssertNodeAcceptEdit("B.182", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 10, 3, true, 8, 8);
            AssertNodeAcceptEdit("B.183", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 10, 3, true, 8, 8);
            // replace with smaller text entire
            AssertNodeAcceptEdit("B.184", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 10, 7, false, 5, 12);
            AssertNodeAcceptEdit("B.185", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 10, 7, false, 5, 10);
            AssertNodeAcceptEdit("B.186", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 10, 7, false, 5, 10);
            AssertNodeAcceptEdit("B.187", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 10, 7, false, 5, 12);
            AssertNodeAcceptEdit("B.188", 5, 10, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges, 5, 10, 7, true, 12, 12);
            AssertNodeAcceptEdit("B.189", 5, 10, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges, 5, 10, 7, true, 12, 12);
            AssertNodeAcceptEdit("B.190", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingBefore, 5, 10, 7, true, 12, 12);
            AssertNodeAcceptEdit("B.191", 5, 10, TrackedRangeStickiness.GrowsOnlyWhenTypingAfter, 5, 10, 7, true, 12, 12);
        }
    }
}
