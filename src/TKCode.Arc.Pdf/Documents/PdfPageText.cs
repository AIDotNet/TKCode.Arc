using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The text of a page and where each character is: the only thing search and selection need from a backend, so both behave
/// the same on every platform. Boxes are in page coordinates (origin at the top-left corner, y down, page DIPs).
/// </summary>
public sealed class PdfPageText
{
    private readonly Rect[] _boxes;
    private IReadOnlyList<Rect>? _lines;

    /// <summary>Creates the text of a page.</summary>
    /// <param name="text">The text, in UTF-16.</param>
    /// <param name="charBoxes">The box of each UTF-16 unit: a surrogate pair shares one, white space may have an empty one.</param>
    /// <exception cref="ArgumentException">There are not as many boxes as characters.</exception>
    public PdfPageText(string text, Rect[] charBoxes)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(charBoxes);
        if (text.Length != charBoxes.Length)
        {
            throw new ArgumentException("There must be one box per UTF-16 unit of the text.", nameof(charBoxes));
        }
        Text = text;
        _boxes = charBoxes;
    }

    /// <summary>The text, in UTF-16.</summary>
    public string Text { get; }

    /// <summary>The box of each UTF-16 unit of <see cref="Text"/>.</summary>
    public IReadOnlyList<Rect> CharBoxes => _boxes;

    /// <summary>The index of the character nearest to a point, ignoring characters without a box; -1 when no character has one.</summary>
    public int HitTest(Point pointInPage)
    {
        var best = -1;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < _boxes.Length; i++)
        {
            ref readonly var box = ref _boxes[i];
            if (IsEmpty(box))
            {
                continue;
            }
            var dx = Math.Max(Math.Max(box.Left - pointInPage.X, 0), pointInPage.X - box.Right);
            var dy = Math.Max(Math.Max(box.Top - pointInPage.Y, 0), pointInPage.Y - box.Bottom);
            var distance = dx * dx + dy * dy;
            // Strictly nearer: the first of a surrogate pair wins, so the result never splits one.
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// The area of a range of characters, one rectangle per line (a highlight or a selection). The range is clamped to the
    /// text. A line ends at a line break, where the next box does not overlap it vertically by half a line, where it goes
    /// back to the left, or across a gap wider than two line heights (a column gutter).
    /// </summary>
    public IReadOnlyList<Rect> GetRects(int start, int length)
    {
        var from = Math.Clamp(start, 0, _boxes.Length);
        var to = (int)Math.Clamp((long)from + Math.Max(length, 0), from, _boxes.Length);
        var rects = new List<Rect>();
        Rect? line = null;
        for (var i = from; i < to; i++)
        {
            if (Text[i] is '\n' or '\r')
            {
                Flush(rects, ref line);
                continue;
            }
            var box = _boxes[i];
            if (IsEmpty(box))
            {
                continue;
            }
            if (line is { } current && !ContinuesLine(current, box))
            {
                Flush(rects, ref line);
            }
            line = line is { } open ? open.Union(box) : box;
        }
        Flush(rects, ref line);
        return rects;
    }

    /// <summary>Whether a point is on a line of text, the gaps between its words included (the I-beam shows there and only there).</summary>
    internal bool IsOverText(Point pointInPage)
    {
        // Written once: a race only reads the same lines twice.
        var lines = _lines ??= GetRects(0, _boxes.Length);
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Inflate(1).Contains(pointInPage))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The position between characters nearest to a point: before or after the nearest character, by the side of its middle.</summary>
    internal int CaretAt(Point pointInPage)
    {
        var hit = HitTest(pointInPage);
        if (hit < 0)
        {
            return 0;
        }
        if (pointInPage.X <= _boxes[hit].Center.X)
        {
            return hit;
        }
        return hit + 1 < Text.Length && char.IsSurrogatePair(Text[hit], Text[hit + 1]) ? hit + 2 : hit + 1;
    }

    /// <summary>The word around a character (double click): a run of the same kind of letters, or the character alone when it is punctuation.</summary>
    internal (int Start, int End) WordAt(int index)
    {
        if (Text.Length == 0)
        {
            return (0, 0);
        }
        var at = Math.Clamp(index, 0, Text.Length - 1);
        if (char.IsLowSurrogate(Text[at]) && at > 0)
        {
            at--;
        }
        var kind = PdfTextCharacters.KindAt(Text, at);
        var start = at;
        var end = at + PdfTextCharacters.LengthAt(Text, at);
        if (kind == PdfCharacterKind.Other)
        {
            return (start, end);
        }
        while (start > 0)
        {
            var previous = char.IsLowSurrogate(Text[start - 1]) && start > 1 ? start - 2 : start - 1;
            if (!Joins(previous, kind, forward: false))
            {
                break;
            }
            start = previous;
        }
        while (end < Text.Length && Joins(end, kind, forward: true))
        {
            end += PdfTextCharacters.LengthAt(Text, end);
        }
        return (start, end);
    }

    private bool Joins(int index, PdfCharacterKind kind, bool forward)
    {
        if (PdfTextCharacters.KindAt(Text, index) == kind)
        {
            return true;
        }
        // An apostrophe inside a word: don't, l'avion.
        if (kind != PdfCharacterKind.Word || Text[index] is not ('\'' or '\u2019'))
        {
            return false;
        }
        var beyond = forward ? index + 1 : index - 1;
        var behind = forward ? index - 1 : index + 1;
        return beyond >= 0 && beyond < Text.Length && behind >= 0 && behind < Text.Length
            && PdfTextCharacters.KindAt(Text, beyond) == kind && PdfTextCharacters.KindAt(Text, behind) == kind;
    }

    /// <summary>The line around a character (triple click), without its line break: up to a break, or to a box that does not share the line.</summary>
    internal (int Start, int End) LineAt(int index)
    {
        if (Text.Length == 0)
        {
            return (0, 0);
        }
        var at = Math.Clamp(index, 0, Text.Length - 1);
        var reference = _boxes[at];
        var start = at;
        while (start > 0 && SameLine(start - 1, reference))
        {
            start--;
        }
        var end = at;
        while (end < Text.Length && SameLine(end, reference))
        {
            end++;
        }
        return (start, Math.Max(start, end));
    }

    private bool SameLine(int index, in Rect reference)
    {
        if (Text[index] is '\n' or '\r')
        {
            return false;
        }
        ref readonly var box = ref _boxes[index];
        if (IsEmpty(box) || IsEmpty(reference))
        {
            return true;
        }
        var overlap = Math.Min(reference.Bottom, box.Bottom) - Math.Max(reference.Top, box.Top);
        return overlap >= Math.Min(reference.Height, box.Height) / 2;
    }

    private static bool ContinuesLine(Rect line, Rect box)
    {
        var overlap = Math.Min(line.Bottom, box.Bottom) - Math.Max(line.Top, box.Top);
        var lineHeight = Math.Min(line.Height, box.Height);
        return overlap >= lineHeight / 2
            && box.Left >= line.Right - lineHeight
            && box.Left - line.Right <= lineHeight * 2;
    }

    private static void Flush(List<Rect> rects, ref Rect? line)
    {
        if (line is { } rect)
        {
            rects.Add(rect);
            line = null;
        }
    }

    private static bool IsEmpty(in Rect box) => box.Width <= 0 || box.Height <= 0;
}
