using System.Globalization;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>The checks, the comparison of two results and the summary of a suite (pure functions, no UI).</summary>
internal static class EvalSuiteGrader
{
    private static readonly TimeSpan s_regexTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>Applies <paramref name="evalCase"/>'s check to <paramref name="output"/>.</summary>
    public static EvalResult Grade(EvalCase evalCase, string versionId, string output, double? score, TimeSpan duration,
        EvalSuiteLabels labels, CultureInfo culture)
    {
        output ??= string.Empty;
        var expected = evalCase.Expected ?? string.Empty;
        bool passed;
        string detail;
        switch (evalCase.Check)
        {
            case EvalCheckKind.Exact:
                passed = string.Equals(output.Trim(), expected.Trim(), StringComparison.Ordinal);
                detail = passed ? labels.ReasonExactPass : labels.ReasonExactFail;
                break;
            case EvalCheckKind.Regex:
                (passed, detail) = MatchPattern(output, expected, labels);
                break;
            case EvalCheckKind.Rubric:
                var threshold = Math.Clamp(evalCase.Threshold, 0, 1);
                if (score is { } s && !double.IsNaN(s))
                {
                    s = Math.Clamp(s, 0, 1);
                    score = s;
                    passed = s >= threshold - 1e-9;
                    detail = string.Format(culture, labels.ReasonRubricFormat, s.ToString("0.00", culture), threshold.ToString("0.00", culture));
                }
                else
                {
                    passed = false;
                    detail = labels.ReasonRubricMissing;
                }
                break;
            default:
                passed = expected.Length > 0 && output.Contains(expected, StringComparison.OrdinalIgnoreCase);
                detail = string.Format(culture, passed ? labels.ReasonContainsPassFormat : labels.ReasonContainsFailFormat, expected);
                break;
        }
        return new EvalResult
        {
            CaseId = evalCase.Id,
            VersionId = versionId,
            Output = output,
            Passed = passed,
            Score = evalCase.Check == EvalCheckKind.Rubric ? score : null,
            Detail = detail,
            Duration = duration
        };
    }

    private static (bool, string) MatchPattern(string output, string pattern, EvalSuiteLabels labels)
    {
        try
        {
            var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, s_regexTimeout);
            return match.Success
                ? (true, string.Format(CultureInfo.InvariantCulture, labels.ReasonRegexPassFormat, match.Value))
                : (false, labels.ReasonRegexFail);
        }
        catch (ArgumentException)
        {
            return (false, labels.ReasonRegexInvalid);
        }
        catch (RegexMatchTimeoutException)
        {
            return (false, labels.ReasonRegexFail);
        }
    }

    /// <summary>The status of a case from its two results (either may be missing).</summary>
    public static EvalCaseStatus Compare(EvalResult? baseline, EvalResult? candidate)
    {
        if (baseline is null || candidate is null)
        {
            return EvalCaseStatus.NotRun;
        }
        return (baseline.Passed, candidate.Passed) switch
        {
            (true, true) => EvalCaseStatus.Passing,
            (false, false) => EvalCaseStatus.Failing,
            (true, false) => EvalCaseStatus.Regression,
            _ => EvalCaseStatus.Fixed
        };
    }

    /// <summary>Whether a status belongs under a filter.</summary>
    public static bool Matches(EvalCaseFilter filter, EvalCaseStatus status) => filter switch
    {
        EvalCaseFilter.Regressions => status == EvalCaseStatus.Regression,
        EvalCaseFilter.Fixed => status == EvalCaseStatus.Fixed,
        EvalCaseFilter.Failing => status is EvalCaseStatus.Regression or EvalCaseStatus.Failing,
        _ => true
    };

    /// <summary>Counts the statuses of every case into a summary.</summary>
    public static EvalSummary Summarize(int total, IEnumerable<(EvalResult? Baseline, EvalResult? Candidate)> pairs)
    {
        int compared = 0, basePassed = 0, candPassed = 0, regressions = 0, fixes = 0, failing = 0, passing = 0;
        foreach (var (b, c) in pairs)
        {
            if (b is null || c is null)
            {
                continue;
            }
            compared++;
            if (b.Passed)
            {
                basePassed++;
            }
            if (c.Passed)
            {
                candPassed++;
            }
            switch (Compare(b, c))
            {
                case EvalCaseStatus.Regression:
                    regressions++;
                    break;
                case EvalCaseStatus.Fixed:
                    fixes++;
                    break;
                case EvalCaseStatus.Failing:
                    failing++;
                    break;
                default:
                    passing++;
                    break;
            }
        }
        return new EvalSummary(total, compared, basePassed, candPassed, regressions, fixes, failing, passing);
    }
}
