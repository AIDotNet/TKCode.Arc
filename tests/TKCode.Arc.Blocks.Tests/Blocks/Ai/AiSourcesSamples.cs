using Avalonia.Collections;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The invented sources of the AI answer spec (section 10: moving a team's builds to a new CI system). Marks are plain
/// geometric shapes in their own colors (circle, square, triangle, diamond); the last source has no mark and shows the
/// first-letter fallback.
/// </summary>
internal static class AiSourcesSamples
{
    public static readonly Geometry Circle = Geometry.Parse("M12 2a10 10 0 1 1 0 20a10 10 0 1 1 0-20z");
    public static readonly Geometry Square = Geometry.Parse("M6 3h12a3 3 0 0 1 3 3v12a3 3 0 0 1-3 3H6a3 3 0 0 1-3-3V6a3 3 0 0 1 3-3z");
    public static readonly Geometry Triangle = Geometry.Parse("M12 2.5L22 21H2z");
    public static readonly Geometry Diamond = Geometry.Parse("M12 1.5L22.5 12L12 22.5L1.5 12z");

    /// <summary>The eight sources, numbered 1 to 8.</summary>
    public static AvaloniaList<AiSource> Sources(int count = 8)
    {
        var all = new AvaloniaList<AiSource>
        {
            Make(1, "Running two pipelines side by side", "docs.pipewright.dev", Circle, "#2F6FEB", "Aug 12, 2026",
                "Mirror each job in the new system and compare results for a week before switching the required checks."),
            Make(2, "Caching dependencies across runners", "eng.northgate.io", Square, "#E8590C", "Jul 30, 2026",
                "A shared cache keyed by the lockfile cut cold builds from eleven minutes to four."),
            Make(3, "Release freeze checklist", "handbook.fieldnote.app", Triangle, "#2B8A3E", "Jun 18, 2026",
                "Freeze pipeline changes three days before a release branch is cut."),
            Make(4, "What broke when we moved CI", "blog.tallyhouse.com", Diamond, "#7048E8", "May 2, 2026",
                "Secrets scoped to the old runners were the most common failure."),
            Make(5, "Required status checks", "quarry.dev", Square, "#0C8599", "Sep 9, 2026",
                "Required checks are matched by name, so renamed jobs silently stop blocking merges."),
            Make(6, "Flaky test quarantine", "lanternworks.org", Triangle, "#E8A100", "Apr 21, 2026",
                "Move a flaky test into quarantine instead of retrying the whole job."),
            Make(7, "Runner images and toolchains", "wiki.brightmill.net", Diamond, "#D6336C", "Mar 15, 2026",
                "Pin toolchain versions in the runner image, not in each job."),
            Make(8, "Build provenance basics", "openbuildnotes.org", null, null, "Feb 7, 2026",
                "Record which commit, image and inputs produced each artifact.")
        };
        while (all.Count > count)
        {
            all.RemoveAt(all.Count - 1);
        }
        return all;
    }

    public static AiSource Make(int index, string title, string? domain, Geometry? mark = null, string? color = null, string? date = null,
        string? snippet = null) => new()
    {
        Index = index,
        Title = title,
        Domain = domain,
        Url = !string.IsNullOrWhiteSpace(domain) && Uri.TryCreate($"https://{domain.Trim()}/", UriKind.Absolute, out var url) ? url : null,
        Mark = mark,
        MarkBrush = color is null ? null : new SolidColorBrush(Color.Parse(color)),
        Date = date,
        Snippet = snippet
    };

    /// <summary>The answer's wording for a card's accessible name (the lead passes <c>AiAnswerLabels.SourceCard</c>).</summary>
    public static string CardName(int number, string title, string? domain) =>
        string.IsNullOrEmpty(domain) ? $"Source {number}: {title}" : $"Source {number}: {title}, {domain}";

    /// <summary>The label of the sources row.</summary>
    public static string Label(int count) => count == 1 ? "1 source" : $"{count} sources";
}
