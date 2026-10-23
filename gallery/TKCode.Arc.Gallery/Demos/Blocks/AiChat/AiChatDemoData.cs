using System.Runtime.CompilerServices;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Invented models, conversations, suggestions and a scripted responder for the AI chat demos.</summary>
internal static class AiChatDemoData
{
    public static IReadOnlyList<AiModelProvider> Providers() => ModelPickerDemoData.Providers();

    /// <summary>The models of the picker demo, without effort levels (the chat has no effort setting).</summary>
    public static IReadOnlyList<AiModel> Models()
    {
        var models = ModelPickerDemoData.Models();
        foreach (var model in models)
        {
            model.EffortLevels = null;
        }

        return models;
    }

    public static AiChatSuggestion[] Suggestions() =>
    [
        new() { Label = "Find the slowest step in the nightly build", Icon = ArcIcons.Timer },
        new() { Label = "Compare this week's test failures to last week", Icon = ArcIcons.Search },
        new() { Label = "Check disk usage on ci-runner-7", Icon = ArcIcons.Activity }
    ];

    public static AiSource[] Sources() =>
    [
        new() { Index = 1, Title = "Caching dependencies in pipelines", Domain = "docs.example.org", Url = new Uri("https://docs.example.org/caching"),
            Snippet = "A cache is restored by its key. When the key changes on every run, the restore step finds nothing and every build starts cold.", Mark = ArcIcons.Globe },
        new() { Index = 2, Title = "Why our cache hit rate fell to zero", Domain = "blog.example.dev", Url = new Uri("https://blog.example.dev/cache-hit-rate"),
            Snippet = "We hashed the whole lock file together with a timestamp. Hashing only the lock file brought the hit rate back above 90 percent.", Mark = ArcIcons.Globe },
        new() { Index = 3, Title = "Build performance checklist", Domain = "handbook.example.com", Url = new Uri("https://handbook.example.com/build-performance"),
            Snippet = "Pin the base image, restore caches before compiling and split slow test suites into parallel jobs.", Mark = ArcIcons.Globe }
    ];

    /// <summary>The conversation of the preview: two turns, the first with reasoning, a search and citations.</summary>
    public static AiChatConversation NightlyBuild(string modelId)
    {
        var conversation = new AiChatConversation { Id = "nightly", Title = "Speed up the nightly build", Group = "Today", ModelId = modelId };

        var first = new AiChatReply
        {
            ModelId = modelId,
            Reasoning = "Compare the stage timings before and after the jump. A cache that stopped restoring usually shows up as a slow restore step and a long compile, not as a slow test stage.",
            ThoughtFor = TimeSpan.FromSeconds(3),
            Text = NightlyText
        };
        var search = new AiChatTool
        {
            Id = "search-1", Kind = AiChatToolKind.Search, Status = AiChatToolStatus.Done,
            RunningLabel = "Searching the docs", DoneLabel = "Searched the docs", Summary = "3 sources"
        };
        search.Queries.AddRange(["build cache key changed lock file", "restore cache miss slow compile"]);
        search.Sources.AddRange(Sources());
        first.Tools.Add(search);
        first.Sources.AddRange(Sources());
        conversation.Turns.Add(new AiChatTurn(
            new AiChatUserMessage { Text = "Our nightly desktop build went from 18 to 31 minutes this month. What changed, and what should we fix first?" }, first));

        var second = new AiChatReply
        {
            ModelId = modelId,
            Reasoning = "Pull the restore and compile timings for the last 30 days and compare them.",
            ThoughtFor = TimeSpan.FromSeconds(2),
            Text = "About 1.5 hours a week.\n\n- Nightly runs: 7\n- Saved per run: about 13 minutes\n- Pull request runs: roughly double that again\n\nThe restore step now takes 4 seconds because it restores nothing; the compile step pays for it."
        };
        second.Tools.Add(new AiChatTool
        {
            Id = "query-1", Kind = AiChatToolKind.Query, Status = AiChatToolStatus.Done,
            RunningLabel = "Querying build history", DoneLabel = "Queried build history", Summary = "212 runs",
            Input = "SELECT avg(restore_ms), avg(compile_ms) FROM runs WHERE started > now() - interval '30 days';",
            Output = "restore 4.1 s, compile 19.6 min"
        });
        conversation.Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = "How much time would the cache fix save us per week?" }, second));
        return conversation;
    }

    public static AiChatConversation[] History() =>
    [
        new() { Id = "staging", Title = "Rename the staging bucket", Group = "Today" },
        new() { Id = "notes", Title = "Write the 4.2 release notes", Group = "Today" },
        new() { Id = "survey", Title = "Summarize the support survey", Group = "Previous 7 days" },
        new() { Id = "login", Title = "Flaky login test", Group = "Previous 7 days" }
    ];

    private const string NightlyText =
        "Most of the lost time is in the compile stage, and the cache is the first suspect.\n\n"
        + "1. **Check the cache key.** If it includes anything that changes on every run, nothing is ever restored [1].\n"
        + "2. **Pin the SDK image.** A moving tag can invalidate the cache without any change on your side.\n"
        + "3. **Split the UI tests into two jobs.** They are the next largest stage [3].\n\n"
        + "```yaml\nkey: build-${{ hashFiles('**/packages.lock.json') }}\nrestore-keys: build-\npath:\n  - ~/.nuget/packages\n```\n\n"
        + "Teams that hashed only the lock file saw the hit rate return within a day [2].";

    /// <summary>
    /// A responder that answers from a few scripts: a question about the build gets reasoning, a search and a cited answer; a
    /// question about a runner gets a tool that times out; anything else gets a short generic answer with a code block.
    /// </summary>
    public sealed class ScriptedResponder : IAiChatResponder
    {
        public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AiChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var prompt = request.Messages[^1].Content;
            var script = prompt.Contains("runner", StringComparison.OrdinalIgnoreCase) ? Script.Failing
                : prompt.Contains("build", StringComparison.OrdinalIgnoreCase) ? Script.Build
                : Script.Generic;

            await Task.Delay(500, cancellationToken);
            yield return new AiChatReasoningDelta(script == Script.Build
                ? "Compare the stage timings before and after the jump. A cache that stopped restoring shows up as a slow compile."
                : "Look at what the question needs, then check it against the data that is available.");
            await Task.Delay(700, cancellationToken);
            yield return new AiChatReasoningEnd();

            if (script == Script.Failing)
            {
                yield return new AiChatToolUpdate("disk", AiChatToolKind.Query, AiChatToolStatus.Running, "Querying ci-runner-7");
                await Task.Delay(900, cancellationToken);
                yield return new AiChatToolUpdate("disk", AiChatToolKind.Query, AiChatToolStatus.Error, "Querying ci-runner-7", "Timed out",
                    Input: "SELECT mount, used_pct FROM disk_usage WHERE host = 'ci-runner-7';");
                yield return new AiChatErrorEvent("ci-runner-7 did not answer within 8 seconds.");
                yield break;
            }

            if (script == Script.Build)
            {
                yield return new AiChatToolUpdate("search", AiChatToolKind.Search, AiChatToolStatus.Running, "Searching the docs");
                await Task.Delay(900, cancellationToken);
                var sources = Sources();
                yield return new AiChatToolUpdate("search", AiChatToolKind.Search, AiChatToolStatus.Done, "Searched the docs", "3 sources",
                    Queries: ["build cache key changed lock file", "restore cache miss slow compile"], Sources: sources);
                yield return new AiChatSourcesEvent(sources);
            }

            var text = script == Script.Build
                ? NightlyText
                : "This demo only knows the nightly build conversation, so here is a quick check you can run anywhere.\n\n"
                  + "```sh\nls -S build/logs | head -5\ngrep -c 'cache hit' build/logs/*.log\n```\n\nIf the hit rate is under 50 %, start with the cache key.";
            foreach (var chunk in Chunks(text, 24))
            {
                await Task.Delay(60, cancellationToken);
                yield return new AiChatTextDelta(chunk);
            }
        }

        private enum Script { Generic, Build, Failing }

        private static IEnumerable<string> Chunks(string text, int size)
        {
            for (var i = 0; i < text.Length; i += size)
            {
                yield return text.Substring(i, Math.Min(size, text.Length - i));
            }
        }
    }
}
