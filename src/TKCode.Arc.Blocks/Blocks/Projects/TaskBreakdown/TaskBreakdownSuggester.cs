using System.Globalization;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>
/// The built-in suggester of <see cref="TaskBreakdown"/>: splits a description into steps (lines, bullets, sentences and
/// "then"), turns each into a short subtask, estimates it from the kind of work its words name, adds a review step when
/// none is mentioned, and spreads the work over the people by load. Deterministic and offline; give the block your own
/// <see cref="TaskBreakdown.SuggestAction"/> to call a model or a service instead.
/// </summary>
internal static partial class TaskBreakdownSuggester
{
    private static readonly (string[] Words, double Hours)[] s_estimates =
    [
        (["research", "investigate", "explore", "spike", "interview"], 3),
        (["design", "mockup", "wireframe", "prototype", "sketch"], 4),
        (["build", "implement", "develop", "add", "create", "integrate", "connect", "wire"], 6),
        (["migrate", "move", "port", "rewrite", "refactor"], 5),
        (["test", "qa", "verify", "check", "validate"], 3),
        (["write", "document", "docs", "copy", "announce", "email"], 2),
        (["review", "approve", "sign off", "feedback"], 1),
        (["deploy", "release", "ship", "launch", "roll out"], 2),
        (["fix", "bug", "patch"], 2),
    ];

    [GeneratedRegex(@"\r?\n|(?<=[.;!?])\s+|\s+then\s+", RegexOptions.IgnoreCase)]
    private static partial Regex StepSplitter();

    [GeneratedRegex(@"^\s*(?:[-*•]|\d+[.)])\s*")]
    private static partial Regex Bullet();

    public static IReadOnlyList<TaskBreakdownSubtask> Suggest(TaskBreakdownRequest request)
    {
        var steps = StepSplitter().Split(request.Description ?? string.Empty)
            .Select(s => Bullet().Replace(s, string.Empty).Trim().TrimEnd('.', ';', ',', '!'))
            .Where(s => s.Length >= 4)
            .Select(Tidy)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Take(12)
            .ToList();
        var title = string.IsNullOrWhiteSpace(request.Title) ? "the task" : request.Title.Trim();
        if (steps.Count == 0)
        {
            var lower = char.ToLower(title[0], CultureInfo.CurrentCulture) + title[1..];
            steps = ["Agree on the scope of " + lower, "Design the approach", "Build " + lower, "Test " + lower];
        }
        if (!steps.Any(s => Mentions(s, ["review", "approve", "sign off", "feedback"])))
        {
            steps.Add("Review and hand off");
        }

        var result = new List<TaskBreakdownSubtask>();
        var load = request.People.ToDictionary(p => p, _ => 0.0);
        foreach (var step in steps)
        {
            var hours = Estimate(step);
            var assignee = load.Count == 0 ? null : load.OrderBy(kv => kv.Value).First().Key;
            if (assignee is not null)
            {
                load[assignee] += hours;
            }
            result.Add(new TaskBreakdownSubtask(step, hours, assignee) { IsSuggested = true });
        }
        return result;
    }

    public static double Estimate(string step)
    {
        foreach (var (words, hours) in s_estimates)
        {
            if (Mentions(step, words))
            {
                // Long steps tend to hide more work.
                return step.Length > 60 ? hours + 2 : hours;
            }
        }
        return 3;
    }

    private static bool Mentions(string text, string[] words)
    {
        foreach (var word in words)
        {
            var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (at >= 0 && (at == 0 || !char.IsLetter(text[at - 1])))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Capitalizes, drops "we need to" style openings and cuts a step to about 80 characters at a word.</summary>
    private static string Tidy(string step)
    {
        foreach (var opening in new[] { "we need to ", "we should ", "need to ", "we'll ", "we will ", "please ", "and ", "also " })
        {
            if (step.StartsWith(opening, StringComparison.OrdinalIgnoreCase))
            {
                step = step[opening.Length..];
            }
        }
        if (step.Length > 80)
        {
            var cut = step.LastIndexOf(' ', 79);
            step = (cut > 40 ? step[..cut] : step[..79]) + "…";
        }
        return step.Length == 0 ? step : char.ToUpper(step[0], CultureInfo.CurrentCulture) + step[1..];
    }
}
