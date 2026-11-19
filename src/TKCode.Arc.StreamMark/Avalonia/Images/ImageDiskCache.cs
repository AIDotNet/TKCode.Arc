using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamMark.Avalonia.Images;

/// <summary>磁盘缓存中一张图片的元数据（<c>&lt;sha256&gt;.json</c>）。</summary>
public sealed class ImageDiskCacheEntry
{
    /// <summary>格式版本。</summary>
    public int Version { get; set; } = 1;

    /// <summary>规范化地址。</summary>
    public string Url { get; set; } = "";

    public string? ETag { get; set; }

    public DateTimeOffset? LastModified { get; set; }

    /// <summary>过期时间（UTC）；此后使用前需要重新验证。</summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>新鲜期（秒），304 响应未带缓存头时沿用。</summary>
    public double FreshnessSeconds { get; set; }

    /// <summary>must-revalidate：过期且重新验证失败时不得使用旧内容。</summary>
    public bool MustRevalidate { get; set; }

    public string? ContentType { get; set; }

    public int PixelWidth { get; set; }

    public int PixelHeight { get; set; }

    /// <summary>最近访问时间（UTC），用于 LRU 清理。</summary>
    public DateTime LastAccessUtc { get; set; }

    /// <summary>原始字节数。</summary>
    public long Size { get; set; }

    /// <summary>缩略图条目：生成它时原图的版本（本地文件为大小与修改时间，远程为原图缓存条目的校验信息）；不一致即失效。</summary>
    public string? SourceVersion { get; set; }

    /// <summary>是否带有可用于条件请求的校验器（ETag 或 Last-Modified）。</summary>
    [JsonIgnore]
    public bool HasValidators => !string.IsNullOrEmpty(ETag) || LastModified is not null;
}

/// <summary>磁盘缓存命中：元数据与原始字节。</summary>
public sealed record ImageDiskCacheHit(ImageDiskCacheEntry Entry, byte[] Data);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
[JsonSerializable(typeof(ImageDiskCacheEntry))]
internal sealed partial class ImageCacheJsonContext : JsonSerializerContext;

/// <summary>
/// 图片磁盘缓存：原始字节存为 <c>&lt;dir&gt;/&lt;sha256(地址)&gt;.bin</c>，元数据存为同名 <c>.json</c>。
/// 写入先写临时文件再原子改名；读取遇到 IO 冲突时重试，遇到损坏的元数据时删除整对文件，
/// 因此多个进程可以共用同一目录。容量超过上限（默认 512MB）时在后台按最近访问时间做 LRU 清理，
/// 启动时也会清理一次。线程安全。
/// </summary>
public sealed class ImageDiskCache
{
    /// <summary>默认容量上限：512MB。</summary>
    public const long DefaultMaxBytes = 512L * 1024 * 1024;

    /// <summary>响应没有缓存头时的默认新鲜期：7 天。</summary>
    public static readonly TimeSpan DefaultFreshness = TimeSpan.FromDays(7);

    private readonly TimeProvider _time;
    private readonly object _trimGate = new();
    private Task _trimTask = Task.CompletedTask;
    private bool _trimRunning;
    private bool _trimAgain;
    private long _approxBytes;

    /// <param name="directory">缓存目录（不存在时创建）。</param>
    /// <param name="maxBytes">容量上限（包含元数据文件）。</param>
    /// <param name="timeProvider">时钟（测试可注入），null 表示系统时钟。</param>
    public ImageDiskCache(string directory, long maxBytes = DefaultMaxBytes, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        DirectoryPath = Path.GetFullPath(directory);
        MaxBytes = maxBytes;
        _time = timeProvider ?? TimeProvider.System;
        try
        {
            Directory.CreateDirectory(DirectoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 目录不可用时所有操作都退化为未命中
        }
        ScheduleTrim(force: true);
    }

    /// <summary>缓存目录（绝对路径）。</summary>
    public string DirectoryPath { get; }

    /// <summary>容量上限。</summary>
    public long MaxBytes { get; }

    /// <summary>地址对应的文件名主干：SHA256(UTF-8 地址) 的小写十六进制。</summary>
    public static string HashKey(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();

    /// <summary>元数据是否仍新鲜（无需重新验证）。</summary>
    public bool IsFresh(ImageDiskCacheEntry entry) => _time.GetUtcNow().UtcDateTime < entry.ExpiresUtc;

    /// <summary>同步读取元数据（用于排版占位时取像素尺寸），不更新访问时间。</summary>
    public bool TryGetEntry(string url, out ImageDiskCacheEntry entry)
    {
        entry = null!;
        var (_, jsonPath) = PathsFor(url);
        var meta = ReadMetadata(jsonPath, url);
        if (meta is null)
        {
            return false;
        }
        entry = meta;
        return true;
    }

    /// <summary>读取缓存的字节与元数据，并更新最近访问时间。未命中或文件不一致时返回 null。</summary>
    public async Task<ImageDiskCacheHit?> TryReadAsync(string url, CancellationToken cancellationToken = default)
    {
        var (binPath, jsonPath) = PathsFor(url);
        var meta = ReadMetadata(jsonPath, url);
        if (meta is null)
        {
            return null;
        }
        byte[]? data = null;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                data = await File.ReadAllBytesAsync(binPath, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                // 元数据失去了对应的数据文件
                TryDelete(jsonPath);
                return null;
            }
            catch (IOException) when (attempt < 3)
            {
                await Task.Delay(15 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
        if (data.LongLength != meta.Size)
        {
            // 另一个进程可能正在替换；视为未命中，随后的写入会修复
            return null;
        }
        meta.LastAccessUtc = _time.GetUtcNow().UtcDateTime;
        await WriteMetadataAsync(jsonPath, meta, cancellationToken).ConfigureAwait(false);
        return new ImageDiskCacheHit(meta, data);
    }

    /// <summary>写入（或替换）一张图片的字节与元数据，之后按需在后台清理。</summary>
    public async Task WriteAsync(string url, byte[] data, ImageDiskCacheEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(entry);
        var (binPath, jsonPath) = PathsFor(url);
        entry.Url = url;
        entry.Size = data.LongLength;
        entry.LastAccessUtc = _time.GetUtcNow().UtcDateTime;
        if (!await WriteAtomicAsync(binPath, data, cancellationToken).ConfigureAwait(false))
        {
            return;
        }
        if (!await WriteMetadataAsync(jsonPath, entry, cancellationToken).ConfigureAwait(false))
        {
            TryDelete(binPath);
            return;
        }
        if (Interlocked.Add(ref _approxBytes, data.LongLength + 512) > MaxBytes)
        {
            ScheduleTrim(force: false);
        }
    }

    /// <summary>只更新元数据（例如 304 之后刷新过期时间）。</summary>
    public Task UpdateAsync(ImageDiskCacheEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var (_, jsonPath) = PathsFor(entry.Url);
        return WriteMetadataAsync(jsonPath, entry, cancellationToken);
    }

    /// <summary>删除一张图片。</summary>
    public Task RemoveAsync(string url)
    {
        var (binPath, jsonPath) = PathsFor(url);
        TryDelete(jsonPath);
        TryDelete(binPath);
        return Task.CompletedTask;
    }

    /// <summary>清空缓存目录中由本缓存创建的文件。</summary>
    public Task ClearAsync() => Task.Run(() =>
    {
        foreach (var file in EnumerateOwnFiles())
        {
            TryDelete(file.FullName);
        }
        Interlocked.Exchange(ref _approxBytes, 0);
    });

    /// <summary>缓存占用的总字节数（数据与元数据）。</summary>
    public Task<long> GetSizeAsync() => Task.Run(() =>
    {
        long total = 0;
        foreach (var file in EnumerateOwnFiles())
        {
            try
            {
                total += file.Length;
            }
            catch (IOException)
            {
                // 文件可能刚被删除
            }
        }
        return total;
    });

    /// <summary>立即执行一次清理（容量超限时按最近访问时间淘汰），并等待完成。</summary>
    public Task TrimAsync()
    {
        ScheduleTrim(force: true);
        return WhenIdleAsync();
    }

    /// <summary>等待后台清理完成（测试用）。</summary>
    internal Task WhenIdleAsync()
    {
        lock (_trimGate)
        {
            return _trimTask;
        }
    }

    /// <summary>由一次成功的获取构造元数据；no-store 时返回 null（不得落盘）。</summary>
    internal ImageDiskCacheEntry? CreateEntry(ImageFetchResult result, global::Avalonia.PixelSize pixelSize)
    {
        var cache = result.Cache;
        if (cache.NoStore)
        {
            return null;
        }
        var now = _time.GetUtcNow();
        var freshness = cache.ExplicitFreshness(now) ?? DefaultFreshness;
        return new ImageDiskCacheEntry
        {
            ETag = cache.ETag,
            LastModified = cache.LastModified,
            ExpiresUtc = (now + freshness).UtcDateTime,
            FreshnessSeconds = freshness.TotalSeconds,
            MustRevalidate = cache.MustRevalidate,
            ContentType = result.ContentType,
            PixelWidth = pixelSize.Width,
            PixelHeight = pixelSize.Height,
        };
    }

    /// <summary>304 之后刷新元数据：新鲜期取响应头，缺省时沿用原值；校验器有更新时替换。</summary>
    internal void Refresh(ImageDiskCacheEntry entry, ImageCacheHeaders headers)
    {
        var now = _time.GetUtcNow();
        var freshness = headers.ExplicitFreshness(now) ?? TimeSpan.FromSeconds(entry.FreshnessSeconds);
        entry.ExpiresUtc = (now + freshness).UtcDateTime;
        entry.FreshnessSeconds = freshness.TotalSeconds;
        if (!string.IsNullOrEmpty(headers.ETag))
        {
            entry.ETag = headers.ETag;
        }
        if (headers.LastModified is { } lastModified)
        {
            entry.LastModified = lastModified;
        }
        if (headers.MustRevalidate)
        {
            entry.MustRevalidate = true;
        }
        entry.LastAccessUtc = now.UtcDateTime;
    }

    private (string Bin, string Json) PathsFor(string url)
    {
        var hash = HashKey(url);
        return (Path.Combine(DirectoryPath, hash + ".bin"), Path.Combine(DirectoryPath, hash + ".json"));
    }

    /// <summary>读取元数据；损坏（或与地址不符）时删除整对文件并返回 null。</summary>
    private static ImageDiskCacheEntry? ReadMetadata(string jsonPath, string? expectedUrl)
    {
        byte[] bytes;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                bytes = File.ReadAllBytes(jsonPath);
                break;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
            catch (IOException) when (attempt < 3)
            {
                Thread.Sleep(10 * (attempt + 1));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
        ImageDiskCacheEntry? meta = null;
        try
        {
            meta = JsonSerializer.Deserialize(bytes, ImageCacheJsonContext.Default.ImageDiskCacheEntry);
        }
        catch (JsonException)
        {
        }
        if (meta is null || meta.Version != 1 || (expectedUrl is not null && meta.Url != expectedUrl))
        {
            TryDelete(jsonPath);
            TryDelete(Path.ChangeExtension(jsonPath, ".bin"));
            return null;
        }
        return meta;
    }

    private static Task<bool> WriteMetadataAsync(string jsonPath, ImageDiskCacheEntry entry, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry, ImageCacheJsonContext.Default.ImageDiskCacheEntry);
        return WriteAtomicAsync(jsonPath, bytes, cancellationToken);
    }

    /// <summary>先写同目录下的临时文件，再覆盖式改名；遇到 IO 冲突时重试。</summary>
    private static async Task<bool> WriteAtomicAsync(string path, byte[] data, CancellationToken cancellationToken)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, data, cancellationToken).ConfigureAwait(false);
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(temp, path, overwrite: true);
                    return true;
                }
                catch (IOException) when (attempt < 4)
                {
                    await Task.Delay(15 * (attempt + 1), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            TryDelete(temp);
            return false;
        }
    }

    private void ScheduleTrim(bool force)
    {
        lock (_trimGate)
        {
            if (_trimRunning)
            {
                _trimAgain = true;
                return;
            }
            if (!force && Interlocked.Read(ref _approxBytes) <= MaxBytes)
            {
                return;
            }
            _trimRunning = true;
            _trimTask = Task.Run(TrimLoopAsync);
        }
    }

    private Task TrimLoopAsync()
    {
        while (true)
        {
            try
            {
                TrimCore();
            }
            catch (Exception)
            {
                // 清理是尽力而为
            }
            lock (_trimGate)
            {
                if (!_trimAgain)
                {
                    _trimRunning = false;
                    return Task.CompletedTask;
                }
                _trimAgain = false;
            }
        }
    }

    private void TrimCore()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            Interlocked.Exchange(ref _approxBytes, 0);
            return;
        }
        var realNow = DateTime.UtcNow;
        var live = new List<(string Json, string Bin, DateTime LastAccess, long Bytes)>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var json in Directory.EnumerateFiles(DirectoryPath, "*.json"))
        {
            var meta = ReadMetadata(json, expectedUrl: null);
            if (meta is null)
            {
                continue;
            }
            var bin = Path.ChangeExtension(json, ".bin");
            var binInfo = new FileInfo(bin);
            if (!binInfo.Exists)
            {
                TryDelete(json);
                continue;
            }
            long bytes;
            try
            {
                bytes = binInfo.Length + new FileInfo(json).Length;
            }
            catch (IOException)
            {
                continue;
            }
            known.Add(Path.GetFileNameWithoutExtension(json));
            live.Add((json, bin, meta.LastAccessUtc, bytes));
            total += bytes;
        }
        foreach (var file in new DirectoryInfo(DirectoryPath).EnumerateFiles())
        {
            try
            {
                var age = realNow - file.LastWriteTimeUtc;
                if (file.Name.EndsWith(".tmp", StringComparison.Ordinal) && age > TimeSpan.FromMinutes(10))
                {
                    TryDelete(file.FullName);
                }
                else if (file.Extension == ".bin" && !known.Contains(Path.GetFileNameWithoutExtension(file.Name)) && age > TimeSpan.FromMinutes(1))
                {
                    // 写入中途崩溃留下的孤立数据文件
                    TryDelete(file.FullName);
                }
            }
            catch (IOException)
            {
            }
        }
        if (total > MaxBytes)
        {
            live.Sort((a, b) => a.LastAccess.CompareTo(b.LastAccess));
            foreach (var item in live)
            {
                if (total <= MaxBytes)
                {
                    break;
                }
                TryDelete(item.Json);
                TryDelete(item.Bin);
                total -= item.Bytes;
            }
        }
        Interlocked.Exchange(ref _approxBytes, total);
    }

    private IEnumerable<FileInfo> EnumerateOwnFiles()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            yield break;
        }
        foreach (var file in new DirectoryInfo(DirectoryPath).EnumerateFiles())
        {
            if (file.Extension is ".bin" or ".json" || file.Name.EndsWith(".tmp", StringComparison.Ordinal))
            {
                yield return file;
            }
        }
    }

    private static void TryDelete(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(10);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}
