// Ported from vscode (src/vs/editor/contrib/lineSelection/test/browser/lineSelection.test.ts, commit b57d5c9e), MIT
// license.

using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;
using LineSelectionImpl = TKCode.Arc.Editor.Contrib.LineSelection.LineSelection;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed class LineSelectionTests
{
    [Fact]
    public void ExpandLineSelection()
    {
        var LINE1 = "    \tMy First Line\t ";
        var LINE2 = "\tMy Second Line";
        var LINE3 = "    Third Line🐶";
        var LINE4 = "";
        var LINE5 = "1";

        var TEXT =
            LINE1 + "\r\n" +
            LINE2 + "\n" +
            LINE3 + "\n" +
            LINE4 + "\r\n" +
            LINE5;

        EditingTestEditor.WithTestCodeEditor(TEXT, null, editor =>
        {
            var action = new TestEditorAction(e => LineSelectionImpl.ExpandLineSelection(e, "keyboard"));

            //              0          1         2
            //              01234 56789012345678 0
            // var LINE1 = '    \tMy First Line\t ';
            editor.SetPosition(new TextPosition(1, 1));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 2, 1));

            editor.SetPosition(new TextPosition(1, 2));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 2, 1));

            editor.SetPosition(new TextPosition(1, 5));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 2, 1));

            editor.SetPosition(new TextPosition(1, 19));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 2, 1));

            editor.SetPosition(new TextPosition(1, 20));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 2, 1));

            editor.SetPosition(new TextPosition(1, 21));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 2, 1));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 3, 1));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 4, 1));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 5, 1));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 5, LINE5.Length + 1));
            ExecuteAction(action, editor);
            AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 5, LINE5.Length + 1));
        });
    }
}
