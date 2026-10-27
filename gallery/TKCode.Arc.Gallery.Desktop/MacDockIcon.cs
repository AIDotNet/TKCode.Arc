using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;

namespace TKCode.Arc.Gallery.Desktop;

/// <summary>
/// Gives an unbundled run on macOS (<c>dotnet run</c>, the bare apphost) the Gallery's Dock icon. Inside a .app the Dock reads
/// <c>AppIcon.icns</c> from the bundle; without one it shows the system's blank document and Avalonia's window icon does not
/// replace it.
/// </summary>
internal static class MacDockIcon
{
    private const string ObjC = "/usr/lib/libobjc.dylib";
    private static readonly Uri s_icon = new("avares://TKCode.Arc.Gallery.Desktop/Assets/AppIcon.icns");

    public static void Apply()
    {
        if (!OperatingSystem.IsMacOS() || AppContext.BaseDirectory.Contains(".app/Contents/", StringComparison.Ordinal))
        {
            return;
        }
        try
        {
            using var stream = AssetLoader.Open(s_icon);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var data = Send(Class("NSData"), Selector("dataWithBytes:length:"), handle.AddrOfPinnedObject(), (nuint)bytes.Length);
                var image = Send(Send(Class("NSImage"), Selector("alloc")), Selector("initWithData:"), data);
                if (image != IntPtr.Zero)
                {
                    Send(Send(Class("NSApplication"), Selector("sharedApplication")), Selector("setApplicationIconImage:"), image);
                }
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or IOException or FileNotFoundException)
        {
            // The icon is cosmetic.
        }
    }

    private static IntPtr Class(string name) => objc_getClass(name);

    private static IntPtr Selector(string name) => sel_registerName(name);

    [DllImport(ObjC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(ObjC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument, nuint length);
}
