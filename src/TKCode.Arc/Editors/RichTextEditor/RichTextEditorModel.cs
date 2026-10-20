using System.Collections.Immutable;
using System.Text;

namespace TKCode.Arc;

/// <summary>The block types the rich text editor writes: Arc's <c>p</c>, <c>h1</c>–<c>h3</c>, <c>ul</c>, <c>ol</c>, <c>blockquote</c>, <c>pre</c> and <c>hr</c>.</summary>
public enum RichTextBlockType
{
    /// <summary>A plain paragraph (<c>p</c>).</summary>
    Paragraph,

    /// <summary>A first level heading (<c>h1</c>, <c># </c>).</summary>
    Heading1,

    /// <summary>A second level heading (<c>h2</c>, <c>## </c>).</summary>
    Heading2,

    /// <summary>A third level heading (<c>h3</c>, <c>### </c>); imported h4–h6 become h3.</summary>
    Heading3,

    /// <summary>An item of a bulleted list (<c>ul</c>, <c>- </c>).</summary>
    BulletedList,

    /// <summary>An item of a numbered list (<c>ol</c>, <c>1. </c>).</summary>
    NumberedList,

    /// <summary>A quote (<c>blockquote</c>, <c>&gt; </c>).</summary>
    Quote,

    /// <summary>A code block (<c>pre</c>, fenced with three backticks); holds plain text only.</summary>
    CodeBlock,

    /// <summary>A horizontal rule (<c>hr</c>, <c>---</c>).</summary>
    Divider
}

/// <summary>Inline marks the editor writes: <c>strong</c>, <c>em</c>, <c>s</c> and <c>code</c> (links carry an address instead).</summary>
public enum RichTextMark
{
    /// <summary>Bold (<c>strong</c>, <c>**text**</c>).</summary>
    Bold,

    /// <summary>Italic (<c>em</c>, <c>_text_</c>).</summary>
    Italic,

    /// <summary>Strikethrough (<c>s</c>, <c>~~text~~</c>).</summary>
    Strikethrough,

    /// <summary>Inline code (<c>code</c>, <c>`text`</c>), kept within one block.</summary>
    Code
}

[Flags]
internal enum RichTextEditorMarks : byte
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Strike = 4,
    Code = 8,

    /// <summary>The marks a caret carries into typed text from the character before it.</summary>
    Carried = Bold | Italic | Strike
}

/// <summary>A run of text with one style, used to build and slice blocks.</summary>
internal readonly record struct RichTextEditorRun(string Text, RichTextEditorMarks Marks = RichTextEditorMarks.None, string? Href = null);

/// <summary>A styled range of a block's text. A block's spans cover its text without gaps or empty spans.</summary>
internal readonly record struct RichTextEditorSpan(int Start, int Length, RichTextEditorMarks Marks, string? Href)
{
    public int End => Start + Length;

    public bool HasStyle(RichTextEditorMarks marks, string? href) => Marks == marks && string.Equals(Href, href, StringComparison.Ordinal);
}

/// <summary>
/// One immutable block of the document: a type, a list nesting level, the text (soft line breaks are <c>\n</c>) and
/// the styled spans. Edits return new blocks, so unchanged blocks keep their identity and their cached layout.
/// </summary>
internal sealed class RichTextEditorBlock
{
    private static readonly RichTextEditorSpan[] s_noSpans = Array.Empty<RichTextEditorSpan>();

    public static readonly RichTextEditorBlock EmptyParagraph = new(RichTextBlockType.Paragraph, 0, string.Empty, s_noSpans);

    public static readonly RichTextEditorBlock Divider = new(RichTextBlockType.Divider, 0, string.Empty, s_noSpans);

    private readonly RichTextEditorSpan[] _spans;

    private RichTextEditorBlock(RichTextBlockType type, int level, string text, RichTextEditorSpan[] spans)
    {
        Type = type;
        Level = level;
        Text = text;
        _spans = spans;
    }

    public RichTextBlockType Type { get; }

    /// <summary>Nesting level of a list item (0 is the outer list); 0 for every other block.</summary>
    public int Level { get; }

    public string Text { get; }

    public int Length => Text.Length;

    public ReadOnlySpan<RichTextEditorSpan> Spans => _spans;

    public int SpanCount => _spans.Length;

    public bool IsList => Type is RichTextBlockType.BulletedList or RichTextBlockType.NumberedList;

    public bool IsHeading => Type is RichTextBlockType.Heading1 or RichTextBlockType.Heading2 or RichTextBlockType.Heading3;

    public bool IsCode => Type == RichTextBlockType.CodeBlock;

    public bool IsDivider => Type == RichTextBlockType.Divider;

    /// <summary>True when the caret can rest in the block (everything except dividers).</summary>
    public bool IsTextual => Type != RichTextBlockType.Divider;

    public bool IsEmpty => Text.Length == 0;

    public static RichTextEditorBlock Plain(RichTextBlockType type, string text, int level = 0) =>
        Create(type, level, string.IsNullOrEmpty(text) ? Array.Empty<RichTextEditorRun>() : new[] { new RichTextEditorRun(text) });

    /// <summary>Builds a block from runs: empty runs are dropped, equal neighbours merge, code blocks lose their marks.</summary>
    public static RichTextEditorBlock Create(RichTextBlockType type, int level, IEnumerable<RichTextEditorRun> runs)
    {
        if (type == RichTextBlockType.Divider)
        {
            return Divider;
        }
        if (!(type is RichTextBlockType.BulletedList or RichTextBlockType.NumberedList))
        {
            level = 0;
        }
        var plain = type == RichTextBlockType.CodeBlock;
        var text = new StringBuilder();
        var spans = new List<RichTextEditorSpan>();
        foreach (var run in runs)
        {
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }
            var marks = plain ? RichTextEditorMarks.None : run.Marks;
            var href = plain || string.IsNullOrEmpty(run.Href) ? null : run.Href;
            var start = text.Length;
            text.Append(run.Text);
            if (spans.Count > 0 && spans[^1].HasStyle(marks, href))
            {
                var last = spans[^1];
                spans[^1] = last with { Length = last.Length + run.Text.Length };
            }
            else
            {
                spans.Add(new RichTextEditorSpan(start, run.Text.Length, marks, href));
            }
        }
        return new RichTextEditorBlock(type, Math.Max(0, level), text.ToString(), spans.Count == 0 ? s_noSpans : spans.ToArray());
    }

    public RichTextEditorBlock With(RichTextBlockType type, int? level = null)
    {
        var nextLevel = level ?? Level;
        if (type == Type && nextLevel == Level)
        {
            return this;
        }
        if (type == RichTextBlockType.Divider)
        {
            return Divider;
        }
        return Create(type, nextLevel, Runs());
    }

    public RichTextEditorBlock WithLevel(int level) => With(Type, level);

    /// <summary>The block's runs in order.</summary>
    public IEnumerable<RichTextEditorRun> Runs()
    {
        foreach (var span in _spans)
        {
            yield return new RichTextEditorRun(Text.Substring(span.Start, span.Length), span.Marks, span.Href);
        }
    }

    /// <summary>The runs covering <c>[start, end)</c>.</summary>
    public List<RichTextEditorRun> Slice(int start, int end)
    {
        start = Math.Clamp(start, 0, Length);
        end = Math.Clamp(end, start, Length);
        var result = new List<RichTextEditorRun>();
        if (start == end)
        {
            return result;
        }
        foreach (var span in _spans)
        {
            var from = Math.Max(span.Start, start);
            var to = Math.Min(span.End, end);
            if (from < to)
            {
                result.Add(new RichTextEditorRun(Text.Substring(from, to - from), span.Marks, span.Href));
            }
        }
        return result;
    }

    /// <summary>Replaces <c>[start, end)</c> with <paramref name="insert"/>.</summary>
    public RichTextEditorBlock Replace(int start, int end, IEnumerable<RichTextEditorRun> insert)
    {
        var runs = Slice(0, start);
        runs.AddRange(insert);
        runs.AddRange(Slice(end, Length));
        return Create(Type, Level, runs);
    }

    public RichTextEditorBlock Insert(int offset, RichTextEditorRun run) => Replace(offset, offset, new[] { run });

    public RichTextEditorBlock Delete(int start, int end) => Replace(start, end, Array.Empty<RichTextEditorRun>());

    /// <summary>Appends <paramref name="runs"/> at the end.</summary>
    public RichTextEditorBlock Append(IEnumerable<RichTextEditorRun> runs) => Replace(Length, Length, runs);

    /// <summary>Restyles <c>[start, end)</c> through <paramref name="restyle"/>.</summary>
    public RichTextEditorBlock Restyle(int start, int end, Func<RichTextEditorMarks, string?, (RichTextEditorMarks Marks, string? Href)> restyle)
    {
        if (IsCode || start >= end)
        {
            return this;
        }
        var middle = Slice(start, end);
        for (var i = 0; i < middle.Count; i++)
        {
            var (marks, href) = restyle(middle[i].Marks, middle[i].Href);
            middle[i] = middle[i] with { Marks = marks, Href = href };
        }
        return Replace(start, end, middle);
    }

    public RichTextEditorBlock SetMark(int start, int end, RichTextEditorMarks mark, bool on) =>
        Restyle(start, end, (marks, href) => (on ? marks | mark : marks & ~mark, href));

    public RichTextEditorBlock SetHref(int start, int end, string? href) =>
        Restyle(start, end, (marks, _) => (marks, string.IsNullOrEmpty(href) ? null : href));

    /// <summary>The span holding the character at <paramref name="index"/> (index must be inside the text).</summary>
    public RichTextEditorSpan SpanAt(int index)
    {
        var lo = 0;
        var hi = _spans.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            var span = _spans[mid];
            if (index < span.Start)
            {
                hi = mid - 1;
            }
            else if (index >= span.End)
            {
                lo = mid + 1;
            }
            else
            {
                return span;
            }
        }
        return default;
    }

    public int SpanIndexAt(int index)
    {
        for (var i = 0; i < _spans.Length; i++)
        {
            if (index >= _spans[i].Start && index < _spans[i].End)
            {
                return i;
            }
        }
        return -1;
    }

    public RichTextEditorMarks MarksAt(int index) => index >= 0 && index < Length ? SpanAt(index).Marks : RichTextEditorMarks.None;

    public string? HrefAt(int index) => index >= 0 && index < Length ? SpanAt(index).Href : null;

    /// <summary>True when every character in <c>[start, end)</c> carries <paramref name="mark"/>.</summary>
    public bool AllHave(int start, int end, RichTextEditorMarks mark)
    {
        if (start >= end)
        {
            return false;
        }
        foreach (var span in _spans)
        {
            if (span.End <= start || span.Start >= end)
            {
                continue;
            }
            if ((span.Marks & mark) == 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The widest range around <paramref name="index"/> whose characters all satisfy <paramref name="predicate"/>.</summary>
    public (int Start, int End) Extent(int index, Func<RichTextEditorSpan, bool> predicate)
    {
        var at = SpanIndexAt(index);
        if (at < 0 || !predicate(_spans[at]))
        {
            return (index, index);
        }
        var first = at;
        while (first > 0 && predicate(_spans[first - 1]))
        {
            first--;
        }
        var last = at;
        while (last < _spans.Length - 1 && predicate(_spans[last + 1]))
        {
            last++;
        }
        return (_spans[first].Start, _spans[last].End);
    }

    /// <summary>The range of the link holding <paramref name="index"/> (same address throughout).</summary>
    public (int Start, int End) LinkExtent(int index)
    {
        var href = HrefAt(index);
        return href is null ? (index, index) : Extent(index, span => string.Equals(span.Href, href, StringComparison.Ordinal));
    }

    public override string ToString() => $"{Type}{(IsList ? $"@{Level}" : string.Empty)}: {Text}";
}

/// <summary>A caret position: a block index and a character offset inside that block's text.</summary>
internal readonly record struct RichTextEditorPosition(int Block, int Offset) : IComparable<RichTextEditorPosition>
{
    public int CompareTo(RichTextEditorPosition other) => Block != other.Block ? Block.CompareTo(other.Block) : Offset.CompareTo(other.Offset);

    public static bool operator <(RichTextEditorPosition a, RichTextEditorPosition b) => a.CompareTo(b) < 0;

    public static bool operator >(RichTextEditorPosition a, RichTextEditorPosition b) => a.CompareTo(b) > 0;

    public static bool operator <=(RichTextEditorPosition a, RichTextEditorPosition b) => a.CompareTo(b) <= 0;

    public static bool operator >=(RichTextEditorPosition a, RichTextEditorPosition b) => a.CompareTo(b) >= 0;

    public static RichTextEditorPosition Min(RichTextEditorPosition a, RichTextEditorPosition b) => a <= b ? a : b;

    public static RichTextEditorPosition Max(RichTextEditorPosition a, RichTextEditorPosition b) => a >= b ? a : b;
}

/// <summary>A selection: the anchor stays put while the focus (the caret) moves.</summary>
internal readonly record struct RichTextEditorSelection(RichTextEditorPosition Anchor, RichTextEditorPosition Focus)
{
    public RichTextEditorPosition Start => RichTextEditorPosition.Min(Anchor, Focus);

    public RichTextEditorPosition End => RichTextEditorPosition.Max(Anchor, Focus);

    public bool IsCollapsed => Anchor == Focus;

    public static RichTextEditorSelection Caret(RichTextEditorPosition position) => new(position, position);

    public static RichTextEditorSelection Caret(int block, int offset) => Caret(new RichTextEditorPosition(block, offset));

    public static RichTextEditorSelection Range(int block, int start, int end) => new(new RichTextEditorPosition(block, start), new RichTextEditorPosition(block, end));
}

/// <summary>The active formatting at the selection, for toolbars: marks, the link address and the block type.</summary>
/// <param name="IsBold">Every selected character is bold (or the caret types bold).</param>
/// <param name="IsItalic">Every selected character is italic (or the caret types italic).</param>
/// <param name="IsStrikethrough">Every selected character is struck through (or the caret types struck through).</param>
/// <param name="IsCode">The selection starts in inline code.</param>
/// <param name="Link">The address of the link at the selection start, or null.</param>
/// <param name="BlockType">The type of the block holding the selection start.</param>
public sealed record RichTextFormats(bool IsBold, bool IsItalic, bool IsStrikethrough, bool IsCode, string? Link, RichTextBlockType BlockType)
{
    /// <summary>The formats of an empty paragraph.</summary>
    public static RichTextFormats Default { get; } = new(false, false, false, false, null, RichTextBlockType.Paragraph);
}

/// <summary>What the editor holds after an edit: HTML, Markdown and plain text, computed on first read.</summary>
public sealed class RichTextValue
{
    private readonly ImmutableList<RichTextEditorBlock> _blocks;
    private string? _html;
    private string? _markdown;
    private string? _text;

    internal RichTextValue(ImmutableList<RichTextEditorBlock> blocks)
    {
        _blocks = blocks;
        IsEmpty = RichTextEditorHtml.IsDocumentEmpty(blocks);
    }

    /// <summary>Clean HTML: canonical tags, safe link targets only, no trailing empty lines.</summary>
    public string Html => _html ??= RichTextEditorHtml.Write(_blocks);

    /// <summary>The same content as Markdown.</summary>
    public string Markdown => _markdown ??= RichTextEditorMarkdown.Write(_blocks);

    /// <summary>Plain text, one line per block.</summary>
    public string Text => _text ??= RichTextEditorHtml.PlainText(_blocks);

    /// <summary>True when there is no text and no divider.</summary>
    public bool IsEmpty { get; }
}
