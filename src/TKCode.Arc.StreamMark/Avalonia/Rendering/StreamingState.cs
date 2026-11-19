namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 文档的流式显示状态：是否仍在流式输出（显示光标）、最近显示的字符何时出现（渐显）。
/// 由 <see cref="MarkdownStream"/> 更新，由文档末尾的文本片段读取。
/// </summary>
public sealed class StreamingState
{
    private readonly object _lock = new();
    private readonly Queue<(long End, TimeSpan Time)> _reveals = new();
    private long _revealed;
    // 已移出队列的批次的终点：队列第一批的起点
    private long _trimmed;

    public StreamingState(TimeSpan fadeDuration)
    {
        FadeDuration = fadeDuration;
    }

    public TimeSpan FadeDuration { get; }

    /// <summary>仍在流式输出。</summary>
    public bool IsActive { get; internal set; }

    /// <summary>是否显示末尾光标。</summary>
    public bool ShowCaret { get; init; } = true;

    /// <summary>是否启用新文本渐显。</summary>
    public bool FadeEnabled { get; init; } = true;

    internal void RecordReveal(int count, TimeSpan now)
    {
        if (count <= 0)
        {
            return;
        }
        lock (_lock)
        {
            _revealed += count;
            _reveals.Enqueue((_revealed, now));
            while (_reveals.Count > 0 && now - _reveals.Peek().Time > FadeDuration && _reveals.Count > 1)
            {
                _trimmed = _reveals.Dequeue().End;
            }
        }
    }

    internal void Reset()
    {
        lock (_lock)
        {
            _reveals.Clear();
            _revealed = 0;
            _trimmed = 0;
        }
    }

    /// <summary>
    /// 仍在渐显中的批次（从旧到新）：每批的起点距文本末尾的字符数、字符数与显示时间。
    /// </summary>
    internal void GetFadingBatches(TimeSpan now, List<(long FromEnd, int Length, TimeSpan Time)> result)
    {
        result.Clear();
        if (!FadeEnabled)
        {
            return;
        }
        lock (_lock)
        {
            var previousEnd = _trimmed;
            foreach (var (end, time) in _reveals)
            {
                if (now - time < FadeDuration)
                {
                    result.Add((_revealed - previousEnd, (int)(end - previousEnd), time));
                }
                previousEnd = end;
            }
        }
    }

    /// <summary>
    /// 仍在渐显中的末尾字符：返回 (字符数, 最早那批的不透明度, 最新那批的不透明度)；没有则字符数为 0。
    /// </summary>
    public (int Count, double OldestOpacity, double NewestOpacity) GetFadingTail(TimeSpan now)
    {
        if (!FadeEnabled)
        {
            return (0, 1, 1);
        }
        lock (_lock)
        {
            long fadingFrom = -1;
            TimeSpan oldest = default, newest = default;
            var previousEnd = _trimmed;
            foreach (var (end, time) in _reveals)
            {
                if (now - time < FadeDuration)
                {
                    if (fadingFrom < 0)
                    {
                        fadingFrom = previousEnd;
                        oldest = time;
                    }
                    newest = time;
                }
                previousEnd = end;
            }
            if (fadingFrom < 0)
            {
                return (0, 1, 1);
            }
            var d = FadeDuration.TotalMilliseconds;
            return ((int)(_revealed - fadingFrom),
                Math.Clamp((now - oldest).TotalMilliseconds / d, 0, 1),
                Math.Clamp((now - newest).TotalMilliseconds / d, 0, 1));
        }
    }
}
