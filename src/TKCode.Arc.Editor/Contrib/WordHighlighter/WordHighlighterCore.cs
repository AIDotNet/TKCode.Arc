// Ported from vscode (src/vs/editor/contrib/wordHighlighter/browser/wordHighlighter.ts: getOccurrencesAtPosition and
// mergeHighlights; src/vs/editor/contrib/wordHighlighter/browser/textualHighlightProvider.ts:
// TextualDocumentHighlightProvider; commit b57d5c9e), MIT license.
// Differences:
//  - the result is a dictionary by model URI (upstream's ResourceMap); only the editor's own model is asked (no
//    multi-document highlights);
//  - provider errors go to the onUnexpectedExternalError callback the caller passes (cancellation is not reported, as
//    upstream's onUnexpectedExternalError ignores it); a provider that throws when called reports at once, one whose
//    task fails reports when it is awaited, in provider order (upstream's microtask order);
//  - model.getWordAtPosition is GetWordAtPosition here: the word by the model language's word definition (the
//    model has no getWordAtPosition yet).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.WordHighlighter;

/// <summary>Computes the occurrences of the symbol at a position (upstream's wordHighlighter.ts free functions).</summary>
internal static class WordHighlighterCore
{
    /// <summary>
    /// The occurrences at <paramref name="position"/> by the document highlight providers of <paramref name="model"/>
    /// (upstream's getOccurrencesAtPosition). With <paramref name="mergeProviders"/>, every provider but the textual
    /// fallback is asked at once and the answers are merged; otherwise the best provider with an answer wins.
    /// </summary>
    public static async Task<Dictionary<Uri, IReadOnlyList<DocumentHighlight>>> GetOccurrencesAtPosition(
        LanguageFeatureRegistry<IDocumentHighlightProvider> registry,
        TextModel model,
        TextPosition position,
        CancellationToken token,
        bool mergeProviders,
        Action<Exception>? onUnexpectedExternalError = null)
    {
        var orderedByScore = registry.Ordered(model);
        if (mergeProviders)
        {
            var providers = orderedByScore.Where(static provider => provider is not TextualDocumentHighlightProvider).ToList();
            var asked = providers.Count > 0 ? providers : orderedByScore.ToList();

            // start every provider first (they run concurrently), then collect in provider order
            var pending = new List<Task<IReadOnlyList<DocumentHighlight>?>?>(asked.Count);
            foreach (var provider in asked)
            {
                try
                {
                    pending.Add(provider.ProvideDocumentHighlights(model, position, token));
                }
                catch (Exception error)
                {
                    Report(error, onUnexpectedExternalError);
                    pending.Add(null);
                }
            }

            var results = new List<IReadOnlyList<DocumentHighlight>>();
            foreach (var task in pending)
            {
                if (task is null)
                {
                    continue;
                }

                try
                {
                    if (await task.ConfigureAwait(true) is { } highlights)
                    {
                        results.Add(highlights);
                    }
                }
                catch (Exception error)
                {
                    Report(error, onUnexpectedExternalError);
                }
            }

            var map = new Dictionary<Uri, IReadOnlyList<DocumentHighlight>>();
            if (results.Count > 0)
            {
                map[model.Uri] = MergeHighlights(results);
            }

            return map;
        }

        // in order of score ask the occurrences provider
        // until someone response with a good result
        // (good = non undefined and non null value)
        // (result of size == 0 is valid, no highlights is a valid/expected result -- not a signal to fall back to other providers)
        foreach (var provider in orderedByScore)
        {
            IReadOnlyList<DocumentHighlight>? result = null;
            try
            {
                result = await provider.ProvideDocumentHighlights(model, position, token).ConfigureAwait(true);
            }
            catch (Exception error)
            {
                Report(error, onUnexpectedExternalError);
            }

            if (result is not null)
            {
                return new Dictionary<Uri, IReadOnlyList<DocumentHighlight>> { [model.Uri] = result };
            }
        }

        return new Dictionary<Uri, IReadOnlyList<DocumentHighlight>>();
    }

    private static void Report(Exception error, Action<Exception>? onUnexpectedExternalError)
    {
        if (error is not OperationCanceledException)
        {
            onUnexpectedExternalError?.Invoke(error);
        }
    }

    // upstream's mergeHighlights: distinct by range, first one wins, in provider order
    private static List<DocumentHighlight> MergeHighlights(List<IReadOnlyList<DocumentHighlight>> results)
    {
        var seen = new HashSet<TextRange>();
        var merged = new List<DocumentHighlight>();
        foreach (var highlights in results)
        {
            foreach (var highlight in highlights)
            {
                if (seen.Add(highlight.Range))
                {
                    merged.Add(highlight);
                }
            }
        }

        return merged;
    }

    /// <summary>
    /// The word at a position by the word definition of the model's language (upstream's model.getWordAtPosition), or
    /// null; the default word definition without a language configuration service.
    /// </summary>
    public static WordAtPosition? GetWordAtPosition(TextModel model, TextPosition position, LanguageConfigurationService? languageConfigurationService)
    {
        var validPosition = model.ValidatePosition(position);
        var wordDefinition = languageConfigurationService?.GetLanguageConfiguration(model.GetLanguageId()).GetWordDefinition()
            ?? WordHelper.DefaultWordRegExp;
        return WordHelper.GetWordAtText(validPosition.Column, wordDefinition, model.GetLineContent(validPosition.LineNumber), 0);
    }
}

/// <summary>
/// The fallback occurrence highlighter: every whole-word, case-sensitive occurrence of the word at the position
/// (upstream's TextualDocumentHighlightProvider, registered for every language).
/// </summary>
internal sealed class TextualDocumentHighlightProvider : IDocumentHighlightProvider
{
    private readonly LanguageConfigurationService? _languageConfigurationService;

    public TextualDocumentHighlightProvider(LanguageConfigurationService? languageConfigurationService = null)
    {
        _languageConfigurationService = languageConfigurationService;
    }

    public Task<IReadOnlyList<DocumentHighlight>?> ProvideDocumentHighlights(TextModel model, TextPosition position, CancellationToken token)
    {
        if (model.IsDisposed())
        {
            return Task.FromResult<IReadOnlyList<DocumentHighlight>?>(null);
        }

        var word = WordHighlighterCore.GetWordAtPosition(model, position, _languageConfigurationService);
        if (word is null)
        {
            return Task.FromResult<IReadOnlyList<DocumentHighlight>?>([]);
        }

        var matches = model.FindMatches(word.Word, true, false, true, WordHelper.UsualWordSeparators, false);
        IReadOnlyList<DocumentHighlight> result = matches.Select(static m => new DocumentHighlight(m.Range) { Kind = DocumentHighlightKind.Text }).ToList();
        return Task.FromResult<IReadOnlyList<DocumentHighlight>?>(result);
    }
}
