// Ported from vscode (src/vs/editor/contrib/multicursor/browser/multicursor.ts: the editor actions' ids, labels,
// keybindings and preconditions, and the registrations of MultiCursorSelectionController and SelectionHighlighter;
// commit b57d5c9e), MIT license. Their logic is the engine's (TKCode.Arc.Editor.Contrib.MultiCursor); this file binds
// it to the CodeEditor.
// Differences:
//  - the controller contribution also holds the feature's options and, for an editor without a find feature, the
//    find state the actions share (upstream creates a temporary CommonFindController per action);
//  - menus (the Selection menu bar entries) and aria announcements are not ported.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.MultiCursor;
using TKCode.Arc.Editor.Contrib.WordHighlighter;
using TKCode.Arc.Editor.Platform;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>The editor's multi-cursor controller (upstream's MultiCursorSelectionController contribution).</summary>
internal sealed class MultiCursorContribution : ICodeEditorContribution
{
    public const string Id = MultiCursorSelectionController.Id;

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new MultiCursorContribution(editor), EditorContributionInstantiation.Lazy);

    private readonly CodeEditor _editor;
    private readonly CodeEditorMultiCursorAdapter _adapter;
    private MultiCursorOptions _options = MultiCursorOptions.Default;

    private MultiCursorContribution(CodeEditor editor)
    {
        _editor = editor;
        _adapter = new CodeEditorMultiCursorAdapter(editor);
        Controller = new MultiCursorSelectionController(_adapter);
    }

    /// <summary>The engine controller.</summary>
    public MultiCursorSelectionController Controller { get; }

    /// <summary>The find state of an editor without a find feature.</summary>
    public StandaloneMultiCursorFindController StandaloneFindController { get; } = new();

    /// <summary>Raised after <see cref="Options"/> changed, with the name of the option (upstream's onDidChangeConfiguration).</summary>
    public event Action<string>? OptionsChanged;

    /// <summary>The feature's options (upstream's selectedTextMatchMode, selectionHighlight*, occurrencesHighlight).</summary>
    public MultiCursorOptions Options
    {
        get => _options;
        set
        {
            var old = _options;
            _options = value;
            if (old.SelectedTextMatchMode != value.SelectedTextMatchMode)
            {
                Raise(MultiCursorOptionNames.SelectedTextMatchMode);
            }

            if (old.SelectionHighlight != value.SelectionHighlight
                || old.SelectionHighlightMultiline != value.SelectionHighlightMultiline
                || old.SelectionHighlightMaxLength != value.SelectionHighlightMaxLength)
            {
                Raise(MultiCursorOptionNames.SelectionHighlight);
            }
        }
    }

    private void Raise(string option)
    {
        _adapter.RaiseConfigurationChanged(option);
        OptionsChanged?.Invoke(option);
    }

    public static MultiCursorContribution? Get(CodeEditor editor) => editor.GetContribution<MultiCursorContribution>(Id);

    public void Dispose()
    {
        Controller.Dispose();
        _adapter.Dispose();
    }

    // ---- actions (upstream's EditorAction subclasses in multicursor.ts)

    private static void WithAdapter(CodeEditor editor, Action<IMultiCursorEditor> run)
    {
        using var adapter = new CodeEditorMultiCursorAdapter(editor);
        run(adapter);
    }

    private static bool LogicalLine(object? args) => args is not MultiCursorArgs { LogicalLine: false };

    private static string? Source(object? args) => args is MultiCursorArgs { Source: { } source } ? source : "keyboard";

    public static readonly EditorAction InsertCursorAbove = EditorAction.Create(
        MultiCursorActions.InsertCursorAboveId,
        "Add Cursor Above",
        null,
        (editor, args) => WithAdapter(editor, a => MultiCursorActions.InsertCursorAbove(a, Source(args), LogicalLine(args))),
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Alt | (int)EditorKeyCode.UpArrow)
        {
            When = EditorContextKeys.EditorTextFocus,
            Linux = new PlatformKeybindings
            {
                Primary = KeyMod.Shift | KeyMod.Alt | (int)EditorKeyCode.UpArrow,
                Secondary = [KeyMod.CtrlCmd | KeyMod.Shift | (int)EditorKeyCode.UpArrow],
            },
        });

    public static readonly EditorAction InsertCursorBelow = EditorAction.Create(
        MultiCursorActions.InsertCursorBelowId,
        "Add Cursor Below",
        null,
        (editor, args) => WithAdapter(editor, a => MultiCursorActions.InsertCursorBelow(a, Source(args), LogicalLine(args))),
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Alt | (int)EditorKeyCode.DownArrow)
        {
            When = EditorContextKeys.EditorTextFocus,
            Linux = new PlatformKeybindings
            {
                Primary = KeyMod.Shift | KeyMod.Alt | (int)EditorKeyCode.DownArrow,
                Secondary = [KeyMod.CtrlCmd | KeyMod.Shift | (int)EditorKeyCode.DownArrow],
            },
        });

    public static readonly EditorAction InsertCursorAtEndOfEachLineSelected = EditorAction.Create(
        MultiCursorActions.InsertCursorAtEndOfEachLineSelectedId,
        "Add Cursors to Line Ends",
        null,
        (editor, _) => WithAdapter(editor, MultiCursorActions.InsertCursorAtEndOfEachLineSelected),
        new EditorKeybinding(KeyMod.Shift | KeyMod.Alt | (int)EditorKeyCode.KeyI) { When = EditorContextKeys.EditorTextFocus });

    public static readonly EditorAction AddCursorsToBottom = EditorAction.Create(
        MultiCursorActions.AddCursorsToBottomId,
        "Add Cursors To Bottom",
        null,
        (editor, _) => WithAdapter(editor, MultiCursorActions.AddCursorsToBottom));

    public static readonly EditorAction AddCursorsToTop = EditorAction.Create(
        MultiCursorActions.AddCursorsToTopId,
        "Add Cursors To Top",
        null,
        (editor, _) => WithAdapter(editor, MultiCursorActions.AddCursorsToTop));

    public static readonly EditorAction AddSelectionToNextFindMatch = EditorAction.Create(
        MultiCursorActions.AddSelectionToNextFindMatchId,
        "Add Selection To Next Find Match",
        null,
        (editor, _) => WithAdapter(editor, MultiCursorActions.AddSelectionToNextFindMatch),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyD) { When = EditorContextKeys.Focus });

    public static readonly EditorAction AddSelectionToPreviousFindMatch = EditorAction.Create(
        MultiCursorActions.AddSelectionToPreviousFindMatchId,
        "Add Selection To Previous Find Match",
        null,
        (editor, _) => WithAdapter(editor, MultiCursorActions.AddSelectionToPreviousFindMatch));

    public static readonly EditorAction MoveSelectionToNextFindMatch = EditorAction.Create(
        MultiCursorActions.MoveSelectionToNextFindMatchId,
        "Move Last Selection To Next Find Match",
        null,
        (editor, _) => WithAdapter(editor, MultiCursorActions.MoveSelectionToNextFindMatch),
        new EditorKeybinding(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyK, KeyMod.CtrlCmd | (int)EditorKeyCode.KeyD)) { When = EditorContextKeys.Focus });

    public static readonly EditorAction MoveSelectionToPreviousFindMatch = EditorAction.Create(
        MultiCursorActions.MoveSelectionToPreviousFindMatchId,
        "Move Last Selection To Previous Find Match",
        null,
        (editor, _) => WithAdapter(editor, MultiCursorActions.MoveSelectionToPreviousFindMatch));

    public static readonly EditorAction SelectHighlights = EditorAction.Create(
        MultiCursorActions.SelectHighlightsId,
        "Select All Occurrences of Find Match",
        null,
        (editor, _) => WithAdapter(editor, MultiCursorActions.SelectHighlights),
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Shift | (int)EditorKeyCode.KeyL) { When = EditorContextKeys.Focus });

    public static readonly EditorAction ChangeAll = EditorAction.Create(
        MultiCursorActions.ChangeAllId,
        "Change All Occurrences",
        ContextKeyExpr.And(EditorContextKeys.Writable, EditorContextKeys.EditorTextFocus),
        (editor, _) => WithAdapter(editor, MultiCursorActions.SelectHighlights),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)EditorKeyCode.F2) { When = EditorContextKeys.EditorTextFocus });

    public static readonly EditorAction FocusNextCursor = EditorAction.Create(
        MultiCursorActions.FocusNextCursorId,
        "Focus Next Cursor",
        null,
        (editor, args) => WithAdapter(editor, a => MultiCursorActions.FocusNextCursor(a, Source(args))));

    public static readonly EditorAction FocusPreviousCursor = EditorAction.Create(
        MultiCursorActions.FocusPreviousCursorId,
        "Focus Previous Cursor",
        null,
        (editor, args) => WithAdapter(editor, a => MultiCursorActions.FocusPreviousCursor(a, Source(args))));
}

/// <summary>The arguments of the insert cursor actions (upstream's <c>{ source, logicalLine }</c>).</summary>
internal sealed record MultiCursorArgs
{
    public string? Source { get; init; }

    public bool LogicalLine { get; init; } = true;
}

/// <summary>Highlights the other occurrences of the selected text (upstream's SelectionHighlighter contribution).</summary>
internal sealed class SelectionHighlighterContribution : ICodeEditorContribution
{
    public const string Id = SelectionHighlighter.Id;

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new SelectionHighlighterContribution(editor), EditorContributionInstantiation.AfterFirstRender);

    private readonly CodeEditorMultiCursorAdapter _adapter;
    private readonly IDisposable _style;
    private readonly MultiCursorContribution? _options;

    private SelectionHighlighterContribution(CodeEditor editor)
    {
        _adapter = new CodeEditorMultiCursorAdapter(editor);

        // the options live on the multi-cursor controller; its changes reach the highlighter through this adapter
        _options = MultiCursorContribution.Get(editor);
        _options?.OptionsChanged += _adapter.RaiseConfigurationChanged;

        _style = editor.DecorationStyles.Register(
            HighlightDecorations.SelectionHighlightClassName,
            new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.SelectionHighlight });
        Highlighter = new SelectionHighlighter(
            _adapter,
            model => editor.LanguageFeatures.DocumentHighlightProvider.Has(model),
            CodeEditorMultiCursorAdapter.RunAfter);
    }

    /// <summary>The engine highlighter.</summary>
    public SelectionHighlighter Highlighter { get; }

    public static SelectionHighlighterContribution? Get(CodeEditor editor) => editor.GetContribution<SelectionHighlighterContribution>(Id);

    public void Dispose()
    {
        _options?.OptionsChanged -= _adapter.RaiseConfigurationChanged;

        Highlighter.Dispose();
        _adapter.Dispose();
        _style.Dispose();
    }
}
