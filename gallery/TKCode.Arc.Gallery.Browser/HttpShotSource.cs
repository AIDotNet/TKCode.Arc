using System;
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
    private string[] _extensions = [".webp", ".png"];

    /// <param name="root">The document's base address, such as <c>https://host/</c>; <c>shots/</c> is relative to it.</param>
    public HttpShotSource(Uri root) => _root = root;

    public async ValueTask<Stream?> OpenAsync(Uri uri)
    {
        // uri is avares://TKCode.Arc.Gallery.Shots/Docs/<Category>/<Component>/images/<Name>.png; the file is <Name>.webp when the
        // publish converted the screenshots, else <Name>.png.
        var stem = new Uri(_root, "shots" + uri.AbsolutePath[..^".png".Length]).AbsoluteUri;
        foreach (var extension in _extensions)
        {
            var url = stem + extension;
            await Load(url);
            var bytes = Take(url);
            if (bytes.Length > 0)
            {
                // The first answer tells which kind the site has: a site that only has PNG is not asked for WebP again.
                if (extension == ".png")
                {
                    _extensions = [".png"];
                }
                return new MemoryStream(bytes);
            }
        }
        return null;
    }

    /// <summary>Starts the request and completes when the picture, or the answer that there is none, has arrived.</summary>
    [JSImport("load", "arc-shots")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    private static partial Task Load(string url);

    /// <summary>The bytes of the loaded picture, once; none when the server has no such image (a host that answers with its index.html is not one).</summary>
    [JSImport("take", "arc-shots")]
    private static partial byte[] Take(string url);
}
