// Ported from vercel/streamdown packages/remend (src/comparison-operator-handler.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

using System.Text;
using System.Text.RegularExpressions;

namespace StreamMark.Core.Remend;

/// <summary>
/// 转义列表项中作为比较运算符的 <c>&gt;</c>。模型常输出 "- &gt; 25: expensive"，
/// 这里的 &gt; 是"大于"而不是引用块标记。
/// </summary>
internal static partial class ComparisonOperatorHandler
{
    private const string Ws = JsText.WhitespaceClass;

    // Match list items where > appears as a comparison operator followed by a digit
    // Pattern: list marker (-, *, +, or 1.) followed by > then optional = and a digit
    // The digit check ensures we only escape > when it's clearly a comparison (not a real blockquote)
    //
    // TS: /^(\s*(?:[-*+]|\d+[.)]) +)>(=?\s*[$]?\d)/gm, where a JS \d is ASCII only.
    // The m-flag ^ is applied by the caller, which tries the pattern (anchored
    // with \G) only at line starts, the way a global JS replace would reach them.
    [GeneratedRegex("\\G([" + Ws + "]*(?:[-*+]|[0-9]+[.)]) +)>(=?[" + Ws + "]*[$]?[0-9])")]
    private static partial Regex ListComparisonPattern();

    // A JS line terminator: with the m flag, ^ matches right after one
    private static bool IsLineTerminator(char c) => c is '\n' or '\r' or '\u2028' or '\u2029';

    // Cheap pre-check for the pattern: after leading whitespace a list marker
    // must follow, so most lines skip the regex entirely
    private static bool MayStartListItem(string text, int index)
    {
        while (index < text.Length && JsText.IsWhitespace(text[index]))
        {
            index++;
        }
        return index < text.Length && text[index] is '-' or '*' or '+' or >= '0' and <= '9';
    }

    public static string HandleComparisonOperators(string text)
    {
        // Quick check: if there's no > in the text, skip processing
        if (string.IsNullOrEmpty(text) || !text.Contains('>'))
        {
            return text;
        }

        var regex = ListComparisonPattern();
        StringBuilder? builder = null;
        var copied = 0;
        // Like String.prototype.replace with /g: matches never overlap, and the
        // search resumes at the end of the previous match
        var searchFrom = 0;
        for (var lineStart = 0; lineStart < text.Length;)
        {
            if (lineStart >= searchFrom && MayStartListItem(text, lineStart))
            {
                var match = regex.Match(text, lineStart);
                if (match.Success)
                {
                    searchFrom = match.Index + match.Length;
                    // Don't escape inside code blocks
                    if (!RemendUtils.IsInsideCodeBlock(text, match.Index))
                    {
                        builder ??= new StringBuilder(text.Length + 16);
                        var gt = match.Groups[2].Index - 1;
                        builder.Append(text, copied, gt - copied).Append("\\>");
                        copied = gt + 1;
                    }
                }
            }

            // Advance to the next position after a line terminator
            var next = lineStart;
            while (next < text.Length && !IsLineTerminator(text[next]))
            {
                next++;
            }
            lineStart = next + 1;
        }

        if (builder is null)
        {
            return text;
        }
        builder.Append(text, copied, text.Length - copied);
        return builder.ToString();
    }
}
