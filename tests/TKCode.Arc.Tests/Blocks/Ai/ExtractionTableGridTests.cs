using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ExtractionTableGrid"/>: the field box and header line, the grid (columns, widths wide and narrow, numbers
/// right-aligned, scrolling below 304), rows arriving live from the owner's list (with the smooth height spring and the fade
/// at Full motion, at once otherwise), the cells' states and landing, the linked tints, typing in a cell, the pointer's
/// paths, "Looks right" for a cell, focus and automation. The rows are the invoice's line items of
/// <see cref="ExtractionFixtures"/>.
/// </summary>
public class ExtractionTableGridTests
{
    private const double Frame = 1.0 / 60;

    private static IReadOnlyList<ExtractionValueEvent> Cells() =>
        ExtractionFixtures.InvoiceEvents().OfType<ExtractionValueEvent>().Where(e => e.Path.StartsWith("lineItems.", StringComparison.Ordinal)).ToList();

    /// <summary>The first <paramref name="count"/> cells of the line items (four per row).</summary>
    private static ExtractionFieldSamples Play(ExtractionFieldSamples form, int count)
    {
        foreach (var e in Cells().Take(count))
        {
            form.Apply(e);
        }
        return form;
    }

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    private static Color Tint(Control owner, double alpha)
    {
        var series = Token(owner, ArcDesignTokenKind.Series2);
        return Oklch.WithAlpha(series, series.A / 255.0 * alpha);
    }

    private static Rect BoundsIn(Visual visual, Visual relativeTo) =>
        new(visual.TranslatePoint(default, relativeTo)!.Value, visual.Bounds.Size);

    private static Border GridFrame(ExtractionTableGrid grid) => TestHelpers.Descendant<Border>(grid, "PART_Frame");

    private static TextBlock EmptyText(ExtractionTableGrid grid) => TestHelpers.Descendant<TextBlock>(grid, "PART_Empty");

    [AvaloniaFact]
    public void The_Field_Box_And_Its_Header_Line()
    {
        var form = ExtractionFieldSamples.InvoiceTable();
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        grid.Tone.ShouldBe(2);
        grid.Padding.ShouldBe(new Thickness(8, 4, 4, 6));
        grid.CornerRadius.ShouldBe(new CornerRadius(12));
        var head = TestHelpers.Descendant<Grid>(grid, "PART_Head");
        head.Bounds.Height.ShouldBe(30, 0.01);
        BoundsIn(head, grid).Y.ShouldBe(4, 0.01);
        var dot = TestHelpers.Descendant<Ellipse>(grid, ExtractionTableGrid.DotPart);
        dot.Bounds.Width.ShouldBe(7);
        BoundsIn(dot, grid).X.ShouldBe(8, 0.01);
        TestHelpers.ColorOf(dot.Fill).ShouldBe(Token(grid, ArcDesignTokenKind.Series2));
        var label = TestHelpers.Descendant<TextBlock>(grid, "PART_Label");
        label.Text.ShouldBe("Line items");
        label.FontWeight.ShouldBe(FontWeight.Medium);
        BoundsIn(label, grid).X.ShouldBe(8 + 7 + 8, 0.01);
        var count = TestHelpers.Descendant<TextBlock>(grid, "PART_Count");
        count.Text.ShouldBe("Table");
        TestHelpers.ColorOf(count.Foreground).ShouldBe(Token(grid, ArcDesignTokenKind.TextMuted));
        BoundsIn(count, grid).X.ShouldBe(BoundsIn(label, grid).Right + 8, 0.01);
        var summary = TestHelpers.Descendant<ConfidenceIndicator>(grid, "PART_Summary");
        summary.Kind.ShouldBe(ConfidenceKind.None);
        BoundsIn(summary, grid).Right.ShouldBe(410 - 4, 0.01);
        summary.Bounds.Width.ShouldBe(60);
        // The grid: a 1px Border, radius 10, Surface, right under the header line.
        var frame = GridFrame(grid);
        frame.BorderThickness.ShouldBe(new Thickness(1));
        frame.CornerRadius.ShouldBe(new CornerRadius(10));
        TestHelpers.ColorOf(frame.Background).ShouldBe(Token(grid, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(Token(grid, ArcDesignTokenKind.Border));
        BoundsIn(frame, grid).ShouldBe(new Rect(8, 34, 410 - 12, frame.Bounds.Height));
        grid.Bounds.Height.ShouldBe(4 + 30 + frame.Bounds.Height + 6, 0.01);
    }

    [AvaloniaFact]
    public void Column_Headers_And_The_Empty_Text()
    {
        var form = ExtractionFieldSamples.InvoiceTable();
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        var line = TestHelpers.Descendant<Border>(grid, "PART_HeaderLine");
        line.Bounds.Height.ShouldBe(28);
        line.BorderThickness.ShouldBe(new Thickness(0, 0, 0, 1));
        TestHelpers.ColorOf(line.BorderBrush).ShouldBe(Token(grid, ArcDesignTokenKind.BorderSubtle));
        var headers = grid.HeaderRow!.Children.OfType<ExtractionTableHeaderCell>().ToArray();
        headers.Select(h => h.Text).ShouldBe(["Description", "Qty", "Unit price", "Amount"]);
        headers.Select(h => h.TextAlignment).ShouldBe([TextAlignment.Start, TextAlignment.End, TextAlignment.End, TextAlignment.End]);
        headers[0].Padding.ShouldBe(new Thickness(9, 0));
        headers[0].FontSize.ShouldBe(12);
        TestHelpers.ColorOf(headers[0].Foreground).ShouldBe(Token(grid, ArcDesignTokenKind.TextMuted));
        var empty = EmptyText(grid);
        empty.IsVisible.ShouldBeTrue();
        empty.Text.ShouldBe("No rows yet");
        empty.Margin.ShouldBe(new Thickness(9, 10));
        empty.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(empty.Foreground).ShouldBe(Token(grid, ArcDesignTokenKind.TextMuted));
        grid.Classes.ShouldContain(ExtractionTableGrid.EmptyPseudoClass);
        grid.IsRunning = true;
        rig.Settle();
        empty.Text.ShouldBe("Rows appear here as they are found");
        grid.Classes.ShouldContain(ExtractionTableGrid.RunningPseudoClass);
        grid.IsRunning = false;
        rig.Settle();
        empty.Text.ShouldBe("No rows yet");
    }

    [AvaloniaFact]
    public void Rows_Arrive_Live_From_The_Owners_List()
    {
        var form = ExtractionFieldSamples.InvoiceTable();
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        grid.IsRunning = true;
        Play(form, 1);
        rig.Settle();
        grid.RowViews.Count.ShouldBe(1);
        grid.CountText.ShouldBe("1 row");
        EmptyText(grid).IsVisible.ShouldBeFalse();
        grid.Classes.ShouldNotContain(ExtractionTableGrid.EmptyPseudoClass);
        var first = grid.RowViews[0];
        first.Cells.Select(c => c.Input.Text).ShouldBe(["Glazed floor tiles, 20×20 cm", "", "", ""]);
        first.Cells.Select(c => c.Value!.Path).ShouldBe(["lineItems.0.description", "lineItems.0.quantity", "lineItems.0.unitPrice", "lineItems.0.amount"]);
        first.Bounds.Height.ShouldBe(32, 0.01);
        first.Classes.ShouldNotContain(ExtractionTableRow.SeparatedPseudoClass);
        // The rest of the row fills in place; the next row arrives with its first value.
        Play(form, 5);
        rig.Settle();
        first.Cells.Select(c => c.Input.Text).ShouldBe(["Glazed floor tiles, 20×20 cm", "2", "640.00", "1,280.00"]);
        grid.RowViews.Count.ShouldBe(2);
        grid.RowViews[0].ShouldBeSameAs(first);
        grid.CountText.ShouldBe("2 rows");
        var second = grid.RowViews[1];
        second.Classes.ShouldContain(ExtractionTableRow.SeparatedPseudoClass);
        second.BorderThickness.ShouldBe(new Thickness(0, 1, 0, 0));
        TestHelpers.ColorOf(second.BorderBrush).ShouldBe(Token(grid, ArcDesignTokenKind.BorderSubtle));
        second.Bounds.Height.ShouldBe(33, 0.01);
        Play(form, 16);
        rig.Settle();
        grid.RowViews.Count.ShouldBe(4);
        grid.CountText.ShouldBe("4 rows");
        // Header 28 + rows 32 + 3 × 33, inside the 1px border.
        GridFrame(grid).Bounds.Height.ShouldBe(1 + 28 + 32 + 3 * 33 + 1, 0.01);
        // The owner clears the values (Extract again): back to the empty text.
        form.TableRows["lineItems"].Clear();
        rig.Settle();
        grid.RowViews.Count.ShouldBe(0);
        grid.CountText.ShouldBe("Table");
        EmptyText(grid).IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Columns_Are_1fr_40_78_80_And_Narrow_126_38_76_78_At_The_358_Form()
    {
        var wide = ExtractionFieldSamples.InvoiceTable();
        Play(wide, 4);
        using (var rig = new ExtractionFieldRig(wide.Root))
        {
            var cells = wide.Table.RowViews[0].Cells;
            cells.Select(c => c.Bounds.Width).ShouldBe([410 - 14 - 198, 40, 78, 80]);
            cells.Select(c => c.Bounds.Height).ShouldAllBe(h => Math.Abs(h - 32) < 0.01);
            var headers = wide.Table.HeaderRow!.Children.Select(c => c.Bounds.Width).ToArray();
            headers.ShouldBe([410 - 14 - 198, 40, 78, 80]);
        }
        // The narrow form is 358 wide: its tab panel leaves 332 for the field (padding 0 12 20 14).
        var narrow = ExtractionFieldSamples.InvoiceTable(narrow: true, width: 332);
        Play(narrow, 4);
        using (var rig = new ExtractionFieldRig(narrow.Root))
        {
            narrow.Table.Classes.ShouldContain(ExtractionTableGrid.NarrowPseudoClass);
            narrow.Table.RowViews[0].Cells.Select(c => c.Bounds.Width).ShouldBe([126, 38, 76, 78]);
            TestHelpers.Descendant<ConfidenceIndicator>(narrow.Table, "PART_Summary").Bounds.Width.ShouldBe(54);
            var scroll = narrow.Table.Scroller!;
            scroll.Extent.Width.ShouldBe(scroll.Viewport.Width, 0.01);
            // Back to wide: the fixed columns are 2 wider again.
            narrow.Table.IsNarrow = false;
            rig.Settle();
            narrow.Table.RowViews[0].Cells.Select(c => c.Bounds.Width).ShouldBe([120, 40, 78, 80]);
        }
    }

    [AvaloniaFact]
    public void Column_Widths_By_Type()
    {
        var columns = new List<ExtractionColumn>
        {
            new() { Key = "item", Label = "Item", Type = ExtractionFieldType.Text },
            new() { Key = "sku", Label = "SKU", Type = ExtractionFieldType.Text },
            new() { Key = "date", Label = "Date", Type = ExtractionFieldType.Date },
            new() { Key = "currency", Label = "Cur.", Type = ExtractionFieldType.Currency },
            new() { Key = "qty", Label = "Qty", Type = ExtractionFieldType.Integer },
            new() { Key = "price", Label = "Price", Type = ExtractionFieldType.Money },
            new() { Key = "amount", Label = "Amount", Type = ExtractionFieldType.Money }
        };
        var (widths, numeric) = ExtractionTableGrid.Layout(columns, narrow: false);
        widths.Select(w => double.IsNaN(w) ? -1 : w).ShouldBe([-1, -1, 92, 56, 40, 78, 80]);
        numeric.ShouldBe([false, false, false, false, true, true, true]);
        var (narrow, _) = ExtractionTableGrid.Layout(columns, narrow: true);
        narrow.Select(w => double.IsNaN(w) ? -1 : w).ShouldBe([-1, -1, 90, 54, 38, 76, 78]);
        ExtractionTableRowPanel.Resolve(widths, 600).ShouldBe([127, 127, 92, 56, 40, 78, 80]);
        // Even a numeric first column takes the rest.
        ExtractionTableGrid.Layout([new ExtractionColumn { Type = ExtractionFieldType.Money }], false).Widths[0].ShouldBe(double.NaN);
    }

    [AvaloniaFact]
    public void Below_304_The_Grid_Scrolls_Sideways_In_Its_Frame()
    {
        var form = ExtractionFieldSamples.InvoiceTable(narrow: true, width: 280);
        Play(form, 16);
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        var scroll = grid.Scroller!;
        scroll.Viewport.Width.ShouldBe(280 - 14, 0.01);
        scroll.Extent.Width.ShouldBe(ExtractionTableGrid.MinTableWidth, 0.01);
        grid.RowViews[0].Cells.Select(c => c.Bounds.Width).ShouldBe([304 - 192, 38, 76, 78]);
        scroll.Offset = new Vector(20, 0);
        rig.Settle();
        scroll.Offset.X.ShouldBe(20, 0.01);
        // The field itself never grows past its slot (the frame clips).
        grid.Bounds.Width.ShouldBe(280);
        GridFrame(grid).ClipToBounds.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Numbers_Are_Right_Aligned_With_Tabular_Figures_In_28px_Cells()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 4);
        using var rig = new ExtractionFieldRig(form.Root);
        var inputs = form.Table.RowViews[0].Cells.Select(c => c.Input).ToArray();
        inputs.Select(i => i.TextAlignment).ShouldBe([TextAlignment.Start, TextAlignment.End, TextAlignment.End, TextAlignment.End]);
        inputs[0].Classes.ShouldNotContain(ExtractionInput.NumericPseudoClass);
        inputs[3].Classes.ShouldContain(ExtractionInput.NumericPseudoClass);
        inputs[3].FontFeatures.ShouldNotBeNull().ShouldContain(f => f.Tag == "tnum");
        foreach (var input in inputs)
        {
            input.Classes.ShouldContain(ExtractionInput.CellPseudoClass);
            input.Bounds.Height.ShouldBe(28, 0.01);
            input.Padding.ShouldBe(new Thickness(7, 0));
            input.CornerRadius.ShouldBe(new CornerRadius(7));
            input.FontSize.ShouldBe(12);
            input.LineHeight.ShouldBe(16.8);
        }
        // The text of a right-aligned cell ends at the cell's right edge less 2 + 7 + 1.
        var amount = inputs[3];
        var display = TestHelpers.Descendant<TextBlock>(amount, "PART_Display");
        display.TextAlignment.ShouldBe(TextAlignment.End);
        BoundsIn(display, form.Table.RowViews[0].Cells[3]).Right.ShouldBe(80 - 2 - 7 - 1, 0.01);
    }

    [AvaloniaFact]
    public void Cells_Have_The_Field_States_And_A_Low_Cell_Carries_Its_Note()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 2);
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        var cells = grid.RowViews[0].Cells;
        var foreground = Token(grid, ArcDesignTokenKind.Foreground);
        cells[2].Input.Look.ShouldBe(ExtractionInputLook.Empty);
        TestHelpers.ColorOf(cells[2].Input.Background).ShouldBe(Oklch.WithAlpha(foreground, foreground.A / 255.0 * 0.04));
        cells[1].Input.Look.ShouldBe(ExtractionInputLook.Filled);
        TestHelpers.ColorOf(cells[1].Input.Background).A.ShouldBe((byte)0);

        form.Apply(new ExtractionValueEvent("lineItems.0.unitPrice", "640.00", "li0u", 0.62, "Two prices are printed for the batch."));
        rig.Settle();
        var low = cells[2];
        low.Input.Look.ShouldBe(ExtractionInputLook.Low);
        TestHelpers.ColorOf(low.Input.Background).ShouldBe(Oklch.MixOklab(Token(grid, ArcDesignTokenKind.Surface), Token(grid, ArcDesignTokenKind.Warning), 0.06));
        Tooltip.GetTip(low).ShouldBe("Two prices are printed for the batch.");
        AutomationProperties.GetHelpText(low.Input).ShouldBe("Two prices are printed for the batch.");
        form.Check("lineItems.0.unitPrice");
        rig.Settle();
        Tooltip.GetTip(low).ShouldBeNull();
        low.Input.Look.ShouldBe(ExtractionInputLook.Filled);

        form.Edit("lineItems.0.quantity", "two", "Enter a number");
        rig.Settle();
        cells[1].Input.Look.ShouldBe(ExtractionInputLook.Invalid);
        TestHelpers.ColorOf(cells[1].Input.BorderBrush).ShouldBe(Oklch.MixOklab(Token(grid, ArcDesignTokenKind.Border), Token(grid, ArcDesignTokenKind.Danger), 0.6));
        AutomationProperties.GetHelpText(cells[1].Input).ShouldBe("Enter a number");

        cells[0].Input.Focus(NavigationMethod.Tab);
        rig.Settle();
        cells[0].Input.Look.ShouldBe(ExtractionInputLook.Focused);
        TestHelpers.ColorOf(cells[0].Input.BorderBrush).ShouldBe(Token(grid, ArcDesignTokenKind.BorderStrong));
    }

    [AvaloniaFact]
    public void A_Cells_Value_Lands()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 1);
        using var rig = new ExtractionFieldRig(form.Root, ArcMotionLevel.Full);
        var cell = form.Table.RowViews[0].Cells[1];
        cell.Input.Classes.ShouldNotContain(ExtractionInput.LandedPseudoClass);
        Play(form, 2);
        rig.Settle();
        cell.Input.Text.ShouldBe("2");
        cell.Input.Classes.ShouldContain(ExtractionInput.LandedPseudoClass);
    }

    [AvaloniaFact]
    public void The_Linked_Path_Tints_The_Field_The_Row_Or_The_Cell()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 16);
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        TestHelpers.ColorOf(grid.Background).A.ShouldBe((byte)0);

        grid.LinkedPath = "lineItems";
        rig.Settle();
        grid.Classes.ShouldContain(ExtractionTableGrid.LinkedPseudoClass);
        TestHelpers.ColorOf(grid.Background).ShouldBe(Tint(grid, 0.06));
        TestHelpers.ColorOf(TestHelpers.Descendant<Border>(grid, "PART_Wrapper").Background).ShouldBe(Tint(grid, 0.06));
        grid.RowViews.ShouldAllBe(r => TestHelpers.ColorOf(r.Background).A == 0);

        grid.LinkedPath = "lineItems.2.amount";
        rig.Settle();
        TestHelpers.ColorOf(grid.Background).ShouldBe(Tint(grid, 0.06));
        var cell = (ExtractionTableCell)grid.FindCell("lineItems.2.amount")!;
        TestHelpers.ColorOf(cell.Background).ShouldBe(Tint(grid, 0.12));
        // The lit cell's box stays text (the tint shows through).
        cell.Input.Look.ShouldBe(ExtractionInputLook.Filled);
        TestHelpers.ColorOf(grid.RowViews[2].Background).A.ShouldBe((byte)0);

        grid.LinkedPath = "lineItems.1";
        rig.Settle();
        TestHelpers.ColorOf(cell.Background).A.ShouldBe((byte)0);
        TestHelpers.ColorOf(grid.RowViews[1].Background).ShouldBe(Tint(grid, 0.12));

        grid.LinkedPath = "vendor";
        rig.Settle();
        grid.Classes.ShouldNotContain(ExtractionTableGrid.LinkedPseudoClass);
        TestHelpers.ColorOf(grid.Background).A.ShouldBe((byte)0);
        TestHelpers.ColorOf(grid.RowViews[1].Background).A.ShouldBe((byte)0);

        // A path that starts like the key but is another field is not the table's.
        grid.LinkedPath = "lineItemsTotal";
        rig.Settle();
        TestHelpers.ColorOf(grid.Background).A.ShouldBe((byte)0);
    }

    [AvaloniaFact]
    public void Typing_In_A_Cell_Writes_Its_Value_And_Raises_ValueEdited()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 8);
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        var edits = new List<ExtractionValueChangedEventArgs>();
        var focused = new List<string?>();
        grid.ValueEdited += (_, e) => edits.Add(e);
        grid.PathFocused += (_, e) => focused.Add(e.Path);
        grid.FocusCell("lineItems.1.quantity").ShouldBeTrue();
        rig.Settle();
        focused.ShouldBe(["lineItems.1.quantity"]);
        rig.Type("0");
        var value = form.Values["lineItems.1.quantity"];
        value.Text.ShouldBe("30");
        edits.Count.ShouldBe(1);
        edits[0].Path.ShouldBe("lineItems.1.quantity");
        edits[0].OldText.ShouldBe("3");
        edits[0].NewText.ShouldBe("30");
        edits[0].Origin.ShouldBe(ExtractionOrigin.User);
        grid.FocusCell("lineItems.9.amount").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Pointer_Raises_The_Cell_Path_Or_The_Tables_Key()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 16);
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        var log = new List<string>();
        grid.PathEntered += (_, e) => log.Add("enter " + e.Path);
        grid.PathExited += (_, e) => log.Add("exit " + e.Path);
        rig.Hover(grid.FindCell("lineItems.2.amount")!);
        grid.HoverPath.ShouldBe("lineItems.2.amount");
        rig.Hover(grid.FindCell("lineItems.2.unitPrice")!);
        rig.Hover(TestHelpers.Descendant<TextBlock>(grid, "PART_Label"));
        rig.Leave();
        log.ShouldBe([
            "enter lineItems.2.amount", "exit lineItems.2.amount", "enter lineItems.2.unitPrice", "exit lineItems.2.unitPrice",
            "enter lineItems", "exit lineItems"
        ]);
        grid.HoverPath.ShouldBeNull();
        // The grid lights nothing itself.
        grid.LinkedPath.ShouldBeNull();
    }

    [AvaloniaFact]
    public void ConfirmCell_Asks_For_A_Cell_And_Changes_Nothing()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 4);
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        var asked = new List<string?>();
        grid.LooksRightRequested += (_, e) => asked.Add(e.Path);
        grid.ConfirmCell("lineItems.0.amount").ShouldBeTrue();
        grid.ConfirmCell("lineItems.4.amount").ShouldBeFalse();
        grid.ConfirmCell("vendor").ShouldBeFalse();
        asked.ShouldBe(["lineItems.0.amount"]);
        form.Values["lineItems.0.amount"].Status.ShouldBe(ExtractionValueStatus.Filled);
    }

    [AvaloniaFact]
    public void A_New_Row_Opens_On_The_Smooth_Spring_And_Fades_In()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 4);
        using var rig = new ExtractionFieldRig(form.Root, ArcMotionLevel.Full);
        var grid = form.Table;
        var size = grid.SizeDecorator!;
        var start = size.Bounds.Height;
        start.ShouldBe(28 + 32, 0.01);
        Play(form, 5);
        rig.Settle();
        grid.RowViews.Count.ShouldBe(2);
        var row = grid.RowViews[1];
        row.Opacity.ShouldBe(0, 0.001);
        size.IsMoving.ShouldBeTrue();
        rig.Frames(6 * 1000.0 / 60);
        var (displacement, _) = SpringSolver.Evaluate(ArcMotionPresets.Smooth, -33, 0, 6 * Frame);
        size.Bounds.Height.ShouldBe(start + 33 + displacement, 1);
        size.Bounds.Height.ShouldBeInRange(start + 5, start + 30);
        row.Opacity.ShouldBeInRange(0.3, 0.99);
        size.Clip.ShouldNotBeNull();
        rig.Frames(100);
        row.Opacity.ShouldBe(1, 0.001);
        rig.Frames(600);
        size.Bounds.Height.ShouldBe(start + 33, 0.01);
        size.IsMoving.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Without_Full_Motion_A_New_Row_Is_There_At_Once()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var form = Play(ExtractionFieldSamples.InvoiceTable(), 4);
            using var rig = new ExtractionFieldRig(form.Root, level);
            var grid = form.Table;
            var start = grid.SizeDecorator!.Bounds.Height;
            Play(form, 5);
            rig.Settle();
            grid.RowViews[1].Opacity.ShouldBe(1);
            grid.SizeDecorator.IsMoving.ShouldBeFalse();
            grid.SizeDecorator.Bounds.Height.ShouldBe(start + 33, 0.01);
        }
    }

    [AvaloniaFact]
    public void The_Header_Shows_The_Mean_Of_The_Cells()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 16);
        using var rig = new ExtractionFieldRig(form.Root);
        var summary = TestHelpers.Descendant<ConfidenceIndicator>(form.Table, "PART_Summary");
        summary.Text.ShouldBe("94%");
        form.Table.SummaryAutomationText = "Confidence 94 percent";
        AutomationProperties.GetName(summary).ShouldBe("Confidence 94 percent");
        form.Apply(new ExtractionValueEvent("lineItems.0.amount", "1,280.00", "li0a", 0.62));
        rig.Settle();
        summary.Text.ShouldBe("92%");
        form.Table.IsAccepted = true;
        rig.Settle();
        summary.Kind.ShouldBe(ConfidenceKind.Accepted);
    }

    [AvaloniaFact]
    public void Automation_Names_The_Table_Its_Headers_Rows_And_Cells()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 8);
        using var rig = new ExtractionFieldRig(form.Root);
        var grid = form.Table;
        var peer = ControlAutomationPeer.CreatePeerForElement(grid);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        peer.GetName().ShouldBe("Line items");
        var body = TestHelpers.Descendant<StackPanel>(grid, ExtractionTableGrid.BodyPart);
        AutomationProperties.GetControlTypeOverride(body).ShouldBe(AutomationControlType.Table);
        AutomationProperties.GetName(body).ShouldBe("Line items");
        AutomationProperties.GetControlTypeOverride(TestHelpers.Descendant<Border>(grid, "PART_HeaderLine")).ShouldBe(AutomationControlType.Header);
        grid.HeaderRow!.Children.ShouldAllBe(h => AutomationProperties.GetControlTypeOverride(h) == AutomationControlType.HeaderItem);
        grid.RowViews.ShouldAllBe(r => AutomationProperties.GetControlTypeOverride(r) == AutomationControlType.DataItem);
        AutomationProperties.GetName(grid.RowViews[1].Cells[1].Input).ShouldBe("Qty, row 2");
        AutomationProperties.GetName(grid.RowViews[0].Cells[0].Input).ShouldBe("Description, row 1");
        grid.CellAutomationName = (column, row) => $"{column} {row}";
        rig.Settle();
        AutomationProperties.GetName(grid.RowViews[1].Cells[1].Input).ShouldBe("Qty 2");
        // Tab goes through the cells row by row.
        grid.FocusCell("lineItems.0.amount", NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.Focused.ShouldBe(grid.FindCell("lineItems.1.description") is ExtractionTableCell c ? c.Input : null);
    }

    [AvaloniaFact]
    public void Back_In_A_Window_The_Cells_Are_Kept()
    {
        var form = Play(ExtractionFieldSamples.InvoiceTable(), 8);
        var host = new Border { Child = form.Root };
        using var rig = new ExtractionFieldRig(host);
        var grid = form.Table;
        var cell = grid.FindCell("lineItems.1.description");
        host.Child = null;
        rig.Settle();
        form.Apply(new ExtractionValueEvent("lineItems.1.quantity", "4", "li1q", 0.9));
        host.Child = form.Root;
        rig.Settle();
        grid.FindCell("lineItems.1.description").ShouldBeSameAs(cell);
        ((ExtractionTableCell)grid.FindCell("lineItems.1.quantity")!).Input.Text.ShouldBe("4");
        // A row added while away shows when back.
        form.Apply(new ExtractionValueEvent("lineItems.2.description", "Kiln firing service", "li2d", 0.94));
        rig.Settle();
        grid.RowViews.Count.ShouldBe(3);
    }
}
