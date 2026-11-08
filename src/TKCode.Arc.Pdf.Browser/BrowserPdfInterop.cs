using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc;

/// <summary>The <c>arc-pdf</c> module (arc-pdf.js). A document handle is the module's per-document state object, a render handle its per-render one.</summary>
internal static partial class BrowserPdfInterop
{
    public const string Module = "arc-pdf";

    [JSImport("init", Module)]
    public static partial Task Init(string assetsUrl);

    /// <summary>NFKC by the browser: search needs it, and <c>string.Normalize</c> does not have that form in WebAssembly.</summary>
    [JSImport("normalizeCompatibility", Module)]
    public static partial string NormalizeCompatibility(string text);

    [JSImport("openUrl", Module)]
    public static partial JSObject OpenUrl(string url, string? password);

    [JSImport("openBytes", Module)]
    public static partial JSObject OpenBytes([JSMarshalAs<JSType.MemoryView>] Span<byte> data, string? password);

    /// <summary>Completes when the document is open or has failed, and never faults: the handle's <c>errorName</c> says which.</summary>
    [JSImport("waitOpen", Module)]
    public static partial Task WaitOpen(JSObject document);

    [JSImport("close", Module)]
    public static partial void Close(JSObject document);

    [JSImport("numbers", Module)]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static partial double[] Numbers(JSObject result, string key);

    [JSImport("strings", Module)]
    [return: JSMarshalAs<JSType.Array<JSType.String>>]
    public static partial string[] Strings(JSObject result, string key);

    [JSImport("readSizes", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    public static partial Task<JSObject> ReadSizes(JSObject document, int first, int count);

    [JSImport("readInfo", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    public static partial Task<JSObject> ReadInfo(JSObject document);

    [JSImport("readLabels", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    public static partial Task<JSObject> ReadLabels(JSObject document);

    [JSImport("readOutline", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    public static partial Task<JSObject> ReadOutline(JSObject document, int maxNodes, int maxDepth);

    [JSImport("readText", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    public static partial Task<JSObject> ReadText(JSObject document, int pageIndex);

    [JSImport("readLinks", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    public static partial Task<JSObject> ReadLinks(JSObject document, int pageIndex);

    [JSImport("beginRender", Module)]
    public static partial JSObject BeginRender(JSObject document, int pageIndex, int targetWidth, int targetHeight, int x, int y, int width, int height);

    /// <summary>1 when painted, 0 when the page cannot be painted, -1 when cancelled. Never faults.</summary>
    [JSImport("waitRender", Module)]
    [return: JSMarshalAs<JSType.Promise<JSType.Number>>]
    public static partial Task<int> WaitRender(JSObject render);

    [JSImport("cancelRender", Module)]
    public static partial void CancelRender(JSObject render);

    /// <summary>Copies the painted pixels into <paramref name="pixels"/> as BGRA, row after row without gaps, and lets go of the canvas.</summary>
    [JSImport("takePixels", Module)]
    public static partial void TakePixels(JSObject render, [JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

    [JSImport("releaseRender", Module)]
    public static partial void ReleaseRender(JSObject render);
}
