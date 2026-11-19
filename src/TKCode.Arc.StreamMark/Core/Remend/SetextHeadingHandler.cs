// Ported from vercel/streamdown packages/remend (src/setext-heading-handler.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

using System.Text.RegularExpressions;

namespace StreamMark.Core.Remend;

/// <summary>
/// 处理流式输出中未完成的 setext 标题下划线：流式过程中的半个列表项（如 "-"）
/// 可能被误判为 setext 标题下划线。
/// </summary>
internal static partial class SetextHeadingHandler
{
    private const string Ws = JsText.WhitespaceClass;

    [GeneratedRegex(@"^-{1,2}\z")]
    private static partial Regex DashOnlyPattern();

    [GeneratedRegex("^[" + Ws + "]*-{1,2}[" + Ws + "]+\\z")]
    private static partial Regex DashWithSpacePattern();

    [GeneratedRegex(@"^={1,2}\z")]
    private static partial Regex EqualsOnlyPattern();

    [GeneratedRegex("^[" + Ws + "]*={1,2}[" + Ws + "]+\\z")]
    private static partial Regex EqualsWithSpacePattern();

    // Whether the line before the last one has content (required for a setext heading)
    private static bool PreviousLineHasContent(string text, int lastNewlineIndex)
    {
        var previousStart = lastNewlineIndex > 0 ? JsText.LastIndexOf(text, '\n', lastNewlineIndex - 1) + 1 : 0;
        var previousLine = text.Substring(previousStart, lastNewlineIndex - previousStart);
        return previousLine.Length > 0 && JsText.Trim(previousLine).Length > 0;
    }

    /// <summary>
    /// 若文本以可能未完成的 setext 下划线结尾，追加零宽空格以打破该模式。
    /// </summary>
    public static string HandleIncompleteSetextHeading(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Find the last line of the text
        var lastNewlineIndex = text.LastIndexOf('\n');

        // If there's no newline, we can't have a setext heading
        if (lastNewlineIndex == -1)
        {
            return text;
        }

        var lastLine = text[(lastNewlineIndex + 1)..];

        // Patterns like "-", "--", "=", "==" but NOT "---" or "===" (valid horizontal
        // rules / setext headings). A trailing space after the markers has already
        // broken the setext heading pattern.
        var trimmedLastLine = JsText.Trim(lastLine);

        if (DashOnlyPattern().IsMatch(trimmedLastLine) && !DashWithSpacePattern().IsMatch(lastLine)
            && PreviousLineHasContent(text, lastNewlineIndex))
        {
            // A zero-width space breaks the pattern without being visible; a regular
            // space may still be read as a setext heading underline
            return text + "\u200B";
        }

        if (EqualsOnlyPattern().IsMatch(trimmedLastLine) && !EqualsWithSpacePattern().IsMatch(lastLine)
            && PreviousLineHasContent(text, lastNewlineIndex))
        {
            return text + "\u200B";
        }

        return text;
    }
}
