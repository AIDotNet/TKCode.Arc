// Ported from vscode (src/vs/editor/test/browser/controller/cursor.integrationTest.ts, suite 'Editor Controller',
// commit b57d5c9e), MIT license. Upstream types through the view model of a test code editor; here the typing goes
// through TypeOperations on OperationsTestEditor.

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TypeOperationsIntegrationTests
{
    [Fact]
    public void Issue_23913_Greater_Than_1000_Multi_Cursor_Typing_Replacement_Text_Appears_Inverted_Lines_Begin_To_Drop_Off_Selection()
    {
        const int lineCnt = 2000;

        var text = new string[lineCnt];
        for (var i = 0; i < lineCnt; i++)
        {
            text[i] = "asd";
        }

        using var editor = OperationsTestEditor.Create(text);
        var model = editor.Model;

        var selections = new TextSelection[lineCnt];
        for (var i = 0; i < lineCnt; i++)
        {
            selections[i] = new TextSelection(i + 1, 1, i + 1, 1);
        }
        editor.SetSelectionsValidated(selections);

        editor.Type("n", OperationsTestEditor.Keyboard);
        editor.Type("n", OperationsTestEditor.Keyboard);

        for (var i = 0; i < lineCnt; i++)
        {
            model.GetLineContent(i + 1).ShouldBe("nnasd", "line #" + (i + 1));
        }

        editor.GetSelections().Count.ShouldBe(lineCnt);
        editor.GetSelections()[lineCnt - 1].StartLineNumber.ShouldBe(lineCnt);
    }
}
