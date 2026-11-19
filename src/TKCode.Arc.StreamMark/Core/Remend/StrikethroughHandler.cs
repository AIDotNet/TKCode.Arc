// Ported from vercel/streamdown packages/remend (src/strikethrough-handler.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

namespace StreamMark.Core.Remend;

/// <summary>补全未闭合的删除线（<c>~~</c>）。</summary>
internal static class StrikethroughHandler
{
    // Tilde runs that open or close a fence are painted as fence regions by the
    // scanner, so a line-start ~~~ fence never counts as strikethrough while a
    // mid-line tilde run still does.
    private static int CountDoubleTildes(string text) => TextScan.CountDoublePairs(text, '~');

    private static bool IsInCode(string text, int position) =>
        RemendUtils.IsInsideCodeBlock(text, position) || RemendUtils.IsWithinCompleteInlineCode(text, position);

    public static string HandleIncompleteStrikethrough(string text)
    {
        var strikethroughMatch = Patterns.MatchTrailing(text, Patterns.Strikethrough);

        if (strikethroughMatch is not null)
        {
            // Don't close if there's no meaningful content after the opening markers
            var contentAfterMarker = strikethroughMatch.Groups[2].Value;
            if (contentAfterMarker.Length == 0 || Patterns.WhitespaceOrMarkers().IsMatch(contentAfterMarker))
            {
                return text;
            }

            // Don't close if the marker is inside an inline code span or fenced code block
            var markerIndex = JsText.LastIndexOf(text, strikethroughMatch.Groups[1].Value);
            if (IsInCode(text, markerIndex))
            {
                return text;
            }

            if (CountDoubleTildes(text) % 2 == 1)
            {
                return text + "~~";
            }
        }
        else
        {
            // Check for half-complete closing marker: ~~content~ should become ~~content~~
            // The pattern /(~~)([^~]*?)$/ won't match ~~content~ because it ends with ~
            var halfCompleteMatch = Patterns.MatchTrailing(text, Patterns.HalfCompleteTilde);
            if (halfCompleteMatch is not null)
            {
                // Don't close if the marker is inside an inline code span or fenced code block
                var markerIndex = JsText.LastIndexOf(text, halfCompleteMatch.Value[..2]);
                if (IsInCode(text, markerIndex))
                {
                    return text;
                }
                if (CountDoubleTildes(text) % 2 == 1)
                {
                    return text + "~";
                }
            }
        }

        return text;
    }
}
