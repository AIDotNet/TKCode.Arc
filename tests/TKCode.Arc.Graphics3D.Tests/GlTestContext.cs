using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Rhi.OpenGL;
using TKCode.Arc.Graphics3D.Rhi.WebGpu;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>An off-screen graphics device for tests that render for real: by default an OpenGL context, available on
/// macOS (CGL, OpenGL 4.1 core) and on Linux with EGL and OpenGL ES 3 (Mesa, also its software rasterizer, without a
/// display server); elsewhere <see cref="TryCreate"/> returns null and the tests skip themselves.</summary>
/// <remarks>
/// <para>Set the environment variable <c>TKCODE_ARC_ANGLE</c> to a directory that holds ANGLE's <c>libEGL</c> and
/// <c>libGLESv2</c> (every Chromium or Electron application ships them) to run the same tests on OpenGL ES 3.0 with
/// GLSL ES 3.00 instead: the implementation and shader dialect Avalonia uses on Windows.</para>
/// <para>Set <c>TKCODE_ARC_WGPU</c> to the path of <c>libwgpu_native</c> (a wgpu-native release, see
/// <see cref="WgpuDevice.NativeVersion"/>) to run them on the WebGPU backend instead; no OpenGL context is created.
/// <c>TKCODE_ARC_WGPU_BACKEND</c> (<c>metal</c>, <c>vulkan</c>, <c>d3d12</c>, <c>gl</c>) picks the graphics API under
/// it, and <c>TKCODE_ARC_WGPU_SOFTWARE=1</c> asks for a software adapter.</para>
/// <para>Set <c>TKCODE_ARC_RENDER_THREAD=1</c> to run them through <see cref="ThreadedGraphicsDevice"/>: the device
/// then belongs to a render thread and the tests only record commands.</para>
/// </remarks>
internal sealed unsafe class GlTestContext : IDisposable
{
    private const string OpenGlFramework = "/System/Library/Frameworks/OpenGL.framework/OpenGL";

    // One rendering test at a time: test classes run in parallel, and neither the measurements nor every OpenGL
    // implementation (ANGLE) take several contexts on several threads at once well.
    private static readonly SemaphoreSlim s_gate = new(1, 1);
    private static bool s_renderThread;
    private readonly nint _library;
    private readonly nint _context;
    private readonly nint _eglLibrary;
    private readonly nint _eglDisplay;
    private readonly nint _eglSurface;
    // The WebGPU device, or its wrapper on a render thread.
    private readonly GraphicsDevice? _wgpu;

    private GlTestContext(GraphicsDevice device)
    {
        _wgpu = device;
        Device = device;
    }

    private GlTestContext(nint library, nint context)
    {
        _library = library;
        _context = context;
        Device = CreateDevice();
    }

    private GlTestContext(nint egl, nint gles, nint display, nint surface, nint context)
    {
        _eglLibrary = egl;
        _library = gles;
        _eglDisplay = display;
        _eglSurface = surface;
        _context = context;
        Device = CreateDevice();
    }

    private GraphicsDevice CreateDevice()
    {
        GlDevice Create() => new(name => NativeLibrary.TryGetExport(_library, name, out var address) ? address : 0);
        if (!s_renderThread)
        {
            return Create();
        }

        // The context was made current on this thread when it was created: give it to the render thread.
        MakeCurrent(false);
        return new ThreadedGraphicsDevice(
            () =>
            {
                MakeCurrent(true);
                return Create();
            },
            () => MakeCurrent(false));
    }

    private void MakeCurrent(bool current)
    {
        if (_eglLibrary != 0)
        {
            var makeCurrent = (delegate* unmanaged<nint, nint, nint, nint, int>)NativeLibrary.GetExport(_eglLibrary, "eglMakeCurrent");
            makeCurrent(_eglDisplay, current ? _eglSurface : 0, current ? _eglSurface : 0, current ? _context : 0);
            return;
        }

        ((delegate* unmanaged<nint, int>)NativeLibrary.GetExport(_library, "CGLSetCurrentContext"))(current ? _context : 0);
    }

    private static GlTestContext? TryCreateAngle(string directory)
    {
        string File(string name) => Path.Combine(directory, OperatingSystem.IsWindows() ? name + ".dll" : OperatingSystem.IsMacOS() ? name + ".dylib" : name + ".so");
        return TryCreateEgl(File("libEGL"), File("libGLESv2"));
    }

    private static GlTestContext? TryCreateEgl(string eglPath, string glesPath)
    {
        if (!NativeLibrary.TryLoad(eglPath, out var egl) || !NativeLibrary.TryLoad(glesPath, out var gles))
        {
            return null;
        }

        var getDisplay = (delegate* unmanaged<nint, nint>)NativeLibrary.GetExport(egl, "eglGetDisplay");
        var initialize = (delegate* unmanaged<nint, int*, int*, int>)NativeLibrary.GetExport(egl, "eglInitialize");
        var chooseConfig = (delegate* unmanaged<nint, int*, nint*, int, int*, int>)NativeLibrary.GetExport(egl, "eglChooseConfig");
        var createPbuffer = (delegate* unmanaged<nint, nint, int*, nint>)NativeLibrary.GetExport(egl, "eglCreatePbufferSurface");
        var bindApi = (delegate* unmanaged<int, int>)NativeLibrary.GetExport(egl, "eglBindAPI");
        var createContext = (delegate* unmanaged<nint, nint, nint, int*, nint>)NativeLibrary.GetExport(egl, "eglCreateContext");
        var makeCurrent = (delegate* unmanaged<nint, nint, nint, nint, int>)NativeLibrary.GetExport(egl, "eglMakeCurrent");
        var display = getDisplay(0);
        int major, minor;
        if (display == 0 || initialize(display, &major, &minor) == 0)
        {
            // No display server (a CI machine): Mesa's surfaceless platform, EGL_PLATFORM_SURFACELESS_MESA.
            if (!NativeLibrary.TryGetExport(egl, "eglGetPlatformDisplay", out var getPlatformDisplay))
            {
                return null;
            }

            display = ((delegate* unmanaged<int, nint, nint*, nint>)getPlatformDisplay)(0x31DD, 0, null);
            if (display == 0 || initialize(display, &major, &minor) == 0)
            {
                return null;
            }
        }

        // EGL_SURFACE_TYPE = EGL_PBUFFER_BIT, EGL_RENDERABLE_TYPE = EGL_OPENGL_ES3_BIT, 8 bits per channel.
        var configAttributes = stackalloc int[] { 0x3033, 0x0001, 0x3040, 0x0040, 0x3024, 8, 0x3023, 8, 0x3022, 8, 0x3021, 8, 0x3038 };
        nint config;
        int count;
        if (chooseConfig(display, configAttributes, &config, 1, &count) == 0 || count == 0)
        {
            return null;
        }

        // EGL_WIDTH, EGL_HEIGHT.
        var surfaceAttributes = stackalloc int[] { 0x3057, 16, 0x3056, 16, 0x3038 };
        var surface = createPbuffer(display, config, surfaceAttributes);

        // EGL_OPENGL_ES_API; EGL_CONTEXT_CLIENT_VERSION = 3.
        bindApi(0x30A0);
        var contextAttributes = stackalloc int[] { 0x3098, 3, 0x3038 };
        var context = createContext(display, config, 0, contextAttributes);
        if (surface == 0 || context == 0 || makeCurrent(display, surface, surface, context) == 0)
        {
            return null;
        }

        return new GlTestContext(egl, gles, display, surface, context);
    }

    /// <summary>The device: a <see cref="GlDevice"/> or a <see cref="WgpuDevice"/>.</summary>
    public GraphicsDevice Device { get; }

    /// <summary>The path of the wgpu-native library when the tests are to run on WebGPU, or null.</summary>
    public static string? WgpuLibrary => Environment.GetEnvironmentVariable("TKCODE_ARC_WGPU") is { Length: > 0 } path ? path : null;

    private static GlTestContext CreateWgpu(string library)
    {
        var backend = Environment.GetEnvironmentVariable("TKCODE_ARC_WGPU_BACKEND")?.ToLowerInvariant() switch
        {
            "metal" => WgpuBackend.Metal,
            "vulkan" => WgpuBackend.Vulkan,
            "d3d12" => WgpuBackend.Direct3D12,
            "gl" => WgpuBackend.OpenGl,
            _ => WgpuBackend.Default,
        };

        // A library that was asked for and does not work is an error, not a reason to skip.
        WgpuDevice Create()
        {
            var device = WgpuDevice.Create(library, new WgpuDeviceOptions
            {
                Backend = backend,
                ForceFallbackAdapter = Environment.GetEnvironmentVariable("TKCODE_ARC_WGPU_SOFTWARE") == "1",
            });

            // wgpu-native ends the process when an invalid object reaches the queue: say why first.
            device.ErrorReported += static error => Console.Error.WriteLine("wgpu: " + error);
            return device;
        }

        return s_renderThread ? new GlTestContext(new ThreadedGraphicsDevice(Create)) : new GlTestContext(Create());
    }

    /// <summary>Creates a device (for OpenGL: a context, made current on the calling thread), or returns null when the
    /// platform has no off-screen OpenGL.</summary>
    /// <param name="renderThread">Whether the device lives on a render thread; null follows
    /// <c>TKCODE_ARC_RENDER_THREAD</c>.</param>
    public static GlTestContext? TryCreate(bool? renderThread = null)
    {
        s_gate.Wait();
        GlTestContext? context = null;
        try
        {
            // Read when the device is created; the gate makes this the only one being created.
            s_renderThread = renderThread ?? Environment.GetEnvironmentVariable("TKCODE_ARC_RENDER_THREAD") is { Length: > 0 };
            context = WgpuLibrary is { } wgpu ? CreateWgpu(wgpu)
                : Environment.GetEnvironmentVariable("TKCODE_ARC_ANGLE") is { Length: > 0 } angle ? TryCreateAngle(angle) : TryCreateNative();
        }
        finally
        {
            if (context is null)
            {
                s_gate.Release();
            }
        }

        return context;
    }

    private static GlTestContext? TryCreateNative()
    {
        if (OperatingSystem.IsLinux())
        {
            return TryCreateEgl("libEGL.so.1", "libGLESv2.so.2");
        }

        if (!OperatingSystem.IsMacOS() || !NativeLibrary.TryLoad(OpenGlFramework, out var library))
        {
            return null;
        }

        var choose = (delegate* unmanaged<int*, nint*, int*, int>)NativeLibrary.GetExport(library, "CGLChoosePixelFormat");
        var create = (delegate* unmanaged<nint, nint, nint*, int>)NativeLibrary.GetExport(library, "CGLCreateContext");
        var destroyFormat = (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(library, "CGLDestroyPixelFormat");
        var setCurrent = (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(library, "CGLSetCurrentContext");

        // kCGLPFAOpenGLProfile (99) = kCGLOGLPVersion_GL4_Core (0x4100), kCGLPFAColorSize (8) = 24,
        // kCGLPFAAllowOfflineRenderers (96).
        var attributes = stackalloc int[] { 99, 0x4100, 8, 24, 96, 0 };
        nint format;
        int count;
        if (choose(attributes, &format, &count) != 0 || format == 0)
        {
            return null;
        }

        nint context;
        var error = create(format, 0, &context);
        destroyFormat(format);
        if (error != 0 || context == 0 || setCurrent(context) != 0)
        {
            return null;
        }

        return new GlTestContext(library, context);
    }

    /// <summary>Creates a context or skips the running test.</summary>
    public static GlTestContext CreateOrSkip(bool? renderThread = null)
    {
        var context = TryCreate(renderThread);
        Assert.SkipWhen(context is null, "No off-screen OpenGL context on this platform (and TKCODE_ARC_WGPU is not set).");
        return context!;
    }

    public void Dispose()
    {
        try
        {
            DisposeCore();
        }
        finally
        {
            s_gate.Release();
        }
    }

    private void DisposeCore()
    {
        if (_wgpu is not null)
        {
            // A validation error wgpu reported and no test looked at is still a failure.
            // (A device whose render thread failed on purpose has nothing left to ask.)
            var errors = _wgpu is ThreadedGraphicsDevice { HasFailed: true } ? [] : _wgpu.DrainErrors();
            _wgpu.Dispose();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("wgpu reported validation errors:\n" + string.Join("\n", errors));
            }

            return;
        }

        Device.Dispose();
        if (_eglLibrary != 0)
        {
            var makeCurrent = (delegate* unmanaged<nint, nint, nint, nint, int>)NativeLibrary.GetExport(_eglLibrary, "eglMakeCurrent");
            var destroyContext = (delegate* unmanaged<nint, nint, int>)NativeLibrary.GetExport(_eglLibrary, "eglDestroyContext");
            var destroySurface = (delegate* unmanaged<nint, nint, int>)NativeLibrary.GetExport(_eglLibrary, "eglDestroySurface");
            makeCurrent(_eglDisplay, 0, 0, 0);
            destroyContext(_eglDisplay, _context);
            destroySurface(_eglDisplay, _eglSurface);
            return;
        }

        var setCurrent = (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(_library, "CGLSetCurrentContext");
        var destroy = (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(_library, "CGLDestroyContext");
        setCurrent(0);
        destroy(_context);
    }
}

/// <summary>What the tests ask of either device beyond <see cref="GraphicsDevice"/>.</summary>
internal static class TestDeviceExtensions
{
    /// <summary>The errors the graphics API reported since the last call (OpenGL error codes, wgpu validation messages).</summary>
    public static IReadOnlyList<string> DrainErrors(this GraphicsDevice device)
    {
        switch (device)
        {
            case GlDevice gl:
                return [.. gl.DrainErrors().Select(e => "0x" + e.ToString("X"))];
            case WgpuDevice wgpu:
                return wgpu.DrainErrors();
            case ThreadedGraphicsDevice threaded:
            {
                // The real device lives on the render thread: ask it there, after everything recorded so far.
                IReadOnlyList<string> errors = [];
                threaded.Invoke(inner => errors = inner.DrainErrors());
                return errors;
            }

            default:
                return [];
        }
    }

    /// <summary>Blocks until the GPU finished all submitted work.</summary>
    public static void Finish(this GraphicsDevice device)
    {
        if (device is GlDevice gl)
        {
            gl.Finish();
        }
        else if (device is WgpuDevice wgpu)
        {
            wgpu.Finish();
        }
        else if (device is ThreadedGraphicsDevice threaded)
        {
            threaded.Invoke(static inner => inner.Finish());
        }
    }
}

/// <summary>Renders scenes to images and writes them under <c>artifacts/graphics3d</c> for inspection (on the WebGPU
/// backend under <c>artifacts/graphics3d/webgpu</c>, so that the two sets can be compared).</summary>
internal static class RenderHarness
{
    public static string OutputDirectory
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (dir is not null && !File.Exists(Path.Combine(dir, "TKCode.Arc.slnx")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            var output = Path.Combine(dir ?? AppContext.BaseDirectory, "artifacts", "graphics3d");
            if (GlTestContext.WgpuLibrary is not null)
            {
                output = Path.Combine(output, "webgpu");
            }

            Directory.CreateDirectory(output);
            return output;
        }
    }

    public static TextureImage Render(Renderer renderer, Scene scene, Camera camera, int width, int height, string? saveAs = null, int samples = 4)
    {
        using var target = new RenderTarget(width, height, Rhi.TextureFormat.RGBA8, samples) { ColorSpace = TextureColorSpace.Srgb };
        renderer.Render(scene, camera, target);
        var image = renderer.ReadPixels(target);
        if (saveAs is not null)
        {
            File.WriteAllBytes(Path.Combine(OutputDirectory, saveAs + ".png"), Png.Encode(image));
        }

        return image;
    }

    public static (byte R, byte G, byte B, byte A) Pixel(TextureImage image, int x, int y)
    {
        var data = (byte[])image.Data;
        var o = ((y * image.Width) + x) * 4;
        return (data[o], data[o + 1], data[o + 2], data[o + 3]);
    }
}
