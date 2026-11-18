using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// The fallback presenter: an OpenGL context of the viewport's own renders off screen, the pixels are read back and drawn
/// as a bitmap. It needs nothing from Avalonia's renderer, so it works under software rendering and wherever no context or
/// surface can be shared, at the price of one read-back per frame. The viewport draws on demand, so a still scene pays it once.
/// In the browser it is the only presenter: Avalonia.Browser shares neither its WebGL context nor textures, and the context
/// is a WebGL 2 context that TKCode.Arc.Graphics3D.Browser creates (<see cref="OffscreenContext.PlatformFactory"/>).
/// </summary>
internal sealed unsafe class BitmapPresenter : ViewportPresenter
{
    private readonly OffscreenContext _context;
    private readonly Action<TimeSpan> _onFrame;
    private OffscreenFramebuffer? _framebuffer;
    private WriteableBitmap? _bitmap;
    private byte[] _pixels = [];
    private PixelSize _size;
    private bool _requested;
    private bool _attached;

    // With a render thread: the frame being drawn and read back there, and whether another was asked for meanwhile.
    private bool _threaded;
    private bool _inFlight;
    private bool _again;

    private BitmapPresenter(Viewport3D owner, OffscreenContext context)
        : base(owner)
    {
        _context = context;
        _onFrame = OnFrame;
    }

    /// <summary>Returns a presenter when an off-screen OpenGL context can be created on this machine; else null.</summary>
    public static BitmapPresenter? TryCreate(Viewport3D owner)
    {
        var context = OffscreenContext.TryCreate();
        return context is null ? null : new BitmapPresenter(owner, context);
    }

    public override void Attach()
    {
        // A browser page has one thread (and its WebGL context is tied to it).
        if (Owner.UseRenderThread && !OperatingSystem.IsBrowser())
        {
            // The context goes to the render thread for good; this thread never makes it current.
            var created = Owner.InitRendererOnThread(
                () =>
                {
                    _context.MakeCurrent();
                    _framebuffer = new OffscreenFramebuffer(_context.GetProcAddress);
                    return new Graphics3D.Rhi.OpenGL.GlDevice(_context.GetProcAddress);
                },
                () =>
                {
                    _framebuffer?.Dispose();
                    _framebuffer = null;
                    _context.Release();
                });
            if (!created)
            {
                throw new NotSupportedException(Owner.RendererError ?? "The OpenGL renderer could not be created.");
            }

            _threaded = true;
            _attached = true;
            return;
        }

        _context.MakeCurrent();
        try
        {
            if (!Owner.InitRenderer(_context.GetProcAddress))
            {
                throw new NotSupportedException(Owner.RendererError ?? "The OpenGL renderer could not be created.");
            }

            _framebuffer = new OffscreenFramebuffer(_context.GetProcAddress);
        }
        finally
        {
            _context.Release();
        }

        _attached = true;
    }

    public override void Detach()
    {
        if (_attached && _threaded)
        {
            // Disposing the device ends the render thread, which releases the framebuffer and the context on its way out.
            _attached = false;
            Owner.DisposeRenderer(contextLost: false);
        }
        else if (_attached)
        {
            _attached = false;
            if (_context.IsLost)
            {
                // The objects went with the context: nothing to delete, and no call to make on it.
                Owner.DisposeRenderer(contextLost: true);
                _framebuffer = null;
            }
            else
            {
                _context.MakeCurrent();
                Owner.DisposeRenderer(contextLost: false);
                _framebuffer?.Dispose();
                _framebuffer = null;
                _context.Release();
            }
        }

        _context.Dispose();
        _bitmap?.Dispose();
        _bitmap = null;
    }

    public override void RequestFrame()
    {
        if (!_requested && _attached && TopLevel.GetTopLevel(Owner) is { } top)
        {
            _requested = true;
            top.RequestAnimationFrame(_onFrame);
        }
    }

    public override void Draw(DrawingContext context, Size size)
    {
        if (_bitmap is not null)
        {
            context.DrawImage(_bitmap, new Rect(size));
        }
    }

    private void OnFrame(TimeSpan time)
    {
        _requested = false;
        if (!_attached || _framebuffer is null || !Owner.IsEffectivelyVisible)
        {
            return;
        }

        var size = Owner.PixelSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        if (_context.IsLost)
        {
            // A browser may take a WebGL context away (too many contexts, a graphics driver reset): start over with a new one.
            Owner.ReplaceLostPresenter(this);
            return;
        }

        if (_threaded)
        {
            RenderOnThread(size);
            return;
        }

        _context.MakeCurrent();
        try
        {
            _framebuffer.Resize(size.Width, size.Height);
            Owner.RenderFrame((int)_framebuffer.Id, size);
            var length = size.Width * size.Height * 4;
            if (_pixels.Length != length)
            {
                _pixels = new byte[length];
            }

            _framebuffer.Read(_pixels);
        }
        finally
        {
            _context.Release();
        }

        Show(_pixels, size);
    }

    private void Show(byte[] pixels, PixelSize size)
    {
        if (_bitmap is null || size != _size)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormats.Rgba8888, AlphaFormat.Premul);
            _size = size;
        }

        // OpenGL rows run bottom to top; the bitmap's run top to bottom.
        using (var locked = _bitmap.Lock())
        {
            var row = size.Width * 4;
            for (var y = 0; y < size.Height; y++)
            {
                Marshal.Copy(pixels, (size.Height - 1 - y) * row, locked.Address + (y * locked.RowBytes), row);
            }
        }

        Owner.InvalidateVisual();
    }

    private void RenderOnThread(PixelSize size)
    {
        if (_inFlight)
        {
            // One frame at a time crosses to the render thread and back; draw again when it has arrived.
            _again = true;
            return;
        }

        var device = Owner.RenderThreadDevice;
        var framebuffer = _framebuffer;
        if (device is null || framebuffer is null)
        {
            return;
        }

        var length = size.Width * size.Height * 4;
        if (_pixels.Length != length)
        {
            _pixels = new byte[length];
        }

        var pixels = _pixels;
        try
        {
            device.Post(_ => framebuffer.Resize(size.Width, size.Height));
            Owner.RenderFrame((int)framebuffer.Id, size);
            _inFlight = true;
            device.Post(_ =>
            {
                framebuffer.Read(pixels);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Arrived(pixels, size));
            });
            device.Submit();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // The render thread failed (the viewport has reported why): no picture will come back.
            _inFlight = false;
        }
    }

    private void Arrived(byte[] pixels, PixelSize size)
    {
        _inFlight = false;
        if (!_attached)
        {
            return;
        }

        Show(pixels, size);
        if (_again)
        {
            _again = false;
            RequestFrame();
        }
    }
}

/// <summary>An RGBA8 color texture with a depth buffer to render into, made with a handful of raw OpenGL calls (the
/// engine's device takes the framebuffer as its "surface").</summary>
internal sealed unsafe class OffscreenFramebuffer : IDisposable
{
    private readonly delegate* unmanaged[Stdcall]<int, uint*, void> _genTextures;
    private readonly delegate* unmanaged[Stdcall]<int, uint*, void> _deleteTextures;
    private readonly delegate* unmanaged[Stdcall]<int, uint, void> _bindTexture;
    private readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, void*, void> _texImage2D;
    private readonly delegate* unmanaged[Stdcall]<int, int, int, void> _texParameteri;
    private readonly delegate* unmanaged[Stdcall]<int, uint*, void> _genRenderbuffers;
    private readonly delegate* unmanaged[Stdcall]<int, uint*, void> _deleteRenderbuffers;
    private readonly delegate* unmanaged[Stdcall]<int, uint, void> _bindRenderbuffer;
    private readonly delegate* unmanaged[Stdcall]<int, int, int, int, void> _renderbufferStorage;
    private readonly delegate* unmanaged[Stdcall]<int, uint*, void> _genFramebuffers;
    private readonly delegate* unmanaged[Stdcall]<int, uint*, void> _deleteFramebuffers;
    private readonly delegate* unmanaged[Stdcall]<int, uint, void> _bindFramebuffer;
    private readonly delegate* unmanaged[Stdcall]<int, int, int, uint, int, void> _framebufferTexture2D;
    private readonly delegate* unmanaged[Stdcall]<int, int, int, uint, void> _framebufferRenderbuffer;
    private readonly delegate* unmanaged[Stdcall]<int, int, int, int, int, int, void*, void> _readPixels;
    private readonly delegate* unmanaged[Stdcall]<int, int, void> _pixelStorei;
    private uint _texture;
    private uint _depth;
    private uint _framebuffer;
    private int _width;
    private int _height;

    /// <summary>Creates the framebuffer's objects (the context must be current); <see cref="Resize"/> gives them storage.</summary>
    public OffscreenFramebuffer(Func<string, nint> getProcAddress)
    {
        nint L(string name)
        {
            var address = getProcAddress(name);
            return address != 0 ? address : throw new NotSupportedException($"The OpenGL context does not provide {name}.");
        }

        _genTextures = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glGenTextures");
        _deleteTextures = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glDeleteTextures");
        _bindTexture = (delegate* unmanaged[Stdcall]<int, uint, void>)L("glBindTexture");
        _texImage2D = (delegate* unmanaged[Stdcall]<int, int, int, int, int, int, int, int, void*, void>)L("glTexImage2D");
        _texParameteri = (delegate* unmanaged[Stdcall]<int, int, int, void>)L("glTexParameteri");
        _genRenderbuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glGenRenderbuffers");
        _deleteRenderbuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glDeleteRenderbuffers");
        _bindRenderbuffer = (delegate* unmanaged[Stdcall]<int, uint, void>)L("glBindRenderbuffer");
        _renderbufferStorage = (delegate* unmanaged[Stdcall]<int, int, int, int, void>)L("glRenderbufferStorage");
        _genFramebuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glGenFramebuffers");
        _deleteFramebuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)L("glDeleteFramebuffers");
        _bindFramebuffer = (delegate* unmanaged[Stdcall]<int, uint, void>)L("glBindFramebuffer");
        _framebufferTexture2D = (delegate* unmanaged[Stdcall]<int, int, int, uint, int, void>)L("glFramebufferTexture2D");
        _framebufferRenderbuffer = (delegate* unmanaged[Stdcall]<int, int, int, uint, void>)L("glFramebufferRenderbuffer");
        _readPixels = (delegate* unmanaged[Stdcall]<int, int, int, int, int, int, void*, void>)L("glReadPixels");
        _pixelStorei = (delegate* unmanaged[Stdcall]<int, int, void>)L("glPixelStorei");
        Create();
    }

    /// <summary>The OpenGL name of the framebuffer.</summary>
    public uint Id => _framebuffer;

    /// <summary>Gives the attachments storage of a size. The framebuffer keeps its name, so the name can be handed out
    /// before the size is known.</summary>
    public void Resize(int width, int height)
    {
        if (width == _width && height == _height)
        {
            return;
        }

        _width = width;
        _height = height;
        const int Texture2D = 0xDE1, Renderbuffer = 0x8D41;
        _bindTexture(Texture2D, _texture);

        // GL_RGBA8, GL_RGBA, GL_UNSIGNED_BYTE
        _texImage2D(Texture2D, 0, 0x8058, width, height, 0, 0x1908, 0x1401, null);
        _bindTexture(Texture2D, 0);
        _bindRenderbuffer(Renderbuffer, _depth);

        // GL_DEPTH_COMPONENT24
        _renderbufferStorage(Renderbuffer, 0x81A6, width, height);
        _bindRenderbuffer(Renderbuffer, 0);
    }

    private void Create()
    {
        const int Texture2D = 0xDE1, Renderbuffer = 0x8D41, Framebuffer = 0x8D40;
        uint texture = 0, depth = 0, framebuffer = 0;
        _genTextures(1, &texture);
        _bindTexture(Texture2D, texture);

        // One pixel until the first Resize; GL_LINEAR filters so the texture is complete without mipmaps.
        _texImage2D(Texture2D, 0, 0x8058, 1, 1, 0, 0x1908, 0x1401, null);
        _texParameteri(Texture2D, 0x2801, 0x2601);
        _texParameteri(Texture2D, 0x2800, 0x2601);
        _bindTexture(Texture2D, 0);
        _genRenderbuffers(1, &depth);
        _bindRenderbuffer(Renderbuffer, depth);
        _renderbufferStorage(Renderbuffer, 0x81A6, 1, 1);
        _bindRenderbuffer(Renderbuffer, 0);
        _genFramebuffers(1, &framebuffer);
        _bindFramebuffer(Framebuffer, framebuffer);

        // GL_COLOR_ATTACHMENT0, GL_DEPTH_ATTACHMENT
        _framebufferTexture2D(Framebuffer, 0x8CE0, Texture2D, texture, 0);
        _framebufferRenderbuffer(Framebuffer, 0x8D00, Renderbuffer, depth);
        _bindFramebuffer(Framebuffer, 0);
        _texture = texture;
        _depth = depth;
        _framebuffer = framebuffer;
        _width = 1;
        _height = 1;
    }

    /// <summary>Reads the color as RGBA8, rows bottom to top.</summary>
    public void Read(byte[] rgba)
    {
        _bindFramebuffer(0x8D40, _framebuffer);

        // GL_PACK_ALIGNMENT
        _pixelStorei(0xD05, 1);
        fixed (byte* p = rgba)
        {
            _readPixels(0, 0, _width, _height, 0x1908, 0x1401, p);
        }

        _bindFramebuffer(0x8D40, 0);
    }

    private void Release()
    {
        if (_framebuffer == 0)
        {
            return;
        }

        uint framebuffer = _framebuffer, depth = _depth, texture = _texture;
        _deleteFramebuffers(1, &framebuffer);
        _deleteRenderbuffers(1, &depth);
        _deleteTextures(1, &texture);
        _framebuffer = 0;
    }

    public void Dispose() => Release();
}

/// <summary>An OpenGL context that needs no window: CGL on macOS, EGL (ANGLE on Windows, the system's EGL on Linux)
/// elsewhere.</summary>
internal abstract class OffscreenContext : IDisposable
{
    /// <summary>Makes the context current on the calling thread, remembering what was current.</summary>
    public abstract void MakeCurrent();

    /// <summary>Puts back what was current before <see cref="MakeCurrent"/>.</summary>
    public abstract void Release();

    public abstract nint GetProcAddress(string name);

    /// <summary>True when the context is gone for good and its objects with it (a WebGL context the browser took away).</summary>
    public virtual bool IsLost => false;

    public abstract void Dispose();

    private static Func<OffscreenContext?>? s_platformFactory;

    /// <summary>
    /// Creates the context on a platform where this assembly cannot: the browser, which has no OpenGL library to load. Its
    /// context is a WebGL 2 context of the page, created through JavaScript by TKCode.Arc.Graphics3D.Browser
    /// (<c>UseArcGraphics3DBrowser()</c>), which sets this. The factory returns null when no context can be had.
    /// </summary>
    internal static Func<OffscreenContext?>? PlatformFactory
    {
        get => s_platformFactory;
        set
        {
            s_platformFactory = value;
            PlatformFactoryChanged?.Invoke();
        }
    }

    /// <summary>Raised when <see cref="PlatformFactory"/> was set: viewports that found no context try again.</summary>
    internal static event Action? PlatformFactoryChanged;

    public static OffscreenContext? TryCreate()
    {
        if (OperatingSystem.IsBrowser())
        {
            return PlatformFactory?.Invoke();
        }

        try
        {
            // Diagnostic: a directory with ANGLE's libEGL and libGLESv2, to exercise the EGL path on any platform.
            if (AppContext.GetData("TKCode.Arc.Graphics3D.EglDirectory") is string directory && directory.Length > 0)
            {
                return EglOffscreenContext.TryCreateEgl(directory);
            }

            return OperatingSystem.IsMacOS() ? MacOffscreenContext.TryCreateCgl() : EglOffscreenContext.TryCreateEgl(null);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }
}

internal sealed unsafe class MacOffscreenContext : OffscreenContext
{
    private readonly nint _context;
    private nint _previous;

    private MacOffscreenContext(nint context) => _context = context;

    public static MacOffscreenContext? TryCreateCgl()
    {
        var context = MacNative.CreateContext();
        return context == 0 ? null : new MacOffscreenContext(context);
    }

    public override void MakeCurrent()
    {
        _previous = MacNative.GetCurrentContext();
        MacNative.MakeCurrent(_context);
    }

    public override void Release() => MacNative.MakeCurrent(_previous);

    public override nint GetProcAddress(string name) => MacNative.GetProcAddress(name);

    public override void Dispose() => MacNative.DestroyContext(_context);
}

internal sealed unsafe class EglOffscreenContext : OffscreenContext
{
    private readonly nint _egl;
    private readonly nint _gles;
    private readonly nint _display;
    private readonly nint _surface;
    private readonly nint _context;
    private readonly delegate* unmanaged<nint, nint, nint, nint, int> _makeCurrent;
    private readonly delegate* unmanaged<byte*, nint> _getProcAddress;
    private readonly delegate* unmanaged<nint> _getCurrentContext;
    private readonly delegate* unmanaged<int, nint> _getCurrentSurface;
    private readonly delegate* unmanaged<nint> _getCurrentDisplay;
    private nint _previousDisplay;
    private nint _previousDraw;
    private nint _previousRead;
    private nint _previousContext;

    private EglOffscreenContext(nint egl, nint gles, nint display, nint surface, nint context)
    {
        _egl = egl;
        _gles = gles;
        _display = display;
        _surface = surface;
        _context = context;
        _makeCurrent = (delegate* unmanaged<nint, nint, nint, nint, int>)EglExport(egl, "eglMakeCurrent");
        _getProcAddress = (delegate* unmanaged<byte*, nint>)EglExport(egl, "eglGetProcAddress");
        _getCurrentContext = (delegate* unmanaged<nint>)EglExport(egl, "eglGetCurrentContext");
        _getCurrentSurface = (delegate* unmanaged<int, nint>)EglExport(egl, "eglGetCurrentSurface");
        _getCurrentDisplay = (delegate* unmanaged<nint>)EglExport(egl, "eglGetCurrentDisplay");
    }

    public static EglOffscreenContext? TryCreateEgl(string? directory)
    {
        // Each candidate: the library with EGL, the library with OpenGL ES. Avalonia's own ANGLE on Windows has both in one.
        (string Egl, string Gles)[] candidates = directory is not null
            ? [(Path.Combine(directory, LibraryFile("libEGL")), Path.Combine(directory, LibraryFile("libGLESv2")))]
            : OperatingSystem.IsWindows()
                ? [("av_libglesv2.dll", "av_libglesv2.dll"), ("libEGL.dll", "libGLESv2.dll")]
                : [("libEGL.so.1", "libGLESv2.so.2"), ("libEGL.so", "libGLESv2.so")];
        foreach (var (eglName, glesName) in candidates)
        {
            if (!NativeLibrary.TryLoad(eglName, typeof(EglOffscreenContext).Assembly, null, out var egl)
                || !NativeLibrary.TryLoad(glesName, typeof(EglOffscreenContext).Assembly, null, out var gles))
            {
                continue;
            }

            if (Create(egl, gles) is { } context)
            {
                return context;
            }
        }

        return null;
    }

    // The EGL entry points by their standard names, or as ANGLE's libGLESv2 exports them (EGL_GetDisplay for
    // eglGetDisplay): Avalonia ships that library alone on Windows (av_libglesv2.dll), without the libEGL wrapper.
    private static bool TryEglExport(nint library, string name, out nint address) =>
        NativeLibrary.TryGetExport(library, name, out address) || NativeLibrary.TryGetExport(library, string.Concat("EGL_", name.AsSpan(3)), out address);

    private static nint EglExport(nint library, string name) =>
        TryEglExport(library, name, out var address) ? address : throw new EntryPointNotFoundException($"The EGL library has no {name}.");

    private static string LibraryFile(string name) => OperatingSystem.IsWindows() ? name + ".dll" : OperatingSystem.IsMacOS() ? name + ".dylib" : name + ".so";

    private static EglOffscreenContext? Create(nint egl, nint gles)
    {
        if (!TryEglExport(egl, "eglGetDisplay", out var getDisplayAddress))
        {
            return null;
        }

        var getDisplay = (delegate* unmanaged<nint, nint>)getDisplayAddress;
        var initialize = (delegate* unmanaged<nint, int*, int*, int>)EglExport(egl, "eglInitialize");
        var chooseConfig = (delegate* unmanaged<nint, int*, nint*, int, int*, int>)EglExport(egl, "eglChooseConfig");
        var createPbuffer = (delegate* unmanaged<nint, nint, int*, nint>)EglExport(egl, "eglCreatePbufferSurface");
        var bindApi = (delegate* unmanaged<int, int>)EglExport(egl, "eglBindAPI");
        var createContext = (delegate* unmanaged<nint, nint, nint, int*, nint>)EglExport(egl, "eglCreateContext");
        var display = getDisplay(0);
        int major, minor;
        if (display == 0 || initialize(display, &major, &minor) == 0)
        {
            // No display the EGL library can open (Linux without X11 or Wayland access): Mesa's surfaceless platform,
            // EGL_PLATFORM_SURFACELESS_MESA.
            if (!TryEglExport(egl, "eglGetPlatformDisplay", out var getPlatformDisplay))
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

        // EGL_WIDTH, EGL_HEIGHT: the pbuffer is only there to make the context current; drawing goes to a framebuffer.
        var surfaceAttributes = stackalloc int[] { 0x3057, 16, 0x3056, 16, 0x3038 };
        var surface = createPbuffer(display, config, surfaceAttributes);

        // EGL_OPENGL_ES_API; EGL_CONTEXT_CLIENT_VERSION = 3.
        bindApi(0x30A0);
        var contextAttributes = stackalloc int[] { 0x3098, 3, 0x3038 };
        var context = createContext(display, config, 0, contextAttributes);
        return surface == 0 || context == 0 ? null : new EglOffscreenContext(egl, gles, display, surface, context);
    }

    public override void MakeCurrent()
    {
        _previousDisplay = _getCurrentDisplay();

        // EGL_DRAW, EGL_READ
        _previousDraw = _getCurrentSurface(0x3059);
        _previousRead = _getCurrentSurface(0x305A);
        _previousContext = _getCurrentContext();
        if (_makeCurrent(_display, _surface, _surface, _context) == 0)
        {
            throw new InvalidOperationException("The off-screen OpenGL context could not be made current.");
        }
    }

    public override void Release()
    {
        if (_previousContext != 0 && _previousDisplay != 0)
        {
            _makeCurrent(_previousDisplay, _previousDraw, _previousRead, _previousContext);
        }
        else
        {
            _makeCurrent(_display, 0, 0, 0);
        }
    }

    public override nint GetProcAddress(string name)
    {
        if (NativeLibrary.TryGetExport(_gles, name, out var address))
        {
            return address;
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* p = bytes)
        {
            return _getProcAddress(p);
        }
    }

    public override void Dispose()
    {
        ((delegate* unmanaged<nint, nint, int>)EglExport(_egl, "eglDestroyContext"))(_display, _context);
        ((delegate* unmanaged<nint, nint, int>)EglExport(_egl, "eglDestroySurface"))(_display, _surface);
    }
}
