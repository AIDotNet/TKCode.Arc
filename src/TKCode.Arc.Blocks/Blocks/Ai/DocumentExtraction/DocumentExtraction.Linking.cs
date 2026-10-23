using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class DocumentExtraction
{
    /// <summary>How far from the pane's edge a field scrolled into view lands (spec 5.2: about 56px).</summary>
    internal const double ScrollMargin = 56;

    /// <summary>The form's own scrolling (spec 5.2: about 200ms, a strong ease-out).</summary>
    private static readonly TimeSpan FormScrollTime = TimeSpan.FromMilliseconds(220);

    private string? _hoverPath;
    private readonly Dictionary<string, string> _pathSpan = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _spanPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _freshUntil = new(StringComparer.Ordinal);
    private readonly List<string> _expired = new();
    private readonly Dictionary<string, DocumentSpanPaint> _spanPaints = new(StringComparer.Ordinal);
    private string? _linkKey;
    private Func<SourceLinkAnchor?>? _linkStart;
    private Func<SourceLinkAnchor?>? _linkEnd;
    private MotionValue<double>? _formScroll;
    private double _formScrollTarget = double.NaN;
    private bool _selecting;
    private bool _followPaused;
    private bool _followPending;
    private string? _lastLanded;

    /// <summary>The path the document and the form are linked on now: the pointer's, over the selection.</summary>
    private string? LinkedPath => _hoverPath ?? SelectedPath;

    /// <summary>The temporary link of the pointer (tests).</summary>
    internal string? HoverPath => _hoverPath;

    /// <summary>Whether following the landing values is paused for the rest of the run (tests).</summary>
    internal bool IsFollowPaused => _followPaused;

    #region Selection and hover

    /// <summary>Selects <paramref name="path"/> from inside the block (a focused field, a clicked span or JSON line).</summary>
    private void SelectPath(string? path)
    {
        _selecting = true;
        try
        {
            SetCurrentValue(SelectedPathProperty, path);
        }
        finally
        {
            _selecting = false;
        }
    }

    private void OnSelectedPathChanged(string? oldPath, string? newPath)
    {
        ApplyLinked(_hoverPath ?? oldPath);
        RaiseEvent(new ExtractionPathEventArgs(SelectedPathChangedEvent, this, newPath));
        if (!_selecting && newPath is not null)
        {
            // Set from outside: both panes bring it into view.
            ScrollToSource(newPath);
            ScrollFormTo(newPath, animate: true);
        }
    }

    private void SetHover(string? path)
    {
        if (path == _hoverPath)
        {
            return;
        }
        var previous = LinkedPath;
        _hoverPath = path;
        ApplyLinked(previous);
    }

    /// <summary>
    /// The link moved from <paramref name="previous"/> to <see cref="LinkedPath"/>: the rows, the table cells and the JSON
    /// line light up (group color, 160ms in their themes), the cited text of both is repainted and the curve redrawn.
    /// </summary>
    private void ApplyLinked(string? previous, bool force = false)
    {
        var linked = LinkedPath;
        if (!force && previous == linked)
        {
            UpdateLink();
            return;
        }
        foreach (var (key, row) in _rows)
        {
            row.IsLinked = linked == key;
        }
        foreach (var (key, table) in _tables)
        {
            table.LinkedPath = linked is not null && (linked == key || linked.StartsWith(key + ".", StringComparison.Ordinal)) ? linked : null;
        }
        if (_json is not null)
        {
            _json.LinkedPath = SelectedTab == ExtractionTab.Json ? linked : null;
        }
        if (previous is not null && _pathSpan.TryGetValue(previous, out var oldSpan))
        {
            PaintSpan(oldSpan);
        }
        if (linked is not null && _pathSpan.TryGetValue(linked, out var newSpan))
        {
            PaintSpan(newSpan);
        }
        UpdateLink();
    }

    private void OnFieldPathEntered(object? sender, ExtractionPathEventArgs e)
    {
        if (e.Path is { } path && _store?.Find(path) is not null || e.Path is not null && _tables.ContainsKey(e.Path))
        {
            SetHover(e.Path);
        }
    }

    private void OnFieldPathExited(object? sender, ExtractionPathEventArgs e)
    {
        if (e.Path == _hoverPath)
        {
            SetHover(null);
        }
    }

    /// <summary>A field took the focus (Tab, a click, Review): it is selected and its cited text scrolls into view (spec 6.2).</summary>
    private void OnFieldPathFocused(object? sender, ExtractionPathEventArgs e)
    {
        if (e.Path is not { } path)
        {
            return;
        }
        SelectPath(path);
        ScrollToSource(path);
        ScrollFormTo(path, animate: true);
    }

    private void OnJsonPathEntered(object? sender, ExtractionPathEventArgs e)
    {
        if (ReferenceEquals(e.Source, _json))
        {
            SetHover(e.Path);
        }
    }

    private void OnJsonPathExited(object? sender, ExtractionPathEventArgs e)
    {
        if (ReferenceEquals(e.Source, _json) && e.Path == _hoverPath)
        {
            SetHover(null);
        }
    }

    private void OnJsonPathPressed(object? sender, ExtractionPathEventArgs e)
    {
        if (ReferenceEquals(e.Source, _json) && e.Path is { } path)
        {
            SelectPath(path);
            ScrollToSource(path);
        }
    }

    private void OnSpanEntered(object? sender, DocumentSpanEventArgs e)
    {
        if (PathOfSpan(e.SpanId) is { } path)
        {
            SetHover(path);
        }
    }

    private void OnSpanExited(object? sender, DocumentSpanEventArgs e)
    {
        if (_hoverPath is not null && PathOfSpan(e.SpanId) == _hoverPath)
        {
            SetHover(null);
        }
    }

    /// <summary>A click on cited text selects its value and focuses its field (or brings its JSON line into view).</summary>
    private void OnSpanPressed(object? sender, DocumentSpanEventArgs e)
    {
        if (PathOfSpan(e.SpanId) is not { } path)
        {
            return;
        }
        SelectPath(path);
        if (SelectedTab == ExtractionTab.Fields)
        {
            FocusField(path, NavigationMethod.Pointer);
        }
        ScrollFormTo(path, animate: true);
    }

    /// <summary>The first value (in schema order) citing <paramref name="spanId"/>.</summary>
    private string? PathOfSpan(string spanId)
    {
        if (!_spanPaths.TryGetValue(spanId, out var paths) || paths.Count == 0)
        {
            return null;
        }
        if (paths.Count == 1 || _store is null)
        {
            return paths[0];
        }
        foreach (var value in _store.Values)
        {
            if (paths.Contains(value.Path))
            {
                return value.Path;
            }
        }
        return paths[0];
    }

    /// <summary>Focuses the input of a field or cell; false when it has none.</summary>
    private bool FocusField(string path, NavigationMethod method)
    {
        if (_rows.TryGetValue(path, out var row))
        {
            return row.FocusInput(method);
        }
        var table = TableOfPath(path);
        return table?.FocusCell(path, method) == true;
    }

    private ExtractionTableGrid? TableOfPath(string path)
    {
        var dot = path.IndexOf('.');
        var key = dot < 0 ? path : path[..dot];
        return _tables.GetValueOrDefault(key);
    }

    #endregion

    #region Cited text

    /// <summary>
    /// Re-reads where the changed values point: a value that lost or changed its span releases the old one, and every
    /// span touched is repainted.
    /// </summary>
    private void UpdateSpans(IReadOnlyCollection<string> paths)
    {
        if (_store is not { } store)
        {
            return;
        }
        foreach (var path in paths)
        {
            var value = store.Find(path);
            var span = value is { Status: not ExtractionValueStatus.Pending, SourceId: { Length: > 0 } id } ? id : null;
            _pathSpan.TryGetValue(path, out var old);
            if (old != span)
            {
                if (old is not null)
                {
                    _pathSpan.Remove(path);
                    if (_spanPaths.TryGetValue(old, out var list))
                    {
                        list.Remove(path);
                    }
                    PaintSpan(old);
                }
                if (span is not null)
                {
                    _pathSpan[path] = span;
                    if (!_spanPaths.TryGetValue(span, out var list))
                    {
                        list = new List<string>(1);
                        _spanPaths[span] = list;
                    }
                    list.Add(path);
                }
                else
                {
                    _freshUntil.Remove(path);
                }
            }
            if (span is not null)
            {
                PaintSpan(span);
            }
        }
    }

    /// <summary>Paints every span again (a new template or viewer).</summary>
    private void RepaintAllSpans()
    {
        foreach (var span in _spanPaths.Keys)
        {
            PaintSpan(span);
        }
    }

    /// <summary>
    /// A span's paint from the values citing it (spec 3.3): tinted in the group color once cited, deep for about 525ms after
    /// a value landed on it (Full motion only), lit while its value is linked, underlined while a value needs a check.
    /// </summary>
    private void PaintSpan(string span)
    {
        if (_viewer is null)
        {
            return;
        }
        if (_store is not { } store || !_spanPaths.TryGetValue(span, out var paths) || paths.Count == 0)
        {
            _spanPaints.Remove(span);
            _viewer.SetSpan(span, null);
            return;
        }
        var linked = LinkedPath;
        var now = Time.GetUtcNow();
        var lit = false;
        var fresh = false;
        var check = false;
        var tone = store.Info.ToneOf(paths[0]);
        foreach (var path in paths)
        {
            lit |= linked == path;
            fresh |= _freshUntil.TryGetValue(path, out var until) && until > now;
            check |= store.Find(path)?.NeedsCheck == true;
        }
        var state = fresh ? DocumentSpanState.Fresh : lit ? DocumentSpanState.Lit : DocumentSpanState.Linked;
        var paint = new DocumentSpanPaint(state, tone, check);
        _spanPaints[span] = paint;
        _viewer.SetSpan(span, paint);
    }

    /// <summary>The paint last given to a span (tests; null when it is not tinted).</summary>
    internal DocumentSpanPaint? SpanPaintOf(string span) => _spanPaints.TryGetValue(span, out var paint) ? paint : null;

    /// <summary>The fresh tints that ran out go back to their resting paint.</summary>
    private void ExpireFresh(DateTimeOffset now)
    {
        _expired.Clear();
        foreach (var (path, until) in _freshUntil)
        {
            if (until <= now)
            {
                _expired.Add(path);
            }
        }
        foreach (var path in _expired)
        {
            _freshUntil.Remove(path);
            if (_pathSpan.TryGetValue(path, out var span))
            {
                PaintSpan(span);
            }
        }
    }

    #endregion

    #region Link

    /// <summary>
    /// Draws (or hides) the curve between the linked value's cited text and its field, JSON line or table row (spec 3.8).
    /// Not when stacked, on the Schema tab, or for a value without cited text. The overlay asks for both ends every frame;
    /// the same pair of ends is kept while the link stays on the same value, so the curve is drawn once.
    /// </summary>
    private void UpdateLink()
    {
        if (_links is null)
        {
            return;
        }
        var path = LinkedPath;
        var tab = SelectedTab;
        if (_isStacked || tab == ExtractionTab.Schema || path is null || _store?.Find(path) is not { Status: not ExtractionValueStatus.Pending } value ||
            value.SourceId is not { Length: > 0 } span)
        {
            _links.Hide();
            _linkKey = null;
            return;
        }
        var key = $"{tab}|{path}|{span}";
        if (key != _linkKey || _linkStart is null || _linkEnd is null)
        {
            _linkKey = key;
            _linkStart = () => StartAnchor(span);
            _linkEnd = tab == ExtractionTab.Json ? () => JsonAnchor(path) : () => FieldAnchor(path);
        }
        _links.Show(_linkStart, _linkEnd, PromptTone.SeriesColor(this, _store.Info.ToneOf(path)));
    }

    private SourceLinkAnchor? StartAnchor(string span)
    {
        if (_viewer is null || _links is null || !_viewer.TryGetSpanBounds(span, _links, out var bounds, out var visible))
        {
            return null;
        }
        return new SourceLinkAnchor(bounds, visible);
    }

    /// <summary>The field's end: the form pane's left edge at the height of the row (or the table row), visible while in the panel.</summary>
    private SourceLinkAnchor? FieldAnchor(string path)
    {
        if (_links is null || _formPane is null || _fieldsScroll is null || ElementOf(path) is not { } element || element.Bounds.Height <= 0)
        {
            return null;
        }
        // A field's end is its value box's line (Arc: the box's middle, also while the reason below it is open).
        var height = element.Bounds.Height;
        if (element is ExtractionFieldRow { Input: { Bounds.Height: > 0 } input })
        {
            element = input;
            height = input.Bounds.Height;
        }
        if (element.TranslatePoint(default, _links) is not { } origin || _formPane.TranslatePoint(default, _links) is not { } pane ||
            element.TranslatePoint(default, _fieldsScroll) is not { } inPanel)
        {
            return null;
        }
        var center = inPanel.Y + height / 2;
        var visible = _fieldsScroll.IsEffectivelyVisible && center >= StickyHeight && center <= _fieldsScroll.Viewport.Height;
        return new SourceLinkAnchor(new Rect(pane.X, origin.Y, 1, height), visible);
    }

    /// <summary>The JSON line's end: the pane's left edge at the line, visible while in the JSON panel.</summary>
    private SourceLinkAnchor? JsonAnchor(string path)
    {
        if (_links is null || _formPane is null || _json is null || _jsonScroll is null ||
            !_json.TryGetLineBounds(path, _links, out var line) || !_json.TryGetLineBounds(path, _jsonScroll, out var inPanel) ||
            _formPane.TranslatePoint(default, _links) is not { } pane)
        {
            return null;
        }
        var center = inPanel.Y + inPanel.Height / 2;
        var visible = _jsonScroll.IsEffectivelyVisible && center >= 0 && center <= _jsonScroll.Viewport.Height;
        return new SourceLinkAnchor(new Rect(pane.X, line.Y, 1, line.Height), visible);
    }

    /// <summary>The row of a field, or the row of a table cell (the table itself for its key).</summary>
    private Control? ElementOf(string path)
    {
        if (_rows.TryGetValue(path, out var row))
        {
            return row;
        }
        if (TableOfPath(path) is not { } table)
        {
            return null;
        }
        if (_store is not null && _store.Info.TryResolve(path, out _, out _, out var index) && index is { } r)
        {
            return table.FindRow(r) ?? table;
        }
        return table;
    }

    /// <summary>The height the sticky status line covers at the top of the Fields panel.</summary>
    private double StickyHeight => _status is { IsVisible: true } status ? status.Bounds.Height + status.Margin.Bottom : 0;

    #endregion

    #region Scrolling

    /// <summary>The canvas brings the value's cited text into view (the viewer's nearest scroll).</summary>
    private void ScrollToSource(string path)
    {
        if (_store?.Find(path) is { Status: not ExtractionValueStatus.Pending, SourceId: { Length: > 0 } span })
        {
            _viewer?.ScrollToSpan(span, ArcMotion.IsFull(this));
        }
    }

    /// <summary>
    /// The panel of the selected tab brings the value into view: nothing when it is in view already, else the shortest
    /// scroll that leaves it about 56px from the nearest edge (below the sticky status line). Eased over 220ms at full motion
    /// (any scrolling by the person stops it); a jump otherwise.
    /// </summary>
    private void ScrollFormTo(string path, bool animate)
    {
        ScrollViewer? panel;
        double top;
        double height;
        double inset;
        if (SelectedTab == ExtractionTab.Json)
        {
            panel = _jsonScroll;
            if (panel?.Content is not Visual content || _json is null || !_json.TryGetLineBounds(path, content, out var line))
            {
                return;
            }
            top = line.Y;
            height = line.Height;
            inset = 0;
        }
        else if (SelectedTab == ExtractionTab.Fields)
        {
            panel = _fieldsScroll;
            if (panel?.Content is not Visual content || ElementOf(path) is not { } element || element.Bounds.Height <= 0 ||
                element.TranslatePoint(default, content) is not { } origin)
            {
                return;
            }
            top = origin.Y;
            height = element.Bounds.Height;
            inset = StickyHeight;
        }
        else
        {
            return;
        }
        var viewport = panel.Viewport.Height;
        if (viewport <= 0)
        {
            return;
        }
        var offset = double.IsNaN(_formScrollTarget) || _formScroll is not { IsAnimating: true } ? panel.Offset.Y : _formScrollTarget;
        double target;
        if (top < offset + inset)
        {
            target = top - inset - ScrollMargin;
        }
        else if (top + height > offset + viewport)
        {
            target = top + height - viewport + ScrollMargin;
        }
        else
        {
            return;
        }
        var max = Math.Max(0, panel.Extent.Height - viewport);
        target = Math.Clamp(target, 0, max);
        ScrollPanel(panel, target, animate && ArcMotion.IsFull(this) && IsLoaded);
    }

    private void ScrollPanel(ScrollViewer panel, double target, bool animate)
    {
        _formScroll?.Dispose();
        _formScroll = null;
        _formScrollTarget = double.NaN;
        if (!animate)
        {
            panel.Offset = new Vector(panel.Offset.X, target);
            return;
        }
        var motion = new MotionValue<double>(panel, panel.Offset.Y, MotionVectors.Double);
        motion.Changed += y => panel.Offset = new Vector(panel.Offset.X, y);
        _formScroll = motion;
        _formScrollTarget = target;
        _ = motion.AnimateTo(target, MotionSpec.Tween(FormScrollTime, ArcEasings.Enter));
    }

    /// <summary>
    /// Following (spec 5.2): while a run goes each landing value is brought into view in both panes, until the person scrolls
    /// either pane by hand; then it pauses for the rest of the run.
    /// </summary>
    private void Follow()
    {
        if (_followPaused || _run is null || _lastLanded is not { } path)
        {
            return;
        }
        ScrollToSource(path);
        // New rows are laid out first: the form scrolls once the layout of this turn is done.
        Dispatcher.UIThread.Post(() =>
        {
            if (!_followPaused && _lastLanded == path)
            {
                ScrollFormTo(path, animate: true);
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>A wheel over either pane is the person scrolling: our own scrolling stops and following pauses.</summary>
    private void OnAnyWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Source is Visual source && (InPane(source, _viewer) || InPane(source, _formPane)))
        {
            UserScrolled();
        }
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _keyboardMode = false;
        // Dragging a scroll bar is scrolling by hand too.
        if (e.Source is Visual source && source.FindAncestorOfType<ScrollBar>(includeSelf: true) is not null)
        {
            UserScrolled();
        }
    }

    private static bool InPane(Visual source, Visual? pane) =>
        pane is not null && (ReferenceEquals(source, pane) || pane.IsVisualAncestorOf(source));

    private void UserScrolled()
    {
        _formScroll?.Dispose();
        _formScroll = null;
        _formScrollTarget = double.NaN;
        if (_run is not null)
        {
            _followPaused = true;
        }
    }

    /// <summary>The panels scrolled: the sticky status line stays on top (with its edge once scrolled) and keys scrolling count as the person's.</summary>
    private void OnPanelScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _fieldsScroll) && _fieldsScroll is not null)
        {
            var y = _fieldsScroll.Offset.Y;
            _statusShift.Y = y;
            if (_status is not null)
            {
                _status.ShowsEdge = y > 0.5;
            }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        // Scrolling a focused panel with the keys pauses following, as a wheel does.
        if (e.Source is ScrollViewer && e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End or Key.Space)
        {
            UserScrolled();
        }
    }

    #endregion
}
