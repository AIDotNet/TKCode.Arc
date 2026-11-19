using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Media;

namespace StreamMark.Avalonia.Tests.Images;

/// <summary>生成真实可解码的 RGBA PNG（不依赖渲染平台）。</summary>
internal static class TestPng
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Create(int width, int height, byte r = 200, byte g = 80, byte b = 40)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++)
            {
                raw.WriteByte((byte)(r ^ (x & 0x0F)));
                raw.WriteByte(g);
                raw.WriteByte((byte)(b ^ (y & 0x0F)));
                raw.WriteByte(255);
            }
        }
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(z);
        }
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        WriteBe(ihdr, 0, (uint)width);
        WriteBe(ihdr, 4, (uint)height);
        ihdr[8] = 8; // 位深
        ihdr[9] = 6; // RGBA
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var header = new byte[8];
        WriteBe(header, 0, (uint)data.Length);
        for (var i = 0; i < 4; i++)
        {
            header[4 + i] = (byte)type[i];
        }
        stream.Write(header);
        stream.Write(data);
        var crc = 0xFFFFFFFFu;
        for (var i = 4; i < 8; i++)
        {
            crc = CrcTable[(crc ^ header[i]) & 0xFF] ^ (crc >> 8);
        }
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }
        var tail = new byte[4];
        WriteBe(tail, 0, crc ^ 0xFFFFFFFFu);
        stream.Write(tail);
    }

    private static void WriteBe(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }
}

/// <summary>手动推进的时钟。</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;

    public ManualTimeProvider()
        : this(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
    {
    }

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}

/// <summary>可观察释放的假图片，用于内存缓存测试。</summary>
internal sealed class FakeImage(int width, int height) : IImage, IDisposable
{
    public bool Disposed { get; private set; }

    public Size Size => new(width, height);

    public PixelSize PixelSize => new(width, height);

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
    }

    public void Dispose() => Disposed = true;
}

/// <summary>本地 HttpListener 图片服务器，按路径注册处理器并统计请求。</summary>
internal sealed class TestImageServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<string, Func<HttpListenerContext, Task>> _routes = new();
    private readonly ConcurrentDictionary<string, int> _counts = new();
    private readonly CancellationTokenSource _stop = new();

    public TestImageServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        Port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        BaseUrl = $"http://127.0.0.1:{Port}/";
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();
        _ = Task.Run(LoopAsync);
    }

    public int Port { get; }

    public string BaseUrl { get; }

    public CancellationToken Stopping => _stop.Token;

    /// <summary>每个请求的路径与条件请求头。</summary>
    public ConcurrentQueue<(string Path, string? IfNoneMatch, string? IfModifiedSince)> Requests { get; } = new();

    public string Url(string path) => BaseUrl + path.TrimStart('/');

    public int Count(string path) => _counts.TryGetValue("/" + path.TrimStart('/'), out var n) ? n : 0;

    public int Total => _counts.Values.Sum();

    public void Map(string path, Func<HttpListenerContext, Task> handler) => _routes["/" + path.TrimStart('/')] = handler;

    /// <summary>注册一张图片；<paramref name="headers"/> 可添加缓存头。</summary>
    public void MapImage(string path, byte[] body, string contentType = "image/png", params (string Name, string Value)[] headers) =>
        Map(path, ctx => RespondAsync(ctx, 200, body, contentType, headers));

    public static async Task RespondAsync(HttpListenerContext ctx, int status, byte[]? body, string? contentType, params (string Name, string Value)[] headers)
    {
        var response = ctx.Response;
        response.StatusCode = status;
        foreach (var (name, value) in headers)
        {
            response.AddHeader(name, value);
        }
        if (contentType is not null)
        {
            response.ContentType = contentType;
        }
        if (body is { Length: > 0 })
        {
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body);
        }
        response.Close();
    }

    private async Task LoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                return;
            }
            _ = Task.Run(async () =>
            {
                var path = ctx.Request.Url!.AbsolutePath;
                _counts.AddOrUpdate(path, 1, (_, n) => n + 1);
                Requests.Enqueue((path, ctx.Request.Headers["If-None-Match"], ctx.Request.Headers["If-Modified-Since"]));
                try
                {
                    if (_routes.TryGetValue(path, out var handler))
                    {
                        await handler(ctx);
                    }
                    else
                    {
                        await RespondAsync(ctx, 404, null, null);
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        ctx.Response.Abort();
                    }
                    catch (Exception)
                    {
                    }
                }
            });
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (Exception)
        {
        }
    }
}

/// <summary>每个测试一个临时目录。</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "streammark-img-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Sub(string name)
    {
        var p = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(p);
        return p;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception)
        {
        }
    }
}
