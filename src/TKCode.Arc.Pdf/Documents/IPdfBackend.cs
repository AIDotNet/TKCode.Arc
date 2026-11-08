namespace TKCode.Arc;

/// <summary>Opens PDF documents for one platform; set <see cref="ArcPdf.Backend"/> to choose it.</summary>
public interface IPdfBackend
{
    /// <summary>A short name for diagnostics: <c>"pdfium"</c>, <c>"pdfjs"</c> or <c>"none"</c>.</summary>
    string Name { get; }

    /// <summary>Whether the backend really renders documents.</summary>
    bool IsReal { get; }

    /// <summary>
    /// Opens a document. A missing or wrong password throws <see cref="PdfException"/> with
    /// <see cref="PdfErrorKind.PasswordRequired"/>; keep the password out of messages and logs and do not hold on to it.
    /// Other failures throw <see cref="PdfException"/> with their kind.
    /// </summary>
    Task<PdfDocument> OpenAsync(PdfSource source, string? password, CancellationToken cancellationToken);
}

/// <summary>The process-wide PDF backend. Platform packages replace the default at startup.</summary>
public static class ArcPdf
{
    private static IPdfBackend s_backend = NoPdfBackend.Instance;

    /// <summary>
    /// The backend documents open with. The default one has no renderer and throws <see cref="PdfErrorKind.BackendUnavailable"/>:
    /// a real document is never shown as a fake one (<see cref="SimulatedPdfDocument"/> is created explicitly).
    /// </summary>
    public static IPdfBackend Backend
    {
        get => s_backend;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            s_backend = value;
        }
    }
}

/// <summary>The default backend: says that there is none.</summary>
internal sealed class NoPdfBackend : IPdfBackend
{
    public static readonly NoPdfBackend Instance = new();

    private NoPdfBackend()
    {
    }

    public string Name => "none";

    public bool IsReal => false;

    public Task<PdfDocument> OpenAsync(PdfSource source, string? password, CancellationToken cancellationToken) =>
        Task.FromException<PdfDocument>(new PdfException(PdfErrorKind.BackendUnavailable));
}
