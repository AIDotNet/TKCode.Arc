namespace TKCode.Arc;

/// <summary>How an <see cref="EvalCase"/> decides whether an output passes.</summary>
public enum EvalCheckKind
{
    /// <summary>The output, trimmed, equals <see cref="EvalCase.Expected"/> exactly (case-sensitive).</summary>
    Exact,

    /// <summary>The output contains <see cref="EvalCase.Expected"/>, ignoring case.</summary>
    Contains,

    /// <summary>The output matches the regular expression in <see cref="EvalCase.Expected"/>, ignoring case.</summary>
    Regex,

    /// <summary>A grader's score (0 to 1, from your runner) reaches <see cref="EvalCase.Threshold"/>; <see cref="EvalCase.Expected"/> describes the rubric.</summary>
    Rubric
}

/// <summary>Where a case stands in the comparison of the baseline and the candidate version.</summary>
public enum EvalCaseStatus
{
    /// <summary>One of the two versions has no result for the case yet.</summary>
    NotRun,

    /// <summary>Waiting in the current run.</summary>
    Queued,

    /// <summary>Being run now.</summary>
    Running,

    /// <summary>Both versions pass.</summary>
    Passing,

    /// <summary>Both versions fail.</summary>
    Failing,

    /// <summary>The baseline passes and the candidate fails.</summary>
    Regression,

    /// <summary>The baseline fails and the candidate passes.</summary>
    Fixed
}

/// <summary>Which cases the list shows.</summary>
public enum EvalCaseFilter
{
    /// <summary>Every case.</summary>
    All,

    /// <summary>Cases that passed on the baseline and fail on the candidate.</summary>
    Regressions,

    /// <summary>Cases that failed on the baseline and pass on the candidate.</summary>
    Fixed,

    /// <summary>Cases the candidate fails (regressions and cases that fail on both).</summary>
    Failing
}

/// <summary>How the open case shows the two outputs.</summary>
public enum EvalDetailMode
{
    /// <summary>The baseline and the candidate output in two columns (stacked when narrow).</summary>
    SideBySide,

    /// <summary>One word diff from the baseline output to the candidate output.</summary>
    Diff
}

/// <summary>Where the suite's run stands.</summary>
public enum EvalRunPhase
{
    /// <summary>No run has started since the suite was shown.</summary>
    Idle,

    /// <summary>A run is in progress.</summary>
    Running,

    /// <summary>The last run finished every case.</summary>
    Completed,

    /// <summary>The last run was stopped before every case finished.</summary>
    Stopped
}

/// <summary>
/// One test case of an <see cref="EvalSuite"/>: an input, what a good output must satisfy and how it is checked. Data only;
/// replace the suite's <see cref="EvalSuite.Cases"/> list to change what is shown.
/// </summary>
public sealed class EvalCase
{
    /// <summary>Stable id that links the case to its <see cref="EvalResult"/>s.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>A short name for the list ("Refund window").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The input sent to the prompt or model.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>The expected text, the text to find, the pattern or, for a rubric, what the grader looks for.</summary>
    public string Expected { get; set; } = string.Empty;

    /// <summary>How an output is checked (default <see cref="EvalCheckKind.Contains"/>).</summary>
    public EvalCheckKind Check { get; set; } = EvalCheckKind.Contains;

    /// <summary>The lowest passing rubric score, 0 to 1 (default 0.7); used by <see cref="EvalCheckKind.Rubric"/> only.</summary>
    public double Threshold { get; set; } = 0.7;

    /// <summary>An optional group name ("Policy", "Tone"); shown in the open case.</summary>
    public string? Tag { get; set; }
}

/// <summary>A version that can be evaluated: a prompt revision, a model or a configuration.</summary>
public sealed class EvalVersion
{
    /// <summary>Stable id that links the version to its <see cref="EvalResult"/>s.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name in the version choices and column titles ("v13").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A short note after the name in the choices ("Shorter prompt").</summary>
    public string? Note { get; set; }
}

/// <summary>
/// The outcome of one case on one version: the output and whether it passed. Build one with
/// <see cref="EvalSuite.Grade"/> to apply the case's check.
/// </summary>
public sealed class EvalResult
{
    /// <summary>The <see cref="EvalCase.Id"/>.</summary>
    public string CaseId { get; set; } = string.Empty;

    /// <summary>The <see cref="EvalVersion.Id"/>.</summary>
    public string VersionId { get; set; } = string.Empty;

    /// <summary>What the version answered.</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>Whether the output passed the case's check.</summary>
    public bool Passed { get; set; }

    /// <summary>The grader's score from 0 to 1 (rubric checks); null for the other checks.</summary>
    public double? Score { get; set; }

    /// <summary>Why it passed or failed, in one short sentence ("Missing “30 days”").</summary>
    public string? Detail { get; set; }

    /// <summary>True when running the case threw; <see cref="Detail"/> holds the message.</summary>
    public bool IsError { get; set; }

    /// <summary>How long the case took on this version.</summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>The numbers of a comparison: pass counts and rates of both versions and how the cases moved between them.</summary>
/// <param name="Total">Every case of the suite.</param>
/// <param name="Compared">Cases with a result on both versions; the rates are over these.</param>
/// <param name="BaselinePassed">Compared cases the baseline passes.</param>
/// <param name="CandidatePassed">Compared cases the candidate passes.</param>
/// <param name="Regressions">Cases the baseline passes and the candidate fails.</param>
/// <param name="Fixes">Cases the baseline fails and the candidate passes.</param>
/// <param name="StillFailing">Cases both versions fail.</param>
/// <param name="StillPassing">Cases both versions pass.</param>
public readonly record struct EvalSummary(int Total, int Compared, int BaselinePassed, int CandidatePassed, int Regressions,
    int Fixes, int StillFailing, int StillPassing)
{
    /// <summary>The baseline's pass rate over the compared cases, 0 to 1 (0 when nothing is compared).</summary>
    public double BaselineRate => Compared == 0 ? 0 : (double)BaselinePassed / Compared;

    /// <summary>The candidate's pass rate over the compared cases, 0 to 1.</summary>
    public double CandidateRate => Compared == 0 ? 0 : (double)CandidatePassed / Compared;

    /// <summary>The change from the baseline rate to the candidate rate, in percentage points.</summary>
    public double DeltaPoints => (CandidateRate - BaselineRate) * 100;

    /// <summary>Cases the candidate fails: regressions and cases both versions fail.</summary>
    public int CandidateFailing => Regressions + StillFailing;
}
