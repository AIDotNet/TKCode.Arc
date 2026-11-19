namespace StreamMark.Avalonia.Selection;

/// <summary>
/// 跨块、跨消息的选区。只记录两个 <see cref="TextPoint"/>，与哪些视图已实例化无关，
/// 因此被虚拟化移出屏幕的块仍然在选区内，复制时从块模型取文本。
/// </summary>
public sealed class SelectionModel
{
    private TextPoint? _anchor;
    private TextPoint? _active;

    /// <summary>选区变化（视图据此重绘选区高亮）。</summary>
    public event Action? Changed;

    public bool IsEmpty => _anchor is null || _active is null || _anchor == _active;

    public TextPoint? Anchor => _anchor;

    public TextPoint? Active => _active;

    public (TextPoint Start, TextPoint End)? Range
    {
        get
        {
            if (IsEmpty)
            {
                return null;
            }
            var a = _anchor!.Value;
            var b = _active!.Value;
            return a <= b ? (a, b) : (b, a);
        }
    }

    public void Set(TextPoint anchor, TextPoint active)
    {
        if (_anchor == anchor && _active == active)
        {
            return;
        }
        _anchor = anchor;
        _active = active;
        Changed?.Invoke();
    }

    public void Extend(TextPoint active)
    {
        if (_anchor is null)
        {
            _anchor = active;
        }
        if (_active == active)
        {
            return;
        }
        _active = active;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_anchor is null && _active is null)
        {
            return;
        }
        _anchor = null;
        _active = null;
        Changed?.Invoke();
    }

    // 查找结果高亮：按片段索引
    private Dictionary<SegmentKey, List<(int Start, int End, int Index)>> _highlights = [];
    private int _currentHighlight = -1;

    /// <summary>设置查找结果高亮（每个结果位于单个片段内）与当前结果序号。</summary>
    public void SetHighlights(IReadOnlyList<(TextPoint Start, TextPoint End)> matches, int current)
    {
        var map = new Dictionary<SegmentKey, List<(int, int, int)>>();
        for (var i = 0; i < matches.Count; i++)
        {
            var (start, end) = matches[i];
            var key = new SegmentKey(start.Entry, start.BlockKey, start.Segment);
            if (!map.TryGetValue(key, out var list))
            {
                map[key] = list = [];
            }
            list.Add((start.Offset, end.Offset, i));
        }
        _highlights = map;
        _currentHighlight = current;
        Changed?.Invoke();
    }

    public void ClearHighlights()
    {
        if (_highlights.Count == 0)
        {
            return;
        }
        _highlights = [];
        _currentHighlight = -1;
        Changed?.Invoke();
    }

    /// <summary>某个片段内的查找高亮区间；<c>Current</c> 表示当前结果。</summary>
    public IEnumerable<(int Start, int End, bool Current)> GetHighlights(SegmentKey key, int length)
    {
        if (!_highlights.TryGetValue(key, out var list))
        {
            yield break;
        }
        foreach (var (start, end, index) in list)
        {
            var s = Math.Clamp(start, 0, length);
            var e = Math.Clamp(end, 0, length);
            if (e > s)
            {
                yield return (s, e, index == _currentHighlight);
            }
        }
    }

    /// <summary>某个片段内被选中的字符区间（左闭右开），未选中返回 null。</summary>
    public (int Start, int End)? GetSegmentRange(SegmentKey key, int length)
    {
        // 编号为负的片段（如自动生成的折叠块标题）不参与选区
        if (key.Segment < 0 || Range is not { } range)
        {
            return null;
        }
        var segmentStart = key.At(0);
        var segmentEnd = key.At(int.MaxValue);
        if (range.End < segmentStart || range.Start > segmentEnd)
        {
            return null;
        }
        var start = range.Start <= segmentStart ? 0 : Math.Min(range.Start.Offset, length);
        var end = range.End >= key.At(length) ? length : Math.Min(range.End.Offset, length);
        return end > start ? (start, end) : null;
    }
}
