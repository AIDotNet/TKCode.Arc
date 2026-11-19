// Ported from vercel/streamdown packages/remend (src/link-image-handler.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

using static StreamMark.Core.Remend.JsText;

namespace StreamMark.Core.Remend;

/// <summary>处理未完成的链接与图片：用占位 URL 补全，或在 <see cref="LinkMode.TextOnly"/> 下只保留文本。</summary>
internal static class LinkImageHandler
{
    private const string LinkSuffix = "](" + Remend.IncompleteLinkUrl + ")";
    private const string ImageSuffix = "](" + Remend.IncompleteImageUrl + ")";

    // Helper function to handle incomplete URLs in links/images
    private static string? HandleIncompleteUrl(string text, int lastParenIndex, LinkMode linkMode)
    {
        if (IndexOf(text, ')', lastParenIndex + 2) != -1)
        {
            return null;
        }

        // We have an incomplete URL like [text](partial-url
        // Now find the matching opening bracket for the ] before (
        var openBracketIndex = RemendUtils.FindMatchingOpeningBracket(text, lastParenIndex);
        if (openBracketIndex == -1 || RemendUtils.IsInsideCodeBlock(text, openBracketIndex))
        {
            return null;
        }

        // Check if there's a ! before the [
        var isImage = openBracketIndex > 0 && text[openBracketIndex - 1] == '!';
        var startIndex = isImage ? openBracketIndex - 1 : openBracketIndex;

        // Extract everything before this link/image
        var beforeLink = text.AsSpan(0, startIndex);

        // Extract the text between [ and ] (alt text for images, link text for links)
        var altOrLinkText = text.AsSpan(openBracketIndex + 1, lastParenIndex - openBracketIndex - 1);

        if (isImage)
        {
            // For images with incomplete URLs, replace with placeholder marker
            return string.Concat(beforeLink, "![", altOrLinkText, ImageSuffix);
        }

        // For links with incomplete URLs, handle based on linkMode
        if (linkMode == LinkMode.TextOnly)
        {
            return string.Concat(beforeLink, altOrLinkText);
        }
        return string.Concat(beforeLink, "[", altOrLinkText, LinkSuffix);
    }

    // Helper to find the first incomplete [ (for text-only mode)
    // Always returns a valid index since callers guarantee text[maxPos] is an incomplete [
    private static int FindFirstIncompleteBracket(string text, int maxPos)
    {
        for (var j = 0; j < maxPos; j++)
        {
            if (text[j] != '[' || RemendUtils.IsInsideCodeBlock(text, j))
            {
                continue;
            }
            // Skip if it's an image
            if (j > 0 && text[j - 1] == '!')
            {
                continue;
            }
            // Check if this [ has a matching ]
            var closingIdx = RemendUtils.FindMatchingClosingBracket(text, j);
            if (closingIdx == -1)
            {
                // This is an incomplete [
                return j;
            }
            // This [ is complete, check if it's a full link [text](url)
            if (closingIdx + 1 < text.Length && text[closingIdx + 1] == '(')
            {
                var urlEnd = IndexOf(text, ')', closingIdx + 2);
                if (urlEnd != -1)
                {
                    // Skip past this complete link
                    j = urlEnd;
                }
            }
        }
        // Fallback: the bracket at maxPos is always incomplete by contract
        return maxPos;
    }

    // Shared tail of handleIncompleteText for an unclosed [ (or ![) at i
    private static string HealUnclosedBracket(string text, int i, bool isImage, int openIndex, LinkMode linkMode)
    {
        if (isImage)
        {
            // For images with incomplete alt text, replace with placeholder marker
            return string.Concat(text.AsSpan(0, openIndex), "![", text.AsSpan(i + 1), ImageSuffix);
        }

        // For links, handle based on linkMode
        if (linkMode == LinkMode.TextOnly)
        {
            // Find the first incomplete [ and strip just that bracket
            var firstIncomplete = FindFirstIncompleteBracket(text, i);
            return string.Concat(text.AsSpan(0, firstIncomplete), text.AsSpan(firstIncomplete + 1));
        }
        // Preserve the text and close the link with a placeholder URL
        return text + LinkSuffix;
    }

    // Helper function to handle incomplete link text (unclosed brackets)
    private static string? HandleIncompleteText(string text, int i, LinkMode linkMode)
    {
        // Check if there's a ! before it
        var isImage = i > 0 && text[i - 1] == '!';
        var openIndex = isImage ? i - 1 : i;

        // Check if we have a closing bracket after this
        if (IndexOf(text, ']', i + 1) == -1)
        {
            // This is an incomplete link/image
            return HealUnclosedBracket(text, i, isImage, openIndex, linkMode);
        }

        // If we found a closing bracket, we need to check if it's the matching one
        // (accounting for nested brackets)
        if (RemendUtils.FindMatchingClosingBracket(text, i) == -1)
        {
            // No matching closing bracket
            return HealUnclosedBracket(text, i, isImage, openIndex, linkMode);
        }

        return null;
    }

    // One healing step over the trailing incomplete link or image, if any
    private static string HealTrailingLinkOrImage(string text, LinkMode linkMode)
    {
        // Look for patterns like [text]( or ![text]( at the end of text
        // We need to handle nested brackets in the link text

        // Start from the end and look for ]( pattern
        var lastParenIndex = LastIndexOf(text, "](");
        if (lastParenIndex != -1 && !RemendUtils.IsInsideCodeBlock(text, lastParenIndex))
        {
            var result = HandleIncompleteUrl(text, lastParenIndex, linkMode);
            if (result is not null)
            {
                return result;
            }
        }

        // Then check for incomplete link text: [partial-text without closing ]
        // Search backwards for an opening bracket that doesn't have a matching closing bracket
        for (var i = text.LastIndexOf('['); i != -1; i = i == 0 ? -1 : text.LastIndexOf('[', i - 1))
        {
            if (!RemendUtils.IsInsideCodeBlock(text, i))
            {
                var result = HandleIncompleteText(text, i, linkMode);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        return text;
    }

    // A pass costs a full rescan of the shortened text, so an adversarial tail
    // of thousands of nested incomplete constructs would turn healing quadratic
    // without a bound. Realistic nesting depth is single digits.
    private const int MaxHealPasses = 32;

    /// <summary>以占位标记保留未完成的链接与图片。</summary>
    public static string HandleIncompleteLinksAndImages(string text, LinkMode linkMode = LinkMode.Protocol)
    {
        var current = text;

        // Removing a trailing incomplete image can expose another incomplete
        // construct that ended immediately before it (an input ending in "![!["
        // heals to "![", which is itself incomplete). Iterate until healed text
        // re-heals to itself, so healing is idempotent. A step that grows the text
        // has completed the construct, and truncating steps strictly shorten,
        // so the loop terminates.
        for (var pass = 0; pass < MaxHealPasses; pass++)
        {
            var next = HealTrailingLinkOrImage(current, linkMode);
            if (next.Length >= current.Length)
            {
                return next;
            }
            current = next;
        }
        return current;
    }
}
