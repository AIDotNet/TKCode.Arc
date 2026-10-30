using System.Globalization;

namespace TKCode.Arc;

/// <summary>Pure helpers of <see cref="AiWorkflow"/>: durations, the menu filter, new ids, and adding or removing steps.</summary>
internal static class AiWorkflowFormat
{
    /// <summary>"700ms" under a second, "1.2s" under a minute, then "1m 05s".</summary>
    public static string Duration(TimeSpan span)
    {
        var ms = AiDuration.WholeMilliseconds(span);
        if (ms < 1000)
        {
            return AiDuration.Milliseconds(ms);
        }
        if (ms < 60_000)
        {
            return AiDuration.Tenths(AiDuration.TenthsOfMilliseconds(ms));
        }
        return AiDuration.MinutesSeconds(AiDuration.WholeSecondsRounded(span), padSeconds: true);
    }

    /// <summary>The confidence in monospace: "0.94".</summary>
    public static string Confidence(double value) =>
        Math.Clamp(value, 0, 1).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// The add step menu's entries for <paramref name="query"/>: every non-trigger entry whose title, app name or detail
    /// contains each word of the query (case does not matter), in catalog order. A branch is left out while the flow
    /// already has one.
    /// </summary>
    public static List<AiWorkflowCatalogItem> Filter(IEnumerable<AiWorkflowCatalogItem> catalog, string? query, bool allowsBranch)
    {
        var words = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<AiWorkflowCatalogItem>();
        foreach (var item in catalog)
        {
            if (item.Kind == AiWorkflowStepKind.Trigger || (item.Kind == AiWorkflowStepKind.Branch && !allowsBranch))
            {
                continue;
            }
            var haystack = item.Title + " " + item.App?.Name + " " + item.Detail;
            var matches = true;
            foreach (var word in words)
            {
                if (haystack.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    matches = false;
                    break;
                }
            }
            if (matches)
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>The triggers of the catalog (the empty state's rows).</summary>
    public static List<AiWorkflowCatalogItem> Triggers(IEnumerable<AiWorkflowCatalogItem> catalog) =>
        [.. catalog.Where(i => i.Kind == AiWorkflowStepKind.Trigger)];

    /// <summary>Every step of the flow, branch paths included, in order.</summary>
    public static IEnumerable<AiWorkflowStep> All(IEnumerable<AiWorkflowStep> steps)
    {
        foreach (var step in steps)
        {
            yield return step;
            foreach (var path in step.Paths)
            {
                foreach (var inner in All(path.Steps))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>The flow's branch (the last top-level step when it is one).</summary>
    public static AiWorkflowStep? Branch(IReadOnlyList<AiWorkflowStep> steps) =>
        steps.Count > 0 && steps[^1].Kind == AiWorkflowStepKind.Branch ? steps[^1] : null;

    /// <summary>An id not used yet: the kind plus a number ("action-4").</summary>
    public static string NewId(IEnumerable<AiWorkflowStep> steps, AiWorkflowStepKind kind)
    {
        var used = new HashSet<string>(All(steps).Select(s => s.Id), StringComparer.Ordinal);
        var stem = kind.ToString().ToLowerInvariant();
        for (var n = 1; ; n++)
        {
            var id = stem + "-" + n.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(id))
            {
                return id;
            }
        }
    }

    /// <summary>
    /// A copy of <paramref name="steps"/> with <paramref name="step"/> added: a trigger goes first, anything else at the end
    /// of the flow, or at the end of the visible path (<paramref name="pathId"/>) when the flow ends in a branch.
    /// </summary>
    public static List<AiWorkflowStep> Add(IReadOnlyList<AiWorkflowStep> steps, AiWorkflowStep step, string? pathId)
    {
        var copy = steps.Select(s => s.Clone()).ToList();
        if (step.Kind == AiWorkflowStepKind.Trigger)
        {
            copy.Insert(0, step);
            return copy;
        }
        if (Branch(copy) is { } branch && step.Kind != AiWorkflowStepKind.Branch && branch.Paths.Count > 0)
        {
            var path = branch.Paths.FirstOrDefault(p => p.Id == pathId) ?? branch.Paths[0];
            path.Steps.Add(step);
            return copy;
        }
        copy.Add(step);
        return copy;
    }

    /// <summary>A copy of <paramref name="steps"/> without the step with <paramref name="id"/> (wherever it is).</summary>
    public static List<AiWorkflowStep> Remove(IReadOnlyList<AiWorkflowStep> steps, string id)
    {
        var copy = steps.Select(s => s.Clone()).ToList();
        RemoveFrom(copy, id);
        return copy;
    }

    private static bool RemoveFrom(List<AiWorkflowStep> steps, string id)
    {
        var index = steps.FindIndex(s => s.Id == id);
        if (index >= 0)
        {
            steps.RemoveAt(index);
            return true;
        }
        foreach (var step in steps)
        {
            foreach (var path in step.Paths)
            {
                if (RemoveFrom(path.Steps, id))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// The step a run goes to after <paramref name="current"/>: the next top-level step, or after a branch the first step
    /// of <paramref name="routePathId"/>, then the rest of that path. Null at the end.
    /// </summary>
    public static AiWorkflowStep? Next(IReadOnlyList<AiWorkflowStep> steps, AiWorkflowStep current, string? routePathId)
    {
        var index = IndexOf(steps, current.Id);
        if (index >= 0)
        {
            if (current.Kind == AiWorkflowStepKind.Branch)
            {
                var path = current.Paths.FirstOrDefault(p => p.Id == routePathId);
                return path is { Steps.Count: > 0 } ? path.Steps[0] : null;
            }
            return index + 1 < steps.Count ? steps[index + 1] : null;
        }
        if (Branch(steps) is { } branch)
        {
            foreach (var path in branch.Paths)
            {
                var at = IndexOf(path.Steps, current.Id);
                if (at >= 0)
                {
                    return at + 1 < path.Steps.Count ? path.Steps[at + 1] : null;
                }
            }
        }
        return null;
    }

    private static int IndexOf(IReadOnlyList<AiWorkflowStep> steps, string id)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }
}
