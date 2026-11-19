// Ported from vercel/streamdown packages/remend (src/katex-handler.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

using static StreamMark.Core.Remend.JsText;

namespace StreamMark.Core.Remend;

/// <summary>补全未闭合的 KaTeX 数学公式（<c>$$</c> 块级，<c>$</c> 行内）。</summary>
internal static class KatexHandler
{
    private static int CountDollarPairs(string text) => TextScan.CountDoublePairs(text, '$');

    // Excludes $$ pairs and any $ inside code regions.
    private static int CountSingleDollars(string text)
    {
        var scan = ScanCache.Get(text);
        var count = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
                continue;
            }

            if (scan.Regions[i] != Region.Prose)
            {
                continue;
            }

            if (text[i] == '$')
            {
                if (i + 1 < text.Length && text[i + 1] == '$')
                {
                    i++;
                }
                else
                {
                    count++;
                }
            }
        }

        return count;
    }

    // Helper function to add closing $$ with appropriate formatting
    private static string AddClosingKatex(string text)
    {
        // If the text already ends with a partial closing $ (but not $$),
        // just append one more $ to complete the $$ marker.
        if (EndsWith(text, '$') && !EndsWith(text, "$$"))
        {
            return text + "$";
        }

        var firstDollarIndex = IndexOf(text, "$$");
        var hasNewlineAfterStart = firstDollarIndex != -1 && IndexOf(text, '\n', firstDollarIndex) != -1;

        if (hasNewlineAfterStart && !EndsWith(text, '\n'))
        {
            return text + "\n$$";
        }

        return text + "$$";
    }

    /// <summary>补全未闭合的块级公式（<c>$$</c>）。</summary>
    public static string HandleIncompleteBlockKatex(string text) =>
        CountDollarPairs(text) % 2 == 0 ? text : AddClosingKatex(text);

    /// <summary>补全未闭合的行内公式（<c>$...$</c>）。</summary>
    public static string HandleIncompleteInlineKatex(string text) =>
        CountSingleDollars(text) % 2 == 1 ? text + "$" : text;
}
