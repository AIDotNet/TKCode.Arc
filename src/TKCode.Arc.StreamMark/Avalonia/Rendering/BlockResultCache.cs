namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 块视图慢结果（渲染好的图表、解析过的数据等）的小型 LRU 缓存，按内容键（通常是块的
/// <see cref="Core.Model.MarkdownBlock.ComputeFingerprint"/> 加上影响结果的环境）索引。
/// 视图被虚拟化回收、样式变化重建后，同样内容的块直接取结果而不必重算。
/// 一般作为插件视图类的静态字段，各文档共享；线程安全，后台任务里也可以写入。
/// </summary>
public sealed class BlockResultCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _items;
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new();
    private readonly object _lock = new();

    public BlockResultCache(int capacity = 32, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _items = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(comparer);
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _items.Count;
            }
        }
    }

    /// <summary>取结果；命中时把它记为最近使用。</summary>
    public bool TryGet(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddLast(node);
                value = node.Value.Value;
                return true;
            }
        }
        value = default!;
        return false;
    }

    /// <summary>放入结果（已有同键时替换）；超出容量时淘汰最久未用的一项。</summary>
    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
            }
            var node = _order.AddLast(new KeyValuePair<TKey, TValue>(key, value));
            _items[key] = node;
            while (_items.Count > _capacity)
            {
                var oldest = _order.First!;
                _order.RemoveFirst();
                _items.Remove(oldest.Value.Key);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
            _order.Clear();
        }
    }
}
