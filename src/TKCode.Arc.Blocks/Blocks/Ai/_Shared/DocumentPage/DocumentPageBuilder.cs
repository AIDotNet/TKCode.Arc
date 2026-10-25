using TKCode.Arc.DesignTokens;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Builds the controls of a <see cref="DocumentPage"/> from its document, one block after another, with the paper
/// typography of spec 3.3 in base units. Blocks are spaced as CSS margins collapse: between two blocks the larger of the
/// first one's bottom margin and the second one's top margin. Every span of a text is registered with the page as a
/// character range of the text control it lands in.
/// </summary>
internal sealed class DocumentPageBuilder
{
    /// <summary>The space inside a Pdf page: 46 at the top, 48 at the sides, 40 at the bottom.</summary>
    internal static readonly Thickness PdfPadding = new(48, 46, 48, 40);

    /// <summary>The space around an email's header (the hairline under it runs the full width).</summary>
    internal static readonly Thickness EmailHeaderPadding = new(32, 26, 32, 18);

    /// <summary>The space around an email's body.</summary>
    internal static readonly Thickness EmailBodyPadding = new(40, 26, 40, 28);

    /// <summary>The footer of a Pdf page sits this far above the bottom edge, 48 in from the sides.</summary>
    internal static readonly Thickness FooterMargin = new(48, 0, 48, 30);

    private readonly DocumentPage _page;
    private readonly ExtractionDocument _document;
    private readonly bool _isEmail;

    public DocumentPageBuilder(DocumentPage page, ExtractionDocument document)
    {
        _page = page;
        _document = document;
        _isEmail = document.EffectiveKind == DocumentKind.Email;
    }

    /// <summary>The page's content: the flow of blocks, with a Pdf page's footers pinned to its bottom.</summary>
    public Control Build()
    {
        var blocks = _document.Blocks;
        if (_isEmail)
        {
            // The header runs edge to edge with its rule under it; the rest is the padded body.
            var root = new StackPanel();
            var first = 0;
            while (first < blocks.Count && blocks[first] is DocumentEmailHeader header)
            {
                root.Children.Add(EmailHeader(header));
                first++;
            }
            var body = Flow(blocks.Skip(first));
            body.Margin = EmailBodyPadding;
            root.Children.Add(body);
            return root;
        }
        var flow = Flow(blocks.Where(b => b is not DocumentFooter));
        flow.Margin = PdfPadding;
        var page = new Panel();
        page.Children.Add(flow);
        foreach (var footer in blocks.OfType<DocumentFooter>())
        {
            var control = Footer(footer);
            control.VerticalAlignment = VerticalAlignment.Bottom;
            control.Margin = FooterMargin;
            page.Children.Add(control);
        }
        return page;
    }

    #region Flow and spacing

    private StackPanel Flow(IEnumerable<DocumentBlock> blocks)
    {
        var panel = new StackPanel();
        var previousBottom = double.NaN;
        Control? last = null;
        foreach (var block in blocks)
        {
            var control = Block(block);
            var (top, bottom) = Spacing(block);
            var gap = double.IsNaN(previousBottom) ? top : Math.Max(previousBottom, top);
            control.Margin = new Thickness(0, gap, 0, 0);
            panel.Children.Add(control);
            previousBottom = bottom;
            last = control;
        }
        if (last is not null)
        {
            last.Margin = new Thickness(0, last.Margin.Top, 0, previousBottom);
        }
        return panel;
    }

    /// <summary>
    /// The top and bottom margins of a block in the page's flow (spec 3.3). The email body is set tighter than the paper
    /// (measured on the reference: 22 under the letterhead, 16 / 4 around the meta strip, 24 above a heading).
    /// </summary>
    private (double Top, double Bottom) Spacing(DocumentBlock block) => block switch
    {
        DocumentMasthead => (0, _isEmail ? 22 : 34),
        DocumentColumns => (0, 24),
        DocumentMetaStrip => (16, _isEmail ? 4 : 26),
        DocumentTotals => (10, 0),
        DocumentHeading => (_isEmail ? 24 : 30, 6),
        DocumentParagraph => (12, 0),
        DocumentRule => (18, 18),
        DocumentEmailHeader => (0, 24),
        DocumentFooter => (24, 0),
        _ => (0, 0)
    };

    private Control Block(DocumentBlock block) => block switch
    {
        DocumentMasthead masthead => Masthead(masthead),
        DocumentEmailHeader header => EmailHeader(header),
        DocumentHeading heading => Text(heading.Text, 10, 15, DocumentInk.InkBrush, FontWeight.Medium),
        DocumentParagraph paragraph => Text(paragraph.Text, 10, 15, paragraph.IsMuted ? DocumentInk.QuietBrush : DocumentInk.InkBrush),
        DocumentLines lines => Lines(lines),
        DocumentPairs pairs => Pairs(pairs),
        DocumentMetaStrip strip => MetaStrip(strip),
        DocumentColumns columns => Columns(columns),
        DocumentTable table => Table(table),
        DocumentTotals totals => Totals(totals),
        DocumentRule => new Border { Height = 1, Background = DocumentInk.RuleBrush },
        DocumentFooter footer => Footer(footer),
        _ => new Panel()
    };

    #endregion

    #region Blocks

    /// <summary>
    /// The letterhead: mark (21) + 6 + name (17 / 17 500 in the brand color, −0.045em) and a lighter word (400, quiet) on
    /// the left; the title (24 / 24 400, −0.035em) on the right; both centered on one line.
    /// </summary>
    private Control Masthead(DocumentMasthead masthead)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var brandBrush = masthead.BrandColor ?? DocumentInk.InkBrush;
        if (masthead.Mark is { } mark)
        {
            brand.Children.Add(new DocumentPageMark(mark, brandBrush, 21) { Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        }
        var name = NewText(17, double.NaN, brandBrush, FontWeight.Medium, wrap: false);
        name.LetterSpacing = -0.045 * 17;
        var runs = new InlineCollection { new Run(masthead.Name) };
        name.Inlines = runs;
        if (!string.IsNullOrEmpty(masthead.Detail))
        {
            runs.Add(new Run(" " + masthead.Detail) { FontWeight = FontWeight.Normal, Foreground = DocumentInk.QuietBrush });
        }
        FitLine(name, 17);
        brand.Children.Add(name);
        grid.Children.Add(brand);
        if (!string.IsNullOrEmpty(masthead.Title))
        {
            var title = NewText(24, double.NaN, DocumentInk.InkBrush, FontWeight.Normal, wrap: false);
            title.LetterSpacing = -0.035 * 24;
            title.Text = masthead.Title;
            FitLine(title, 24);
            Grid.SetColumn(title, 1);
            grid.Children.Add(title);
        }
        return grid;
    }

    /// <summary>
    /// An email's header: subject (15 / 19.5 500, −0.02em) 16 over the sender row (avatar 30 · name and address over the
    /// recipient · date 9 / 13.5), padding 26 32 18, a thin rule under it.
    /// </summary>
    private Control EmailHeader(DocumentEmailHeader header)
    {
        var stack = new StackPanel();
        var subject = Text(header.Subject, 15, 19.5, DocumentInk.InkBrush, FontWeight.Medium);
        subject.LetterSpacing = -0.02 * 15;
        subject.Margin = new Thickness(0, 0, 0, 16);
        stack.Children.Add(subject);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("30,*,Auto"), ColumnSpacing = 10 };
        row.Children.Add(Avatar(header));
        var sender = new StackPanel();
        sender.Children.Add(Pieces(10, 15, (header.FromName, DocumentInk.InkBrush), (header.FromAddress, DocumentInk.QuietBrush)));
        sender.Children.Add(Text(header.To, 10, 15, DocumentInk.QuietBrush));
        Grid.SetColumn(sender, 1);
        row.Children.Add(sender);
        var date = Text(header.Date, 9, 13.5, DocumentInk.QuietBrush, alignment: TextAlignment.Right, wrap: false);
        Grid.SetColumn(date, 2);
        row.Children.Add(date);
        stack.Children.Add(row);

        return new Border
        {
            Padding = EmailHeaderPadding,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = DocumentInk.RuleBrush,
            Child = stack
        };
    }

    /// <summary>The sender's avatar: a 30 circle in the avatar color with the sender's initials (10.5 500, white).</summary>
    private static Control Avatar(DocumentEmailHeader header)
    {
        var initials = new TextBlock
        {
            Text = Initials(header.FromName.PlainText),
            FontFamily = DocumentInk.Font,
            FontSize = 10.5,
            FontWeight = FontWeight.Medium,
            Foreground = DocumentInk.PaperBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return new Border
        {
            Width = 30,
            Height = 30,
            Background = header.AvatarColor ?? DocumentInk.InkBrush,
            VerticalAlignment = VerticalAlignment.Top,
            Child = initials
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep15);
    }

    /// <summary>The first letters of the first two words, upper case ("Fernhill Books" → "FB").</summary>
    internal static string Initials(string name)
    {
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder(2);
        foreach (var word in words.Take(2))
        {
            builder.Append(char.ToUpperInvariant(word[0]));
        }
        return builder.ToString();
    }

    /// <summary>A label (8.5 / 13.2 500, quiet, 3 above the lines) over lines of 10 / 15.5.</summary>
    private Control Lines(DocumentLines lines)
    {
        var stack = new StackPanel();
        if (!string.IsNullOrEmpty(lines.Label))
        {
            var label = Text(lines.Label, 8.5, 13.2, DocumentInk.QuietBrush, FontWeight.Medium);
            label.Margin = new Thickness(0, 0, 0, 3);
            stack.Children.Add(label);
        }
        foreach (var line in lines.Lines)
        {
            stack.Children.Add(Text(line, 10, 15.5, DocumentInk.InkBrush));
        }
        return stack;
    }

    /// <summary>Rows of a quiet label (92 wide) and a value, 8 apart, rows 2 apart; 10 / 15.</summary>
    private Control Pairs(DocumentPairs pairs)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("92,*"), ColumnSpacing = 8, RowSpacing = 2 };
        for (var i = 0; i < pairs.Pairs.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = Text(pairs.Pairs[i].Label, 10, 15, DocumentInk.QuietBrush);
            Grid.SetRow(label, i);
            grid.Children.Add(label);
            var value = Text(pairs.Pairs[i].Value, 10, 15, DocumentInk.InkBrush);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
        }
        return grid;
    }

    /// <summary>
    /// The meta strip: thin rules over and under it, padding 8 0 9; up to four equal cells a row, 12 apart (102 wide on a
    /// Pdf page); each a quiet label (8.5 / 12.75) over its value (10 / 15, 1 below).
    /// </summary>
    private Control MetaStrip(DocumentMetaStrip strip)
    {
        var columns = Math.Clamp(strip.Cells.Count, 1, 4);
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        }
        for (var i = 0; i < strip.Cells.Count; i++)
        {
            if (i % columns == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
            var cell = new StackPanel();
            cell.Children.Add(Text(strip.Cells[i].Label, 8.5, 12.75, DocumentInk.QuietBrush));
            var value = Text(strip.Cells[i].Value, 10, 15, DocumentInk.InkBrush);
            value.Margin = new Thickness(0, 1, 0, 0);
            cell.Children.Add(value);
            Grid.SetRow(cell, i / columns);
            Grid.SetColumn(cell, i % columns);
            grid.Children.Add(cell);
        }
        return new Border
        {
            BorderThickness = new Thickness(0, 1),
            BorderBrush = DocumentInk.RuleBrush,
            Padding = new Thickness(0, 8, 0, 9),
            Child = grid
        };
    }

    /// <summary>Blocks side by side in equal columns 24 apart (two of 210 on a Pdf page).</summary>
    private Control Columns(DocumentColumns columns)
    {
        var grid = new Grid { ColumnSpacing = 24 };
        for (var i = 0; i < columns.Columns.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var child = Block(columns.Columns[i]);
            Grid.SetColumn(child, i);
            grid.Children.Add(child);
        }
        return grid;
    }

    /// <summary>
    /// A table. Head: 8.5 / 12.75 500 quiet, 6 over a strong rule. Rows: 10 / 15, padding 7 0 8, a thin rule under each;
    /// a detail line (8.5 quiet) under the first cell. Numeric columns are right aligned with 12 on their left. The text
    /// columns share half the width and the numeric ones the other half (Arc's invoice: 222 + 3 × 74); a table of one kind
    /// shares it equally.
    /// </summary>
    private Control Table(DocumentTable table)
    {
        var count = table.Columns.Count;
        foreach (var row in table.Rows)
        {
            count = Math.Max(count, row.Cells.Count);
        }
        var numeric = new bool[count];
        var numericCount = 0;
        for (var c = 0; c < count; c++)
        {
            numeric[c] = c < table.Columns.Count && table.Columns[c].IsNumeric;
            numericCount += numeric[c] ? 1 : 0;
        }
        var textCount = count - numericCount;
        var grid = new Grid();
        for (var c = 0; c < count; c++)
        {
            var weight = numericCount == 0 || textCount == 0 ? 1 : numeric[c] ? textCount : numericCount;
            grid.ColumnDefinitions.Add(new ColumnDefinition(weight, GridUnitType.Star));
        }

        // Head.
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddRule(grid, 0, count, DocumentInk.StrongRuleBrush);
        for (var c = 0; c < table.Columns.Count; c++)
        {
            var head = Text(table.Columns[c].Header, 8.5, 12.75, DocumentInk.QuietBrush, FontWeight.Medium,
                numeric[c] ? TextAlignment.Right : TextAlignment.Left, wrap: !numeric[c]);
            head.Margin = new Thickness(numeric[c] ? 12 : 0, 0, 0, 6 + 1);
            Grid.SetColumn(head, c);
            grid.Children.Add(head);
        }

        // Rows.
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var gridRow = r + 1;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddRule(grid, gridRow, count, DocumentInk.RuleBrush);
            for (var c = 0; c < row.Cells.Count; c++)
            {
                // Numbers wrap too ("2 pallets" in a narrow column): a fixed column cannot grow as a web table's does.
                var text = Text(row.Cells[c], 10, 15, DocumentInk.InkBrush, alignment: numeric[c] ? TextAlignment.Right : TextAlignment.Left);
                Control cell = text;
                if (c == 0 && row.Detail is { } detail)
                {
                    var stack = new StackPanel();
                    stack.Children.Add(text);
                    var line = Text(detail, 8.5, 13, DocumentInk.QuietBrush);
                    line.Margin = new Thickness(0, 1, 0, 0);
                    stack.Children.Add(line);
                    cell = stack;
                }
                cell.Margin = new Thickness(numeric[c] ? 12 : 0, 7, 0, 8 + 1);
                Grid.SetRow(cell, gridRow);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        return grid;
    }

    /// <summary>A 1px rule along the bottom of a table row, under its cells.</summary>
    private static void AddRule(Grid grid, int row, int columns, IBrush brush)
    {
        var rule = new Border { Height = 1, Background = brush, VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetRow(rule, row);
        Grid.SetColumnSpan(rule, Math.Max(1, columns));
        grid.Children.Add(rule);
    }

    /// <summary>
    /// The totals, 210 wide on the right (label 118, amount 92): rows of 10 / 15 with padding 2 0, labels in the secondary
    /// ink; the strong row 6 lower, 11 / 16.5 500 between a strong rule (padding 6 0 5) and a 3px double rule.
    /// </summary>
    private Control Totals(DocumentTotals totals)
    {
        var stack = new StackPanel { Width = 210, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var row in totals.Rows)
        {
            if (!row.IsStrong)
            {
                var plain = TotalsRow(row, 10, 15, DocumentInk.SecondaryBrush, FontWeight.Normal);
                plain.Margin = new Thickness(0, 2);
                stack.Children.Add(plain);
                continue;
            }
            var strong = new Border
            {
                Margin = new Thickness(0, 6, 0, 0),
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = DocumentInk.StrongRuleBrush,
                Padding = new Thickness(0, 6, 0, 5),
                Child = TotalsRow(row, 11, 16.5, DocumentInk.InkBrush, FontWeight.Medium)
            };
            stack.Children.Add(strong);
            stack.Children.Add(new Border
            {
                Height = 3,
                BorderThickness = new Thickness(0, 1),
                BorderBrush = DocumentInk.StrongRuleBrush
            });
        }
        return stack;
    }

    private Grid TotalsRow(DocumentTotalsRow row, double size, double lineHeight, IBrush labelInk, FontWeight weight)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("118,92") };
        grid.Children.Add(Text(row.Label, size, lineHeight, labelInk, weight));
        var value = Text(row.Value, size, lineHeight, DocumentInk.InkBrush, weight, TextAlignment.Right, wrap: false);
        Grid.SetColumn(value, 1);
        grid.Children.Add(value);
        return grid;
    }

    /// <summary>The footer: a thin rule, 8, then quiet lines of 7.5 / 11.25 on the left and the page number on the right, on the last line.</summary>
    private Control Footer(DocumentFooter footer)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        var left = new StackPanel();
        foreach (var line in footer.Left)
        {
            left.Children.Add(Text(line, 7.5, 11.25, DocumentInk.QuietBrush));
        }
        grid.Children.Add(left);
        if (!string.IsNullOrEmpty(footer.Right))
        {
            var right = Text(footer.Right, 7.5, 11.25, DocumentInk.QuietBrush, alignment: TextAlignment.Right, wrap: false);
            right.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
        }
        return new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = DocumentInk.RuleBrush,
            Padding = new Thickness(0, 8, 0, 0),
            Child = grid
        };
    }

    #endregion

    #region Texts

    private static DocumentPageText NewText(double size, double lineHeight, IBrush ink, FontWeight weight, TextAlignment alignment = TextAlignment.Left,
                                            bool wrap = true) => new()
    {
        FontFamily = DocumentInk.Font,
        FontFeatures = DocumentInk.TabularNumbers,
        FontSize = size,
        LineHeight = lineHeight,
        FontWeight = weight,
        Foreground = ink,
        TextAlignment = alignment,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap
    };

    /// <summary>
    /// A line height below the font's own (17 / 17, 24 / 24): the text keeps its natural line and is centered in a box of
    /// the given height, which is where CSS puts it (the half-leading is negative on both sides).
    /// </summary>
    private static void FitLine(DocumentPageText text, double lineHeight)
    {
        text.Height = lineHeight;
        text.VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>A text; its spans are registered with the page as ranges of it.</summary>
    private DocumentPageText Text(DocumentText? content, double size, double lineHeight, IBrush ink, FontWeight? weight = null,
                                  TextAlignment alignment = TextAlignment.Left, bool wrap = true)
    {
        var text = NewText(size, lineHeight, ink, weight ?? FontWeight.Normal, alignment, wrap);
        if (content is null)
        {
            return text;
        }
        var builder = new StringBuilder();
        Append(text, content, builder);
        text.Text = builder.ToString();
        return text;
    }

    private DocumentPageText Text(string? content, double size, double lineHeight, IBrush ink, FontWeight? weight = null,
                                  TextAlignment alignment = TextAlignment.Left, bool wrap = true)
    {
        var text = NewText(size, lineHeight, ink, weight ?? FontWeight.Normal, alignment, wrap);
        text.Text = content ?? string.Empty;
        return text;
    }

    /// <summary>One text made of several pieces in their own ink, a space between them (an email's name and address).</summary>
    private DocumentPageText Pieces(double size, double lineHeight, params (DocumentText Content, IBrush Ink)[] pieces)
    {
        var text = NewText(size, lineHeight, DocumentInk.InkBrush, FontWeight.Normal);
        var builder = new StringBuilder();
        var inlines = new InlineCollection();
        text.Inlines = inlines;
        foreach (var (content, ink) in pieces)
        {
            if (content.Inlines.Count == 0)
            {
                continue;
            }
            if (builder.Length > 0)
            {
                builder.Append(' ');
                inlines.Add(new Run(" "));
            }
            var start = builder.Length;
            Append(text, content, builder);
            inlines.Add(new Run(builder.ToString(start, builder.Length - start)) { Foreground = ink });
        }
        return text;
    }

    /// <summary>Appends the pieces of <paramref name="content"/>, registering each span at the index it starts at.</summary>
    private void Append(DocumentPageText text, DocumentText content, StringBuilder builder)
    {
        foreach (var inline in content.Inlines)
        {
            if (inline is DocumentSpan span && !string.IsNullOrEmpty(span.Id) && span.Text.Length > 0)
            {
                _page.RegisterSpan(span.Id, text, builder.Length, span.Text.Length);
            }
            builder.Append(inline.Text);
        }
    }

    #endregion
}
