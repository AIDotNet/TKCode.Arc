namespace TKCode.Arc;

/// <summary>Why a PDF could not be opened.</summary>
public enum PdfErrorKind
{
    /// <summary>No renderer is set up (<see cref="ArcPdf.Backend"/>).</summary>
    BackendUnavailable,

    /// <summary>The file or address does not exist.</summary>
    NotFound,

    /// <summary>The download failed.</summary>
    Network,

    /// <summary>The file is damaged or is not a PDF.</summary>
    Corrupt,

    /// <summary>The document uses something the renderer does not support.</summary>
    Unsupported,

    /// <summary>The document needs a password, or the one given is wrong.</summary>
    PasswordRequired,

    /// <summary>Opening was stopped by something other than the viewer.</summary>
    Aborted
}

/// <summary>A failure to open a PDF. Its message never contains a password.</summary>
public sealed class PdfException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="kind">Why opening failed.</param>
    /// <param name="message">A description for developers; null uses a standard one. Do not put a password in it.</param>
    /// <param name="innerException">The underlying failure.</param>
    public PdfException(PdfErrorKind kind, string? message = null, Exception? innerException = null)
        : base(message ?? DefaultMessage(kind), innerException)
    {
        Kind = kind;
    }

    /// <summary>Why opening failed.</summary>
    public PdfErrorKind Kind { get; }

    private static string DefaultMessage(PdfErrorKind kind) => kind switch
    {
        PdfErrorKind.BackendUnavailable => "No PDF backend is set up. Call ArcPdfNative.Use() on desktop or await ArcPdfBrowser.UseAsync() in a browser.",
        PdfErrorKind.NotFound => "The PDF was not found.",
        PdfErrorKind.Network => "The PDF could not be downloaded.",
        PdfErrorKind.Corrupt => "The file is damaged or is not a PDF.",
        PdfErrorKind.Unsupported => "The PDF uses a feature the backend does not support.",
        PdfErrorKind.PasswordRequired => "The PDF needs a password, or the one given is wrong.",
        _ => "Opening the PDF was stopped."
    };
}
