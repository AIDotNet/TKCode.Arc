using System.Runtime.CompilerServices;
using Avalonia.Collections;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for the whole answer page (spec ai-answer.md section 10: moving a team's builds to a new CI system
/// without slowing releases): the four sample questions with their answers, reasoning, sources and follow-ups, the models,
/// and a responder that plays them. Every word is our own; the source and model marks are plain shapes. Shared by the
/// tests, the snapshot tool and the Gallery.
/// </summary>
internal static class AiAnswerPageSamples
{
    public const string MoveBuilds = "How do we move our builds to a new CI system without slowing releases?";
    public const string BlockingChecks = "Which checks should block a merge during the move?";
    public const string BuildTimes = "How do we keep build times down after switching runners?";
    public const string FlakyTests = "What should we do with flaky tests before cutting over?";

    /// <summary>The four sample questions (the empty page's suggestions).</summary>
    public static readonly string[] Questions = [MoveBuilds, BlockingChecks, BuildTimes, FlakyTests];

    /// <summary>What a question outside the script gets (no sources, a short thought).</summary>
    public const string OutOfScript =
        "Sample mode: only the four CI questions have scripted answers. Set a `Responder` to use a real model.";

    public const string MoveBuildsReasoning =
        "The risk is not the new pipeline but switching required checks before it has proven itself. I will suggest a mirror period, compare results, and switch on a quiet day.\n\n" +
        "Two traps: renamed jobs that stop blocking merges, and caches that start cold. I will cover the phases, runner choice and the required-check list.";

    public const string MoveBuildsConcise =
        "Run both pipelines side by side and switch the required checks only after a week of matching results. [1] Keep the old system as the fallback until two releases have shipped. [3]\n" +
        "\n" +
        "### Three steps\n" +
        "\n" +
        "1. **Mirror** every job without blocking anything. [1]\n" +
        "2. **Compare** pass rates and durations. [4]\n" +
        "3. **Switch** the checks without renaming jobs. [5]\n";

    public const string BlockingChecksAnswer =
        "Block merges only on the checks that already run in both systems; everything else reports without blocking until the switch. [5] A required check that silently stops running is worse than one that fails, so list them by name. [5] [1]\n" +
        "\n" +
        "### What blocks and what reports\n" +
        "\n" +
        "| Check | During the move | After the switch |\n" +
        "|---|---|---|\n" +
        "| Build | Blocks in both | Blocks |\n" +
        "| Unit tests | Blocks in the old system | Blocks |\n" +
        "| Lint | Reports | Blocks |\n" +
        "| Integration tests | Reports | Blocks once stable |\n" +
        "\n" +
        "Freeze changes to the check list three days before a release branch is cut. [3]\n";

    public const string BlockingChecksReasoning =
        "Required checks are matched by name, so the move is mostly about not breaking that match. I will separate what blocks from what only reports.";

    public const string BuildTimesAnswer =
        "Most of the time after a runner switch goes to cold caches, not to slower machines. [2] Key the dependency cache by the lockfile and share it across runners. [2]\n" +
        "\n" +
        "### Where the minutes go\n" +
        "\n" +
        "1. **Dependencies.** A shared cache keyed by the lockfile cut cold builds from eleven minutes to four. [2]\n" +
        "2. **Toolchains.** Pin them in the runner image instead of installing them in every job. [7]\n" +
        "3. **Artifacts.** Record what produced each artifact so a rebuild can be skipped safely. [8]\n";

    public const string BuildTimesReasoning =
        "Build time after a switch usually comes back once caches warm up; the question is how to make them warm from the start.";

    public const string FlakyTestsAnswer =
        "Quarantine flaky tests before the cutover so they do not hide real differences between the two pipelines. [6] Retrying a whole job hides the flake and doubles the time. [6] [4]\n" +
        "\n" +
        "### Before you cut over\n" +
        "\n" +
        "1. **Find them.** Compare failures between the two pipelines for the same commit. [1]\n" +
        "2. **Quarantine them.** Move each flaky test to a non-blocking suite with an owner. [6]\n" +
        "3. **Fix the secrets.** Most new failures come from secrets scoped to the old runners. [4]\n";

    public const string FlakyTestsReasoning =
        "A flaky test looks like a pipeline difference during a move. Quarantine first, then compare.";

    /// <summary>The models: Lumen Pro 4 (the default), Lumen 4, Lumen Mini 4, Atlas 2.1.</summary>
    public static AvaloniaList<AiModel> Models() =>
        new() { AiAnswerActionsSamples.LumenPro(), AiAnswerActionsSamples.Lumen(), AiAnswerActionsSamples.LumenMini(), AiAnswerActionsSamples.Atlas() };

    /// <summary>The eight sources (fresh objects every call: a stream numbers them).</summary>
    public static AvaloniaList<AiSource> Sources() => AiSourcesSamples.Sources();

    /// <summary>The sources a scripted question cites, by number.</summary>
    public static IReadOnlyList<AiSource> SourcesFor(string question)
    {
        var all = Sources();
        int[] numbers = question switch
        {
            MoveBuilds => [1, 2, 3, 4, 5, 6, 7, 8],
            BlockingChecks => [1, 3, 5],
            BuildTimes => [2, 7, 8],
            FlakyTests => [1, 4, 6],
            _ => []
        };
        return numbers.Select(n => all[n - 1]).ToArray();
    }

    /// <summary>The follow-ups of a question: the other sample questions.</summary>
    public static IReadOnlyList<string> FollowUpsFor(string question) =>
        Questions.Where(q => q != question).Take(3).ToArray();

    /// <summary>The scripted answer of a question in a mode ("More concise" shortens the first one).</summary>
    public static string AnswerFor(string question, AiAnswerMode mode) => question switch
    {
        MoveBuilds when mode == AiAnswerMode.Concise => MoveBuildsConcise,
        MoveBuilds => AiAnswerSamples.Answer,
        BlockingChecks => BlockingChecksAnswer,
        BuildTimes => BuildTimesAnswer,
        FlakyTests => FlakyTestsAnswer,
        _ => OutOfScript
    };

    public static string ReasoningFor(string question) => question switch
    {
        MoveBuilds => MoveBuildsReasoning,
        BlockingChecks => BlockingChecksReasoning,
        BuildTimes => BuildTimesReasoning,
        FlakyTests => FlakyTestsReasoning,
        _ => "This is not one of the four sample questions, so the sample has no answer for it."
    };

    /// <summary>A finished page without a responder: the first question with its answer, reasoning, sources and usage.</summary>
    public static AiAnswer StaticPage(Action<AiAnswer>? configure = null)
    {
        var answer = new AiAnswer
        {
            Question = MoveBuilds,
            Answer = AiAnswerSamples.Answer,
            Reasoning = MoveBuildsReasoning,
            Usage = new AiAnswerUsage(812, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(18.6)),
            ShareLinkProvider = (info, _) => ValueTask.FromResult(ShareLink(info))
        };
        answer.Models.AddRange(Models());
        answer.Sources.AddRange(Sources());
        answer.FollowUps.AddRange(FollowUpsFor(MoveBuilds));
        answer.Suggestions.AddRange(Questions);
        configure?.Invoke(answer);
        return answer;
    }

    /// <summary>A page that answers the sample questions with <paramref name="responder"/> (the sample one by default).</summary>
    public static AiAnswer LivePage(IAiAnswerResponder? responder = null, Action<AiAnswer>? configure = null)
    {
        var answer = new AiAnswer
        {
            Responder = responder ?? new AiAnswerSampleResponder(),
            ShareLinkProvider = (info, _) => ValueTask.FromResult(ShareLink(info))
        };
        answer.Models.AddRange(Models());
        answer.Suggestions.AddRange(Questions);
        configure?.Invoke(answer);
        return answer;
    }

    /// <summary>The sample's share link (an example address, marked as such by the Gallery).</summary>
    public static string ShareLink(AiAnswerShareInfo info) =>
        info.VersionIndex is { } index ? $"https://example.com/answers/ci-migration?v={index + 1}" : "https://example.com/answers/ci-migration";
}

/// <summary>
/// Plays the sample answers like a model would: the reasoning in small pieces, the sources early while it still thinks,
/// then the text in uneven chunks and the end with what it cost and the follow-ups. Questions outside the script get a
/// short note. The pauses go through <see cref="Delay"/> (tests replace it).
/// </summary>
internal sealed class AiAnswerSampleResponder : IAiAnswerResponder
{
    /// <summary>Waits between events; replace it to run the script without time.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (time, token) => Task.Delay(time, token);

    /// <summary>How fast the text arrives (characters per chunk and pause).</summary>
    public int ChunkLength { get; set; } = 24;

    public TimeSpan ChunkPause { get; set; } = TimeSpan.FromMilliseconds(40);

    public async IAsyncEnumerable<AiAnswerEvent> RespondAsync(AiAnswerRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var question = request.Question;
        var scripted = AiAnswerPageSamples.Questions.Contains(question);
        var small = request.Model?.Id == "lumen-mini";
        var concise = request.Mode == AiAnswerMode.Concise;
        foreach (var piece in Split(AiAnswerPageSamples.ReasoningFor(question), 18))
        {
            await Delay(TimeSpan.FromMilliseconds(30), cancellationToken);
            yield return new AiAnswerReasoningEvent(piece);
            if (scripted && piece.Length > 0 && piece.Contains('.'))
            {
                // Arc sends the sources early, while the model still thinks.
                scripted = false;
                await Delay(TimeSpan.FromMilliseconds(120), cancellationToken);
                yield return new AiAnswerSourcesEvent(AiAnswerPageSamples.SourcesFor(question));
            }
        }
        var thought = AiAnswerPageSamples.Questions.Contains(question) ? (small ? 6 : 8) : 2;
        await Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        yield return new AiAnswerThoughtEvent(TimeSpan.FromSeconds(thought));
        var text = AiAnswerPageSamples.AnswerFor(question, request.Mode);
        foreach (var chunk in Split(text, ChunkLength))
        {
            await Delay(ChunkPause, cancellationToken);
            yield return new AiAnswerTextEvent(chunk);
        }
        var tokens = concise ? 430 : AiAnswerPageSamples.Questions.Contains(question) ? (small ? 640 : 812) : 62;
        var seconds = concise ? 10.2 : small ? 7.9 : AiAnswerPageSamples.Questions.Contains(question) ? 18.6 : 2.7;
        yield return new AiAnswerDoneEvent(tokens, TimeSpan.FromSeconds(seconds), AiAnswerPageSamples.FollowUpsFor(question));
    }

    /// <summary>Uneven pieces of about <paramref name="length"/> characters, cut after a space where one is near.</summary>
    internal static IEnumerable<string> Split(string text, int length)
    {
        var start = 0;
        var step = 0;
        while (start < text.Length)
        {
            var size = Math.Max(4, length + (step++ % 3 - 1) * length / 3);
            var end = Math.Min(text.Length, start + size);
            if (end < text.Length && text.LastIndexOf(' ', end - 1, Math.Min(8, end - start)) is var space and > 0 && space > start)
            {
                end = space + 1;
            }
            yield return text[start..end];
            start = end;
        }
    }
}
