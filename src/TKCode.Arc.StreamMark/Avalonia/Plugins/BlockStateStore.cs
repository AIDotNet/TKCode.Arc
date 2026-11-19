namespace StreamMark.Avalonia.Plugins;

/// <summary>
/// 块级界面状态（展开 / 折叠、换行、图表与源码切换、滚动位置等），按 (顶层块 key, 状态名) 保存。
/// 每个文档一份，独立于视图：视图被虚拟化回收后再出现、或样式变化重建视图时，状态得以保留。
/// 源码只追加，块 key（源码起点）稳定；宿主整篇替换文本（<c>MarkdownView.RefreshMarkdown</c>）时由 <see cref="Remap"/> 搬到新 key。只在界面线程访问。
/// </summary>
public sealed class BlockStateStore
{
    private readonly Dictionary<(int Key, string Name), object> _values = [];

    public T Get<T>(int blockKey, string name, T fallback) =>
        _values.TryGetValue((blockKey, name), out var value) && value is T typed ? typed : fallback;

    public void Set<T>(int blockKey, string name, T value) where T : notnull => _values[(blockKey, name)] = value;

    public void Clear() => _values.Clear();

    /// <summary>
    /// 文档被编辑、块的 key（源码偏移）变了时搬运状态：<paramref name="map"/> 返回旧 key 对应的新 key，
    /// 返回 null 表示那个块已经不存在，它的状态被丢弃。
    /// </summary>
    public void Remap(Func<int, int?> map)
    {
        var moved = new Dictionary<(int Key, string Name), object>(_values.Count);
        foreach (var ((key, name), value) in _values)
        {
            if (map(key) is { } newKey)
            {
                moved[(newKey, name)] = value;
            }
        }
        _values.Clear();
        foreach (var (entry, value) in moved)
        {
            _values[entry] = value;
        }
    }
}
