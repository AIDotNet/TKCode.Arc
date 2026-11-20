using System.Text;

namespace StreamMark.Core.Parsing;

/// <summary>
/// CommonMark 0.31.2 链接目标 / 标题解析、尖括号自动链接、GFM 裸自动链接（含 CJK 边界裁剪）及原始行内 HTML 扫描。
/// 全程零 Regex、Span 驱动、NativeAOT 安全。
/// </summary>
internal static class InlineLinkAndAutolink
{
    /// <summary>
    /// 尝试从 <paramref name="input"/>[<paramref name="pos"/>]（必须是 '('）解析行内链接目标与可选标题 <c>(dest "title")</c>。
    /// 成功时 <paramref name="endPos"/> 指向闭合 ')' 之后的下标。
    /// </summary>
    public static bool TryParseInlineLinkTail(
        ReadOnlySpan<char> input,
        int pos,
        out string url,
        out string? title,
        out int endPos)
    {
        url = string.Empty;
        title = null;
        endPos = pos;
        if (pos >= input.Length || input[pos] != '(')
        {
            return false;
        }
        var i = pos + 1;
        SkipWhitespaceAndNewlines(input, ref i);
        if (i < input.Length && input[i] == ')')
        {
            endPos = i + 1;
            return true;
        }
        if (!TryParseLinkDestination(input, ref i, out url))
        {
            return false;
        }
        var afterDest = i;
        SkipWhitespaceAndNewlines(input, ref i);
        if (i < input.Length && input[i] == ')')
        {
            endPos = i + 1;
            return true;
        }
        // 标题之前必须至少有一个空白字符
        if (i == afterDest)
        {
            return false;
        }
        if (!TryParseLinkTitle(input, ref i, out title))
        {
            return false;
        }
        SkipWhitespaceAndNewlines(input, ref i);
        if (i >= input.Length || input[i] != ')')
        {
            return false;
        }
        endPos = i + 1;
        return true;
    }

    /// <summary>解析链接目标（尖括号 <c>&lt;...&gt;</c> 或平衡括号形式，支持转义与实体解码）。</summary>
    public static bool TryParseLinkDestination(ReadOnlySpan<char> input, ref int i, out string destination)
    {
        destination = string.Empty;
        if (i >= input.Length)
        {
            return false;
        }
        if (input[i] == '<')
        {
            var start = i + 1;
            var j = start;
            while (j < input.Length)
            {
                var c = input[j];
                if (c is '\n' or '\r' or '<')
                {
                    return false;
                }
                if (c == '\\' && j + 1 < input.Length)
                {
                    j += 2;
                    continue;
                }
                if (c == '>')
                {
                    destination = UnicodeClassification.UnescapeAndDecode(input.Slice(start, j - start));
                    i = j + 1;
                    return true;
                }
                j++;
            }
            return false;
        }

        // 无尖括号：允许平衡圆括号（最多 32 层嵌套），不能含空白或 ASCII 控制字符
        var rawStart = i;
        var parens = 0;
        var k = i;
        while (k < input.Length)
        {
            var c = input[k];
            if (c <= 0x20 || c == 0x7F)
            {
                break;
            }
            if (c == '\\' && k + 1 < input.Length && UnicodeClassification.IsAsciiPunctuation(input[k + 1]))
            {
                k += 2;
                continue;
            }
            if (c == '(')
            {
                parens++;
                if (parens > 32)
                {
                    return false;
                }
                k++;
                continue;
            }
            if (c == ')')
            {
                if (parens == 0)
                {
                    break;
                }
                parens--;
                k++;
                continue;
            }
            k++;
        }
        if (parens != 0 || k == rawStart)
        {
            return false;
        }
        destination = UnicodeClassification.UnescapeAndDecode(input.Slice(rawStart, k - rawStart));
        i = k;
        return true;
    }

    /// <summary>解析链接标题（<c>"..."</c>、<c>'...'</c> 或 <c>(...)</c>）。</summary>
    public static bool TryParseLinkTitle(ReadOnlySpan<char> input, ref int i, out string? title)
    {
        title = null;
        if (i >= input.Length)
        {
            return false;
        }
        var open = input[i];
        var close = open switch
        {
            '"' => '"',
            '\'' => '\'',
            '(' => ')',
            _ => '\0',
        };
        if (close == '\0')
        {
            return false;
        }
        var start = i + 1;
        var j = start;
        while (j < input.Length)
        {
            var c = input[j];
            if (c == '\\' && j + 1 < input.Length)
            {
                j += 2;
                continue;
            }
            if (c == close)
            {
                title = UnicodeClassification.UnescapeAndDecode(input.Slice(start, j - start));
                i = j + 1;
                return true;
            }
            if (open == '(' && c == '(')
            {
                return false;
            }
            j++;
        }
        return false;
    }

    /// <summary>尝试解析引用标签 <c>[label]</c>（允许最多 999 字符，不含未转义 '['/']'）。</summary>
    public static bool TryParseLinkLabel(ReadOnlySpan<char> input, int pos, out ReadOnlySpan<char> label, out int endPos)
    {
        label = ReadOnlySpan<char>.Empty;
        endPos = pos;
        if (pos >= input.Length || input[pos] != '[')
        {
            return false;
        }
        var max = Math.Min(input.Length, pos + 1001);
        var hasNonWs = false;
        for (var i = pos + 1; i < max; i++)
        {
            var c = input[i];
            if (c == '\\' && i + 1 < max)
            {
                hasNonWs = true;
                i++;
                continue;
            }
            if (c == '[')
            {
                return false;
            }
            if (c == ']')
            {
                if (!hasNonWs)
                {
                    return false;
                }
                label = input.Slice(pos + 1, i - (pos + 1));
                endPos = i + 1;
                return true;
            }
            if (c is not (' ' or '\t' or '\r' or '\n'))
            {
                hasNonWs = true;
            }
        }
        return false;
    }

    /// <summary>尝试解析 CommonMark 尖括号自动链接（<c>&lt;https://...&gt;</c> 或 <c>&lt;user@host&gt;</c>）。</summary>
    public static bool TryMatchAngleAutolink(ReadOnlySpan<char> slice, out string url, out bool isEmail, out int consumed)
    {
        url = string.Empty;
        isEmail = false;
        consumed = 0;
        if (slice.Length < 4 || slice[0] != '<')
        {
            return false;
        }
        var close = slice.IndexOf('>');
        if (close < 3)
        {
            return false;
        }
        var inner = slice.Slice(1, close - 1);
        if (inner.IndexOfAny(" \t\r\n<") >= 0)
        {
            return false;
        }
        // URI autolink: scheme ':' rest
        var colon = inner.IndexOf(':');
        if (colon is >= 2 and <= 32 && char.IsAsciiLetter(inner[0]))
        {
            var validScheme = true;
            for (var i = 1; i < colon; i++)
            {
                if (!(char.IsAsciiLetterOrDigit(inner[i]) || inner[i] is '+' or '-' or '.'))
                {
                    validScheme = false;
                    break;
                }
            }
            if (validScheme)
            {
                for (var i = colon + 1; i < inner.Length; i++)
                {
                    if (inner[i] <= 0x20 || inner[i] == 0x7F)
                    {
                        return false;
                    }
                }
                url = inner.ToString();
                consumed = close + 1;
                return true;
            }
        }
        // Email autolink
        var at = inner.IndexOf('@');
        if (at > 0 && at < inner.Length - 1 && inner[(at + 1)..].IndexOf('@') < 0 && IsValidEmailAutolink(inner, at))
        {
            url = inner.ToString();
            isEmail = true;
            consumed = close + 1;
            return true;
        }
        return false;
    }

    private static bool IsValidEmailAutolink(ReadOnlySpan<char> s, int at)
    {
        for (var i = 0; i < at; i++)
        {
            var c = s[i];
            if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '/' or '=' or '?' or '^' or '_' or '`' or '{' or '|' or '}' or '~' or '-'))
            {
                return false;
            }
        }
        var labelLen = 0;
        for (var i = at + 1; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '.')
            {
                if (labelLen == 0 || s[i - 1] == '-')
                {
                    return false;
                }
                labelLen = 0;
            }
            else if (char.IsAsciiLetterOrDigit(c) || (c == '-' && labelLen > 0))
            {
                labelLen++;
                if (labelLen > 63)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        return labelLen > 0 && s[^1] != '-';
    }

    /// <summary>
    /// 尝试匹配 GFM 扩展裸自动链接（<c>http://</c>、<c>https://</c>、<c>ftp://</c>、<c>mailto:</c>、<c>www.</c>），
    /// 自动裁剪尾部标点及 CJK 字符/标点边界。
    /// </summary>
    public static bool TryMatchBareAutolink(
        ReadOnlySpan<char> input,
        int pos,
        out string display,
        out string url,
        out int consumed)
    {
        display = string.Empty;
        url = string.Empty;
        consumed = 0;
        // 前导字符检查：GFM §6.5 要求在行首、空白、CJK 字符或允许的前导标点（( * _ ~）之后（不含 '['，避免吞噬 Markdown 链接标签内的 URL）
        if (pos > 0)
        {
            var prev = input[pos - 1];
            if (!(prev <= 0x20 || prev is '(' or '*' or '_' or '~' || UnicodeClassification.IsCjkWide(prev) || UnicodeClassification.IsCjkPunctuation(prev)))
            {
                return false;
            }
        }
        var slice = input[pos..];
        var prefixLen = 0;
        var isWww = false;
        var isMailto = false;
        if (slice.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            prefixLen = 8;
        }
        else if (slice.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            prefixLen = 7;
        }
        else if (slice.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
        {
            prefixLen = 6;
        }
        else if (slice.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            prefixLen = 7;
            isMailto = true;
        }
        else if (slice.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            prefixLen = 4;
            isWww = true;
        }
        else
        {
            return false;
        }
        if (slice.Length <= prefixLen)
        {
            return false;
        }

        // 扫描到空白 / '<' / 未配对 ']' / CJK 边界为止（保留配对的 IPv6 方括号，如 http://[::1]:8080）
        var end = prefixLen;
        var openBrackets = 0;
        while (end < slice.Length)
        {
            var c = slice[end];
            if (c == '<' || UnicodeClassification.StopsAutoLink(c))
            {
                break;
            }
            if (c == '[')
            {
                openBrackets++;
            }
            else if (c == ']')
            {
                if (openBrackets == 0)
                {
                    break;
                }
                openBrackets--;
            }
            end++;
        }

        // GFM 尾部标点裁剪
        while (end > prefixLen)
        {
            var last = slice[end - 1];
            if (last is '?' or '!' or '.' or ',' or ':' or '*' or '_' or '~' or '\'' or '"')
            {
                end--;
                continue;
            }
            if (last == ')')
            {
                var opens = 0;
                var closes = 0;
                for (var j = prefixLen; j < end; j++)
                {
                    if (slice[j] == '(')
                    {
                        opens++;
                    }
                    else if (slice[j] == ')')
                    {
                        closes++;
                    }
                }
                if (closes > opens)
                {
                    end--;
                    continue;
                }
            }
            if (last == ';')
            {
                // 裁剪尾部的 HTML 实体（如 &amp;）
                var amp = slice[..end].LastIndexOf('&');
                if (amp >= prefixLen)
                {
                    var allAlnum = true;
                    for (var j = amp + 1; j < end - 1; j++)
                    {
                        if (!char.IsAsciiLetterOrDigit(slice[j]))
                        {
                            allAlnum = false;
                            break;
                        }
                    }
                    if (allAlnum && end - amp > 2)
                    {
                        end = amp;
                        continue;
                    }
                }
            }
            break;
        }

        var body = slice[prefixLen..end];
        if (body.IsEmpty)
        {
            return false;
        }
        if (isMailto)
        {
            var at = body.IndexOf('@');
            if (at <= 0 || at >= body.Length - 1)
            {
                return false;
            }
        }
        else
        {
            // 域名至少包含一个合法字符；www. 后需至少再有一个点
            var slash = body.IndexOfAny('/', '?', '#');
            var host = slash < 0 ? body : body[..slash];
            if (host.IsEmpty || (isWww && host.IndexOf('.') <= 0))
            {
                return false;
            }
            if (host[^1] == '-')
            {
                return false;
            }
        }

        display = slice[..end].ToString();
        url = isWww ? "http://" + display : display;
        consumed = end;
        return true;
    }

    /// <summary>尝试匹配 CommonMark 原始行内 HTML（开/闭标签、注释、处理指令、声明、CDATA）。</summary>
    public static bool TryMatchRawInlineHtml(ReadOnlySpan<char> slice, out int consumed)
    {
        consumed = 0;
        if (slice.Length < 3 || slice[0] != '<')
        {
            return false;
        }
        var second = slice[1];
        if (second == '!')
        {
            if (slice.StartsWith("<!--"))
            {
                var close = slice[4..].IndexOf("-->", StringComparison.Ordinal);
                if (close < 0)
                {
                    return false;
                }
                consumed = 4 + close + 3;
                return true;
            }
            if (slice.StartsWith("<![CDATA["))
            {
                var close = slice[9..].IndexOf("]]>", StringComparison.Ordinal);
                if (close < 0)
                {
                    return false;
                }
                consumed = 9 + close + 3;
                return true;
            }
            if (slice.Length > 3 && char.IsAsciiLetter(slice[2]))
            {
                var close = slice[2..].IndexOf('>');
                if (close < 0)
                {
                    return false;
                }
                consumed = 2 + close + 1;
                return true;
            }
            return false;
        }
        if (second == '?')
        {
            var close = slice[2..].IndexOf("?>", StringComparison.Ordinal);
            if (close < 0)
            {
                return false;
            }
            consumed = 2 + close + 2;
            return true;
        }
        if (second == '/')
        {
            // Closing tag: </tagname ws*>
            if (slice.Length < 4 || !char.IsAsciiLetter(slice[2]))
            {
                return false;
            }
            var i = 3;
            while (i < slice.Length && (char.IsAsciiLetterOrDigit(slice[i]) || slice[i] == '-'))
            {
                i++;
            }
            while (i < slice.Length && slice[i] is ' ' or '\t' or '\r' or '\n')
            {
                i++;
            }
            if (i < slice.Length && slice[i] == '>')
            {
                consumed = i + 1;
                return true;
            }
            return false;
        }
        if (char.IsAsciiLetter(second))
        {
            // Open tag: <tagname (ws attr)* ws* /?>
            var i = 2;
            while (i < slice.Length && (char.IsAsciiLetterOrDigit(slice[i]) || slice[i] == '-'))
            {
                i++;
            }
            while (i < slice.Length)
            {
                var wsStart = i;
                while (i < slice.Length && slice[i] is ' ' or '\t' or '\r' or '\n')
                {
                    i++;
                }
                if (i >= slice.Length)
                {
                    return false;
                }
                if (slice[i] == '>')
                {
                    consumed = i + 1;
                    return true;
                }
                if (slice[i] == '/' && i + 1 < slice.Length && slice[i + 1] == '>')
                {
                    consumed = i + 2;
                    return true;
                }
                if (i == wsStart)
                {
                    return false;
                }
                // Attribute name
                var attrStart = i;
                if (!(char.IsAsciiLetter(slice[i]) || slice[i] is '_' or ':'))
                {
                    return false;
                }
                i++;
                while (i < slice.Length && (char.IsAsciiLetterOrDigit(slice[i]) || slice[i] is '_' or '.' or ':' or '-'))
                {
                    i++;
                }
                if (i == attrStart)
                {
                    return false;
                }
                var afterName = i;
                while (i < slice.Length && slice[i] is ' ' or '\t' or '\r' or '\n')
                {
                    i++;
                }
                if (i < slice.Length && slice[i] == '=')
                {
                    i++;
                    while (i < slice.Length && slice[i] is ' ' or '\t' or '\r' or '\n')
                    {
                        i++;
                    }
                    if (i >= slice.Length)
                    {
                        return false;
                    }
                    if (slice[i] is '"' or '\'')
                    {
                        var q = slice[i++];
                        var closeQ = slice[i..].IndexOf(q);
                        if (closeQ < 0)
                        {
                            return false;
                        }
                        i += closeQ + 1;
                    }
                    else
                    {
                        var valStart = i;
                        while (i < slice.Length && slice[i] > 0x20 && slice[i] is not ('"' or '\'' or '=' or '<' or '>' or '`'))
                        {
                            i++;
                        }
                        if (i == valStart)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    i = afterName;
                }
            }
        }
        return false;
    }

    public static void SkipWhitespaceAndNewlines(ReadOnlySpan<char> input, ref int i)
    {
        while (i < input.Length && input[i] is ' ' or '\t' or '\r' or '\n')
        {
            i++;
        }
    }
}
