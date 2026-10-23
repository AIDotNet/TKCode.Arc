using System.Buffers;
using System.Globalization;
using System.Text;

namespace TKCode.Arc.Primitives;

/// <summary>What a text element is to the word rules of <see cref="WordDiff"/> and <see cref="WordPacer"/>.</summary>
internal enum WordElementKind
{
    /// <summary>Part of a word (letters, digits, punctuation, symbols): consecutive ones form one word.</summary>
    Word,

    /// <summary>Whitespace: separates words.</summary>
    Space,

    /// <summary>A CJK ideograph, kana or hangul element: a word on its own.</summary>
    Cjk
}

/// <summary>
/// Text element (grapheme cluster) classification shared by <see cref="WordDiff"/> and <see cref="WordPacer"/>, so a
/// diff and a stream cut words at the same places.
/// </summary>
internal static class WordText
{
    /// <summary>Length in chars of the text element at the start of <paramref name="text"/> (0 when empty).</summary>
    public static int ElementLength(ReadOnlySpan<char> text)
    {
        if (text.Length <= 1)
        {
            return text.Length;
        }
        // Two ASCII chars only join as CR LF; everything else needs the full grapheme rules.
        var first = text[0];
        if (first < 0x80 && text[1] < 0x80)
        {
            return first == '\r' && text[1] == '\n' ? 2 : 1;
        }
        return StringInfo.GetNextTextElementLength(text);
    }

    /// <summary>Classifies an element by its first code point (a lone surrogate is part of a word).</summary>
    public static WordElementKind Classify(ReadOnlySpan<char> element)
    {
        if (Rune.DecodeFromUtf16(element, out var rune, out _) != OperationStatus.Done)
        {
            return WordElementKind.Word;
        }
        if (Rune.IsWhiteSpace(rune))
        {
            return WordElementKind.Space;
        }
        return IsCjk(rune.Value) ? WordElementKind.Cjk : WordElementKind.Word;
    }

    /// <summary>
    /// True for scripts written without spaces between words, where every element is a word: Han ideographs (with
    /// radicals, 々 〆 〇 and Hangzhou numerals), hiragana, katakana, bopomofo and hangul.
    /// </summary>
    public static bool IsCjk(int codePoint) => codePoint switch
    {
        >= 0x1100 and <= 0x11FF => true, // Hangul Jamo
        >= 0x2E80 and <= 0x2FDF => true, // CJK Radicals Supplement, Kangxi Radicals
        >= 0x3005 and <= 0x3007 => true, // 々 〆 〇
        >= 0x3021 and <= 0x3029 => true, // Hangzhou numerals
        >= 0x3038 and <= 0x303C => true,
        >= 0x3040 and <= 0x30FF => true, // Hiragana, Katakana
        >= 0x3100 and <= 0x312F => true, // Bopomofo
        >= 0x3130 and <= 0x318F => true, // Hangul Compatibility Jamo
        >= 0x31A0 and <= 0x31BF => true, // Bopomofo Extended
        >= 0x31F0 and <= 0x31FF => true, // Katakana Phonetic Extensions
        >= 0x3400 and <= 0x4DBF => true, // CJK Extension A
        >= 0x4E00 and <= 0x9FFF => true, // CJK Unified Ideographs
        >= 0xA960 and <= 0xA97F => true, // Hangul Jamo Extended-A
        >= 0xAC00 and <= 0xD7FF => true, // Hangul Syllables, Jamo Extended-B
        >= 0xF900 and <= 0xFAFF => true, // CJK Compatibility Ideographs
        >= 0xFF66 and <= 0xFFDC => true, // Halfwidth katakana and hangul
        >= 0x1B000 and <= 0x1B16F => true, // Kana Supplement, Extended-A, Small Kana Extension
        >= 0x20000 and <= 0x323AF => true, // CJK Extensions B–I, Compatibility Supplement
        _ => false
    };
}
