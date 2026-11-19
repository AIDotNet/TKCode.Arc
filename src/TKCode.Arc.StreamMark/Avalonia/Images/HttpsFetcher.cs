using System.Buffers;
using System.Net;
using System.Net.Http.Headers;

namespace StreamMark.Avalonia.Images;

/// <summary>
/// 基于 <see cref="HttpClient"/> 获取 https（以及策略允许时的 http）图片：
/// 不发送也不保存 Cookie；手动跟随重定向并对每一跳重新检查策略；校验 Content-Type 为 image/*；
/// 边读边检查大小上限；整体超时；支持 If-None-Match / If-Modified-Since 条件请求。
/// </summary>
public sealed class HttpsFetcher : IImageFetcher, IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    /// <summary>使用内置的 <see cref="SocketsHttpHandler"/>（无 Cookie、不自动重定向、自动解压）。</summary>
    public HttpsFetcher()
        : this(CreatePlatformHandler(), disposeHandler: true)
    {
    }

    /// <summary>使用宿主提供的处理器（例如设置代理）。处理器必须关闭自动重定向，否则重定向次数与策略检查失效。</summary>
    public HttpsFetcher(HttpMessageHandler handler, bool disposeHandler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _client = new HttpClient(handler, disposeHandler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        _ownsClient = true;
    }

    /// <summary>
    /// 浏览器没有 <see cref="SocketsHttpHandler"/>（它的 Cookie、自动重定向等设置会抛 <see cref="PlatformNotSupportedException"/>），
    /// 改用平台处理器。浏览器的 fetch 自动跟随重定向，因此逐跳策略检查只在桌面生效。
    /// </summary>
    private static HttpMessageHandler CreatePlatformHandler() =>
        OperatingSystem.IsBrowser() ? new HttpClientHandler() : CreateDefaultHandler();

    /// <summary>创建默认处理器。</summary>
    public static SocketsHttpHandler CreateDefaultHandler() => new()
    {
        UseCookies = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(15),
    };

    public bool CanFetch(string scheme) => scheme is "https" or "http";

    public bool IsRemote => true;

    public async Task<ImageFetchResult> FetchAsync(ImageFetchRequest request, CancellationToken cancellationToken)
    {
        var policy = request.Policy;
        var uri = request.Uri ?? (Uri.TryCreate(request.Url, UriKind.Absolute, out var parsed) ? parsed : null);
        if (uri is null)
        {
            return ImageFetchResult.Failed("invalid url");
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(policy.Timeout);
        try
        {
            for (var redirects = 0; ; redirects++)
            {
                if (policy.CheckUri(uri) is { } blocked)
                {
                    return ImageFetchResult.Blocked(blocked);
                }
                using var message = new HttpRequestMessage(HttpMethod.Get, uri);
                message.Headers.Accept.ParseAdd("image/avif,image/webp,image/png,image/jpeg,image/gif,image/*;q=0.8");
                if (!string.IsNullOrEmpty(policy.UserAgent))
                {
                    message.Headers.TryAddWithoutValidation("User-Agent", policy.UserAgent);
                }
                if (!string.IsNullOrEmpty(request.IfNoneMatch))
                {
                    message.Headers.TryAddWithoutValidation("If-None-Match", request.IfNoneMatch);
                }
                if (request.IfModifiedSince is { } since)
                {
                    message.Headers.IfModifiedSince = since;
                }
                using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                var status = (int)response.StatusCode;
                if (status is 301 or 302 or 303 or 307 or 308)
                {
                    var location = response.Headers.Location;
                    if (location is null)
                    {
                        return ImageFetchResult.Failed($"HTTP {status} without location");
                    }
                    if (redirects >= policy.MaxRedirects)
                    {
                        return ImageFetchResult.Failed("too many redirects");
                    }
                    uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                    continue;
                }
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return ImageFetchResult.NotModified(ReadCacheHeaders(response));
                }
                if (!response.IsSuccessStatusCode)
                {
                    return ImageFetchResult.Failed($"HTTP {status}");
                }
                var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
                if (contentType is null || !contentType.StartsWith("image/", StringComparison.Ordinal))
                {
                    return ImageFetchResult.Failed("not an image: " + (contentType ?? "(none)"));
                }
                if (response.Content.Headers.ContentLength is { } length && length > policy.MaxBytes)
                {
                    return ImageFetchResult.Failed("image too large");
                }
                var data = await ReadCappedAsync(response.Content, policy.MaxBytes, timeout.Token).ConfigureAwait(false);
                if (data is null)
                {
                    return ImageFetchResult.Failed("image too large");
                }
                return ImageFetchResult.Ok(data, contentType, ReadCacheHeaders(response));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ImageFetchResult.Failed("timeout");
        }
        catch (HttpRequestException ex)
        {
            return ImageFetchResult.Failed("network error: " + ex.Message);
        }
        catch (IOException ex)
        {
            return ImageFetchResult.Failed("network error: " + ex.Message);
        }
    }

    private static async Task<byte[]?> ReadCappedAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    return null;
                }
                buffer.Write(chunk, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
        return buffer.ToArray();
    }

    internal static ImageCacheHeaders ReadCacheHeaders(HttpResponseMessage response)
    {
        CacheControlHeaderValue? cacheControl = response.Headers.CacheControl;
        var content = response.Content.Headers;
        var hasExpires = content.TryGetValues("Expires", out _);
        return new ImageCacheHeaders
        {
            ETag = response.Headers.ETag?.ToString()
                   ?? (response.Headers.TryGetValues("ETag", out var raw) ? raw.FirstOrDefault() : null),
            LastModified = content.LastModified,
            MaxAge = cacheControl?.MaxAge,
            NoStore = cacheControl?.NoStore ?? false,
            NoCache = cacheControl?.NoCache ?? false,
            MustRevalidate = cacheControl?.MustRevalidate ?? false,
            Expires = content.Expires,
            ExpiresInvalid = hasExpires && content.Expires is null,
            Date = response.Headers.Date,
            Age = response.Headers.Age,
        };
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
