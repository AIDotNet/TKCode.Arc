using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for the reply parts (spec section 10: the nightly build conversation). Marks are plain shapes drawn
/// here, not any real site's or vendor's logo. Shared by the tests and the snapshot tool.
/// </summary>
internal static class AiReplySamples
{
    public const string Thinking = "Thinking";
    public const string Thought = "Thought for 3s";

    public const string Reasoning =
        "Compare the stage timings before and after the jump. A cache that stopped restoring usually shows up as a slow restore "
        + "step and a long compile, not as a slow test stage.";

    /// <summary>A ring with a small satellite: the sample "Orbit" model mark (coral).</summary>
    public static readonly Geometry OrbitMark = Geometry.Parse("M12 5a7 7 0 1 0 0 14a7 7 0 1 0 0-14z M12 8a4 4 0 1 1 0 8a4 4 0 1 1 0-8z M19.5 2.5a2 2 0 1 0 0.01 0z");

    public static readonly IBrush OrbitBrush = new SolidColorBrush(Color.Parse("#E8714F"));

    private static readonly Geometry Tile = Geometry.Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z");
    private static readonly Geometry Dot = Geometry.Parse("M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z");

    public static ModelMark ModelMark() => new() { Mark = OrbitMark, MarkColor = OrbitBrush, Size = 16 };

    public static AiSource[] Sources() =>
    [
        new AiSource { Index = 1, Title = "Caching dependencies in pipelines", Domain = "docs.example.org", Mark = Tile, MarkBrush = new SolidColorBrush(Color.Parse("#3B6FD8")) },
        new AiSource { Index = 2, Title = "Why our cache hit rate fell to zero", Domain = "blog.example.dev", Mark = Dot, MarkBrush = new SolidColorBrush(Color.Parse("#D9822B")) },
        new AiSource { Index = 3, Title = "Build performance checklist", Domain = "handbook.example.com" },
        new AiSource { Index = 4, Title = "Restore keys and partial cache hits", Domain = "docs.example.org", Mark = Tile, MarkBrush = new SolidColorBrush(Color.Parse("#3B6FD8")) }
    ];

    public static AiChatTool Search(AiChatToolStatus status = AiChatToolStatus.Done)
    {
        var tool = new AiChatTool
        {
            Id = "search-1",
            Kind = AiChatToolKind.Search,
            Status = status,
            RunningLabel = "Searching the docs",
            DoneLabel = "Searched the docs",
            Summary = status == AiChatToolStatus.Done ? "4 sources" : null
        };
        tool.Queries.Add("build cache key changed lock file");
        tool.Queries.Add("restore cache miss slow compile");
        if (status == AiChatToolStatus.Done)
        {
            foreach (var source in Sources())
            {
                tool.Sources.Add(source);
            }
        }
        return tool;
    }

    public static AiChatTool Query(AiChatToolStatus status = AiChatToolStatus.Done) => new()
    {
        Id = "query-1",
        Kind = AiChatToolKind.Query,
        Status = status,
        RunningLabel = status == AiChatToolStatus.Error ? "Querying ci-runner-7" : "Querying build history",
        DoneLabel = "Queried build history",
        Summary = status switch
        {
            AiChatToolStatus.Done => "212 runs",
            AiChatToolStatus.Error => "Timed out",
            _ => null
        },
        Input = status == AiChatToolStatus.Error
            ? "SELECT mount, used_pct FROM disk_usage WHERE host = 'ci-runner-7';"
            : "SELECT avg(restore_ms), avg(compile_ms) FROM runs WHERE started > now() - interval '30 days';",
        Output = status == AiChatToolStatus.Done ? "restore 4.1 s, compile 19.6 min" : null
    };

    public static string SourceLink(int n, string title, string? domain) =>
        string.IsNullOrEmpty(domain) ? $"Source {n}: {title}" : $"Source {n}: {title}, {domain}";
}
