using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>Opens documents with PDFium (design/components/pdf/pdf.md section 5.8).</summary>
internal sealed class PdfiumBackend : IPdfBackend
{
    private const int MaxRedirects = 8;

    /// <summary>Redirects are followed by hand, so that what a server may redirect to is decided here (<see cref="NextAddress"/>) on every platform.</summary>
    private static readonly HttpClient s_http = new(new HttpClientHandler { AllowAutoRedirect = false });

    public string Name => "pdfium";

    public bool IsReal => true;

    public async Task<PdfDocument> OpenAsync(PdfSource source, string? password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        return source.Kind switch
        {
            PdfSourceKind.File => await OpenFileAsync(source.FilePath!, password, cancellationToken).ConfigureAwait(false),
            PdfSourceKind.Bytes => await OpenBytesAsync(source.Bytes, password, cancellationToken).ConfigureAwait(false),
            PdfSourceKind.Stream => await OpenStreamAsync(source.OpenStream!, password, cancellationToken).ConfigureAwait(false),
            _ => await OpenBytesAsync(await ReadAddressAsync(source.Uri!, cancellationToken).ConfigureAwait(false), password, cancellationToken).ConfigureAwait(false),
        };
    }

    private static Task<PdfDocument> OpenFileAsync(string path, string? password, CancellationToken cancellationToken) =>
        RunOpenAsync(cancellationToken, () =>
        {
            var passwordBytes = NullTerminatedUtf8(password);
            try
            {
                return PdfiumDocument.Create(LoadFile(NullTerminatedUtf8(path)!, passwordBytes), null, cancellationToken);
            }
            finally
            {
                Wipe(passwordBytes);
            }
        });

    private static Task<PdfDocument> OpenBytesAsync(ReadOnlyMemory<byte> data, string? password, CancellationToken cancellationToken)
    {
        if (data.IsEmpty)
        {
            return Task.FromException<PdfDocument>(new PdfException(PdfErrorKind.Corrupt));
        }
        return RunOpenAsync(cancellationToken, () =>
        {
            var passwordBytes = NullTerminatedUtf8(password);
            try
            {
                var memory = new PinnedMemory(data);
                return PdfiumDocument.Create(LoadMemory(memory, passwordBytes), memory, cancellationToken);
            }
            finally
            {
                Wipe(passwordBytes);
            }
        });
    }

    private static async Task<PdfDocument> OpenStreamAsync(Func<Stream> open, string? password, CancellationToken cancellationToken)
    {
        // The caller's factory may take its time, so not on the PDFium thread. The reads have to be there: PDFium makes them.
        var stream = await Task.Run(() => CallFactory(open), cancellationToken).ConfigureAwait(false);
        var started = false;
        try
        {
            return await RunOpenAsync(cancellationToken, () =>
            {
                started = true;
                StreamAccess access;
                try
                {
                    access = new StreamAccess(stream);
                }
                catch (Exception)
                {
                    stream.Dispose();
                    throw;
                }
                var passwordBytes = NullTerminatedUtf8(password);
                try
                {
                    return PdfiumDocument.Create(LoadCustom(access, passwordBytes), access, cancellationToken);
                }
                finally
                {
                    Wipe(passwordBytes);
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Skipped in the queue, so nobody owns the stream. If the work did start, it owns it (and may be using it right now):
            // asking on the thread itself settles which.
            _ = PdfiumThread.Instance.RunAsync(() =>
            {
                if (!started)
                {
                    stream.Dispose();
                }
                return true;
            }, PdfiumPriority.Lifetime, CancellationToken.None);
            throw;
        }
    }

    private static unsafe nint LoadFile(byte[] path, byte[]? password)
    {
        fixed (byte* pathPointer = path, passwordPointer = password)
        {
            return PdfiumInterop.FPDF_LoadDocument(pathPointer, passwordPointer);
        }
    }

    private static unsafe nint LoadMemory(PinnedMemory memory, byte[]? password)
    {
        fixed (byte* passwordPointer = password)
        {
            return PdfiumInterop.FPDF_LoadMemDocument64(memory.Pointer, memory.Length, passwordPointer);
        }
    }

    private static unsafe nint LoadCustom(StreamAccess access, byte[]? password)
    {
        fixed (byte* passwordPointer = password)
        {
            return PdfiumInterop.FPDF_LoadCustomDocument(access.Pointer, passwordPointer);
        }
    }

    private static Stream CallFactory(Func<Stream> open)
    {
        try
        {
            return open();
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            throw new PdfException(PdfErrorKind.NotFound, null, exception);
        }
        catch (Exception exception) when (exception is IOException)
        {
            throw new PdfException(PdfErrorKind.Aborted, "The stream of the PDF could not be opened.", exception);
        }
    }

    /// <summary>A document the call no longer wants is closed on the thread, so a cancelled open never leaks one.</summary>
    private static async Task<PdfDocument> RunOpenAsync(CancellationToken cancellationToken, Func<PdfDocument> open) =>
        await PdfiumThread.Instance.RunAsync(open, PdfiumPriority.Interactive, cancellationToken, static document => ((PdfiumDocument)document).CloseOnThread()).ConfigureAwait(false);

    private static async Task<ReadOnlyMemory<byte>> ReadAddressAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Scheme is "http" or "https")
        {
            return await DownloadAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        try
        {
            using var asset = AssetLoader.Open(uri);
            using var memory = new MemoryStream();
            await asset.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            return memory.ToArray();
        }
        catch (FileNotFoundException exception)
        {
            throw new PdfException(PdfErrorKind.NotFound, null, exception);
        }
    }

    private static async Task<ReadOnlyMemory<byte>> DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            var address = uri;
            for (var redirects = 0; redirects <= MaxRedirects; redirects++)
            {
                using var response = await s_http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    address = NextAddress(address, response.Headers.Location)
                        ?? throw new PdfException(PdfErrorKind.Network, "The address of the PDF redirected to something that is not allowed.");
                    continue;
                }
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    throw new PdfException(PdfErrorKind.NotFound);
                }
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }
            throw new PdfException(PdfErrorKind.Network, "The address of the PDF redirected too many times.");
        }
        catch (HttpRequestException exception)
        {
            throw new PdfException(PdfErrorKind.Network, null, exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout.
            throw new PdfException(PdfErrorKind.Network, "The download of the PDF timed out.", exception);
        }
    }

    /// <summary>Where a redirect leads, or null when it may not be followed: only http and https, and never from https back to http.</summary>
    internal static Uri? NextAddress(Uri current, Uri? location)
    {
        if (location is null || !Uri.TryCreate(current, location, out var next))
        {
            return null;
        }
        return next.Scheme == Uri.UriSchemeHttps || (next.Scheme == Uri.UriSchemeHttp && current.Scheme == Uri.UriSchemeHttp) ? next : null;
    }

    private static byte[]? NullTerminatedUtf8(string? text)
    {
        if (text is null)
        {
            return null;
        }
        var bytes = new byte[Encoding.UTF8.GetByteCount(text) + 1];
        Encoding.UTF8.GetBytes(text, bytes);
        return bytes;
    }

    /// <summary>The string a caller passed cannot be wiped, but no copy made here stays behind.</summary>
    private static void Wipe(byte[]? secret)
    {
        if (secret is not null)
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>Keeps the bytes of a document where PDFium read them until the document closes.</summary>
    private sealed unsafe class PinnedMemory : IDisposable
    {
        private MemoryHandle _handle;

        public PinnedMemory(ReadOnlyMemory<byte> data)
        {
            _handle = data.Pin();
            Length = (nuint)data.Length;
        }

        public void* Pointer => _handle.Pointer;

        public nuint Length { get; }

        public void Dispose() => _handle.Dispose();
    }

    /// <summary>Lets PDFium read a stream: <c>FPDF_FILEACCESS</c> and what it points to live until the document closes, which disposes the stream.</summary>
    private sealed unsafe class StreamAccess : IDisposable
    {
        private readonly Stream _stream;
        private GCHandle _self;
        private PdfiumInterop.FileAccess* _access;

        /// <exception cref="PdfException">The stream cannot be read this way.</exception>
        public StreamAccess(Stream stream)
        {
            if (!stream.CanSeek || !stream.CanRead)
            {
                throw new PdfException(PdfErrorKind.Unsupported, "PdfSource.FromStream needs a readable, seekable stream.");
            }
            var length = stream.Length;
            if (OperatingSystem.IsWindows() && length > uint.MaxValue)
            {
                // unsigned long, the type of the length in FPDF_FILEACCESS, has 32 bits on Windows.
                throw new PdfException(PdfErrorKind.Unsupported, "This PDF is too large to read from a stream on this platform.");
            }
            _stream = stream;
            _self = GCHandle.Alloc(this);
            _access = (PdfiumInterop.FileAccess*)NativeMemory.Alloc((nuint)sizeof(PdfiumInterop.FileAccess));
            _access->FileLength = new CULong((nuint)length);
            _access->GetBlock = &ReadBlock;
            _access->Param = (void*)GCHandle.ToIntPtr(_self);
        }

        public PdfiumInterop.FileAccess* Pointer => _access;

        public void Dispose()
        {
            if (_access is not null)
            {
                NativeMemory.Free(_access);
                _access = null;
            }
            if (_self.IsAllocated)
            {
                _self.Free();
            }
            _stream.Dispose();
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static int ReadBlock(void* param, CULong position, byte* buffer, CULong size)
        {
            // An exception must not cross into native code: a failed read is what PDFium is told, and it treats the data as damaged.
            try
            {
                var self = (StreamAccess)GCHandle.FromIntPtr((nint)param).Target!;
                var target = new Span<byte>(buffer, checked((int)size.Value));
                self._stream.Seek(checked((long)position.Value), SeekOrigin.Begin);
                while (!target.IsEmpty)
                {
                    var read = self._stream.Read(target);
                    if (read <= 0)
                    {
                        return 0;
                    }
                    target = target[read..];
                }
                return 1;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
