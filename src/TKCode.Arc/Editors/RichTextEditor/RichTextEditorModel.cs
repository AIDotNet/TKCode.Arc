using System.Collections.Immutable;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The block types the rich text editor writes: Arc's <c>p</c>, <c>h1</c>–<c>h3</c>, <c>ul</c>, <c>ol</c>, <c>blockquote</c>, <c>pre</c> and
/// <c>hr</c>, and the <see cref="RichTextEditorProfile.Docs"/> profile's <c>h4</c>–<c>h6</c>, to-do items, images, table cells and callouts.
/// </summary>
public enum RichTextBlockType
{
    /// <summary>A plain paragraph (<c>p</c>).</summary>
    Paragraph,

    /// <summary>A first level heading (<c>h1</c>, <c># </c>).</summary>
    Heading1,

    /// <summary>A second level heading (<c>h2</c>, <c>## </c>).</summary>
    Heading2,

    /// <summary>A third level heading (<c>h3</c>, <c>### </c>); the <see cref="RichTextEditorProfile.Arc"/> profile imports h4–h6 as h3.</summary>
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
    Divider,

    /// <summary>An item of a to-do list (a checkbox and text, <c>[] </c>); nests like the other list items. Written by the <see cref="RichTextEditorProfile.Docs"/> profile.</summary>
    TodoList,

    /// <summary>
    /// An image (<c>img</c>, <c>![alt](src)</c>): one atomic block that holds an address and a description instead of text.
    /// Written by the <see cref="RichTextEditorProfile.Docs"/> profile.
    /// </summary>
    Image,

    /// <summary>
    /// A cell of a table (<c>td</c>/<c>th</c>, a pipe table cell). The cells of one table are consecutive blocks in row-major order;
    /// the first row is the header. Written by the <see cref="RichTextEditorProfile.Docs"/> profile.
    /// </summary>
    TableCell,

    /// <summary>
    /// A paragraph of a callout: a tinted box with an icon (<c>aside</c>, a <c>&gt; [!NOTE]</c> alert). Neighbouring callout blocks
    /// with the same icon and color are drawn as one box. Written by the <see cref="RichTextEditorProfile.Docs"/> profile.
    /// </summary>
    Callout,

    /// <summary>A fourth level heading (<c>h4</c>, <c>#### </c>). Written by the <see cref="RichTextEditorProfile.Docs"/> profile.</summary>
    Heading4,

    /// <summary>A fifth level heading (<c>h5</c>, <c>##### </c>). Written by the <see cref="RichTextEditorProfile.Docs"/> profile.</summary>
    Heading5,

    /// <summary>A sixth level heading (<c>h6</c>, <c>###### </c>), drawn in the secondary text color. Written by the <see cref="RichTextEditorProfile.Docs"/> profile.</summary>
    Heading6,

    /// <summary>
    /// An item of a toggle list: a list item with a disclosure arrow that folds away the items nested under it (<c>ul data-toggle</c>,
    /// a plain <c>- </c> item in Markdown). Written by the <see cref="RichTextEditorProfile.Docs"/> profile.
    /// </summary>
    Toggle
}

/// <summary>The feature set of a <see cref="RichTextEditor"/>.</summary>
public enum RichTextEditorProfile
{
    /// <summary>Arc's editor: <c>p</c>, <c>h1</c>–<c>h3</c>, lists, quotes, code, dividers and bold, italic, strikethrough, code and links only.</summary>
    Arc,

    /// <summary>
    /// A document editor: Arc's features plus headings 4 to 6, underline, superscript and subscript, text and highlight colors, to-do lists,
    /// text alignment, a language label on code blocks, images, tables and callouts.
    /// </summary>
    Docs
}

/// <summary>The palette of text and highlight colors the <see cref="RichTextEditorProfile.Docs"/> profile offers.</summary>
public enum RichTextColor
{
    /// <summary>No color: the editor's text color, or no highlight.</summary>
    Default,

    /// <summary>Gray.</summary>
    Gray,

    /// <summary>Red.</summary>
    Red,

    /// <summary>Orange.</summary>
    Orange,

    /// <summary>Yellow.</summary>
    Yellow,

    /// <summary>Green.</summary>
    Green,

    /// <summary>Blue.</summary>
    Blue,

    /// <summary>Purple.</summary>
    Purple
}

/// <summary>The text sizes the <see cref="RichTextEditorProfile.Docs"/> profile offers, relative to the size of the block's text.</summary>
public enum RichTextSize
{
    /// <summary>The block's own size.</summary>
    Default,

    /// <summary>Seven eighths of the block's size (<c>data-size="small"</c>).</summary>
    Small,

    /// <summary>A quarter larger than the block's size (<c>data-size="large"</c>).</summary>
    Large,

    /// <summary>Half again the block's size (<c>data-size="xlarge"</c>).</summary>
    ExtraLarge
}

/// <summary>The line spacing of a block in the <see cref="RichTextEditorProfile.Docs"/> profile, relative to the block type's own.</summary>
public enum RichTextLineSpacing
{
    /// <summary>The block type's own spacing.</summary>
    Default,

    /// <summary>About 15% tighter (<c>data-spacing="compact"</c>).</summary>
    Compact,

    /// <summary>About 20% looser (<c>data-spacing="relaxed"</c>).</summary>
    Relaxed,

    /// <summary>About 45% looser (<c>data-spacing="loose"</c>).</summary>
    Loose
}

/// <summary>How a paragraph, heading or quote lines up its text.</summary>
public enum RichTextAlign
{
    /// <summary>At the start edge (left in left-to-right text).</summary>
    Start,

    /// <summary>Centered.</summary>
    Center,

    /// <summary>At the end edge (right in left-to-right text).</summary>
    End
}

/// <summary>Inline marks the editor writes: <c>strong</c>, <c>em</c>, <c>s</c>, <c>code</c>, and in the Docs profile <c>u</c>, <c>sup</c> and <c>sub</c> (links carry an address instead).</summary>
public enum RichTextMark
{
    /// <summary>Bold (<c>strong</c>, <c>**text**</c>).</summary>
    Bold,

    /// <summary>Italic (<c>em</c>, <c>_text_</c>).</summary>
    Italic,

    /// <summary>Strikethrough (<c>s</c>, <c>~~text~~</c>).</summary>
    Strikethrough,

    /// <summary>Inline code (<c>code</c>, <c>`text`</c>), kept within one block.</summary>
    Code,

    /// <summary>Underline (<c>u</c>); only the <see cref="RichTextEditorProfile.Docs"/> profile writes it.</summary>
    Underline,

    /// <summary>Superscript (<c>sup</c>): smaller and raised. Turning it on turns subscript off. Only the <see cref="RichTextEditorProfile.Docs"/> profile writes it.</summary>
    Superscript,

    /// <summary>Subscript (<c>sub</c>): smaller and lowered. Turning it on turns superscript off. Only the <see cref="RichTextEditorProfile.Docs"/> profile writes it.</summary>
    Subscript
}

/// <summary>
/// The style bits of a span. Bits 0–4 are flags; bits 5–7 hold the text color and bits 8–10 the highlight, each a
/// <see cref="RichTextColor"/> index; bits 11 and 12 are superscript and subscript (never both) and bits 13–14 the <see cref="RichTextSize"/>,
/// so a span's whole style compares as one value.
/// </summary>
[Flags]
internal enum RichTextEditorMarks : ushort
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Strike = 4,
    Code = 8,
    Underline = 16,
    ColorMask = 0xE0,
    FillMask = 0x700,
    Superscript = 0x800,
    Subscript = 0x1000,
    ScriptMask = Superscript | Subscript,
    SizeMask = 0x6000,

    /// <summary>The marks a caret carries into typed text from the character before it.</summary>
    Carried = Bold | Italic | Strike | Underline | ColorMask | FillMask | ScriptMask | SizeMask
}

internal static class RichTextEditorMarkBits
{
    private const int ColorShift = 5;
    private const int FillShift = 8;
    private const int SizeShift = 13;

    public static RichTextSize Size(this RichTextEditorMarks marks) => (RichTextSize)(((int)marks & (int)RichTextEditorMarks.SizeMask) >> SizeShift);

    public static RichTextEditorMarks WithSize(this RichTextEditorMarks marks, RichTextSize size) =>
        (marks & ~RichTextEditorMarks.SizeMask) | (RichTextEditorMarks)((int)size << SizeShift);

    /// <summary>How much larger than the block's text a size draws.</summary>
    public static double Scale(this RichTextSize size) => size switch
    {
        RichTextSize.Small => 0.875,
        RichTextSize.Large => 1.25,
        RichTextSize.ExtraLarge => 1.5,
        _ => 1
    };

    /// <summary>How much a spacing stretches the block type's line height.</summary>
    public static double Scale(this RichTextLineSpacing spacing) => spacing switch
    {
        RichTextLineSpacing.Compact => 0.85,
        RichTextLineSpacing.Relaxed => 1.2,
        RichTextLineSpacing.Loose => 1.45,
        _ => 1
    };

    public static RichTextColor TextColor(this RichTextEditorMarks marks) => (RichTextColor)(((int)marks & (int)RichTextEditorMarks.ColorMask) >> ColorShift);

    public static RichTextColor Highlight(this RichTextEditorMarks marks) => (RichTextColor)(((int)marks & (int)RichTextEditorMarks.FillMask) >> FillShift);

    public static RichTextEditorMarks WithTextColor(this RichTextEditorMarks marks, RichTextColor color) =>
        (marks & ~RichTextEditorMarks.ColorMask) | (RichTextEditorMarks)((int)color << ColorShift);

    public static RichTextEditorMarks WithHighlight(this RichTextEditorMarks marks, RichTextColor color) =>
        (marks & ~RichTextEditorMarks.FillMask) | (RichTextEditorMarks)((int)color << FillShift);
}

/// <summary>
/// Per-block settings beyond type, level and text: the to-do checkmark, the alignment, a code block's language, an image's
/// address and description, and a table cell's row and column, how many rows and columns a merged cell spans (0 is 1), whether a
/// merged cell covers it, and its background (<see cref="Tone"/>). A first-row cell's <see cref="Width"/> is its column's width in pixels (0
/// shares the free width), and the first cell of a table says whether the first row and the first column are headers. Text blocks other
/// than code and table cells carry a line <see cref="Spacing"/>, and a toggle item whether it is <see cref="Collapsed"/>.
/// </summary>
internal readonly record struct RichTextEditorBlockAttrs(bool Checked = false, RichTextAlign Align = RichTextAlign.Start, string? Language = null,
    string? Src = null, string? Alt = null, int Row = 0, int Col = 0, int Width = 0, string? Icon = null, RichTextColor Tone = RichTextColor.Default,
    int RowSpan = 0, int ColSpan = 0, bool Covered = false, bool NoHeaderRow = false, bool HeaderColumn = false,
    RichTextLineSpacing Spacing = RichTextLineSpacing.Default, bool Collapsed = false);

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

    public static readonly RichTextEditorBlock EmptyParagraph = new(RichTextBlockType.Paragraph, 0, string.Empty, s_noSpans, default);

    public static readonly RichTextEditorBlock Divider = new(RichTextBlockType.Divider, 0, string.Empty, s_noSpans, default);

    /// <summary>An image block with the given address and description.</summary>
    public static RichTextEditorBlock Image(string src, string? alt = null, int width = 0) =>
        new(RichTextBlockType.Image, 0, string.Empty, s_noSpans, Limit(RichTextBlockType.Image, new RichTextEditorBlockAttrs(Src: src, Alt: alt, Width: width)));

    /// <summary>An empty cell of a table at <paramref name="row"/> and <paramref name="col"/>.</summary>
    public static RichTextEditorBlock Cell(int row, int col) =>
        Create(RichTextBlockType.TableCell, 0, Array.Empty<RichTextEditorRun>(), new RichTextEditorBlockAttrs(Row: row, Col: col));

    private readonly RichTextEditorSpan[] _spans;

    private RichTextEditorBlock(RichTextBlockType type, int level, string text, RichTextEditorSpan[] spans, RichTextEditorBlockAttrs attrs)
    {
        Type = type;
        Level = level;
        Text = text;
        _spans = spans;
        Attrs = attrs;
    }

    public RichTextBlockType Type { get; }

    /// <summary>The block's checkmark, alignment and language, already limited to what its type supports.</summary>
    public RichTextEditorBlockAttrs Attrs { get; }

    public bool IsChecked => Attrs.Checked;

    public RichTextAlign Align => Attrs.Align;

    public string? Language => Attrs.Language;

    /// <summary>An image block's address, or null.</summary>
    public string? Src => Attrs.Src;

    /// <summary>An image block's description, or null.</summary>
    public string? Alt => Attrs.Alt;

    /// <summary>An image block's chosen width in pixels, or 0 for its natural size (shrunk to fit).</summary>
    public int Width => Attrs.Width;

    /// <summary>A table cell's row (0 is the header row).</summary>
    public int Row => Attrs.Row;

    /// <summary>A table cell's column.</summary>
    public int Col => Attrs.Col;

    /// <summary>The number of rows a table cell spans (1 unless it is a merged cell).</summary>
    public int RowSpan => Math.Max(1, Attrs.RowSpan);

    /// <summary>The number of columns a table cell spans (1 unless it is a merged cell).</summary>
    public int ColSpan => Math.Max(1, Attrs.ColSpan);

    /// <summary>True for a table cell that a merged cell covers: it holds no text and is neither drawn nor entered.</summary>
    public bool IsCovered => Attrs.Covered;

    /// <summary>True for a merged table cell (it spans more than one row or column).</summary>
    public bool IsMerged => RowSpan > 1 || ColSpan > 1;

    /// <summary>Nesting level of a list item (0 is the outer list); 0 for every other block.</summary>
    public int Level { get; }

    public string Text { get; }

    public int Length => Text.Length;

    public ReadOnlySpan<RichTextEditorSpan> Spans => _spans;

    public int SpanCount => _spans.Length;

    public bool IsList => Type is RichTextBlockType.BulletedList or RichTextBlockType.NumberedList or RichTextBlockType.TodoList or RichTextBlockType.Toggle;

    public bool IsTodo => Type == RichTextBlockType.TodoList;

    public bool IsHeading => Type is RichTextBlockType.Heading1 or RichTextBlockType.Heading2 or RichTextBlockType.Heading3
        or RichTextBlockType.Heading4 or RichTextBlockType.Heading5 or RichTextBlockType.Heading6;

    public bool IsCode => Type == RichTextBlockType.CodeBlock;

    public bool IsDivider => Type == RichTextBlockType.Divider;

    public bool IsImage => Type == RichTextBlockType.Image;

    public bool IsCell => Type == RichTextBlockType.TableCell;

    /// <summary>True for blocks without text that are deleted and selected as a whole: dividers and images.</summary>
    public bool IsAtomic => Type is RichTextBlockType.Divider or RichTextBlockType.Image;

    /// <summary>True for blocks that count as content even without text: dividers, images and table cells.</summary>
    public bool IsStructural => IsAtomic || IsCell;

    /// <summary>True when the caret can rest in the block's text (everything except dividers, images and cells a merged cell covers).</summary>
    public bool IsTextual => !IsAtomic && !IsCovered;

    /// <summary>True for the first cell of a table.</summary>
    public bool StartsTable => IsCell && Row == 0 && Col == 0;

    public bool IsEmpty => Text.Length == 0;

    public static RichTextEditorBlock Plain(RichTextBlockType type, string text, int level = 0) =>
        Create(type, level, string.IsNullOrEmpty(text) ? Array.Empty<RichTextEditorRun>() : new[] { new RichTextEditorRun(text) });

    /// <summary>Builds a block from runs: empty runs are dropped, equal neighbours merge, code blocks lose their marks.</summary>
    public static RichTextEditorBlock Create(RichTextBlockType type, int level, IEnumerable<RichTextEditorRun> runs, RichTextEditorBlockAttrs attrs = default)
    {
        if (type == RichTextBlockType.Divider)
        {
            return Divider;
        }
        if (type == RichTextBlockType.Image)
        {
            return new RichTextEditorBlock(type, 0, string.Empty, s_noSpans, Limit(type, attrs));
        }
        if (!(type is RichTextBlockType.BulletedList or RichTextBlockType.NumberedList or RichTextBlockType.TodoList))
        {
            level = 0;
        }
        attrs = Limit(type, attrs);
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
        return new RichTextEditorBlock(type, Math.Max(0, level), text.ToString(), spans.Count == 0 ? s_noSpans : spans.ToArray(), attrs);
    }

    /// <summary>
    /// Drops what a block type cannot carry: only to-do items check, only code blocks have a language, only paragraphs, headings, quotes
    /// and table cells align, only images have an address and a description, only table cells have a row, a column, spans and a cover flag,
    /// only images and first-row cells have a width, only callouts and cells have a tone, and only a table's first cell has the header flags.
    /// </summary>
    private static RichTextEditorBlockAttrs Limit(RichTextBlockType type, RichTextEditorBlockAttrs attrs)
    {
        var aligns = type is RichTextBlockType.Paragraph or RichTextBlockType.Heading1 or RichTextBlockType.Heading2 or RichTextBlockType.Heading3
            or RichTextBlockType.Heading4 or RichTextBlockType.Heading5 or RichTextBlockType.Heading6
            or RichTextBlockType.Quote or RichTextBlockType.TableCell or RichTextBlockType.Callout;
        var image = type == RichTextBlockType.Image;
        var cell = type == RichTextBlockType.TableCell;
        var first = cell && attrs.Row == 0 && attrs.Col == 0;
        return new RichTextEditorBlockAttrs(
            type == RichTextBlockType.TodoList && attrs.Checked,
            aligns ? attrs.Align : RichTextAlign.Start,
            type == RichTextBlockType.CodeBlock && !string.IsNullOrWhiteSpace(attrs.Language) ? attrs.Language.Trim() : null,
            image ? attrs.Src : null,
            image && !string.IsNullOrWhiteSpace(attrs.Alt) ? attrs.Alt.Trim() : null,
            cell ? Math.Max(0, attrs.Row) : 0,
            cell ? Math.Max(0, attrs.Col) : 0,
            image || cell && attrs.Row == 0 ? Math.Clamp(attrs.Width, 0, 10000) : 0,
            type == RichTextBlockType.Callout ? (string.IsNullOrWhiteSpace(attrs.Icon) ? DefaultCalloutIcon : attrs.Icon.Trim()) : null,
            type == RichTextBlockType.Callout || cell ? attrs.Tone : RichTextColor.Default,
            cell && !attrs.Covered && attrs.RowSpan > 1 ? Math.Min(attrs.RowSpan, RichTextEditorTables.MaxRows) : 0,
            cell && !attrs.Covered && attrs.ColSpan > 1 ? Math.Min(attrs.ColSpan, RichTextEditorTables.MaxColumns) : 0,
            cell && attrs.Covered,
            first && attrs.NoHeaderRow,
            first && attrs.HeaderColumn,
            type is RichTextBlockType.CodeBlock or RichTextBlockType.Divider or RichTextBlockType.Image or RichTextBlockType.TableCell
                ? RichTextLineSpacing.Default
                : attrs.Spacing,
            type == RichTextBlockType.Toggle && attrs.Collapsed);
    }

    /// <summary>True for a toggle item that folds its nested items away.</summary>
    public bool IsCollapsed => Attrs.Collapsed;

    public bool IsToggle => Type == RichTextBlockType.Toggle;

    /// <summary>The line spacing of a text block.</summary>
    public RichTextLineSpacing Spacing => Attrs.Spacing;

    /// <summary>The icon a callout starts with.</summary>
    public const string DefaultCalloutIcon = "💡";

    /// <summary>The icon of a callout block.</summary>
    public string? Icon => Attrs.Icon;

    /// <summary>The color of a callout block or the background of a table cell (<see cref="RichTextColor.Default"/> is neutral).</summary>
    public RichTextColor Tone => Attrs.Tone;

    public bool IsCallout => Type == RichTextBlockType.Callout;

    /// <summary>True when <paramref name="other"/> is a callout block that draws in the same box as this one.</summary>
    public bool SharesCalloutBox(RichTextEditorBlock other) => IsCallout && other.IsCallout && Attrs.Icon == other.Attrs.Icon && Attrs.Tone == other.Attrs.Tone;

    /// <summary>A block of the same type and level with other attributes (limited to what the type supports).</summary>
    public RichTextEditorBlock WithAttrs(RichTextEditorBlockAttrs attrs)
    {
        attrs = Limit(Type, attrs);
        return attrs == Attrs || IsDivider ? this : Create(Type, Level, Runs(), attrs);
    }

    /// <summary>The attributes a block that continues this one (after Enter) starts with: the alignment carries, a checkmark and a fold do not.</summary>
    public RichTextEditorBlockAttrs ContinuedAttrs => Attrs with { Checked = false, Collapsed = false };

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
        if (IsAtomic || IsCell || type is RichTextBlockType.Image or RichTextBlockType.TableCell)
        {
            return this;
        }
        return Create(type, nextLevel, Runs(), Attrs);
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
        return Create(Type, Level, runs, Attrs);
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

/// <summary>The cells of one table inside the block list: consecutive <see cref="RichTextBlockType.TableCell"/> blocks in row-major order.</summary>
/// <param name="Start">Index of the first cell.</param>
/// <param name="End">Index after the last cell.</param>
/// <param name="Rows">Number of rows (the first is the header).</param>
/// <param name="Cols">Number of columns.</param>
internal readonly record struct RichTextEditorTableExtent(int Start, int End, int Rows, int Cols)
{
    /// <summary>Index of the cell at <paramref name="row"/> and <paramref name="col"/>.</summary>
    public int IndexOf(int row, int col) => Start + row * Cols + col;
}

internal static class RichTextEditorTables
{
    public const int MaxRows = 100;
    public const int MaxColumns = 20;

    /// <summary>The narrowest a column draws or can be dragged to.</summary>
    public const double MinColumnWidth = 48;

    /// <summary>True when <paramref name="cell"/> is a header cell of the table whose first cell is <paramref name="first"/>.</summary>
    public static bool IsHeader(RichTextEditorBlock first, RichTextEditorBlock cell) =>
        cell.Row == 0 && !first.Attrs.NoHeaderRow || cell.Col == 0 && first.Attrs.HeaderColumn;

    /// <summary>
    /// The drawn widths of the table's columns filling <paramref name="width"/>: columns without a stored width share what the others leave
    /// (at least <see cref="MinColumnWidth"/> each), and stored widths scale to fit.
    /// </summary>
    public static double[] ColumnWidths(IReadOnlyList<RichTextEditorBlock> blocks, RichTextEditorTableExtent table, double width)
    {
        var widths = new double[table.Cols];
        var stored = 0d;
        var auto = 0;
        for (var c = 0; c < table.Cols; c++)
        {
            var w = table.IndexOf(0, c) < table.End ? blocks[table.IndexOf(0, c)].Width : 0;
            widths[c] = w;
            stored += w;
            auto += w > 0 ? 0 : 1;
        }
        if (auto == table.Cols)
        {
            Array.Fill(widths, width / table.Cols);
            return widths;
        }
        var share = auto == 0 ? 0 : Math.Max((width - stored) / auto, Math.Min(MinColumnWidth, width / table.Cols));
        var scale = (width - share * auto) / stored;
        for (var c = 0; c < table.Cols; c++)
        {
            widths[c] = widths[c] > 0 ? widths[c] * scale : share;
        }
        return widths;
    }

    /// <summary>The table holding the cell at <paramref name="index"/>; the cells must be in row-major order.</summary>
    public static RichTextEditorTableExtent Extent(IReadOnlyList<RichTextEditorBlock> blocks, int index)
    {
        var start = index;
        while (start > 0 && !blocks[start].StartsTable && blocks[start - 1].IsCell)
        {
            start--;
        }
        var end = start + 1;
        var rows = 1;
        var cols = 1;
        while (end < blocks.Count && blocks[end].IsCell && !blocks[end].StartsTable)
        {
            rows = Math.Max(rows, blocks[end].Row + 1);
            cols = Math.Max(cols, blocks[end].Col + 1);
            end++;
        }
        return new RichTextEditorTableExtent(start, end, rows, cols);
    }

    /// <summary>A table of empty cells.</summary>
    public static List<RichTextEditorBlock> Create(int rows, int cols)
    {
        var cells = new List<RichTextEditorBlock>(rows * cols);
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                cells.Add(RichTextEditorBlock.Cell(r, c));
            }
        }
        return cells;
    }

    /// <summary>
    /// Cells in row-major order with every position filled and numbered and every merged cell inside the grid, apart from the others and
    /// covering only empty cells; null when the cells already are.
    /// </summary>
    public static List<RichTextEditorBlock>? Repair(IReadOnlyList<RichTextEditorBlock> blocks, RichTextEditorTableExtent table)
    {
        var count = table.End - table.Start;
        var ok = count == table.Rows * table.Cols;
        var spans = false;
        for (var k = 0; k < count; k++)
        {
            var cell = blocks[table.Start + k];
            spans |= cell.IsMerged || cell.IsCovered;
            ok = ok && cell.Row == k / table.Cols && cell.Col == k % table.Cols;
        }
        if (ok && !spans)
        {
            return null;
        }
        List<RichTextEditorBlock> repaired;
        if (ok)
        {
            repaired = new List<RichTextEditorBlock>(count);
            for (var k = table.Start; k < table.End; k++)
            {
                repaired.Add(blocks[k]);
            }
        }
        else
        {
            var grid = new RichTextEditorBlock?[table.Rows, table.Cols];
            for (var k = table.Start; k < table.End; k++)
            {
                grid[blocks[k].Row, blocks[k].Col] ??= blocks[k];
            }
            repaired = new List<RichTextEditorBlock>(table.Rows * table.Cols);
            for (var r = 0; r < table.Rows; r++)
            {
                for (var c = 0; c < table.Cols; c++)
                {
                    repaired.Add(grid[r, c] ?? RichTextEditorBlock.Cell(r, c));
                }
            }
        }
        var fixedSpans = spans && FixSpans(repaired, table.Rows, table.Cols);
        return ok && !fixedSpans ? null : repaired;
    }

    /// <summary>
    /// Makes the merged cells of row-major <paramref name="cells"/> consistent: a merged cell stays inside the grid and off the cells of an
    /// earlier one, and the cells it covers are empty and flagged while every other cell is not. Returns true when a cell changed.
    /// </summary>
    private static bool FixSpans(List<RichTextEditorBlock> cells, int rows, int cols)
    {
        var owned = new bool[rows, cols];
        var changed = false;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var i = r * cols + c;
                var cell = cells[i];
                if (owned[r, c])
                {
                    if (!cell.IsCovered || cell.Length > 0 || cell.IsMerged)
                    {
                        cells[i] = RichTextEditorBlock.Cell(r, c).WithAttrs(new RichTextEditorBlockAttrs(Align: cell.Align, Row: r, Col: c, Width: cell.Width, Covered: true));
                        changed = true;
                    }
                    continue;
                }
                if (cell.IsCovered)
                {
                    cell = cell.WithAttrs(cell.Attrs with { Covered = false });
                    cells[i] = cell;
                    changed = true;
                }
                var cs = Math.Min(cell.ColSpan, cols - c);
                for (var k = 1; k < cs; k++)
                {
                    if (owned[r, c + k])
                    {
                        cs = k;
                        break;
                    }
                }
                var rs = Math.Min(cell.RowSpan, rows - r);
                for (var k = 1; k < rs; k++)
                {
                    var free = true;
                    for (var j = 0; j < cs && free; j++)
                    {
                        free = !owned[r + k, c + j];
                    }
                    if (!free)
                    {
                        rs = k;
                        break;
                    }
                }
                if (cs != cell.ColSpan || rs != cell.RowSpan)
                {
                    cells[i] = cell.WithAttrs(cell.Attrs with { RowSpan = rs > 1 ? rs : 0, ColSpan = cs > 1 ? cs : 0 });
                    changed = true;
                }
                for (var k = 0; k < rs; k++)
                {
                    for (var j = 0; j < cs; j++)
                    {
                        owned[r + k, c + j] = true;
                    }
                }
            }
        }
        return changed;
    }

    /// <summary>The row and column of the cell that shows grid position (<paramref name="row"/>, <paramref name="col"/>): the cell itself, or the merged cell covering it.</summary>
    public static (int Row, int Col) Owner(Func<int, int, RichTextEditorBlock> at, int row, int col)
    {
        if (!at(row, col).IsCovered)
        {
            return (row, col);
        }
        for (var r = row; r >= 0; r--)
        {
            for (var c = col; c >= 0; c--)
            {
                var cell = at(r, c);
                if (!cell.IsCovered && r + cell.RowSpan > row && c + cell.ColSpan > col)
                {
                    return (r, c);
                }
            }
        }
        return (row, col);
    }

    /// <summary>The index of the block that shows grid position (<paramref name="row"/>, <paramref name="col"/>).</summary>
    public static int OwnerIndex(IReadOnlyList<RichTextEditorBlock> blocks, RichTextEditorTableExtent table, int row, int col)
    {
        var (r, c) = Owner((rr, cc) => blocks[table.IndexOf(rr, cc)], Math.Clamp(row, 0, table.Rows - 1), Math.Clamp(col, 0, table.Cols - 1));
        return table.IndexOf(r, c);
    }

    /// <summary>Widens the rectangle until no merged cell sticks out of it.</summary>
    public static (int R1, int C1, int R2, int C2) Expand(Func<int, int, RichTextEditorBlock> at, int r1, int c1, int r2, int c2)
    {
        bool grew;
        do
        {
            grew = false;
            for (var r = r1; r <= r2; r++)
            {
                for (var c = c1; c <= c2; c++)
                {
                    var (ar, ac) = Owner(at, r, c);
                    var cell = at(ar, ac);
                    var nr1 = Math.Min(r1, ar);
                    var nc1 = Math.Min(c1, ac);
                    var nr2 = Math.Max(r2, ar + cell.RowSpan - 1);
                    var nc2 = Math.Max(c2, ac + cell.ColSpan - 1);
                    if (nr1 != r1 || nc1 != c1 || nr2 != r2 || nc2 != c2)
                    {
                        (r1, c1, r2, c2) = (nr1, nc1, nr2, nc2);
                        grew = true;
                    }
                }
            }
        }
        while (grew);
        return (r1, c1, r2, c2);
    }

    /// <summary>True when the rectangle holds more than one cell (a merged cell that fills it alone does not count).</summary>
    public static bool IsMergeable(Func<int, int, RichTextEditorBlock> at, int r1, int c1, int r2, int c2)
    {
        var head = at(r1, c1);
        return !(!head.IsCovered && head.RowSpan == r2 - r1 + 1 && head.ColSpan == c2 - c1 + 1);
    }

    /// <summary>Merges the rectangle into its top left cell: the texts join with line breaks and the other cells become covered.</summary>
    public static void Merge(List<List<RichTextEditorBlock>> grid, int r1, int c1, int r2, int c2)
    {
        var runs = new List<RichTextEditorRun>();
        for (var r = r1; r <= r2; r++)
        {
            for (var c = c1; c <= c2; c++)
            {
                var cell = grid[r][c];
                if (cell.IsCovered || cell.Length == 0)
                {
                    continue;
                }
                if (runs.Count > 0)
                {
                    runs.Add(new RichTextEditorRun("\n"));
                }
                runs.AddRange(cell.Slice(0, cell.Length));
            }
        }
        var head = grid[r1][c1];
        for (var r = r1; r <= r2; r++)
        {
            for (var c = c1; c <= c2; c++)
            {
                grid[r][c] = RichTextEditorBlock.Cell(r, c).WithAttrs(new RichTextEditorBlockAttrs(Align: head.Align, Row: r, Col: c, Covered: true));
            }
        }
        grid[r1][c1] = RichTextEditorBlock.Create(RichTextBlockType.TableCell, 0, runs,
            head.Attrs with { RowSpan = r2 - r1 + 1, ColSpan = c2 - c1 + 1, Covered = false });
    }

    /// <summary>Gives the cells a merged cell covers back as empty cells; returns the merged cell's position, or null when the cell is not merged.</summary>
    public static (int Row, int Col)? Split(List<List<RichTextEditorBlock>> grid, int row, int col)
    {
        var (r0, c0) = Owner((r, c) => grid[r][c], row, col);
        var head = grid[r0][c0];
        if (!head.IsMerged)
        {
            return null;
        }
        for (var r = r0; r < r0 + head.RowSpan; r++)
        {
            for (var c = c0; c < c0 + head.ColSpan; c++)
            {
                grid[r][c] = RichTextEditorBlock.Cell(r, c).WithAttrs(new RichTextEditorBlockAttrs(Align: head.Align, Row: r, Col: c));
            }
        }
        grid[r0][c0] = head.WithAttrs(head.Attrs with { RowSpan = 0, ColSpan = 0 });
        return (r0, c0);
    }

    /// <summary>Inserts a row before row <paramref name="at"/>; a merged cell that spans the boundary grows and covers the new row's cells under it.</summary>
    public static void InsertRow(List<List<RichTextEditorBlock>> grid, int at, Func<int, RichTextEditorBlock> create)
    {
        var cols = grid[0].Count;
        var row = new List<RichTextEditorBlock>(cols);
        var grown = new HashSet<(int, int)>();
        for (var c = 0; c < cols; c++)
        {
            var cell = create(c);
            if (at > 0 && at < grid.Count)
            {
                var (r0, c0) = Owner((r, k) => grid[r][k], at, c);
                if (r0 < at)
                {
                    if (grown.Add((r0, c0)))
                    {
                        var owner = grid[r0][c0];
                        grid[r0][c0] = owner.WithAttrs(owner.Attrs with { RowSpan = owner.RowSpan + 1 });
                    }
                    cell = cell.WithAttrs(cell.Attrs with { Covered = true });
                }
            }
            row.Add(cell);
        }
        grid.Insert(at, row);
    }

    /// <summary>Inserts a column before column <paramref name="at"/>; a merged cell that spans the boundary grows and covers the new column's cells beside it.</summary>
    public static void InsertColumn(List<List<RichTextEditorBlock>> grid, int at, Func<int, RichTextEditorBlock> create)
    {
        var cols = grid[0].Count;
        var grown = new HashSet<(int, int)>();
        for (var r = 0; r < grid.Count; r++)
        {
            var cell = create(r);
            if (at > 0 && at < cols)
            {
                var (r0, c0) = Owner((rr, k) => grid[rr][k], r, at);
                if (c0 < at)
                {
                    if (grown.Add((r0, c0)))
                    {
                        var owner = grid[r0][c0];
                        grid[r0][c0] = owner.WithAttrs(owner.Attrs with { ColSpan = owner.ColSpan + 1 });
                    }
                    cell = cell.WithAttrs(cell.Attrs with { Covered = true });
                }
            }
            grid[r].Insert(at, cell);
        }
    }

    /// <summary>
    /// True when row <paramref name="row"/> can move to the gap <paramref name="to"/> (0 is above the first row, <paramref name="rows"/> below
    /// the last): no merged cell spans rows through it or across the gap, and the gap is not one of its own edges.
    /// </summary>
    public static bool CanMoveRow(Func<int, int, RichTextEditorBlock> at, int rows, int cols, int row, int to)
    {
        if (row < 0 || row >= rows || to < 0 || to > rows || to == row || to == row + 1)
        {
            return false;
        }
        for (var c = 0; c < cols; c++)
        {
            var cell = at(row, c);
            if (cell.IsCovered ? Owner(at, row, c).Row != row : cell.RowSpan > 1)
            {
                return false;
            }
            if (to > 0 && to < rows && Owner(at, to, c).Row < to)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>True when column <paramref name="col"/> can move to the gap <paramref name="to"/>, as <see cref="CanMoveRow"/> says for rows.</summary>
    public static bool CanMoveColumn(Func<int, int, RichTextEditorBlock> at, int rows, int cols, int col, int to)
    {
        if (col < 0 || col >= cols || to < 0 || to > cols || to == col || to == col + 1)
        {
            return false;
        }
        for (var r = 0; r < rows; r++)
        {
            var cell = at(r, col);
            if (cell.IsCovered ? Owner(at, r, col).Col != col : cell.ColSpan > 1)
            {
                return false;
            }
            if (to > 0 && to < cols && Owner(at, r, to).Col < to)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Where row (or column) <paramref name="index"/> ends up when the one at <paramref name="from"/> moves to the gap <paramref name="to"/>.</summary>
    public static int Moved(int index, int from, int to)
    {
        var target = to > from ? to - 1 : to;
        if (index == from)
        {
            return target;
        }
        if (from < index && index <= target)
        {
            return index - 1;
        }
        if (target <= index && index < from)
        {
            return index + 1;
        }
        return index;
    }

    /// <summary>Moves row <paramref name="row"/> to the gap <paramref name="to"/>; <see cref="CanMoveRow"/> must allow it.</summary>
    public static void MoveRow(List<List<RichTextEditorBlock>> grid, int row, int to)
    {
        var moved = grid[row];
        grid.RemoveAt(row);
        grid.Insert(to > row ? to - 1 : to, moved);
    }

    /// <summary>Moves column <paramref name="col"/> to the gap <paramref name="to"/>; <see cref="CanMoveColumn"/> must allow it.</summary>
    public static void MoveColumn(List<List<RichTextEditorBlock>> grid, int col, int to)
    {
        foreach (var cells in grid)
        {
            var moved = cells[col];
            cells.RemoveAt(col);
            cells.Insert(to > col ? to - 1 : to, moved);
        }
    }

    /// <summary>Removes row <paramref name="row"/>; merged cells that start in it move down a row and the ones that cross it shrink.</summary>
    public static void DeleteRow(List<List<RichTextEditorBlock>> grid, int row)
    {
        var cols = grid[row].Count;
        for (var c = 0; c < cols; c++)
        {
            var cell = grid[row][c];
            if (!cell.IsCovered)
            {
                if (cell.RowSpan > 1)
                {
                    grid[row + 1][c] = cell.WithAttrs(cell.Attrs with { RowSpan = cell.RowSpan - 1 });
                }
                continue;
            }
            var (r0, c0) = Owner((r, k) => grid[r][k], row, c);
            if (r0 < row && c0 == c)
            {
                var owner = grid[r0][c0];
                grid[r0][c0] = owner.WithAttrs(owner.Attrs with { RowSpan = owner.RowSpan - 1 });
            }
        }
        grid.RemoveAt(row);
    }

    /// <summary>Removes column <paramref name="col"/>; merged cells that start in it move right a column and the ones that cross it shrink.</summary>
    public static void DeleteColumn(List<List<RichTextEditorBlock>> grid, int col)
    {
        for (var r = 0; r < grid.Count; r++)
        {
            var cell = grid[r][col];
            if (!cell.IsCovered)
            {
                if (cell.ColSpan > 1)
                {
                    grid[r][col + 1] = cell.WithAttrs(cell.Attrs with { ColSpan = cell.ColSpan - 1, Width = grid[r][col + 1].Width });
                }
                continue;
            }
            var (r0, c0) = Owner((rr, k) => grid[rr][k], r, col);
            if (c0 < col && r0 == r)
            {
                var owner = grid[r0][c0];
                grid[r0][c0] = owner.WithAttrs(owner.Attrs with { ColSpan = owner.ColSpan - 1 });
            }
        }
        foreach (var cells in grid)
        {
            cells.RemoveAt(col);
        }
    }
}

/// <summary>The active formatting at the selection, for toolbars: marks, the link address and the block type.</summary>
/// <param name="IsBold">Every selected character is bold (or the caret types bold).</param>
/// <param name="IsItalic">Every selected character is italic (or the caret types italic).</param>
/// <param name="IsStrikethrough">Every selected character is struck through (or the caret types struck through).</param>
/// <param name="IsCode">The selection starts in inline code.</param>
/// <param name="Link">The address of the link at the selection start, or null.</param>
/// <param name="BlockType">The type of the block holding the selection start.</param>
/// <param name="IsUnderline">Every selected character is underlined (or the caret types underlined).</param>
/// <param name="TextColor">The text color shared by the selection, or <see cref="RichTextColor.Default"/> when it has none or several.</param>
/// <param name="Highlight">The highlight color shared by the selection, or <see cref="RichTextColor.Default"/> when it has none or several.</param>
/// <param name="Align">The alignment of the block holding the selection start.</param>
/// <param name="CodeLanguage">The language label of the code block holding the selection start, or null.</param>
/// <param name="IsSuperscript">Every selected character is superscript (or the caret types superscript).</param>
/// <param name="IsSubscript">Every selected character is subscript (or the caret types subscript).</param>
/// <param name="Size">The text size shared by the selection (or the size the caret types), <see cref="RichTextSize.Default"/> when they differ.</param>
/// <param name="LineSpacing">The line spacing of the block holding the selection start.</param>
public sealed record RichTextFormats(bool IsBold, bool IsItalic, bool IsStrikethrough, bool IsCode, string? Link, RichTextBlockType BlockType,
    bool IsUnderline = false, RichTextColor TextColor = RichTextColor.Default, RichTextColor Highlight = RichTextColor.Default,
    RichTextAlign Align = RichTextAlign.Start, string? CodeLanguage = null, bool IsSuperscript = false, bool IsSubscript = false,
    RichTextSize Size = RichTextSize.Default, RichTextLineSpacing LineSpacing = RichTextLineSpacing.Default)
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

/// <summary>One choice of the code block language list: the label shown and the id that is stored (and highlighted); a null id is plain text.</summary>
/// <param name="Label">The name shown in the list.</param>
/// <param name="Id">The language id written to the block (<c>data-lang</c>, the Markdown fence), or null for plain text.</param>
public sealed record RichTextCodeLanguage(string Label, string? Id)
{
    /// <summary>The languages the list offers unless <see cref="RichTextEditor.CodeLanguages"/> is set.</summary>
    public static IReadOnlyList<RichTextCodeLanguage> Defaults { get; } = new RichTextCodeLanguage[]
    {
        new("Plain text", null), new("Bash", "bash"), new("C", "c"), new("C++", "cpp"), new("C#", "csharp"), new("CSS", "css"), new("Go", "go"),
        new("HTML", "html"), new("Java", "java"), new("JavaScript", "javascript"), new("JSON", "json"), new("Kotlin", "kotlin"),
        new("Markdown", "markdown"), new("PHP", "php"), new("Python", "python"), new("Ruby", "ruby"), new("Rust", "rust"), new("SQL", "sql"),
        new("Swift", "swift"), new("TypeScript", "typescript"), new("XML", "xml"), new("YAML", "yaml")
    };
}
