using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TKCode.Arc;

/// <summary>Turns on the desktop audio backend (miniaudio).</summary>
public static class ArcAudioNative
{
    private static readonly object s_gate = new();
    private static bool s_resolverSet;
    private static MiniaudioAudioBackend? s_backend;

    /// <summary>
    /// Loads the native library, opens the default output device and makes <see cref="MiniaudioAudioBackend"/> the
    /// <see cref="ArcAudio.Backend"/>. Returns false, and leaves the current backend in place, when the library is missing
    /// for this platform or no output device can be opened; the reason goes to <see cref="Trace"/>. Safe to call more than once.
    /// </summary>
    public static bool Use()
    {
        lock (s_gate)
        {
            if (s_backend is not null)
            {
                ArcAudio.Backend = s_backend;
                return true;
            }
            EnsureResolver();
            try
            {
                var version = MiniaudioInterop.Version();
                if (version != MiniaudioInterop.AbiVersion)
                {
                    Trace.TraceWarning($"TKCode.Arc.Audio.Native: tkcode_audio ABI {version}, expected {MiniaudioInterop.AbiVersion}; audio stays simulated.");
                    return false;
                }
                var result = MiniaudioInterop.EngineCreate(out var engine);
                if (result != 0 || engine == 0)
                {
                    Trace.TraceWarning($"TKCode.Arc.Audio.Native: no audio output (miniaudio result {result}); audio stays simulated.");
                    return false;
                }
                s_backend = new MiniaudioAudioBackend(engine);
                ArcAudio.Backend = s_backend;
                return true;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                Trace.TraceWarning($"TKCode.Arc.Audio.Native: {exception.Message}; audio stays simulated.");
                return false;
            }
        }
    }

    /// <summary>
    /// NuGet consumers get the library from the deps.json probe paths; a project reference copies it to
    /// <c>runtimes/&lt;rid&gt;/native</c> beside the assembly, which the default probing does not look in.
    /// </summary>
    private static void EnsureResolver() => ArcNativeLibrary.SetResolverOnce(typeof(ArcAudioNative).Assembly, Resolve, ref s_resolverSet);

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != MiniaudioInterop.Library)
        {
            return 0;
        }
        if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
        {
            return handle;
        }
        return ArcNativeLibrary.TryLoadFromRuntimes("tkcode_audio", out handle) ? handle : 0;
    }
}
