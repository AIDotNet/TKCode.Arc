using System.Text;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Text selection (design/components/pdf/pdf.md section 5.6). The ends of a selection are kept as they were given, points of pages,
/// and turned into character positions whenever the text is there: a page's text is read only when the pointer first enters it,
/// so a press may come before its text does. Until it arrives such a selection simply has nothing to show, and it is worked out
/// from the same points when it does. Pages between the ends are read one after another, in the background.
/// </summary>
internal sealed class PdfSelectionLayer : PageLayer
{
    private enum SelectionUnit
    {
        Character,
        Word,
        Line
    }

    /// <summary>One end of a selection: a point of a page, or a position in its text when no pointer was involved.</summary>
    private readonly record struct End(int Page, Point Point, int? Index = null);

    /// <summary>What is selected on one page. <see cref="To"/> of int.MaxValue means up to the end of the page.</summary>
    private sealed record PageSelection(int From, int To, string Text, Rect[] Rects);

    private static Cursor? s_ibeam;

    private readonly PdfViewer _viewer;
    private readonly SortedDictionary<int, PageSelection> _selected = new();
    private End? _anchor;
    private End? _focus;
    private SelectionUnit _unit;
    private bool _hasRange;
    private bool _started;
    private bool _dragging;
    private End _press;
    private int _pendingPage = -1;
    private (int StartPage, int StartIndex, int EndPage, int EndIndex) _range;
    private TaskCompletionSource? _complete;

    public PdfSelectionLayer(PdfViewer viewer)
    {
        _viewer = viewer;
    }

    /// <summary>The pointer is down with a press this layer took.</summary>
    public bool IsDragging => _dragging;

    /// <summary>Whether anything is selected, or will be once its text arrives.</summary>
    public bool HasSelection => _hasRange || (_started && _anchor is not null);

    /// <summary>Finished when the text of every selected page has arrived.</summary>
    public Task WhenComplete => _complete?.Task ?? Task.CompletedTask;

    /// <summary>Selects a whole page.</summary>
    public void SelectPage(int pageIndex)
    {
        _anchor = new End(pageIndex, default, 0);
        _focus = new End(pageIndex, default, int.MaxValue);
        _unit = SelectionUnit.Character;
        _started = true;
        Recompute();
    }

    public void Clear()
    {
        _anchor = null;
        _focus = null;
        _started = false;
        _dragging = false;
        ClearRange();
    }

    private void ClearRange()
    {
        _hasRange = false;
        _pendingPage = -1;
        _selected.Clear();
        FinishComplete();
        _viewer.SetSelectedText(string.Empty);
        Invalidate();
    }

    /// <summary>The text of a page arrived; it may be what the selection was waiting for.</summary>
    public void OnTextArrived(int pageIndex)
    {
        if (pageIndex == _pendingPage || (_started && (_anchor?.Page == pageIndex || _focus?.Page == pageIndex)))
        {
            Recompute();
        }
        // The cursor over this page may change.
        Invalidate();
    }

    protected override void Render(DrawingContext context, in PageLayerContext page)
    {
        if (_selected.Count == 0 || !_selected.TryGetValue(page.PageIndex, out var selection) || _viewer.SelectionBrush is not { } brush)
        {
            return;
        }
        var (bounds, scale) = (page.PageBounds, page.Scale);
        foreach (var rect in selection.Rects)
        {
            context.FillRectangle(brush, new Rect(bounds.X + rect.X * scale, bounds.Y + rect.Y * scale, rect.Width * scale, rect.Height * scale));
        }
    }

    protected override void OnPointerPressed(PageLayerPointerArgs e)
    {
        if (!_viewer.CanSelectText)
        {
            return;
        }
        _viewer.RequestText(e.PageIndex);
        // A finger scrolls; only a double tap selects.
        if (e.PointerType == PointerType.Touch && e.ClickCount < 2)
        {
            return;
        }
        var point = new End(e.PageIndex, e.PointInPage);
        _press = point;
        _dragging = true;
        e.Handled = true;
        if ((e.Modifiers & KeyModifiers.Shift) != 0 && _anchor is not null && e.ClickCount <= 1)
        {
            _focus = point;
            _unit = SelectionUnit.Character;
            _started = true;
            Recompute();
            return;
        }
        _anchor = point;
        _focus = point;
        if (e.ClickCount >= 2)
        {
            _unit = e.ClickCount == 2 ? SelectionUnit.Word : SelectionUnit.Line;
            _started = true;
            Recompute();
            return;
        }
        _unit = SelectionUnit.Character;
        _started = false;
        // A click on a link is about the link: what is selected stays until the press turns out to be a drag.
        if (!_viewer.LinkLayer.IsPressingLink)
        {
            ClearRange();
        }
    }

    protected override void OnPointerMoved(PageLayerPointerArgs e)
    {
        if (!_dragging)
        {
            if (_viewer.CanSelectText)
            {
                _viewer.RequestText(e.PageIndex);
            }
            return;
        }
        e.Handled = true;
        if (!_started)
        {
            var delta = e.PointInPage - _press.Point;
            if (e.PageIndex == _press.Page && Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) * _viewer.Zoom <= PdfLinkLayer.ClickSlop)
            {
                return;
            }
            _started = true;
        }
        _focus = new End(e.PageIndex, e.PointInPage);
        Recompute();
    }

    protected override void OnPointerReleased(PageLayerPointerArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            e.Handled = true;
        }
    }

    protected override void OnPointerDragCancelled() => _dragging = false;

    protected override Cursor? GetCursor(int pageIndex, Point pointInPage)
    {
        if (!_viewer.CanSelectText)
        {
            return null;
        }
        if (_dragging || _viewer.TryGetText(pageIndex)?.IsOverText(pointInPage) == true)
        {
            return s_ibeam ??= new Cursor(StandardCursorType.Ibeam);
        }
        return null;
    }

    /// <summary>Works the selection out from its ends, as far as the text is there.</summary>
    private void Recompute()
    {
        if (!_started || _anchor is not { } anchor || _focus is not { } focus)
        {
            return;
        }
        var anchorText = _viewer.TryGetText(anchor.Page);
        var focusText = anchor.Page == focus.Page ? anchorText : _viewer.TryGetText(focus.Page);
        if (anchorText is null || focusText is null)
        {
            // Nothing to go by yet. Whatever was shown stays as it was; a guess could only be wrong.
            WaitFor(anchorText is null ? anchor.Page : focus.Page);
            return;
        }
        var (anchorStart, anchorEnd) = Resolve(anchor, anchorText);
        var (focusStart, focusEnd) = Resolve(focus, focusText);
        (int, int, int, int) range;
        if (anchor.Page == focus.Page)
        {
            range = (anchor.Page, Math.Min(anchorStart, focusStart), anchor.Page, Math.Max(anchorEnd, focusEnd));
        }
        else if (anchor.Page < focus.Page)
        {
            range = (anchor.Page, anchorStart, focus.Page, focusEnd);
        }
        else
        {
            range = (focus.Page, focusStart, anchor.Page, anchorEnd);
        }
        _range = range;
        _hasRange = range.Item1 != range.Item3 || range.Item2 != range.Item4;
        ApplyRange();
    }

    private (int Start, int End) Resolve(in End end, PdfPageText text)
    {
        if (end.Index is { } index)
        {
            var at = Math.Min(index, text.Text.Length);
            return (at, at);
        }
        var size = _viewer.PageSizeOf(end.Page);
        // Past the top or the bottom of the page (a drag over the gap to the next one) means all the way.
        var caret = end.Point.Y < 0 ? 0 : end.Point.Y > size.Height ? text.Text.Length : text.CaretAt(end.Point);
        switch (_unit)
        {
            case SelectionUnit.Word when text.Text.Length > 0:
                return text.WordAt(text.HitTest(end.Point) is >= 0 and var word ? word : caret);
            case SelectionUnit.Line when text.Text.Length > 0:
                return text.LineAt(text.HitTest(end.Point) is >= 0 and var line ? line : caret);
            default:
                return (caret, caret);
        }
    }

    /// <summary>Brings the per-page parts in line with <see cref="_range"/>, reusing pages that did not change: a drag touches only its end.</summary>
    private void ApplyRange()
    {
        var (startPage, startIndex, endPage, endIndex) = _range;
        if (!_hasRange)
        {
            ClearRange();
            return;
        }
        List<int>? gone = null;
        foreach (var page in _selected.Keys)
        {
            if (page < startPage || page > endPage)
            {
                (gone ??= new List<int>()).Add(page);
            }
        }
        if (gone is not null)
        {
            foreach (var page in gone)
            {
                _selected.Remove(page);
            }
        }
        var missing = -1;
        var changed = gone is not null;
        for (var page = startPage; page <= endPage; page++)
        {
            var from = page == startPage ? startIndex : 0;
            var to = page == endPage ? endIndex : int.MaxValue;
            if (_selected.TryGetValue(page, out var known) && known.From == from && known.To == to)
            {
                continue;
            }
            if (_viewer.TryGetText(page) is not { } text)
            {
                if (missing < 0)
                {
                    missing = page;
                }
                continue;
            }
            var end = Math.Min(to, text.Text.Length);
            var start = Math.Min(from, end);
            _selected[page] = new PageSelection(from, to, text.Text.Substring(start, end - start), text.GetRects(start, end - start).ToArray());
            changed = true;
        }
        if (missing >= 0)
        {
            WaitFor(missing);
        }
        else
        {
            _pendingPage = -1;
            if (changed || _viewer.SelectedText.Length == 0)
            {
                _viewer.SetSelectedText(BuildText());
            }
            FinishComplete();
        }
        Invalidate();
    }

    private void WaitFor(int page)
    {
        _pendingPage = page;
        _complete ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _viewer.RequestText(page);
    }

    private void FinishComplete()
    {
        var complete = _complete;
        _complete = null;
        complete?.TrySetResult();
    }

    /// <summary>The pages in order with one line break between them.</summary>
    private string BuildText()
    {
        var text = new StringBuilder();
        foreach (var part in _selected.Values)
        {
            var clean = Clean(part.Text);
            if (clean.Length == 0)
            {
                continue;
            }
            if (text.Length > 0)
            {
                text.Append('\n');
            }
            text.Append(clean);
        }
        return text.ToString();
    }

    /// <summary>Line ends as one character, and the control character PDFium writes for a line-end hyphen as what it stands for: the hyphen and the line end it left out.</summary>
    internal static string Clean(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\u0002", "-\n", StringComparison.Ordinal).TrimEnd('\n');
}
