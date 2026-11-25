// Ported from vscode (src/vs/editor/contrib/cursorUndo/browser/cursorUndo.ts, commit b57d5c9e), MIT license:
// CursorUndoRedoController's editor wiring and the CursorUndo / CursorRedo actions.
// Differences from upstream: the stacks are the engine's CursorUndoRedo; the scroll position is the view model's
// (restored without animation, as upstream's setScrollPosition); there is no model swap (the editor's model is fixed).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.CursorUndo;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>Remembers the cursor states so that Ctrl+U goes back to the previous one (upstream's CursorUndoRedoController).</summary>
internal sealed class CursorUndoRedoController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.cursorUndoRedoController";

    // eager because it needs to listen to record cursor state ASAP
    public static readonly EditorContributionDescription Description =
        new(Id, editor => new CursorUndoRedoController(editor), EditorContributionInstantiation.Eager);

    private readonly CodeEditor _editor;
    private readonly CursorUndoRedo _stacks = new();

    private CursorUndoRedoController(CodeEditor editor)
    {
        _editor = editor;
        editor.DidChangeModelContent += OnModelContentChanged;
        editor.DidChangeCursorSelection += OnCursorSelectionChanged;
    }

    public static CursorUndoRedoController? Get(CodeEditor editor) => editor.GetContribution<CursorUndoRedoController>(Id);

    private void OnModelContentChanged(Editor.Model.ModelContentChangedEvent e) => _stacks.Clear();

    private void OnCursorSelectionChanged(CursorStateChangedEvent e)
    {
        var (scrollTop, scrollLeft) = ScrollPosition();
        _stacks.OnCursorSelectionChanged(e, scrollTop, scrollLeft);
    }

    private (double Top, double Left) ScrollPosition() =>
        _editor.ViewModel is { } viewModel
            ? (viewModel.ViewLayout.GetCurrentScrollTop(), viewModel.ViewLayout.GetCurrentScrollLeft())
            : (0, 0);

    /// <summary>upstream's cursorUndo.</summary>
    public void CursorUndo()
    {
        var (scrollTop, scrollLeft) = ScrollPosition();
        _stacks.CursorUndo(_editor.GetSelections(), scrollTop, scrollLeft, ApplyState);
    }

    /// <summary>upstream's cursorRedo.</summary>
    public void CursorRedo()
    {
        var (scrollTop, scrollLeft) = ScrollPosition();
        _stacks.CursorRedo(_editor.GetSelections(), scrollTop, scrollLeft, ApplyState);
    }

    // _applyState
    private void ApplyState(CursorUndoStackElement stackElement)
    {
        _editor.SetSelections(stackElement.CursorState.Selections);
        _editor.ViewModel?.ViewLayout.SetScrollPosition(new NewScrollPosition(stackElement.ScrollLeft, stackElement.ScrollTop), ScrollType.Immediate);
    }

    public void Dispose()
    {
        _editor.DidChangeModelContent -= OnModelContentChanged;
        _editor.DidChangeCursorSelection -= OnCursorSelectionChanged;
    }

    /// <summary>cursorUndo (Ctrl+U).</summary>
    public static readonly EditorAction CursorUndoAction = EditorAction.Create(
        "cursorUndo", "Cursor Undo", null,
        (editor, _) => Get(editor)?.CursorUndo(),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)KeyCode.KeyU) { When = EditorContextKeys.TextInputFocus });

    /// <summary>cursorRedo.</summary>
    public static readonly EditorAction CursorRedoAction = EditorAction.Create(
        "cursorRedo", "Cursor Redo", null,
        (editor, _) => Get(editor)?.CursorRedo());
}
