using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// What the document extraction reads from a schema: fields and sections in order, the series tone of each field (groups in
/// turn, a field without a group on its own), how value paths resolve and how many values there are to fill.
/// </summary>
public class ExtractionSchemaInfoTests
{
    [AvaloniaFact]
    public void The_Invoice_Has_Four_Sections_In_Order_With_Their_Tones()
    {
        var info = new ExtractionSchemaInfo(ExtractionFixtures.InvoiceSchema());
        info.Fields.Select(f => f.Key).ShouldBe(["vendor", "invoiceNumber", "issueDate", "dueDate", "lineItems", "currency", "subtotal", "vat", "total", "iban"]);
        info.ScalarCount.ShouldBe(9);
        info.TableCount.ShouldBe(1);
        info.Sections.Select(s => s.Group!.Title).ShouldBe(["Invoice", "Line items", "Totals", "Payment"]);
        info.Sections[2].Fields.Select(f => f.Key).ShouldBe(["currency", "subtotal", "vat", "total"]);
        info.Fields.Select(info.ToneOf).ShouldBe([1, 1, 1, 1, 2, 3, 3, 3, 3, 4]);
        info.GroupIndexOf(info.FindField("iban")!).ShouldBe(3);
        info.GroupOf(info.FindField("vat")!)!.Key.ShouldBe("totals");
        info.IndexOf(info.FindField("lineItems")!).ShouldBe(4);
    }

    [AvaloniaFact]
    public void Paths_Take_The_Tone_Of_Their_Field()
    {
        var info = new ExtractionSchemaInfo(ExtractionFixtures.InvoiceSchema());
        info.ToneOf("lineItems.3.amount").ShouldBe(2);
        info.ToneOf("lineItems.0").ShouldBe(2);
        info.ToneOf("lineItems").ShouldBe(2);
        info.ToneOf("iban").ShouldBe(4);
        info.ToneOf("nothing").ShouldBe(1);
    }

    [AvaloniaFact]
    public void Without_Groups_Every_Field_Takes_The_Next_Tone()
    {
        var schema = new ExtractionSchema { Name = "receipt" };
        foreach (var key in new[] { "merchant", "date", "guests", "distance", "tip" })
        {
            schema.Fields.Add(new ExtractionField { Key = key, Label = key });
        }
        var info = new ExtractionSchemaInfo(schema);
        info.Sections.Count.ShouldBe(5);
        info.Sections.ShouldAllBe(s => s.Group == null && s.Fields.Count == 1);
        info.Fields.Select(info.ToneOf).ShouldBe([1, 2, 3, 4, 1]);
    }

    [AvaloniaFact]
    public void Groups_Are_Colored_In_The_Order_They_Appear_And_Loners_Take_Their_Own()
    {
        var schema = new ExtractionSchema { Name = "mixed" };
        // Declared in another order than used; "empty" has no fields and takes no color; "ghost" is not declared.
        schema.Groups.AddRange([new ExtractionGroup { Key = "b", Title = "B" }, new ExtractionGroup { Key = "empty", Title = "Empty" },
            new ExtractionGroup { Key = "a", Title = "A" }]);
        schema.Fields.Add(new ExtractionField { Key = "a1", Group = "a" });
        schema.Fields.Add(new ExtractionField { Key = "solo" });
        schema.Fields.Add(new ExtractionField { Key = "b1", Group = "b" });
        schema.Fields.Add(new ExtractionField { Key = "a2", Group = "a" });
        schema.Fields.Add(new ExtractionField { Key = "lost", Group = "ghost" });
        var info = new ExtractionSchemaInfo(schema);
        info.Sections.Select(s => s.Group?.Key ?? s.Fields[0].Key).ShouldBe(["a", "solo", "b", "lost"]);
        info.Sections[0].Fields.Select(f => f.Key).ShouldBe(["a1", "a2"]);
        info.Fields.Select(info.ToneOf).ShouldBe([1, 2, 3, 1, 4]);
        info.GroupOf(info.FindField("lost")!).ShouldBeNull();
    }

    [AvaloniaFact]
    public void Paths_Resolve_To_Fields_Rows_And_Columns()
    {
        var info = new ExtractionSchemaInfo(ExtractionFixtures.InvoiceSchema());
        info.TryResolve("vendor", out var field, out var column, out var row).ShouldBeTrue();
        field.Key.ShouldBe("vendor");
        column.ShouldBeNull();
        row.ShouldBeNull();

        info.TryResolve("lineItems.2.unitPrice", out field, out column, out row).ShouldBeTrue();
        field.ShouldBeOfType<ExtractionTableField>().Key.ShouldBe("lineItems");
        column!.Type.ShouldBe(ExtractionFieldType.Money);
        row.ShouldBe(2);

        info.TryResolve("lineItems", out field, out column, out _).ShouldBeTrue();
        field.ShouldBeOfType<ExtractionTableField>();
        column.ShouldBeNull();

        info.TryResolve("lineItems.12.amount", out _, out _, out row).ShouldBeTrue();
        row.ShouldBe(12);
    }

    [AvaloniaTheory]
    [InlineData("unknown")]
    [InlineData("lineItems.0")]
    [InlineData("lineItems.0.price")]
    [InlineData("lineItems.x.amount")]
    [InlineData("lineItems.-1.amount")]
    [InlineData("lineItems.01.amount")]
    [InlineData("lineItems..amount")]
    [InlineData("vendor.0.amount")]
    [InlineData("")]
    public void Other_Paths_Do_Not_Resolve(string path)
    {
        new ExtractionSchemaInfo(ExtractionFixtures.InvoiceSchema()).TryResolve(path, out _, out _, out _).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Table_Counts_As_One_Value_Until_It_Has_Rows()
    {
        var info = new ExtractionSchemaInfo(ExtractionFixtures.InvoiceSchema());
        info.LeafCount(_ => 0).ShouldBe(10);
        info.LeafCount(_ => 1).ShouldBe(13);
        info.LeafCount(_ => 4).ShouldBe(25);
        new ExtractionSchemaInfo(ExtractionFixtures.EmailSchema()).LeafCount(_ => 0).ShouldBe(12);
    }

    [AvaloniaFact]
    public void Cell_And_Row_Paths_Are_Written_Plainly()
    {
        ExtractionSchemaInfo.CellPath("lineItems", 10, "amount").ShouldBe("lineItems.10.amount");
        ExtractionSchemaInfo.RowPath("items", 0).ShouldBe("items.0");
    }
}
