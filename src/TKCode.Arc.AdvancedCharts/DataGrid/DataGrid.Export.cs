using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class DataGrid
{
    /// <summary>True when the top level can show a save dialog; the Export buttons are disabled otherwise (for example in browsers without the File System Access API).</summary>
    internal bool CanSaveFiles => TopLevel.GetTopLevel(this)?.StorageProvider is { CanSave: true };

    /// <summary>
    /// Exports the rows in view order (all rows the filters keep, or only the checked rows) as CSV or an Excel workbook,
    /// after <see cref="Exporting"/> and a save dialog. Returns whether a file was written. With a <see cref="DataSource"/>
    /// only checked rows can be exported.
    /// </summary>
    public async Task<bool> ExportAsync(DataGridExportFormat format, bool checkedRowsOnly = false)
    {
        EnsureView();
        if (HasDataSource)
        {
            checkedRowsOnly = true;
        }
        var rows = checkedRowsOnly ? CheckedItems() : View.Filtered.Select(i => _store.ItemAt(i)).ToArray();
        var name = ExportFileName is { Length: > 0 } explicitName ? explicitName : Slug(Label);
        var args = new DataGridExportingEventArgs(ExportingEvent, format, rows, name);
        RaiseEvent(args);
        if (args.Cancel)
        {
            return false;
        }
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanSave)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.ExportFailed));
            return false;
        }
        var extension = format == DataGridExportFormat.Xlsx ? "xlsx" : "csv";
        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = args.FileName + "." + extension,
                DefaultExtension = extension,
                ShowOverwritePrompt = true,
                FileTypeChoices =
                [
                    format == DataGridExportFormat.Xlsx
                        ? new FilePickerFileType(DataGridText.Get(DataGridLangResourceKind.ExportXlsx))
                        {
                            Patterns = ["*.xlsx"],
                            MimeTypes = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
                            AppleUniformTypeIdentifiers = ["org.openxmlformats.spreadsheetml.sheet"]
                        }
                        : new FilePickerFileType("CSV")
                        {
                            Patterns = ["*.csv"],
                            MimeTypes = ["text/csv"],
                            AppleUniformTypeIdentifiers = ["public.comma-separated-values-text"]
                        }
                ]
            });
            if (file is null)
            {
                return false;
            }
            await using (var stream = await file.OpenWriteAsync())
            {
                WriteExport(stream, format, rows, grouped: !checkedRowsOnly);
            }
            file.Dispose();
            Say(DataGridText.Count(DataGridLangResourceKind.ExportedRowsOne, DataGridLangResourceKind.ExportedRowsOther, rows.Count, EffectiveCulture));
            return true;
        }
        catch (Exception)
        {
            Say(DataGridText.Get(DataGridLangResourceKind.ExportFailed));
            return false;
        }
    }

    /// <summary>Writes an export of <paramref name="rows"/> (visible columns, view order); groups become xlsx outline rows.</summary>
    internal void WriteExport(Stream stream, DataGridExportFormat format, IReadOnlyList<object> rows, bool grouped)
    {
        var visible = VisibleAccessors;
        var headers = visible.Select(a => ColumnOf(a).Header).ToArray();
        IReadOnlyList<object?> Values(object item) => visible.Select(a => ValueOfItem(item, a)).ToArray();
        if (format == DataGridExportFormat.Csv)
        {
            DataGridCsv.Write(stream, headers, rows.Select(Values));
            return;
        }
        var layout = Layout;
        var columns = visible.Select((a, i) =>
        {
            var column = ColumnOf(a);
            var width = i < layout.Widths.Length ? layout.Widths[i] : WidthOf(column);
            return new DataGridXlsxColumn(column.Header, column.CellFormat, column.EffectiveAggregate, width);
        }).ToArray();
        IReadOnlyList<DataGridExportNode> nodes;
        if (grouped && View.Groups is { } groups)
        {
            DataGridExportNode Node(DataGridGroupNode group)
            {
                var column = ColumnOf(group.Accessor);
                var value = FormatValue(group.Value, group.Accessor);
                var label = column.Header + ": " + (string.IsNullOrEmpty(value) ? DataGridText.Get(DataGridLangResourceKind.EmptyValue) : value);
                var children = group.Children is { } nested
                    ? nested.Select(Node).ToArray()
                    : group.Rows.Select(r => DataGridExportNode.Row(Values(_store.ItemAt(r)))).ToArray();
                return DataGridExportNode.Group(label, children);
            }
            nodes = groups.Select(Node).ToArray();
        }
        else
        {
            nodes = rows.Select(r => DataGridExportNode.Row(Values(r))).ToArray();
        }
        DataGridXlsx.Write(stream, columns, nodes, new DataGridXlsxOptions
        {
            SheetName = string.IsNullOrWhiteSpace(Label) ? "Sheet1" : Label,
            FreezeColumns = PinCount,
            Totals = ShowTotals,
            TotalLabel = DataGridText.Get(DataGridLangResourceKind.TotalRow)
        });
    }

    /// <summary>A file name from a label: lower case letters and digits joined by hyphens ("data" when nothing is left).</summary>
    internal static string Slug(string? label)
    {
        var builder = new StringBuilder();
        foreach (var ch in (label ?? string.Empty).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }
        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? "data" : slug;
    }
}
