using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The simulation: with no code and no hunks the card plays its <see cref="Simulation"/> on a clock. It reads the file for
/// 0.8s, then each hunk opens (its removed lines tint red in 240ms) and its new lines stream in one every 38ms; the whole
/// sample streams in about 1.6s. With <see cref="AutoPlay"/> it then reviews itself (one hunk at a time and Accept all on
/// alternate runs) and replays 2.6s after it finishes. Any pointer, key or focus inside the card hands control to the
/// person for that run; Stop keeps the hunks that finished and Regenerate replays. Every beat is a single one-shot timer,
/// and a beat that falls due while the card is out of view waits until it is back. Reduced motion and Off open on the
/// finished suggestion with no streaming and no autoplay.
/// </summary>
public partial class CodeSuggestion
{
    /// <summary>How long the sample reads the file.</summary>
    internal static readonly TimeSpan SampleThinking = TimeSpan.FromMilliseconds(800);

    /// <summary>The wait between two streamed lines.</summary>
    internal static readonly TimeSpan SampleLineInterval = TimeSpan.FromMilliseconds(38);

    /// <summary>The wait after a hunk opens before its first line (its removed lines tint meanwhile).</summary>
    internal static readonly TimeSpan SampleHunkInterval = TimeSpan.FromMilliseconds(240);

    /// <summary>The wait from the finished suggestion to the first decision of the self review.</summary>
    internal static readonly TimeSpan SampleFirstDecision = TimeSpan.FromMilliseconds(1400);

    /// <summary>The wait between two decisions of the self review.</summary>
    internal static readonly TimeSpan SampleDecisionInterval = TimeSpan.FromMilliseconds(1000);

    /// <summary>The wait from a resolved sample to the replay.</summary>
    internal static readonly TimeSpan SampleReplay = TimeSpan.FromMilliseconds(2600);

    private List<CodeSuggestionHunk> _sampleScript = [];
    private List<CodeSuggestionHunk> _sampleHunks = [];
    private CodeSuggestionStatus _sampleStatus = CodeSuggestionStatus.Ready;
    private bool _wasSample;
    private bool _simStarted;
    private bool _inView;
    private bool _personInControl;
    private int _runCount;
    private int _streamHunk;
    private int _streamLine;
    private int _generation;
    private ITimer? _beat;
    private bool _beatIsAuto;
    private Action? _parked;
    private bool _parkedIsAuto;
    private ViewportVisibility? _visibility;

    /// <summary>The clock of the sample's beats (tests step it).</summary>
    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>How many sample runs have started.</summary>
    internal int RunCount => _runCount;

    /// <summary>Whether the person has taken over the current sample run.</summary>
    internal bool PersonInControl => _personInControl;

    /// <summary>Whether a sample beat is waiting (on its timer or for the card to be in view).</summary>
    internal bool HasPendingBeat => _beat is not null || _parked is not null;

    /// <summary>The sample reviews itself unless the person took over: its own decisions are not read aloud.</summary>
    private bool Quiet => UsesSample && !_personInControl;

    private void InitSample()
    {
        _wasSample = UsesSample;
        _sampleHunks = Simulation?.CopyHunks() ?? [];
        _sampleStatus = CodeSuggestionStatus.Ready;
    }

    /// <summary>The simulation asks for a new run (Replay in a demo).</summary>
    private void OnSimulationRestart(object? sender, EventArgs e)
    {
        if (UsesSample && ReferenceEquals(sender, Simulation) && this.IsAttachedToVisualTree())
        {
            _simStarted = true;
            StartRun();
        }
    }

    /// <summary>Before the first run: full motion waits on the plain file, otherwise the finished suggestion shows.</summary>
    private void PrepareSample()
    {
        if (ArcMotion.IsFull(this))
        {
            _sampleHunks = [];
            _sampleStatus = CodeSuggestionStatus.Thinking;
        }
        else
        {
            _sampleHunks = Simulation?.CopyHunks() ?? [];
            _sampleStatus = CodeSuggestionStatus.Ready;
        }
        ClearDecisions();
        Sync(animate: false);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _visibility?.Dispose();
        _visibility = new ViewportVisibility(this, 0.2, once: false);
        _visibility.VisibilityChanged += OnVisibilityChanged;
        if (UsesSample && !_simStarted)
        {
            PrepareSample();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
        _inView = false;
        StopSimulation();
        // Back in a window, the sample starts over.
        _simStarted = false;
    }

    private void OnVisibilityChanged(object? sender, bool visible)
    {
        _inView = visible;
        if (!visible)
        {
            return;
        }
        if (UsesSample && !_simStarted)
        {
            StartRun();
            return;
        }
        if (_parked is { } parked)
        {
            _parked = null;
            parked();
        }
    }

    /// <summary>Starts a sample run from the plain file (or shows the finished suggestion without full motion).</summary>
    internal void StartRun()
    {
        if (!UsesSample)
        {
            return;
        }
        StopSimulation();
        _simStarted = true;
        _runCount++;
        _personInControl = false;
        ClearDecisions();
        // Each run reads the simulation afresh, so changes to it show on the next run.
        _sampleScript = Simulation?.CopyHunks() ?? [];
        if (!ArcMotion.IsFull(this))
        {
            _sampleHunks = Simulation?.CopyHunks() ?? [];
            _sampleStatus = CodeSuggestionStatus.Ready;
            Sync(animate: false);
            return;
        }
        _sampleHunks = [];
        SetSampleStatus(CodeSuggestionStatus.Thinking);
        Beat(SampleThinking, BeginStreaming, auto: false);
    }

    private void SetSampleStatus(CodeSuggestionStatus status)
    {
        if (status == CodeSuggestionStatus.Thinking)
        {
            ClearDecisions();
        }
        _sampleStatus = status;
        Sync(animate: true);
        AnnounceStatus();
    }

    private void BeginStreaming()
    {
        _streamHunk = 0;
        SetSampleStatus(CodeSuggestionStatus.Streaming);
        NextHunk();
    }

    private void NextHunk()
    {
        if (_streamHunk >= _sampleScript.Count)
        {
            FinishStreaming();
            return;
        }
        var source = _sampleScript[_streamHunk];
        _sampleHunks.Add(new CodeSuggestionHunk(source.Id, source.Start, source.Remove) { Summary = source.Summary });
        _streamLine = 0;
        Sync(animate: true);
        Beat(SampleHunkInterval, NextLine, auto: false);
    }

    private void NextLine()
    {
        var source = _sampleScript[_streamHunk];
        if (_streamLine < source.Insert.Count)
        {
            _sampleHunks[^1].AppendLine(source.Insert[_streamLine++]);
            Sync(animate: true);
            Beat(SampleLineInterval, NextLine, auto: false);
            return;
        }
        _streamHunk++;
        NextHunk();
    }

    private void FinishStreaming()
    {
        SetSampleStatus(CodeSuggestionStatus.Ready);
        ScheduleAutoReview(SampleFirstDecision);
    }

    private void ScheduleAutoReview(TimeSpan delay)
    {
        if (AutoPlay && !_personInControl && ArcMotion.IsFull(this) && PendingCount > 0)
        {
            Beat(delay, AutoStep, auto: true);
        }
    }

    /// <summary>One beat of the self review: odd runs decide one hunk at a time (accept, reject, accept), even runs Accept all.</summary>
    private void AutoStep()
    {
        if (!UsesSample || _personInControl || EffectiveStatus != CodeSuggestionStatus.Ready)
        {
            return;
        }
        if (_runCount % 2 == 0)
        {
            AcceptAll();
            return;
        }
        for (var i = 0; i < _spans.Count; i++)
        {
            if (DecisionOf(_spans[i].Id) == CodeSuggestionDecision.Pending)
            {
                if (i % 2 == 0)
                {
                    Accept(_spans[i].Id);
                }
                else
                {
                    Reject(_spans[i].Id);
                }
                break;
            }
        }
        ScheduleAutoReview(SampleDecisionInterval);
    }

    /// <summary>Every sample hunk has a decision: replay later unless the person took over.</summary>
    private void OnSampleResolved()
    {
        if (UsesSample && AutoPlay && !_personInControl && ArcMotion.IsFull(this))
        {
            Beat(SampleReplay, StartRun, auto: true);
        }
    }

    /// <summary>Stop on the sample: the hunks that finished stay, a hunk half streamed goes, and the review begins.</summary>
    private void StopSample()
    {
        var status = _sampleStatus;
        if (status is not (CodeSuggestionStatus.Thinking or CodeSuggestionStatus.Streaming))
        {
            return;
        }
        StopSimulation();
        if (status == CodeSuggestionStatus.Streaming && _sampleHunks.Count > 0 && _streamHunk < _sampleScript.Count
            && _sampleHunks[^1].Insert.Count < _sampleScript[_streamHunk].Insert.Count)
        {
            _sampleHunks.RemoveAt(_sampleHunks.Count - 1);
        }
        SetSampleStatus(CodeSuggestionStatus.Ready);
    }

    /// <summary>A pointer, key or focus inside the card: the person reviews this run, the self review stops.</summary>
    private void HandOver()
    {
        if (!UsesSample || _personInControl)
        {
            return;
        }
        _personInControl = true;
        if (_beatIsAuto)
        {
            CancelBeat();
        }
        if (_parkedIsAuto)
        {
            _parked = null;
        }
    }

    private void StopSimulation()
    {
        CancelBeat();
        _parked = null;
    }

    /// <summary>Runs <paramref name="action"/> after <paramref name="delay"/>, or once the card is back in view. One beat at a time.</summary>
    private void Beat(TimeSpan delay, Action action, bool auto)
    {
        CancelBeat();
        _beatIsAuto = auto;
        var generation = _generation;
        _beat = ToolCallTimer.After(Clock, delay, () =>
        {
            if (generation != _generation)
            {
                return;
            }
            _beat = null;
            if (!_inView)
            {
                _parked = action;
                _parkedIsAuto = auto;
                return;
            }
            action();
        });
    }

    private void CancelBeat()
    {
        _generation++;
        _beat?.Dispose();
        _beat = null;
    }
}
