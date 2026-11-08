using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>Opens documents with pdf.js (design/components/pdf/pdf.md section 5.9). Turned on by <see cref="ArcPdfBrowser.UseAsync"/>.</summary>
internal sealed class BrowserPdfBackend : IPdfBackend
{
    public static BrowserPdfBackend Instance { get; } = new();

    private BrowserPdfBackend()
    {
    }

    public string Name => "pdfjs";

    public bool IsReal => true;

    public async Task<PdfDocument> OpenAsync(PdfSource source, string? password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Kind == PdfSourceKind.Uri && source.Uri!.Scheme is "http" or "https")
        {
            // pdf.js fetches it, with range requests when the server has them. Fetch follows redirects to http(s) only, and the
            // browser's mixed content rule stops https going back to http on a secure page.
            return await OpenAsync(Start(() => BrowserPdfInterop.OpenUrl(source.Uri.AbsoluteUri, password)), fromAddress: true, cancellationToken);
        }
        var data = await ReadAsync(source, cancellationToken);
        if (data.IsEmpty)
        {
            throw new PdfException(PdfErrorKind.Corrupt);
        }
        return await OpenAsync(Start(() => BrowserPdfInterop.OpenBytes(MemoryMarshal.AsMemory(data).Span, password)), fromAddress: false, cancellationToken);
    }

    /// <summary>pdf.js can refuse its parameters right away.</summary>
    private static JSObject Start(Func<JSObject> open)
    {
        try
        {
            return open();
        }
        catch (JSException exception)
        {
            throw new PdfException(PdfErrorKind.Corrupt, null, exception);
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadAsync(PdfSource source, CancellationToken cancellationToken)
    {
        try
        {
            switch (source.Kind)
            {
                case PdfSourceKind.Bytes:
                    return source.Bytes;
                case PdfSourceKind.File:
                    return await File.ReadAllBytesAsync(source.FilePath!, cancellationToken);
                case PdfSourceKind.Stream:
                {
                    await using var stream = source.OpenStream!();
                    return await ReadAllAsync(stream, cancellationToken);
                }
                default:
                {
                    await using var asset = AssetLoader.Open(source.Uri!);
                    return await ReadAllAsync(asset, cancellationToken);
                }
            }
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            throw new PdfException(PdfErrorKind.NotFound, null, exception);
        }
        catch (IOException exception)
        {
            throw new PdfException(PdfErrorKind.Aborted, "The stream of the PDF could not be read.", exception);
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!stream.CanRead)
        {
            throw new PdfException(PdfErrorKind.Unsupported, "PdfSource.FromStream needs a readable stream.");
        }
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.GetBuffer().AsMemory(0, (int)memory.Length);
    }

    /// <summary>Waits for a document being opened. The handle is closed unless it ends up in a document, so a cancelled or failed open never leaks one.</summary>
    private static async Task<PdfDocument> OpenAsync(JSObject handle, bool fromAddress, CancellationToken cancellationToken)
    {
        var handedOver = false;
        try
        {
            using (cancellationToken.Register(static state => BrowserPdfInterop.Close((JSObject)state!), handle))
            {
                await BrowserPdfInterop.WaitOpen(handle);
                cancellationToken.ThrowIfCancellationRequested();
                if (handle.GetPropertyAsString("errorName") is { } errorName)
                {
                    // The message of pdf.js only helps to classify the failure. It has no password in it, but it stays out of the exception too.
                    throw new PdfException(PdfJsErrors.Map(
                        errorName,
                        handle.GetPropertyAsInt32("errorStatus"),
                        handle.GetPropertyAsBoolean("errorMissing"),
                        handle.GetPropertyAsString("errorDetail"),
                        fromAddress));
                }
                var document = await BrowserPdfDocument.CreateAsync(handle, cancellationToken);
                handedOver = true;
                return document;
            }
        }
        catch (JSException exception)
        {
            throw new PdfException(PdfErrorKind.Corrupt, null, exception);
        }
        finally
        {
            if (!handedOver)
            {
                BrowserPdfInterop.Close(handle);
                handle.Dispose();
            }
        }
    }
}
