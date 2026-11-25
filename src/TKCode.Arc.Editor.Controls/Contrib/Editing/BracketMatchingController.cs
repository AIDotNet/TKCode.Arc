// Ported from vscode (src/vs/editor/contrib/bracketMatching/browser/bracketMatching.ts, commit b57d5c9e), MIT license:
// BracketMatchingController's editor wiring (the 50 ms update scheduler, the listeners, the decorations collection)
// and the three actions (jumpToBracket, selectToBracket, removeBrackets); and the bracket-match class of
// bracketMatching.css.
// Differences from upstream:
//  - the brackets themselves are computed by the engine (BracketMatchingState); the actions call the engine's
//    BracketMatching methods on the editor directly (upstream goes through the controller, which has the same code);
//  - the style of the bracket-match class is a decoration style registration (background BracketMatchBrush, a 1 px
//    BracketMatchBorderBrush border); the overview ruler mark is part of the decoration options and shows once the
//    editor draws an overview ruler;
//  - "the editor widget has focus" is IsKeyboardFocusWithin; focus changes come from the editor's text focus event.

using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.BracketMatching;
using TKCode.Arc.Editor.Model;
using BracketMatchingImpl = TKCode.Arc.Editor.Contrib.BracketMatching.BracketMatching;

namespace TKCode.Arc;

/// <summary>Highlights the bracket matching the one at each cursor (upstream's BracketMatchingController).</summary>
internal sealed class BracketMatchingController : ICodeEditorContribution
{
    public const string Id = "editor.contrib.bracketMatchingController";

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new BracketMatchingController(editor), EditorContributionInstantiation.AfterFirstRender);

    private static readonly TimeSpan s_updateDelay = TimeSpan.FromMilliseconds(50);

    private readonly CodeEditor _editor;
    private readonly BracketMatchingState _state = new();
    private readonly DispatcherTimer _updateBracketsSoon;
    private readonly IDisposable _style;
    private string[] _decorations = [];
    private bool _disposed;

    private BracketMatchingController(CodeEditor editor)
    {
        _editor = editor;
        _style = editor.DecorationStyles.Register(BracketMatchingImpl.BracketMatchClassName, new CodeEditorDecorationStyle
        {
            Background = CodeEditorThemeColor.BracketMatch,
            Border = CodeEditorThemeColor.BracketMatchBorder,
            BorderThickness = 1,
        });

        _updateBracketsSoon = new DispatcherTimer(s_updateDelay, DispatcherPriority.Background, (_, _) =>
        {
            _updateBracketsSoon!.Stop();
            UpdateBrackets();
        });

        editor.DidChangeCursorSelection += OnCursorSelectionChanged;
        editor.DidChangeModelContent += OnModelContentChanged;
        editor.DidChangeModelLanguage += OnModelLanguageChanged;
        editor.LanguageConfigurationService.DidChange += OnLanguageConfigurationChanged;
        editor.DidChangeEditingOptions += OnEditingOptionsChanged;
        editor.DidChangeTextFocus += OnFocusChanged;

        ScheduleUpdate();
    }

    public static BracketMatchingController? Get(CodeEditor editor) => editor.GetContribution<BracketMatchingController>(Id);

    /// <summary>The bracket ranges the editor highlights now (for tests).</summary>
    internal IReadOnlyList<TextRange> HighlightedRanges =>
        _editor.Model.IsDisposed() ? [] : [.. _decorations.Select(id => _editor.Model.GetDecorationRange(id)).OfType<TextRange>()];

    private void ScheduleUpdate()
    {
        if (_disposed)
        {
            return;
        }

        _updateBracketsSoon.Stop();
        _updateBracketsSoon.Start();
    }

    private void OnCursorSelectionChanged(Editor.ViewModel.CursorStateChangedEvent e)
    {
        if (_editor.EditingOptions.MatchBrackets == MatchBracketsMode.Never)
        {
            // Early exit if nothing needs to be done!
            // Leave some form of early exit check here if you wish to continue being a cursor position change listener ;)
            return;
        }

        ScheduleUpdate();
    }

    private void OnModelContentChanged(ModelContentChangedEvent e) => ScheduleUpdate();

    private void OnModelLanguageChanged(ModelLanguageChangedEvent e)
    {
        _state.Reset();
        ScheduleUpdate();
    }

    private void OnLanguageConfigurationChanged(Editor.Languages.LanguageConfigurationServiceChangeEvent e)
    {
        _state.Reset();
        ScheduleUpdate();
    }

    private void OnEditingOptionsChanged(CodeEditorEditingOptions options)
    {
        // matchBrackets changed (or another editing option): clear and recompute
        SetDecorations([]);
        _state.Reset();
        ScheduleUpdate();
    }

    private void OnFocusChanged(bool focused) => ScheduleUpdate();

    /// <summary>Recomputes the brackets now (upstream's _updateBrackets; the scheduler calls it 50 ms after a change).</summary>
    internal void UpdateBrackets()
    {
        if (_disposed || _editor.Model.IsDisposed())
        {
            return;
        }

        var hasWidgetFocus = _editor.IsTextFocused || _editor.IsKeyboardFocusWithin;
        if (_state.Update(_editor, hasWidgetFocus, _editor.EditingOptions.MatchBrackets) is { } newDecorations)
        {
            SetDecorations(newDecorations);
        }
    }

    private void SetDecorations(IReadOnlyList<ModelDeltaDecoration> decorations)
    {
        if (_editor.Model.IsDisposed())
        {
            _decorations = [];
            return;
        }

        _decorations = _editor.Model.DeltaDecorations(_decorations, decorations);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _updateBracketsSoon.Stop();
        _editor.DidChangeCursorSelection -= OnCursorSelectionChanged;
        _editor.DidChangeModelContent -= OnModelContentChanged;
        _editor.DidChangeModelLanguage -= OnModelLanguageChanged;
        _editor.LanguageConfigurationService.DidChange -= OnLanguageConfigurationChanged;
        _editor.DidChangeEditingOptions -= OnEditingOptionsChanged;
        _editor.DidChangeTextFocus -= OnFocusChanged;
        SetDecorations([]);
        _style.Dispose();
    }

    // ------ the actions

    /// <summary>editor.action.jumpToBracket (Ctrl+Shift+\).</summary>
    public static readonly EditorAction JumpToBracketAction = EditorAction.Create(
        "editor.action.jumpToBracket", "Go to Bracket", null,
        (editor, _) => BracketMatchingImpl.JumpToBracket(editor),
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.Backslash) { When = EditorContextKeys.EditorTextFocus });

    /// <summary>editor.action.selectToBracket; the argument <see cref="SelectToBracketArgs"/> { SelectBrackets: false } selects only the inside.</summary>
    public static readonly EditorAction SelectToBracketAction = EditorAction.Create(
        "editor.action.selectToBracket", "Select to Bracket", null,
        (editor, args) => BracketMatchingImpl.SelectToBracket(editor, args is not SelectToBracketArgs { SelectBrackets: false }));

    /// <summary>editor.action.removeBrackets (Ctrl+Alt+Backspace).</summary>
    public static readonly EditorAction RemoveBracketsAction = EditorAction.Create(
        "editor.action.removeBrackets", "Remove Brackets", null,
        (editor, _) => BracketMatchingImpl.RemoveBrackets(editor, "editor.action.removeBrackets"),
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.Backspace) { When = EditorContextKeys.EditorTextFocus });

    /// <summary>The argument of selectToBracket (upstream's { selectBrackets }).</summary>
    internal sealed record SelectToBracketArgs(bool SelectBrackets);
}
