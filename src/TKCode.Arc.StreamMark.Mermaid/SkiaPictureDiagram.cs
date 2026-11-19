using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using StreamMark.Avalonia.Extensions;

namespace StreamMark.Mermaid;

/// <summary>
/// 以 <see cref="SKPicture"/> 保存的矢量图表。<see cref="Draw"/> 通过 <see cref="ICustomDrawOperation"/> +
/// <see cref="ISkiaSharpApiLeaseFeature"/> 直接把图元回放到 Avalonia 的 Skia 画布上，任意缩放都保持清晰。
/// </summary>
/// <remarks>
/// 绘制操作在渲染线程执行，可能晚于 <see cref="Dispose"/>；因此图元由引用计数的 <see cref="PictureHandle"/> 持有，
/// 最后一个持有者（结果本身或仍在场景中的绘制操作）释放时才真正销毁。
/// </remarks>
internal sealed class SkiaPictureDiagram : DiagramResult
{
    private PictureHandle? _handle;

    public SkiaPictureDiagram(SKPicture picture, IDisposable? owner, Size size, string? warning)
    {
        _handle = new PictureHandle(picture, owner);
        Size = size;
        Warning = warning;
    }

    public override Size Size { get; }

    public override string? Warning { get; }

    /// <summary>底层图元（测试 / 导出用）。已释放时为 null。</summary>
    internal SKPicture? Picture => _handle?.Picture;

    public override void Draw(DrawingContext context, Rect destination)
    {
        var handle = _handle;
        if (handle is null || destination.Width <= 0 || destination.Height <= 0 || Size.Width <= 0 || Size.Height <= 0)
        {
            return;
        }

        context.Custom(new PictureDrawOperation(handle.AddRef(), destination, Size));
    }

    public override void Dispose()
    {
        Interlocked.Exchange(ref _handle, null)?.Release();
        base.Dispose();
    }

    internal sealed class PictureHandle
    {
        private int _refs = 1;

        public PictureHandle(SKPicture picture, IDisposable? owner)
        {
            Picture = picture;
            Owner = owner;
        }

        public SKPicture Picture { get; }

        private IDisposable? Owner { get; }

        public PictureHandle AddRef()
        {
            Interlocked.Increment(ref _refs);
            return this;
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _refs) == 0)
            {
                if (Owner is not null)
                {
                    Owner.Dispose(); // SKSvg 拥有并释放其 Picture
                }
                else
                {
                    Picture.Dispose();
                }
            }
        }
    }

    private sealed class PictureDrawOperation : ICustomDrawOperation
    {
        private PictureHandle? _handle;
        private readonly Size _natural;

        public PictureDrawOperation(PictureHandle handle, Rect destination, Size natural)
        {
            _handle = handle;
            Bounds = destination;
            _natural = natural;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) =>
            other is PictureDrawOperation op && ReferenceEquals(op._handle, _handle) && op.Bounds == Bounds;

        public void Render(ImmediateDrawingContext context)
        {
            var handle = _handle;
            if (handle is null || context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature)
            {
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            var count = canvas.Save();
            try
            {
                canvas.ClipRect(new SKRect((float)Bounds.X, (float)Bounds.Y, (float)Bounds.Right, (float)Bounds.Bottom));
                canvas.Translate((float)Bounds.X, (float)Bounds.Y);
                canvas.Scale((float)(Bounds.Width / _natural.Width), (float)(Bounds.Height / _natural.Height));
                var opacity = lease.CurrentOpacity;
                if (opacity < 1)
                {
                    using var paint = new SKPaint { Color = new SKColor(0, 0, 0, (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255)) };
                    canvas.DrawPicture(handle.Picture, paint);
                }
                else
                {
                    canvas.DrawPicture(handle.Picture);
                }
            }
            finally
            {
                canvas.RestoreToCount(count);
            }
        }

        public void Dispose() => Interlocked.Exchange(ref _handle, null)?.Release();
    }
}
