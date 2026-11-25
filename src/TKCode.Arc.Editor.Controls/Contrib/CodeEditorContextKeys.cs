// Context keys of the editor features (upstream defines each in its contrib module, e.g. CONTEXT_FIND_WIDGET_VISIBLE
// in contrib/find/browser/findModel.ts and SuggestContext.Visible in contrib/suggest/browser/suggest.ts; commit
// b57d5c9e, MIT license). The keys the editor itself sets are TKCode.Arc.Editor.EditorContextKeys (engine); these are
// the ones features set and read across each other (an Escape keybinding of one feature checks that another's widget is
// not visible, ...). The ids and defaults are upstream's.
//
// ONE LINE PER KEY: a feature adds the keys it owns here (or keeps keys only it reads in its own file). Bind a key to
// an editor with CodeEditor.CreateContextKey(key) and set it as the widget shows and hides.

using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>The context keys of the editor features (upstream's contrib RawContextKeys).</summary>
internal static class CodeEditorContextKeys
{
    public static readonly RawContextKey<bool> FindWidgetVisible = new("findWidgetVisible", false, "Whether the find widget is visible");
    public static readonly RawContextKey<bool> FindInputFocussed = new("findInputFocussed", false, "Whether the find input is focused");
    public static readonly RawContextKey<bool> ReplaceInputFocussed = new("replaceInputFocussed", false, "Whether the replace input is focused");
    public static readonly RawContextKey<bool> SuggestWidgetVisible = new("suggestWidgetVisible", false, "Whether suggestion are visible");
    public static readonly RawContextKey<bool> ParameterHintsVisible = new("parameterHintsVisible", false);
    public static readonly RawContextKey<bool> RenameInputVisible = new("renameInputVisible", false, "Whether the rename input widget is visible");
    public static readonly RawContextKey<bool> ReferenceSearchVisible = new("referenceSearchVisible", false, "Whether reference peek is visible, like 'Peek References' or 'Peek Definition'");
    public static readonly RawContextKey<bool> InlineSuggestionVisible = new("inlineSuggestionVisible", false, "Whether an inline suggestion is visible");
    public static readonly RawContextKey<bool> MarkersNavigationVisible = new("markersNavigationVisible", false);
    public static readonly RawContextKey<bool> InSnippetMode = new("inSnippetMode", false, "Whether the editor in current in snippet mode");
    public static readonly RawContextKey<bool> HasWordHighlights = new("hasWordHighlights", false);
    public static readonly RawContextKey<bool> FoldingEnabled = new("foldingEnabled", false);
    public static readonly RawContextKey<bool> CodeActionMenuVisible = new("codeActionMenuVisible", false, "Whether the action widget list is visible");
}
