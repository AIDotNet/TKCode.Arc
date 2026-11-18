using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc;

/// <summary>The <c>arc-graphics3d</c> module (arc-graphics3d.js) and the OpenGL ES lookup of the WebAssembly runtime.</summary>
internal static unsafe partial class BrowserGraphics3DInterop
{
    public const string Module = "arc-graphics3d";

    /// <summary>Creates the WebGL 2 context; returns its handle, or 0 when there is none to be had.</summary>
    [JSImport("create", Module)]
    public static partial int Create();

    /// <summary>Makes a context current and returns the handle of the one that was current (0 for none).</summary>
    [JSImport("makeCurrent", Module)]
    public static partial int MakeCurrent(int handle);

    [JSImport("restore", Module)]
    public static partial void Restore(int previous);

    [JSImport("isLost", Module)]
    public static partial bool IsLost(int handle);

    [JSImport("destroy", Module)]
    public static partial void Destroy(int handle);

    /// <summary>
    /// The address of an OpenGL ES function of the runtime's native module (Emscripten's implementation over WebGL), or 0.
    /// Avalonia.Browser resolves the functions for its own renderer through the same entry: the symbol is part of every
    /// Avalonia browser app, and the module name is the one its P/Invoke table is built under (the app links Skia statically;
    /// the WebAssembly build only binds P/Invokes whose module is a native library of the app).
    /// </summary>
    [DllImport("libSkiaSharp", EntryPoint = "eglGetProcAddress")]
    public static extern nint GetProcAddress(byte* name);
}
