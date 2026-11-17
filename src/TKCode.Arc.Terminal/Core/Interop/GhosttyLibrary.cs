using System.Reflection;
using System.Runtime.InteropServices;

namespace TKCode.Terminal.Core.Interop;

/// <summary>
/// Finds libghostty-vt where the default probing does not. Desktop apps get it from <c>runtimes/&lt;rid&gt;/native</c>
/// (deps.json) and Android apps from the APK's native library folder, so this only acts on iOS, where the library ships as
/// the dynamic framework <c>ghostty-vt.framework</c> (from <c>runtimes/ios/native/ghostty-vt.xcframework</c>) in the app
/// bundle's <c>Frameworks/</c> folder.
/// </summary>
internal static class GhosttyLibrary
{
    /// <summary>The library name of every <see cref="GhosttyNative"/> import.</summary>
    public const string Name = "ghostty-vt";

    private static int s_resolverSet;

    /// <summary>Registers the resolver once, before the first call into the library (no-op off iOS).</summary>
    public static void EnsureResolver()
    {
        if (!OperatingSystem.IsIOS() || Interlocked.Exchange(ref s_resolverSet, 1) != 0)
        {
            return;
        }
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(GhosttyLibrary).Assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // The host already set a resolver for this assembly; it owns the lookup.
        }
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Name)
        {
            return 0;
        }
        var framework = Path.Combine(AppContext.BaseDirectory, "Frameworks", "ghostty-vt.framework", "ghostty-vt");
        if (NativeLibrary.TryLoad(framework, out var handle))
        {
            return handle;
        }
        // Not embedded as a framework (for example linked into the app another way): fall back to the default probing.
        return NativeLibrary.TryLoad(name, assembly, searchPath, out handle) ? handle : 0;
    }
}
