// Ported from vscode (src/vs/editor/contrib/find/test/browser/find.test.ts, commit b57d5c9e), MIT license.
// All 3 upstream tests of getSelectionSearchString (CommonFindController.GetSelectionSearchString here).

using Shouldly;
using TKCode.Arc.Editor.Contrib.Find;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class FindTests
{
    private static readonly string[] s_lines = ["ABC DEF", "0123 456"];

    [Fact]
    public void Search_string_at_position() => TestFindEditor.With(s_lines, editor =>
    {
        // The cursor is at the very top, of the file, at the first ABC
        CommonFindController.GetSelectionSearchString(editor).ShouldBe("ABC");

        // Move cursor to the end of ABC
        editor.SetPosition(new TextPosition(1, 3));
        CommonFindController.GetSelectionSearchString(editor).ShouldBe("ABC");

        // Move cursor to DEF
        editor.SetPosition(new TextPosition(1, 5));
        CommonFindController.GetSelectionSearchString(editor).ShouldBe("DEF");
    });

    [Fact]
    public void Search_string_with_selection() => TestFindEditor.With(s_lines, editor =>
    {
        // Select A of ABC
        editor.SetSelection(new TextRange(1, 1, 1, 2));
        CommonFindController.GetSelectionSearchString(editor).ShouldBe("A");

        // Select BC of ABC
        editor.SetSelection(new TextRange(1, 2, 1, 4));
        CommonFindController.GetSelectionSearchString(editor).ShouldBe("BC");

        // Select BC DE
        editor.SetSelection(new TextRange(1, 2, 1, 7));
        CommonFindController.GetSelectionSearchString(editor).ShouldBe("BC DE");
    });

    [Fact]
    public void Search_string_with_multiline_selection() => TestFindEditor.With(s_lines, editor =>
    {
        // Select first line and newline
        editor.SetSelection(new TextRange(1, 1, 2, 1));
        CommonFindController.GetSelectionSearchString(editor).ShouldBeNull();

        // Select first line and chunk of second
        editor.SetSelection(new TextRange(1, 1, 2, 4));
        CommonFindController.GetSelectionSearchString(editor).ShouldBeNull();

        // Select end of first line newline and chunk of second
        editor.SetSelection(new TextRange(1, 7, 2, 4));
        CommonFindController.GetSelectionSearchString(editor).ShouldBeNull();
    });
}
