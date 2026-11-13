namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="AgentRun"/>: a coding agent adding a rate limit to an upload endpoint of a made-up
/// project. With no <see cref="AgentRun.Steps"/> the run plays these steps live.
/// </summary>
public static class AgentRunSamples
{
    /// <summary>The sample goal.</summary>
    public const string Goal = "Add a rate limit to the public upload endpoint and cover it with tests";

    /// <summary>The sample agent's name.</summary>
    public const string AgentName = "Patchwork";

    /// <summary>One beat of the built-in simulation: the finished step, how long it runs, and what it costs.</summary>
    internal sealed record Beat(Func<AgentRunStep> Create, TimeSpan Duration, decimal Cost, long Tokens, string? Output = null);

    internal static IReadOnlyList<Beat> Script() =>
    [
        new(() => new AgentRunStep
        {
            Kind = AgentRunStepKind.Thinking,
            Title = "Plan the change",
            Text = "Uploads go through one route handler with no limiter in front of it. The project already has a token " +
                   "bucket in the shared middleware, so the smallest change is to reuse it: 20 uploads a minute per API key, " +
                   "a 429 with a Retry-After header past that, and tests for both sides of the limit."
        }, TimeSpan.FromSeconds(2.4), 0.0031m, 1_840),
        new(() => new AgentRunStep
        {
            Kind = AgentRunStepKind.ToolCall,
            Title = "Find the upload route",
            ToolName = "search_code",
            Input = "{ \"query\": \"router.post('/uploads'\", \"path\": \"src/\" }"
        }, TimeSpan.FromSeconds(1.3), 0.0012m, 920,
            "3 matches\nsrc/routes/uploads.ts:18\nsrc/routes/uploads.test.ts:9\nsrc/routes/index.ts:41"),
        new(() => new AgentRunStep
        {
            Kind = AgentRunStepKind.ToolCall,
            Title = "Read the route and the limiter",
            ToolName = "read_files",
            Input = "{ \"paths\": [\"src/routes/uploads.ts\", \"src/middleware/bucket.ts\"] }"
        }, TimeSpan.FromSeconds(1.8), 0.0046m, 3_210,
            "src/routes/uploads.ts · 142 lines\nsrc/middleware/bucket.ts · 64 lines (exports createBucket)"),
        new(() => new AgentRunStep
        {
            Kind = AgentRunStepKind.FileEdit,
            Title = "Add the limiter to the route",
            Files =
            [
                new AgentRunFile("src/middleware/uploadLimit.ts", AgentRunFileChange.Created, 38),
                new AgentRunFile("src/routes/uploads.ts", AgentRunFileChange.Modified, 4, 1),
                new AgentRunFile("src/routes/uploads.test.ts", AgentRunFileChange.Modified, 46)
            ]
        }, TimeSpan.FromSeconds(3.6), 0.0118m, 5_470),
        new(() => new AgentRunStep
        {
            Kind = AgentRunStepKind.ToolCall,
            Title = "Run the upload tests",
            ToolName = "run_command",
            Input = "npm test -- uploads"
        }, TimeSpan.FromSeconds(4.2), 0.0009m, 640,
            "PASS src/routes/uploads.test.ts\n  ✓ accepts 20 uploads in a minute\n  ✓ answers the 21st with 429 and Retry-After\n  ✓ counts each key on its own\n\nTests: 26 passed, 26 total"),
        new(() => new AgentRunStep
        {
            Kind = AgentRunStepKind.Message,
            Title = "Summary",
            Text = "Done. Uploads are now limited to 20 a minute per API key. Past that the route answers 429 with a " +
                   "Retry-After header. The limiter reuses the shared token bucket, and three new tests cover the limit."
        }, TimeSpan.FromSeconds(1.6), 0.0024m, 1_120)
    ];

    /// <summary>
    /// The sample run as finished steps: thinking, three tool calls, a file edit and a closing message, all done. Use it to
    /// show a completed run without the live simulation.
    /// </summary>
    public static IReadOnlyList<AgentRunStep> CompletedSteps()
    {
        var script = Script();
        var steps = new List<AgentRunStep>(script.Count);
        foreach (var beat in script)
        {
            var step = beat.Create();
            step.Status = AgentRunStepStatus.Done;
            step.Duration = beat.Duration;
            if (beat.Output is not null)
            {
                step.Output = beat.Output;
            }
            steps.Add(step);
        }
        return steps;
    }

    /// <summary>The sample run's total cost once finished.</summary>
    public static decimal TotalCost => Script().Sum(b => b.Cost);

    /// <summary>The sample run's total tokens once finished.</summary>
    public static long TotalTokens => Script().Sum(b => b.Tokens);
}
