using System.Globalization;
using System.Text;

namespace StreamMark.Core.Pacing;

/// <summary>节奏预设：目标落后时间越短，显示越贴近网络到达，越长越平滑。</summary>
public enum PacingPreset
{
    /// <summary>目标落后约 40ms。</summary>
    Responsive,
    /// <summary>目标落后约 120ms（默认）。</summary>
    Balanced,
    /// <summary>目标落后约 200ms。</summary>
    Smooth,
}

public sealed record StreamPacerOptions
{
    public static StreamPacerOptions Default { get; } = new();

    public PacingPreset Preset { get; init; } = PacingPreset.Balanced;

    /// <summary>任何字符从到达到显示的最长等待时间。</summary>
    public TimeSpan MaxLag { get; init; } = TimeSpan.FromMilliseconds(400);

    /// <summary>流结束后放完剩余积压的时间范围。</summary>
    public TimeSpan MinDrain { get; init; } = TimeSpan.FromMilliseconds(180);

    public TimeSpan MaxDrain { get; init; } = TimeSpan.FromMilliseconds(520);

    /// <summary>
    /// 积压中已经包含完整的块（遇到空行）时，直接放出到最后一个空行，只对最后一个未完成的块做逐字平滑。
    /// 适合网络突发很大的情况（例如恢复连接后一次到达多个段落）。默认关闭。
    /// </summary>
    public bool SkipCompletedBlocks { get; init; }

    /// <summary>积压超过这个字符数时直接放出（例如一次性粘贴的大段文本）。</summary>
    public int BypassThreshold { get; init; } = 4096;

    /// <summary>记录显示时间的最近批次数（每次 <see cref="StreamPacer.Tick"/> 放出的一批文本为一个批次，供渐显动画使用）。</summary>
    public int RevealHistory { get; init; } = 256;

    internal TimeSpan TargetLag => Preset switch
    {
        PacingPreset.Responsive => TimeSpan.FromMilliseconds(40),
        PacingPreset.Smooth => TimeSpan.FromMilliseconds(200),
        _ => TimeSpan.FromMilliseconds(120),
    };
}

/// <summary>
/// 把突发的网络分片整理成平滑的显示节奏。纯算法、无计时器：调用方在到达文本时调用 <see cref="Push"/>，
/// 在每个动画帧调用 <see cref="Tick"/> 取出本帧应显示的文本。时间由调用方传入，便于测试。
/// </summary>
/// <remarks>
/// 速率 = max(到达速率的滑动平均, 积压 / 目标落后时间)，并保证任何字符的等待不超过 <see cref="StreamPacerOptions.MaxLag"/>；
/// 流结束后在 180–520ms 内放完剩余积压。只在字素簇边界切分，中日韩文字逐字显示。
/// 非线程安全：Push 与 Tick 应在同一线程（通常是界面线程）调用。
/// </remarks>
public sealed class StreamPacer
{
    private readonly StreamPacerOptions _options;
    private readonly StringBuilder _pending = new();
    private readonly Queue<(long End, TimeSpan Time)> _arrivals = new();
    private readonly long[] _revealOffsets;
    private readonly TimeSpan[] _revealTimes;
    private int _revealHead;
    private int _revealCount;

    private long _arrived;
    private long _revealed;
    private double _carry;
    private double _rate;
    private TimeSpan? _lastPush;
    private TimeSpan? _lastTick;
    private bool _finished;
    private double _drainRate;

    public StreamPacer(StreamPacerOptions? options = null)
    {
        _options = options ?? StreamPacerOptions.Default;
        _revealOffsets = new long[Math.Max(1, _options.RevealHistory)];
        _revealTimes = new TimeSpan[_revealOffsets.Length];
    }

    /// <summary>已到达的总字符数。</summary>
    public long Arrived => _arrived;

    /// <summary>已显示的总字符数。</summary>
    public long Revealed => _revealed;

    public long Backlog => _arrived - _revealed;

    public bool IsFinished => _finished;

    /// <summary>已结束且积压全部放完。</summary>
    public bool IsDrained => _finished && Backlog == 0;

    /// <summary>当前平滑后的到达速率（字符/秒）。</summary>
    public double ArrivalRate => _rate;

    public void Push(ReadOnlySpan<char> text, TimeSpan now)
    {
        if (text.IsEmpty)
        {
            return;
        }
        if (_finished)
        {
            throw new InvalidOperationException("The stream has finished.");
        }
        if (_lastPush is { } last && now > last)
        {
            var instant = text.Length / Math.Max((now - last).TotalSeconds, 0.001);
            _rate = _rate == 0 ? instant : _rate * 0.8 + instant * 0.2;
        }
        _lastPush = now;
        if (Backlog == 0)
        {
            // 空闲之后的第一帧：不能把空闲时长当作这一帧的间隔（否则会一次放出全部积压）
            _lastTick = null;
        }
        _pending.Append(text);
        _arrived += text.Length;
        _arrivals.Enqueue((_arrived, now));
    }

    /// <summary>流结束：剩余积压在 MinDrain–MaxDrain 内放完。</summary>
    public void Finish(TimeSpan now)
    {
        if (_finished)
        {
            return;
        }
        _finished = true;
        var backlog = Backlog;
        var natural = _rate > 0 ? TimeSpan.FromSeconds(backlog / _rate) : _options.MinDrain;
        var drain = natural < _options.MinDrain ? _options.MinDrain : natural > _options.MaxDrain ? _options.MaxDrain : natural;
        _drainRate = backlog / Math.Max(drain.TotalSeconds, 0.001);
    }

    /// <summary>放出全部积压（例如窗口不可见、用户点击"跳到结尾"）。</summary>
    public string Flush(TimeSpan now) => Release((int)Backlog, now);

    /// <summary>推进到 <paramref name="now"/>，返回本帧新显示的文本（可能为空）。</summary>
    public string Tick(TimeSpan now)
    {
        var dt = _lastTick is { } last ? Math.Max((now - last).TotalSeconds, 0) : 1.0 / 60;
        _lastTick = now;
        var backlog = Backlog;
        if (backlog == 0)
        {
            _carry = 0;
            return "";
        }
        if (backlog > _options.BypassThreshold)
        {
            return Release((int)backlog, now);
        }

        var rate = Math.Max(_rate, backlog / _options.TargetLag.TotalSeconds);
        if (_finished)
        {
            rate = Math.Max(rate, _drainRate);
        }
        _carry += rate * dt;
        var count = (int)Math.Min(_carry, backlog);

        // 最大等待：到达时间早于 now - MaxLag 的字符必须显示
        var deadline = now - _options.MaxLag;
        foreach (var (end, time) in _arrivals)
        {
            if (time > deadline)
            {
                break;
            }
            count = (int)Math.Max(count, end - _revealed);
        }

        if (_options.SkipCompletedBlocks && backlog > count)
        {
            var boundary = _pending.ToString().LastIndexOf("\n\n", StringComparison.Ordinal);
            if (boundary >= 0)
            {
                count = Math.Max(count, boundary + 2);
            }
        }

        if (count <= 0)
        {
            return "";
        }
        var text = Release(count, now);
        _carry = Math.Max(_carry - text.Length, 0);
        return text;
    }

    /// <summary>
    /// 已显示文本中第 <paramref name="offset"/> 个字符的显示时间；早于记录窗口的字符返回 null（视为早已完全显示）。
    /// </summary>
    public TimeSpan? GetRevealTime(long offset)
    {
        if (offset < 0 || offset >= _revealed || _revealCount == 0)
        {
            return null;
        }
        // 环形缓冲按显示顺序记录 (该批次起点, 时间)，从新到旧找第一个起点不大于 offset 的批次
        for (var i = 0; i < _revealCount; i++)
        {
            var slot = (_revealHead - 1 - i + _revealOffsets.Length) % _revealOffsets.Length;
            if (_revealOffsets[slot] <= offset)
            {
                return _revealTimes[slot];
            }
        }
        return null;
    }

    private string Release(int count, TimeSpan now)
    {
        count = (int)Math.Min(count, Backlog);
        if (count <= 0)
        {
            return "";
        }
        // 对齐到字素簇边界，避免拆开代理对、组合字符或 emoji 序列
        var pending = _pending.ToString();
        var cut = 0;
        while (cut < count && cut < pending.Length)
        {
            cut += StringInfo.GetNextTextElementLength(pending, cut);
        }
        if (cut > pending.Length)
        {
            cut = pending.Length;
        }
        // 流未结束时，末尾不完整的代理对留到下一帧
        if (!_finished && cut == pending.Length && cut > 0 && char.IsHighSurrogate(pending[cut - 1]))
        {
            cut--;
        }
        if (cut == 0)
        {
            return "";
        }
        var text = pending[..cut];
        _pending.Remove(0, cut);

        _revealOffsets[_revealHead] = _revealed;
        _revealTimes[_revealHead] = now;
        _revealHead = (_revealHead + 1) % _revealOffsets.Length;
        _revealCount = Math.Min(_revealCount + 1, _revealOffsets.Length);

        _revealed += cut;
        while (_arrivals.Count > 0 && _arrivals.Peek().End <= _revealed)
        {
            _arrivals.Dequeue();
        }
        return text;
    }
}
