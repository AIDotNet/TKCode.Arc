using Avalonia;
using Avalonia.Media;

namespace StreamMark.Avalonia.Extensions;

/// <summary>
/// 图表渲染的环境信息。<see cref="Rendering.DiagramBlockView"/> 传入的颜色已经是叠到真实底色上的不透明色；
/// 其它调用方传入的颜色可以带透明度（宿主主题常用半透明的文字 / 填充），渲染器需要不透明色时用 <see cref="Flatten"/>。
/// </summary>
public sealed record DiagramRenderContext(bool IsDark, Color Foreground, Color Background, Color Accent, string? FontFamily, double FontSize)
{
    /// <summary>
    /// 把带透明度的 <paramref name="color"/> 叠到 <paramref name="backdrop"/> 上，得到视觉上等效的不透明色。
    /// 完全透明时就是底色；<paramref name="backdrop"/> 的透明度被忽略。
    /// </summary>
    public static Color Flatten(Color color, Color backdrop)
    {
        if (color.A == byte.MaxValue)
        {
            return color;
        }

        if (color.A == 0)
        {
            return Color.FromRgb(backdrop.R, backdrop.G, backdrop.B);
        }

        var alpha = color.A / 255.0;
        return Color.FromRgb(Mix(color.R, backdrop.R), Mix(color.G, backdrop.G), Mix(color.B, backdrop.B));

        byte Mix(byte top, byte bottom) => (byte)Math.Round(top * alpha + bottom * (1 - alpha));
    }
}

/// <summary>
/// 代码块图表渲染器（如 Mermaid）。代码块闭合后才会调用；<see cref="RenderAsync"/> 可在后台线程执行。
/// </summary>
public interface IDiagramRenderer
{
    /// <summary>是否处理这个围栏语言（如 "mermaid"）。</summary>
    bool CanRender(string? language);

    /// <summary>渲染图表。失败时返回 null 并给出错误信息（界面将回退为显示源码）。</summary>
    Task<DiagramResult?> RenderAsync(string language, string code, DiagramRenderContext context, CancellationToken cancellationToken);
}

/// <summary>渲染好的图表，可按任意尺寸绘制。</summary>
public abstract class DiagramResult : IDisposable
{
    /// <summary>图表的自然尺寸（设备无关像素）。</summary>
    public abstract Size Size { get; }

    /// <summary>错误信息（部分渲染时可用）。</summary>
    public virtual string? Warning => null;

    /// <summary>绘制到目标矩形（保持宽高比由调用方负责）。必须在界面线程调用。</summary>
    public abstract void Draw(DrawingContext context, Rect destination);

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
