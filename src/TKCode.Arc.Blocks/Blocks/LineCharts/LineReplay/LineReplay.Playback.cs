using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Input;

namespace TKCode.Arc;

public partial class LineReplay
{
    // Playback is code-driven: one position in minutes advances every frame from a LiveLoop (at every motion level,
    // because it is started by the viewer and reports function), and rewinds and jumps glide it on the smooth spring.
    // Every part (line, playhead, labels, scrubber, readout) is written from that one value; no ControlTheme transition
    // can drive a self-drawn chart along a timeline.
    private readonly MotionValue<double> _motion;
    private readonly LiveLoop _loop;
    private ViewportVisibility? _visibility;
    private bool _playedOnce;

    /// <summary>Plays from the playhead, or from the start when it is at the end.</summary>
    public void Play()
    {
        if (_data.Count == 0)
        {
            return;
        }
        if (_motion.Value >= _data.End - 1e-6)
        {
            Replay();
            return;
        }
        StartPlaying();
    }

    /// <summary>Pauses where the playhead is.</summary>
    public void Pause() => StopPlaying(announce: true);

    /// <summary>Rewinds to the start (gliding on the smooth spring) and plays.</summary>
    public void Replay()
    {
        if (_data.Count == 0)
        {
            return;
        }
        StartPlaying();
        _ = _motion.AnimateTo(_data.Start, MotionSpec.Spring(ArcMotionPresets.Smooth));
    }

    /// <summary>Plays or pauses, like the play button.</summary>
    public void TogglePlay()
    {
        if (_isPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    /// <summary>
    /// Moves the playhead to <paramref name="minutes"/>; with <paramref name="animate"/> it glides there on the smooth
    /// spring (Reduced and Off land at once). Playback continues from there if it was playing.
    /// </summary>
    public void Seek(double minutes, bool animate = true)
    {
        if (_data.Count == 0 || !double.IsFinite(minutes))
        {
            return;
        }
        var target = Math.Clamp(minutes, _data.Start, _data.End);
        _positioned = true;
        if (animate && IsLoaded)
        {
            _ = _motion.AnimateTo(target, MotionSpec.Spring(ArcMotionPresets.Smooth));
        }
        else
        {
            _motion.Jump(target);
        }
        if (!_isPlaying)
        {
            Report(target);
        }
    }

    /// <summary>Moves the playhead to <paramref name="moment"/> and announces it; playback continues from there if it was playing.</summary>
    public void SeekToMoment(LineReplayMoment moment)
    {
        var info = _data.Moments.FirstOrDefault(m => ReferenceEquals(m.Moment, moment));
        if (info is null)
        {
            return;
        }
        Seek(info.Time);
        _liveRegion?.Announce(string.Format(CultureInfo.CurrentCulture, MomentAnnouncementFormat, FormatTimeText(info.Time),
            moment.Title));
    }

    private void StartPlaying()
    {
        _playedOnce = true;
        _positioned = true;
        IsPlaying = true;
        _loop.Start();
        UpdatePlayButton();
    }

    private void StopPlaying(bool announce)
    {
        if (!_isPlaying)
        {
            return;
        }
        IsPlaying = false;
        _loop.Stop();
        UpdatePlayButton();
        Report(_motion.Value);
        if (announce)
        {
            _liveRegion?.Announce(string.Format(CultureInfo.CurrentCulture, PausedAnnouncementFormat, FormatTimeText(_motion.Value)));
        }
    }

    /// <summary>Advances playback by <paramref name="seconds"/> of wall time (the frame loop; tests call it directly).</summary>
    internal bool Advance(double seconds)
    {
        if (!_isPlaying || _data.Count == 0)
        {
            return false;
        }
        // A rewind or a jump glides first; playback picks up where it lands.
        if (_motion.IsAnimating)
        {
            return true;
        }
        var duration = Math.Max(0.1, Duration.TotalSeconds);
        var from = _motion.Value;
        var next = Math.Min(_data.End, from + seconds * (_data.End - _data.Start) / duration * Speed);
        foreach (var info in _data.Moments)
        {
            if (info.Time > from + 1e-9 && info.Time <= next + 1e-9)
            {
                _motion.Jump(info.Time);
                RaiseEvent(new LineReplayMomentEventArgs(MomentReachedEvent, info.Moment));
                _liveRegion?.Announce(string.Format(CultureInfo.CurrentCulture, MomentAnnouncementFormat,
                    FormatTimeText(info.Time), info.Moment.Title));
            }
        }
        _motion.Jump(next);
        if (next >= _data.End - 1e-9)
        {
            IsPlaying = false;
            UpdatePlayButton();
            Report(next);
            var (peak, at) = _data.PeakAt(next);
            _liveRegion?.Announce(string.Format(CultureInfo.CurrentCulture, FinishedAnnouncementFormat, FormatValueText(peak),
                FormatTimeText(at)));
            return false;
        }
        return true;
    }

    private bool OnFrame(double dt) => Advance(Math.Min(dt, 0.1));

    private void OnPositionChanged(double position)
    {
        UpdateFrame(position, animate: IsLoaded);
    }

    private void UpdateFrame(double position, bool animate)
    {
        Position = position;
        _stage.Update(position, animate);
        _scrubber.SetPosition(position);
        UpdateReadout(position);
        UpdateMomentStates(position);
        UpdatePlayButton();
        if (!_isPlaying)
        {
            Report(position);
        }
    }

    private void Report(double position) => _scrubber.Report(position, ValueText(position));

    private void ReportPosition() => Report(_motion.Value);

    // Scrubbing ------------------------------------------------------------------------------------------------------------

    /// <summary>A press or drag on the scrubber or the chart: playback pauses and the playhead follows the pointer.</summary>
    internal void ScrubTo(double minutes)
    {
        StopPlaying(announce: false);
        Seek(minutes, animate: false);
    }

    internal void EndScrub()
    {
        Report(_motion.Value);
    }

    /// <summary>The scrubber's keys; returns whether the key was used.</summary>
    internal bool HandleScrubberKey(Key key, KeyModifiers modifiers)
    {
        if (_data.Count == 0)
        {
            return false;
        }
        var step = _data.MinStep;
        var hour = 60.0;
        var position = _motion.Target;
        double? target = key switch
        {
            Key.Left => position - (modifiers.HasFlag(KeyModifiers.Shift) ? hour : step),
            Key.Right => position + (modifiers.HasFlag(KeyModifiers.Shift) ? hour : step),
            Key.PageUp => position - hour,
            Key.PageDown => position + hour,
            Key.Home => _data.Start,
            Key.End => _data.End,
            _ => null,
        };
        if (key is Key.Space or Key.Enter or Key.K)
        {
            TogglePlay();
            return true;
        }
        if (target is not { } t)
        {
            return false;
        }
        StopPlaying(announce: false);
        Seek(t, animate: false);
        return true;
    }

    // Auto play ------------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (AutoPlay && !_playedOnce && _visibility is null)
        {
            _visibility = new ViewportVisibility(this, 0.4, once: true);
            _visibility.VisibilityChanged += OnVisibilityChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_visibility is not null)
        {
            _visibility.VisibilityChanged -= OnVisibilityChanged;
            _visibility.Dispose();
            _visibility = null;
        }
        StopPlaying(announce: false);
    }

    private void OnVisibilityChanged(object? sender, bool visible)
    {
        if (!visible || _playedOnce || !AutoPlay || !ArcMotion.IsFull(this) || _data.Count == 0)
        {
            return;
        }
        // Auto play starts from the start, not by rewinding the line the reader is looking at.
        _motion.Jump(_data.Start);
        StartPlaying();
    }
}
