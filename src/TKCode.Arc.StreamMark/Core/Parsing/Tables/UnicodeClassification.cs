using System.Globalization;
using System.Text;

namespace StreamMark.Core.Parsing;

/// <summary>
/// CommonMark 0.31.2、GFM 与 CJK 友好强调所需的字符分类、标签规范化与 GitHub 标题 Slug 工具。
/// 全程无反射、零 Regex、NativeAOT 安全。
/// </summary>
internal static class UnicodeClassification
{
    public static bool IsAsciiPunctuation(char c) =>
        c is (>= '!' and <= '/') or (>= ':' and <= '@') or (>= '[' and <= '`') or (>= '{' and <= '~');

    public static bool IsUnicodeWhitespace(int codePoint)
    {
        if (codePoint <= 0x7F)
        {
            return codePoint is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';
        }
        if (codePoint == 0x00A0)
        {
            return true;
        }
        if (codePoint <= 0xFFFF)
        {
            return CharUnicodeInfo.GetUnicodeCategory((char)codePoint) == UnicodeCategory.SpaceSeparator;
        }
        return Rune.GetUnicodeCategory(new Rune(codePoint)) == UnicodeCategory.SpaceSeparator;
    }

    public static bool IsUnicodePunctuation(int codePoint)
    {
        if (codePoint <= 0x7F)
        {
            return IsAsciiPunctuation((char)codePoint);
        }
        var cat = codePoint <= 0xFFFF
            ? CharUnicodeInfo.GetUnicodeCategory((char)codePoint)
            : Rune.GetUnicodeCategory(new Rune(codePoint));
        return cat is UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.DashPunctuation
            or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.OtherPunctuation
            or UnicodeCategory.CurrencySymbol
            or UnicodeCategory.MathSymbol
            or UnicodeCategory.ModifierSymbol
            or UnicodeCategory.OtherSymbol;
    }

    /// <summary>是否为 CJK 宽字符（用于软换行不插空格及 CJK 友好强调判定）。</summary>
    public static bool IsCjkWide(char c) =>
        c is (>= '\u2E80' and <= '\u9FFF')
            or (>= '\uAC00' and <= '\uD7AF')
            or (>= '\uF900' and <= '\uFAFF')
            or (>= '\uFF00' and <= '\uFFEF');

    /// <summary>CJK 友好强调中的东亚标点 / 字符判定（参考 remark-cjk-friendly / Markdig CjkFriendlyEmphasis）。</summary>
    public static bool IsCjkOrFullwidth(int codePoint) =>
        codePoint is (>= 0x1100 and <= 0x11FF)
            or (>= 0x2E80 and <= 0x9FFF)
            or (>= 0xA960 and <= 0xA97F)
            or (>= 0xAC00 and <= 0xD7FF)
            or (>= 0xF900 and <= 0xFAFF)
            or (>= 0xFE10 and <= 0xFE6F)
            or (>= 0xFF00 and <= 0xFFEF)
            or (>= 0x20000 and <= 0x3FFFF);

    /// <summary>CJK 友好强调中的“非 CJK 标点”判断（CJK 标点紧邻强调符号时允许正常开闭）。</summary>
    public static bool IsCjkPunctuation(int codePoint) =>
        codePoint is (>= 0x3000 and <= 0x303F)
            or (>= 0xFE10 and <= 0xFE1F)
            or (>= 0xFE30 and <= 0xFE6F)
            or (>= 0xFF01 and <= 0xFF0F)
            or (>= 0xFF1A and <= 0xFF20)
            or (>= 0xFF3B and <= 0xFF40)
            or (>= 0xFF5B and <= 0xFF65)
            or 0x2018 or 0x2019 or 0x201C or 0x201D or 0x2026 or 0x2014;

    /// <summary>是否为需要中断 GFM 裸自动链接的 CJK / 全角字符。</summary>
    public static bool StopsAutoLink(char c) =>
        c <= 0x20 || c >= 0x7F && (IsCjkWide(c) || IsCjkPunctuation(c));

    /// <summary>读取位置 <paramref name="index"/> 之前的 Unicode 码点；开头返回空格。</summary>
    public static int CodePointBefore(ReadOnlySpan<char> text, int index)
    {
        if (index <= 0)
        {
            return ' ';
        }
        var c = text[index - 1];
        if (char.IsLowSurrogate(c) && index >= 2 && char.IsHighSurrogate(text[index - 2]))
        {
            return char.ConvertToUtf32(text[index - 2], c);
        }
        return c;
    }

    /// <summary>读取位置 <paramref name="index"/> 处的 Unicode 码点；末尾返回空格。</summary>
    public static int CodePointAt(ReadOnlySpan<char> text, int index)
    {
        if (index >= text.Length)
        {
            return ' ';
        }
        var c = text[index];
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            return char.ConvertToUtf32(c, text[index + 1]);
        }
        return c;
    }

    /// <summary>CommonMark 链接引用标签规范化：去首尾空白、连续空白折叠为单空格、Unicode 大小写折叠。</summary>
    public static string NormalizeLabel(ReadOnlySpan<char> raw)
    {
        raw = raw.Trim();
        if (raw.IsEmpty)
        {
            return string.Empty;
        }
        var sb = new StringBuilder(raw.Length);
        var inSpace = false;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c is ' ' or '\t' or '\r' or '\n')
            {
                if (!inSpace)
                {
                    sb.Append(' ');
                    inSpace = true;
                }
                continue;
            }
            inSpace = false;
            // 德语 ß 按 CommonMark 折叠为 ss
            if (c == 'ß')
            {
                sb.Append("ss");
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    /// <summary>去除反斜杠转义并解码 HTML 实体（用于链接 URL、标题、代码围栏 info 串）。</summary>
    public static string UnescapeAndDecode(ReadOnlySpan<char> raw)
    {
        if (raw.IsEmpty || raw.IndexOfAny('\\', '&') < 0)
        {
            return raw.IsEmpty ? string.Empty : raw.ToString();
        }
        var sb = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '\\' && i + 1 < raw.Length && IsAsciiPunctuation(raw[i + 1]))
            {
                sb.Append(raw[i + 1]);
                i++;
                continue;
            }
            if (c == '&' && HtmlEntityTable.TryDecode(raw[i..], out var decoded, out var consumed))
            {
                sb.Append(decoded);
                i += consumed - 1;
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>GitHub 风格标题自动 Slug（与 Markdig AutoIdentifierOptions.GitHub 对齐）。</summary>
    public static string ToGitHubSlug(ReadOnlySpan<char> plainText)
    {
        if (plainText.IsEmpty)
        {
            return string.Empty;
        }
        var sb = new StringBuilder(plainText.Length);
        for (var i = 0; i < plainText.Length; i++)
        {
            var c = plainText[i];
            if (c is ' ' or '-')
            {
                sb.Append('-');
            }
            else if (c == '_')
            {
                sb.Append('_');
            }
            else if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        var slug = sb.ToString().Trim('-');
        return slug;
    }

    /// <summary>按 4 列制表位计算行首缩进列数，返回缩进列数及首个非空白字符索引。</summary>
    public static int MeasureIndent(ReadOnlySpan<char> line, int startIndex, int startColumn, out int firstNonSpace)
    {
        var col = startColumn;
        var i = startIndex;
        while (i < line.Length)
        {
            var c = line[i];
            if (c == ' ')
            {
                col++;
                i++;
            }
            else if (c == '\t')
            {
                col += 4 - (col % 4);
                i++;
            }
            else
            {
                break;
            }
        }
        firstNonSpace = i;
        return col - startColumn;
    }

    /// <summary>前进指定列数（支持部分消费 Tab，返回剩余需补齐的虚拟空格数）。</summary>
    public static void AdvanceColumns(ReadOnlySpan<char> line, ref int index, ref int column, int columnsToAdvance, out int virtualSpaces)
    {
        virtualSpaces = 0;
        var target = column + columnsToAdvance;
        while (index < line.Length && column < target)
        {
            var c = line[index];
            if (c == ' ')
            {
                column++;
                index++;
            }
            else if (c == '\t')
            {
                var nextStop = column + (4 - (column % 4));
                index++;
                if (nextStop > target)
                {
                    virtualSpaces = nextStop - target;
                    column = target;
                }
                else
                {
                    column = nextStop;
                }
            }
            else
            {
                break;
            }
        }
    }
}
