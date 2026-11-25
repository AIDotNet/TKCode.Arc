// Ported from vscode (src/vs/editor/contrib/gotoSymbol/browser/link/goToDefinitionAtPosition.ts and
// clickLinkGesture.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the trigger modifier is Ctrl (Cmd on macOS), Alt being the multi-cursor modifier (upstream's default); Ctrl+Alt
//    opens to the side (through the host); the middle-click 'openLink' action is not ported;
//  - the link decoration has no hover message (the preview of the definition is the hover track's); the pointer
//    becomes a hand over the link;
//  - key up comes from the editor's KeyUp routed event (the contract has no key up event).
//  The contribution also gives the symbol navigation's jump highlight its style.

using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.GotoSymbol;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using TokenFontStyle = TKCode.Arc.Editor.Tokens.FontStyle;

namespace TKCode.Arc;

/// <summary>Ctrl + hover underlines a symbol with a definition; Ctrl + click goes there (upstream's GotoDefinitionAtPositionEditorContribution).</summary>
internal sealed class GotoDefinitionAtPosition : ICodeEditorContribution
{
    public const string Id = "editor.contrib.gotodefinitionatposition";
    public const string LinkClassName = "goto-definition-link";

    public static readonly EditorContributionDescription Description = new(Id, editor => new GotoDefinitionAtPosition(editor), EditorContributionInstantiation.BeforeFirstInteraction);

    private static readonly Cursor s_hand = new(StandardCursorType.Hand);

    private readonly CodeEditor _editor;
    private readonly List<IDisposable> _styles = new();
    private string[] _linkDecorations = [];
    private WordAtPosition? _currentWordAtPosition;
    private CancellationTokenSource? _previousRequest;
    private CodeEditorMouseEvent? _lastMouseMoveEvent;
    private bool _hasTriggerKeyOnMouseDown;
    private int _lineNumberOnMouseDown;

    private GotoDefinitionAtPosition(CodeEditor editor)
    {
        _editor = editor;
        _styles.Add(editor.DecorationStyles.Register(LinkClassName, new CodeEditorDecorationStyle { Foreground = CodeEditorThemeColor.Link, FontStyle = TokenFontStyle.Underline }));
        _styles.Add(editor.DecorationStyles.Register(SymbolNavigationAction.SymbolHighlightClassName, new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.RangeHighlight }));

        editor.MouseMove += OnMouseMove;
        editor.MouseDown += OnMouseDown;
        editor.MouseUp += OnMouseUp;
        editor.MouseLeave += OnMouseLeave;
        editor.EditorKeyDown += OnKeyDown;
        editor.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Bubble, handledEventsToo: true);
        editor.DidChangeCursorSelection += OnCursorSelectionChanged;
        editor.DidChangeModelContent += OnContentChanged;
        editor.DidScrollChange += OnScrollChanged;
    }

    public static GotoDefinitionAtPosition? Get(CodeEditor editor) => editor.GetContribution<GotoDefinitionAtPosition>(Id);

    /// <summary>The range of the link that shows, or null.</summary>
    internal TextRange? LinkRange => _linkDecorations.Length > 0 ? _editor.Model.GetDecorationRange(_linkDecorations[0]) : null;

    /// <summary>The pending request for the link (for tests).</summary>
    internal Task? PendingRequest { get; private set; }

    public void Dispose()
    {
        _editor.MouseMove -= OnMouseMove;
        _editor.MouseDown -= OnMouseDown;
        _editor.MouseUp -= OnMouseUp;
        _editor.MouseLeave -= OnMouseLeave;
        _editor.EditorKeyDown -= OnKeyDown;
        _editor.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
        _editor.DidChangeCursorSelection -= OnCursorSelectionChanged;
        _editor.DidChangeModelContent -= OnContentChanged;
        _editor.DidScrollChange -= OnScrollChanged;
        _previousRequest?.Cancel();
        RemoveLinkDecorations();
        foreach (var style in _styles)
        {
            style.Dispose();
        }
    }

    private static bool HasTriggerModifier(KeyModifiers modifiers) =>
        OperatingSystem.IsMacOS() ? modifiers.HasFlag(KeyModifiers.Meta) : modifiers.HasFlag(KeyModifiers.Control);

    private static bool HasSideBySideModifier(KeyModifiers modifiers) => modifiers.HasFlag(KeyModifiers.Alt);

    private static bool IsTriggerKey(Key key) =>
        OperatingSystem.IsMacOS() ? key is Key.LWin or Key.RWin : key is Key.LeftCtrl or Key.RightCtrl;

    private bool IsEnabled(CodeEditorMouseEvent mouseEvent, bool withTriggerKey = false)
    {
        var properties = mouseEvent.Event.GetCurrentPoint(null).Properties;
        return mouseEvent.Target.Type == CodeEditorMouseTargetType.ContentText
            && mouseEvent.Target.Position is not null
            && !properties.IsRightButtonPressed && !properties.IsMiddleButtonPressed
            && (HasTriggerModifier(mouseEvent.Event.KeyModifiers) || withTriggerKey)
            && _editor.LanguageFeatures.DefinitionProvider.Has(_editor.Model);
    }

    // ---- the gesture (upstream's ClickLinkGesture)

    private void OnMouseMove(CodeEditorMouseEvent e)
    {
        _lastMouseMoveEvent = e;
        StartFindDefinitionFromMouse(e, withTriggerKey: false);
        if (_linkDecorations.Length > 0 && e.Target.Position is { } position && LinkRange is { } link && link.ContainsPosition(position)
            && _editor.View is { } view)
        {
            view.Cursor = s_hand;
        }
    }

    private void OnMouseDown(CodeEditorMouseEvent e)
    {
        // whether the trigger key was down at the press: a selection that starts without it never navigates
        _hasTriggerKeyOnMouseDown = HasTriggerModifier(e.Event.KeyModifiers);
        _lineNumberOnMouseDown = e.Target.Position?.LineNumber ?? 0;
    }

    private void OnMouseUp(CodeEditorMouseEvent e)
    {
        var currentLineNumber = e.Target.Position?.LineNumber ?? 0;
        var lineNumbersCorrect = _lineNumberOnMouseDown != 0 && _lineNumberOnMouseDown == currentLineNumber;
        if (lineNumbersCorrect && _hasTriggerKeyOnMouseDown && IsEnabled(e) && e.Target.Position is { } position)
        {
            var sideBySide = HasSideBySideModifier(e.Event.KeyModifiers);
            _ = GotoDefinition(position, sideBySide).ContinueWith(_ => RemoveLinkDecorations(), TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    private void OnMouseLeave(PointerEventArgs e) => ResetHandler();

    private void OnKeyDown(KeyEventArgs e)
    {
        if (_lastMouseMoveEvent is { } last && (IsTriggerKey(e.Key) || (e.Key is Key.LeftAlt or Key.RightAlt && HasTriggerModifier(e.KeyModifiers))))
        {
            // the user just pressed Ctrl / Cmd (normal go to definition) or Ctrl / Cmd + Alt (to the side)
            StartFindDefinitionFromMouse(last, withTriggerKey: true);
        }
        else if (HasTriggerModifier(e.KeyModifiers))
        {
            // another key with Ctrl / Cmd: no accidental navigation
            Cancel();
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (IsTriggerKey(e.Key))
        {
            Cancel();
        }
    }

    private void OnCursorSelectionChanged(CursorStateChangedEvent e)
    {
        if (_editor.GetSelection() is { IsEmpty: false } selection && selection.StartColumn != selection.EndColumn)
        {
            // the user starts to select
            ResetHandler();
        }
    }

    private void OnContentChanged(ModelContentChangedEvent e) => ResetHandler();

    private void OnScrollChanged(ScrollChangedEvent e)
    {
        if (e.ScrollTopChanged || e.ScrollLeftChanged)
        {
            ResetHandler();
        }
    }

    private void ResetHandler()
    {
        _lastMouseMoveEvent = null;
        _hasTriggerKeyOnMouseDown = false;
        Cancel();
    }

    private void Cancel()
    {
        RemoveLinkDecorations();
        _currentWordAtPosition = null;
    }

    // ---- the link

    private void StartFindDefinitionFromMouse(CodeEditorMouseEvent mouseEvent, bool withTriggerKey)
    {
        if (!IsEnabled(mouseEvent, withTriggerKey) || mouseEvent.Target.Position is not { } position)
        {
            _currentWordAtPosition = null;
            RemoveLinkDecorations();
            return;
        }

        PendingRequest = StartFindDefinition(position);
    }

    private async Task StartFindDefinition(TextPosition position)
    {
        // the word at the mouse position
        var word = ModelWords.GetWordAtPosition(_editor.Model, position, _editor.LanguageConfigurationService);
        if (word is null)
        {
            _currentWordAtPosition = null;
            RemoveLinkDecorations();
            return;
        }

        // the same word: nothing to do
        if (_currentWordAtPosition is { } current && current.StartColumn == word.StartColumn && current.EndColumn == word.EndColumn && current.Word == word.Word)
        {
            return;
        }

        _currentWordAtPosition = word;

        // find the definition and decorate the word when there is one
        var state = new EditorState(_editor, CodeEditorStateFlag.Position | CodeEditorStateFlag.Value | CodeEditorStateFlag.Selection | CodeEditorStateFlag.Scroll);
        _previousRequest?.Cancel();
        var cts = new CancellationTokenSource();
        _previousRequest = cts;

        IReadOnlyList<LocationLink> results;
        try
        {
            results = await GoToSymbol.GetDefinitionsAtPosition(_editor.LanguageFeatures.DefinitionProvider, _editor.Model, position, false, cts.Token).ConfigureAwait(true);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            UnexpectedErrors.Report(error);
            return;
        }

        if (cts.IsCancellationRequested || results.Count == 0 || !state.Validate(_editor) || !ReferenceEquals(_currentWordAtPosition, word))
        {
            if (!cts.IsCancellationRequested)
            {
                RemoveLinkDecorations();
            }

            return;
        }

        var linkRange = results[0].OriginSelectionRange ?? new TextRange(position.LineNumber, word.StartColumn, position.LineNumber, word.EndColumn);
        if (results.Count > 1)
        {
            // several results: the link covers every origin range
            foreach (var result in results)
            {
                if (result.OriginSelectionRange is { } origin)
                {
                    linkRange = linkRange.PlusRange(origin);
                }
            }
        }

        _linkDecorations = _editor.Model.DeltaDecorations(_linkDecorations,
            [new ModelDeltaDecoration(linkRange, new ModelDecorationOptions("goto-definition-link") { InlineClassName = LinkClassName })]);
        if (_lastMouseMoveEvent?.Target.Position is { } mouse && linkRange.ContainsPosition(mouse) && _editor.View is { } view)
        {
            view.Cursor = s_hand;
        }
    }

    private void RemoveLinkDecorations()
    {
        if (_linkDecorations.Length > 0 && !_editor.Model.IsDisposed())
        {
            _linkDecorations = _editor.Model.DeltaDecorations(_linkDecorations, []);
            if (_editor.View is { } view)
            {
                view.ClearValue(InputElement.CursorProperty);
            }
        }
    }

    private async Task GotoDefinition(TextPosition position, bool openToSide)
    {
        _editor.SetPosition(position);
        var action = openToSide ? GoToCommands.OpenDefinitionToSide : GoToCommands.GoToDefinition;
        var muted = new SymbolNavigationAction(action.Id, action.Label, null, new SymbolNavigationActionConfig(openToSide, false, MuteMessage: true), GoToCommands.DefinitionsKind);
        await muted.Run(_editor, null).ConfigureAwait(true);
    }
}
