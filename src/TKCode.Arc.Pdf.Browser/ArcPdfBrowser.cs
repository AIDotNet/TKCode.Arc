using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc;

/// <summary>Turns on the browser PDF backend: pdf.js, from the package's static web assets (design/components/pdf/pdf.md section 5.9).</summary>
public static class ArcPdfBrowser
{
    /// <summary>Where a host serves the package's static web assets, relative to the page's base address.</summary>
    private const string DefaultAssetsUrl = "_content/TKCode.Arc.Pdf.Browser/pdfjs/";

    private static Task<bool>? s_use;

    /// <summary>
    /// Imports pdf.js and makes it the <see cref="ArcPdf.Backend"/>. Returns false, and leaves the current backend in place, when not
    /// running in a browser or pdf.js cannot be imported; the reason goes to <see cref="System.Diagnostics.Trace"/>. Safe to call more
    /// than once; the first successful call decides where the files come from.
    /// </summary>
    /// <param name="assetsUrl">
    /// Where pdf.js is served, for a host that does not serve static web assets at the usual place (a CDN, say). A relative address
    /// is resolved against the page's base address (<c>document.baseURI</c>), so an app under a sub-path needs a <c>&lt;base&gt;</c>
    /// element only if its page addresses change. Null is <c>_content/TKCode.Arc.Pdf.Browser/pdfjs/</c>.
    /// </param>
    public static Task<bool> UseAsync(string? assetsUrl = null) => s_use ??= ImportAsync(assetsUrl ?? DefaultAssetsUrl);

    private static async Task<bool> ImportAsync(string assetsUrl)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return false;
        }
        try
        {
            await JSHost.ImportAsync(BrowserPdfInterop.Module, ModuleUrl());
            await BrowserPdfInterop.Init(assetsUrl);
            ArcPdf.Backend = BrowserPdfBackend.Instance;
            PdfTextNormalizer.CompatibilityForm = BrowserPdfInterop.NormalizeCompatibility;
            return true;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"TKCode.Arc.Pdf.Browser: {exception.Message}; PDF stays on {ArcPdf.Backend.Name}.");
            s_use = null;
            return false;
        }
    }

    private static string ModuleUrl()
    {
        using var stream = typeof(ArcPdfBrowser).Assembly.GetManifestResourceStream("TKCode.Arc.Pdf.Browser.arc-pdf.js")
                           ?? throw new InvalidOperationException("arc-pdf.js is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return "data:text/javascript;charset=utf-8," + Uri.EscapeDataString(reader.ReadToEnd());
    }
}
