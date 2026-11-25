// Ported from vscode (src/vs/editor/common/config/editorOptions.ts: the options the suggest contribution reads -
// suggest (InternalSuggestOptions), quickSuggestions, quickSuggestionsDelay, suggestOnTriggerCharacters,
// acceptSuggestionOnEnter, acceptSuggestionOnCommitCharacter, snippetSuggestions, suggestSelection, tabCompletion and
// wordBasedSuggestions; commit b57d5c9e), MIT license.
// Differences from upstream: one record with upstream's names and defaults instead of EditorOption entries read with
// getOption; string enums are C# enums. Options the widget does not have (showStatusBar, preview,
// previewMode, shareSuggestSelections, showInlineDetails) are left out.

using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>Insert the suggestion, or replace the word right of the cursor too (upstream's suggest.insertMode).</summary>
internal enum SuggestInsertMode
{
    Insert,
    Replace,
}

/// <summary>Whether a suggestion is focused when the list shows automatically (upstream's suggest.selectionMode).</summary>
internal enum SuggestSelectionMode
{
    Always,
    Never,
    WhenTriggerCharacter,
    WhenQuickSuggestion,
}

/// <summary>Quick suggestions in one kind of token (upstream's QuickSuggestionsValue).</summary>
internal enum QuickSuggestionsValue
{
    On,
    Inline,
    Off,
    OffWhenInlineCompletions,
}

/// <summary>Where snippets sort (upstream's snippetSuggestions).</summary>
internal enum SnippetSuggestions
{
    Top,
    Bottom,
    Inline,
    None,
}

/// <summary>When Enter accepts a suggestion (upstream's acceptSuggestionOnEnter).</summary>
internal enum AcceptSuggestionOnEnter
{
    On,
    Smart,
    Off,
}

/// <summary>Which suggestion is selected when the list shows (upstream's suggestSelection, MemMode).</summary>
internal enum SuggestSelection
{
    First,
    RecentlyUsed,
    RecentlyUsedByPrefix,
}

/// <summary>Words from the document as completions (upstream's wordBasedSuggestions; only the current document here).</summary>
internal enum WordBasedSuggestions
{
    Off,
    CurrentDocument,
}

/// <summary>Quick suggestions per token type (upstream's InternalQuickSuggestionsOptions).</summary>
internal sealed record QuickSuggestionsOptions(QuickSuggestionsValue Other, QuickSuggestionsValue Comments, QuickSuggestionsValue Strings)
{
    /// <summary>Upstream's default: other offWhenInlineCompletions, comments and strings off.</summary>
    public static readonly QuickSuggestionsOptions Default = new(QuickSuggestionsValue.OffWhenInlineCompletions, QuickSuggestionsValue.Off, QuickSuggestionsValue.Off);

    public static bool IsAllOff(QuickSuggestionsOptions config) =>
        config.Other == QuickSuggestionsValue.Off && config.Comments == QuickSuggestionsValue.Off && config.Strings == QuickSuggestionsValue.Off;

    public static bool IsAllOn(QuickSuggestionsOptions config) =>
        config.Other == QuickSuggestionsValue.On && config.Comments == QuickSuggestionsValue.On && config.Strings == QuickSuggestionsValue.On;

    public static QuickSuggestionsValue ValueFor(QuickSuggestionsOptions config, StandardTokenType tokenType) => tokenType switch
    {
        StandardTokenType.Comment => config.Comments,
        StandardTokenType.String => config.Strings,
        _ => config.Other,
    };
}

/// <summary>The options of the suggest feature, with upstream's defaults.</summary>
internal sealed record SuggestOptions
{
    public static readonly SuggestOptions Default = new();

    // ---- editor.suggest (InternalSuggestOptions)

    public SuggestInsertMode InsertMode { get; init; } = SuggestInsertMode.Insert;

    /// <summary>Filtering and sorting account for small typos.</summary>
    public bool FilterGraceful { get; init; } = true;

    public bool SnippetsPreventQuickSuggestions { get; init; }

    /// <summary>Sort words near the cursor first.</summary>
    public bool LocalityBonus { get; init; }

    public SuggestSelectionMode SelectionMode { get; init; } = SuggestSelectionMode.Always;

    /// <summary>Backspace brings back suggestions that closed because nothing matched.</summary>
    public bool ReopenOnDelete { get; init; }

    public bool ShowIcons { get; init; } = true;

    /// <summary>The first typed character must match the start of a word.</summary>
    public bool MatchOnWordStartOnly { get; init; } = true;

    public bool ShowMethods { get; init; } = true;
    public bool ShowFunctions { get; init; } = true;
    public bool ShowConstructors { get; init; } = true;
    public bool ShowDeprecated { get; init; } = true;
    public bool ShowFields { get; init; } = true;
    public bool ShowVariables { get; init; } = true;
    public bool ShowClasses { get; init; } = true;
    public bool ShowStructs { get; init; } = true;
    public bool ShowInterfaces { get; init; } = true;
    public bool ShowModules { get; init; } = true;
    public bool ShowProperties { get; init; } = true;
    public bool ShowEvents { get; init; } = true;
    public bool ShowOperators { get; init; } = true;
    public bool ShowUnits { get; init; } = true;
    public bool ShowValues { get; init; } = true;
    public bool ShowConstants { get; init; } = true;
    public bool ShowEnums { get; init; } = true;
    public bool ShowEnumMembers { get; init; } = true;
    public bool ShowKeywords { get; init; } = true;
    public bool ShowWords { get; init; } = true;
    public bool ShowColors { get; init; } = true;
    public bool ShowFiles { get; init; } = true;
    public bool ShowReferences { get; init; } = true;
    public bool ShowFolders { get; init; } = true;
    public bool ShowTypeParameters { get; init; } = true;
    public bool ShowSnippets { get; init; } = true;
    public bool ShowUsers { get; init; } = true;
    public bool ShowIssues { get; init; } = true;

    // ---- other editor options

    public QuickSuggestionsOptions QuickSuggestions { get; init; } = QuickSuggestionsOptions.Default;

    /// <summary>Milliseconds after typing before quick suggestions show.</summary>
    public int QuickSuggestionsDelay { get; init; } = 10;

    public bool SuggestOnTriggerCharacters { get; init; } = true;

    public AcceptSuggestionOnEnter AcceptSuggestionOnEnter { get; init; } = AcceptSuggestionOnEnter.On;

    public bool AcceptSuggestionOnCommitCharacter { get; init; } = true;

    public SnippetSuggestions SnippetSuggestions { get; init; } = SnippetSuggestions.Inline;

    public SuggestSelection SuggestSelection { get; init; } = SuggestSelection.First;

    public WordBasedSuggestions WordBasedSuggestions { get; init; } = WordBasedSuggestions.CurrentDocument;

    /// <summary>Whether the inline suggestion (ghost text) feature hides quick suggestions while it shows (upstream's inlineSuggest.suppressSuggestions).</summary>
    public bool InlineSuggestSuppressSuggestions { get; init; }
}
