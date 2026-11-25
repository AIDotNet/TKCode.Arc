// Ported from vscode (src/vs/editor/contrib/links/browser/getLinks.ts: Link, LinksList and getLinks, commit b57d5c9e),
// MIT license.
// Differences:
//  - upstream's Link and LinksList are ProviderLink and ProviderLinksList here (the provider data types of
//    languages.ts are already Link and LinksList);
//  - resolve returns the target as a Uri or a string through LinkTarget instead of `URI | string`, and fails with
//    LinkResolveException("missing") as upstream rejects with Error('missing');
//  - provider errors go to the callback the caller passes (upstream's onUnexpectedExternalError).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Links;

/// <summary>Where a link goes: a URI, or text that is not one.</summary>
internal readonly record struct LinkTarget(Uri? Uri, string? Text)
{
    public override string ToString() => Uri?.OriginalString ?? Text ?? string.Empty;
}

/// <summary>A link that cannot be opened: 'missing' (no target) or 'invalid' (upstream rejects with these messages).</summary>
internal sealed class LinkResolveException(string message) : Exception(message);

/// <summary>A link with the provider that found it, which can resolve its target (upstream's Link in getLinks.ts).</summary>
internal sealed class ProviderLink
{
    private readonly ILinkProvider _provider;
    private Link _link;

    public ProviderLink(Link link, ILinkProvider provider)
    {
        _link = link;
        _provider = provider;
    }

    public TextRange Range => _link.Range;

    public Uri? Url => _link.Url;

    public string? UrlText => _link.UrlText;

    public string? Tooltip => _link.Tooltip;

    /// <summary>The link's target, resolving it with its provider when it has none yet (upstream's resolve).</summary>
    public async Task<LinkTarget> Resolve(CancellationToken token)
    {
        while (true)
        {
            if (_link.Url is not null || _link.UrlText is not null)
            {
                return new LinkTarget(_link.Url, _link.UrlText);
            }

            if (!_provider.SupportsResolveLink)
            {
                throw new LinkResolveException("missing");
            }

            var value = await _provider.ResolveLink(_link, token).ConfigureAwait(true);
            _link = value ?? _link;
            if (_link.Url is null && _link.UrlText is null)
            {
                throw new LinkResolveException("missing");
            }
        }
    }
}

/// <summary>The links of every provider, merged (upstream's LinksList in getLinks.ts).</summary>
internal sealed class ProviderLinksList : IDisposable
{
    public static ProviderLinksList Empty => new([]);

    private readonly List<Action> _disposables = [];

    public ProviderLinksList(IReadOnlyList<(LinksList List, ILinkProvider Provider)> tuples)
    {
        var links = new List<ProviderLink>();
        foreach (var (list, provider) in tuples)
        {
            // merge all links
            var newLinks = list.Links.Select(link => new ProviderLink(link, provider)).ToList();
            links = Union(links, newLinks);
            // register disposables
            if (list.Dispose is { } dispose)
            {
                _disposables.Add(dispose);
            }
        }

        Links = links;
    }

    public List<ProviderLink> Links { get; }

    public void Dispose()
    {
        foreach (var dispose in _disposables)
        {
            dispose();
        }

        _disposables.Clear();
        Links.Clear();
    }

    // reunite oldLinks with newLinks and remove duplicates: a new link replaces old ones it touches
    private static List<ProviderLink> Union(List<ProviderLink> oldLinks, List<ProviderLink> newLinks)
    {
        var result = new List<ProviderLink>();
        int oldIndex = 0, newIndex = 0;
        int oldLen = oldLinks.Count, newLen = newLinks.Count;
        while (oldIndex < oldLen && newIndex < newLen)
        {
            var oldLink = oldLinks[oldIndex];
            var newLink = newLinks[newIndex];

            if (TextRange.AreIntersectingOrTouching(oldLink.Range, newLink.Range))
            {
                // Remove the oldLink
                oldIndex++;
                continue;
            }

            var comparisonResult = TextRange.CompareRangesUsingStarts(oldLink.Range, newLink.Range);
            if (comparisonResult < 0)
            {
                // oldLink is before
                result.Add(oldLink);
                oldIndex++;
            }
            else
            {
                // newLink is before
                result.Add(newLink);
                newIndex++;
            }
        }

        for (; oldIndex < oldLen; oldIndex++)
        {
            result.Add(oldLinks[oldIndex]);
        }

        for (; newIndex < newLen; newIndex++)
        {
            result.Add(newLinks[newIndex]);
        }

        return result;
    }
}

/// <summary>Upstream's getLinks.</summary>
internal static class LinksComputer
{
    /// <summary>
    /// Asks every link provider of <paramref name="model"/> at once and merges their links; a better provider's link
    /// wins over the links it touches.
    /// </summary>
    public static async Task<ProviderLinksList> GetLinks(
        LanguageFeatureRegistry<ILinkProvider> providers,
        TextModel model,
        CancellationToken token,
        Action<Exception>? onUnexpectedExternalError = null)
    {
        // ask all providers for links in parallel
        var ordered = providers.Ordered(model).Reverse().ToList();
        var lists = new (LinksList List, ILinkProvider Provider)?[ordered.Count];
        var tasks = new List<Task>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var index = i;
            var provider = ordered[i];
            tasks.Add(Ask(provider, index));
        }

        await Task.WhenAll(tasks).ConfigureAwait(true);

        var res = new ProviderLinksList(lists.Where(static l => l is not null).Select(static l => l!.Value).ToList());
        if (token.IsCancellationRequested)
        {
            res.Dispose();
            res = ProviderLinksList.Empty;
        }

        return res;

        async Task Ask(ILinkProvider provider, int index)
        {
            try
            {
                if (await provider.ProvideLinks(model, token).ConfigureAwait(true) is { } result)
                {
                    lists[index] = (result, provider);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                onUnexpectedExternalError?.Invoke(error);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
