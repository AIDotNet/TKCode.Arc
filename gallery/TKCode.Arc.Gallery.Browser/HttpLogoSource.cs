using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// Fetches the logos of the Friends page from <c>friends/&lt;name&gt;.png</c> on the server (<c>wwwroot/friends</c>), with the
/// browser's own <c>fetch</c> (the <c>arc-shots</c> module of main.js). A logo is fetched once; the bytes are kept for the
/// home page and the Friends page, which show the same ones.
/// </summary>
internal sealed partial class HttpLogoSource : IGalleryLogoSource
{
    private readonly Uri _root;
    // The fetch of each logo, shared by every view that asks for it: the page's take() hands the bytes over once, so two
    // views that asked at the same time would otherwise leave the second with nothing (shown as initials all session).
    private readonly Dictionary<string, Task<byte[]>> _cache = new();

    public HttpLogoSource(Uri root) => _root = root;

    public async ValueTask<Stream?> OpenAsync(string name)
    {
        if (!_cache.TryGetValue(name, out var pending))
        {
            pending = Fetch(new Uri(_root, $"friends/{name}.png").AbsoluteUri);
            _cache[name] = pending;
        }
        var bytes = await pending;
        return bytes.Length == 0 ? null : new MemoryStream(bytes, writable: false);
    }

    private static async Task<byte[]> Fetch(string url)
    {
        await Load(url);
        return Take(url);
    }

    [JSImport("load", "arc-shots")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task Load(string url);

    [JSImport("take", "arc-shots")]
    private static partial byte[] Take(string url);
}
