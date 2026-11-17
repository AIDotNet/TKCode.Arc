using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for <see cref="AiChat"/> (spec ai-chat section 10): the nightly build conversation, the history,
/// five made-up models with marks drawn here (no vendor's logo), three suggestions and a scripted responder that plays
/// replies with delays. Shared by the tests, the snapshot tool and (for the lead) the Gallery.
/// </summary>
internal static class AiChatSamples
{
    public const string Title = "Speed up the nightly build";

    public const string FirstPrompt =
        "Our nightly desktop build went from 18 to 31 minutes this month. What changed, and what should we fix first?";

    public const string FirstReasoning =
        "Compare the stage timings before and after the jump. A cache that stopped restoring usually shows up as a slow " +
        "restore step and a long compile, not as a slow test stage.";

    public const string FirstText =
        "The jump lines up with the week the lock file changed, so the dependency cache most likely stopped restoring. Fix " +
        "these in order:\n\n" +
        "1. **Check the cache key.** A key made from the whole lock file misses on every dependency bump, so each run " +
        "compiles from scratch. [1]\n" +
        "2. **Pin the SDK image.** A floating tag pulled a new toolchain twice this month, and every pull starts cold.\n" +
        "3. **Split the UI tests into two jobs.** They take 9 of the 31 minutes and can run next to the package step. [3]\n\n" +
        "```yaml\n" +
        "key: deps-${{ hashFiles('**/packages.lock.json') }}\n" +
        "restore-keys: deps-   # fall back to the newest cache\n" +
        "path:\n" +
        "  - ~/.nuget/packages\n" +
        "```\n\n" +
        "With a partial restore key, a changed lock file still restores most packages instead of none. [2]";

    public const string SecondPrompt = "How much time would the cache fix save us per week?";

    public const string SecondReasoning =
        "Average the restore and compile steps over the last month and multiply by the runs per week.";

    public const string SecondText =
        "About 1.5 hours a week.\n\n" +
        "- Nightly runs: 7\n" +
        "- Saved per run: about 13 minutes\n" +
        "- Pull request runs: roughly double that again\n\n" +
        "Pull request builds share the same cache, so each one that hits the key saves the same 13 minutes:\n\n" +
        "```sql\n" +
        "SELECT count(*) FROM runs\n" +
        "WHERE trigger = 'pull_request' AND started > now() - interval '7 days';\n" +
        "```";

    public const string GenericText =
        "This demo only knows the nightly build conversation, so here is a quick check you can run anywhere.\n\n" +
        "```sh\n" +
        "ci stats steps --slowest 5\n" +
        "ci stats cache --hit-rate\n" +
        "```\n\n" +
        "If the hit rate is under 50 %, start with the cache key.";

    public const string RunnerError = "ci-runner-7 did not answer within 8 seconds.";

    #region Marks (drawn here: plain shapes, no real brand)

    /// <summary>A ring with a small satellite (coral).</summary>
    public static readonly Geometry OrbitMark = AiReplySamples.OrbitMark;

    /// <summary>A thinner ring with the satellite.</summary>
    public static readonly Geometry OrbitSwiftMark =
        Geometry.Parse("M12 6a6 6 0 1 0 0 12a6 6 0 1 0 0-12z M12 7.6a4.4 4.4 0 1 1 0 8.8a4.4 4.4 0 1 1 0-8.8z M19.5 2.5a2 2 0 1 0 0.01 0z");

    /// <summary>A leaf-like quill (single color: it takes the text color).</summary>
    public static readonly Geometry QuillMark =
        Geometry.Parse("M20 3C13 3 7.5 8 6.6 14.2L4 20.4l1.8.8 2.5-5.3C14.5 15 19.6 9.6 20 3z");

    /// <summary>Two folded lines (dark teal).</summary>
    public static readonly Geometry TernMark =
        Geometry.Parse("M2 9.5l5-4 5 4 5-4 5 4v3l-5-4-5 4-5-4-5 4z M2 16l5-4 5 4 5-4 5 4v3l-5-4-5 4-5-4-5 4z");

    /// <summary>A triangle in three colors.</summary>
    public static IImage PrismMark()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing { Brush = new SolidColorBrush(Color.Parse("#4C7BE8")), Geometry = Geometry.Parse("M12 3L3 20h9z") });
        group.Children.Add(new GeometryDrawing { Brush = new SolidColorBrush(Color.Parse("#E8A23C")), Geometry = Geometry.Parse("M12 3l9 17h-9z") });
        group.Children.Add(new GeometryDrawing { Brush = new SolidColorBrush(Color.Parse("#3FA77A")), Geometry = Geometry.Parse("M7.5 14h9L12 20z") });
        group.Children.Add(new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(new Rect(0, 0, 24, 24)) });
        return new DrawingImage(group);
    }

    #endregion

    public static AiModel[] Models() =>
    [
        new AiModel { Id = "orbit-pro", Name = "Orbit Pro 3", Note = "Northwind Labs", Mark = OrbitMark, MarkColor = new SolidColorBrush(Color.Parse("#E8714F")) },
        new AiModel { Id = "orbit-swift", Name = "Orbit Swift 3", Note = "Northwind Labs", Mark = OrbitSwiftMark, MarkColor = new SolidColorBrush(Color.Parse("#E8714F")) },
        new AiModel { Id = "quill-2", Name = "Quill 2.4", Note = "Fernwood AI", Mark = QuillMark },
        new AiModel { Id = "prism-x", Name = "Prism X", Note = "Lattice Research", Mark = PrismMark() },
        new AiModel { Id = "tern-large", Name = "Tern Large", Note = "Coastline", Mark = TernMark, MarkColor = new SolidColorBrush(Color.Parse("#1F6F78")) }
    ];

    public static AiChatSuggestion[] Suggestions() =>
    [
        new AiChatSuggestion { Label = "Find the slowest step in the nightly build", Icon = ArcIcons.Timer },
        new AiChatSuggestion { Label = "Compare this week's test failures to last week", Icon = ArcIcons.Search },
        new AiChatSuggestion { Label = "Check disk usage on ci-runner-7", Icon = ArcIcons.Activity }
    ];

    public static AiSource[] Sources() =>
    [
        new AiSource { Index = 1, Title = "Caching dependencies in pipelines", Domain = "docs.example.org", Url = new Uri("https://docs.example.org/pipelines/caching"),
            Snippet = "Key the cache on the lock file, and add a restore key so a changed file still restores most packages." },
        new AiSource { Index = 2, Title = "Why our cache hit rate fell to zero", Domain = "blog.example.dev", Url = new Uri("https://blog.example.dev/cache-hit-rate"),
            Snippet = "Every dependency bump changed the key, so the cache never matched and each build compiled from scratch." },
        new AiSource { Index = 3, Title = "Build performance checklist", Domain = "handbook.example.com", Url = new Uri("https://handbook.example.com/build/checklist"),
            Snippet = "Split long test stages into parallel jobs before tuning individual tests." }
    ];

    public static AiChatTool SearchTool()
    {
        var tool = new AiChatTool
        {
            Id = "search-1",
            Kind = AiChatToolKind.Search,
            Status = AiChatToolStatus.Done,
            RunningLabel = "Searching the docs",
            DoneLabel = "Searched the docs",
            Summary = "3 sources"
        };
        tool.Queries.Add("build cache key changed lock file");
        tool.Queries.Add("restore cache miss slow compile");
        tool.Sources.AddRange(Sources());
        return tool;
    }

    public static AiChatTool QueryTool() => new()
    {
        Id = "query-1",
        Kind = AiChatToolKind.Query,
        Status = AiChatToolStatus.Done,
        RunningLabel = "Querying build history",
        DoneLabel = "Queried build history",
        Summary = "212 runs",
        Input = "SELECT avg(restore_ms), avg(compile_ms) FROM runs WHERE started > now() - interval '30 days';",
        Output = "restore 4.1 s, compile 19.6 min"
    };

    /// <summary>The settled nightly build conversation: two turns, both done.</summary>
    public static AiChatConversation NightlyBuild(string modelId = "orbit-pro")
    {
        var conversation = new AiChatConversation { Id = "nightly", Title = Title, Group = "Today", ModelId = modelId };
        var first = new AiChatReply
        {
            ModelId = modelId,
            Status = AiChatReplyStatus.Done,
            Reasoning = FirstReasoning,
            ThoughtFor = TimeSpan.FromSeconds(3),
            Text = FirstText
        };
        first.Tools.Add(SearchTool());
        first.Sources.AddRange(Sources());
        conversation.Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = FirstPrompt }, first));
        var second = new AiChatReply
        {
            ModelId = modelId,
            Status = AiChatReplyStatus.Done,
            Reasoning = SecondReasoning,
            ThoughtFor = TimeSpan.FromSeconds(2),
            Text = SecondText
        };
        second.Tools.Add(QueryTool());
        conversation.Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = SecondPrompt }, second));
        return conversation;
    }

    /// <summary>A short conversation of the history (one done turn), written by <paramref name="modelId"/>.</summary>
    public static AiChatConversation Short(string id, string title, string? group, string prompt, string answer, string modelId = "quill-2")
    {
        var conversation = new AiChatConversation { Id = id, Title = title, Group = group, ModelId = modelId };
        conversation.Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = prompt },
            new AiChatReply { ModelId = modelId, Status = AiChatReplyStatus.Done, ThoughtFor = TimeSpan.FromSeconds(1), Text = answer }));
        return conversation;
    }

    /// <summary>The history of the title menu: the nightly build first ("Today"), then four more.</summary>
    public static AiChatConversation[] History() =>
    [
        NightlyBuild(),
        Short("bucket", "Rename the staging bucket", "Today", "What breaks if we rename the staging bucket?",
            "Anything that hard-codes the old name: the deploy script, two scheduled jobs and the CDN origin."),
        Short("notes", "Write the 4.2 release notes", "Today", "Draft the 4.2 release notes from the merged pull requests.",
            "Here is a first draft grouped by area: editor, sync and settings."),
        Short("survey", "Summarize the support survey", "Previous 7 days", "Summarize the support survey in five bullets.",
            "Most answers ask for faster replies on weekends and a clearer refund page."),
        Short("flaky", "Flaky login test", "Previous 7 days", "Why does the login test fail one run in ten?",
            "It waits for a fixed 500 ms before the redirect finishes; wait for the dashboard heading instead.")
    ];

    /// <summary>A chat with the sample models and suggestions.</summary>
    public static AiChat Chat(IAiChatResponder? responder = null, bool history = false)
    {
        var chat = new AiChat { Responder = responder };
        chat.Models.AddRange(Models());
        chat.Suggestions.AddRange(Suggestions());
        if (history)
        {
            var conversations = History();
            chat.Conversations.AddRange(conversations);
            chat.CurrentConversation = conversations[0];
        }
        return chat;
    }
}

/// <summary>
/// The sample responder of the Gallery and the snapshots: it plays one of three invented scripts with Arc-like delays
/// (thinking about a second, a tool about 850ms, text in small pieces): questions about time or savings query the build
/// history; the disk check of ci-runner-7 times out and fails; anything else gets the generic answer.
/// </summary>
internal sealed class AiChatSampleResponder : IAiChatResponder
{
    /// <summary>Waits between events (tests replace it).</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (delay, token) => Task.Delay(delay, token);

    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AiChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var prompt = request.Messages.Count > 0 ? request.Messages[^1].Content : string.Empty;
        var lower = prompt.ToLowerInvariant();
        if (lower.Contains("disk") || lower.Contains("ci-runner-7"))
        {
            await Delay(TimeSpan.FromMilliseconds(600), cancellationToken);
            yield return new AiChatReasoningDelta("Ask the runner for its disk usage per mount.");
            yield return new AiChatReasoningEnd(TimeSpan.FromSeconds(1));
            yield return new AiChatToolUpdate("disk", AiChatToolKind.Query, AiChatToolStatus.Running, "Querying ci-runner-7",
                Input: "SELECT mount, used_pct FROM disk_usage WHERE host = 'ci-runner-7';");
            await Delay(TimeSpan.FromMilliseconds(850), cancellationToken);
            yield return new AiChatToolUpdate("disk", AiChatToolKind.Query, AiChatToolStatus.Error, "Querying ci-runner-7", Summary: "Timed out");
            await Delay(TimeSpan.FromMilliseconds(170), cancellationToken);
            yield return new AiChatErrorEvent(AiChatSamples.RunnerError);
            yield break;
        }
        var query = lower.Contains("how much") || lower.Contains("week") || lower.Contains("how long");
        await Delay(TimeSpan.FromMilliseconds(query ? 550 : 1000), cancellationToken);
        if (query)
        {
            yield return new AiChatReasoningDelta(AiChatSamples.SecondReasoning);
            yield return new AiChatReasoningEnd(TimeSpan.FromSeconds(2));
            var tool = AiChatSamples.QueryTool();
            yield return new AiChatToolUpdate(tool.Id, tool.Kind, AiChatToolStatus.Running, tool.RunningLabel, Input: tool.Input);
            await Delay(TimeSpan.FromMilliseconds(850), cancellationToken);
            yield return new AiChatToolUpdate(tool.Id, tool.Kind, AiChatToolStatus.Done, tool.DoneLabel, tool.Summary, Output: tool.Output);
            await Delay(TimeSpan.FromMilliseconds(170), cancellationToken);
        }
        else
        {
            yield return new AiChatReasoningDelta("Nothing in this conversation matches; give a general check instead.");
            yield return new AiChatReasoningEnd();
        }
        var text = query ? AiChatSamples.SecondText : AiChatSamples.GenericText;
        // Pieces of uneven size, as a network stream delivers them; the chat paces them into whole words.
        for (var i = 0; i < text.Length;)
        {
            var size = Math.Min(text.Length - i, 12 + (i * 7 % 29));
            yield return new AiChatTextDelta(text.Substring(i, size));
            i += size;
            await Delay(TimeSpan.FromMilliseconds(40), cancellationToken);
        }
    }
}
