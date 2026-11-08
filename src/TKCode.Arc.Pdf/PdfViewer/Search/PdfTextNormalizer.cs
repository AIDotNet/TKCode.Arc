using System.Text;

namespace TKCode.Arc;

/// <summary>Text prepared for matching, with the way back: <see cref="Starts"/> and <see cref="Ends"/> give, for each unit of <see cref="Text"/>, the range of the original it came from.</summary>
internal sealed class PdfNormalizedText
{
    public PdfNormalizedText(string text, int[] starts, int[] ends)
    {
        Text = text;
        Starts = starts;
        Ends = ends;
    }

    /// <summary>The normalized text.</summary>
    public string Text { get; }

    /// <summary>Where in the original each unit's source begins.</summary>
    public int[] Starts { get; }

    /// <summary>Where in the original each unit's source ends (exclusive).</summary>
    public int[] Ends { get; }

    /// <summary>The original range of a normalized range. A unit stands for its whole source, so a part of a ligature maps to the ligature and a surrogate pair is never split.</summary>
    public (int Start, int Length) ToOriginal(int start, int length)
    {
        var from = Starts[start];
        return (from, Ends[start + length - 1] - from);
    }
}

/// <summary>
/// Search normalization (design/components/pdf/pdf.md section 5.5): NFKC, case folding unless the case must match, white space
/// compressed to one space, and a word split by a hyphen at a line end joined again. <c>string.Normalize</c> says nothing of
/// where its output came from and can change the length (ligatures, composed marks), so the text is cut into pieces that
/// normalize on their own, a base character with the marks on it, and each output unit remembers its piece.
/// </summary>
internal static class PdfTextNormalizer
{
    private static bool s_noCompatibilityForm;

    /// <summary>
    /// Brings text to NFKC where <c>string.Normalize</c> cannot: .NET in a browser throws <see cref="PlatformNotSupportedException"/>
    /// for that form. The browser backend sets this to the browser's own function when it is turned on.
    /// </summary>
    internal static Func<string, string>? CompatibilityForm { get; set; }

    public static PdfNormalizedText Normalize(string text, bool matchCase)
    {
        var builder = new Builder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (IsSpace(c))
            {
                i = AppendSpace(text, i, builder);
                continue;
            }
            if (IsHyphen(c) && TryJoinAcrossLineEnd(text, i, builder, out var resume))
            {
                i = resume;
                continue;
            }
            if (IsIgnorable(c))
            {
                i++;
                continue;
            }
            if (c == PdfiumHyphen)
            {
                builder.Append('-', i, i + 1);
                i++;
                continue;
            }
            // Nearly all text is ASCII with nothing on top: the common case stays free of allocations.
            if (c < 0x80 && (i + 1 == text.Length || text[i + 1] < 0x80))
            {
                builder.Append(matchCase ? c : char.ToLowerInvariant(c), i, i + 1);
                i++;
                continue;
            }
            var end = PieceEnd(text, i);
            AppendPiece(text, i, end, matchCase, builder);
            i = end;
        }
        return builder.Build();
    }

    /// <summary>White space and the control characters a text extraction leaves in place of it.</summary>
    private static bool IsSpace(char c) => char.IsWhiteSpace(c) || (c < 0x20 && c != PdfiumHyphen);

    /// <summary>PDFium writes a hyphen it takes for a line-end one as this control character, and then leaves the line break out.</summary>
    private const char PdfiumHyphen = '\u0002';

    private const char SoftHyphen = '\u00AD';

    private static bool IsHyphen(char c) => c is '-' or SoftHyphen or PdfiumHyphen or '\u2010';

    /// <summary>Characters with nothing to show: they must not stand between the letters of a word.</summary>
    private static bool IsIgnorable(char c) => c is SoftHyphen or '\u200B' or '\u200C' or '\u200D' or '\u2060' or '\uFEFF';

    private static bool IsLineBreak(char c) => c is '\n' or '\r' or '\u0085' or '\u2028' or '\u2029' or '\f' or '\v';

    private static int AppendSpace(string text, int start, Builder builder)
    {
        var i = start;
        var hasBreak = false;
        while (i < text.Length && IsSpace(text[i]))
        {
            hasBreak |= IsLineBreak(text[i]);
            i++;
        }
        // A line of Chinese ends anywhere and has no space in it: the break is layout, not text.
        if (hasBreak && i < text.Length && builder.LastIsCjk && PdfTextCharacters.IsCjk(PdfTextCharacters.RuneAt(text, i).Value))
        {
            return i;
        }
        builder.AppendSpace(start, i);
        return i;
    }

    /// <summary>A hyphen, then a line break, between two letters: the word goes on. Returns where it does; nothing is written for the hyphen and the break. PDFium's own hyphen says there was a line end by itself.</summary>
    private static bool TryJoinAcrossLineEnd(string text, int hyphen, Builder builder, out int resume)
    {
        resume = hyphen;
        if (!builder.LastIsLetterOrDigit)
        {
            return false;
        }
        var i = hyphen + 1;
        var hasBreak = text[hyphen] == PdfiumHyphen;
        while (i < text.Length && IsSpace(text[i]))
        {
            hasBreak |= IsLineBreak(text[i]);
            i++;
        }
        if (!hasBreak || i == text.Length || !Rune.IsLetterOrDigit(PdfTextCharacters.RuneAt(text, i)))
        {
            return false;
        }
        resume = i;
        return true;
    }

    /// <summary>The end of the piece starting at <paramref name="start"/>: one character and whatever composes with it.</summary>
    private static int PieceEnd(string text, int start)
    {
        var i = start + PdfTextCharacters.LengthAt(text, start);
        while (i < text.Length)
        {
            var rune = PdfTextCharacters.RuneAt(text, i);
            var composes = Rune.GetUnicodeCategory(rune) is System.Globalization.UnicodeCategory.NonSpacingMark
                               or System.Globalization.UnicodeCategory.SpacingCombiningMark
                               or System.Globalization.UnicodeCategory.EnclosingMark
                           // Conjoining Hangul vowels and finals: letters to Unicode, one syllable to NFKC.
                           || rune.Value is >= 0x1160 and <= 0x11FF;
            if (!composes)
            {
                break;
            }
            i += rune.Utf16SequenceLength;
        }
        return i;
    }

    /// <summary>CJK ideographs and Hangul syllables are in NFKC already and have no case. Whole pages of them must not go through a string each, least of all into JavaScript.</summary>
    private static bool StaysAsItIs(char c) => c is (>= '\u3400' and <= '\u4DBF') or (>= '\u4E00' and <= '\u9FFF') or (>= '\uAC00' and <= '\uD7A3');

    private static void AppendPiece(string text, int start, int end, bool matchCase, Builder builder)
    {
        if (end == start + 1 && StaysAsItIs(text[start]))
        {
            builder.Append(new Rune(text[start]), start, end);
            return;
        }
        var piece = NormalizePiece(text.AsSpan(start, end - start));
        foreach (var rune in piece.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                // NFKC may produce spaces (a no-break space inside a compatibility form).
                builder.AppendSpace(start, end);
            }
            else if (matchCase)
            {
                builder.Append(rune, start, end);
            }
            else
            {
                AppendFolded(rune, start, end, builder);
            }
        }
    }

    private static string NormalizePiece(ReadOnlySpan<char> piece)
    {
        // Normalize throws on a lone surrogate, and a backend or a file may produce one.
        var clean = new StringBuilder(piece.Length);
        for (var i = 0; i < piece.Length;)
        {
            if (Rune.DecodeFromUtf16(piece[i..], out var rune, out var consumed) == System.Buffers.OperationStatus.Done)
            {
                clean.Append(rune.ToString());
            }
            else
            {
                clean.Append('\uFFFD');
            }
            i += consumed > 0 ? consumed : 1;
        }
        return ToCompatibilityForm(clean.ToString());
    }

    private static string ToCompatibilityForm(string text)
    {
        if (CompatibilityForm is { } form)
        {
            return form(text);
        }
        if (s_noCompatibilityForm)
        {
            return text;
        }
        try
        {
            return text.Normalize(NormalizationForm.FormKC);
        }
        catch (PlatformNotSupportedException)
        {
            // A browser without the backend turned on (simulated documents): plain matching still finds nearly everything.
            s_noCompatibilityForm = true;
            return text;
        }
    }

    /// <summary>Case folding by way of upper and then lower case, which unites the forms of one letter (final sigma); the sharp s has no single upper case, so it is folded by hand.</summary>
    private static void AppendFolded(Rune rune, int start, int end, Builder builder)
    {
        if (rune.Value is 0xDF or 0x1E9E)
        {
            builder.Append(new Rune('s'), start, end);
            builder.Append(new Rune('s'), start, end);
            return;
        }
        builder.Append(Rune.ToLowerInvariant(Rune.ToUpperInvariant(rune)), start, end);
    }

    private sealed class Builder
    {
        private readonly StringBuilder _text;
        private int[] _starts;
        private int[] _ends;

        public Builder(int capacity)
        {
            _text = new StringBuilder(capacity);
            _starts = new int[Math.Max(capacity, 4)];
            _ends = new int[_starts.Length];
        }

        public bool LastIsCjk => TryLastRune(out var rune) && PdfTextCharacters.IsCjk(rune.Value);

        public bool LastIsLetterOrDigit => TryLastRune(out var rune) && Rune.IsLetterOrDigit(rune);

        private bool TryLastRune(out Rune rune)
        {
            rune = default;
            var length = _text.Length;
            if (length == 0)
            {
                return false;
            }
            var last = _text[length - 1];
            if (char.IsLowSurrogate(last) && length > 1 && char.IsHighSurrogate(_text[length - 2]))
            {
                rune = new Rune(_text[length - 2], last);
                return true;
            }
            return Rune.TryCreate(last, out rune);
        }

        public void Append(char c, int start, int end)
        {
            Grow(1);
            _starts[_text.Length] = start;
            _ends[_text.Length] = end;
            _text.Append(c);
        }

        public void Append(Rune rune, int start, int end)
        {
            Span<char> units = stackalloc char[2];
            var count = rune.EncodeToUtf16(units);
            for (var i = 0; i < count; i++)
            {
                Append(units[i], start, end);
            }
        }

        /// <summary>One space for a run of them; another run right after (with only ignorable characters between) widens the same one.</summary>
        public void AppendSpace(int start, int end)
        {
            if (_text.Length > 0 && _text[^1] == ' ')
            {
                _ends[_text.Length - 1] = end;
                return;
            }
            Append(' ', start, end);
        }

        public PdfNormalizedText Build()
        {
            var length = _text.Length;
            return new PdfNormalizedText(_text.ToString(), _starts[..length], _ends[..length]);
        }

        private void Grow(int more)
        {
            var needed = _text.Length + more;
            if (needed > _starts.Length)
            {
                var size = Math.Max(needed, _starts.Length * 2);
                Array.Resize(ref _starts, size);
                Array.Resize(ref _ends, size);
            }
        }
    }
}
