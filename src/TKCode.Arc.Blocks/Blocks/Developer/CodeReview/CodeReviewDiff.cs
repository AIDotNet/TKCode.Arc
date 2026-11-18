using System.Globalization;

namespace TKCode.Arc;

/// <summary>A diff line with its derived numbers (0 where the side has none) and, for a hunk, its header text.</summary>
internal readonly record struct CodeReviewRow(CodeReviewDiffLine Line, int OldNumber, int NewNumber, string HunkHeader)
{
    public CodeReviewLineKind Kind => Line.Kind;

    /// <summary>The number a reader uses for the line: the new side, or the old side for a removed line.</summary>
    public int Number => Line.Kind == CodeReviewLineKind.Deleted ? OldNumber : NewNumber;
}

/// <summary>
/// The rules of a file's diff: line numbers follow from the hunk headers, a hunk header reads "@@ −a,b +c,d @@" from its
/// lines, and applying a suggestion swaps added lines and turns unchanged ones into removals followed by the new lines.
/// Pure functions, so the rules are tested without a window.
/// </summary>
internal static class CodeReviewDiff
{
    /// <summary>Numbers every line and writes every hunk header.</summary>
    public static List<CodeReviewRow> Rows(IReadOnlyList<CodeReviewDiffLine> lines)
    {
        var rows = new List<CodeReviewRow>(lines.Count);
        int oldNo = 1, newNo = 1;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            switch (line.Kind)
            {
                case CodeReviewLineKind.Hunk:
                {
                    oldNo = line.OldStart;
                    newNo = line.NewStart;
                    int oldCount = 0, newCount = 0;
                    for (var j = i + 1; j < lines.Count && lines[j].Kind != CodeReviewLineKind.Hunk; j++)
                    {
                        if (lines[j].Kind != CodeReviewLineKind.Added)
                        {
                            oldCount++;
                        }
                        if (lines[j].Kind != CodeReviewLineKind.Deleted)
                        {
                            newCount++;
                        }
                    }
                    // A side with no lines starts at 0 in a unified diff ("@@ −0,0 +1,11 @@" for a new file).
                    var oldStart = oldCount == 0 ? 0 : line.OldStart;
                    var newStart = newCount == 0 ? 0 : line.NewStart;
                    var header = string.Create(CultureInfo.InvariantCulture, $"@@ −{oldStart},{oldCount} +{newStart},{newCount} @@");
                    rows.Add(new CodeReviewRow(line, 0, 0, header));
                    break;
                }
                case CodeReviewLineKind.Context:
                    rows.Add(new CodeReviewRow(line, oldNo++, newNo++, string.Empty));
                    break;
                case CodeReviewLineKind.Added:
                    rows.Add(new CodeReviewRow(line, 0, newNo++, string.Empty));
                    break;
                default:
                    rows.Add(new CodeReviewRow(line, oldNo++, 0, string.Empty));
                    break;
            }
        }
        return rows;
    }

    /// <summary>How many lines are added and removed.</summary>
    public static (int Added, int Deleted) Stat(IEnumerable<CodeReviewDiffLine> lines)
    {
        int added = 0, deleted = 0;
        foreach (var line in lines)
        {
            if (line.Kind == CodeReviewLineKind.Added)
            {
                added++;
            }
            else if (line.Kind == CodeReviewLineKind.Deleted)
            {
                deleted++;
            }
        }
        return (added, deleted);
    }

    /// <summary>
    /// The diff after applying <paramref name="suggestion"/>: the target lines in order are replaced where the first one
    /// stood. Added targets are dropped; unchanged targets stay as removals; the new lines (ids
    /// <c>{idPrefix}-1</c>, <c>-2</c>, ...) follow them. Returns null when a target is missing or is a removed line.
    /// </summary>
    public static List<CodeReviewDiffLine>? Apply(IReadOnlyList<CodeReviewDiffLine> lines, CodeReviewSuggestion suggestion,
        string idPrefix, out IReadOnlyList<string> newIds)
    {
        newIds = Array.Empty<string>();
        if (suggestion.LineIds.Count == 0)
        {
            return null;
        }
        var targets = new HashSet<string>(suggestion.LineIds, StringComparer.Ordinal);
        var found = 0;
        foreach (var line in lines)
        {
            if (targets.Contains(line.Id))
            {
                if (line.Kind is CodeReviewLineKind.Deleted or CodeReviewLineKind.Hunk)
                {
                    return null;
                }
                found++;
            }
        }
        if (found != targets.Count)
        {
            return null;
        }

        var result = new List<CodeReviewDiffLine>(lines.Count + suggestion.NewLines.Count);
        var ids = new List<string>(suggestion.NewLines.Count);
        var inserted = false;
        var seen = 0;
        foreach (var line in lines)
        {
            if (!targets.Contains(line.Id))
            {
                result.Add(line);
                continue;
            }
            seen++;
            if (line.Kind == CodeReviewLineKind.Context)
            {
                result.Add(CodeReviewDiffLine.Deleted(line.Id, line.Text));
            }
            if (seen == targets.Count && !inserted)
            {
                for (var i = 0; i < suggestion.NewLines.Count; i++)
                {
                    var id = string.Create(CultureInfo.InvariantCulture, $"{idPrefix}-{i + 1}");
                    ids.Add(id);
                    result.Add(CodeReviewDiffLine.Added(id, suggestion.NewLines[i]));
                }
                inserted = true;
            }
        }
        newIds = ids;
        return result;
    }

    /// <summary>The text of the lines a suggestion replaces, in order (the "−" lines of its card).</summary>
    public static List<string> OldText(IReadOnlyList<CodeReviewDiffLine> lines, CodeReviewSuggestion suggestion)
    {
        var byId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            byId.TryAdd(line.Id, line.Text);
        }
        var result = new List<string>(suggestion.LineIds.Count);
        foreach (var id in suggestion.LineIds)
        {
            if (byId.TryGetValue(id, out var text))
            {
                result.Add(text);
            }
        }
        return result;
    }
}
