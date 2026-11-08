using TKCode.Arc.Generated.TKCodeArcPdf;

namespace TKCode.Arc;

/// <summary>Registers the TKCode.Arc.Pdf package.</summary>
public static class ArcPdfBuilderExtensions
{
    /// <summary>
    /// Enables <see cref="PdfViewer"/> and its built-in text in en-US, zh-CN and zh-TW. Depends on
    /// <see cref="ArcBuilderExtensions.UseArcControls"/>. The renderer is chosen separately (<see cref="ArcPdf.Backend"/>;
    /// <c>ArcPdfNative.Use()</c> on desktop, <c>ArcPdfBrowser.UseAsync()</c> in a browser).
    /// </summary>
    public static ArcBuilder UseArcPdf(this ArcBuilder builder)
        => builder.UsePackage(ArcGeneratedAssets.PackageId, static package => ArcGeneratedAssets.AddTo(package
            .DependsOn(static b => b.UseArcControls())));
}
