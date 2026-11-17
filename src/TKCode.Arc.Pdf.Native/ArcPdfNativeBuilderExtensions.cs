namespace TKCode.Arc;

/// <summary>The <c>UseArc(...)</c> entry of TKCode.Arc.Pdf.Native (#149, #151).</summary>
public static class ArcPdfNativeBuilderExtensions
{
    /// <summary>The package id in <c>ArcRuntime.Packages</c>.</summary>
    internal const string PackageId = "TKCode.Arc.Pdf.Native";

    /// <summary>
    /// Enables <see cref="ArcPdfBuilderExtensions.UseArcPdf"/> and turns on the desktop PDF backend (PDFium) when the app
    /// starts, as <see cref="ArcPdfNative.Use"/> does: when the library cannot be loaded, documents fail with
    /// <see cref="PdfErrorKind.BackendUnavailable"/> and the reason goes to <c>Trace</c>.
    /// </summary>
    public static ArcBuilder UseArcPdfNative(this ArcBuilder builder)
        => builder.UsePackage(PackageId, static package => package
            .DependsOn(static b => b.UseArcPdf())
            .OnCommit(static _ => ArcPdfNative.Use()));
}
