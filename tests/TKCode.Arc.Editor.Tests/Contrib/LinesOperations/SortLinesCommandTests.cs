// Ported from vscode (src/vs/editor/contrib/linesOperations/test/browser/sortLinesCommand.test.ts, commit b57d5c9e),
// MIT license.

using TKCode.Arc.Editor.Contrib.LinesOperations;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed class SortLinesCommandTests
{
    private static void TestSortLinesAscendingCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommand.Run(lines, null, selection, sel => new SortLinesCommand(sel, false), expectedLines, expectedSelection);

    private static void TestSortLinesDescendingCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommand.Run(lines, null, selection, sel => new SortLinesCommand(sel, true), expectedLines, expectedSelection);

    // suite: Editor Contrib - Sort Lines Command


    [Fact]
    public void NoOpUnlessAtLeastTwoLinesSelected1()
    {
        TestSortLinesAscendingCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 3, 1, 1),
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 3, 1, 1)
        );
    }


    [Fact]
    public void NoOpUnlessAtLeastTwoLinesSelected2()
    {
        TestSortLinesAscendingCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 3, 2, 1),
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 3, 2, 1)
        );
    }


    [Fact]
    public void SortingTwoLinesAscending()
    {
        TestSortLinesAscendingCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(3, 3, 4, 2),
            [
                "first",
                "second line",
                "fourth line",
                "third line",
                "fifth"
            ],
            new TextSelection(3, 3, 4, 1)
        );
    }


    [Fact]
    public void SortingFirst4LinesAscending()
    {
        TestSortLinesAscendingCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 5, 1),
            [
                "first",
                "fourth line",
                "second line",
                "third line",
                "fifth"
            ],
            new TextSelection(1, 1, 5, 1)
        );
    }


    [Fact]
    public void SortingAllLinesAscending()
    {
        TestSortLinesAscendingCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 5, 6),
            [
                "fifth",
                "first",
                "fourth line",
                "second line",
                "third line",
            ],
            new TextSelection(1, 1, 5, 11)
        );
    }


    [Fact]
    public void SortingFirst4LinesDescending()
    {
        TestSortLinesDescendingCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 5, 1),
            [
                "third line",
                "second line",
                "fourth line",
                "first",
                "fifth"
            ],
            new TextSelection(1, 1, 5, 1)
        );
    }


    [Fact]
    public void SortingAllLinesDescending()
    {
        TestSortLinesDescendingCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(1, 1, 5, 6),
            [
                "third line",
                "second line",
                "fourth line",
                "first",
                "fifth",
            ],
            new TextSelection(1, 1, 5, 6)
        );
    }
}
