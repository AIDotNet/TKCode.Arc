using Avalonia;
using Avalonia.Media;

namespace StreamMark.Avalonia.Extensions;

/// <summary>
/// 数学公式排版器。<see cref="Layout"/> 必须线程安全（渲染层可能在后台线程预排版）。
/// </summary>
public interface IMathRenderer
{
    /// <summary>
    /// 排版 LaTeX。<paramref name="display"/> 为 true 表示块级（<c>$$…$$</c>、<c>\[…\]</c>）。
    /// 无法排版时（流式中公式未写完、语法错误）返回 null 并给出错误信息。
    /// </summary>
    MathLayout? Layout(string latex, bool display, out string? error);
}

/// <summary>
/// 与字号无关的公式排版结果（以 em 为单位），可在任意字号、颜色下绘制。不可变、可跨线程共享。
/// </summary>
public abstract class MathLayout
{
    /// <summary>宽度（em）。</summary>
    public abstract double Width { get; }

    /// <summary>基线以上高度（em）。</summary>
    public abstract double Ascent { get; }

    /// <summary>基线以下深度（em）。</summary>
    public abstract double Descent { get; }

    /// <summary>
    /// 在 <paramref name="origin"/>（排版框左上角）绘制。必须在界面线程调用。
    /// <paramref name="foreground"/> 为默认颜色；公式中显式的 <c>\color</c> 应保留。
    /// </summary>
    public abstract void Draw(DrawingContext context, Point origin, double fontSize, IBrush foreground);
}
