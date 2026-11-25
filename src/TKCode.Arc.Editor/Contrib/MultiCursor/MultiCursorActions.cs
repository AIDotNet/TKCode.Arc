// Ported from vscode (src/vs/editor/contrib/multicursor/browser/multicursor.ts: the run methods of InsertCursorAbove,
// InsertCursorBelow, InsertCursorAtEndOfEachLineSelected, InsertCursorAtEndOfLineSelected (add cursors to bottom),
// InsertCursorAtTopOfLineSelected (add cursors to top), FocusNextCursor, FocusPreviousCursor and
// MultiCursorSelectionControllerAction; commit b57d5c9e), MIT license.
// Differences: the actions' logic is static methods on IMultiCursorEditor (the control's editor actions call them,
// with their ids, labels and keybindings); no aria announcements; MultiCursorSelectionControllerAction takes the find
// controller of the editor, or a temporary state-only one as upstream creates a temporary CommonFindController.

using TKCode.Arc.Editor.Cursor;

namespace TKCode.Arc.Editor.Contrib.MultiCursor;

/// <summary>The multi-cursor actions' logic (upstream's EditorAction run methods in multicursor.ts).</summary>
internal static class MultiCursorActions
{
    public const string InsertCursorAboveId = "editor.action.insertCursorAbove";
    public const string InsertCursorBelowId = "editor.action.insertCursorBelow";
    public const string InsertCursorAtEndOfEachLineSelectedId = "editor.action.insertCursorAtEndOfEachLineSelected";
    public const string AddCursorsToBottomId = "editor.action.addCursorsToBottom";
    public const string AddCursorsToTopId = "editor.action.addCursorsToTop";
    public const string AddSelectionToNextFindMatchId = "editor.action.addSelectionToNextFindMatch";
    public const string AddSelectionToPreviousFindMatchId = "editor.action.addSelectionToPreviousFindMatch";
    public const string MoveSelectionToNextFindMatchId = "editor.action.moveSelectionToNextFindMatch";
    public const string MoveSelectionToPreviousFindMatchId = "editor.action.moveSelectionToPreviousFindMatch";
    public const string SelectHighlightsId = "editor.action.selectHighlights";
    public const string ChangeAllId = "editor.action.changeAll";
    public const string FocusNextCursorId = "editor.action.focusNextCursor";
    public const string FocusPreviousCursorId = "editor.action.focusPreviousCursor";

    /// <summary>InsertCursorAbove.run: a cursor on the line above each cursor (logical lines unless <paramref name="logicalLine"/> is false).</summary>
    public static void InsertCursorAbove(IMultiCursorEditor editor, string? source = null, bool logicalLine = true)
    {
        if (editor.Model is not { } model || editor.ViewModel is not { } viewModel || viewModel.CursorConfig.ReadOnly)
        {
            return;
        }

        model.PushStackElement();
        var previousCursorState = viewModel.GetCursorStates();
        viewModel.SetCursorStates(source, CursorChangeReason.Explicit, CursorMoveCommands.AddCursorUp(viewModel, previousCursorState, logicalLine));
        viewModel.RevealTopMostCursor(source);
    }

    /// <summary>InsertCursorBelow.run.</summary>
    public static void InsertCursorBelow(IMultiCursorEditor editor, string? source = null, bool logicalLine = true)
    {
        if (editor.Model is not { } model || editor.ViewModel is not { } viewModel || viewModel.CursorConfig.ReadOnly)
        {
            return;
        }

        model.PushStackElement();
        var previousCursorState = viewModel.GetCursorStates();
        viewModel.SetCursorStates(source, CursorChangeReason.Explicit, CursorMoveCommands.AddCursorDown(viewModel, previousCursorState, logicalLine));
        viewModel.RevealBottomMostCursor(source);
    }

    /// <summary>InsertCursorAtEndOfEachLineSelected.run: a cursor at the end of every line a selection covers.</summary>
    public static void InsertCursorAtEndOfEachLineSelected(IMultiCursorEditor editor)
    {
        if (editor.Model is not { } model)
        {
            return;
        }

        var newSelections = new List<TextSelection>();
        foreach (var selection in editor.GetSelections())
        {
            GetCursorsForSelection(selection, model, newSelections);
        }

        if (newSelections.Count > 0)
        {
            editor.SetSelections(newSelections);
        }
    }

    private static void GetCursorsForSelection(TextSelection selection, Model.TextModel model, List<TextSelection> result)
    {
        if (selection.IsEmpty)
        {
            return;
        }

        for (var i = selection.StartLineNumber; i < selection.EndLineNumber; i++)
        {
            var currentLineMaxColumn = model.GetLineMaxColumn(i);
            result.Add(new TextSelection(i, currentLineMaxColumn, i, currentLineMaxColumn));
        }

        if (selection.EndColumn > 1)
        {
            result.Add(new TextSelection(selection.EndLineNumber, selection.EndColumn, selection.EndLineNumber, selection.EndColumn));
        }
    }

    /// <summary>InsertCursorAtEndOfLineSelected.run (add cursors to bottom): the primary selection repeated on every line below it.</summary>
    public static void AddCursorsToBottom(IMultiCursorEditor editor)
    {
        if (editor.Model is not { } model)
        {
            return;
        }

        var selections = editor.GetSelections();
        var lineCount = model.GetLineCount();
        var newSelections = new List<TextSelection>();
        for (var i = selections[0].StartLineNumber; i <= lineCount; i++)
        {
            newSelections.Add(new TextSelection(i, selections[0].StartColumn, i, selections[0].EndColumn));
        }

        if (newSelections.Count > 0)
        {
            editor.SetSelections(newSelections);
        }
    }

    /// <summary>InsertCursorAtTopOfLineSelected.run (add cursors to top).</summary>
    public static void AddCursorsToTop(IMultiCursorEditor editor)
    {
        if (editor.Model is null)
        {
            return;
        }

        var selections = editor.GetSelections();
        var newSelections = new List<TextSelection>();
        for (var i = selections[0].StartLineNumber; i >= 1; i--)
        {
            newSelections.Add(new TextSelection(i, selections[0].StartColumn, i, selections[0].EndColumn));
        }

        if (newSelections.Count > 0)
        {
            editor.SetSelections(newSelections);
        }
    }

    /// <summary>FocusNextCursor.run: the next cursor becomes the primary one.</summary>
    public static void FocusNextCursor(IMultiCursorEditor editor, string? source = null) => FocusCursor(editor, source, next: true);

    /// <summary>FocusPreviousCursor.run.</summary>
    public static void FocusPreviousCursor(IMultiCursorEditor editor, string? source = null) => FocusCursor(editor, source, next: false);

    private static void FocusCursor(IMultiCursorEditor editor, string? source, bool next)
    {
        if (editor.Model is not { } model || editor.ViewModel is not { } viewModel || viewModel.CursorConfig.ReadOnly)
        {
            return;
        }

        model.PushStackElement();
        var states = viewModel.GetCursorStates().ToList();
        if (states.Count == 0)
        {
            return;
        }

        if (next)
        {
            var firstCursor = states[0];
            states.RemoveAt(0);
            states.Add(firstCursor);
        }
        else
        {
            var lastCursor = states[^1];
            states.RemoveAt(states.Count - 1);
            states.Insert(0, lastCursor);
        }

        viewModel.SetCursorStates(source, CursorChangeReason.Explicit, states.Select(PartialCursorState.From).ToList());
        viewModel.RevealPrimaryCursor(source, true);
    }

    /// <summary>
    /// MultiCursorSelectionControllerAction.run: runs <paramref name="run"/> with the editor's multi-cursor controller and
    /// its find controller (a temporary one when it has none).
    /// </summary>
    public static void RunWithController(IMultiCursorEditor editor, Action<MultiCursorSelectionController, IMultiCursorFindController> run)
    {
        if (editor.MultiCursorController is not { } multiCursorController || editor.ViewModel is null)
        {
            return;
        }

        run(multiCursorController, editor.FindController ?? new StandaloneMultiCursorFindController());
    }

    public static void AddSelectionToNextFindMatch(IMultiCursorEditor editor) =>
        RunWithController(editor, static (controller, find) => controller.AddSelectionToNextFindMatch(find));

    public static void AddSelectionToPreviousFindMatch(IMultiCursorEditor editor) =>
        RunWithController(editor, static (controller, find) => controller.AddSelectionToPreviousFindMatch(find));

    public static void MoveSelectionToNextFindMatch(IMultiCursorEditor editor) =>
        RunWithController(editor, static (controller, find) => controller.MoveSelectionToNextFindMatch(find));

    public static void MoveSelectionToPreviousFindMatch(IMultiCursorEditor editor) =>
        RunWithController(editor, static (controller, find) => controller.MoveSelectionToPreviousFindMatch(find));

    /// <summary>SelectHighlightsAction and CompatChangeAll: every occurrence becomes a selection.</summary>
    public static void SelectHighlights(IMultiCursorEditor editor) =>
        RunWithController(editor, static (controller, find) => controller.SelectAll(find));
}
