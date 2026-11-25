// Ported from vscode (src/vs/editor/contrib/hover/browser/getHover.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - a hover without a range is valid (the contract's Hover.Range: null uses the anchor); upstream drops it
//    (isValid needs a range) although its participant falls back to the anchor range as well;
//  - the _executeHoverProvider commands are not registered (there is no process-wide command registry);
//  - provider errors go to UnexpectedErrors.Report (upstream's onUnexpectedExternalError), cancellation is not one.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>A provider's hover and where the provider stands in the order (upstream's HoverProviderResult).</summary>
internal sealed record HoverProviderResult(IHoverProvider Provider, Languages.Hover Hover, int Ordinal);

/// <summary>Asks the hover providers (upstream's getHover.ts functions).</summary>
internal static class GetHover
{
    private static async Task<HoverProviderResult?> ExecuteProvider(IHoverProvider provider, int ordinal, TextModel model, TextPosition position, CancellationToken token)
    {
        Languages.Hover? result;
        try
        {
            result = await provider.ProvideHover(model, position, token).ConfigureAwait(true);
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

        if (result is null || !IsValid(result))
        {
            return null;
        }

        return new HoverProviderResult(provider, result, ordinal);
    }

    /// <summary>The providers' hovers in the order they arrive (upstream's getHoverProviderResultsAsAsyncIterable).</summary>
    public static IAsyncEnumerable<HoverProviderResult> GetHoverProviderResultsAsAsyncIterable(
        LanguageFeatureRegistry<IHoverProvider> registry,
        TextModel model,
        TextPosition position,
        CancellationToken token,
        bool recursive = false)
    {
        var providers = registry.Ordered(model, recursive);
        var tasks = providers.Select((provider, index) => ExecuteProvider(provider, index, model, position, token)).ToList();
        return AsyncEnumerables.FromTasksResolveOrder(tasks);
    }

    /// <summary>All providers' hovers (upstream's getHoversPromise).</summary>
    public static async Task<IReadOnlyList<Languages.Hover>> GetHoversPromise(
        LanguageFeatureRegistry<IHoverProvider> registry,
        TextModel model,
        TextPosition position,
        CancellationToken token,
        bool recursive = false)
    {
        var result = new List<Languages.Hover>();
        await foreach (var item in GetHoverProviderResultsAsAsyncIterable(registry, model, position, token, recursive).ConfigureAwait(true))
        {
            result.Add(item.Hover);
        }

        return result;
    }

    private static bool IsValid(Languages.Hover result) => result.Contents is { Count: > 0 };
}
