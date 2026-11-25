// Ported from vscode (src/vs/editor/contrib/codelens/browser/codelens.ts: CodeLensItem, CodeLensModel and
// getCodeLensModel, commit b57d5c9e), MIT license.
// Differences from upstream: the lists' Dispose callbacks are what the model disposes (upstream: IDisposable lists in a
// DisposableStore); the sort is stable; the _executeCodeLensProvider API command is not ported.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.CodeLens;

/// <summary>A code lens and the provider that returned it (upstream's CodeLensItem).</summary>
internal sealed record CodeLensItem(Languages.CodeLens Symbol, ICodeLensProvider Provider);

/// <summary>The code lenses of a model, sorted by line (upstream's CodeLensModel).</summary>
internal sealed class CodeLensModel : IDisposable
{
    private List<Action>? _disposables;
    private bool _disposed;

    /// <summary>A model without lenses.</summary>
    public static CodeLensModel Empty => new();

    public List<CodeLensItem> Lenses { get; set; } = new();

    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_disposables is { } disposables)
        {
            foreach (var dispose in disposables)
            {
                try
                {
                    dispose();
                }
                catch (Exception error)
                {
                    UnexpectedErrors.Report(error);
                }
            }
        }
    }

    /// <summary>Adds the lenses of a provider's list.</summary>
    public void Add(CodeLensList list, ICodeLensProvider provider)
    {
        if (list.Dispose is { } dispose)
        {
            (_disposables ??= new()).Add(dispose);
        }

        foreach (var symbol in list.Lenses)
        {
            Lenses.Add(new CodeLensItem(symbol, provider));
        }
    }
}

/// <summary>Asks the code lens providers of a model (upstream's getCodeLensModel).</summary>
internal static class CodeLensService
{
    /// <summary>The lenses of every provider, sorted by line, provider rank and column; an empty model when cancelled.</summary>
    public static async Task<CodeLensModel> GetCodeLensModel(LanguageFeatureRegistry<ICodeLensProvider> registry, TextModel model, CancellationToken token)
    {
        var providers = registry.Ordered(model);
        var providerRanks = new Dictionary<ICodeLensProvider, int>(ReferenceEqualityComparer.Instance);
        var result = new CodeLensModel();

        var lists = new (CodeLensList? List, ICodeLensProvider Provider)[providers.Count];
        var tasks = providers.Select(async (provider, i) =>
        {
            providerRanks[provider] = i;
            try
            {
                lists[i] = (await provider.ProvideCodeLenses(model, token).ConfigureAwait(true), provider);
            }
            catch (OperationCanceledException)
            {
                lists[i] = (null, provider);
            }
            catch (Exception error)
            {
                UnexpectedErrors.Report(error);
                lists[i] = (null, provider);
            }
        }).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(true);

        // upstream adds the lists as they arrive; the sort below makes the order the same
        foreach (var (list, provider) in lists)
        {
            if (list is not null)
            {
                result.Add(list, provider);
            }
        }

        if (token.IsCancellationRequested)
        {
            result.Dispose();
            return CodeLensModel.Empty;
        }

        result.Lenses = result.Lenses
            .Order(Comparer<CodeLensItem>.Create((a, b) =>
            {
                // sort by line number, provider rank and column
                var byLine = a.Symbol.Range.StartLineNumber.CompareTo(b.Symbol.Range.StartLineNumber);
                if (byLine != 0)
                {
                    return byLine;
                }

                var byRank = providerRanks[a.Provider].CompareTo(providerRanks[b.Provider]);
                if (byRank != 0)
                {
                    return byRank;
                }

                return a.Symbol.Range.StartColumn.CompareTo(b.Symbol.Range.StartColumn);
            }))
            .ToList();
        return result;
    }
}
