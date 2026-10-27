using System;
using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// Reads a map outline of the Choropleth explorer demos from <c>content/Demos/Blocks/ChoroplethExplorer/Maps/</c> on the
/// server, so the outlines (about 680 KB) are not part of the downloaded gallery. The demo data asks for a file in
/// synchronous code, so the request is synchronous (the <c>arc-maps</c> module of main.js); each area is asked for once.
/// </summary>
internal sealed partial class HttpMapSource
{
    private readonly Uri _root;

    /// <param name="root">The document's base address, such as <c>https://host/</c>.</param>
    public HttpMapSource(Uri root) => _root = root;

    /// <summary>The text of <paramref name="file"/> (<c>map-us.txt</c>), or null when the server has none.</summary>
    public string? Read(string file)
    {
        var text = Load(new Uri(_root, "content/Demos/Blocks/ChoroplethExplorer/Maps/" + file).AbsoluteUri);
        return text.Length == 0 ? null : text;
    }

    [JSImport("loadSync", "arc-maps")]
    private static partial string Load(string url);
}
