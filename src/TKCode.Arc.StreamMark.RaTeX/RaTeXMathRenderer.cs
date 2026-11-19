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
    private readonly Dictionary<(string Latex, bool Display), LinkedListNode<Entry>> _map = new();
    private readonly LinkedList<Entry> _lru = new();
    private readonly object _gate = new();

    public RaTeXMathRenderer(int capacity = 512)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    /// <summary>当前缓存条目数。</summary>
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
        }

        // 排版放在锁外：原生调用可并发，偶发的重复排版无害
        var result = RaTeXEngine.Layout(latex, display, RaTeXMathLayout.DefaultColorSentinel);
        var entry = new Entry(key, result.DisplayList is { } list ? new RaTeXMathLayout(list, HasUncoloredFrame(latex)) : null, result.Error);

        lock (_gate)
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
        error = entry.Error;
        return entry.Layout;
    }

    // native/patches/0001 修复了 ratex-layout 中 \fbox / \boxed 边框固定为黑色的问题，
    // 边框已使用默认颜色，不再需要在绘制端把黑色细边框替换为前景色
    private static bool HasUncoloredFrame(string latex) => false;

    /// <summary>清空缓存。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
            _lru.Clear();
        }
    }

    private sealed record Entry((string Latex, bool Display) Key, RaTeXMathLayout? Layout, string? Error);
}
