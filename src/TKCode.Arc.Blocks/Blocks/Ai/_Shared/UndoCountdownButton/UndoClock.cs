using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The one clock behind every running <see cref="UndoCountdownButton"/>: a single <see cref="DispatcherTimer"/> ticks all
/// registered countdowns, and it runs only while at least one is registered (Arc serves every undo window with one 200ms
/// interval that idles when nothing can be undone). Countdowns keep an absolute deadline, so a late tick never stretches
/// a window. The time comes from a <see cref="TimeProvider"/>, so tests move time by hand and call <see cref="Tick"/>.
/// </summary>
internal sealed class UndoClock
{
    /// <summary>The tick interval: fine enough that whole seconds change on time and the 14px ring drains in sub-pixel steps.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The clock every countdown uses unless it is given another one.</summary>
    public static UndoClock Shared { get; } = new();

    private readonly TimeProvider _time;
    private readonly TimeSpan _interval;
    private readonly List<UndoCountdownButton> _countdowns = new();
    private UndoCountdownButton[] _snapshot = new UndoCountdownButton[4];
    private DispatcherTimer? _timer;

    /// <param name="time">The time source; null uses the system clock.</param>
    /// <param name="interval">The tick interval; null uses <see cref="DefaultInterval"/>.</param>
    public UndoClock(TimeProvider? time = null, TimeSpan? interval = null)
    {
        _time = time ?? TimeProvider.System;
        _interval = interval ?? DefaultInterval;
    }

    /// <summary>The current time of this clock's source.</summary>
    public DateTimeOffset Now => _time.GetUtcNow();

    /// <summary>How many countdowns are registered (running and in a window).</summary>
    public int Count => _countdowns.Count;

    /// <summary>True while the timer is on, which is exactly while <see cref="Count"/> is above zero.</summary>
    public bool IsTimerRunning => _timer is { IsEnabled: true };

    /// <summary>Whether <paramref name="countdown"/> is registered.</summary>
    public bool IsRegistered(UndoCountdownButton countdown) => _countdowns.Contains(countdown);

    /// <summary>Adds a countdown to the ticks and starts the timer if it was idle; registering twice does nothing.</summary>
    public void Register(UndoCountdownButton countdown)
    {
        if (_countdowns.Contains(countdown))
        {
            return;
        }
        _countdowns.Add(countdown);
        if (_timer is null)
        {
            _timer = new DispatcherTimer { Interval = _interval };
            _timer.Tick += OnTimerTick;
        }
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    /// <summary>Removes a countdown; the timer stops with the last one.</summary>
    public void Unregister(UndoCountdownButton countdown)
    {
        if (!_countdowns.Remove(countdown))
        {
            return;
        }
        if (_countdowns.Count == 0)
        {
            _timer?.Stop();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e) => Tick();

    /// <summary>
    /// Updates every registered countdown to the current time. A countdown that runs out unregisters itself and raises
    /// its event from here, and that handler may start or cancel others: the round works on a copy (a reused buffer, no
    /// allocation per tick) and skips countdowns that left in the meantime.
    /// </summary>
    public void Tick()
    {
        var count = _countdowns.Count;
        if (count == 0)
        {
            return;
        }
        if (_snapshot.Length < count)
        {
            _snapshot = new UndoCountdownButton[count * 2];
        }
        _countdowns.CopyTo(_snapshot);
        var now = Now;
        for (var i = 0; i < count; i++)
        {
            var countdown = _snapshot[i];
            _snapshot[i] = null!;
            if (_countdowns.Contains(countdown))
            {
                countdown.OnClockTick(now);
            }
        }
    }
}
