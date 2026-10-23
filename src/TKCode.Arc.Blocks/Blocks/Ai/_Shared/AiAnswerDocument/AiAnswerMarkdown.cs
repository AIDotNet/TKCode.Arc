using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>What a block of an answer is.</summary>
internal enum AiAnswerBlockKind
{
    /// <summary>Running text.</summary>
    Paragraph,

    /// <summary>A short heading (every level looks the same in an answer).</summary>
    Heading,

    /// <summary>An ordered or bullet list; each item is one text.</summary>
    List,

    /// <summary>A table: the first row is the header when a delimiter row follows it.</summary>
    Table,

    /// <summary>A fenced code block.</summary>
    Code
}

/// <summary>What an inline piece of a text is.</summary>
internal enum AiAnswerInlineKind
{
    /// <summary>Plain, bold or italic text.</summary>
    Text,

    /// <summary>Inline code.</summary>
    Code,

    /// <summary>A numbered citation, <c>[n]</c>.</summary>
    Citation
}

/// <summary>Text styles of an inline piece.</summary>
[Flags]
internal enum AiAnswerInlineStyle : byte
{
    /// <summary>Regular.</summary>
    None = 0,

    /// <summary>Strong (<c>**x**</c>); drawn at weight 500.</summary>
    Bold = 1,

    /// <summary>Emphasis (<c>*x*</c>).</summary>
    Italic = 2
}

/// <summary>One inline piece of a text: plain text (with its style), an inline code span or a citation.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Text">The text shown (the code of a code span; empty for a citation).</param>
/// <param name="Style">The text style.</param>
/// <param name="Number">The cited number of a citation.</param>
internal readonly record struct AiAnswerInline(AiAnswerInlineKind Kind, string Text, AiAnswerInlineStyle Style = AiAnswerInlineStyle.None, int Number = 0)
{
    /// <summary>A text piece.</summary>
    public static AiAnswerInline Plain(string text, AiAnswerInlineStyle style = AiAnswerInlineStyle.None) => new(AiAnswerInlineKind.Text, text, style);

    /// <summary>An inline code piece.</summary>
    public static AiAnswerInline CodeSpan(string code) => new(AiAnswerInlineKind.Code, code);

    /// <summary>A citation of <paramref name="number"/>.</summary>
    public static AiAnswerInline Cite(int number) => new(AiAnswerInlineKind.Citation, string.Empty, AiAnswerInlineStyle.None, number);
}

/// <summary>
/// One block of an answer as parsed from its Markdown: the inline texts it shows (one for a paragraph or heading, one
/// per item of a list, one per cell of a table) or the code of a fenced block.
/// </summary>
internal sealed class AiAnswerBlock
{
    /// <summary>The kind.</summary>
    public AiAnswerBlockKind Kind { get; init; }

    /// <summary>Where the block starts in the Markdown.</summary>
    public int Start { get; init; }

    /// <summary>The heading level (1-6).</summary>
    public int Level { get; init; }

    /// <summary>Whether a list is numbered.</summary>
    public bool Ordered { get; init; }

    /// <summary>The number of the first item of an ordered list.</summary>
    public int ListStart { get; init; } = 1;

    /// <summary>The texts: the paragraph or heading, each list item, or each table row's cells (see <see cref="Rows"/>).</summary>
    public List<AiAnswerInline[]> Texts { get; } = [];

    /// <summary>The table rows: how many cells each row has (the cells are in <see cref="Texts"/>, row by row).</summary>
    public List<int> Rows { get; } = [];

    /// <summary>Whether the first table row is a header (a delimiter row followed it).</summary>
    public bool HasHeader { get; init; }

    /// <summary>The language of a code block (the first word of its info string), or empty.</summary>
    public string Language { get; init; } = string.Empty;

    /// <summary>The file name of a code block (the rest of its info string), or empty.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>The code of a code block, without the fences.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Whether a code block's closing fence has arrived.</summary>
    public bool IsClosed { get; set; }

    /// <summary>True when <paramref name="other"/> shows the same thing (kind, attributes and every text).</summary>
    public bool SameAs(AiAnswerBlock other)
    {
        if (Kind != other.Kind || Level != other.Level || Ordered != other.Ordered || ListStart != other.ListStart
            || HasHeader != other.HasHeader || !string.Equals(Language, other.Language, StringComparison.Ordinal)
            || !string.Equals(FileName, other.FileName, StringComparison.Ordinal) || !string.Equals(Code, other.Code, StringComparison.Ordinal)
            || IsClosed != other.IsClosed || Texts.Count != other.Texts.Count || !Rows.SequenceEqual(other.Rows))
        {
            return false;
        }
        for (var i = 0; i < Texts.Count; i++)
        {
            if (!Texts[i].AsSpan().SequenceEqual(other.Texts[i]))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// The Markdown subset of an answer: paragraphs, headings, ordered and bullet lists, tables, fenced code, and inline bold,
/// italic, code spans and <c>[n]</c> citations (links show their text). Pure text in, blocks out.
/// </summary>
/// <remarks>
/// <para>
/// Streaming (<c>final: false</c>): the last line is shown word by word in paragraphs and list items, but a heading, a
/// table row or a code line (and a fence line) waits until its line break has arrived, so those appear one whole line at a
/// time. Inline markup still open at the very end of the stream is taken as open (<c>**Mirror</c> shows "Mirror" in bold,
/// an unclosed code span shows as code) so the text does not flash its markers; at the end of the stream (<c>final</c>)
/// an unclosed marker is plain text again.
/// </para>
/// <para>
/// Citations follow the reply text's rules (<see cref="AiCitationText"/>): 1-3 digits without a leading zero, not escaped,
/// not a link text or a link definition, not the label of a reference link; <c>[1][2]</c> is two citations.
/// </para>
/// </remarks>
internal static class AiAnswerMarkdown
{
    /// <summary>The non-breaking space put before a citation so its pill never starts a line alone.</summary>
    public const char NoBreakSpace = ' ';

    /// <summary>Parses <paramref name="source"/>. Without <paramref name="final"/> the text is a stream that may continue.</summary>
    public static List<AiAnswerBlock> Parse(string source, bool final = true)
    {
        var lines = SplitLines(source ?? string.Empty, final);
        var blocks = new List<AiAnswerBlock>();
        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (IsBlank(line.Text))
            {
                i++;
                continue;
            }
            if (TryFence(line.Text, out var fenceChar, out var fenceLength, out var info))
            {
                i = ParseCode(lines, i, fenceChar, fenceLength, info, final, blocks);
                continue;
            }
            if (TryHeading(line.Text, out var level, out var headingText))
            {
                blocks.Add(TextBlock(AiAnswerBlockKind.Heading, line.Start, level, headingText, open: !final && i == lines.Count - 1));
                i++;
                continue;
            }
            if (IsTableRow(line.Text))
            {
                i = ParseTable(lines, i, final, blocks);
                continue;
            }
            if (TryListItem(line.Text, out _, out _, out _))
            {
                i = ParseList(lines, i, final, blocks);
                continue;
            }
            if (IsThematicBreak(line.Text))
            {
                i++;
                continue;
            }
            i = ParseParagraph(lines, i, final, blocks);
        }
        return blocks;
    }

    #region Lines

    private readonly record struct Line(string Text, int Start);

    /// <summary>
    /// The lines of <paramref name="source"/>. While streaming, a last line without its line break is dropped when it
    /// would be a heading, a table row, a fence or a line of an open code block (those appear whole); the code-block test
    /// is made by the caller, so here only the line-start forms are checked, and an open fence is detected by counting.
    /// </summary>
    private static List<Line> SplitLines(string source, bool final)
    {
        var lines = new List<Line>();
        var start = 0;
        var inFence = false;
        var fenceChar = '\0';
        var fenceLength = 0;
        while (start <= source.Length)
        {
            var end = source.IndexOf('\n', start);
            var complete = end >= 0;
            if (!complete)
            {
                end = source.Length;
            }
            var text = source.AsSpan(start, end - start).TrimEnd('\r').ToString();
            if (!complete)
            {
                if (start == source.Length)
                {
                    break;
                }
                if (!final && (inFence || IsGatedLineStart(text)))
                {
                    break;
                }
            }
            lines.Add(new Line(text, start));
            if (inFence)
            {
                if (IsClosingFence(text, fenceChar, fenceLength))
                {
                    inFence = false;
                }
            }
            else if (TryFence(text, out var ch, out var length, out _))
            {
                inFence = true;
                fenceChar = ch;
                fenceLength = length;
            }
            if (!complete)
            {
                break;
            }
            start = end + 1;
        }
        return lines;
    }

    /// <summary>Whether a line that is still arriving waits for its line break: a heading, a table row or a fence.</summary>
    private static bool IsGatedLineStart(string text)
    {
        var trimmed = text.AsSpan().TrimStart(' ');
        if (trimmed.Length == 0)
        {
            return false;
        }
        return trimmed[0] is '#' or '|' || trimmed.StartsWith("```") || trimmed.StartsWith("~~~");
    }

    private static bool IsBlank(string text) => text.AsSpan().Trim().Length == 0;

    private static int Indent(string text)
    {
        var count = 0;
        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                count++;
            }
            else if (ch == '\t')
            {
                count += 4 - count % 4;
            }
            else
            {
                break;
            }
        }
        return count;
    }

    #endregion

    #region Blocks

    private static AiAnswerBlock TextBlock(AiAnswerBlockKind kind, int start, int level, string text, bool open)
    {
        var block = new AiAnswerBlock { Kind = kind, Start = start, Level = level };
        block.Texts.Add(ParseInlines(text, open));
        return block;
    }

    private static bool TryHeading(string text, out int level, out string content)
    {
        level = 0;
        content = string.Empty;
        if (Indent(text) > 3)
        {
            return false;
        }
        var trimmed = text.TrimStart(' ');
        while (level < trimmed.Length && trimmed[level] == '#')
        {
            level++;
        }
        if (level is < 1 or > 6 || (level < trimmed.Length && trimmed[level] is not (' ' or '\t')))
        {
            level = 0;
            return false;
        }
        var body = trimmed[level..].Trim();
        // A closing sequence of #s (after a space) is not part of the heading.
        var end = body.Length;
        while (end > 0 && body[end - 1] == '#')
        {
            end--;
        }
        if (end < body.Length && (end == 0 || body[end - 1] == ' '))
        {
            body = body[..end].TrimEnd();
        }
        content = body;
        return true;
    }

    private static bool IsThematicBreak(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length < 3 || trimmed[0] is not ('-' or '*' or '_'))
        {
            return false;
        }
        var ch = trimmed[0];
        var count = 0;
        foreach (var c in trimmed)
        {
            if (c == ch)
            {
                count++;
            }
            else if (c != ' ')
            {
                return false;
            }
        }
        return count >= 3;
    }

    private static int ParseParagraph(List<Line> lines, int i, bool final, List<AiAnswerBlock> blocks)
    {
        var start = lines[i].Start;
        var builder = new StringBuilder();
        while (i < lines.Count)
        {
            var text = lines[i].Text;
            if (IsBlank(text))
            {
                break;
            }
            if (builder.Length > 0 && StartsBlock(text))
            {
                break;
            }
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }
            // A quote marker is dropped: quotes read as plain paragraphs in an answer.
            var trimmed = text.TrimStart(' ');
            if (trimmed.StartsWith('>'))
            {
                trimmed = trimmed[1..].TrimStart(' ');
            }
            builder.Append(trimmed);
            i++;
        }
        blocks.Add(TextBlock(AiAnswerBlockKind.Paragraph, start, 0, builder.ToString(), open: !final && i == lines.Count));
        return i;
    }

    /// <summary>Whether a line interrupts a paragraph or a list item: a heading, a fence, a table row, a list item or a break.</summary>
    private static bool StartsBlock(string text) =>
        TryHeading(text, out _, out _) || TryFence(text, out _, out _, out _) || IsTableRow(text)
        || TryListItem(text, out _, out _, out _) || IsThematicBreak(text);

    #endregion

    #region Lists

    /// <summary>A list item line: <c>1. text</c>, <c>1) text</c>, or <c>- text</c> / <c>* text</c> / <c>+ text</c>.</summary>
    private static bool TryListItem(string text, out bool ordered, out int number, out int contentStart)
    {
        ordered = false;
        number = 0;
        contentStart = 0;
        var indent = 0;
        while (indent < text.Length && text[indent] == ' ')
        {
            indent++;
        }
        if (indent >= text.Length)
        {
            return false;
        }
        var i = indent;
        if (text[i] is '-' or '*' or '+')
        {
            i++;
        }
        else
        {
            var digits = 0;
            while (i < text.Length && char.IsAsciiDigit(text[i]) && digits < 9)
            {
                number = number * 10 + (text[i] - '0');
                i++;
                digits++;
            }
            if (digits == 0 || i >= text.Length || text[i] is not ('.' or ')'))
            {
                number = 0;
                return false;
            }
            ordered = true;
            i++;
        }
        if (i < text.Length && text[i] is not (' ' or '\t'))
        {
            number = 0;
            return false;
        }
        // "- " alone at the end of a stream is an item that has not got its text yet; "---" is a break, not an item.
        if (!ordered && IsThematicBreak(text))
        {
            return false;
        }
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }
        contentStart = i;
        return true;
    }

    private static int ParseList(List<Line> lines, int i, bool final, List<AiAnswerBlock> blocks)
    {
        TryListItem(lines[i].Text, out var ordered, out var first, out _);
        var block = new AiAnswerBlock { Kind = AiAnswerBlockKind.List, Start = lines[i].Start, Ordered = ordered, ListStart = ordered ? first : 1 };
        var items = new List<StringBuilder>();
        while (i < lines.Count)
        {
            var text = lines[i].Text;
            if (TryListItem(text, out var itemOrdered, out _, out var contentStart))
            {
                if (itemOrdered != ordered)
                {
                    break;
                }
                items.Add(new StringBuilder(text[contentStart..]));
                i++;
                continue;
            }
            if (IsBlank(text))
            {
                // A blank line ends the list unless the next text is another item of it or an indented continuation.
                var next = i + 1;
                while (next < lines.Count && IsBlank(lines[next].Text))
                {
                    next++;
                }
                if (next < lines.Count && ((TryListItem(lines[next].Text, out var nextOrdered, out _, out _) && nextOrdered == ordered)
                                           || Indent(lines[next].Text) >= 2))
                {
                    i = next;
                    continue;
                }
                break;
            }
            if (StartsBlock(text) && Indent(text) < 2)
            {
                break;
            }
            // A continuation line (indented or lazy) belongs to the item above.
            items[^1].Append('\n').Append(text.TrimStart(' '));
            i++;
        }
        for (var k = 0; k < items.Count; k++)
        {
            block.Texts.Add(ParseInlines(items[k].ToString(), open: !final && i == lines.Count && k == items.Count - 1));
        }
        blocks.Add(block);
        return i;
    }

    #endregion

    #region Tables

    private static bool IsTableRow(string text)
    {
        var trimmed = text.AsSpan().TrimStart(' ');
        return trimmed.Length > 0 && trimmed[0] == '|' && Indent(text) <= 3;
    }

    private static int ParseTable(List<Line> lines, int i, bool final, List<AiAnswerBlock> blocks)
    {
        var start = lines[i].Start;
        var rows = new List<List<string>>();
        var hasHeader = false;
        while (i < lines.Count && IsTableRow(lines[i].Text))
        {
            var cells = SplitRow(lines[i].Text);
            if (rows.Count == 1 && !hasHeader && IsDelimiterRow(cells))
            {
                hasHeader = true;
            }
            else if (!(rows.Count > 1 && IsDelimiterRow(cells)))
            {
                rows.Add(cells);
            }
            i++;
        }
        var block = new AiAnswerBlock { Kind = AiAnswerBlockKind.Table, Start = start, HasHeader = hasHeader || (!final && rows.Count == 1) };
        var columns = rows.Count > 0 ? rows[0].Count : 0;
        foreach (var row in rows)
        {
            // Rows take the header's column count: short rows are padded, long ones cut (as GitHub does).
            var count = Math.Max(1, columns);
            for (var c = 0; c < count; c++)
            {
                block.Texts.Add(ParseInlines(c < row.Count ? row[c] : string.Empty, open: false));
            }
            block.Rows.Add(count);
        }
        blocks.Add(block);
        return i;
    }

    /// <summary>The cells of a table row: split at pipes that are not escaped and not inside a code span.</summary>
    internal static List<string> SplitRow(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }
        var cells = new List<string>();
        var cell = new StringBuilder();
        var code = 0;
        for (var i = 0; i < trimmed.Length; i++)
        {
            var ch = trimmed[i];
            if (ch == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|')
            {
                cell.Append('|');
                i++;
                continue;
            }
            if (ch == '`')
            {
                var run = 1;
                while (i + run < trimmed.Length && trimmed[i + run] == '`')
                {
                    run++;
                }
                code = code == 0 ? run : code == run ? 0 : code;
                cell.Append('`', run);
                i += run - 1;
                continue;
            }
            if (ch == '|' && code == 0)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }
            cell.Append(ch);
        }
        var last = cell.ToString().Trim();
        if (last.Length > 0)
        {
            cells.Add(last);
        }
        return cells;
    }

    private static bool IsDelimiterRow(List<string> cells)
    {
        if (cells.Count == 0)
        {
            return false;
        }
        foreach (var cell in cells)
        {
            var body = cell.AsSpan().Trim(':');
            if (body.Length == 0)
            {
                return false;
            }
            foreach (var ch in body)
            {
                if (ch != '-')
                {
                    return false;
                }
            }
        }
        return true;
    }

    #endregion

    #region Code

    private static bool TryFence(string text, out char fenceChar, out int length, out string info)
    {
        fenceChar = '\0';
        length = 0;
        info = string.Empty;
        if (Indent(text) > 3)
        {
            return false;
        }
        var trimmed = text.TrimStart(' ');
        if (trimmed.Length < 3 || trimmed[0] is not ('`' or '~'))
        {
            return false;
        }
        var ch = trimmed[0];
        var run = 0;
        while (run < trimmed.Length && trimmed[run] == ch)
        {
            run++;
        }
        if (run < 3)
        {
            return false;
        }
        var rest = trimmed[run..].Trim();
        if (ch == '`' && rest.Contains('`'))
        {
            return false;
        }
        fenceChar = ch;
        length = run;
        info = rest;
        return true;
    }

    private static bool IsClosingFence(string text, char fenceChar, int length)
    {
        if (Indent(text) > 3)
        {
            return false;
        }
        var trimmed = text.Trim();
        if (trimmed.Length < length)
        {
            return false;
        }
        foreach (var ch in trimmed)
        {
            if (ch != fenceChar)
            {
                return false;
            }
        }
        return true;
    }

    private static int ParseCode(List<Line> lines, int i, char fenceChar, int fenceLength, string info, bool final, List<AiAnswerBlock> blocks)
    {
        var (language, fileName) = SplitInfo(info);
        var start = lines[i].Start;
        i++;
        var code = new StringBuilder();
        var closed = false;
        var first = true;
        while (i < lines.Count)
        {
            var text = lines[i].Text;
            if (IsClosingFence(text, fenceChar, fenceLength))
            {
                closed = true;
                i++;
                break;
            }
            if (!first)
            {
                code.Append('\n');
            }
            code.Append(text);
            first = false;
            i++;
        }
        blocks.Add(new AiAnswerBlock
        {
            Kind = AiAnswerBlockKind.Code,
            Start = start,
            Language = language,
            FileName = fileName,
            Code = code.ToString(),
            IsClosed = closed || final
        });
        return i;
    }

    /// <summary>
    /// The language and file name of a fence's info string: <c>yaml ci/checks.yml</c>, <c>yaml title="ci/checks.yml"</c>
    /// or a bare file name with an extension (<c>checks.yml</c>, whose language is the extension).
    /// </summary>
    internal static (string Language, string FileName) SplitInfo(string info)
    {
        var trimmed = info.Trim();
        if (trimmed.Length == 0)
        {
            return (string.Empty, string.Empty);
        }
        var space = trimmed.IndexOfAny([' ', '\t']);
        var first = space < 0 ? trimmed : trimmed[..space];
        var rest = space < 0 ? string.Empty : trimmed[(space + 1)..].Trim();
        if (rest.StartsWith("title=", StringComparison.OrdinalIgnoreCase) || rest.StartsWith("file=", StringComparison.OrdinalIgnoreCase))
        {
            rest = rest[(rest.IndexOf('=') + 1)..].Trim().Trim('"', '\'');
        }
        if (rest.Length == 0 && first.Contains('.') && !first.StartsWith('.') && !first.EndsWith('.'))
        {
            // A file name alone: the extension is the language.
            return (first[(first.LastIndexOf('.') + 1)..], first);
        }
        return (first, rest);
    }

    #endregion

    #region Inlines

    private enum TokenKind : byte
    {
        Text,
        Delimiter,
        Code,
        Citation
    }

    private sealed class Token
    {
        public TokenKind Kind;
        public string Text = string.Empty;
        public char DelimiterChar;
        public int Count;
        public int Original;
        public bool CanOpen;
        public bool CanClose;
        public int Number;
        // Styles this delimiter opens after its leftover characters, and closes before them.
        public int OpenStrong;
        public int OpenEmphasis;
        public int CloseStrong;
        public int CloseEmphasis;
        public bool IsUnclosedCode;
    }

    /// <summary>
    /// Parses the inline markup of one text: escapes, code spans, citations, link texts, bold and italic, line breaks.
    /// <paramref name="open"/> marks the end of a stream (unclosed markers are taken as open).
    /// </summary>
    public static AiAnswerInline[] ParseInlines(string text, bool open = false)
    {
        var tokens = Tokenize(text ?? string.Empty, open);
        ResolveEmphasis(tokens, open);
        return Emit(tokens);
    }

    private static List<Token> Tokenize(string text, bool open)
    {
        var tokens = new List<Token>();
        var buffer = new StringBuilder();
        var lastWasCitation = false;
        var lastClosedBracket = false;

        void Flush()
        {
            if (buffer.Length > 0)
            {
                tokens.Add(new Token { Kind = TokenKind.Text, Text = buffer.ToString() });
                buffer.Clear();
            }
        }

        var i = 0;
        while (i < text.Length)
        {
            var ch = text[i];
            if (ch == '\\' && i + 1 < text.Length)
            {
                var next = text[i + 1];
                if (next == '\n')
                {
                    buffer.Append('\n');
                    i += 2;
                    lastWasCitation = lastClosedBracket = false;
                    continue;
                }
                if (char.IsAsciiLetterOrDigit(next) || next is ' ' or '\t')
                {
                    buffer.Append(ch);
                    i++;
                    continue;
                }
                buffer.Append(next);
                i += 2;
                lastWasCitation = lastClosedBracket = false;
                continue;
            }
            if (ch == '\n')
            {
                // Two trailing spaces make a hard break; otherwise a line break inside a paragraph is a space.
                var hard = buffer.Length >= 2 && buffer[^1] == ' ' && buffer[^2] == ' ';
                while (buffer.Length > 0 && buffer[^1] == ' ')
                {
                    buffer.Length--;
                }
                buffer.Append(hard ? '\n' : ' ');
                i++;
                while (i < text.Length && text[i] == ' ')
                {
                    i++;
                }
                lastWasCitation = lastClosedBracket = false;
                continue;
            }
            if (ch == '`')
            {
                var run = 1;
                while (i + run < text.Length && text[i + run] == '`')
                {
                    run++;
                }
                var close = FindBacktickRun(text, i + run, run);
                if (close >= 0 || open)
                {
                    var end = close >= 0 ? close : text.Length;
                    var code = text[(i + run)..end].Replace('\n', ' ');
                    if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim().Length > 0)
                    {
                        code = code[1..^1];
                    }
                    Flush();
                    tokens.Add(new Token { Kind = TokenKind.Code, Text = code, IsUnclosedCode = close < 0 });
                    i = close >= 0 ? close + run : text.Length;
                    lastWasCitation = lastClosedBracket = false;
                    continue;
                }
                buffer.Append('`', run);
                i += run;
                lastWasCitation = lastClosedBracket = false;
                continue;
            }
            if (ch == '[')
            {
                if (TryCitation(text, i, lastClosedBracket && !lastWasCitation, out var number, out var length))
                {
                    Flush();
                    tokens.Add(new Token { Kind = TokenKind.Citation, Number = number });
                    i += length;
                    lastWasCitation = true;
                    lastClosedBracket = true;
                    continue;
                }
                if (TryLink(text, i, out var linkText, out var linkLength, out var isReference))
                {
                    // The text of a link (its target is not shown in an answer); an image shows its alt text.
                    if (i > 0 && text[i - 1] == '!' && buffer.Length > 0 && buffer[^1] == '!')
                    {
                        buffer.Length--;
                    }
                    Flush();
                    foreach (var token in Tokenize(linkText, open: false))
                    {
                        tokens.Add(token);
                    }
                    i += linkLength;
                    lastWasCitation = false;
                    lastClosedBracket = isReference;
                    continue;
                }
            }
            if (ch is '*' or '_')
            {
                var run = 1;
                while (i + run < text.Length && text[i + run] == ch)
                {
                    run++;
                }
                var before = i > 0 ? text[i - 1] : ' ';
                var after = i + run < text.Length ? text[i + run] : ' ';
                var leftFlanking = !char.IsWhiteSpace(after) && (!char.IsPunctuation(after) || char.IsWhiteSpace(before) || char.IsPunctuation(before));
                var rightFlanking = !char.IsWhiteSpace(before) && (!char.IsPunctuation(before) || char.IsWhiteSpace(after) || char.IsPunctuation(after));
                if (i + run >= text.Length && open)
                {
                    // At the end of a stream the next word is still coming: a run there only closes.
                    leftFlanking = false;
                }
                bool canOpen;
                bool canClose;
                if (ch == '*')
                {
                    canOpen = leftFlanking;
                    canClose = rightFlanking;
                }
                else
                {
                    canOpen = leftFlanking && (!rightFlanking || char.IsPunctuation(before));
                    canClose = rightFlanking && (!leftFlanking || char.IsPunctuation(after));
                }
                Flush();
                tokens.Add(new Token { Kind = TokenKind.Delimiter, DelimiterChar = ch, Count = run, Original = run, CanOpen = canOpen, CanClose = canClose });
                i += run;
                lastWasCitation = lastClosedBracket = false;
                continue;
            }
            if (ch == '&' && TryEntity(text, i, out var decoded, out var entityLength))
            {
                buffer.Append(decoded);
                i += entityLength;
                lastWasCitation = lastClosedBracket = false;
                continue;
            }
            buffer.Append(ch);
            i++;
            lastWasCitation = false;
            lastClosedBracket = ch == ']';
        }
        Flush();
        return tokens;
    }

    private static int FindBacktickRun(string text, int from, int length)
    {
        var i = from;
        while (i < text.Length)
        {
            if (text[i] != '`')
            {
                i++;
                continue;
            }
            var run = 1;
            while (i + run < text.Length && text[i + run] == '`')
            {
                run++;
            }
            if (run == length)
            {
                return i;
            }
            i += run;
        }
        return -1;
    }

    /// <summary><c>[n]</c> at <paramref name="i"/>: 1-3 digits, no leading zero, not a link, a definition or a reference label.</summary>
    private static bool TryCitation(string text, int i, bool afterLinkText, out int number, out int length)
    {
        number = 0;
        length = 0;
        if (afterLinkText)
        {
            return false;
        }
        var j = i + 1;
        while (j < text.Length && char.IsAsciiDigit(text[j]) && j - i - 1 < AiCitationText.MaxDigits + 1)
        {
            j++;
        }
        var digits = j - i - 1;
        if (digits is < 1 or > AiCitationText.MaxDigits || text[i + 1] == '0' || j >= text.Length || text[j] != ']')
        {
            return false;
        }
        var after = j + 1 < text.Length ? text[j + 1] : '\0';
        if (after == '(' || (after == ':' && IsLineStart(text, i)))
        {
            return false;
        }
        number = int.Parse(text.AsSpan(i + 1, digits), NumberStyles.None, CultureInfo.InvariantCulture);
        length = digits + 2;
        return true;
    }

    private static bool IsLineStart(string text, int i)
    {
        for (var k = i - 1; k >= 0; k--)
        {
            if (text[k] == '\n')
            {
                return true;
            }
            if (text[k] != ' ')
            {
                return false;
            }
        }
        return true;
    }

    /// <summary><c>[text](target)</c> or <c>[text][label]</c> at <paramref name="i"/>.</summary>
    private static bool TryLink(string text, int i, out string linkText, out int length, out bool isReference)
    {
        linkText = string.Empty;
        length = 0;
        isReference = false;
        var depth = 0;
        var j = i;
        for (; j < text.Length; j++)
        {
            if (text[j] == '\\')
            {
                j++;
                continue;
            }
            if (text[j] == '[')
            {
                depth++;
            }
            else if (text[j] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }
            }
        }
        if (j >= text.Length || j + 1 >= text.Length)
        {
            return false;
        }
        if (text[j + 1] == '(')
        {
            var close = text.IndexOf(')', j + 2);
            if (close < 0)
            {
                return false;
            }
            linkText = text[(i + 1)..j];
            length = close + 1 - i;
            return true;
        }
        if (text[j + 1] == '[')
        {
            var close = text.IndexOf(']', j + 2);
            if (close < 0)
            {
                return false;
            }
            linkText = text[(i + 1)..j];
            length = close + 1 - i;
            isReference = true;
            return true;
        }
        return false;
    }

    private static bool TryEntity(string text, int i, out string decoded, out int length)
    {
        ReadOnlySpan<string> names = ["&amp;", "&lt;", "&gt;", "&quot;", "&#39;", "&apos;", "&nbsp;"];
        ReadOnlySpan<string> values = ["&", "<", ">", "\"", "'", "'", " "];
        for (var k = 0; k < names.Length; k++)
        {
            if (string.CompareOrdinal(text, i, names[k], 0, names[k].Length) == 0)
            {
                decoded = values[k];
                length = names[k].Length;
                return true;
            }
        }
        decoded = string.Empty;
        length = 0;
        return false;
    }

    /// <summary>Pairs delimiter runs (a simplified CommonMark: nearest opener of the same character; two and two make strong).</summary>
    private static void ResolveEmphasis(List<Token> tokens, bool open)
    {
        var openers = new List<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind != TokenKind.Delimiter)
            {
                continue;
            }
            if (token.CanClose)
            {
                for (var o = openers.Count - 1; o >= 0 && token.Count > 0; o--)
                {
                    var opener = tokens[openers[o]];
                    if (opener.DelimiterChar != token.DelimiterChar || opener.Count == 0)
                    {
                        continue;
                    }
                    while (opener.Count > 0 && token.Count > 0)
                    {
                        var strong = opener.Count >= 2 && token.Count >= 2;
                        var used = strong ? 2 : 1;
                        opener.Count -= used;
                        token.Count -= used;
                        if (strong)
                        {
                            opener.OpenStrong++;
                            token.CloseStrong++;
                        }
                        else
                        {
                            opener.OpenEmphasis++;
                            token.CloseEmphasis++;
                        }
                    }
                    // Openers between this pair can no longer close anything after it.
                    openers.RemoveRange(o + 1, openers.Count - o - 1);
                    if (opener.Count == 0)
                    {
                        openers.RemoveAt(o);
                    }
                }
            }
            if (token.CanOpen && token.Count > 0)
            {
                openers.Add(i);
            }
        }
        if (!open)
        {
            return;
        }
        // The end of a stream: what is still open stays open, so the coming closer does not change what is shown.
        foreach (var index in openers)
        {
            var opener = tokens[index];
            while (opener.Count > 0)
            {
                var strong = opener.Count >= 2;
                opener.Count -= strong ? 2 : 1;
                if (strong)
                {
                    opener.OpenStrong++;
                }
                else
                {
                    opener.OpenEmphasis++;
                }
            }
        }
    }

    private static AiAnswerInline[] Emit(List<Token> tokens)
    {
        var result = new List<AiAnswerInline>();
        var text = new StringBuilder();
        var style = AiAnswerInlineStyle.None;
        var strong = 0;
        var emphasis = 0;

        void FlushText()
        {
            if (text.Length > 0)
            {
                result.Add(AiAnswerInline.Plain(text.ToString(), style));
                text.Clear();
            }
        }

        void SetStyle()
        {
            var next = (strong > 0 ? AiAnswerInlineStyle.Bold : 0) | (emphasis > 0 ? AiAnswerInlineStyle.Italic : 0);
            if (next != style)
            {
                FlushText();
                style = next;
            }
        }

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case TokenKind.Text:
                    text.Append(token.Text);
                    break;
                case TokenKind.Delimiter:
                    if (token.CloseStrong > 0 || token.CloseEmphasis > 0)
                    {
                        strong -= token.CloseStrong;
                        emphasis -= token.CloseEmphasis;
                        SetStyle();
                    }
                    if (token.Count > 0)
                    {
                        text.Append(token.DelimiterChar, token.Count);
                    }
                    if (token.OpenStrong > 0 || token.OpenEmphasis > 0)
                    {
                        strong += token.OpenStrong;
                        emphasis += token.OpenEmphasis;
                        SetStyle();
                    }
                    break;
                case TokenKind.Code:
                    FlushText();
                    result.Add(AiAnswerInline.CodeSpan(token.Text));
                    break;
                case TokenKind.Citation:
                    FlushText();
                    AddCitation(result, token.Number);
                    break;
            }
        }
        FlushText();
        TrimEnds(result);
        return [.. result];
    }

    /// <summary>
    /// Adds a citation after a no-break space so its pill never starts a line alone: a space before it becomes the
    /// no-break space, text right before it gets one, and a citation right after another gets none (the pills sit 2px apart).
    /// </summary>
    private static void AddCitation(List<AiAnswerInline> result, int number)
    {
        if (result.Count > 0 && result[^1] is { Kind: AiAnswerInlineKind.Text } last)
        {
            var body = last.Text;
            var trimmed = body.TrimEnd(' ');
            string replaced;
            if (trimmed.Length == 0 && result.Count >= 2 && result[^2].Kind == AiAnswerInlineKind.Citation)
            {
                replaced = NoBreakSpace.ToString();
            }
            else if (trimmed.EndsWith('\n'))
            {
                replaced = trimmed;
            }
            else
            {
                replaced = trimmed + NoBreakSpace;
            }
            if (replaced.Length == 0)
            {
                result.RemoveAt(result.Count - 1);
            }
            else
            {
                result[^1] = last with { Text = replaced };
            }
        }
        else if (result.Count > 0 && result[^1].Kind == AiAnswerInlineKind.Code)
        {
            result.Add(AiAnswerInline.Plain(NoBreakSpace.ToString()));
        }
        result.Add(AiAnswerInline.Cite(number));
    }

    /// <summary>Removes leading and trailing white space of the whole text (a block's own margins do the spacing).</summary>
    private static void TrimEnds(List<AiAnswerInline> result)
    {
        while (result.Count > 0 && result[0].Kind == AiAnswerInlineKind.Text)
        {
            var trimmed = result[0].Text.TrimStart(' ', '\n');
            if (trimmed.Length > 0)
            {
                result[0] = result[0] with { Text = trimmed };
                break;
            }
            result.RemoveAt(0);
        }
        while (result.Count > 0 && result[^1].Kind == AiAnswerInlineKind.Text)
        {
            var trimmed = result[^1].Text.TrimEnd(' ', '\n');
            if (trimmed.Length > 0)
            {
                result[^1] = result[^1] with { Text = trimmed };
                break;
            }
            result.RemoveAt(result.Count - 1);
        }
    }

    /// <summary>The inlines as plain text: code as is, citations as <c>[n]</c>, no-break spaces as spaces.</summary>
    public static string ToPlainText(IReadOnlyList<AiAnswerInline> inlines)
    {
        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline.Kind)
            {
                case AiAnswerInlineKind.Citation:
                    builder.Append('[').Append(inline.Number.ToString(CultureInfo.InvariantCulture)).Append(']');
                    break;
                default:
                    builder.Append(inline.Text.Replace(NoBreakSpace, ' '));
                    break;
            }
        }
        return builder.ToString();
    }

    #endregion
}
