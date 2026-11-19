using Avalonia.Controls;
using Avalonia.Threading;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Pacing;
using StreamMark.Core.Parsing;
using StreamMark.Core.Streaming;

namespace StreamMark.Avalonia;

/// <summary>
/// 一条流式回答：把网络分片经 <see cref="StreamPacer"/> 平滑后送入 <see cref="StreamingDocument"/>，
/// 并记录每批文本的显示时间供渐显动画使用。<see cref="Push"/> / <see cref="Complete"/> 可在任意线程调用。
/// </summary>
/// <remarks>
/// 显示它的视图挂到窗口后，节奏控制器随显示帧（<see cref="TopLevel.RequestAnimationFrame"/>）推进，每帧最多放出一次文本；
/// 未挂载或窗口不再出帧（最小化、无头环境未驱动渲染）时退回 16ms 计时器，保证流总能结束。
/// </remarks>
public sealed class MarkdownStream
{
    private readonly StreamPacer _pacer;
    private readonly bool _smooth;
    private readonly DispatcherTimer _timer;
    private bool _completeRequested;
    private WeakReference<TopLevel>? _frameSource;
    private bool _tickScheduled;
    private int _tickGeneration;

    /// <summary>显示帧来源未在这个时间内出帧时，改由计时器推进。</summary>
    private static readonly TimeSpan FrameFallback = TimeSpan.FromMilliseconds(100);

    public MarkdownStream(StreamPacerOptions? pacing = null, bool smooth = true, TimeSpan? fadeDuration = null, MarkdownProfile? profile = null)
    {
        _pacer = new StreamPacer(pacing);
        _smooth = smooth;
        Document = new StreamingDocument(profile);
        State = new StreamingState(fadeDuration ?? TimeSpan.FromMilliseconds(220));
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => RunScheduled(_tickGeneration));
    }

    public StreamingDocument Document { get; }

    public StreamingState State { get; }

    /// <summary>流（包括平滑放出的积压）已全部结束。</summary>
    public event Action? Completed;

    /// <summary>由显示这条流的视图设置：之后按该窗口的显示帧推进（界面线程调用）。</summary>
    internal void SetFrameSource(TopLevel? top)
    {
        if (top is null)
        {
            return;
        }
        if (_frameSource is null || !_frameSource.TryGetTarget(out var current) || !ReferenceEquals(current, top))
        {
            _frameSource = new WeakReference<TopLevel>(top);
        }
    }

    public void Push(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (_completeRequested)
            {
                return;
            }
            State.IsActive = true;
            if (!_smooth)
            {
                Document.Append(text);
                State.RecordReveal(text.Length, StreamClock.Now);
                return;
            }
            var idle = !_tickScheduled;
            _pacer.Push(text, StreamClock.Now);
            if (idle)
            {
                Tick();
            }
        });
    }

    public void Complete() => Dispatcher.UIThread.Post(() =>
    {
        if (_completeRequested)
        {
            return;
        }
        _completeRequested = true;
        _pacer.Finish(StreamClock.Now);
        if (!_smooth || _pacer.IsDrained)
        {
            Finish();
        }
        else
        {
            ScheduleTick();
        }
    });

    /// <summary>立即显示全部积压（例如用户点击"跳到结尾"或窗口被隐藏）。</summary>
    public void Flush() => Dispatcher.UIThread.Post(() =>
    {
        var text = _pacer.Flush(StreamClock.Now);
        if (text.Length > 0)
        {
            Document.Append(text);
        }
    });

    private void Tick()
    {
        var now = StreamClock.Now;
        var text = _pacer.Tick(now);
        if (text.Length > 0)
        {
            Document.Append(text);
            State.RecordReveal(text.Length, now);
        }
        if (_pacer.Backlog > 0)
        {
            ScheduleTick();
        }
        else if (_completeRequested)
        {
            Finish();
        }
    }

    private void ScheduleTick()
    {
        if (_tickScheduled)
        {
            return;
        }
        _tickScheduled = true;
        var generation = ++_tickGeneration;
        if (_frameSource is not null && _frameSource.TryGetTarget(out var top))
        {
            top.RequestAnimationFrame(_ => RunScheduled(generation));
            _timer.Interval = FrameFallback;
        }
        else
        {
            _timer.Interval = TimeSpan.FromMilliseconds(16);
        }
        _timer.Start();
    }

    private void RunScheduled(int generation)
    {
        if (!_tickScheduled || generation != _tickGeneration)
        {
            return;
        }
        _tickScheduled = false;
        _timer.Stop();
        Tick();
    }

    private void Finish()
    {
        _timer.Stop();
        _tickScheduled = false;
        Document.Complete();
        State.IsActive = false;
        Completed?.Invoke();
    }
}
