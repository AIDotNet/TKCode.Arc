using System.Net.Sockets;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Pdf.Native.Tests;

/// <summary>Opening a document: every source, every failure, passwords.</summary>
public sealed class PdfiumBackendTests
{
    [Fact]
    public void Use_selects_pdfium_and_can_be_called_again()
    {
        var backend = Pdfium.RequireBackend();
        backend.Name.ShouldBe("pdfium");
        backend.IsReal.ShouldBeTrue();
        ArcPdfNative.Use().ShouldBeTrue();
        ArcPdf.Backend.ShouldBeSameAs(backend);
    }

    [Fact]
    public async Task File_source_opens_with_page_count_and_metadata()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        document.PageCount.ShouldBe(12);
        document.Info.Title.ShouldBe("The Arc page viewer");
        document.Info.Author.ShouldBe("TKCode.Arc");
        document.Info.Subject.ShouldBe("A sample document for the PDF viewer");
        document.Info.Keywords.ShouldBe("pdf, viewer, sample");
        document.Info.Creator.ShouldBe("tools/pdf/make-sample.py");
        document.Info.Producer.ShouldBe("tools/pdf/make-sample.py");
        var expected = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.FromHours(8));
        document.Info.Created.ShouldBe(expected);
        document.Info.Modified.ShouldBe(expected);
    }

    [Fact]
    public async Task Sizes_are_page_dips_and_differ_per_page()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var portrait = document.GetPageSize(0);
        portrait.Width.ShouldBe(595 * 96.0 / 72, 0.01);
        portrait.Height.ShouldBe(842 * 96.0 / 72, 0.01);
        var landscape = document.GetPageSize(11);
        landscape.Width.ShouldBe(842 * 96.0 / 72, 0.01);
        landscape.Height.ShouldBe(595 * 96.0 / 72, 0.01);
        Should.Throw<ArgumentOutOfRangeException>(() => document.GetPageSize(12));
        Should.Throw<ArgumentOutOfRangeException>(() => document.GetPageSize(-1));
    }

    [Fact]
    public async Task Page_labels_come_from_the_document()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        document.GetPageLabel(0).ShouldBe("i");
        document.GetPageLabel(1).ShouldBe("ii");
        document.GetPageLabel(2).ShouldBe("1");
        document.GetPageLabel(11).ShouldBe("10");
        document.GetPageLabel(11).ShouldBe("10");
        Should.Throw<ArgumentOutOfRangeException>(() => document.GetPageLabel(12));

        using var plain = await Pdfium.OpenAsync(Pdfium.Rotated);
        plain.GetPageLabel(0).ShouldBeNull();
    }

    [Fact]
    public async Task Bytes_source_stays_readable_while_the_document_is_open()
    {
        var bytes = await File.ReadAllBytesAsync(Pdfium.Sample);
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(bytes));
        document.PageCount.ShouldBe(12);
        // The GC may move anything not pinned; PDFium reads page objects lazily from this memory.
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var text = await document.GetPageTextAsync(i + 2, CancellationToken.None).WaitAsync(Pdfium.Timeout);
            text.Text.ShouldNotBeEmpty();
        }
    }

    [Fact]
    public async Task Stream_source_reads_on_demand_and_is_closed_with_the_document()
    {
        var bytes = await File.ReadAllBytesAsync(Pdfium.Sample);
        var stream = new CountingStream(bytes);
        var document = await Pdfium.OpenAsync(PdfSource.FromStream(() => stream));
        document.PageCount.ShouldBe(12);
        var text = await document.GetPageTextAsync(2, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        text.Text.ShouldContain("Pages and coordinates");
        var pixels = await Pdfium.RenderAsync(document, 2);
        pixels.InkBounds().Width.ShouldBeGreaterThan(0);
        stream.Reads.ShouldBeGreaterThan(0);
        stream.IsDisposed.ShouldBeFalse();
        var threadId = await PdfiumThread.Instance.RunAsync(() => Environment.CurrentManagedThreadId, PdfiumPriority.Background, CancellationToken.None);
        stream.ReadThreads.ShouldBe([threadId]);

        document.Dispose();
        await Flush();
        stream.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Stream_source_failures_are_reported()
    {
        (await Pdfium.OpenFailsAsync(PdfSource.FromStream(() => throw new FileNotFoundException()))).Kind.ShouldBe(PdfErrorKind.NotFound);

        var unseekable = new CountingStream(new byte[16]) { Seekable = false };
        (await Pdfium.OpenFailsAsync(PdfSource.FromStream(() => unseekable))).Kind.ShouldBe(PdfErrorKind.Unsupported);
        unseekable.IsDisposed.ShouldBeTrue();

        var garbage = new CountingStream(await File.ReadAllBytesAsync(Pdfium.Fixture("corrupt.pdf")));
        (await Pdfium.OpenFailsAsync(PdfSource.FromStream(() => garbage))).Kind.ShouldBe(PdfErrorKind.Corrupt);
        garbage.IsDisposed.ShouldBeTrue();

        var failing = new CountingStream(await File.ReadAllBytesAsync(Pdfium.Sample)) { FailReads = true };
        (await Pdfium.OpenFailsAsync(PdfSource.FromStream(() => failing))).Kind.ShouldBe(PdfErrorKind.Corrupt);
        failing.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Http_source_downloads_and_maps_errors()
    {
        var bytes = await File.ReadAllBytesAsync(Pdfium.Sample);
        using var server = new TinyHttpServer(path => path switch
        {
            "/ok.pdf" => TinyHttpServer.Response("200 OK", bytes),
            "/moved.pdf" => TinyHttpServer.Response("302 Found", [], "Location: /ok.pdf\r\n"),
            "/loop.pdf" => TinyHttpServer.Response("302 Found", [], "Location: /loop.pdf\r\n"),
            "/gone.pdf" => TinyHttpServer.Response("404 Not Found", []),
            "/broken.pdf" => TinyHttpServer.Response("500 Internal Server Error", []),
            // A document must not be able to lead a download out of http(s).
            "/to-file.pdf" => TinyHttpServer.Response("302 Found", [], $"Location: file://{Pdfium.Sample}\r\n"),
            _ => TinyHttpServer.Response("400 Bad Request", [])
        });

        using (var document = await Pdfium.OpenAsync(PdfSource.FromUri(server.UriOf("/ok.pdf"))))
        {
            document.PageCount.ShouldBe(12);
        }
        using (var document = await Pdfium.OpenAsync(PdfSource.FromUri(server.UriOf("/moved.pdf"))))
        {
            document.PageCount.ShouldBe(12);
        }
        (await Pdfium.OpenFailsAsync(PdfSource.FromUri(server.UriOf("/gone.pdf")))).Kind.ShouldBe(PdfErrorKind.NotFound);
        (await Pdfium.OpenFailsAsync(PdfSource.FromUri(server.UriOf("/broken.pdf")))).Kind.ShouldBe(PdfErrorKind.Network);
        (await Pdfium.OpenFailsAsync(PdfSource.FromUri(server.UriOf("/to-file.pdf")))).Kind.ShouldBe(PdfErrorKind.Network);
        (await Pdfium.OpenFailsAsync(PdfSource.FromUri(server.UriOf("/loop.pdf")))).Kind.ShouldBe(PdfErrorKind.Network);
    }

    [Theory]
    [InlineData("http://a.example/x.pdf", "/y.pdf", "http://a.example/y.pdf")]
    [InlineData("http://a.example/x.pdf", "https://b.example/y.pdf", "https://b.example/y.pdf")]
    [InlineData("https://a.example/x.pdf", "https://b.example/y.pdf", "https://b.example/y.pdf")]
    [InlineData("http://a.example/x.pdf", "http://b.example/y.pdf", "http://b.example/y.pdf")]
    [InlineData("https://a.example/x.pdf", "http://b.example/y.pdf", null)]
    [InlineData("http://a.example/x.pdf", "file:///etc/passwd", null)]
    [InlineData("http://a.example/x.pdf", "javascript:alert(1)", null)]
    [InlineData("http://a.example/x.pdf", "avares://App/secret.pdf", null)]
    public void Redirects_go_only_to_http_and_https(string current, string location, string? expected)
    {
        var next = PdfiumBackend.NextAddress(new Uri(current), new Uri(location, UriKind.RelativeOrAbsolute));
        next?.AbsoluteUri.ShouldBe(expected);
        (next is null).ShouldBe(expected is null);
    }

    [Fact]
    public async Task Http_source_that_nobody_answers_is_a_network_error()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        (await Pdfium.OpenFailsAsync(PdfSource.FromUri(new Uri($"http://127.0.0.1:{port}/x.pdf")))).Kind.ShouldBe(PdfErrorKind.Network);
    }

    [Fact]
    public async Task Avares_source_that_does_not_exist_is_not_found()
    {
        (await Pdfium.OpenFailsAsync(PdfSource.FromUri(new Uri("avares://TKCode.Arc.Pdf.Native.Tests/none.pdf")))).Kind.ShouldBe(PdfErrorKind.NotFound);
    }

    [Fact]
    public async Task Missing_file_is_not_found()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "does-not-exist.pdf");
        (await Pdfium.OpenFailsAsync(PdfSource.FromFile(path))).Kind.ShouldBe(PdfErrorKind.NotFound);
    }

    [Fact]
    public async Task Unicode_file_path_opens()
    {
        var directory = Directory.CreateTempSubdirectory("arc-pdf-路径-");
        try
        {
            var path = Path.Combine(directory.FullName, "样例 sample.pdf");
            File.Copy(Pdfium.Sample, path);
            using var document = await Pdfium.OpenAsync(path);
            document.PageCount.ShouldBe(12);
        }
        finally
        {
            // Closing is queued; wait for it, or Windows keeps the file locked.
            await Flush();
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Corrupt_data_is_corrupt_from_every_source()
    {
        var path = Pdfium.Fixture("corrupt.pdf");
        (await Pdfium.OpenFailsAsync(PdfSource.FromFile(path))).Kind.ShouldBe(PdfErrorKind.Corrupt);
        (await Pdfium.OpenFailsAsync(PdfSource.FromBytes(await File.ReadAllBytesAsync(path)))).Kind.ShouldBe(PdfErrorKind.Corrupt);
        (await Pdfium.OpenFailsAsync(PdfSource.FromBytes(ReadOnlyMemory<byte>.Empty))).Kind.ShouldBe(PdfErrorKind.Corrupt);
    }

    [Fact]
    public async Task Locked_document_opens_with_the_right_password_only()
    {
        var bytes = await File.ReadAllBytesAsync(Pdfium.Locked);
        PdfSource[] sources =
        [
            PdfSource.FromFile(Pdfium.Locked),
            PdfSource.FromBytes(bytes),
            PdfSource.FromStream(() => new MemoryStream(bytes))
        ];
        foreach (var source in sources)
        {
            (await Pdfium.OpenFailsAsync(source)).Kind.ShouldBe(PdfErrorKind.PasswordRequired);
            const string wrong = "swordfish-not-this-one";
            var refused = await Pdfium.OpenFailsAsync(source, wrong);
            refused.Kind.ShouldBe(PdfErrorKind.PasswordRequired);
            refused.ToString().ShouldNotContain(wrong);

            using var document = await Pdfium.OpenAsync(source, Pdfium.LockedPassword);
            document.PageCount.ShouldBe(3);
            document.Info.Title.ShouldBe("A locked document");
            var text = await document.GetPageTextAsync(0, CancellationToken.None).WaitAsync(Pdfium.Timeout);
            text.Text.ShouldContain("the password was right");
            var outline = await document.GetOutlineAsync(CancellationToken.None).WaitAsync(Pdfium.Timeout);
            outline.Select(item => item.Title).ShouldBe(["A locked document", "The last page"]);
            (await Pdfium.RenderAsync(document, 0)).InkBounds().Width.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public async Task Open_cancelled_before_it_runs_leaves_nothing_open()
    {
        Pdfium.RequireBackend();
        using var cancellation = new CancellationTokenSource();
        var stream = new CountingStream(await File.ReadAllBytesAsync(Pdfium.Sample));
        var factoryRan = false;
        Task<PdfDocument> open;
        using (new ThreadBlocker())
        {
            open = ArcPdf.Backend.OpenAsync(PdfSource.FromStream(() =>
            {
                factoryRan = true;
                return stream;
            }), null, cancellation.Token);
            // Wait until the factory ran, and give the open a moment to join the queue behind the blocker.
            await WaitUntilAsync(() => factoryRan);
            await Task.Delay(50);
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(open.WaitAsync(Pdfium.Timeout));
        }
        await Flush();
        stream.IsDisposed.ShouldBeTrue();
        stream.Reads.ShouldBe(0);
        await Should.ThrowAsync<OperationCanceledException>(ArcPdf.Backend.OpenAsync(PdfSource.FromFile(Pdfium.Sample), null, cancellation.Token));
    }

    [Theory]
    [InlineData("D:20261108120000+08'00'", 2026, 11, 8, 12, 0, 0, 8)]
    [InlineData("D:20261108120000-05'00'", 2026, 11, 8, 12, 0, 0, -5)]
    [InlineData("D:20261108120000Z", 2026, 11, 8, 12, 0, 0, 0)]
    [InlineData("D:20261108120000Z00'00'", 2026, 11, 8, 12, 0, 0, 0)]
    [InlineData("D:2026", 2026, 1, 1, 0, 0, 0, 0)]
    [InlineData("D:202611", 2026, 11, 1, 0, 0, 0, 0)]
    [InlineData("20261108", 2026, 11, 8, 0, 0, 0, 0)]
    public void Pdf_dates_parse(string text, int year, int month, int day, int hour, int minute, int second, int offsetHours)
    {
        PdfiumDocument.ParsePdfDate(text).ShouldBe(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromHours(offsetHours)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("D:")]
    [InlineData("D:202")]
    [InlineData("D:20261399")]
    [InlineData("D:20261108120000+99")]
    public void Bad_pdf_dates_are_null(string? text)
    {
        PdfiumDocument.ParsePdfDate(text).ShouldBeNull();
    }

    /// <summary>Everything queued so far, a queued close included, has run once this returns.</summary>
    private static Task Flush() =>
        PdfiumThread.Instance.RunAsync(() => true, PdfiumPriority.Background, CancellationToken.None).WaitAsync(Pdfium.Timeout);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Pdfium.Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException();
            }
            await Task.Delay(5);
        }
    }

    private sealed class CountingStream(byte[] data) : MemoryStream(data)
    {
        private readonly HashSet<int> _threads = [];

        public int Reads { get; private set; }

        public bool IsDisposed { get; private set; }

        public bool Seekable { get; init; } = true;

        public bool FailReads { get; init; }

        public IReadOnlyCollection<int> ReadThreads => _threads;

        public override bool CanSeek => Seekable;

        public override int Read(Span<byte> buffer)
        {
            Reads++;
            _threads.Add(Environment.CurrentManagedThreadId);
            if (FailReads)
            {
                throw new IOException("The disk went away.");
            }
            return base.Read(buffer);
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
