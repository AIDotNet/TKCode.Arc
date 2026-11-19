namespace StreamMark.Avalonia.Images;

/// <summary>
/// 按协议获取图片原始字节。内置 <see cref="DataUriFetcher"/>、<see cref="HttpsFetcher"/>、<see cref="FileFetcher"/>；
/// 宿主可通过 <see cref="ImagePipelineOptions.Fetchers"/> 注册自定义协议（优先于内置实现）。
/// 实现必须线程安全。
/// </summary>
public interface IImageFetcher
{
    /// <summary>是否处理该协议（小写，不含冒号，例如 <c>https</c>）。</summary>
    bool CanFetch(string scheme);

    /// <summary>
    /// 是否为远程来源：远程来源受 <see cref="ImagePolicy.AutoLoad"/>（用户同意）约束，并写入磁盘缓存。
    /// 本地来源（data:、file:）不需要同意，也不落盘。
    /// </summary>
    bool IsRemote { get; }

    /// <summary>获取图片。失败时返回 <see cref="ImageFetchStatus.Failed"/> 而不是抛出；调用方取消时抛出 <see cref="OperationCanceledException"/>。</summary>
    Task<ImageFetchResult> FetchAsync(ImageFetchRequest request, CancellationToken cancellationToken);
}

/// <summary>一次获取请求。</summary>
public sealed record ImageFetchRequest
{
    /// <summary>规范化后的地址字符串（data: URI 原样保留）。</summary>
    public required string Url { get; init; }

    /// <summary>解析后的地址；data: URI 可能过长而为 null。</summary>
    public Uri? Uri { get; init; }

    /// <summary>当前生效的策略。</summary>
    public required ImagePolicy Policy { get; init; }

    /// <summary>条件请求：上次的 ETag（原样，含引号）。</summary>
    public string? IfNoneMatch { get; init; }

    /// <summary>条件请求：上次的 Last-Modified。</summary>
    public DateTimeOffset? IfModifiedSince { get; init; }
}

/// <summary>获取结果状态。</summary>
public enum ImageFetchStatus
{
    /// <summary>成功取得内容。</summary>
    Ok,
    /// <summary>条件请求返回 304，内容未变化。</summary>
    NotModified,
    /// <summary>失败（网络错误、非图片、超出大小等），会进入负缓存。</summary>
    Failed,
    /// <summary>被策略拒绝（例如重定向到禁止的域名、本地路径越界），不进入负缓存。</summary>
    Blocked,
}

/// <summary>响应中与缓存相关的头部。</summary>
public sealed record ImageCacheHeaders
{
    /// <summary>无任何缓存信息。</summary>
    public static ImageCacheHeaders None { get; } = new();

    public string? ETag { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    /// <summary>Cache-Control: max-age。</summary>
    public TimeSpan? MaxAge { get; init; }
    /// <summary>Cache-Control: no-store。</summary>
    public bool NoStore { get; init; }
    /// <summary>Cache-Control: no-cache（可存储，但每次使用前都要重新验证）。</summary>
    public bool NoCache { get; init; }
    /// <summary>Cache-Control: must-revalidate（过期后重新验证失败时不得使用旧内容）。</summary>
    public bool MustRevalidate { get; init; }
    /// <summary>Expires 头部（解析成功时）。</summary>
    public DateTimeOffset? Expires { get; init; }
    /// <summary>存在 Expires 头部但无法解析（按 HTTP 语义视为已过期）。</summary>
    public bool ExpiresInvalid { get; init; }
    /// <summary>Date 头部。</summary>
    public DateTimeOffset? Date { get; init; }
    /// <summary>Age 头部。</summary>
    public TimeSpan? Age { get; init; }

    /// <summary>
    /// 计算新鲜期：null 表示响应未给出显式新鲜度（由调用方套用默认值）。no-cache 返回 0。
    /// </summary>
    public TimeSpan? ExplicitFreshness(DateTimeOffset now)
    {
        if (NoCache)
        {
            return TimeSpan.Zero;
        }
        TimeSpan? lifetime = null;
        if (MaxAge is { } maxAge)
        {
            lifetime = maxAge;
        }
        else if (Expires is { } expires)
        {
            lifetime = expires - (Date ?? now);
        }
        else if (ExpiresInvalid)
        {
            lifetime = TimeSpan.Zero;
        }
        if (lifetime is { } value)
        {
            value -= Age ?? TimeSpan.Zero;
            return value < TimeSpan.Zero ? TimeSpan.Zero : value;
        }
        return null;
    }
}

/// <summary>获取结果。</summary>
public sealed class ImageFetchResult
{
    private ImageFetchResult(ImageFetchStatus status)
    {
        Status = status;
    }

    public ImageFetchStatus Status { get; }

    /// <summary>原始字节（仅 <see cref="ImageFetchStatus.Ok"/>）。</summary>
    public byte[]? Data { get; private init; }

    /// <summary>Content-Type（不含参数，小写）。</summary>
    public string? ContentType { get; private init; }

    /// <summary>缓存头部（Ok 与 NotModified）。</summary>
    public ImageCacheHeaders Cache { get; private init; } = ImageCacheHeaders.None;

    /// <summary>失败或拒绝原因。</summary>
    public string? Error { get; private init; }

    public static ImageFetchResult Ok(byte[] data, string? contentType, ImageCacheHeaders? cache = null) =>
        new(ImageFetchStatus.Ok) { Data = data, ContentType = contentType, Cache = cache ?? ImageCacheHeaders.None };

    public static ImageFetchResult NotModified(ImageCacheHeaders? cache = null) =>
        new(ImageFetchStatus.NotModified) { Cache = cache ?? ImageCacheHeaders.None };

    public static ImageFetchResult Failed(string error) => new(ImageFetchStatus.Failed) { Error = error };

    public static ImageFetchResult Blocked(string reason) => new(ImageFetchStatus.Blocked) { Error = reason };
}
