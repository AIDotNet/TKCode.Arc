using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented documents (an invoice and an email), their schemas and a scripted extractor, shared by every document
/// extraction test. All names, numbers and texts are made up; the IBANs have valid check digits.
/// </summary>
internal static class ExtractionFixtures
{
    private static DocumentText Text(params object[] parts)
    {
        var text = new DocumentText();
        foreach (var part in parts)
        {
            text.Inlines.Add(part switch
            {
                string s => new DocumentRun(s),
                DocumentInline i => i,
                _ => throw new ArgumentException("A string or an inline", nameof(parts))
            });
        }

        return text;
    }

    private static DocumentSpan Span(string id, string text) => new(id, text);

    private static DocumentLines Lines(string label, params DocumentText[] lines)
    {
        var block = new DocumentLines { Label = label };
        block.Lines.AddRange(lines);
        return block;
    }

    private static DocumentPair Pair(string label, DocumentText value) => new() { Label = label, Value = value };

    private static DocumentTableRow Row(DocumentText description, string? detail, params DocumentText[] rest)
    {
        var row = new DocumentTableRow { Detail = detail is null ? null : (DocumentText)detail };
        row.Cells.Add(description);
        row.Cells.AddRange(rest);
        return row;
    }

    /// <summary>A one-page invoice from a ceramics shop; spans are named after what cites them.</summary>
    public static ExtractionDocument Invoice()
    {
        var document = new ExtractionDocument { Id = "invoice", Name = "oakridge-invoice-1187.pdf", Label = "PDF, 1 page", Kind = DocumentKind.Pdf };
        document.Blocks.Add(new DocumentMasthead
        {
            Mark = Geometry.Parse("M5 12a2.5 2.5 0 1 0 5 0 2.5 2.5 0 0 0-5 0ZM14.5 6a2.5 2.5 0 1 0 5 0 2.5 2.5 0 0 0-5 0ZM14.5 18a2.5 2.5 0 1 0 5 0 2.5 2.5 0 0 0-5 0Z"),
            Name = "oakridge", Detail = "ceramics", Title = "Invoice", BrandColor = new SolidColorBrush(Color.Parse("#2F6F73"))
        });
        var columns = new DocumentColumns();
        columns.Columns.Add(Lines("From", Text(Span("vendor", "Oakridge Ceramics Ltd")), "14 Kiln Lane", "Bristol BS1 6QA, United Kingdom", "VAT GB 908 4417 22"));
        columns.Columns.Add(Lines("Bill to", "Bluefin Logistics BV", "Attn. Sanne de Vries", "Havenstraat 9", "3011 AB Rotterdam, Netherlands"));
        document.Blocks.Add(columns);
        var strip = new DocumentMetaStrip();
        strip.Cells.Add(Pair("Invoice no.", Text(Span("invoiceNumber", "OC-2026-1187"))));
        strip.Cells.Add(Pair("Issue date", Text(Span("issueDate", "3 September 2026"))));
        strip.Cells.Add(Pair("Payment terms", Text(Span("terms", "14 days net"))));
        strip.Cells.Add(Pair("Customer no.", "BL-5530"));
        document.Blocks.Add(strip);
        var table = new DocumentTable();
        table.Columns.Add(new DocumentTableColumn { Header = "Description" });
        table.Columns.Add(new DocumentTableColumn { Header = "Qty", IsNumeric = true });
        table.Columns.Add(new DocumentTableColumn { Header = "Unit price", IsNumeric = true });
        table.Columns.Add(new DocumentTableColumn { Header = "Amount", IsNumeric = true });
        table.Rows.Add(Row(Text(Span("li0d", "Glazed floor tiles, 20×20 cm")), "Batch 41, matte sand", Text(Span("li0q", "2 pallets")), Text(Span("li0u", "640.00")), Text(Span("li0a", "1,280.00"))));
        table.Rows.Add(Row(Text(Span("li1d", "Hand-thrown planters")), "Set of six, stoneware", Text(Span("li1q", "3")), Text(Span("li1u", "210.00")), Text(Span("li1a", "630.00"))));
        table.Rows.Add(Row(Text(Span("li2d", "Kiln firing service")), "Customer pieces, two loads", Text(Span("li2q", "2")), Text(Span("li2u", "95.00")), Text(Span("li2a", "190.00"))));
        table.Rows.Add(Row(Text(Span("li3d", "Packing and freight")), "Crated, insured", Text(Span("li3q", "1")), Text(Span("li3u", "120.00")), Text(Span("li3a", "120.00"))));
        document.Blocks.Add(table);
        var totals = new DocumentTotals();
        totals.Rows.Add(new DocumentTotalsRow { Label = "Subtotal", Value = Text(Span("subtotal", "2,220.00")) });
        totals.Rows.Add(new DocumentTotalsRow { Label = "VAT 21%", Value = Text(Span("vat", "466.20")) });
        totals.Rows.Add(new DocumentTotalsRow { Label = "Total due", IsStrong = true, Value = Text(Span("currency", "EUR"), " ", Span("total", "2,686.20")) });
        document.Blocks.Add(totals);
        document.Blocks.Add(new DocumentHeading { Text = "Payment details" });
        var pairs = new DocumentPairs();
        pairs.Pairs.Add(Pair("Account holder", "Oakridge Ceramics Ltd"));
        pairs.Pairs.Add(Pair("IBAN", Text(Span("iban", "NL70 ZZTE 0417 2635 98"))));
        pairs.Pairs.Add(Pair("Reference", "OC-2026-1187"));
        document.Blocks.Add(pairs);
        document.Blocks.Add(new DocumentParagraph
        {
            IsMuted = true,
            Text = Text("Please note our new account from 1 August 2026; the old account ", Span("ibanOld", "NL63 ZZTE 0398 1147 60"), " is closed.")
        });
        var footer = new DocumentFooter { Right = "Page 1 of 1" };
        footer.Left.Add("Oakridge Ceramics Ltd · 14 Kiln Lane · Bristol");
        footer.Left.Add("accounts@oakridge.example · oakridge.example");
        document.Blocks.Add(footer);
        return document;
    }

    /// <summary>The schema of <see cref="Invoice"/>: four groups, ten fields (one of them a table).</summary>
    public static ExtractionSchema InvoiceSchema()
    {
        var schema = new ExtractionSchema { Name = "invoice", Title = "Invoice", Description = "A supplier invoice." };
        schema.Groups.AddRange([
            new ExtractionGroup { Key = "invoice", Title = "Invoice" }, new ExtractionGroup { Key = "items", Title = "Line items" },
            new ExtractionGroup { Key = "totals", Title = "Totals" }, new ExtractionGroup { Key = "payment", Title = "Payment" }]);
        schema.Fields.Add(new ExtractionField { Key = "vendor", Label = "Vendor", Type = ExtractionFieldType.Text, Group = "invoice", Description = "Who sent the invoice" });
        schema.Fields.Add(new ExtractionField { Key = "invoiceNumber", Label = "Invoice no.", Type = ExtractionFieldType.Text, Group = "invoice" });
        schema.Fields.Add(new ExtractionField { Key = "issueDate", Label = "Issue date", Type = ExtractionFieldType.Date, Group = "invoice" });
        schema.Fields.Add(new ExtractionField { Key = "dueDate", Label = "Due date", Type = ExtractionFieldType.Date, Group = "invoice", Description = "The date payment is due" });
        var items = new ExtractionTableField { Key = "lineItems", Label = "Line items", Type = ExtractionFieldType.Text, Group = "items" };
        items.Columns.Add(new ExtractionColumn { Key = "description", Label = "Description", Type = ExtractionFieldType.Text });
        items.Columns.Add(new ExtractionColumn { Key = "quantity", Label = "Qty", Type = ExtractionFieldType.Number });
        items.Columns.Add(new ExtractionColumn { Key = "unitPrice", Label = "Unit price", Type = ExtractionFieldType.Money });
        items.Columns.Add(new ExtractionColumn { Key = "amount", Label = "Amount", Type = ExtractionFieldType.Money });
        schema.Fields.Add(items);
        schema.Fields.Add(new ExtractionField { Key = "currency", Label = "Currency", Type = ExtractionFieldType.Currency, Group = "totals" });
        schema.Fields.Add(new ExtractionField { Key = "subtotal", Label = "Subtotal", Type = ExtractionFieldType.Money, Group = "totals" });
        schema.Fields.Add(new ExtractionField { Key = "vat", Label = "VAT", Type = ExtractionFieldType.Money, Group = "totals" });
        schema.Fields.Add(new ExtractionField { Key = "total", Label = "Total", Type = ExtractionFieldType.Money, Group = "totals" });
        schema.Fields.Add(new ExtractionField { Key = "iban", Label = "IBAN", Type = ExtractionFieldType.Iban, Group = "payment" });
        return schema;
    }

    /// <summary>The events a model would produce for <see cref="Invoice"/>: ten fields, two of them below 0.8.</summary>
    public static IReadOnlyList<ExtractionEvent> InvoiceEvents()
    {
        var events = new List<ExtractionEvent>
        {
            new ExtractionValueEvent("vendor", "Oakridge Ceramics Ltd", "vendor", 0.99),
            new ExtractionValueEvent("invoiceNumber", "OC-2026-1187", "invoiceNumber", 0.98),
            new ExtractionValueEvent("issueDate", "2026-09-03", "issueDate", 0.97),
            new ExtractionValueEvent("dueDate", "2026-09-17", "terms", 0.66, "The due date is not printed; it is the issue date plus 14 days."),
            new ExtractionUsageEvent(1180, 40)
        };
        string[][] rows =
        [
            ["Glazed floor tiles, 20×20 cm", "2", "640.00", "1,280.00"], ["Hand-thrown planters", "3", "210.00", "630.00"],
            ["Kiln firing service", "2", "95.00", "190.00"], ["Packing and freight", "1", "120.00", "120.00"]
        ];
        string[] columns = ["description", "quantity", "unitPrice", "amount"];
        string[] ids = ["d", "q", "u", "a"];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < columns.Length; c++)
            {
                events.Add(new ExtractionValueEvent($"lineItems.{r}.{columns[c]}", rows[r][c], $"li{r}{ids[c]}", 0.94));
            }
        }

        events.Add(new ExtractionValueEvent("currency", "EUR", "currency", 0.98));
        events.Add(new ExtractionValueEvent("subtotal", "2,220.00", "subtotal", 0.99));
        events.Add(new ExtractionValueEvent("vat", "466.20", "vat", 0.96));
        events.Add(new ExtractionValueEvent("total", "2,686.20", "total", 0.99));
        events.Add(new ExtractionValueEvent("iban", "NL70 ZZTE 0417 2635 98", "iban", 0.71, "Two account numbers appear; the note says the new one is current."));
        events.Add(new ExtractionUsageEvent(1180, 210));
        return events;
    }

    /// <summary>A short email about a shipped parcel.</summary>
    public static ExtractionDocument Email()
    {
        var document = new ExtractionDocument { Id = "email", Name = "Your parcel FB-77302 has shipped.eml", Label = "Email", Kind = DocumentKind.Email };
        document.Blocks.Add(new DocumentEmailHeader
        {
            Subject = Text("Your parcel ", Span("orderNumber", "FB-77302"), " has shipped"),
            FromName = Text(Span("merchant", "Fernhill Books")), FromAddress = "<orders@fernhillbooks.example>", To = "To Jonas Weber",
            Date = Text(Span("mailDate", "Tue, 22 Sep 2026, 08:15")), AvatarColor = new SolidColorBrush(Color.Parse("#3E6B48"))
        });
        document.Blocks.Add(new DocumentMasthead { Name = "fernhill", Detail = "books", BrandColor = new SolidColorBrush(Color.Parse("#3E6B48")) });
        document.Blocks.Add(new DocumentParagraph { Text = "Hello Jonas," });
        document.Blocks.Add(new DocumentParagraph
        {
            Text = Text("Your order FB-77302 left our Leeds warehouse ", Span("shipDate", "today"), ". It is travelling with ", Span("carrier", "Swiftline Parcel"),
                " and should reach you on ", Span("estimatedDelivery", "Friday, 25 September"), ".")
        });
        var strip = new DocumentMetaStrip();
        strip.Cells.Add(Pair("Tracking number", Text(Span("trackingNumber", "SWL 4410 2093 7781"))));
        strip.Cells.Add(Pair("Shipping to", Text(Span("recipient", "Jonas Weber"))));
        document.Blocks.Add(strip);
        var lines = new DocumentLines { Label = "Address" };
        lines.Lines.Add(Text(Span("shipTo", "Bergmannstraße 41, 10961 Berlin, Germany")));
        document.Blocks.Add(lines);
        document.Blocks.Add(new DocumentHeading { Text = "Items in this parcel" });
        var table = new DocumentTable();
        table.Columns.Add(new DocumentTableColumn { Header = "Item" });
        table.Columns.Add(new DocumentTableColumn { Header = "Qty", IsNumeric = true });
        table.Columns.Add(new DocumentTableColumn { Header = "Price", IsNumeric = true });
        table.Rows.Add(Row(Text(Span("e0d", "A Field Guide to Lichens")), null, Text(Span("e0q", "1")), Text(Span("e0a", "€24.00"))));
        table.Rows.Add(Row(Text(Span("e1d", "Notebook, dot grid, A5")), null, Text(Span("e1q", "2")), Text(Span("e1a", "€19.00"))));
        table.Rows.Add(Row(Text(Span("e2d", "Bookmark set, brass")), null, Text(Span("e2q", "1")), Text(Span("e2a", "€8.50"))));
        document.Blocks.Add(table);
        var totals = new DocumentTotals();
        totals.Rows.Add(new DocumentTotalsRow { Label = "Shipping", Value = Text(Span("shippingCost", "€0.00")) });
        totals.Rows.Add(new DocumentTotalsRow { Label = "Total", IsStrong = true, Value = Text(Span("total", "€51.50")) });
        document.Blocks.Add(totals);
        return document;
    }

    /// <summary>The schema of <see cref="Email"/>: four groups, twelve fields counting the table's cells as one.</summary>
    public static ExtractionSchema EmailSchema()
    {
        var schema = new ExtractionSchema { Name = "shipment", Title = "Shipment" };
        schema.Groups.AddRange([
            new ExtractionGroup { Key = "order", Title = "Order" }, new ExtractionGroup { Key = "delivery", Title = "Delivery" },
            new ExtractionGroup { Key = "items", Title = "Items" }, new ExtractionGroup { Key = "totals", Title = "Totals" }]);
        schema.Fields.Add(new ExtractionField { Key = "merchant", Label = "Merchant", Type = ExtractionFieldType.Text, Group = "order" });
        schema.Fields.Add(new ExtractionField { Key = "orderNumber", Label = "Order no.", Type = ExtractionFieldType.Text, Group = "order" });
        schema.Fields.Add(new ExtractionField { Key = "carrier", Label = "Carrier", Type = ExtractionFieldType.Text, Group = "delivery" });
        schema.Fields.Add(new ExtractionField { Key = "trackingNumber", Label = "Tracking no.", Type = ExtractionFieldType.Text, Group = "delivery" });
        schema.Fields.Add(new ExtractionField { Key = "shipDate", Label = "Ship date", Type = ExtractionFieldType.Date, Group = "delivery" });
        schema.Fields.Add(new ExtractionField { Key = "estimatedDelivery", Label = "Estimated delivery", Type = ExtractionFieldType.Date, Group = "delivery" });
        schema.Fields.Add(new ExtractionField { Key = "recipient", Label = "Recipient", Type = ExtractionFieldType.Text, Group = "delivery" });
        schema.Fields.Add(new ExtractionField { Key = "shipTo", Label = "Ship to", Type = ExtractionFieldType.Text, Group = "delivery" });
        var items = new ExtractionTableField { Key = "items", Label = "Items", Type = ExtractionFieldType.Text, Group = "items" };
        items.Columns.Add(new ExtractionColumn { Key = "description", Label = "Item", Type = ExtractionFieldType.Text });
        items.Columns.Add(new ExtractionColumn { Key = "quantity", Label = "Qty", Type = ExtractionFieldType.Integer });
        items.Columns.Add(new ExtractionColumn { Key = "amount", Label = "Price", Type = ExtractionFieldType.Money });
        schema.Fields.Add(items);
        schema.Fields.Add(new ExtractionField { Key = "currency", Label = "Currency", Type = ExtractionFieldType.Currency, Group = "totals" });
        schema.Fields.Add(new ExtractionField { Key = "shippingCost", Label = "Shipping", Type = ExtractionFieldType.Money, Group = "totals" });
        schema.Fields.Add(new ExtractionField { Key = "total", Label = "Total", Type = ExtractionFieldType.Money, Group = "totals" });
        return schema;
    }

    /// <summary>The events for <see cref="Email"/>; the two inferred dates are below 0.8.</summary>
    public static IReadOnlyList<ExtractionEvent> EmailEvents() =>
    [
        new ExtractionValueEvent("merchant", "Fernhill Books", "merchant", 0.99),
        new ExtractionValueEvent("orderNumber", "FB-77302", "orderNumber", 0.99),
        new ExtractionValueEvent("carrier", "Swiftline Parcel", "carrier", 0.98),
        new ExtractionValueEvent("trackingNumber", "SWL 4410 2093 7781", "trackingNumber", 0.97),
        new ExtractionValueEvent("shipDate", "2026-09-22", "shipDate", 0.73, "The email says \"today\"; taken as the date the email was sent."),
        new ExtractionValueEvent("estimatedDelivery", "2026-09-25", "estimatedDelivery", 0.78, "No year is printed; the year of the email is used."),
        new ExtractionValueEvent("recipient", "Jonas Weber", "recipient", 0.97),
        new ExtractionValueEvent("shipTo", "Bergmannstraße 41, 10961 Berlin, Germany", "shipTo", 0.96),
        new ExtractionUsageEvent(640, 70),
        new ExtractionValueEvent("items.0.description", "A Field Guide to Lichens", "e0d", 0.95),
        new ExtractionValueEvent("items.0.quantity", "1", "e0q", 0.95),
        new ExtractionValueEvent("items.0.amount", "24.00", "e0a", 0.95),
        new ExtractionValueEvent("items.1.description", "Notebook, dot grid, A5", "e1d", 0.95),
        new ExtractionValueEvent("items.1.quantity", "2", "e1q", 0.95),
        new ExtractionValueEvent("items.1.amount", "19.00", "e1a", 0.95),
        new ExtractionValueEvent("items.2.description", "Bookmark set, brass", "e2d", 0.95),
        new ExtractionValueEvent("items.2.quantity", "1", "e2q", 0.95),
        new ExtractionValueEvent("items.2.amount", "8.50", "e2a", 0.95),
        new ExtractionValueEvent("currency", "EUR", "total", 0.9),
        new ExtractionValueEvent("shippingCost", "0.00", "shippingCost", 0.95),
        new ExtractionValueEvent("total", "51.50", "total", 0.99),
        new ExtractionUsageEvent(640, 190)
    ];

    /// <summary>
    /// Plays a list of events with a pause before the first and between them. Tests pass a <paramref name="delay"/> that
    /// completes at once (or on a gate); <paramref name="failAfter"/> throws after that many events once.
    /// </summary>
    public sealed class ScriptedExtractor(
        IReadOnlyList<ExtractionEvent> events,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        TimeSpan? readingTime = null,
        TimeSpan? step = null) : IDocumentExtractor
    {
        /// <summary>When set, the next run fails after this many events (and the setting is cleared).</summary>
        public int? FailAfter { get; set; }

        /// <summary>How many runs started.</summary>
        public int Runs { get; private set; }

        public async IAsyncEnumerable<ExtractionEvent> ExtractAsync(
            ExtractionDocument document, ExtractionSchema schema, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Runs++;
            var wait = delay ?? ((time, token) => Task.Delay(time, token));
            await wait(readingTime ?? TimeSpan.FromMilliseconds(880), cancellationToken);
            var failAfter = FailAfter;
            FailAfter = null;
            var count = 0;
            foreach (var item in events)
            {
                if (failAfter is { } limit && count >= limit)
                {
                    throw new InvalidOperationException("The model is busy right now. Nothing was charged, so you can try again.");
                }

                await wait(step ?? TimeSpan.FromMilliseconds(150), cancellationToken);
                count++;
                yield return item;
            }
        }
    }
}
