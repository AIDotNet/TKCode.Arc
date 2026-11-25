// Ported from vscode (src/vs/editor/contrib/gotoSymbol/browser/goToSymbol.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - a provider result is a list of LocationLink (a Location converts to one), so flattening is a SelectMany;
//  - the _execute*Provider API commands are not ported; SortedAndDeduped is their sorting and de-duplication.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.GotoSymbol;

/// <summary>Asks the definition, declaration, implementation, type definition and reference providers (upstream's goToSymbol.ts).</summary>
internal static class GoToSymbol
{
    // schemes of embedded snippets whose locations are left out unless the source has the same scheme
    private static readonly string[] s_internalSchemes = ["walkThroughSnippet", "vscode-chat-code-block", "vscode-chat-code-compare-block"];

    public static Task<IReadOnlyList<LocationLink>> GetDefinitionsAtPosition(LanguageFeatureRegistry<IDefinitionProvider> registry, TextModel model, TextPosition position, bool recursive, CancellationToken token) =>
        GetLocationLinks(model, position, registry, recursive, (provider, m, p) => provider.ProvideDefinition(m, p, token));

    public static Task<IReadOnlyList<LocationLink>> GetDeclarationsAtPosition(LanguageFeatureRegistry<IDeclarationProvider> registry, TextModel model, TextPosition position, bool recursive, CancellationToken token) =>
        GetLocationLinks(model, position, registry, recursive, (provider, m, p) => provider.ProvideDeclaration(m, p, token));

    public static Task<IReadOnlyList<LocationLink>> GetImplementationsAtPosition(LanguageFeatureRegistry<IImplementationProvider> registry, TextModel model, TextPosition position, bool recursive, CancellationToken token) =>
        GetLocationLinks(model, position, registry, recursive, (provider, m, p) => provider.ProvideImplementation(m, p, token));

    public static Task<IReadOnlyList<LocationLink>> GetTypeDefinitionsAtPosition(LanguageFeatureRegistry<ITypeDefinitionProvider> registry, TextModel model, TextPosition position, bool recursive, CancellationToken token) =>
        GetLocationLinks(model, position, registry, recursive, (provider, m, p) => provider.ProvideTypeDefinition(m, p, token));

    /// <summary>
    /// The references at the position, declaration included; <paramref name="compact"/>: when a provider returns two
    /// references and one of them is the declaration, only the other (upstream's getReferencesAtPosition).
    /// </summary>
    public static Task<IReadOnlyList<LocationLink>> GetReferencesAtPosition(LanguageFeatureRegistry<IReferenceProvider> registry, TextModel model, TextPosition position, bool compact, bool recursive, CancellationToken token) =>
        GetLocationLinks(model, position, registry, recursive, async (provider, m, p) =>
        {
            var result = (await provider.ProvideReferences(m, p, new ReferenceContext(true), token).ConfigureAwait(true))?
                .Where(reference => ShouldIncludeLocationLink(m, reference)).ToList();
            if (!compact || result is null || result.Count != 2)
            {
                return result?.Select(static l => (LocationLink)l).ToList();
            }

            var resultWithoutDeclaration = (await provider.ProvideReferences(m, p, new ReferenceContext(false), token).ConfigureAwait(true))?
                .Where(reference => ShouldIncludeLocationLink(m, reference)).ToList();
            if (resultWithoutDeclaration is { Count: 1 })
            {
                return resultWithoutDeclaration.Select(static l => (LocationLink)l).ToList();
            }

            return result.Select(static l => (LocationLink)l).ToList();
        });

    /// <summary>The links sorted by document and range, without duplicates (upstream's _sortedAndDeduped).</summary>
    public static IReadOnlyList<LocationLink> SortedAndDeduped(IReadOnlyList<LocationLink> links) =>
        [.. new ReferencesModel(links, string.Empty).References.Select(static reference => reference.Link)];

    private static bool ShouldIncludeLocationLink(TextModel sourceModel, Location location) =>
        ShouldIncludeLocationLink(sourceModel, location.Uri);

    private static bool ShouldIncludeLocationLink(TextModel sourceModel, Uri uri)
    {
        // always allow the location if the request comes from the same scheme
        if (uri.Scheme == sourceModel.Uri.Scheme)
        {
            return true;
        }

        // otherwise filter out locations from internal schemes
        return !s_internalSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<LocationLink>> GetLocationLinks<T>(
        TextModel model,
        TextPosition position,
        LanguageFeatureRegistry<T> registry,
        bool recursive,
        Func<T, TextModel, TextPosition, Task<IReadOnlyList<LocationLink>?>> provide)
        where T : class
    {
        var providers = registry.Ordered(model, recursive);

        // get results
        var tasks = providers.Select(async provider =>
        {
            try
            {
                return await provide(provider, model, position).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception error)
            {
                UnexpectedErrors.Report(error);
                return null;
            }
        });

        var values = await Task.WhenAll(tasks).ConfigureAwait(true);
        return [.. values.Where(static v => v is not null).SelectMany(static v => v!).Where(link => link is not null && ShouldIncludeLocationLink(model, link.Uri))];
    }
}
