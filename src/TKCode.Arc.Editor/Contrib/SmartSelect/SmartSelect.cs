// Ported from vscode (src/vs/editor/contrib/smartSelect/browser/smartSelect.ts, commit b57d5c9e), MIT license:
// SelectionRanges, SmartSelectController.run and provideSelectionRanges.
// Differences from upstream:
//  - the controller's state is the engine class SmartSelectState; the control's controller creates it, runs it from
//    the expand / shrink actions and calls Reset when the cursor moves (upstream's onDidChangeCursorPosition listener);
//  - provideSelectionRanges takes the language configuration service the word and bracket providers read (upstream's
//    model brings its own);
//  - provider errors are reported to UnexpectedErrors (upstream's onUnexpectedExternalError);
//  - the _executeSelectionRangeProvider command is not ported (there is no model service).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.SmartSelect;

/// <summary>The ranges one cursor grows and shrinks through (upstream's SelectionRanges).</summary>
internal sealed class SelectionRanges
{
    public SelectionRanges(int index, IReadOnlyList<TextRange> ranges)
    {
        Index = index;
        Ranges = ranges;
    }

    public int Index { get; }

    public IReadOnlyList<TextRange> Ranges { get; }

    /// <summary>The next (<paramref name="fwd"/>) or previous range that differs from the current one.</summary>
    public SelectionRanges Mov(bool fwd)
    {
        var index = Index + (fwd ? 1 : -1);
        if (index < 0 || index >= Ranges.Count)
        {
            return this;
        }
        var res = new SelectionRanges(index, Ranges);
        if (res.Ranges[index].Equals(Ranges[Index]))
        {
            // next range equals this range, retry with next-next
            return res.Mov(fwd);
        }
        return res;
    }
}

/// <summary>The smart select state of an editor (upstream's SmartSelectController without its editor wiring).</summary>
internal sealed class SmartSelectState
{
    private SelectionRanges[]? _state;
    private bool _ignoreSelection;

    /// <summary>Whether a run is setting the selections (the controller ignores that cursor change).</summary>
    public bool IsSettingSelections => _ignoreSelection;

    /// <summary>Whether there are computed ranges to grow and shrink through.</summary>
    public bool HasState => _state is not null;

    /// <summary>Forgets the ranges (the caret moved).</summary>
    public void Reset() => _state = null;

    /// <summary>SmartSelectController.run: expand (<paramref name="forward"/>) or shrink the selections.</summary>
    public async Task Run(IEditorActionHost editor, bool forward)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var selections = editor.GetSelections().ToArray();

        if (_state is null)
        {
            var registry = editor.LanguageFeatures?.SelectionRangeProvider;
            var ranges = await SmartSelect.ProvideSelectionRanges(
                registry,
                editor.LanguageConfigurationService,
                model,
                selections.Select(s => s.Position).ToList(),
                editor.SmartSelectOptions,
                CancellationToken.None);

            if (ranges.Count == 0 || ranges.Count != selections.Length)
            {
                // invalid result
                return;
            }
            if (editor.GetModel() is null || !editor.GetSelections().SequenceEqual(selections))
            {
                // invalid editor state
                return;
            }

            var state = new SelectionRanges[ranges.Count];
            for (var i = 0; i < ranges.Count; i++)
            {
                var selection = selections[i];
                var filtered = new List<TextRange> { selection.Range };
                foreach (var range in ranges[i])
                {
                    // filter ranges inside the selection
                    if (range.ContainsPosition(selection.Range.Start) && range.ContainsPosition(selection.Range.End))
                    {
                        filtered.Add(range);
                    }
                }
                // the current selection comes first
                state[i] = new SelectionRanges(0, filtered);
            }
            _state = state;
        }

        if (_state is not { } current)
        {
            // no state
            return;
        }

        _state = current.Select(state => state.Mov(forward)).ToArray();
        var newSelections = _state.Select(state =>
        {
            var range = state.Ranges[state.Index];
            return new TextSelection(range.StartLineNumber, range.StartColumn, range.EndLineNumber, range.EndColumn);
        }).ToList();
        _ignoreSelection = true;
        try
        {
            editor.SetSelections(newSelections, "api");
        }
        finally
        {
            _ignoreSelection = false;
        }
    }
}

/// <summary>The selection ranges of the providers, merged (upstream's provideSelectionRanges).</summary>
internal static class SmartSelect
{
    /// <summary>Upstream's provideSelectionRanges.</summary>
    public static async Task<IReadOnlyList<IReadOnlyList<TextRange>>> ProvideSelectionRanges(
        LanguageFeatureRegistry<ISelectionRangeProvider>? registry,
        LanguageConfigurationService languageConfigurationService,
        TextModel model,
        IReadOnlyList<TextPosition> positions,
        SmartSelectOptions options,
        CancellationToken token,
        int bracketMaxDuration = BracketSelectionRangeProvider.DefaultMaxDuration)
    {
        // ALWAYS have word based selection range
        var providers = (registry?.All(model) ?? []).ToList();
        providers.Add(new WordSelectionRangeProvider(languageConfigurationService, options.SelectSubwords));

        if (providers.Count == 1)
        {
            // add word selection and bracket selection when no provider exists
            providers.Insert(0, new BracketSelectionRangeProvider(languageConfigurationService, bracketMaxDuration));
        }

        var allRawRanges = new List<TextRange>?[positions.Count];

        async Task Collect(ISelectionRangeProvider provider)
        {
            IReadOnlyList<IReadOnlyList<SelectionRange>>? allProviderRanges;
            try
            {
                allProviderRanges = await provider.ProvideSelectionRanges(model, positions, token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                UnexpectedErrors.Report(e);
                return;
            }

            if (allProviderRanges is { Count: > 0 } && allProviderRanges.Count == positions.Count)
            {
                for (var i = 0; i < positions.Count; i++)
                {
                    var list = allRawRanges[i] ??= new List<TextRange>();
                    foreach (var oneProviderRanges in allProviderRanges[i])
                    {
                        if (oneProviderRanges.Range.ContainsPosition(positions[i]))
                        {
                            list.Add(oneProviderRanges.Range);
                        }
                    }
                }
            }
        }

        await Task.WhenAll(providers.Select(Collect));

        var result = new List<IReadOnlyList<TextRange>>();
        foreach (var raw in allRawRanges)
        {
            result.Add(Merge(model, raw, options));
        }
        return result;
    }

    private static IReadOnlyList<TextRange> Merge(TextModel model, List<TextRange>? oneRawRanges, SmartSelectOptions options)
    {
        if (oneRawRanges is null || oneRawRanges.Count == 0)
        {
            return [];
        }

        // sort all by start/end position (stable, as Array.prototype.sort)
        var sorted = oneRawRanges.OrderBy(r => r, Comparer<TextRange>.Create(static (a, b) =>
        {
            if (a.Start.IsBefore(b.Start))
            {
                return 1;
            }
            else if (b.Start.IsBefore(a.Start))
            {
                return -1;
            }
            else if (a.End.IsBefore(b.End))
            {
                return -1;
            }
            else if (b.End.IsBefore(a.End))
            {
                return 1;
            }
            else
            {
                return 0;
            }
        })).ToList();

        // remove ranges that don't contain the former range or that are equal to the
        // former range
        var oneRanges = new List<TextRange>();
        TextRange? last = null;
        foreach (var range in sorted)
        {
            if (last is not { } l || (range.ContainsRange(l) && !range.Equals(l)))
            {
                oneRanges.Add(range);
                last = range;
            }
        }

        if (!options.SelectLeadingAndTrailingWhitespace)
        {
            return oneRanges;
        }

        // add ranges that expand trivia at line starts and ends whenever a range
        // wraps onto the a new line
        var oneRangesWithTrivia = new List<TextRange> { oneRanges[0] };
        for (var i = 1; i < oneRanges.Count; i++)
        {
            var prev = oneRanges[i - 1];
            var cur = oneRanges[i];
            if (cur.StartLineNumber != prev.StartLineNumber || cur.EndLineNumber != prev.EndLineNumber)
            {
                // add line/block range without leading/failing whitespace
                var rangeNoWhitespace = new TextRange(prev.StartLineNumber, model.GetLineFirstNonWhitespaceColumn(prev.StartLineNumber), prev.EndLineNumber, model.GetLineLastNonWhitespaceColumn(prev.EndLineNumber));
                if (rangeNoWhitespace.ContainsRange(prev) && !rangeNoWhitespace.Equals(prev) && cur.ContainsRange(rangeNoWhitespace) && !cur.Equals(rangeNoWhitespace))
                {
                    oneRangesWithTrivia.Add(rangeNoWhitespace);
                }
                // add line/block range
                var rangeFull = new TextRange(prev.StartLineNumber, 1, prev.EndLineNumber, model.GetLineMaxColumn(prev.EndLineNumber));
                if (rangeFull.ContainsRange(prev) && !rangeFull.Equals(rangeNoWhitespace) && cur.ContainsRange(rangeFull) && !cur.Equals(rangeFull))
                {
                    oneRangesWithTrivia.Add(rangeFull);
                }
            }
            oneRangesWithTrivia.Add(cur);
        }
        return oneRangesWithTrivia;
    }
}
