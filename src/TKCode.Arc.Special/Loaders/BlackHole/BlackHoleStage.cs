using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// The surface of a running black hole: the renderer's frames, shown over the (hidden) content while the effect runs.
/// It takes no input and is hidden from accessibility, like Arc's aria-hidden canvas.
/// </summary>
internal sealed class BlackHoleStage : Control
{
    private WriteableBitmap? _bitmap;
    private Rect _target;

    public BlackHoleStage()
    {
        IsHitTestVisible = false;
        IsVisible = false;
        Focusable = false;
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(this, Avalonia.Automation.AccessibilityView.Raw);
    }

    /// <summary>Frames presented since the stage was created (tests).</summary>
    public int Frames { get; private set; }

    /// <summary>Shows the stage over <paramref name="target"/> (DIP, the stage's coordinates) with a bitmap of the given size.</summary>
    public void Begin(PixelSize pixels, Rect target, bool redFirst)
    {
        if (_bitmap is null || _bitmap.PixelSize != pixels)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(pixels, new Vector(96, 96), redFirst ? PixelFormat.Rgba8888 : PixelFormat.Bgra8888, AlphaFormat.Premul);
        }
        _target = target;
        IsVisible = true;
    }

    /// <summary>Copies a frame into the bitmap and repaints.</summary>
    public void Present(uint[] pixels, int width, int height)
    {
        if (_bitmap is null)
        {
            return;
        }
        var rowBytes = width * 4;
        var source = MemoryMarshal.AsBytes(pixels.AsSpan(0, width * height));
        using (var locked = _bitmap.Lock())
        {
            if (locked.RowBytes == rowBytes)
            {
                source.CopyTo(NativeBytes(locked.Address, rowBytes * height));
            }
            else
            {
                for (var y = 0; y < height; y++)
                {
                    source.Slice(y * rowBytes, rowBytes).CopyTo(NativeBytes(locked.Address + y * locked.RowBytes, rowBytes));
                }
            }
        }
        Frames++;
        InvalidateVisual();
    }

    /// <summary>Hides the stage and lets go of the bitmap.</summary>
    public void End()
    {
        IsVisible = false;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    // A span over the bitmap's locked memory, without an unsafe block (the package does not allow them).
    private static Span<byte> NativeBytes(IntPtr address, int length) =>
        MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), address), length);

    public override void Render(DrawingContext context)
    {
        if (_bitmap is not { } bitmap)
        {
            return;
        }
        context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), _target);
    }
}
