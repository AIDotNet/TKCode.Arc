// Ported from vscode (src/vs/editor/contrib/format/browser/format.ts, commit b57d5c9e), MIT license.
// Differences:
//  - the editor half of formatDocumentWithProvider / formatDocumentRangesWithProvider is split off: the engine computes
//    the edits (ComputeDocumentFormattingEdits, ComputeDocumentRangesFormattingEdits) and applies them to a model (the
//    model path, FormatDocumentWithProvider / FormatDocumentRangesWithProvider); the CodeEditor applies them through
//    FormattingEdit with its own cancellation on cursor moves (Controls, Contrib/Format);
//  - FormattingConflicts has no process-wide selector list (no static mutable state): a selector is passed in; without
//    one the first formatter is used, which is what Monaco installs (editor.api.ts: setFormatterSelector(formatter =>
//    formatter[0]));
//  - the worker service's computeMoreMinimalEdits is EditorWorker.ComputeMoreMinimalEdits on the model itself;
//  - TextModelCancellationTokenSource cancels when the model's content changes (upstream: when its version changes);
//  - no logging, progress, accessibility signals or the _executeFormat* commands.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;

namespace TKCode.Arc.Editor.Contrib.Format;

/// <summary>What is formatted (upstream's FormattingKind).</summary>
internal enum FormattingKind
{
    File = 1,
    Selection = 2,
}

/// <summary>Whether the user asked for formatting (upstream's FormattingMode).</summary>
internal enum FormattingMode
{
    /// <summary>A format command.</summary>
    Explicit = 1,

    /// <summary>Format on type, on paste or on save: no undo stops of its own, no reveal.</summary>
    Silent = 2,
}

/// <summary>Picks one of several formatters (upstream's IFormattingEditProviderSelector); null picks none.</summary>
internal delegate Task<T?> FormattingEditProviderSelector<T>(IReadOnlyList<T> formatters, TextModel document, FormattingMode mode, FormattingKind kind)
    where T : class;

/// <summary>Chooses between formatters (upstream's FormattingConflicts).</summary>
internal static class FormattingConflicts
{
    /// <summary>
    /// The formatter to use: what <paramref name="selector"/> picks, else the first (Monaco's selector); null when there
    /// is none.
    /// </summary>
    public static async Task<T?> Select<T>(IReadOnlyList<T> formatters, TextModel document, FormattingMode mode, FormattingKind kind, FormattingEditProviderSelector<T>? selector = null)
        where T : class
    {
        if (formatters.Count == 0)
        {
            return null;
        }

        if (selector is not null)
        {
            return await selector(formatters, document, mode, kind).ConfigureAwait(true);
        }

        return formatters[0];
    }
}

/// <summary>Cancels when the model's text changes (upstream's TextModelCancellationTokenSource).</summary>
internal sealed class TextModelCancellationTokenSource : IDisposable
{
    private readonly TextModel _model;
    private readonly CancellationTokenSource _source;

    public TextModelCancellationTokenSource(TextModel model, CancellationToken parent = default)
    {
        _model = model;
        _source = CancellationTokenSource.CreateLinkedTokenSource(parent);
        _model.DidChangeContent += OnChange;
    }

    public CancellationToken Token => _source.Token;

    public void Cancel() => _source.Cancel();

    public void Dispose()
    {
        _model.DidChangeContent -= OnChange;
        _source.Dispose();
    }

    private void OnChange(ModelContentChangedEvent e) => _source.Cancel();
}

/// <summary>Formatting through the language feature providers (upstream's format.ts).</summary>
internal static class Formatting
{
    /// <summary>
    /// The document formatters of a model: the document formatters, then each range formatter as a document formatter
    /// for the whole model unless the same extension already formats documents (upstream's
    /// getRealAndSyntheticDocumentFormattersOrdered).
    /// </summary>
    public static IReadOnlyList<IDocumentFormattingEditProvider> GetRealAndSyntheticDocumentFormattersOrdered(
        LanguageFeatureRegistry<IDocumentFormattingEditProvider> documentFormattingEditProvider,
        LanguageFeatureRegistry<IDocumentRangeFormattingEditProvider> documentRangeFormattingEditProvider,
        TextModel model)
    {
        var result = new List<IDocumentFormattingEditProvider>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // (1) add all document formatter
        foreach (var formatter in documentFormattingEditProvider.Ordered(model))
        {
            result.Add(formatter);
            if (formatter.ExtensionId is { } extensionId)
            {
                seen.Add(extensionId);
            }
        }

        // (2) add all range formatter as document formatter (unless the same extension already did that)
        foreach (var formatter in documentRangeFormattingEditProvider.Ordered(model))
        {
            if (formatter.ExtensionId is { } extensionId)
            {
                if (!seen.Add(extensionId))
                {
                    continue;
                }
            }

            result.Add(new SyntheticDocumentFormatter(formatter));
        }

        return result;
    }

    /// <summary>The model's formatting options (upstream's model.getFormattingOptions()).</summary>
    public static FormattingOptions GetFormattingOptions(TextModel model)
    {
        var options = model.GetOptions();
        return new FormattingOptions(options.TabSize, options.InsertSpaces);
    }

    /// <summary>
    /// The minimal edits of one document formatter for the model; null when the token was cancelled (also by an edit
    /// of the model while the provider ran), empty when there is nothing to change.
    /// </summary>
    public static async Task<IReadOnlyList<TextEdit>?> ComputeDocumentFormattingEdits(IDocumentFormattingEditProvider provider, TextModel model, CancellationToken token)
    {
        using var cts = new TextModelCancellationTokenSource(model, token);
        var rawEdits = await provider.ProvideDocumentFormattingEdits(model, GetFormattingOptions(model), cts.Token).ConfigureAwait(true);
        if (cts.Token.IsCancellationRequested)
        {
            return null;
        }

        return EditorWorker.ComputeMoreMinimalEdits(model, rawEdits) ?? [];
    }

    /// <summary>
    /// The minimal edits of one range formatter for some ranges of the model (upstream's formatDocumentRangesWithProvider
    /// before it applies them): ranges that touch are merged, and ranges whose edits intersect are formatted again as
    /// one. Null when cancelled.
    /// </summary>
    public static async Task<IReadOnlyList<TextEdit>?> ComputeDocumentRangesFormattingEdits(
        IDocumentRangeFormattingEditProvider provider,
        TextModel model,
        IReadOnlyList<TextRange> rangeOrRanges,
        CancellationToken token)
    {
        using var cts = new TextModelCancellationTokenSource(model, token);

        // make sure that ranges don't overlap nor touch each other
        var ranges = new List<TextRange>();
        foreach (var range in rangeOrRanges.OrderBy(static r => r, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts)))
        {
            if (ranges.Count > 0 && TextRange.AreIntersectingOrTouching(ranges[^1], range))
            {
                ranges[^1] = new TextRange(ranges[^1].Start, range.End);
            }
            else
            {
                ranges.Add(range);
            }
        }

        async Task<IReadOnlyList<TextEdit>> ComputeEdits(TextRange range) =>
            await provider.ProvideDocumentRangeFormattingEdits(model, range, GetFormattingOptions(model), cts.Token).ConfigureAwait(true) ?? [];

        static bool HasIntersectingEdit(IReadOnlyList<TextEdit> a, IReadOnlyList<TextEdit> b)
        {
            if (a.Count == 0 || b.Count == 0)
            {
                return false;
            }

            // quick exit if the list of ranges are completely unrelated [O(n)]
            var mergedA = a.Aggregate(a[0].Range, static (acc, val) => acc.PlusRange(val.Range));
            if (!b.Any(x => mergedA.IntersectRanges(x.Range) is not null))
            {
                return false;
            }

            // fallback to a complete check [O(n^2)]
            foreach (var edit in a)
            {
                foreach (var otherEdit in b)
                {
                    if (edit.Range.IntersectRanges(otherEdit.Range) is not null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        var allEdits = new List<TextEdit>();
        var rawEditsList = new List<IReadOnlyList<TextEdit>>();
        if (provider.SupportsRangesFormatting)
        {
            rawEditsList.Add(await provider.ProvideDocumentRangesFormattingEdits(model, ranges, GetFormattingOptions(model), cts.Token).ConfigureAwait(true) ?? []);
        }
        else
        {
            foreach (var range in ranges)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    return null;
                }

                rawEditsList.Add(await ComputeEdits(range).ConfigureAwait(true));
            }

            for (var i = 0; i < ranges.Count; ++i)
            {
                for (var j = i + 1; j < ranges.Count; ++j)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        return null;
                    }

                    if (HasIntersectingEdit(rawEditsList[i], rawEditsList[j]))
                    {
                        // Merge ranges i and j into a single range, recompute the associated edits
                        var mergedRange = ranges[i].PlusRange(ranges[j]);
                        var edits = await ComputeEdits(mergedRange).ConfigureAwait(true);
                        ranges.RemoveAt(j);
                        ranges.RemoveAt(i);
                        ranges.Add(mergedRange);
                        rawEditsList.RemoveAt(j);
                        rawEditsList.RemoveAt(i);
                        rawEditsList.Add(edits);
                        // Restart scanning
                        i = 0;
                        j = 0;
                    }
                }
            }
        }

        foreach (var rawEdits in rawEditsList)
        {
            if (cts.Token.IsCancellationRequested)
            {
                return null;
            }

            if (EditorWorker.ComputeMoreMinimalEdits(model, rawEdits) is { } minimalEdits)
            {
                allEdits.AddRange(minimalEdits);
            }
        }

        return cts.Token.IsCancellationRequested ? null : allEdits;
    }

    /// <summary>Formats the model with the formatter <paramref name="selector"/> picks (upstream's formatDocumentWithSelectedProvider, model path).</summary>
    public static async Task<bool> FormatDocumentWithSelectedProvider(
        LanguageFeatures languageFeatures,
        TextModel model,
        FormattingMode mode,
        CancellationToken token,
        FormattingEditProviderSelector<IDocumentFormattingEditProvider>? selector = null)
    {
        var provider = GetRealAndSyntheticDocumentFormattersOrdered(languageFeatures.DocumentFormattingEditProvider, languageFeatures.DocumentRangeFormattingEditProvider, model);
        var selected = await FormattingConflicts.Select(provider, model, mode, FormattingKind.File, selector).ConfigureAwait(true);
        return selected is not null && await FormatDocumentWithProvider(selected, model, token).ConfigureAwait(true);
    }

    /// <summary>Formats the model with a document formatter, as one undo step (upstream's formatDocumentWithProvider, model path).</summary>
    public static async Task<bool> FormatDocumentWithProvider(IDocumentFormattingEditProvider provider, TextModel model, CancellationToken token)
    {
        var edits = await ComputeDocumentFormattingEdits(provider, model, token).ConfigureAwait(true);
        return ApplyToModel(model, edits);
    }

    /// <summary>Formats ranges of the model with a range formatter, as one undo step (upstream's formatDocumentRangesWithProvider, model path).</summary>
    public static async Task<bool> FormatDocumentRangesWithProvider(IDocumentRangeFormattingEditProvider provider, TextModel model, IReadOnlyList<TextRange> ranges, CancellationToken token)
    {
        var edits = await ComputeDocumentRangesFormattingEdits(provider, model, ranges, token).ConfigureAwait(true);
        return ApplyToModel(model, edits);
    }

    // upstream: "use model to apply edits"
    private static bool ApplyToModel(TextModel model, IReadOnlyList<TextEdit>? edits)
    {
        if (edits is null || edits.Count == 0)
        {
            return false;
        }

        var withRange = edits.Where(EditorWorker.HasRange).ToList();
        if (withRange.Count == 0)
        {
            return false;
        }

        var range = withRange[0].Range;
        var initialSelection = new TextSelection(range.StartLineNumber, range.StartColumn, range.EndLineNumber, range.EndColumn);
        model.PushEditOperations(
            [initialSelection],
            [.. withRange.Select(static edit => new SingleEditOperation(edit.Range, edit.Text, ForceMoveMarkers: true))],
            undoEdits =>
            {
                foreach (var undoEdit in undoEdits)
                {
                    if (TextRange.AreIntersectingOrTouching(undoEdit.Range, initialSelection.Range))
                    {
                        var r = undoEdit.Range;
                        return [new TextSelection(r.StartLineNumber, r.StartColumn, r.EndLineNumber, r.EndColumn)];
                    }
                }

                return null;
            });
        return true;
    }

    /// <summary>The first non-empty minimal edits of the range formatters (upstream's getDocumentRangeFormattingEditsUntilResult).</summary>
    public static async Task<IReadOnlyList<TextEdit>?> GetDocumentRangeFormattingEditsUntilResult(
        LanguageFeatures languageFeatures, TextModel model, TextRange range, FormattingOptions options, CancellationToken token)
    {
        foreach (var provider in languageFeatures.DocumentRangeFormattingEditProvider.Ordered(model))
        {
            var rawEdits = await Guard(() => provider.ProvideDocumentRangeFormattingEdits(model, range, options, token)).ConfigureAwait(true);
            if (rawEdits is { Count: > 0 })
            {
                return EditorWorker.ComputeMoreMinimalEdits(model, rawEdits);
            }
        }

        return null;
    }

    /// <summary>The first non-empty minimal edits of the document formatters (upstream's getDocumentFormattingEditsUntilResult).</summary>
    public static async Task<IReadOnlyList<TextEdit>?> GetDocumentFormattingEditsUntilResult(
        LanguageFeatures languageFeatures, TextModel model, FormattingOptions options, CancellationToken token)
    {
        var providers = GetRealAndSyntheticDocumentFormattersOrdered(languageFeatures.DocumentFormattingEditProvider, languageFeatures.DocumentRangeFormattingEditProvider, model);
        foreach (var provider in providers)
        {
            var rawEdits = await Guard(() => provider.ProvideDocumentFormattingEdits(model, options, token)).ConfigureAwait(true);
            if (rawEdits is { Count: > 0 })
            {
                return EditorWorker.ComputeMoreMinimalEdits(model, rawEdits);
            }
        }

        return null;
    }

    /// <summary>
    /// The minimal edits of the best on-type formatter when <paramref name="ch"/> is one of its trigger characters
    /// (upstream's getOnTypeFormattingEdits); null otherwise.
    /// </summary>
    public static async Task<IReadOnlyList<TextEdit>?> GetOnTypeFormattingEdits(
        LanguageFeatures languageFeatures, TextModel model, TextPosition position, string ch, FormattingOptions options, CancellationToken token)
    {
        var providers = languageFeatures.OnTypeFormattingEditProvider.Ordered(model);
        if (providers.Count == 0)
        {
            return null;
        }

        if (!providers[0].AutoFormatTriggerCharacters.Contains(ch))
        {
            return null;
        }

        var edits = await Guard(() => providers[0].ProvideOnTypeFormattingEdits(model, position, ch, options, token)).ConfigureAwait(true);
        return EditorWorker.ComputeMoreMinimalEdits(model, edits);
    }

    // upstream: .catch(onUnexpectedExternalError)
    private static async Task<IReadOnlyList<TextEdit>?> Guard(Func<Task<IReadOnlyList<TextEdit>?>> call)
    {
        try
        {
            return await call().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
            return null;
        }
    }

    // a range formatter used as a document formatter for the whole model
    private sealed class SyntheticDocumentFormatter(IDocumentRangeFormattingEditProvider formatter) : IDocumentFormattingEditProvider
    {
        public string? ExtensionId => formatter.ExtensionId;

        public string? DisplayName => formatter.DisplayName;

        public Task<IReadOnlyList<TextEdit>?> ProvideDocumentFormattingEdits(TextModel model, FormattingOptions options, CancellationToken token) =>
            formatter.ProvideDocumentRangeFormattingEdits(model, model.GetFullModelRange(), options, token);
    }
}
