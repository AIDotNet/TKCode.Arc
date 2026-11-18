using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;

namespace TKCode.Arc;

/// <summary>
/// The macOS presenter: an OpenGL 4.1 core context of the viewport's own (CGL) renders into IOSurface-backed textures, and
/// the compositor imports those surfaces (<see cref="ICompositionGpuInterop"/>). This works under Avalonia's default Metal
/// renderer, where <c>OpenGlControlBase</c> has no context to share, and under its OpenGL renderer alike.
/// </summary>
/// <remarks>
/// A small pool of surfaces rotates: the compositor reads a surface some time after it was handed over, so the next frame
/// is drawn into another one. Under Metal each surface has its own <c>MTLSharedEvent</c> timeline: the viewport signals the
/// "written" value after flushing its drawing, the compositor signals the "read" value when it copied the picture, and only
/// then is the surface drawn into again.
/// </remarks>
internal sealed class MacSurfacePresenter : ViewportPresenter
{
    private const int MaxImages = 3;
    private const int MaxPending = 2;

    private readonly Compositor _compositor;
    private readonly ICompositionGpuInterop _interop;
    private readonly bool _timeline;
    private readonly List<SwapImage> _images = [];
    private readonly Action<TimeSpan> _onFrame;
    private CompositionDrawingSurface? _surface;
    private CompositionSurfaceVisual? _visual;
    private nint _context;

    // True when the context lives on the viewport's render thread: OpenGL is then only called through that device.
    private bool _threaded;
    private nint _metalDevice;
    private PixelSize _size;
    private bool _requested;
    private bool _retry;
    private bool _attached;
    private int _pending;

    private sealed class SwapImage
    {
        public nint Surface;
        public uint Texture;
        public uint Depth;
        public uint Framebuffer;
        public nint Event;
        public ulong Value;
        public bool InFlight;
        public ICompositionImportedGpuImage? Imported;
        public ICompositionImportedGpuSemaphore? Semaphore;
    }

    private MacSurfacePresenter(Viewport3D owner, Compositor compositor, ICompositionGpuInterop interop, bool timeline)
        : base(owner)
    {
        _compositor = compositor;
        _interop = interop;
        _timeline = timeline;
        _onFrame = OnFrame;
    }

    /// <summary>Returns a presenter when the compositor imports IOSurfaces in a way the viewport can synchronise; else null.</summary>
    public static MacSurfacePresenter? TryCreate(Viewport3D owner, Compositor compositor, ICompositionGpuInterop interop)
    {
        if (interop.IsLost || !interop.SupportedImageHandleTypes.Contains(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef))
        {
            return null;
        }

        var capabilities = interop.GetSynchronizationCapabilities(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef);
        if ((capabilities & CompositionGpuImportedImageSynchronizationCapabilities.Automatic) != 0)
        {
            return new MacSurfacePresenter(owner, compositor, interop, timeline: false);
        }

        if ((capabilities & CompositionGpuImportedImageSynchronizationCapabilities.TimelineSemaphores) != 0
            && interop.SupportedSemaphoreTypes.Contains(KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent))
        {
            return new MacSurfacePresenter(owner, compositor, interop, timeline: true);
        }

        return null;
    }

    public override void Attach()
    {
        _context = MacNative.CreateContext();
        if (_context == 0)
        {
            throw new NotSupportedException("An OpenGL 4.1 core context could not be created.");
        }

        if (_timeline)
        {
            _metalDevice = MacNative.CreateMetalDevice();
            if (_metalDevice == 0)
            {
                MacNative.DestroyContext(_context);
                _context = 0;
                throw new NotSupportedException("No Metal device is available for synchronising with the compositor.");
            }
        }

        // A render thread needs the timeline semaphores: they let the compositor wait for a picture that is still being
        // drawn elsewhere.
        _threaded = Owner.UseRenderThread && _timeline;
        bool created;
        if (_threaded)
        {
            var context = _context;
            created = Owner.InitRendererOnThread(
                () =>
                {
                    MacNative.MakeCurrent(context);
                    return new Graphics3D.Rhi.OpenGL.GlDevice(MacNative.GetProcAddress);
                },
                static () => MacNative.MakeCurrent(0));
        }
        else
        {
            MacNative.MakeCurrent(_context);
            created = Owner.InitRenderer(MacNative.GetProcAddress);
        }

        if (!created)
        {
            MacNative.MakeCurrent(0);
            MacNative.DestroyContext(_context);
            _context = 0;
            _threaded = false;
            throw new NotSupportedException(Owner.RendererError ?? "The OpenGL renderer could not be created.");
        }

        _surface = _compositor.CreateDrawingSurface();
        _visual = _compositor.CreateSurfaceVisual();
        _visual.Surface = _surface;
        ElementComposition.SetElementChildVisual(Owner, _visual);
        _attached = true;
    }

    public override void Detach()
    {
        if (!_attached && _context == 0)
        {
            return;
        }

        _attached = false;
        if (_context != 0)
        {
            if (!_threaded)
            {
                MacNative.MakeCurrent(_context);
            }

            foreach (var image in _images)
            {
                Release(image);
            }

            _images.Clear();
            Owner.DisposeRenderer(contextLost: false);
            MacNative.MakeCurrent(0);
            MacNative.DestroyContext(_context);
            _context = 0;
            _threaded = false;
        }

        if (_metalDevice != 0)
        {
            MacNative.ReleaseObject(_metalDevice);
            _metalDevice = 0;
        }

        ElementComposition.SetElementChildVisual(Owner, null);
        _visual = null;
        _surface?.Dispose();
        _surface = null;
    }

    public override void RequestFrame()
    {
        if (!_requested && _attached && TopLevel.GetTopLevel(Owner) is { } top)
        {
            _requested = true;
            top.RequestAnimationFrame(_onFrame);
        }
    }

    public override void Arrange(Size finalSize)
    {
        if (_visual is not null)
        {
            _visual.Size = new Vector(finalSize.Width, finalSize.Height);
        }
    }

    private void OnFrame(TimeSpan time)
    {
        _requested = false;
        if (!_attached || _surface is null || _interop.IsLost || !Owner.IsEffectivelyVisible)
        {
            return;
        }

        var size = Owner.PixelSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        if (_pending >= MaxPending)
        {
            // The compositor has not taken the previous pictures yet: draw when one of them is done.
            _retry = true;
            return;
        }

        if (!_threaded)
        {
            MacNative.MakeCurrent(_context);
        }

        if (size != _size)
        {
            if (_pending > 0)
            {
                // Surfaces of the old size are still being read.
                _retry = true;
                return;
            }

            foreach (var old in _images)
            {
                Release(old);
            }

            _images.Clear();
            _size = size;
        }

        var image = Acquire();
        if (image is null)
        {
            _retry = true;
            return;
        }

        Owner.RenderFrame((int)image.Framebuffer, size);
        Task update;
        if (_threaded)
        {
            // The frame is only recorded so far. The compositor waits on the semaphore, which the render thread
            // signals once the picture is really in the surface.
            var wait = ++image.Value;
            var signal = ++image.Value;
            var shared = image.Event;
            try
            {
                var device = Owner.RenderThreadDevice!;
                device.Post(_ =>
                {
                    MacNative.Flush();
                    MacNative.SetSignaledValue(shared, wait);
                });
                device.Submit();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                // The render thread failed (the viewport has reported why): let the compositor go on without this picture.
                MacNative.SetSignaledValue(shared, wait);
            }

            update = _surface.UpdateWithTimelineSemaphoresAsync(image.Imported!, image.Semaphore!, wait, image.Semaphore!, signal);
        }
        else if (_timeline)
        {
            MacNative.Flush();
            var wait = ++image.Value;
            var signal = ++image.Value;
            MacNative.SetSignaledValue(image.Event, wait);
            update = _surface.UpdateWithTimelineSemaphoresAsync(image.Imported!, image.Semaphore!, wait, image.Semaphore!, signal);
        }
        else
        {
            MacNative.Flush();
            update = _surface.UpdateAsync(image.Imported!);
        }

        image.InFlight = true;
        _pending++;
        Observe(update, image);
    }

    private async void Observe(Task update, SwapImage image)
    {
        try
        {
            await update;
        }
        catch (Exception)
        {
            // The compositor went away or lost its device; the next attach starts over.
        }

        image.InFlight = false;
        _pending = Math.Max(0, _pending - 1);
        if (_retry && _attached)
        {
            _retry = false;
            RequestFrame();
        }
    }

    private SwapImage? Acquire()
    {
        foreach (var image in _images)
        {
            if (!image.InFlight && (!_timeline || MacNative.GetSignaledValue(image.Event) >= image.Value))
            {
                return image;
            }
        }

        return _images.Count < MaxImages ? Create() : null;
    }

    private SwapImage Create()
    {
        var image = new SwapImage { Surface = MacNative.CreateSurface(_size.Width, _size.Height) };
        if (image.Surface == 0)
        {
            throw new InvalidOperationException("An IOSurface could not be created.");
        }

        if (_threaded)
        {
            // Rare (a new size or a third image): wait for the render thread to wrap the surface in a framebuffer.
            var context = _context;
            var size = _size;
            Owner.RenderThreadDevice!.Invoke(_ =>
                MacNative.CreateFramebuffer(context, image.Surface, size.Width, size.Height, out image.Texture, out image.Depth, out image.Framebuffer));
        }
        else
        {
            MacNative.CreateFramebuffer(_context, image.Surface, _size.Width, _size.Height, out image.Texture, out image.Depth, out image.Framebuffer);
        }

        image.Imported = _interop.ImportImage(
            new PlatformHandle(image.Surface, KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef),
            new PlatformGraphicsExternalImageProperties
            {
                Width = _size.Width,
                Height = _size.Height,
                Format = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm,

                // OpenGL draws with the first row at the bottom.
                TopLeftOrigin = false,
            });
        if (_timeline)
        {
            image.Event = MacNative.CreateSharedEvent(_metalDevice);
            image.Semaphore = _interop.ImportSemaphore(new PlatformHandle(image.Event, KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent));
        }

        _images.Add(image);
        return image;
    }

    private void Release(SwapImage image)
    {
        if (_threaded)
        {
            try
            {
                Owner.RenderThreadDevice?.Invoke(_ => MacNative.DeleteFramebuffer(image.Texture, image.Depth, image.Framebuffer));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                // The render thread is gone, and its context's objects with it.
            }
        }
        else
        {
            MacNative.DeleteFramebuffer(image.Texture, image.Depth, image.Framebuffer);
        }

        _ = image.Imported?.DisposeAsync();
        _ = image.Semaphore?.DisposeAsync();
        if (image.Event != 0)
        {
            MacNative.ReleaseObject(image.Event);
        }

        if (image.Surface != 0)
        {
            MacNative.Release(image.Surface);
        }
    }
}

/// <summary>The few entry points of CGL, OpenGL, IOSurface, CoreFoundation, Metal and the Objective-C runtime that the
/// macOS presenter needs, called through function pointers (no marshalling stubs, no reflection).</summary>
internal static unsafe class MacNative
{
    private const int TextureRectangle = 0x84F5;
    private const int Framebuffer = 0x8D40;
    private const int Renderbuffer = 0x8D41;

    private static readonly nint s_gl = NativeLibrary.Load("/System/Library/Frameworks/OpenGL.framework/OpenGL");
    private static readonly nint s_coreFoundation = NativeLibrary.Load("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");
    private static readonly nint s_ioSurface = NativeLibrary.Load("/System/Library/Frameworks/IOSurface.framework/IOSurface");
    private static readonly nint s_metal = NativeLibrary.Load("/System/Library/Frameworks/Metal.framework/Metal");
    private static readonly nint s_objc = NativeLibrary.Load("/usr/lib/libobjc.A.dylib");
    private static readonly nint s_msgSend = NativeLibrary.GetExport(s_objc, "objc_msgSend");
    private static readonly delegate* unmanaged<byte*, nint> s_selRegisterName =
        (delegate* unmanaged<byte*, nint>)NativeLibrary.GetExport(s_objc, "sel_registerName");

    private static readonly nint s_newSharedEvent = Selector("newSharedEvent"u8);
    private static readonly nint s_setSignaledValue = Selector("setSignaledValue:"u8);
    private static readonly nint s_signaledValue = Selector("signaledValue"u8);
    private static readonly nint s_release = Selector("release"u8);

    private static nint Selector(ReadOnlySpan<byte> name)
    {
        Span<byte> z = stackalloc byte[name.Length + 1];
        name.CopyTo(z);
        fixed (byte* p = z)
        {
            return s_selRegisterName(p);
        }
    }

    private static nint Gl(string name) => NativeLibrary.GetExport(s_gl, name);

    public static nint GetProcAddress(string name) => NativeLibrary.TryGetExport(s_gl, name, out var address) ? address : 0;

    // ---- CGL ----

    public static nint CreateContext()
    {
        var choose = (delegate* unmanaged<int*, nint*, int*, int>)Gl("CGLChoosePixelFormat");
        var create = (delegate* unmanaged<nint, nint, nint*, int>)Gl("CGLCreateContext");
        var destroyFormat = (delegate* unmanaged<nint, int>)Gl("CGLDestroyPixelFormat");

        // kCGLPFAOpenGLProfile (99) = 4.1 core (0x4100), kCGLPFAColorSize (8) = 24, kCGLPFAAllowOfflineRenderers (96),
        // kCGLPFASupportsAutomaticGraphicsSwitching (101): do not force the discrete GPU on dual-GPU machines.
        var attributes = stackalloc int[] { 99, 0x4100, 8, 24, 96, 101, 0 };
        nint format;
        int count;
        if (choose(attributes, &format, &count) != 0 || format == 0)
        {
            return 0;
        }

        nint context;
        var error = create(format, 0, &context);
        destroyFormat(format);
        return error == 0 ? context : 0;
    }

    public static void MakeCurrent(nint context) => ((delegate* unmanaged<nint, int>)Gl("CGLSetCurrentContext"))(context);

    public static nint GetCurrentContext() => ((delegate* unmanaged<nint>)Gl("CGLGetCurrentContext"))();

    public static void DestroyContext(nint context) => ((delegate* unmanaged<nint, int>)Gl("CGLDestroyContext"))(context);

    public static void Flush() => ((delegate* unmanaged<void>)Gl("glFlush"))();

    // ---- IOSurface ----

    public static nint CreateSurface(int width, int height)
    {
        var createDictionary = (delegate* unmanaged<nint, nint, nint, nint, nint>)NativeLibrary.GetExport(s_coreFoundation, "CFDictionaryCreateMutable");
        var createSurface = (delegate* unmanaged<nint, nint>)NativeLibrary.GetExport(s_ioSurface, "IOSurfaceCreate");
        var dictionary = createDictionary(
            0, 0,
            NativeLibrary.GetExport(s_coreFoundation, "kCFTypeDictionaryKeyCallBacks"),
            NativeLibrary.GetExport(s_coreFoundation, "kCFTypeDictionaryValueCallBacks"));
        SetNumber(dictionary, "kIOSurfaceWidth", width);
        SetNumber(dictionary, "kIOSurfaceHeight", height);
        SetNumber(dictionary, "kIOSurfaceBytesPerElement", 4);

        // 'BGRA'
        SetNumber(dictionary, "kIOSurfacePixelFormat", 0x42475241);
        var surface = createSurface(dictionary);
        Release(dictionary);
        return surface;
    }

    private static void SetNumber(nint dictionary, string key, int value)
    {
        var setValue = (delegate* unmanaged<nint, nint, nint, void>)NativeLibrary.GetExport(s_coreFoundation, "CFDictionarySetValue");
        var createNumber = (delegate* unmanaged<nint, nint, int*, nint>)NativeLibrary.GetExport(s_coreFoundation, "CFNumberCreate");
        const nint SInt32Type = 3;
        var number = createNumber(0, SInt32Type, &value);
        setValue(dictionary, Marshal.ReadIntPtr(NativeLibrary.GetExport(s_ioSurface, key)), number);
        Release(number);
    }

    public static void Release(nint coreFoundationObject) =>
        ((delegate* unmanaged<nint, void>)NativeLibrary.GetExport(s_coreFoundation, "CFRelease"))(coreFoundationObject);

    /// <summary>A framebuffer whose color attachment is a rectangle texture backed by the surface.</summary>
    public static void CreateFramebuffer(nint context, nint surface, int width, int height, out uint texture, out uint depth, out uint framebuffer)
    {
        var genTextures = (delegate* unmanaged<int, uint*, void>)Gl("glGenTextures");
        var bindTexture = (delegate* unmanaged<int, uint, void>)Gl("glBindTexture");
        var texImage = (delegate* unmanaged<nint, int, int, int, int, int, int, nint, uint, int>)Gl("CGLTexImageIOSurface2D");
        var genFramebuffers = (delegate* unmanaged<int, uint*, void>)Gl("glGenFramebuffers");
        var bindFramebuffer = (delegate* unmanaged<int, uint, void>)Gl("glBindFramebuffer");
        var framebufferTexture = (delegate* unmanaged<int, int, int, uint, int, void>)Gl("glFramebufferTexture2D");
        var genRenderbuffers = (delegate* unmanaged<int, uint*, void>)Gl("glGenRenderbuffers");
        var bindRenderbuffer = (delegate* unmanaged<int, uint, void>)Gl("glBindRenderbuffer");
        var renderbufferStorage = (delegate* unmanaged<int, int, int, int, void>)Gl("glRenderbufferStorage");
        var framebufferRenderbuffer = (delegate* unmanaged<int, int, int, uint, void>)Gl("glFramebufferRenderbuffer");
        var checkStatus = (delegate* unmanaged<int, int>)Gl("glCheckFramebufferStatus");
        uint t = 0, d = 0, f = 0;
        genTextures(1, &t);
        bindTexture(TextureRectangle, t);

        // GL_RGBA8 storage read as GL_BGRA / GL_UNSIGNED_INT_8_8_8_8_REV, the layout of a 'BGRA' surface.
        var error = texImage(context, TextureRectangle, 0x8058, width, height, 0x80E1, 0x8367, surface, 0);
        bindTexture(TextureRectangle, 0);
        if (error != 0)
        {
            ((delegate* unmanaged<int, uint*, void>)Gl("glDeleteTextures"))(1, &t);
            throw new InvalidOperationException($"CGLTexImageIOSurface2D failed ({error}).");
        }

        genRenderbuffers(1, &d);
        bindRenderbuffer(Renderbuffer, d);

        // GL_DEPTH_COMPONENT24
        renderbufferStorage(Renderbuffer, 0x81A6, width, height);
        genFramebuffers(1, &f);
        bindFramebuffer(Framebuffer, f);

        // GL_COLOR_ATTACHMENT0, GL_DEPTH_ATTACHMENT
        framebufferTexture(Framebuffer, 0x8CE0, TextureRectangle, t, 0);
        framebufferRenderbuffer(Framebuffer, 0x8D00, Renderbuffer, d);
        var status = checkStatus(Framebuffer);
        bindFramebuffer(Framebuffer, 0);
        texture = t;
        depth = d;
        framebuffer = f;

        // GL_FRAMEBUFFER_COMPLETE
        if (status != 0x8CD5)
        {
            DeleteFramebuffer(t, d, f);
            throw new InvalidOperationException($"The surface framebuffer is incomplete (0x{status:X}).");
        }
    }

    public static void DeleteFramebuffer(uint texture, uint depth, uint framebuffer)
    {
        ((delegate* unmanaged<int, uint*, void>)Gl("glDeleteFramebuffers"))(1, &framebuffer);
        ((delegate* unmanaged<int, uint*, void>)Gl("glDeleteRenderbuffers"))(1, &depth);
        ((delegate* unmanaged<int, uint*, void>)Gl("glDeleteTextures"))(1, &texture);
    }

    // ---- Metal shared events ----

    public static nint CreateMetalDevice() =>
        ((delegate* unmanaged<nint>)NativeLibrary.GetExport(s_metal, "MTLCreateSystemDefaultDevice"))();

    public static nint CreateSharedEvent(nint device) => ((delegate* unmanaged<nint, nint, nint>)s_msgSend)(device, s_newSharedEvent);

    public static void SetSignaledValue(nint sharedEvent, ulong value) =>
        ((delegate* unmanaged<nint, nint, ulong, void>)s_msgSend)(sharedEvent, s_setSignaledValue, value);

    public static ulong GetSignaledValue(nint sharedEvent) => ((delegate* unmanaged<nint, nint, ulong>)s_msgSend)(sharedEvent, s_signaledValue);

    public static void ReleaseObject(nint objcObject) => ((delegate* unmanaged<nint, nint, void>)s_msgSend)(objcObject, s_release);
}
