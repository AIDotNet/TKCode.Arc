// Ported from vercel/streamdown packages/remend (src/html-tag-handler.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

using System.Text.RegularExpressions;

namespace StreamMark.Core.Remend;

/// <summary>去掉文本末尾未完成的 HTML 标签（如 <c>text &lt;custom</c> → <c>text</c>）。</summary>
internal static partial class HtmlTagHandler
{
    // Matches an incomplete HTML tag at the end of the string.
    // Must start with < followed by a letter (opening tag) or / (closing tag),
    // and must NOT contain a > (which would close the tag).
    [GeneratedRegex(@"<[a-zA-Z/][^>]*\z")]
    private static partial Regex IncompleteHtmlTagPattern();

    private static bool StartsTag(string text, int index) =>
        JsText.At(text, index + 1) is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '/';

    public static string HandleIncompleteHtmlTag(string text)
    {
        var match = IncompleteHtmlTagPattern().Match(text);
        if (!match.Success)
        {
            return text;
        }

        // The pattern is leftmost-matching and always runs to the end of the string,
        // so every later < that starts a tag name is an equally valid candidate.
        // Walk forward until we find one that is not inside code or math: a comparison
        // operator such as \sum_{j<k} must not swallow the rest of the message.
        var checkMath = TextScan.HasMathDelimiters(text);

        for (var index = match.Index; index < text.Length; index++)
        {
            if (text[index] != '<' || !StartsTag(text, index))
            {
                continue;
            }

            if (RemendUtils.IsInsideCodeBlock(text, index))
            {
                continue;
            }

            if (checkMath && RemendUtils.IsWithinMathBlock(text, index))
            {
                continue;
            }

            // Strip the incomplete tag and any trailing whitespace before it
            return JsText.TrimEnd(text[..index]);
        }

        return text;
    }
}
