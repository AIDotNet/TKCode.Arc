// Ported from vercel/streamdown packages/remend (src/utils.ts, src/code-block-utils.ts),
// Copyright 2023 Vercel, Inc., Apache-2.0; modified.

namespace StreamMark.Core.Remend;

/// <summary>各 handler 共用的查询函数。字符参数用 int 表示，<see cref="JsText.None"/> 对应 JS 的空字符串。</summary>
internal static class RemendUtils
{
    /// <summary>
    /// 是否是单词字符（<c>[\p{L}\p{N}_]</c>）。参数是单个 UTF-16 码元，
    /// 孤立代理项与 JS 的 <c>u</c> 模式一样不算字母或数字。
    /// </summary>
    public static bool IsWordChar(int c)
    {
        if (c < 0)
        {
            return false;
        }
        // ASCII optimization: a-z, A-Z, 0-9, _
        if (c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_')
        {
            return true;
        }
        if (c < 0x80)
        {
            return false;
        }
        var ch = (char)c;
        return char.IsLetter(ch) || char.IsNumber(ch);
    }

    /// <summary>字符串形式，对应 TS 的 <c>isWordChar(char)</c>（只看首个码元）。</summary>
    public static bool IsWordChar(string text) => text.Length > 0 && IsWordChar(text[0]);

    public static bool IsWithinCodeBlock(string text, int position) => ScanCache.Get(text).IsFenceAt(position);

    // A code construct here means a fenced block or an inline code span.
    public static bool IsInsideCodeBlock(string text, int position) => ScanCache.Get(text).IsCodeAt(position);

    // Check if a position is within a COMPLETE inline code span (both opening and
    // closing backtick runs present). Returns false for incomplete spans
    // (streaming) so emphasis markers can still be completed.
    public static bool IsWithinCompleteInlineCode(string text, int position) =>
        ScanCache.Get(text).IsCompleteSpanAt(position);

    // Helper function to find the matching opening bracket for a closing bracket
    // Handles nested brackets correctly by searching backwards
    public static int FindMatchingOpeningBracket(string text, int closeIndex)
    {
        var depth = 1;
        for (var i = closeIndex - 1; i >= 0; i--)
        {
            if (text[i] == ']')
            {
                depth++;
            }
            else if (text[i] == '[')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    // Helper function to find the matching closing bracket for an opening bracket
    // Handles nested brackets correctly
    public static int FindMatchingClosingBracket(string text, int openIndex)
    {
        var depth = 1;
        for (var i = openIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '[')
            {
                depth++;
            }
            else if (text[i] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    // Check if a position is within a math block (between $ or $$)
    public static bool IsWithinMathBlock(string text, int position) => ScanCache.Get(text).InMathAt(position);

    // Check if a position is within a link or image URL
    public static bool IsWithinLinkOrImageUrl(string text, int position) => ScanCache.Get(text).InLinkUrlAt(position);

    // Check if a position is within an HTML tag (between < and >)
    // e.g. <a target="_blank"> — the underscore in _blank is inside the tag
    public static bool IsWithinHtmlTag(string text, int position) => ScanCache.Get(text).InHtmlTagAt(position);

    // Check if a marker sequence appears to be a horizontal rule
    // Horizontal rules must be on their own line with optional leading/trailing whitespace
    // Valid patterns: ---, ***, ___, or longer sequences with optional spaces between markers
    public static bool IsHorizontalRule(string text, int markerIndex, char marker)
    {
        // Find the start of the line containing this marker
        var lineStart = 0;
        for (var i = markerIndex - 1; i >= 0; i--)
        {
            if (text[i] == '\n')
            {
                lineStart = i + 1;
                break;
            }
        }

        // Find the end of the line containing this marker
        var lineEnd = text.Length;
        for (var i = Math.Max(markerIndex, 0); i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineEnd = i;
                break;
            }
        }

        // Must be: optional spaces + at least 3 markers + optional spaces
        // Can have spaces between markers (e.g., "* * *")
        var markerCount = 0;
        for (var i = lineStart; i < lineEnd; i++)
        {
            var c = text[i];
            if (c == marker)
            {
                markerCount++;
            }
            else if (c != ' ' && c != '\t')
            {
                // Found a character that's not a space, tab, or the marker
                return false;
            }
        }

        return markerCount >= 3;
    }
}
