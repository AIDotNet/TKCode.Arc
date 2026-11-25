// Ported from vscode (src/vs/editor/browser/services/editorWorkerService.ts: WordBasedCompletionItemProvider and
// canSyncModel; src/vs/editor/common/services/editorWebWorker.ts: $textualSuggest; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - only the current document is read (wordBasedSuggestions 'currentDocument'; 'matchingDocuments' and
//    'allDocuments' need a model service, 'offWithInlineSuggestions' an inline completions setting);
//  - the words are collected on the thread pool from an immutable TextSnapshot (upstream: in the editor worker);
//  - the setting is read through a callback, so each editor's provider follows its own options.

using System.Diagnostics;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>Completes the words of the document (upstream's WordBasedCompletionItemProvider): the fallback when no language provider answers.</summary>
internal sealed class WordBasedCompletionItemProvider(
    Func<WordBasedSuggestions> getConfig,
    LanguageConfigurationService? languageConfigurationService) : ICompletionItemProvider
{
    public string DebugDisplayName => "wordbasedCompletions";

    public async Task<CompletionList?> ProvideCompletionItems(TextModel model, TextPosition position, CompletionContext context, CancellationToken token)
    {
        if (getConfig() == WordBasedSuggestions.Off)
        {
            return null;
        }

        // only current file and only if not too large
        if (model.IsDisposed() || model.IsTooLargeForSyncing())
        {
            return null;
        }

        var wordDefRegExp = SuggestWords.GetWordDefinition(model, languageConfigurationService);
        var word = SuggestWords.GetWordAtPosition(model, position, languageConfigurationService);
        var replace = word is null
            ? new TextRange(position)
            : new TextRange(position.LineNumber, word.StartColumn, position.LineNumber, word.EndColumn);
        var insert = new TextRange(replace.Start, position);

        var snapshot = model.CreateTextSnapshot();
        var sw = Stopwatch.StartNew();
        var words = await Task.Run(() => SuggestWords.TextualSuggest(snapshot, word?.Word, wordDefRegExp, token), token).ConfigureAwait(true);

        return new CompletionList(words.Select(w => new CompletionItem
        {
            Kind = CompletionItemKind.Text,
            Label = w,
            InsertText = w,
            Range = new CompletionItemRanges(insert, replace),
        }).ToList())
        {
            Duration = sw.Elapsed,
        };
    }
}
