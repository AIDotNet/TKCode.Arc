// The editor's events for its contributions (upstream's ICodeEditor events in codeEditorWidget.ts: onDidChangeCursor*,
// onDidScrollChange, onDidChangeModelContent/Decorations/Language/Options, onDidFocusEditorText, onDidType, onDidPaste,
// onMouseDown/Up/Move/Leave, onKeyDown, onDidLayoutChange, onDidChangeViewZones, onDidChangeHiddenAreas,
// onDidContentSizeChange; commit b57d5c9e, MIT license). They relay the view model's outgoing events while it exists.
// Differences from upstream: one event per outgoing view model event instead of upstream's split of cursor events into
// position and selection; the mouse events carry the Avalonia pointer event and a CodeEditorMouseTarget.

using Avalonia;
using Avalonia.Input;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    private readonly Emitter<string> _onWillType = new();
    private readonly Emitter<string> _onDidType = new();
    private readonly Emitter<TextRange> _onDidPaste = new();
    private readonly Emitter<CursorStateChangedEvent> _onDidChangeCursorSelection = new();
    private readonly Emitter<ScrollChangedEvent> _onDidScrollChange = new();
    private readonly Emitter<ModelContentChangedEvent> _onDidChangeModelContent = new();
    private readonly Emitter<ModelDecorationsChangedEvent> _onDidChangeModelDecorations = new();
    private readonly Emitter<ModelLanguageChangedEvent> _onDidChangeModelLanguage = new();
    private readonly Emitter<ModelOptionsChangedEvent> _onDidChangeModelOptions = new();
    private readonly Emitter<ModelTokensChangedEvent> _onDidChangeModelTokens = new();
    private readonly Emitter<ContentSizeChangedEvent> _onDidContentSizeChange = new();
    private readonly Emitter<bool> _onDidChangeTextFocus = new();
    private readonly Emitter<EditorViewModel> _onDidChangeViewModel = new();
    private readonly Emitter<Size> _onDidLayoutChange = new();
    private readonly Emitter<object?> _onDidChangeViewZones = new();
    private readonly Emitter<object?> _onDidChangeHiddenAreas = new();
    private readonly Emitter<CodeEditorMouseEvent> _onMouseDown = new();
    private readonly Emitter<CodeEditorMouseEvent> _onMouseUp = new();
    private readonly Emitter<CodeEditorMouseEvent> _onMouseMove = new();
    private readonly Emitter<PointerEventArgs> _onMouseLeave = new();
    private readonly Emitter<KeyEventArgs> _onKeyDown = new();
    private EditorViewModel? _relayedViewModel;

    /// <summary>Raised before typed text goes into the model (upstream's onWillType; keyboard only).</summary>
    internal event Action<string>? WillType { add => _onWillType.Add(value!); remove => _onWillType.Remove(value); }

    /// <summary>Raised after typed text went into the model (upstream's onDidType; keyboard only).</summary>
    internal event Action<string>? DidType { add => _onDidType.Add(value!); remove => _onDidType.Remove(value); }

    /// <summary>Raised after a paste from the keyboard, with the range the text took (upstream's onDidPaste).</summary>
    internal event Action<TextRange>? DidPaste { add => _onDidPaste.Add(value!); remove => _onDidPaste.Remove(value); }

    /// <summary>The cursors or selections changed (upstream's onDidChangeCursorPosition and onDidChangeCursorSelection).</summary>
    internal event Action<CursorStateChangedEvent>? DidChangeCursorSelection
    {
        add => _onDidChangeCursorSelection.Add(value!);
        remove => _onDidChangeCursorSelection.Remove(value);
    }

    /// <summary>The scroll position or size changed (upstream's onDidScrollChange).</summary>
    internal event Action<ScrollChangedEvent>? DidScrollChange { add => _onDidScrollChange.Add(value!); remove => _onDidScrollChange.Remove(value); }

    /// <summary>The text changed, as the view model saw it (upstream's onDidChangeModelContent).</summary>
    internal event Action<ModelContentChangedEvent>? DidChangeModelContent
    {
        add => _onDidChangeModelContent.Add(value!);
        remove => _onDidChangeModelContent.Remove(value);
    }

    /// <summary>The model's decorations changed (upstream's onDidChangeModelDecorations).</summary>
    internal event Action<ModelDecorationsChangedEvent>? DidChangeModelDecorations
    {
        add => _onDidChangeModelDecorations.Add(value!);
        remove => _onDidChangeModelDecorations.Remove(value);
    }

    /// <summary>The model's language changed (upstream's onDidChangeModelLanguage); raised also without a view.</summary>
    internal event Action<ModelLanguageChangedEvent>? DidChangeModelLanguage
    {
        add => _onDidChangeModelLanguage.Add(value!);
        remove => _onDidChangeModelLanguage.Remove(value);
    }

    /// <summary>The model's options (tab size, ...) changed (upstream's onDidChangeModelOptions).</summary>
    internal event Action<ModelOptionsChangedEvent>? DidChangeModelOptions
    {
        add => _onDidChangeModelOptions.Add(value!);
        remove => _onDidChangeModelOptions.Remove(value);
    }

    /// <summary>The model's tokens changed (upstream's onDidChangeModelTokens).</summary>
    internal event Action<ModelTokensChangedEvent>? DidChangeModelTokens
    {
        add => _onDidChangeModelTokens.Add(value!);
        remove => _onDidChangeModelTokens.Remove(value);
    }

    /// <summary>The content width or height changed (upstream's onDidContentSizeChange).</summary>
    internal event Action<ContentSizeChangedEvent>? DidContentSizeChange
    {
        add => _onDidContentSizeChange.Add(value!);
        remove => _onDidContentSizeChange.Remove(value);
    }

    /// <summary>The editor text got (true) or lost (false) keyboard focus (upstream's onDidFocusEditorText / onDidBlurEditorText).</summary>
    internal event Action<bool>? DidChangeTextFocus { add => _onDidChangeTextFocus.Add(value!); remove => _onDidChangeTextFocus.Remove(value); }

    /// <summary>A new view model was created (the font, the size of the editor's view or the template changed).</summary>
    internal event Action<EditorViewModel>? DidChangeViewModel
    {
        add => _onDidChangeViewModel.Add(value!);
        remove => _onDidChangeViewModel.Remove(value);
    }

    /// <summary>The size of the editor's view changed (upstream's onDidLayoutChange).</summary>
    internal event Action<Size>? DidLayoutChange { add => _onDidLayoutChange.Add(value!); remove => _onDidLayoutChange.Remove(value); }

    /// <summary>View zones were added, changed or removed (upstream's onDidChangeViewZones).</summary>
    internal event Action<object?>? DidChangeViewZones { add => _onDidChangeViewZones.Add(value!); remove => _onDidChangeViewZones.Remove(value); }

    /// <summary>Hidden areas (folded regions) changed (upstream's onDidChangeHiddenAreas).</summary>
    internal event Action<object?>? DidChangeHiddenAreas
    {
        add => _onDidChangeHiddenAreas.Add(value!);
        remove => _onDidChangeHiddenAreas.Remove(value);
    }

    /// <summary>A pointer button went down on the view (upstream's onMouseDown); set Handled on the args to keep the editor from acting on it.</summary>
    internal event Action<CodeEditorMouseEvent>? MouseDown { add => _onMouseDown.Add(value!); remove => _onMouseDown.Remove(value); }

    /// <summary>A pointer button went up on the view (upstream's onMouseUp).</summary>
    internal event Action<CodeEditorMouseEvent>? MouseUp { add => _onMouseUp.Add(value!); remove => _onMouseUp.Remove(value); }

    /// <summary>The pointer moved over the view (upstream's onMouseMove).</summary>
    internal event Action<CodeEditorMouseEvent>? MouseMove { add => _onMouseMove.Add(value!); remove => _onMouseMove.Remove(value); }

    /// <summary>The pointer left the view (upstream's onMouseLeave).</summary>
    internal event Action<PointerEventArgs>? MouseLeave { add => _onMouseLeave.Add(value!); remove => _onMouseLeave.Remove(value); }

    /// <summary>A key went down in the editor, before its keybindings (upstream's onKeyDown); set Handled to consume it.</summary>
    internal event Action<KeyEventArgs>? EditorKeyDown { add => _onKeyDown.Add(value!); remove => _onKeyDown.Remove(value); }

    internal void RaiseMouseDown(CodeEditorMouseEvent e) => _onMouseDown.Fire(e);

    internal void RaiseMouseUp(CodeEditorMouseEvent e) => _onMouseUp.Fire(e);

    internal void RaiseMouseMove(CodeEditorMouseEvent e) => _onMouseMove.Fire(e);

    internal void RaiseMouseLeave(PointerEventArgs e) => _onMouseLeave.Fire(e);

    internal void RaiseLayoutChange(Size size) => _onDidLayoutChange.Fire(size);

    internal void RaiseTextFocusChanged(bool focused) => _onDidChangeTextFocus.Fire(focused);

    // Called after a view model was created: relay its outgoing events.
    private void RelayViewModelEvents(EditorViewModel viewModel)
    {
        UnrelayViewModelEvents();
        _relayedViewModel = viewModel;
        viewModel.OutgoingEvent += OnOutgoingViewModelEvent;
        _onDidChangeViewModel.Fire(viewModel);
    }

    private void UnrelayViewModelEvents()
    {
        if (_relayedViewModel is not null)
        {
            _relayedViewModel.OutgoingEvent -= OnOutgoingViewModelEvent;
            _relayedViewModel = null;
        }
    }

    private void OnOutgoingViewModelEvent(OutgoingViewModelEvent e)
    {
        switch (e)
        {
            case CursorStateChangedEvent cursor:
                _onDidChangeCursorSelection.Fire(cursor);
                break;
            case ScrollChangedEvent scroll:
                _onDidScrollChange.Fire(scroll);
                break;
            case ContentSizeChangedEvent size:
                _onDidContentSizeChange.Fire(size);
                break;
            case OutgoingModelContentChangedEvent content:
                _onDidChangeModelContent.Fire(content.Event);
                break;
            case OutgoingModelDecorationsChangedEvent decorations:
                _onDidChangeModelDecorations.Fire(decorations.Event);
                break;
            case OutgoingModelOptionsChangedEvent options:
                _onDidChangeModelOptions.Fire(options.Event);
                break;
            case OutgoingModelTokensChangedEvent tokens:
                _onDidChangeModelTokens.Fire(tokens.Event);
                break;
            case OutgoingViewZonesChangedEvent:
                _onDidChangeViewZones.Fire(null);
                break;
            case HiddenAreasChangedEvent:
                _onDidChangeHiddenAreas.Fire(null);
                break;
        }
    }

    // the language event comes from the model itself, so that it reaches contributions also without a view
    private void OnModelLanguageChanged(ModelLanguageChangedEvent e) => _onDidChangeModelLanguage.Fire(e);
}

/// <summary>What is under the pointer (upstream's MouseTargetType).</summary>
internal enum CodeEditorMouseTargetType
{
    /// <summary>Somewhere else.</summary>
    Unknown,

    /// <summary>A gutter column before the line numbers (the glyph margin).</summary>
    GutterGlyphMargin,

    /// <summary>The line numbers.</summary>
    GutterLineNumbers,

    /// <summary>A gutter column after the line numbers (lines decorations, folding).</summary>
    GutterLineDecorations,

    /// <summary>The gutter next to a view zone.</summary>
    GutterViewZone,

    /// <summary>Text.</summary>
    ContentText,

    /// <summary>The text area after the end of a line or after the last line.</summary>
    ContentEmpty,

    /// <summary>A view zone.</summary>
    ContentViewZone,

    /// <summary>A content widget.</summary>
    ContentWidget,

    /// <summary>An overlay widget.</summary>
    OverlayWidget,

    /// <summary>A scroll bar.</summary>
    Scrollbar,
}

/// <summary>
/// What a point of the editor's view lies on (upstream's IMouseTarget): its type, the model position and range there
/// (null over a widget or a zone), and the detail: the gutter column's id for the gutter, the zone's id for a zone.
/// </summary>
internal sealed record CodeEditorMouseTarget(CodeEditorMouseTargetType Type, TextPosition? Position, TextRange? Range)
{
    /// <summary>The id of the gutter column, view zone or widget under the point, or null.</summary>
    public string? Detail { get; init; }

    /// <summary>The 1-based visible column under the point (also past the end of the line); 0 outside the text.</summary>
    public int MouseColumn { get; init; }

    /// <summary>The point, relative to the editor's view.</summary>
    public Point Point { get; init; }
}

/// <summary>A pointer event on the editor's view with its target (upstream's IEditorMouseEvent).</summary>
internal sealed class CodeEditorMouseEvent(PointerEventArgs e, CodeEditorMouseTarget target)
{
    /// <summary>The Avalonia event.</summary>
    public PointerEventArgs Event { get; } = e;

    /// <summary>What lies under the pointer.</summary>
    public CodeEditorMouseTarget Target { get; } = target;

    /// <summary>Set by a listener to keep the editor from acting on a pointer press (cursor move, selection).</summary>
    public bool Handled { get; set; }
}
