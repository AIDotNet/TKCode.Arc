using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using TKCode.Arc.Gallery.Docs;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// Fetches the text of a docs page or guide from <c>content/</c> on the server when the page is shown, so a visitor
/// downloads the pages they read, in their language. A published site leaves the content assembly out of the boot list
/// and serves the same files (see <c>Content</c> in the project file); the server revalidates them, so a deploy shows at once.
/// The request is the browser's own <c>fetch</c> (the <c>arc-texts</c> module of main.js), as for the screenshots.
/// </summary>
internal sealed partial class HttpTextSource : IGalleryTextSource
{
    private readonly Uri _root;

    /// <param name="root">The document's base address, such as <c>https://host/</c>; <c>content/</c> is relative to it.</param>
    public HttpTextSource(Uri root) => _root = root;

    // Each file once per session (the whole site's text is a few MB): a page opened again, a guide created twice or two views
    // of the same page share one request. A file that could not be read is asked for again next time.
    private readonly Dictionary<string, Task<string>> _cache = new(StringComparer.Ordinal);

    public async ValueTask<string?> ReadAsync(string path)
    {
        if (!_cache.TryGetValue(path, out var pending))
        {
            pending = Load(new Uri(_root, "content/" + path).AbsoluteUri);
            _cache[path] = pending;
        }
        var text = await pending;
        if (text.Length == 0)
        {
            _cache.Remove(path);
            return null;
        }
        return text;
    }

    /// <summary>The file's text, or an empty string when the server has none or cannot be reached.</summary>
    [JSImport("load", "arc-texts")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    private static partial Task<string> Load(string url);
}
