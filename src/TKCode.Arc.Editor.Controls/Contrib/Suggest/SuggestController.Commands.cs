// Ported from vscode (src/vs/editor/contrib/suggest/browser/suggestController.ts: TriggerSuggestAction and the
// SuggestCommand registrations, commit b57d5c9e), MIT license.
// Differences: the commands are listed in Contrib/CodeEditorContributions.cs (one line each); no menu entries (the
// status bar is not ported); not ported: insertBestCompletion, insertNextSuggestion, insertPrevSuggestion,
// toggleExplainMode, toggleSuggestionFocus, suggestWidgetCopy, editor.action.resetSuggestSize.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Platform;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

internal sealed partial class SuggestController
{
    /// <summary>Upstream's TriggerSuggestAction.id.</summary>
    public const string TriggerSuggestActionId = "editor.action.triggerSuggest";

    private const int Weight = KeybindingWeight.EditorContrib + 90;

    private static readonly ContextKeyExpression s_visible = CodeEditorContextKeys.SuggestWidgetVisible;

    private static readonly ContextKeyExpression? s_canMove = ContextKeyExpr.And(
        s_visible,
        ContextKeyExpr.Or(SuggestContext.MultipleSuggestions, SuggestContext.HasFocusedSuggestion.ToNegated()));

    /// <summary>Ctrl+Space (Mac: Ctrl+Space), with Ctrl+I (and Mac: Alt+Escape) as secondaries.</summary>
    private static EditorKeybinding CtrlSpace(ContextKeyExpression? when, int weight, bool macAltEscape) => new(KeyMod.CtrlCmd | (int)EditorKeyCode.Space)
    {
        Secondary = [KeyMod.CtrlCmd | (int)EditorKeyCode.KeyI],
        Mac = new PlatformKeybindings
        {
            Primary = KeyMod.WinCtrl | (int)EditorKeyCode.Space,
            Secondary = macAltEscape
                ? [KeyMod.Alt | (int)EditorKeyCode.Escape, KeyMod.CtrlCmd | (int)EditorKeyCode.KeyI]
                : [KeyMod.CtrlCmd | (int)EditorKeyCode.KeyI],
        },
        When = when,
        Weight = weight,
    };

    /// <summary>Asks for suggestions (Ctrl+Space; upstream's TriggerSuggestAction).</summary>
    public static readonly EditorAction TriggerSuggestAction = EditorAction.Create(
        TriggerSuggestActionId,
        "Trigger Suggest",
        ContextKeyExpr.And(EditorContextKeys.Writable, EditorContextKeys.HasCompletionItemProvider, CodeEditorContextKeys.SuggestWidgetVisible.ToNegated()),
        (editor, args) => Get(editor)?.TriggerSuggest(auto: args is SuggestTriggerArgs { Auto: true } ? true : null),
        CtrlSpace(EditorContextKeys.TextInputFocus, KeybindingWeight.EditorContrib, macAltEscape: true));

    /// <summary>Tab, or Enter when it makes an edit, accepts the focused suggestion.</summary>
    public static readonly EditorCommand AcceptSelectedSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "acceptSelectedSuggestion",
        ContextKeyExpr.And(s_visible, SuggestContext.HasFocusedSuggestion),
        (x, _) => x.AcceptSelectedSuggestion(true, false),
        new EditorKeybinding((int)EditorKeyCode.Tab) { When = ContextKeyExpr.And(s_visible, EditorContextKeys.TextInputFocus), Weight = Weight },
        new EditorKeybinding((int)EditorKeyCode.Enter)
        {
            When = ContextKeyExpr.And(s_visible, EditorContextKeys.TextInputFocus, SuggestContext.AcceptSuggestionsOnEnter, SuggestContext.MakesTextEdit),
            Weight = Weight,
        });

    /// <summary>Shift+Enter or Shift+Tab accepts with the other insert mode (insert vs. replace).</summary>
    public static readonly EditorCommand AcceptAlternativeSelectedSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "acceptAlternativeSelectedSuggestion",
        ContextKeyExpr.And(s_visible, EditorContextKeys.TextInputFocus, SuggestContext.HasFocusedSuggestion),
        (x, _) => x.AcceptSelectedSuggestion(false, true),
        new EditorKeybinding(KeyMod.Shift | (int)EditorKeyCode.Enter)
        {
            Secondary = [KeyMod.Shift | (int)EditorKeyCode.Tab],
            When = EditorContextKeys.TextInputFocus,
            Weight = Weight,
        });

    /// <summary>Escape hides the suggestions.</summary>
    public static readonly EditorCommand HideSuggestWidgetCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "hideSuggestWidget",
        s_visible,
        (x, _) => x.CancelSuggestWidget(),
        new EditorKeybinding((int)EditorKeyCode.Escape)
        {
            Secondary = [KeyMod.Shift | (int)EditorKeyCode.Escape],
            When = EditorContextKeys.TextInputFocus,
            Weight = Weight,
        });

    public static readonly EditorCommand SelectNextSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "selectNextSuggestion",
        s_canMove,
        (c, _) => c.SelectNextSuggestion(),
        new EditorKeybinding((int)EditorKeyCode.DownArrow)
        {
            Secondary = [KeyMod.CtrlCmd | (int)EditorKeyCode.DownArrow],
            Mac = new PlatformKeybindings
            {
                Primary = (int)EditorKeyCode.DownArrow,
                Secondary = [KeyMod.CtrlCmd | (int)EditorKeyCode.DownArrow, KeyMod.WinCtrl | (int)EditorKeyCode.KeyN],
            },
            When = EditorContextKeys.TextInputFocus,
            Weight = Weight,
        });

    public static readonly EditorCommand SelectNextPageSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "selectNextPageSuggestion",
        s_canMove,
        (c, _) => c.SelectNextPageSuggestion(),
        new EditorKeybinding((int)EditorKeyCode.PageDown)
        {
            Secondary = [KeyMod.CtrlCmd | (int)EditorKeyCode.PageDown],
            When = EditorContextKeys.TextInputFocus,
            Weight = Weight,
        });

    public static readonly EditorCommand SelectLastSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id, "selectLastSuggestion", s_canMove, (c, _) => c.SelectLastSuggestion());

    public static readonly EditorCommand SelectPrevSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "selectPrevSuggestion",
        s_canMove,
        (c, _) => c.SelectPrevSuggestion(),
        new EditorKeybinding((int)EditorKeyCode.UpArrow)
        {
            Secondary = [KeyMod.CtrlCmd | (int)EditorKeyCode.UpArrow],
            Mac = new PlatformKeybindings
            {
                Primary = (int)EditorKeyCode.UpArrow,
                Secondary = [KeyMod.CtrlCmd | (int)EditorKeyCode.UpArrow, KeyMod.WinCtrl | (int)EditorKeyCode.KeyP],
            },
            When = EditorContextKeys.TextInputFocus,
            Weight = Weight,
        });

    public static readonly EditorCommand SelectPrevPageSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "selectPrevPageSuggestion",
        s_canMove,
        (c, _) => c.SelectPrevPageSuggestion(),
        new EditorKeybinding((int)EditorKeyCode.PageUp)
        {
            Secondary = [KeyMod.CtrlCmd | (int)EditorKeyCode.PageUp],
            When = EditorContextKeys.TextInputFocus,
            Weight = Weight,
        });

    public static readonly EditorCommand SelectFirstSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id, "selectFirstSuggestion", s_canMove, (c, _) => c.SelectFirstSuggestion());

    /// <summary>Ctrl+Space while suggestions show and none is focused focuses the first.</summary>
    public static readonly EditorCommand FocusSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "focusSuggestion",
        ContextKeyExpr.And(s_visible, SuggestContext.HasFocusedSuggestion.ToNegated()),
        (x, _) => x.FocusSuggestion(),
        CtrlSpace(EditorContextKeys.TextInputFocus, Weight, macAltEscape: false));

    public static readonly EditorCommand FocusAndAcceptSuggestionCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "focusAndAcceptSuggestion",
        ContextKeyExpr.And(s_visible, SuggestContext.HasFocusedSuggestion.ToNegated()),
        (c, _) =>
        {
            c.FocusSuggestion();
            c.AcceptSelectedSuggestion(true, false);
        });

    /// <summary>Ctrl+Space while a suggestion is focused shows or hides its details.</summary>
    public static readonly EditorCommand ToggleSuggestionDetailsCommand = EditorCommand.BindToContribution<SuggestController>(
        Id,
        "toggleSuggestionDetails",
        ContextKeyExpr.And(s_visible, SuggestContext.HasFocusedSuggestion),
        (x, _) => x.ToggleSuggestionDetails(),
        CtrlSpace(EditorContextKeys.TextInputFocus, Weight, macAltEscape: false));
}

/// <summary>The arguments of <c>editor.action.triggerSuggest</c> (upstream's <c>{ auto: boolean }</c>).</summary>
internal sealed record SuggestTriggerArgs(bool Auto);
