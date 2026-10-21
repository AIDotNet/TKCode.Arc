using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>Clipboard and file formats: the contract with Excel, Google Sheets and Numbers.</summary>
public class DataGridInteropTests
{
    [Fact]
    public void Tsv_Quotes_Cells_With_Tabs_Line_Breaks_And_Quotes_And_Reads_Them_Back()
    {
        IReadOnlyList<IReadOnlyList<string>> matrix =
        [
            ["plain", "tab\there", "line\nbreak"],
            ["say \"hi\"", "", "last"]
        ];
        var tsv = DataGridClipboardText.ToTsv(matrix);
        tsv.ShouldBe("plain\t\"tab\there\"\t\"line\nbreak\"\n\"say \"\"hi\"\"\"\t\tlast");
        DataGridClipboardText.ParseTsv(tsv + "\r\n").ShouldBe(matrix.Select(r => r.ToList()).ToList());
    }

    [Fact]
    public void Html_Tables_Round_Trip_And_Excel_Markup_Is_Read()
    {
        IReadOnlyList<IReadOnlyList<string>> matrix = [["a & b", "<x>"], ["two\nlines", "$1,200"]];
        DataGridClipboardText.ParseHtmlTable(DataGridClipboardText.ToHtml(matrix)).ShouldBe(matrix.Select(r => r.ToList()).ToList());
        const string excel = """
            <html><head><style>td {mso-number-format:General}</style></head><body>
            <!--StartFragment--><table border=0 cellpadding=0>
             <col width=64>
             <tr height=20>
              <td height=20 class=xl65 align=right>1,234</td>
              <td colspan=2 class=xl66>Merged&nbsp;cell</td>
              <td><span style='mso-spacerun:yes'> </span>Lumen
              Labs</td>
             </tr>
            </table><!--EndFragment--></body></html>
            """;
        DataGridClipboardText.ParseHtmlTable(excel).ShouldBe([["1,234", "Merged cell", "", "Lumen Labs"]]);
        DataGridClipboardText.ParseHtmlTable("<p>no table</p>").ShouldBeNull();
    }

    [Fact]
    public void Cf_Html_Offsets_Point_At_The_Fragment()
    {
        var payload = DataGridClipboardText.WrapCfHtml("<table><tr><td>é</td></tr></table>");
        var bytes = Encoding.UTF8.GetBytes(payload);
        int Offset(string name)
        {
            var at = payload.IndexOf(name + ":", StringComparison.Ordinal) + name.Length + 1;
            return int.Parse(payload.AsSpan(at, 10));
        }
        Encoding.UTF8.GetString(bytes, Offset("StartFragment"), Offset("EndFragment") - Offset("StartFragment"))
            .ShouldBe("<table><tr><td>é</td></tr></table>");
        Encoding.UTF8.GetString(bytes, Offset("StartHTML"), Offset("EndHTML") - Offset("StartHTML")).ShouldStartWith("<html>");
        DataGridClipboardText.UnwrapCfHtml(payload).ShouldStartWith("<html>");
    }

    [Fact]
    public void Csv_Is_Rfc_4180_With_A_Byte_Order_Mark_And_Raw_Values()
    {
        using var stream = new MemoryStream();
        DataGridCsv.Write(stream, ["Account", "ARR", "Renews"],
        [
            ["Lumen, Labs", 1200.5m, new DateOnly(2026, 1, 31)],
            ["Say \"hi\"", null, null]
        ]);
        var bytes = stream.ToArray();
        bytes.Take(3).ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });
        Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
            .ShouldBe("Account,ARR,Renews\r\n\"Lumen, Labs\",1200.5,2026-01-31\r\n\"Say \"\"hi\"\"\",,");
    }

    private static XDocument Part(byte[] workbook, string name)
    {
        using var zip = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
        using var stream = zip.GetEntry(name)!.Open();
        return XDocument.Load(stream);
    }

    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void Xlsx_Writes_Typed_Cells_Formats_Freeze_And_Totals()
    {
        DataGridXlsxColumn[] columns =
        [
            new("Account", new DataGridCellFormat(DataGridColumnType.Text), DataGridAggregate.None, 188),
            new("ARR", new DataGridCellFormat(DataGridColumnType.Currency, 2, "€"), DataGridAggregate.Sum, 128),
            new("Health", new DataGridCellFormat(DataGridColumnType.Percent), DataGridAggregate.Average, 104),
            new("Renews", new DataGridCellFormat(DataGridColumnType.Date), DataGridAggregate.None, 132)
        ];
        DataGridExportNode[] rows =
        [
            DataGridExportNode.Row(["Lumen", 1200m, 50m, new DateOnly(2026, 1, 31)]),
            DataGridExportNode.Row(["Basalt", 800m, 70m, null])
        ];
        var bytes = DataGridXlsx.ToBytes(columns, rows, new DataGridXlsxOptions { SheetName = "Q1: plan/draft", FreezeColumns = 1, TotalLabel = "Total" });
        using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            zip.Entries.Select(e => e.FullName).ShouldBe(
                ["[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/worksheets/sheet1.xml", "xl/styles.xml"],
                ignoreOrder: true);
        }
        Part(bytes, "xl/workbook.xml").Descendants(Main + "sheet").Single().Attribute("name")!.Value.ShouldBe("Q1  plan draft");
        var sheet = Part(bytes, "xl/worksheets/sheet1.xml");
        var cells = sheet.Descendants(Main + "c").ToDictionary(c => c.Attribute("r")!.Value);
        cells["A1"].Attribute("t")!.Value.ShouldBe("inlineStr");
        cells["B2"].Element(Main + "v")!.Value.ShouldBe("1200");
        cells["C2"].Element(Main + "v")!.Value.ShouldBe("0.5");
        cells["D2"].Element(Main + "v")!.Value.ShouldBe("46053");
        cells.ContainsKey("D3").ShouldBeFalse();
        cells["A4"].Descendants(Main + "t").Single().Value.ShouldBe("Total");
        cells["B4"].Element(Main + "f")!.Value.ShouldBe("SUBTOTAL(9,B2:B3)");
        cells["B4"].Element(Main + "v")!.Value.ShouldBe("2000");
        cells["C4"].Element(Main + "f")!.Value.ShouldBe("SUBTOTAL(1,C2:C3)");
        var pane = sheet.Descendants(Main + "pane").Single();
        pane.Attribute("xSplit")!.Value.ShouldBe("1");
        pane.Attribute("ySplit")!.Value.ShouldBe("1");
        var formats = Part(bytes, "xl/styles.xml").Descendants(Main + "numFmt").Select(f => f.Attribute("formatCode")!.Value).ToList();
        formats.ShouldContain("\"€\"#,##0.00;-\"€\"#,##0.00");
        formats.ShouldContain("0%");
    }

    [Fact]
    public void Xlsx_Writes_Groups_As_Outlined_Rows_With_Subtotals()
    {
        DataGridXlsxColumn[] columns =
        [
            new("Region", new DataGridCellFormat(DataGridColumnType.Text)),
            new("ARR", new DataGridCellFormat(DataGridColumnType.Number), DataGridAggregate.Sum)
        ];
        DataGridExportNode[] nodes =
        [
            DataGridExportNode.Group("Region: Europe", [DataGridExportNode.Row(["Europe", 10m]), DataGridExportNode.Row(["Europe", 20m])]),
            DataGridExportNode.Group("Region: Asia", [DataGridExportNode.Row(["Asia", 5m])])
        ];
        var sheet = Part(DataGridXlsx.ToBytes(columns, nodes), "xl/worksheets/sheet1.xml");
        var rows = sheet.Descendants(Main + "row").ToList();
        rows.Select(r => r.Attribute("outlineLevel")?.Value).ShouldBe([null, null, "1", "1", null, "1", null]);
        var cells = sheet.Descendants(Main + "c").ToDictionary(c => c.Attribute("r")!.Value);
        cells["B2"].Element(Main + "f")!.Value.ShouldBe("SUBTOTAL(9,B3:B4)");
        cells["B2"].Element(Main + "v")!.Value.ShouldBe("30");
        cells["B5"].Element(Main + "f")!.Value.ShouldBe("SUBTOTAL(9,B6:B6)");
        cells["B7"].Element(Main + "f")!.Value.ShouldBe("SUBTOTAL(9,B2:B6)");
        cells["B7"].Element(Main + "v")!.Value.ShouldBe("35");
        sheet.Descendants(Main + "outlinePr").Single().Attribute("summaryBelow")!.Value.ShouldBe("0");
    }

    [Fact]
    public void Undo_Stack_Keeps_The_Limit_And_A_New_Step_Clears_Redo()
    {
        var stack = new DataGridUndoStack { Limit = 2 };
        DataGridChangeSet Step() => new() { Reason = DataGridChangeReason.Edit };
        var first = Step();
        stack.Push(first);
        stack.Push(Step());
        stack.Push(Step());
        stack.PastCount.ShouldBe(2);
        stack.PopUndo().ShouldNotBeNull();
        stack.CanRedo.ShouldBeTrue();
        stack.Push(Step());
        stack.CanRedo.ShouldBeFalse();
        stack.Limit = 0;
        stack.Push(Step());
        stack.CanUndo.ShouldBeFalse();
    }
}
