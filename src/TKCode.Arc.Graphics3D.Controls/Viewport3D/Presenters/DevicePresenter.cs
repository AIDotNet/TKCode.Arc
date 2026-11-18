using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc;

/// <summary>
/// Presents with a graphics device the application supplies (<see cref="Viewport3D.DeviceFactory"/>), for example the
/// WebGPU backend: the device draws into an off-screen surface of its own, the pixels are read back and drawn as a
/// bitmap. Nothing is shared with Avalonia's renderer, so it works with any device and any rendering mode, at the price
/// of one read-back per frame.
/// </summary>
internal sealed class DevicePresenter : ViewportPresenter
{
    private readonly Func<GraphicsDevice> _factory;
    private readonly Action<TimeSpan> _onFrame;
    private GraphicsDevice? _device;
    private WriteableBitmap? _bitmap;
    private byte[] _pixels = [];
    private PixelSize _size;
    private bool _requested;
    private bool _attached;
    private bool _threaded;
    private bool _inFlight;
    private bool _again;

    public DevicePresenter(Viewport3D owner, Func<GraphicsDevice> factory)
        : base(owner)
    {
        _factory = factory;
        _onFrame = OnFrame;
    }

    public override void Attach()
    {
        bool created;
        if (Owner.UseRenderThread && !OperatingSystem.IsBrowser())
        {
            created = Owner.InitRendererOnThread(_factory, static () => { });
            _threaded = created;
        }
        else
        {
            _device = _factory() ?? throw new InvalidOperationException("The device factory returned no device.");
            created = Owner.InitRenderer(_device);
        }

        if (!created)
        {
            _device?.Dispose();
            _device = null;
            throw new NotSupportedException(Owner.RendererError ?? "The renderer could not be created on the supplied device.");
        }

        _attached = true;
    }

    public override void Detach()
    {
        if (_attached)
        {
            // The viewport disposes the renderer and, with it, the device it was given.
            _attached = false;
            Owner.DisposeRenderer(contextLost: false);
            _device = null;
        }

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
        if (!_attached || !Owner.IsEffectivelyVisible)
        {
            return;
        }

        var size = Owner.PixelSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        if (_inFlight)
        {
            // One frame at a time crosses to the render thread and back; draw again when it has arrived.
            _again = true;
            return;
        }

        var length = size.Width * size.Height * 4;
        if (_pixels.Length != length)
        {
            _pixels = new byte[length];
        }

        var pixels = _pixels;

        // Surface 0 is the off-screen surface the device keeps for itself.
        Owner.RenderFrame(0, size);
        if (Owner.RendererError is not null)
        {
            return;
        }

        try
        {
            if (_threaded && Owner.RenderThreadDevice is { } threaded)
            {
                _inFlight = true;
                threaded.Post(device =>
                {
                    device.ReadPixels(null, 0, 0, size.Width, size.Height, pixels);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Arrived(pixels, size));
                });
                threaded.Submit();
            }
            else if (_device is { } device)
            {
                device.ReadPixels(null, 0, 0, size.Width, size.Height, pixels);
                Show(pixels, size);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or NotSupportedException)
        {
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

    private void Show(byte[] pixels, PixelSize size)
    {
        if (_bitmap is null || size != _size)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormats.Rgba8888, AlphaFormat.Premul);
            _size = size;
        }

        // Devices hand rows over bottom to top (the engine's convention); the bitmap's run top to bottom.
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
}
