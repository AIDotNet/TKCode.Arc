using Avalonia.Platform;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Reads the encoded bytes of an <see cref="ImageSource"/> for <see cref="ArcImage"/>, which decodes, sizes and caches them.
/// Replace <see cref="ArcImage.DefaultLoader"/> (or set <see cref="ArcImage.Loader"/> on a subtree) to add headers, use
/// your own HTTP client or serve pictures from a test fixture.
/// </summary>
public interface IArcImageLoader
{
    /// <summary>
    /// Opens the encoded picture (PNG, JPEG, WebP …) of <paramref name="source"/>. Called on the UI thread; the work may
    /// continue anywhere. Throw (or return a faulted task) when the picture cannot be read; honour
    /// <paramref name="cancellationToken"/>, which fires when no control waits for the picture any more.
    /// <see cref="BitmapImageSource"/> never reaches a loader.
    /// </summary>
    Task<Stream> OpenAsync(ImageSource source, CancellationToken cancellationToken);
}

/// <summary>
/// The default <see cref="IArcImageLoader"/>: http(s) through one shared <see cref="HttpClient"/> (the browser's
/// <c>fetch</c> in WebAssembly), <c>avares://</c> through <see cref="AssetLoader"/>, <c>file://</c> from disk,
/// <c>data:</c> addresses inline, and storage files through <see cref="Avalonia.Platform.Storage.IStorageFile.OpenReadAsync"/>.
/// </summary>
public class ArcImageLoader : IArcImageLoader
{
    private static readonly Lazy<HttpClient> s_http = new(static () => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

    /// <summary>The shared instance.</summary>
    public static ArcImageLoader Shared { get; } = new();

    /// <summary>The HTTP client used for http(s) addresses.</summary>
    protected virtual HttpClient Http => s_http.Value;

    /// <inheritdoc />
    public virtual async Task<Stream> OpenAsync(ImageSource source, CancellationToken cancellationToken)
    {
        switch (source)
        {
            case UriImageSource { Uri: var uri }:
                return await OpenUriAsync(uri, cancellationToken).ConfigureAwait(false);
            case StorageFileImageSource { File: var file }:
            {
                await using var stream = await file.OpenReadAsync().ConfigureAwait(false);
                return await CopyAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            default:
                throw new NotSupportedException($"{source.GetType().Name} cannot be opened by {nameof(ArcImageLoader)}.");
        }
    }

    /// <summary>Opens an absolute http(s), <c>avares</c>, <c>file</c> or <c>data</c> address.</summary>
    protected virtual async Task<Stream> OpenUriAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        {
            var bytes = await Http.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
            return new MemoryStream(bytes, writable: false);
        }
        if (uri.Scheme == "avares")
        {
            using var asset = AssetLoader.Open(uri);
            return await CopyAsync(asset, cancellationToken).ConfigureAwait(false);
        }
        if (uri.Scheme == Uri.UriSchemeFile)
        {
            var bytes = await File.ReadAllBytesAsync(uri.LocalPath, cancellationToken).ConfigureAwait(false);
            return new MemoryStream(bytes, writable: false);
        }
        if (uri.Scheme == "data")
        {
            return new MemoryStream(DecodeDataUri(uri.OriginalString), writable: false);
        }
        throw new NotSupportedException($"'{uri.Scheme}' addresses are not supported.");
    }

    /// <summary>The bytes of a <c>data:</c> address (base64 or percent-encoded).</summary>
    internal static byte[] DecodeDataUri(string text)
    {
        var comma = text.IndexOf(',');
        if (comma < 0)
        {
            throw new FormatException("A data: address needs a comma before its content.");
        }
        var header = text.AsSpan(0, comma);
        var body = text[(comma + 1)..];
        return header.Contains(";base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(Uri.UnescapeDataString(body))
            : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(body));
    }

    private static async Task<Stream> CopyAsync(Stream stream, CancellationToken cancellationToken)
    {
        var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        copy.Position = 0;
        return copy;
    }
}
