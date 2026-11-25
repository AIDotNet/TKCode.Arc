// Ported from vscode (src/vs/editor/contrib/gotoSymbol/browser/peek/referencesController.ts: ReferencesController and
// the goToNextReference, goToPreviousReference, closeReferenceSearch and togglePeekWidgetFocus keybindings; commit
// b57d5c9e), MIT license.
// Differences from upstream:
//  - the results arrive as a Task<ReferencesModel> with a CancellationTokenSource (upstream's CancelablePromise);
//  - another document opens through the host (CodeEditor.OpenLocation): a peek continues only in this editor;
//  - the layout data is not stored; editor.stablePeek and peekWidgetDefaultFocus are their defaults (false, 'tree');
//  - revealReference / openReferenceToSide keybindings of the workbench list are the list's own keys (Enter,
//    Ctrl+Enter).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.GotoSymbol;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>Shows references and definitions in a peek view below the line (upstream's ReferencesController).</summary>
internal sealed class ReferencesController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.referencesController";

    public static readonly EditorContributionDescription Description = new(Id, editor => new ReferencesController(editor), EditorContributionInstantiation.Lazy);

    private static readonly ContextKeyExpression? s_peekWhen =
        ContextKeyExpr.Or(CodeEditorContextKeys.ReferenceSearchVisible, PeekContext.InPeekEditor);

    /// <summary>F4: the next reference.</summary>
    public static readonly EditorCommand GoToNextReferenceCommand = EditorCommand.Create(
        "goToNextReference", null,
        static (editor, _) => Get(editor)?.GoToNextOrPreviousReference(true) ?? Task.CompletedTask,
        new EditorKeybinding((int)KeyCode.F4) { Secondary = [(int)KeyCode.F12], Weight = KeybindingWeight.EditorContrib - 10, When = s_peekWhen });

    /// <summary>Shift+F4: the previous reference.</summary>
    public static readonly EditorCommand GoToPreviousReferenceCommand = EditorCommand.Create(
        "goToPreviousReference", null,
        static (editor, _) => Get(editor)?.GoToNextOrPreviousReference(false) ?? Task.CompletedTask,
        new EditorKeybinding(KeyMod.Shift | (int)KeyCode.F4) { Secondary = [KeyMod.Shift | (int)KeyCode.F12], Weight = KeybindingWeight.EditorContrib - 10, When = s_peekWhen });

    /// <summary>Escape: closes the peek view.</summary>
    public static readonly EditorCommand CloseReferenceSearchCommand = EditorCommand.Create(
        "closeReferenceSearch", null,
        static (editor, _) => Get(editor)?.CloseWidget(),
        new EditorKeybinding((int)KeyCode.Escape)
        {
            Secondary = [KeyMod.Shift | (int)KeyCode.Escape],
            Weight = KeybindingWeight.WorkbenchContrib + 50,
            When = CodeEditorContextKeys.ReferenceSearchVisible,
        });

    /// <summary>Ctrl+K F2: moves the focus between the preview and the list.</summary>
    public static readonly EditorCommand TogglePeekWidgetFocusCommand = EditorCommand.Create(
        "togglePeekWidgetFocus", null,
        static (editor, _) => Get(editor)?.ChangeFocusBetweenPreviewAndReferences(),
        new EditorKeybinding(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, (int)KeyCode.F2)) { When = s_peekWhen });

    private readonly CodeEditor _editor;
    private readonly IContextKey<bool> _referenceSearchVisible;
    private ReferencesWidget? _widget;
    private ReferencesModel? _model;
    private CancellationTokenSource? _modelCts;
    private bool _peekMode;
    private int _requestIdPool;
    private bool _ignoreModelChangeEvent;

    private ReferencesController(CodeEditor editor)
    {
        _editor = editor;
        _referenceSearchVisible = editor.CreateContextKey(CodeEditorContextKeys.ReferenceSearchVisible);
    }

    public static ReferencesController? Get(CodeEditor editor) => editor.GetContribution<ReferencesController>(Id);

    /// <summary>The peek view that shows, or null.</summary>
    internal ReferencesWidget? Widget => _widget;

    /// <summary>The results the peek view shows.</summary>
    internal ReferencesModel? Model => _model;

    /// <summary>The alternative symbol navigation commands running in this editor (upstream's static _activeAlternativeCommands).</summary>
    internal HashSet<string> ActiveAlternativeCommands { get; } = new(StringComparer.Ordinal);

    public void Dispose()
    {
        CloseWidget(focusEditor: false);
    }

    /// <summary>
    /// Opens the peek view for <paramref name="range"/> with the results of <paramref name="modelTask"/>; when the view
    /// already shows at that range, closes it (upstream's toggleWidget).
    /// </summary>
    public void ToggleWidget(TextRange range, Task<ReferencesModel> modelTask, CancellationTokenSource? modelCts, bool peekMode)
    {
        // close the current widget and return early if it was at the same position
        var widgetPosition = _widget?.Position;
        CloseWidget(focusEditor: false);
        if (widgetPosition is { } position && range.ContainsPosition(position))
        {
            modelCts?.Cancel();
            return;
        }

        _peekMode = peekMode;
        _referenceSearchVisible.Set(true);
        _modelCts = modelCts;

        // close the widget on language or content changes
        _editor.DidChangeModelLanguage += OnLanguageChanged;
        _editor.DidChangeModelContent += OnContentChanged;

        var widget = new ReferencesWidget(_editor);
        _widget = widget;
        widget.SetTitle("Loading...");
        widget.Show(range, 18);
        widget.Closed += OnWidgetClosed;
        widget.SelectReference += OnSelectReference;

        var requestId = ++_requestIdPool;
        _ = ShowModel(requestId, range, modelTask, widget);
    }

    private async Task ShowModel(int requestId, TextRange range, Task<ReferencesModel> modelTask, ReferencesWidget widget)
    {
        ReferencesModel model;
        try
        {
            model = await modelTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error)
        {
            MessageController.Get(_editor)?.ShowMessage(error.Message, range.Start);
            return;
        }

        // still the current request? the widget still open?
        if (requestId != _requestIdPool || !ReferenceEquals(_widget, widget))
        {
            model.Dispose();
            return;
        }

        _model?.Dispose();
        _model = model;

        await widget.SetModel(model).ConfigureAwait(true);
        if (!ReferenceEquals(_widget, widget) || !ReferenceEquals(_model, model))
        {
            return;
        }

        widget.SetMetaTitle(model.IsEmpty ? string.Empty : $"{model.Title} ({model.References.Count})");

        // the best selection: the reference nearest to the range
        if (model.NearestReference(_editor.Model.Uri, range.Start) is { } selection)
        {
            await widget.SetSelection(selection).ConfigureAwait(true);
            if (ReferenceEquals(_widget, widget))
            {
                widget.FocusOnReferenceTree();
            }
        }
    }

    /// <summary>Moves the focus between the preview and the list (Ctrl+K F2).</summary>
    public void ChangeFocusBetweenPreviewAndReferences()
    {
        if (_widget is not { } widget)
        {
            return;
        }

        if (widget.IsPreviewEditorFocused())
        {
            widget.FocusOnReferenceTree();
        }
        else
        {
            widget.FocusOnPreviewEditor();
        }
    }

    /// <summary>Goes to the next or previous reference and shows it (F4 / Shift+F4).</summary>
    public async Task GoToNextOrPreviousReference(bool fwd)
    {
        if (_model is not { } model || _widget is not { } widget || widget.Position is not { } currentPosition)
        {
            return;
        }

        if (model.NearestReference(_editor.Model.Uri, currentPosition) is not { } source)
        {
            return;
        }

        var target = model.NextOrPreviousReference(source, fwd);
        var editorFocus = _editor.IsTextFocused;
        var previewEditorFocus = widget.IsPreviewEditorFocused();
        await widget.SetSelection(target).ConfigureAwait(true);
        await GotoReference(target, pinned: false).ConfigureAwait(true);
        if (editorFocus)
        {
            _editor.Focus();
        }
        else if (_widget is { } current && previewEditorFocus)
        {
            current.FocusOnPreviewEditor();
        }
    }

    /// <summary>Closes the peek view (upstream's closeWidget).</summary>
    public void CloseWidget(bool focusEditor = true)
    {
        if (_widget is { } widget)
        {
            widget.Closed -= OnWidgetClosed;
            widget.SelectReference -= OnSelectReference;
            widget.Dispose();
        }

        _model?.Dispose();
        _modelCts?.Cancel();
        _modelCts = null;
        _referenceSearchVisible.Reset();
        _editor.DidChangeModelLanguage -= OnLanguageChanged;
        _editor.DidChangeModelContent -= OnContentChanged;
        var hadWidget = _widget is not null;
        _widget = null;
        _model = null;
        if (focusEditor && hadWidget)
        {
            _editor.Focus();
        }

        // cancel pending requests
        _requestIdPool += 1;
    }

    /// <summary>Opens a reference and closes the peek view, unless it opens to the side (upstream's openReference).</summary>
    public void OpenReference(Location reference, bool sideBySide)
    {
        if (!sideBySide)
        {
            CloseWidget();
        }

        _ = _editor.OpenLocation(reference.Uri, reference.Range, sideBySide);
    }

    // upstream's _gotoReference: show the reference in this editor and the peek view there; another document goes to
    // the host and the peek view closes
    private async Task GotoReference(OneReference reference, bool pinned)
    {
        _widget?.Hide();
        _ignoreModelChangeEvent = true;
        var range = new TextRange(reference.Range.Start);
        bool shownHere;
        try
        {
            shownHere = await _editor.OpenLocation(reference.Uri, range, focus: false).ConfigureAwait(true);
        }
        finally
        {
            _ignoreModelChangeEvent = false;
        }

        if (_widget is not { } widget)
        {
            return;
        }

        if (shownHere)
        {
            widget.Show(range, 18);
            widget.FocusOnReferenceTree();
        }
        else
        {
            CloseWidget();
        }
    }

    private void OnSelectReference(OneReference element, ReferenceSelectKind kind)
    {
        switch (kind)
        {
            case ReferenceSelectKind.Show:
                _ = _widget?.RevealReference(element);
                break;
            case ReferenceSelectKind.Open:
                OpenReference(new Location(element.Uri, element.Range), sideBySide: false);
                break;
            case ReferenceSelectKind.Side:
                OpenReference(new Location(element.Uri, element.Range), sideBySide: true);
                break;
            case ReferenceSelectKind.Goto:
                if (_peekMode)
                {
                    _ = GotoReference(element, pinned: true);
                }
                else
                {
                    OpenReference(new Location(element.Uri, element.Range), sideBySide: false);
                }

                break;
        }
    }

    private void OnWidgetClosed() => CloseWidget();

    private void OnLanguageChanged(Editor.Model.ModelLanguageChangedEvent e) => CloseWidget();

    private void OnContentChanged(Editor.Model.ModelContentChangedEvent e)
    {
        if (!_ignoreModelChangeEvent)
        {
            CloseWidget();
        }
    }
}
