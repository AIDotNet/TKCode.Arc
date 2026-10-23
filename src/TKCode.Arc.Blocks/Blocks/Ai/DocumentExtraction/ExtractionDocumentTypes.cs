using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How a document is drawn.</summary>
public enum DocumentKind
{
    /// <summary>Decided from the file name: ".eml" is an email, anything else is a paper page.</summary>
    Auto,

    /// <summary>A white A4 page with a page count.</summary>
    Pdf,

    /// <summary>An email: subject, sender, date and body, no page count.</summary>
    Email
}

/// <summary>A piece of the text of a document: plain text or a <see cref="DocumentSpan"/> that a value can cite.</summary>
public abstract class DocumentInline
{
    /// <summary>The text.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>Plain text.</summary>
public sealed class DocumentRun : DocumentInline
{
    /// <summary>Creates an empty run.</summary>
    public DocumentRun() { }

    /// <summary>Creates a run.</summary>
    public DocumentRun(string text) => Text = text;
}

/// <summary>A piece of text an extracted value can point back to: it is tinted once a value cites it and lit when the value is.</summary>
public sealed class DocumentSpan : DocumentInline
{
    /// <summary>Creates an empty span.</summary>
    public DocumentSpan() { }

    /// <summary>Creates a span.</summary>
    /// <param name="id">The id values cite (<see cref="ExtractionValueEvent.SourceId"/>), unique in the document.</param>
    /// <param name="text">The text.</param>
    public DocumentSpan(string id, string text)
    {
        Id = id;
        Text = text;
    }

    /// <summary>The id values cite.</summary>
    public string Id { get; set; } = string.Empty;
}

/// <summary>Text made of runs and spans; a string converts to one run. In XAML write text and <c>&lt;DocumentSpan Id="vendor" Text="..."/&gt;</c> inside it.</summary>
public sealed class DocumentText
{
    /// <summary>The pieces in order.</summary>
    [Content]
    public AvaloniaList<DocumentInline> Inlines { get; } = new();

    /// <summary>The text without the span boundaries.</summary>
    public string PlainText => string.Concat(Inlines.Select(i => i.Text));

    /// <summary>A text of one run.</summary>
    public static implicit operator DocumentText(string text)
    {
        var result = new DocumentText();
        result.Inlines.Add(new DocumentRun(text));
        return result;
    }
}

/// <summary>A label and a value (the rows of a "pairs" block and the cells of the meta strip).</summary>
public sealed class DocumentPair
{
    /// <summary>The label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The value.</summary>
    public DocumentText Value { get; set; } = string.Empty;
}

/// <summary>A block of a document. Blocks are drawn on the page in order.</summary>
public abstract class DocumentBlock
{
}

/// <summary>The paper letterhead: a small mark and the sender's name, with the page title under it.</summary>
public sealed class DocumentMasthead : DocumentBlock
{
    /// <summary>A geometry in a 24 × 24 box drawn in <see cref="BrandColor"/>.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>The sender's name, in the brand color.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A lighter word after the name.</summary>
    public string? Detail { get; set; }

    /// <summary>The page title ("Invoice").</summary>
    public string? Title { get; set; }

    /// <summary>The brand color of the mark and the name.</summary>
    public IBrush? BrandColor { get; set; }
}

/// <summary>The header of an email page: subject, sender, recipient and date.</summary>
public sealed class DocumentEmailHeader : DocumentBlock
{
    /// <summary>The subject.</summary>
    public DocumentText Subject { get; set; } = string.Empty;

    /// <summary>The sender's name.</summary>
    public DocumentText FromName { get; set; } = string.Empty;

    /// <summary>The sender's address.</summary>
    public DocumentText FromAddress { get; set; } = string.Empty;

    /// <summary>The recipient.</summary>
    public DocumentText To { get; set; } = string.Empty;

    /// <summary>The date.</summary>
    public DocumentText Date { get; set; } = string.Empty;

    /// <summary>The background of the sender's round avatar (its initials are the sender's first letters).</summary>
    public IBrush? AvatarColor { get; set; }
}

/// <summary>A small heading.</summary>
public sealed class DocumentHeading : DocumentBlock
{
    /// <summary>The text.</summary>
    public DocumentText Text { get; set; } = string.Empty;
}

/// <summary>A paragraph.</summary>
public sealed class DocumentParagraph : DocumentBlock
{
    /// <summary>The text.</summary>
    public DocumentText Text { get; set; } = string.Empty;

    /// <summary>Whether it is drawn in the quiet ink (a note under the rest).</summary>
    public bool IsMuted { get; set; }
}

/// <summary>A label with lines under it (an address block).</summary>
public sealed class DocumentLines : DocumentBlock
{
    /// <summary>The label ("From").</summary>
    public string? Label { get; set; }

    /// <summary>The lines.</summary>
    public AvaloniaList<DocumentText> Lines { get; } = new();
}

/// <summary>Rows of a label and a value.</summary>
public sealed class DocumentPairs : DocumentBlock
{
    /// <summary>The rows.</summary>
    public AvaloniaList<DocumentPair> Pairs { get; } = new();
}

/// <summary>A band of labelled cells between two thin rules (invoice number, dates and so on).</summary>
public sealed class DocumentMetaStrip : DocumentBlock
{
    /// <summary>The cells, four to a row.</summary>
    public AvaloniaList<DocumentPair> Cells { get; } = new();
}

/// <summary>Blocks side by side in equal columns.</summary>
public sealed class DocumentColumns : DocumentBlock
{
    /// <summary>One block per column.</summary>
    public AvaloniaList<DocumentBlock> Columns { get; } = new();
}

/// <summary>A column of a <see cref="DocumentTable"/>.</summary>
public sealed class DocumentTableColumn
{
    /// <summary>The header text.</summary>
    public string Header { get; set; } = string.Empty;

    /// <summary>Whether the column is right-aligned (numbers).</summary>
    public bool IsNumeric { get; set; }
}

/// <summary>A row of a <see cref="DocumentTable"/>.</summary>
public sealed class DocumentTableRow
{
    /// <summary>One text per column.</summary>
    public AvaloniaList<DocumentText> Cells { get; } = new();

    /// <summary>A small line under the first cell.</summary>
    public DocumentText? Detail { get; set; }
}

/// <summary>A table with a header row.</summary>
public sealed class DocumentTable : DocumentBlock
{
    /// <summary>The columns.</summary>
    public AvaloniaList<DocumentTableColumn> Columns { get; } = new();

    /// <summary>The rows.</summary>
    public AvaloniaList<DocumentTableRow> Rows { get; } = new();
}

/// <summary>A row of <see cref="DocumentTotals"/>.</summary>
public sealed class DocumentTotalsRow
{
    /// <summary>The label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The amount.</summary>
    public DocumentText Value { get; set; } = string.Empty;

    /// <summary>Whether it is the emphasised last row, drawn above a double line.</summary>
    public bool IsStrong { get; set; }
}

/// <summary>The totals under a table, right aligned.</summary>
public sealed class DocumentTotals : DocumentBlock
{
    /// <summary>The rows.</summary>
    public AvaloniaList<DocumentTotalsRow> Rows { get; } = new();
}

/// <summary>A thin horizontal rule.</summary>
public sealed class DocumentRule : DocumentBlock
{
}

/// <summary>The foot of a paper page: address lines on the left, the page number on the right.</summary>
public sealed class DocumentFooter : DocumentBlock
{
    /// <summary>The lines on the left.</summary>
    public AvaloniaList<string> Left { get; } = new();

    /// <summary>The text on the right.</summary>
    public string? Right { get; set; }
}

/// <summary>A document to read: a name and the blocks to draw.</summary>
public sealed partial class ExtractionDocument
{
    /// <summary>The id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The file name shown on the file button and in accessible names.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A short description for the document menu ("PDF, 1 page", "Email").</summary>
    public string? Label { get; set; }

    /// <summary>How the document is drawn.</summary>
    public DocumentKind Kind { get; set; }

    /// <summary>The kind after <see cref="DocumentKind.Auto"/> is resolved.</summary>
    public DocumentKind EffectiveKind => Kind != DocumentKind.Auto ? Kind
        : Name.EndsWith(".eml", StringComparison.OrdinalIgnoreCase) ? DocumentKind.Email : DocumentKind.Pdf;

    /// <summary>The blocks in order.</summary>
    [Content]
    public AvaloniaList<DocumentBlock> Blocks { get; } = new();
}
