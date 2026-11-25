// Ported from vscode (src/vs/editor/contrib/linesOperations/test/browser/copyLinesCommand.test.ts, commit b57d5c9e),
// MIT license. Differences from upstream: DuplicateSelectionAction is LinesOperations.DuplicateSelection.

using TKCode.Arc.Editor.Contrib.LinesOperations;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;
using static TKCode.Arc.Editor.Tests.Contrib.EditingTestEditor;
using LinesOperationsImpl = TKCode.Arc.Editor.Contrib.LinesOperations.LinesOperations;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed class CopyLinesCommandTests
{
    private static void TestCopyLinesDownCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommand.Run(lines, null, selection, sel => new CopyLinesCommand(sel, true), expectedLines, expectedSelection);

    private static void TestCopyLinesUpCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommand.Run(lines, null, selection, sel => new CopyLinesCommand(sel, false), expectedLines, expectedSelection);

    // suite: Editor Contrib - Copy Lines Command


    [Fact]
    public void CopyFirstLineDown()
    {
        TestCopyLinesDownCommand(
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
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 3, 2, 1)
        );
    }


    [Fact]
    public void CopyFirstLineUp()
    {
        TestCopyLinesUpCommand(
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
    public void CopyLastLineDown()
    {
        TestCopyLinesDownCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 3, 5, 1),
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth",
                "fifth"
            ],
            new TextSelection(6, 3, 6, 1)
        );
    }


    [Fact]
    public void CopyLastLineUp()
    {
        TestCopyLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 3, 5, 1),
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth",
                "fifth"
            ],
            new TextSelection(5, 3, 5, 1)
        );
    }


    [Fact]
    public void Issue1322CopyLineUp()
    {
        TestCopyLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(3, 11, 3, 11),
            [
                "first",
                "second line",
                "third line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(3, 11, 3, 11)
        );
    }


    [Fact]
    public void Issue1322CopyLastLineUp()
    {
        TestCopyLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(5, 6, 5, 6),
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth",
                "fifth"
            ],
            new TextSelection(5, 6, 5, 6)
        );
    }


    [Fact]
    public void CopyManyLinesUp()
    {
        TestCopyLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(4, 3, 2, 1),
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(4, 3, 2, 1)
        );
    }


    [Fact]
    public void IgnoreEmptySelection()
    {
        TestCopyLinesUpCommand(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 1, 1),
            [
                "first",
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            new TextSelection(2, 1, 1, 1)
        );
    }


    // suite: Editor Contrib - Duplicate Selection


    private static void TestDuplicateSelectionAction(string[] lines, TextSelection[] selections, string[] expectedLines, TextSelection[] expectedSelections)
    {
        WithTestCodeEditor(lines, null, editor =>
        {
            editor.SetSelections(selections);
            LinesOperationsImpl.DuplicateSelection(editor, "editor.action.duplicateSelection");
            AssertDeepStrictEqual(editor.GetValue(), string.Join("\n", expectedLines));
            AssertDeepStrictEqual(editor.GetSelections().Select(s => s.ToString()).ToList(), expectedSelections.Select(s => s.ToString()).ToArray());
        });
    }

    [Fact]
    public void EmptySelection()
    {
        TestDuplicateSelectionAction(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            [new TextSelection(2, 2, 2, 2), new TextSelection(3, 2, 3, 2)],
            [
                "first",
                "second line",
                "second line",
                "third line",
                "third line",
                "fourth line",
                "fifth"
            ],
            [new TextSelection(3, 2, 3, 2), new TextSelection(5, 2, 5, 2)]
        );
    }


    [Fact]
    public void WithSelection()
    {
        TestDuplicateSelectionAction(
            [
                "first",
                "second line",
                "third line",
                "fourth line",
                "fifth"
            ],
            [new TextSelection(2, 1, 2, 4), new TextSelection(3, 1, 3, 4)],
            [
                "first",
                "secsecond line",
                "thithird line",
                "fourth line",
                "fifth"
            ],
            [new TextSelection(2, 4, 2, 7), new TextSelection(3, 4, 3, 7)]
        );
    }
}
