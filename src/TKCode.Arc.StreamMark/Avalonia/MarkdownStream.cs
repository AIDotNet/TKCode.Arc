using System.Text;
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
/// 高频 <see cref="Push"/> 会在入口先合并再投递到界面线程，避免 300+ TPS 时向调度器队列投递大量闭包。
/// </remarks>
public sealed class MarkdownStream
{
    private readonly StreamPacer _pacer;
    private readonly bool _smooth;
    private readonly DispatcherTimer _timer;
    private readonly Action _drainIngress;
    private readonly Action<TimeSpan> _onFrame;
    private readonly SyncLock _ingressLock = new();
    private readonly StringBuilder _ingress = new();
    private TimeSpan _ingressTime;
    private bool _completeQueued;
    private bool _tickScheduledLocked;
    private int _ingressPosted;

    private bool _completeRequested;
    private WeakReference<TopLevel>? _frameSource;
    private WeakReference<TopLevel>? _rafTop;
    private bool _rafPending;
    private bool _tickScheduled;
    private TimeSpan _scheduledAt;

    /// <summary>显示帧来源未在这个时间内出帧时，改由计时器推进。</summary>
    private static readonly TimeSpan FrameFallback = TimeSpan.FromMilliseconds(100);

    public MarkdownStream(StreamPacerOptions? pacing = null, bool smooth = true, TimeSpan? fadeDuration = null, MarkdownProfile? profile = null)
    {
        _pacer = new StreamPacer(pacing);
        _smooth = smooth;
        Document = new StreamingDocument(profile);
        State = new StreamingState(fadeDuration ?? TimeSpan.FromMilliseconds(220));
        _drainIngress = OnDrainPosted;
        _onFrame = OnAnimationFrame;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnFallbackTimer);
    }

    public StreamingDocument Document { get; }

    public StreamingState State { get; }

    /// <summary>是否在流式输出期间在末尾显示块状光标（默认 <c>true</c>）。</summary>
    public bool ShowCaret
    {
        get => State.ShowCaret;
        set => State.SetShowCaret(value);
    }

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
        bool shouldPost;
        lock (_ingressLock)
        {
            if (_completeQueued)
            {
                return;
            }
            _ingress.Append(text);
            _ingressTime = StreamClock.Now;
            // 平滑模式且下一帧已排期时，由帧回调统一拉取入口缓冲，避免 300 TPS 频繁唤醒 UI 线程
            shouldPost = !(_smooth && _tickScheduledLocked);
        }
        if (shouldPost && Interlocked.Exchange(ref _ingressPosted, 1) == 0)
        {
            Dispatcher.UIThread.Post(_drainIngress);
        }
    }

    public void Complete()
    {
        lock (_ingressLock)
        {
            if (_completeQueued)
            {
                return;
            }
            _completeQueued = true;
        }
        if (Interlocked.Exchange(ref _ingressPosted, 1) == 0)
        {
            Dispatcher.UIThread.Post(_drainIngress);
        }
    }

    /// <summary>立即显示全部积压（例如用户点击"跳到结尾"或窗口被隐藏）。</summary>
    public void Flush() => Dispatcher.UIThread.Post(() =>
    {
        DrainIngress(fromScheduledTick: false);
        var text = _pacer.Flush(StreamClock.Now);
        if (text.Length > 0)
        {
            Document.Append(text);
        }
    });

    private void OnDrainPosted() => DrainIngress(fromScheduledTick: false);

    private void DrainIngress(bool fromScheduledTick)
    {
        string? text;
        TimeSpan now;
        bool complete;
        lock (_ingressLock)
        {
            if (fromScheduledTick)
            {
                _tickScheduledLocked = false;
            }
            Volatile.Write(ref _ingressPosted, 0);
            if (_ingress.Length > 0)
            {
                text = _ingress.ToString();
                _ingress.Clear();
                if (_ingress.Capacity > 64 * 1024)
                {
                    _ingress.Capacity = 4096;
                }
            }
            else
            {
                text = null;
            }
            now = _ingressTime;
            complete = _completeQueued && !_completeRequested;
        }

        if (text is not null && !_completeRequested)
        {
            State.IsActive = true;
            if (!_smooth)
            {
                Document.Append(text);
                State.RecordReveal(text.Length, now);
            }
            else
            {
                var idle = !_tickScheduled && !fromScheduledTick;
                _pacer.Push(text, now);
                if (idle)
                {
                    Tick();
                }
            }
        }

        if (complete)
        {
            _completeRequested = true;
            _pacer.Finish(StreamClock.Now);
            if (!fromScheduledTick)
            {
                if (!_smooth || _pacer.IsDrained)
                {
                    Finish();
                }
                else
                {
                    ScheduleTick();
                }
            }
        }
    }

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
        else
        {
            _timer.Stop();
        }
    }

    private void ScheduleTick()
    {
        if (_tickScheduled)
        {
            return;
        }
        _tickScheduled = true;
        lock (_ingressLock)
        {
            _tickScheduledLocked = true;
        }
        _scheduledAt = StreamClock.Now;
        if (_frameSource is not null && _frameSource.TryGetTarget(out var top))
        {
            if (!_rafPending || _rafTop is null || !_rafTop.TryGetTarget(out var rafTop) || !ReferenceEquals(rafTop, top))
            {
                _rafPending = true;
                _rafTop = _frameSource;
                top.RequestAnimationFrame(_onFrame);
            }
            if (_timer.Interval != FrameFallback)
            {
                _timer.Interval = FrameFallback;
            }
        }
        else
        {
            var interval = TimeSpan.FromMilliseconds(16);
            if (_timer.Interval != interval)
            {
                _timer.Interval = interval;
            }
        }
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void OnAnimationFrame(TimeSpan _)
    {
        _rafPending = false;
        RunScheduled();
    }

    private void OnFallbackTimer(object? sender, EventArgs e)
    {
        if (!_tickScheduled)
        {
            _timer.Stop();
            return;
        }
        // 有显示帧来源且尚未超过兜底时间时，把推进权留给 RequestAnimationFrame，避免每帧启停定时器
        if (_frameSource is not null && _frameSource.TryGetTarget(out _) && StreamClock.Now - _scheduledAt < FrameFallback)
        {
            return;
        }
        RunScheduled();
    }

    private void RunScheduled()
    {
        if (!_tickScheduled)
        {
            return;
        }
        _tickScheduled = false;
        // 统一拉取上一帧至今积累的所有 Push，再一次性推进节奏控制器
        DrainIngress(fromScheduledTick: true);
        Tick();
    }

    private void Finish()
    {
        _timer.Stop();
        _tickScheduled = false;
        lock (_ingressLock)
        {
            _tickScheduledLocked = false;
        }
        Document.Complete();
        State.IsActive = false;
        Completed?.Invoke();
    }
}
