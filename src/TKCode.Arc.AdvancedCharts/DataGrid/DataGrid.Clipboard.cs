using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DataGrid
{
    private static DataFormat<byte[]>? s_htmlFormat;

    /// <summary>The platform's HTML clipboard format (CF_HTML on Windows, public.html on macOS, text/html elsewhere).</summary>
    private static DataFormat<byte[]> HtmlFormat => s_htmlFormat ??= DataFormat.CreateBytesPlatformFormat(
        OperatingSystem.IsWindows() ? "HTML Format" : OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ? "public.html" : "text/html");

    /// <summary>Copies the selected cells as tab separated text and an HTML table; returns whether the clipboard took them.</summary>
    public Task<bool> CopySelectionAsync() => CopySelectionAsync(withHeaders: false);

    /// <summary>Copies the current selection, with a header row on top when <paramref name="withHeaders"/> is true; returns whether it was copied.</summary>
    public async Task<bool> CopySelectionAsync(bool withHeaders)
    {
        EnsureView();
        if (!HasCells)
        {
            return false;
        }
        var range = Range;
        var ok = await WriteClipboardAsync(Matrix(range, header: withHeaders, formatted: false), Matrix(range, header: withHeaders, formatted: true));
        if (ok)
        {
            Viewport.Flash(range);
            Say(DataGridText.Count(DataGridLangResourceKind.CopiedCellsOne, DataGridLangResourceKind.CopiedCellsOther, range.CellCount, EffectiveCulture));
        }
        else
        {
            Say(DataGridText.Get(DataGridLangResourceKind.CopyFailed));
        }
        return ok;
    }

    /// <summary>Copies the selection, then clears it (one undo step).</summary>
    internal async Task CutSelectionAsync()
    {
        if (HasDataSource)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.GridReadOnly));
            return;
        }
        if (!HasCells)
        {
            return;
        }
        var range = Range;
        if (await WriteClipboardAsync(Matrix(range, header: false, formatted: false), Matrix(range, header: false, formatted: true)))
        {
            var written = WriteCells(RangeCells(range, (_, _) => null), range, DataGridChangeReason.Cut);
            Say(written > 0
                ? DataGridText.Count(DataGridLangResourceKind.ClearedCellsOne, DataGridLangResourceKind.ClearedCellsOther, written, EffectiveCulture)
                : DataGridText.Get(DataGridLangResourceKind.NothingToClear));
        }
        else
        {
            Say(DataGridText.Get(DataGridLangResourceKind.CopyFailed));
        }
    }

    /// <summary>Pastes from the clipboard at the selection (an HTML table first, then tab separated text).</summary>
    internal async Task PasteAsync()
    {
        if (HasDataSource)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.GridReadOnly));
            return;
        }
        if (!HasCells)
        {
            return;
        }
        var matrix = await ReadClipboardAsync();
        if (matrix is null || matrix.Count == 0)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.NothingToPaste));
            return;
        }
        Paste(matrix);
    }

    /// <summary>
    /// Pastes a matrix at the selection: a selection that is a whole multiple of the matrix is tiled, otherwise the matrix
    /// lands at the top-left cell (what falls outside the grid is dropped). Values that do not parse are skipped.
    /// </summary>
    internal void Paste(IReadOnlyList<IReadOnlyList<string>> matrix)
    {
        var range = Range;
        var rows = matrix.Count;
        if (rows == 0)
        {
            return;
        }
        var cellCount = 0L;
        foreach (var line in matrix)
        {
            cellCount += line.Count;
        }
        if (cellCount > MaxPasteCells)
        {
            Say(DataGridText.Format(DataGridLangResourceKind.PasteTooLarge, MaxPasteCells.ToString("N0", EffectiveCulture)));
            return;
        }
        var columns = matrix.Max(r => r.Count);
        if (columns == 0)
        {
            return;
        }
        var visible = VisibleAccessors;
        var tile = (range.RowCount > rows || range.ColumnCount > columns) && range.RowCount % rows == 0 && range.ColumnCount % columns == 0;
        if (!tile && PasteOverflow == DataGridPasteOverflow.AppendRows && range.Row0 + rows > DisplayCount)
        {
            AppendRows(range.Row0 + rows - DisplayCount);
        }
        var height = tile ? range.RowCount : Math.Min(rows, DisplayCount - range.Row0);
        var width = tile ? range.ColumnCount : Math.Min(columns, visible.Length - range.Column0);
        var cells = new List<(int, int, object?)>();
        var skipped = 0;
        for (var r = 0; r < height; r++)
        {
            var source = SourceOfRow(range.Row0 + r);
            for (var c = 0; c < width; c++)
            {
                var line = matrix[r % rows];
                var text = c % columns < line.Count ? line[c % columns] : string.Empty;
                var accessor = visible[range.Column0 + c];
                var column = ColumnOf(accessor);
                if (DataGridValueParser.TryParse(text, column.CellFormat, EffectiveCulture,
                        column.Type == DataGridColumnType.Select ? OptionsOf(accessor) : null, out var value))
                {
                    cells.Add((source, accessor, value));
                }
                else
                {
                    skipped++;
                }
            }
        }
        var target = new DataGridCellRange(range.Row0, range.Column0, range.Row0 + height - 1, range.Column0 + width - 1);
        var written = WriteCells(cells, target, DataGridChangeReason.Paste, flash: false);
        skipped += _refusedWrites;
        Select(new DataGridCellAddress(target.Row0, target.Column0), new DataGridCellAddress(target.Row1, target.Column1), scroll: false);
        Viewport.Flash(target);
        var message = new StringBuilder(DataGridText.Count(DataGridLangResourceKind.PastedCellsOne, DataGridLangResourceKind.PastedCellsOther,
            written, EffectiveCulture));
        if (skipped > 0)
        {
            message.Append(DataGridText.Count(DataGridLangResourceKind.PasteSkippedOne, DataGridLangResourceKind.PasteSkippedOther, skipped,
                EffectiveCulture));
        }
        if (!tile && (rows > height || columns > width))
        {
            message.Append(DataGridText.Get(DataGridLangResourceKind.PasteOverflow));
        }
        Say(message.ToString());
    }

    /// <summary>Copies the checked rows with a header row (bulk Copy).</summary>
    internal async Task CopyCheckedRowsAsync()
    {
        var items = CheckedItems();
        var visible = VisibleAccessors;
        List<IReadOnlyList<string>> Rows(bool formatted)
        {
            var matrix = new List<IReadOnlyList<string>> { visible.Select(a => ColumnOf(a).Header).ToArray() };
            foreach (var item in items)
            {
                matrix.Add(visible.Select(a =>
                {
                    var value = ValueOfItem(item, a);
                    return formatted ? FormatValue(value, a) : ClipboardText(value);
                }).ToArray());
            }
            return matrix;
        }
        var ok = await WriteClipboardAsync(Rows(false), Rows(true));
        Say(ok
            ? DataGridText.Count(DataGridLangResourceKind.CopiedRowsOne, DataGridLangResourceKind.CopiedRowsOther, items.Count, EffectiveCulture)
            : DataGridText.Get(DataGridLangResourceKind.CopyFailed));
    }

    /// <summary>The normalized value of an item (checked rows outside the view, data source rows).</summary>
    internal object? ValueOfItem(object item, int accessor)
    {
        if (!HasDataSource)
        {
            var source = _store.IndexOfItem(item);
            if (source >= 0)
            {
                return _store.Value(source, accessor);
            }
        }
        var access = _store.Accessors[accessor];
        var value = access.Read(item);
        access.Release();
        return value;
    }

    /// <summary>The cells of a range as text: raw values (numbers without grouping, ISO days) or display text.</summary>
    internal List<IReadOnlyList<string>> Matrix(DataGridCellRange range, bool header, bool formatted)
    {
        var visible = VisibleAccessors;
        var matrix = new List<IReadOnlyList<string>>();
        if (header)
        {
            matrix.Add(Enumerable.Range(range.Column0, range.ColumnCount).Select(c => ColumnOf(visible[c]).Header).ToArray());
        }
        for (var r = range.Row0; r <= range.Row1; r++)
        {
            var line = new string[range.ColumnCount];
            for (var c = range.Column0; c <= range.Column1; c++)
            {
                var value = ValueAt(r, visible[c]);
                line[c - range.Column0] = formatted ? FormatValue(value, visible[c]) : ClipboardText(value);
            }
            matrix.Add(line);
        }
        return matrix;
    }

    /// <summary>Raw clipboard text of a value: numbers with the culture's decimal separator and no grouping, ISO days.</summary>
    private string ClipboardText(object? value) => value switch
    {
        null => string.Empty,
        decimal d => d.ToString("0.############################", EffectiveCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        string s => s,
        bool flag => flag ? "TRUE" : "FALSE",
        _ => DataGridSorting.TextOf(value)
    };

    private async Task<bool> WriteClipboardAsync(IReadOnlyList<IReadOnlyList<string>> raw, IReadOnlyList<IReadOnlyList<string>> formatted)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }
        var tsv = DataGridClipboardText.ToTsv(raw);
        var html = DataGridClipboardText.ToHtml(formatted);
        try
        {
            var item = new DataTransferItem();
            item.SetText(tsv);
            var payload = OperatingSystem.IsWindows() ? DataGridClipboardText.WrapCfHtml(html) : html;
            item.Set(HtmlFormat, Encoding.UTF8.GetBytes(payload));
            var transfer = new DataTransfer();
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer);
            return true;
        }
        catch (Exception)
        {
            try
            {
                await clipboard.SetTextAsync(tsv);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    private async Task<List<List<string>>?> ReadClipboardAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return null;
        }
        try
        {
            using var data = await clipboard.TryGetDataAsync();
            if (data is null)
            {
                return null;
            }
            // Read one cell more than the limit, so Paste can tell a table that fits from one that was cut short.
            var readLimit = (int)Math.Min(int.MaxValue, (long)MaxPasteCells + 1);
            if (data.Contains(HtmlFormat) && await data.TryGetValueAsync(HtmlFormat) is { Length: > 0 } bytes)
            {
                var html = DataGridClipboardText.UnwrapCfHtml(Encoding.UTF8.GetString(bytes));
                if (DataGridClipboardText.ParseHtmlTable(html, readLimit) is { Count: > 0 } table)
                {
                    return table;
                }
            }
            var text = await data.TryGetTextAsync();
            return string.IsNullOrEmpty(text) ? null : DataGridClipboardText.ParseTsv(text, readLimit);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
