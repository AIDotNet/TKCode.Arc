using Avalonia;
using Avalonia.Media;

namespace StreamMark.Avalonia.Images;

/// <summary>
/// 已解码图片的句柄。持有期间图片不会被淘汰或释放；最后一个句柄释放、且条目已被淘汰时才真正释放图片。
/// 每个句柄只能 <see cref="Dispose"/> 一次（重复调用无效果）。线程安全。
/// </summary>
public sealed class ImageLease : IDisposable
{
    private readonly ImageMemoryCache.Entry _entry;
    private int _disposed;

    internal ImageLease(ImageMemoryCache.Entry entry)
    {
        _entry = entry;
    }

    /// <summary>解码后的图片。释放句柄后不应再使用。</summary>
    public IImage Image => _entry.Image;

    /// <summary>图片原始像素尺寸（未缩放）。</summary>
    public PixelSize PixelSize => _entry.NaturalSize;

    /// <summary>实际解码的像素尺寸（按宽度档位缩小后可能小于原始尺寸）。</summary>
    public PixelSize DecodedSize => _entry.DecodedSize;

    /// <summary>规范化后的地址。</summary>
    public string Url => _entry.Url;

    /// <summary>把这张解码图移出内存缓存，之后的请求不再命中（例如查看器临时加载的原图）；图片在最后一个句柄释放时释放。</summary>
    public void Evict() => _entry.Owner.Evict(_entry);

    /// <summary>再取得一个独立的句柄（例如交给另一个视图）。</summary>
    public ImageLease AddRef()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _entry.Owner.AddRef(_entry);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _entry.Owner.Release(_entry);
        }
    }
}

/// <summary>
/// 已解码图片的内存缓存。键为（规范化地址，解码宽度档位），档位按 256px 向上取整，0 表示原始尺寸。
/// 按解码字节估算（宽 × 高 × 4）做 LRU 淘汰，默认预算 96MB；被句柄引用的条目不会被淘汰。
/// 同时记录原始像素尺寸，以及失败地址的负缓存（默认 5 分钟）。线程安全。
/// </summary>
public sealed class ImageMemoryCache : IDisposable
{
    /// <summary>默认字节预算：96MB。</summary>
    public const long DefaultByteBudget = 96L * 1024 * 1024;

    private const int MaxRememberedSizes = 8192;

    private readonly object _gate = new();
    private readonly Dictionary<string, List<Entry>> _byUrl = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _lru = new();
    private readonly Dictionary<string, PixelSize> _sizes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Reason, DateTimeOffset Until)> _failures = new(StringComparer.Ordinal);
    private readonly TimeProvider _time;
    private long _bytes;
    private bool _disposed;

    /// <param name="byteBudget">解码字节预算。</param>
    /// <param name="negativeCacheDuration">失败地址的负缓存时长，null 表示 5 分钟。</param>
    /// <param name="timeProvider">时钟（测试可注入），null 表示系统时钟。</param>
    public ImageMemoryCache(long byteBudget = DefaultByteBudget, TimeSpan? negativeCacheDuration = null, TimeProvider? timeProvider = null)
    {
        ByteBudget = byteBudget;
        NegativeCacheDuration = negativeCacheDuration ?? TimeSpan.FromMinutes(5);
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>字节预算。</summary>
    public long ByteBudget { get; }

    /// <summary>负缓存时长。</summary>
    public TimeSpan NegativeCacheDuration { get; }

    /// <summary>当前缓存的解码字节估算。</summary>
    public long CurrentBytes
    {
        get
        {
            lock (_gate)
            {
                return _bytes;
            }
        }
    }

    /// <summary>当前缓存的条目数。</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _lru.Count;
            }
        }
    }

    /// <summary>
    /// 查找可用的解码结果：同档位、原始尺寸（档位 0）或更大档位都可满足请求。命中时返回新句柄并刷新 LRU。
    /// </summary>
    public ImageLease? TryAcquire(string url, int bucket)
    {
        lock (_gate)
        {
            if (!_byUrl.TryGetValue(url, out var entries))
            {
                return null;
            }
            Entry? best = null;
            foreach (var entry in entries)
            {
                if (entry.Bucket == bucket)
                {
                    best = entry;
                    break;
                }
                // 原始尺寸或更大的档位都能满足；优先选最接近的
                if (bucket > 0 && (entry.Bucket == 0 || entry.Bucket > bucket))
                {
                    if (best is null || Rank(entry) < Rank(best))
                    {
                        best = entry;
                    }
                }
            }
            if (best is null)
            {
                return null;
            }
            Touch(best);
            best.Refs++;
            return new ImageLease(best);
        }

        static int Rank(Entry e) => e.Bucket == 0 ? int.MaxValue : e.Bucket;
    }

    /// <summary>
    /// 加入解码结果并返回一个句柄。若同键已存在，释放新图片并返回已有条目的句柄。
    /// 加入后按预算淘汰未被引用的最久未用条目。
    /// </summary>
    /// <param name="url">规范化地址。</param>
    /// <param name="bucket">宽度档位（0 = 原始尺寸）。</param>
    /// <param name="image">图片；若实现 <see cref="IDisposable"/>，淘汰且无句柄时释放。</param>
    /// <param name="decodedSize">解码尺寸，用于字节估算。</param>
    /// <param name="naturalSize">原始尺寸。</param>
    public ImageLease Add(string url, int bucket, IImage image, PixelSize decodedSize, PixelSize naturalSize)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(image);
        IDisposable? duplicate = null;
        List<IDisposable>? evicted;
        ImageLease lease;
        lock (_gate)
        {
            RememberSize(url, naturalSize);
            _failures.Remove(url);
            if (!_byUrl.TryGetValue(url, out var entries))
            {
                entries = new List<Entry>(1);
                _byUrl[url] = entries;
            }
            var existing = entries.Find(e => e.Bucket == bucket);
            if (existing is not null)
            {
                if (!ReferenceEquals(existing.Image, image))
                {
                    duplicate = image as IDisposable;
                }
                Touch(existing);
                existing.Refs++;
                lease = new ImageLease(existing);
            }
            else
            {
                var entry = new Entry(this, url, bucket, image, decodedSize, naturalSize);
                entry.Node = _lru.AddFirst(entry);
                entries.Add(entry);
                _bytes += entry.Bytes;
                entry.Refs = 1;
                lease = new ImageLease(entry);
            }
            evicted = TrimLocked();
            if (_disposed)
            {
                // 缓存已释放：条目只靠句柄存活
                evicted = RemoveAllLocked(evicted);
            }
        }
        duplicate?.Dispose();
        DisposeAll(evicted);
        return lease;
    }

    /// <summary>已知的原始像素尺寸（即使解码结果已被淘汰）。</summary>
    public bool TryGetPixelSize(string url, out PixelSize size)
    {
        lock (_gate)
        {
            return _sizes.TryGetValue(url, out size);
        }
    }

    /// <summary>记录原始像素尺寸（例如从磁盘元数据读到）。</summary>
    public void RememberPixelSize(string url, PixelSize size)
    {
        lock (_gate)
        {
            RememberSize(url, size);
        }
    }

    /// <summary>记录一次失败，在 <see cref="NegativeCacheDuration"/> 内不再重试。</summary>
    public void AddFailure(string url, string reason)
    {
        lock (_gate)
        {
            _failures[url] = (reason, _time.GetUtcNow() + NegativeCacheDuration);
        }
    }

    /// <summary>查询负缓存；过期的记录会被移除。</summary>
    public bool TryGetFailure(string url, out string reason)
    {
        lock (_gate)
        {
            if (_failures.TryGetValue(url, out var failure))
            {
                if (_time.GetUtcNow() < failure.Until)
                {
                    reason = failure.Reason;
                    return true;
                }
                _failures.Remove(url);
            }
        }
        reason = "";
        return false;
    }

    /// <summary>清除某个地址的负缓存（例如用户点击“重试”）。</summary>
    public void ForgetFailure(string url)
    {
        lock (_gate)
        {
            _failures.Remove(url);
        }
    }

    /// <summary>移除所有条目；仍被句柄引用的图片在最后一个句柄释放时才释放。负缓存一并清除。</summary>
    public void Clear()
    {
        List<IDisposable>? evicted;
        lock (_gate)
        {
            evicted = RemoveAllLocked(null);
            _failures.Clear();
            _sizes.Clear();
        }
        DisposeAll(evicted);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
        Clear();
    }

    internal ImageLease AddRef(Entry entry)
    {
        lock (_gate)
        {
            entry.Refs++;
            return new ImageLease(entry);
        }
    }

    internal void Evict(Entry entry)
    {
        List<IDisposable>? evicted = null;
        lock (_gate)
        {
            if (!entry.Evicted)
            {
                RemoveLocked(entry, ref evicted);
            }
        }
        DisposeAll(evicted);
    }

    internal void Release(Entry entry)
    {
        List<IDisposable>? evicted = null;
        lock (_gate)
        {
            entry.Refs--;
            if (entry.Refs > 0)
            {
                return;
            }
            if (entry.Evicted)
            {
                if (entry.Image is IDisposable disposable)
                {
                    (evicted ??= []).Add(disposable);
                }
            }
            else
            {
                evicted = TrimLocked();
            }
        }
        DisposeAll(evicted);
    }

    private List<IDisposable>? TrimLocked()
    {
        List<IDisposable>? evicted = null;
        var node = _lru.Last;
        while (_bytes > ByteBudget && node is not null)
        {
            var previous = node.Previous;
            var entry = node.Value;
            if (entry.Refs == 0)
            {
                RemoveLocked(entry, ref evicted);
            }
            node = previous;
        }
        return evicted;
    }

    private List<IDisposable>? RemoveAllLocked(List<IDisposable>? evicted)
    {
        while (_lru.Last is { } node)
        {
            RemoveLocked(node.Value, ref evicted);
        }
        return evicted;
    }

    private void RemoveLocked(Entry entry, ref List<IDisposable>? evicted)
    {
        if (entry.Node is not null)
        {
            _lru.Remove(entry.Node);
            entry.Node = null;
        }
        if (_byUrl.TryGetValue(entry.Url, out var entries))
        {
            entries.Remove(entry);
            if (entries.Count == 0)
            {
                _byUrl.Remove(entry.Url);
            }
        }
        _bytes -= entry.Bytes;
        entry.Evicted = true;
        if (entry.Refs == 0 && entry.Image is IDisposable disposable)
        {
            (evicted ??= []).Add(disposable);
        }
    }

    private void Touch(Entry entry)
    {
        if (entry.Node is not null && entry.Node != _lru.First)
        {
            _lru.Remove(entry.Node);
            _lru.AddFirst(entry.Node);
        }
    }

    private void RememberSize(string url, PixelSize size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (_sizes.Count >= MaxRememberedSizes && !_sizes.ContainsKey(url))
        {
            _sizes.Clear();
        }
        _sizes[url] = size;
    }

    private static void DisposeAll(List<IDisposable>? items)
    {
        if (items is null)
        {
            return;
        }
        foreach (var item in items)
        {
            try
            {
                item.Dispose();
            }
            catch (Exception)
            {
                // 释放失败不影响缓存状态
            }
        }
    }

    internal sealed class Entry(ImageMemoryCache owner, string url, int bucket, IImage image, PixelSize decodedSize, PixelSize naturalSize)
    {
        public ImageMemoryCache Owner { get; } = owner;
        public string Url { get; } = url;
        public int Bucket { get; } = bucket;
        public IImage Image { get; } = image;
        public PixelSize DecodedSize { get; } = decodedSize;
        public PixelSize NaturalSize { get; } = naturalSize;
        public long Bytes { get; } = Math.Max(1L, (long)decodedSize.Width * decodedSize.Height * 4);
        public int Refs { get; set; }
        public bool Evicted { get; set; }
        public LinkedListNode<Entry>? Node { get; set; }
    }
}
