using System.Globalization;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// An evaluation suite for prompts or models: a list of test cases run on a baseline and a candidate version, the pass
/// rate of each with the change in points, and every case marked as passing, failing, a regression or a fix. Filter to the
/// regressions, open a case to read both outputs side by side or as a word diff with the reason each check gave, and run
/// the suite again: cases queue, run a few at a time and flip to their result one by one, and the run can be stopped.
/// <para>
/// Every number derives from <see cref="Cases"/>, <see cref="Results"/> and the two version ids. Without
/// <see cref="RunCaseAsync"/> a run is simulated from <see cref="EvalSuiteSamples"/>; with it, your code runs each case on
/// each version and returns an <see cref="EvalResult"/> (build it with <see cref="Grade"/> to apply the case's check).
/// The defaults are an invented sample suite of fourteen cases on three prompt versions.
/// </para>
/// </summary>
/// <example><code>
/// &lt;arc:EvalSuite Title="Support replies" Cases="{Binding Cases}" Versions="{Binding Versions}"
///                Results="{Binding Results, Mode=TwoWay}" BaselineVersionId="v12" CandidateVersionId="v13" /&gt;
/// </code></example>
public partial class EvalSuite : TemplatedControl
{
    #region Properties

    /// <summary>The suite's title over the version choices. Default: the sample suite's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EvalSuite, string?>(nameof(Title), EvalSuiteSamples.Title);

    /// <summary>The test cases, in list order. Default: fourteen invented cases (<see cref="EvalSuiteSamples.Cases"/>).</summary>
    public static readonly StyledProperty<IReadOnlyList<EvalCase>?> CasesProperty =
        AvaloniaProperty.Register<EvalSuite, IReadOnlyList<EvalCase>?>(nameof(Cases), EvalSuiteSamples.Cases());

    /// <summary>The versions offered as baseline and candidate. Default: v11, v12 and v13.</summary>
    public static readonly StyledProperty<IReadOnlyList<EvalVersion>?> VersionsProperty =
        AvaloniaProperty.Register<EvalSuite, IReadOnlyList<EvalVersion>?>(nameof(Versions), EvalSuiteSamples.Versions());

    /// <summary>
    /// Every known result, for any case and version (two-way: a run replaces the results of its two versions with a new
    /// list). Default: the sample results of all three versions.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<EvalResult>?> ResultsProperty =
        AvaloniaProperty.Register<EvalSuite, IReadOnlyList<EvalResult>?>(nameof(Results), EvalSuiteSamples.Results(),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The id of the version compared against (two-way; the Baseline choice edits it). Default "v12".</summary>
    public static readonly StyledProperty<string?> BaselineVersionIdProperty =
        AvaloniaProperty.Register<EvalSuite, string?>(nameof(BaselineVersionId), EvalSuiteSamples.BaselineId,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The id of the version under test (two-way; the Candidate choice edits it). Default "v13".</summary>
    public static readonly StyledProperty<string?> CandidateVersionIdProperty =
        AvaloniaProperty.Register<EvalSuite, string?>(nameof(CandidateVersionId), EvalSuiteSamples.CandidateId,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Which cases the list shows (two-way; the filter pills edit it).</summary>
    public static readonly StyledProperty<EvalCaseFilter> FilterProperty =
        AvaloniaProperty.Register<EvalSuite, EvalCaseFilter>(nameof(Filter), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The id of the case open below the list (two-way); null closes it.</summary>
    public static readonly StyledProperty<string?> SelectedCaseIdProperty =
        AvaloniaProperty.Register<EvalSuite, string?>(nameof(SelectedCaseId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How the open case compares the two outputs (two-way; its switch edits it).</summary>
    public static readonly StyledProperty<EvalDetailMode> DetailModeProperty =
        AvaloniaProperty.Register<EvalSuite, EvalDetailMode>(nameof(DetailMode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many cases run at the same time (default 2; each case runs both versions at once).</summary>
    public static readonly StyledProperty<int> MaxConcurrencyProperty =
        AvaloniaProperty.Register<EvalSuite, int>(nameof(MaxConcurrency), 2);

    /// <summary>Culture of every count, rate, score and time (default "en-US").</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<EvalSuite, string>(nameof(CultureName), "en-US");

    /// <summary>The built-in text; replace the object to reword or localize the suite.</summary>
    public static readonly StyledProperty<EvalSuiteLabels> LabelsProperty =
        AvaloniaProperty.Register<EvalSuite, EvalSuiteLabels>(nameof(Labels), EvalSuiteLabels.Default);

    /// <summary>Where the run stands: idle, running, completed or stopped.</summary>
    public static readonly DirectProperty<EvalSuite, EvalRunPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<EvalSuite, EvalRunPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The comparison of the baseline and the candidate over the current results.</summary>
    public static readonly DirectProperty<EvalSuite, EvalSummary> SummaryProperty =
        AvaloniaProperty.RegisterDirect<EvalSuite, EvalSummary>(nameof(Summary), o => o.Summary);

    /// <summary>How many cases of the current or last run have finished.</summary>
    public static readonly DirectProperty<EvalSuite, int> FinishedCountProperty =
        AvaloniaProperty.RegisterDirect<EvalSuite, int>(nameof(FinishedCount), o => o.FinishedCount);

    /// <summary>How many cases the current or last run had.</summary>
    public static readonly DirectProperty<EvalSuite, int> RunTotalProperty =
        AvaloniaProperty.RegisterDirect<EvalSuite, int>(nameof(RunTotal), o => o.RunTotal);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<EvalSuite, double>(nameof(CompactWidth), 620);

    /// <summary>Raised when a run starts, with the two versions and the cases.</summary>
    public static readonly RoutedEvent<EvalSuiteRunStartedEventArgs> RunStartedEvent =
        RoutedEvent.Register<EvalSuite, EvalSuiteRunStartedEventArgs>(nameof(RunStarted), RoutingStrategies.Bubble);

    /// <summary>Raised when a case has finished on both versions, with its results and status.</summary>
    public static readonly RoutedEvent<EvalSuiteCaseCompletedEventArgs> CaseCompletedEvent =
        RoutedEvent.Register<EvalSuite, EvalSuiteCaseCompletedEventArgs>(nameof(CaseCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised when a run ends, finished or stopped, with the summary after it.</summary>
    public static readonly RoutedEvent<EvalSuiteRunCompletedEventArgs> RunCompletedEvent =
        RoutedEvent.Register<EvalSuite, EvalSuiteRunCompletedEventArgs>(nameof(RunCompleted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="CasesProperty"/>
    public IReadOnlyList<EvalCase>? Cases { get => GetValue(CasesProperty); set => SetValue(CasesProperty, value); }

    /// <inheritdoc cref="VersionsProperty"/>
    public IReadOnlyList<EvalVersion>? Versions { get => GetValue(VersionsProperty); set => SetValue(VersionsProperty, value); }

    /// <inheritdoc cref="ResultsProperty"/>
    public IReadOnlyList<EvalResult>? Results { get => GetValue(ResultsProperty); set => SetValue(ResultsProperty, value); }

    /// <inheritdoc cref="BaselineVersionIdProperty"/>
    public string? BaselineVersionId { get => GetValue(BaselineVersionIdProperty); set => SetValue(BaselineVersionIdProperty, value); }

    /// <inheritdoc cref="CandidateVersionIdProperty"/>
    public string? CandidateVersionId { get => GetValue(CandidateVersionIdProperty); set => SetValue(CandidateVersionIdProperty, value); }

    /// <inheritdoc cref="FilterProperty"/>
    public EvalCaseFilter Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <inheritdoc cref="SelectedCaseIdProperty"/>
    public string? SelectedCaseId { get => GetValue(SelectedCaseIdProperty); set => SetValue(SelectedCaseIdProperty, value); }

    /// <inheritdoc cref="DetailModeProperty"/>
    public EvalDetailMode DetailMode { get => GetValue(DetailModeProperty); set => SetValue(DetailModeProperty, value); }

    /// <inheritdoc cref="MaxConcurrencyProperty"/>
    public int MaxConcurrency { get => GetValue(MaxConcurrencyProperty); set => SetValue(MaxConcurrencyProperty, value); }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public EvalSuiteLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public EvalRunPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <inheritdoc cref="SummaryProperty"/>
    public EvalSummary Summary { get => _summary; private set => SetAndRaise(SummaryProperty, ref _summary, value); }

    /// <inheritdoc cref="FinishedCountProperty"/>
    public int FinishedCount { get => _finished; private set => SetAndRaise(FinishedCountProperty, ref _finished, value); }

    /// <inheritdoc cref="RunTotalProperty"/>
    public int RunTotal { get => _runTotal; private set => SetAndRaise(RunTotalProperty, ref _runTotal, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <summary>
    /// Runs one case on one version: return the graded result (see <see cref="Grade"/>). The token is cancelled when the
    /// run is stopped or the suite leaves the visual tree; an exception other than a cancellation becomes an error result
    /// with its message. Null (the default) simulates each case from <see cref="EvalSuiteSamples"/>.
    /// </summary>
    public Func<EvalCase, EvalVersion, CancellationToken, Task<EvalResult>>? RunCaseAsync { get; set; }

    /// <summary>Whether runs are simulated (no <see cref="RunCaseAsync"/>).</summary>
    public bool IsSimulated => RunCaseAsync is null;

    /// <summary>The waits of a simulated run; tests replace it to step time.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (time, token) => Task.Delay(time, token);

    /// <inheritdoc cref="RunStartedEvent"/>
    public event EventHandler<EvalSuiteRunStartedEventArgs>? RunStarted
    {
        add => AddHandler(RunStartedEvent, value);
        remove => RemoveHandler(RunStartedEvent, value);
    }

    /// <inheritdoc cref="CaseCompletedEvent"/>
    public event EventHandler<EvalSuiteCaseCompletedEventArgs>? CaseCompleted
    {
        add => AddHandler(CaseCompletedEvent, value);
        remove => RemoveHandler(CaseCompletedEvent, value);
    }

    /// <inheritdoc cref="RunCompletedEvent"/>
    public event EventHandler<EvalSuiteRunCompletedEventArgs>? RunCompleted
    {
        add => AddHandler(RunCompletedEvent, value);
        remove => RemoveHandler(RunCompletedEvent, value);
    }

    #endregion

    private EvalRunPhase _phase;
    private EvalSummary _summary;
    private int _finished;
    private int _runTotal;

    /// <summary>
    /// Grades an output with a case's check and returns the result for <paramref name="versionId"/>: exact text, contained
    /// text and patterns are checked here; a rubric passes when <paramref name="score"/> reaches the case's threshold. The
    /// reason uses <paramref name="labels"/> (default English).
    /// </summary>
    /// <param name="evalCase">The case.</param>
    /// <param name="versionId">The version that produced the output.</param>
    /// <param name="output">The output.</param>
    /// <param name="score">The grader's score from 0 to 1, for rubric cases.</param>
    /// <param name="duration">How long the case took.</param>
    /// <param name="labels">The words of the reason; null uses the defaults.</param>
    public static EvalResult Grade(EvalCase evalCase, string versionId, string output, double? score = null,
        TimeSpan duration = default, EvalSuiteLabels? labels = null)
    {
        ArgumentNullException.ThrowIfNull(evalCase);
        return EvalSuiteGrader.Grade(evalCase, versionId, output, score, duration, labels ?? EvalSuiteLabels.Default,
            CultureInfo.InvariantCulture);
    }

    /// <summary>How a case moved between two results: passing, failing, a regression, a fix, or not run when one is missing.</summary>
    /// <param name="baseline">The baseline's result.</param>
    /// <param name="candidate">The candidate's result.</param>
    public static EvalCaseStatus Compare(EvalResult? baseline, EvalResult? candidate) => EvalSuiteGrader.Compare(baseline, candidate);

    private CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(CultureName) ? "en-US" : CultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("en-US");
            }
        }
    }

    /// <summary>Creates a suite with the sample data.</summary>
    public EvalSuite()
    {
        Rebuild();
    }
}
