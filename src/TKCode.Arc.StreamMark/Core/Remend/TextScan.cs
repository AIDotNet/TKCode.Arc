// Ported from vercel/streamdown packages/remend (src/scan.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

using System.Buffers;
using System.Text.RegularExpressions;

namespace StreamMark.Core.Remend;

// Single-pass classification of a text into code and prose regions.
//
// Healing runs on every streaming token, and every handler needs to know
// whether a candidate delimiter sits in prose or in code. A single scan
// paints a region code for every position, memoized per input string, so
// each query is O(1) and healing stays linear in the input no matter how
// many delimiters it holds. The lazy masks below answer the same question
// for math, link URLs, and HTML tags.
//
// Fence and span semantics follow CommonMark:
//
// - A fence opens only at the start of a line, with any indentation, after any
//   block quote and list markers. CommonMark caps a top-level fence at 3
//   spaces, but fences nested in list items carry deeper absolute indents and
//   a line-based scan has no list context. Reading an indented line as code is
//   the safe direction: healing then leaves it alone instead of corrupting it.
// - A fence inside a block quote reads its lines after the quote markers, and
//   ends with the quote: a line with fewer markers closes it, because fenced
//   code has no lazy continuation. Likewise a fence opened on a list marker's
//   line ends with the item, at a non-blank line indented short of its
//   content. A fence opened on a later line of an item keeps the lenient
//   reading above.
// - Both ``` and ~~~ fences are recognized, with runs of 3 or more.
// - The info string of a backtick fence cannot contain a backtick
//   (a line like ```code``` is inline code, not a fence).
// - A fence closes on a run of the same character at least as long as the
//   opener, alone on its line. Lines may end in \n or \r\n.
// - An inline code span opened by a run of N backticks closes only on a run
//   of exactly N backticks. Other runs are literal inside the span.
// - A span cannot cross a blank line: inline parsing is paragraph-scoped, so
//   an unmatched run in a finished paragraph stays literal prose.

/// <summary>每个 UTF-16 码元所属的区域。</summary>
internal static class Region
{
    public const byte Prose = 0;

    /// <summary>开启或关闭围栏的 ``` / ~~~ 序列。</summary>
    public const byte FenceMarker = 1;

    /// <summary>围栏开启行上的 info 字符串，既不是正文也不是代码体。</summary>
    public const byte FenceInfo = 2;

    public const byte FenceBody = 3;

    /// <summary>完整的行内代码（含反引号）。</summary>
    public const byte CodeSpan = 4;

    /// <summary>尚未出现闭合序列的行内代码。</summary>
    public const byte CodeSpanOpen = 5;
}

/// <summary>未闭合的围栏代码块。</summary>
/// <param name="Char">围栏字符：<c>`</c> 或 <c>~</c>。</param>
/// <param name="Length">开启序列长度，闭合序列至少同样长。</param>
/// <param name="ListIndent">开启行所在列表项的内容列（引用标记之后计），开启行不是列表项时为 0。</param>
/// <param name="QuoteDepth">开启行前的引用标记数，代码体每行都必须重复。</param>
internal sealed record OpenFence(char Char, int Length, int ListIndent, int QuoteDepth);

/// <summary>未闭合的行内代码。</summary>
/// <param name="RunLength">开启序列长度，闭合序列必须等长。</param>
/// <param name="Start">开启序列第一个反引号的下标。</param>
internal sealed record OpenSpan(int RunLength, int Start);

/// <summary>一次扫描的结果：区域数组与按需构建的数学 / 链接 URL / HTML 标签掩码。</summary>
internal sealed partial class TextScan
{
    // The masks share the empty array when their trigger character is absent, so
    // plain prose skips three allocations and passes per scan
    private static readonly byte[] EmptyMask = [];

    private byte[]? _mathMask;
    private byte[]? _linkUrlMask;
    private byte[]? _htmlTagMask;

    private TextScan(string text, byte[] regions, OpenFence? openFence, OpenSpan? openSpan)
    {
        Text = text;
        Regions = regions;
        OpenFence = openFence;
        OpenSpan = openSpan;
    }

    public string Text { get; }

    /// <summary>区域数组。可能来自数组池，长度可以大于 <see cref="Length"/>。</summary>
    public byte[] Regions { get; }

    public int Length => Text.Length;

    public OpenFence? OpenFence { get; }

    public OpenSpan? OpenSpan { get; }

    /// <summary>对应 TS 的 <c>regions[i]</c>：越界时返回 <see cref="JsText.None"/>。</summary>
    public int RegionAt(int position) => (uint)position < (uint)Text.Length ? Regions[position] : JsText.None;

    [GeneratedRegex(@"^(`{3,}|~{3,})([^\n\r\u2028\u2029]*)\z")]
    private static partial Regex FenceRunPattern();

    #region Scanning

    public static TextScan Create(string text)
    {
        var regions = ScanCache.AllocateMask(text.Length);
        var openFence = PaintFences(text, regions);
        var openSpan = PaintSpans(text, regions);
        return new TextScan(text, regions, openFence, openSpan);
    }

    private static bool IsDigit(int c) => c is >= '0' and <= '9';

    private static void Fill(byte[] regions, int length, byte value, int start, int end)
    {
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, 0, length);
        if (end > start)
        {
            regions.AsSpan(start, end - start).Fill(value);
        }
    }

    // Index just past a list marker and its space at `start`, or -1
    private static int SkipListMarker(string text, int start)
    {
        var i = start;
        var c = JsText.At(text, i);
        if (c is '-' or '*' or '+')
        {
            i++;
        }
        else
        {
            while (IsDigit(JsText.At(text, i)) && i - start < 9)
            {
                i++;
            }
            if (i == start || JsText.At(text, i) is not ('.' or ')'))
            {
                return -1;
            }
            i++;
        }
        return JsText.At(text, i) == ' ' ? i + 1 : -1;
    }

    private readonly record struct ContainerPrefix(int ContentStart, int ListIndent, int QuoteDepth);

    // Walks the block quote and list markers that open a line. A single forward
    // pass: a regex with nested repetition over these markers backtracks
    // exponentially on a line of repeated list markers.
    private static ContainerPrefix SkipContainerPrefix(string text, int lineStart, int lineEnd)
    {
        var quoteDepth = 0;
        var afterQuotes = lineStart;
        var startsItem = false;
        var i = lineStart;
        for (;;)
        {
            while (i < lineEnd && text[i] == ' ')
            {
                i++;
            }
            if (JsText.At(text, i) == '>')
            {
                quoteDepth++;
                i += JsText.At(text, i + 1) == ' ' ? 2 : 1;
                afterQuotes = i;
                startsItem = false;
                continue;
            }
            var afterMarker = i < lineEnd ? SkipListMarker(text, i) : -1;
            if (afterMarker == -1)
            {
                return new ContainerPrefix(i, startsItem ? i - afterQuotes : 0, quoteDepth);
            }
            i = afterMarker;
            startsItem = true;
        }
    }

    // Index just past up to `depth` block quote markers at the start of a line,
    // or -1 if the line carries fewer
    private static int SkipQuoteMarkers(string text, int lineStart, int lineEnd, int depth)
    {
        var i = lineStart;
        for (var found = 0; found < depth; found++)
        {
            while (i < lineEnd && text[i] == ' ')
            {
                i++;
            }
            if (JsText.At(text, i) != '>')
            {
                return -1;
            }
            i++;
            if (JsText.At(text, i) == ' ')
            {
                i++;
            }
        }
        return i;
    }

    private static void PaintFenceOpener(
        byte[] regions, int length, int lineStart, int lineEnd, int indentLength, int markerLength)
    {
        var markerStart = lineStart + indentLength;
        Fill(regions, length, Region.FenceMarker, markerStart, markerStart + markerLength);
        // Info string plus the line terminator belong to the fence.
        Fill(regions, length, Region.FenceInfo, markerStart + markerLength, Math.Min(lineEnd + 1, length));
    }

    // Whether a line inside an open fence closes it: optional indent, then a run
    // of the fence character at least as long as the opener, then only whitespace
    private static bool IsFenceCloser(string text, int contentStart, int lineEnd, OpenFence fence)
    {
        var i = contentStart;
        while (i < lineEnd && text[i] == ' ')
        {
            i++;
        }
        var runLength = 0;
        while (i < lineEnd && text[i] == fence.Char)
        {
            i++;
            runLength++;
        }
        if (runLength < fence.Length)
        {
            return false;
        }
        while (i < lineEnd)
        {
            if (text[i] is not (' ' or '\t' or '\r'))
            {
                return false;
            }
            i++;
        }
        return true;
    }

    private static OpenFence? OpenFenceAt(string text, byte[] regions, int lineStart, int lineEnd)
    {
        var contentEnd = lineEnd > lineStart && text[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;
        var prefix = SkipContainerPrefix(text, lineStart, contentEnd);
        // The pattern requires a ` or ~ first, so only such lines are worth slicing
        if (prefix.ContentStart >= contentEnd || text[prefix.ContentStart] is not ('`' or '~'))
        {
            return null;
        }
        var opener = FenceRunPattern().Match(text.Substring(prefix.ContentStart, contentEnd - prefix.ContentStart));
        if (!opener.Success)
        {
            return null;
        }
        var run = opener.Groups[1];
        var info = opener.Groups[2];
        var ch = run.ValueSpan[0];
        // A backtick fence's info string cannot contain a backtick; such a line is
        // inline code instead
        if (ch == '`' && info.ValueSpan.Contains('`'))
        {
            return null;
        }
        PaintFenceOpener(regions, text.Length, lineStart, lineEnd, prefix.ContentStart - lineStart, run.Length);
        return new OpenFence(ch, run.Length, prefix.ListIndent, prefix.QuoteDepth);
    }

    // Index where a body line's content starts inside its fence's containers, or
    // -1 if the line leaves them and so ends the fence
    private static int FenceContentStart(string text, int lineStart, int lineEnd, OpenFence fence)
    {
        var contentStart = SkipQuoteMarkers(text, lineStart, lineEnd, fence.QuoteDepth);
        if (contentStart == -1 || fence.ListIndent == 0)
        {
            return contentStart;
        }
        var i = contentStart;
        while (i < lineEnd && text[i] == ' ')
        {
            i++;
        }
        var blank = i == lineEnd || (JsText.At(text, i) == '\r' && i + 1 == lineEnd);
        return blank || i - contentStart >= fence.ListIndent ? contentStart : -1;
    }

    private static OpenFence? PaintFences(string text, byte[] regions)
    {
        var n = text.Length;
        OpenFence? openFence = null;
        var lineStart = 0;

        while (lineStart < n)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd == -1)
            {
                lineEnd = n;
            }

            var contentStart = openFence is not null ? FenceContentStart(text, lineStart, lineEnd, openFence) : -1;
            if (openFence is not null && contentStart == -1)
            {
                // The block quote or list item ended, taking the fence with it
                openFence = null;
            }

            if (openFence is null)
            {
                openFence = OpenFenceAt(text, regions, lineStart, lineEnd);
            }
            else if (IsFenceCloser(text, contentStart, lineEnd, openFence))
            {
                Fill(regions, n, Region.FenceMarker, lineStart, lineEnd);
                openFence = null;
            }
            else
            {
                Fill(regions, n, Region.FenceBody, lineStart, Math.Min(lineEnd + 1, n));
            }

            lineStart = lineEnd + 1;
        }

        return openFence;
    }

    private static int MeasureBacktickRun(string text, int start)
    {
        var end = start + 1;
        while (end < text.Length && text[end] == '`')
        {
            end++;
        }
        return end;
    }

    // A blank line ends the paragraph, and with it any chance of closing a span
    private static bool IsParagraphBreakAt(string text, int newlineIndex)
    {
        var j = newlineIndex + 1;
        while (j < text.Length && text[j] is ' ' or '\t' or '\r')
        {
            j++;
        }
        return j < text.Length && text[j] == '\n';
    }

    // Outside a span only a backtick changes anything, so jump to the next prose
    // backtick that could open one, applying the escape the scan would have seen
    // on the character before it. Returns -1 when none remains.
    private static int NextSpanOpener(string text, byte[] regions, int from)
    {
        var i = from;
        for (;;)
        {
            var next = JsText.IndexOf(text, '`', i);
            if (next == -1)
            {
                return -1;
            }
            var escaped = next > i && text[next - 1] == '\\' && regions[next - 1] == Region.Prose;
            if (regions[next] == Region.Prose && !escaped)
            {
                return next;
            }
            i = next + 1;
        }
    }

    private static OpenSpan? PaintSpans(string text, byte[] regions)
    {
        var n = text.Length;
        var spanStart = -1;
        var spanRunLength = 0;
        var i = 0;

        while (i < n)
        {
            if (spanStart < 0)
            {
                i = NextSpanOpener(text, regions, i);
                if (i == -1)
                {
                    break;
                }
                var openerEnd = MeasureBacktickRun(text, i);
                spanStart = i;
                spanRunLength = openerEnd - i;
                i = openerEnd;
                continue;
            }

            // Inside a span from here on. Backslashes are literal in code.
            if (regions[i] != Region.Prose)
            {
                // A span cannot cross into a fence, so leave it marked open up to here
                Fill(regions, n, Region.CodeSpanOpen, spanStart, i);
                spanStart = -1;
                i++;
                continue;
            }
            if (text[i] == '\n' && IsParagraphBreakAt(text, i))
            {
                // The unmatched opener stays literal prose in its finished paragraph
                spanStart = -1;
                i++;
                continue;
            }
            if (text[i] != '`')
            {
                i++;
                continue;
            }

            var runEnd = MeasureBacktickRun(text, i);
            // A run of a different length is literal inside the open span
            if (runEnd - i == spanRunLength)
            {
                Fill(regions, n, Region.CodeSpan, spanStart, runEnd);
                spanStart = -1;
            }
            i = runEnd;
        }

        if (spanStart >= 0)
        {
            Fill(regions, n, Region.CodeSpanOpen, spanStart, n);
            return new OpenSpan(spanRunLength, spanStart);
        }
        return null;
    }

    #endregion

    #region Region queries

    /// <summary>位置是否处于代码结构（围栏或行内代码）中。</summary>
    public bool IsCodeAt(int position)
    {
        if (position >= Length)
        {
            return OpenFence is not null || OpenSpan is not null;
        }
        if (position < 0)
        {
            return false;
        }
        return Regions[position] != Region.Prose;
    }

    /// <summary>位置是否处于围栏代码块中（标记、info 或代码体）。</summary>
    public bool IsFenceAt(int position)
    {
        if (position >= Length)
        {
            return OpenFence is not null;
        }
        if (position < 0)
        {
            return false;
        }
        return Regions[position] is Region.FenceMarker or Region.FenceInfo or Region.FenceBody;
    }

    public bool IsCompleteSpanAt(int position) => RegionAt(position) == Region.CodeSpan;

    /// <summary>统计正文中不重叠的双字符对（**、~~、$$）。</summary>
    public static int CountDoublePairs(string text, char ch)
    {
        var scan = ScanCache.Get(text);
        var pair = new string(ch, 2);
        var count = 0;

        for (var i = JsText.IndexOf(text, pair); i != -1; i = JsText.IndexOf(text, pair, i))
        {
            if (scan.Regions[i] == Region.Prose)
            {
                count++;
                i += 2;
            }
            else
            {
                i++;
            }
        }
        return count;
    }

    #endregion

    #region Math mask

    private enum MathContext : byte
    {
        None,
        InlineDollar,
        BlockDollar,
        InlineLatex,
        BlockLatex,
    }

    private static bool IsLatexMathContext(MathContext context) =>
        context is MathContext.InlineLatex or MathContext.BlockLatex;

    private static MathContext? GetLatexMathContext(MathContext context, int nextChar)
    {
        if (nextChar == '[' && context == MathContext.None)
        {
            return MathContext.BlockLatex;
        }
        if (nextChar == ']' && context == MathContext.BlockLatex)
        {
            return MathContext.None;
        }
        if (nextChar == '(' && context == MathContext.None)
        {
            return MathContext.InlineLatex;
        }
        if (nextChar == ')' && context == MathContext.InlineLatex)
        {
            return MathContext.None;
        }
        return null;
    }

    private static MathContext GetDollarMathContext(MathContext context, bool isBlockDelimiter)
    {
        if (isBlockDelimiter)
        {
            return context == MathContext.BlockDollar ? MathContext.None : MathContext.BlockDollar;
        }
        if (context == MathContext.BlockDollar)
        {
            return context;
        }
        return context == MathContext.InlineDollar ? MathContext.None : MathContext.InlineDollar;
    }

    public static bool HasMathDelimiters(string text) =>
        text.Contains('$') || text.Contains("\\(", StringComparison.Ordinal) || text.Contains("\\[", StringComparison.Ordinal);

    // Recognizes a math delimiter or escaped dollar at position i; Length == 0 means none
    private static (MathContext Context, int Length) MathDelimiterAt(string text, int i, MathContext context)
    {
        var next = JsText.At(text, i + 1);
        if (text[i] == '\\')
        {
            if (next == '$')
            {
                return (context, 2);
            }
            var latexContext = GetLatexMathContext(context, next);
            return latexContext is { } value ? (value, 2) : (context, 0);
        }
        if (text[i] == '$' && !IsLatexMathContext(context))
        {
            var isBlockDelimiter = next == '$';
            return (GetDollarMathContext(context, isBlockDelimiter), isBlockDelimiter ? 2 : 1);
        }
        return (context, 0);
    }

    // Math mask: for each position, whether it is inside $...$, $$...$$,
    // \(...\) or \[...\]. Delimiters inside code regions are literal and do
    // not change math state. A two-character delimiter marks its second
    // character with the context it establishes.
    private byte[] BuildMathMask()
    {
        var text = Text;
        var n = text.Length;
        var mask = ScanCache.AllocateMask(n);
        var context = MathContext.None;

        var i = 0;
        while (i < n)
        {
            mask[i] = context == MathContext.None ? (byte)0 : (byte)1;
            var (next, length) = Regions[i] == Region.Prose ? MathDelimiterAt(text, i, context) : (context, 0);
            if (length == 0)
            {
                i++;
                continue;
            }
            context = next;
            if (length == 2)
            {
                mask[i + 1] = context == MathContext.None ? (byte)0 : (byte)1;
            }
            i += length;
        }

        return mask;
    }

    public bool InMathAt(int position)
    {
        if (position < 0 || position >= Length)
        {
            return false;
        }
        _mathMask ??= HasMathDelimiters(Text) ? BuildMathMask() : EmptyMask;
        return position < _mathMask.Length && _mathMask[position] == 1;
    }

    #endregion

    #region Link URL mask

    // Marks the URL positions of one line: those between a "](" opener and the
    // next ")" on the line. Two sub-passes: backward to know whether a ")" still
    // follows a position, forward to know whether the nearest paren boundary
    // before a position is a "](" opener.
    private void PaintLinkUrlLine(int lineStart, int lineEnd, byte[] mask, byte[] closerFollows)
    {
        var text = Text;
        // closerFollows[i - lineStart]: a ")" exists at or after i on this line
        byte seenCloser = 0;
        for (var i = lineEnd - 1; i >= lineStart; i--)
        {
            if (text[i] == ')' && Regions[i] == Region.Prose)
            {
                seenCloser = 1;
            }
            closerFollows[i - lineStart] = seenCloser;
        }

        var inUrl = false;
        for (var i = lineStart; i < lineEnd; i++)
        {
            if (inUrl && closerFollows[i - lineStart] == 1)
            {
                mask[i] = 1;
            }
            if (Regions[i] != Region.Prose)
            {
                continue;
            }
            if (text[i] == ')')
            {
                inUrl = false;
            }
            else if (text[i] == '(')
            {
                inUrl = i > 0 && text[i - 1] == ']';
            }
        }
    }

    // Link/image URL mask: positions inside the (url) part of [text](url).
    // Delimiters inside code regions are literal and never open or close a URL.
    private byte[] BuildLinkUrlMask()
    {
        var text = Text;
        var n = text.Length;
        var mask = ScanCache.AllocateMask(n);
        var closerFollows = ArrayPool<byte>.Shared.Rent(n);
        try
        {
            var lineStart = 0;
            while (lineStart < n)
            {
                var lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd == -1)
                {
                    lineEnd = n;
                }
                PaintLinkUrlLine(lineStart, lineEnd, mask, closerFollows);
                lineStart = lineEnd + 1;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(closerFollows);
        }

        return mask;
    }

    public bool InLinkUrlAt(int position)
    {
        if (position < 0 || position >= Length)
        {
            return false;
        }
        _linkUrlMask ??= Text.Contains("](", StringComparison.Ordinal) ? BuildLinkUrlMask() : EmptyMask;
        return position < _linkUrlMask.Length && _linkUrlMask[position] == 1;
    }

    #endregion

    #region HTML tag mask

    // HTML tag mask: positions after a "<" that begins a plausible tag (letter
    // or /), through the closing ">" inclusive, within a single line. Angle
    // brackets inside code regions are literal and never open or close a tag.
    private byte[] BuildHtmlTagMask()
    {
        var text = Text;
        var n = text.Length;
        var mask = ScanCache.AllocateMask(n);
        var inTag = false;

        for (var i = 0; i < n; i++)
        {
            if (text[i] == '\n')
            {
                inTag = false;
                continue;
            }
            mask[i] = inTag ? (byte)1 : (byte)0;
            if (Regions[i] != Region.Prose)
            {
                continue;
            }
            if (text[i] == '>')
            {
                inTag = false;
            }
            else if (text[i] == '<')
            {
                var next = JsText.At(text, i + 1);
                inTag = next is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '/';
            }
        }

        return mask;
    }

    public bool InHtmlTagAt(int position)
    {
        if (position < 0 || position >= Length)
        {
            return false;
        }
        _htmlTagMask ??= Text.Contains('<') ? BuildHtmlTagMask() : EmptyMask;
        return position < _htmlTagMask.Length && _htmlTagMask[position] == 1;
    }

    #endregion
}

/// <summary>
/// 单条目扫描缓存。TS 版本用模块级变量缓存最近一次扫描并在每次 remend 结束后清空；
/// 这里改为线程静态，作用域限定在一次 <see cref="Remend.Complete"/> 调用内，
/// 因此并发调用互不干扰。调用期间的区域 / 掩码数组从 <see cref="ArrayPool{T}"/> 租借，
/// 调用结束时统一归还（扫描对象在调用内可能被多处持有，提前归还不安全）。
/// 不在 Complete 调用内时（例如测试直接调用工具函数）不缓存、不租借。
/// </summary>
internal static class ScanCache
{
    [ThreadStatic] private static string? t_text;
    [ThreadStatic] private static TextScan? t_scan;
    [ThreadStatic] private static int t_depth;
    [ThreadStatic] private static List<byte[]>? t_rented;

    // Memoize the most recent scan. Handlers query many positions of the same
    // string, and remend's handler chain passes each handler's output to the
    // next, so a single-entry cache gives O(1) queries within a handler while
    // staying O(n) per handler overall. Keyed by reference: handlers reuse the
    // same string instance, and a value comparison would cost O(n) per query.
    public static TextScan Get(string text)
    {
        if (t_depth == 0)
        {
            return TextScan.Create(text);
        }
        if (t_scan is not null && ReferenceEquals(text, t_text))
        {
            return t_scan;
        }
        var scan = TextScan.Create(text);
        t_text = text;
        t_scan = scan;
        return scan;
    }

    /// <summary>分配一个前 <paramref name="length"/> 个元素为 0 的字节数组。</summary>
    public static byte[] AllocateMask(int length)
    {
        if (t_depth == 0 || length < 256)
        {
            return new byte[length];
        }
        var array = ArrayPool<byte>.Shared.Rent(length);
        Array.Clear(array, 0, length);
        (t_rented ??= []).Add(array);
        return array;
    }

    public static void Enter() => t_depth++;

    // Drops the memoized scan so a long streamed message and its region array
    // are not retained between calls
    public static void Exit()
    {
        if (--t_depth > 0)
        {
            return;
        }
        t_text = null;
        t_scan = null;
        if (t_rented is { Count: > 0 } rented)
        {
            foreach (var array in rented)
            {
                ArrayPool<byte>.Shared.Return(array);
            }
            rented.Clear();
        }
    }
}
