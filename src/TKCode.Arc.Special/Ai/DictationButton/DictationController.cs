using System.Diagnostics;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The headless state machine behind <see cref="DictationControl"/> and <see cref="DictationButton"/>: idle, starting,
/// listening, processing, then idle, or denied and error. It drives an <see cref="IDictationRecognizer"/>, keeps the live
/// transcript and the latest input level (read it per frame), stops on its own after <see cref="MaxDuration"/> and raises
/// an event for every change. Use it on the UI thread.
/// </summary>
public sealed class DictationController
{
    private readonly DispatcherTimer _limit;
    private IDictationRecognizer? _recognizer;
    private CancellationTokenSource? _cancel;
    private IDictationSession? _session;
    private int _generation;
    private long _startedAt;
    private long _stoppedAt;
    private double _level;

    /// <summary>Creates a controller; with no recognizer, starting fails with <see cref="DictationErrorCode.Unsupported"/>.</summary>
    public DictationController(IDictationRecognizer? recognizer = null)
    {
        _recognizer = recognizer;
        _limit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(120) };
        _limit.Tick += OnLimit;
    }

    /// <summary>The speech source; null makes every start fail with <see cref="DictationErrorCode.Unsupported"/>.</summary>
    public IDictationRecognizer? Recognizer
    {
        get => _recognizer;
        set => _recognizer = value;
    }

    /// <summary>How long listening may run before it stops and inserts on its own; zero or less turns the limit off.</summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>Where the dictation is.</summary>
    public DictationStatus Status { get; private set; }

    /// <summary>The live transcript of the current session (empty when idle).</summary>
    public DictationTranscript Transcript { get; private set; } = DictationTranscript.Empty;

    /// <summary>The error of a denied or failed dictation, otherwise null.</summary>
    public DictationErrorCode? Error { get; private set; }

    /// <summary>The latest input level, 0 to 1 (0 unless listening).</summary>
    public double Level => Status == DictationStatus.Listening ? Volatile.Read(ref _level) : 0;

    /// <summary>How long the current session has listened; it stops counting when listening stops.</summary>
    public TimeSpan Elapsed
    {
        get
        {
            if (_startedAt == 0)
            {
                return TimeSpan.Zero;
            }
            var end = Status == DictationStatus.Listening ? Now() : _stoppedAt;
            return Stopwatch.GetElapsedTime(_startedAt, Math.Max(_startedAt, end));
        }
    }

    /// <summary>True while starting, listening or processing.</summary>
    public bool IsActive => Status is DictationStatus.Starting or DictationStatus.Listening or DictationStatus.Processing;

    /// <summary>True when the effective recognizer only simulates speech.</summary>
    public bool IsSimulated => (_recognizer ?? DictationUnavailableRecognizer.Instance).IsSimulated;

    /// <summary>The clock (Stopwatch timestamps); tests replace it.</summary>
    internal Func<long> Now { get; set; } = Stopwatch.GetTimestamp;

    /// <summary>Raised on every status change.</summary>
    public event EventHandler<DictationStatusChangedEventArgs>? StatusChanged;

    /// <summary>Raised whenever the live transcript changes.</summary>
    public event EventHandler<DictationTranscriptEventArgs>? TranscriptChanged;

    /// <summary>Raised with the final text when a dictation ends with words.</summary>
    public event EventHandler<DictationCommittedEventArgs>? Committed;

    /// <summary>Raised when a dictation is cancelled; drop any live text you showed.</summary>
    public event EventHandler? Cancelled;

    /// <summary>Raised when the recognizer detects the end of an utterance.</summary>
    public event EventHandler? SpeechEnded;

    /// <summary>Starts dictating from idle, denied or error. Completes when listening has started or failed.</summary>
    public async Task StartAsync()
    {
        if (IsActive)
        {
            return;
        }
        var generation = ++_generation;
        _cancel?.Dispose();
        var cancel = _cancel = new CancellationTokenSource();
        _session = null;
        _startedAt = 0;
        _stoppedAt = 0;
        Volatile.Write(ref _level, 0);
        Error = null;
        SetTranscript(DictationTranscript.Empty);
        SetStatus(DictationStatus.Starting);
        var handlers = new DictationHandlers(
            t =>
            {
                if (generation == _generation && Status is DictationStatus.Listening or DictationStatus.Starting or DictationStatus.Processing)
                {
                    SetTranscript(t);
                }
            },
            level =>
            {
                if (generation == _generation)
                {
                    Volatile.Write(ref _level, level);
                }
            },
            () =>
            {
                if (generation == _generation && IsActive)
                {
                    SpeechEnded?.Invoke(this, EventArgs.Empty);
                }
            },
            code =>
            {
                if (generation == _generation && IsActive)
                {
                    Fail(code);
                }
            });
        IDictationSession session;
        try
        {
            session = await (_recognizer ?? DictationUnavailableRecognizer.Instance).StartAsync(handlers, cancel.Token);
        }
        catch (DictationException e)
        {
            if (generation == _generation)
            {
                Fail(e.Code);
            }
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (generation == _generation)
            {
                Fail(DictationErrorCode.Failed);
            }
            return;
        }
        if (generation != _generation || Status != DictationStatus.Starting)
        {
            return;
        }
        _session = session;
        _startedAt = Now();
        SetStatus(DictationStatus.Listening);
        var max = MaxDuration;
        if (max > TimeSpan.Zero)
        {
            _limit.Interval = max;
            _limit.Start();
        }
    }

    /// <summary>
    /// Stops listening and inserts: the recognizer settles the last words, then <see cref="Committed"/> carries the text.
    /// While starting, it cancels instead.
    /// </summary>
    public async Task ConfirmAsync()
    {
        if (Status == DictationStatus.Starting)
        {
            Cancel();
            return;
        }
        if (Status != DictationStatus.Listening || _session is not { } session)
        {
            return;
        }
        var generation = _generation;
        _limit.Stop();
        _stoppedAt = Now();
        Volatile.Write(ref _level, 0);
        SetStatus(DictationStatus.Processing);
        string? text;
        try
        {
            text = await session.StopAsync();
        }
        catch (DictationException e)
        {
            if (generation == _generation)
            {
                Fail(e.Code);
            }
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (generation == _generation)
            {
                Fail(DictationErrorCode.Failed);
            }
            return;
        }
        if (generation != _generation || Status != DictationStatus.Processing)
        {
            return;
        }
        var final = string.IsNullOrWhiteSpace(text) ? Transcript.Text : text.Trim();
        _generation++;
        _session = null;
        _cancel?.Dispose();
        _cancel = null;
        SetTranscript(DictationTranscript.Empty);
        SetStatus(DictationStatus.Idle);
        if (final.Length > 0)
        {
            Committed?.Invoke(this, new DictationCommittedEventArgs(null, final));
        }
    }

    /// <summary>Cancels a running dictation and discards its words.</summary>
    public void Cancel()
    {
        if (!IsActive)
        {
            return;
        }
        _generation++;
        _limit.Stop();
        if (_stoppedAt == 0 && _startedAt != 0)
        {
            _stoppedAt = Now();
        }
        var cancel = _cancel;
        _cancel = null;
        _session = null;
        cancel?.Cancel();
        cancel?.Dispose();
        Volatile.Write(ref _level, 0);
        SetTranscript(DictationTranscript.Empty);
        SetStatus(DictationStatus.Idle);
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Dismisses a denied or failed state back to idle.</summary>
    public void Dismiss()
    {
        if (Status is DictationStatus.Denied or DictationStatus.Error)
        {
            Error = null;
            SetStatus(DictationStatus.Idle);
        }
    }

    private void OnLimit(object? sender, EventArgs e)
    {
        _limit.Stop();
        _ = ConfirmAsync();
    }

    private void Fail(DictationErrorCode code)
    {
        _generation++;
        _limit.Stop();
        if (_stoppedAt == 0 && _startedAt != 0)
        {
            _stoppedAt = Now();
        }
        var cancel = _cancel;
        _cancel = null;
        _session = null;
        cancel?.Cancel();
        cancel?.Dispose();
        Volatile.Write(ref _level, 0);
        Error = code;
        SetTranscript(DictationTranscript.Empty);
        SetStatus(code == DictationErrorCode.NotAllowed ? DictationStatus.Denied : DictationStatus.Error);
    }

    private void SetStatus(DictationStatus status)
    {
        if (Status == status)
        {
            return;
        }
        Status = status;
        StatusChanged?.Invoke(this, new DictationStatusChangedEventArgs(null, status, Error));
    }

    private void SetTranscript(DictationTranscript transcript)
    {
        if (Transcript == transcript)
        {
            return;
        }
        Transcript = transcript;
        TranscriptChanged?.Invoke(this, new DictationTranscriptEventArgs(null, transcript));
    }
}
