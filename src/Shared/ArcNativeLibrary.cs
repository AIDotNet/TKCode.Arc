using System.Reflection;
using System.Runtime.InteropServices;

namespace TKCode.Arc;

/// <summary>
/// Finding a native library next to the app (#149): the runtime identifier, the platform file name and the
/// <c>runtimes/&lt;rid&gt;/native</c> folder the packages put it in. Linked as source into Audio.Native, Video.Native and
/// Pdf.Native, so each keeps its own copy of these few lines as an internal type.
/// </summary>
internal static class ArcNativeLibrary
{
    /// <summary>The runtime identifier of the running process, such as <c>osx-arm64</c>.</summary>
    public static string CurrentRid()
    {
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64",
        };
        return $"{os}-{arch}";
    }

    /// <summary>The file name of library <paramref name="name"/> on this platform: <c>name.dll</c>, <c>libname.dylib</c> or <c>libname.so</c>.</summary>
    public static string FileName(string name)
        => OperatingSystem.IsWindows() ? name + ".dll" : OperatingSystem.IsMacOS() ? "lib" + name + ".dylib" : "lib" + name + ".so";

    /// <summary>Loads library <paramref name="name"/> from <c>runtimes/&lt;rid&gt;/native</c> under the app's folder.</summary>
    public static bool TryLoadFromRuntimes(string name, out nint handle)
        => NativeLibrary.TryLoad(Path.Combine(AppContext.BaseDirectory, "runtimes", CurrentRid(), "native", FileName(name)), out handle);

    /// <summary>
    /// Sets <paramref name="resolver"/> for <paramref name="assembly"/> once; a resolver the host set first is kept.
    /// </summary>
    public static void SetResolverOnce(Assembly assembly, DllImportResolver resolver, ref bool set)
    {
        if (set)
        {
            return;
        }
        set = true;
        try
        {
            NativeLibrary.SetDllImportResolver(assembly, resolver);
        }
        catch (InvalidOperationException)
        {
            // A resolver was already set for this assembly by the host; keep it.
        }
    }
}
