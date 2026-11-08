namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The documents of the PDF demos: gallery/TKCode.Arc.Gallery/Assets/Pdf, made by tools/pdf/make-sample.py. They are resources of
/// TKCode.Arc.Gallery.Shots on desktop; the WebAssembly host serves them as files and sets <see cref="HttpRoot"/>, so a browser
/// downloads them only when a PDF page shows them. A host without a renderer (docs screenshots, headless tests) gets
/// <see cref="Simulated"/> instead: the two properties are set together, and a viewer with neither source shows nothing.
/// </summary>
public static class PdfDemoMedia
{
    /// <summary>The password of <see cref="Locked"/>.</summary>
    public const string LockedPassword = "arc-sample";

    /// <summary>The page's base address when the documents are served over HTTP (<c>media/Pdf/</c> under it); null for the resources.</summary>
    public static Uri? HttpRoot { get; set; }

    /// <summary>12 pages with an outline, page labels, internal and external links, English and Chinese text. Null without a renderer.</summary>
    public static PdfSource? Sample => Open("arc-sample.pdf");

    /// <summary>3 pages behind the password <see cref="LockedPassword"/>. Null without a renderer.</summary>
    public static PdfSource? Locked => Open("arc-sample-locked.pdf");

    /// <summary>Generated pages for a host without a renderer; null when there is one.</summary>
    public static PdfDocument? Simulated => ArcPdf.Backend.IsReal ? null : new SimulatedPdfDocument();

    private static PdfSource? Open(string name)
    {
        if (!ArcPdf.Backend.IsReal)
        {
            return null;
        }
        return PdfSource.FromUri(HttpRoot is { } root
            ? new Uri(root, "media/Pdf/" + name)
            : new Uri("avares://TKCode.Arc.Gallery.Shots/Assets/Pdf/" + name));
    }
}
