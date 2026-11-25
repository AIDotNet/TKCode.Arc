// Ported from vscode (src/vs/editor/contrib/caretOperations/test/browser/moveCarretCommand.test.ts, commit b57d5c9e),
// MIT license.

using TKCode.Arc.Editor.Contrib.CaretOperations;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed class MoveCaretCommandTests
{
    private static void TestMoveCaretLeftCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommand.Run(lines, null, selection, sel => new MoveCaretCommand(sel, true), expectedLines, expectedSelection);

    private static void TestMoveCaretRightCommand(string[] lines, TextSelection selection, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommand.Run(lines, null, selection, sel => new MoveCaretCommand(sel, false), expectedLines, expectedSelection);

    [Fact]
    public void MoveSelectionToLeft()
    {
        TestMoveCaretLeftCommand(
            [
                "012345"
            ],
            new TextSelection(1, 3, 1, 5),
            [
                "023145"
            ],
            new TextSelection(1, 2, 1, 4)
        );
    }

    [Fact]
    public void MoveSelectionToRight()
    {
        TestMoveCaretRightCommand(
            [
                "012345"
            ],
            new TextSelection(1, 3, 1, 5),
            [
                "014235"
            ],
            new TextSelection(1, 4, 1, 6)
        );
    }

    [Fact]
    public void MoveSelectionToLeftFromFirstColumnNoChange()
    {
        TestMoveCaretLeftCommand(
            [
                "012345"
            ],
            new TextSelection(1, 1, 1, 1),
            [
                "012345"
            ],
            new TextSelection(1, 1, 1, 1)
        );
    }

    [Fact]
    public void MoveSelectionToRightFromLastColumnNoChange()
    {
        TestMoveCaretRightCommand(
            [
                "012345"
            ],
            new TextSelection(1, 5, 1, 7),
            [
                "012345"
            ],
            new TextSelection(1, 5, 1, 7)
        );
    }
}
