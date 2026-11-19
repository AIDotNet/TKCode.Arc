// Ported from vercel/streamdown packages/remend (src/emphasis-handlers.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

using static StreamMark.Core.Remend.JsText;

namespace StreamMark.Core.Remend;

/// <summary>补全未闭合的强调标记：<c>***</c>、<c>**</c>、<c>__</c>、<c>*</c>、<c>_</c>。</summary>
internal static class EmphasisHandlers
{
    private static bool IsWhitespaceChar(int c) => c is ' ' or '\t' or '\n';

    // Helper function to check if an asterisk should be skipped
    private static bool ShouldSkipAsterisk(TextScan scan, int index, int prevChar, int nextChar)
    {
        // Skip if escaped with backslash
        if (prevChar == '\\')
        {
            return true;
        }

        // Skip if within math block
        if (scan.InMathAt(index))
        {
            return true;
        }

        // Special handling for *** sequences
        // If this is the first * in ***, don't skip it - it can close a single * italic
        // Example: **bold and *italic*** should count the first * of *** as closing the italic
        if (prevChar != '*' && nextChar == '*')
        {
            var nextNextChar = index < scan.Length - 2 ? scan.Text[index + 2] : None;
            // First * of *** counts as a single asterisk; first * of ** does not
            return nextNextChar != '*';
        }

        // Skip if this is the second or third * in a sequence
        if (prevChar == '*')
        {
            return true;
        }

        // Skip if flanked by whitespace on both sides (not a valid emphasis delimiter per CommonMark)
        // This also catches list markers (e.g., "* item") since they have whitespace on both sides
        var prevIsWhitespace = prevChar == None || IsWhitespaceChar(prevChar);
        var nextIsWhitespace = nextChar == None || IsWhitespaceChar(nextChar);
        return prevIsWhitespace && nextIsWhitespace;
    }

    private static bool IsWordInternalAsterisk(int prevChar, int nextChar) =>
        prevChar != None && nextChar != None && RemendUtils.IsWordChar(prevChar) && RemendUtils.IsWordChar(nextChar);

    // Decide whether a candidate single-asterisk delimiter should participate in the
    // open/close parity count used by incomplete-italic completion.
    //
    // Intraword asterisks only count while resolving an active emphasis chain:
    // they can close an open run and may reopen within the same word after a close.
    // Cold (unmatched) intraword asterisks stay literal (hello*world / #189).
    // Trailing right-flanking-only markers after a closed run also stay literal
    // (*foo*bar* / a *b*c*).
    private static bool ShouldCountSingleAsterisk(
        int prevChar, int nextChar, int count, ref bool inWordAsteriskChain)
    {
        var isWordInternal = IsWordInternalAsterisk(prevChar, nextChar);
        var canOpen = nextChar != None && !IsWhitespaceChar(nextChar);
        var canClose = prevChar != None && !IsWhitespaceChar(prevChar);

        // Do not start emphasis from a cold word-internal asterisk.
        if (isWordInternal && count % 2 == 0 && !inWordAsteriskChain)
        {
            return false;
        }

        // Prefer closing an active run; only open when left-flanking.
        if ((canClose && count % 2 == 1) || canOpen)
        {
            inWordAsteriskChain = isWordInternal;
            return true;
        }

        return false;
    }

    public static int CountSingleAsterisks(string text)
    {
        var scan = ScanCache.Get(text);
        var count = 0;
        var inWordAsteriskChain = false;
        var len = text.Length;

        for (var index = 0; index < len; index++)
        {
            // Code regions neither contribute delimiters nor belong to a word chain
            if (text[index] != '*' || scan.Regions[index] != Region.Prose)
            {
                if (!RemendUtils.IsWordChar(text[index]))
                {
                    inWordAsteriskChain = false;
                }
                continue;
            }

            var prevChar = index > 0 ? text[index - 1] : None;
            var nextChar = index < len - 1 ? text[index + 1] : None;

            if (ShouldSkipAsterisk(scan, index, prevChar, nextChar))
            {
                continue;
            }

            if (ShouldCountSingleAsterisk(prevChar, nextChar, count, ref inWordAsteriskChain))
            {
                count++;
            }
        }

        return count;
    }

    // Helper function to check if an underscore should be skipped
    private static bool ShouldSkipUnderscore(TextScan scan, int index, int prevChar, int nextChar)
    {
        // Skip if escaped with backslash
        if (prevChar == '\\')
        {
            return true;
        }

        // Skip if within math block, a link or image URL, or an HTML tag (e.g. <a target="_blank">)
        if (scan.InMathAt(index) || scan.InLinkUrlAt(index) || scan.InHtmlTagAt(index))
        {
            return true;
        }

        // Skip if part of __
        if (prevChar == '_' || nextChar == '_')
        {
            return true;
        }

        // Skip if underscore is word-internal (between word characters)
        return prevChar != None && nextChar != None && RemendUtils.IsWordChar(prevChar) && RemendUtils.IsWordChar(nextChar);
    }

    public static int CountSingleUnderscores(string text)
    {
        var scan = ScanCache.Get(text);
        var count = 0;
        var len = text.Length;

        for (var index = 0; index < len; index++)
        {
            if (text[index] != '_' || scan.Regions[index] != Region.Prose)
            {
                continue;
            }

            var prevChar = index > 0 ? text[index - 1] : None;
            var nextChar = index < len - 1 ? text[index + 1] : None;

            if (!ShouldSkipUnderscore(scan, index, prevChar, nextChar))
            {
                count++;
            }
        }

        return count;
    }

    // Counts triple asterisks that are not part of quadruple or more asterisks
    // and not inside code regions
    public static int CountTripleAsterisks(string text)
    {
        var scan = ScanCache.Get(text);
        var count = 0;
        var consecutiveAsterisks = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '*' && scan.Regions[i] == Region.Prose)
            {
                consecutiveAsterisks++;
            }
            else
            {
                // End of asterisk sequence
                if (consecutiveAsterisks >= 3)
                {
                    count += consecutiveAsterisks / 3;
                }
                consecutiveAsterisks = 0;
            }
        }

        // Handle trailing asterisks
        if (consecutiveAsterisks >= 3)
        {
            count += consecutiveAsterisks / 3;
        }

        return count;
    }

    private static int CountDoubleAsterisks(string text) => TextScan.CountDoublePairs(text, '*');

    // Whether the text has an unmatched __ delimiter, counted per maximal
    // underscore run.
    //
    // Counting raw occurrences misreads identifiers: a name like snake__case
    // contains __ but cannot open or close emphasis, and counting it either
    // invents a closer (odd count) or pairs it against a real delimiter and
    // swallows a closer that was needed (even count). Per run:
    //
    // - A run contributes floor(length / 2) pairs, and flips delimiter parity
    //   only when that is odd. __ and ___ flip; ____ does not.
    // - A word-internal run (word characters on both sides) is part of an
    //   identifier, never a delimiter.
    // - Runs inside code regions, math, link URLs, and HTML tags are skipped,
    //   matching the single-underscore handler's flanking rules.
    // - A run alone on its line is a thematic break, not emphasis.
    private static bool IsLineBoundaryChar(int c) => c is None or ' ' or '\t' or '\n';

    // isHorizontalRule scans the run's whole line, so its verdict is memoized
    // per line to keep run counting linear when many runs share a line
    private struct ThematicBreakMemo
    {
        public int LineEnd;
        public bool Result;
    }

    private static bool IsThematicBreakRun(string text, int runStart, int prevChar, int nextChar, ref ThematicBreakMemo memo)
    {
        // A thematic break line holds only markers and whitespace, so only runs
        // flanked by whitespace or line boundaries can be part of one
        if (!(IsLineBoundaryChar(prevChar) && IsLineBoundaryChar(nextChar)))
        {
            return false;
        }
        if (runStart > memo.LineEnd)
        {
            var lineEnd = IndexOf(text, '\n', runStart);
            memo.LineEnd = lineEnd == -1 ? text.Length : lineEnd;
            memo.Result = RemendUtils.IsHorizontalRule(text, runStart, '_');
        }
        return memo.Result;
    }

    // Whether an underscore run at [runStart, runEnd) flips delimiter parity
    private static bool DoubleUnderscoreRunFlips(TextScan scan, int initialRunStart, int runEnd, ref ThematicBreakMemo memo)
    {
        var text = scan.Text;

        // A backslash escapes the first underscore of the run. The escaped
        // underscore is literal punctuation, so the rest of the run still flanks
        // as a delimiter.
        var runStart = initialRunStart;
        var escaped = false;
        if (runStart > 0 && text[runStart - 1] == '\\')
        {
            runStart++;
            escaped = true;
        }
        var runLength = runEnd - runStart;
        if (runLength < 2)
        {
            return false;
        }

        var beforeRun = runStart > 0 ? text[runStart - 1] : None;
        var prevChar = escaped ? '\\' : beforeRun;
        var nextChar = runEnd < text.Length ? text[runEnd] : None;
        if (RemendUtils.IsWordChar(prevChar) && RemendUtils.IsWordChar(nextChar))
        {
            return false;
        }
        if (IsThematicBreakRun(text, runStart, prevChar, nextChar, ref memo))
        {
            return false;
        }
        if (scan.InMathAt(runStart) || scan.InLinkUrlAt(runStart) || scan.InHtmlTagAt(runStart))
        {
            return false;
        }

        return runLength / 2 % 2 == 1;
    }

    private static bool HasUnmatchedDoubleUnderscore(string text)
    {
        var scan = ScanCache.Get(text);
        var n = text.Length;
        var memo = new ThematicBreakMemo { LineEnd = -1, Result = false };
        var unmatched = false;
        var i = 0;

        while (i < n)
        {
            if (text[i] != '_' || scan.Regions[i] != Region.Prose)
            {
                i++;
                continue;
            }

            var runStart = i;
            var runEnd = i + 1;
            while (runEnd < n && text[runEnd] == '_' && scan.Regions[runEnd] == Region.Prose)
            {
                runEnd++;
            }
            i = runEnd;

            if (DoubleUnderscoreRunFlips(scan, runStart, runEnd, ref memo))
            {
                unmatched = !unmatched;
            }
        }

        return unmatched;
    }

    private static bool IsInCode(string text, int position) =>
        RemendUtils.IsInsideCodeBlock(text, position) || RemendUtils.IsWithinCompleteInlineCode(text, position);

    private static bool IsEmptyOrMarkers(ReadOnlySpan<char> content) =>
        content.IsEmpty || Patterns.WhitespaceOrMarkers().IsMatch(content);

    // Shared by bold and italic: skip when there is no meaningful content, when a
    // list item's marker is followed by multiline content, or on a horizontal rule
    private static bool ShouldSkipCompletion(string text, string contentAfterMarker, int markerIndex, char marker)
    {
        if (IsEmptyOrMarkers(contentAfterMarker))
        {
            return true;
        }

        // Check if this is in a list item with multiline content
        var lastNewlineBeforeMarker = markerIndex > 0 ? LastIndexOf(text, '\n', markerIndex - 1) : -1;
        var lineStart = lastNewlineBeforeMarker == -1 ? 0 : lastNewlineBeforeMarker + 1;
        var lineBeforeMarker = Substring(text, lineStart, markerIndex);

        if (Patterns.ListItem().IsMatch(lineBeforeMarker) && contentAfterMarker.Contains('\n'))
        {
            return true;
        }

        return RemendUtils.IsHorizontalRule(text, markerIndex, marker);
    }

    /// <summary>补全未闭合的粗体（<c>**</c>）。</summary>
    public static string HandleIncompleteBold(string text)
    {
        var boldMatch = Patterns.MatchTrailing(text, Patterns.Bold);
        if (boldMatch is null)
        {
            return text;
        }

        var contentAfterMarker = boldMatch.Groups[2].Value;
        var markerIndex = LastIndexOf(text, boldMatch.Groups[1].Value);

        // Check if the bold marker is within a code block (fenced or inline)
        if (IsInCode(text, markerIndex))
        {
            return text;
        }

        if (ShouldSkipCompletion(text, contentAfterMarker, markerIndex, '*'))
        {
            return text;
        }

        var asteriskPairs = CountDoubleAsterisks(text);
        if (asteriskPairs % 2 == 1)
        {
            // Check for half-complete closing marker: **content* should become **content**
            // The trailing * is the first char of the closing ** being streamed
            return EndsWith(contentAfterMarker, '*') ? text + "*" : text + "**";
        }

        return text;
    }

    /// <summary>补全未闭合的双下划线斜体（<c>__</c>）。</summary>
    public static string HandleIncompleteDoubleUnderscoreItalic(string text)
    {
        var italicMatch = Patterns.MatchTrailing(text, Patterns.Italic);
        if (italicMatch is null)
        {
            // Check for half-complete closing marker: __content_ should become __content__
            // The pattern /(__)([^_]*?)$/ won't match __content_ because it ends with _
            // So we need a separate check for this case
            var halfCompleteMatch = Patterns.MatchTrailing(text, Patterns.HalfCompleteUnderscore);
            if (halfCompleteMatch is not null)
            {
                var halfMarkerIndex = LastIndexOf(text, halfCompleteMatch.Groups[1].Value);
                if (!IsInCode(text, halfMarkerIndex) && HasUnmatchedDoubleUnderscore(text))
                {
                    return text + "_";
                }
            }
            return text;
        }

        var contentAfterMarker = italicMatch.Groups[2].Value;
        var markerIndex = LastIndexOf(text, italicMatch.Groups[1].Value);

        // Check if the italic marker is within a code block (fenced or inline)
        if (IsInCode(text, markerIndex))
        {
            return text;
        }

        if (ShouldSkipCompletion(text, contentAfterMarker, markerIndex, '_'))
        {
            return text;
        }

        return HasUnmatchedDoubleUnderscore(text) ? text + "__" : text;
    }

    // A lone, unescaped asterisk in prose outside math: the only positions that
    // can open incomplete italic
    private static bool IsLoneProseAsterisk(TextScan scan, int i)
    {
        var text = scan.Text;
        return text[i] == '*'
            && scan.Regions[i] == Region.Prose
            && At(text, i - 1) != '*'
            && At(text, i + 1) != '*'
            && At(text, i - 1) != '\\'
            && !scan.InMathAt(i);
    }

    private static int FindFirstSingleAsteriskIndex(string text)
    {
        var scan = ScanCache.Get(text);

        for (var i = IndexOf(text, '*'); i != -1; i = IndexOf(text, '*', i + 1))
        {
            if (!IsLoneProseAsterisk(scan, i))
            {
                continue;
            }
            var prevChar = i > 0 ? text[i - 1] : None;
            var nextChar = i < text.Length - 1 ? text[i + 1] : None;

            // Skip if flanked by whitespace on both sides (not a valid emphasis delimiter)
            var prevIsWs = prevChar == None || IsWhitespaceChar(prevChar);
            var nextIsWs = nextChar == None || IsWhitespaceChar(nextChar);
            if (prevIsWs && nextIsWs)
            {
                continue;
            }

            // Skip cold word-internal asterisks; they are not openers for completion.
            // (Active-run closers are still counted in countSingleAsterisks.)
            if (IsWordInternalAsterisk(prevChar, nextChar))
            {
                continue;
            }

            // A right-flanking-only marker cannot open incomplete italic.
            if (nextIsWs)
            {
                continue;
            }

            return i;
        }
        return -1;
    }

    /// <summary>补全未闭合的单星号斜体（<c>*</c>）。</summary>
    public static string HandleIncompleteSingleAsteriskItalic(string text)
    {
        if (Patterns.MatchTrailing(text, Patterns.SingleAsterisk) is null)
        {
            return text;
        }

        var firstSingleAsteriskIndex = FindFirstSingleAsteriskIndex(text);
        if (firstSingleAsteriskIndex == -1)
        {
            return text;
        }

        // Don't close if the marker is inside a complete inline code span or fenced code block
        if (IsInCode(text, firstSingleAsteriskIndex))
        {
            return text;
        }

        // Don't close if content is only whitespace or emphasis markers
        if (IsEmptyOrMarkers(text.AsSpan(firstSingleAsteriskIndex + 1)))
        {
            return text;
        }

        return CountSingleAsterisks(text) % 2 == 1 ? text + "*" : text;
    }

    private static int FindFirstSingleUnderscoreIndex(string text)
    {
        var scan = ScanCache.Get(text);

        for (var i = IndexOf(text, '_'); i != -1; i = IndexOf(text, '_', i + 1))
        {
            if (scan.Regions[i] == Region.Prose
                && At(text, i - 1) != '_'
                && At(text, i + 1) != '_'
                && At(text, i - 1) != '\\'
                && !scan.InMathAt(i)
                && !scan.InLinkUrlAt(i))
            {
                // Check if underscore is word-internal (between word characters)
                var prevChar = i > 0 ? text[i - 1] : None;
                var nextChar = i < text.Length - 1 ? text[i + 1] : None;
                if (prevChar != None && nextChar != None && RemendUtils.IsWordChar(prevChar) && RemendUtils.IsWordChar(nextChar))
                {
                    continue;
                }

                return i;
            }
        }
        return -1;
    }

    // Helper function to insert closing underscore, handling trailing newlines
    private static string InsertClosingUnderscore(string text)
    {
        // If text ends with newline(s), insert underscore before them
        var endIndex = text.Length;
        while (endIndex > 0 && text[endIndex - 1] == '\n')
        {
            endIndex--;
        }
        if (endIndex < text.Length)
        {
            return string.Concat(text.AsSpan(0, endIndex), "_", text.AsSpan(endIndex));
        }
        return text + "_";
    }

    // Helper to handle trailing ** for proper nesting of _ and ** markers
    private static string? HandleTrailingAsterisksForUnderscore(string text)
    {
        if (!EndsWith(text, "**"))
        {
            return null;
        }

        var textWithoutTrailingAsterisks = text[..^2];
        var asteriskPairsAfterRemoval = CountDoubleAsterisks(textWithoutTrailingAsterisks);

        // If removing trailing ** makes the count odd, it was added to close an unclosed **
        if (asteriskPairsAfterRemoval % 2 != 1)
        {
            return null;
        }

        var firstDoubleAsteriskIndex = IndexOf(textWithoutTrailingAsterisks, "**");
        var underscoreIndex = FindFirstSingleUnderscoreIndex(textWithoutTrailingAsterisks);

        // If ** opened before _, then _ should close before **
        if (firstDoubleAsteriskIndex != -1 && underscoreIndex != -1 && firstDoubleAsteriskIndex < underscoreIndex)
        {
            return textWithoutTrailingAsterisks + "_**";
        }

        return null;
    }

    /// <summary>补全未闭合的单下划线斜体（<c>_</c>）。</summary>
    public static string HandleIncompleteSingleUnderscoreItalic(string text)
    {
        if (Patterns.MatchTrailing(text, Patterns.SingleUnderscore) is null)
        {
            return text;
        }

        var firstSingleUnderscoreIndex = FindFirstSingleUnderscoreIndex(text);
        if (firstSingleUnderscoreIndex == -1)
        {
            return text;
        }

        // Don't close if content is only whitespace or emphasis markers
        if (IsEmptyOrMarkers(text.AsSpan(firstSingleUnderscoreIndex + 1)))
        {
            return text;
        }

        if (IsInCode(text, firstSingleUnderscoreIndex))
        {
            return text;
        }

        if (CountSingleUnderscores(text) % 2 == 1)
        {
            // Check if we need to insert _ before trailing ** for proper nesting
            return HandleTrailingAsterisksForUnderscore(text) ?? InsertClosingUnderscore(text);
        }

        return text;
    }

    // Helper to check if bold-italic markers are already balanced
    private static bool AreBoldItalicMarkersBalanced(string text) =>
        CountDoubleAsterisks(text) % 2 == 0 && CountSingleAsterisks(text) % 2 == 0;

    // Helper to check if bold-italic should be skipped
    private static bool ShouldSkipBoldItalicCompletion(string text, string contentAfterMarker, int markerIndex)
    {
        if (IsEmptyOrMarkers(contentAfterMarker))
        {
            return true;
        }

        if (IsInCode(text, markerIndex))
        {
            return true;
        }

        return RemendUtils.IsHorizontalRule(text, markerIndex, '*');
    }

    /// <summary>补全未闭合的粗斜体（<c>***</c>）。</summary>
    public static string HandleIncompleteBoldItalic(string text)
    {
        // Don't process if text is only asterisks and has 4 or more consecutive asterisks
        if (Patterns.FourOrMoreAsterisks().IsMatch(text))
        {
            return text;
        }

        var boldItalicMatch = Patterns.MatchTrailing(text, Patterns.BoldItalic);
        if (boldItalicMatch is null)
        {
            return text;
        }

        var contentAfterMarker = boldItalicMatch.Groups[2].Value;
        var markerIndex = LastIndexOf(text, boldItalicMatch.Groups[1].Value);

        if (ShouldSkipBoldItalicCompletion(text, contentAfterMarker, markerIndex))
        {
            return text;
        }

        if (CountTripleAsterisks(text) % 2 == 1)
        {
            // If both ** and * are balanced, don't add closing ***
            // The *** is likely overlapping markers (e.g., **bold and *italic***)
            return AreBoldItalicMarkersBalanced(text) ? text : text + "***";
        }

        return text;
    }
}
