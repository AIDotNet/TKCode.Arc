using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A simulated search for a <see cref="SourceCards"/>, for demos, prototypes and tests. In the
/// <see cref="SourceCardsScenario.Live"/> scenario the <see cref="Sources"/> you pass stream into their slots (the first
/// after 0.45s, then one every 0.33s), the search finishes, the <see cref="Answer"/> streams a word every 70ms with its
/// [n] citations, the cards tour two citations, close the stack and start over, looping until someone interacts. In
/// <see cref="SourceCardsScenario.Error"/> the search fails after two sources with <see cref="ErrorText"/>; in
/// <see cref="SourceCardsScenario.Empty"/> it finds nothing for <see cref="EmptyQuery"/>. Nothing is searched, and
/// simulated cards open no address.
/// <para>
/// Set it as the cards' <see cref="SourceCards.Simulation"/>: it plays while <see cref="SourceCards.Sources"/> is null and
/// only while the cards are on screen, and starts over when its <see cref="Scenario"/> changes or the person presses Retry
/// or Search again. The other values take effect at the next start. Use one simulation per <see cref="SourceCards"/>.
/// </para>
/// </summary>
public sealed class SourceCardsSimulation : AvaloniaObject
{
    /// <summary>When the first source arrives, and the step between arrivals (seconds).</summary>
    internal const double FirstArrival = 0.45;

    internal const double ArrivalStep = 0.33;

    /// <summary>The live story: done at 2.4s, the answer from 2.7s (a word every 70ms), the citation tour at 7.4s and
    /// 10.4s, the stack closes at 13.6s and the story starts over at 15.5s. Later beats move back when more sources or a
    /// longer answer need the time.</summary>
    internal const double DoneAt = 2.4;

    internal const double AnswerAt = 2.7;

    internal const double WordStep = 0.07;

    internal const double FirstTourAt = 7.4;

    internal const double SecondTourAt = 10.4;

    internal const double CollapseAt = 13.6;

    internal const double LoopAt = 15.5;

    /// <summary>The failed search: two sources arrive, then it fails at 2.6s.</summary>
    internal const double ErrorAt = 2.6;

    internal const int ErrorArrivals = 2;

    /// <summary>The empty search finishes at 1.9s and reports that time.</summary>
    internal const double EmptyDoneAt = 1.9;

    /// <summary>Which story to tell: a live search with a cited answer, a failed search or an empty one.</summary>
    public static readonly StyledProperty<SourceCardsScenario> ScenarioProperty =
        AvaloniaProperty.Register<SourceCardsSimulation, SourceCardsScenario>(nameof(Scenario));

    /// <summary>The sources the search finds, in citation order (source 1 is cited as [1]).</summary>
    public static readonly StyledProperty<IReadOnlyList<SourceCardsSource>?> SourcesProperty =
        AvaloniaProperty.Register<SourceCardsSimulation, IReadOnlyList<SourceCardsSource>?>(nameof(Sources));

    /// <summary>The query shown while searching.</summary>
    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<SourceCardsSimulation, string?>(nameof(Query));

    /// <summary>The query of the empty scenario, one that finds nothing; <see cref="Query"/> when not set.</summary>
    public static readonly StyledProperty<string?> EmptyQueryProperty =
        AvaloniaProperty.Register<SourceCardsSimulation, string?>(nameof(EmptyQuery));

    /// <summary>
    /// The answer the live scenario streams, words separated by spaces; a word written as <c>[n]</c> (optionally followed
    /// by punctuation, as in <c>[3].</c>) becomes a <see cref="SourceCitation"/> of source n.
    /// </summary>
    public static readonly StyledProperty<string?> AnswerProperty =
        AvaloniaProperty.Register<SourceCardsSimulation, string?>(nameof(Answer));

    /// <summary>The search time the live scenario reports when it finishes ("6 sources · 2.1s").</summary>
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<SourceCardsSimulation, TimeSpan?>(nameof(Duration));

    /// <summary>The detail the error scenario shows after "Search failed".</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<SourceCardsSimulation, string?>(nameof(ErrorText));

    private readonly List<(double At, Action Run)> _beats = [];
    private readonly List<SourceCardsSource> _arrived = [];
    private string[] _answer = [];
    private int _next;

    /// <summary>Creates a simulation with nothing to play; set <see cref="Sources"/>, <see cref="Answer"/> and the rest.</summary>
    public SourceCardsSimulation()
    {
    }

    /// <inheritdoc cref="ScenarioProperty"/>
    public SourceCardsScenario Scenario { get => GetValue(ScenarioProperty); set => SetValue(ScenarioProperty, value); }

    /// <inheritdoc cref="SourcesProperty"/>
    public IReadOnlyList<SourceCardsSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <inheritdoc cref="QueryProperty"/>
    public string? Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <inheritdoc cref="EmptyQueryProperty"/>
    public string? EmptyQuery { get => GetValue(EmptyQueryProperty); set => SetValue(EmptyQueryProperty, value); }

    /// <inheritdoc cref="AnswerProperty"/>
    public string? Answer { get => GetValue(AnswerProperty); set => SetValue(AnswerProperty, value); }

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan? Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <summary>Raised when the simulated search changes: a source arrives, the status changes or a word of the answer streams.</summary>
    public event EventHandler? Changed;

    /// <summary>A one-off for the cards to act on: the story started over, a citation tour, or the stack closing.</summary>
    internal event Action<SourceCardsSimulationCue>? Cued;

    /// <summary>Whether the story has started since the simulation was made.</summary>
    internal bool HasStarted { get; private set; }

    /// <summary>The story time in seconds.</summary>
    internal double Time { get; private set; }

    /// <summary>Whether someone has pressed or typed inside the cards: the live story then stops touring and looping.</summary>
    internal bool Interacted { get; set; }

    internal SourceCardsStatus Status { get; private set; } = SourceCardsStatus.Done;

    /// <summary>The sources that have arrived so far.</summary>
    internal IReadOnlyList<SourceCardsSource> Arrived => _arrived;

    internal int? Total { get; private set; }

    internal string? ShownQuery { get; private set; }

    internal TimeSpan? ShownDuration { get; private set; }

    internal string? ShownError { get; private set; }

    /// <summary>The words of the answer; the first <see cref="AnswerShown"/> have streamed.</summary>
    internal IReadOnlyList<string> AnswerWords => _answer;

    internal int AnswerShown { get; private set; }

    /// <summary>Plays the story from the top.</summary>
    public void Restart()
    {
        HasStarted = true;
        Time = 0;
        _next = 0;
        BuildBeats();
        RunDueBeats();
    }

    /// <summary>Moves the story on by <paramref name="seconds"/>; returns whether beats are still to come.</summary>
    internal bool Advance(double seconds)
    {
        if (!HasStarted)
        {
            return false;
        }
        Time += Math.Max(0, seconds);
        RunDueBeats();
        return _next < _beats.Count;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScenarioProperty && HasStarted)
        {
            Restart();
        }
    }

    private void RunDueBeats()
    {
        while (_next < _beats.Count && _beats[_next].At <= Time + 1e-9)
        {
            _beats[_next++].Run();
        }
    }

    private void BuildBeats()
    {
        _beats.Clear();
        var pool = Sources ?? [];
        _answer = string.IsNullOrWhiteSpace(Answer) ? [] : Answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (Scenario)
        {
            case SourceCardsScenario.Error:
                Beat(0, () => Reset(Query, pool.Count));
                Arrivals(pool, Math.Min(ErrorArrivals, pool.Count));
                Beat(ErrorAt, () =>
                {
                    Status = SourceCardsStatus.Error;
                    ShownError = ErrorText;
                    OnChanged();
                });
                break;
            case SourceCardsScenario.Empty:
                Beat(0, () => Reset(EmptyQuery ?? Query, null));
                Beat(EmptyDoneAt, () =>
                {
                    Status = SourceCardsStatus.Done;
                    ShownDuration = TimeSpan.FromSeconds(EmptyDoneAt);
                    OnChanged();
                });
                break;
            default:
                Beat(0, () => Reset(Query, pool.Count));
                var lastArrival = Arrivals(pool, pool.Count);
                // More sources than the sample's six arrive past 2.4s: the rest of the story waits for them.
                var shift = Math.Max(0, lastArrival + 0.3 - DoneAt);
                Beat(DoneAt + shift, () =>
                {
                    Status = SourceCardsStatus.Done;
                    ShownDuration = Duration;
                    OnChanged();
                });
                for (var i = 0; i < _answer.Length; i++)
                {
                    Beat(AnswerAt + shift + i * WordStep, () =>
                    {
                        AnswerShown++;
                        OnChanged();
                    });
                }
                // A longer answer pushes the tour back so it starts 0.8s after the last word at the earliest.
                var lastWord = AnswerAt + shift + Math.Max(0, _answer.Length - 1) * WordStep;
                shift = Math.Max(shift, lastWord + 0.8 - FirstTourAt);
                Beat(FirstTourAt + shift, () => Cue(SourceCardsSimulationCue.FirstTour));
                Beat(SecondTourAt + shift, () => Cue(SourceCardsSimulationCue.SecondTour));
                Beat(CollapseAt + shift, () => Cue(SourceCardsSimulationCue.Collapse));
                Beat(LoopAt + shift, () =>
                {
                    // The story loops until someone interacts; then it rests on its last state.
                    if (!Interacted)
                    {
                        Time = 0;
                        _next = 0;
                    }
                });
                break;
        }
    }

    private void Beat(double at, Action run) => _beats.Add((at, run));

    /// <summary>Schedules the first <paramref name="count"/> arrivals; returns when the last one lands.</summary>
    private double Arrivals(IReadOnlyList<SourceCardsSource> pool, int count)
    {
        var last = 0.0;
        for (var i = 0; i < count; i++)
        {
            var source = pool[i];
            last = FirstArrival + i * ArrivalStep;
            Beat(last, () =>
            {
                _arrived.Add(source);
                OnChanged();
            });
        }
        return last;
    }

    private void Reset(string? query, int? total)
    {
        _arrived.Clear();
        Status = SourceCardsStatus.Searching;
        Total = total;
        ShownQuery = query;
        ShownDuration = null;
        ShownError = null;
        AnswerShown = 0;
        Cue(SourceCardsSimulationCue.Reset);
        OnChanged();
    }

    private void Cue(SourceCardsSimulationCue cue)
    {
        if (cue == SourceCardsSimulationCue.Reset || !Interacted)
        {
            Cued?.Invoke(cue);
        }
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>A one-off a <see cref="SourceCardsSimulation"/> asks its cards to act on.</summary>
internal enum SourceCardsSimulationCue
{
    /// <summary>The story starts over: close the stack and clear the highlight.</summary>
    Reset,

    /// <summary>Highlight a card in the row.</summary>
    FirstTour,

    /// <summary>Highlight a card inside the stack (which opens it).</summary>
    SecondTour,

    /// <summary>Close the stack.</summary>
    Collapse
}
