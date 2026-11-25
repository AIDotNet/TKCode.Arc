// Ported from vscode (src/vs/editor/contrib/semanticTokens/common/getSemanticTokens.ts and semanticTokensConfig.ts,
// commit b57d5c9e), MIT license.
//
// Differences:
//  - the _provideDocumentSemanticTokens* commands (for extensions) are not registered;
//  - SemanticTokens | SemanticTokensEdits is SemanticTokensOrEdits; isSemanticTokens / isSemanticTokensEdits test
//    which member is set;
//  - the semantic highlighting setting is an EditorSemanticHighlighting value the editor holds (upstream reads the
//    editor.semanticHighlighting.enabled configuration per language);
//  - a provider that throws synchronously is treated like one whose promise rejects.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;

namespace TKCode.Arc.Editor.Contrib.SemanticTokens;

/// <summary>Upstream's editor.semanticHighlighting.enabled: true, false or 'configuredByTheme'.</summary>
internal enum EditorSemanticHighlighting
{
    ConfiguredByTheme,
    Enabled,
    Disabled,
}

/// <summary>The result of a document semantic tokens request: the provider that answered and what it answered.</summary>
internal sealed record DocumentSemanticTokensResult(IDocumentSemanticTokensProvider Provider, SemanticTokensOrEdits? Tokens, Exception? Error);

/// <summary>The result of a range semantic tokens request.</summary>
internal sealed record DocumentRangeSemanticTokensResult(IDocumentRangeSemanticTokensProvider Provider, Languages.SemanticTokens? Tokens);

/// <summary>Asks the semantic tokens providers (upstream's getSemanticTokens.ts).</summary>
internal static class GetSemanticTokens
{
    public const string SemanticHighlightingSettingId = "editor.semanticHighlighting";

    public static bool IsSemanticTokens(SemanticTokensOrEdits? v) => v?.Tokens is not null;

    public static bool IsSemanticTokensEdits(SemanticTokensOrEdits? v) => v?.Tokens is null && v?.Edits is not null;

    /// <summary>Upstream's isSemanticColoringEnabled.</summary>
    public static bool IsSemanticColoringEnabled(EditorSemanticHighlighting setting, ISemanticTokensThemeService themeService) => setting switch
    {
        EditorSemanticHighlighting.Enabled => true,
        EditorSemanticHighlighting.Disabled => false,
        _ => themeService.GetColorTheme().SemanticHighlighting,
    };

    public static bool HasDocumentSemanticTokensProvider(LanguageFeatureRegistry<IDocumentSemanticTokensProvider> registry, TextModel model) =>
        registry.Has(model);

    private static IReadOnlyList<T> GetProviders<T>(LanguageFeatureRegistry<T> registry, TextModel model)
        where T : class
    {
        var groups = registry.OrderedGroups(model);
        return groups.Count > 0 ? groups[0] : [];
    }

    public static async Task<DocumentSemanticTokensResult?> GetDocumentSemanticTokens(
        LanguageFeatureRegistry<IDocumentSemanticTokensProvider> registry,
        TextModel model,
        IDocumentSemanticTokensProvider? lastProvider,
        string? lastResultId,
        CancellationToken token)
    {
        var providers = GetProviders(registry, model);

        // Get tokens from all providers at the same time.
        var results = await Task.WhenAll(providers.Select(async provider =>
        {
            SemanticTokensOrEdits? result;
            Exception? error = null;
            try
            {
                result = await provider.ProvideDocumentSemanticTokens(model, ReferenceEquals(provider, lastProvider) ? lastResultId : null, token).ConfigureAwait(true);
            }
            catch (Exception err)
            {
                error = err;
                result = null;
            }

            if (result is null || (!IsSemanticTokens(result) && !IsSemanticTokensEdits(result)))
            {
                result = null;
            }

            return new DocumentSemanticTokensResult(provider, result, error);
        })).ConfigureAwait(true);

        // Try to return the first result with actual tokens or the first result which threw an error (!!)
        foreach (var result in results)
        {
            if (result.Error is { } error)
            {
                throw error;
            }

            if (result.Tokens is not null)
            {
                return result;
            }
        }

        // Return the first result, even if it doesn't have tokens
        return results.Length > 0 ? results[0] : null;
    }

    public static bool HasDocumentRangeSemanticTokensProvider(LanguageFeatureRegistry<IDocumentRangeSemanticTokensProvider> providers, TextModel model) =>
        providers.Has(model);

    public static async Task<DocumentRangeSemanticTokensResult?> GetDocumentRangeSemanticTokens(
        LanguageFeatureRegistry<IDocumentRangeSemanticTokensProvider> registry,
        TextModel model,
        TextRange range,
        CancellationToken token)
    {
        var providers = GetProviders(registry, model);

        // Get tokens from all providers at the same time.
        var results = await Task.WhenAll(providers.Select(async provider =>
        {
            Languages.SemanticTokens? result;
            try
            {
                result = await provider.ProvideDocumentRangeSemanticTokens(model, range, token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                result = null;
            }
            catch (Exception err)
            {
                UnexpectedErrors.Report(err);
                result = null;
            }

            return new DocumentRangeSemanticTokensResult(provider, result);
        })).ConfigureAwait(true);

        // Try to return the first result with actual tokens
        foreach (var result in results)
        {
            if (result.Tokens is not null)
            {
                return result;
            }
        }

        // Return the first result, even if it doesn't have tokens
        return results.Length > 0 ? results[0] : null;
    }
}
