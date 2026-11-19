namespace StreamMark.Core.Model;

/// <summary>源码中的行 / 列位置，均从 0 开始。</summary>
public readonly record struct SourceLocation(int Line, int Column);

/// <summary>
/// 源码偏移与行 / 列之间的换算约定：
/// 偏移是 UTF-16 code unit 的下标（即 <see cref="string"/> 的索引，与 <see cref="SourceRange"/> 一致），
/// 所以中文按 1 个单位、代理对（emoji 等）按 2 个单位；<c>\r\n</c> 是一个换行符但占 2 个单位，不做归一化；
/// 换行符有 <c>\n</c>、<c>\r\n</c> 和单独的 <c>\r</c> 三种；列 = 偏移 − 该行起点，同样按 UTF-16 单位。
/// 每次调用都从头扫描文本，需要大量换算时请自行建立行起点表。
/// </summary>
public static class SourceText
{
    /// <summary>
    /// 偏移 → 行 / 列。偏移落在 <c>\r\n</c> 中间时算作该行行尾（列 = 行长度）；
    /// 超出范围的偏移钳到 [0, text.Length]。
    /// </summary>
    public static SourceLocation ToLocation(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var line = 0;
        var lineStart = 0;
        for (var i = 0; i < offset; i++)
        {
            if (!IsBreak(text, i, out var length))
            {
                continue;
            }
            if (i + length > offset)
            {
                // 偏移在 \r 与 \n 之间
                return new SourceLocation(line, i - lineStart);
            }
            line++;
            lineStart = i + length;
            i += length - 1;
        }
        return new SourceLocation(line, offset - lineStart);
    }

    /// <summary>
    /// 行 / 列 → 偏移。行号超过最后一行时返回文本末尾；列超过该行长度（不含换行符）时钳到行尾；负数按 0 处理。
    /// </summary>
    public static int ToOffset(string text, SourceLocation location)
    {
        var line = 0;
        var lineStart = 0;
        var wanted = Math.Max(location.Line, 0);
        for (var i = 0; i < text.Length && line < wanted; i++)
        {
            if (IsBreak(text, i, out var length))
            {
                line++;
                lineStart = i + length;
                i += length - 1;
            }
        }
        if (line < wanted)
        {
            return text.Length;
        }
        var lineEnd = lineStart;
        while (lineEnd < text.Length && !IsBreak(text, lineEnd, out _))
        {
            lineEnd++;
        }
        return lineStart + Math.Min(Math.Max(location.Column, 0), lineEnd - lineStart);
    }

    private static bool IsBreak(string text, int index, out int length)
    {
        length = 0;
        switch (text[index])
        {
            case '\n':
                length = 1;
                return true;
            case '\r':
                length = index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
                return true;
            default:
                return false;
        }
    }
}
