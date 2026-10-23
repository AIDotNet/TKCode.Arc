using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The arithmetic and formats of an <see cref="AgentPlan"/> (measured on the reference, notes 2b.4): step and total times,
/// the run position, the finished count and the progress fraction.
/// </summary>
internal static class AgentPlanFormat
{
    /// <summary>"23s" under a minute; "1m 07s" from a minute on (two-digit seconds); whole seconds, rounded.</summary>
    public static string Duration(TimeSpan duration)
    {
        var total = AiDuration.WholeSecondsRounded(duration);
        return total < 60 ? AiDuration.Seconds(total) : AiDuration.MinutesSeconds(total, padSeconds: true);
    }

    /// <summary>The total time: the sum of the done steps' durations (skipped and failed steps do not count).</summary>
    public static TimeSpan Total(IEnumerable<AgentPlanStep> steps)
    {
        var total = TimeSpan.Zero;
        foreach (var step in steps)
        {
            if (step.State == AgentPlanStepState.Done && step.Duration is { } duration)
            {
                total += duration;
            }
        }
        return total;
    }

    /// <summary>Steps that are done or skipped.</summary>
    public static int Finished(IReadOnlyList<AgentPlanStep> steps)
    {
        var count = 0;
        foreach (var step in steps)
        {
            if (step.State is AgentPlanStepState.Done or AgentPlanStepState.Skipped)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Done and skipped steps over all steps (0 for an empty plan).</summary>
    public static double Progress(IReadOnlyList<AgentPlanStep> steps) =>
        steps.Count == 0 ? 0 : Finished(steps) / (double)steps.Count;

    /// <summary>
    /// The 1-based position the run is at: the first step that is not done or skipped (the active or failed step, or the
    /// next one to start); the count once every step is finished; 1 for an empty plan.
    /// </summary>
    public static int Position(IReadOnlyList<AgentPlanStep> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].State is not (AgentPlanStepState.Done or AgentPlanStepState.Skipped))
            {
                return i + 1;
            }
        }
        return Math.Max(1, steps.Count);
    }

    /// <summary>The index of the first failed step, or -1.</summary>
    public static int FailedIndex(IReadOnlyList<AgentPlanStep> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].State == AgentPlanStepState.Failed)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Splits <paramref name="text"/> around the first occurrence of <paramref name="number"/> written in the current
    /// culture, so the number can roll while the words stay; Number is -1 when it does not occur.
    /// </summary>
    public static (string Before, int Number, string After) SplitOne(string text, int number)
    {
        var digits = number.ToString(CultureInfo.CurrentCulture);
        var at = text.IndexOf(digits, StringComparison.Ordinal);
        return at < 0 ? (text, -1, string.Empty) : (text[..at], number, text[(at + digits.Length)..]);
    }

    private const int FirstSentinel = 7_000_001;
    private const int SecondSentinel = 8_000_002;

    /// <summary>
    /// Splits a two-number text such as "3 of 7" into its words and numbers (in the order the format writes them), by
    /// formatting sentinel numbers. Null when the format does not write both numbers plainly.
    /// </summary>
    public static (string[] Words, bool FirstComesFirst)? SplitTwo(Func<int, int, string> format)
    {
        var probe = format(FirstSentinel, SecondSentinel);
        var a = FirstSentinel.ToString(CultureInfo.CurrentCulture);
        var b = SecondSentinel.ToString(CultureInfo.CurrentCulture);
        var ia = probe.IndexOf(a, StringComparison.Ordinal);
        var ib = probe.IndexOf(b, StringComparison.Ordinal);
        if (ia < 0 || ib < 0)
        {
            return null;
        }
        var firstFirst = ia < ib;
        var (i1, l1, i2, l2) = firstFirst ? (ia, a.Length, ib, b.Length) : (ib, b.Length, ia, a.Length);
        return ([probe[..i1], probe[(i1 + l1)..i2], probe[(i2 + l2)..]], firstFirst);
    }
}
