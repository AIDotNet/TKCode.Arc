using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace TKCode.Arc;

/// <summary>A column of an xlsx export.</summary>
/// <param name="Header">Header text (row 1, bold).</param>
/// <param name="Format">How values are typed and formatted.</param>
/// <param name="Aggregate">What the totals row and group rows show (SUBTOTAL formulas).</param>
/// <param name="Width">Column width in px (converted to Excel character widths).</param>
public sealed record DataGridXlsxColumn(string Header, DataGridCellFormat Format, DataGridAggregate Aggregate = DataGridAggregate.None, double Width = 140);

/// <summary>A row of an xlsx export, or a group of rows written as an outlined group with subtotals.</summary>
public sealed class DataGridExportNode
{
    private DataGridExportNode(IReadOnlyList<object?>? values, string? label, IReadOnlyList<DataGridExportNode>? children)
    {
        Values = values;
        Label = label;
        Children = children;
    }

    /// <summary>The cell values of a row (normalized: string, decimal, DateOnly or null); null for a group.</summary>
    public IReadOnlyList<object?>? Values { get; }

    /// <summary>The label of a group; null for a row.</summary>
    public string? Label { get; }

    /// <summary>The rows and subgroups of a group; null for a row.</summary>
    public IReadOnlyList<DataGridExportNode>? Children { get; }

    /// <summary>True for a group.</summary>
    public bool IsGroup => Children is not null;

    /// <summary>A data row.</summary>
    public static DataGridExportNode Row(IReadOnlyList<object?> values) => new(values, null, null);

    /// <summary>A group of rows.</summary>
    public static DataGridExportNode Group(string label, IReadOnlyList<DataGridExportNode> children) => new(null, label, children);
}

/// <summary>Options of <see cref="DataGridXlsx.Write"/>.</summary>
public sealed record DataGridXlsxOptions
{
    /// <summary>Worksheet name (characters Excel refuses are replaced, cut to 31 characters).</summary>
    public string SheetName { get; init; } = "Sheet1";

    /// <summary>Columns frozen on the left (pinned columns); the header row is always frozen.</summary>
    public int FreezeColumns { get; init; }

    /// <summary>Adds a bold totals row with SUBTOTAL formulas under the data.</summary>
    public bool Totals { get; init; } = true;

    /// <summary>Label of the totals row when the first column has no aggregate.</summary>
    public string TotalLabel { get; init; } = "Total";
}

/// <summary>
/// Writes an .xlsx workbook (ECMA-376) with no dependencies: typed numbers, currency, percent and date formats, column
/// widths, a frozen header and pinned columns, a bold totals row, and groups as outlined rows whose SUBTOTAL formulas
/// Excel never counts twice. Pure functions, usable on any thread.
/// </summary>
public static class DataGridXlsx
{
    private const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>Writes the workbook to <paramref name="stream"/>.</summary>
    public static void Write(Stream stream, IReadOnlyList<DataGridXlsxColumn> columns, IReadOnlyList<DataGridExportNode> nodes,
                             DataGridXlsxOptions? options = null)
    {
        options ??= new DataGridXlsxOptions();
        var styles = new Styles();
        var sheet = BuildSheet(columns, nodes, options, styles);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        Add(zip, "[Content_Types].xml", Declaration +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>");
        Add(zip, "_rels/.rels", Declaration +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"" + RelNs + "/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Add(zip, "xl/workbook.xml", Declaration +
            "<workbook xmlns=\"" + MainNs + "\" xmlns:r=\"" + RelNs + "\"><sheets><sheet name=\"" + Xml(SheetName(options.SheetName)) +
            "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Add(zip, "xl/_rels/workbook.xml.rels", Declaration +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"" + RelNs + "/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"" + RelNs + "/styles\" Target=\"styles.xml\"/></Relationships>");
        Add(zip, "xl/worksheets/sheet1.xml", sheet);
        Add(zip, "xl/styles.xml", styles.Xml());
    }

    /// <summary>The workbook as bytes.</summary>
    public static byte[] ToBytes(IReadOnlyList<DataGridXlsxColumn> columns, IReadOnlyList<DataGridExportNode> nodes, DataGridXlsxOptions? options = null)
    {
        using var memory = new MemoryStream();
        Write(memory, columns, nodes, options);
        return memory.ToArray();
    }

    /// <summary>The Excel number format of a column, or of its aggregate.</summary>
    public static string? NumberFormat(DataGridCellFormat format, DataGridAggregate aggregate = DataGridAggregate.None)
    {
        if (aggregate == DataGridAggregate.Count)
        {
            return "#,##0";
        }
        var decimals = Math.Clamp(format.Type == DataGridColumnType.Percent && aggregate == DataGridAggregate.Average
            ? Math.Max(1, format.Decimals)
            : format.Decimals, 0, 20);
        var fraction = decimals > 0 ? "." + new string('0', decimals) : string.Empty;
        return format.Type switch
        {
            DataGridColumnType.Currency =>
                $"\"{format.CurrencySymbol.Replace("\"", "\"\"")}\"#,##0{fraction};-\"{format.CurrencySymbol.Replace("\"", "\"\"")}\"#,##0{fraction}",
            DataGridColumnType.Percent => "0" + fraction + "%",
            DataGridColumnType.Number => "#,##0" + fraction,
            DataGridColumnType.Date => null,
            _ => null
        };
    }

    private static string BuildSheet(IReadOnlyList<DataGridXlsxColumn> columns, IReadOnlyList<DataGridExportNode> nodes,
                                     DataGridXlsxOptions options, Styles styles)
    {
        var rows = new List<string>();
        var current = 1;
        var maxLevel = 0;
        var header = new StringBuilder();
        for (var c = 0; c < columns.Count; c++)
        {
            header.Append(InlineString(Ref(c, 1), columns[c].Header, styles.Get(null, bold: true)));
        }
        rows.Add(Row(1, header.ToString(), 0));

        void WriteNode(DataGridExportNode node, int level)
        {
            if (!node.IsGroup)
            {
                current++;
                var cells = new StringBuilder();
                for (var c = 0; c < columns.Count; c++)
                {
                    var value = node.Values is { } values && c < values.Count ? values[c] : null;
                    cells.Append(Cell(columns[c], c, current, value, styles));
                }
                rows.Add(Row(current, cells.ToString(), level));
                return;
            }
            maxLevel = Math.Max(maxLevel, level + 1);
            current++;
            var headerRow = current;
            var slot = rows.Count;
            rows.Add(string.Empty);
            foreach (var child in node.Children!)
            {
                WriteNode(child, level + 1);
            }
            var last = current;
            var leaves = Leaves(node).ToList();
            var groupCells = new StringBuilder();
            for (var c = 0; c < columns.Count; c++)
            {
                groupCells.Append(c == 0
                    ? InlineString(Ref(0, headerRow), node.Label ?? string.Empty, styles.Get(null, bold: true))
                    : Subtotal(columns[c], c, headerRow, headerRow + 1, last, leaves, styles));
            }
            rows[slot] = Row(headerRow, groupCells.ToString(), level);
        }

        foreach (var node in nodes)
        {
            WriteNode(node, 0);
        }
        var lastData = current;
        if (options.Totals && columns.Any(c => c.Aggregate != DataGridAggregate.None))
        {
            current++;
            var leaves = nodes.SelectMany(Leaves).ToList();
            var totals = new StringBuilder();
            for (var c = 0; c < columns.Count; c++)
            {
                totals.Append(c == 0 && columns[0].Aggregate == DataGridAggregate.None
                    ? InlineString(Ref(0, current), options.TotalLabel, styles.Get(null, bold: true))
                    : Subtotal(columns[c], c, current, 2, lastData, leaves, styles));
            }
            rows.Add(Row(current, totals.ToString(), 0));
        }

        var cols = new StringBuilder("<cols>");
        for (var c = 0; c < columns.Count; c++)
        {
            var width = Math.Max(4, Math.Round(columns[c].Width / 7.0, 2));
            cols.Append(CultureInfo.InvariantCulture, $"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{width}\" customWidth=\"1\"/>");
        }
        cols.Append("</cols>");
        var freeze = Math.Clamp(options.FreezeColumns, 0, Math.Max(0, columns.Count - 1));
        var topLeft = Ref(freeze, 2);
        var pane = freeze > 0 ? "bottomRight" : "bottomLeft";
        var view = $"<pane{(freeze > 0 ? $" xSplit=\"{freeze}\"" : string.Empty)} ySplit=\"1\" topLeftCell=\"{topLeft}\" activePane=\"{pane}\" state=\"frozen\"/>" +
                   $"<selection pane=\"{pane}\" activeCell=\"{topLeft}\" sqref=\"{topLeft}\"/>";
        var dimension = $"A1:{DataGridValueFormat.ColumnLetters(Math.Max(0, columns.Count - 1))}{Math.Max(1, current)}";
        return Declaration +
               $"<worksheet xmlns=\"{MainNs}\" xmlns:r=\"{RelNs}\"><sheetPr><outlinePr summaryBelow=\"0\"/></sheetPr>" +
               $"<dimension ref=\"{dimension}\"/><sheetViews><sheetView workbookViewId=\"0\">{view}</sheetView></sheetViews>" +
               $"<sheetFormatPr defaultRowHeight=\"15\"{(maxLevel > 0 ? $" outlineLevelRow=\"{Math.Min(7, maxLevel)}\"" : string.Empty)}/>" +
               cols + "<sheetData>" + string.Concat(rows) + "</sheetData></worksheet>";
    }

    private static IEnumerable<DataGridExportNode> Leaves(DataGridExportNode node) =>
        node.IsGroup ? node.Children!.SelectMany(Leaves) : [node];

    private static string Row(int index, string cells, int level) =>
        $"<row r=\"{index}\"{(level > 0 ? $" outlineLevel=\"{Math.Min(7, level)}\"" : string.Empty)}>{cells}</row>";

    private static string Cell(DataGridXlsxColumn column, int c, int row, object? value, Styles styles)
    {
        var reference = Ref(c, row);
        switch (value)
        {
            case null:
            case string { Length: 0 }:
                return string.Empty;
            case decimal number:
                var stored = column.Format.Type == DataGridColumnType.Percent ? number / 100m : number;
                return $"<c r=\"{reference}\" s=\"{styles.Get(NumberFormat(column.Format), false)}\"><v>{stored.ToString(CultureInfo.InvariantCulture)}</v></c>";
            case DateOnly day:
                var serial = day.DayNumber - new DateOnly(1899, 12, 30).DayNumber;
                return $"<c r=\"{reference}\" s=\"{styles.GetBuiltIn(14, false)}\"><v>{serial.ToString(CultureInfo.InvariantCulture)}</v></c>";
            default:
                return InlineString(reference, DataGridCsv.Raw(value), 0);
        }
    }

    private static string Subtotal(DataGridXlsxColumn column, int c, int row, int first, int last, IReadOnlyList<DataGridExportNode> leaves,
                                   Styles styles)
    {
        var function = column.Aggregate switch
        {
            DataGridAggregate.Average => 1,
            DataGridAggregate.Count => 3,
            DataGridAggregate.Max => 4,
            DataGridAggregate.Min => 5,
            DataGridAggregate.Sum => 9,
            _ => 0
        };
        if (function == 0 || last < first)
        {
            return string.Empty;
        }
        var cached = DataGridAggregates.Compute(leaves.Select(l => l.Values is { } v && c < v.Count ? v[c] : null), column.Aggregate);
        var value = cached is { } result
            ? "<v>" + (column.Format.Type == DataGridColumnType.Percent && column.Aggregate != DataGridAggregate.Count ? result / 100m : result)
                .ToString(CultureInfo.InvariantCulture) + "</v>"
            : string.Empty;
        var letters = DataGridValueFormat.ColumnLetters(c);
        var format = column.Format.Type == DataGridColumnType.Date && column.Aggregate is DataGridAggregate.Min or DataGridAggregate.Max
            ? styles.GetBuiltIn(14, true)
            : styles.Get(NumberFormat(column.Format, column.Aggregate), true);
        return $"<c r=\"{Ref(c, row)}\" s=\"{format}\"><f>SUBTOTAL({function},{letters}{first}:{letters}{last})</f>{value}</c>";
    }

    private static string InlineString(string reference, string text, int style) =>
        $"<c r=\"{reference}\" t=\"inlineStr\"{(style != 0 ? $" s=\"{style}\"" : string.Empty)}><is><t xml:space=\"preserve\">{Xml(text)}</t></is></c>";

    private static string Ref(int column, int row) => DataGridValueFormat.ColumnLetters(column) + row.ToString(CultureInfo.InvariantCulture);

    private static string SheetName(string name)
    {
        var builder = new StringBuilder();
        foreach (var ch in name)
        {
            builder.Append(ch is ':' or '\\' or '/' or '?' or '*' or '[' or ']' ? ' ' : ch);
        }
        var cleaned = builder.ToString().Trim();
        if (cleaned.Length > 31)
        {
            cleaned = cleaned[..31].Trim();
        }
        return cleaned.Length == 0 ? "Sheet1" : cleaned;
    }

    /// <summary>Escapes text for XML and drops characters XML cannot hold.</summary>
    private static string Xml(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch < 0x20 && ch is not ('\t' or '\n' or '\r') || ch is '￾' or '￿')
            {
                continue;
            }
            builder.Append(ch);
        }
        return SecurityElement.Escape(builder.ToString()) ?? string.Empty;
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes);
    }

    /// <summary>Number formats and cell styles collected while writing.</summary>
    private sealed class Styles
    {
        private readonly List<string> _formats = new();
        private readonly List<(int Format, bool Bold)> _xfs = [(0, false), (0, true)];

        public int Get(string? numberFormat, bool bold)
        {
            var id = 0;
            if (numberFormat is not null)
            {
                var index = _formats.IndexOf(numberFormat);
                if (index < 0)
                {
                    _formats.Add(numberFormat);
                    index = _formats.Count - 1;
                }
                id = 164 + index;
            }
            return GetBuiltIn(id, bold);
        }

        public int GetBuiltIn(int formatId, bool bold)
        {
            var index = _xfs.IndexOf((formatId, bold));
            if (index < 0)
            {
                _xfs.Add((formatId, bold));
                index = _xfs.Count - 1;
            }
            return index;
        }

        public string Xml()
        {
            var builder = new StringBuilder(Declaration);
            builder.Append("<styleSheet xmlns=\"").Append(MainNs).Append("\">");
            if (_formats.Count > 0)
            {
                builder.Append(CultureInfo.InvariantCulture, $"<numFmts count=\"{_formats.Count}\">");
                for (var i = 0; i < _formats.Count; i++)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"<numFmt numFmtId=\"{164 + i}\" formatCode=\"{DataGridXlsx.Xml(_formats[i])}\"/>");
                }
                builder.Append("</numFmts>");
            }
            builder.Append("<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                           "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font></fonts>" +
                           "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
                           "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                           "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
            builder.Append(CultureInfo.InvariantCulture, $"<cellXfs count=\"{_xfs.Count}\">");
            foreach (var (format, bold) in _xfs)
            {
                builder.Append(CultureInfo.InvariantCulture,
                    $"<xf numFmtId=\"{format}\" fontId=\"{(bold ? 1 : 0)}\" fillId=\"0\" borderId=\"0\" xfId=\"0\"{(format != 0 ? " applyNumberFormat=\"1\"" : string.Empty)}{(bold ? " applyFont=\"1\"" : string.Empty)}/>");
            }
            builder.Append("</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
            return builder.ToString();
        }
    }
}
