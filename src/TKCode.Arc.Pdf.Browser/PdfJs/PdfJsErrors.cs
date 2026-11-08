namespace TKCode.Arc;

/// <summary>Maps a failure to open a document in pdf.js to a <see cref="PdfErrorKind"/>.</summary>
internal static class PdfJsErrors
{
    /// <param name="name">The exception's name, which pdf.js keeps across the worker boundary.</param>
    /// <param name="status">The HTTP status of a <c>ResponseException</c>.</param>
    /// <param name="missing">Whether pdf.js says that the address does not exist.</param>
    /// <param name="detail">
    /// The message. Only used to tell failures apart that all arrive as <c>UnknownErrorException</c>: any error that is not one of
    /// pdf.js's few own kinds gets that name in the worker, with the original in the message.
    /// </param>
    /// <param name="fromAddress">Whether pdf.js was downloading the document.</param>
    public static PdfErrorKind Map(string name, int status, bool missing, string? detail, bool fromAddress)
    {
        switch (name)
        {
            case "PasswordException":
                return PdfErrorKind.PasswordRequired;
            case "AbortException":
                return PdfErrorKind.Aborted;
            case "ResponseException":
                return missing || status is 404 or 410 ? PdfErrorKind.NotFound : PdfErrorKind.Network;
            case "InvalidPDFException":
                return PdfErrorKind.Corrupt;
        }
        var text = detail ?? string.Empty;
        // What fetch rejects with when a server cannot be reached or CORS refuses it: Chromium, Firefox and Safari in turn.
        if (fromAddress && (text.Contains("Failed to fetch", StringComparison.OrdinalIgnoreCase)
                            || text.Contains("NetworkError", StringComparison.OrdinalIgnoreCase)
                            || text.Contains("Load failed", StringComparison.OrdinalIgnoreCase)))
        {
            return PdfErrorKind.Network;
        }
        if (text.Contains("encryption", StringComparison.OrdinalIgnoreCase) || text.Contains("crypt", StringComparison.OrdinalIgnoreCase))
        {
            return PdfErrorKind.Unsupported;
        }
        // As in the PDFium backend: what cannot be read is damaged as far as anyone can tell.
        return PdfErrorKind.Corrupt;
    }
}
