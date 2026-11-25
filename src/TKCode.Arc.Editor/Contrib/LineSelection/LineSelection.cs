// Ported from vscode (src/vs/editor/contrib/lineSelection/browser/lineSelection.ts, commit b57d5c9e), MIT license: the
// run method of ExpandLineSelectionAction.
// Differences from upstream: the run is a static method on an IEditorActionHost; its `source` argument is a string.

using TKCode.Arc.Editor.Cursor;

namespace TKCode.Arc.Editor.Contrib.LineSelection;

/// <summary>Expand line selection (Ctrl+L).</summary>
internal static class LineSelection
{
    /// <summary>ExpandLineSelectionAction.run.</summary>
    public static void ExpandLineSelection(IEditorActionHost editor, string? source)
    {
        if (editor.GetViewModel() is not { } viewModel)
        {
            return;
        }
        viewModel.Model.PushStackElement();
        viewModel.SetCursorStates(
            source,
            CursorChangeReason.Explicit,
            CursorMoveCommands.ExpandLineSelection(viewModel, viewModel.GetCursorStates()));
        viewModel.RevealAllCursors(source, true);
    }
}
