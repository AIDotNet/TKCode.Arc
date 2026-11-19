namespace StreamMark.Avalonia.Selection;

/// <summary>
/// 在文档 / 会话中查找文字。在块模型上按片段搜索（与视图无关，被虚拟化移出屏幕的内容也能找到；
/// 会话中尚未解析的按需条目会被解析），结果以选区坐标表示，由视图绘制高亮。
/// <see cref="Next"/> / <see cref="Previous"/> 移动当前结果并滚动到它。
/// </summary>
public sealed class SearchModel
{
    private readonly ISelectionHost _host;
    private readonly List<(TextPoint Start, TextPoint End)> _matches = [];

    internal SearchModel(ISelectionHost host)
    {
        _host = host;
    }

    public string Query { get; private set; } = "";

    public bool MatchCase { get; private set; }

    public IReadOnlyList<(TextPoint Start, TextPoint End)> Matches => _matches;

    public int Count => _matches.Count;

    /// <summary>当前结果序号（0 起），没有结果时为 -1。</summary>
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>结果或当前结果变化。</summary>
    public event Action? Changed;

    /// <summary>查找并高亮全部结果，当前结果设为第一个（不滚动）。返回结果数。</summary>
    public int Find(string query, bool matchCase = false)
    {
        Query = query;
        MatchCase = matchCase;
        _matches.Clear();
        if (!string.IsNullOrEmpty(query))
        {
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            for (var entry = 0; entry < _host.EntryCount; entry++)
            {
                if (_host.GetSnapshot(entry) is not { } snapshot)
                {
                    continue;
                }
                foreach (var block in snapshot.Blocks)
                {
                    var segments = BlockText.Segments(block, _host.Plugins);
                    for (var s = 0; s < segments.Count; s++)
                    {
                        var text = segments[s].Content?.Text ?? segments[s].Raw ?? "";
                        for (var i = text.IndexOf(query, comparison); i >= 0; i = text.IndexOf(query, i + query.Length, comparison))
                        {
                            _matches.Add((new TextPoint(entry, block.Key, s, i), new TextPoint(entry, block.Key, s, i + query.Length)));
                        }
                    }
                }
            }
        }
        CurrentIndex = _matches.Count > 0 ? 0 : -1;
        Publish();
        return _matches.Count;
    }

    /// <summary>移动到下一个结果（循环）并滚动到它。</summary>
    public void Next() => MoveTo(_matches.Count == 0 ? -1 : (CurrentIndex + 1) % _matches.Count);

    /// <summary>移动到上一个结果（循环）并滚动到它。</summary>
    public void Previous() => MoveTo(_matches.Count == 0 ? -1 : (CurrentIndex - 1 + _matches.Count) % _matches.Count);

    /// <summary>移动到指定结果并滚动到它。</summary>
    public void MoveTo(int index)
    {
        if (index < 0 || index >= _matches.Count)
        {
            return;
        }
        CurrentIndex = index;
        Publish();
        _host.ScrollToPoint(_matches[index].Start);
    }

    /// <summary>清除查找结果与高亮。</summary>
    public void Clear()
    {
        Query = "";
        _matches.Clear();
        CurrentIndex = -1;
        _host.Selection.ClearHighlights();
        Changed?.Invoke();
    }

    private void Publish()
    {
        if (_matches.Count == 0)
        {
            _host.Selection.ClearHighlights();
        }
        else
        {
            _host.Selection.SetHighlights(_matches, CurrentIndex);
        }
        Changed?.Invoke();
    }
}
