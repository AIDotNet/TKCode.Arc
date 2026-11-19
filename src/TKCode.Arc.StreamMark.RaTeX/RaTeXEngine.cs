using System.Runtime.InteropServices;
using System.Text.Json;

namespace StreamMark.RaTeX;

/// <summary>排版结果：成功时带显示列表，失败时带 RaTeX 的错误信息（流式输入时公式未写完属于正常情况）。</summary>
public readonly record struct MathLayoutResult(DisplayList? DisplayList, string? Error)
{
    public bool IsSuccess => DisplayList is not null;
}

/// <summary>RaTeX 排版入口。线程安全：原生库的错误状态是线程局部的。</summary>
public static class RaTeXEngine
{
    /// <summary>以默认颜色（黑色）排版。</summary>
    public static MathLayoutResult Layout(string latex, bool displayMode) => Layout(latex, displayMode, null);

    /// <summary>
    /// 排版 LaTeX。<paramref name="defaultColor"/> 为公式的默认颜色（分量 0..1），null 表示黑色；
    /// 显式的 <c>\color</c> / <c>\textcolor</c> 不受影响。
    /// </summary>
    public static unsafe MathLayoutResult Layout(string latex, bool displayMode, DisplayColor? defaultColor)
    {
        ArgumentNullException.ThrowIfNull(latex);
        RaTeXNative.RatexColor color = default;
        if (defaultColor is { } c)
        {
            color = new RaTeXNative.RatexColor { R = c.R, G = c.G, B = c.B, A = c.A };
        }
        var options = new RaTeXNative.RatexOptions
        {
            StructSize = (nuint)sizeof(RaTeXNative.RatexOptions),
            DisplayMode = displayMode ? 1 : 0,
            Color = defaultColor is null ? null : &color,
        };
        var result = RaTeXNative.ParseAndLayout(latex, &options);
        if (result.ErrorCode != 0 || result.Data is null)
        {
            var error = Marshal.PtrToStringUTF8((nint)RaTeXNative.GetLastError());
            return new MathLayoutResult(null, error ?? $"RaTeX error {result.ErrorCode}");
        }
        try
        {
            var json = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(result.Data);
            var list = JsonSerializer.Deserialize(json, DisplayListJsonContext.Default.DisplayList);
            return list is null ? new MathLayoutResult(null, "Empty display list") : new MathLayoutResult(list, null);
        }
        finally
        {
            RaTeXNative.FreeDisplayList(result.Data);
        }
    }
}
