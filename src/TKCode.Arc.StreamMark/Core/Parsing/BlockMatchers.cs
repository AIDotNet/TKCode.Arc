using System.Collections.Frozen;
using System.Collections.Immutable;
using StreamMark.Core.Model;

namespace StreamMark.Core.Parsing;

/// <summary>
/// 无状态的块级语法行匹配器（零 Regex、全 Span、NativeAOT 安全）：
/// 覆盖 CommonMark 0.31.2、GFM 管道表格、任务列表、GitHub Alerts、数学块、自定义容器、
/// 定义列表、脚注、链接引用定义、HTML 块 Type 1–7 与白名单块级组件。
/// </summary>
internal static class BlockMatchers
{
    private static readonly FrozenSet<string> s_htmlType6Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "base", "basefont", "blockquote", "body", "caption", "center",
        "col", "colgroup", "dd", "details", "dialog", "dir", "div", "dl", "dt", "fieldset", "figcaption",
        "figure", "footer", "form", "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head",
        "header", "hr", "html", "iframe", "legend", "li", "link", "main", "menu", "menuitem", "nav",
        "noframes", "ol", "optgroup", "option", "p", "param", "search", "section", "summary", "table",
        "tbody", "td", "tfoot", "th", "thead", "title", "tr", "track", "ul",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> s_htmlType1Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "pre", "script", "style", "textarea",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>匹配 ATX 标题（<c>#</c>–<c>######</c> + 空白/行尾，自动剥离可选尾部闭合 <c>#</c> 串）。</summary>
    public static bool TryMatchAtxHeading(ReadOnlySpan<char> content, out int level, out int textStart, out int textLength)
    {
        level = 0;
        textStart = 0;
        textLength = 0;
        if (content.IsEmpty || content[0] != '#')
        {
            return false;
        }
        while (level < content.Length && level < 7 && content[level] == '#')
        {
            level++;
        }
        if (level is < 1 or > 6)
        {
            return false;
        }
        if (level < content.Length && content[level] is not (' ' or '\t'))
        {
            return false;
        }
        var start = level;
        while (start < content.Length && content[start] is ' ' or '\t')
        {
            start++;
        }
        var end = content.Length;
        while (end > start && content[end - 1] is ' ' or '\t')
        {
            end--;
        }
        // 可选的尾部闭合 '#' 串（前面必须为空白或就是整行内容）
        var hashEnd = end;
        while (hashEnd > start && content[hashEnd - 1] == '#')
        {
            hashEnd--;
        }
        if (hashEnd < end && (hashEnd == start || content[hashEnd - 1] is ' ' or '\t'))
        {
            end = hashEnd;
            while (end > start && content[end - 1] is ' ' or '\t')
            {
                end--;
            }
        }
        textStart = start;
        textLength = Math.Max(0, end - start);
        return true;
    }

    /// <summary>匹配 Setext 标题下划线（<c>===</c> → H1，<c>---</c> → H2）。</summary>
    public static bool TryMatchSetextUnderline(ReadOnlySpan<char> content, bool isPartialStreamingLine, bool guardSetextFlicker, out int level)
    {
        level = 0;
        if (content.IsEmpty || content[0] is not ('=' or '-'))
        {
            return false;
        }
        var ch = content[0];
        var count = 0;
        while (count < content.Length && content[count] == ch)
        {
            count++;
        }
        var trailingWs = content[count..];
        if (!trailingWs.IsWhiteSpace())
        {
            return false;
        }
        // 流式半行防闪烁：仅 1–2 个 '-' / '=' 且无尾随空格时不提前把上一段转为标题
        if (isPartialStreamingLine && guardSetextFlicker && count <= 2 && trailingWs.IsEmpty)
        {
            return false;
        }
        level = ch == '=' ? 1 : 2;
        return true;
    }

    /// <summary>匹配分隔线（<c>***</c> / <c>---</c> / <c>___</c>，≥3 字符允许夹杂空格/制表符）。</summary>
    public static bool IsThematicBreak(ReadOnlySpan<char> content)
    {
        if (content.Length < 3 || content[0] is not ('-' or '*' or '_'))
        {
            return false;
        }
        var ch = content[0];
        var count = 0;
        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c == ch)
            {
                count++;
            }
            else if (c is not (' ' or '\t'))
            {
                return false;
            }
        }
        return count >= 3;
    }

    /// <summary>匹配代码围栏开启行（`` ``` `` 或 <c>~~~</c>）。</summary>
    public static bool TryMatchFencedCodeOpen(ReadOnlySpan<char> content, out char fenceChar, out int fenceLen, out string? info, out string? language)
    {
        fenceChar = '\0';
        fenceLen = 0;
        info = null;
        language = null;
        if (content.Length < 3 || content[0] is not ('`' or '~'))
        {
            return false;
        }
        var ch = content[0];
        var count = 0;
        while (count < content.Length && content[count] == ch)
        {
            count++;
        }
        if (count < 3)
        {
            return false;
        }
        var rest = content[count..].Trim();
        if (ch == '`' && rest.Contains('`'))
        {
            return false;
        }
        fenceChar = ch;
        fenceLen = count;
        if (!rest.IsEmpty)
        {
            var decoded = UnicodeClassification.UnescapeAndDecode(rest);
            info = decoded;
            var sp = decoded.AsSpan().IndexOfAny(' ', '\t');
            language = sp < 0 ? decoded : decoded[..sp];
            if (string.IsNullOrWhiteSpace(language))
            {
                language = null;
            }
        }
        return true;
    }

    /// <summary>匹配代码围栏闭合行（同字符 ≥ 开启长度，后仅空白）。</summary>
    public static bool IsFencedCodeClose(ReadOnlySpan<char> content, char fenceChar, int minLength)
    {
        if (content.Length < minLength || content[0] != fenceChar)
        {
            return false;
        }
        var count = 0;
        while (count < content.Length && content[count] == fenceChar)
        {
            count++;
        }
        return count >= minLength && content[count..].IsWhiteSpace();
    }

    /// <summary>匹配自定义容器围栏开启行（<c>:::info args</c>）。</summary>
    public static bool TryMatchCustomContainerOpen(ReadOnlySpan<char> content, out int fenceLen, out string? info, out string? arguments)
    {
        fenceLen = 0;
        info = null;
        arguments = null;
        if (content.Length < 3 || content[0] != ':')
        {
            return false;
        }
        var count = 0;
        while (count < content.Length && content[count] == ':')
        {
            count++;
        }
        if (count < 3)
        {
            return false;
        }
        var rest = content[count..].Trim();
        // 单纯的 ":::" 不带 info 是关闭围栏而非开启（在已有容器外才按空容器）
        fenceLen = count;
        if (!rest.IsEmpty)
        {
            var sp = rest.IndexOfAny(' ', '\t');
            if (sp < 0)
            {
                info = rest.ToString();
            }
            else
            {
                info = rest[..sp].ToString();
                var args = rest[(sp + 1)..].Trim();
                if (!args.IsEmpty)
                {
                    arguments = args.ToString();
                }
            }
        }
        return true;
    }

    public static bool IsCustomContainerClose(ReadOnlySpan<char> content, int minLength)
    {
        if (content.Length < minLength || content[0] != ':')
        {
            return false;
        }
        var count = 0;
        while (count < content.Length && content[count] == ':')
        {
            count++;
        }
        return count >= minLength && content[count..].IsWhiteSpace();
    }

    /// <summary>匹配块级公式开启行：<c>$$</c>（含单行 <c>$$...$$</c>）或 <c>\[</c>（含单行 <c>\[...\]</c>）。</summary>
    public static bool TryMatchMathBlockOpen(
        ReadOnlySpan<char> content,
        out bool isBracket,
        out bool closedOnSameLine,
        out ReadOnlySpan<char> firstLineBody)
    {
        isBracket = false;
        closedOnSameLine = false;
        firstLineBody = ReadOnlySpan<char>.Empty;
        if (content.Length < 2)
        {
            return false;
        }
        if (content[0] == '$' && content[1] == '$')
        {
            var after = content[2..];
            if (after.IsWhiteSpace())
            {
                return true;
            }
            // 同行闭合 $$x^2$$
            var trimmed = after.TrimEnd();
            if (trimmed.Length >= 2 && trimmed[^1] == '$' && trimmed[^2] == '$')
            {
                closedOnSameLine = true;
                firstLineBody = trimmed[..^2].Trim();
                return true;
            }
            return false;
        }
        if (content[0] == '\\' && content[1] == '[')
        {
            isBracket = true;
            var after = content[2..];
            var closeIdx = FindClosingBracketMath(after);
            if (closeIdx >= 0)
            {
                closedOnSameLine = true;
                firstLineBody = after[..closeIdx];
            }
            else
            {
                firstLineBody = after;
            }
            return true;
        }
        return false;
    }

    public static int FindClosingBracketMath(ReadOnlySpan<char> span)
    {
        for (var i = 0; i + 1 < span.Length; i++)
        {
            if (span[i] == '\\' && span[i + 1] == ']')
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>匹配列表项标记（CommonMark §5.2），返回消耗的字符数与内容列缩进。</summary>
    public static bool TryMatchListMarker(
        ReadOnlySpan<char> line,
        int contentIdx,
        int contentCol,
        bool interruptingParagraph,
        out bool isOrdered,
        out char bullet,
        out int startNumber,
        out int afterMarkerIdx,
        out int contentColumn,
        out bool lineIsEmptyAfterMarker)
    {
        isOrdered = false;
        bullet = '\0';
        startNumber = 1;
        afterMarkerIdx = contentIdx;
        contentColumn = contentCol;
        lineIsEmptyAfterMarker = false;

        if (contentIdx >= line.Length)
        {
            return false;
        }

        var c = line[contentIdx];
        int markerEndIdx;
        int markerWidth;

        if (c is '-' or '*' or '+')
        {
            bullet = c;
            markerEndIdx = contentIdx + 1;
            markerWidth = 1;
        }
        else if (char.IsAsciiDigit(c))
        {
            var digits = 0;
            var num = 0;
            while (contentIdx + digits < line.Length && digits < 9 && char.IsAsciiDigit(line[contentIdx + digits]))
            {
                num = num * 10 + (line[contentIdx + digits] - '0');
                digits++;
            }
            if (digits == 0 || contentIdx + digits >= line.Length)
            {
                return false;
            }
            var delim = line[contentIdx + digits];
            if (delim is not ('.' or ')'))
            {
                return false;
            }
            // CommonMark §5.2: 只有起始号为 1 的有序列表允许打断段落
            if (interruptingParagraph && num != 1)
            {
                return false;
            }
            isOrdered = true;
            bullet = delim;
            startNumber = num;
            markerEndIdx = contentIdx + digits + 1;
            markerWidth = digits + 1;
        }
        else
        {
            return false;
        }

        // 标记之后必须是行尾、空格或制表符
        if (markerEndIdx < line.Length && line[markerEndIdx] is not (' ' or '\t'))
        {
            return false;
        }

        var afterCol = contentCol + markerWidth;
        var spacesAfter = UnicodeClassification.MeasureIndent(line, markerEndIdx, afterCol, out var firstNonWs);
        lineIsEmptyAfterMarker = firstNonWs >= line.Length;

        // CommonMark §5.2: 空列表项不能打断段落
        if (interruptingParagraph && lineIsEmptyAfterMarker)
        {
            return false;
        }

        // 如果标记后全是空白，或者缩进 ≥ 5 列（缩进代码块在首行），按 1 列计
        if (lineIsEmptyAfterMarker || spacesAfter > 4)
        {
            contentColumn = afterCol + 1;
            afterMarkerIdx = markerEndIdx < line.Length ? markerEndIdx + 1 : markerEndIdx;
            if (markerEndIdx < line.Length && line[markerEndIdx] == '\t')
            {
                // Tab 展开由调用方按列前进处理
                afterMarkerIdx = markerEndIdx;
            }
        }
        else
        {
            contentColumn = afterCol + Math.Max(1, spacesAfter);
            afterMarkerIdx = firstNonWs;
        }
        return true;
    }

    /// <summary>检查是否是列表项内的比较运算符（例如 <c>- &gt; 25</c>、<c>- &gt;= 10</c>、<c>- &gt; $5</c>），不应被当作引用块。</summary>
    public static bool IsComparisonOperatorAfterQuote(ReadOnlySpan<char> afterGt)
    {
        var i = 0;
        if (i < afterGt.Length && afterGt[i] == '=')
        {
            i++;
        }
        while (i < afterGt.Length && afterGt[i] is ' ' or '\t')
        {
            i++;
        }
        if (i < afterGt.Length && afterGt[i] == '$')
        {
            i++;
        }
        return i < afterGt.Length && char.IsAsciiDigit(afterGt[i]);
    }

    /// <summary>检查引用块首行是否为 GitHub 提示块标记（<c>[!NOTE]</c> 等）。</summary>
    public static bool TryMatchAlertKind(ReadOnlySpan<char> firstLine, out string kind)
    {
        kind = string.Empty;
        var trimmed = firstLine.Trim();
        if (trimmed.Length < 5 || trimmed[0] != '[' || trimmed[1] != '!' || trimmed[^1] != ']')
        {
            return false;
        }
        var inner = trimmed.Slice(2, trimmed.Length - 3);
        if (inner.Equals("NOTE", StringComparison.OrdinalIgnoreCase))
        {
            kind = "NOTE";
            return true;
        }
        if (inner.Equals("TIP", StringComparison.OrdinalIgnoreCase))
        {
            kind = "TIP";
            return true;
        }
        if (inner.Equals("IMPORTANT", StringComparison.OrdinalIgnoreCase))
        {
            kind = "IMPORTANT";
            return true;
        }
        if (inner.Equals("WARNING", StringComparison.OrdinalIgnoreCase))
        {
            kind = "WARNING";
            return true;
        }
        if (inner.Equals("CAUTION", StringComparison.OrdinalIgnoreCase))
        {
            kind = "CAUTION";
            return true;
        }
        return false;
    }

    /// <summary>匹配 GFM 管道表格的对齐分隔行（<c>|:--|--:|:--:|---|</c>），返回每列对齐方式。</summary>
    public static bool TryParseTableDelimiterRow(ReadOnlySpan<char> line, out ImmutableArray<TableAlignment> columns)
    {
        columns = default;
        var trimmed = line.Trim();
        if (trimmed.IsEmpty || !trimmed.Contains('|') && !trimmed.Contains('-'))
        {
            return false;
        }
        // 至少必须有 '|' 或者合法单列对齐（GFM 要求表头与分隔行包含 '|' 或严格匹配列数；单列至少需 '|'）
        if (!trimmed.Contains('|'))
        {
            return false;
        }
        if (trimmed[0] == '|')
        {
            trimmed = trimmed[1..];
        }
        if (trimmed.Length > 0 && trimmed[^1] == '|')
        {
            trimmed = trimmed[..^1];
        }
        if (trimmed.IsEmpty)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<TableAlignment>();
        var pos = 0;
        while (pos <= trimmed.Length)
        {
            var bar = trimmed[pos..].IndexOf('|');
            var cell = (bar < 0 ? trimmed[pos..] : trimmed.Slice(pos, bar)).Trim();
            if (cell.IsEmpty)
            {
                return false;
            }
            var left = cell[0] == ':';
            var right = cell.Length > 1 && cell[^1] == ':';
            var dashStart = left ? 1 : 0;
            var dashEnd = right ? cell.Length - 1 : cell.Length;
            if (dashEnd <= dashStart)
            {
                return false;
            }
            for (var i = dashStart; i < dashEnd; i++)
            {
                if (cell[i] != '-')
                {
                    return false;
                }
            }
            builder.Add((left, right) switch
            {
                (true, true) => TableAlignment.Center,
                (true, false) => TableAlignment.Left,
                (false, true) => TableAlignment.Right,
                _ => TableAlignment.None,
            });
            if (bar < 0)
            {
                break;
            }
            pos += bar + 1;
        }
        if (builder.Count == 0)
        {
            return false;
        }
        columns = builder.ToImmutable();
        return true;
    }

    /// <summary>
    /// 按 GFM 规则将表格行拆分为单元格切片列表（忽略反引号代码跨度与 <c>$...$</c> 行内公式中的 <c>|</c>，
    /// 支持 <c>\|</c> 转义）。返回切出的单元格数量。
    /// </summary>
    public static int SplitTableRowCells(ReadOnlySpan<char> line, List<string> outputCells)
    {
        outputCells.Clear();
        var trimmed = line.Trim();
        if (trimmed.IsEmpty)
        {
            return 0;
        }
        // 剥离行首与未转义的行尾 '|'
        var start = 0;
        var end = trimmed.Length;
        if (trimmed[0] == '|')
        {
            start = 1;
        }
        if (end > start && trimmed[end - 1] == '|' && !IsEscaped(trimmed, end - 1))
        {
            end--;
        }

        var cellStart = start;
        var inMath = false;
        var i = start;
        while (i < end)
        {
            var c = trimmed[i];
            if (c == '\\' && i + 1 < end)
            {
                i += 2;
                continue;
            }
            if (c == '`')
            {
                var tickStart = i;
                while (i < end && trimmed[i] == '`')
                {
                    i++;
                }
                var tickLen = i - tickStart;
                // 扫描匹配闭合反引号
                var scan = i;
                while (scan < end)
                {
                    var next = trimmed[scan..end].IndexOf('`');
                    if (next < 0)
                    {
                        break;
                    }
                    var rs = scan + next;
                    var re = rs;
                    while (re < end && trimmed[re] == '`')
                    {
                        re++;
                    }
                    if (re - rs == tickLen)
                    {
                        i = re;
                        break;
                    }
                    scan = re;
                }
                continue;
            }
            if (c == '$')
            {
                inMath = !inMath;
                i++;
                continue;
            }
            if (c == '|' && !inMath)
            {
                outputCells.Add(UnescapeTableCell(trimmed.Slice(cellStart, i - cellStart).Trim()));
                cellStart = i + 1;
            }
            i++;
        }
        if (cellStart <= end)
        {
            outputCells.Add(UnescapeTableCell(trimmed.Slice(cellStart, end - cellStart).Trim()));
        }
        return outputCells.Count;
    }

    /// <summary>快速计算表格行的列数（不分配字符串，用于判定表头行与分隔行列数是否匹配）。</summary>
    public static int CountTableRowCells(ReadOnlySpan<char> line)
    {
        var trimmed = line.Trim();
        if (trimmed.IsEmpty || !trimmed.Contains('|'))
        {
            return 0;
        }
        var start = trimmed[0] == '|' ? 1 : 0;
        var end = trimmed.Length;
        if (end > start && trimmed[end - 1] == '|' && !IsEscaped(trimmed, end - 1))
        {
            end--;
        }
        var count = 1;
        for (var i = start; i < end; i++)
        {
            if (trimmed[i] == '\\' && i + 1 < end)
            {
                i++;
                continue;
            }
            if (trimmed[i] == '|')
            {
                count++;
            }
        }
        return count;
    }

    private static bool IsEscaped(ReadOnlySpan<char> span, int index)
    {
        var backslashes = 0;
        for (var i = index - 1; i >= 0 && span[i] == '\\'; i--)
        {
            backslashes++;
        }
        return (backslashes & 1) == 1;
    }

    private static string UnescapeTableCell(ReadOnlySpan<char> cell)
    {
        if (cell.IsEmpty)
        {
            return string.Empty;
        }
        if (!cell.Contains(@"\|", StringComparison.Ordinal))
        {
            return cell.ToString();
        }
        var sb = new System.Text.StringBuilder(cell.Length);
        for (var i = 0; i < cell.Length; i++)
        {
            if (cell[i] == '\\' && i + 1 < cell.Length && cell[i + 1] == '|')
            {
                sb.Append('|');
                i++;
            }
            else
            {
                sb.Append(cell[i]);
            }
        }
        return sb.ToString();
    }

    /// <summary>匹配 CommonMark HTML 块类型 1–7。</summary>
    public static bool TryMatchHtmlBlockOpen(
        ReadOnlySpan<char> content,
        bool interruptingParagraph,
        out CommonMarkHtmlBlockKind kind,
        out string? rawTagName)
    {
        kind = default;
        rawTagName = null;
        if (content.Length < 2 || content[0] != '<')
        {
            return false;
        }
        if (content.StartsWith("<!--", StringComparison.Ordinal))
        {
            kind = CommonMarkHtmlBlockKind.Type2Comment;
            return true;
        }
        if (content.StartsWith("<?", StringComparison.Ordinal))
        {
            kind = CommonMarkHtmlBlockKind.Type3Processing;
            return true;
        }
        if (content.StartsWith("<![CDATA[", StringComparison.Ordinal))
        {
            kind = CommonMarkHtmlBlockKind.Type5CData;
            return true;
        }
        if (content[1] == '!' && content.Length > 2 && char.IsAsciiLetter(content[2]))
        {
            kind = CommonMarkHtmlBlockKind.Type4Declaration;
            return true;
        }

        // 提取标签名判断 Type 1 / Type 6
        var idx = 1;
        var isClosing = false;
        if (content[idx] == '/')
        {
            isClosing = true;
            idx++;
        }
        var nameStart = idx;
        while (idx < content.Length && (char.IsAsciiLetterOrDigit(content[idx]) || content[idx] == '-'))
        {
            idx++;
        }
        if (idx > nameStart && char.IsAsciiLetter(content[nameStart]))
        {
            var tagName = content.Slice(nameStart, idx - nameStart);
            var afterOk1 = idx == content.Length || content[idx] is ' ' or '\t' or '>';
            if (!isClosing && afterOk1 && ContainsIgnoreCase(s_htmlType1Tags, tagName))
            {
                kind = CommonMarkHtmlBlockKind.Type1RawTag;
                rawTagName = tagName.ToString().ToLowerInvariant();
                return true;
            }
            var afterOk6 = afterOk1 || (content[idx] == '/' && idx + 1 < content.Length && content[idx + 1] == '>');
            if (afterOk6 && ContainsIgnoreCase(s_htmlType6Tags, tagName))
            {
                kind = CommonMarkHtmlBlockKind.Type6BlockTag;
                return true;
            }
        }

        // Type 7: 完整开/闭标签后仅允许空白到行尾，且不能打断段落
        if (!interruptingParagraph && InlineLinkAndAutolink.TryMatchRawInlineHtml(content, out var consumed))
        {
            // Type 7 排除注释/声明/CDATA（已在上面匹配）
            if (content[1] is not ('!' or '?') && content[consumed..].IsWhiteSpace())
            {
                kind = CommonMarkHtmlBlockKind.Type7CompleteTag;
                return true;
            }
        }
        return false;
    }

    public static bool IsHtmlBlockCloseLine(ReadOnlySpan<char> line, CommonMarkHtmlBlockKind kind, string? rawTagName) => kind switch
    {
        CommonMarkHtmlBlockKind.Type1RawTag => rawTagName is not null && line.Contains("</" + rawTagName + ">", StringComparison.OrdinalIgnoreCase),
        CommonMarkHtmlBlockKind.Type2Comment => line.Contains("-->", StringComparison.Ordinal),
        CommonMarkHtmlBlockKind.Type3Processing => line.Contains("?>", StringComparison.Ordinal),
        CommonMarkHtmlBlockKind.Type4Declaration => line.Contains('>'),
        CommonMarkHtmlBlockKind.Type5CData => line.Contains("]]>", StringComparison.Ordinal),
        _ => false,
    };

    private static bool ContainsIgnoreCase(FrozenSet<string> set, ReadOnlySpan<char> name)
    {
#if NET9_0_OR_GREATER
        return set.GetAlternateLookup<ReadOnlySpan<char>>().Contains(name);
#else
        return set.Contains(name.ToString());
#endif
    }

    /// <summary>匹配脚注定义开头行 <c>[^label]: ...</c>。</summary>
    public static bool TryMatchFootnoteOpen(ReadOnlySpan<char> content, out string rawLabel, out int bodyStart)
    {
        rawLabel = string.Empty;
        bodyStart = 0;
        if (content.Length < 5 || content[0] != '[' || content[1] != '^')
        {
            return false;
        }
        var close = content[2..].IndexOf(']');
        if (close <= 0 || 2 + close + 1 >= content.Length || content[2 + close + 1] != ':')
        {
            return false;
        }
        var labelSpan = content.Slice(2, close);
        if (labelSpan.IndexOfAny(" \t\r\n[") >= 0)
        {
            return false;
        }
        rawLabel = labelSpan.ToString();
        var after = 2 + close + 2;
        if (after < content.Length && content[after] is ' ' or '\t')
        {
            after++;
        }
        bodyStart = after;
        return true;
    }

    /// <summary>匹配定义列表释义行（<c>: ...</c> 或 <c>~ ...</c> 后跟空白）。</summary>
    public static bool TryMatchDefinitionMarker(ReadOnlySpan<char> content, out int bodyStart)
    {
        bodyStart = 0;
        if (content.Length >= 2 && content[0] == ':' && content[1] is ' ' or '\t')
        {
            var i = 1;
            while (i < content.Length && content[i] is ' ' or '\t')
            {
                i++;
            }
            bodyStart = i;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 按 CommonMark §4.7 从段落行首消费 0 个或多个链接引用定义（<c>[label]: url "title"</c>），
    /// 返回消费掉的字符数（相对拼接后的段落文本）；若未匹配返回 0。
    /// </summary>
    public static int ConsumeLinkReferenceDefinitions(
        ReadOnlySpan<char> text,
        ReferenceRegistry references,
        HashSet<int>? affectedTopKeys)
    {
        var pos = 0;
        while (pos < text.Length)
        {
            var lineStart = pos;
            // 最多跳过 3 个前导空格
            var spaces = 0;
            while (pos < text.Length && text[pos] == ' ' && spaces < 3)
            {
                pos++;
                spaces++;
            }
            if (pos >= text.Length || text[pos] != '[')
            {
                return lineStart;
            }
            if (!InlineLinkAndAutolink.TryParseLinkLabel(text, pos, out var rawLabel, out var afterLabel))
            {
                return lineStart;
            }
            // 排除脚注定义 [^label]:
            if (rawLabel.Length >= 1 && rawLabel[0] == '^')
            {
                return lineStart;
            }
            if (afterLabel >= text.Length || text[afterLabel] != ':')
            {
                return lineStart;
            }
            var i = afterLabel + 1;
            SkipSpacesAndAtMostOneNewline(text, ref i);
            if (!InlineLinkAndAutolink.TryParseLinkDestination(text, ref i, out var url))
            {
                return lineStart;
            }

            var afterDest = i;
            var sawNewline = SkipSpacesAndTrackNewline(text, ref i);
            var endAfterDest = i;
            string? title = null;

            // 尝试解析可选标题
            if ((i > afterDest) && i < text.Length && text[i] is '"' or '\'' or '(')
            {
                var titlePos = i;
                if (InlineLinkAndAutolink.TryParseLinkTitle(text, ref titlePos, out var parsedTitle))
                {
                    var afterTitle = titlePos;
                    while (afterTitle < text.Length && text[afterTitle] is ' ' or '\t')
                    {
                        afterTitle++;
                    }
                    if (afterTitle == text.Length || text[afterTitle] == '\n')
                    {
                        title = parsedTitle;
                        i = afterTitle < text.Length ? afterTitle + 1 : afterTitle;
                        var norm = UnicodeClassification.NormalizeLabel(rawLabel);
                        references.AddLinkDefinition(norm, url, title, affectedTopKeys);
                        pos = i;
                        continue;
                    }
                }
                // 标题所在行有额外非空白：若标题与目标同行，则整条引用定义失败；若跨行，则无标题定义成功！
                if (!sawNewline)
                {
                    return lineStart;
                }
            }

            // 无标题情形：目标后直到行尾必须只有空白
            var check = afterDest;
            while (check < text.Length && text[check] is ' ' or '\t')
            {
                check++;
            }
            if (check < text.Length && text[check] != '\n')
            {
                return lineStart;
            }
            var nextPos = check < text.Length ? check + 1 : check;
            var labelNorm = UnicodeClassification.NormalizeLabel(rawLabel);
            references.AddLinkDefinition(labelNorm, url, title: null, affectedTopKeys);
            pos = nextPos;
        }
        return pos;
    }

    private static void SkipSpacesAndAtMostOneNewline(ReadOnlySpan<char> text, ref int i)
    {
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }
        if (i < text.Length && text[i] == '\n')
        {
            i++;
            while (i < text.Length && text[i] is ' ' or '\t')
            {
                i++;
            }
        }
    }

    private static bool SkipSpacesAndTrackNewline(ReadOnlySpan<char> text, ref int i)
    {
        var sawNewline = false;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }
        if (i < text.Length && text[i] == '\n')
        {
            sawNewline = true;
            i++;
            while (i < text.Length && text[i] is ' ' or '\t')
            {
                i++;
            }
        }
        return sawNewline;
    }

    /// <summary>流式末尾半行清理：去除未闭合的 HTML 尾标签（与 Remend HtmlTagHandler 同语义，保护行内代码/公式）。</summary>
    public static ReadOnlySpan<char> StripTrailingIncompleteHtmlTag(ReadOnlySpan<char> line)
    {
        if (line.IsEmpty)
        {
            return line;
        }
        var lastLt = line.LastIndexOf('<');
        if (lastLt < 0 || lastLt + 1 >= line.Length)
        {
            return line;
        }
        // 已闭合的标签（存在 '>'）不剥离
        if (line[(lastLt + 1)..].Contains('>'))
        {
            return line;
        }
        var next = line[lastLt + 1];
        if (!(char.IsAsciiLetter(next) || next is '/' or '!'))
        {
            return line;
        }
        // 如果 '<' 前存在奇数个未闭合反引号或 '$'，视为代码/数学内容，不剥离
        var ticks = 0;
        for (var i = 0; i < lastLt; i++)
        {
            if (line[i] == '`')
            {
                ticks++;
            }
        }
        if ((ticks & 1) == 1)
        {
            return line;
        }
        return line[..lastLt].TrimEnd();
    }
}
