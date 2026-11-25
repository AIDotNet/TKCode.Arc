// Ported from vscode (src/vs/editor/contrib/cursorUndo/browser/cursorUndo.ts, commit b57d5c9e), MIT license: the state
// of CursorUndoRedoController (CursorState, StackElement and the two stacks).
// Differences from upstream: the stacks are this engine class, which the control's controller feeds from the editor's
// events (OnModelChanged, OnCursorSelectionChanged) and asks (CursorUndo / CursorRedo); the scroll position is a pair of
// doubles the controller reads and restores.

using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Contrib.CursorUndo;

/// <summary>Saved selections (upstream's CursorState).</summary>
internal sealed class CursorUndoState
{
    public CursorUndoState(IReadOnlyList<TextSelection> selections)
    {
        Selections = selections;
    }

    public IReadOnlyList<TextSelection> Selections { get; }

    public bool Equals(CursorUndoState other)
    {
        var thisLen = Selections.Count;
        var otherLen = other.Selections.Count;
        if (thisLen != otherLen)
        {
            return false;
        }
        for (var i = 0; i < thisLen; i++)
        {
            if (!Selections[i].Equals(other.Selections[i]))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>A saved cursor state with the scroll position (upstream's StackElement).</summary>
internal sealed record CursorUndoStackElement(CursorUndoState CursorState, double ScrollTop, double ScrollLeft);

/// <summary>The cursor undo and redo stacks of an editor (Ctrl+U).</summary>
internal sealed class CursorUndoRedo
{
    private const int MaxUndoStackLength = 50;

    private readonly List<CursorUndoStackElement> _undoStack = new();
    private readonly List<CursorUndoStackElement> _redoStack = new();
    private bool _isCursorUndoRedo;

    /// <summary>The model or its content changed: forget everything.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    /// <summary>The cursors moved (upstream's onDidChangeCursorSelection listener).</summary>
    public void OnCursorSelectionChanged(CursorStateChangedEvent e, double scrollTop, double scrollLeft)
    {
        if (_isCursorUndoRedo)
        {
            return;
        }
        if (e.OldSelections is not { } oldSelections)
        {
            return;
        }
        if (e.OldModelVersionId != e.ModelVersionId)
        {
            return;
        }
        var prevState = new CursorUndoState(oldSelections);
        var isEqualToLastUndoStack = _undoStack.Count > 0 && _undoStack[^1].CursorState.Equals(prevState);
        if (!isEqualToLastUndoStack)
        {
            _undoStack.Add(new CursorUndoStackElement(prevState, scrollTop, scrollLeft));
            _redoStack.Clear();
            if (_undoStack.Count > MaxUndoStackLength)
            {
                // keep the cursor undo stack bounded
                _undoStack.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Goes back to the previous cursor state (upstream's cursorUndo): <paramref name="apply"/> sets the selections and
    /// the scroll position; nothing when there is none.
    /// </summary>
    public void CursorUndo(IReadOnlyList<TextSelection> currentSelections, double scrollTop, double scrollLeft, Action<CursorUndoStackElement> apply)
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        _redoStack.Add(new CursorUndoStackElement(new CursorUndoState(currentSelections), scrollTop, scrollLeft));
        var element = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        ApplyState(element, apply);
    }

    /// <summary>Goes forward again (upstream's cursorRedo).</summary>
    public void CursorRedo(IReadOnlyList<TextSelection> currentSelections, double scrollTop, double scrollLeft, Action<CursorUndoStackElement> apply)
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        _undoStack.Add(new CursorUndoStackElement(new CursorUndoState(currentSelections), scrollTop, scrollLeft));
        var element = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        ApplyState(element, apply);
    }

    private void ApplyState(CursorUndoStackElement stackElement, Action<CursorUndoStackElement> apply)
    {
        _isCursorUndoRedo = true;
        try
        {
            apply(stackElement);
        }
        finally
        {
            _isCursorUndoRedo = false;
        }
    }
}
