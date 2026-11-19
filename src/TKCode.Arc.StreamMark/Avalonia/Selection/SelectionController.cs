using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using StreamMark.Core.Model;
using StreamMark.Core.Streaming;

namespace StreamMark.Avalonia.Selection;

/// <summary>选区宿主：提供已实例化的文本片段、各文档的快照与源码。</summary>
public interface ISelectionHost
{
    Control HostControl { get; }

    SelectionModel Selection { get; }

    /// <summary>已实例化的文本片段（阅读顺序）。</summary>
    IEnumerable<ISelectableSegment> RealizedSegments();

    int EntryCount { get; }

    /// <summary>第 <paramref name="entry"/> 个文档的当前快照（非 Markdown 条目返回 null）。</summary>
    DocumentSnapshot? GetSnapshot(int entry);

    /// <summary>第 <paramref name="entry"/> 个文档的源码片段。</summary>
    string GetSource(int entry, int start, int length);

    /// <summary>拖拽选择时指针超出可视区域，宿主可借此自动滚动。</summary>
    void OnSelectionDrag(Point hostPoint) { }

    /// <summary>插件注册表（复制时行内组件的纯文本由插件提供）。</summary>
    Plugins.PluginRegistry? Plugins => null;

    /// <summary>滚动到选区坐标所在位置（查找结果跳转）。</summary>
    void ScrollToPoint(TextPoint point) { }
}

/// <summary>
/// 处理鼠标选择（单击定位、拖拽、双击选词、三击选段、Shift 扩展）与复制。
/// 选区坐标与视图无关，复制时从块模型取文本，所以被虚拟化移出屏幕的块也能复制。
/// </summary>
public sealed class SelectionController
{
    private readonly ISelectionHost _host;
    private bool _dragging;
    // 按下后尚未移动：此时不捕获指针，松开事件照常送达链接 / 图片 / 引用等可点击内容
    private bool _pressPending;
    private Point _pressPoint;
    private IPointer? _pressPointer;

    /// <summary>按下后移动超过这个距离才开始拖拽选择（并捕获指针）。</summary>
    private const double DragThreshold = 3;

    public SelectionController(ISelectionHost host)
    {
        _host = host;
        var control = host.HostControl;
        control.AddHandler(InputElement.PointerPressedEvent, OnPressed, handledEventsToo: false);
        control.AddHandler(InputElement.PointerMovedEvent, OnMoved, handledEventsToo: true);
        control.AddHandler(InputElement.PointerReleasedEvent, OnReleased, handledEventsToo: true);
        control.AddHandler(InputElement.KeyDownEvent, OnKeyDown, handledEventsToo: false);
    }

    /// <summary>所控制的选区。</summary>
    public SelectionModel Selection => _host.Selection;

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_host.HostControl);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }
        // 按在宿主内容（会话中的 ContentEntry 控件）上：交给它自己处理点击，不开始选择
        if (IsHostContent(e.Source as Visual))
        {
            return;
        }
        _host.HostControl.Focus();
        if (FindPoint(point.Position) is not { } hit)
        {
            Selection.Clear();
            return;
        }
        var (segment, position) = hit;
        switch (e.ClickCount)
        {
            case 2:
                var (start, end) = WordAt(segment.Text, position.Offset);
                Selection.Set(position with { Offset = start }, position with { Offset = end });
                break;
            case >= 3:
                Selection.Set(position with { Offset = 0 }, position with { Offset = segment.Text.Length });
                break;
            default:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && Selection.Anchor is not null)
                {
                    Selection.Extend(position);
                }
                else
                {
                    Selection.Set(position, position);
                }
                break;
        }
        _dragging = false;
        _pressPending = e.ClickCount == 1;
        _pressPoint = point.Position;
        _pressPointer = e.Pointer;
    }

    // 指针所在的控件是否属于宿主提供的内容（而不是 Markdown 块视图或宿主本身的空白处）
    internal bool IsHostContent(Visual? source)
    {
        var host = _host.HostControl;
        for (var visual = source; visual is not null && !ReferenceEquals(visual, host); visual = visual.GetVisualParent())
        {
            if (visual is Rendering.BlockView or ISelectableSegment || visual is global::Avalonia.Controls.Primitives.OverlayLayer)
            {
                return false;
            }
            if (ReferenceEquals(visual.GetVisualParent(), host))
            {
                // 宿主的直接子控件不是块视图：宿主内容
                return true;
            }
        }
        return false;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(_host.HostControl);
        if (_pressPending && ReferenceEquals(e.Pointer, _pressPointer))
        {
            var delta = position - _pressPoint;
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) < DragThreshold)
            {
                return;
            }
            // 真正开始拖拽：捕获指针，指针移出宿主也能继续选择
            _pressPending = false;
            _dragging = true;
            e.Pointer.Capture(_host.HostControl);
        }
        if (!_dragging)
        {
            return;
        }
        _host.OnSelectionDrag(position);
        if (FindPoint(position) is { } hit)
        {
            Selection.Extend(hit.Point);
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressPending = false;
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var platform = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        var copy = platform?.Copy.Any(g => g.Matches(e)) ?? (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control));
        var selectAll = platform?.SelectAll.Any(g => g.Matches(e)) ?? (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control));
        if (copy && !Selection.IsEmpty)
        {
            e.Handled = true;
            await CopyAsync(asMarkdown: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }
        else if (selectAll)
        {
            e.Handled = true;
            SelectAll();
        }
        else if (e.Key == Key.Escape)
        {
            Selection.Clear();
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && ExtendByKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 键盘扩展选区（需要已有选区或插入点）：Shift+←/→ 按字符（Alt / Ctrl 按词），可跨片段；Shift+↑/↓ 按显示行；
    /// Shift+Home/End 到片段首尾。返回是否处理了该按键。
    /// </summary>
    public bool ExtendByKey(Key key, KeyModifiers modifiers = KeyModifiers.Shift)
    {
        if (Selection.Active is not { } active)
        {
            return false;
        }
        var byWord = modifiers.HasFlag(KeyModifiers.Alt) || modifiers.HasFlag(KeyModifiers.Control);
        TextPoint? target = key switch
        {
            Key.Left => MoveHorizontal(active, -1, byWord),
            Key.Right => MoveHorizontal(active, 1, byWord),
            Key.Up => MoveVertical(active, -1),
            Key.Down => MoveVertical(active, 1),
            Key.Home => active with { Offset = 0 },
            Key.End => SegmentLength(active) is { } length ? active with { Offset = length } : null,
            _ => null,
        };
        if (target is not { } point)
        {
            return key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End;
        }
        Selection.Extend(point);
        return true;
    }

    // 片段文本（来自块模型，被虚拟化的片段也能取到）
    private string? SegmentText(TextPoint point)
    {
        if (_host.GetSnapshot(point.Entry) is not { } snapshot)
        {
            return null;
        }
        foreach (var block in snapshot.Blocks)
        {
            if (block.Key == point.BlockKey)
            {
                var segments = BlockText.Segments(block, _host.Plugins);
                return point.Segment >= 0 && point.Segment < segments.Count ? segments[point.Segment].Content?.Text ?? segments[point.Segment].Raw ?? "" : null;
            }
        }
        return null;
    }

    private int? SegmentLength(TextPoint point) => SegmentText(point)?.Length;

    private TextPoint? MoveHorizontal(TextPoint point, int direction, bool byWord)
    {
        if (SegmentText(point) is not { } text)
        {
            return null;
        }
        if (direction < 0 ? point.Offset > 0 : point.Offset < text.Length)
        {
            var offset = point.Offset + direction;
            if (byWord)
            {
                // 跳过空白，再跳过一个词
                offset = point.Offset;
                if (direction < 0)
                {
                    while (offset > 0 && char.IsWhiteSpace(text[offset - 1])) offset--;
                    while (offset > 0 && !char.IsWhiteSpace(text[offset - 1])) offset--;
                }
                else
                {
                    while (offset < text.Length && char.IsWhiteSpace(text[offset])) offset++;
                    while (offset < text.Length && !char.IsWhiteSpace(text[offset])) offset++;
                }
            }
            else if (direction < 0 && offset > 0 && char.IsLowSurrogate(text[offset]))
            {
                offset--;
            }
            else if (direction > 0 && offset < text.Length && char.IsLowSurrogate(text[offset]))
            {
                offset++;
            }
            return point with { Offset = offset };
        }
        // 片段边界：移到相邻片段的末尾 / 开头
        return NeighbourSegment(point, direction) is { } neighbour
            ? direction < 0 ? neighbour with { Offset = SegmentLength(neighbour) ?? 0 } : neighbour with { Offset = 0 }
            : null;
    }

    // 阅读顺序中的相邻片段（跨块、跨消息）
    private TextPoint? NeighbourSegment(TextPoint point, int direction)
    {
        TextPoint? previous = null;
        var found = false;
        for (var entry = 0; entry < _host.EntryCount; entry++)
        {
            if (_host.GetSnapshot(entry) is not { } snapshot)
            {
                continue;
            }
            foreach (var block in snapshot.Blocks)
            {
                var count = BlockText.Segments(block, _host.Plugins).Count;
                for (var s = 0; s < count; s++)
                {
                    var current = new TextPoint(entry, block.Key, s, 0);
                    if (found)
                    {
                        return current;
                    }
                    if (entry == point.Entry && block.Key == point.BlockKey && s == point.Segment)
                    {
                        if (direction < 0)
                        {
                            return previous;
                        }
                        found = true;
                    }
                    previous = current;
                }
            }
        }
        return null;
    }

    // 按显示行移动：取当前位置字符框的上方 / 下方一点，按坐标命中
    private TextPoint? MoveVertical(TextPoint point, int direction)
    {
        var key = new SegmentKey(point.Entry, point.BlockKey, point.Segment);
        var segment = _host.RealizedSegments().FirstOrDefault(s => s.Key == key);
        if (segment is null)
        {
            return null;
        }
        var rect = segment.GetCharacterBounds(point.Offset);
        // 逐步加大探测距离：块之间有间距，紧挨着的探测点可能仍命中当前行
        foreach (var distance in new[] { 2, rect.Height * 0.5 + 2, rect.Height + 14, rect.Height * 2 + 28 })
        {
            var probe = new Point(rect.X + 1, direction < 0 ? rect.Top - distance : rect.Bottom + distance);
            if (segment.View.TranslatePoint(probe, _host.HostControl) is not { } hostPoint || FindPoint(hostPoint) is not { } hit)
            {
                continue;
            }
            var sameLine = hit.Segment.Key == key && Math.Abs(hit.Segment.GetCharacterBounds(hit.Point.Offset).Top - rect.Top) < 1;
            if (!sameLine)
            {
                return hit.Point;
            }
        }
        return null;
    }

    public void SelectAll()
    {
        for (var first = 0; first < _host.EntryCount; first++)
        {
            if (_host.GetSnapshot(first) is not { Count: > 0 } firstSnapshot)
            {
                continue;
            }
            for (var last = _host.EntryCount - 1; last >= first; last--)
            {
                if (_host.GetSnapshot(last) is not { Count: > 0 } lastSnapshot)
                {
                    continue;
                }
                var lastBlock = lastSnapshot[lastSnapshot.Count - 1];
                var segments = BlockText.Segments(lastBlock, _host.Plugins);
                var end = segments.Count == 0
                    ? new TextPoint(last, lastBlock.Key, 0, 0)
                    : new TextPoint(last, lastBlock.Key, segments.Count - 1, segments[^1].Length);
                Selection.Set(new TextPoint(first, firstSnapshot[0].Key, 0, 0), end);
                return;
            }
        }
    }

    public async Task CopyAsync(bool asMarkdown = false)
    {
        var text = asMarkdown ? GetSelectedMarkdown() : GetSelectedText();
        if (!string.IsNullOrEmpty(text) && TopLevel.GetTopLevel(_host.HostControl)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    /// <summary>选中内容的纯文本（公式为 <c>$latex$</c>，表格单元格以制表符分隔）。</summary>
    public string GetSelectedText()
    {
        if (Selection.Range is not { } range)
        {
            return "";
        }
        var sb = new StringBuilder();
        var first = true;
        for (var entry = range.Start.Entry; entry <= range.End.Entry; entry++)
        {
            if (_host.GetSnapshot(entry) is not { } snapshot)
            {
                continue;
            }
            foreach (var block in snapshot.Blocks)
            {
                var blockStart = new TextPoint(entry, block.Key, 0, 0);
                var blockEnd = new TextPoint(entry, block.Key, int.MaxValue, int.MaxValue);
                if (blockEnd < range.Start || blockStart > range.End)
                {
                    continue;
                }
                var segments = BlockText.Segments(block, _host.Plugins);
                for (var i = 0; i < segments.Count; i++)
                {
                    var key = new SegmentKey(entry, block.Key, i);
                    var segment = segments[i];
                    var s = range.Start <= key.At(0) ? 0 : range.Start > key.At(int.MaxValue) ? -1 : range.Start.Offset;
                    var e = range.End >= key.At(segment.Length) ? segment.Length : range.End < key.At(0) ? -1 : range.End.Offset;
                    if (s < 0 || e < 0 || e < s || (e == s && segment.Length > 0))
                    {
                        continue;
                    }
                    if (!first)
                    {
                        // 块与块之间空一行，块内片段之间按片段自身的分隔符
                        sb.Append(i == 0 ? "\n\n" : segment.Separator);
                    }
                    if (s == 0)
                    {
                        sb.Append(segment.Prefix);
                    }
                    sb.Append(segment.Slice(s, e, ComponentText));
                    first = false;
                }
            }
        }
        return sb.ToString();
    }

    private string? ComponentText(StreamMark.Core.Model.InlineObject obj) => _host.Plugins?.FindInlineComponent(obj.Value)?.ToPlainText(obj);

    /// <summary>选中内容所覆盖的完整块的 Markdown 源码。</summary>
    public string GetSelectedMarkdown()
    {
        if (Selection.Range is not { } range)
        {
            return "";
        }
        var sb = new StringBuilder();
        for (var entry = range.Start.Entry; entry <= range.End.Entry; entry++)
        {
            if (_host.GetSnapshot(entry) is not { } snapshot)
            {
                continue;
            }
            int start = int.MaxValue, end = -1;
            foreach (var block in snapshot.Blocks)
            {
                var blockStart = new TextPoint(entry, block.Key, 0, 0);
                var blockEnd = new TextPoint(entry, block.Key, int.MaxValue, int.MaxValue);
                if (blockEnd < range.Start || blockStart > range.End)
                {
                    continue;
                }
                start = Math.Min(start, block.Range.Start);
                end = Math.Max(end, block.Range.End);
            }
            if (end > start)
            {
                if (sb.Length > 0)
                {
                    sb.Append("\n\n");
                }
                sb.Append(_host.GetSource(entry, start, end - start).TrimEnd());
            }
        }
        return sb.ToString();
    }

    private (ISelectableSegment Segment, TextPoint Point)? FindPoint(Point hostPoint)
    {
        var control = _host.HostControl;
        // 先找指针正下方的片段
        if (control.InputHitTest(hostPoint) is Visual visual)
        {
            foreach (var ancestor in visual.GetSelfAndVisualAncestors())
            {
                if (ancestor is ISelectableSegment segment)
                {
                    var local = control.TranslatePoint(hostPoint, segment.View) ?? default;
                    return (segment, segment.Key.At(segment.HitTest(local)));
                }
                if (ReferenceEquals(ancestor, control))
                {
                    break;
                }
            }
        }

        // 否则取纵向最近的片段：在其上方取开头，在其下方取末尾，在其行内按横坐标命中
        ISelectableSegment? best = null;
        var bestDistance = double.MaxValue;
        Rect bestRect = default;
        foreach (var segment in _host.RealizedSegments())
        {
            if (!segment.View.IsEffectivelyVisible || segment.View.TranslatePoint(default, control) is not { } origin)
            {
                continue;
            }
            var rect = new Rect(origin, segment.View.Bounds.Size);
            var distance = hostPoint.Y < rect.Top ? rect.Top - hostPoint.Y : hostPoint.Y > rect.Bottom ? hostPoint.Y - rect.Bottom : 0;
            if (distance < bestDistance)
            {
                best = segment;
                bestDistance = distance;
                bestRect = rect;
                if (distance == 0)
                {
                    break;
                }
            }
        }
        if (best is null)
        {
            return null;
        }
        int offset;
        if (hostPoint.Y < bestRect.Top)
        {
            offset = 0;
        }
        else if (hostPoint.Y > bestRect.Bottom)
        {
            offset = best.Text.Length;
        }
        else
        {
            offset = best.HitTest(new Point(hostPoint.X - bestRect.X, hostPoint.Y - bestRect.Y));
        }
        return (best, best.Key.At(offset));
    }

    internal static (int Start, int End) WordAt(string text, int offset)
    {
        if (text.Length == 0)
        {
            return (0, 0);
        }
        offset = Math.Clamp(offset, 0, text.Length - 1);
        static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
        if (!IsWord(text[offset]))
        {
            return (offset, offset + 1);
        }
        var start = offset;
        while (start > 0 && IsWord(text[start - 1]))
        {
            start--;
        }
        var end = offset;
        while (end < text.Length && IsWord(text[end]))
        {
            end++;
        }
        return (start, end);
    }
}
