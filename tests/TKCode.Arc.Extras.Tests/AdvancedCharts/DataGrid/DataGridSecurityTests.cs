using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>
/// Hostile input and hostile output: text that a spreadsheet would run as a formula, and clipboard tables built to
/// exhaust memory or time. These guard what the grid hands to other programs and what it accepts from them.
/// </summary>
public class DataGridSecurityTests
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+49 170 1234", "'+49 170 1234")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1:A9)", "'@SUM(A1:A9)")]
    [InlineData("\tcmd", "'\tcmd")]
    [InlineData("\rcmd", "'\rcmd")]
    [InlineData("plain", "plain")]
    [InlineData("a=b", "a=b")]
    [InlineData(" =1", " =1")]
    [InlineData("", "")]
    public void NeutralizeFormula_Prefixes_Only_Text_That_Starts_Like_A_Formula(string text, string expected) =>
        DataGridCsv.NeutralizeFormula(text).ShouldBe(expected);

    [Fact]
    public void Csv_Neutralizes_Formula_Text_In_Headers_And_Cells_But_Not_Numbers_Or_Days()
    {
        var csv = DataGridCsv.ToCsv(["=Name", "Amount", "Day"],
        [
            ["=HYPERLINK(\"http://evil\",\"x\")", -5m, new DateOnly(2026, 1, 31)],
            ["@a,b", 12.5m, null]
        ]);
        csv.ShouldBe("'=Name,Amount,Day\r\n\"'=HYPERLINK(\"\"http://evil\"\",\"\"x\"\")\",-5,2026-01-31\r\n\"'@a,b\",12.5,");
    }

    [Fact]
    public void Csv_Leaves_Text_Alone_When_Neutralizing_Is_Off()
    {
        var csv = DataGridCsv.ToCsv(["=Name"], [["=1+1"]], new DataGridCsvOptions { NeutralizeFormulas = false });
        csv.ShouldBe("=Name\r\n=1+1");
    }

    [AvaloniaFact]
    public void The_Grid_Neutralizes_Exported_Formula_Text_Unless_Told_Not_To()
    {
        ObservableCollection<DataGridRow> rows =
        [
            new("r1") { { "account", "=1+1" }, { "region", "Europe" }, { "arr", -3m } }
        ];
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        grid.NeutralizeCsvFormulas.ShouldBeTrue();
        Csv(grid).ShouldContain("\r\n'=1+1,Europe,-3,");
        grid.NeutralizeCsvFormulas = false;
        Csv(grid).ShouldContain("\r\n=1+1,Europe,-3,");
    }

    private static string Csv(TKCode.Arc.DataGrid grid)
    {
        grid.EnsureView();
        using var stream = new MemoryStream();
        grid.WriteExport(stream, DataGridExportFormat.Csv, grid.View.Filtered.Select(i => grid.Store.ItemAt(i)).ToArray(), grouped: false);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void Xlsx_Writes_Formula_Text_As_An_Inline_String_Never_As_A_Formula()
    {
        DataGridXlsxColumn[] columns = [new("=Name", new DataGridCellFormat(DataGridColumnType.Text), DataGridAggregate.None, 120)];
        var bytes = DataGridXlsx.ToBytes(columns, [DataGridExportNode.Row(["=1+1"]), DataGridExportNode.Row(["@SUM(A1)"])],
            new DataGridXlsxOptions { TotalLabel = "Total", Totals = false });
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var sheet = XDocument.Load(stream);
        sheet.Descendants(Main + "f").ShouldBeEmpty();
        var cells = sheet.Descendants(Main + "c").ToDictionary(c => c.Attribute("r")!.Value);
        foreach (var reference in new[] { "A1", "A2", "A3" })
        {
            cells[reference].Attribute("t")!.Value.ShouldBe("inlineStr");
        }
        cells["A2"].Value.ShouldBe("=1+1");
    }

    [Fact]
    public void ParseTsv_Stops_At_The_Cell_Limit()
    {
        var tsv = string.Join('\n', Enumerable.Range(0, 1000).Select(_ => string.Join('\t', Enumerable.Repeat("x", 1000))));
        var table = DataGridClipboardText.ParseTsv(tsv, 500);
        table.Sum(r => r.Count).ShouldBe(500);
        DataGridClipboardText.ParseTsv("a\tb\nc\td", 4).Sum(r => r.Count).ShouldBe(4);
        DataGridClipboardText.ParseTsv("a\tb\nc\td", 3).Sum(r => r.Count).ShouldBe(3);
        DataGridClipboardText.ParseTsv("a\tb\nc\td").Sum(r => r.Count).ShouldBe(4);
        DataGridClipboardText.ParseTsv("").ShouldBe([[""]]);
    }

    [Fact]
    public void ParseHtmlTable_Counts_Colspan_Padding_Against_The_Limit()
    {
        var html = "<table><tr>" + string.Concat(Enumerable.Repeat("<td colspan=\"999999\">x</td>", 5000)) + "</tr></table>";
        var stopwatch = Stopwatch.StartNew();
        var table = DataGridClipboardText.ParseHtmlTable(html, 10_000)!;
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        table.Sum(r => r.Count).ShouldBe(10_000);
        DataGridClipboardText.ParseHtmlTable("<table><tr><td>a</td><td colspan=3>b</td></tr></table>", 4)!
            .ShouldBe([["a", "b", "", ""]]);
    }

    [Fact]
    public void Random_And_Malformed_Clipboards_Never_Throw_And_Respect_The_Limit()
    {
        string[] pieces =
        [
            "<table>", "</table>", "<tr>", "</tr>", "<td>", "</td>", "<th colspan=\"999999\">", "<td colspan='", "<td colspan=-5>",
            "<td colspan=99999999999999999999>", "&amp;", "&#x", "&#xD800;", "<!--", "-->", "<script>", "</script>", "<style>", "<br>",
            "\t", "\n", "\r", "\"", "x", "<", ">", "\ud800", "é", " ", "Version:0.9\r\nStartHTML:", "<p>", "</p>", "</div>"
        ];
        var random = new Random(20261021);
        var stopwatch = Stopwatch.StartNew();
        for (var run = 0; run < 3000; run++)
        {
            var builder = new StringBuilder();
            for (var i = random.Next(0, 200); i > 0; i--)
            {
                builder.Append(pieces[random.Next(pieces.Length)]);
            }
            var text = builder.ToString();
            var limit = random.Next(1, 60);
            var table = DataGridClipboardText.ParseHtmlTable(text, limit);
            (table?.Sum(r => r.Count) ?? 0).ShouldBeLessThanOrEqualTo(limit);
            DataGridClipboardText.ParseTsv(text, limit).Sum(r => r.Count).ShouldBeLessThanOrEqualTo(limit);
            DataGridClipboardText.UnwrapCfHtml(text).ShouldNotBeNull();
            DataGridClipboardText.ParseHtmlTable(text);
            DataGridClipboardText.ParseTsv(text);
        }
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Huge_Single_Inputs_Are_Read_In_Linear_Time()
    {
        var inputs = new (string Name, Func<object?> Run)[]
        {
            ("5 MB cell in TSV", () => DataGridClipboardText.ParseTsv(new string('a', 5_000_000))),
            ("5 MB quoted cell in TSV", () => DataGridClipboardText.ParseTsv("\"" + new string('a', 5_000_000) + "\"")),
            ("200k quotes in TSV", () => DataGridClipboardText.ParseTsv("\"" + string.Concat(Enumerable.Repeat("\"\"", 200_000)))),
            ("5 MB cell in HTML", () => DataGridClipboardText.ParseHtmlTable("<table><tr><td>" + new string('a', 5_000_000) + "</td></tr></table>")),
            ("200k nested tables", () => DataGridClipboardText.ParseHtmlTable(string.Concat(Enumerable.Repeat("<table>", 200_000)))),
            ("1M open angle brackets", () => DataGridClipboardText.ParseHtmlTable("<table>" + new string('<', 1_000_000))),
            ("200k unclosed comments", () => DataGridClipboardText.ParseHtmlTable("<table>" + string.Concat(Enumerable.Repeat("<!--", 200_000)))),
            ("200k small cells in HTML", () => DataGridClipboardText.ParseHtmlTable("<table><tr>" + string.Concat(Enumerable.Repeat("<td>x", 200_000)))),
            ("truncated CF_HTML", () => DataGridClipboardText.UnwrapCfHtml("Version:0.9\r\nStartHTML:00000000"))
        };
        foreach (var (name, run) in inputs)
        {
            var stopwatch = Stopwatch.StartNew();
            Should.NotThrow(() => run(), name);
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10), name);
        }
    }

    [AvaloniaFact]
    public void A_Paste_Over_The_Cell_Limit_Is_Refused_With_A_Message()
    {
        var rows = DataGridBehaviorTests.Rows();
        var grid = DataGridBehaviorTests.Create(rows);
        DataGridBehaviorTests.Show(grid);
        grid.MaxPasteCells.ShouldBe(1_000_000);
        grid.MaxPasteCells = 5;
        grid.Select(new DataGridCellAddress(0, 2));
        grid.Paste([["1", "2", "3"], ["4", "5", "6"]]);
        rows[0]["arr"].ShouldBe(1200m);
        grid.StatusBar!.MessageText!.ShouldContain("more than 5 cells");
        grid.Paste([["1", "2", "3"], ["4", "5"]]);
        rows[0]["arr"].ShouldBe(1m);
        grid.Paste([]);
    }
}
