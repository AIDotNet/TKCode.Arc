using StreamMark.Core.Parsing;

namespace StreamMark.Core.Streaming;

/// <summary>
/// 在原始源码上逐行扫描，找出可以安全冻结的块边界（规则见 docs/streaming-rules.md）。
/// </summary>
/// <remarks>
/// 冻结边界之前的文本单独解析，结果必须与整篇解析一致，所以只在"之后无论再来什么文本都不会改变前面解析结果"
/// 的位置切开：空行之后（不在围栏代码、公式块、HTML 原始块内，下一行没有缩进，列表后面不是新的列表项），
/// 或者顶层围栏代码块 / 公式块闭合之后、ATX 标题之后。边界只向前推进，从不回退；永远不冻结最后一个块。
/// 只扫描新到达的完整行，每次追加的成本与新增文本成正比。
/// </remarks>
internal sealed class StreamSegmenter
{
    private enum HtmlKind : byte
    {
        None,
        Comment,
        Processing,
        Declaration,
        CData,
        Raw,
        /// <summary>白名单块级组件（如 &lt;details&gt;），内部可含空行，带嵌套深度。</summary>
        Container,
        /// <summary>内容原样保留的块级组件：到行尾是结束标签的那一行为止（与解析器同一规则），不计嵌套。</summary>
        RawComponent,
    }

    private readonly string[] _containerTags;
    private readonly string[] _rawComponentTags;
    private int _containerDepth;

    public StreamSegmenter() : this([])
    {
    }

    public StreamSegmenter(IEnumerable<string> containerTags) : this(containerTags, [])
    {
    }

    public StreamSegmenter(IEnumerable<string> containerTags, IEnumerable<string> rawComponentTags)
    {
        _containerTags = [.. containerTags];
        _rawComponentTags = [.. rawComponentTags];
    }

    // 已冻结的边界（绝对偏移）
    public int FrozenEnd { get; private set; }

    // 出现了链接引用定义或脚注定义：它们会改变前文的解析结果，此后不再冻结
    public bool SawDefinition { get; private set; }

    private int _scanPos;
    private bool _runHasContent;
    private bool _runHasListItem;
    private int _pendingBoundary = -1;
    private bool _afterClosedBlock;

    private char _fenceChar;
    private int _fenceLength;
    private bool _fenceTopLevel;
    private int _fenceIndent;
    private string? _fenceInfo;
    private int _fenceContentStart = -1;
    private int _fenceStart = -1;
    private bool _fenceStartsRun;

    private bool _inDollarMath;
    private bool _inBracketMath;
    private bool _mathTopLevel;
    private HtmlKind _html;
    private string? _htmlRawTag;
    private int _rawRegionStart = -1;

    /// <summary>
    /// 不为 null 时记录原样组件的内容区间（绝对偏移，成对：起点、终点；不含开始 / 结束标签）。
    /// 只用于解析前的遮盖（<see cref="DocumentComposer"/>），冻结判断不需要它。
    /// </summary>
    internal List<int>? RawRegions { get; init; }

    /// <summary>扫描到文本末尾后收尾：仍开着的原样组件延伸到末尾，或止于尚未结束的最后一行里的结束标签。</summary>
    internal void CloseRawRegions(ReadOnlySpan<char> text, int textStart)
    {
        if (RawRegions is null || _rawRegionStart < 0)
        {
            return;
        }
        var end = textStart + text.Length;
        var partial = text[Math.Min(_scanPos - textStart, text.Length)..].TrimEnd();
        if (!partial.IsEmpty && EndsWithClosingTag(partial))
        {
            end = _scanPos + partial.Length - (_htmlRawTag!.Length + 3);
        }
        AddRawRegion(_rawRegionStart, end);
        _rawRegionStart = -1;
    }

    private void AddRawRegion(int start, int end)
    {
        if (end > start)
        {
            RawRegions!.Add(start);
            RawRegions.Add(end);
        }
    }

    /// <summary>
    /// 处理新文本。<paramref name="text"/> 覆盖源码区间 [<paramref name="textStart"/>, 源码末尾)，
    /// 且 <paramref name="textStart"/> 不大于 <see cref="FrozenEnd"/>。返回新的冻结边界。
    /// </summary>
    public int Advance(ReadOnlySpan<char> text, int textStart)
    {
        if (textStart > _scanPos)
        {
            throw new ArgumentOutOfRangeException(nameof(textStart));
        }
        var end = textStart + text.Length;
        while (_scanPos < end)
        {
            var rel = _scanPos - textStart;
            var newline = text[rel..].IndexOf('\n');
            if (newline < 0)
            {
                break;
            }
            var line = text.Slice(rel, newline);
            if (line.EndsWith("\r"))
            {
                line = line[..^1];
            }
            ProcessLine(line, _scanPos);
            _scanPos += newline + 1;
        }

        // 尚未结束的最后一行：只用来判断是否可以提前提交边界，不更新扫描状态
        if (_scanPos < end && (_pendingBoundary >= 0 || _afterClosedBlock) && !InOpaqueBlock)
        {
            var partial = text[(_scanPos - textStart)..];
            if (IsDecisive(partial))
            {
                TryCommit(partial, _pendingBoundary >= 0 ? _pendingBoundary : _scanPos, afterBlank: _pendingBoundary >= 0);
            }
        }
        return FrozenEnd;
    }

    /// <summary>
    /// 当前未冻结部分是否只是一个以开头行开始、尚未闭合的围栏代码块（流式代码块的快速路径）。
    /// </summary>
    /// <remarks>
    /// <paramref name="partialLine"/> 为尚未结束的最后一行：如果它已经构成结束围栏，代码块其实已经闭合。
    /// </remarks>
    public bool TryGetOpenFence(ReadOnlySpan<char> partialLine, out int fenceStart, out int contentStart, out int indent, out string? info)
    {
        if (_fenceChar is '`' or '~' && _fenceStartsRun && _fenceContentStart >= 0 && (partialLine.IsEmpty || !IsClosingFence(partialLine)))
        {
            fenceStart = _fenceStart;
            contentStart = _fenceContentStart;
            indent = _fenceIndent;
            info = _fenceInfo;
            return true;
        }
        fenceStart = -1;
        contentStart = -1;
        indent = 0;
        info = null;
        return false;
    }

    private bool InOpaqueBlock => _fenceChar != '\0' || _inDollarMath || _inBracketMath || _html != HtmlKind.None;

    private void ProcessLine(ReadOnlySpan<char> line, int lineStart)
    {
        var lineEnd = lineStart + line.Length + 1;
        if (_fenceChar != '\0')
        {
            if (IsClosingFence(line))
            {
                _afterClosedBlock = _fenceTopLevel;
                ResetFence();
            }
            return;
        }
        if (_inDollarMath)
        {
            if (line.Trim() is "$$")
            {
                _inDollarMath = false;
                _afterClosedBlock = _mathTopLevel;
            }
            return;
        }
        if (_inBracketMath)
        {
            if (line.Contains(@"\]", StringComparison.Ordinal))
            {
                _inBracketMath = false;
                _afterClosedBlock = _mathTopLevel;
            }
            return;
        }
        if (_html != HtmlKind.None)
        {
            if (EndsHtmlBlock(line))
            {
                if (_html == HtmlKind.RawComponent && _rawRegionStart >= 0)
                {
                    AddRawRegion(_rawRegionStart, lineStart + line.TrimEnd().Length - (_htmlRawTag!.Length + 3));
                    _rawRegionStart = -1;
                }
                _html = HtmlKind.None;
                _htmlRawTag = null;
            }
            return;
        }

        if (line.IsWhiteSpace())
        {
            if (_runHasContent)
            {
                _pendingBoundary = lineEnd;
            }
            _afterClosedBlock = false;
            return;
        }

        if (_pendingBoundary >= 0 || _afterClosedBlock)
        {
            TryCommit(line, _pendingBoundary >= 0 ? _pendingBoundary : lineStart, afterBlank: _pendingBoundary >= 0);
            _pendingBoundary = -1;
            _afterClosedBlock = false;
        }

        var runWasEmpty = !_runHasContent;
        _runHasContent = true;
        AnalyzeLine(line, lineStart, lineEnd, runWasEmpty);
    }

    private void AnalyzeLine(ReadOnlySpan<char> line, int lineStart, int lineEnd, bool startsRun)
    {
        var indent = LeadingSpaces(line);
        var content = line[Math.Min(indent, line.Length)..];
        var inQuote = false;
        while (content.Length > 0 && content[0] == '>')
        {
            inQuote = true;
            content = content[1..].TrimStart(' ');
        }
        var topLevel = indent <= 3 && !inQuote && !_runHasListItem;

        if (indent <= 3 && !inQuote && IsDefinitionLine(content))
        {
            SawDefinition = true;
        }

        // 围栏检测是保守的：任何缩进、引用内的围栏都算，以免在代码块中间切开
        var fenceText = content.TrimStart();
        if (TryOpenFence(fenceText, out var fenceChar, out var fenceLength, out var info))
        {
            _fenceChar = fenceChar;
            _fenceLength = fenceLength;
            _fenceTopLevel = topLevel && indent <= 3;
            _fenceIndent = indent;
            _fenceInfo = info;
            _fenceStartsRun = startsRun && topLevel;
            _fenceContentStart = lineEnd;
            _fenceStart = lineStart + Math.Min(indent, line.Length);
            return;
        }

        var trimmed = content.Trim();
        if (trimmed is "$$")
        {
            _inDollarMath = true;
            _mathTopLevel = topLevel;
            return;
        }
        if (trimmed.StartsWith(@"\[") && !trimmed[2..].Contains(@"\]", StringComparison.Ordinal))
        {
            _inBracketMath = true;
            _mathTopLevel = topLevel;
            return;
        }

        if (indent <= 3 && TryOpenHtmlBlock(content))
        {
            // 原样组件的内容从开始标签之后算起；标签读不完整时解析器也不会把它当组件，不记录
            if (_html == HtmlKind.RawComponent && RawRegions is not null && HtmlTagReader.TryRead(content, out var tag) && !tag.IsClosing)
            {
                _rawRegionStart = lineStart + (line.Length - content.Length) + tag.Length;
                if (EndsHtmlBlock(content[1..]))
                {
                    AddRawRegion(_rawRegionStart, lineStart + line.TrimEnd().Length - (_htmlRawTag!.Length + 3));
                    _rawRegionStart = -1;
                }
            }
            if (EndsHtmlBlock(content[1..]))
            {
                _html = HtmlKind.None;
                _htmlRawTag = null;
            }
            return;
        }

        if (indent <= 3 && !inQuote && IsListMarker(content))
        {
            _runHasListItem = true;
            return;
        }

        if (topLevel && IsAtxHeading(content))
        {
            _afterClosedBlock = true;
        }
    }

    private void TryCommit(ReadOnlySpan<char> nextLine, int boundary, bool afterBlank)
    {
        if (!CanCut(nextLine, afterBlank) || boundary <= FrozenEnd)
        {
            return;
        }
        FrozenEnd = boundary;
        _runHasContent = false;
        _runHasListItem = false;
        _pendingBoundary = -1;
        _afterClosedBlock = false;
    }

    private bool CanCut(ReadOnlySpan<char> next, bool afterBlank)
    {
        if (SawDefinition)
        {
            return false;
        }
        // 下一行有缩进：可能是列表项续行、缩进代码块或脚注续行
        if (next.Length == 0 || char.IsWhiteSpace(next[0]))
        {
            return false;
        }
        if (afterBlank && _runHasListItem && IsListMarker(next))
        {
            return false;
        }
        // 定义列表的释义行（": …"）可以与术语之间隔着空行
        if (next[0] == ':')
        {
            return false;
        }
        if (!afterBlank)
        {
            // 紧跟在闭合块之后：可能被 setext 下划线或懒续行改变的情况一律不切
            var trimmed = next.Trim();
            if (trimmed.Length > 0 && (trimmed[0] is '=' or '-') && trimmed.Trim(trimmed[0]).IsEmpty)
            {
                return false;
            }
        }
        if (IsDefinitionLine(next))
        {
            return false;
        }
        return true;
    }

    // 未结束的行是否已经足以判断：首字符既不是空白，也不是可能构成列表标记 / 定义的字符
    private static bool IsDecisive(ReadOnlySpan<char> partial)
    {
        if (partial.IsEmpty || char.IsWhiteSpace(partial[0]))
        {
            return false;
        }
        if (partial[0] is ':')
        {
            return false;
        }
        if (partial[0] is '-' or '*' or '+' or '[' or '=' or (>= '0' and <= '9'))
        {
            return partial.Length >= 16;
        }
        return true;
    }

    private bool IsClosingFence(ReadOnlySpan<char> line)
    {
        var text = line.TrimStart();
        while (text.Length > 0 && text[0] == '>')
        {
            text = text[1..].TrimStart();
        }
        var count = 0;
        while (count < text.Length && text[count] == _fenceChar)
        {
            count++;
        }
        return count >= _fenceLength && text[count..].IsWhiteSpace();
    }

    private void ResetFence()
    {
        _fenceChar = '\0';
        _fenceLength = 0;
        _fenceInfo = null;
        _fenceContentStart = -1;
        _fenceStart = -1;
        _fenceStartsRun = false;
    }

    private static bool TryOpenFence(ReadOnlySpan<char> text, out char fenceChar, out int length, out string? info)
    {
        fenceChar = '\0';
        length = 0;
        info = null;
        if (text.Length < 3 || text[0] is not ('`' or '~' or ':'))
        {
            return false;
        }
        var c = text[0];
        var count = 0;
        while (count < text.Length && text[count] == c)
        {
            count++;
        }
        if (count < 3)
        {
            return false;
        }
        var rest = text[count..].Trim();
        if (c == '`' && rest.Contains('`'))
        {
            return false;
        }
        fenceChar = c;
        length = count;
        info = rest.IsEmpty ? null : rest.ToString();
        return true;
    }

    private bool TryOpenHtmlBlock(ReadOnlySpan<char> content)
    {
        if (content.Length < 2 || content[0] != '<')
        {
            return false;
        }
        if (content.StartsWith("<!--"))
        {
            _html = HtmlKind.Comment;
            return true;
        }
        if (content.StartsWith("<?"))
        {
            _html = HtmlKind.Processing;
            return true;
        }
        if (content.StartsWith("<![CDATA["))
        {
            _html = HtmlKind.CData;
            return true;
        }
        if (content[1] == '!' && content.Length > 2 && char.IsAsciiLetter(content[2]))
        {
            _html = HtmlKind.Declaration;
            return true;
        }
        foreach (var tag in _rawComponentTags)
        {
            if (content.Length > tag.Length && content[1..].StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                var after = content.Length > tag.Length + 1 ? content[tag.Length + 1] : ' ';
                if (after is ' ' or '\t' or '>' or '/')
                {
                    _html = HtmlKind.RawComponent;
                    _htmlRawTag = tag;
                    return true;
                }
            }
        }
        foreach (var tag in _containerTags)
        {
            if (content.Length > tag.Length && content[1..].StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                var after = content.Length > tag.Length + 1 ? content[tag.Length + 1] : ' ';
                if (after is ' ' or '\t' or '>' or '/')
                {
                    _html = HtmlKind.Container;
                    _htmlRawTag = tag;
                    _containerDepth = 0;
                    return true;
                }
            }
        }
        foreach (var tag in RawTags)
        {
            if (content.Length > tag.Length && content[1..].StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                var after = content.Length > tag.Length + 1 ? content[tag.Length + 1] : ' ';
                if (after is ' ' or '\t' or '>')
                {
                    _html = HtmlKind.Raw;
                    _htmlRawTag = tag;
                    return true;
                }
            }
        }
        return false;
    }

    private static readonly string[] RawTags = ["script", "pre", "style", "textarea"];

    private bool EndsHtmlBlock(ReadOnlySpan<char> line) => _html switch
    {
        HtmlKind.Comment => line.Contains("-->", StringComparison.Ordinal),
        HtmlKind.Processing => line.Contains("?>", StringComparison.Ordinal),
        HtmlKind.Declaration => line.Contains('>'),
        HtmlKind.CData => line.Contains("]]>", StringComparison.Ordinal),
        HtmlKind.Raw => line.Contains("</" + _htmlRawTag, StringComparison.OrdinalIgnoreCase),
        HtmlKind.Container => ContainerEnds(line),
        HtmlKind.RawComponent => EndsWithClosingTag(line.TrimEnd()),
        _ => true,
    };

    // 解析器只认行尾的结束标签（独占一行或跟在内容后）：切分器结束得不能比解析器早，否则会在仍开着的组件里冻结
    private bool EndsWithClosingTag(ReadOnlySpan<char> line) =>
        line.Length >= _htmlRawTag!.Length + 3
        && line[^1] == '>'
        && line[..^1].EndsWith(_htmlRawTag, StringComparison.OrdinalIgnoreCase)
        && line[..^(_htmlRawTag.Length + 1)].EndsWith("</", StringComparison.Ordinal);

    // 统计同名开 / 闭标签（不含行首的开始标签之前的状态），深度回到 0 以下时容器结束
    private bool ContainerEnds(ReadOnlySpan<char> line)
    {
        var open = "<" + _htmlRawTag;
        var close = "</" + _htmlRawTag;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] != '<')
            {
                continue;
            }
            var rest = line[i..];
            if (rest.StartsWith(close, StringComparison.OrdinalIgnoreCase))
            {
                if (_containerDepth == 0)
                {
                    return true;
                }
                _containerDepth--;
            }
            else if (rest.StartsWith(open, StringComparison.OrdinalIgnoreCase) && rest.Length > open.Length && rest[open.Length] is ' ' or '>' or '\t')
            {
                _containerDepth++;
            }
        }
        return false;
    }

    private static int LeadingSpaces(ReadOnlySpan<char> line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c == ' ')
            {
                count++;
            }
            else if (c == '\t')
            {
                count += 4;
            }
            else
            {
                break;
            }
        }
        return count;
    }

    internal static bool IsListMarker(ReadOnlySpan<char> content)
    {
        if (content.IsEmpty)
        {
            return false;
        }
        if (content[0] is '-' or '*' or '+')
        {
            return content.Length == 1 || content[1] is ' ' or '\t';
        }
        var digits = 0;
        while (digits < content.Length && digits < 10 && char.IsAsciiDigit(content[digits]))
        {
            digits++;
        }
        if (digits == 0 || digits >= content.Length || content[digits] is not ('.' or ')'))
        {
            return false;
        }
        return digits + 1 == content.Length || content[digits + 1] is ' ' or '\t';
    }

    private static bool IsAtxHeading(ReadOnlySpan<char> content)
    {
        var level = 0;
        while (level < content.Length && content[level] == '#')
        {
            level++;
        }
        return level is >= 1 and <= 6 && (level == content.Length || content[level] is ' ' or '\t');
    }

    // "[label]: …" 链接引用定义或 "[^label]: …" 脚注定义
    private static bool IsDefinitionLine(ReadOnlySpan<char> content)
    {
        if (content.Length < 4 || content[0] != '[')
        {
            return false;
        }
        var close = content.IndexOf(']');
        return close > 1 && close + 1 < content.Length && content[close + 1] == ':';
    }
}
