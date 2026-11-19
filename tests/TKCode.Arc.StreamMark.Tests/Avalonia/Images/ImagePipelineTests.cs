using System.Net;
using Avalonia;
using StreamMark.Avalonia.Images;
using Xunit;

namespace StreamMark.Avalonia.Tests.Images;

/// <summary>加载管线：两级缓存、HTTP 缓存语义、合并请求、失败与策略。</summary>
public sealed class ImagePipelineTests : IDisposable
{
    private static readonly ImagePolicy HttpPolicy = new() { AllowHttp = true };

    private readonly TestImageServer _server = new();
    private readonly TempDir _temp = new();
    private readonly ManualTimeProvider _clock = new();

    public void Dispose()
    {
        _server.Dispose();
        _temp.Dispose();
    }

    private ImagePipeline NewPipeline(ImagePolicy? policy = null, bool disk = true, long memoryBudget = ImageMemoryCache.DefaultByteBudget) => new(new ImagePipelineOptions
    {
        Policy = policy ?? HttpPolicy,
        DiskCacheDirectory = disk ? Path.Combine(_temp.Path, "cache") : null,
        MemoryBudgetBytes = memoryBudget,
        TimeProvider = _clock,
    });

    private static void AssertLoaded(ImageLoadResult result, int width, int height)
    {
        Assert.True(result.Status == ImageLoadStatus.Loaded, result.ToString());
        Assert.Equal(new PixelSize(width, height), result.PixelSize);
        Assert.NotNull(result.Lease!.Image);
    }

    private async Task<ImageDiskCacheEntry> WaitForThumbnailAsync(ImagePipeline pipeline, string url, int bucket)
    {
        // 缩略图在后台写入
        for (var i = 0; i < 200; i++)
        {
            if (pipeline.Disk!.TryGetEntry($"thumbnail:{bucket}:{new Uri(url).AbsoluteUri}", out var entry))
            {
                return entry;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("thumbnail was not written");
    }

    [Fact]
    public Task Local_file_thumbnail_is_reused_until_the_file_changes() => HeadlessApp.Run(async () =>
    {
        var root = Path.Combine(_temp.Path, "work");
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "wide.png");
        File.WriteAllBytes(file, TestPng.Create(1600, 400, 22, 119, 255));
        var url = new Uri(file).AbsoluteUri;
        var policy = ImagePolicy.Default with { AllowedFileRoots = [root] };
        ImagePipeline Pipeline(IImageEncoder? encoder = null) => new(new ImagePipelineOptions
        {
            Policy = policy,
            DiskCacheDirectory = Path.Combine(_temp.Path, "cache"),
            ThumbnailEncoder = encoder,
        });

        using (var first = Pipeline(new StreamMark.Mermaid.WebPImageEncoder()))
        {
            using var result = await first.LoadAsync(url, 400);
            AssertLoaded(result, 1600, 400);
            Assert.Equal(512, result.Lease!.DecodedSize.Width);
            Assert.Equal(1, first.Decodes);
            var entry = await WaitForThumbnailAsync(first, url, 512);
            Assert.Equal(1600, entry.PixelWidth);
            // WebP 文件头：RIFF....WEBP
            var bytes = (await first.Disk!.TryReadAsync(entry.Url))!.Data;
            Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
            await first.Disk.WhenIdleAsync();
        }

        using (var second = Pipeline())
        {
            using var result = await second.LoadAsync(url, 400);
            AssertLoaded(result, 1600, 400);
            Assert.Equal(512, result.Lease!.DecodedSize.Width);
            Assert.Equal(128, result.Lease.DecodedSize.Height);
            Assert.Equal(1, second.ThumbnailHits);
            Assert.Equal(0, second.Decodes);
            // 查看原图不走缩略图
            using var original = await second.LoadAsync(url, 0);
            Assert.Equal(1600, original.Lease!.DecodedSize.Width);
        }

        // 文件被修改：缩略图失效，重新解码原图
        File.WriteAllBytes(file, TestPng.Create(1600, 800, 200, 40, 40));
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        using (var third = Pipeline())
        {
            using var result = await third.LoadAsync(url, 400);
            AssertLoaded(result, 1600, 800);
            Assert.Equal(0, third.ThumbnailHits);
            Assert.Equal(1, third.Decodes);
        }

        // 缩略图不能绕过本地目录策略
        using (var blocked = new ImagePipeline(new ImagePipelineOptions
        {
            Policy = ImagePolicy.Default with { AllowedFileRoots = [Path.Combine(_temp.Path, "other")] },
            DiskCacheDirectory = Path.Combine(_temp.Path, "cache"),
        }))
        {
            Assert.Equal(ImageLoadStatus.Blocked, (await blocked.LoadAsync(url, 400)).Status);
            Assert.Equal(0, blocked.ThumbnailHits);
        }
    });

    [Fact]
    public Task Remote_thumbnail_is_used_while_the_original_entry_is_fresh() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("big.png", TestPng.Create(1200, 300));
        var url = _server.Url("big.png");
        using (var first = NewPipeline())
        {
            using var result = await first.LoadAsync(url, 300);
            AssertLoaded(result, 1200, 300);
            await WaitForThumbnailAsync(first, url, 512);
        }
        using (var second = NewPipeline())
        {
            using var result = await second.LoadAsync(url, 300);
            AssertLoaded(result, 1200, 300);
            Assert.Equal(512, result.Lease!.DecodedSize.Width);
            Assert.Equal(1, second.ThumbnailHits);
            Assert.Equal(0, second.Decodes);
            Assert.Equal(0, second.NetworkRequests);
        }
    });

    [Fact]
    public Task Network_then_memory_then_disk_across_pipelines() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("a.png", TestPng.Create(40, 20));
        var url = _server.Url("a.png");

        using (var pipeline = NewPipeline())
        {
            using var first = await pipeline.LoadAsync(url, 0);
            AssertLoaded(first, 40, 20);
            Assert.Equal(1, _server.Count("a.png"));
            Assert.Equal(1, pipeline.NetworkRequests);

            using var second = await pipeline.LoadAsync(url + "#fragment", 0);
            AssertLoaded(second, 40, 20);
            Assert.Same(first.Lease!.Image, second.Lease!.Image);
            Assert.Equal(1, pipeline.MemoryHits);
            Assert.Equal(1, _server.Count("a.png"));
        }

        using (var restarted = NewPipeline())
        {
            Assert.True(restarted.TryGetPixelSize(url, out var size));
            Assert.Equal(new PixelSize(40, 20), size);
            using var third = await restarted.LoadAsync(url, 0);
            AssertLoaded(third, 40, 20);
            Assert.Equal(1, restarted.DiskHits);
            Assert.Equal(0, restarted.NetworkRequests);
            Assert.Equal(1, _server.Count("a.png"));
        }
    });

    [Fact]
    public Task Expired_entry_revalidates_with_etag_and_304_refreshes() => HeadlessApp.Run(async () =>
    {
        var body = TestPng.Create(30, 30);
        _server.Map("etag.png", ctx =>
        {
            if (ctx.Request.Headers["If-None-Match"] == "\"v1\"")
            {
                return TestImageServer.RespondAsync(ctx, 304, null, null, ("ETag", "\"v1\""), ("Cache-Control", "max-age=60"));
            }
            return TestImageServer.RespondAsync(ctx, 200, body, "image/png", ("ETag", "\"v1\""), ("Cache-Control", "max-age=60"));
        });
        var url = _server.Url("etag.png");

        using (var p1 = NewPipeline())
        {
            using var r = await p1.LoadAsync(url, 0);
            AssertLoaded(r, 30, 30);
        }

        _clock.Advance(TimeSpan.FromSeconds(30));
        using (var p2 = NewPipeline())
        {
            using var r = await p2.LoadAsync(url, 0);
            AssertLoaded(r, 30, 30);
            Assert.Equal(1, p2.DiskHits);
            Assert.Equal(1, _server.Count("etag.png"));
        }

        _clock.Advance(TimeSpan.FromSeconds(31));
        using (var p3 = NewPipeline())
        {
            using var r = await p3.LoadAsync(url, 0);
            AssertLoaded(r, 30, 30);
            Assert.Equal(1, p3.Revalidations);
            Assert.Equal(1, p3.NotModifiedCount);
            Assert.Equal(2, _server.Count("etag.png"));
            var conditional = _server.Requests.Last();
            Assert.Equal("\"v1\"", conditional.IfNoneMatch);
        }

        // 304 刷新了过期时间：再过 30 秒仍然新鲜
        _clock.Advance(TimeSpan.FromSeconds(30));
        using (var p4 = NewPipeline())
        {
            using var r = await p4.LoadAsync(url, 0);
            AssertLoaded(r, 30, 30);
            Assert.Equal(1, p4.DiskHits);
            Assert.Equal(0, p4.NetworkRequests);
            Assert.Equal(2, _server.Count("etag.png"));
        }
    });

    [Fact]
    public Task Last_modified_is_sent_as_if_modified_since() => HeadlessApp.Run(async () =>
    {
        var body = TestPng.Create(8, 8);
        _server.Map("lm.png", ctx => ctx.Request.Headers["If-Modified-Since"] is not null
            ? TestImageServer.RespondAsync(ctx, 304, null, null)
            : TestImageServer.RespondAsync(ctx, 200, body, "image/png", ("Last-Modified", "Wed, 21 Oct 2015 07:28:00 GMT"), ("Cache-Control", "no-cache")));
        var url = _server.Url("lm.png");
        using (var p1 = NewPipeline())
        {
            using var r = await p1.LoadAsync(url, 0);
            AssertLoaded(r, 8, 8);
        }
        using (var p2 = NewPipeline())
        {
            // no-cache：每次都重新验证
            using var r = await p2.LoadAsync(url, 0);
            AssertLoaded(r, 8, 8);
            Assert.Equal(1, p2.NotModifiedCount);
        }
        Assert.Equal("Wed, 21 Oct 2015 07:28:00 GMT", _server.Requests.Last().IfModifiedSince);
    });

    [Fact]
    public Task No_store_is_not_written_to_disk() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("nostore.png", TestPng.Create(10, 10), "image/png", ("Cache-Control", "no-store"));
        var url = _server.Url("nostore.png");
        using (var p1 = NewPipeline())
        {
            using var r = await p1.LoadAsync(url, 0);
            AssertLoaded(r, 10, 10);
            Assert.Equal(0, await p1.Disk!.GetSizeAsync());
        }
        using (var p2 = NewPipeline())
        {
            using var r = await p2.LoadAsync(url, 0);
            AssertLoaded(r, 10, 10);
            Assert.Equal(1, p2.NetworkRequests);
        }
        Assert.Equal(2, _server.Count("nostore.png"));
    });

    [Fact]
    public Task Changed_etag_200_replaces_disk_entry() => HeadlessApp.Run(async () =>
    {
        var version = 1;
        _server.Map("swap.png", ctx => version == 1
            ? TestImageServer.RespondAsync(ctx, 200, TestPng.Create(10, 10), "image/png", ("ETag", "\"v1\""), ("Cache-Control", "max-age=0"))
            : TestImageServer.RespondAsync(ctx, 200, TestPng.Create(20, 12), "image/png", ("ETag", "\"v2\""), ("Cache-Control", "max-age=600")));
        var url = _server.Url("swap.png");
        using (var p1 = NewPipeline())
        {
            using var r = await p1.LoadAsync(url, 0);
            AssertLoaded(r, 10, 10);
        }
        version = 2;
        using (var p2 = NewPipeline())
        {
            using var r = await p2.LoadAsync(url, 0);
            AssertLoaded(r, 20, 12);
            Assert.Equal("\"v1\"", _server.Requests.Last().IfNoneMatch);
            Assert.True(p2.Disk!.TryGetEntry(url, out var entry));
            Assert.Equal("\"v2\"", entry.ETag);
            Assert.Equal(20, entry.PixelWidth);
        }
        using (var p3 = NewPipeline())
        {
            using var r = await p3.LoadAsync(url, 0);
            AssertLoaded(r, 20, 12);
            Assert.Equal(0, p3.NetworkRequests);
        }
    });

    [Fact]
    public Task Concurrent_identical_loads_issue_one_request() => HeadlessApp.Run(async () =>
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var body = TestPng.Create(16, 16);
        _server.Map("slow.png", async ctx =>
        {
            await gate.Task;
            await TestImageServer.RespondAsync(ctx, 200, body, "image/png");
        });
        var url = _server.Url("slow.png");
        using var pipeline = NewPipeline();
        // 不同档位但原图更小，也共享同一次获取
        var loads = Enumerable.Range(0, 8).Select(i => pipeline.LoadAsync(url, i % 2 == 0 ? 0 : 300)).ToArray();
        await Task.Delay(100);
        gate.SetResult();
        var results = await Task.WhenAll(loads);
        foreach (var result in results)
        {
            AssertLoaded(result, 16, 16);
        }
        Assert.Equal(1, _server.Count("slow.png"));
        Assert.Equal(1, pipeline.NetworkRequests);
        foreach (var result in results)
        {
            result.Dispose();
        }
    });

    [Fact]
    public Task Cancelled_waiter_does_not_cancel_shared_fetch() => HeadlessApp.Run(async () =>
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var body = TestPng.Create(12, 12);
        _server.Map("c.png", async ctx =>
        {
            await gate.Task;
            await TestImageServer.RespondAsync(ctx, 200, body, "image/png");
        });
        var url = _server.Url("c.png");
        using var pipeline = NewPipeline(disk: false);
        using var cts = new CancellationTokenSource();
        var cancelled = pipeline.LoadAsync(url, 0, cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        gate.SetResult();
        // 等后台获取完成后直接命中内存
        for (var i = 0; i < 100 && pipeline.Memory.Count == 0; i++)
        {
            await Task.Delay(20);
        }
        using var result = await pipeline.LoadAsync(url, 0);
        AssertLoaded(result, 12, 12);
        Assert.Equal(1, _server.Count("c.png"));
    });

    public static TheoryData<string> FailureCases => ["oversize", "oversize-chunked", "content-type", "timeout", "redirects", "http-500", "not-image-bytes"];

    [Theory]
    [MemberData(nameof(FailureCases))]
    public Task Failures_are_negatively_cached(string kind) => HeadlessApp.Run(async () =>
    {
        var png = TestPng.Create(200, 100);
        var policy = HttpPolicy with { Timeout = TimeSpan.FromMilliseconds(300) };
        switch (kind)
        {
            case "oversize":
                policy = policy with { MaxBytes = 64 };
                _server.MapImage("f", png);
                break;
            case "oversize-chunked":
                policy = policy with { MaxBytes = 64 };
                _server.Map("f", async ctx =>
                {
                    ctx.Response.SendChunked = true;
                    ctx.Response.ContentType = "image/png";
                    await ctx.Response.OutputStream.WriteAsync(png);
                    ctx.Response.Close();
                });
                break;
            case "content-type":
                _server.MapImage("f", png, "text/html");
                break;
            case "timeout":
                _server.Map("f", async ctx =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), _server.Stopping);
                    await TestImageServer.RespondAsync(ctx, 200, png, "image/png");
                });
                break;
            case "redirects":
                _server.Map("f", ctx => TestImageServer.RespondAsync(ctx, 302, null, null, ("Location", "/f")));
                break;
            case "http-500":
                _server.Map("f", ctx => TestImageServer.RespondAsync(ctx, 500, null, null));
                break;
            case "not-image-bytes":
                _server.MapImage("f", "definitely not a png"u8.ToArray(), "image/png");
                break;
        }
        var url = _server.Url("f");
        using var pipeline = NewPipeline(policy);
        var first = await pipeline.LoadAsync(url, 0);
        Assert.Equal(ImageLoadStatus.Failed, first.Status);
        var expected = kind switch
        {
            "oversize" or "oversize-chunked" => "image too large",
            "content-type" => "not an image: text/html",
            "timeout" => "timeout",
            "redirects" => "too many redirects",
            "http-500" => "HTTP 500",
            _ => "decode failed",
        };
        Assert.Equal(expected, first.Reason);
        var requests = _server.Count("f");
        Assert.Equal(kind == "redirects" ? 6 : 1, requests);

        var second = await pipeline.LoadAsync(url, 0);
        Assert.Equal(ImageLoadStatus.Failed, second.Status);
        Assert.Equal(expected, second.Reason);
        Assert.Equal(requests, _server.Count("f"));

        // 负缓存 5 分钟后重试
        _clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        var third = await pipeline.LoadAsync(url, 0);
        Assert.Equal(ImageLoadStatus.Failed, third.Status);
        Assert.True(_server.Count("f") > requests);
    });

    [Fact]
    public Task Forget_failure_allows_immediate_retry() => HeadlessApp.Run(async () =>
    {
        var ok = false;
        var png = TestPng.Create(5, 5);
        _server.Map("retry.png", ctx => ok
            ? TestImageServer.RespondAsync(ctx, 200, png, "image/png")
            : TestImageServer.RespondAsync(ctx, 503, null, null));
        var url = _server.Url("retry.png");
        using var pipeline = NewPipeline(disk: false);
        Assert.Equal(ImageLoadStatus.Failed, (await pipeline.LoadAsync(url, 0)).Status);
        ok = true;
        Assert.Equal(ImageLoadStatus.Failed, (await pipeline.LoadAsync(url, 0)).Status);
        pipeline.ForgetFailure(url);
        using var result = await pipeline.LoadAsync(url, 0);
        AssertLoaded(result, 5, 5);
    });

    [Fact]
    public Task Stale_entry_is_served_when_revalidation_fails() => HeadlessApp.Run(async () =>
    {
        var up = true;
        var png = TestPng.Create(9, 9);
        _server.Map("stale.png", ctx => up
            ? TestImageServer.RespondAsync(ctx, 200, png, "image/png", ("ETag", "\"s\""), ("Cache-Control", "max-age=10"))
            : TestImageServer.RespondAsync(ctx, 502, null, null));
        var url = _server.Url("stale.png");
        using (var p1 = NewPipeline())
        {
            using var r = await p1.LoadAsync(url, 0);
            AssertLoaded(r, 9, 9);
        }
        up = false;
        _clock.Advance(TimeSpan.FromSeconds(20));
        using (var p2 = NewPipeline())
        {
            using var r = await p2.LoadAsync(url, 0);
            AssertLoaded(r, 9, 9);
            Assert.Equal(1, p2.Revalidations);
        }
    });

    [Fact]
    public Task Must_revalidate_does_not_serve_stale_on_failure() => HeadlessApp.Run(async () =>
    {
        var up = true;
        var png = TestPng.Create(9, 9);
        _server.Map("mr.png", ctx => up
            ? TestImageServer.RespondAsync(ctx, 200, png, "image/png", ("ETag", "\"m\""), ("Cache-Control", "max-age=10, must-revalidate"))
            : TestImageServer.RespondAsync(ctx, 502, null, null));
        var url = _server.Url("mr.png");
        using (var p1 = NewPipeline())
        {
            using var r = await p1.LoadAsync(url, 0);
            AssertLoaded(r, 9, 9);
        }
        up = false;
        _clock.Advance(TimeSpan.FromSeconds(20));
        using var p2 = NewPipeline();
        var result = await p2.LoadAsync(url, 0);
        Assert.Equal(ImageLoadStatus.Failed, result.Status);
        Assert.Equal("HTTP 502", result.Reason);
    });

    [Fact]
    public Task Http_is_blocked_by_default() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("a.png", TestPng.Create(4, 4));
        using var pipeline = NewPipeline(ImagePolicy.Default);
        var result = await pipeline.LoadAsync(_server.Url("a.png"), 0);
        Assert.Equal(ImageLoadStatus.Blocked, result.Status);
        Assert.Equal("http not allowed", result.Reason);
        Assert.Equal(0, _server.Total);
    });

    [Fact]
    public Task Allowed_and_blocked_hosts() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("a.png", TestPng.Create(4, 4));
        var url = _server.Url("a.png");

        using (var allowOther = NewPipeline(HttpPolicy with { AllowedHosts = ["example.com", "*.example.org"] }))
        {
            var r = await allowOther.LoadAsync(url, 0);
            Assert.Equal(ImageLoadStatus.Blocked, r.Status);
            Assert.Equal("host not allowed: 127.0.0.1", r.Reason);
        }
        using (var blocked = NewPipeline(HttpPolicy with { BlockedHosts = ["127.0.0.1"] }))
        {
            var r = await blocked.LoadAsync(url, 0);
            Assert.Equal(ImageLoadStatus.Blocked, r.Status);
        }
        Assert.Equal(0, _server.Total);
        using (var allowed = NewPipeline(HttpPolicy with { AllowedHosts = ["127.0.0.1"] }, disk: false))
        {
            using var r = await allowed.LoadAsync(url, 0);
            AssertLoaded(r, 4, 4);
        }
        Assert.Equal(1, _server.Total);

        var policy = HttpPolicy with { AllowedHosts = ["*.example.com"], BlockedHosts = ["bad.example.com"] };
        Assert.Null(policy.CheckUri(new Uri("https://cdn.example.com/x.png")));
        Assert.NotNull(policy.CheckUri(new Uri("https://example.com/x.png")));
        Assert.NotNull(policy.CheckUri(new Uri("https://BAD.example.com/x.png")));
        Assert.NotNull(policy.CheckUri(new Uri("https://evil-example.com/x.png")));
    });

    [Fact]
    public Task Redirect_to_blocked_host_is_blocked() => HeadlessApp.Run(async () =>
    {
        _server.Map("r", ctx => TestImageServer.RespondAsync(ctx, 302, null, null, ("Location", $"http://localhost:{_server.Port}/a.png")));
        _server.MapImage("a.png", TestPng.Create(4, 4));
        using var pipeline = NewPipeline(HttpPolicy with { BlockedHosts = ["localhost"] }, disk: false);
        var result = await pipeline.LoadAsync(_server.Url("r"), 0);
        Assert.Equal(ImageLoadStatus.Blocked, result.Status);
        Assert.Equal("host blocked: localhost", result.Reason);
        Assert.Equal(0, _server.Count("a.png"));

        // 允许的重定向（相对地址）正常跟随
        _server.Map("r2", ctx => TestImageServer.RespondAsync(ctx, 301, null, null, ("Location", "/a.png")));
        using var followed = await pipeline.LoadAsync(_server.Url("r2"), 0);
        AssertLoaded(followed, 4, 4);
    });

    [Fact]
    public Task Auto_load_off_needs_consent_until_approved() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("a.png", TestPng.Create(6, 6));
        var url = _server.Url("a.png");
        using var pipeline = NewPipeline(HttpPolicy with { AutoLoad = false }, disk: false);
        var result = await pipeline.LoadAsync(url, 0);
        Assert.Equal(ImageLoadStatus.NeedsConsent, result.Status);
        Assert.Equal("127.0.0.1", result.Host);
        Assert.Equal(0, _server.Total);

        // data: 不需要同意
        var data = "data:image/png;base64," + Convert.ToBase64String(TestPng.Create(3, 3));
        using (var inline = await pipeline.LoadAsync(data, 0))
        {
            AssertLoaded(inline, 3, 3);
        }

        pipeline.Approve(result.Host!);
        using var approved = await pipeline.LoadAsync(url, 0);
        AssertLoaded(approved, 6, 6);
        Assert.Equal(1, _server.Total);

        pipeline.Policy = HttpPolicy with { AutoLoadHttps = false };
        Assert.Equal(ImageLoadStatus.Loaded, (await pipeline.LoadAsync(url, 0)).Status);
    });

    [Fact]
    public Task Corrupt_metadata_is_discarded_and_refetched() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("a.png", TestPng.Create(7, 7));
        var url = _server.Url("a.png");
        using (var p1 = NewPipeline())
        {
            using var r = await p1.LoadAsync(url, 0);
            AssertLoaded(r, 7, 7);
        }
        var json = Path.Combine(_temp.Path, "cache", ImageDiskCache.HashKey(url) + ".json");
        Assert.True(File.Exists(json));
        await File.WriteAllTextAsync(json, "{ this is not json");
        using (var p2 = NewPipeline())
        {
            Assert.False(p2.TryGetPixelSize(url, out _));
            using var r = await p2.LoadAsync(url, 0);
            AssertLoaded(r, 7, 7);
            Assert.Equal(1, p2.NetworkRequests);
            Assert.True(p2.Disk!.TryGetEntry(url, out var entry));
            Assert.Equal(7, entry.PixelHeight);
        }
        Assert.Equal(2, _server.Count("a.png"));
    });

    [Fact]
    public Task Corrupt_cached_bytes_are_dropped_and_refetched() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("a.png", TestPng.Create(7, 7));
        var url = _server.Url("a.png");
        using (var p1 = NewPipeline())
        {
            using var r = await p1.LoadAsync(url, 0);
        }
        var bin = Path.Combine(_temp.Path, "cache", ImageDiskCache.HashKey(url) + ".bin");
        var length = new FileInfo(bin).Length;
        var garbage = new byte[length];
        Array.Fill(garbage, (byte)0x42);
        await File.WriteAllBytesAsync(bin, garbage);
        using (var p2 = NewPipeline())
        {
            var r = await p2.LoadAsync(url, 0);
            Assert.Equal(ImageLoadStatus.Failed, r.Status);
            Assert.False(File.Exists(bin));
            p2.ForgetFailure(url);
            using var again = await p2.LoadAsync(url, 0);
            AssertLoaded(again, 7, 7);
        }
    });

    [Fact]
    public Task Decode_width_bucket_yields_smaller_bitmap() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("big.png", TestPng.Create(1000, 400));
        _server.MapImage("small.png", TestPng.Create(100, 50));
        using var pipeline = NewPipeline();
        var big = _server.Url("big.png");

        using var scaled = await pipeline.LoadAsync(big, 300);
        AssertLoaded(scaled, 1000, 400);
        Assert.Equal(512, scaled.Lease!.DecodedSize.Width);
        Assert.InRange(scaled.Lease.DecodedSize.Height, 204, 205);

        // 同档位（257..512）命中内存
        using var sameBucket = await pipeline.LoadAsync(big, 500);
        Assert.Same(scaled.Lease.Image, sameBucket.Lease!.Image);

        using var full = await pipeline.LoadAsync(big, 0);
        Assert.Equal(new PixelSize(1000, 400), full.Lease!.DecodedSize);
        // 换档位需要原始字节：来自磁盘缓存，不再联网
        Assert.Equal(1, _server.Count("big.png"));
        Assert.Equal(1, pipeline.DiskHits);
        Assert.InRange(pipeline.Memory.CurrentBytes, 1000L * 400 * 4 + 512L * 204 * 4, 1000L * 400 * 4 + 512L * 205 * 4);

        // 原图比档位窄：完整解码并存为原始尺寸档位，任何档位都能命中
        using var small = await pipeline.LoadAsync(_server.Url("small.png"), 300);
        Assert.Equal(new PixelSize(100, 50), small.Lease!.DecodedSize);
        using var cached = pipeline.TryGetCached(_server.Url("small.png"), 900);
        Assert.NotNull(cached);
        Assert.Same(small.Lease.Image, cached.Image);
    });

    [Fact]
    public Task Data_uri_loads_and_reports_size_synchronously() => HeadlessApp.Run(async () =>
    {
        var data = "data:image/png;base64," + Convert.ToBase64String(TestPng.Create(33, 11));
        using var pipeline = NewPipeline();
        Assert.True(pipeline.TryGetPixelSize(data, out var size));
        Assert.Equal(new PixelSize(33, 11), size);
        using var result = await pipeline.LoadAsync(data, 0);
        AssertLoaded(result, 33, 11);
        Assert.Equal(0, await pipeline.Disk!.GetSizeAsync());

        var notImage = await pipeline.LoadAsync("data:text/plain;base64,aGVsbG8=", 0);
        Assert.Equal(ImageLoadStatus.Failed, notImage.Status);
    });

    [Fact]
    public Task Svg_is_reported_as_unsupported() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("x.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"u8.ToArray(), "image/svg+xml");
        using var pipeline = NewPipeline();
        var result = await pipeline.LoadAsync(_server.Url("x.svg"), 0);
        Assert.Equal(ImageLoadStatus.Failed, result.Status);
        Assert.Equal("svg not supported", result.Reason);
        var inline = await pipeline.LoadAsync("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg'/%3E", 0);
        Assert.Equal("svg not supported", inline.Reason);
    });

    [Fact]
    public Task Invalid_and_unsupported_urls() => HeadlessApp.Run(async () =>
    {
        using var pipeline = NewPipeline(disk: false);
        Assert.Equal(ImageLoadStatus.Failed, (await pipeline.LoadAsync("relative/path.png", 0)).Status);
        Assert.Equal(ImageLoadStatus.Blocked, (await pipeline.LoadAsync("ftp://example.com/a.png", 0)).Status);
        Assert.Equal(ImageLoadStatus.Blocked, (await pipeline.LoadAsync("file:///etc/hosts", 0)).Status);
        Assert.Equal("https://example.com/a.png?x=1", ImagePipeline.NormalizeUrl("  HTTPS://Example.COM:443/a.png?x=1#top "));
        Assert.Null(ImagePipeline.NormalizeUrl("a.png"));
        Assert.Equal(0, ImagePipeline.GetDecodeBucket(0));
        Assert.Equal(256, ImagePipeline.GetDecodeBucket(1));
        Assert.Equal(512, ImagePipeline.GetDecodeBucket(257));
    });

    [Fact]
    public Task Custom_scheme_fetcher_is_used() => HeadlessApp.Run(async () =>
    {
        var png = TestPng.Create(14, 14);
        using var pipeline = new ImagePipeline(new ImagePipelineOptions
        {
            Fetchers = [new LambdaFetcher("app", _ => ImageFetchResult.Ok(png, "image/png"))],
            TimeProvider = _clock,
        });
        using var result = await pipeline.LoadAsync("app://avatars/42", 0);
        AssertLoaded(result, 14, 14);
    });

    [Fact]
    public Task Lease_outlives_eviction_from_pipeline_memory() => HeadlessApp.Run(async () =>
    {
        _server.MapImage("a.png", TestPng.Create(64, 64));
        _server.MapImage("b.png", TestPng.Create(64, 64));
        // 预算只够一张
        using var pipeline = NewPipeline(disk: false, memoryBudget: 64 * 64 * 4);
        var a = await pipeline.LoadAsync(_server.Url("a.png"), 0);
        using (var b = await pipeline.LoadAsync(_server.Url("b.png"), 0))
        {
            // a 被持有，不会被淘汰
            Assert.Equal(2, pipeline.Memory.Count);
            var bitmap = (global::Avalonia.Media.Imaging.Bitmap)a.Lease!.Image;
            Assert.Equal(64, bitmap.PixelSize.Width);
            a.Dispose();
            // a 释放后超出预算，立即淘汰
            Assert.Equal(1, pipeline.Memory.Count);
        }
        Assert.True(pipeline.TryGetPixelSize(_server.Url("a.png"), out var size));
        Assert.Equal(new PixelSize(64, 64), size);
    });

    private sealed class LambdaFetcher(string scheme, Func<ImageFetchRequest, ImageFetchResult> fetch) : IImageFetcher
    {
        public bool CanFetch(string s) => s == scheme;

        public bool IsRemote => false;

        public Task<ImageFetchResult> FetchAsync(ImageFetchRequest request, CancellationToken cancellationToken) => Task.FromResult(fetch(request));
    }
}
