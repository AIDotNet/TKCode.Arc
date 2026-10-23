using System.Text;
using TKCode.Arc.Primitives;
using Avalonia;

namespace TKCode.Arc;

/// <summary>How an <see cref="AiDiffText"/> shows a change.</summary>
internal enum AiDiffAppearance
{
    /// <summary>
    /// The side panel's change card: kept words in secondary text, deleted words muted and struck through (no tint, no
    /// sign), inserted words on a soft accent tint with 2px padding; a deleted run and the inserted run that replaces it
    /// read "old new" with a space between.
    /// </summary>
    Card,

    /// <summary>
    /// The prompt studio's compare view: kept words in Foreground, every changed run on a tint (Success for inserted, Danger
    /// for deleted, 3px padding) that starts with a "+" or "−" sign; deleted words are struck through; newlines are kept.
    /// </summary>
    Studio
}

/// <summary>What a run of the text an <see cref="AiDiffText"/> shows is.</summary>
internal enum AiDiffSpanKind : byte
{
    /// <summary>Text in both versions.</summary>
    Equal,

    /// <summary>Text only in the new version.</summary>
    Insert,

    /// <summary>Text only in the old version, shown struck through.</summary>
    Delete,

    /// <summary>
    /// A no-break space sized to <see cref="AiDiffSpan.Width"/>: the padding of a changed run or the gap after its sign.
    /// Shown only; not copied (unless <see cref="AiDiffSpan.CopiesAsSpace"/>).
    /// </summary>
    Space,

    /// <summary>A plain space between a deleted run and the inserted run right after it (card): shown and copied.</summary>
    Separator,

    /// <summary>The "+" that starts an inserted run (studio); shown only.</summary>
    InsertSign,

    /// <summary>The "−" that starts a deleted run (studio); shown only.</summary>
    DeleteSign,

    /// <summary>
    /// The open or close marker of a chip ("{{", "}}"): kept in the text (so a copy reads <c>{{name}}</c>) but drawn
    /// transparent and squeezed to <see cref="AiDiffSpan.Width"/> (the chip's margin and side padding).
    /// </summary>
    ChipBrace,

    /// <summary>The name inside a chip, drawn smaller in the chip's color.</summary>
    ChipName
}

/// <summary>A run of the shown text.</summary>
/// <param name="Start">First index in <see cref="AiDiffDocument.Text"/>.</param>
/// <param name="Length">Number of characters.</param>
/// <param name="Kind">What the run is.</param>
/// <param name="Tone">The chip tone (1 to 4) of a chip brace or name; 0 otherwise.</param>
/// <param name="Width">The width a <see cref="AiDiffSpanKind.Space"/> or <see cref="AiDiffSpanKind.ChipBrace"/> run takes.</param>
/// <param name="CopiesAsSpace">A shown-only space that copies as one plain space (a deleted run touching the inserted one).</param>
internal readonly record struct AiDiffSpan(int Start, int Length, AiDiffSpanKind Kind, int Tone = 0, double Width = 0, bool CopiesAsSpace = false);

/// <summary>What an <see cref="AiDiffMark"/> draws.</summary>
internal enum AiDiffMarkKind : byte
{
    /// <summary>The tint behind an inserted run (sign included).</summary>
    InsertTint,

    /// <summary>The tint behind a deleted run (studio; sign included).</summary>
    DeleteTint,

    /// <summary>The chip of a template variable: a tinted rounded box with a 1px inner outline.</summary>
    Chip,

    /// <summary>The strikethrough of deleted words (the sign is not struck).</summary>
    Strike
}

/// <summary>A decoration on a range of the shown text, drawn by <see cref="AiDiffTextBlock"/>.</summary>
/// <param name="Start">First index in <see cref="AiDiffDocument.Text"/>.</param>
/// <param name="Length">Number of characters.</param>
/// <param name="Kind">What is drawn.</param>
/// <param name="Tone">The chip tone (1 to 4); 0 for the other kinds.</param>
internal readonly record struct AiDiffMark(int Start, int Length, AiDiffMarkKind Kind, int Tone = 0);

/// <summary>
/// The text an <see cref="AiDiffText"/> shows for a word diff, and how to draw it: one string holding the kept, deleted and
/// inserted words in reading order plus the shown-only pieces (paddings, signs, the space between "old new"), the runs
/// that say what each piece is, and the decorations (tints, chips, strikethroughs) as ranges of that string. Pure data:
/// built once per diff, read by the text block when it lays out and draws, and by copy to drop what is not text.
/// </summary>
/// <remarks>
/// Rules: each changed region is the deleted run then the inserted run. A run's leading and trailing white space stays
/// outside its decorations (a change at the end of a line does not tint the line break), white space inside a run belongs
/// to it (one tint, no gaps). A deleted run of only white space is not shown (the new text decides the spacing and the line
/// breaks); an inserted run of only white space is shown plain. Paddings are no-break spaces so they never start a line
/// break of their own (CSS inline padding does not either).
/// </remarks>
internal sealed class AiDiffDocument
{
    /// <summary>No-break space: the character of every padding and gap.</summary>
    internal const char PadChar = ' ';

    /// <summary>The sign of an inserted run.</summary>
    internal const string InsertSignText = "+";

    /// <summary>The sign of a deleted run (U+2212 MINUS SIGN, the width of "+").</summary>
    internal const string DeleteSignText = "−";

    /// <summary>Card: horizontal padding of an inserted run (Arc <c>ins { padding: 0 2px }</c>).</summary>
    internal const double CardPad = 2;

    /// <summary>Studio: horizontal padding of a changed run (Arc <c>padding: 1px 3px</c>).</summary>
    internal const double StudioPad = 3;

    /// <summary>Studio: gap between the sign and the words (Arc <c>margin-right: 3px</c>).</summary>
    internal const double StudioSignGap = 3;

    /// <summary>Width of a chip's open or close marker: 1px margin and 5px padding (Arc <c>padding: 1px 5px; margin: 0 1px</c>).</summary>
    internal const double ChipBraceWidth = 6;

    /// <summary>Corner radius of a change tint.</summary>
    internal const double TintRadius = 4;

    /// <summary>Corner radius of a chip.</summary>
    internal const double ChipRadius = 6;

    /// <summary>Empty document (both texts empty).</summary>
    internal static AiDiffDocument Empty { get; } = new(string.Empty, [], [], 0, 0, false, string.Empty);

    private AiDiffDocument(string text, AiDiffSpan[] spans, AiDiffMark[] marks, int insertedWords, int deletedWords, bool hasChanges,
                           string afterText)
    {
        Text = text;
        Spans = spans;
        Marks = marks;
        InsertedWords = insertedWords;
        DeletedWords = deletedWords;
        HasChanges = hasChanges;
        AfterText = afterText;
    }

    /// <summary>The shown text: kept, deleted and inserted words in reading order with the shown-only pieces.</summary>
    public string Text { get; }

    /// <summary>The runs of <see cref="Text"/>, in order, covering it without gaps.</summary>
    public IReadOnlyList<AiDiffSpan> Spans { get; }

    /// <summary>The decorations: tints first, then chips, then strikethroughs (the order they are drawn in).</summary>
    public IReadOnlyList<AiDiffMark> Marks { get; }

    /// <summary>Inserted words (an atom counts as one).</summary>
    public int InsertedWords { get; }

    /// <summary>Deleted words (an atom counts as one).</summary>
    public int DeletedWords { get; }

    /// <summary>Whether anything changed (white space included).</summary>
    public bool HasChanges { get; }

    /// <summary>The new text exactly (what a screen reader is given).</summary>
    public string AfterText { get; }

    /// <summary>The background outset of a mark: the padding the tint reaches past the words, per appearance.</summary>
    public static Thickness OutsetOf(AiDiffMarkKind kind, AiDiffAppearance appearance) => kind switch
    {
        // The 2px (card) or 3px (studio) padding spaces sit just outside the range, so the tint covers them exactly on a
        // one-line run and repeats the padding on every line of a wrapped one (box-decoration-break: clone).
        AiDiffMarkKind.InsertTint or AiDiffMarkKind.DeleteTint => appearance == AiDiffAppearance.Studio
            ? new Thickness(StudioPad, 1, StudioPad, 1)
            : new Thickness(CardPad, 0, CardPad, 0),
        // The chip box leaves the 1px margin outside on each side and grows the text box to Arc's 18px.
        AiDiffMarkKind.Chip => new Thickness(-1, 0.5, -1, 0.5),
        _ => default
    };

    /// <summary>The corner radius of a mark's background.</summary>
    public static double RadiusOf(AiDiffMarkKind kind) => kind == AiDiffMarkKind.Chip ? ChipRadius : TintRadius;

    /// <summary>
    /// Compares <paramref name="before"/> with <paramref name="after"/> and builds the document. <paramref name="atoms"/> are
    /// the marker pairs of template variables: each is one word for the diff and, with <paramref name="chipTone"/> set, a
    /// chip in the tone it returns (1 to 4; other values wrap); without it atoms are plain words.
    /// </summary>
    /// <remarks>
    /// Line breaks compare as words of their own (a private marker pair around each <c>\n</c> while diffing, removed after,
    /// not counted): <see cref="WordDiff"/> joins the white space between two changes into one changed region, which for a
    /// multi-line prompt would pull the line breaks between two edited lines into the change and move the inserted line
    /// after the deleted paragraph. Kept as words, the line structure of the new text stays as it is.
    /// </remarks>
    public static AiDiffDocument Compare(string before, string after, AiDiffAppearance appearance,
                                         IReadOnlyList<(string Open, string Close)> atoms, Func<string, int>? chipTone)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(atoms);
        if (before.IndexOf('\n') < 0 && after.IndexOf('\n') < 0)
        {
            var plain = WordDiff.Compute(before, after, new WordDiffOptions { Atoms = atoms });
            return Build(plain.Segments, plain.InsertedWords, plain.DeletedWords, plain.HasChanges, appearance, atoms, chipTone);
        }
        var options = new WordDiffOptions { Atoms = [(LineOpen.ToString(), LineClose.ToString()), .. atoms] };
        var diff = WordDiff.Compute(EncodeLines(before), EncodeLines(after), options);
        var segments = new WordDiffSegment[diff.Segments.Count];
        var inserted = diff.InsertedWords;
        var deleted = diff.DeletedWords;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = diff.Segments[i];
            if (segment.Kind != WordDiffKind.Equal)
            {
                // The line-break words were counted: take them out again.
                var breaks = WordDiff.Tokenize(segment.Text, options).Count(token => token == EncodedLineBreak);
                if (segment.Kind == WordDiffKind.Insert)
                {
                    inserted -= breaks;
                }
                else
                {
                    deleted -= breaks;
                }
            }
            segments[i] = new WordDiffSegment(segment.Kind, DecodeLines(segment.Text));
        }
        return Build(segments, inserted, deleted, diff.HasChanges, appearance, atoms, chipTone);
    }

    /// <summary>Builds the document of diff segments (each changed region a Delete then an Insert, as <see cref="WordDiff"/> gives them).</summary>
    public static AiDiffDocument Build(IReadOnlyList<WordDiffSegment> segments, int insertedWords, int deletedWords, bool hasChanges,
                                       AiDiffAppearance appearance, IReadOnlyList<(string Open, string Close)> atoms, Func<string, int>? chipTone)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(atoms);
        if (segments.Count == 0)
        {
            return Empty;
        }
        var builder = new Builder(appearance, atoms, chipTone);
        var after = new StringBuilder();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.Kind == WordDiffKind.Equal)
            {
                builder.Words(segment.Text, AiDiffSpanKind.Equal);
                after.Append(segment.Text);
                continue;
            }
            string? deleted = null;
            string? inserted = null;
            if (segment.Kind == WordDiffKind.Delete)
            {
                deleted = segment.Text;
                if (i + 1 < segments.Count && segments[i + 1].Kind == WordDiffKind.Insert)
                {
                    inserted = segments[++i].Text;
                }
            }
            else
            {
                inserted = segment.Text;
            }
            builder.Region(deleted, inserted);
            after.Append(inserted);
        }
        return builder.Build(insertedWords, deletedWords, hasChanges, after.ToString());
    }

    /// <summary>Opens the private atom around a line break while diffing (U+FDD0, a noncharacter never found in text).</summary>
    private const char LineOpen = '\uFDD0';

    /// <summary>Closes it (U+FDD1).</summary>
    private const char LineClose = '\uFDD1';

    private const string EncodedLineBreak = "\uFDD0\n\uFDD1";

    private static string EncodeLines(string text) => text.Replace("\n", EncodedLineBreak, StringComparison.Ordinal);

    private static string DecodeLines(string text) =>
        text.IndexOf(LineOpen) < 0 && text.IndexOf(LineClose) < 0 ? text : text.Replace(LineOpen.ToString(), null).Replace(LineClose.ToString(), null);

    /// <summary>
    /// The text a copy of <c>[start, end)</c> of <see cref="Text"/> gives: the selected words without signs and paddings
    /// (chips copy as their <c>{{name}}</c> text).
    /// </summary>
    public string CopyText(int start, int end)
    {
        if (end < start)
        {
            (start, end) = (end, start);
        }
        start = Math.Max(0, start);
        end = Math.Min(Text.Length, end);
        if (end <= start)
        {
            return string.Empty;
        }
        var copy = new StringBuilder(end - start);
        foreach (var span in Spans)
        {
            var from = Math.Max(start, span.Start);
            var to = Math.Min(end, span.Start + span.Length);
            if (to <= from)
            {
                continue;
            }
            switch (span.Kind)
            {
                case AiDiffSpanKind.InsertSign:
                case AiDiffSpanKind.DeleteSign:
                    break;
                case AiDiffSpanKind.Space:
                    if (span.CopiesAsSpace)
                    {
                        copy.Append(' ');
                    }
                    break;
                default:
                    copy.Append(Text, from, to - from);
                    break;
            }
        }
        return copy.ToString();
    }

    /// <summary>The index of the span that holds <paramref name="index"/> of <see cref="Text"/>, or -1.</summary>
    public int SpanAt(int index)
    {
        var lo = 0;
        var hi = Spans.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            var span = Spans[mid];
            if (index < span.Start)
            {
                hi = mid - 1;
            }
            else if (index >= span.Start + span.Length)
            {
                lo = mid + 1;
            }
            else
            {
                return mid;
            }
        }
        return -1;
    }

    /// <summary>Normalizes a chip tone to 1 to 4 (5 is 1 again, 0 is 4).</summary>
    internal static int WrapTone(int tone) => ((tone - 1) % 4 + 4) % 4 + 1;

    /// <summary>Collects the shown text, its runs and its marks.</summary>
    private sealed class Builder
    {
        private readonly AiDiffAppearance _appearance;
        private readonly IReadOnlyList<(string Open, string Close)> _atoms;
        private readonly Func<string, int>? _chipTone;
        private readonly StringBuilder _text = new();
        private readonly List<AiDiffSpan> _spans = new();
        private readonly List<AiDiffMark> _marks = new();

        public Builder(AiDiffAppearance appearance, IReadOnlyList<(string Open, string Close)> atoms, Func<string, int>? chipTone)
        {
            _appearance = appearance;
            _atoms = atoms;
            _chipTone = chipTone;
        }

        private bool IsStudio => _appearance == AiDiffAppearance.Studio;

        /// <summary>One changed region: the deleted run, then the inserted run that replaces it (either may be missing).</summary>
        public void Region(string? deleted, string? inserted)
        {
            var showsDeleted = deleted is not null && !IsBlank(deleted);
            var showsInserted = inserted is not null && !IsBlank(inserted);
            // "old new": the two runs touch in the texts (no white space between them).
            var touching = showsDeleted && showsInserted && !char.IsWhiteSpace(deleted![^1]) && !char.IsWhiteSpace(inserted![0]);
            if (showsDeleted)
            {
                Changed(deleted!, AiDiffSpanKind.Delete, touching);
            }
            if (touching && !IsStudio)
            {
                Append(" ", AiDiffSpanKind.Separator);
            }
            if (inserted is null)
            {
                return;
            }
            if (showsInserted)
            {
                Changed(inserted, AiDiffSpanKind.Insert, false);
            }
            else
            {
                Append(inserted, AiDiffSpanKind.Insert);
            }
        }

        /// <summary>A deleted or inserted run that has words: its white space outside, paddings, sign and marks inside.</summary>
        private void Changed(string text, AiDiffSpanKind kind, bool touchesNext)
        {
            var lead = 0;
            while (lead < text.Length && char.IsWhiteSpace(text[lead]))
            {
                lead++;
            }
            var trail = 0;
            while (trail < text.Length - lead && char.IsWhiteSpace(text[text.Length - 1 - trail]))
            {
                trail++;
            }
            var core = text.Substring(lead, text.Length - lead - trail);
            if (lead > 0)
            {
                Append(text[..lead], kind);
            }
            var isDelete = kind == AiDiffSpanKind.Delete;
            if (IsStudio)
            {
                Space(StudioPad);
                var tintStart = _text.Length;
                Append(isDelete ? DeleteSignText : InsertSignText, isDelete ? AiDiffSpanKind.DeleteSign : AiDiffSpanKind.InsertSign);
                Space(StudioSignGap);
                var wordsStart = _text.Length;
                Words(core, kind);
                LineMarks(tintStart, isDelete ? AiDiffMarkKind.DeleteTint : AiDiffMarkKind.InsertTint);
                if (isDelete)
                {
                    LineMarks(wordsStart, AiDiffMarkKind.Strike);
                }
                // Copying "−150+120" gives "150 120".
                Space(StudioPad, touchesNext && trail == 0);
            }
            else if (isDelete)
            {
                var wordsStart = _text.Length;
                Words(core, kind);
                LineMarks(wordsStart, AiDiffMarkKind.Strike);
            }
            else
            {
                Space(CardPad);
                var wordsStart = _text.Length;
                Words(core, kind);
                LineMarks(wordsStart, AiDiffMarkKind.InsertTint);
                Space(CardPad);
            }
            if (trail > 0)
            {
                Append(text[^trail..], kind);
            }
        }

        /// <summary>
        /// Marks <c>[start, end of text)</c> one line at a time: split at line breaks, each piece without its leading and
        /// trailing white space, blank pieces left out (an empty line inside a run gets no stray tint).
        /// </summary>
        private void LineMarks(int start, AiDiffMarkKind kind)
        {
            var end = _text.Length;
            var from = start;
            while (from < end)
            {
                var to = from;
                while (to < end && _text[to] != '\n')
                {
                    to++;
                }
                var first = from;
                var last = to;
                while (first < last && char.IsWhiteSpace(_text[first]))
                {
                    first++;
                }
                while (last > first && char.IsWhiteSpace(_text[last - 1]))
                {
                    last--;
                }
                if (last > first)
                {
                    _marks.Add(new AiDiffMark(first, last - first, kind));
                }
                from = to + 1;
            }
        }

        /// <summary>Words of one kind; with chips on, each named atom becomes a chip.</summary>
        public void Words(string text, AiDiffSpanKind kind)
        {
            if (_chipTone is null || _atoms.Count == 0)
            {
                Append(text, kind);
                return;
            }
            var plainStart = 0;
            var i = 0;
            while (i < text.Length)
            {
                if (MatchAtom(text, i, out var open, out var close) is var end and > 0)
                {
                    var name = text.Substring(i + open, end - close - i - open);
                    var trimmed = name.Trim();
                    if (trimmed.Length > 0)
                    {
                        if (i > plainStart)
                        {
                            Append(text[plainStart..i], kind);
                        }
                        var tone = WrapTone(_chipTone(trimmed));
                        var chipStart = _text.Length;
                        Append(text.Substring(i, open), AiDiffSpanKind.ChipBrace, tone, ChipBraceWidth);
                        Append(name, AiDiffSpanKind.ChipName, tone);
                        Append(text.Substring(end - close, close), AiDiffSpanKind.ChipBrace, tone, ChipBraceWidth);
                        _marks.Add(new AiDiffMark(chipStart, _text.Length - chipStart, AiDiffMarkKind.Chip, tone));
                        plainStart = end;
                    }
                    i = end;
                    continue;
                }
                i++;
            }
            if (plainStart < text.Length)
            {
                Append(text[plainStart..], kind);
            }
        }

        /// <summary>
        /// End of the atom that starts at <paramref name="index"/> (its open marker and the first close marker after it,
        /// as <see cref="WordDiff"/> reads them), or 0; the matched markers' lengths come out.
        /// </summary>
        private int MatchAtom(string text, int index, out int openLength, out int closeLength)
        {
            foreach (var (open, close) in _atoms)
            {
                if (string.IsNullOrEmpty(open) || string.IsNullOrEmpty(close) ||
                    string.CompareOrdinal(text, index, open, 0, open.Length) != 0)
                {
                    continue;
                }
                var at = text.IndexOf(close, index + open.Length, StringComparison.Ordinal);
                if (at >= 0)
                {
                    openLength = open.Length;
                    closeLength = close.Length;
                    return at + close.Length;
                }
            }
            openLength = 0;
            closeLength = 0;
            return 0;
        }

        private void Space(double width, bool copiesAsSpace = false)
        {
            _spans.Add(new AiDiffSpan(_text.Length, 1, AiDiffSpanKind.Space, Width: width, CopiesAsSpace: copiesAsSpace));
            _text.Append(PadChar);
        }

        private void Append(string text, AiDiffSpanKind kind, int tone = 0, double width = 0)
        {
            if (text.Length == 0)
            {
                return;
            }
            _spans.Add(new AiDiffSpan(_text.Length, text.Length, kind, tone, width));
            _text.Append(text);
        }

        public AiDiffDocument Build(int insertedWords, int deletedWords, bool hasChanges, string after)
        {
            // Tints under chips under strikethroughs (a stable sort keeps reading order within a kind).
            var marks = _marks.OrderBy(m => m.Kind switch
            {
                AiDiffMarkKind.InsertTint or AiDiffMarkKind.DeleteTint => 0,
                AiDiffMarkKind.Chip => 1,
                _ => 2
            }).ToArray();
            return new AiDiffDocument(_text.ToString(), _spans.ToArray(), marks, insertedWords, deletedWords, hasChanges, after);
        }

        private static bool IsBlank(string text)
        {
            foreach (var c in text)
            {
                if (!char.IsWhiteSpace(c))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
