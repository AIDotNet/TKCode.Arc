// Ported from vercel/streamdown packages/remend (src/inline-code-handler.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

namespace StreamMark.Core.Remend;

/// <summary>补全未闭合的行内代码。</summary>
internal static class InlineCodeHandler
{
    // A span opened by a run of N backticks closes only on a run of exactly N,
    // so the completion appends whatever remains of the closing run. If the text
    // already ends with a partial closing run of k < N backticks, only N - k are
    // appended.
    public static string HandleIncompleteInlineCode(string text)
    {
        var scan = ScanCache.Get(text);

        // Inside an unterminated fenced code block, backticks are content and the
        // block is left for the renderer to display as streaming code
        if (scan.OpenFence is not null)
        {
            return text;
        }

        if (scan.OpenSpan is not { } span)
        {
            return text;
        }

        // Don't close if there's no meaningful content after the opening run
        var content = text.AsSpan(Math.Min(span.Start + span.RunLength, text.Length));
        if (content.IsEmpty || Patterns.WhitespaceOrMarkers().IsMatch(content))
        {
            return text;
        }

        // A trailing backtick run is the closing run being streamed
        var trailingRun = 0;
        for (var i = text.Length - 1; i >= 0 && text[i] == '`'; i--)
        {
            trailingRun++;
        }

        // A trailing run at least as long as the opener is a literal run inside
        // the span. Appending backticks would only extend it, never close the span.
        if (trailingRun >= span.RunLength)
        {
            return text;
        }

        return text + new string('`', span.RunLength - trailingRun);
    }
}
