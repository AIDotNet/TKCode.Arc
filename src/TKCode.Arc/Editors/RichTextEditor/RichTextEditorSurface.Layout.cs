using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace TKCode.Arc;

internal partial class RichTextEditorSurface
{
    private void EnsureLayout(double width)
    {
        var engine = Engine;
        if (engine is null)
        {
            return;
        }
        var blocks = engine.Blocks;
        var widthChanged = Math.Abs(width - _layoutWidth) > 0.01;
        if (!widthChanged && ReferenceEquals(blocks, _laidOut) && !_views.Any(v => v.StyleVersion != _styleVersion) && !PreeditStale())
        {
            return;
        }
        _layoutWidth = width;
        _laidOut = blocks;
        var used = new HashSet<RichTextEditorBlock>(ReferenceEqualityComparer.Instance);
        _views.Clear();
        var caretBlock = engine.Selection.Focus.Block;
        var index = 0;
        var tableEnd = 0;
        var columns = Array.Empty<double>();
        RichTextEditorBlock? tableHead = null;
        foreach (var block in blocks)
        {
            used.Add(block);
            if (block.IsCell && index >= tableEnd)
            {
                var table = RichTextEditorTables.Extent(blocks, index);
                tableEnd = table.End;
                columns = ColumnWidths(blocks, table, width);
                tableHead = blocks[table.Start];
            }
            var blockWidth = width;
            var header = false;
            if (block.IsCell)
            {
                blockWidth = 0;
                for (var c = block.Col; c < Math.Min(columns.Length, block.Col + (block.IsCovered ? 1 : block.ColSpan)); c++)
                {
                    blockWidth += columns[c];
                }
                header = RichTextEditorTables.IsHeader(tableHead!, block);
            }
            var preedit = IsComposing && index == caretBlock ? _preedit : null;
            if (preedit is null && _cache.TryGetValue(block, out var cached) && cached.StyleVersion == _styleVersion
                && Math.Abs(cached.LayoutWidth - blockWidth) < 0.01 && cached.Preedit is null && cached.IsHeader == header)
            {
                _views.Add(cached);
            }
            else
            {
                var view = Build(block, blockWidth, header, preedit, preedit is null ? 0 : engine.Selection.Focus.Offset);
                if (preedit is null)
                {
                    if (_cache.Remove(block, out var old))
                    {
                        old.Dispose();
                    }
                    _cache[block] = view;
                }
                _views.Add(view);
            }
            index++;
        }
        // Drop layouts of blocks that left the document.
        if (_cache.Count > used.Count)
        {
            var stale = _cache.Keys.Where(key => !used.Contains(key)).ToList();
            foreach (var key in stale)
            {
                _cache[key].Dispose();
                _cache.Remove(key);
            }
        }
        _tops.Clear();
        _markers.Clear();
        _tables.Clear();
        _hoverTable = -1;
        var y = VerticalPadding;
        var hidden = engine.HiddenMask;
        var lastShown = -1;
        for (var i = 0; i < _views.Count;)
        {
            var view = _views[i];
            if (hidden is not null && hidden[i])
            {
                // Folded under a collapsed toggle: no room, no gap; it sits where the next shown block starts.
                view.BoxX = 0;
                view.BoxWidth = width;
                view.TextX = view.PadX;
                _tops.Add(y);
                i++;
                continue;
            }
            if (lastShown >= 0)
            {
                y += Gap(_views[lastShown], view);
            }
            lastShown = view.Block.IsCell ? RichTextEditorTables.Extent(blocks, i).End - 1 : i;
            if (!view.Block.IsCell)
            {
                view.BoxX = 0;
                view.BoxWidth = width;
                view.TextX = view.PadX;
                _tops.Add(y);
                y += view.Height;
                i++;
                continue;
            }
            // A table: columns at their stored widths (scaled to fill the width); every row as tall as its tallest cell, and a merged cell
            // as tall as the rows it spans (the last one grows when its text needs more).
            var grid = RichTextEditorTables.Extent(blocks, i);
            var widths = ColumnWidths(blocks, grid, width);
            var offsets = new double[grid.Cols + 1];
            for (var c = 0; c < grid.Cols; c++)
            {
                offsets[c + 1] = offsets[c] + widths[c];
            }
            var rowHeights = new double[grid.Rows];
            for (var r = 0; r < grid.Rows; r++)
            {
                for (var c = 0; c < grid.Cols; c++)
                {
                    var cell = _views[grid.IndexOf(r, c)];
                    if (!cell.Block.IsCovered && cell.Block.RowSpan == 1)
                    {
                        rowHeights[r] = Math.Max(rowHeights[r], cell.ContentHeight);
                    }
                }
            }
            for (var span = 2; span <= grid.Rows; span++)
            {
                for (var r = 0; r + span <= grid.Rows; r++)
                {
                    for (var c = 0; c < grid.Cols; c++)
                    {
                        var cell = _views[grid.IndexOf(r, c)];
                        if (cell.Block.IsCovered || cell.Block.RowSpan != span)
                        {
                            continue;
                        }
                        var have = 0d;
                        for (var k = 0; k < span; k++)
                        {
                            have += rowHeights[r + k];
                        }
                        if (cell.ContentHeight > have)
                        {
                            rowHeights[r + span - 1] += cell.ContentHeight - have;
                        }
                    }
                }
            }
            var rowTop = y;
            var rowTops = new double[grid.Rows + 1];
            for (var r = 0; r < grid.Rows; r++)
            {
                rowTops[r] = rowTop;
                for (var c = 0; c < grid.Cols; c++)
                {
                    var cell = _views[grid.IndexOf(r, c)];
                    var block = cell.Block;
                    var rows = block.IsCovered ? 1 : block.RowSpan;
                    var height = 0d;
                    for (var k = 0; k < rows; k++)
                    {
                        height += rowHeights[r + k];
                    }
                    cell.Height = height;
                    cell.BoxX = offsets[c];
                    cell.BoxWidth = offsets[Math.Min(grid.Cols, c + (block.IsCovered ? 1 : block.ColSpan))] - offsets[c];
                    cell.TextX = cell.PadX + cell.BoxX;
                    cell.IsLastColumn = c + (block.IsCovered ? 1 : block.ColSpan) == grid.Cols;
                    cell.IsLastRow = r + rows == grid.Rows;
                    _tops.Add(rowTop);
                }
                rowTop += rowHeights[r];
            }
            rowTops[grid.Rows] = rowTop;
            _tables.Add(new TableBox(grid.Start, grid.Rows, grid.Cols, y, rowTop, offsets, rowTops));
            y = rowTop;
            i = grid.End;
        }
        ComputeMarkers();
        _renderedBand = default;
    }

    private bool PreeditStale()
    {
        var engine = Engine!;
        var caret = engine.Selection.Focus.Block;
        for (var i = 0; i < _views.Count; i++)
        {
            var view = _views[i];
            var wanted = IsComposing && i == caret ? _preedit : null;
            if (!string.Equals(view.Preedit, wanted, StringComparison.Ordinal))
            {
                return true;
            }
            if (wanted is not null && view.PreeditAt != engine.Selection.Focus.Offset)
            {
                return true;
            }
        }
        return false;
    }

    private bool HasCodeHeader(RichTextEditorBlock block) => block.IsCode && (ShowCodeHeader || block.Language is not null);

    /// <summary>
    /// The syntax spans of a code block in its language, or null for plain text, an unknown language, a very long block or
    /// no installed highlighter (<see cref="CodeBlockHighlighter.Provider"/>).
    /// </summary>
    internal static List<CodeBlockSyntaxSpan>? HighlightSyntax(RichTextEditorBlock block)
    {
        if (block.Language is not { } language || block.Length == 0 || block.Length > 30000 || !CodeBlockHighlighter.SupportsLanguage(language))
        {
            return null;
        }
        try
        {
            using var session = CodeBlockHighlighter.CreateSession(language);
            if (session is null)
            {
                return null;
            }
            var spans = new List<CodeBlockSyntaxSpan>();
            CodeBlockHighlighter.AppendSpans(block.Text, session.Update(block.Text), spans);
            return spans;
        }
        catch (Exception)
        {
            // A grammar that fails to load or tokenize leaves the block plain; editing never depends on highlighting.
            return null;
        }
    }

    private double FontSizeOf(RichTextBlockType type) => type switch
    {
        RichTextBlockType.Heading1 => Heading1FontSize,
        RichTextBlockType.Heading2 => Heading2FontSize,
        RichTextBlockType.Heading3 => Heading3FontSize,
        RichTextBlockType.Heading4 => Math.Round((Heading3FontSize + BaseFontSize) / 2),
        RichTextBlockType.Heading5 => BaseFontSize,
        RichTextBlockType.Heading6 => Math.Round(BaseFontSize * 0.875),
        RichTextBlockType.CodeBlock => CodeFontSize,
        _ => BaseFontSize
    };

    /// <summary>Arc's block spacing with CSS margin collapsing: <c>.6em</c> between blocks, headings <c>1.1em</c> above and <c>.45em</c> below, rules <c>1.1em</c> around, list items <c>.2em</c>.</summary>
    private double Gap(RichTextEditorBlockView previous, RichTextEditorBlockView next)
    {
        var a = previous.Block;
        var b = next.Block;
        if (a.IsCell && b.IsCell && !b.StartsTable)
        {
            return 0;
        }
        if (a.IsList && b.IsList && (b.Level > 0 || a.Level > 0 || a.Type == b.Type))
        {
            return ListItemPaddingEm * BaseFontSize;
        }
        if (a.SharesCalloutBox(b))
        {
            return 0;
        }
        var nextSize = FontSizeOf(b.Type);
        var bottom = a.IsDivider ? 1.1 * BaseFontSize : a.IsImage ? 0.8 * BaseFontSize : 0;
        double top;
        if (b.IsImage)
        {
            top = 0.8 * BaseFontSize;
        }
        else if (b.IsDivider)
        {
            top = 1.1 * BaseFontSize;
        }
        else if (a.IsHeading)
        {
            top = 0.45 * nextSize;
        }
        else if (b.IsHeading)
        {
            top = 1.1 * nextSize;
        }
        else
        {
            top = 0.6 * nextSize;
        }
        return Math.Max(top, bottom);
    }

    /// <summary>
    /// List markers in one forward pass: numbers count siblings of the same list (a change of list type at a level
    /// starts a new list), bullets are discs at the outer level and circles inside a bulleted list (Arc's <c>ul ul</c>).
    /// </summary>
    private void ComputeMarkers()
    {
        var counters = new List<(RichTextBlockType Type, int Count)>();
        foreach (var view in _views)
        {
            var block = view.Block;
            if (!block.IsList)
            {
                counters.Clear();
                _markers.Add(null);
                continue;
            }
            var level = Math.Min(block.Level, counters.Count);
            if (counters.Count > level + 1)
            {
                counters.RemoveRange(level + 1, counters.Count - level - 1);
            }
            if (counters.Count == level + 1)
            {
                var (type, count) = counters[level];
                counters[level] = type == block.Type ? (type, count + 1) : (block.Type, 1);
            }
            else
            {
                counters.Add((block.Type, 1));
            }
            if (block.IsTodo)
            {
                _markers.Add("todo");
                continue;
            }
            if (block.IsToggle)
            {
                _markers.Add("toggle");
                continue;
            }
            if (block.Type == RichTextBlockType.NumberedList)
            {
                _markers.Add(counters[level].Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
                continue;
            }
            var nestedInBullets = false;
            for (var l = 0; l < level; l++)
            {
                if (counters[l].Type == RichTextBlockType.BulletedList)
                {
                    nestedInBullets = true;
                    break;
                }
            }
            _markers.Add(nestedInBullets ? "circle" : "disc");
        }
    }

    private Typeface TypefaceFor(RichTextEditorMarks marks, FontWeight baseWeight)
    {
        var family = (marks & RichTextEditorMarks.Code) != 0 && MonoFontFamily is { } mono ? mono : FontFamily;
        var weight = (marks & RichTextEditorMarks.Bold) != 0 ? FontWeight.SemiBold : baseWeight;
        var style = (marks & RichTextEditorMarks.Italic) != 0 ? FontStyle.Italic : FontStyle.Normal;
        return new Typeface(family, style, weight);
    }

    private IBrush LinkUnderlineBrush
    {
        get
        {
            if (_linkUnderlineBrush is null)
            {
                var accent = (AccentBrush as ISolidColorBrush)?.Color ?? Colors.Gray;
                _linkUnderlineBrush = new ImmutableSolidColorBrush(Color.FromArgb((byte)(accent.A * 0.45), accent.R, accent.G, accent.B));
            }
            return _linkUnderlineBrush;
        }
    }

    internal IBrush SelectionBrush
    {
        get
        {
            if (_selectionBrush is null)
            {
                var accent = (AccentBrush as ISolidColorBrush)?.Color ?? Colors.SteelBlue;
                _selectionBrush = new ImmutableSolidColorBrush(Color.FromArgb((byte)(accent.A * 0.22), accent.R, accent.G, accent.B));
            }
            return _selectionBrush;
        }
    }

    /// <summary>Strikethrough, the accent underline of a link, or the plain underline mark (a link already has its own underline).</summary>
    private TextDecorationCollection? Decorations(bool strike, bool link, bool underline)
    {
        var key = (strike ? 1 : 0) | (link || underline ? 2 : 0);
        if (key == 0)
        {
            return null;
        }
        if (_decorations[key] is { } cached)
        {
            return cached;
        }
        var collection = new TextDecorationCollection();
        if (strike)
        {
            collection.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough, StrokeThickness = 1, StrokeThicknessUnit = TextDecorationUnit.Pixel });
        }
        if (link || underline)
        {
            collection.Add(new TextDecoration
            {
                Location = TextDecorationLocation.Underline, StrokeThickness = 1, StrokeThicknessUnit = TextDecorationUnit.Pixel,
                Stroke = link ? LinkUnderlineBrush : null, StrokeOffset = 1.5, StrokeOffsetUnit = TextDecorationUnit.Pixel
            });
        }
        return _decorations[key] = collection;
    }

    private TextDecorationCollection PreeditDecorations => _preeditUnderline ??= new TextDecorationCollection
    {
        new TextDecoration { Location = TextDecorationLocation.Underline, StrokeThickness = 1, StrokeThicknessUnit = TextDecorationUnit.Pixel }
    };

    /// <summary>How much smaller superscript and subscript text is than the text around it.</summary>
    private const double ScriptScale = 0.72;

    /// <summary>
    /// The superscript and subscript pieces of a laid out block, one per line they fall on: each is laid out on its own and placed over
    /// the room the block's layout keeps for it, raised by about a third of the surrounding text's size or lowered by a sixth.
    /// </summary>
    private static List<(TextLayout Layout, Point At)> ScriptRuns(TextLayout layout, string text,
        List<(int Start, int Length, GenericTextRunProperties Properties, bool Raised)> spans, double tracking)
    {
        var runs = new List<(TextLayout Layout, Point At)>();
        foreach (var line in layout.TextLines)
        {
            var lineStart = line.FirstTextSourceIndex;
            var lineEnd = lineStart + line.Length;
            foreach (var (start, length, properties, raised) in spans)
            {
                var from = Math.Max(start, lineStart);
                var to = Math.Min(start + length, Math.Min(lineEnd, text.Length));
                if (to <= from)
                {
                    continue;
                }
                var piece = text[from..to].TrimEnd('\n', '\r');
                if (piece.Length == 0 || layout.HitTestTextRange(from, piece.Length).FirstOrDefault() is not { Width: > 0 } box)
                {
                    continue;
                }
                var small = new TextLayout(piece, properties.Typeface, properties.FontRenderingEmSize, properties.ForegroundBrush,
                    TextAlignment.Left, TextWrapping.NoWrap, null, properties.TextDecorations, FlowDirection.LeftToRight,
                    double.PositiveInfinity, double.PositiveInfinity, double.NaN, tracking * ScriptScale);
                // Relative to the size the text around it has (a sized run raises further).
                var around = properties.FontRenderingEmSize / ScriptScale;
                var shift = raised ? -0.36 * around : 0.16 * around;
                runs.Add((small, new Point(box.X, box.Y + line.Baseline - small.TextLines[0].Baseline + shift)));
            }
        }
        return runs;
    }

    private RichTextEditorBlockView Build(RichTextEditorBlock block, double width, bool header, string? preedit, int preeditAt)
    {
        var view = new RichTextEditorBlockView(block) { LayoutWidth = width, StyleVersion = _styleVersion, Preedit = preedit, PreeditAt = preeditAt, IsHeader = header };
        if (block.IsCovered)
        {
            // A merged cell covers it: no text, no box of its own.
            view.FontSize = BaseFontSize;
            view.LineHeight = 1;
            return view;
        }
        if (block.IsDivider)
        {
            view.Height = 1;
            view.ContentHeight = 1;
            view.FontSize = BaseFontSize;
            view.LineHeight = 1;
            return view;
        }
        if (block.IsImage)
        {
            var entry = block.Src is { } src ? Owner?.Images.Get(src) : null;
            var available = Math.Max(1, width);
            var chosen = ReferenceEquals(block, _resizeBlock) ? _resizeWidth : block.Width;
            if (entry is { State: RichTextEditorImageState.Ready, Image: { } picture })
            {
                var natural = picture.Size;
                var target = Math.Min(chosen > 0 ? chosen : natural.Width, available);
                view.ImageSize = natural.Width > 0 && natural.Height > 0
                    ? new Size(target, natural.Height * target / natural.Width)
                    : RichTextEditorImageCache.Fit(natural, available);
            }
            else
            {
                view.ImageSize = new Size(Math.Min(chosen > 0 ? chosen : 320, available), entry is { State: RichTextEditorImageState.Failed } ? 72 : 160);
            }
            view.Height = view.ImageSize.Height;
            view.ContentHeight = view.Height;
            view.FontSize = BaseFontSize;
            view.LineHeight = 1;
            return view;
        }
        var size = FontSizeOf(block.Type);
        // Lines are as tall as the block's largest text needs, stretched by the block's spacing.
        var largest = 1d;
        if (!block.IsCode)
        {
            foreach (var span in block.Spans)
            {
                largest = Math.Max(largest, span.Marks.Size().Scale());
            }
        }
        var lineHeight = size * largest * (block.IsHeading ? 1.25 : block.IsCode ? 1.6 : 1.65) * block.Spacing.Scale();
        var weight = block.IsHeading || header ? FontWeight.Medium : FontWeight.Normal;
        var tracking = (block.IsHeading ? -0.02 : Tracking) * size;
        var checkedTodo = block.IsTodo && block.IsChecked;
        var baseBrush = block.Type is RichTextBlockType.Quote or RichTextBlockType.Heading6 || checkedTodo ? SecondaryBrush ?? Foreground : Foreground;
        double textX = 0;
        double textY = 0;
        double right = 0;
        switch (block.Type)
        {
            case RichTextBlockType.Quote:
                textX = 16;
                break;
            case RichTextBlockType.BulletedList:
            case RichTextBlockType.NumberedList:
            case RichTextBlockType.TodoList:
            case RichTextBlockType.Toggle:
                textX = (block.Level + 1) * (ListIndentEm + ListItemPaddingEm) * BaseFontSize;
                break;
            case RichTextBlockType.Callout:
                textX = 46;
                textY = 10;
                right = 16;
                break;
            case RichTextBlockType.CodeBlock:
                textX = 15;
                textY = HasCodeHeader(block) ? 28 : 13;
                right = 15;
                break;
            case RichTextBlockType.TableCell:
                textX = 12;
                textY = 8;
                right = 12;
                break;
        }
        view.FontSize = size;
        view.PadX = textX;
        view.LineHeight = lineHeight;
        view.Weight = weight;
        view.TextX = textX;
        view.TextY = textY;

        var text = preedit is null ? block.Text : block.Text.Insert(Math.Clamp(preeditAt, 0, block.Length), preedit);
        var overrides = new List<ValueSpan<TextRunProperties>>();
        var codeSpans = new List<(int Start, int Length)>();
        var fillSpans = new List<(int Start, int Length, RichTextColor Color)>();
        var scriptSpans = new List<(int Start, int Length, GenericTextRunProperties Properties, bool Raised)>();
        if (block.IsCode)
        {
            var plain = new GenericTextRunProperties(TypefaceFor(RichTextEditorMarks.Code, weight), size, null, baseBrush);
            var syntax = HighlightSyntax(block);
            if (syntax is null || syntax.Count == 0)
            {
                overrides.Add(new ValueSpan<TextRunProperties>(0, Math.Max(1, text.Length), plain));
            }
            else
            {
                var italic = new Typeface(MonoFontFamily ?? FontFamily, FontStyle.Italic, weight);
                var at = 0;
                foreach (var span in syntax)
                {
                    if (span.Start > at)
                    {
                        AddShifted(overrides, at, span.Start - at, plain, view);
                    }
                    var brush = SyntaxBrushOf(span.Kind) ?? baseBrush;
                    var face = span.Kind == CodeBlockSyntaxKind.Comment ? italic : TypefaceFor(RichTextEditorMarks.Code, weight);
                    AddShifted(overrides, span.Start, span.Length, new GenericTextRunProperties(face, size, null, brush), view);
                    at = span.Start + span.Length;
                }
                if (at < block.Length)
                {
                    AddShifted(overrides, at, block.Length - at, plain, view);
                }
            }
        }
        else
        {
            foreach (var span in block.Spans)
            {
                if (span.Marks == RichTextEditorMarks.None && span.Href is null && !checkedTodo)
                {
                    continue;
                }
                var strike = (span.Marks & RichTextEditorMarks.Strike) != 0 || checkedTodo;
                var underline = (span.Marks & RichTextEditorMarks.Underline) != 0;
                var link = span.Href is not null;
                var code = (span.Marks & RichTextEditorMarks.Code) != 0;
                var colored = TextBrushOf(span.Marks.TextColor());
                var brush = strike ? SecondaryBrush ?? baseBrush : link ? LinkBrush ?? baseBrush : colored ?? baseBrush;
                var script = span.Marks & RichTextEditorMarks.ScriptMask;
                var runSize = (code ? size * 0.86 : size) * span.Marks.Size().Scale() * (script != 0 ? ScriptScale : 1);
                var properties = new GenericTextRunProperties(TypefaceFor(span.Marks, weight), runSize, Decorations(strike, link, underline), brush);
                if (script != 0)
                {
                    // The layout keeps the small glyphs' room (so the caret and hit tests fit) but draws nothing; the glyphs are drawn
                    // raised or lowered on top, as Avalonia's text lines draw every run on the shared baseline.
                    scriptSpans.Add((view.ToLayout(span.Start), span.Length, properties, script == RichTextEditorMarks.Superscript));
                    properties = new GenericTextRunProperties(properties.Typeface, runSize, null, Brushes.Transparent);
                }
                if (span.Marks.Highlight() != RichTextColor.Default)
                {
                    fillSpans.Add((view.ToLayout(span.Start), span.Length, span.Marks.Highlight()));
                }
                AddShifted(overrides, span.Start, span.Length, properties, view);
                if (code)
                {
                    codeSpans.Add((view.ToLayout(span.Start), span.Length));
                }
            }
        }
        if (preedit is not null)
        {
            var at = Math.Clamp(preeditAt, 0, block.Length);
            var marks = Engine?.CurrentTypingMarks() ?? RichTextEditorMarks.None;
            overrides.Add(new ValueSpan<TextRunProperties>(at, preedit.Length,
                new GenericTextRunProperties(TypefaceFor(block.IsCode ? RichTextEditorMarks.Code : marks, weight), block.IsCode ? size : size * marks.Size().Scale(), PreeditDecorations, baseBrush)));
        }
        var maxWidth = Math.Max(1, width - textX - right);
        view.Layout = new TextLayout(text, TypefaceFor(block.IsCode ? RichTextEditorMarks.Code : RichTextEditorMarks.None, weight), size, baseBrush,
            block.Align switch { RichTextAlign.Center => TextAlignment.Center, RichTextAlign.End => TextAlignment.Right, _ => TextAlignment.Left },
            TextWrapping.Wrap, null, null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity,
            lineHeight, tracking, 0, null, overrides.Count == 0 ? null : overrides);
        view.Height = view.Layout.Height + textY + (block.IsCode ? 13 : textY);
        view.ContentHeight = view.Height;
        if (scriptSpans.Count > 0)
        {
            view.ScriptRuns = ScriptRuns(view.Layout, text, scriptSpans, tracking);
        }
        if (codeSpans.Count > 0)
        {
            var rects = new List<Rect>();
            foreach (var (start, length) in codeSpans)
            {
                foreach (var rect in view.Layout.HitTestTextRange(start, length))
                {
                    var box = size * 0.86 * 1.2 + size * 0.24;
                    rects.Add(new Rect(rect.X - 4, rect.Y + (rect.Height - box) / 2, rect.Width + 8, box));
                }
            }
            view.CodeRects = rects;
        }
        if (fillSpans.Count > 0)
        {
            var fills = new List<(Rect Rect, RichTextColor Color)>();
            foreach (var (start, length, color) in fillSpans)
            {
                foreach (var rect in view.Layout.HitTestTextRange(start, length))
                {
                    fills.Add((rect, color));
                }
            }
            view.FillRects = fills;
        }
        return view;
    }

    private static void AddShifted(List<ValueSpan<TextRunProperties>> overrides, int start, int length, TextRunProperties properties, RichTextEditorBlockView view)
    {
        if (view.Preedit is not { Length: > 0 } preedit || start + length <= view.PreeditAt)
        {
            overrides.Add(new ValueSpan<TextRunProperties>(start, length, properties));
            return;
        }
        if (start >= view.PreeditAt)
        {
            overrides.Add(new ValueSpan<TextRunProperties>(start + preedit.Length, length, properties));
            return;
        }
        var head = view.PreeditAt - start;
        overrides.Add(new ValueSpan<TextRunProperties>(start, head, properties));
        overrides.Add(new ValueSpan<TextRunProperties>(view.PreeditAt + preedit.Length, length - head, properties));
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Geometry
     * -------------------------------------------------------------------------------------------------------- */

    public double BlockTop(int index) => index >= 0 && index < _tops.Count ? _tops[index] : 0;

    /// <summary>The caret rectangle for a position, in surface coordinates (full line box height).</summary>
    public Rect LineRect(RichTextEditorPosition position, bool preeditCursor = false)
    {
        if (position.Block < 0 || position.Block >= _views.Count)
        {
            return new Rect(0, VerticalPadding, 1, BaseFontSize * 1.65);
        }
        var view = _views[position.Block];
        var top = _tops[position.Block];
        if (view.Layout is null)
        {
            return new Rect(0, top, 1, Math.Max(1, view.Height));
        }
        var layoutPosition = view.ToLayout(position.Offset);
        if (preeditCursor && view.Preedit is { } preedit)
        {
            layoutPosition = view.PreeditAt + Math.Clamp(_preeditCursor ?? preedit.Length, 0, preedit.Length);
        }
        var lineIndex = LineOfLayout(view, layoutPosition);
        var line = view.Layout.TextLines[lineIndex];
        var x = line.GetDistanceFromCharacterHit(new CharacterHit(layoutPosition));
        return new Rect(view.TextX + x, top + view.TextY + view.LineTop(lineIndex), 1, line.Height);
    }

    private static int LineOfLayout(RichTextEditorBlockView view, int layoutPosition)
    {
        var lines = view.Layout!.TextLines;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var contentEnd = line.FirstTextSourceIndex + line.Length - line.NewLineLength;
            if (layoutPosition < contentEnd || (layoutPosition == contentEnd && (line.NewLineLength > 0 || i == lines.Count - 1)))
            {
                return i;
            }
        }
        return lines.Count - 1;
    }

    /// <summary>The drawn caret: the line box narrowed to the font's height.</summary>
    public Rect CaretRect(RichTextEditorPosition position, bool preeditCursor = false)
    {
        var line = LineRect(position, preeditCursor);
        var view = position.Block >= 0 && position.Block < _views.Count ? _views[position.Block] : null;
        var size = view?.FontSize ?? BaseFontSize;
        var height = Math.Min(line.Height, size * 1.3);
        return new Rect(Math.Round(line.X - 0.5, 1), line.Y + (line.Height - height) / 2, 1.5, height);
    }

    private Rect? CaretBounds()
    {
        var engine = Engine;
        if (engine is null || _views.Count == 0)
        {
            return null;
        }
        return CaretRect(engine.Selection.Focus, preeditCursor: IsComposing);
    }

    /// <summary>The position under a point in surface coordinates (dividers resolve to the nearest text).</summary>
    public RichTextEditorPosition HitTest(Point point)
    {
        if (_views.Count == 0)
        {
            return new RichTextEditorPosition(0, 0);
        }
        var index = CellAtX(BlockAtY(point.Y), point.X);
        var view = _views[index];
        if (view.Layout is null)
        {
            if (view.Block.IsImage)
            {
                return new RichTextEditorPosition(index, 0);
            }
            var center = _tops[index] + view.Height / 2;
            var engine = Engine!;
            var target = RichTextEditorEngine.NearestTextual(engine.Blocks, index, forward: point.Y >= center);
            var targetView = _views[target.Block];
            return HitTestInBlock(target.Block, new Point(point.X, point.Y >= center ? _tops[target.Block] : _tops[target.Block] + targetView.Height - 1));
        }
        return HitTestInBlock(index, point);
    }

    /// <summary>For a table cell, the cell of the same row at <paramref name="x"/>; any other block stays.</summary>
    private int CellAtX(int index, double x)
    {
        var blocks = Engine!.Blocks;
        if (!blocks[index].IsCell)
        {
            return index;
        }
        var table = RichTextEditorTables.Extent(blocks, index);
        var rowStart = index - blocks[index].Col;
        var row = blocks[index].Row;
        for (var c = 0; c < table.Cols; c++)
        {
            var view = _views[rowStart + c];
            if (x < view.BoxX + view.BoxWidth)
            {
                return RichTextEditorTables.OwnerIndex(blocks, table, row, c);
            }
        }
        return RichTextEditorTables.OwnerIndex(blocks, table, row, table.Cols - 1);
    }

    /// <summary>The block at <paramref name="y"/> (surface coordinates; the nearer block in a gap).</summary>
    internal int BlockIndexAtY(double y) => _views.Count == 0 ? 0 : BlockAtY(y);

    /// <summary>The vertical centre of the first line of <paramref name="block"/> (a tall atomic block or table: near its top).</summary>
    internal double HandleY(int block)
    {
        if (block < 0 || block >= _views.Count)
        {
            return 0;
        }
        var view = _views[block];
        return _tops[block] + (view.Layout is null ? Math.Min(14, view.Height / 2) : view.TextY + view.LineHeight / 2);
    }

    /// <summary>The left edge of what <paramref name="block"/> draws first: a list item's marker column (so the handle sits beside a nested item, not at the page edge), 0 for any other block.</summary>
    internal double HandleX(int block)
    {
        if (block < 0 || block >= _views.Count)
        {
            return 0;
        }
        var item = _views[block].Block;
        return item.Type is RichTextBlockType.BulletedList or RichTextBlockType.NumberedList or RichTextBlockType.TodoList or RichTextBlockType.Toggle
            ? item.Level * (ListIndentEm + ListItemPaddingEm) * BaseFontSize
            : 0;
    }

    /// <summary>The height of the box of <paramref name="block"/> (tests).</summary>
    internal double BlockHeightForTests(int block) => _views[block].Height;

    /// <summary>The laid out view of <paramref name="block"/> (tests).</summary>
    internal RichTextEditorBlockView ViewForTests(int block) => _views[block];

    private Dictionary<int, List<RichTextEditorMatch>>? _matchesByBlock;

    /// <summary>The find bar's matches, drawn as highlights (null clears them).</summary>
    internal void SetMatches(List<RichTextEditorMatch>? matches, int current)
    {
        _matchesByBlock = matches is { Count: > 0 } ? matches.GroupBy(m => m.Block).ToDictionary(g => g.Key, g => g.ToList()) : null;
        InvalidateVisual();
    }

    internal int MatchCountForTests => _matchesByBlock?.Values.Sum(list => list.Count) ?? 0;

    internal double GapY(int gap)
    {
        if (_views.Count == 0)
        {
            return 0;
        }
        if (gap >= _views.Count)
        {
            var last = _views.Count - 1;
            var end = _tops[last] + _views[last].Height;
            return end;
        }
        return Math.Max(0, _tops[gap] - (gap > 0 ? 3 : 0));
    }

    /// <summary>The valid drop boundary nearest to <paramref name="y"/> (surface coordinates).</summary>
    internal int DropGapAt(double y, IReadOnlyList<RichTextEditorBlock> blocks)
    {
        var best = 0;
        var distance = double.MaxValue;
        for (var gap = 0; gap <= blocks.Count && gap <= _views.Count; gap++)
        {
            if (!RichTextEditorEngine.IsDropGap(blocks, gap))
            {
                continue;
            }
            var d = Math.Abs(GapY(gap) - y);
            if (d < distance)
            {
                distance = d;
                best = gap;
            }
        }
        return best;
    }

    private (int Start, int End)? _activeUnit;

    /// <summary>The blocks of the unit the block handle is acting on (tinted like a selection), or null.</summary>
    internal (int Start, int End)? ActiveUnit
    {
        get => _activeUnit;
        set
        {
            if (_activeUnit != value)
            {
                _activeUnit = value;
                InvalidateVisual();
            }
        }
    }

    private int BlockAtY(double y)
    {
        var lo = 0;
        var hi = _tops.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) >> 1;
            if (_tops[mid] <= y)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        // In the gap below a block, the nearer edge wins.
        if (lo + 1 < _tops.Count)
        {
            var bottom = _tops[lo] + _views[lo].Height;
            if (y > bottom && y - bottom > _tops[lo + 1] - y)
            {
                lo++;
            }
        }
        // Folded blocks take no room: the point belongs to the toggle that hides them.
        if (Engine is { } engine && engine.HiddenMask is { } hidden)
        {
            while (lo > 0 && lo < hidden.Length && hidden[lo])
            {
                lo--;
            }
        }
        return lo;
    }

    public RichTextEditorPosition HitTestInBlock(int index, Point point)
    {
        var view = _views[index];
        if (view.Layout is null)
        {
            return new RichTextEditorPosition(index, 0);
        }
        var local = new Point(point.X - view.TextX, point.Y - _tops[index] - view.TextY);
        var lines = view.Layout.TextLines;
        var lineIndex = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            lineIndex = i;
            if (local.Y < view.LineTop(i + 1))
            {
                break;
            }
        }
        return new RichTextEditorPosition(index, view.FromLayout(PositionInLine(view, lineIndex, local.X)));
    }

    private static int PositionInLine(RichTextEditorBlockView view, int lineIndex, double x)
    {
        var lines = view.Layout!.TextLines;
        var line = lines[lineIndex];
        var hit = line.GetCharacterHitFromDistance(x);
        var position = hit.FirstCharacterIndex + hit.TrailingLength;
        var start = line.FirstTextSourceIndex;
        var contentEnd = start + line.Length - line.NewLineLength;
        if (line.NewLineLength == 0 && lineIndex < lines.Count - 1 && line.TrailingWhitespaceLength > 0)
        {
            // Past the end of a wrapped line: stay before its trailing space, on this line.
            contentEnd -= line.TrailingWhitespaceLength;
        }
        return Math.Clamp(position, start, Math.Max(start, contentEnd));
    }

    /// <summary>The position one visual line up or down from <paramref name="from"/>, at <paramref name="goalX"/>.</summary>
    public RichTextEditorPosition MoveVertical(RichTextEditorPosition from, double goalX, int direction)
    {
        var blocks = Engine!.Blocks;
        var view = _views[from.Block];
        var line = view.Layout is null ? 0 : view.LineOf(from.Offset);
        if (view.Layout is not null && (direction < 0 ? line > 0 : line < view.LineCount - 1))
        {
            var target = line + direction;
            return new RichTextEditorPosition(from.Block, view.FromLayout(PositionInLine(view, target, goalX - view.TextX)));
        }
        var origin = blocks[from.Block];
        var start = from.Block + direction;
        if (origin.IsCell)
        {
            // Inside a table, up and down stay in the column; the first and last rows leave the table.
            var table = RichTextEditorTables.Extent(blocks, from.Block);
            start = direction < 0
                ? origin.Row > 0 ? RichTextEditorTables.OwnerIndex(blocks, table, origin.Row - 1, origin.Col) : table.Start - 1
                : origin.Row + origin.RowSpan < table.Rows ? RichTextEditorTables.OwnerIndex(blocks, table, origin.Row + origin.RowSpan, origin.Col) : table.End;
        }
        for (var i = start; i >= 0 && i < _views.Count; i += direction)
        {
            if (blocks[i].IsDivider || Engine!.IsHidden(i))
            {
                continue;
            }
            if (blocks[i].IsImage)
            {
                return new RichTextEditorPosition(i, 0);
            }
            if (blocks[i].IsCell && !(origin.IsCell && i >= RichTextEditorTables.Extent(blocks, from.Block).Start && i < RichTextEditorTables.Extent(blocks, from.Block).End))
            {
                i = CellAtX(i, goalX);
            }
            var next = _views[i];
            var targetLine = direction < 0 ? next.LineCount - 1 : 0;
            return new RichTextEditorPosition(i, next.FromLayout(PositionInLine(next, targetLine, goalX - next.TextX)));
        }
        return direction < 0 ? new RichTextEditorPosition(from.Block, 0) : new RichTextEditorPosition(from.Block, blocks[from.Block].Length);
    }

    /// <summary>Start and end of the visual line holding <paramref name="position"/>.</summary>
    public (int Start, int End) LineBounds(RichTextEditorPosition position)
    {
        var view = _views[position.Block];
        if (view.Layout is null)
        {
            return (0, 0);
        }
        var lines = view.Layout.TextLines;
        var index = view.LineOf(position.Offset);
        var line = lines[index];
        var start = line.FirstTextSourceIndex;
        var end = start + line.Length - line.NewLineLength;
        if (line.NewLineLength == 0 && index < lines.Count - 1 && line.TrailingWhitespaceLength > 0)
        {
            end -= line.TrailingWhitespaceLength;
        }
        return (view.FromLayout(start), view.FromLayout(end));
    }

    /// <summary>
    /// The first line's rectangle and the bounding box of a range (Arc's getClientRects()[0] and getBoundingClientRect()),
    /// measuring only the two end blocks so long selections stay cheap.
    /// </summary>
    public (Rect First, Rect Whole)? SelectionBounds(RichTextEditorPosition start, RichTextEditorPosition end)
    {
        var engine = Engine;
        if (engine is null || start.Block >= _views.Count || end.Block >= _views.Count)
        {
            return null;
        }
        var head = RangeRects(start, start.Block == end.Block ? end : new RichTextEditorPosition(start.Block, engine.Blocks[start.Block].Length));
        if (start.Block == end.Block)
        {
            if (head.Count == 0)
            {
                return null;
            }
            var union = head[0];
            foreach (var rect in head)
            {
                union = union.Union(rect);
            }
            return (head[0], union);
        }
        var tail = RangeRects(new RichTextEditorPosition(end.Block, 0), end);
        var first = head.Count > 0 ? head[0] : LineRect(start);
        var whole = first;
        foreach (var rect in head.Concat(tail))
        {
            whole = whole.Union(rect);
        }
        for (var i = start.Block + 1; i < end.Block; i++)
        {
            var view = _views[i];
            var width = view.Layout?.WidthIncludingTrailingWhitespace ?? Bounds.Width;
            whole = whole.Union(new Rect(view.TextX, _tops[i], Math.Max(1, width), view.Height));
        }
        if (tail.Count == 0)
        {
            whole = whole.Union(LineRect(end));
        }
        return (first, whole);
    }

    /// <summary>Rectangles covering a range, in surface coordinates, line by line (for toolbar placement).</summary>
    public List<Rect> RangeRects(RichTextEditorPosition start, RichTextEditorPosition end)
    {
        var rects = new List<Rect>();
        var engine = Engine;
        if (engine is null)
        {
            return rects;
        }
        foreach (var (index, from, to) in RichTextEditorEngine.Segments(engine.Blocks, start, end))
        {
            if (index >= _views.Count)
            {
                break;
            }
            var view = _views[index];
            if (view.Layout is null || from == to)
            {
                continue;
            }
            foreach (var rect in view.Layout.HitTestTextRange(view.ToLayout(from), to - from))
            {
                rects.Add(rect.Translate(new Vector(view.TextX, _tops[index] + view.TextY)));
            }
        }
        return rects;
    }
}
