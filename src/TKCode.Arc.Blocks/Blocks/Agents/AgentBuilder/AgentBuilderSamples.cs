using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="AgentBuilder"/>: a support agent called Harbor for a made-up bike shop, three
/// made-up models from two made-up providers, five tools and three knowledge sources (one that failed). An empty builder
/// shows it until you give it your own data (<see cref="AgentBuilder.ShowsSampleData"/>).
/// </summary>
public static class AgentBuilderSamples
{
    /// <summary>The sample agent's name.</summary>
    public const string Name = "Harbor";

    /// <summary>The sample agent's description.</summary>
    public const string Description = "Answers order and repair questions for Tidewater Cycles";

    /// <summary>The sample agent's instructions.</summary>
    public const string Instructions =
        "You are Harbor, the support agent of Tidewater Cycles, a small bike shop with an online store.\n\n"
        + "- Answer questions about orders, returns, repairs and bike fitting.\n"
        + "- Look things up in the knowledge sources before you answer, and say which one you used.\n"
        + "- Keep replies short and friendly. Use lists for steps.\n"
        + "- Never promise a refund or a repair date; offer to hand the case to the team instead.";

    /// <summary>The id of the sample agent's model.</summary>
    public const string ModelId = "lumen-pro-2";

    /// <summary>Two invented providers with simple geometric marks.</summary>
    public static IReadOnlyList<AiModelProvider> Providers() =>
    [
        new AiModelProvider { Id = "lumen", Name = "Lumen Works", Mark = Geometry.Parse("M12 2A10 10 0 1 1 11.99 2Z M12 6A6 6 0 1 0 12.01 6Z") },
        new AiModelProvider { Id = "quill", Name = "Quill AI", Mark = Geometry.Parse("M4 20L12 4L20 20Z M12 10L8.5 17H15.5Z") }
    ];

    /// <summary>Three invented models.</summary>
    public static IReadOnlyList<AiModel> Models() =>
    [
        new AiModel
        {
            Id = "lumen-pro-2", Name = "Lumen Pro 2", ProviderId = "lumen", Blurb = "Careful answers with tool use",
            Description = "A balanced model that follows long instructions and uses tools well.", ContextTokens = 400_000
        },
        new AiModel
        {
            Id = "lumen-lite-2", Name = "Lumen Lite 2", ProviderId = "lumen", Blurb = "Fast and low cost", Tag = "Fast",
            Description = "A small model for quick, short replies.", ContextTokens = 200_000
        },
        new AiModel
        {
            Id = "quill-deep-1", Name = "Quill Deep 1", ProviderId = "quill", Blurb = "Thinks longer on hard questions",
            Description = "A reasoning model for multi-step questions; slower and more thorough.", ContextTokens = 300_000
        }
    ];

    /// <summary>Five tools; knowledge search, order lookup and hand-off are on.</summary>
    public static IReadOnlyList<AgentBuilderTool> Tools() =>
    [
        new AgentBuilderTool
        {
            Id = "orders", Name = "Order lookup", Glyph = ArcIcons.Package, IsOn = true, RunningLabel = "Looking up the order",
            Description = "Reads order status and tracking by order number."
        },
        new AgentBuilderTool
        {
            Id = "handoff", Name = "Hand off to a person", Glyph = ArcIcons.Users, IsOn = true, RunningLabel = "Opening a case",
            Description = "Opens a case for the support team with the chat attached."
        },
        new AgentBuilderTool
        {
            Id = "web", Name = "Web search", Glyph = ArcIcons.Globe, RunningLabel = "Searching the web",
            Description = "Searches the public web for things the sources do not cover."
        },
        new AgentBuilderTool
        {
            Id = "calendar", Name = "Book a repair slot", Glyph = ArcIcons.CalendarCheck, RunningLabel = "Checking the repair calendar",
            Description = "Finds and holds open slots in the workshop calendar."
        },
        new AgentBuilderTool
        {
            Id = "code", Name = "Run code", Glyph = ArcIcons.Terminal, RunningLabel = "Running code",
            Description = "Runs short scripts to work out sizes, prices and dates."
        }
    ];

    /// <summary>Three sources: a ready file, a ready website and a file whose indexing failed.</summary>
    public static IReadOnlyList<AgentBuilderSource> Sources() =>
    [
        new AgentBuilderSource
        {
            Id = "returns", Name = "returns-and-repairs.pdf", Kind = AgentBuilderSourceKind.File, Size = 412_000,
            Status = AgentBuilderSourceStatus.Ready, Progress = 1, ChunkCount = 86
        },
        new AgentBuilderSource
        {
            Id = "help", Name = "help.tidewater.example/fitting", Kind = AgentBuilderSourceKind.Website,
            Url = new Uri("https://help.tidewater.example/fitting"), Status = AgentBuilderSourceStatus.Ready, Progress = 1, ChunkCount = 34
        },
        new AgentBuilderSource
        {
            Id = "prices", Name = "workshop-prices-2026.xlsx", Kind = AgentBuilderSourceKind.File, Size = 58_000,
            Status = AgentBuilderSourceStatus.Failed, ErrorMessage = "The file is password protected."
        }
    ];

    /// <summary>Three prompts for the empty test chat.</summary>
    public static IReadOnlyList<AiChatSuggestion> Suggestions() =>
    [
        new AiChatSuggestion { Label = "Where is order 4417?", Icon = ArcIcons.Package },
        new AiChatSuggestion { Label = "How do I return a helmet?", Icon = ArcIcons.RotateCcw },
        new AiChatSuggestion { Label = "Can you book a brake service?", Icon = ArcIcons.Wrench }
    ];
}

/// <summary>
/// The built-in test chat responder of <see cref="AgentBuilder"/>: no model is called. It shows how the settings shape a
/// reply: it searches the ready knowledge sources, runs a tool that is on when the prompt asks for it, and answers with
/// a short summary of the agent's name, model, tools and sources. Replace it with <see cref="AgentBuilder.Responder"/>.
/// </summary>
public sealed class AgentBuilderSimulatedResponder : IAgentBuilderResponder
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <summary>Creates the responder.</summary>
    /// <param name="delay">The wait between steps; null uses <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    public AgentBuilderSimulatedResponder(Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _delay = delay ?? Task.Delay;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AgentBuilderTestRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = request.Config;
        var prompt = request.Chat.Messages.Count > 0 ? request.Chat.Messages[^1].Content : string.Empty;
        var name = string.IsNullOrWhiteSpace(config.Name) ? "The agent" : config.Name.Trim();

        await _delay(TimeSpan.FromMilliseconds(450), cancellationToken);
        yield return new AiChatReasoningDelta(FirstLine(config.Instructions) is { Length: > 0 } rule
            ? $"Follow the instructions: \"{rule}\" Then check what the question needs."
            : "There are no instructions yet, so answer plainly and briefly.");
        await _delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        yield return new AiChatReasoningEnd();

        var ready = config.Sources.Where(s => s.Status == AgentBuilderSourceStatus.Ready).ToList();
        IReadOnlyList<AiSource> sources = [];
        if (ready.Count > 0)
        {
            yield return new AiChatToolUpdate("knowledge", AiChatToolKind.Search, AiChatToolStatus.Running, "Searching knowledge");
            await _delay(TimeSpan.FromMilliseconds(700), cancellationToken);
            sources = ready.Take(3).Select((s, i) => new AiSource
            {
                Index = i + 1,
                Title = s.Name,
                Domain = s.Url?.Host ?? "Uploaded file",
                Url = s.Url,
                Mark = s.Kind == AgentBuilderSourceKind.Website ? ArcIcons.Globe : ArcIcons.FileText
            }).ToList();
            yield return new AiChatToolUpdate("knowledge", AiChatToolKind.Search, AiChatToolStatus.Done, "Searched knowledge",
                sources.Count == 1 ? "1 source" : $"{sources.Count} sources", Queries: [Shorten(prompt, 60)], Sources: sources);
            yield return new AiChatSourcesEvent(sources);
        }

        var tool = PickTool(prompt, request.Tools);
        if (tool is not null)
        {
            var running = tool.RunningLabel ?? tool.Name;
            yield return new AiChatToolUpdate("tool-" + tool.Id, AiChatToolKind.Custom, AiChatToolStatus.Running, running);
            await _delay(TimeSpan.FromMilliseconds(650), cancellationToken);
            yield return new AiChatToolUpdate("tool-" + tool.Id, AiChatToolKind.Custom, AiChatToolStatus.Done, tool.Name, "Simulated");
        }

        var text = new StringBuilder();
        text.Append("**").Append(name).Append("** would answer this with the settings below. This is a simulated reply; connect a responder to try the real agent.\n\n");
        text.Append("- **Model:** ").Append(request.Model?.Name ?? "none chosen").Append('\n');
        var tools = request.Tools.Select(t => t.Name).ToList();
        text.Append("- **Tools:** ").Append(tools.Count == 0 ? "none" : string.Join(", ", tools)).Append('\n');
        text.Append("- **Knowledge:** ").Append(ready.Count == 0 ? "no ready sources" : ready.Count == 1 ? "1 ready source" : $"{ready.Count} ready sources");
        text.Append(sources.Count > 0 ? " [1]\n" : "\n");
        if (tool is not null)
        {
            text.Append("\nYour question matched **").Append(tool.Name).Append("**, so the agent would call it before answering.");
        }
        foreach (var chunk in Chunks(text.ToString(), 18))
        {
            await _delay(TimeSpan.FromMilliseconds(35), cancellationToken);
            yield return new AiChatTextDelta(chunk);
        }
    }

    /// <summary>The tool a prompt seems to ask for, among the tools that are on.</summary>
    internal static AgentBuilderTool? PickTool(string prompt, IReadOnlyList<AgentBuilderTool> tools)
    {
        foreach (var tool in tools)
        {
            var words = tool.Id switch
            {
                "orders" => new[] { "order", "tracking", "parcel", "delivery" },
                "handoff" => ["person", "human", "agent", "complaint", "refund"],
                "web" => ["latest", "news", "search", "today", "web"],
                "calendar" => ["book", "slot", "appointment", "schedule", "tomorrow"],
                "code" => ["calculate", "compute", "how many", "convert"],
                _ => (tool.Name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            };
            if (words.Any(w => w.Length > 2 && prompt.Contains(w, StringComparison.OrdinalIgnoreCase)))
            {
                return tool;
            }
        }
        return null;
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        return Shorten(line, 110);
    }

    private static string Shorten(string text, int length) =>
        text.Length <= length ? text : string.Concat(text.AsSpan(0, length - 1).TrimEnd(), "…");

    private static IEnumerable<string> Chunks(string text, int size)
    {
        for (var i = 0; i < text.Length; i += size)
        {
            yield return text.Substring(i, Math.Min(size, text.Length - i));
        }
    }
}
