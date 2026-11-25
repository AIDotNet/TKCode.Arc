// Ported from vscode (src/vs/editor/contrib/find/browser/findController.ts, commit b57d5c9e), MIT license.
// This file: the widget-owning FindController, the find actions and commands with their keybindings (and the toggle
// keybindings of findModel.ts). The controller logic and the bodies of the actions are the engine's
// (TKCode.Arc.Editor.Contrib.Find: CommonFindController, FindActions).
// Differences:
//  - StartFindAction and StartFindReplaceAction are editor actions (upstream: MultiEditorActions that also serve the
//    workbench); their precondition is upstream's without the workbench's editorIsOpen;
//  - MoveToMatchFindAction (a quick input to go to match N) is not registered; the controller's GoToMatch is there;
//  - the view state (saveViewState / restoreViewState) is not kept.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Find;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>The labels of the find keybindings for the widget's tooltips, for the current platform.</summary>
internal static class FindKeybindings
{
    private static bool Mac => OperatingSystem.IsMacOS();

    public static string ToggleCaseSensitive => Mac ? "⌥⌘C" : "Alt+C";

    public static string ToggleWholeWord => Mac ? "⌥⌘W" : "Alt+W";

    public static string ToggleRegex => Mac ? "⌥⌘R" : "Alt+R";

    public static string ToggleSearchScope => Mac ? "⌥⌘L" : "Alt+L";

    public static string TogglePreserveCase => Mac ? "⌥⌘P" : "Alt+P";

    public static string PreviousMatch => "Shift+Enter";

    public static string NextMatch => "Enter";

    public static string ReplaceOne => Mac ? "⇧⌘1" : "Ctrl+Shift+1";

    public static string ReplaceAll => Mac ? "⌥⌘Enter" : "Ctrl+Alt+Enter";
}

/// <summary>
/// Find and replace in a <see cref="CodeEditor"/> (upstream's FindController): the engine's controller with the find
/// widget, created when find first starts.
/// </summary>
internal sealed class FindController : CommonFindController, ICodeEditorContribution
{
    public static readonly EditorContributionDescription Description =
        new(Id, static editor => new FindController(editor), EditorContributionInstantiation.Eager);

    private readonly CodeEditor _editor;
    private readonly CodeEditorFindEditor _findEditor;
    private readonly IDisposable[] _styles;
    private CodeEditorFindWidget? _widget;

    private FindController(CodeEditor editor)
        : this(editor, new CodeEditorFindEditor(editor))
    {
    }

    private FindController(CodeEditor editor, CodeEditorFindEditor findEditor)
        : base(findEditor)
    {
        _editor = editor;
        _findEditor = findEditor;
        _styles = RegisterStyles(editor.DecorationStyles);
    }

    // upstream's findWidget.css and the theme colors it names: every match in editor.findMatchHighlightBackground, the
    // current one in editor.findMatchBackground, the line of the current match in editor.rangeHighlightBackground and
    // the search scope in editor.findRangeHighlightBackground
    private static IDisposable[] RegisterStyles(CodeEditorDecorationStyles styles) =>
    [
        styles.Register("findMatch", new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.FindMatchHighlight }),
        styles.Register("currentFindMatch", new CodeEditorDecorationStyle
        {
            Background = CodeEditorThemeColor.FindMatch,
            Border = CodeEditorThemeColor.Warning,
            BorderThickness = 1,
        }),
        styles.Register("rangeHighlight", new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.RangeHighlight }),
        styles.Register("findScope", new CodeEditorDecorationStyle { Background = CodeEditorThemeColor.FindRangeHighlight }),
    ];

    /// <summary>The editor's find controller, or null when the editor has none.</summary>
    public static FindController? Get(CodeEditor editor) => editor.GetContribution<FindController>(Id);

    /// <summary>The editor as find sees it (its find options can be changed here).</summary>
    public CodeEditorFindEditor FindEditor => _findEditor;

    /// <summary>The find widget, once find has started.</summary>
    internal CodeEditorFindWidget? Widget => _widget;

    protected override async Task StartCore(FindStartOptions opts, NewFindReplaceState? newState)
    {
        _widget ??= CreateFindWidget();

        var selection = _editor.GetSelection();
        var updateSearchScope = _findEditor.FindOptions.AutoFindInSelection switch
        {
            AutoFindInSelection.Always => true,
            AutoFindInSelection.Multiline => selection is { } s && s.StartLineNumber != s.EndLineNumber,
            _ => false,
        };

        opts = opts with { UpdateSearchScope = opts.UpdateSearchScope || updateSearchScope };

        await base.StartCore(opts, newState);

        if (opts.ShouldFocus == FindStartFocusAction.FocusReplaceInput)
        {
            _widget.FocusReplaceInput();
        }
        else if (opts.ShouldFocus == FindStartFocusAction.FocusFindInput)
        {
            _widget.FocusFindInput();
        }
    }

    public override void HighlightFindOptions(bool ignoreWhenVisible = false)
    {
        _widget ??= CreateFindWidget();
        if (State.IsRevealed && !ignoreWhenVisible)
        {
            _widget.HighlightFindOptions();
        }
    }

    public override bool WasReplaceInputLastFocused() => _widget?.LastFocusedInputWasReplace ?? false;

    public override void FocusLastElement() => _widget?.FocusLastElement();

    private CodeEditorFindWidget CreateFindWidget()
    {
        var widget = new CodeEditorFindWidget(_editor, this, State);
        _editor.AddOverlayWidget(widget);
        return widget;
    }

    public override void Dispose()
    {
        if (_widget is { } widget)
        {
            widget.Dispose();
            _editor.RemoveOverlayWidget(widget);
            _widget = null;
        }

        base.Dispose();
        _findEditor.Dispose();
        foreach (var style in _styles)
        {
            style.Dispose();
        }
    }

    // ----- actions and commands (upstream's registrations at the end of findController.ts)

    private static Task WithController(CodeEditor editor, Func<FindController, Task> run) =>
        Get(editor) is { } controller ? run(controller) : Task.CompletedTask;

    private static EditorKeybinding Mac(int primary, int mac, params int[] macSecondary) => new(primary)
    {
        Mac = new PlatformKeybindings { Primary = mac, Secondary = macSecondary.Length > 0 ? macSecondary : null },
    };

    /// <summary>Ctrl+F (Cmd+F): shows the find widget, seeded from the selection.</summary>
    public static readonly EditorAction StartFindAction = EditorAction.Create(
        FindIds.StartFindAction, "Find", EditorContextKeys.Focus,
        (editor, _) => WithController(editor, FindActions.StartFind),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)KeyCode.KeyF));

    /// <summary>Starts find with a search string and options given as <see cref="FindStartArguments"/>.</summary>
    public static readonly EditorAction StartFindWithArgsAction = EditorAction.Create(
        FindIds.StartFindWithArgs, "Find with Arguments", null,
        (editor, args) => WithController(editor, controller => FindActions.StartFindWithArgs(controller, args as FindStartArguments)));

    /// <summary>Cmd+E on macOS: finds the selection without showing the widget's focus.</summary>
    public static readonly EditorAction StartFindWithSelectionAction = EditorAction.Create(
        FindIds.StartFindWithSelection, "Find with Selection", null,
        (editor, _) => WithController(editor, FindActions.StartFindWithSelection),
        new EditorKeybinding(0) { Mac = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | (int)KeyCode.KeyE } });

    /// <summary>F3 (Cmd+G), and Enter in the find input: the next match.</summary>
    public static readonly EditorAction NextMatchFindAction = EditorAction.Create(
        FindIds.NextMatchFindAction, "Find Next", null,
        (editor, _) => WithController(editor, controller => FindActions.MatchFind(controller, next: true)),
        Mac((int)KeyCode.F3, KeyMod.CtrlCmd | (int)KeyCode.KeyG, (int)KeyCode.F3) with { When = EditorContextKeys.Focus },
        new EditorKeybinding((int)KeyCode.Enter) { When = ContextKeyExpr.And(EditorContextKeys.Focus, CodeEditorContextKeys.FindInputFocussed) });

    /// <summary>Shift+F3 (Cmd+Shift+G), and Shift+Enter in the find input: the previous match.</summary>
    public static readonly EditorAction PreviousMatchFindAction = EditorAction.Create(
        FindIds.PreviousMatchFindAction, "Find Previous", null,
        (editor, _) => WithController(editor, controller => FindActions.MatchFind(controller, next: false)),
        Mac(KeyMod.Shift | (int)KeyCode.F3, KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyG, KeyMod.Shift | (int)KeyCode.F3) with { When = EditorContextKeys.Focus },
        new EditorKeybinding(KeyMod.Shift | (int)KeyCode.Enter) { When = ContextKeyExpr.And(EditorContextKeys.Focus, CodeEditorContextKeys.FindInputFocussed) });

    /// <summary>Ctrl+F3: the next match of the selection.</summary>
    public static readonly EditorAction NextSelectionMatchFindAction = EditorAction.Create(
        FindIds.NextSelectionMatchFindAction, "Find Next Selection", null,
        (editor, _) => WithController(editor, controller => FindActions.SelectionMatchFind(controller, next: true)),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)KeyCode.F3) { When = EditorContextKeys.Focus });

    /// <summary>Ctrl+Shift+F3: the previous match of the selection.</summary>
    public static readonly EditorAction PreviousSelectionMatchFindAction = EditorAction.Create(
        FindIds.PreviousSelectionMatchFindAction, "Find Previous Selection", null,
        (editor, _) => WithController(editor, controller => FindActions.SelectionMatchFind(controller, next: false)),
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.F3) { When = EditorContextKeys.Focus });

    /// <summary>Ctrl+H (Cmd+Alt+F): shows the find widget with the replace row.</summary>
    public static readonly EditorAction StartFindReplaceAction = EditorAction.Create(
        FindIds.StartFindReplaceAction, "Replace", EditorContextKeys.Focus,
        (editor, _) => WithController(editor, FindActions.StartFindReplace),
        Mac(KeyMod.CtrlCmd | (int)KeyCode.KeyH, KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.KeyF));

    private static EditorCommand FindCommand(string id, ContextKeyExpression? precondition, Action<FindController> handler, params EditorKeybinding[] keybindings) =>
        EditorCommand.BindToContribution<FindController>(Id, id, precondition, (controller, _) => handler(controller), keybindings);

    private static EditorKeybinding Contrib(int primary, ContextKeyExpression? when = null) =>
        new(primary) { Weight = KeybindingWeight.EditorContrib + 5, When = when ?? EditorContextKeys.Focus };

    private static EditorKeybinding Toggle(int key) =>
        Contrib(KeyMod.Alt | key) with { Mac = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | KeyMod.Alt | key } };

    /// <summary>Escape (and Shift+Escape) closes the widget.</summary>
    public static readonly EditorCommand CloseFindWidgetCommand = FindCommand(
        FindIds.CloseFindWidgetCommand, CodeEditorContextKeys.FindWidgetVisible, static c => c.CloseFindWidget(),
        Contrib((int)KeyCode.Escape) with { Secondary = [KeyMod.Shift | (int)KeyCode.Escape] });

    /// <summary>Alt+C (Cmd+Alt+C): match case.</summary>
    public static readonly EditorCommand ToggleCaseSensitiveCommand = FindCommand(
        FindIds.ToggleCaseSensitiveCommand, null, static c => c.ToggleCaseSensitive(), Toggle((int)KeyCode.KeyC));

    /// <summary>Alt+W (Cmd+Alt+W): whole word.</summary>
    public static readonly EditorCommand ToggleWholeWordCommand = FindCommand(
        FindIds.ToggleWholeWordCommand, null, static c => c.ToggleWholeWords(), Toggle((int)KeyCode.KeyW));

    /// <summary>Alt+R (Cmd+Alt+R): regular expression.</summary>
    public static readonly EditorCommand ToggleRegexCommand = FindCommand(
        FindIds.ToggleRegexCommand, null, static c => c.ToggleRegex(), Toggle((int)KeyCode.KeyR));

    /// <summary>Alt+L (Cmd+Alt+L): find in selection.</summary>
    public static readonly EditorCommand ToggleSearchScopeCommand = FindCommand(
        FindIds.ToggleSearchScopeCommand, null, static c => c.ToggleSearchScope(), Toggle((int)KeyCode.KeyL));

    /// <summary>Alt+P (Cmd+Alt+P): preserve case.</summary>
    public static readonly EditorCommand TogglePreserveCaseCommand = FindCommand(
        FindIds.TogglePreserveCaseCommand, null, static c => c.TogglePreserveCase(), Toggle((int)KeyCode.KeyP));

    /// <summary>Ctrl+Shift+1, and Enter in the replace input: replaces the current match.</summary>
    public static readonly EditorCommand ReplaceOneCommand = FindCommand(
        FindIds.ReplaceOneAction, CodeEditorContextKeys.FindWidgetVisible, static c => c.Replace(),
        Contrib(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.Digit1),
        Contrib((int)KeyCode.Enter, ContextKeyExpr.And(EditorContextKeys.Focus, CodeEditorContextKeys.ReplaceInputFocussed)));

    /// <summary>Ctrl+Alt+Enter, and Cmd+Enter in the replace input on macOS: replaces every match.</summary>
    public static readonly EditorCommand ReplaceAllCommand = FindCommand(
        FindIds.ReplaceAllAction, CodeEditorContextKeys.FindWidgetVisible, static c => c.ReplaceAll(),
        Contrib(KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.Enter),
        Contrib(0, ContextKeyExpr.And(EditorContextKeys.Focus, CodeEditorContextKeys.ReplaceInputFocussed)) with
        {
            Mac = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | (int)KeyCode.Enter },
        });

    /// <summary>Alt+Enter: selects every match.</summary>
    public static readonly EditorCommand SelectAllMatchesCommand = FindCommand(
        FindIds.SelectAllMatchesAction, CodeEditorContextKeys.FindWidgetVisible, static c => c.SelectAllMatches(),
        Contrib(KeyMod.Alt | (int)KeyCode.Enter));
}
