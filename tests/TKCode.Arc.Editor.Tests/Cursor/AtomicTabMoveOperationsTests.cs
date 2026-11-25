// Ported from vscode (src/vs/editor/test/common/controller/cursorAtomicMoveOperations.test.ts, commit b57d5c9e), MIT license.

using Shouldly;
using TKCode.Arc.Editor.Cursor;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class AtomicTabMoveOperationsTests
{
    private sealed record WhitespaceCase(
        string LineContent,
        int TabSize,
        int[] ExpectedPrevTabStopPosition,
        int[] ExpectedPrevTabStopVisibleColumn,
        int[] ExpectedVisibleColumn);

    private sealed record AtomicCase(
        string LineContent,
        int TabSize,
        int[] ExpectedLeft,
        int[] ExpectedRight,
        int[] ExpectedNearest);

    [Fact]
    public void Test_WhitespaceVisibleColumn()
    {
        WhitespaceCase[] testCases =
        [
            new("        ", 4,
                [-1, 0, 0, 0, 0, 4, 4, 4, 4, -1],
                [-1, 0, 0, 0, 0, 4, 4, 4, 4, -1],
                [0, 1, 2, 3, 4, 5, 6, 7, 8, -1]),
            new("  ", 4,
                [-1, 0, 0, -1],
                [-1, 0, 0, -1],
                [0, 1, 2, -1]),
            new("\t", 4,
                [-1, 0, -1],
                [-1, 0, -1],
                [0, 4, -1]),
            new("\t ", 4,
                [-1, 0, 1, -1],
                [-1, 0, 4, -1],
                [0, 4, 5, -1]),
            new(" \t\t ", 4,
                [-1, 0, 0, 2, 3, -1],
                [-1, 0, 0, 4, 8, -1],
                [0, 1, 4, 8, 9, -1]),
            new(" \tA", 4,
                [-1, 0, 0, -1, -1],
                [-1, 0, 0, -1, -1],
                [0, 1, 4, -1, -1]),
            new("A", 4,
                [-1, -1, -1],
                [-1, -1, -1],
                [0, -1, -1]),
            new("", 4,
                [-1, -1],
                [-1, -1],
                [0, -1]),
        ];

        foreach (var testCase in testCases)
        {
            var maxPosition = testCase.ExpectedVisibleColumn.Length;
            for (var position = 0; position < maxPosition; position++)
            {
                var actual = AtomicTabMoveOperations.WhitespaceVisibleColumn(testCase.LineContent, position, testCase.TabSize);
                var expected = (
                    testCase.ExpectedPrevTabStopPosition[position],
                    testCase.ExpectedPrevTabStopVisibleColumn[position],
                    testCase.ExpectedVisibleColumn[position]);
                actual.ShouldBe(expected);
            }
        }
    }

    [Fact]
    public void Test_AtomicPosition()
    {
        AtomicCase[] testCases =
        [
            new("        ", 4,
                [-1, 0, 0, 0, 0, 4, 4, 4, 4, -1],
                [4, 4, 4, 4, 8, 8, 8, 8, -1, -1],
                [0, 0, 0, 4, 4, 4, 4, 8, 8, -1]),
            new(" \t", 4,
                [-1, 0, 0, -1],
                [2, 2, -1, -1],
                [0, 0, 2, -1]),
            new("\t ", 4,
                [-1, 0, -1, -1],
                [1, -1, -1, -1],
                [0, 1, -1, -1]),
            new(" \t ", 4,
                [-1, 0, 0, -1, -1],
                [2, 2, -1, -1, -1],
                [0, 0, 2, -1, -1]),
            new("        A", 4,
                [-1, 0, 0, 0, 0, 4, 4, 4, 4, -1, -1],
                [4, 4, 4, 4, 8, 8, 8, 8, -1, -1, -1],
                [0, 0, 0, 4, 4, 4, 4, 8, 8, -1, -1]),
            new("      foo", 4,
                [-1, 0, 0, 0, 0, -1, -1, -1, -1, -1, -1],
                [4, 4, 4, 4, -1, -1, -1, -1, -1, -1, -1],
                [0, 0, 0, 4, 4, -1, -1, -1, -1, -1, -1]),
        ];

        foreach (var testCase in testCases)
        {
            foreach (var (direction, expected) in new[]
                     {
                         (AtomicTabMoveDirection.Left, testCase.ExpectedLeft),
                         (AtomicTabMoveDirection.Right, testCase.ExpectedRight),
                         (AtomicTabMoveDirection.Nearest, testCase.ExpectedNearest),
                     })
            {
                var actual = expected
                    .Select((_, i) => AtomicTabMoveOperations.AtomicPosition(testCase.LineContent, i, testCase.TabSize, direction))
                    .ToArray();
                actual.ShouldBe(expected);
            }
        }
    }
}
