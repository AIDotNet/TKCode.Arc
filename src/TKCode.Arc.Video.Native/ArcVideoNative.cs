using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TKCode.Arc;

/// <summary>
/// Turns on the desktop video backend: the system player on each platform (Media Foundation on Windows, AVFoundation on
/// macOS, GStreamer on Linux) through the tkcode_video shim (design/components/video/video.md section 5.2).
/// </summary>
public static class ArcVideoNative
{
    private static readonly object s_gate = new();
    private static bool s_resolverSet;
    private static NativeVideoBackend? s_backend;

    /// <summary>
    /// Loads the tkcode_video library, initializes the system player framework and makes the native backend the
    /// <see cref="ArcVideo.Backend"/>. Returns false, and leaves the current backend in place, when the library or the
    /// framework is missing; the reason goes to <see cref="System.Diagnostics.Trace"/>. Safe to call more than once.
    /// </summary>
    /// <remarks>
    /// On macOS, AVFoundation needs the main dispatch queue to be serviced (an Avalonia app's main thread runs it); a host
    /// whose main thread never runs a run loop sees every load fail with <see cref="VideoErrorKind.Aborted"/> after a few seconds.
    /// </remarks>
    public static bool Use()
    {
        lock (s_gate)
        {
            if (s_backend is not null)
            {
                ArcVideo.Backend = s_backend;
                return true;
            }
            EnsureResolver();
            try
            {
                var version = VideoNativeInterop.Version();
                if (version != VideoNativeInterop.AbiVersion)
                {
                    Trace.TraceWarning($"TKCode.Arc.Video.Native: tkcode_video ABI {version}, expected {VideoNativeInterop.AbiVersion}; video stays on {ArcVideo.Backend.Name}.");
                    return false;
                }
                var result = VideoNativeInterop.Initialize();
                if (result == VideoNativeInterop.ErrorUnavailable)
                {
                    Trace.TraceWarning($"TKCode.Arc.Video.Native: the system player framework is not available ({FrameworkName}); video stays on {ArcVideo.Backend.Name}.");
                    return false;
                }
                if (result != VideoNativeInterop.Ok)
                {
                    Trace.TraceWarning($"TKCode.Arc.Video.Native: tkv_initialize failed ({result}); video stays on {ArcVideo.Backend.Name}.");
                    return false;
                }
                s_backend = new NativeVideoBackend(BackendName, VideoNativeInterop.FrameKinds(), VideoNativeInterop.GpuHandleType());
                ArcVideo.Backend = s_backend;
                return true;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                Trace.TraceWarning($"TKCode.Arc.Video.Native: {exception.Message}; video stays on {ArcVideo.Backend.Name}.");
                return false;
            }
        }
    }

    private static string BackendName => OperatingSystem.IsWindows() ? "mediafoundation" : OperatingSystem.IsMacOS() ? "avfoundation" : "gstreamer";

    private static string FrameworkName => OperatingSystem.IsWindows() ? "Media Foundation" : OperatingSystem.IsMacOS() ? "AVFoundation" : "GStreamer 1.0";

    /// <summary>
    /// NuGet consumers get the library from the deps.json probe paths; a project reference copies it to
    /// <c>runtimes/&lt;rid&gt;/native</c> beside the assembly, which the default probing does not look in.
    /// </summary>
    private static void EnsureResolver()
    {
        if (s_resolverSet)
        {
            return;
        }
        s_resolverSet = true;
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(ArcVideoNative).Assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // A resolver was already set for this assembly by the host; keep it.
        }
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != VideoNativeInterop.Library)
        {
            return 0;
        }
        if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
        {
            return handle;
        }
        var file = OperatingSystem.IsWindows() ? "tkcode_video.dll" : OperatingSystem.IsMacOS() ? "libtkcode_video.dylib" : "libtkcode_video.so";
        var path = Path.Combine(AppContext.BaseDirectory, "runtimes", CurrentRid(), "native", file);
        return NativeLibrary.TryLoad(path, out handle) ? handle : 0;
    }

    private static string CurrentRid()
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
}
