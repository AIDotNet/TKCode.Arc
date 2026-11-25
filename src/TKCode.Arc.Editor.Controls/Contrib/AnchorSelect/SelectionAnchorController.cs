// Ported from vscode (src/vs/editor/contrib/anchorSelect/browser/anchorSelect.ts: SelectionAnchorSet,
// SelectionAnchorController, SetSelectionAnchor, GoToSelectionAnchor, SelectFromAnchorToCursor and
// CancelSelectionAnchor; commit b57d5c9e), MIT license.
// Differences: no aria alert when the anchor is set; the anchor's hover message is not shown (no hover here); its
// marker is a 2px bar in the theme's selection anchor color (upstream's .selection-anchor CSS).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>A position to select from later (upstream's SelectionAnchorController).</summary>
internal sealed class SelectionAnchorController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.selectionAnchorController";

    /// <summary>The class name of the anchor's marker.</summary>
    public const string ClassName = "selection-anchor";

    /// <summary>Whether an anchor is set (upstream's SelectionAnchorSet).</summary>
    public static readonly RawContextKey<bool> SelectionAnchorSet = new("selectionAnchorSet", false);

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new SelectionAnchorController(editor), EditorContributionInstantiation.Lazy);

    private static readonly ModelDecorationOptions s_options = new("selection-anchor")
    {
        Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
        ClassName = ClassName,
    };

    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _selectionAnchorSetContextKey;
    private readonly IDisposable _style;
    private string? _decorationId;

    private SelectionAnchorController(CodeEditor editor)
    {
        _editor = editor;
        _selectionAnchorSetContextKey = editor.CreateContextKey(SelectionAnchorSet);
        _style = editor.DecorationStyles.Register(ClassName, new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.SelectionAnchor, CollapsedWidth = 2 });
    }

    /// <summary>The anchor's position, or null when none is set (tests).</summary>
    internal TextPosition? AnchorPosition => _decorationId is { } id && _editor.Model.GetDecorationRange(id) is { } range ? range.Start : null;

    public static SelectionAnchorController? Get(CodeEditor editor) => editor.GetContribution<SelectionAnchorController>(Id);

    /// <summary>Sets the anchor at the cursor (upstream's setSelectionAnchor).</summary>
    public void SetSelectionAnchor()
    {
        if (_editor.GetPosition() is not { } position)
        {
            return;
        }

        _decorationId = _editor.Model.DeltaDecorations(
            _decorationId is { } old ? [old] : [],
            [new ModelDeltaDecoration(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column), s_options)])[0];
        _selectionAnchorSetContextKey.Set(_decorationId is not null);
    }

    /// <summary>Moves the cursor to the anchor (upstream's goToSelectionAnchor).</summary>
    public void GoToSelectionAnchor()
    {
        if (_decorationId is { } id && _editor.Model.GetDecorationRange(id) is { } anchor)
        {
            _editor.SetPosition(anchor.Start);
        }
    }

    /// <summary>Selects from the anchor to the cursor and removes the anchor (upstream's selectFromAnchorToCursor).</summary>
    public void SelectFromAnchorToCursor()
    {
        if (_decorationId is { } id && _editor.Model.GetDecorationRange(id) is { } start && _editor.GetPosition() is { } end)
        {
            _editor.SetSelection(new TextSelection(start.Start, end));
            CancelSelectionAnchor();
        }
    }

    /// <summary>Removes the anchor (upstream's cancelSelectionAnchor).</summary>
    public void CancelSelectionAnchor()
    {
        if (_decorationId is { } id)
        {
            if (!_editor.Model.IsDisposed())
            {
                _editor.Model.DeltaDecorations([id], []);
            }

            _decorationId = null;
            _selectionAnchorSetContextKey.Set(false);
        }
    }

    public void Dispose()
    {
        CancelSelectionAnchor();
        _style.Dispose();
    }

    public static readonly EditorAction SetSelectionAnchorAction = EditorAction.Create(
        "editor.action.setSelectionAnchor",
        "Set Selection Anchor",
        null,
        (editor, _) => Get(editor)?.SetSelectionAnchor(),
        new EditorKeybinding(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyK, KeyMod.CtrlCmd | (int)EditorKeyCode.KeyB)) { When = EditorContextKeys.EditorTextFocus });

    public static readonly EditorAction GoToSelectionAnchorAction = EditorAction.Create(
        "editor.action.goToSelectionAnchor",
        "Go to Selection Anchor",
        SelectionAnchorSet,
        (editor, _) => Get(editor)?.GoToSelectionAnchor());

    public static readonly EditorAction SelectFromAnchorToCursorAction = EditorAction.Create(
        "editor.action.selectFromAnchorToCursor",
        "Select from Anchor to Cursor",
        SelectionAnchorSet,
        (editor, _) => Get(editor)?.SelectFromAnchorToCursor(),
        new EditorKeybinding(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyK, KeyMod.CtrlCmd | (int)EditorKeyCode.KeyK)) { When = EditorContextKeys.EditorTextFocus });

    public static readonly EditorAction CancelSelectionAnchorAction = EditorAction.Create(
        "editor.action.cancelSelectionAnchor",
        "Cancel Selection Anchor",
        SelectionAnchorSet,
        (editor, _) => Get(editor)?.CancelSelectionAnchor(),
        new EditorKeybinding((int)EditorKeyCode.Escape) { When = EditorContextKeys.EditorTextFocus });
}
