using StreamMark.Avalonia.Extensions;

namespace StreamMark.RaTeX;

/// <summary>
/// 基于 RaTeX 原生引擎的 <see cref="IMathRenderer"/>。<see cref="Layout"/> 线程安全，
/// 结果（包括错误）按 (latex, display) 存入有界 LRU 缓存。
/// </summary>
public sealed class RaTeXMathRenderer : IMathRenderer
{
    /// <summary>进程共享的默认实例。</summary>
    public static RaTeXMathRenderer Shared { get; } = new();

    private readonly int _capacity;
    private readonly int _errorCapacity;
    private readonly Dictionary<(string Latex, bool Display), LinkedListNode<Entry>> _map = new();
    private readonly LinkedList<Entry> _lru = new();
    // 失败结果单独存放于小容量 LRU，防止流式半截残缺公式把历史已排版好的有效公式全部挤出主缓存
    private readonly Dictionary<(string Latex, bool Display), LinkedListNode<Entry>> _errorMap = new();
    private readonly LinkedList<Entry> _errorLru = new();
    private readonly Lock _gate = new();

    public RaTeXMathRenderer(int capacity = 512)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _errorCapacity = Math.Clamp(capacity / 4, 16, 128);
    }

    /// <summary>当前有效排版缓存条目数（受构造参数 <c>capacity</c> 限制）。</summary>
    public int CacheCount
    {
        get
        {
            lock (_gate)
            {
                return _map.Count;
            }
        }
    }

    /// <summary>当前失败结果缓存条目数（独立小容量 LRU，不会驱逐有效排版结果）。</summary>
    internal int ErrorCacheCount
    {
        get
        {
            lock (_gate)
            {
                return _errorMap.Count;
            }
        }
    }

    public MathLayout? Layout(string latex, bool display, out string? error)
    {
        ArgumentNullException.ThrowIfNull(latex);
        var key = (latex, display);
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                error = node.Value.Error;
                return node.Value.Layout;
            }
            if (_errorMap.TryGetValue(key, out var errNode))
            {
                _errorLru.Remove(errNode);
                _errorLru.AddFirst(errNode);
                error = errNode.Value.Error;
                return null;
            }
        }

        // 排版放在锁外：原生调用可并发，偶发的重复排版无害
        var result = RaTeXEngine.Layout(latex, display, RaTeXMathLayout.DefaultColorSentinel);
        var entry = new Entry(key, result.DisplayList is { } list ? new RaTeXMathLayout(list) : null, result.Error);

        lock (_gate)
        {
            if (entry.Layout is not null)
            {
                if (_map.TryGetValue(key, out var existing))
                {
                    error = existing.Value.Error;
                    return existing.Value.Layout;
                }
                _map[key] = _lru.AddFirst(entry);
                while (_map.Count > _capacity && _lru.Last is { } last)
                {
                    _lru.RemoveLast();
                    _map.Remove(last.Value.Key);
                }
            }
            else
            {
                if (_errorMap.TryGetValue(key, out var existingErr))
                {
                    error = existingErr.Value.Error;
                    return null;
                }
                _errorMap[key] = _errorLru.AddFirst(entry);
                while (_errorMap.Count > _errorCapacity && _errorLru.Last is { } lastErr)
                {
                    _errorLru.RemoveLast();
                    _errorMap.Remove(lastErr.Value.Key);
                }
            }
        }
        error = entry.Error;
        return entry.Layout;
    }

    /// <summary>清空缓存。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
            _lru.Clear();
            _errorMap.Clear();
            _errorLru.Clear();
        }
    }

    private sealed record Entry((string Latex, bool Display) Key, RaTeXMathLayout? Layout, string? Error);
}
