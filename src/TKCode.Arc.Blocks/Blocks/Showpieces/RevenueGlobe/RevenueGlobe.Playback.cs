using System.Diagnostics;
using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class RevenueGlobe
{
    /// <summary>Arcs started per second at most while replaying (live, every payment flies).</summary>
    internal const double ReplayArcsPerSecond = 14;

    /// <summary>Arcs started in one frame at most.</summary>
    internal const int MaxArcsPerFrame = 24;

    private readonly RevenueGlobePlayback _playback = new();
    private readonly LiveLoop _clockLoop;
    private long _clockAnchorTicks;
    private DateTimeOffset _clockAnchorNow;
    private bool _settingNow;
    private bool _settingTime;
    private bool _settingPaused;
    private double _arcCredit;
    private double _frameDt = 1 / 60.0;
    private long _clockSecond = long.MinValue;
    private ArcMotionLevel _level = ArcMotionLevel.Full;

    private bool IsFullMotion => _level == ArcMotionLevel.Full;

    private bool IsPausedState => _playback.State == RevenueGlobePlaybackState.Paused;

    /// <summary>The effective day start: <see cref="DayStart"/>, or 00:00 UTC of <see cref="Now"/> when unset.</summary>
    private DateTimeOffset EffectiveDayStart =>
        DayStart != default ? DayStart : new DateTimeOffset(Now.UtcDateTime.Date, TimeSpan.Zero);

    /// <summary>Seconds from the day start to the present (the timeline slider's maximum).</summary>
    internal double DaySecondsNow => _playback.Now;

    /// <summary>Seconds from the day start to the playhead (the timeline slider's value).</summary>
    internal double DaySecondsPlayhead => _playback.Time;

    internal RevenueGlobePlayback Playback => _playback;

    /// <summary>The clock loop is wanted (tests: live with the internal clock, replaying, fast-forwarding).</summary>
    internal bool IsClockActive => _clockLoop.IsActive;

    private void InitializeTime()
    {
        var now = DateTimeOffset.UtcNow;
        now = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        _settingNow = true;
        SetCurrentValue(NowProperty, now);
        _settingNow = false;
        _playback.ReplaySpeed = ReplaySpeed;
        _playback.SetNow(Seconds(now));
        SyncTimeProperty();
        AnchorClock();
    }

    private void AnchorClock()
    {
        _clockAnchorTicks = Stopwatch.GetTimestamp();
        _clockAnchorNow = Now;
    }

    private bool WantsClockFrames =>
        _playback.State == RevenueGlobePlaybackState.Replaying ||
        (_playback.State == RevenueGlobePlaybackState.Live && ClockMode == RevenueGlobeClockMode.Internal);

    private void RefreshClockLoop()
    {
        if (WantsClockFrames && IsFullMotion)
        {
            _clockLoop.MinimumLevel = ArcMotionLevel.Full;
            _clockLoop.Start();
        }
        else
        {
            _clockLoop.Stop();
        }
    }

    private bool OnClockFrame(double dt) => AdvanceClock(dt);

    /// <summary>
    /// One clock frame: moves the present with the real clock (Internal mode), steps the playhead, starts the arcs of
    /// the payments it passed and refreshes every figure. Returns whether more frames are wanted.
    /// </summary>
    internal bool AdvanceClock(double dt)
    {
        dt = Math.Clamp(dt, 0, 0.25);
        _frameDt = dt;
        var previous = _playback.Time;
        if (ClockMode == RevenueGlobeClockMode.Internal && _playback.State != RevenueGlobePlaybackState.Paused)
        {
            var elapsed = Stopwatch.GetElapsedTime(_clockAnchorTicks);
            SetNowInternal(_clockAnchorNow + elapsed);
        }
        var wentLive = _playback.Advance(dt);
        SyncPlayhead(previous, continuous: true);
        if (wentLive)
        {
            Announce(Labels.BackToLive);
        }
        return WantsClockFrames;
    }

    /// <summary>Test hook: the Internal clock moves on by <paramref name="seconds"/> of real time.</summary>
    internal void ShiftClock(double seconds) => _clockAnchorTicks -= (long)(seconds * Stopwatch.Frequency);

    private void SetNowInternal(DateTimeOffset now)
    {
        _settingNow = true;
        SetCurrentValue(NowProperty, now);
        _settingNow = false;
        if (EffectiveDayStart != _ledgerDayStart)
        {
            // Past midnight of a day that follows Now: a new day starts.
            OnDayStartChanged();
            return;
        }
        _playback.SetNow(Seconds(now));
    }

    private void OnNowPropertyChanged()
    {
        if (_settingNow)
        {
            return;
        }
        AnchorClock();
        var previous = _playback.Time;
        if (EffectiveDayStart != _ledgerDayStart)
        {
            // The day follows Now and Now moved to another day.
            OnDayStartChanged();
            return;
        }
        _playback.SetNow(Seconds(Now));
        SyncPlayhead(previous, continuous: _playback.IsLive && ClockMode == RevenueGlobeClockMode.External);
        RefreshClockLoop();
    }

    private void OnDayStartChanged()
    {
        _playback.SetNow(Seconds(Now));
        var previous = _playback.Time;
        _playback.GoLive(animate: false);
        RebuildLedger();
        SyncPlayhead(previous, continuous: false);
        RefreshClockLoop();
    }

    private void OnTimePropertyChanged()
    {
        if (_settingTime)
        {
            return;
        }
        SeekToDayTime(Seconds(Time), fromKeyboard: false);
    }

    private void OnIsPausedChanged()
    {
        if (_settingPaused)
        {
            return;
        }
        if (IsPaused)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    /// <summary>
    /// After the playhead moved from <paramref name="previous"/>: starts arcs for the payments passed on the way (a
    /// continuous move at full motion) or drops the arcs' pending payments (a jump), then refreshes figures, timeline,
    /// clock and the <see cref="Time"/> property.
    /// </summary>
    private void SyncPlayhead(double previous, bool continuous)
    {
        var time = _playback.Time;
        if (continuous && time > previous && IsFullMotion && _playback.State != RevenueGlobePlaybackState.Scrubbing)
        {
            SpawnBetween(previous, time);
        }
        else if (time != previous)
        {
            DropPending();
        }
        if (!IsFullMotion && time != previous)
        {
            RefreshStill();
        }
        SyncState();
        UpdateTimeline(force: false);
        UpdateFigures();
        UpdateClockText();
        if (time != previous)
        {
            SyncTimeProperty();
            RaiseEvent(new RevenueGlobeTimeChangedEventArgs(TimeChangedEvent, Time, _playback.IsLive, _playback.State));
        }
    }

    private void SyncTimeProperty()
    {
        _settingTime = true;
        SetCurrentValue(TimeProperty, EffectiveDayStart + TimeSpan.FromSeconds(_playback.Time));
        _settingTime = false;
    }

    /// <summary>Starts arcs for the payments in (<paramref name="from"/>, <paramref name="to"/>]; while replaying at most 14 a second, evenly spread.</summary>
    private void SpawnBetween(double from, double to)
    {
        var first = _ledger.CountAt(from);
        var end = _ledger.CountAt(to);
        var count = end - first;
        var limited = _playback.State == RevenueGlobePlaybackState.Replaying && _playback.Velocity > 4;
        if (limited)
        {
            // A budget of 14 arcs a second that fills every frame (at most two in hand).
            _arcCredit = Math.Min(_arcCredit + _frameDt * ReplayArcsPerSecond, 2);
        }
        else
        {
            _arcCredit = 0;
        }
        if (count <= 0 || _canvas is null)
        {
            return;
        }
        var allowed = MaxArcsPerFrame;
        if (limited)
        {
            allowed = Math.Min(count, (int)_arcCredit);
            _arcCredit -= allowed;
        }
        if (allowed <= 0)
        {
            return;
        }
        if (count <= allowed)
        {
            for (var i = first; i < end; i++)
            {
                TrySpawn(i);
            }
            return;
        }
        for (var k = 0; k < allowed; k++)
        {
            TrySpawn(first + (int)((k + 0.5) * count / allowed));
        }
    }

    private void SyncState()
    {
        var state = _playback.State;
        if (state != _playbackState)
        {
            // The screen-reader summary is throttled while replaying: refresh it when the state changes.
            _summaryKey = long.MinValue;
        }
        PlaybackState = state;
        IsLive = _playback.IsLive;
        PseudoClasses.Set(LivePseudoClass, state == RevenueGlobePlaybackState.Live);
        PseudoClasses.Set(PausedPseudoClass, state == RevenueGlobePlaybackState.Paused);
        PseudoClasses.Set(ReplayingPseudoClass, state == RevenueGlobePlaybackState.Replaying);
        PseudoClasses.Set(ScrubbingPseudoClass, state == RevenueGlobePlaybackState.Scrubbing);
        if (IsPaused != (state == RevenueGlobePlaybackState.Paused))
        {
            _settingPaused = true;
            SetCurrentValue(IsPausedProperty, state == RevenueGlobePlaybackState.Paused);
            _settingPaused = false;
        }
        if (_canvas is not null)
        {
            _canvas.IsReplaying = state == RevenueGlobePlaybackState.Replaying;
        }
        UpdatePlaybackVisuals();
        RefreshClockLoop();
    }

    private void UpdatePlaybackVisuals()
    {
        var labels = Labels;
        if (_liveText is not null)
        {
            _liveText.Text = _playback.IsLive ? IsFullMotion ? labels.Live : labels.Now : labels.GoLive;
        }
        if (_liveButton is not null)
        {
            Avalonia.Automation.AutomationProperties.SetName(_liveButton, _liveText?.Text ?? labels.Live);
        }
        if (_pauseButton is not null)
        {
            Avalonia.Automation.AutomationProperties.SetName(_pauseButton, IsPausedState ? labels.Play : labels.Pause);
        }
    }

    private void UpdateClockText()
    {
        if (_clockText is null)
        {
            return;
        }
        var second = (long)Math.Floor(_playback.Time);
        if (second == _clockSecond)
        {
            return;
        }
        _clockSecond = second;
        var time = EffectiveDayStart + TimeSpan.FromSeconds(second);
        string text;
        try
        {
            text = time.ToString(TimeFormat, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            text = time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
        _clockText.Text = text;
    }

    private void UpdateTimeline(bool force)
    {
        if (_timeline is null)
        {
            return;
        }
        if (force || _timelineVersion != _ledger.Version)
        {
            _timelineVersion = _ledger.Version;
            _timeline.SetBins(_ledger.Bins, _ledger.MaxBin);
        }
        _timeline.AvailableFraction = _playback.Now / RevenueGlobeLedger.DaySeconds;
        _timeline.PlayedFraction = _playback.Time / RevenueGlobeLedger.DaySeconds;
    }

    private int _timelineVersion = -1;

    private void Announce(string text) => _announcer?.Announce(text);

    // Commands -------------------------------------------------------------------------------------------------------------

    /// <summary>Starts the day over at 00:00 and replays it at <see cref="ReplaySpeed"/> until it is live again.</summary>
    public void Replay()
    {
        var previous = _playback.Time;
        _playback.ReplaySpeed = Math.Max(1, ReplaySpeed);
        _playback.Replay();
        _arcCredit = 0;
        SyncPlayhead(previous, continuous: false);
        Announce(Labels.Replaying);
    }

    /// <summary>Holds the playhead; arcs in flight still land and the globe keeps drifting.</summary>
    public void Pause()
    {
        if (_playback.State == RevenueGlobePlaybackState.Paused)
        {
            return;
        }
        var previous = _playback.Time;
        _playback.Pause();
        SyncPlayhead(previous, continuous: false);
        Announce(Labels.Paused);
    }

    /// <summary>Resumes after a pause: replays on from the paused moment until it is live.</summary>
    public void Play()
    {
        if (_playback.State != RevenueGlobePlaybackState.Paused)
        {
            return;
        }
        if (ClockMode == RevenueGlobeClockMode.Internal)
        {
            // The present moved on while paused.
            SetNowInternal(_clockAnchorNow + Stopwatch.GetElapsedTime(_clockAnchorTicks));
        }
        var previous = _playback.Time;
        _playback.ReplaySpeed = Math.Max(1, ReplaySpeed);
        _playback.Play();
        _arcCredit = 0;
        SyncPlayhead(previous, continuous: false);
        Announce(_playback.IsLive ? Labels.BackToLive : Labels.Replaying);
    }

    private void TogglePause()
    {
        if (IsPausedState)
        {
            Play();
        }
        else
        {
            Pause();
        }
    }

    /// <summary>Back to the present: a short fast-forward (600 ms + 85 ms per hour, at most 2 s), at once under reduced motion.</summary>
    public void GoLive()
    {
        if (ClockMode == RevenueGlobeClockMode.Internal && !_playback.IsLive)
        {
            SetNowInternal(_clockAnchorNow + Stopwatch.GetElapsedTime(_clockAnchorTicks));
        }
        var previous = _playback.Time;
        var wasLive = _playback.IsLive;
        _playback.GoLive(animate: IsFullMotion);
        SyncPlayhead(previous, continuous: false);
        if (_playback.IsLive && !wasLive)
        {
            Announce(Labels.BackToLive);
        }
    }

    /// <summary>
    /// Jumps the playhead to <paramref name="time"/> (clamped to the day so far); in the past it replays on from there,
    /// at the present it goes live. Every figure is recomputed at once.
    /// </summary>
    public void SeekTo(DateTimeOffset time) => SeekToDayTime(Seconds(time), fromKeyboard: false);

    internal void SeekToDayTime(double seconds, bool fromKeyboard)
    {
        var previous = _playback.Time;
        var wasReplaying = _playback.State == RevenueGlobePlaybackState.Replaying;
        _playback.ReplaySpeed = Math.Max(1, ReplaySpeed);
        _playback.Seek(seconds, continueReplay: IsFullMotion);
        _arcCredit = 0;
        SyncPlayhead(previous, continuous: false);
        if (_playback.State == RevenueGlobePlaybackState.Replaying && !wasReplaying)
        {
            Announce(Labels.Replaying);
        }
    }

    /// <summary>Moves the playhead by <paramref name="seconds"/> (timeline keys).</summary>
    internal void StepTime(double seconds) => SeekToDayTime(_playback.Time + seconds, fromKeyboard: true);

    internal void BeginScrub(double seconds)
    {
        var previous = _playback.Time;
        _playback.BeginScrub();
        _playback.ScrubTo(seconds);
        SyncPlayhead(previous, continuous: false);
        Announce(Labels.Paused);
    }

    internal void ScrubTo(double seconds)
    {
        var previous = _playback.Time;
        _playback.ScrubTo(seconds);
        SyncPlayhead(previous, continuous: false);
    }

    internal void EndScrub(double speed)
    {
        var previous = _playback.Time;
        _playback.ReplaySpeed = Math.Max(1, ReplaySpeed);
        _playback.EndScrub(speed, continueReplay: IsFullMotion);
        _arcCredit = 0;
        SyncPlayhead(previous, continuous: false);
        Announce(_playback.IsLive ? Labels.BackToLive : _playback.State == RevenueGlobePlaybackState.Replaying ? Labels.Replaying : Labels.Paused);
    }

    private void OnReplayClick(object? sender, RoutedEventArgs e) => Replay();

    private void OnPauseClick(object? sender, RoutedEventArgs e) => TogglePause();

    private void OnLiveClick(object? sender, RoutedEventArgs e) => GoLive();

    // Motion level ----------------------------------------------------------------------------------------------------------

    private void OnMotionLevelChanged(ArcMotionLevel level) => UpdateMotionLevel();

    private void UpdateMotionLevel()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var changed = level != _level;
        _level = level;
        PseudoClasses.Set(ReducedPseudoClass, level != ArcMotionLevel.Full);
        if (changed && level != ArcMotionLevel.Full)
        {
            _canvas?.ClearArcs();
            DropPending();
        }
        RefreshStill();
        UpdatePlaybackVisuals();
        RefreshClockLoop();
        UpdateFigures();
    }
}
