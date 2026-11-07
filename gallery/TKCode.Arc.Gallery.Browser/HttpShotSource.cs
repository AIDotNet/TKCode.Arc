using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// Fetches the screenshots of the docs pages from <c>shots/</c> on the server when a page shows one, so a visitor
/// downloads only the pictures of the pages they open. A published site leaves the screenshots assembly out of the boot
/// list and serves the same files (see <c>Content</c> in the project file); the browser caches each one. The request is the
/// browser's own <c>fetch</c> (the <c>arc-shots</c> module of main.js), not <c>HttpClient</c>: a picture for a page the visitor has
/// already left must not be cancelled half way.
/// </summary>
internal sealed partial class HttpShotSource : IGalleryShotSource
{
    private readonly Uri _root;

    /// <param name="root">The document's base address, such as <c>https://host/</c>; <c>shots/</c> is relative to it.</param>
    public HttpShotSource(Uri root) => _root = root;

    // Kept for the session up to this many bytes (all captures together are about 4.5 MB as WebP), so a page opened again
    // or a theme switched back shows its pictures without a request; two views asking at once share one fetch (take()
    // hands the bytes over only once).
    private const long CacheLimit = 16 * 1024 * 1024;
    private readonly Dictionary<string, Task<byte[]>> _cache = new(StringComparer.Ordinal);
    private long _cached;

    public async ValueTask<Stream?> OpenAsync(Uri uri)
    {
        // uri is avares://TKCode.Arc.Gallery.Shots/Docs/<Category>/<Component>/images/<Name>.webp
        var url = new Uri(_root, "shots" + uri.AbsolutePath).AbsoluteUri;
        if (!_cache.TryGetValue(url, out var pending))
        {
            pending = Fetch(url);
            _cache[url] = pending;
        }
        // A capture the server does not have (a light-only one asked for in dark) stays known as missing.
        var bytes = await pending;
        return bytes.Length == 0 ? null : new MemoryStream(bytes, writable: false);
    }

    private async Task<byte[]> Fetch(string url)
    {
        await Load(url);
        var bytes = Take(url);
        _cached += bytes.Length;
        if (_cached > CacheLimit)
        {
            // Rare (a long session in both themes): start over rather than track the order of use.
            _cache.Clear();
            _cached = bytes.Length;
        }
        return bytes;
    }

    /// <summary>Starts the request and completes when the picture, or the answer that there is none, has arrived.</summary>
    [JSImport("load", "arc-shots")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task Load(string url);

    /// <summary>The bytes of the loaded picture, once; none when the server has no such image (a host that answers with its index.html is not one).</summary>
    [JSImport("take", "arc-shots")]
    private static partial byte[] Take(string url);
}
