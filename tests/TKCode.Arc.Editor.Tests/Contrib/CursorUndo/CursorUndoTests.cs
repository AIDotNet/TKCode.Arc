// Ported from vscode (src/vs/editor/contrib/cursorUndo/test/browser/cursorUndo.test.ts, commit b57d5c9e), MIT license.
// Differences from upstream: the controller is the engine's CursorUndoRedo, fed from the test editor's cursor and
// content events the way the control's controller feeds it (CursorUndoRedoTestController).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Contrib.CursorUndo;
using TKCode.Arc.Editor.ViewModel;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;

namespace TKCode.Arc.Editor.Tests.Contrib;

/// <summary>CursorUndoRedoController on a test editor.</summary>
internal sealed class CursorUndoRedoTestController
{
    private readonly EditingTestEditor _editor;
    private readonly CursorUndoRedo _stacks = new();

    public CursorUndoRedoTestController(EditingTestEditor editor)
    {
        _editor = editor;
        editor.GetModel().DidChangeContent += _ => _stacks.Clear();
        editor.ViewModel.OutgoingEvent += e =>
        {
            if (e is CursorStateChangedEvent cursorEvent)
            {
                _stacks.OnCursorSelectionChanged(cursorEvent, 0, 0);
            }
        };
    }

    public void CursorUndo() => _stacks.CursorUndo(_editor.GetSelections(), 0, 0, element => _editor.SetSelections(element.CursorState.Selections));

    public void CursorRedo() => _stacks.CursorRedo(_editor.GetSelections(), 0, 0, element => _editor.SetSelections(element.CursorState.Selections));
}

public sealed class CursorUndoTests
{
    [Fact]
    public void Issue82535EdgeCaseWithCursorUndo()
    {
        EditingTestEditor.WithTestCodeEditor([""], null, editor =>
        {
            var controller = new CursorUndoRedoTestController(editor);

            // type hello
            editor.Type("hello");

            // press left
            CoreNavigationCommands.CursorLeft.RunEditorCommand(editor, null);

            // press Delete
            CoreEditingCommands.DeleteRight.RunEditorCommand(editor, null);
            AssertDeepStrictEqual(editor.GetValue(), "hell");
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 5, 1, 5)]);

            // press left
            CoreNavigationCommands.CursorLeft.RunEditorCommand(editor, null);
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 4, 1, 4)]);

            // press Ctrl+U
            controller.CursorUndo();
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 5, 1, 5)]);
        });
    }

    [Fact]
    public void Issue82535EdgeCaseWithCursorUndoReverse()
    {
        EditingTestEditor.WithTestCodeEditor([""], null, editor =>
        {
            var controller = new CursorUndoRedoTestController(editor);

            // type hello
            editor.Type("hell");
            editor.Type("o");
            AssertDeepStrictEqual(editor.GetValue(), "hello");
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 6, 1, 6)]);

            // press Ctrl+U
            controller.CursorUndo();
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 6, 1, 6)]);
        });
    }

    // Not upstream: undo then redo walks the stacks both ways, and a cursor move after an undo drops the redo stack.
    [Fact]
    public void CursorUndoThenRedo()
    {
        EditingTestEditor.WithTestCodeEditor(["hello world"], null, editor =>
        {
            var controller = new CursorUndoRedoTestController(editor);
            editor.SetPosition(new TextPosition(1, 3));
            editor.SetSelection(new TextSelection(1, 7, 1, 12));
            editor.SetPosition(new TextPosition(1, 1));

            controller.CursorUndo();
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 7, 1, 12)]);
            controller.CursorUndo();
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 3, 1, 3)]);
            controller.CursorRedo();
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 7, 1, 12)]);
            controller.CursorRedo();
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 1, 1, 1)]);
            controller.CursorRedo();
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 1, 1, 1)]);

            controller.CursorUndo();
            editor.SetPosition(new TextPosition(1, 5));
            controller.CursorRedo();
            AssertDeepStrictEqual(editor.GetSelections(), [new TextSelection(1, 5, 1, 5)]);
        });
    }
}
