using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class SpeechPlayer
{
    /// <summary>The most 1× time one frame of the simulated clock can add (a stalled frame loses time instead of jumping).</summary>
    internal const double MaxStep = 0.1;

    /// <summary>Silence after the last word of simulated speech, so the bar does not end on the word's last frame.</summary>
    internal const double Tail = 0.25;

    /// <summary>The pause between two passes of a self-playing simulation.</summary>
    internal static readonly TimeSpan SimulationLoopPause = TimeSpan.FromMilliseconds(1600);

    /// <summary>Speech ready to play: what the service returned, the timings (given, or estimated and fitted) and the length.</summary>
    private sealed class Prepared(SpeechAudio source, IReadOnlyList<SpeechWordTiming> timings, double duration)
    {
        public SpeechAudio Source { get; } = source;

        public IReadOnlyList<SpeechWordTiming> Timings { get; } = timings;

        public double Duration { get; } = duration;

        public ISpeechAudio? Audio => Source.Audio;
    }

    private readonly Dictionary<string, Prepared> _cache = new();
    private Prepared? _prepared;
    private CancellationTokenSource? _request;
    private LiveLoop _clock = null!;
    private double _time;
    private double _duration;
    private long _shownSecond = -1;
    private string? _error;
    private bool _interacted;
    private bool _autoPlayed;
    private bool _inView;
    private int _resumeWord;
    private bool _playWhenReady;
    private ViewportVisibility? _viewport;
    private DispatcherTimer? _loopTimer;
    private MotionValue<double>? _readingValue;
    private MotionValue<double>? _fadeValue;
    private MotionValue<double>? _glideValue;
    private SpeechPlayerParagraph? _gliding;
    private double _reading;
    private double _readingTarget = -1;
    private double _fade = 1;

    /// <summary>The wait of the built-in simulation (tests shorten it).</summary>
    internal TimeSpan SimulationDelay { get; set; } = TimeSpan.FromMilliseconds(640);

    /// <summary>Tests drive the clock with <see cref="Step"/> only (no frames from the render loop).</summary>
    internal bool ManualClock { get; set; }

    /// <summary>The words spoken so far, with their timings (null while nothing is prepared).</summary>
    internal IReadOnlyList<SpeechWordTiming>? Timings => _prepared?.Timings;

    /// <summary>How much of the reading look applies now (0..1).</summary>
    internal double ReadingAmount => _reading;

    /// <summary>The voices whose speech is kept.</summary>
    internal IReadOnlyCollection<string> CachedVoices => _cache.Keys;

    /// <summary>Whether the simulation loop is waiting to start the next pass.</summary>
    internal bool IsSimulationLoopPending => _loopTimer?.IsEnabled == true;

    private void InitPlayback()
    {
        _clock = new LiveLoop(this, OnFrame) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <summary>Opens the player in place of the action row and starts reading (what Read aloud does).</summary>
    public void Open()
    {
        if (IsOpen)
        {
            if (State is SpeechPlayerState.Paused or SpeechPlayerState.Finished)
            {
                Play();
            }
            return;
        }
        IsOpen = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        if (_dock is not null)
        {
            _dock.Content = _controls;
        }
        _time = 0;
        _shownSecond = -1;
        CurrentWordIndex = -1;
        Prepare(EffectiveVoice, 0, play: true);
    }

    /// <summary>Plays or resumes. From the action row it opens the player; after the end it starts over; after a failure it retries.</summary>
    public void Play()
    {
        switch (State)
        {
            case SpeechPlayerState.Idle:
                Open();
                return;
            case SpeechPlayerState.Preparing:
                _playWhenReady = true;
                return;
            case SpeechPlayerState.Failed:
                Retry();
                return;
            case SpeechPlayerState.Playing:
                return;
            case SpeechPlayerState.Finished:
                _prepared?.Audio?.Seek(TimeSpan.Zero);
                SetTime(0, raiseWord: true, animate: false);
                break;
        }
        if (_prepared is null)
        {
            Prepare(EffectiveVoice, 0, play: true);
            return;
        }
        StartPlaying();
    }

    /// <summary>Pauses at the word being spoken.</summary>
    public void Pause()
    {
        if (State == SpeechPlayerState.Preparing)
        {
            _playWhenReady = false;
            return;
        }
        if (State != SpeechPlayerState.Playing)
        {
            return;
        }
        if (_prepared?.Audio is { } audio)
        {
            audio.Pause();
            SetTime(audio.Position.TotalSeconds, raiseWord: true, animate: false);
        }
        _clock.Stop();
        State = SpeechPlayerState.Paused;
        SyncControls();
        SyncWords(animate: false);
        Announce(Labels.PausedAt(SpeechPlayerText.Clock(_time)));
        RaiseEvent(Args(PausedEvent));
    }

    /// <summary>Plays when paused, pauses when playing (the play button, Space and K on the bar).</summary>
    public void TogglePlayback()
    {
        if (State == SpeechPlayerState.Playing)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    /// <summary>Moves playback to <paramref name="position"/> on the 1× timeline (raises <see cref="Seeked"/>).</summary>
    public void Seek(TimeSpan position) => SeekCore(position.TotalSeconds, dragging: false);

    /// <summary>Moves playback to the start of a word (what a click on the word does).</summary>
    public void SeekToWord(int wordIndex)
    {
        if (_prepared is null)
        {
            if (State == SpeechPlayerState.Preparing)
            {
                _resumeWord = Math.Clamp(wordIndex, 0, Math.Max(0, _document.Words.Count - 1));
            }
            return;
        }
        var timings = _prepared.Timings;
        if (timings.Count == 0)
        {
            return;
        }
        var index = Math.Clamp(wordIndex, 0, timings.Count - 1);
        SeekCore(timings[index].Start.TotalSeconds, dragging: false);
    }

    /// <summary>Stops reading and brings the action row back (Stop reading). Fetched speech is kept.</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }
        CancelRequest();
        StopSimulationLoop();
        _prepared?.Audio?.Pause();
        _clock.Stop();
        _playWhenReady = false;
        IsOpen = false;
        PseudoClasses.Set(OpenPseudoClass, false);
        State = SpeechPlayerState.Idle;
        _error = null;
        _time = 0;
        CurrentWordIndex = -1;
        if (_dock is not null)
        {
            _dock.Content = _actionRow;
        }
        SyncControls();
        SyncWords(animate: false);
        Announce(Labels.Stopped);
        RaiseEvent(new RoutedEventArgs(ClosedEvent));
    }

    /// <summary>Asks for the speech again after a failure.</summary>
    public void Retry()
    {
        if (State != SpeechPlayerState.Failed)
        {
            return;
        }
        Prepare(EffectiveVoice, _resumeWord, play: true);
    }

    private SpeechPlaybackEventArgs Args(RoutedEvent<SpeechPlaybackEventArgs> routedEvent) =>
        new(routedEvent, Position, Duration, CurrentWordIndex, EffectiveVoice, Speed);

    private void StartPlaying()
    {
        if (_prepared is null)
        {
            return;
        }
        if (_time >= _duration - 1e-6)
        {
            SetTime(0, raiseWord: true, animate: false);
        }
        if (_prepared.Audio is { } audio)
        {
            audio.Rate = RateOf(Speed);
            audio.Seek(Position);
            audio.Play();
        }
        State = SpeechPlayerState.Playing;
        if (!ManualClock)
        {
            _clock.Start();
        }
        SyncControls();
        SyncWords(animate: false);
        var voice = EffectiveVoice;
        Announce(Labels.Reading(voice.Name, Labels.SpeedText(Speed), EffectiveSimulated));
        RaiseEvent(Args(PlayedEvent));
    }

    /// <summary>
    /// Gets speech for <paramref name="voice"/> (from the cache, or from the service with a cancellable request) and then
    /// plays or waits at <paramref name="resumeWord"/>. A newer request cancels this one.
    /// </summary>
    private async void Prepare(SpeechVoice voice, int resumeWord, bool play)
    {
        CancelRequest();
        StopSimulationLoop();
        _resumeWord = Math.Max(0, resumeWord);
        _playWhenReady = play;
        _clock.Stop();
        _prepared?.Audio?.Pause();
        if (_cache.TryGetValue(voice.Id, out var cached))
        {
            Apply(cached);
            return;
        }
        _prepared = null;
        _error = null;
        State = SpeechPlayerState.Preparing;
        SyncControls();
        SyncWords(animate: false);
        var request = new CancellationTokenSource();
        _request = request;
        SpeechAudio audio;
        try
        {
            var speech = new SpeechRequest(_document.Spoken, voice, request.Token);
            audio = Synthesize is { } synthesize
                ? await synthesize(speech)
                : await SpeechSimulation.SynthesizeAsync(speech, new SpeechSimulationOptions { Delay = SimulationDelay });
        }
        catch (Exception ex)
        {
            if (request.IsCancellationRequested || !ReferenceEquals(_request, request))
            {
                return;
            }
            _request = null;
            _error = ex is OperationCanceledException ? null : ex.Message;
            State = SpeechPlayerState.Failed;
            SyncControls();
            SyncWords(animate: false);
            Announce(string.IsNullOrEmpty(_error) ? Labels.Failed : Labels.Failed + ". " + _error);
            return;
        }
        if (request.IsCancellationRequested || !ReferenceEquals(_request, request))
        {
            return;
        }
        _request = null;
        var prepared = Build(audio, voice);
        _cache[voice.Id] = prepared;
        Apply(prepared);
    }

    /// <summary>The timings to use: the service's when they match the words, otherwise estimated and fitted to the length.</summary>
    private Prepared Build(SpeechAudio audio, SpeechVoice voice)
    {
        var words = _document.Words.Count;
        double? length = audio.Audio is { Duration: var d } && d > TimeSpan.Zero ? d.TotalSeconds : audio.Duration?.TotalSeconds;
        IReadOnlyList<SpeechWordTiming> timings;
        if (audio.Timings is { } given && given.Count == words)
        {
            timings = given;
        }
        else
        {
            var estimate = SpeechSimulation.EstimateTimings(_document.Spoken, SpeechSimulation.PaceOf(voice));
            timings = length is { } l ? SpeechSimulation.FitTimings(estimate, TimeSpan.FromSeconds(l)) : estimate;
        }
        var last = timings.Count > 0 ? timings[^1].End.TotalSeconds : 0;
        var duration = Math.Max(length ?? last + Tail, last);
        return new Prepared(audio, timings, duration);
    }

    private void Apply(Prepared prepared)
    {
        _prepared = prepared;
        _duration = prepared.Duration;
        _error = null;
        var timings = prepared.Timings;
        var at = _resumeWord < timings.Count ? timings[_resumeWord].Start.TotalSeconds : 0;
        prepared.Audio?.Seek(TimeSpan.FromSeconds(at));
        SetTime(at, raiseWord: true, animate: false);
        if (_playWhenReady)
        {
            _playWhenReady = false;
            StartPlaying();
        }
        else
        {
            State = SpeechPlayerState.Paused;
            SyncControls();
            SyncWords(animate: false);
        }
    }

    private void CancelRequest()
    {
        if (_request is { } request)
        {
            _request = null;
            request.Cancel();
            request.Dispose();
        }
    }

    /// <summary>
    /// One frame while playing: the position is the app's playback position, or the simulated clock advanced by the
    /// frame at the current speed. The bar is written every frame; words and the times only change when they change.
    /// </summary>
    private bool OnFrame(double dt)
    {
        if (State != SpeechPlayerState.Playing || _prepared is null)
        {
            return false;
        }
        if (_controls.Progress?.IsDragging == true)
        {
            return true;
        }
        var t = _prepared.Audio is { } audio
            ? audio.Position.TotalSeconds
            : _time + Math.Min(Math.Max(0, dt), MaxStep) * RateOf(Speed);
        if (t >= _duration - 1e-6)
        {
            SetTime(_duration, raiseWord: true, animate: true);
            Finish();
            return false;
        }
        SetTime(t, raiseWord: true, animate: true);
        return true;
    }

    /// <summary>Runs one frame of <paramref name="seconds"/> by hand (tests).</summary>
    internal void Step(double seconds)
    {
        if (!OnFrame(seconds))
        {
            _clock.Stop();
        }
    }

    private void Finish()
    {
        _clock.Stop();
        _prepared?.Audio?.Pause();
        State = SpeechPlayerState.Finished;
        SyncControls();
        SyncWords(animate: true);
        Announce(Labels.FinishedReading);
        RaiseEvent(Args(EndedEvent));
        ScheduleSimulationLoop();
    }

    private void SetTime(double seconds, bool raiseWord, bool animate)
    {
        _time = Math.Clamp(double.IsFinite(seconds) ? seconds : 0, 0, Math.Max(0, _duration));
        var word = _prepared is null ? CurrentWordIndex : SpeechPlayerText.WordAt(_prepared.Timings, _time);
        if (word != CurrentWordIndex)
        {
            CurrentWordIndex = word;
            SyncWords(animate, wordChanged: true);
            if (raiseWord && word >= 0 && word < _document.Words.Count)
            {
                RaiseEvent(new SpeechWordEventArgs(WordChangedEvent, Position, Duration, word, EffectiveVoice, Speed, _document.Words[word]));
            }
        }
        SyncTimes(force: false);
    }

    private void SeekCore(double seconds, bool dragging)
    {
        _interacted = true;
        StopSimulationLoop();
        if (_prepared is null)
        {
            return;
        }
        var t = Math.Clamp(double.IsFinite(seconds) ? seconds : 0, 0, _duration);
        _prepared.Audio?.Seek(TimeSpan.FromSeconds(t));
        SetTime(t, raiseWord: true, animate: false);
        if (!dragging && t >= _duration - 1e-6 && State is SpeechPlayerState.Playing or SpeechPlayerState.Paused)
        {
            _clock.Stop();
            _prepared.Audio?.Pause();
            State = SpeechPlayerState.Finished;
            SyncControls();
            SyncWords(animate: false);
        }
        else if (State == SpeechPlayerState.Finished && t < _duration - 1e-6)
        {
            State = SpeechPlayerState.Paused;
            SyncControls();
            SyncWords(animate: false);
        }
        if (!dragging)
        {
            RaiseEvent(Args(SeekedEvent));
        }
    }

    private void OnWordPressed(int wordIndex)
    {
        _interacted = true;
        StopSimulationLoop();
        SeekToWord(wordIndex);
    }

    /// <summary>
    /// A new voice: kept in the picker; while reading, the request for the old voice is aborted and the new voice is
    /// fetched and resumes from the word being spoken (from the start after the end).
    /// </summary>
    private void OnVoiceChanged(string? oldId)
    {
        var voice = EffectiveVoice;
        var voices = EffectiveVoices;
        var previous = voices.FirstOrDefault(v => v.Id == oldId) ?? (voices.Count > 0 ? voices[0] : voice);
        SyncControls();
        if (ReferenceEquals(previous, voice))
        {
            return;
        }
        RaiseEvent(new SpeechVoiceChangedEventArgs(VoiceChangedEvent, voice));
        if (!IsOpen)
        {
            return;
        }
        var state = State;
        var resume = state == SpeechPlayerState.Finished ? 0 : Math.Max(0, CurrentWordIndex);
        var play = state is SpeechPlayerState.Playing or SpeechPlayerState.Finished or SpeechPlayerState.Failed
                   || (state == SpeechPlayerState.Preparing && _playWhenReady);
        Announce(Labels.SwitchingTo(voice.Name));
        Prepare(voice, resume, play);
    }

    private void OnSpeedChanged()
    {
        if (_prepared?.Audio is { } audio)
        {
            audio.Rate = RateOf(Speed);
        }
        SyncControls();
        Announce(Labels.SpeedIs(Labels.SpeedText(Speed)));
        RaiseEvent(new SpeechSpeedChangedEventArgs(SpeedChangedEvent, Speed));
    }

    private void ResetForNewText()
    {
        CancelRequest();
        StopSimulationLoop();
        foreach (var prepared in _cache.Values)
        {
            prepared.Audio?.Pause();
            if (prepared.Audio is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _cache.Clear();
        _prepared = null;
        _clock.Stop();
        _playWhenReady = false;
        _time = 0;
        _duration = 0;
        _error = null;
        CurrentWordIndex = -1;
        if (IsOpen)
        {
            IsOpen = false;
            PseudoClasses.Set(OpenPseudoClass, false);
            if (_dock is not null)
            {
                _dock.Content = _actionRow;
            }
        }
        State = SpeechPlayerState.Idle;
        SyncControls();
        SyncWords(animate: false);
    }

    private void OnReadAloud(object? sender, RoutedEventArgs e)
    {
        var keyboard = _actionRow.ReadAloud?.Classes.Contains(":focus-visible") == true;
        _interacted = true;
        Open();
        Dispatcher.UIThread.Post(() => _controls.Play?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer),
            DispatcherPriority.Loaded);
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        // Preparing: the button keeps focus but does nothing (aria-disabled).
        if (State == SpeechPlayerState.Preparing)
        {
            return;
        }
        TogglePlayback();
    }

    private void OnSpeedClick(object? sender, RoutedEventArgs e)
    {
        var next = Speed switch
        {
            SpeechSpeed.Normal => SpeechSpeed.OneAndHalf,
            SpeechSpeed.OneAndHalf => SpeechSpeed.Twice,
            _ => SpeechSpeed.Normal
        };
        SetCurrentValue(SpeedProperty, next);
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        var keyboard = _controls.Stop?.Classes.Contains(":focus-visible") == true;
        Close();
        Dispatcher.UIThread.Post(() => _actionRow.ReadAloud?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer),
            DispatcherPriority.Loaded);
    }

    /// <summary>
    /// The paragraphs follow the spoken word: the highlight glides along a line (150ms at 1×, divided by the speed so it
    /// lands before the next word) and cuts to a new line or paragraph; the new word fades from dimmed to full; the
    /// reading look fades in and out with the player. Code-driven because the positions are measured from the text
    /// layout and the timing follows the speech, which a ControlTheme transition cannot express. Reduced motion: no
    /// glide (the highlight moves at once), fades kept. Off: everything at once.
    /// </summary>
    private void SyncWords(bool animate, bool wordChanged = false)
    {
        var word = CurrentWordIndex;
        var state = State;
        var finished = state == SpeechPlayerState.Finished;
        var level = ArcMotion.GetEffectiveLevel(this);
        var full = level == ArcMotionLevel.Full && IsLoaded;
        var clickable = IsOpen && _prepared is not null && state != SpeechPlayerState.Failed;
        var glides = animate && full && state == SpeechPlayerState.Playing;
        SpeechPlayerParagraph? glider = null;
        foreach (var paragraph in _paragraphs)
        {
            paragraph.WordsClickable = clickable;
            if (paragraph.Source is not { } source)
            {
                continue;
            }
            int local;
            if (finished || word >= source.WordOffset + source.Words.Count)
            {
                local = SpeechPlayerParagraph.AllSpoken;
            }
            else if (word < source.WordOffset)
            {
                local = -1;
            }
            else
            {
                local = word - source.WordOffset;
            }
            if (paragraph.MoveTo(local, glides))
            {
                glider = paragraph;
            }
        }
        var glide = GlideDuration();
        if (glider is not null)
        {
            if (_gliding is not null && !ReferenceEquals(_gliding, glider))
            {
                _gliding.SetGlide(1);
            }
            _gliding = glider;
            _glideValue ??= CreateValue(v => _gliding?.SetGlide(v));
            _glideValue.Jump(0);
            _ = _glideValue.AnimateTo(1, MotionSpec.Tween(glide, ArcEasings.Standard));
        }
        if (wordChanged)
        {
            _fadeValue ??= CreateValue(v =>
            {
                _fade = v;
                PushReading();
            });
            if (animate && level != ArcMotionLevel.Off && IsLoaded && state == SpeechPlayerState.Playing)
            {
                _fade = 0;
                _fadeValue.Jump(0);
                _ = _fadeValue.AnimateTo(1, MotionSpec.Tween(glide, ArcEasings.Standard));
            }
            else
            {
                _fadeValue.Jump(1);
                _fade = 1;
            }
        }
        var target = IsOpen && !finished && state != SpeechPlayerState.Idle && word >= 0 ? 1.0 : 0.0;
        if (!target.Equals(_readingTarget))
        {
            _readingTarget = target;
            _readingValue ??= CreateValue(v =>
            {
                _reading = v;
                PushReading();
            });
            if (IsLoaded && level != ArcMotionLevel.Off)
            {
                _ = _readingValue.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _readingValue.Jump(target);
                _reading = target;
            }
        }
        PushReading();
    }

    /// <summary>The glide between words: the token's 150ms at 1×, about 100ms at 1.5× and 75ms at 2×.</summary>
    private TimeSpan GlideDuration()
    {
        var at1 = _paragraphs.Count > 0 ? _paragraphs[0].GlideDuration : TimeSpan.FromMilliseconds(150);
        return TimeSpan.FromMilliseconds(at1.TotalMilliseconds / RateOf(Speed));
    }

    private MotionValue<double> CreateValue(Action<double> changed)
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += changed;
        return value;
    }

    private void PushReading()
    {
        foreach (var paragraph in _paragraphs)
        {
            paragraph.SetReading(_reading, _fade);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_viewport is null)
        {
            _viewport = new ViewportVisibility(this, 0.5, once: true);
            _viewport.VisibilityChanged += OnViewportVisibility;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopSimulationLoop();
        if (State == SpeechPlayerState.Playing)
        {
            Pause();
        }
        if (_viewport is not null)
        {
            _viewport.VisibilityChanged -= OnViewportVisibility;
            _viewport.Dispose();
            _viewport = null;
        }
    }

    private void OnViewportVisibility(object? sender, bool visible)
    {
        _inView = visible || _inView;
        TryAutoPlay();
    }

    /// <summary>Starts reading once in view when <see cref="AutoPlay"/> allows it (a simulation does by default), once.</summary>
    private void TryAutoPlay()
    {
        if (EffectiveAutoPlay && _inView && !_autoPlayed && State == SpeechPlayerState.Idle && !IsOpen)
        {
            _autoPlayed = true;
            Open();
        }
    }

    /// <summary>
    /// The simulation reads again with the other of its first two voices after a short pause, until someone interacts.
    /// Reduced motion and Off drop the loop.
    /// </summary>
    private void ScheduleSimulationLoop()
    {
        if (!PlaysSimulation || !EffectiveAutoPlay || _interacted || !IsOpen || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            return;
        }
        StopSimulationLoop();
        _loopTimer = new DispatcherTimer { Interval = SimulationLoopPause };
        _loopTimer.Tick += OnSimulationLoop;
        _loopTimer.Start();
    }

    private void OnSimulationLoop(object? sender, EventArgs e)
    {
        StopSimulationLoop();
        if (_interacted || State != SpeechPlayerState.Finished)
        {
            return;
        }
        var voices = EffectiveVoices;
        var next = voices.Count > 1 && ReferenceEquals(EffectiveVoice, voices[0]) ? voices[1] : voices[0];
        if (ReferenceEquals(next, EffectiveVoice))
        {
            Play();
            return;
        }
        SetCurrentValue(SelectedVoiceIdProperty, next.Id);
    }

    private void StopSimulationLoop()
    {
        if (_loopTimer is { } timer)
        {
            _loopTimer = null;
            timer.Stop();
            timer.Tick -= OnSimulationLoop;
        }
    }
}
