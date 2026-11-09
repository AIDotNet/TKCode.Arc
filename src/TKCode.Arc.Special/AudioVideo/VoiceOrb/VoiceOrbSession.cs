using System.Diagnostics;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A simulated voice conversation that drives a <see cref="VoiceOrb"/>, a <see cref="VoiceTranscript"/> and a
/// <see cref="VoiceControls"/> together, with the timing of the Arc Voice orb preview (design reference). It listens
/// (the user's words appear 0.9 s in, then every 150 ms + 22 ms per character of the previous word), thinks 0.7 s after
/// the last word for 1.5 s, speaks the answer word by word (pulsing the orb with each word), and listens again 1.3 s after
/// the last word, looping through the <see cref="Script"/>. Muting pauses the conversation, not the call timer.
/// </summary>
/// <remarks>
/// Nothing is heard or spoken: it is a stand-in for a real voice backend, replaceable piece by piece. Its clock is
/// <see cref="Advance"/>; <see cref="Attach"/> also runs a frame timer that calls it while a session is active.
/// </remarks>
public sealed class VoiceOrbSession
{
    /// <summary>The first user word appears this long after listening starts.</summary>
    public static readonly TimeSpan FirstWordDelay = TimeSpan.FromMilliseconds(900);

    /// <summary>Thinking starts this long after the user's last word.</summary>
    public static readonly TimeSpan ThinkDelay = TimeSpan.FromMilliseconds(700);

    /// <summary>How long the assistant thinks.</summary>
    public static readonly TimeSpan ThinkDuration = TimeSpan.FromMilliseconds(1500);

    /// <summary>The first answer word appears this long after speaking starts.</summary>
    public static readonly TimeSpan FirstAnswerWordDelay = TimeSpan.FromMilliseconds(270);

    /// <summary>The next exchange starts this long after the answer's last word.</summary>
    public static readonly TimeSpan NextTurnDelay = TimeSpan.FromMilliseconds(1300);

    private enum Phase
    {
        Idle,
        Hearing,
        Heard,
        Thinking,
        Speaking,
        Spoken
    }

    private Phase _phase;
    private double _wait;
    private string[] _words = [];
    private int _shown;
    private double _elapsed;
    private DispatcherTimer? _timer;
    private LiveLoop? _clock;
    private Visual? _clockOwner;
    private Stopwatch? _stopwatch;

    /// <summary>Creates a session that plays <paramref name="script"/>.</summary>
    public VoiceOrbSession(VoiceOrbScript script)
    {
        Script = script;
        Text = script.Intro;
        Speaker = VoiceTranscriptSpeaker.System;
        IsComplete = true;
    }

    /// <summary>The conversation it plays.</summary>
    public VoiceOrbScript Script { get; }

    /// <summary>Whether a call is running.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Whether the microphone is muted (the conversation pauses).</summary>
    public bool IsMuted { get; private set; }

    /// <summary>The orb's state.</summary>
    public VoiceOrbState State { get; private set; }

    /// <summary>The call's length.</summary>
    public TimeSpan Elapsed => TimeSpan.FromSeconds(_elapsed);

    /// <summary>The caption text of the current turn.</summary>
    public string Text { get; private set; }

    /// <summary>The caption turn number (it grows with every new line).</summary>
    public int Turn { get; private set; }

    /// <summary>Who the caption belongs to.</summary>
    public VoiceTranscriptSpeaker Speaker { get; private set; }

    /// <summary>Whether the caption is final.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>The index of the exchange being played.</summary>
    public int ExchangeIndex { get; private set; }

    /// <summary>Raised after any of the session's properties changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised with each word the assistant speaks (the orb pulses with it).</summary>
    public event EventHandler<string>? AnswerWord;

    /// <summary>The gap after <paramref name="word"/> before the next word appears: 150 ms + 22 ms per character.</summary>
    public static TimeSpan WordGap(string word) => TimeSpan.FromMilliseconds(150 + 22 * word.Length);

    /// <summary>Starts a call at the first exchange.</summary>
    public void Start()
    {
        if (IsActive)
        {
            return;
        }
        IsActive = true;
        IsMuted = false;
        _elapsed = 0;
        ExchangeIndex = 0;
        Listen();
    }

    /// <summary>Ends the call and shows how long it lasted.</summary>
    public void End()
    {
        if (!IsActive)
        {
            return;
        }
        IsActive = false;
        IsMuted = false;
        _phase = Phase.Idle;
        State = VoiceOrbState.Idle;
        Turn++;
        Speaker = VoiceTranscriptSpeaker.System;
        Text = Script.Ended(Elapsed);
        IsComplete = true;
        Raise();
    }

    /// <summary>Mutes or unmutes the microphone; while muted the conversation waits where it is.</summary>
    public void SetMuted(bool muted)
    {
        if (IsMuted == muted || !IsActive)
        {
            return;
        }
        IsMuted = muted;
        Raise();
    }

    /// <summary>
    /// What a tap on the orb does in the current state: start the call, send the user's sentence now, cancel the
    /// answer and keep talking, or interrupt the assistant.
    /// </summary>
    public void Tap()
    {
        switch (State)
        {
            case VoiceOrbState.Idle:
                Start();
                break;
            case VoiceOrbState.Listening:
                SendNow();
                break;
            case VoiceOrbState.Thinking:
                CancelAnswer();
                break;
            case VoiceOrbState.Speaking:
                Interrupt();
                break;
        }
    }

    /// <summary>Completes the user's sentence at once and starts thinking.</summary>
    public void SendNow()
    {
        if (!IsActive || State != VoiceOrbState.Listening)
        {
            return;
        }
        _words = Words(Exchange.User);
        _shown = _words.Length;
        Text = Exchange.User;
        Think();
    }

    /// <summary>Stops thinking and goes back to listening; the same sentence is sent again 0.7 s later.</summary>
    public void CancelAnswer()
    {
        if (!IsActive || State != VoiceOrbState.Thinking)
        {
            return;
        }
        State = VoiceOrbState.Listening;
        _phase = Phase.Heard;
        _wait = ThinkDelay.TotalSeconds;
        Raise();
    }

    /// <summary>Stops the assistant at the current word and starts the next exchange.</summary>
    public void Interrupt()
    {
        if (!IsActive || State != VoiceOrbState.Speaking)
        {
            return;
        }
        ExchangeIndex = (ExchangeIndex + 1) % Math.Max(1, Script.Turns.Count);
        Listen();
    }

    /// <summary>Advances the session's clock by <paramref name="delta"/>.</summary>
    public void Advance(TimeSpan delta)
    {
        if (!IsActive || delta <= TimeSpan.Zero)
        {
            return;
        }
        var whole = Math.Floor(_elapsed);
        var dt = delta.TotalSeconds;
        _elapsed += dt;
        var changed = Math.Floor(_elapsed) != whole;
        if (!IsMuted)
        {
            _wait -= dt;
            // Several beats may fall in one long step; run each in order.
            var guard = 0;
            while (_wait <= 0 && _phase != Phase.Idle && guard++ < 64)
            {
                Beat();
                changed = true;
            }
        }
        if (changed)
        {
            Raise();
        }
    }

    /// <summary>
    /// Connects the session to the three controls: it pushes its state into them, listens to the bar's start, mute and
    /// end, pulses the orb with each answer word, and runs a frame timer while a call is active. Dispose the result to
    /// disconnect.
    /// </summary>
    public IDisposable Attach(VoiceOrb? orb, VoiceTranscript? transcript, VoiceControls? controls, bool runClock = true)
    {
        if (runClock)
        {
            SetClockOwner((Visual?)orb ?? (Visual?)transcript ?? controls);
        }

        void Push(object? sender, EventArgs e)
        {
            if (orb is not null)
            {
                orb.State = State;
                orb.IsMuted = IsMuted;
            }
            if (transcript is not null)
            {
                transcript.Turn = Turn;
                transcript.Speaker = Speaker;
                transcript.Text = Text;
                transcript.IsComplete = IsComplete;
            }
            if (controls is not null)
            {
                controls.IsActive = IsActive;
                controls.IsMuted = IsMuted;
                controls.Elapsed = TimeSpan.FromSeconds(Math.Floor(_elapsed));
            }
            if (runClock)
            {
                UpdateClock();
            }
        }

        void Pulse(object? sender, string word) => orb?.PulseOutput(0.55 + Math.Min(0.3, 0.03 * word.Length));
        void OnStart(object? sender, RoutedEventArgs e) => Start();
        void OnEnd(object? sender, RoutedEventArgs e) => End();
        void OnMute(object? sender, VoiceControlsMuteChangedEventArgs e) => SetMuted(e.IsMuted);

        Changed += Push;
        AnswerWord += Pulse;
        if (controls is not null)
        {
            controls.StartRequested += OnStart;
            controls.EndRequested += OnEnd;
            controls.MuteChanged += OnMute;
        }
        Push(this, EventArgs.Empty);
        return new Detach(() =>
        {
            Changed -= Push;
            AnswerWord -= Pulse;
            if (controls is not null)
            {
                controls.StartRequested -= OnStart;
                controls.EndRequested -= OnEnd;
                controls.MuteChanged -= OnMute;
            }
            _timer?.Stop();
            _clock?.Dispose();
            _clock = null;
            _clockOwner = null;
        });
    }

    private VoiceOrbScriptTurn Exchange => Script.Turns.Count == 0
        ? new VoiceOrbScriptTurn("", "")
        : Script.Turns[ExchangeIndex % Script.Turns.Count];

    private void SetClockOwner(Visual? owner)
    {
        if (ReferenceEquals(owner, _clockOwner))
        {
            return;
        }
        _clock?.Dispose();
        _clock = null;
        _clockOwner = owner;
    }

    // A session with a visual ticks on that visual's frames and rests while it is hidden or detached. It is a functional
    // clock, so it runs at every motion level and keeps time while merely scrolled out of view.
    private bool ClockStep(double dt)
    {
        // A stalled window loses time instead of jumping ahead.
        Advance(TimeSpan.FromSeconds(Math.Min(dt, 0.25)));
        return true;
    }

    private void UpdateClock()
    {
        if (_clockOwner is { } owner)
        {
            if (IsActive)
            {
                _clock ??= new LiveLoop(owner, ClockStep) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
                _clock.Start();
            }
            else
            {
                _clock?.Stop();
            }
            return;
        }
        if (IsActive)
        {
            if (_timer is null)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
                _timer.Tick += (_, _) =>
                {
                    var now = _stopwatch!.Elapsed;
                    _stopwatch.Restart();
                    // A stalled window loses time instead of jumping ahead.
                    Advance(now > TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : now);
                };
            }
            if (!_timer.IsEnabled)
            {
                _stopwatch = Stopwatch.StartNew();
                _timer.Start();
            }
        }
        else
        {
            _timer?.Stop();
        }
    }

    private void Listen()
    {
        _phase = Phase.Hearing;
        _words = Words(Exchange.User);
        _shown = 0;
        _wait = FirstWordDelay.TotalSeconds;
        State = VoiceOrbState.Listening;
        Turn++;
        Speaker = VoiceTranscriptSpeaker.User;
        Text = "";
        IsComplete = false;
        Raise();
    }

    private void Think()
    {
        _phase = Phase.Thinking;
        _wait = ThinkDuration.TotalSeconds;
        State = VoiceOrbState.Thinking;
        IsComplete = true;
        Raise();
    }

    private void Beat()
    {
        switch (_phase)
        {
            case Phase.Hearing:
                if (_shown < _words.Length)
                {
                    _shown++;
                    Text = string.Join(' ', _words, 0, _shown);
                }
                if (_shown >= _words.Length)
                {
                    _phase = Phase.Heard;
                    _wait += ThinkDelay.TotalSeconds;
                }
                else
                {
                    _wait += WordGap(_words[_shown - 1]).TotalSeconds;
                }
                break;
            case Phase.Heard:
            {
                var carry = _wait;
                Think();
                _wait += carry;
                break;
            }
            case Phase.Thinking:
                _phase = Phase.Speaking;
                _words = Words(Exchange.Assistant);
                _shown = 0;
                State = VoiceOrbState.Speaking;
                Turn++;
                Speaker = VoiceTranscriptSpeaker.Assistant;
                Text = "";
                IsComplete = false;
                _wait += FirstAnswerWordDelay.TotalSeconds;
                break;
            case Phase.Speaking:
                if (_shown < _words.Length)
                {
                    _shown++;
                    Text = string.Join(' ', _words, 0, _shown);
                    AnswerWord?.Invoke(this, _words[_shown - 1]);
                }
                if (_shown >= _words.Length)
                {
                    IsComplete = true;
                    _phase = Phase.Spoken;
                    _wait += NextTurnDelay.TotalSeconds;
                }
                else
                {
                    _wait += WordGap(_words[_shown - 1]).TotalSeconds;
                }
                break;
            case Phase.Spoken:
            {
                ExchangeIndex = (ExchangeIndex + 1) % Math.Max(1, Script.Turns.Count);
                var carry = _wait;
                Listen();
                _wait += carry;
                break;
            }
        }
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    private static string[] Words(string text) => VoiceTranscriptLayout.Words(text);

    private sealed class Detach(Action action) : IDisposable
    {
        private Action? _action = action;

        public void Dispose()
        {
            _action?.Invoke();
            _action = null;
        }
    }
}
