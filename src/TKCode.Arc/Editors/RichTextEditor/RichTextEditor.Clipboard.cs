using TKCode.Arc.Primitives;
using System.Collections.Immutable;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class RichTextEditor
{
    /* ----------------------------------------------------------------------------------------------------------
     * Clipboard
     * -------------------------------------------------------------------------------------------------------- */

    private async Task CopyAsync()
    {
        if (_engine.Selection.IsCollapsed || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        var fragment = _engine.GetFragment();
        var item = new DataTransferItem();
        item.Set(DataFormat.Text, _engine.GetSelectedText());
        item.Set(s_markdownFormat, RichTextEditorMarkdown.Write(fragment));
        item.Set(s_fragmentFormat, new RichTextEditorClipboardFragment(fragment));
        var data = new DataTransfer();
        data.Add(item);
        try
        {
            await clipboard.SetDataAsync(data);
        }
        catch (Exception)
        {
            // The platform refused the clipboard; nothing to do.
        }
    }

    private async Task CutAsync()
    {
        if (_engine.Selection.IsCollapsed)
        {
            return;
        }
        await CopyAsync();
        _engine.DeleteSelectionAsCommand();
    }

    private async Task PasteAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        try
        {
            var own = await clipboard.TryGetInProcessDataAsync();
            if (own is not null && await own.TryGetValueAsync(s_fragmentFormat) is { } fragment)
            {
                PasteContent(await own.TryGetTextAsync() ?? string.Empty, fragment.Blocks, html: null);
                return;
            }
            using var data = await clipboard.TryGetDataAsync();
            if (data is null)
            {
                return;
            }
            if (_engine.Docs && await data.TryGetFilesAsync() is { Length: > 0 } files && files.OfType<IStorageFile>().Where(f => ImageContentTypeOrNull(f.Name) is not null).ToList() is { Count: > 0 } pictures)
            {
                foreach (var picture in pictures)
                {
                    await InsertImageFileAsync(RichTextImageOrigin.Paste, picture);
                }
                return;
            }
            var text = await data.TryGetTextAsync() ?? string.Empty;
            if (await data.TryGetValueAsync(s_markdownFormat) is { } markdown)
            {
                PasteContent(text, RichTextEditorMarkdown.ToDocument(markdown, _engine.Docs), html: null);
                return;
            }
            string? html = null;
            foreach (var format in data.Formats)
            {
                if (Array.IndexOf(s_htmlFormats, format.Identifier) >= 0)
                {
                    html = await data.TryGetValueAsync(DataFormat.CreateStringPlatformFormat(format.Identifier));
                    if (!string.IsNullOrEmpty(html))
                    {
                        break;
                    }
                }
            }
            if (_engine.Docs && text.Length == 0 && string.IsNullOrEmpty(html) && await data.TryGetBitmapAsync() is { } bitmap)
            {
                using (bitmap)
                {
                    var png = new MemoryStream();
                    bitmap.Save(png, PngBitmapEncoderOptions.Default);
                    var bytes = png.ToArray();
                    await InsertPictureAsync(RichTextImageOrigin.Paste, null, "image/png", () => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false)));
                }
                return;
            }
            PasteContent(text, null, html);
        }
        catch (Exception)
        {
            // Clipboard formats a platform cannot convert are skipped.
        }
    }

    /// <summary>Arc's paste rules: HTML is sanitized, Markdown-looking text is converted, other text goes in as text.</summary>
    internal void PasteContent(string text, IReadOnlyList<RichTextEditorBlock>? blocks, string? html)
    {
        if (IsReadOnly)
        {
            return;
        }
        CloseSlash();
        if (blocks is null && _engine.Docs && html is null && IsImageAddress(text) && _engine.Selection.IsCollapsed)
        {
            // A bare picture address pasted on its own becomes an image.
            InsertImage(text.Trim());
            return;
        }
        if (blocks is null)
        {
            if (!string.IsNullOrEmpty(html))
            {
                var fragmentStart = html.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
                var fragmentEnd = html.IndexOf("<!--EndFragment-->", StringComparison.OrdinalIgnoreCase);
                if (fragmentStart >= 0 && fragmentEnd > fragmentStart)
                {
                    html = html[(fragmentStart + 20)..fragmentEnd];
                }
                else if (html.IndexOf('<') is var first and > 0)
                {
                    html = html[first..];
                }
                blocks = RichTextEditorHtml.ToBlocks(html, _engine.Docs);
            }
            else if (RichTextEditorMarkdown.LooksLikeMarkdown(text) || text.Contains("\n\n", StringComparison.Ordinal))
            {
                blocks = RichTextEditorMarkdown.ToDocument(text, _engine.Docs);
            }
        }
        if (blocks is { Count: 0 })
        {
            blocks = null;
        }
        _engine.Paste(text, blocks);
    }

    private static bool IsImageAddress(string text)
    {
        var value = text.Trim();
        if (value.Length == 0 || value.AsSpan().IndexOfAny(" \t\r\n") >= 0)
        {
            return false;
        }
        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return RichTextEditorHtml.IsSafeImageUrl(value);
        }
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var path = value.Split('?', '#')[0];
        return ImageContentTypeOrNull(path) is not null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (IsReadOnly || !_engine.Docs || e.DataTransfer.TryGetFiles() is not { Length: > 0 } files || !files.OfType<IStorageFile>().Any(f => ImageContentTypeOrNull(f.Name) is not null))
        {
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (IsReadOnly || !_engine.Docs || _surface is null || e.DataTransfer.TryGetFiles() is not { Length: > 0 } files)
        {
            return;
        }
        var pictures = files.OfType<IStorageFile>().Where(f => ImageContentTypeOrNull(f.Name) is not null).ToList();
        if (pictures.Count == 0)
        {
            return;
        }
        e.Handled = true;
        // The pictures go where they were dropped.
        _engine.SetSelection(RichTextEditorSelection.Caret(_surface.HitTest(e.GetPosition(_surface))));
        _ = InsertDroppedAsync(pictures);
    }

    private async Task InsertDroppedAsync(List<IStorageFile> pictures)
    {
        foreach (var picture in pictures)
        {
            await InsertImageFileAsync(RichTextImageOrigin.Drop, picture);
        }
    }

    private MenuSurface? _tableMenu;
    private Border? _tableAnchor;

    /// <summary>The context menu of a table cell: rows, columns and the table itself.</summary>
    private void OnSurfaceContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (IsReadOnly || !_engine.Docs || _surface is null || !e.TryGetPosition(_surface, out var point))
        {
            return;
        }
        var position = _surface.HitTest(point);
        if (!_engine.Blocks[position.Block].IsCell)
        {
            return;
        }
        e.Handled = true;
        EnsureOverlay();
        CloseSlash();
        if (!(_engine.Selection.Start.Block <= position.Block && _engine.Selection.End.Block >= position.Block))
        {
            _engine.SetSelection(RichTextEditorSelection.Caret(position));
        }
        if (_tableMenu is null)
        {
            _tableAnchor = new Border { Background = Brushes.Transparent, IsHitTestVisible = false, Width = 1, Height = 1 };
            _overlay!.Children.Add(_tableAnchor);
            _tableMenu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.Start, AnchorGap = 4 };
            AutomationProperties.SetName(_tableMenu, "Table");
            _tableMenu.Items.Add(new MenuEntry { Header = "Insert row above", Tag = "row-above" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Insert row below", Tag = "row-below" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Insert column left", Tag = "column-left" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Insert column right", Tag = "column-right" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Move row up", Tag = "row-up", HasSeparatorBefore = true });
            _tableMenu.Items.Add(new MenuEntry { Header = "Move row down", Tag = "row-down" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Move column left", Tag = "column-move-left" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Move column right", Tag = "column-move-right" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Merge cells", Tag = "cells-merge", HasSeparatorBefore = true });
            _tableMenu.Items.Add(new MenuEntry { Header = "Split cell", Tag = "cell-split" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Header row", Tag = "header-row", ToggleType = MenuEntryToggleType.CheckBox, HasSeparatorBefore = true });
            _tableMenu.Items.Add(new MenuEntry { Header = "Header column", Tag = "header-column", ToggleType = MenuEntryToggleType.CheckBox });
            _tableMenu.Items.Add(new MenuEntry { Header = "Distribute columns", Tag = "columns-even" });
            foreach (var color in Enum.GetValues<RichTextColor>())
            {
                _tableMenu.Items.Add(new MenuEntry
                {
                    Header = "Color: " + (color == RichTextColor.Default ? "None" : color.ToString()), Tag = color, ToggleType = MenuEntryToggleType.Radio,
                    GroupName = "cell-color", HasSeparatorBefore = color == RichTextColor.Default
                });
            }
            _tableMenu.Items.Add(new MenuEntry { Header = "Delete row", Tag = "row-delete", HasSeparatorBefore = true });
            _tableMenu.Items.Add(new MenuEntry { Header = "Delete column", Tag = "column-delete" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Delete table", Tag = "table-delete" });
            _tableMenu.ItemClicked += (_, args) =>
            {
                if (args.Entry.Tag is RichTextColor color)
                {
                    SetTableCellColor(color);
                    FocusEditor();
                    return;
                }
                switch (args.Entry.Tag as string)
                {
                    case "row-above":
                        AddTableRow(below: false);
                        break;
                    case "row-below":
                        AddTableRow();
                        break;
                    case "column-left":
                        AddTableColumn(after: false);
                        break;
                    case "column-right":
                        AddTableColumn();
                        break;
                    case "row-up":
                        MoveTableRow(down: false);
                        break;
                    case "row-down":
                        MoveTableRow();
                        break;
                    case "column-move-left":
                        MoveTableColumn(right: false);
                        break;
                    case "column-move-right":
                        MoveTableColumn();
                        break;
                    case "cells-merge":
                        MergeTableCells();
                        break;
                    case "cell-split":
                        SplitTableCell();
                        break;
                    case "header-row":
                        SetTableHeaderRow(!TableHasHeaderRow);
                        break;
                    case "header-column":
                        SetTableHeaderColumn(!TableHasHeaderColumn);
                        break;
                    case "columns-even":
                        DistributeTableColumns();
                        break;
                    case "row-delete":
                        DeleteTableRow();
                        break;
                    case "column-delete":
                        DeleteTableColumn();
                        break;
                    case "table-delete":
                        DeleteTable();
                        break;
                }
                FocusEditor();
            };
        }
        var cellColor = TableCellColor;
        foreach (var entry in _tableMenu.Items.OfType<MenuEntry>())
        {
            entry.IsChecked = entry.Tag switch
            {
                "header-row" => TableHasHeaderRow,
                "header-column" => TableHasHeaderColumn,
                RichTextColor color => color == cellColor,
                _ => false
            };
            entry.IsEnabled = (entry.Tag as string) switch
            {
                "cells-merge" => CanMergeTableCells,
                "row-up" => CanMoveTableRow(down: false),
                "row-down" => CanMoveTableRow(),
                "column-move-left" => CanMoveTableColumn(right: false),
                "column-move-right" => CanMoveTableColumn(),
                "cell-split" => CanSplitTableCell,
                _ => true
            };
        }
        var origin = _surface.TranslatePoint(point, this) ?? default;
        _tableAnchor!.RenderTransform = new TranslateTransform(origin.X, origin.Y);
        _tableMenu.Open(_tableAnchor);
    }
}
