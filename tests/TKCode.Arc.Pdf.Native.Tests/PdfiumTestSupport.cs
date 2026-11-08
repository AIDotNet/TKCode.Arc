using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Xunit;

namespace TKCode.Arc.Pdf.Native.Tests;

/// <summary>What the tests need: real PDFium, real bitmaps and the files from tools/pdf/make-sample.py.</summary>
internal static class Pdfium
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    /// <summary>The password of arc-sample-locked.pdf.</summary>
    public const string LockedPassword = "arc-sample";

    private static readonly object s_avaloniaGate = new();
    private static bool s_avaloniaReady;

    public static string Sample => FileIn("Samples", "arc-sample.pdf");

    public static string Locked => FileIn("Samples", "arc-sample-locked.pdf");

    public static string Rotated => FileIn("Fixtures", "rotated.pdf");

    public static string Fixture(string name) => FileIn("Fixtures", name);

    /// <summary>
    /// The PDFium backend. Without the library the test fails on macOS, the platform the repository's native tests run on, and
    /// skips elsewhere (the same as the Video.Native tests); both messages say what to do.
    /// </summary>
    public static IPdfBackend RequireBackend()
    {
        EnsureAvalonia();
        if (!ArcPdfNative.Use())
        {
            const string message = "PDFium is not available: ArcPdfNative.Use() returned false. Run tools/pdf/sync-native.sh to download the runtimes, then build again.";
            if (OperatingSystem.IsMacOS())
            {
                Assert.Fail(message);
            }
            Assert.Skip(message);
        }
        return ArcPdf.Backend;
    }

    public static async Task<PdfDocument> OpenAsync(string path, string? password = null) =>
        await RequireBackend().OpenAsync(PdfSource.FromFile(path), password, CancellationToken.None).WaitAsync(Timeout);

    public static async Task<PdfDocument> OpenAsync(PdfSource source, string? password = null) =>
        await RequireBackend().OpenAsync(source, password, CancellationToken.None).WaitAsync(Timeout);

    public static async Task<PdfException> OpenFailsAsync(PdfSource source, string? password = null)
    {
        try
        {
            var document = await OpenAsync(source, password);
            document.Dispose();
        }
        catch (PdfException exception)
        {
            return exception;
        }
        Assert.Fail("The document opened, but it should not have.");
        throw new InvalidOperationException();
    }

    /// <summary>A whole page at one pixel per DIP.</summary>
    public static async Task<Pixels> RenderAsync(PdfDocument document, int pageIndex, PixelRect? region = null, double scale = 1)
    {
        var size = document.GetPageSize(pageIndex);
        var target = new PixelSize((int)Math.Round(size.Width * scale), (int)Math.Round(size.Height * scale));
        var image = await document.RenderAsync(new PageRenderRequest(pageIndex, target, region), CancellationToken.None).AsTask().WaitAsync(Timeout);
        Assert.NotNull(image);
        return Pixels.Of(image!.Bitmap);
    }

    private static string FileIn(string folder, string name) => Path.Combine(AppContext.BaseDirectory, folder, name);

    /// <summary>A real render interface: a <see cref="Bitmap"/> cannot be created without one.</summary>
    internal static void EnsureAvalonia()
    {
        lock (s_avaloniaGate)
        {
            if (s_avaloniaReady)
            {
                return;
            }
            // On a thread of its own: setting up installs Avalonia's synchronization context on the calling thread, where the
            // await of a test would then wait for a dispatcher that nobody runs. Only the render interface is used here.
            var setup = new Thread(static () => AppBuilder.Configure<Application>()
                                                          .UseSkia()
                                                          .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                                                          .SetupWithoutStarting());
            setup.Start();
            setup.Join();
            s_avaloniaReady = true;
        }
    }
}

/// <summary>The pixels of a bitmap, BGRA.</summary>
internal sealed class Pixels
{
    private readonly byte[] _data;

    private Pixels(byte[] data, int width, int height)
    {
        _data = data;
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public static unsafe Pixels Of(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var data = new byte[size.Width * size.Height * 4];
        fixed (byte* pointer = data)
        {
            bitmap.CopyPixels(new PixelRect(size), (nint)pointer, data.Length, size.Width * 4);
        }
        return new Pixels(data, size.Width, size.Height);
    }

    public (byte B, byte G, byte R, byte A) At(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (_data[i], _data[i + 1], _data[i + 2], _data[i + 3]);
    }

    public bool IsInk(int x, int y)
    {
        var (b, g, r, _) = At(x, y);
        return b < 200 || g < 200 || r < 200;
    }

    public int CountInk(PixelRect area)
    {
        var count = 0;
        for (var y = Math.Max(area.Y, 0); y < Math.Min(area.Bottom, Height); y++)
        {
            for (var x = Math.Max(area.X, 0); x < Math.Min(area.Right, Width); x++)
            {
                if (IsInk(x, y))
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>The bounding box of the ink, leaving out one area.</summary>
    public PixelRect InkBounds(PixelRect? except = null)
    {
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (IsInk(x, y) && except?.Contains(new PixelPoint(x, y)) != true)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x + 1);
                    bottom = Math.Max(bottom, y + 1);
                }
            }
        }
        return left == int.MaxValue ? default : new PixelRect(left, top, right - left, bottom - top);
    }
}

/// <summary>A small document made in code, for sizes that would be silly to commit: many pages or a huge outline.</summary>
internal static class GeneratedPdf
{
    /// <summary><paramref name="pageCount"/> Letter pages. Each outline entry leads to (72, 700) of a page in turn.</summary>
    /// <param name="pageCount">How many pages.</param>
    /// <param name="outlineEntries">How many outline entries.</param>
    /// <param name="rotatedPage">A page with /Rotate 90.</param>
    /// <param name="pageLabels">Roman page labels from the first page on.</param>
    /// <param name="lines">Lines of text of each page instead of its number; each is Latin-1 for the Helvetica of the file.</param>
    public static byte[] Build(int pageCount, int outlineEntries = 0, int? rotatedPage = null, bool pageLabels = false, Func<int, string[]>? lines = null)
    {
        var objects = new List<string>();
        int Add(string body)
        {
            objects.Add(body);
            return objects.Count;
        }

        var font = Add("<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>");
        var pagesNumber = font + 1;
        objects.Add(string.Empty);
        var pageNumbers = new List<int>();
        for (var i = 0; i < pageCount; i++)
        {
            var content = lines is null
                ? $"BT /F1 18 Tf 72 700 Td (Page {i + 1}) Tj ET"
                : "BT /F1 18 Tf 72 700 Td 24 TL " + string.Join(" T* ", lines(i).Select(line => $"({line}) Tj")) + " ET";
            var stream = Add($"<</Length {content.Length}>>\nstream\n{content}\nendstream");
            var rotate = rotatedPage == i ? "/Rotate 90" : string.Empty;
            pageNumbers.Add(Add($"<</Type/Page/Parent {pagesNumber} 0 R/MediaBox[0 0 612 792]{rotate}/Contents {stream} 0 R/Resources<</Font<</F1 {font} 0 R>>>>>>"));
        }
        objects[pagesNumber - 1] = $"<</Type/Pages/Count {pageCount}/Kids[{string.Join(' ', pageNumbers.Select(n => $"{n} 0 R"))}]>>";

        var outline = string.Empty;
        if (outlineEntries > 0)
        {
            var root = Add(string.Empty);
            var first = objects.Count + 1;
            for (var i = 0; i < outlineEntries; i++)
            {
                var number = first + i;
                var previous = i > 0 ? $"/Prev {number - 1} 0 R" : string.Empty;
                var next = i + 1 < outlineEntries ? $"/Next {number + 1} 0 R" : string.Empty;
                Add($"<</Title(Entry {i})/Parent {root} 0 R{previous}{next}/Dest[{pageNumbers[i % pageCount]} 0 R/XYZ 72 700 null]>>");
            }
            objects[root - 1] = $"<</Type/Outlines/First {first} 0 R/Last {first + outlineEntries - 1} 0 R/Count {outlineEntries}>>";
            outline = $"/Outlines {root} 0 R";
        }
        var labels = pageLabels ? "/PageLabels<</Nums[0<</S/r>>]>>" : string.Empty;
        var catalog = Add($"<</Type/Catalog/Pages {pagesNumber} 0 R{outline}{labels}>>");

        var file = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(file.Length);
            file.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }
        var xref = file.Length;
        file.Append("xref\n0 ").Append(objects.Count + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            file.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }
        file.Append("trailer\n<</Size ").Append(objects.Count + 1).Append("/Root ").Append(catalog).Append(" 0 R>>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(file.ToString());
    }
}

/// <summary>Stops the PDFium thread on demand, so that a test can see what happens to work waiting in the queue.</summary>
internal sealed class ThreadBlocker : IDisposable
{
    private readonly ManualResetEventSlim _started = new();
    private readonly ManualResetEventSlim _release = new();
    private readonly Task _work;

    public ThreadBlocker()
    {
        _work = PdfiumThread.Instance.RunAsync(() =>
        {
            _started.Set();
            _release.Wait();
            return true;
        }, PdfiumPriority.Lifetime, CancellationToken.None);
        _started.Wait(Pdfium.Timeout).ShouldBeTrueOrThrow();
    }

    public void Dispose()
    {
        _release.Set();
        _work.Wait(Pdfium.Timeout);
    }
}

internal static class TestChecks
{
    public static void ShouldBeTrueOrThrow(this bool value)
    {
        if (!value)
        {
            throw new TimeoutException("The PDFium thread did not start the work in time.");
        }
    }
}

/// <summary>A very small HTTP server on the loopback address: one fixed answer per path.</summary>
internal sealed class TinyHttpServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly Func<string, byte[]> _respond;

    public TinyHttpServer(Func<string, byte[]> respond)
    {
        _respond = respond;
        _listener.Start();
        _ = Task.Run(AcceptAsync);
    }

    public Uri UriOf(string path) => new($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}{path}");

    public static byte[] Response(string status, byte[] body, string headers = "") =>
        [.. Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n{headers}\r\n"), .. body];

    public void Dispose() => _listener.Stop();

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                using var client = await _listener.AcceptTcpClientAsync();
                var stream = client.GetStream();
                var request = new StringBuilder();
                var buffer = new byte[1024];
                while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await stream.ReadAsync(buffer);
                    if (read == 0)
                    {
                        break;
                    }
                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }
                var path = request.ToString().Split(' ')[1];
                await stream.WriteAsync(_respond(path));
            }
        }
        catch (Exception exception) when (exception is ObjectDisposedException or SocketException or IOException)
        {
            // The test finished.
        }
    }
}
