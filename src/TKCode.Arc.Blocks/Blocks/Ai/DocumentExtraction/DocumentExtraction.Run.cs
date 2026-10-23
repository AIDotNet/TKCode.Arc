using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class DocumentExtraction
{
    /// <summary>How long a span keeps the deep "fresh" tint after its value lands (spec 3.3: about 525ms).</summary>
    internal static readonly TimeSpan FreshTime = TimeSpan.FromMilliseconds(525);

    /// <summary>The cost readout's roll (spec 5.2: about 130ms).</summary>
    private static readonly TimeSpan CostRoll = TimeSpan.FromMilliseconds(130);

    /// <summary>The average's roll (spec 5.2: about 350ms, ease-out).</summary>
    private static readonly TimeSpan AverageRoll = TimeSpan.FromMilliseconds(350);

    private readonly LiveLoop _ticker;
    private Run? _run;
    private DateTimeOffset _runStart;
    private bool _hasRun;
    private MotionValue<double>? _costShown;
    private MotionValue<double>? _averageShown;
    private string? _costText;
    private string? _averageText;
    private int _averagePercent = -1;
    private double _averageTarget = double.NaN;
    private string? _elapsedText;
    private TimeSpan _elapsedShown;

    /// <summary>The clock of runs and timed tints (tests replace it).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>
    /// Starts the producer of a run off the UI thread (the extractor's contract); tests may run it inline on the UI thread so
    /// the steps are deterministic.
    /// </summary>
    internal Func<Func<Task>, Task> StartProducer { get; set; } = static work => Task.Run(work);

    /// <summary>Whether a run is going (reading or extracting).</summary>
    internal bool IsRunning => _run is not null;

    /// <summary>
    /// Reads the document with <see cref="Extractor"/>: clears every value, then fills the form as the events arrive. The
    /// task completes when the run ends (done, stopped or failed); it never throws for the extractor's failures, which show
    /// as <see cref="ExtractionState.Failed"/>. A run that is going is stopped first. Nothing happens without an extractor,
    /// a document or a schema.
    /// </summary>
    public Task StartAsync()
    {
        var extractor = Extractor;
        var document = EffectiveDocument;
        var schema = EffectiveSchema;
        if (extractor is null || document is null || schema is null || _store is null)
        {
            return Task.CompletedTask;
        }
        if (_run is not null)
        {
            EndRun(ExtractionState.Stopped, null, announce: false);
        }
        FinishCascadeAtOnce();
        ResetValues();
        var run = new Run(this);
        _run = run;
        _hasRun = true;
        _followPaused = false;
        _runStart = Time.GetUtcNow();
        Elapsed = TimeSpan.Zero;
        Cost = 0;
        ErrorMessage = null;
        State = ExtractionState.Reading;
        _viewer?.StartScan();
        if (_fieldsScroll is not null && _fieldsScroll.Offset.Y > 0)
        {
            // The form starts over from its top, where the first values land.
            ScrollPanel(_fieldsScroll, 0, ArcMotion.IsFull(this) && IsLoaded);
        }
        AnnounceRun(Labels.AnnounceStarted(_store.TotalFieldCount));
        RaiseEvent(new RoutedEventArgs(ExtractionStartedEvent, this));
        RefreshAll();
        _ticker.Start();
        _ = StartProducer(() => ProduceAsync(run, extractor, document, schema));
        return run.Completion.Task;
    }

    /// <summary>Stops the run that is going; the values found so far stay (spec 1.2: Stop and Escape).</summary>
    public void Stop()
    {
        if (_run is not null)
        {
            EndRun(ExtractionState.Stopped, null);
        }
    }

    /// <summary>Reads the extractor's stream (off the UI thread) and hands every event to the run's queue.</summary>
    private static async Task ProduceAsync(Run run, IDocumentExtractor extractor, ExtractionDocument document, ExtractionSchema schema)
    {
        try
        {
            await foreach (var item in extractor.ExtractAsync(document, schema, run.Token).WithCancellation(run.Token))
            {
                run.Post(item);
                if (item is ExtractionErrorEvent)
                {
                    return;
                }
            }
            run.Finish(null);
        }
        catch (OperationCanceledException) when (run.Token.IsCancellationRequested)
        {
            // Stopped by the control: it has already moved on.
        }
        catch (Exception exception)
        {
            run.Finish(exception);
        }
    }

    /// <summary>
    /// Applies every event the run queued since the last turn, as one update (spec 8: values that arrive together are laid
    /// out once), then ends the run when the stream has ended.
    /// </summary>
    private void Drain(Run run)
    {
        if (!ReferenceEquals(run, _run) || _store is not { } store)
        {
            return;
        }
        var landed = false;
        string? lastLanded = null;
        while (run.Events.TryDequeue(out var item))
        {
            switch (item)
            {
                case ExtractionValueEvent value:
                {
                    var before = store.Find(value.Path)?.Text ?? string.Empty;
                    if (store.ApplyModelValue(value) is not { } applied)
                    {
                        break;
                    }
                    landed = true;
                    lastLanded = applied.Path;
                    if (applied.SourceId is not null && ArcMotion.IsFull(this))
                    {
                        _freshUntil[applied.Path] = Time.GetUtcNow() + FreshTime;
                    }
                    RaiseEvent(new ExtractionValueChangedEventArgs(ValueChangedEvent, this, applied.Path, before, applied.Text, ExtractionOrigin.Model));
                    break;
                }
                case ExtractionUsageEvent usage:
                    SetCost(usage);
                    break;
                case ExtractionErrorEvent error:
                    store.Flush();
                    EndRun(ExtractionState.Failed, error.Message);
                    return;
            }
            if (!ReferenceEquals(run, _run))
            {
                // A handler of ValueChanged stopped or restarted the run.
                return;
            }
        }
        if (landed && State == ExtractionState.Reading)
        {
            State = ExtractionState.Extracting;
        }
        _lastLanded = lastLanded ?? _lastLanded;
        _followPending |= landed;
        store.Flush();
        if (run.IsFinished && run.Events.IsEmpty)
        {
            EndRun(run.Error is null ? ExtractionState.Done : ExtractionState.Failed, run.Error?.Message);
        }
        else if (landed)
        {
            _ticker.Start();
        }
    }

    /// <summary>
    /// Ends the run that is going with <paramref name="state"/> (Done, Stopped or Failed): the stream is cancelled, the
    /// clock stops at its last tenth, the run is announced and <see cref="ExtractionCompleted"/> raised.
    /// </summary>
    private void EndRun(ExtractionState state, string? error, bool announce = true)
    {
        if (_run is not { } run)
        {
            return;
        }
        _run = null;
        run.Cancel();
        _viewer?.StopScan();
        UpdateElapsed(Time.GetUtcNow());
        ErrorMessage = state == ExtractionState.Failed ? error ?? string.Empty : null;
        State = state;
        if (announce && _store is { } store)
        {
            AnnounceRun(state switch
            {
                ExtractionState.Done => Labels.AnnounceDone(store.FoundFieldCount, store.TotalFieldCount, store.CheckCount),
                ExtractionState.Stopped => Labels.AnnounceStopped,
                _ => Labels.AnnounceFailed(error ?? string.Empty)
            });
        }
        RefreshAll();
        RaiseEvent(new ExtractionCompletedEventArgs(ExtractionCompletedEvent, this, state, ErrorMessage));
        run.Completion.TrySetResult();
    }

    /// <summary>Back to no values: the store, the tints, the counters and the clock.</summary>
    private void ResetValues()
    {
        _freshUntil.Clear();
        _lastLanded = null;
        _followPending = false;
        _store?.Clear();
        _store?.Flush();
        _viewer?.ClearSpans();
        _pathSpan.Clear();
        _spanPaths.Clear();
        _spanPaints.Clear();
        Cost = 0;
        Elapsed = TimeSpan.Zero;
        _costShown?.Jump(0);
        _averageShown?.Jump(0);
        _averageTarget = double.NaN;
    }

    /// <summary>The cost of the tokens used so far (running totals) at the model's prices; the readout rolls to it.</summary>
    private void SetCost(ExtractionUsageEvent usage)
    {
        var model = Model;
        if (model is null)
        {
            return;
        }
        Cost = Math.Max(0, usage.InputTokens) * model.InputPricePerMillion / 1_000_000m
               + Math.Max(0, usage.OutputTokens) * model.OutputPricePerMillion / 1_000_000m;
        RollCost();
    }

    private void RollCost()
    {
        if (_meta is null)
        {
            return;
        }
        _costShown ??= CreateRoll(_meta, value => ShowCost((decimal)value));
        var target = (double)Cost;
        if (ArcMotion.IsFull(this) && IsLoaded)
        {
            _ = _costShown.AnimateTo(target, MotionSpec.Tween(CostRoll, ArcEasings.Standard));
        }
        else
        {
            _costShown.Jump(target);
            ShowCost(Cost);
        }
    }

    private void ShowCost(decimal cost)
    {
        // Text only when the shown digits change (no string per frame otherwise).
        var text = _hasRun ? Labels.Cost(cost) : Labels.NoCost;
        if (text != _costText && _meta is not null)
        {
            _costText = text;
            _meta.CostText = text;
        }
    }

    /// <summary>The average rolls to its new value (Full: about 350ms, ease-out; otherwise at once).</summary>
    private void RollAverage(double average)
    {
        if (_average is null)
        {
            return;
        }
        if (average == _averageTarget)
        {
            return;
        }
        _averageTarget = average;
        _averageShown ??= CreateRoll(_average, ShowAverage);
        if (ArcMotion.IsFull(this) && IsLoaded && _averagePercent >= 0)
        {
            _ = _averageShown.AnimateTo(average, MotionSpec.Tween(AverageRoll, ArcEasings.Enter));
        }
        else
        {
            _averageShown.Jump(average);
            ShowAverage(average);
        }
    }

    private void ShowAverage(double average)
    {
        var percent = (int)Math.Round(Math.Clamp(average, 0, 1) * 100);
        if (percent == _averagePercent || _average is null)
        {
            return;
        }
        _averagePercent = percent;
        _averageText = Labels.Average(percent);
        _average.Text = _averageText;
    }

    private static MotionValue<double> CreateRoll(Avalonia.Visual owner, Action<double> show)
    {
        var roll = new MotionValue<double>(owner, 0, MotionVectors.Double);
        roll.Changed += show;
        return roll;
    }

    /// <summary>The run's time to the tenth of a second (never negative), and its readout.</summary>
    private void UpdateElapsed(DateTimeOffset now)
    {
        if (_run is null && State is not (ExtractionState.Reading or ExtractionState.Extracting))
        {
            RefreshElapsedText();
            return;
        }
        var ticks = Math.Max(0, (now - _runStart).Ticks);
        var tenth = TimeSpan.FromMilliseconds(100).Ticks;
        var elapsed = TimeSpan.FromTicks(ticks / tenth * tenth);
        if (elapsed != Elapsed)
        {
            // A new tenth: the only time the readout's text is made (not every frame).
            Elapsed = elapsed;
            RefreshElapsedText();
        }
    }

    private void RefreshElapsedText()
    {
        if (_meta is not null && (_elapsedText is null || _elapsedShown != Elapsed))
        {
            _elapsedShown = Elapsed;
            _elapsedText = Labels.Seconds(Elapsed);
            _meta.ElapsedText = _elapsedText;
        }
    }

    /// <summary>The readouts are made again (new words).</summary>
    private void ForgetReadouts()
    {
        _elapsedText = null;
        _costText = null;
        _averagePercent = -1;
        _averageTarget = double.NaN;
    }

    /// <summary>The frame tick: the clock while running, the fresh tints running out, the accept cascade.</summary>
    private bool Tick(double seconds)
    {
        var now = Time.GetUtcNow();
        var keep = false;
        if (_run is not null)
        {
            UpdateElapsed(now);
            keep = true;
        }
        if (_freshUntil.Count > 0)
        {
            ExpireFresh(now);
            keep |= _freshUntil.Count > 0;
        }
        if (_cascade is not null)
        {
            AdvanceCascade(now);
            keep |= _cascade is not null;
        }
        if (_followPending)
        {
            _followPending = false;
            Follow();
        }
        return keep;
    }

    /// <summary>One run: its cancellation, the events waiting for the UI thread and how the stream ended.</summary>
    private sealed class Run
    {
        private readonly DocumentExtraction _owner;
        private readonly CancellationTokenSource _cancel = new();

        public Run(DocumentExtraction owner) => _owner = owner;

        public CancellationToken Token => _cancel.Token;

        public ConcurrentQueue<ExtractionEvent> Events { get; } = new();

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Whether a drain is queued on the UI thread (read and written there and by the producer under a lock).</summary>
        public bool DrainQueued { get; set; }

        public bool IsFinished { get; private set; }

        public Exception? Error { get; private set; }

        private readonly object _gate = new();

        /// <summary>Queues an event; the UI thread applies everything queued in one turn.</summary>
        public void Post(ExtractionEvent item)
        {
            Events.Enqueue(item);
            Schedule();
        }

        /// <summary>The stream ended (with an error, or none).</summary>
        public void Finish(Exception? error)
        {
            Error = error;
            IsFinished = true;
            Schedule();
        }

        private void Schedule()
        {
            lock (_gate)
            {
                if (DrainQueued)
                {
                    return;
                }
                DrainQueued = true;
            }
            Dispatcher.UIThread.Post(() =>
            {
                lock (_gate)
                {
                    DrainQueued = false;
                }
                _owner.Drain(this);
            }, DispatcherPriority.Normal);
        }

        public void Cancel()
        {
            try
            {
                _cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
