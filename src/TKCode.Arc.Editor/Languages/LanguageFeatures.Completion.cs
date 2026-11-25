// Ported from vscode (src/vs/editor/common/languages.ts: CompletionItemProvider, InlineCompletionsProvider and their
// types, commit b57d5c9e), MIT license. The general differences are listed in LanguageFeatures.Types.cs. Here also:
//  - CompletionItem.label (string | CompletionItemLabel) is a CompletionItemLabel, implicit from a string;
//  - CompletionItem.range (IRange | CompletionItemRanges) is a CompletionItemRanges, implicit from a TextRange (insert
//    and replace the same range);
//  - InlineCompletion.insertText (string | { snippet } | undefined) is InsertText with IsSnippet;
//  - the inline completion provider's telemetry callbacks (handleItemDidShow, handleEndOfLifetime, model info,
//    provider options) are not ported; handlePartialAccept, handleRejection and disposeInlineCompletions are.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Languages;

/// <summary>What a completion item is (upstream's CompletionItemKind; Snippet is the highest value).</summary>
internal enum CompletionItemKind
{
    Method,
    Function,
    Constructor,
    Field,
    Variable,
    Class,
    Struct,
    Interface,
    Module,
    Property,
    Event,
    Operator,
    Unit,
    Value,
    Constant,
    Enum,
    EnumMember,
    Keyword,
    Text,
    Color,
    File,
    Reference,
    Customcolor,
    Folder,
    TypeParameter,
    User,
    Issue,
    Tool,
    Snippet,
}

internal enum CompletionItemTag
{
    Deprecated = 1,
}

[Flags]
internal enum CompletionItemInsertTextRule
{
    None = 0,

    /// <summary>Adjust whitespace/indentation of multiline insert texts to match the current line indentation.</summary>
    KeepWhitespace = 0b001,

    /// <summary><c>insertText</c> is a snippet.</summary>
    InsertAsSnippet = 0b100,
}

/// <summary>A completion's label with its details (upstream's CompletionItemLabel).</summary>
internal sealed record CompletionItemLabel(string Label)
{
    public string? Detail { get; init; }

    public string? Description { get; init; }

    public static implicit operator CompletionItemLabel(string label) => new(label);
}

/// <summary>The ranges a completion inserts into and replaces (upstream's CompletionItemRanges).</summary>
internal sealed record CompletionItemRanges(TextRange Insert, TextRange Replace)
{
    public static implicit operator CompletionItemRanges(TextRange range) => new(range, range);
}

/// <summary>A proposal to complete the text being typed (upstream's CompletionItem).</summary>
internal sealed record CompletionItem
{
    /// <summary>The label; by default also the text inserted.</summary>
    public required CompletionItemLabel Label { get; init; }

    /// <summary>Picks the item's icon.</summary>
    public CompletionItemKind Kind { get; init; }

    public IReadOnlyList<CompletionItemTag>? Tags { get; init; }

    /// <summary>Type or symbol information.</summary>
    public string? Detail { get; init; }

    /// <summary>A doc-comment.</summary>
    public StringOrMarkdown? Documentation { get; init; }

    /// <summary>Compared instead of the label when sorting.</summary>
    public string? SortText { get; init; }

    /// <summary>Compared instead of the label when filtering.</summary>
    public string? FilterText { get; init; }

    /// <summary>Select this item when showing.</summary>
    public bool Preselect { get; init; }

    /// <summary>The text or snippet inserted.</summary>
    public required string InsertText { get; init; }

    public CompletionItemInsertTextRule InsertTextRules { get; init; }

    /// <summary>The range replaced (single line, containing the position completion was requested at).</summary>
    public required CompletionItemRanges Range { get; init; }

    /// <summary>Characters that accept the item and are then typed.</summary>
    public IReadOnlyList<string>? CommitCharacters { get; init; }

    /// <summary>Edits applied with the item; they overlap neither the main edit nor each other.</summary>
    public IReadOnlyList<SingleEditOperation>? AdditionalTextEdits { get; init; }

    /// <summary>A command run after the item is accepted.</summary>
    public LanguageCommand? Command { get; init; }

    public LanguageCommand? Action { get; init; }

    public string? ExtensionId { get; init; }
}

/// <summary>The completions of one request (upstream's CompletionList).</summary>
internal sealed class CompletionList(IReadOnlyList<CompletionItem> suggestions)
{
    public IReadOnlyList<CompletionItem> Suggestions { get; } = suggestions;

    /// <summary>Typing more asks the provider again.</summary>
    public bool Incomplete { get; init; }

    /// <summary>Called when the list is no longer used.</summary>
    public Action? Dispose { get; init; }

    public TimeSpan? Duration { get; init; }
}

/// <summary>How a partial acceptance was triggered.</summary>
internal enum PartialAcceptTriggerKind
{
    Word = 0,
    Line = 1,
    Suggest = 2,
}

/// <summary>Info provided on partial acceptance.</summary>
internal sealed record PartialAcceptInfo(PartialAcceptTriggerKind Kind, int AcceptedLength);

/// <summary>How a suggest provider was triggered.</summary>
internal enum CompletionTriggerKind
{
    Invoke = 0,
    TriggerCharacter = 1,
    TriggerForIncompleteCompletions = 2,
}

/// <summary>The context a completion provider is called in.</summary>
internal sealed record CompletionContext(CompletionTriggerKind TriggerKind)
{
    /// <summary>The character that triggered the provider, or null.</summary>
    public string? TriggerCharacter { get; init; }
}

/// <summary>Provides completions (upstream's CompletionItemProvider).</summary>
internal interface ICompletionItemProvider
{
    /// <summary>Identifies the provider in the UI (upstream's _debugDisplayName).</summary>
    string DebugDisplayName => GetType().Name;

    /// <summary>Characters that trigger completion when typed.</summary>
    IReadOnlyList<string>? TriggerCharacters => null;

    /// <summary>Provide completion items for the given position and document.</summary>
    Task<CompletionList?> ProvideCompletionItems(TextModel model, TextPosition position, CompletionContext context, CancellationToken token);

    /// <summary>Whether <see cref="ResolveCompletionItem"/> is implemented.</summary>
    bool SupportsResolveCompletionItem => false;

    /// <summary>Fills in more data of an item (documentation, detail); the editor resolves an item once.</summary>
    Task<CompletionItem?> ResolveCompletionItem(CompletionItem item, CancellationToken token) => Task.FromResult<CompletionItem?>(null);
}

/// <summary>How an inline completion provider was triggered.</summary>
internal enum InlineCompletionTriggerKind
{
    /// <summary>Completion was triggered automatically while editing.</summary>
    Automatic = 0,

    /// <summary>Completion was triggered explicitly by a user gesture.</summary>
    Explicit = 1,
}

/// <summary>The suggestion selected in the suggest widget while inline completions are asked for.</summary>
internal sealed record SelectedSuggestionInfo(TextRange Range, string Text, CompletionItemKind CompletionKind, bool IsSnippetText);

/// <summary>The context an inline completion provider is called in.</summary>
internal sealed record InlineCompletionContext(InlineCompletionTriggerKind TriggerKind)
{
    public SelectedSuggestionInfo? SelectedSuggestionInfo { get; init; }

    public string? UserPrompt { get; init; }

    public string RequestUuid { get; init; } = Guid.NewGuid().ToString();

    public bool IncludeInlineEdits { get; init; }

    public bool IncludeInlineCompletions { get; init; } = true;
}

/// <summary>A ghost text proposal (upstream's InlineCompletion).</summary>
internal sealed record InlineCompletion
{
    /// <summary>The text to insert; null when the item only carries a command.</summary>
    public string? InsertText { get; init; }

    /// <summary>Whether <see cref="InsertText"/> is a snippet.</summary>
    public bool IsSnippet { get; init; }

    /// <summary>The range to replace; must begin and end on the same line. Defaults to an empty range at the position.</summary>
    public TextRange? Range { get; init; }

    public IReadOnlyList<SingleEditOperation>? AdditionalTextEdits { get; init; }

    /// <summary>A command run after the completion is accepted.</summary>
    public LanguageCommand? Command { get; init; }

    public LanguageCommand? ShownCommand { get; init; }

    /// <summary>Close the brackets the text opens when it is accepted.</summary>
    public bool CompleteBracketPairs { get; init; }

    public bool IsInlineEdit { get; init; }

    public TextRange? ShowRange { get; init; }

    public string? CorrelationId { get; init; }
}

/// <summary>The proposals of one inline completion request (upstream's InlineCompletions).</summary>
internal sealed class InlineCompletions(IReadOnlyList<InlineCompletion> items)
{
    public IReadOnlyList<InlineCompletion> Items { get; } = items;

    public IReadOnlyList<LanguageCommand>? Commands { get; init; }

    public bool SuppressSuggestions { get; init; }

    public bool EnableForwardStability { get; init; }
}

/// <summary>Why inline completions are disposed (upstream's InlineCompletionsDisposeReason).</summary>
internal enum InlineCompletionsDisposeReasonKind
{
    LostRace,
    TokenCancellation,
    Other,
    Empty,
    NotTaken,
}

/// <summary>Provides ghost text (upstream's InlineCompletionsProvider).</summary>
internal interface IInlineCompletionsProvider
{
    Task<InlineCompletions?> ProvideInlineCompletions(TextModel model, TextPosition position, InlineCompletionContext context, CancellationToken token);

    /// <summary>Will be called when an item is partially accepted.</summary>
    void HandlePartialAccept(InlineCompletions completions, InlineCompletion item, int acceptedCharacters, PartialAcceptInfo info)
    {
    }

    /// <summary>Will be called when an item is rejected.</summary>
    void HandleRejection(InlineCompletions completions, InlineCompletion item)
    {
    }

    /// <summary>Will be called when the completions are no longer used.</summary>
    void DisposeInlineCompletions(InlineCompletions completions, InlineCompletionsDisposeReasonKind reason)
    {
    }

    /// <summary>Raised when the provider would answer differently now.</summary>
    event Action? DidChangeInlineCompletions
    {
        add
        {
        }

        remove
        {
        }
    }

    /// <summary>Only used for yieldsToGroupIds; multiple providers can have the same group id.</summary>
    string? GroupId => null;

    /// <summary>Returns a list of preferred provider groupIds; the current provider is only requested for completions if no provider with a preferred group id returned a result.</summary>
    IReadOnlyList<string>? YieldsToGroupIds => null;

    string? DisplayName => null;

    /// <summary>The delay before asking after a change, or null for the editor's default.</summary>
    int? DebounceDelayMs => null;
}
