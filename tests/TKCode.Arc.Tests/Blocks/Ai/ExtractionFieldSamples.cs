using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// A stand-in for the document extraction form, for the field part tests and snapshots: the invoice schema of
/// <see cref="ExtractionFixtures"/> as group titles, <see cref="ExtractionFieldRow"/>s and an <see cref="ExtractionTableGrid"/>,
/// with the values kept the way the owner keeps them (one value per field up front, a row per table row when its first
/// cell arrives, the table's summary as the mean of its cells). The words are the English defaults of
/// <see cref="DocumentExtractionLabels"/>.
/// </summary>
internal sealed class ExtractionFieldSamples
{
    public static readonly DocumentExtractionLabels Labels = new();

    private ExtractionFieldSamples(ExtractionSchema schema)
    {
        Schema = schema;
    }

    public ExtractionSchema Schema { get; }

    /// <summary>The form: group titles, rows and tables in schema order.</summary>
    public StackPanel Root { get; } = new() { Width = 410, HorizontalAlignment = HorizontalAlignment.Left };

    /// <summary>The value of every scalar field by key, and of every cell by path.</summary>
    public Dictionary<string, ExtractionValue> Values { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ExtractionFieldRow> Rows { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ExtractionTableGrid> Tables { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, AvaloniaList<AvaloniaList<ExtractionValue>>> TableRows { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ExtractionValue> Summaries { get; } = new(StringComparer.Ordinal);

    /// <summary>The invoice's form with every value pending.</summary>
    public static ExtractionFieldSamples Invoice(bool narrow = false, double width = 410) => Build(ExtractionFixtures.InvoiceSchema(), narrow, width);

    /// <summary>The invoice's line items alone (its group, tone 2), every value pending.</summary>
    public static ExtractionFieldSamples InvoiceTable(bool narrow = false, double width = 410)
    {
        var invoice = ExtractionFixtures.InvoiceSchema();
        var schema = new ExtractionSchema { Name = invoice.Name };
        schema.Groups.Add(new ExtractionGroup { Key = "invoice", Title = "Invoice" });
        schema.Groups.Add(new ExtractionGroup { Key = "items", Title = "Line items" });
        schema.Fields.Add(new ExtractionField { Key = "vendor", Label = "Vendor", Group = "invoice" });
        schema.Fields.Add(invoice.Fields.First(f => f.Key == "lineItems"));
        var form = Build(schema, narrow, width);
        // Only the table is shown (the vendor row gives it the second tone).
        form.Root.Children.Clear();
        form.Root.Children.Add(form.Tables["lineItems"]);
        return form;
    }

    /// <summary>The line items' grid of <see cref="InvoiceTable"/>.</summary>
    public ExtractionTableGrid Table => Tables["lineItems"];

    /// <summary>The email's form with every value pending.</summary>
    public static ExtractionFieldSamples Email(bool narrow = false, double width = 410) => Build(ExtractionFixtures.EmailSchema(), narrow, width);

    private static ExtractionFieldSamples Build(ExtractionSchema schema, bool narrow, double width)
    {
        var form = new ExtractionFieldSamples(schema);
        form.Root.Width = width;
        string? group = null;
        var tone = 0;
        foreach (var field in schema.Fields)
        {
            if (field.Group != group || field.Group is null)
            {
                group = field.Group;
                tone++;
                var title = schema.Groups.FirstOrDefault(g => g.Key == group)?.Title ?? field.Label;
                // A group that starts with a table has no title of its own: the table's header line carries the dot.
                if (field is not ExtractionTableField)
                {
                    form.Root.Children.Add(GroupTitle(title, tone));
                }
            }
            if (field is ExtractionTableField table)
            {
                var rows = new AvaloniaList<AvaloniaList<ExtractionValue>>();
                var summary = new ExtractionValue(table.Key);
                var grid = new ExtractionTableGrid
                {
                    Label = table.Label,
                    Tone = tone,
                    FieldKey = table.Key,
                    Columns = table.Columns,
                    Rows = rows,
                    SummaryValue = summary,
                    TableText = Labels.Table,
                    EmptyText = Labels.NoRows,
                    PendingText = Labels.RowsPending,
                    EditedText = Labels.Edited,
                    IsNarrow = narrow,
                    CellAutomationName = Labels.CellName
                };
                form.Tables[table.Key] = grid;
                form.TableRows[table.Key] = rows;
                form.Summaries[table.Key] = summary;
                form.Root.Children.Add(grid);
                continue;
            }
            var value = new ExtractionValue(field.Key);
            form.Values[field.Key] = value;
            var row = new ExtractionFieldRow
            {
                Value = value,
                Label = field.Label,
                Tone = tone,
                FieldType = field.Type,
                TypeName = Labels.TypeName(field.Type),
                EditedText = Labels.Edited,
                LooksRightText = Labels.LooksRight,
                AutomationName = field.Label,
                IsNarrow = narrow
            };
            form.Rows[field.Key] = row;
            form.Root.Children.Add(row);
            form.UpdateAutomation(field.Key);
        }
        return form;
    }

    /// <summary>A group title like the owner's (padding 10 8 4, a 7px dot 8 before 12 / 500 TextSecondary).</summary>
    private static Control GroupTitle(string title, int tone)
    {
        var dot = new Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(dot, Shape.FillProperty, tone switch
        {
            1 => DesignTokens.ArcDesignTokenKind.Series1,
            2 => DesignTokens.ArcDesignTokenKind.Series2,
            3 => DesignTokens.ArcDesignTokenKind.Series3,
            _ => DesignTokens.ArcDesignTokenKind.Series4
        });
        var text = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeight.Medium, LineHeight = 16.8, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 10, 8, 4), Children = { dot, text } };
    }

    /// <summary>The value of a path (a scalar field or a cell); creates the cell's row when it is the first of its row.</summary>
    public ExtractionValue ValueOf(string path)
    {
        if (Values.TryGetValue(path, out var value))
        {
            return value;
        }
        var parts = path.Split('.');
        var table = (ExtractionTableField)Schema.Fields.First(f => f.Key == parts[0]);
        var rows = TableRows[table.Key];
        var index = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        while (rows.Count <= index)
        {
            var row = new AvaloniaList<ExtractionValue>();
            foreach (var column in table.Columns)
            {
                var cell = new ExtractionValue($"{table.Key}.{rows.Count}.{column.Key}");
                Values[cell.Path] = cell;
                row.Add(cell);
            }
            rows.Add(row);
            Tables[table.Key].RowsText = Labels.Rows(rows.Count);
        }
        return Values[path];
    }

    /// <summary>Applies a model's value the way the owner does (a review threshold of 0.8).</summary>
    public ExtractionValue Apply(ExtractionValueEvent e)
    {
        var value = ValueOf(e.Path);
        value.Text = e.Value;
        value.SourceId = e.SourceId;
        value.Confidence = e.Confidence;
        value.Note = e.Note;
        value.Status = ExtractionValueStatus.Filled;
        value.NeedsCheck = e.Confidence < 0.8;
        UpdateSummary(e.Path);
        UpdateAutomation(e.Path);
        return value;
    }

    /// <summary>Applies the first <paramref name="count"/> value events (all when null).</summary>
    public ExtractionFieldSamples Play(IEnumerable<ExtractionEvent> events, int? count = null)
    {
        var applied = 0;
        foreach (var e in events)
        {
            if (count is { } limit && applied >= limit)
            {
                break;
            }
            if (e is ExtractionValueEvent value)
            {
                Apply(value);
                applied++;
            }
        }
        return this;
    }

    /// <summary>The person typed <paramref name="text"/>: the owner's parse (dates must be ISO) and status.</summary>
    public void Edit(string path, string text, string? error = null)
    {
        var value = ValueOf(path);
        value.Text = text;
        value.Status = ExtractionValueStatus.Edited;
        value.NeedsCheck = false;
        value.Error = error;
        UpdateAutomation(path);
    }

    /// <summary>The owner's "Looks right".</summary>
    public void Check(string path)
    {
        var value = ValueOf(path);
        value.Status = ExtractionValueStatus.Checked;
        value.NeedsCheck = false;
        UpdateAutomation(path);
    }

    private void UpdateSummary(string path)
    {
        var key = path.Split('.')[0];
        if (!Summaries.TryGetValue(key, out var summary))
        {
            return;
        }
        var filled = TableRows[key].SelectMany(r => r).Where(v => v.Status != ExtractionValueStatus.Pending).ToList();
        summary.Confidence = filled.Count == 0 ? 0 : filled.Average(v => v.Confidence);
        summary.Status = filled.Count == 0 ? ExtractionValueStatus.Pending : ExtractionValueStatus.Filled;
    }

    private void UpdateAutomation(string path)
    {
        if (Rows.TryGetValue(path, out var row) && Values.TryGetValue(path, out var value))
        {
            row.ConfidenceAutomationText = Labels.ConfidenceName(value.Status, value.NeedsCheck, (int)Math.Round(value.Confidence * 100));
        }
    }
}
