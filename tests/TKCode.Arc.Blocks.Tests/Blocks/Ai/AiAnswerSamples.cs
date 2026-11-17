using Avalonia.Collections;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>Invented sample data for the answer parts (spec ai-answer.md section 10: moving builds to a new CI system).</summary>
internal static class AiAnswerSamples
{
    /// <summary>The answer to the first sample question: every block kind, citations in paragraphs, list items and after code.</summary>
    public const string Answer =
        "Keep both pipelines running for a while and switch the required checks only after the new one has matched the old one for a week. [1] Keep the old system as the fallback until two releases have shipped on the new one. [3]\n" +
        "\n" +
        "### Move it in three phases\n" +
        "\n" +
        "1. **Mirror.** Copy each job into the new system and let it run on every push without blocking anything. [1]\n" +
        "2. **Compare.** Track pass rates and durations for both pipelines; investigate any job that disagrees. [4] [6]\n" +
        "3. **Switch.** Rename nothing on the day you flip the required checks, because checks are matched by name. [5]\n" +
        "\n" +
        "### Old runners or new runners\n" +
        "\n" +
        "Most teams can move straight to the new hosted runners. [2] Keep a self-hosted pool only for jobs that need special hardware. [7]\n" +
        "\n" +
        "| | Hosted runners | Self-hosted pool |\n" +
        "|---|---|---|\n" +
        "| Setup | Minutes | Days |\n" +
        "| Cache | Shared by lockfile | Local disk |\n" +
        "| Good fit | Most jobs | GPU and device tests |\n" +
        "| Cost model | Per minute | Fixed machines |\n" +
        "\n" +
        "### Keep the checks honest\n" +
        "\n" +
        "List the required checks once in `ci/required-checks.yml` and compare them before and after the switch. [5]\n" +
        "\n" +
        "```yaml ci/required-checks.yml\n" +
        "# Checks that must pass before a merge.\n" +
        "required:\n" +
        "  - build\n" +
        "  - unit-tests\n" +
        "  - lint\n" +
        "freeze_days_before_release: 3\n" +
        "```\n" +
        "\n" +
        "Watch merge-queue wait times during the first week. If they double, pause the switch and look at caching first. [2] [8]\n";

    /// <summary>The eight invented sources of the sample answer.</summary>
    public static AvaloniaList<AiSource> Sources() =>
    [
        Source(1, "Running two pipelines side by side", "docs.pipewright.dev", "Aug 12, 2026",
            "Mirror each job in the new system and compare results for a week before switching the required checks."),
        Source(2, "Caching dependencies across runners", "eng.northgate.io", "Jul 30, 2026",
            "A shared cache keyed by the lockfile cut cold builds from eleven minutes to four."),
        Source(3, "Release freeze checklist", "handbook.fieldnote.app", "Jun 18, 2026",
            "Freeze pipeline changes three days before a release branch is cut."),
        Source(4, "What broke when we moved CI", "blog.tallyhouse.com", "May 2, 2026",
            "Secrets scoped to the old runners were the most common failure."),
        Source(5, "Required status checks", "quarry.dev", "Sep 9, 2026",
            "Required checks are matched by name, so renamed jobs silently stop blocking merges."),
        Source(6, "Flaky test quarantine", "lanternworks.org", "Apr 21, 2026",
            "Move a flaky test into quarantine instead of retrying the whole job."),
        Source(7, "Runner images and toolchains", "wiki.brightmill.net", "Mar 15, 2026",
            "Pin toolchain versions in the runner image, not in each job."),
        Source(8, "Build provenance basics", "openbuildnotes.org", "Feb 7, 2026",
            "Record which commit, image and inputs produced each artifact.")
    ];

    public static AiSource Source(int index, string title, string domain, string? date = null, string? snippet = null) =>
        new() { Index = index, Title = title, Domain = domain, Date = date, Snippet = snippet, Url = new Uri("https://" + domain + "/") };
}
