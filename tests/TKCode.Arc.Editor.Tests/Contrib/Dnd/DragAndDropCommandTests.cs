// Tests of the port of vscode's DragAndDropCommand (src/vs/editor/contrib/dnd/browser/dragAndDropCommand.ts, commit
// b57d5c9e; MIT license). Upstream has no tests for it; these cover each of its cases through TestCommand.Run.

using TKCode.Arc.Editor.Contrib.Dnd;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class DragAndDropCommandTests
{
    private static readonly string[] s_lines = ["first line", "second line", "third line"];

    private static void Run(TextSelection selection, TextPosition target, bool copy, string[] expectedLines, TextSelection expectedSelection) =>
        TestCommand.Run(s_lines, null, selection, sel => new DragAndDropCommand(sel, target, copy), expectedLines, expectedSelection);

    [Fact]
    public void Moves_A_Word_Down_To_Another_Line() =>
        Run(new TextSelection(1, 1, 1, 6), new TextPosition(3, 7), false,
            [" line", "second line", "third firstline"],
            new TextSelection(3, 7, 3, 12));

    [Fact]
    public void Moves_A_Word_Up_To_Another_Line() =>
        Run(new TextSelection(3, 1, 3, 6), new TextPosition(1, 1), false,
            ["thirdfirst line", "second line", " line"],
            new TextSelection(1, 1, 1, 6));

    [Fact]
    public void Moves_A_Word_To_The_Right_On_Its_Line() =>
        Run(new TextSelection(1, 1, 1, 6), new TextPosition(1, 11), false,
            [" linefirst", "second line", "third line"],
            new TextSelection(1, 6, 1, 11));

    [Fact]
    public void Moves_A_Word_To_The_Left_On_Its_Line() =>
        Run(new TextSelection(1, 7, 1, 11), new TextPosition(1, 1), false,
            ["linefirst ", "second line", "third line"],
            new TextSelection(1, 1, 1, 5));

    [Fact]
    public void Moves_Several_Lines_Down() =>
        Run(new TextSelection(1, 1, 2, 7), new TextPosition(3, 1), false,
            [" line", "first line", "secondthird line"],
            new TextSelection(2, 1, 3, 7));

    [Fact]
    public void Moves_Several_Lines_Up() =>
        Run(new TextSelection(2, 8, 3, 6), new TextPosition(1, 1), false,
            ["line", "thirdfirst line", "second  line"],
            new TextSelection(1, 1, 2, 6));

    [Fact]
    public void Copies_A_Word_Next_To_Itself() =>
        Run(new TextSelection(1, 1, 1, 6), new TextPosition(1, 6), true,
            ["firstfirst line", "second line", "third line"],
            new TextSelection(1, 6, 1, 11));

    [Fact]
    public void Copies_Several_Lines() =>
        Run(new TextSelection(1, 7, 2, 7), new TextPosition(3, 1), true,
            ["first line", "second line", "line", "secondthird line"],
            new TextSelection(3, 1, 4, 7));

    [Fact]
    public void A_Move_Into_The_Selection_Keeps_The_Text_And_The_Selection() =>
        Run(new TextSelection(2, 1, 2, 7), new TextPosition(2, 4), false,
            ["first line", "second line", "third line"],
            new TextSelection(2, 1, 2, 7));
}
