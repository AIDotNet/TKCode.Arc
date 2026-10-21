using System.Globalization;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>What the rich text editor would do to a construct when it imports Markdown.</summary>
public enum RichTextImportIssueKind
{
    /// <summary>A heading deeper than three levels; the editor stores it as Heading 3.</summary>
    HeadingLevelDowngraded,

    /// <summary>The language or other info string of a fenced code block, which the editor does not store.</summary>
    CodeFenceInfoDropped,

    /// <summary>A tilde fence (<c>~~~</c>); the editor reads only backtick fences.</summary>
    TildeFence,

    /// <summary>A table; the editor has no table block, so the cells become one paragraph.</summary>
    Table,

    /// <summary>A setext heading underlined with <c>===</c> or <c>---</c>; the editor reads only ATX headings.</summary>
    SetextHeading,

    /// <summary>An indented code block; the editor reads only fenced code.</summary>
    IndentedCode,

    /// <summary>A list that starts at a number other than 1; the editor numbers lists from 1.</summary>
    ListStartNumber,

    /// <summary>A hard line break (two trailing spaces); the editor joins the lines with a space.</summary>
    HardLineBreak,

    /// <summary>Block level HTML; the editor keeps it as text and joins the lines into one paragraph.</summary>
    HtmlBlock,

    /// <summary>An image; the editor has no image block, so the source is kept as text.</summary>
    Image,

    /// <summary>A reference link or its definition; the editor reads only inline links, so it is kept as text.</summary>
    ReferenceLink,

    /// <summary>A footnote reference or definition, which the editor keeps as text.</summary>
    Footnote,

    /// <summary>A backslash escape, which the editor does not keep.</summary>
    Escape,

    /// <summary>A bullet written with <c>+</c> or <c>*</c>; the editor writes bulleted items with <c>-</c>.</summary>
    BulletMarker,

    /// <summary>A numbered marker written with <c>)</c>; the editor writes numbered items with <c>1.</c>.</summary>
    NumberedMarker,

    /// <summary>Bold written with <c>__</c>; the editor writes bold with <c>**</c>.</summary>
    StrongMarker,

    /// <summary>Italic written with <c>*</c>; the editor writes italic with <c>_</c>.</summary>
    ItalicMarker,

    /// <summary>A divider written with <c>*</c> or <c>_</c>; the editor writes dividers as <c>---</c>.</summary>
    ThematicBreakMarker
}

/// <summary>Whether an issue loses source or only rewrites it.</summary>
public enum RichTextImportSeverity
{
    /// <summary>The editor cannot store the construct: saving the editor output loses or changes it.</summary>
    Lossy,

    /// <summary>The editor stores the construct and writes it back in its canonical form.</summary>
    Normalized
}

/// <summary>One construct of a Markdown source that the editor does not import as written.</summary>
public sealed class RichTextImportIssue
{
    internal RichTextImportIssue(RichTextImportIssueKind kind, RichTextImportSeverity severity, int line, int column, int length,
                                 string source, string reason)
    {
        Kind = kind;
        Severity = severity;
        Line = line;
        Column = column;
        Length = length;
        Source = source;
        Reason = reason;
    }

    /// <summary>What the construct is.</summary>
    public RichTextImportIssueKind Kind { get; }

    /// <summary>Whether the construct is lost or only rewritten.</summary>
    public RichTextImportSeverity Severity { get; }

    /// <summary>The 1-based line of the source.</summary>
    public int Line { get; }

    /// <summary>The 1-based column of the source.</summary>
    public int Column { get; }

    /// <summary>The number of characters the issue covers, or 0 when it covers the whole line.</summary>
    public int Length { get; }

    /// <summary>The offending source line, trimmed and shortened to 120 characters.</summary>
    public string Source { get; }

    /// <summary>Why the editor changes or drops the construct, in one English sentence.</summary>
    public string Reason { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Line}:{Column} {Kind}: {Reason}";
}

/// <summary>
/// The result of <see cref="RichTextEditorMarkdown.Analyze"/>: every construct of a Markdown source that the rich text
/// editor cannot store (<see cref="RichTextImportSeverity.Lossy"/>) or writes back in another form
/// (<see cref="RichTextImportSeverity.Normalized"/>), in source order.
/// </summary>
public sealed class RichTextImportReport
{
    private static readonly RichTextImportIssue[] s_none = Array.Empty<RichTextImportIssue>();

    /// <summary>A report with no issues: the source imports as written.</summary>
    public static RichTextImportReport Empty { get; } = new(s_none);

    internal RichTextImportReport(IReadOnlyList<RichTextImportIssue> issues)
    {
        Issues = issues;
        for (var i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == RichTextImportSeverity.Lossy)
            {
                HasLosses = true;
                break;
            }
        }
    }

    /// <summary>The issues in source order, both losses and normalizations.</summary>
    public IReadOnlyList<RichTextImportIssue> Issues { get; }

    /// <summary>True when the editor would lose or change something; <see cref="RichTextEditor.TrySetMarkdown"/> refuses such a source.</summary>
    public bool HasLosses { get; }

    /// <summary>True when nothing is lost; the source may still carry <see cref="RichTextImportSeverity.Normalized"/> rewrites.</summary>
    public bool IsLossless => !HasLosses;

    /// <summary>True when there is no issue at all: the editor writes the source back as it came in.</summary>
    public bool IsEmpty => Issues.Count == 0;

    /// <inheritdoc />
    public override string ToString() => IsEmpty ? "Lossless import" : $"{Issues.Count} issue(s), losses: {HasLosses}";
}

public static partial class RichTextEditorMarkdown
{
    /// <summary>
    /// Reads a Markdown source the way <see cref="ToHtml"/> would and reports every construct the editor cannot store,
    /// plus the constructs it writes back in another form, with the line, the column and a reason. Nothing is converted
    /// and no editor is involved, so a host can warn, keep the original file or open a source editor instead.
    /// </summary>
    /// <remarks>
    /// The analysis covers the block and inline rules of <see cref="ToHtml"/>: headings deeper than h3, a fence's info
    /// string, tilde fences, tables, setext headings, indented code, a list's start number, hard line breaks, block
    /// HTML, images, reference links, footnotes and backslash escapes are losses; bullet, numbered, bold, italic and
    /// divider markers written in another form are normalizations. Everything else in the supported subset (paragraphs,
    /// three heading levels, nested lists, quotes, fenced code, dividers, bold, italic, strikethrough, inline code and
    /// inline links) produces no issue.
    /// </remarks>
    public static RichTextImportReport Analyze(string? markdown)
    {
        var source = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        if (source.Length == 0)
        {
            return RichTextImportReport.Empty;
        }
        var lines = source.Split('\n');
        var issues = new List<RichTextImportIssue>();
        for (var index = 0; index < lines.Length;)
        {
            index = ScanBlock(lines, index, issues);
        }
        return issues.Count == 0 ? RichTextImportReport.Empty : new RichTextImportReport(issues);
    }

    /// <summary>Scans one block starting at <paramref name="index"/>; returns the index of the next block.</summary>
    private static int ScanBlock(string[] lines, int index, List<RichTextImportIssue> issues)
    {
        var line = lines[index];
        if (line.Trim().Length == 0)
        {
            return index + 1;
        }
        if (FenceRegex().IsMatch(line))
        {
            var info = FenceInfo(line);
            if (info.Length > 0)
            {
                Add(issues, RichTextImportIssueKind.CodeFenceInfoDropped, index, line.IndexOf(info, StringComparison.Ordinal) + 1,
                    info.Length, line);
            }
            var end = index + 1;
            while (end < lines.Length && !FenceRegex().IsMatch(lines[end]))
            {
                end++;
            }
            return end + 1;
        }
        if (TildeFenceRegex().IsMatch(line))
        {
            Add(issues, RichTextImportIssueKind.TildeFence, index, 1, line.Trim().Length, line);
            var end = index + 1;
            while (end < lines.Length && !TildeFenceRegex().IsMatch(lines[end]))
            {
                end++;
            }
            return end + 1;
        }
        if (index + 1 < lines.Length && IsTableRow(line) && IsTableDelimiter(lines[index + 1]))
        {
            Add(issues, RichTextImportIssueKind.Table, index, 1, line.Length, line);
            var end = index + 2;
            while (end < lines.Length && IsTableRow(lines[end]))
            {
                end++;
            }
            return end;
        }
        if (IsSetextUnderline(line) && index > 0 && IsParagraphLine(lines[index - 1]))
        {
            // The previous line is the heading text; the editor turns it into a paragraph and this line into a divider.
            Add(issues, RichTextImportIssueKind.SetextHeading, index - 1, 1, lines[index - 1].Length, lines[index - 1]);
            return index + 1;
        }
        if (IsHtmlBlockStart(line))
        {
            Add(issues, RichTextImportIssueKind.HtmlBlock, index, 1, line.Length, line);
            var end = index + 1;
            while (end < lines.Length && lines[end].Trim().Length > 0)
            {
                end++;
            }
            return end;
        }
        if (IsIndentedCodeStart(lines, index))
        {
            Add(issues, RichTextImportIssueKind.IndentedCode, index, 1, line.Length, line);
            var end = index + 1;
            while (end < lines.Length && (lines[end].Trim().Length == 0 || IsIndented(lines[end])))
            {
                end++;
            }
            return end;
        }
        var heading = HeadingRegex().Match(line);
        if (heading.Success)
        {
            var level = heading.Groups[1].Length;
            if (level > 3)
            {
                Add(issues, RichTextImportIssueKind.HeadingLevelDowngraded, index, 1, level, line);
            }
            ScanInline(heading.Groups[2].Value, index, issues, heading.Groups[2].Index);
            return index + 1;
        }
        if (RuleRegex().IsMatch(line))
        {
            var marker = line.TrimStart()[0];
            if (marker != '-')
            {
                Add(issues, RichTextImportIssueKind.ThematicBreakMarker, index, line.Length - line.TrimStart().Length + 1, 1, line);
            }
            return index + 1;
        }
        if (QuoteRegex().IsMatch(line))
        {
            var end = index;
            while (end < lines.Length && QuoteRegex().IsMatch(lines[end]))
            {
                ScanInline(lines[end], end, issues, QuotePrefixRegex().Match(lines[end]).Length);
                end++;
            }
            return end;
        }
        if (ItemRegex().IsMatch(line))
        {
            var start = index;
            var end = index;
            while (end < lines.Length && ItemRegex().IsMatch(lines[end]))
            {
                var parts = ItemPartsRegex().Match(lines[end]);
                var marker = parts.Groups[2].Value;
                var column = parts.Groups[2].Index + 1;
                var numbered = char.IsAsciiDigit(marker[0]);
                if (end == start && numbered && !IsOne(marker.AsSpan(0, marker.Length - 1)))
                {
                    Add(issues, RichTextImportIssueKind.ListStartNumber, end, column, marker.Length, lines[end]);
                }
                if (numbered && marker[^1] == ')')
                {
                    Add(issues, RichTextImportIssueKind.NumberedMarker, end, column, marker.Length, lines[end]);
                }
                else if (!numbered && marker[0] != '-')
                {
                    Add(issues, RichTextImportIssueKind.BulletMarker, end, column, marker.Length, lines[end]);
                }
                ScanInline(lines[end], end, issues, parts.Groups[3].Index);
                end++;
            }
            return end;
        }
        ScanInline(line, index, issues, 0);
        if (line.Length >= 2 && line[^1] == ' ' && line[^2] == ' ' && index + 1 < lines.Length && IsParagraphLine(lines[index + 1]))
        {
            Add(issues, RichTextImportIssueKind.HardLineBreak, index, line.TrimEnd().Length + 1, line.Length - line.TrimEnd().Length, line);
        }
        return index + 1;
    }

    /// <summary>Reports the inline constructs of one line; <paramref name="offset"/> is the column of <paramref name="text"/> in the source.</summary>
    private static void ScanInline(string text, int line, List<RichTextImportIssue> issues, int offset)
    {
        if (text.Length == 0)
        {
            return;
        }
        var masked = MaskInlineCode(text);
        foreach (Match match in ImageRegex().Matches(masked))
        {
            Add(issues, RichTextImportIssueKind.Image, line, offset + match.Index + 1, match.Length, text);
        }
        foreach (Match match in FootnoteRegex().Matches(masked))
        {
            Add(issues, RichTextImportIssueKind.Footnote, line, offset + match.Index + 1, match.Length, text);
        }
        foreach (Match match in ReferenceLinkRegex().Matches(masked))
        {
            Add(issues, RichTextImportIssueKind.ReferenceLink, line, offset + match.Index + 1, match.Length, text);
        }
        foreach (Match match in EscapeRegex().Matches(masked))
        {
            Add(issues, RichTextImportIssueKind.Escape, line, offset + match.Index + 1, match.Length, text);
        }
        // An escaped marker is literal text: it is not bold, italic or a divider, so the escape is the only issue.
        var unescaped = EscapeRegex().Replace(masked, match => new string(' ', match.Length));
        foreach (Match match in StrongRegex().Matches(unescaped))
        {
            if (match.Groups[1].Value[0] == '_')
            {
                Add(issues, RichTextImportIssueKind.StrongMarker, line, offset + match.Index + 1, match.Length, text);
            }
        }
        foreach (Match match in EmStarRegex().Matches(unescaped))
        {
            Add(issues, RichTextImportIssueKind.ItalicMarker, line, offset + match.Groups[0].Index + match.Groups[1].Length + 1,
                match.Groups[2].Length + 2, text);
        }
    }

    /// <summary>The info string after a backtick fence, or an empty string.</summary>
    private static string FenceInfo(string line)
    {
        var ticks = 0;
        while (ticks < line.Length && line[ticks] == '`')
        {
            ticks++;
        }
        return ticks >= 3 ? line[ticks..].Trim() : string.Empty;
    }

    /// <summary>Blanks out inline code spans so that their content is not read as Markdown.</summary>
    private static string MaskInlineCode(string text)
    {
        if (text.IndexOf('`') < 0)
        {
            return text;
        }
        var chars = text.ToCharArray();
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] != '`')
            {
                index++;
                continue;
            }
            var run = 1;
            while (index + run < text.Length && text[index + run] == '`')
            {
                run++;
            }
            var close = text.IndexOf(new string('`', run), index + run, StringComparison.Ordinal);
            if (close < 0)
            {
                index += run;
                continue;
            }
            for (var i = index; i < close + run; i++)
            {
                chars[i] = ' ';
            }
            index = close + run;
        }
        return new string(chars);
    }

    private static bool IsTableRow(string line) => line.Contains('|') && line.Trim().Length > 0;

    private static bool IsTableDelimiter(string line) => TableDelimiterRegex().IsMatch(line);

    private static bool IsSetextUnderline(string line) => SetextRegex().IsMatch(line);

    private static bool IsIndented(string line) => line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith('\t');

    /// <summary>An indented code block starts after a blank line and is not a list item, a quote or a rule.</summary>
    private static bool IsIndentedCodeStart(string[] lines, int index)
    {
        var line = lines[index];
        if (!IsIndented(line) || line.Trim().Length == 0)
        {
            return false;
        }
        if (index > 0 && lines[index - 1].Trim().Length > 0)
        {
            return false;
        }
        return !ItemRegex().IsMatch(line) && !QuoteRegex().IsMatch(line) && !RuleRegex().IsMatch(line);
    }

    /// <summary>A line that starts a block of HTML the editor would keep as text.</summary>
    private static bool IsHtmlBlockStart(string line)
    {
        var match = HtmlStartRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }
        var tag = match.Groups[1].Value;
        return tag.Length == 0 || s_htmlBlockTags.Contains(tag);
    }

    /// <summary>True when the line is a plain paragraph line, so a following underline makes a setext heading.</summary>
    private static bool IsParagraphLine(string line)
    {
        if (line.Trim().Length == 0 || IsIndented(line))
        {
            return false;
        }
        return !FenceRegex().IsMatch(line) && !TildeFenceRegex().IsMatch(line) && !HeadingRegex().IsMatch(line)
               && !RuleRegex().IsMatch(line) && !QuoteRegex().IsMatch(line) && !ItemRegex().IsMatch(line)
               && !IsHtmlBlockStart(line);
    }

    private static bool IsOne(ReadOnlySpan<char> digits)
        => int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number == 1;

    private static void Add(List<RichTextImportIssue> issues, RichTextImportIssueKind kind, int line, int column, int length, string source)
        => issues.Add(new RichTextImportIssue(kind, SeverityOf(kind), line + 1, Math.Max(1, column), Math.Max(0, length),
            Snippet(source), ReasonOf(kind)));

    private static RichTextImportSeverity SeverityOf(RichTextImportIssueKind kind) => kind switch
    {
        RichTextImportIssueKind.BulletMarker or RichTextImportIssueKind.NumberedMarker or RichTextImportIssueKind.StrongMarker
            or RichTextImportIssueKind.ItalicMarker or RichTextImportIssueKind.ThematicBreakMarker => RichTextImportSeverity.Normalized,
        _ => RichTextImportSeverity.Lossy
    };

    private static string ReasonOf(RichTextImportIssueKind kind) => kind switch
    {
        RichTextImportIssueKind.HeadingLevelDowngraded => "The editor has three heading levels; this heading becomes Heading 3.",
        RichTextImportIssueKind.CodeFenceInfoDropped => "The editor does not store a code block's language, so the info string is dropped.",
        RichTextImportIssueKind.TildeFence => "The editor reads only backtick fences; this block becomes a paragraph.",
        RichTextImportIssueKind.Table => "The editor has no table block; the cells become one paragraph.",
        RichTextImportIssueKind.SetextHeading => "The editor reads only ATX headings; this heading becomes a paragraph.",
        RichTextImportIssueKind.IndentedCode => "The editor reads only fenced code; this block becomes a paragraph.",
        RichTextImportIssueKind.ListStartNumber => "The editor numbers lists from 1.",
        RichTextImportIssueKind.HardLineBreak => "The editor joins the lines of a paragraph with a space.",
        RichTextImportIssueKind.HtmlBlock => "The editor keeps HTML as text and joins the lines into one paragraph.",
        RichTextImportIssueKind.Image => "The editor has no image block; the source is kept as text.",
        RichTextImportIssueKind.ReferenceLink => "The editor reads only inline links; this reference is kept as text.",
        RichTextImportIssueKind.Footnote => "The editor has no footnotes; the reference is kept as text.",
        RichTextImportIssueKind.Escape => "The editor does not keep backslash escapes.",
        RichTextImportIssueKind.BulletMarker => "The editor writes bulleted items with `-`.",
        RichTextImportIssueKind.NumberedMarker => "The editor writes numbered items with `1.`.",
        RichTextImportIssueKind.StrongMarker => "The editor writes bold with `**`.",
        RichTextImportIssueKind.ItalicMarker => "The editor writes italic with `_`.",
        RichTextImportIssueKind.ThematicBreakMarker => "The editor writes dividers as `---`.",
        _ => "The editor cannot store this construct."
    };

    private static string Snippet(string line)
    {
        var text = line.Trim();
        return text.Length <= 120 ? text : text[..117] + "…";
    }

    /// <summary>HTML tags the editor would keep as a paragraph of text instead of a block.</summary>
    private static readonly HashSet<string> s_htmlBlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "audio", "blockquote", "canvas", "dd", "details", "dialog", "div", "dl", "dt",
        "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hgroup",
        "hr", "iframe", "img", "li", "main", "nav", "noscript", "ol", "p", "pre", "script", "section", "style", "svg",
        "table", "tbody", "td", "template", "tfoot", "th", "thead", "tr", "ul", "video"
    };

    [GeneratedRegex(@"^ {0,3}(?:```|~~~)")]
    private static partial Regex TildeFenceRegex();

    [GeneratedRegex(@"^\s*\|?\s*:?-+:?\s*(?:\|\s*:?-+:?\s*)*\|?\s*$")]
    private static partial Regex TableDelimiterRegex();

    [GeneratedRegex(@"^ {0,3}(?:=+|-+) *$")]
    private static partial Regex SetextRegex();

    [GeneratedRegex(@"^ {0,3}<!--|^ {0,3}</?([A-Za-z][A-Za-z0-9-]*)\b")]
    private static partial Regex HtmlStartRegex();

    [GeneratedRegex(@"!\[[^\]\n]*\]\([^)\n]*\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[\^[^\]\n]+\]")]
    private static partial Regex FootnoteRegex();

    [GeneratedRegex(@"\[[^\]\n]+\]\[[^\]\n]*\]")]
    private static partial Regex ReferenceLinkRegex();

    [GeneratedRegex(@"\\[\\`*_{}\[\]()#+\-.!>~|]")]
    private static partial Regex EscapeRegex();
}
