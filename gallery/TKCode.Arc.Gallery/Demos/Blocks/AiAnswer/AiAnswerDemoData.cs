using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Invented models, sources, questions and a scripted responder for the AI answer demos (a team moving its builds to a new CI system).</summary>
internal static class AiAnswerDemoData
{
    public const string FirstQuestion = "How do we move our builds to a new CI system without slowing releases?";

    public static string[] Questions { get; } =
    [
        FirstQuestion,
        "Which checks should block a merge during the move?",
        "How do we keep build times down after switching runners?",
        "What should we do with flaky tests before cutting over?"
    ];

    public static AiModel[] Models() =>
    [
        Model("lumen-pro", "Lumen Pro 4", "#E8742A"),
        Model("lumen", "Lumen 4", "#2F6FEB"),
        Model("lumen-mini", "Lumen Mini 4", "#2E9F6B"),
        Model("atlas", "Atlas 2.1", "#7B4FD6")
    ];

    private static AiModel Model(string id, string name, string color) => new()
    {
        Id = id, Name = name,
        Mark = Geometry.Parse("M12 2v20M3.3 7l17.4 10M3.3 17L20.7 7"),
        MarkColor = new SolidColorBrush(Color.Parse(color))
    };

    public static AiSource[] Sources() =>
    [
        Source(1, "Running two pipelines side by side", "docs.pipewright.dev", "Aug 12, 2026", "Mirror each job in the new system and compare results for a week before switching the required checks.", "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z", "#E8742A"),
        Source(2, "Caching dependencies across runners", "eng.northgate.io", "Jul 30, 2026", "A shared cache keyed by the lockfile cut cold builds from eleven minutes to four.", "M4 4h16v16H4Z", "#2F6FEB"),
        Source(3, "Release freeze checklist", "handbook.fieldnote.app", "Jun 18, 2026", "Freeze pipeline changes three days before a release branch is cut.", "M12 3l9 17H3Z", "#2E9F6B"),
        Source(4, "What broke when we moved CI", "blog.tallyhouse.com", "May 2, 2026", "Secrets scoped to the old runners were the most common failure.", "M12 2l10 10-10 10L2 12Z", "#7B4FD6"),
        Source(5, "Required status checks", "quarry.dev", "Sep 9, 2026", "Required checks are matched by name, so renamed jobs silently stop blocking merges.", "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z", "#C0392B"),
        Source(6, "Flaky test quarantine", "lanternworks.org", "Apr 21, 2026", "Move a flaky test into quarantine instead of retrying the whole job.", "M4 4h16v16H4Z", "#16837F"),
        Source(7, "Runner images and toolchains", "wiki.brightmill.net", "Mar 15, 2026", "Pin toolchain versions in the runner image, not in each job.", "M12 3l9 17H3Z", "#B7791F"),
        Source(8, "Build provenance basics", "openbuildnotes.org", "Feb 7, 2026", "Record which commit, image and inputs produced each artifact.", "M12 2l10 10-10 10L2 12Z", "#5A6B7B")
    ];

    private static AiSource Source(int index, string title, string domain, string date, string snippet, string mark, string color) => new()
    {
        Index = index, Title = title, Domain = domain, Date = date, Snippet = snippet,
        Url = new Uri("https://" + domain + "/"), Mark = Geometry.Parse(mark), MarkBrush = new SolidColorBrush(Color.Parse(color))
    };

    public const string FirstAnswer =
        "Keep both pipelines running for a while and switch the required checks only after the new one has matched the old one for a week. [1] Keep the old system as the fallback until two releases have shipped on the new one. [3]\n\n"
        + "### Move it in three phases\n\n"
        + "1. **Mirror.** Copy each job into the new system and let it run on every push without blocking anything. [1]\n"
        + "2. **Compare.** Track pass rates and durations for both pipelines; investigate any job that disagrees. [4] [6]\n"
        + "3. **Switch.** Rename nothing on the day you flip the required checks, because checks are matched by name. [5]\n\n"
        + "### Old runners or new runners\n\n"
        + "Most teams can move straight to the new hosted runners. [2] Keep a self-hosted pool only for jobs that need special hardware. [7]\n\n"
        + "| | Hosted runners | Self-hosted pool |\n|---|---|---|\n| Setup | Minutes | Days |\n| Cache | Shared by lockfile | Local disk |\n| Good fit | Most jobs | GPU and device tests |\n| Cost model | Per minute | Fixed machines |\n\n"
        + "### Keep the checks honest\n\n"
        + "List the required checks once and compare them before and after the switch. [5]\n\n"
        + "```yaml ci/required-checks.yml\nrequired:\n  - build\n  - unit-tests\n  - lint\nfreeze_days_before_release: 3\n```\n\n"
        + "Watch merge-queue wait times during the first week. If they double, pause the switch and look at caching first. [2] [8]";

    public const string FirstReasoning =
        "The risk is not the new pipeline but switching required checks before it has proven itself. I will suggest a mirror period, compare results, and switch on a quiet day.\n\n"
        + "Two traps: renamed jobs that stop blocking merges, and caches that start cold. I will cover the phases, runner choice and the required-check list.";

    /// <summary>A responder for the four sample questions; anything else gets a short note. Retry, concise, detailed and switch model change the text a little.</summary>
    public sealed class ScriptedResponder : IAiAnswerResponder
    {
        public async IAsyncEnumerable<AiAnswerEvent> RespondAsync(AiAnswerRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var known = Array.IndexOf(Questions, request.Question) >= 0;
            var sources = Sources();

            if (known)
            {
                yield return new AiAnswerSourcesEvent(sources);
            }

            foreach (var chunk in Chunks(known ? FirstReasoning : "Check whether the question matches a scripted one.", 40))
            {
                await Task.Delay(40, cancellationToken);
                yield return new AiAnswerReasoningEvent(chunk);
            }

            await Task.Delay(300, cancellationToken);
            yield return new AiAnswerThoughtEvent();

            var text = known
                ? request.Mode switch
                {
                    AiAnswerMode.Concise => FirstAnswer[..FirstAnswer.IndexOf("### Move it", StringComparison.Ordinal)].TrimEnd(),
                    _ => FirstAnswer
                }
                : "Sample mode: only the four CI questions have scripted answers. Set a Responder to use a real model.";
            foreach (var chunk in Chunks(text, 28))
            {
                await Task.Delay(45, cancellationToken);
                yield return new AiAnswerTextEvent(chunk);
            }

            yield return new AiAnswerDoneEvent(known ? 812 : 96, TimeSpan.FromSeconds(known ? 18.6 : 3.1),
                known ? Questions.Where(q => q != request.Question).Take(3).ToArray() : null);
        }

        private static IEnumerable<string> Chunks(string text, int size)
        {
            for (var i = 0; i < text.Length; i += size)
            {
                yield return text.Substring(i, Math.Min(size, text.Length - i));
            }
        }
    }
}
