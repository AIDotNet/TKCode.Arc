using System.Runtime.InteropServices.JavaScript;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The off-screen OpenGL context of a <see cref="Viewport3D"/> in the browser: one WebGL 2 context on a canvas that is never
/// shown, shared by every viewport of the page (a page may hold only a limited number of WebGL contexts, and each viewport
/// draws into framebuffers of its own). The engine's OpenGL ES calls reach it through Emscripten's OpenGL ES layer.
/// </summary>
internal sealed unsafe class BrowserOffscreenContext : OffscreenContext
{
    private static int s_handle;
    private static int s_users;
    private readonly int _handle;
    private int _previous;
    private bool _disposed;

    private BrowserOffscreenContext(int handle) => _handle = handle;

    /// <summary>Returns a context, or null when the page has no WebGL 2.</summary>
    public static OffscreenContext? TryCreateShared()
    {
        try
        {
            if (s_users > 0 && BrowserGraphics3DInterop.IsLost(s_handle))
            {
                // The browser took the context away. Viewports that still hold it stop drawing (IsLost); a new one starts over.
                s_handle = 0;
                s_users = 0;
            }

            if (s_users == 0)
            {
                s_handle = BrowserGraphics3DInterop.Create();
                if (s_handle == 0)
                {
                    return null;
                }
            }

            s_users++;
            return new BrowserOffscreenContext(s_handle);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public override bool IsLost => _handle != s_handle || BrowserGraphics3DInterop.IsLost(_handle);

    public override void MakeCurrent() => _previous = BrowserGraphics3DInterop.MakeCurrent(_handle);

    // Avalonia's renderer expects its own context to stay current between its frames.
    public override void Release() => BrowserGraphics3DInterop.Restore(_previous);

    public override nint GetProcAddress(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* p = bytes)
        {
            return BrowserGraphics3DInterop.GetProcAddress(p);
        }
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_handle != s_handle)
        {
            // The context was lost and replaced since.
            BrowserGraphics3DInterop.Destroy(_handle);
            return;
        }

        if (--s_users == 0)
        {
            BrowserGraphics3DInterop.Destroy(s_handle);
            s_handle = 0;
        }
    }
}
