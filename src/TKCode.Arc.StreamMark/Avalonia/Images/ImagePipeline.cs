using System.Collections.Concurrent;
using Avalonia;

namespace StreamMark.Avalonia.Images;

/// <summary><see cref="ImagePipeline"/> 的构造选项。</summary>
public sealed record ImagePipelineOptions
{
    /// <summary>初始策略（之后可通过 <see cref="ImagePipeline.Policy"/> 修改）。</summary>
    public ImagePolicy Policy { get; init; } = ImagePolicy.Default;

    /// <summary>内存缓存的解码字节预算，默认 96MB。</summary>
    public long MemoryBudgetBytes { get; init; } = ImageMemoryCache.DefaultByteBudget;

    /// <summary>失败地址的负缓存时长，默认 5 分钟。</summary>
    public TimeSpan NegativeCacheDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>磁盘缓存目录；null 表示不启用磁盘缓存。</summary>
    public string? DiskCacheDirectory { get; init; }

    /// <summary>磁盘缓存容量上限，默认 512MB。</summary>
    public long DiskCacheMaxBytes { get; init; } = ImageDiskCache.DefaultMaxBytes;

    /// <summary>宿主注册的获取器（自定义协议或替换内置实现），优先于内置的 data:/https:/file:。</summary>
    public IReadOnlyList<IImageFetcher> Fetchers { get; init; } = [];

    /// <summary>内置 https 获取器使用的处理器（例如设置代理）；null 表示默认 <see cref="SocketsHttpHandler"/>。必须关闭自动重定向。</summary>
    public HttpMessageHandler? HttpHandler { get; init; }

    /// <summary>时钟（测试可注入）；null 表示系统时钟。</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>SVG 栅格化器（例如 StreamMark.Mermaid 的 <c>SvgImageRasterizer</c>）；null 表示不支持 SVG。</summary>
    public ISvgRasterizer? SvgRasterizer { get; init; }

    /// <summary>
    /// 文档内图片的解码密度上限：每个逻辑像素最多解码多少物理像素（之后可通过 <see cref="ImagePipeline.DecodeDensity"/> 修改）。
    /// null 表示按窗口渲染缩放解码（Retina 上为 2，最清晰）；1 表示按逻辑尺寸解码，Retina 上内存约为 1/4，画面略软。
    /// 只影响文档内显示，点击图片打开的查看器始终加载原图。
    /// </summary>
    public double? DecodeDensity { get; init; }

    /// <summary>
    /// 磁盘缩略图：按宽度档位缩小解码（或栅格化 SVG）后，把结果编码存进磁盘缓存，再次打开时直接读缩略图，
    /// 不再解码原图（避免整张原图解码的内存峰值与耗时）。需要 <see cref="DiskCacheDirectory"/>；默认开启。
    /// 本地文件按大小与修改时间、远程图片按原图缓存条目判断缩略图是否失效。
    /// </summary>
    public bool DiskThumbnails { get; init; } = true;

    /// <summary>缩略图编码器（例如 StreamMark.Mermaid 的 WebP 编码器）；null 表示用 Avalonia 编码为 PNG。</summary>
    public IImageEncoder? ThumbnailEncoder { get; init; }
}

/// <summary>把解码后的位图编码为可存盘的字节（在线程池上调用，必须线程安全）。</summary>
public interface IImageEncoder
{
    /// <summary>编码；失败返回 null（该缩略图不落盘）。</summary>
    byte[]? Encode(global::Avalonia.Media.Imaging.Bitmap bitmap);
}

/// <summary>把 SVG 字节栅格化为位图（在线程池上调用，必须线程安全）。</summary>
public interface ISvgRasterizer
{
    /// <summary>
    /// 栅格化。<paramref name="targetWidth"/> 为期望的像素宽度（0 表示按 SVG 的自然尺寸）；
    /// <paramref name="naturalSize"/> 返回 SVG 的自然尺寸（像素）。失败返回 null。
    /// </summary>
    global::Avalonia.Media.Imaging.Bitmap? Rasterize(byte[] svg, int targetWidth, out global::Avalonia.PixelSize naturalSize);
}

/// <summary>
/// 图片加载管线，每个宿主一份。流程：规范化地址 → 内存缓存 → 合并进行中的同一请求 →
/// 磁盘缓存（新鲜直接用；过期则条件请求重新验证）→ 获取 → 后台解码（按宽度档位缩小）→ 写回两级缓存。
/// 所有公开成员都可以在任意线程调用。
/// </summary>
public sealed class ImagePipeline : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Url, int Bucket), InFlight> _inflight = new();
    private readonly Dictionary<string, Task<BytesResult>> _bytesInflight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _approved = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IImageFetcher> _fetchers;
    private readonly HttpsFetcher _https;
    private readonly ISvgRasterizer? _svg;
    private readonly bool _thumbnails;
    private readonly IImageEncoder? _thumbnailEncoder;
    private readonly CancellationTokenSource _lifetime = new();
    private volatile ImagePolicy _policy;
    private double _decodeDensity; // 0 表示未设置
    private long _networkRequests;
    private long _memoryHits;
    private long _diskHits;
    private long _revalidations;
    private long _notModified;
    private long _decodes;
    private long _thumbnailHits;
    private int _disposed;

    public ImagePipeline(ImagePipelineOptions? options = null)
    {
        options ??= new ImagePipelineOptions();
        _policy = options.Policy ?? ImagePolicy.Default;
        var time = options.TimeProvider ?? TimeProvider.System;
        Memory = new ImageMemoryCache(options.MemoryBudgetBytes, options.NegativeCacheDuration, time);
        if (!string.IsNullOrWhiteSpace(options.DiskCacheDirectory))
        {
            Disk = new ImageDiskCache(options.DiskCacheDirectory, options.DiskCacheMaxBytes, time);
        }
        _https = options.HttpHandler is { } handler ? new HttpsFetcher(handler, disposeHandler: false) : new HttpsFetcher();
        _fetchers = [.. options.Fetchers, new DataUriFetcher(), _https, new FileFetcher()];
        _svg = options.SvgRasterizer;
        _thumbnails = options.DiskThumbnails && Disk is not null;
        _thumbnailEncoder = options.ThumbnailEncoder;
        DecodeDensity = options.DecodeDensity;
    }

    /// <summary>文档内图片的解码密度上限（见 <see cref="ImagePipelineOptions.DecodeDensity"/>）。修改后对之后排版的图片生效。</summary>
    public double? DecodeDensity
    {
        get => _decodeDensity > 0 ? _decodeDensity : null;
        set => _decodeDensity = value is not { } density ? 0
            : double.IsFinite(density) && density > 0 ? density : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>当前策略。可随时替换（例如设置页修改），对之后的请求生效。</summary>
    public ImagePolicy Policy
    {
        get => _policy;
        set => _policy = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>内存缓存。</summary>
    public ImageMemoryCache Memory { get; }

    /// <summary>磁盘缓存；未提供目录时为 null。宿主可用它的 <see cref="ImageDiskCache.ClearAsync"/> / <see cref="ImageDiskCache.GetSizeAsync"/> 做设置页。</summary>
    public ImageDiskCache? Disk { get; }

    internal long NetworkRequests => Interlocked.Read(ref _networkRequests);
    internal long MemoryHits => Interlocked.Read(ref _memoryHits);
    internal long DiskHits => Interlocked.Read(ref _diskHits);
    internal long Revalidations => Interlocked.Read(ref _revalidations);
    internal long NotModifiedCount => Interlocked.Read(ref _notModified);
    internal long Decodes => Interlocked.Read(ref _decodes);
    internal long ThumbnailHits => Interlocked.Read(ref _thumbnailHits);

    /// <summary>用户同意加载该域名的图片（<see cref="ImagePolicy.AutoLoad"/> = false 时）。</summary>
    public void Approve(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        _approved[host.Trim()] = 0;
    }

    /// <summary>该域名是否已被用户同意。</summary>
    public bool IsApproved(string host) => _approved.ContainsKey(host.Trim());

    /// <summary>清除某地址的失败记录，下次请求会重新获取（“重试”按钮）。</summary>
    public void ForgetFailure(string url)
    {
        if (TryNormalize(url, out var normalized, out _, out _))
        {
            Memory.ForgetFailure(normalized);
        }
    }

    /// <summary>宽度档位：按 256px 向上取整，0 表示原始尺寸。<paramref name="decodeWidth"/> 是物理像素（逻辑宽度 × 渲染缩放）。</summary>
    public static int GetDecodeBucket(int decodeWidth) => ImageDecoder.Bucket(decodeWidth);

    /// <summary>
    /// 规范化地址：去掉首尾空白与片段（#...），协议和域名小写、去掉默认端口；data: URI 原样保留。
    /// 相对地址或无法解析时返回 null。
    /// </summary>
    public static string? NormalizeUrl(string url) => TryNormalize(url, out var normalized, out _, out _) ? normalized : null;

    /// <summary>
    /// 同步查询内存缓存（不发起任何加载），命中时返回新句柄，用于渲染时立即显示已加载的图片。
    /// </summary>
    public ImageLease? TryGetCached(string url, int decodeWidth)
    {
        if (!TryNormalize(url, out var normalized, out _, out _))
        {
            return null;
        }
        var lease = Memory.TryAcquire(normalized, ImageDecoder.Bucket(decodeWidth));
        if (lease is not null)
        {
            Interlocked.Increment(ref _memoryHits);
        }
        return lease;
    }

    /// <summary>
    /// 同步取得原始像素尺寸（不解码、不联网）：依次查内存记录、磁盘元数据、data: URI 文件头。
    /// 用于在图片加载前按正确宽高比占位。
    /// </summary>
    public bool TryGetPixelSize(string url, out PixelSize size)
    {
        size = default;
        if (!TryNormalize(url, out var normalized, out _, out var scheme))
        {
            return false;
        }
        if (Memory.TryGetPixelSize(normalized, out size))
        {
            return true;
        }
        if (scheme == "data")
        {
            if (TryReadDataUriSize(normalized, out size))
            {
                Memory.RememberPixelSize(normalized, size);
                return true;
            }
            return false;
        }
        if (Disk is not null && FindFetcher(scheme) is { IsRemote: true }
            && Disk.TryGetEntry(normalized, out var entry) && entry.PixelWidth > 0 && entry.PixelHeight > 0)
        {
            size = new PixelSize(entry.PixelWidth, entry.PixelHeight);
            Memory.RememberPixelSize(normalized, size);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 加载图片。<paramref name="decodeWidth"/> 是显示所需的物理像素宽度（0 = 原始尺寸），按 256px 档位解码。
    /// 取消只影响本次等待：底层获取会继续完成并写入缓存，之后的请求直接命中。
    /// 返回 Loaded 时调用方负责释放结果。
    /// </summary>
    public async Task<ImageLoadResult> LoadAsync(string url, int decodeWidth, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(url);
        if (!TryNormalize(url, out var normalized, out var uri, out var scheme))
        {
            return ImageLoadResult.Failed("invalid url");
        }
        var fetcher = FindFetcher(scheme);
        if (fetcher is null)
        {
            return ImageLoadResult.Blocked("unsupported scheme: " + scheme);
        }
        var policy = _policy;
        if (uri is not null && policy.CheckUri(uri) is { } blocked)
        {
            return ImageLoadResult.Blocked(blocked);
        }
        if (scheme == "file" && policy.AllowedFileRoots.Count == 0)
        {
            return ImageLoadResult.Blocked("file access not allowed");
        }
        var bucket = ImageDecoder.Bucket(decodeWidth);
        if (Memory.TryAcquire(normalized, bucket) is { } cached)
        {
            Interlocked.Increment(ref _memoryHits);
            return ImageLoadResult.Loaded(cached);
        }
        if (Memory.TryGetFailure(normalized, out var failure))
        {
            return ImageLoadResult.Failed(failure);
        }
        if (fetcher.IsRemote && policy.RequiresConsent(uri))
        {
            var host = uri is null ? "" : ImagePolicy.NormalizeHost(uri);
            if (!_approved.ContainsKey(host))
            {
                return ImageLoadResult.NeedsConsent(host);
            }
        }

        var key = (normalized, bucket);
        InFlight flight;
        var start = false;
        lock (_gate)
        {
            if (!_inflight.TryGetValue(key, out flight!))
            {
                flight = new InFlight();
                _inflight[key] = flight;
                start = true;
            }
            flight.Waiters++;
        }
        if (start)
        {
            var request = new ImageFetchRequest { Url = normalized, Uri = uri, Policy = policy };
            _ = Task.Run(() => RunAsync(key, flight, request, fetcher));
        }
        try
        {
            var result = await flight.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return result.Status switch
            {
                ImageLoadStatus.Loaded => ImageLoadResult.Loaded(result.Master!.AddRef()),
                ImageLoadStatus.Blocked => ImageLoadResult.Blocked(result.Reason ?? "blocked"),
                _ => ImageLoadResult.Failed(result.Reason ?? "failed"),
            };
        }
        finally
        {
            ImageLease? release = null;
            lock (_gate)
            {
                flight.Waiters--;
                if (flight.Waiters == 0 && flight.Done)
                {
                    release = flight.Master;
                    flight.Master = null;
                }
            }
            release?.Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        _lifetime.Cancel();
        Memory.Dispose();
        _https.Dispose();
    }

    private IImageFetcher? FindFetcher(string scheme)
    {
        foreach (var fetcher in _fetchers)
        {
            if (fetcher.CanFetch(scheme))
            {
                return fetcher;
            }
        }
        return null;
    }

    private async Task RunAsync((string Url, int Bucket) key, InFlight flight, ImageFetchRequest request, IImageFetcher fetcher)
    {
        CoreResult result;
        try
        {
            result = await LoadCoreAsync(key.Url, key.Bucket, request, fetcher).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new CoreResult(ImageLoadStatus.Failed, null, "canceled");
        }
        catch (Exception ex)
        {
            result = new CoreResult(ImageLoadStatus.Failed, null, "error: " + ex.Message);
            Memory.AddFailure(key.Url, result.Reason!);
        }
        ImageLease? release = null;
        lock (_gate)
        {
            _inflight.Remove(key);
            flight.Done = true;
            flight.Master = result.Master;
            if (flight.Waiters == 0)
            {
                release = result.Master;
                flight.Master = null;
            }
        }
        flight.Completion.SetResult(result);
        release?.Dispose();
    }

    private async Task<CoreResult> LoadCoreAsync(string url, int bucket, ImageFetchRequest request, IImageFetcher fetcher)
    {
        if (bucket > 0 && await TryLoadThumbnailAsync(url, bucket, request, fetcher).ConfigureAwait(false) is { } thumbnail)
        {
            return thumbnail;
        }
        var bytes = await GetBytesAsync(request, fetcher).ConfigureAwait(false);
        if (bytes.Status != ImageFetchStatus.Ok)
        {
            if (bytes.Status == ImageFetchStatus.Failed && bytes.Reason != "canceled")
            {
                Memory.AddFailure(url, bytes.Reason!);
            }
            return new CoreResult(bytes.Status == ImageFetchStatus.Blocked ? ImageLoadStatus.Blocked : ImageLoadStatus.Failed, null, bytes.Reason);
        }
        // 已在线程池上：解码不占用界面线程
        _lifetime.Token.ThrowIfCancellationRequested();
        global::Avalonia.Media.Imaging.Bitmap? bitmap;
        Interlocked.Increment(ref _decodes);
        if (bytes.Format == ImageFormat.Svg)
        {
            if (_svg is null)
            {
                Memory.AddFailure(url, "svg not supported");
                return new CoreResult(ImageLoadStatus.Failed, null, "svg not supported");
            }
            bitmap = _svg.Rasterize(bytes.Data!, bucket, out var svgSize);
            if (bitmap is not null && svgSize.Width > 0)
            {
                bytes = bytes with { NaturalSize = svgSize };
            }
        }
        else
        {
            bitmap = ImageDecoder.Decode(bytes.Data!, bucket, bytes.NaturalSize);
        }
        if (bitmap is null)
        {
            Memory.AddFailure(url, "decode failed");
            if (Disk is not null && fetcher.IsRemote)
            {
                await Disk.RemoveAsync(url).ConfigureAwait(false);
            }
            return new CoreResult(ImageLoadStatus.Failed, null, "decode failed");
        }
        var decoded = bitmap.PixelSize;
        var natural = bytes.NaturalSize ?? decoded;
        // 矢量图按目标宽度栅格化，可以大于自然尺寸，自然尺寸仍取 SVG 声明的尺寸
        if (decoded.Width >= natural.Width && bytes.Format != ImageFormat.Svg)
        {
            // 完整解码：以实际结果为准（例如 EXIF 旋转），存为原始尺寸档位
            natural = decoded;
            bucket = 0;
        }
        var lease = Memory.Add(url, bucket, bitmap, decoded, natural);
        if (bucket > 0 && SourceVersion(url, request, fetcher) is { } version)
        {
            // 落盘不阻塞显示；编码期间位图由这个额外句柄保持可用
            var hold = lease.AddRef();
            _ = Task.Run(() => WriteThumbnailAsync(url, bucket, hold, natural, version));
        }
        return new CoreResult(ImageLoadStatus.Loaded, lease, null);
    }

    private static string ThumbnailKey(string url, int bucket) => "thumbnail:" + bucket + ":" + url;

    /// <summary>
    /// 原图的版本（缩略图失效依据）；不能确定时返回 null（不使用缩略图）。
    /// 本地文件先按策略解析出允许的路径，因此缩略图不会绕过 <see cref="ImagePolicy.AllowedFileRoots"/>。
    /// </summary>
    private string? SourceVersion(string url, ImageFetchRequest request, IImageFetcher fetcher)
    {
        if (!_thumbnails || request.Uri is not { } uri)
        {
            return null;
        }
        try
        {
            if (fetcher is FileFetcher)
            {
                var path = FileFetcher.ResolveAllowedPath(uri.LocalPath, request.Policy.AllowedFileRoots);
                var info = path is null ? null : new FileInfo(path);
                return info is { Exists: true } ? $"file:{info.Length}:{info.LastWriteTimeUtc.Ticks}" : null;
            }
            // 远程图片：原图缓存条目新鲜时才使用缩略图（过期则先走重新验证）
            if (fetcher.IsRemote && Disk!.TryGetEntry(url, out var entry) && Disk.IsFresh(entry))
            {
                return $"remote:{entry.Size}:{entry.ETag}:{entry.LastModified?.UtcTicks}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    private async Task<CoreResult?> TryLoadThumbnailAsync(string url, int bucket, ImageFetchRequest request, IImageFetcher fetcher)
    {
        if (SourceVersion(url, request, fetcher) is not { } version)
        {
            return null;
        }
        var hit = await Disk!.TryReadAsync(ThumbnailKey(url, bucket), _lifetime.Token).ConfigureAwait(false);
        if (hit is null || hit.Entry.SourceVersion != version || hit.Entry.PixelWidth <= 0 || hit.Entry.PixelHeight <= 0)
        {
            return null;
        }
        global::Avalonia.Media.Imaging.Bitmap bitmap;
        try
        {
            using var stream = new MemoryStream(hit.Data, writable: false);
            bitmap = new global::Avalonia.Media.Imaging.Bitmap(stream);
        }
        catch (Exception)
        {
            await Disk.RemoveAsync(ThumbnailKey(url, bucket)).ConfigureAwait(false);
            return null;
        }
        Interlocked.Increment(ref _thumbnailHits);
        var natural = new PixelSize(hit.Entry.PixelWidth, hit.Entry.PixelHeight);
        return new CoreResult(ImageLoadStatus.Loaded, Memory.Add(url, bucket, bitmap, bitmap.PixelSize, natural), null);
    }

    private async Task WriteThumbnailAsync(string url, int bucket, ImageLease lease, PixelSize natural, string version)
    {
        try
        {
            if (lease.Image is not global::Avalonia.Media.Imaging.Bitmap bitmap)
            {
                return;
            }
            byte[]? data;
            if (_thumbnailEncoder is { } encoder)
            {
                data = encoder.Encode(bitmap);
            }
            else
            {
                using var output = new MemoryStream();
                bitmap.Save(output, new global::Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                data = output.ToArray();
            }
            if (data is { Length: > 0 })
            {
                var entry = new ImageDiskCacheEntry { SourceVersion = version, PixelWidth = natural.Width, PixelHeight = natural.Height };
                await Disk!.WriteAsync(ThumbnailKey(url, bucket), data, entry, _lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // 缩略图只是加速，失败不影响显示
        }
        finally
        {
            lease.Dispose();
        }
    }

    /// <summary>取得原始字节；同一地址的并发请求（不同档位）共享一次获取。</summary>
    private Task<BytesResult> GetBytesAsync(ImageFetchRequest request, IImageFetcher fetcher)
    {
        var url = request.Url;
        lock (_gate)
        {
            if (_bytesInflight.TryGetValue(url, out var existing))
            {
                return existing;
            }
            var task = Task.Run(async () =>
            {
                try
                {
                    return await FetchBytesAsync(request, fetcher).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return BytesResult.Fail(ImageFetchStatus.Failed, "canceled");
                }
                finally
                {
                    lock (_gate)
                    {
                        _bytesInflight.Remove(url);
                    }
                }
            });
            _bytesInflight[url] = task;
            return task;
        }
    }

    private async Task<BytesResult> FetchBytesAsync(ImageFetchRequest request, IImageFetcher fetcher)
    {
        var url = request.Url;
        var token = _lifetime.Token;
        var disk = fetcher.IsRemote ? Disk : null;
        ImageDiskCacheHit? hit = null;
        if (disk is not null)
        {
            hit = await disk.TryReadAsync(url, token).ConfigureAwait(false);
            if (hit is not null && disk.IsFresh(hit.Entry))
            {
                Interlocked.Increment(ref _diskHits);
                return BytesResult.FromDisk(hit);
            }
            if (hit is not null)
            {
                Interlocked.Increment(ref _revalidations);
                if (hit.Entry.HasValidators)
                {
                    request = request with { IfNoneMatch = hit.Entry.ETag, IfModifiedSince = hit.Entry.LastModified };
                }
            }
        }
        if (fetcher.IsRemote)
        {
            Interlocked.Increment(ref _networkRequests);
        }
        var result = await fetcher.FetchAsync(request, token).ConfigureAwait(false);
        switch (result.Status)
        {
            case ImageFetchStatus.NotModified when hit is not null && disk is not null:
                Interlocked.Increment(ref _notModified);
                if (result.Cache.NoStore)
                {
                    await disk.RemoveAsync(url).ConfigureAwait(false);
                }
                else
                {
                    disk.Refresh(hit.Entry, result.Cache);
                    await disk.UpdateAsync(hit.Entry, token).ConfigureAwait(false);
                }
                return BytesResult.FromDisk(hit);
            case ImageFetchStatus.NotModified:
                return BytesResult.Fail(ImageFetchStatus.Failed, "unexpected 304");
            case ImageFetchStatus.Ok:
                var data = result.Data!;
                var format = ImageDecoder.Sniff(data, result.ContentType);
                PixelSize? size = ImageDecoder.TryReadSize(data, format, out var parsed) ? parsed : null;
                if (disk is not null)
                {
                    var entry = format is ImageFormat.Unknown or ImageFormat.Svg ? null : disk.CreateEntry(result, size ?? default);
                    if (entry is null)
                    {
                        // no-store（或无法识别的格式）：不落盘，并删除旧副本
                        if (hit is not null || result.Cache.NoStore)
                        {
                            await disk.RemoveAsync(url).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await disk.WriteAsync(url, data, entry, token).ConfigureAwait(false);
                    }
                }
                return new BytesResult(ImageFetchStatus.Ok, data, format, size, null);
            case ImageFetchStatus.Blocked:
                return BytesResult.Fail(ImageFetchStatus.Blocked, result.Error ?? "blocked");
            default:
                if (hit is not null && !hit.Entry.MustRevalidate)
                {
                    // 重新验证失败：未要求 must-revalidate 时继续使用旧内容
                    return BytesResult.FromDisk(hit);
                }
                return BytesResult.Fail(ImageFetchStatus.Failed, result.Error ?? "failed");
        }
    }

    internal static bool TryNormalize(string raw, out string normalized, out Uri? uri, out string scheme)
    {
        normalized = "";
        uri = null;
        scheme = "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = trimmed;
            scheme = "data";
            return true;
        }
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed))
        {
            return false;
        }
        scheme = parsed.Scheme.ToLowerInvariant();
        normalized = parsed.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.Fragment, UriFormat.UriEscaped);
        uri = new Uri(normalized, UriKind.Absolute);
        return true;
    }

    private static bool TryReadDataUriSize(string url, out PixelSize size)
    {
        size = default;
        var comma = url.IndexOf(',');
        if (comma < 0 || !url.AsSpan(0, comma).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        // 文件头通常在前几 KB 内（JPEG 可能更靠后），最多解码 64KB 的前缀
        var length = Math.Min(url.Length - comma - 1, 64 * 1024 / 3 * 4);
        length -= length % 4;
        var buffer = new byte[length / 4 * 3];
        if (!Convert.TryFromBase64Chars(url.AsSpan(comma + 1, length), buffer, out var written))
        {
            return false;
        }
        var head = buffer.AsSpan(0, written);
        return ImageDecoder.TryReadSize(head, ImageDecoder.Sniff(head, null), out size);
    }

    private sealed class InFlight
    {
        public TaskCompletionSource<CoreResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Waiters { get; set; }
        public bool Done { get; set; }
        public ImageLease? Master { get; set; }
    }

    private sealed record CoreResult(ImageLoadStatus Status, ImageLease? Master, string? Reason);

    private sealed record BytesResult(ImageFetchStatus Status, byte[]? Data, ImageFormat Format, PixelSize? NaturalSize, string? Reason)
    {
        public static BytesResult Fail(ImageFetchStatus status, string reason) => new(status, null, ImageFormat.Unknown, null, reason);

        public static BytesResult FromDisk(ImageDiskCacheHit hit)
        {
            var format = ImageDecoder.Sniff(hit.Data, hit.Entry.ContentType);
            PixelSize? size = hit.Entry.PixelWidth > 0 && hit.Entry.PixelHeight > 0
                ? new PixelSize(hit.Entry.PixelWidth, hit.Entry.PixelHeight)
                : ImageDecoder.TryReadSize(hit.Data, format, out var parsed) ? parsed : null;
            return new BytesResult(ImageFetchStatus.Ok, hit.Data, format, size, null);
        }
    }
}
