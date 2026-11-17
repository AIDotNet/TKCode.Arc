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
    /* ----------------------------------------------------------------------------------------------------------
     * Rendering
     * -------------------------------------------------------------------------------------------------------- */

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        _viewport = e.EffectiveViewport;
        if (_renderedBand != default && (_viewport.Top < _renderedBand.Top || _viewport.Bottom > _renderedBand.Bottom))
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        var engine = Engine;
        if (engine is null || _views.Count == 0)
        {
            return;
        }
        var band = bounds;
        if (_viewport.Height > 0 && _viewport.Width > 0)
        {
            band = new Rect(0, _viewport.Top - _viewport.Height, bounds.Width, _viewport.Height * 3);
        }
        _renderedBand = _viewport.Height > 0 ? band : default;
        var first = BlockAtY(Math.Max(0, band.Top));
        if (engine.Blocks[first].IsCell)
        {
            first -= engine.Blocks[first].Col;
        }
        var selection = engine.Selection;
        var focused = IsKeyboardFocusWithin || Owner?.KeepsSelectionVisible == true;
        var showSelection = !selection.IsCollapsed && focused;
        var blocks = engine.Blocks;
        var width = bounds.Width;
        var cells = showSelection ? engine.CellSelection : null;
        for (var i = Math.Max(0, first - 1); i < _views.Count; i++)
        {
            var top = _tops[i];
            if (top > band.Bottom)
            {
                break;
            }
            var view = _views[i];
            var block = view.Block;
            if (block.IsCovered || engine.IsHidden(i))
            {
                continue;
            }
            if (_activeUnit is { } unit && i >= unit.Start && i < unit.End)
            {
                context.DrawRectangle(SelectionBrush, null, new RoundedRect(new Rect(0, top, width, Math.Max(1, view.Height)), 6));
            }
            DrawDecoration(context, i, view, top, width);
            var inCells = cells is { } picked && i >= picked.Table.Start && i < picked.Table.End;
            if (inCells && block.Row >= cells!.Value.R1 && block.Row <= cells.Value.R2 && block.Col >= cells.Value.C1 && block.Col <= cells.Value.C2)
            {
                context.FillRectangle(SelectionBrush, new Rect(view.BoxX, top, view.BoxWidth, view.Height));
            }
            if (view.Layout is null)
            {
                if (block.IsImage)
                {
                    var covered = showSelection && selection.Start.Block <= i && selection.End.Block >= i;
                    DrawImage(context, view, top, covered || (focused && selection.IsCollapsed && selection.Focus.Block == i));
                }
                else if (showSelection && selection.Start.Block <= i && selection.End.Block >= i)
                {
                    context.FillRectangle(SelectionBrush, new Rect(0, top - 3, width, 7));
                }
                continue;
            }
            var origin = new Point(view.TextX, top + view.TextY);
            if (view.FillRects is { } fillRects)
            {
                foreach (var (rect, color) in fillRects)
                {
                    if (FillBrushOf(color) is { } fill)
                    {
                        context.DrawRectangle(fill, null, new RoundedRect(rect.Translate(origin), 3));
                    }
                }
            }
            if (view.CodeRects is { } codeRects && CodeBackground is { } codeBrush)
            {
                foreach (var rect in codeRects)
                {
                    context.DrawRectangle(codeBrush, null, new RoundedRect(rect.Translate(origin), 6));
                }
            }
            if (_matchesByBlock is { } found && found.TryGetValue(i, out var blockMatches) && (FillYellowBrush ?? SelectionBrush) is { } matchBrush)
            {
                foreach (var rect in MatchRects(view, blockMatches))
                {
                    context.DrawRectangle(matchBrush, null, new RoundedRect(rect.Translate(origin), 3));
                }
            }
            if (showSelection && !inCells && selection.Start.Block <= i && selection.End.Block >= i)
            {
                DrawSelection(context, i, view, origin, selection);
            }
            view.Layout.Draw(context, origin);
            if (view.ScriptRuns is { } scriptRuns)
            {
                foreach (var (layout, at) in scriptRuns)
                {
                    layout.Draw(context, origin + at);
                }
            }
            DrawPlaceholder(context, i, view, origin, blocks);
        }
        DrawTableGrips(context);
    }

    private void DrawDecoration(DrawingContext context, int index, RichTextEditorBlockView view, double top, double width)
    {
        var block = view.Block;
        switch (block.Type)
        {
            case RichTextBlockType.Divider:
                if (RuleBrush is { } rule)
                {
                    context.FillRectangle(rule, new Rect(0, top, width, 1));
                }
                break;
            case RichTextBlockType.TableCell:
                DrawCell(context, view, top);
                break;
            case RichTextBlockType.Quote:
                if (QuoteBarBrush is { } bar)
                {
                    context.FillRectangle(bar, new Rect(0, top, 2, view.Height));
                }
                break;
            case RichTextBlockType.Callout:
                DrawCallout(context, index, view, top, width);
                break;
            case RichTextBlockType.CodeBlock:
                var pen = CodeBorderBrush is { } border ? PenFor(CodeBorderPen, border, 1) : null;
                context.DrawRectangle(CodeBackground, pen, new RoundedRect(new Rect(0.5, top + 0.5, Math.Max(0, width - 1), Math.Max(0, view.Height - 1)), 14));
                if (HasCodeHeader(block) && MutedBrush is { } muted)
                {
                    var language = block.Language ?? "Plain text";
                    var key = "lang:" + language;
                    if (!_markerLayouts.TryGetValue(key, out var label))
                    {
                        label = new TextLayout(language, new Typeface(MonoFontFamily ?? FontFamily), CodeFontSize * 0.85, muted, TextAlignment.Left, TextWrapping.NoWrap);
                        _markerLayouts[key] = label;
                    }
                    label.Draw(context, new Point(15, top + 7));
                    if (ShowCodeHeader)
                    {
                        // A chevron after the name marks the label as a picker: one static geometry, translated into place.
                        var x = 15 + label.WidthIncludingTrailingWhitespace + 8;
                        var y = top + 7 + label.Height / 2;
                        using (context.PushTransform(Matrix.CreateTranslation(x, y)))
                        {
                            context.DrawGeometry(null, PenFor(CodeChevronPen, muted, 1.25, round: true), s_codeChevron);
                        }
                    }
                }
                break;
            case RichTextBlockType.BulletedList:
            case RichTextBlockType.NumberedList:
            case RichTextBlockType.TodoList:
            case RichTextBlockType.Toggle:
                DrawMarker(context, index, view, top);
                break;
        }
    }

    /// <summary>
    /// A table cell's box: filled with its color, or tinted when it is a header cell, and sharing its borders with the others (each draws
    /// its top and left edge, the last ones the closing edges).
    /// </summary>
    private void DrawCell(DrawingContext context, RichTextEditorBlockView view, double top)
    {
        var rect = new Rect(view.BoxX, top, view.BoxWidth, view.Height);
        if ((FillBrushOf(view.Block.Tone) ?? (view.IsHeader ? CodeBackground : null)) is { } fill)
        {
            context.FillRectangle(fill, rect);
        }
        if (RuleBrush is not { } rule)
        {
            return;
        }
        context.FillRectangle(rule, new Rect(rect.X, rect.Y, rect.Width, 1));
        context.FillRectangle(rule, new Rect(rect.X, rect.Y, 1, rect.Height));
        if (view.IsLastColumn)
        {
            context.FillRectangle(rule, new Rect(rect.Right - 1, rect.Y, 1, rect.Height));
        }
        if (view.IsLastRow)
        {
            context.FillRectangle(rule, new Rect(rect.X, rect.Bottom - 1, rect.Width, 1));
        }
    }

    /// <summary>An image block: the picture (or a placeholder while it loads or when it failed), outlined when selected.</summary>
    private void DrawImage(DrawingContext context, RichTextEditorBlockView view, double top, bool selected)
    {
        var rect = new Rect(0, top, view.ImageSize.Width, view.ImageSize.Height);
        var entry = view.Block.Src is { } src ? Owner?.Images.Get(src) : null;
        if (entry is { State: RichTextEditorImageState.Ready, Image: { } image })
        {
            using (context.PushClip(new RoundedRect(rect, 8)))
            {
                context.DrawImage(image, new Rect(image.Size), rect);
            }
        }
        else
        {
            var pen = CodeBorderBrush is { } border ? PenFor(CodeBorderPen, border, 1) : null;
            context.DrawRectangle(CodeBackground, pen, new RoundedRect(rect.Deflate(0.5), 8));
            if (MutedBrush is { } muted)
            {
                var label = entry is { State: RichTextEditorImageState.Failed } ? view.Block.Alt ?? "Image unavailable" : "Loading image…";
                var key = "image:" + label;
                if (!_markerLayouts.TryGetValue(key, out var layout))
                {
                    layout = new TextLayout(label, new Typeface(FontFamily), BaseFontSize * 0.9, muted, TextAlignment.Center, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, maxWidth: Math.Max(1, rect.Width - 24));
                    _markerLayouts[key] = layout;
                }
                layout.Draw(context, new Point(rect.X + 12, rect.Y + (rect.Height - layout.Height) / 2));
            }
        }
        if (selected && AccentBrush is { } accent)
        {
            context.DrawRectangle(null, PenFor(ImageFramePen, accent, 2), new RoundedRect(rect.Inflate(1), 9));
            var grip = new Rect(rect.Right - 7, rect.Bottom - 7, 12, 12);
            context.DrawRectangle(accent, CheckBrush is { } edge ? PenFor(ImageGripPen, edge, 1.5) : null, new RoundedRect(grip, 4));
        }
    }

    /// <summary>One paragraph of a callout box: tinted background (rounded where the box begins or ends) and, on the first, the icon.</summary>
    private void DrawCallout(DrawingContext context, int index, RichTextEditorBlockView view, double top, double width)
    {
        var block = view.Block;
        var first = index == 0 || !_views[index - 1].Block.SharesCalloutBox(block);
        var last = index + 1 >= _views.Count || !_views[index + 1].Block.SharesCalloutBox(block);
        var fill = FillBrushOf(block.Tone) ?? CodeBackground;
        const double radius = 14;
        context.DrawRectangle(fill, null, new RoundedRect(new Rect(0, top, width, Math.Max(1, view.Height)),
            first ? radius : 0, first ? radius : 0, last ? radius : 0, last ? radius : 0));
        if (first && block.Icon is { } icon)
        {
            var key = "icon:" + icon;
            if (!_markerLayouts.TryGetValue(key, out var layout))
            {
                layout = new TextLayout(icon, new Typeface(FontFamily), BaseFontSize * 1.15, Foreground, TextAlignment.Left, TextWrapping.NoWrap, lineHeight: view.LineHeight);
                _markerLayouts[key] = layout;
            }
            layout.Draw(context, new Point(16, top + view.TextY));
        }
    }

    private void DrawMarker(DrawingContext context, int index, RichTextEditorBlockView view, double top)
    {
        var marker = index < _markers.Count ? _markers[index] : null;
        if (marker is null || view.Layout is null || MutedBrush is not { } brush)
        {
            return;
        }
        var size = BaseFontSize;
        var itemLeft = view.TextX - ListItemPaddingEm * size;
        var line = view.Layout.TextLines[0];
        if (marker == "todo")
        {
            DrawTodoBox(context, view, top, view.Block.IsChecked);
            return;
        }
        if (marker == "toggle")
        {
            DrawToggleArrow(context, view, top, brush);
            return;
        }
        if (marker is "disc" or "circle")
        {
            var radius = size * 0.165;
            var center = new Point(itemLeft - size * 0.62, top + view.TextY + line.Baseline - size * 0.31);
            if (marker == "disc")
            {
                context.DrawEllipse(brush, null, center, radius, radius);
            }
            else
            {
                context.DrawEllipse(null, PenFor(CircleMarkerPen, brush, 1), center, radius - 0.5, radius - 0.5);
            }
            return;
        }
        if (!_markerLayouts.TryGetValue(marker, out var layout))
        {
            layout = new TextLayout(marker, new Typeface(FontFamily), size, brush, TextAlignment.Left, TextWrapping.NoWrap,
                lineHeight: view.LineHeight, fontFeatures: new FontFeatureCollection { new FontFeature { Tag = "tnum", Value = 1 } });
            _markerLayouts[marker] = layout;
        }
        layout.Draw(context, new Point(itemLeft - size * 0.3 - layout.WidthIncludingTrailingWhitespace, top + view.TextY));
    }

    /// <summary>The disclosure arrow of a toggle item, where a to-do item has its checkbox.</summary>
    private Rect ToggleArrowRect(RichTextEditorBlockView view, double top) => TodoBoxRect(view, top);

    /// <summary>A chevron pointing right (collapsed) or down (open), in the muted text color.</summary>
    private void DrawToggleArrow(DrawingContext context, RichTextEditorBlockView view, double top, IBrush brush)
    {
        var box = ToggleArrowRect(view, top);
        var c = box.Center;
        var r = box.Width * 0.22;
        // One geometry per (size, direction), drawn around the origin and translated to the box: a translation leaves
        // the stroke width alone, so the arrow is the same as when it was built in place on every pass.
        var key = (Math.Round(r, 3), view.Block.IsCollapsed);
        if (!_toggleArrows.TryGetValue(key, out var arrow))
        {
            arrow = new StreamGeometry();
            using (var g = arrow.Open())
            {
                if (view.Block.IsCollapsed)
                {
                    g.BeginFigure(new Point(-r * 0.6, -r * 1.2), false);
                    g.LineTo(new Point(r * 0.6, 0));
                    g.LineTo(new Point(-r * 0.6, r * 1.2));
                }
                else
                {
                    g.BeginFigure(new Point(-r * 1.2, -r * 0.6), false);
                    g.LineTo(new Point(0, r * 0.6));
                    g.LineTo(new Point(r * 1.2, -r * 0.6));
                }
                g.EndFigure(false);
            }
            _toggleArrows[key] = arrow;
        }
        using (context.PushTransform(Matrix.CreateTranslation(c.X, c.Y)))
        {
            context.DrawGeometry(null, PenFor(ToggleArrowPen, brush, 1.6, round: true), arrow);
        }
    }

    private const int CodeBorderPen = 0;
    private const int CodeChevronPen = 1;
    private const int ImageFramePen = 2;
    private const int ImageGripPen = 3;
    private const int CircleMarkerPen = 4;
    private const int ToggleArrowPen = 5;
    private const int PenSlotCount = 6;

    /// <summary>The pen of <paramref name="slot"/> for <paramref name="brush"/>; rebuilt only when the brush object changed.</summary>
    private IPen PenFor(int slot, IBrush brush, double thickness, bool round = false)
    {
        if (_pens[slot] is { } pen && ReferenceEquals(_penBrushes[slot], brush))
        {
            return pen;
        }
        pen = round
            ? new ImmutablePen(brush.ToImmutable(), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round)
            : new ImmutablePen(brush.ToImmutable(), thickness);
        _pens[slot] = pen;
        _penBrushes[slot] = brush;
        return pen;
    }

    /// <summary>The code header's picker chevron around the origin (7 x 3.5, open downward).</summary>
    private static StreamGeometry BuildCodeChevron()
    {
        var chevron = new StreamGeometry();
        using (var g = chevron.Open())
        {
            g.BeginFigure(new Point(0, -1.5), false);
            g.LineTo(new Point(3.5, 2));
            g.LineTo(new Point(7, -1.5));
            g.EndFigure(false);
        }
        return chevron;
    }

    /// <summary>The find highlights of <paramref name="view"/> for <paramref name="matches"/>, hit-tested once per match list and layout.</summary>
    private static List<Rect> MatchRects(RichTextEditorBlockView view, List<RichTextEditorMatch> matches)
    {
        if (view.MatchRects is { } rects && ReferenceEquals(view.MatchSource, matches) && ReferenceEquals(view.MatchLayout, view.Layout))
        {
            return rects;
        }
        rects = view.MatchRects ?? new List<Rect>();
        rects.Clear();
        foreach (var match in matches)
        {
            foreach (var rect in view.Layout!.HitTestTextRange(view.ToLayout(match.Start), match.Length))
            {
                rects.Add(rect);
            }
        }
        view.MatchRects = rects;
        view.MatchSource = matches;
        view.MatchLayout = view.Layout;
        return rects;
    }

    /// <summary>The toggle item whose arrow is under <paramref name="point"/>, or -1.</summary>
    private int ToggleAt(Point point)
    {
        if (_views.Count == 0)
        {
            return -1;
        }
        var index = BlockAtY(point.Y);
        var view = _views[index];
        return view.Block.IsToggle && view.Layout is not null && ToggleArrowRect(view, _tops[index]).Inflate(4).Contains(point) ? index : -1;
    }

    /// <summary>The arrow of the toggle item at <paramref name="block"/>, in surface coordinates.</summary>
    internal Rect ToggleArrowBounds(int block) => ToggleArrowRect(_views[block], _tops[block]);

    /// <summary>The checkbox of a to-do item, relative to the surface: left of the text, centered on the first line's x-height.</summary>
    private Rect TodoBoxRect(RichTextEditorBlockView view, double top)
    {
        var size = BaseFontSize;
        var side = size * 0.95;
        var itemLeft = view.TextX - ListItemPaddingEm * size;
        var centerY = top + view.TextY + (view.Layout is { } layout ? layout.TextLines[0].Baseline : size) - size * 0.31;
        return new Rect(itemLeft - size * 0.3 - side, centerY - side / 2, side, side);
    }

    /// <summary>The checkbox of the to-do item at <paramref name="block"/>, in surface coordinates.</summary>
    internal Rect TodoBoxBounds(int block) => TodoBoxRect(_views[block], _tops[block]);

    private void DrawTodoBox(DrawingContext context, RichTextEditorBlockView view, double top, bool isChecked)
    {
        var box = TodoBoxRect(view, top);
        if (isChecked)
        {
            context.DrawRectangle(AccentBrush, null, new RoundedRect(box, 4));
            if (CheckBrush is { } check)
            {
                var pen = new ImmutablePen(check.ToImmutable(), 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                var tick = new StreamGeometry();
                using (var g = tick.Open())
                {
                    g.BeginFigure(new Point(box.X + box.Width * 0.27, box.Y + box.Height * 0.53), false);
                    g.LineTo(new Point(box.X + box.Width * 0.44, box.Y + box.Height * 0.70));
                    g.LineTo(new Point(box.X + box.Width * 0.74, box.Y + box.Height * 0.32));
                    g.EndFigure(false);
                }
                context.DrawGeometry(null, pen, tick);
            }
            return;
        }
        var border = QuoteBarBrush is { } brush ? new ImmutablePen(brush.ToImmutable(), 1.5) : null;
        context.DrawRectangle(null, border, new RoundedRect(box.Deflate(0.75), 3.5));
    }

    /// <summary>The language label of the code block under <paramref name="point"/> (surface coordinates), as a block index and its rectangle; null elsewhere.</summary>
    internal (int Block, Rect Rect)? CodeHeaderAt(Point point)
    {
        if (!ShowCodeHeader || _views.Count == 0)
        {
            return null;
        }
        var index = BlockAtY(point.Y);
        var view = _views[index];
        if (!view.Block.IsCode)
        {
            return null;
        }
        var rect = CodeHeaderRect(index);
        return rect.Contains(point) ? (index, rect) : null;
    }

    /// <summary>The clickable language label of a code block, in surface coordinates.</summary>
    internal Rect CodeHeaderRect(int block)
    {
        var key = "lang:" + (_views[block].Block.Language ?? "Plain text");
        var width = _markerLayouts.TryGetValue(key, out var label) ? label.WidthIncludingTrailingWhitespace + 24 : 96;
        return new Rect(9, _tops[block] + 3, width, 24);
    }

    /// <summary>The to-do item whose checkbox is under <paramref name="point"/> (surface coordinates), or -1.</summary>
    private int TodoAt(Point point)
    {
        if (_views.Count == 0)
        {
            return -1;
        }
        var index = BlockAtY(point.Y);
        var view = _views[index];
        if (!view.Block.IsTodo || view.Layout is null)
        {
            return -1;
        }
        return TodoBoxRect(view, _tops[index]).Inflate(4).Contains(point) ? index : -1;
    }

    private void DrawSelection(DrawingContext context, int index, RichTextEditorBlockView view, Point origin, RichTextEditorSelection selection)
    {
        var from = index == selection.Start.Block ? selection.Start.Offset : 0;
        var to = index == selection.End.Block ? selection.End.Offset : view.Block.Length;
        var brush = SelectionBrush;
        if (to > from)
        {
            foreach (var rect in view.Layout!.HitTestTextRange(view.ToLayout(from), to - from))
            {
                context.FillRectangle(brush, rect.Translate(origin));
            }
        }
        if (index < selection.End.Block)
        {
            // The selection runs past the block end: show the line break as a narrow selected cell.
            var end = LineRect(new RichTextEditorPosition(index, view.Block.Length));
            context.FillRectangle(brush, new Rect(end.X, end.Y, view.FontSize * 0.32, end.Height));
        }
    }

    private void DrawPlaceholder(DrawingContext context, int index, RichTextEditorBlockView view, Point origin, ImmutableList<RichTextEditorBlock> blocks)
    {
        var owner = Owner;
        if (owner is null || !view.Block.IsEmpty || view.Preedit is { Length: > 0 } || view.Block.IsCode)
        {
            return;
        }
        var engine = Engine!;
        var empty = owner.IsDocumentEmpty;
        var focused = IsKeyboardFocusWithin && !owner.IsReadOnly;
        var hinted = focused && engine.Selection.IsCollapsed && engine.Selection.Focus.Block == index;
        string? text = null;
        if (empty && (index == 0 || hinted))
        {
            text = owner.Placeholder;
        }
        else if (hinted)
        {
            text = owner.BlockHint;
        }
        if (string.IsNullOrEmpty(text) || MutedBrush is not { } brush)
        {
            return;
        }
        var key = (text, view.FontSize, view.Weight, _styleVersion);
        if (_placeholderLayout is null || _placeholderKey != key)
        {
            _placeholderLayout?.Dispose();
            _placeholderLayout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, view.Weight), view.FontSize, brush,
                TextAlignment.Left, TextWrapping.NoWrap, lineHeight: view.LineHeight, letterSpacing: (view.Block.IsHeading ? -0.02 : Tracking) * view.FontSize);
            _placeholderKey = key;
        }
        using (context.PushClip(new Rect(origin.X, origin.Y, Math.Max(0, Bounds.Width - origin.X), _placeholderLayout.Height)))
        {
            _placeholderLayout.Draw(context, origin);
        }
    }
}
