using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using StreamMark.Core.Html;
using StreamMark.Core.Model;

namespace StreamMark.Core.Parsing;

/// <summary>
/// 零 AST 单遍行内解析器：直接将源码 Span 扫入可复用的扁平值类型双向链表（<see cref="InlineTape"/>），
/// 完成 CommonMark §6.2 强调（含 CJK-Friendly）、GFM 删除线、数学公式、链接/图片/引用、自动链接、
/// HTML 实体、Emoji 短码、行内 HTML 与白名单组件、自定义 <see cref="IInlineSyntaxRule"/> 解析，
/// 最后一次线性遍历直出不可变 <see cref="InlineContent"/>。
/// </summary>
internal sealed class InlineParser
{
    private const int IncompleteLinkSentinel = -2;

    // 默认 SIMD 停止字符集：http:// / https:// / ftp:// / mailto: 通过已包含的 ':' 反向回看匹配，
    // 不需要把高频字母 h/f/m 加入停止集，避免含 URL 的英文段落 SIMD 退化
    private static readonly SearchValues<char> s_baseTriggers =
        SearchValues.Create("\\`*_~[]!<&$:\n\r");

    // 仅当段落包含 "www." 时追加 w/W 停止字符
    private static readonly SearchValues<char> s_wwwTriggers =
        SearchValues.Create("\\`*_~[]!<&$:\n\rwW");

    private readonly InlineTape _tape = new();
    private DelimiterEntry[] _delims = new DelimiterEntry[32];
    private int _delimCount;
    private BracketEntry[] _brackets = new BracketEntry[16];
    private int _bracketCount;

    // 池化对象/链接/组件表（仅单次 Parse 期间使用）
    private readonly List<LinkTarget> _pendingLinks = [];
    private readonly List<PendingObject> _pendingObjects = [];
    private readonly List<InlineComponent> _pendingComponents = [];
    private readonly List<(string Name, int OpenNodeIndex)> _openComponentTags = [];

    // 反引号 O(N) 防退化缓存：记录本段中已确认无匹配的关闭反引号长度（最多缓存 1..32）
    private uint _unmatchedBacktickMask;

    // 输出直出构建器（复用 StringBuilder 与列表）
    private readonly StringBuilder _outText = new();
    private readonly List<InlineSpan> _outSpans = [];
    private readonly List<LinkTarget> _outLinks = [];
    private readonly List<InlineObject> _outObjects = [];
    private readonly List<InlineComponent> _outComponents = [];
    private readonly List<HtmlTextStyle> _outTextStyles = [];
    private readonly List<(string Name, InlineStyle Style, int Link, int TextStyle, HtmlTextStyle Current)> _htmlStack = [];
    private readonly List<InlineStyle> _styleStack = [];
    private readonly List<int> _linkStack = [];
    private readonly List<int> _componentStack = [];

    private readonly record struct PendingObject(
        InlineObjectKind Kind,
        string Value,
        string? Alt,
        string? Title,
        bool Flag,
        ComponentAttributes Attributes);

    /// <summary>解析一段行内源码（可含多行），直接产出不可变 <see cref="InlineContent"/>。</summary>
    public InlineContent Parse(
        ReadOnlySpan<char> input,
        MarkdownProfile profile,
        ReferenceRegistry? references,
        int topKey = -1)
    {
        if (input.IsEmpty)
        {
            return InlineContent.Empty;
        }
        // 去掉整段尾部多余的空格/制表符（保留前导空白已在块级剥离，这里也做兜底）
        var trimmed = TrimParagraphOuter(input);
        if (trimmed.IsEmpty)
        {
            return InlineContent.Empty;
        }

        _tape.Reset();
        _delimCount = 0;
        _bracketCount = 0;
        _pendingLinks.Clear();
        _pendingObjects.Clear();
        _pendingComponents.Clear();
        _openComponentTags.Clear();
        _unmatchedBacktickMask = 0;

        var hasWwwAutolink = profile.EnableAutoLinks
            && trimmed.Contains("www.", StringComparison.OrdinalIgnoreCase);
        var hasSchemeAutolink = profile.EnableAutoLinks
            && (trimmed.Contains("://", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("mailto:", StringComparison.OrdinalIgnoreCase));

        var triggers = profile.GetInlineTriggers(hasWwwAutolink, s_baseTriggers, s_wwwTriggers);

        ScanRange(trimmed, profile, references, topKey, hasSchemeAutolink, hasWwwAutolink, triggers);
        InlineDelimiterProcessor.ProcessEmphasis(_tape, _delims, ref _delimCount, 0);
        return EmitContent(profile);
    }

    private static ReadOnlySpan<char> TrimParagraphOuter(ReadOnlySpan<char> input)
    {
        var start = 0;
        while (start < input.Length && input[start] is ' ' or '\t')
        {
            start++;
        }
        var end = input.Length;
        while (end > start && input[end - 1] is ' ' or '\t')
        {
            end--;
        }
        return input.Slice(start, end - start);
    }

    private void ScanRange(
        ReadOnlySpan<char> input,
        MarkdownProfile profile,
        ReferenceRegistry? references,
        int topKey,
        bool hasSchemeAutolink,
        bool hasWwwAutolink,
        SearchValues<char> triggers)
    {
        var pos = 0;
        var plainRunLen = 0;
        var hasCustomRules = !profile.InlineRules.IsEmpty;

        while (pos < input.Length)
        {
            var next = input[pos..].IndexOfAny(triggers);
            if (next < 0)
            {
                _tape.AppendText(input[pos..]);
                break;
            }
            if (next > 0)
            {
                _tape.AppendText(input.Slice(pos, next));
                pos += next;
                plainRunLen += next;
            }

            var ch = input[pos];

            // 自定义行内规则优先于内置规则
            if (hasCustomRules && TryMatchCustomInlineRule(input, ref pos, ch, profile, references, topKey, hasSchemeAutolink, hasWwwAutolink, triggers))
            {
                plainRunLen = 0;
                continue;
            }

            switch (ch)
            {
                case '\\':
                    plainRunLen = 0;
                    HandleBackslash(input, ref pos, profile);
                    break;
                case '`':
                    plainRunLen = 0;
                    HandleBacktick(input, ref pos);
                    break;
                case '*':
                case '_':
                    plainRunLen = 0;
                    HandleDelimiter(input, ref pos, ch, profile.EnableCjkFriendlyEmphasis);
                    break;
                case '~':
                    plainRunLen = 0;
                    if (profile.EnableStrikethrough)
                    {
                        HandleDelimiter(input, ref pos, '~', profile.EnableCjkFriendlyEmphasis);
                    }
                    else
                    {
                        _tape.AppendText(input.Slice(pos, 1));
                        pos++;
                    }
                    break;
                case '!':
                    plainRunLen = 0;
                    if (pos + 1 < input.Length && input[pos + 1] == '[')
                    {
                        var idx = _tape.AppendText(input.Slice(pos, 2));
                        PushBracket(idx, pos, isImage: true);
                        pos += 2;
                    }
                    else
                    {
                        _tape.AppendText(input.Slice(pos, 1));
                        pos++;
                    }
                    break;
                case '[':
                    {
                        plainRunLen = 0;
                        var idx = _tape.AppendText(input.Slice(pos, 1));
                        PushBracket(idx, pos, isImage: false);
                        pos++;
                        break;
                    }
                case ']':
                    plainRunLen = 0;
                    HandleCloseBracket(input, ref pos, profile, references, topKey);
                    break;
                case '<':
                    plainRunLen = 0;
                    HandleAngleBracket(input, ref pos, profile);
                    break;
                case '&':
                    plainRunLen = 0;
                    if (HtmlEntityTable.TryDecode(input[pos..], out var decoded, out var consumed))
                    {
                        _tape.AppendText(decoded.AsSpan());
                        pos += consumed;
                    }
                    else
                    {
                        _tape.AppendText(input.Slice(pos, 1));
                        pos++;
                    }
                    break;
                case '$':
                    plainRunLen = 0;
                    if (profile.EnableMathematics && TryHandleDollarMath(input, ref pos))
                    {
                        break;
                    }
                    _tape.AppendText(input.Slice(pos, 1));
                    pos++;
                    break;
                case ':':
                    if (hasSchemeAutolink && TryMatchSchemeAutolinkAtColon(input, ref pos, plainRunLen))
                    {
                        plainRunLen = 0;
                        break;
                    }
                    plainRunLen = 0;
                    if (profile.EnableEmojiShortcodes && EmojiShortcodeTable.TryMatch(input[pos..], out var emoji, out var emojiConsumed))
                    {
                        _tape.AppendText(emoji.AsSpan());
                        pos += emojiConsumed;
                    }
                    else
                    {
                        _tape.AppendText(input.Slice(pos, 1));
                        pos++;
                    }
                    break;
                case '\r':
                    plainRunLen = 0;
                    pos++;
                    if (pos < input.Length && input[pos] == '\n')
                    {
                        HandleNewline(input, ref pos);
                    }
                    else
                    {
                        HandleNewlineCore(input, ref pos);
                    }
                    break;
                case '\n':
                    plainRunLen = 0;
                    HandleNewline(input, ref pos);
                    break;
                default:
                    if (hasWwwAutolink && ch is 'w' or 'W'
                        && InlineLinkAndAutolink.TryMatchBareAutolink(input, pos, out var display, out var url, out var urlLen))
                    {
                        plainRunLen = 0;
                        var linkIndex = _pendingLinks.Count;
                        _pendingLinks.Add(new LinkTarget(url, null, IsAutoLink: true));
                        var (s, l) = _tape.StoreText(display.AsSpan());
                        _tape.Append(new InlineNode { Kind = InlineNodeKind.Autolink, Start = s, Length = l, Extra = linkIndex, Flag = true });
                        pos += urlLen;
                    }
                    else
                    {
                        _tape.AppendText(input.Slice(pos, 1));
                        pos++;
                        plainRunLen++;
                    }
                    break;
            }
        }
    }

    // 命中 ':' 时向前回看紧邻的连续普通文本是否为 http/https/ftp/mailto 协议前缀，避免把 h/f/m 放进 SIMD 停止集
    private bool TryMatchSchemeAutolinkAtColon(ReadOnlySpan<char> input, ref int pos, int plainRunLen)
    {
        if (plainRunLen < 3)
        {
            return false;
        }
        var schemeLen = 0;
        if (pos + 2 < input.Length && input[pos + 1] == '/' && input[pos + 2] == '/')
        {
            if (plainRunLen >= 5 && pos >= 5 && input.Slice(pos - 5, 5).Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                schemeLen = 5;
            }
            else if (plainRunLen >= 4 && pos >= 4 && input.Slice(pos - 4, 4).Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                schemeLen = 4;
            }
            else if (plainRunLen >= 3 && pos >= 3 && input.Slice(pos - 3, 3).Equals("ftp", StringComparison.OrdinalIgnoreCase))
            {
                schemeLen = 3;
            }
        }
        else if (plainRunLen >= 6 && pos >= 6 && input.Slice(pos - 6, 6).Equals("mailto", StringComparison.OrdinalIgnoreCase))
        {
            schemeLen = 6;
        }
        if (schemeLen == 0)
        {
            return false;
        }
        var schemeStart = pos - schemeLen;
        if (!InlineLinkAndAutolink.TryMatchBareAutolink(input, schemeStart, out var display, out var url, out var urlLen))
        {
            return false;
        }
        TrimTailPlainText(schemeLen);
        var linkIndex = _pendingLinks.Count;
        _pendingLinks.Add(new LinkTarget(url, null, IsAutoLink: true));
        var (s, l) = _tape.StoreText(display.AsSpan());
        _tape.Append(new InlineNode { Kind = InlineNodeKind.Autolink, Start = s, Length = l, Extra = linkIndex, Flag = true });
        pos = schemeStart + urlLen;
        return true;
    }

    private void TrimTailPlainText(int count)
    {
        var remaining = count;
        while (remaining > 0 && _tape.Tail >= 0)
        {
            var tailIdx = _tape.Tail;
            var node = _tape.Nodes[tailIdx];
            if (node.Kind != InlineNodeKind.Text)
            {
                break;
            }
            if (node.Length <= remaining)
            {
                remaining -= node.Length;
                _tape.Remove(tailIdx);
            }
            else
            {
                _tape.ShrinkTextRight(tailIdx, remaining);
                remaining = 0;
            }
        }
    }

    private bool TryMatchCustomInlineRule(
        ReadOnlySpan<char> input,
        ref int pos,
        char ch,
        MarkdownProfile profile,
        ReferenceRegistry? references,
        int topKey,
        bool hasSchemeAutolink,
        bool hasWwwAutolink,
        SearchValues<char> triggers)
    {
        var rules = profile.InlineRules;
        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule.OpeningCharacters.IndexOf(ch) < 0)
            {
                continue;
            }
            var ctx = new RuleInlineSyntaxContext(this, profile, references, topKey, hasSchemeAutolink, hasWwwAutolink, triggers);
            if (rule.TryMatch(input[pos..], ctx, out var consumed) && consumed > 0)
            {
                pos += Math.Min(consumed, input.Length - pos);
                return true;
            }
        }
        return false;
    }

    private void HandleBackslash(ReadOnlySpan<char> input, ref int pos, MarkdownProfile profile)
    {
        if (pos + 1 >= input.Length)
        {
            _tape.AppendText(input.Slice(pos, 1));
            pos++;
            return;
        }
        var next = input[pos + 1];

        // 反斜杠数学定界符 \(...\) 与 \[...\]（排在转义之前，与 BackslashMathInlineParser 一致）
        if (profile.EnableMathematics && next is '(' or '[')
        {
            var closeChar = next == '(' ? ')' : ']';
            var contentStart = pos + 2;
            var closePos = -1;
            for (var i = contentStart; i + 1 < input.Length; i++)
            {
                var c = input[i];
                if (c is '\r' or '\n' && closeChar == ')')
                {
                    break;
                }
                if (c == '\\' && input[i + 1] == closeChar)
                {
                    closePos = i;
                    break;
                }
            }
            if (closePos >= 0)
            {
                var content = input.Slice(contentStart, closePos - contentStart);
                var (s, l) = _tape.StoreText(content);
                _tape.Append(new InlineNode { Kind = InlineNodeKind.Math, Start = s, Length = l, Flag = closeChar == ']' });
                pos = closePos + 2;
                return;
            }
        }

        // 反斜杠硬换行
        if (next == '\n' || (next == '\r' && pos + 2 < input.Length && input[pos + 2] == '\n'))
        {
            pos += next == '\r' ? 3 : 2;
            while (pos < input.Length && input[pos] is ' ' or '\t')
            {
                pos++;
            }
            _tape.Append(new InlineNode { Kind = InlineNodeKind.HardBreak });
            return;
        }

        // ASCII 标点转义
        if (UnicodeClassification.IsAsciiPunctuation(next))
        {
            _tape.AppendText(input.Slice(pos + 1, 1));
            pos += 2;
            return;
        }

        _tape.AppendText(input.Slice(pos, 1));
        pos++;
    }

    private void HandleBacktick(ReadOnlySpan<char> input, ref int pos)
    {
        var start = pos;
        while (pos < input.Length && input[pos] == '`')
        {
            pos++;
        }
        var tickCount = pos - start;

        // 已知此长度无匹配关闭反引号，直接按字面输出
        if (tickCount <= 32 && (_unmatchedBacktickMask & (1u << (tickCount - 1))) != 0)
        {
            _tape.AppendText(input.Slice(start, tickCount));
            return;
        }

        var search = pos;
        while (search < input.Length)
        {
            var nextTick = input[search..].IndexOf('`');
            if (nextTick < 0)
            {
                break;
            }
            var runStart = search + nextTick;
            var runEnd = runStart;
            while (runEnd < input.Length && input[runEnd] == '`')
            {
                runEnd++;
            }
            if (runEnd - runStart == tickCount)
            {
                var raw = input.Slice(pos, runStart - pos);
                AppendCodeSpan(raw);
                pos = runEnd;
                return;
            }
            search = runEnd;
        }

        if (tickCount <= 32)
        {
            _unmatchedBacktickMask |= 1u << (tickCount - 1);
        }
        _tape.AppendText(input.Slice(start, tickCount));
    }

    private void AppendCodeSpan(ReadOnlySpan<char> raw)
    {
        // CommonMark §6.1: 换行转为空格；若非全空且首尾皆空格则首尾各剥去一个空格
        if (raw.IsEmpty)
        {
            _tape.Append(new InlineNode { Kind = InlineNodeKind.Code, Start = 0, Length = 0 });
            return;
        }
        var hasNewline = raw.IndexOfAny('\n', '\r') >= 0;
        if (!hasNewline)
        {
            if (raw.Length >= 2 && raw[0] == ' ' && raw[^1] == ' ' && !IsAllSpaces(raw))
            {
                raw = raw.Slice(1, raw.Length - 2);
            }
            var (s, l) = _tape.StoreText(raw);
            _tape.Append(new InlineNode { Kind = InlineNodeKind.Code, Start = s, Length = l });
            return;
        }

        Span<char> stackBuf = raw.Length <= 256 ? stackalloc char[raw.Length] : [];
        var rented = raw.Length > 256 ? new char[raw.Length] : null;
        var dst = rented is not null ? rented.AsSpan() : stackBuf;
        var len = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '\r')
            {
                if (i + 1 < raw.Length && raw[i + 1] == '\n')
                {
                    i++;
                }
                dst[len++] = ' ';
            }
            else if (c == '\n')
            {
                dst[len++] = ' ';
            }
            else
            {
                dst[len++] = c;
            }
        }
        var normalized = dst[..len];
        if (normalized.Length >= 2 && normalized[0] == ' ' && normalized[^1] == ' ' && !IsAllSpaces(normalized))
        {
            normalized = normalized.Slice(1, normalized.Length - 2);
        }
        var (start, outLen) = _tape.StoreText(normalized);
        _tape.Append(new InlineNode { Kind = InlineNodeKind.Code, Start = start, Length = outLen });
    }

    private static bool IsAllSpaces(ReadOnlySpan<char> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] != ' ')
            {
                return false;
            }
        }
        return true;
    }

    private void HandleDelimiter(ReadOnlySpan<char> input, ref int pos, char ch, bool cjkFriendly)
    {
        var start = pos;
        while (pos < input.Length && input[pos] == ch)
        {
            pos++;
        }
        var count = pos - start;
        var nodeIdx = _tape.AppendText(input.Slice(start, count));

        InlineDelimiterProcessor.ClassifyFlanking(input, start, count, ch, cjkFriendly, out var canOpen, out var canClose);
        if (!canOpen && !canClose)
        {
            return;
        }

        if (_delimCount >= _delims.Length)
        {
            Array.Resize(ref _delims, _delims.Length * 2);
        }
        _delims[_delimCount++] = new DelimiterEntry
        {
            NodeIndex = nodeIdx,
            Char = ch,
            Count = count,
            OriginalCount = count,
            CanOpen = canOpen,
            CanClose = canClose,
            Active = true,
        };
    }

    private void PushBracket(int nodeIdx, int sourcePos, bool isImage)
    {
        if (_bracketCount >= _brackets.Length)
        {
            Array.Resize(ref _brackets, _brackets.Length * 2);
        }
        _brackets[_bracketCount++] = new BracketEntry
        {
            NodeIndex = nodeIdx,
            SourcePosition = sourcePos,
            DelimiterStackIndex = _delimCount,
            IsImage = isImage,
            Active = true,
        };
    }

    private void HandleCloseBracket(
        ReadOnlySpan<char> input,
        ref int pos,
        MarkdownProfile profile,
        ReferenceRegistry? references,
        int topKey)
    {
        if (_bracketCount == 0)
        {
            _tape.AppendText(input.Slice(pos, 1));
            pos++;
            return;
        }

        var entry = _brackets[--_bracketCount];
        if (!entry.Active)
        {
            _tape.AppendText(input.Slice(pos, 1));
            pos++;
            return;
        }

        var labelStart = entry.SourcePosition + (entry.IsImage ? 2 : 1);
        var rawInner = input.Slice(labelStart, pos - labelStart);

        // 1. 脚注引用 [^label]
        if (!entry.IsImage && profile.EnableFootnotes && rawInner.Length >= 2 && rawInner[0] == '^' && rawInner.IndexOfAny(" \t\r\n[]") < 0)
        {
            var fnRaw = rawInner[1..];
            var fnNorm = UnicodeClassification.NormalizeLabel(fnRaw);
            if (references is not null && references.TryGetFootnote(fnNorm, out var fnEntry))
            {
                RemoveNodesAfter(entry.NodeIndex);
                _delimCount = entry.DelimiterStackIndex;
                var linkIdx = _pendingLinks.Count;
                var labelWithCaret = fnEntry.RawLabel.StartsWith('^') ? fnEntry.RawLabel : "^" + fnEntry.RawLabel;
                _pendingLinks.Add(new LinkTarget("#fn:" + labelWithCaret, null, IsAutoLink: false));
                var (s, l) = _tape.StoreText(fnEntry.Order.ToString().AsSpan());
                _tape.Nodes[entry.NodeIndex].Kind = InlineNodeKind.FootnoteRef;
                _tape.Nodes[entry.NodeIndex].Start = s;
                _tape.Nodes[entry.NodeIndex].Length = l;
                _tape.Nodes[entry.NodeIndex].Extra = linkIdx;
                pos++;
                return;
            }
            references?.RecordUnresolvedFootnote(fnNorm, topKey);
        }

        var afterBracket = pos + 1;
        string? matchedUrl = null;
        string? matchedTitle = null;
        var matched = false;
        var newPos = afterBracket;

        // 2. 行内链接 / 图片 [text](dest "title")
        if (afterBracket < input.Length && input[afterBracket] == '('
            && InlineLinkAndAutolink.TryParseInlineLinkTail(input, afterBracket, out var inlineUrl, out var inlineTitle, out var inlineEnd))
        {
            matched = true;
            matchedUrl = inlineUrl;
            matchedTitle = inlineTitle;
            newPos = inlineEnd;
        }
        // 3. 完整引用 [text][label] 或折叠引用 [label][]
        else if (afterBracket < input.Length && input[afterBracket] == '[')
        {
            if (afterBracket + 1 < input.Length && input[afterBracket + 1] == ']')
            {
                // Collapsed reference [label][]
                var norm = UnicodeClassification.NormalizeLabel(rawInner);
                if (references is not null && references.TryGetLink(norm, out var linkRef))
                {
                    matched = true;
                    matchedUrl = linkRef.Url;
                    matchedTitle = linkRef.Title;
                    newPos = afterBracket + 2;
                }
                else
                {
                    references?.RecordUnresolvedLink(norm, topKey);
                }
            }
            else if (InlineLinkAndAutolink.TryParseLinkLabel(input, afterBracket, out var refLabel, out var refEnd))
            {
                // Full reference [text][label]
                var norm = UnicodeClassification.NormalizeLabel(refLabel);
                if (references is not null && references.TryGetLink(norm, out var linkRef))
                {
                    matched = true;
                    matchedUrl = linkRef.Url;
                    matchedTitle = linkRef.Title;
                    newPos = refEnd;
                }
                else
                {
                    references?.RecordUnresolvedLink(norm, topKey);
                }
            }
        }
        // 4. 快捷引用 [label]
        if (!matched && !(afterBracket < input.Length && input[afterBracket] == '['))
        {
            if (rawInner.Length <= 999 && HasValidShortcutLabel(rawInner))
            {
                var norm = UnicodeClassification.NormalizeLabel(rawInner);
                if (references is not null && references.TryGetLink(norm, out var linkRef))
                {
                    matched = true;
                    matchedUrl = linkRef.Url;
                    matchedTitle = linkRef.Title;
                    newPos = afterBracket;
                }
                else
                {
                    references?.RecordUnresolvedLink(norm, topKey);
                }
            }
        }

        if (!matched)
        {
            _tape.AppendText(input.Slice(pos, 1));
            pos++;
            return;
        }

        // 先结算括号内部的强调分隔符
        InlineDelimiterProcessor.ProcessEmphasis(_tape, _delims, ref _delimCount, entry.DelimiterStackIndex);

        if (entry.IsImage)
        {
            var alt = ExtractPlainTextAfter(entry.NodeIndex);
            RemoveNodesAfter(entry.NodeIndex);
            var objIdx = _pendingObjects.Count;
            _pendingObjects.Add(new PendingObject(InlineObjectKind.Image, matchedUrl ?? string.Empty, alt, matchedTitle, Flag: false, ComponentAttributes.Empty));
            _tape.Nodes[entry.NodeIndex].Kind = InlineNodeKind.Image;
            _tape.Nodes[entry.NodeIndex].Extra = objIdx;
            _tape.Nodes[entry.NodeIndex].Start = 0;
            _tape.Nodes[entry.NodeIndex].Length = 0;
        }
        else
        {
            var url = matchedUrl ?? string.Empty;
            if (url == Remend.Remend.IncompleteLinkUrl)
            {
                _tape.Nodes[entry.NodeIndex].Kind = InlineNodeKind.LinkOpen;
                _tape.Nodes[entry.NodeIndex].Extra = IncompleteLinkSentinel;
                _tape.Nodes[entry.NodeIndex].Start = 0;
                _tape.Nodes[entry.NodeIndex].Length = 0;
            }
            else
            {
                var linkIdx = _pendingLinks.Count;
                _pendingLinks.Add(new LinkTarget(url, string.IsNullOrEmpty(matchedTitle) ? null : matchedTitle, IsAutoLink: false));
                _tape.Nodes[entry.NodeIndex].Kind = InlineNodeKind.LinkOpen;
                _tape.Nodes[entry.NodeIndex].Extra = linkIdx;
                _tape.Nodes[entry.NodeIndex].Start = 0;
                _tape.Nodes[entry.NodeIndex].Length = 0;
            }
            _tape.Append(new InlineNode { Kind = InlineNodeKind.LinkClose });

            // CommonMark §6.5: 链接内部不允许再嵌套链接，使所有更早的非图片 '[' 失效
            for (var b = 0; b < _bracketCount; b++)
            {
                if (!_brackets[b].IsImage)
                {
                    _brackets[b].Active = false;
                }
            }
        }

        pos = newPos;
    }

    private static bool HasValidShortcutLabel(ReadOnlySpan<char> raw)
    {
        var hasNonWs = false;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '\\' && i + 1 < raw.Length)
            {
                hasNonWs = true;
                i++;
                continue;
            }
            if (c is '[' or ']')
            {
                return false;
            }
            if (c is not (' ' or '\t' or '\r' or '\n'))
            {
                hasNonWs = true;
            }
        }
        return hasNonWs;
    }

    private string ExtractPlainTextAfter(int openNodeIdx)
    {
        var sb = new StringBuilder();
        for (var idx = _tape.Nodes[openNodeIdx].Next; idx >= 0; idx = _tape.Nodes[idx].Next)
        {
            var node = _tape.Nodes[idx];
            switch (node.Kind)
            {
                case InlineNodeKind.Text:
                case InlineNodeKind.Code:
                case InlineNodeKind.Autolink:
                    sb.Append(_tape.GetText(node.Start, node.Length));
                    break;
                case InlineNodeKind.SoftBreak:
                    sb.Append(' ');
                    break;
                case InlineNodeKind.HardBreak:
                    sb.Append('\n');
                    break;
                case InlineNodeKind.Image:
                    if (_pendingObjects[node.Extra].Alt is { } nestedAlt)
                    {
                        sb.Append(nestedAlt);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    private void RemoveNodesAfter(int openNodeIdx)
    {
        var idx = _tape.Nodes[openNodeIdx].Next;
        while (idx >= 0)
        {
            var next = _tape.Nodes[idx].Next;
            _tape.Remove(idx);
            idx = next;
        }
    }

    private void HandleAngleBracket(ReadOnlySpan<char> input, ref int pos, MarkdownProfile profile)
    {
        var slice = input[pos..];

        // 1. 白名单行内组件（InlinePair / SelfClosing）优先于自动链接与原始 HTML
        if (HtmlTagReader.TryRead(slice, out var tag) && profile.Table.TryGet(tag.Name, out var spec) && spec.Kind != ComponentKind.Block)
        {
            if (tag.IsClosing)
            {
                if (spec.Kind == ComponentKind.InlinePair)
                {
                    var match = -1;
                    for (var i = _openComponentTags.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(_openComponentTags[i].Name, spec.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            match = i;
                            break;
                        }
                    }
                    if (match >= 0)
                    {
                        var openIdx = _openComponentTags[match].OpenNodeIndex;
                        _openComponentTags.RemoveRange(match, _openComponentTags.Count - match);
                        var openNode = _tape.Nodes[openIdx];
                        _tape.Append(new InlineNode { Kind = InlineNodeKind.ComponentClose, Style = openNode.Style, Extra = openNode.Extra });
                        pos += tag.Length;
                        return;
                    }
                }
                // 未匹配的关闭白名单标签：退化为普通 HTML 标签处理
            }
            else if (spec.Kind == ComponentKind.SelfClosing || tag.IsSelfClosing)
            {
                if (string.Equals(spec.Name, "img", StringComparison.OrdinalIgnoreCase))
                {
                    var objIdx = _pendingObjects.Count;
                    _pendingObjects.Add(new PendingObject(
                        InlineObjectKind.Image,
                        tag.Attributes["src"] ?? string.Empty,
                        tag.Attributes["alt"],
                        tag.Attributes["title"],
                        Flag: false,
                        tag.Attributes));
                    _tape.Append(new InlineNode { Kind = InlineNodeKind.Image, Extra = objIdx });
                }
                else
                {
                    var objIdx = _pendingObjects.Count;
                    _pendingObjects.Add(new PendingObject(
                        InlineObjectKind.Component,
                        spec.Name.ToLowerInvariant(),
                        Alt: null,
                        Title: null,
                        Flag: false,
                        tag.Attributes));
                    _tape.Append(new InlineNode { Kind = InlineNodeKind.ComponentLeaf, Extra = objIdx });
                }
                pos += tag.Length;
                return;
            }
            else
            {
                // InlinePair 开始标签
                if (ComponentSpec.StyleFor(spec.Name) is { } builtInStyle)
                {
                    var openNodeIdx = _tape.Append(new InlineNode { Kind = InlineNodeKind.ComponentOpen, Style = builtInStyle, Extra = -1 });
                    _openComponentTags.Add((spec.Name, openNodeIdx));
                }
                else
                {
                    var compIdx = _pendingComponents.Count;
                    _pendingComponents.Add(new InlineComponent(spec.Name.ToLowerInvariant(), tag.Attributes));
                    var openNodeIdx = _tape.Append(new InlineNode { Kind = InlineNodeKind.ComponentOpen, Style = InlineStyle.None, Extra = compIdx });
                    _openComponentTags.Add((spec.Name, openNodeIdx));
                }
                pos += tag.Length;
                return;
            }
        }

        // 2. CommonMark 尖括号自动链接 <https://...> / <a@b.c>
        if (InlineLinkAndAutolink.TryMatchAngleAutolink(slice, out var autoUrl, out var isEmail, out var autoLen))
        {
            var finalUrl = isEmail && !autoUrl.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? "mailto:" + autoUrl : autoUrl;
            var linkIdx = _pendingLinks.Count;
            _pendingLinks.Add(new LinkTarget(finalUrl, null, IsAutoLink: true));
            var (s, l) = _tape.StoreText(autoUrl.AsSpan());
            _tape.Append(new InlineNode { Kind = InlineNodeKind.Autolink, Start = s, Length = l, Extra = linkIdx, Flag = false });
            // CommonMark §6.5: 显式尖括号自动链接属于链接，使外层尚未闭合的非图片 '[' 失效，避免嵌套链接
            for (var b = 0; b < _bracketCount; b++)
            {
                if (!_brackets[b].IsImage)
                {
                    _brackets[b].Active = false;
                }
            }
            pos += autoLen;
            return;
        }

        // 3. CommonMark 原始行内 HTML 标签（<span style=...>, <strong>, <br>, <!--...-->, 未知标签）
        if (InlineLinkAndAutolink.TryMatchRawInlineHtml(slice, out var htmlLen))
        {
            var (s, l) = _tape.StoreText(slice[..htmlLen]);
            _tape.Append(new InlineNode { Kind = InlineNodeKind.HtmlTag, Start = s, Length = l });
            pos += htmlLen;
            return;
        }

        _tape.AppendText(input.Slice(pos, 1));
        pos++;
    }

    private bool TryHandleDollarMath(ReadOnlySpan<char> input, ref int pos)
    {
        // 统计连续 '$' 数量（最多处理 1 或 2）
        var start = pos;
        var count = 0;
        while (start + count < input.Length && input[start + count] == '$')
        {
            count++;
        }
        if (count > 2)
        {
            return false;
        }

        var contentStart = start + count;
        if (contentStart >= input.Length)
        {
            return false;
        }

        if (count == 2)
        {
            // 行内 $$...$$
            for (var i = contentStart; i + 1 < input.Length; i++)
            {
                if (input[i] == '\\' && i + 1 < input.Length)
                {
                    i++;
                    continue;
                }
                if (input[i] == '$' && input[i + 1] == '$')
                {
                    var content = input.Slice(contentStart, i - contentStart).Trim();
                    var (s, l) = _tape.StoreText(content);
                    _tape.Append(new InlineNode { Kind = InlineNodeKind.Math, Start = s, Length = l, Flag = true });
                    pos = i + 2;
                    return true;
                }
            }
            return false;
        }

        // 单个 $...$ 行内公式：紧随 '$' 后不可为空白，闭合 '$' 前不可为空白、后不可为数字或 '$'，且不跨行
        if (input[contentStart] is ' ' or '\t' or '\r' or '\n')
        {
            return false;
        }
        // 前导字符不能是数字（避免 100$...$ 误判）
        if (start > 0 && char.IsAsciiDigit(input[start - 1]))
        {
            return false;
        }
        for (var i = contentStart; i < input.Length; i++)
        {
            var c = input[i];
            if (c is '\r' or '\n')
            {
                return false;
            }
            if (c == '\\' && i + 1 < input.Length)
            {
                i++;
                continue;
            }
            if (c == '$')
            {
                if (i == contentStart)
                {
                    return false;
                }
                if (input[i - 1] is ' ' or '\t')
                {
                    return false;
                }
                if (i + 1 < input.Length && (char.IsAsciiDigit(input[i + 1]) || input[i + 1] == '$'))
                {
                    return false;
                }
                var content = input.Slice(contentStart, i - contentStart);
                var (s, l) = _tape.StoreText(content);
                _tape.Append(new InlineNode { Kind = InlineNodeKind.Math, Start = s, Length = l, Flag = false });
                pos = i + 1;
                return true;
            }
        }
        return false;
    }

    private void HandleNewline(ReadOnlySpan<char> input, ref int pos)
    {
        pos++; // consume '\n'
        HandleNewlineCore(input, ref pos);
    }

    private void HandleNewlineCore(ReadOnlySpan<char> input, ref int pos)
    {
        // CommonMark §6.7: 换行前 2+ 个空格为硬换行，否则为软换行；去掉行尾空格/制表符与下一行前导空格/制表符
        var trailingSpaces = 0;
        if (_tape.Tail >= 0 && _tape.Nodes[_tape.Tail].Kind == InlineNodeKind.Text)
        {
            ref var tail = ref _tape.Nodes[_tape.Tail];
            var text = _tape.GetText(tail.Start, tail.Length);
            var end = text.Length;
            while (end > 0 && text[end - 1] == ' ')
            {
                trailingSpaces++;
                end--;
            }
            while (end > 0 && text[end - 1] is ' ' or '\t')
            {
                end--;
            }
            tail.Length = end;
            if (tail.Length == 0)
            {
                _tape.Remove(_tape.Tail);
            }
        }

        while (pos < input.Length && input[pos] is ' ' or '\t')
        {
            pos++;
        }

        _tape.Append(new InlineNode
        {
            Kind = trailingSpaces >= 2 ? InlineNodeKind.HardBreak : InlineNodeKind.SoftBreak,
        });
    }

    // ---- 第二阶段：扁平链表一次线性遍历直出 InlineContent ----

    private InlineContent EmitContent(MarkdownProfile profile)
    {
        _outText.Clear();
        _outSpans.Clear();
        _outLinks.Clear();
        _outObjects.Clear();
        _outComponents.Clear();
        _outTextStyles.Clear();
        _htmlStack.Clear();
        _styleStack.Clear();
        _linkStack.Clear();
        _componentStack.Clear();

        var curStyle = InlineStyle.None;
        var curLink = -1;
        var curComp = -1;
        var softIsHard = profile.SoftlineBreakAsHardline;

        for (var idx = _tape.Head; idx >= 0; idx = _tape.Nodes[idx].Next)
        {
            var node = _tape.Nodes[idx];
            switch (node.Kind)
            {
                case InlineNodeKind.Text:
                    EmitText(_tape.GetText(node.Start, node.Length), curStyle, curLink, curComp);
                    break;
                case InlineNodeKind.Code:
                    EmitText(_tape.GetText(node.Start, node.Length), curStyle | InlineStyle.Code, curLink, curComp);
                    break;
                case InlineNodeKind.HardBreak:
                    EmitText("\n", curStyle, curLink, curComp);
                    break;
                case InlineNodeKind.SoftBreak:
                    if (softIsHard)
                    {
                        EmitText("\n", curStyle, curLink, curComp);
                    }
                    else if (_outText.Length > 0 && UnicodeClassification.IsCjkWide(_outText[^1]))
                    {
                        // CJK 软换行不插空格
                    }
                    else
                    {
                        EmitText(" ", curStyle, curLink, curComp);
                    }
                    break;
                case InlineNodeKind.Math:
                    EmitObject(InlineObjectKind.Math, _tape.GetText(node.Start, node.Length).ToString(), flag: node.Flag);
                    break;
                case InlineNodeKind.Autolink:
                    if (curLink >= 0 || curStyle.HasFlag(InlineStyle.IncompleteLink))
                    {
                        EmitText(_tape.GetText(node.Start, node.Length), curStyle, curLink, curComp);
                    }
                    else
                    {
                        var target = _pendingLinks[node.Extra];
                        var linkIndex = AddOutLink(target.Url, target.Title, target.IsAutoLink);
                        EmitText(_tape.GetText(node.Start, node.Length), curStyle | InlineStyle.Link, linkIndex, curComp);
                    }
                    break;
                case InlineNodeKind.FootnoteRef:
                    {
                        var target = _pendingLinks[node.Extra];
                        var linkIndex = AddOutLink(target.Url, target.Title, isAutoLink: false);
                        EmitText(_tape.GetText(node.Start, node.Length), curStyle | InlineStyle.FootnoteReference | InlineStyle.Superscript, linkIndex, curComp);
                        break;
                    }
                case InlineNodeKind.Image:
                    {
                        var obj = _pendingObjects[node.Extra];
                        EmitObject(InlineObjectKind.Image, obj.Value, obj.Alt, obj.Title, obj.Flag, obj.Attributes, curLink);
                        break;
                    }
                case InlineNodeKind.ComponentLeaf:
                    {
                        var obj = _pendingObjects[node.Extra];
                        EmitObject(obj.Kind, obj.Value, obj.Alt, obj.Title, obj.Flag, obj.Attributes, link: -1);
                        break;
                    }
                case InlineNodeKind.EmphasisOpen:
                    _styleStack.Add(curStyle);
                    curStyle |= node.Style;
                    break;
                case InlineNodeKind.EmphasisClose:
                    if (_styleStack.Count > 0)
                    {
                        curStyle = _styleStack[^1];
                        _styleStack.RemoveAt(_styleStack.Count - 1);
                    }
                    break;
                case InlineNodeKind.LinkOpen:
                    _styleStack.Add(curStyle);
                    _linkStack.Add(curLink);
                    if (node.Extra == IncompleteLinkSentinel)
                    {
                        curStyle |= InlineStyle.IncompleteLink;
                        curLink = -1;
                    }
                    else
                    {
                        var target = _pendingLinks[node.Extra];
                        curLink = AddOutLink(target.Url, target.Title, target.IsAutoLink);
                        curStyle |= InlineStyle.Link;
                    }
                    break;
                case InlineNodeKind.LinkClose:
                    if (_styleStack.Count > 0)
                    {
                        curStyle = _styleStack[^1];
                        _styleStack.RemoveAt(_styleStack.Count - 1);
                    }
                    if (_linkStack.Count > 0)
                    {
                        curLink = _linkStack[^1];
                        _linkStack.RemoveAt(_linkStack.Count - 1);
                    }
                    break;
                case InlineNodeKind.ComponentOpen:
                    if (node.Extra < 0)
                    {
                        _styleStack.Add(curStyle);
                        curStyle |= node.Style;
                    }
                    else
                    {
                        _componentStack.Add(curComp);
                        _outComponents.Add(_pendingComponents[node.Extra]);
                        curComp = _outComponents.Count - 1;
                    }
                    break;
                case InlineNodeKind.ComponentClose:
                    if (node.Extra < 0)
                    {
                        if (_styleStack.Count > 0)
                        {
                            curStyle = _styleStack[^1];
                            _styleStack.RemoveAt(_styleStack.Count - 1);
                        }
                    }
                    else if (_componentStack.Count > 0)
                    {
                        curComp = _componentStack[^1];
                        _componentStack.RemoveAt(_componentStack.Count - 1);
                    }
                    break;
                case InlineNodeKind.HtmlTag:
                    EmitHtmlTag(_tape.GetText(node.Start, node.Length), curStyle, curLink, curComp);
                    break;
            }
        }

        if (_outText.Length == 0 && _outObjects.Count == 0)
        {
            return InlineContent.Empty;
        }
        return new InlineContent(
            _outText.ToString(),
            _outSpans.Count == 0 ? [] : [.. _outSpans],
            _outLinks.Count == 0 ? [] : [.. _outLinks],
            _outObjects.Count == 0 ? [] : [.. _outObjects])
        {
            Components = _outComponents.Count == 0 ? [] : [.. _outComponents],
            TextStyles = _outTextStyles.Count == 0 ? [] : [.. _outTextStyles],
        };
    }

    private void EmitHtmlTag(ReadOnlySpan<char> tag, InlineStyle style, int link, int component)
    {
        if (!HtmlTagReader.TryRead(tag, out var parsed) || !HtmlElements.Inline.Contains(parsed.Name))
        {
            if (tag.StartsWith("<!--", StringComparison.Ordinal))
            {
                return;
            }
            EmitText(tag, style, link, component);
            return;
        }
        var name = parsed.Name.ToLowerInvariant();
        if (parsed.IsClosing)
        {
            var index = _htmlStack.FindLastIndex(f => f.Name == name);
            if (index >= 0)
            {
                _htmlStack.RemoveRange(index, _htmlStack.Count - index);
            }
            return;
        }
        switch (name)
        {
            case "br":
                EmitText("\n", style, link, component);
                return;
            case "wbr" or "source":
                return;
            case "img":
                EmitObject(InlineObjectKind.Image, parsed.Attributes["src"] ?? string.Empty, parsed.Attributes["alt"], parsed.Attributes["title"], attributes: parsed.Attributes, link: link);
                return;
        }
        if (parsed.IsSelfClosing)
        {
            return;
        }
        var outer = _htmlStack.Count > 0
            ? _htmlStack[^1]
            : (Name: "", Style: InlineStyle.None, Link: -1, TextStyle: -1, Current: HtmlTextStyle.Default);
        var frameStyle = outer.Style | HtmlInlineBuilder.StyleFor(name);
        var frameLink = outer.Link;
        if (name == "a" && parsed.Attributes["href"] is { Length: > 0 } href)
        {
            frameLink = AddOutLink(href.Trim(), parsed.Attributes["title"], isAutoLink: false);
            frameStyle |= InlineStyle.Link;
        }
        var (_, own) = CssStyle.FromAttributes(name, parsed.Attributes);
        own = own with { Align = TextAlign.Inherit };
        var current = outer.Current;
        var textStyle = outer.TextStyle;
        if (!own.IsDefault)
        {
            current = own.Over(current);
            _outTextStyles.Add(current);
            textStyle = _outTextStyles.Count - 1;
        }
        _htmlStack.Add((name, frameStyle, frameLink, textStyle, current));
    }

    private int AddOutLink(string url, string? title, bool isAutoLink)
    {
        _outLinks.Add(new LinkTarget(url, string.IsNullOrEmpty(title) ? null : title, isAutoLink));
        return _outLinks.Count - 1;
    }

    private void EmitObject(
        InlineObjectKind kind,
        string value,
        string? alt = null,
        string? title = null,
        bool flag = false,
        ComponentAttributes? attributes = null,
        int link = -1)
    {
        var offset = _outText.Length;
        _outObjects.Add(new InlineObject(offset, kind, value, alt, string.IsNullOrEmpty(title) ? null : title, flag)
        {
            Attributes = attributes ?? ComponentAttributes.Empty,
        });
        _outText.Append(InlineObject.Placeholder);
        if (link < 0 && _htmlStack.Count > 0)
        {
            link = _htmlStack[^1].Link;
        }
        if (kind == InlineObjectKind.Image && link >= 0)
        {
            _outSpans.Add(new InlineSpan(offset, 1, InlineStyle.Link, link));
        }
    }

    private void EmitText(ReadOnlySpan<char> text, InlineStyle style, int link, int component)
    {
        if (text.IsEmpty)
        {
            return;
        }
        var textStyle = -1;
        if (_htmlStack.Count > 0)
        {
            var frame = _htmlStack[^1];
            style |= frame.Style;
            if (link < 0)
            {
                link = frame.Link;
            }
            textStyle = frame.TextStyle;
        }
        var start = _outText.Length;
        _outText.Append(text);
        if (style == InlineStyle.None && link < 0 && component < 0 && textStyle < 0)
        {
            return;
        }
        if (_outSpans.Count > 0)
        {
            var last = _outSpans[^1];
            if (last.End == start && last.Style == style && last.LinkIndex == link && last.ComponentIndex == component && last.TextStyleIndex == textStyle)
            {
                _outSpans[^1] = last with { Length = last.Length + text.Length };
                return;
            }
        }
        _outSpans.Add(new InlineSpan(start, text.Length, style, link, component, textStyle));
    }

    // ---- IInlineSyntaxRule 上下文桥接 ----

    private sealed class RuleInlineSyntaxContext(
        InlineParser owner,
        MarkdownProfile profile,
        ReferenceRegistry? references,
        int topKey,
        bool hasSchemeAutolink,
        bool hasWwwAutolink,
        SearchValues<char> triggers) : InlineSyntaxContext
    {
        public override InlineStyle Style => InlineStyle.None;

        public override void AppendText(ReadOnlySpan<char> text, InlineStyle extraStyle = InlineStyle.None)
        {
            if (text.IsEmpty)
            {
                return;
            }
            if (extraStyle != InlineStyle.None)
            {
                owner._tape.Append(new InlineNode { Kind = InlineNodeKind.EmphasisOpen, Style = extraStyle });
                owner._tape.AppendText(text);
                owner._tape.Append(new InlineNode { Kind = InlineNodeKind.EmphasisClose, Style = extraStyle });
            }
            else
            {
                owner._tape.AppendText(text);
            }
        }

        public override void AppendObject(InlineObjectKind kind, string value, ComponentAttributes? attributes = null, string? alt = null, string? title = null, bool flag = false)
        {
            var objIdx = owner._pendingObjects.Count;
            owner._pendingObjects.Add(new PendingObject(kind, value, alt, title, flag, attributes ?? ComponentAttributes.Empty));
            var nodeKind = kind == InlineObjectKind.Image ? InlineNodeKind.Image : InlineNodeKind.ComponentLeaf;
            owner._tape.Append(new InlineNode { Kind = nodeKind, Extra = objIdx });
        }

        public override void AppendLink(ReadOnlySpan<char> text, string url, string? title = null, bool isAutoLink = false, InlineStyle extraStyle = InlineStyle.None)
        {
            var linkIdx = owner._pendingLinks.Count;
            owner._pendingLinks.Add(new LinkTarget(url, string.IsNullOrEmpty(title) ? null : title, isAutoLink));
            owner._tape.Append(new InlineNode { Kind = InlineNodeKind.LinkOpen, Extra = linkIdx });
            AppendText(text, extraStyle);
            owner._tape.Append(new InlineNode { Kind = InlineNodeKind.LinkClose });
        }

        public override int BeginComponent(string name, ComponentAttributes? attributes = null)
        {
            var compIdx = owner._pendingComponents.Count;
            owner._pendingComponents.Add(new InlineComponent(name.ToLowerInvariant(), attributes ?? ComponentAttributes.Empty));
            owner._tape.Append(new InlineNode { Kind = InlineNodeKind.ComponentOpen, Extra = compIdx });
            return compIdx;
        }

        public override void EndComponent(int token)
        {
            owner._tape.Append(new InlineNode { Kind = InlineNodeKind.ComponentClose, Extra = token });
        }

        public override void ParseInlines(ReadOnlySpan<char> slice, InlineStyle extraStyle = InlineStyle.None)
        {
            if (slice.IsEmpty)
            {
                return;
            }
            var savedDelim = owner._delimCount;
            var savedBracket = owner._bracketCount;
            if (extraStyle != InlineStyle.None)
            {
                owner._tape.Append(new InlineNode { Kind = InlineNodeKind.EmphasisOpen, Style = extraStyle });
            }
            owner.ScanRange(slice, profile, references, topKey, hasSchemeAutolink, hasWwwAutolink, triggers);
            InlineDelimiterProcessor.ProcessEmphasis(owner._tape, owner._delims, ref owner._delimCount, savedDelim);
            owner._bracketCount = savedBracket;
            if (extraStyle != InlineStyle.None)
            {
                owner._tape.Append(new InlineNode { Kind = InlineNodeKind.EmphasisClose, Style = extraStyle });
            }
        }
    }
}
