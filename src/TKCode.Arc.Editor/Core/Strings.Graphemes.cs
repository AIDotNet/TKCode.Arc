// Ported from vscode (src/vs/base/common/strings.ts, commit b57d5c9e), MIT license: surrogate pairs, code point and
// grapheme iteration, isFullWidthCharacter, isEmojiImprecise, the grapheme break rules and getLeftDeleteOffset.
// Differences from upstream:
//  - JavaScript's charCodeAt returns NaN outside the string; CharCodeAt here returns -1, which no surrogate or emoji
//    test accepts, so the functions step one code unit past either end as upstream does instead of throwing.
//  - getCharContainingOffset returns a (Start, End) tuple instead of a two-element array.
//  - getOffsetBeforeLastEmojiComponent returns -1 where upstream returns undefined.
//  - getPrevCodePoint and breakBetweenGraphemeBreakType are internal (module-private upstream), for the iterators.

namespace TKCode.Arc.Editor;

internal static partial class Strings
{
    private const int CodePointZwj = 0x200D;

    // Variation Selector-16 (VS16)
    private const int CodePointEmojiVariantSelector = 0xFE0F;

    // Combining Enclosing Keycap
    private const int CodePointEnclosingKeyCap = 0x20E3;

    /// <summary>The UTF-16 code unit at <paramref name="offset"/>, or -1 outside the string (NaN in JavaScript).</summary>
    private static int CharCodeAt(string str, int offset) => (uint)offset < (uint)str.Length ? str[offset] : -1;

    /// <summary>See http://en.wikipedia.org/wiki/Surrogate_pair.</summary>
    public static bool IsHighSurrogate(int charCode) => 0xD800 <= charCode && charCode <= 0xDBFF;

    /// <summary>See http://en.wikipedia.org/wiki/Surrogate_pair.</summary>
    public static bool IsLowSurrogate(int charCode) => 0xDC00 <= charCode && charCode <= 0xDFFF;

    /// <summary>See http://en.wikipedia.org/wiki/Surrogate_pair.</summary>
    public static int ComputeCodePoint(int highSurrogate, int lowSurrogate) =>
        ((highSurrogate - 0xD800) << 10) + (lowSurrogate - 0xDC00) + 0x10000;

    /// <summary>The code point that begins at offset <paramref name="offset"/>.</summary>
    public static int GetNextCodePoint(string str, int len, int offset)
    {
        var charCode = CharCodeAt(str, offset);
        if (IsHighSurrogate(charCode) && offset + 1 < len)
        {
            var nextCharCode = CharCodeAt(str, offset + 1);
            if (IsLowSurrogate(nextCharCode))
            {
                return ComputeCodePoint(charCode, nextCharCode);
            }
        }
        return charCode;
    }

    /// <summary>The code point that ends right before offset <paramref name="offset"/>.</summary>
    internal static int GetPrevCodePoint(string str, int offset)
    {
        var charCode = CharCodeAt(str, offset - 1);
        if (IsLowSurrogate(charCode) && offset > 1)
        {
            var prevCharCode = CharCodeAt(str, offset - 2);
            if (IsHighSurrogate(prevCharCode))
            {
                return ComputeCodePoint(prevCharCode, charCode);
            }
        }
        return charCode;
    }

    /// <summary>The length in code units of the grapheme that starts at <paramref name="initialOffset"/>.</summary>
    public static int NextCharLength(string str, int initialOffset)
    {
        var iterator = new GraphemeIterator(str, initialOffset);
        return iterator.NextGraphemeLength();
    }

    /// <summary>The length in code units of the grapheme that ends at <paramref name="initialOffset"/>.</summary>
    public static int PrevCharLength(string str, int initialOffset)
    {
        var iterator = new GraphemeIterator(str, initialOffset);
        return iterator.PrevGraphemeLength();
    }

    /// <summary>The start and end offsets of the grapheme that contains <paramref name="offset"/>.</summary>
    public static (int Start, int End) GetCharContainingOffset(string str, int offset)
    {
        if (offset > 0 && IsLowSurrogate(CharCodeAt(str, offset)))
        {
            offset--;
        }
        var endOffset = offset + NextCharLength(str, offset);
        var startOffset = endOffset - PrevCharLength(str, endOffset);
        return (startOffset, endOffset);
    }

    /// <summary>
    /// Whether <paramref name="charCode"/> is in a block of wide (East Asian) characters, which are counted as two
    /// columns. A cheap approximation.
    /// </summary>
    public static bool IsFullWidthCharacter(int charCode)
    {
        // Do a cheap trick to better support wrapping of wide characters, treat them as 2 columns
        // http://jrgraphix.net/research/unicode_blocks.php
        //          2E80 - 2EFF   CJK Radicals Supplement
        //          2F00 - 2FDF   Kangxi Radicals
        //          2FF0 - 2FFF   Ideographic Description Characters
        //          3000 - 303F   CJK Symbols and Punctuation
        //          3040 - 309F   Hiragana
        //          30A0 - 30FF   Katakana
        //          3100 - 312F   Bopomofo
        //          3130 - 318F   Hangul Compatibility Jamo
        //          3190 - 319F   Kanbun
        //          31A0 - 31BF   Bopomofo Extended
        //          31F0 - 31FF   Katakana Phonetic Extensions
        //          3200 - 32FF   Enclosed CJK Letters and Months
        //          3300 - 33FF   CJK Compatibility
        //          3400 - 4DBF   CJK Unified Ideographs Extension A
        //          4DC0 - 4DFF   Yijing Hexagram Symbols
        //          4E00 - 9FFF   CJK Unified Ideographs
        //          A000 - A48F   Yi Syllables
        //          A490 - A4CF   Yi Radicals
        //          AC00 - D7AF   Hangul Syllables
        // [IGNORE] D800 - DB7F   High Surrogates
        // [IGNORE] DB80 - DBFF   High Private Use Surrogates
        // [IGNORE] DC00 - DFFF   Low Surrogates
        // [IGNORE] E000 - F8FF   Private Use Area
        //          F900 - FAFF   CJK Compatibility Ideographs
        // [IGNORE] FB00 - FB4F   Alphabetic Presentation Forms
        // [IGNORE] FB50 - FDFF   Arabic Presentation Forms-A
        // [IGNORE] FE00 - FE0F   Variation Selectors
        // [IGNORE] FE20 - FE2F   Combining Half Marks
        // [IGNORE] FE30 - FE4F   CJK Compatibility Forms
        // [IGNORE] FE50 - FE6F   Small Form Variants
        // [IGNORE] FE70 - FEFF   Arabic Presentation Forms-B
        //          FF00 - FFEF   Halfwidth and Fullwidth Forms
        //               [https://en.wikipedia.org/wiki/Halfwidth_and_fullwidth_forms]
        //               of which FF01 - FF5E fullwidth ASCII of 21 to 7E
        //               and FFE0 - FFE6 fullwidth symbol variants
        // [IGNORE]    and FF65 - FFDC halfwidth of Katakana and Hangul
        // [IGNORE] FFF0 - FFFF   Specials
        return (charCode >= 0x2E80 && charCode <= 0xD7AF)
            || (charCode >= 0xF900 && charCode <= 0xFAFF)
            || (charCode >= 0xFF01 && charCode <= 0xFF5E)
            || (charCode >= 0xFFE0 && charCode <= 0xFFE6);
    }

    /// <summary>
    /// A fast function (therefore imprecise) to check if code points are emojis.
    /// Generated using https://github.com/alexdima/unicode-utils/blob/main/emoji-test.js.
    /// </summary>
    public static bool IsEmojiImprecise(int x)
    {
        return (x >= 0x1F1E6 && x <= 0x1F1FF) || x == 8986 || x == 8987 || x == 9200
            || x == 9203 || (x >= 9728 && x <= 10175) || x == 11088 || x == 11093
            || (x >= 127744 && x <= 128591) || (x >= 128640 && x <= 128764)
            || (x >= 128992 && x <= 129008) || (x >= 129280 && x <= 129535)
            || (x >= 129648 && x <= 129782);
    }

    /// <summary>The grapheme cluster break property of <paramref name="codePoint"/>.</summary>
    public static GraphemeBreakType GetGraphemeBreakType(int codePoint)
    {
        var graphemeBreakTree = GraphemeBreakTree.GetInstance();
        return graphemeBreakTree.GetGraphemeBreakType(codePoint);
    }

    /// <summary>Whether there is a grapheme cluster boundary between two code points of these types.</summary>
    internal static bool BreakBetweenGraphemeBreakType(GraphemeBreakType breakTypeA, GraphemeBreakType breakTypeB)
    {
        // http://www.unicode.org/reports/tr29/#Grapheme_Cluster_Boundary_Rules

        // !!! Let's make the common case a bit faster
        if (breakTypeA == GraphemeBreakType.Other)
        {
            // see https://www.unicode.org/Public/13.0.0/ucd/auxiliary/GraphemeBreakTest-13.0.0d10.html#table
            return breakTypeB != GraphemeBreakType.Extend && breakTypeB != GraphemeBreakType.SpacingMark;
        }

        // Do not break between a CR and LF. Otherwise, break before and after controls.
        // GB3                                        CR × LF
        // GB4                       (Control | CR | LF) ÷
        // GB5                                           ÷ (Control | CR | LF)
        if (breakTypeA == GraphemeBreakType.CR)
        {
            if (breakTypeB == GraphemeBreakType.LF)
            {
                return false; // GB3
            }
        }
        if (breakTypeA is GraphemeBreakType.Control or GraphemeBreakType.CR or GraphemeBreakType.LF)
        {
            return true; // GB4
        }
        if (breakTypeB is GraphemeBreakType.Control or GraphemeBreakType.CR or GraphemeBreakType.LF)
        {
            return true; // GB5
        }

        // Do not break Hangul syllable sequences.
        // GB6                                         L × (L | V | LV | LVT)
        // GB7                                  (LV | V) × (V | T)
        // GB8                                 (LVT | T) × T
        if (breakTypeA == GraphemeBreakType.L)
        {
            if (breakTypeB is GraphemeBreakType.L or GraphemeBreakType.V or GraphemeBreakType.LV or GraphemeBreakType.LVT)
            {
                return false; // GB6
            }
        }
        if (breakTypeA is GraphemeBreakType.LV or GraphemeBreakType.V)
        {
            if (breakTypeB is GraphemeBreakType.V or GraphemeBreakType.T)
            {
                return false; // GB7
            }
        }
        if (breakTypeA is GraphemeBreakType.LVT or GraphemeBreakType.T)
        {
            if (breakTypeB == GraphemeBreakType.T)
            {
                return false; // GB8
            }
        }

        // Do not break before extending characters or ZWJ.
        // GB9                                           × (Extend | ZWJ)
        if (breakTypeB is GraphemeBreakType.Extend or GraphemeBreakType.ZWJ)
        {
            return false; // GB9
        }

        // The GB9a and GB9b rules only apply to extended grapheme clusters:
        // Do not break before SpacingMarks, or after Prepend characters.
        // GB9a                                          × SpacingMark
        // GB9b                                  Prepend ×
        if (breakTypeB == GraphemeBreakType.SpacingMark)
        {
            return false; // GB9a
        }
        if (breakTypeA == GraphemeBreakType.Prepend)
        {
            return false; // GB9b
        }

        // Do not break within emoji modifier sequences or emoji zwj sequences.
        // GB11    \p{Extended_Pictographic} Extend* ZWJ × \p{Extended_Pictographic}
        if (breakTypeA == GraphemeBreakType.ZWJ && breakTypeB == GraphemeBreakType.ExtendedPictographic)
        {
            // Note: we are not implementing the rule entirely here to avoid introducing states
            return false; // GB11
        }

        // GB12                          sot (RI RI)* RI × RI
        // GB13                        [^RI] (RI RI)* RI × RI
        if (breakTypeA == GraphemeBreakType.RegionalIndicator && breakTypeB == GraphemeBreakType.RegionalIndicator)
        {
            // Note: we are not implementing the rule entirely here to avoid introducing states
            return false; // GB12 & GB13
        }

        // GB999                                     Any ÷ Any
        return true;
    }

    /// <summary>
    /// Computes the offset after performing a left delete on the given string, while considering unicode
    /// grapheme/emoji rules.
    /// </summary>
    public static int GetLeftDeleteOffset(int offset, string str)
    {
        if (offset == 0)
        {
            return 0;
        }

        // Try to delete emoji part.
        var emojiOffset = GetOffsetBeforeLastEmojiComponent(offset, str);
        if (emojiOffset != -1)
        {
            return emojiOffset;
        }

        // Otherwise, just skip a single code point.
        var iterator = new CodePointIterator(str, offset);
        iterator.PrevCodePoint();
        return iterator.Offset;
    }

    private static int GetOffsetBeforeLastEmojiComponent(int initialOffset, string str)
    {
        // See https://www.unicode.org/reports/tr51/tr51-14.html#EBNF_and_Regex for the
        // structure of emojis.
        var iterator = new CodePointIterator(str, initialOffset);
        var codePoint = iterator.PrevCodePoint();

        // Skip modifiers
        while (IsEmojiModifier(codePoint) || codePoint == CodePointEmojiVariantSelector || codePoint == CodePointEnclosingKeyCap)
        {
            if (iterator.Offset == 0)
            {
                // Cannot skip modifier, no preceding emoji base.
                return -1;
            }
            codePoint = iterator.PrevCodePoint();
        }

        // Expect base emoji
        if (!IsEmojiImprecise(codePoint))
        {
            // Unexpected code point, not a valid emoji.
            return -1;
        }

        var resultOffset = iterator.Offset;

        if (resultOffset > 0)
        {
            // Skip optional ZWJ code points that combine multiple emojis.
            // In theory, we should check if that ZWJ actually combines multiple emojis
            // to prevent deleting ZWJs in situations we didn't account for.
            var optionalZwjCodePoint = iterator.PrevCodePoint();
            if (optionalZwjCodePoint == CodePointZwj)
            {
                resultOffset = iterator.Offset;
            }
        }

        return resultOffset;
    }

    private static bool IsEmojiModifier(int codePoint) => 0x1F3FB <= codePoint && codePoint <= 0x1F3FF;
}
