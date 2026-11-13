using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="EvalSuite"/>. Replace the whole object to reword or localize the suite. Entries with
/// <c>{0}</c>, <c>{1}</c> and <c>{2}</c> receive already formatted values (numbers use <see cref="EvalSuite.CultureName"/>).
/// </summary>
public sealed class EvalSuiteLabels
{
    /// <summary>The suite's accessible name: "Evaluation suite".</summary>
    public string SuiteName { get; init; } = "Evaluation suite";

    /// <summary>Under the title, with the case count: "{0} cases".</summary>
    public string CaseCountFormat { get; init; } = "{0} cases";

    /// <summary>Label of the baseline choice: "Baseline".</summary>
    public string Baseline { get; init; } = "Baseline";

    /// <summary>Label of the candidate choice: "Candidate".</summary>
    public string Candidate { get; init; } = "Candidate";

    /// <summary>A version in the choices, with its name and note: "{0} · {1}".</summary>
    public string VersionWithNoteFormat { get; init; } = "{0} · {1}";

    /// <summary>The run button at rest: "Run suite".</summary>
    public string Run { get; init; } = "Run suite";

    /// <summary>The run button while running: "Stop".</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The status line before any result exists: "Not run yet".</summary>
    public string StatusIdle { get; init; } = "Not run yet";

    /// <summary>The status line with results that came with the suite, with the compared count: "{0} cases compared".</summary>
    public string StatusLoadedFormat { get; init; } = "{0} cases compared";

    /// <summary>The status line while running, with the finished and the total count: "Running {0} of {1}".</summary>
    public string StatusRunningFormat { get; init; } = "Running {0} of {1}";

    /// <summary>The status line after a complete run, with the count and the time: "Ran {0} cases in {1}".</summary>
    public string StatusDoneFormat { get; init; } = "Ran {0} cases in {1}";

    /// <summary>The status line after a stopped run, with the finished and the total count: "Stopped after {0} of {1}".</summary>
    public string StatusStoppedFormat { get; init; } = "Stopped after {0} of {1}";

    /// <summary>A time in seconds, with the formatted number: "{0} s".</summary>
    public string SecondsFormat { get; init; } = "{0} s";

    /// <summary>The subtitle, with the case count and the status line: "{0} · {1}".</summary>
    public string SubtitleFormat { get; init; } = "{0} · {1}";

    /// <summary>A result's reason line, with the reason and the time: "{0} · {1}".</summary>
    public string ReasonLineFormat { get; init; } = "{0} · {1}";

    /// <summary>The progress bar's accessible name: "Run progress".</summary>
    public string ProgressName { get; init; } = "Run progress";

    /// <summary>A pass rate tile's caption, with the version name: "{0} pass rate".</summary>
    public string RateCaptionFormat { get; init; } = "{0} pass rate";

    /// <summary>Under a pass rate, with the passed and the compared count: "{0} of {1} passed".</summary>
    public string PassedOfFormat { get; init; } = "{0} of {1} passed";

    /// <summary>The change tile's caption: "Change".</summary>
    public string DeltaCaption { get; init; } = "Change";

    /// <summary>After the change figure: " pts".</summary>
    public string DeltaUnit { get; init; } = " pts";

    /// <summary>Under the change, with the regressions and the fixes: "{0} regressions · {1} fixed".</summary>
    public string DeltaNoteFormat { get; init; } = "{0} regressions · {1} fixed";

    /// <summary>The change tile's accessible text, with the signed points: "Change: {0} points".</summary>
    public string DeltaNameFormat { get; init; } = "Change: {0} points";

    /// <summary>A pass rate tile's accessible text, with the version, the percent, the passed and the compared count: "{0}: {1}% pass rate, {2} of {3} cases".</summary>
    public string RateNameFormat { get; init; } = "{0}: {1}% pass rate, {2} of {3} cases";

    /// <summary>The filter group's accessible name: "Filter cases".</summary>
    public string FilterName { get; init; } = "Filter cases";

    /// <summary>Filter pill, with the count: "All {0}".</summary>
    public string FilterAllFormat { get; init; } = "All {0}";

    /// <summary>Filter pill, with the count: "Regressions {0}".</summary>
    public string FilterRegressionsFormat { get; init; } = "Regressions {0}";

    /// <summary>Filter pill, with the count: "Fixed {0}".</summary>
    public string FilterFixedFormat { get; init; } = "Fixed {0}";

    /// <summary>Filter pill, with the count: "Failing {0}".</summary>
    public string FilterFailingFormat { get; init; } = "Failing {0}";

    /// <summary>Column title: "Status".</summary>
    public string ColumnStatus { get; init; } = "Status";

    /// <summary>Column title: "Case".</summary>
    public string ColumnCase { get; init; } = "Case";

    /// <summary>Column title: "Check".</summary>
    public string ColumnCheck { get; init; } = "Check";

    /// <summary>The list's accessible name, with the shown count: "Cases, {0} shown".</summary>
    public string ListNameFormat { get; init; } = "Cases, {0} shown";

    /// <summary>
    /// A row's accessible text, with the position ("3 of 14"), the title, the status, the baseline verdict and the
    /// candidate verdict: "{0}: {1}, {2}. Baseline {3}, candidate {4}".
    /// </summary>
    public string RowNameFormat { get; init; } = "{0}: {1}, {2}. Baseline {3}, candidate {4}";

    /// <summary>A row's position, with its index and the count: "{0} of {1}".</summary>
    public string RowPositionFormat { get; init; } = "{0} of {1}";

    /// <summary>The list when the filter matches nothing, with the filter's name: "No {0} cases.".</summary>
    public string EmptyFilterFormat { get; init; } = "No {0} cases.";

    /// <summary>The list without any case: "This suite has no cases yet.".</summary>
    public string EmptySuite { get; init; } = "This suite has no cases yet.";

    /// <summary>Status names (chips, accessible text).</summary>
    public string NotRun { get; init; } = "Not run";

    /// <summary>Status name: "Queued".</summary>
    public string Queued { get; init; } = "Queued";

    /// <summary>Status name: "Running".</summary>
    public string Running { get; init; } = "Running";

    /// <summary>Status name: "Passing".</summary>
    public string Passing { get; init; } = "Passing";

    /// <summary>Status name: "Failing".</summary>
    public string Failing { get; init; } = "Failing";

    /// <summary>Status name: "Regression".</summary>
    public string Regression { get; init; } = "Regression";

    /// <summary>Status name: "Fixed".</summary>
    public string Fixed { get; init; } = "Fixed";

    /// <summary>A verdict: "Pass".</summary>
    public string Pass { get; init; } = "Pass";

    /// <summary>A verdict: "Fail".</summary>
    public string Fail { get; init; } = "Fail";

    /// <summary>A verdict when the case threw: "Error".</summary>
    public string Error { get; init; } = "Error";

    /// <summary>A verdict without a result: "No result".</summary>
    public string NoResult { get; init; } = "No result";

    /// <summary>Check names: "Exact".</summary>
    public string CheckExact { get; init; } = "Exact";

    /// <summary>Check name: "Contains".</summary>
    public string CheckContains { get; init; } = "Contains";

    /// <summary>Check name: "Pattern".</summary>
    public string CheckRegex { get; init; } = "Pattern";

    /// <summary>Check name: "Rubric".</summary>
    public string CheckRubric { get; init; } = "Rubric";

    /// <summary>The open case's label above the input: "Input".</summary>
    public string InputTitle { get; init; } = "Input";

    /// <summary>The open case's check line, with the check name and the expected text: "{0}: {1}".</summary>
    public string CheckLineFormat { get; init; } = "{0}: {1}";

    /// <summary>The rubric check line, with the check name, the rubric and the threshold: "{0} (pass at {2}): {1}".</summary>
    public string RubricLineFormat { get; init; } = "{0} (pass at {2}): {1}";

    /// <summary>The detail view switch: "Side by side".</summary>
    public string SideBySide { get; init; } = "Side by side";

    /// <summary>The detail view switch: "Word diff".</summary>
    public string Diff { get; init; } = "Word diff";

    /// <summary>The detail view switch's accessible name: "Compare outputs".</summary>
    public string DetailModeName { get; init; } = "Compare outputs";

    /// <summary>The open case's close button: "Close case".</summary>
    public string CloseCase { get; init; } = "Close case";

    /// <summary>An output column's title, with the role and the version name: "{0} · {1}".</summary>
    public string OutputTitleFormat { get; init; } = "{0} · {1}";

    /// <summary>The word diff's summary, with the added and the removed word counts: "{0} words added, {1} removed".</summary>
    public string DiffSummaryFormat { get; init; } = "{0} words added, {1} removed";

    /// <summary>An output that does not exist yet: "Run the suite to see this output.".</summary>
    public string NoOutput { get; init; } = "Run the suite to see this output.";

    /// <summary>The grader's reason, exact match: "Matches the expected text".</summary>
    public string ReasonExactPass { get; init; } = "Matches the expected text";

    /// <summary>The grader's reason, exact mismatch: "Differs from the expected text".</summary>
    public string ReasonExactFail { get; init; } = "Differs from the expected text";

    /// <summary>The grader's reason, with the text: "Contains “{0}”".</summary>
    public string ReasonContainsPassFormat { get; init; } = "Contains “{0}”";

    /// <summary>The grader's reason, with the text: "Missing “{0}”".</summary>
    public string ReasonContainsFailFormat { get; init; } = "Missing “{0}”";

    /// <summary>The grader's reason, with the matched text: "Matches the pattern at “{0}”".</summary>
    public string ReasonRegexPassFormat { get; init; } = "Matches the pattern at “{0}”";

    /// <summary>The grader's reason: "No match for the pattern".</summary>
    public string ReasonRegexFail { get; init; } = "No match for the pattern";

    /// <summary>The grader's reason: "The pattern is not valid".</summary>
    public string ReasonRegexInvalid { get; init; } = "The pattern is not valid";

    /// <summary>The grader's reason, with the score and the threshold: "Scored {0}, needs {1}".</summary>
    public string ReasonRubricFormat { get; init; } = "Scored {0}, needs {1}";

    /// <summary>The grader's reason without a score: "No score from the grader".</summary>
    public string ReasonRubricMissing { get; init; } = "No score from the grader";

    /// <summary>Announced when a run starts, with the count and the two version names: "Running {0} cases on {1} and {2}".</summary>
    public string AnnounceStartFormat { get; init; } = "Running {0} cases on {1} and {2}";

    /// <summary>
    /// Announced when a run ends, with the candidate's name, its passed and compared counts, the signed points and the
    /// regressions: "Run complete. {0} passed {1} of {2}, {3} points. {4} regressions.".
    /// </summary>
    public string AnnounceDoneFormat { get; init; } = "Run complete. {0} passed {1} of {2}, {3} points. {4} regressions.";

    /// <summary>Announced when a run is stopped, with the finished and the total count: "Run stopped after {0} of {1} cases.".</summary>
    public string AnnounceStoppedFormat { get; init; } = "Run stopped after {0} of {1} cases.";

    /// <summary>The default labels.</summary>
    public static EvalSuiteLabels Default { get; } = new();

    /// <summary>The name of a status.</summary>
    /// <param name="status">The status.</param>
    public string StatusName(EvalCaseStatus status) => status switch
    {
        EvalCaseStatus.Queued => Queued,
        EvalCaseStatus.Running => Running,
        EvalCaseStatus.Passing => Passing,
        EvalCaseStatus.Failing => Failing,
        EvalCaseStatus.Regression => Regression,
        EvalCaseStatus.Fixed => Fixed,
        _ => NotRun
    };

    /// <summary>The name of a check.</summary>
    /// <param name="check">The check.</param>
    public string CheckName(EvalCheckKind check) => check switch
    {
        EvalCheckKind.Exact => CheckExact,
        EvalCheckKind.Regex => CheckRegex,
        EvalCheckKind.Rubric => CheckRubric,
        _ => CheckContains
    };

    /// <summary>The verdict of a result: Pass, Fail, Error or No result; a rubric result adds its score ("Pass 0.84").</summary>
    /// <param name="result">The result, or null.</param>
    /// <param name="culture">Formats the score.</param>
    public string Verdict(EvalResult? result, CultureInfo culture)
    {
        if (result is null)
        {
            return NoResult;
        }
        if (result.IsError)
        {
            return Error;
        }
        var word = result.Passed ? Pass : Fail;
        return result.Score is { } score ? word + " " + score.ToString("0.00", culture) : word;
    }
}
