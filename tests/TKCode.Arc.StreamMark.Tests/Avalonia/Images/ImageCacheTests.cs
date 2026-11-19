using Avalonia;
using StreamMark.Avalonia.Images;
using Xunit;

namespace StreamMark.Avalonia.Tests.Images;

/// <summary>内存缓存（字节预算、句柄、负缓存）、磁盘缓存（LRU、损坏恢复）与本地文件获取。</summary>
public sealed class ImageCacheTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly ManualTimeProvider _clock = new();

    public void Dispose() => _temp.Dispose();

    private static ImageLease Put(ImageMemoryCache cache, string url, FakeImage image, int bucket = 0) =>
        cache.Add(url, bucket, image, image.PixelSize, image.PixelSize);

    [Fact]
    public void Memory_evicts_least_recently_used_by_byte_budget()
    {
        // 每张 10×10×4 = 400 字节，预算 3 张
        using var cache = new ImageMemoryCache(byteBudget: 1200, timeProvider: _clock);
        var images = Enumerable.Range(0, 4).Select(_ => new FakeImage(10, 10)).ToArray();
        Put(cache, "a", images[0]).Dispose();
        Put(cache, "b", images[1]).Dispose();
        Put(cache, "c", images[2]).Dispose();
        // 访问 a，使 b 成为最久未用
        cache.TryAcquire("a", 0)!.Dispose();
        Put(cache, "d", images[3]).Dispose();

        Assert.Equal(3, cache.Count);
        Assert.Equal(1200, cache.CurrentBytes);
        Assert.Null(cache.TryAcquire("b", 0));
        Assert.True(images[1].Disposed);
        Assert.False(images[0].Disposed);
        Assert.False(images[2].Disposed);
        Assert.False(images[3].Disposed);
        // 被淘汰后仍记得尺寸
        Assert.True(cache.TryGetPixelSize("b", out var size));
        Assert.Equal(new PixelSize(10, 10), size);
    }

    [Fact]
    public void Leased_image_is_not_disposed_until_released()
    {
        using var cache = new ImageMemoryCache(byteBudget: 400, timeProvider: _clock);
        var held = new FakeImage(10, 10);
        var lease = Put(cache, "held", held);
        var other = new FakeImage(10, 10);
        Put(cache, "other", other).Dispose();

        // 持有的条目不会被淘汰；超出预算时淘汰未被持有的条目
        Assert.False(held.Disposed);
        Assert.True(other.Disposed);
        Assert.Same(held, lease.Image);

        var second = lease.AddRef();
        lease.Dispose();
        lease.Dispose(); // 重复释放无效果
        Assert.False(held.Disposed);

        var third = new FakeImage(10, 10);
        Put(cache, "third", third).Dispose();
        // held 仍被 second 持有，third 被挤出
        Assert.False(held.Disposed);
        Assert.True(third.Disposed);

        second.Dispose();
        // held 已释放：缓存内只有它（400 字节）不超预算，保留
        Assert.False(held.Disposed);
        Put(cache, "fourth", new FakeImage(10, 10)).Dispose();
        Assert.True(held.Disposed);
    }

    [Fact]
    public void Clear_defers_disposal_of_leased_images()
    {
        var cache = new ImageMemoryCache(timeProvider: _clock);
        var held = new FakeImage(4, 4);
        var free = new FakeImage(4, 4);
        var lease = Put(cache, "held", held);
        Put(cache, "free", free).Dispose();
        cache.Dispose();
        Assert.True(free.Disposed);
        Assert.False(held.Disposed);
        Assert.Equal(0, cache.Count);
        lease.Dispose();
        Assert.True(held.Disposed);
    }

    [Fact]
    public void Larger_bucket_or_natural_entry_satisfies_smaller_request()
    {
        using var cache = new ImageMemoryCache(timeProvider: _clock);
        var natural = new FakeImage(2000, 1000);
        var at512 = new FakeImage(512, 256);
        cache.Add("u", 512, at512, at512.PixelSize, natural.PixelSize).Dispose();
        using (var hit = cache.TryAcquire("u", 256))
        {
            Assert.Same(at512, hit!.Image);
            Assert.Equal(new PixelSize(2000, 1000), hit.PixelSize);
        }
        Assert.Null(cache.TryAcquire("u", 768));
        Assert.Null(cache.TryAcquire("u", 0));
        cache.Add("u", 0, natural, natural.PixelSize, natural.PixelSize).Dispose();
        using var big = cache.TryAcquire("u", 768);
        Assert.Same(natural, big!.Image);
        using var exact = cache.TryAcquire("u", 512);
        Assert.Same(at512, exact!.Image);
    }

    [Fact]
    public void Negative_cache_expires()
    {
        using var cache = new ImageMemoryCache(negativeCacheDuration: TimeSpan.FromMinutes(5), timeProvider: _clock);
        cache.AddFailure("x", "HTTP 404");
        Assert.True(cache.TryGetFailure("x", out var reason));
        Assert.Equal("HTTP 404", reason);
        _clock.Advance(TimeSpan.FromMinutes(4));
        Assert.True(cache.TryGetFailure("x", out _));
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(cache.TryGetFailure("x", out _));
    }

    private static ImageDiskCacheEntry Meta(DateTime expires) => new() { ExpiresUtc = expires, ContentType = "image/png", PixelWidth = 1, PixelHeight = 1 };

    [Fact]
    public async Task Disk_trims_least_recently_accessed_to_cap()
    {
        var dir = _temp.Sub("disk");
        var cache = new ImageDiskCache(dir, maxBytes: 35_000, timeProvider: _clock);
        await cache.WhenIdleAsync();
        var payload = new byte[10_000];
        var expires = _clock.GetUtcNow().UtcDateTime.AddDays(1);
        foreach (var name in new[] { "a", "b", "c" })
        {
            await cache.WriteAsync("https://x/" + name, payload, Meta(expires));
            _clock.Advance(TimeSpan.FromMinutes(1));
        }
        // 访问 a，使 b 成为最久未访问
        Assert.NotNull(await cache.TryReadAsync("https://x/a"));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await cache.WriteAsync("https://x/d", payload, Meta(expires));
        await cache.WhenIdleAsync();

        Assert.True(cache.TryGetEntry("https://x/a", out _));
        Assert.False(cache.TryGetEntry("https://x/b", out _));
        Assert.True(cache.TryGetEntry("https://x/c", out _));
        Assert.True(cache.TryGetEntry("https://x/d", out _));
        Assert.False(File.Exists(Path.Combine(dir, ImageDiskCache.HashKey("https://x/b") + ".bin")));
        var size = await cache.GetSizeAsync();
        Assert.InRange(size, 30_000, 35_000);

        // 新实例启动时清理到更小的上限
        var smaller = new ImageDiskCache(dir, maxBytes: 15_000, timeProvider: _clock);
        await smaller.WhenIdleAsync();
        Assert.True(smaller.TryGetEntry("https://x/d", out _));
        Assert.False(smaller.TryGetEntry("https://x/c", out _));
        Assert.False(smaller.TryGetEntry("https://x/a", out _));

        await smaller.ClearAsync();
        Assert.Equal(0, await smaller.GetSizeAsync());
    }

    [Fact]
    public async Task Disk_recovers_from_corrupt_or_orphaned_files()
    {
        var dir = _temp.Sub("disk");
        var cache = new ImageDiskCache(dir, timeProvider: _clock);
        await cache.WhenIdleAsync();
        var expires = _clock.GetUtcNow().UtcDateTime.AddDays(1);
        await cache.WriteAsync("https://x/a", [1, 2, 3], Meta(expires));
        await cache.WriteAsync("https://x/b", [4, 5, 6], Meta(expires));
        var hashA = ImageDiskCache.HashKey("https://x/a");
        var hashB = ImageDiskCache.HashKey("https://x/b");

        await File.WriteAllTextAsync(Path.Combine(dir, hashA + ".json"), "\u0000garbage");
        Assert.Null(await cache.TryReadAsync("https://x/a"));
        Assert.False(File.Exists(Path.Combine(dir, hashA + ".bin")));
        Assert.False(File.Exists(Path.Combine(dir, hashA + ".json")));

        File.Delete(Path.Combine(dir, hashB + ".bin"));
        Assert.Null(await cache.TryReadAsync("https://x/b"));
        Assert.False(File.Exists(Path.Combine(dir, hashB + ".json")));

        // 地址与元数据不符（哈希碰撞或被篡改）也视为损坏
        await cache.WriteAsync("https://x/c", [7], Meta(expires));
        var hashC = ImageDiskCache.HashKey("https://x/c");
        File.Copy(Path.Combine(dir, hashC + ".json"), Path.Combine(dir, hashA + ".json"));
        File.Copy(Path.Combine(dir, hashC + ".bin"), Path.Combine(dir, hashA + ".bin"));
        Assert.False(cache.TryGetEntry("https://x/a", out _));

        var hit = await cache.TryReadAsync("https://x/c");
        Assert.Equal(new byte[] { 7 }, hit!.Data);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime, hit.Entry.LastAccessUtc);
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
    }

    [Fact]
    public void Disk_freshness_follows_http_headers()
    {
        var cache = new ImageDiskCache(_temp.Sub("fresh"), timeProvider: _clock);
        var now = _clock.GetUtcNow();
        ImageDiskCacheEntry Create(ImageCacheHeaders headers) => cache.CreateEntry(ImageFetchResult.Ok([1], "image/png", headers), new PixelSize(1, 1))!;

        Assert.Equal((now + TimeSpan.FromDays(7)).UtcDateTime, Create(ImageCacheHeaders.None).ExpiresUtc);
        Assert.Equal((now + TimeSpan.FromSeconds(100)).UtcDateTime, Create(new ImageCacheHeaders { MaxAge = TimeSpan.FromSeconds(120), Age = TimeSpan.FromSeconds(20) }).ExpiresUtc);
        Assert.Equal((now + TimeSpan.FromHours(1)).UtcDateTime, Create(new ImageCacheHeaders { Expires = now.AddHours(2), Date = now.AddHours(1) }).ExpiresUtc);
        Assert.Equal(now.UtcDateTime, Create(new ImageCacheHeaders { ExpiresInvalid = true }).ExpiresUtc);
        Assert.Equal(now.UtcDateTime, Create(new ImageCacheHeaders { NoCache = true, MaxAge = TimeSpan.FromDays(1) }).ExpiresUtc);
        Assert.Null(cache.CreateEntry(ImageFetchResult.Ok([1], "image/png", new ImageCacheHeaders { NoStore = true }), default));

        var entry = Create(new ImageCacheHeaders { MaxAge = TimeSpan.FromSeconds(60), ETag = "\"a\"" });
        Assert.False(cache.IsFresh(new ImageDiskCacheEntry { ExpiresUtc = now.UtcDateTime }));
        _clock.Advance(TimeSpan.FromSeconds(90));
        Assert.False(cache.IsFresh(entry));
        // 304 未带缓存头：沿用原新鲜期
        cache.Refresh(entry, ImageCacheHeaders.None);
        Assert.True(cache.IsFresh(entry));
        Assert.Equal((_clock.GetUtcNow() + TimeSpan.FromSeconds(60)).UtcDateTime, entry.ExpiresUtc);
        cache.Refresh(entry, new ImageCacheHeaders { ETag = "\"b\"", MaxAge = TimeSpan.FromSeconds(5) });
        Assert.Equal("\"b\"", entry.ETag);
        Assert.Equal(5.0, entry.FreshnessSeconds);
    }

    [Fact]
    public async Task File_fetcher_rejects_paths_outside_roots()
    {
        var root = _temp.Sub("workspace");
        var outside = _temp.Sub("secret");
        var png = TestPng.Create(2, 2);
        await File.WriteAllBytesAsync(Path.Combine(root, "in.png"), png);
        await File.WriteAllBytesAsync(Path.Combine(outside, "out.png"), png);
        Directory.CreateSymbolicLink(Path.Combine(root, "link"), outside);
        var policy = new ImagePolicy { AllowedFileRoots = [root] };
        var fetcher = new FileFetcher();

        async Task<ImageFetchResult> Fetch(string path)
        {
            var uri = new Uri(path);
            return await fetcher.FetchAsync(new ImageFetchRequest { Url = uri.AbsoluteUri, Uri = uri, Policy = policy }, CancellationToken.None);
        }

        var inside = await Fetch(Path.Combine(root, "in.png"));
        Assert.Equal(ImageFetchStatus.Ok, inside.Status);
        Assert.Equal("image/png", inside.ContentType);
        Assert.Equal(png, inside.Data);

        Assert.Equal(ImageFetchStatus.Blocked, (await Fetch(root + "/../secret/out.png")).Status);
        Assert.Equal(ImageFetchStatus.Blocked, (await Fetch(Path.Combine(root, "link", "out.png"))).Status);
        Assert.Equal(ImageFetchStatus.Blocked, (await Fetch(root + "-evil/x.png")).Status);
        Assert.Equal(ImageFetchStatus.Blocked, (await Fetch(root)).Status);
        Assert.Equal(ImageFetchStatus.Failed, (await Fetch(Path.Combine(root, "missing.png"))).Status);

        policy = policy with { AllowedFileRoots = [] };
        Assert.Equal(ImageFetchStatus.Blocked, (await Fetch(Path.Combine(root, "in.png"))).Status);
    }

    [Fact]
    public Task File_images_load_through_pipeline_without_disk_cache() => HeadlessApp.Run(async () =>
    {
        var root = _temp.Sub("ws");
        var file = Path.Combine(root, "pic.png");
        await File.WriteAllBytesAsync(file, TestPng.Create(21, 13));
        using var pipeline = new ImagePipeline(new ImagePipelineOptions
        {
            Policy = new ImagePolicy { AllowedFileRoots = [root], AutoLoad = false },
            DiskCacheDirectory = _temp.Sub("cache"),
            TimeProvider = _clock,
        });
        using var result = await pipeline.LoadAsync(new Uri(file).AbsoluteUri, 0);
        Assert.Equal(ImageLoadStatus.Loaded, result.Status);
        Assert.Equal(new PixelSize(21, 13), result.PixelSize);
        Assert.Equal(0, await pipeline.Disk!.GetSizeAsync());
        var escaped = await pipeline.LoadAsync(new Uri(root + "/../x.png").AbsoluteUri, 0);
        Assert.Equal(ImageLoadStatus.Blocked, escaped.Status);
    });

    [Theory]
    [InlineData("png")]
    [InlineData("gif")]
    [InlineData("bmp")]
    [InlineData("jpeg")]
    [InlineData("webp-vp8x")]
    public void Header_size_sniffing(string kind)
    {
        byte[] data = kind switch
        {
            "png" => TestPng.Create(37, 19),
            "gif" => [.. "GIF89a"u8, 37, 0, 19, 0, 0, 0, 0],
            "bmp" => [(byte)'B', (byte)'M', .. new byte[12], 40, 0, 0, 0, 37, 0, 0, 0, 0xED, 0xFF, 0xFF, 0xFF, 0, 0],
            "jpeg" => [0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 0, 0, 0xFF, 0xC0, 0, 11, 8, 0, 19, 0, 37, 3, 0, 0, 0, 0],
            _ => [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. "VP8X"u8, 10, 0, 0, 0, 0, 0, 0, 0, 36, 0, 0, 18, 0, 0],
        };
        var format = ImageDecoder.Sniff(data, null);
        Assert.NotEqual(ImageFormat.Unknown, format);
        Assert.True(ImageDecoder.TryReadSize(data, format, out var size));
        Assert.Equal(new PixelSize(37, 19), size);
    }
}
