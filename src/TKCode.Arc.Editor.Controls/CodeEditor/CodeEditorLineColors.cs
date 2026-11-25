using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc;

/// <summary>
/// The token colors of one view line, as the text cache keys and draws them: for each token its end offset in the line
/// content, its foreground color id (an index into the color map) and its font style. Only what changes the drawing is
/// kept, so lines whose tokens differ in language or type alone share an entry. Immutable; compared by value.
/// </summary>
internal sealed class CodeEditorLineColors : IEquatable<CodeEditorLineColors>
{
    // per token: end offset, then foreground | (font style << 16)
    private readonly int[] _data;
    private readonly int _hash;

    private CodeEditorLineColors(int[] data)
    {
        _data = data;
        var hash = new HashCode();
        foreach (var value in data)
        {
            hash.Add(value);
        }

        _hash = hash.ToHashCode();
        for (var i = 0; i < Count; i++)
        {
            if (GetFontStyle(i) != FontStyle.None)
            {
                HasFontStyle = true;
                break;
            }
        }
    }

    public int Count => _data.Length / 2;

    /// <summary>Whether a token is bold, italic, underlined or struck through (the glyph-run fast path draws none of those).</summary>
    public bool HasFontStyle { get; }

    public int GetEndOffset(int index) => _data[index * 2];

    public int GetForeground(int index) => _data[(index * 2) + 1] & 0xFFFF;

    public FontStyle GetFontStyle(int index) => (FontStyle)(_data[(index * 2) + 1] >> 16);

    /// <summary>The foreground color id at an offset of the line content.</summary>
    public int GetForegroundAt(int offset)
    {
        for (var i = 0; i < Count; i++)
        {
            if (offset < GetEndOffset(i))
            {
                return GetForeground(i);
            }
        }

        return Count > 0 ? GetForeground(Count - 1) : (int)ColorId.DefaultForeground;
    }

    /// <summary>
    /// The colors of a view line's tokens, with <paramref name="overrides"/> (ranges of the line content in other colors,
    /// such as colorized brackets and inline decorations; sorted by start, not overlapping) drawn over them; null when the whole line is one
    /// token in the default foreground with no style (a language without a tokenizer), which the cache draws with the
    /// view's foreground.
    /// </summary>
    public static CodeEditorLineColors? From(IViewLineTokens tokens, IReadOnlyList<ColorOverride>? overrides = null)
    {
        var count = tokens.GetCount();
        var data = new List<int>(count * 2);
        var overrideIndex = 0;
        var start = 0;
        for (var i = 0; i < count; i++)
        {
            var presentation = tokens.GetPresentation(i);
            var style = (presentation.Italic ? FontStyle.Italic : FontStyle.None)
                | (presentation.Bold ? FontStyle.Bold : FontStyle.None)
                | (presentation.Underline ? FontStyle.Underline : FontStyle.None)
                | (presentation.Strikethrough ? FontStyle.Strikethrough : FontStyle.None);
            var styleBits = (int)style << 16;
            var end = tokens.GetEndOffset(i);

            // the overrides inside this token split it; an override keeps the token's font style
            while (overrides is not null && overrideIndex < overrides.Count && start < end)
            {
                var o = overrides[overrideIndex];
                if (o.End <= start)
                {
                    overrideIndex++;
                    continue;
                }

                if (o.Start >= end)
                {
                    break;
                }

                if (o.Start > start)
                {
                    Add(data, o.Start, (int)presentation.Foreground | styleBits);
                }

                var overrideEnd = Math.Min(o.End, end);
                var color = o.Color >= 0 ? o.Color : (int)presentation.Foreground;
                if (o.FadeBase >= 0)
                {
                    color += o.FadeBase;
                }

                Add(data, overrideEnd, color | styleBits | ((int)o.Style << 16));
                start = overrideEnd;
                if (o.End <= end)
                {
                    overrideIndex++;
                }
            }

            if (start < end || data.Count == 0)
            {
                Add(data, end, (int)presentation.Foreground | styleBits);
            }

            start = end;
        }

        if (data.Count == 0 || (data.Count == 2 && data[1] == (int)ColorId.DefaultForeground))
        {
            return null;
        }

        return new CodeEditorLineColors([.. data]);
    }

    private static void Add(List<int> data, int endOffset, int value)
    {
        if (data.Count > 0 && data[^1] == value)
        {
            // a neighbour with the same look: one run
            data[^2] = endOffset;
            return;
        }

        data.Add(endOffset);
        data.Add(value);
    }

    public bool Equals(CodeEditorLineColors? other) =>
        other is not null && (ReferenceEquals(this, other) || (_hash == other._hash && _data.AsSpan().SequenceEqual(other._data)));

    public override bool Equals(object? obj) => Equals(obj as CodeEditorLineColors);

    public override int GetHashCode() => _hash;
}

/// <summary>
/// A range of a line's content drawn differently: in another color (an index into the view's palette; -1 keeps the
/// token's), with font styles added, and faded (<paramref name="FadeBase"/>: the palette offset of a faded copy of the
/// palette, -1 for none).
/// </summary>
internal readonly record struct ColorOverride(int Start, int End, int Color, FontStyle Style = FontStyle.None, int FadeBase = -1);
