using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc;

/// <summary>Turns on the browser backend of <see cref="Viewport3D"/> and <see cref="ModelViewer"/>.</summary>
public static class ArcGraphics3DBrowser
{
    private static Task<bool>? s_use;

    /// <summary>
    /// Imports the <c>arc-graphics3d</c> JavaScript module and lets viewports draw in a WebGL 2 context of the page.
    /// Viewports that are already on screen start drawing when it has loaded. Returns false when not running in a browser
    /// or when the module cannot be imported; the viewports then keep reporting <see cref="Viewport3D.RendererError"/>.
    /// Safe to call more than once.
    /// </summary>
    public static Task<bool> UseAsync() => s_use ??= ImportAsync();

    private static async Task<bool> ImportAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return false;
        }

        try
        {
            await JSHost.ImportAsync(BrowserGraphics3DInterop.Module, ModuleUrl());
            OffscreenContext.PlatformFactory = BrowserOffscreenContext.TryCreateShared;
            return true;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"TKCode.Arc.Graphics3D.Browser: {exception.Message}; the 3D viewports show no picture.");
            s_use = null;
            return false;
        }
    }

    private static string ModuleUrl()
    {
        using var stream = typeof(ArcGraphics3DBrowser).Assembly.GetManifestResourceStream("TKCode.Arc.Graphics3D.Browser.arc-graphics3d.js")
                           ?? throw new InvalidOperationException("arc-graphics3d.js is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return "data:text/javascript;charset=utf-8," + Uri.EscapeDataString(reader.ReadToEnd());
    }
}
