using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The values of a document extraction: the invoice and the email of the fixtures played through the store (counts, the
/// average, values to check, table rows), what the person does (edit, Looks right, Accept all and its undo), clearing,
/// type errors and the batched change notice.
/// </summary>
public class ExtractionStoreTests
{
    private static ExtractionStore Invoice(Func<double>? threshold = null) => new(ExtractionFixtures.InvoiceSchema(), threshold ?? (() => 0.8));

    private static ExtractionStore Email() => new(ExtractionFixtures.EmailSchema(), () => 0.8);

    private static void Play(ExtractionStore store, IEnumerable<ExtractionEvent> events)
    {
        foreach (var value in events.OfType<ExtractionValueEvent>())
        {
            store.ApplyModelValue(value).ShouldNotBeNull(value.Path);
        }
    }

    private static ExtractionStore PlayedInvoice()
    {
        var store = Invoice();
        Play(store, ExtractionFixtures.InvoiceEvents());
        return store;
    }

    [AvaloniaFact]
    public void A_New_Store_Has_Every_Field_Pending_And_No_Rows()
    {
        var store = Invoice();
        store.Values.Select(v => v.Path).ShouldBe(["vendor", "invoiceNumber", "issueDate", "dueDate", "currency", "subtotal", "vat", "total", "iban"]);
        store.Values.ShouldAllBe(v => v.Status == ExtractionValueStatus.Pending && v.Text == "" && !v.NeedsCheck && v.IsValid);
        store.FieldValues.Select(v => v.Path).ShouldBe(["vendor", "invoiceNumber", "issueDate", "dueDate", "lineItems", "currency", "subtotal", "vat", "total", "iban"]);
        store.TableRows("lineItems").ShouldNotBeNull().ShouldBeEmpty();
        store.TableRows("vendor").ShouldBeNull();
        store.FoundCount.ShouldBe(0);
        store.FieldCount.ShouldBe(10);
        store.FoundFieldCount.ShouldBe(0);
        store.TotalFieldCount.ShouldBe(10);
        store.CheckCount.ShouldBe(0);
        store.AverageConfidence.ShouldBe(0);
        store.InvalidValues.ShouldBeEmpty();
        store.AllAccepted.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Invoice_Run_Fills_Ten_Fields_And_Sixteen_Cells()
    {
        var store = PlayedInvoice();
        store.FoundCount.ShouldBe(25);
        store.FieldCount.ShouldBe(25);
        store.FoundFieldCount.ShouldBe(10);
        store.CheckCount.ShouldBe(2);
        // (0.99 + 0.98 + 0.97 + 0.66 + 16 × 0.94 + 0.98 + 0.99 + 0.96 + 0.99 + 0.71) / 25: every leaf, cells included.
        store.AverageConfidence.ShouldBe(23.27 / 25, 1e-9);
        store.InvalidValues.ShouldBeEmpty();

        var rows = store.TableRows("lineItems")!;
        rows.Count.ShouldBe(4);
        rows.ShouldAllBe(r => r.Count == 4);
        rows[3].Select(c => c.Path).ShouldBe(["lineItems.3.description", "lineItems.3.quantity", "lineItems.3.unitPrice", "lineItems.3.amount"]);
        store.Values.Count.ShouldBe(25);
        store.Values[4].Path.ShouldBe("lineItems.0.description");
        store.Values[20].Path.ShouldBe("currency");

        var due = store.Find("dueDate")!;
        due.Status.ShouldBe(ExtractionValueStatus.Filled);
        due.NeedsCheck.ShouldBeTrue();
        due.SourceId.ShouldBe("terms");
        due.Note.ShouldStartWith("The due date is not printed");
        due.TypedValue.ShouldBe(new DateOnly(2026, 9, 17));
        store.Find("vendor")!.NeedsCheck.ShouldBeFalse();
        store.Find("lineItems.0.amount")!.Text.ShouldBe("1,280.00");
        store.Find("lineItems.0.amount")!.TypedValue.ShouldBe(1280.00m);
        store.Find("lineItems.0.quantity")!.TypedValue.ShouldBe(2m);
        store.Find("iban")!.TypedValue.ShouldBe("NL70ZZTE0417263598");

        var table = store.FieldValue("lineItems")!;
        table.Status.ShouldBe(ExtractionValueStatus.Filled);
        table.Confidence.ShouldBe(0.94, 1e-9);
        table.NeedsCheck.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Email_Run_Reads_Integers_And_Amounts_Of_Its_Columns()
    {
        var store = Email();
        Play(store, ExtractionFixtures.EmailEvents());
        store.FoundCount.ShouldBe(20);
        store.FieldCount.ShouldBe(20);
        store.FoundFieldCount.ShouldBe(12);
        store.TotalFieldCount.ShouldBe(12);
        store.CheckCount.ShouldBe(2);
        store.Find("items.1.quantity")!.TypedValue.ShouldBeOfType<long>().ShouldBe(2L);
        store.Find("items.2.amount")!.TypedValue.ShouldBe(8.50m);
        store.Find("shipDate")!.TypedValue.ShouldBe(new DateOnly(2026, 9, 22));
        store.Find("currency")!.TypedValue.ShouldBe("EUR");
        store.InvalidValues.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Row_Is_Made_With_All_Its_Cells_When_Its_First_Cell_Arrives()
    {
        var store = Invoice();
        store.ApplyModelValue(new ExtractionValueEvent("lineItems.0.description", "Glazed floor tiles, 20×20 cm", "li0d", 0.94));
        var row = store.TableRows("lineItems")!.ShouldHaveSingleItem();
        row.Select(c => c.Status).ShouldBe([ExtractionValueStatus.Filled, ExtractionValueStatus.Pending, ExtractionValueStatus.Pending, ExtractionValueStatus.Pending]);
        store.FoundCount.ShouldBe(1);
        store.FieldCount.ShouldBe(13);
        store.FoundFieldCount.ShouldBe(1);
        store.Values.Count.ShouldBe(13);

        // A later row fills the rows before it with pending cells, so the table stays dense.
        store.ApplyModelValue(new ExtractionValueEvent("lineItems.2.amount", "190.00", "li2a", 0.94));
        store.TableRows("lineItems")!.Count.ShouldBe(3);
        store.TableRows("lineItems")![1].ShouldAllBe(c => c.Status == ExtractionValueStatus.Pending);
        store.FieldCount.ShouldBe(21);
        store.FoundCount.ShouldBe(2);
    }

    [AvaloniaTheory]
    [InlineData("nothing")]
    [InlineData("lineItems")]
    [InlineData("lineItems.0.price")]
    [InlineData("lineItems.0")]
    [InlineData("lineItems.10000.amount")]
    [InlineData("vendor.0.description")]
    public void Unknown_Paths_Are_Ignored_And_Reported(string path)
    {
        var store = Invoice();
        var changes = 0;
        store.Changed += _ => changes++;
        store.ApplyModelValue(new ExtractionValueEvent(path, "x", null, 0.9)).ShouldBeNull();
        store.EditByUser(path, "x").ShouldBeNull();
        store.MarkChecked(path).ShouldBeFalse();
        store.TableRows("lineItems")!.ShouldBeEmpty();
        store.FoundCount.ShouldBe(0);
        Dispatcher.UIThread.RunJobs();
        changes.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Last_Row_Below_The_Guard_Is_Accepted()
    {
        var store = Invoice();
        store.ApplyModelValue(new ExtractionValueEvent($"lineItems.{ExtractionStore.MaxRows - 1}.amount", "1.00", null, 0.9)).ShouldNotBeNull();
        store.TableRows("lineItems")!.Count.ShouldBe(ExtractionStore.MaxRows);
    }

    [AvaloniaFact]
    public void Confidence_Is_Clamped_And_Compared_With_The_Threshold()
    {
        var store = Invoice();
        store.ApplyModelValue(new ExtractionValueEvent("vendor", "A", null, 1.5))!.Confidence.ShouldBe(1);
        store.ApplyModelValue(new ExtractionValueEvent("invoiceNumber", "B", null, double.NaN))!.Confidence.ShouldBe(0);
        store.ApplyModelValue(new ExtractionValueEvent("issueDate", "2026-09-03", null, -2))!.NeedsCheck.ShouldBeTrue();
        store.ApplyModelValue(new ExtractionValueEvent("vat", "1.00", null, 0.8))!.NeedsCheck.ShouldBeFalse();
        store.ApplyModelValue(new ExtractionValueEvent("total", "1.00", null, 0.7999))!.NeedsCheck.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Changed_Threshold_Applies_To_Unreviewed_Model_Values()
    {
        var threshold = 0.8;
        var store = Invoice(() => threshold);
        Play(store, ExtractionFixtures.InvoiceEvents());
        store.MarkChecked("iban").ShouldBeTrue();
        store.EditByUser("vat", "466.20");
        threshold = 0.95;
        store.RefreshThreshold();
        // dueDate and the sixteen cells (0.94); not the checked IBAN nor the edited VAT (0.96 anyway).
        store.CheckCount.ShouldBe(17);
        store.Find("iban")!.NeedsCheck.ShouldBeFalse();
        store.FieldValue("lineItems")!.NeedsCheck.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void An_Edit_Marks_The_Value_Edited_Clears_Its_Check_And_Parses_It_Again()
    {
        var store = PlayedInvoice();
        var due = store.EditByUser("dueDate", "2026-09-18")!;
        due.Status.ShouldBe(ExtractionValueStatus.Edited);
        due.NeedsCheck.ShouldBeFalse();
        due.TypedValue.ShouldBe(new DateOnly(2026, 9, 18));
        store.CheckCount.ShouldBe(1);
        // The model's confidence stays in the average.
        store.AverageConfidence.ShouldBe(23.27 / 25, 1e-9);

        var cell = store.EditByUser("lineItems.1.amount", "630,50")!;
        cell.TypedValue.ShouldBe(630.50m);
        cell.Status.ShouldBe(ExtractionValueStatus.Edited);
    }

    [AvaloniaFact]
    public void An_Edit_After_The_Part_Wrote_The_Text_Still_Counts()
    {
        var store = PlayedInvoice();
        var vendor = store.Find("vendor")!;
        vendor.Text = "Oakridge Ltd";
        store.EditByUser("vendor", vendor.Text).ShouldBeSameAs(vendor);
        vendor.Status.ShouldBe(ExtractionValueStatus.Edited);
        vendor.TypedValue.ShouldBe("Oakridge Ltd");
    }

    [AvaloniaFact]
    public void Text_That_Does_Not_Fit_Is_Invalid_Null_And_Listed_In_Schema_Order()
    {
        var store = PlayedInvoice();
        store.EditByUser("iban", "NL70 ZZTE 0417 2635 99");
        store.EditByUser("dueDate", "16 Oct");
        store.EditByUser("lineItems.2.quantity", "two");
        var due = store.Find("dueDate")!;
        due.Error.ShouldBe("Use YYYY-MM-DD");
        due.IsValid.ShouldBeFalse();
        due.TypedValue.ShouldBeNull();
        store.Find("iban")!.Error.ShouldBe("Check the IBAN");
        store.Find("lineItems.2.quantity")!.Error.ShouldBe("Enter a number");
        store.InvalidValues.Select(v => v.Path).ShouldBe(["dueDate", "lineItems.2.quantity", "iban"]);

        store.EditByUser("currency", "euro");
        store.Find("currency")!.Error.ShouldBe("Use a three-letter code");

        store.EditByUser("dueDate", "2026-09-17");
        due.Error.ShouldBeNull();
        due.TypedValue.ShouldBe(new DateOnly(2026, 9, 17));
        store.InvalidValues.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Error_Texts_Follow_The_Labels()
    {
        var store = PlayedInvoice();
        store.EditByUser("dueDate", "16 Oct");
        store.EditByUser("subtotal", "a lot");
        store.Labels = new DocumentExtractionLabels { DateError = "Datum als JJJJ-MM-TT", NumberError = "Zahl eingeben" };
        store.Find("dueDate")!.Error.ShouldBe("Datum als JJJJ-MM-TT");
        store.Find("subtotal")!.Error.ShouldBe("Zahl eingeben");
        store.Find("vendor")!.Error.ShouldBeNull();
        store.EditByUser("vat", "?");
        store.Find("vat")!.Error.ShouldBe("Zahl eingeben");
    }

    [AvaloniaFact]
    public void Looks_Right_Checks_Only_Values_That_Need_It()
    {
        var store = PlayedInvoice();
        store.MarkChecked("vendor").ShouldBeFalse();
        store.Find("vendor")!.Status.ShouldBe(ExtractionValueStatus.Filled);
        store.MarkChecked("iban").ShouldBeTrue();
        var iban = store.Find("iban")!;
        iban.Status.ShouldBe(ExtractionValueStatus.Checked);
        iban.NeedsCheck.ShouldBeFalse();
        store.CheckCount.ShouldBe(1);
        store.MarkChecked("iban").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Accept_All_Accepts_Every_Value_And_Undo_Brings_Back_What_Each_Was()
    {
        var store = PlayedInvoice();
        store.MarkChecked("iban");
        store.EditByUser("vendor", "Oakridge Ltd");
        store.AcceptAll().ShouldBe(25);
        store.Values.ShouldAllBe(v => v.Status == ExtractionValueStatus.Accepted && !v.NeedsCheck);
        store.AllAccepted.ShouldBeTrue();
        store.CheckCount.ShouldBe(0);
        store.FieldValues.ShouldAllBe(v => v.Status == ExtractionValueStatus.Accepted);
        store.AcceptAll().ShouldBe(0);

        store.UndoAccept().ShouldBe(25);
        store.Find("vendor")!.Status.ShouldBe(ExtractionValueStatus.Edited);
        store.Find("iban")!.Status.ShouldBe(ExtractionValueStatus.Checked);
        store.Find("dueDate")!.Status.ShouldBe(ExtractionValueStatus.Filled);
        store.Find("dueDate")!.NeedsCheck.ShouldBeTrue();
        store.CheckCount.ShouldBe(1);
        store.AllAccepted.ShouldBeFalse();
        store.FieldValue("lineItems")!.Status.ShouldBe(ExtractionValueStatus.Filled);
    }

    [AvaloniaFact]
    public void An_Edit_After_Accept_Returns_Only_That_Value_And_Survives_An_Undo()
    {
        var store = PlayedInvoice();
        store.AcceptAll();
        store.EditByUser("total", "2,686.21");
        store.Find("total")!.Status.ShouldBe(ExtractionValueStatus.Edited);
        store.AllAccepted.ShouldBeFalse();
        store.Values.Count(v => v.Status == ExtractionValueStatus.Accepted).ShouldBe(24);

        store.UndoAccept().ShouldBe(24);
        store.Find("total")!.Status.ShouldBe(ExtractionValueStatus.Edited);
        store.Find("vat")!.Status.ShouldBe(ExtractionValueStatus.Filled);
    }

    [AvaloniaFact]
    public void A_Committed_Accept_Cannot_Be_Undone_And_A_Second_Accept_Takes_The_Rest()
    {
        var store = PlayedInvoice();
        store.AcceptAll();
        store.CommitAccept();
        store.EditByUser("vendor", "Oakridge Ltd");
        store.AcceptAll().ShouldBe(1);
        store.UndoAccept().ShouldBe(1);
        store.Find("vendor")!.Status.ShouldBe(ExtractionValueStatus.Edited);
        store.Find("vat")!.Status.ShouldBe(ExtractionValueStatus.Accepted);
    }

    [AvaloniaFact]
    public void Pending_Values_Stay_Pending_When_A_Stopped_Run_Is_Accepted()
    {
        var store = Invoice();
        Play(store, ExtractionFixtures.InvoiceEvents().Take(6));
        store.FoundCount.ShouldBe(5);
        store.AcceptAll().ShouldBe(5);
        store.Find("total")!.Status.ShouldBe(ExtractionValueStatus.Pending);
        store.TableRows("lineItems")![0][1].Status.ShouldBe(ExtractionValueStatus.Pending);
        store.AllAccepted.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Clear_Empties_Everything_And_Removes_The_Rows()
    {
        var store = PlayedInvoice();
        store.EditByUser("dueDate", "16 Oct");
        var rows = store.TableRows("lineItems")!;
        var vendor = store.Find("vendor")!;
        ExtractionStoreChange? change = null;
        store.Changed += c => change = c;
        store.Flush();
        store.Clear();
        rows.ShouldBeEmpty();
        store.TableRows("lineItems").ShouldBeSameAs(rows);
        store.Find("vendor").ShouldBeSameAs(vendor);
        vendor.Status.ShouldBe(ExtractionValueStatus.Pending);
        vendor.Text.ShouldBe("");
        vendor.SourceId.ShouldBeNull();
        vendor.Confidence.ShouldBe(0);
        store.Find("dueDate")!.Error.ShouldBeNull();
        store.Find("dueDate")!.Note.ShouldBeNull();
        store.Values.Count.ShouldBe(9);
        store.FoundCount.ShouldBe(0);
        store.FieldCount.ShouldBe(10);
        store.CheckCount.ShouldBe(0);
        store.AverageConfidence.ShouldBe(0);
        store.InvalidValues.ShouldBeEmpty();
        store.FieldValue("lineItems")!.Status.ShouldBe(ExtractionValueStatus.Pending);
        store.Find("lineItems.0.amount").ShouldBeNull();

        store.Flush();
        change.ShouldNotBeNull();
        change.Cleared.ShouldBeTrue();
        change.RowsChanged.ShouldBeTrue();
        change.Paths.ShouldContain("vendor");
        change.Paths.ShouldContain("lineItems.3.amount");

        // A second run starts from scratch.
        Play(store, ExtractionFixtures.InvoiceEvents());
        store.FoundCount.ShouldBe(25);
        store.TableRows("lineItems")!.Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Values_Typed_Into_Empty_Fields_Count_As_Found_But_Not_In_The_Average()
    {
        var store = Invoice();
        store.EditByUser("vendor", "Oakridge Ceramics Ltd");
        store.FoundCount.ShouldBe(1);
        store.AverageConfidence.ShouldBe(0);
        store.ApplyModelValue(new ExtractionValueEvent("invoiceNumber", "OC-2026-1187", null, 0.5));
        store.AverageConfidence.ShouldBe(0.5);
    }

    [AvaloniaFact]
    public void Changes_Of_One_Dispatcher_Turn_Are_Reported_Once()
    {
        var store = Invoice();
        var changes = new List<ExtractionStoreChange>();
        store.Changed += changes.Add;
        Play(store, ExtractionFixtures.InvoiceEvents());
        changes.ShouldBeEmpty();
        Dispatcher.UIThread.RunJobs();
        var change = changes.ShouldHaveSingleItem();
        change.Paths.Count.ShouldBe(25);
        change.RowsChanged.ShouldBeTrue();
        change.Cleared.ShouldBeFalse();

        store.EditByUser("vat", "466.21");
        store.MarkChecked("iban");
        Dispatcher.UIThread.RunJobs();
        changes.Count.ShouldBe(2);
        changes[1].Paths.ShouldBe(["vat", "iban"], ignoreOrder: true);
        changes[1].RowsChanged.ShouldBeFalse();

        Dispatcher.UIThread.RunJobs();
        changes.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Flush_Reports_At_Once_And_Only_When_Something_Changed()
    {
        var posted = new List<Action>();
        var store = Invoice();
        store.Post = posted.Add;
        var changes = 0;
        store.Changed += _ => changes++;
        store.Flush();
        changes.ShouldBe(0);
        store.ApplyModelValue(new ExtractionValueEvent("vendor", "A", null, 0.9));
        store.ApplyModelValue(new ExtractionValueEvent("vat", "1.00", null, 0.9));
        posted.Count.ShouldBe(1);
        store.Flush();
        changes.ShouldBe(1);
        // The queued flush finds nothing left.
        posted[0]();
        changes.ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Output_Has_One_Entry_Per_Field_With_Typed_Values()
    {
        var store = PlayedInvoice();
        var output = store.BuildOutput();
        output.Values.Select(e => e.Key).ShouldBe(["vendor", "invoiceNumber", "issueDate", "dueDate", "lineItems", "currency", "subtotal", "vat", "total", "iban"]);
        var values = output.Values.ToDictionary(e => e.Key, e => e.Value);
        values["issueDate"].ShouldBe("2026-09-03");
        values["subtotal"].ShouldBe(2220.00m);
        values["iban"].ShouldBe("NL70ZZTE0417263598");
        var rows = values["lineItems"].ShouldBeAssignableTo<IReadOnlyList<IReadOnlyDictionary<string, object?>>>()!;
        rows.Count.ShouldBe(4);
        rows[0].Keys.ShouldBe(["description", "quantity", "unitPrice", "amount"]);
        rows[0]["amount"].ShouldBe(1280.00m);
        rows[0]["quantity"].ShouldBe(2m);
        rows[3]["description"].ShouldBe("Packing and freight");
    }

    [AvaloniaFact]
    public void Pending_Empty_And_Invalid_Values_Are_Null_In_The_Output()
    {
        var store = Invoice();
        var empty = store.BuildOutput().Values.ToDictionary(e => e.Key, e => e.Value);
        empty.Where(e => e.Key != "lineItems").ShouldAllBe(e => e.Value == null);
        empty["lineItems"].ShouldBeAssignableTo<IReadOnlyList<IReadOnlyDictionary<string, object?>>>()!.ShouldBeEmpty();

        store.ApplyModelValue(new ExtractionValueEvent("lineItems.0.amount", "1,280.00", null, 0.9));
        store.EditByUser("vendor", "   ");
        store.EditByUser("dueDate", "16 Oct");
        var values = store.BuildOutput().Values.ToDictionary(e => e.Key, e => e.Value);
        values["vendor"].ShouldBeNull();
        values["dueDate"].ShouldBeNull();
        var row = values["lineItems"].ShouldBeAssignableTo<IReadOnlyList<IReadOnlyDictionary<string, object?>>>()!.ShouldHaveSingleItem();
        row["description"].ShouldBeNull();
        row["amount"].ShouldBe(1280.00m);
        row.ContainsKey("unitPrice").ShouldBeTrue();
        row.ContainsKey("price").ShouldBeFalse();
    }
}
