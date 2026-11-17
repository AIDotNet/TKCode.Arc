namespace TKCode.Arc;

/// <summary>The <c>UseArc(...)</c> entry of TKCode.Arc.Pdf.Browser (#149, #151).</summary>
public static class ArcPdfBrowserBuilderExtensions
{
    /// <summary>The package id in <c>ArcRuntime.Packages</c>.</summary>
    internal const string PackageId = "TKCode.Arc.Pdf.Browser";

    /// <summary>
    /// Enables <see cref="ArcPdfBuilderExtensions.UseArcPdf"/> and starts the browser PDF backend (pdf.js) when the app
    /// starts (<see cref="ArcPdfBrowser.UseAsync"/>, not awaited). A <see cref="PdfViewer"/> whose document failed because the
    /// backend was not there yet opens it again when it arrives (<see cref="ArcPdf.BackendChanged"/>).
    /// </summary>
    /// <param name="builder">The Arc builder.</param>
    /// <param name="assetsUrl">Where pdf.js is served from; null for the default of <see cref="ArcPdfBrowser.UseAsync"/>.</param>
    public static ArcBuilder UseArcPdfBrowser(this ArcBuilder builder, string? assetsUrl = null)
        => builder.UsePackage(PackageId, package => package
            .DependsOn(static b => b.UseArcPdf())
            .OnCommit(context => { _ = ArcPdfBrowser.UseAsync(assetsUrl); }));
}
