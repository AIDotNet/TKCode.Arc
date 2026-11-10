using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class ResearchRun
{
    /// <summary>The sample run advances in ticks of this length.</summary>
    internal static readonly TimeSpan SimulationTickLength = TimeSpan.FromMilliseconds(120);

    private readonly List<string> _simPlan = new();
    private readonly ObservableCollection<ResearchRunSource> _simSources = new();
    private readonly ObservableCollection<ResearchRunNote> _simNotes = new();
    private string? _simReport;
    private int _simPlanDone;
    private int _simTicks;
    private string[] _simParagraphs = [];
    private int _simParagraphsShown;
    private IDisposable? _simTimer;
    private int _simGeneration;

    // The clock.
    private DispatcherTimer? _clock;
    private long _runningSince;
    private TimeSpan _elapsedAtStart;

    internal IReadOnlyList<ResearchRunSource> ShownSources => IsSimulated ? _simSources : Sources ?? [];

    internal IReadOnlyList<ResearchRunNote> ShownNotes => IsSimulated ? _simNotes : Notes ?? [];

    internal IReadOnlyList<string> ShownPlan => IsSimulated ? _simPlan : Plan ?? [];

    internal int ShownPlanDone => IsSimulated ? _simPlanDone : CompletedPlanSteps;

    internal string? ShownReport => IsSimulated ? _simReport : Report;

    private void StartSimulation()
    {
        CancelSimulation();
        _simPlan.Clear();
        _simSources.Clear();
        _simNotes.Clear();
        _simReport = null;
        _simPlanDone = 0;
        _simTicks = 0;
        _simParagraphs = [];
        _simParagraphsShown = 0;
        SetCurrentValue(ElapsedProperty, TimeSpan.Zero);
        SetPhaseFromInside(ResearchRunPhase.Planning);
        RebuildAll();
        ResumeSimulation();
    }

    private void ResumeSimulation()
    {
        if (_simTimer is not null || !IsSimulated || !IsWorking(Phase))
        {
            return;
        }
        var generation = _simGeneration;
        _simTimer = Schedule(SimulationTickLength, () =>
        {
            if (generation != _simGeneration)
            {
                return;
            }
            _simTimer = null;
            SimulationTick();
            ResumeSimulation();
        });
    }

    private void PauseSimulation()
    {
        _simGeneration++;
        _simTimer?.Dispose();
        _simTimer = null;
    }

    private void CancelSimulation() => PauseSimulation();

    /// <summary>Advances the sample run by one tick (tests call it directly).</summary>
    internal void SimulationTick()
    {
        if (!IsSimulated || !IsWorking(Phase))
        {
            return;
        }
        _simTicks++;
        switch (Phase)
        {
            case ResearchRunPhase.Planning:
                TickPlanning();
                break;
            case ResearchRunPhase.Reading:
                TickReading();
                break;
            case ResearchRunPhase.Writing:
                TickWriting();
                break;
        }
    }

    private void TickPlanning()
    {
        if (_simTicks % 4 != 0)
        {
            return;
        }
        var plan = ResearchRunSamples.Plan();
        if (_simPlan.Count < plan.Count)
        {
            _simPlan.Add(plan[_simPlan.Count]);
            RebuildPlan();
            return;
        }
        _simPlanDone = 1;
        foreach (var source in ResearchRunSamples.Sources())
        {
            _simSources.Add(source);
        }
        SetPhaseFromInside(ResearchRunPhase.Reading);
    }

    private void TickReading()
    {
        var reading = 0;
        foreach (var source in _simSources)
        {
            if (source.Status == ResearchRunSourceStatus.Reading)
            {
                reading++;
            }
        }
        foreach (var source in _simSources)
        {
            if (reading >= 2)
            {
                break;
            }
            if (source.Status == ResearchRunSourceStatus.Queued)
            {
                source.Status = ResearchRunSourceStatus.Reading;
                reading++;
            }
        }
        var read = 0;
        var open = 0;
        foreach (var source in _simSources)
        {
            if (source.Status == ResearchRunSourceStatus.Reading)
            {
                // Each source reads at its own pace: 0.06 to 0.12 of it a tick.
                var progress = Math.Min(1, source.Progress + 0.06 + source.Index % 4 * 0.02);
                if (source.Index == 6 && progress >= 0.35)
                {
                    source.Snippet = ResearchRunSamples.SkippedReason;
                    source.Status = ResearchRunSourceStatus.Skipped;
                    continue;
                }
                source.Progress = progress;
                if (progress >= 1)
                {
                    source.Status = ResearchRunSourceStatus.Read;
                    if (ResearchRunSamples.NoteFor(source.Index) is { } note)
                    {
                        _simNotes.Add(note);
                    }
                }
            }
            if (source.Status == ResearchRunSourceStatus.Read)
            {
                read++;
            }
            else if (source.Status is ResearchRunSourceStatus.Queued or ResearchRunSourceStatus.Reading)
            {
                open++;
            }
        }
        var planDone = read >= 3 ? 2 : 1;
        if (planDone != _simPlanDone)
        {
            _simPlanDone = planDone;
            RebuildPlan();
        }
        if (open == 0)
        {
            _simPlanDone = 3;
            _simParagraphs = ResearchRunSamples.Report.Split("\n\n");
            _simParagraphsShown = 0;
            _simTicks = 0;
            SetPhaseFromInside(ResearchRunPhase.Writing);
        }
    }

    private void TickWriting()
    {
        if (_simTicks % 6 != 0)
        {
            return;
        }
        if (_simParagraphsShown < _simParagraphs.Length)
        {
            _simParagraphsShown++;
            _simReport = string.Join("\n\n", _simParagraphs, 0, _simParagraphsShown);
            RebuildReport();
            return;
        }
        _simPlanDone = _simPlan.Count;
        SetPhaseFromInside(ResearchRunPhase.Done);
    }

    #region Clock

    private void StartClock()
    {
        if (!_attached || _clock is not null)
        {
            return;
        }
        _elapsedAtStart = Elapsed;
        _runningSince = Now().Ticks;
        _clock = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => Tick());
        _clock.Start();
    }

    private void StopClock()
    {
        if (_clock is null)
        {
            return;
        }
        Tick();
        _clock.Stop();
        _clock = null;
    }

    /// <summary>Advances <see cref="Elapsed"/> by the real time since the clock started, in whole seconds.</summary>
    internal void Tick()
    {
        if (_clock is null)
        {
            return;
        }
        var elapsed = _elapsedAtStart + TimeSpan.FromTicks(Now().Ticks - _runningSince);
        var shown = TimeSpan.FromSeconds(Math.Floor(elapsed.TotalSeconds));
        if (shown != Elapsed)
        {
            SetCurrentValue(ElapsedProperty, shown);
        }
    }

    #endregion
}
