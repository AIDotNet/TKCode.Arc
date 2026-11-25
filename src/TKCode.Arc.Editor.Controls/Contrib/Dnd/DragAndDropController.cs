// Ported from vscode (src/vs/editor/contrib/dnd/browser/dnd.ts: DragAndDropController; the start condition of a
// drag from src/vs/editor/browser/controller/mouseHandler.ts MouseDownOperation.start; commit b57d5c9e), MIT license.
// The edit is the engine's DragAndDropCommand.
// Differences:
//  - the drag mode is entered from the editor's MouseDown event (upstream's mouse handler decides it and emits
//    onMouseDrag / onMouseDrop / onMouseDropCanceled); this contribution handles the press, captures the pointer and
//    treats the moves and the release as drag and drop, Escape as cancel;
//  - mouseStyle is the view's pointer cursor (DragCopy / Arrow while dragging);
//  - the dragged selection is the one pressed on (upstream takes the selection under the first drag event, which is
//    the same one unless the pointer jumped);
//  - text dragged in from outside the editor (Avalonia drag and drop) is inserted at the drop position and selected,
//    with the same drop marker (upstream: the dropIntoEditor contribution, without its drop edit providers).

using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Dnd;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>Drags selected text to move it, or with Ctrl (Alt on macOS) to copy it (upstream's DragAndDropController).</summary>
internal sealed class DragAndDropController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.dragAndDrop";

    /// <summary>The class name of the drop marker.</summary>
    public const string DropTargetClassName = "dnd-target";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new DragAndDropController(editor), EditorContributionInstantiation.BeforeFirstInteraction);

    private static readonly ModelDecorationOptions s_decorationOptions = new("dnd-target") { ClassName = DropTargetClassName };
    private static readonly Cursor s_copyCursor = new(StandardCursorType.DragCopy);
    private static readonly Cursor s_defaultCursor = new(StandardCursorType.Arrow);

    private readonly CodeEditor _editor;
    private readonly IDisposable _style;
    private CodeEditorView? _dropView;
    private string[] _dndDecorationIds = [];
    private TextSelection? _dragSelection;
    private TextSelection? _pressedSelection;
    private bool _isDragAndDrop;
    private bool _mouseDown;
    private bool _modifierPressed;

    private DragAndDropController(CodeEditor editor)
    {
        _editor = editor;
        _style = editor.DecorationStyles.Register(DropTargetClassName, new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.DropTarget, CollapsedWidth = 2 });
        editor.MouseDown += OnEditorMouseDown;
        editor.MouseMove += OnEditorMouseMove;
        editor.MouseUp += OnEditorMouseUp;
        editor.EditorKeyDown += OnEditorKeyDown;
        editor.AddHandler(InputElement.KeyUpEvent, OnEditorKeyUp, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        editor.DidChangeTextFocus += OnTextFocus;
        AttachDropTarget(editor.View);
    }

    /// <summary>Whether selected text can be dragged (upstream's dragAndDrop option, default true).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether a drag of selected text is under way.</summary>
    internal bool IsDragging => _isDragAndDrop;

    /// <summary>The position of the drop marker, or null when none shows (tests).</summary>
    internal TextPosition? DropMarkerPosition =>
        _dndDecorationIds.Length > 0 && _editor.Model.GetDecorationRange(_dndDecorationIds[0]) is { } range ? range.Start : null;

    public static DragAndDropController? Get(CodeEditor editor) => editor.GetContribution<DragAndDropController>(Id);

    // Ctrl on Windows and Linux, Alt (Option) on macOS (upstream's TRIGGER_KEY_VALUE / hasTriggerModifier)
    private static bool HasTriggerModifier(KeyModifiers modifiers) =>
        OperatingSystem.IsMacOS() ? modifiers.HasFlag(KeyModifiers.Alt) : modifiers.HasFlag(KeyModifiers.Control);

    private static bool IsTriggerKey(Key key) =>
        OperatingSystem.IsMacOS() ? key is Key.LeftAlt or Key.RightAlt : key is Key.LeftCtrl or Key.RightCtrl;

    private void OnTextFocus(bool focused)
    {
        if (!focused && !_isDragAndDrop)
        {
            OnEditorBlur();
        }
    }

    private void OnEditorBlur()
    {
        RemoveDecoration();
        _dragSelection = null;
        _pressedSelection = null;
        _mouseDown = false;
        _modifierPressed = false;
    }

    private void OnEditorKeyDown(KeyEventArgs e)
    {
        if (_isDragAndDrop && e.Key == Key.Escape)
        {
            // upstream: a key ends the mouse move monitor and cancels the drop
            e.Handled = true;
            EndDrag();
            OnEditorMouseDropCanceled();
            return;
        }

        if (!IsEnabled)
        {
            return;
        }

        if (HasTriggerModifier(e.KeyModifiers) || IsTriggerKey(e.Key))
        {
            _modifierPressed = true;
        }

        if (_mouseDown && _modifierPressed)
        {
            SetMouseStyle(s_copyCursor);
        }
    }

    private void OnEditorKeyUp(object? sender, KeyEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (IsTriggerKey(e.Key) || !HasTriggerModifier(e.KeyModifiers))
        {
            _modifierPressed = false;
        }

        if (_mouseDown && IsTriggerKey(e.Key))
        {
            SetMouseStyle(s_defaultCursor);
        }
    }

    // upstream's MouseDownOperation.start: a single left click on the text of a non-empty primary selection starts a drag
    private void OnEditorMouseDown(CodeEditorMouseEvent e)
    {
        _mouseDown = true;
        var point = e.Event.GetCurrentPoint(_editor.View);
        if (e.Handled
            || !IsEnabled
            || _editor.IsReadOnly
            || _editor.View is not { } view
            || !point.Properties.IsLeftButtonPressed
            || e.Event.KeyModifiers.HasFlag(KeyModifiers.Alt) // we don't support multiple mouse
            || (e.Event is PointerPressedEventArgs { ClickCount: >= 2 })
            || e.Target.Type != CodeEditorMouseTargetType.ContentText
            || e.Target.Position is not { } position
            || _editor.GetSelection() is not { IsEmpty: false } selection
            || !selection.Range.ContainsPosition(position))
        {
            return;
        }

        e.Handled = true;
        _isDragAndDrop = true;
        // the selection under the press is the one dragged, even when the first move lands outside it already
        _pressedSelection = selection;
        e.Event.Pointer.Capture(view);
    }

    private void OnEditorMouseMove(CodeEditorMouseEvent e)
    {
        if (_isDragAndDrop)
        {
            OnEditorMouseDrag(e.Target, e.Event.KeyModifiers);
        }
    }

    private void OnEditorMouseUp(CodeEditorMouseEvent e)
    {
        if (_isDragAndDrop)
        {
            EndDrag();
            e.Event.Pointer.Capture(null);
            OnEditorMouseDrop(e.Target, e.Event.KeyModifiers);
            return;
        }

        _mouseDown = false;
        // Whenever users release the mouse, the drag and drop operation should finish and the cursor should revert to text.
        RestoreMouseStyle();
    }

    private void EndDrag() => _isDragAndDrop = false;

    /// <summary>Upstream's _onEditorMouseDrag: shows where the text would land.</summary>
    internal void OnEditorMouseDrag(CodeEditorMouseTarget target, KeyModifiers modifiers)
    {
        if (_dragSelection is null && _pressedSelection is { } pressed)
        {
            _dragSelection = pressed;
        }

        if (_dragSelection is null)
        {
            var selections = _editor.GetSelections();
            var possibleSelections = selections.Where(s => target.Position is { } p && s.Range.ContainsPosition(p)).ToList();
            if (possibleSelections.Count == 1)
            {
                _dragSelection = possibleSelections[0];
            }
            else
            {
                return;
            }
        }

        SetMouseStyle(HasTriggerModifier(modifiers) ? s_copyCursor : s_defaultCursor);

        if (target.Position is { } position)
        {
            if (_dragSelection.Value.Range.ContainsPosition(position))
            {
                RemoveDecoration();
            }
            else
            {
                ShowAt(position);
            }
        }
    }

    private void OnEditorMouseDropCanceled()
    {
        RestoreMouseStyle();
        RemoveDecoration();
        _dragSelection = null;
        _pressedSelection = null;
        _mouseDown = false;
    }

    /// <summary>Upstream's _onEditorMouseDrop: moves or copies the dragged text, or places the cursor for a click.</summary>
    internal void OnEditorMouseDrop(CodeEditorMouseTarget target, KeyModifiers modifiers)
    {
        if ((HitContent(target) || HitMargin(target)) && target.Position is { } newCursorPosition)
        {
            if (_dragSelection is not { } dragSelection)
            {
                List<TextSelection>? newSelections = null;
                if (modifiers.HasFlag(KeyModifiers.Shift))
                {
                    if (_editor.GetSelection() is { } primarySelection)
                    {
                        newSelections = [new TextSelection(primarySelection.SelectionStartLineNumber, primarySelection.SelectionStartColumn, newCursorPosition.LineNumber, newCursorPosition.Column)];
                    }
                }
                else
                {
                    newSelections = _editor.GetSelections()
                        .Select(s => s.Range.ContainsPosition(newCursorPosition) ? new TextSelection(newCursorPosition, newCursorPosition) : s)
                        .ToList();
                }

                // Use `mouse` as the source instead of `api` and setting the reason to explicit (to behave like any other mouse operation).
                _editor.SetSelections(newSelections ?? [], "mouse", CursorChangeReason.Explicit);
            }
            else
            {
                var copy = HasTriggerModifier(modifiers) || _modifierPressed;
                if (!dragSelection.Range.ContainsPosition(newCursorPosition)
                    || (copy && (dragSelection.Range.End == newCursorPosition || dragSelection.Range.Start == newCursorPosition)))
                {
                    if (_editor.ViewModel is { } viewModel && !_editor.IsReadOnly)
                    {
                        _editor.PushUndoStop();
                        viewModel.ExecuteCommand(new DragAndDropCommand(dragSelection, newCursorPosition, copy), Id);
                        _editor.PushUndoStop();
                    }
                }
            }
        }

        RestoreMouseStyle();
        RemoveDecoration();
        _dragSelection = null;
        _pressedSelection = null;
        _mouseDown = false;
    }

    /// <summary>Shows the drop marker at <paramref name="position"/> (upstream's showAt).</summary>
    public void ShowAt(TextPosition position)
    {
        _dndDecorationIds = _editor.Model.DeltaDecorations(
            _dndDecorationIds,
            [new ModelDeltaDecoration(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column), s_decorationOptions)]);
        _editor.RevealRange(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column), VerticalRevealType.Simple, ScrollType.Immediate);
    }

    private void RemoveDecoration()
    {
        if (_dndDecorationIds.Length > 0 && !_editor.Model.IsDisposed())
        {
            _dndDecorationIds = _editor.Model.DeltaDecorations(_dndDecorationIds, []);
        }

        _dndDecorationIds = [];
    }

    private static bool HitContent(CodeEditorMouseTarget target) =>
        target.Type is CodeEditorMouseTargetType.ContentText or CodeEditorMouseTargetType.ContentEmpty;

    private static bool HitMargin(CodeEditorMouseTarget target) =>
        target.Type is CodeEditorMouseTargetType.GutterGlyphMargin or CodeEditorMouseTargetType.GutterLineNumbers or CodeEditorMouseTargetType.GutterLineDecorations;

    private void SetMouseStyle(Cursor cursor)
    {
        if (_editor.View is { } view)
        {
            view.Cursor = cursor;
        }
    }

    private void RestoreMouseStyle()
    {
        if (_editor.View is { } view)
        {
            view.Cursor = new Cursor(StandardCursorType.Ibeam);
        }
    }

    // ---- text dragged in from outside the editor

    private void AttachDropTarget(CodeEditorView? view)
    {
        if (view is null)
        {
            return;
        }

        _dropView = view;
        DragDrop.SetAllowDrop(view, true);
        view.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        view.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        view.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void DetachDropTarget()
    {
        if (_dropView is not { } view)
        {
            return;
        }

        DragDrop.SetAllowDrop(view, false);
        view.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        view.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        view.RemoveHandler(DragDrop.DropEvent, OnDrop);
        _dropView = null;
    }

    private TextPosition? GetDropPosition(DragEventArgs e) =>
        _editor.View is { } view && view.GetMouseTargetAt(e.GetPosition(view)) is { } target && (HitContent(target) || HitMargin(target))
            ? target.Position
            : null;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!IsEnabled || _editor.IsReadOnly || !e.DataTransfer.Contains(DataFormat.Text) || GetDropPosition(e) is not { } position)
        {
            e.DragEffects = DragDropEffects.None;
            RemoveDecoration();
            return;
        }

        e.DragEffects = DragDropEffects.Copy;
        ShowAt(position);
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => RemoveDecoration();

    private void OnDrop(object? sender, DragEventArgs e)
    {
        RemoveDecoration();
        if (!IsEnabled || _editor.IsReadOnly || GetDropPosition(e) is not { } position || e.DataTransfer.TryGetText() is not { Length: > 0 } text)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        DropText(position, text);
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
        _editor.Focus();
    }

    /// <summary>Inserts dropped text at <paramref name="position"/> and selects it (one undo step).</summary>
    internal void DropText(TextPosition position, string text)
    {
        if (_editor.ViewModel is null || _editor.IsReadOnly)
        {
            return;
        }

        _editor.PushUndoStop();
        _editor.ExecuteEdits(Id, [EditOperation.Insert(position, text)], static inverse =>
            inverse.Count == 1 ? [TextSelection.FromRange(inverse[0].Range)] : null);
        _editor.PushUndoStop();
    }

    public void Dispose()
    {
        RemoveDecoration();
        DetachDropTarget();
        _dragSelection = null;
        _pressedSelection = null;
        _mouseDown = false;
        _modifierPressed = false;
        _editor.MouseDown -= OnEditorMouseDown;
        _editor.MouseMove -= OnEditorMouseMove;
        _editor.MouseUp -= OnEditorMouseUp;
        _editor.EditorKeyDown -= OnEditorKeyDown;
        _editor.RemoveHandler(InputElement.KeyUpEvent, OnEditorKeyUp);
        _editor.DidChangeTextFocus -= OnTextFocus;
        _style.Dispose();
    }
}
