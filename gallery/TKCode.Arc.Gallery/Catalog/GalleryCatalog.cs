namespace TKCode.Arc.Gallery.Catalog;

/// <param name="IsArc">True for Arc components (uiarc.dev originals); false for TKCode extras such as Markdown.</param>
/// <param name="ArcPath">The page under uiarc.dev/components/ when it is not the slug (blocks live under <c>blocks/</c>).</param>
public sealed record GalleryEntry(string ClassName, string Title, string Slug, int Phase, string Description, bool IsArc = true,
    string? ArcPath = null)
{
    /// <summary>The Arc original on uiarc.dev; null for extras, which have none.</summary>
    public string? ArcUrl => IsArc ? $"https://uiarc.dev/components/{ArcPath ?? Slug}" : null;
}

/// <param name="IsArc">True for Arc's sidebar groups; false for the TKCode extras group.</param>
/// <param name="Subcategories">Groups nested under this one (Blocks); null for a flat group.</param>
public sealed record GalleryCategory(string Code, string Title, string ChineseTitle, GalleryEntry[] Entries, bool IsArc = true,
    GalleryCategory[]? Subcategories = null)
{
    /// <summary>The entries of this group and of all its subgroups.</summary>
    public IEnumerable<GalleryEntry> AllEntries => Entries.Concat((Subcategories ?? Array.Empty<GalleryCategory>()).SelectMany(s => s.AllEntries));
}

public static partial class GalleryCatalog
{
    /// <summary>
    /// TKCode components with no Arc original, listed after Arc's groups. They are not part of the 110 Arc components
    /// (<see cref="Categories"/> / <see cref="Count"/>).
    /// </summary>
    public static readonly GalleryCategory[] Extras =
    [
        new("AdvancedCharts", "Advanced charts", "高级图表",
        [
            new("DataGrid", "Data grid", "data-grid", 0,
                "A spreadsheet grid with range selection, inline editing, a fill handle, clipboard, undo, sorting, filters, grouping, totals and export.",
                IsArc: false),
        ], IsArc: false),
        new("Workflow", "Workflow", "工作流",
        [
            new("WorkflowCanvas", "Workflow canvas", "workflow-canvas", 0,
                "Show a workflow on a dotted canvas and play its runs: each step lights up in turn, the data draws along the edges, and every step reports its result and time.",
                IsArc: false),
        ], IsArc: false),
        new("Content", "Extras", "扩展",
        [
            new("Markdown", "Markdown", "markdown", 0,
                "Render Markdown documents and streamed replies in Arc's style, with fenced code highlighted in Arc's syntax palette.",
                IsArc: false),
            new("TokenTextEditor", "Token text editor", "token-text-editor", 0,
                "A multi-line text editor whose tokens, such as commands and mentions, behave as single units, with triggers that open a query for your own list.",
                IsArc: false),
        ], IsArc: false),
    ];

    /// <summary>
    /// The groups of the Special components section, listed after Blocks as on uiarc.dev. Their pages live under
    /// <c>/components/</c> like any component, but they are not part of the 110 Arc components (<see cref="Count"/>).
    /// </summary>
    public static readonly GalleryCategory[] Special =
    [
        new("Ai", "AI", "AI",
        [
            new("AgentStatus", "Agent status", "agent-status", 0,
                "A live status pill for a running agent: its mark in a spinning ring, the current step and a timer, opening into the recent steps with pause, stop and approval controls.",
                IsArc: false),
            new("ToolCall", "Tool call", "tool-call", 0,
                "A tool call row for an agent transcript: the tool, what it did and how it went, opening into the call's input and output, with approval and retry in place.",
                IsArc: false),
            new("AgentPlan", "Agent plan", "agent-plan", 0,
                "An agent's plan as an editable checklist: reorder, rename and add steps while it is a draft, then follow the run step by step once it is approved.",
                IsArc: false),
            new("AgentTimeline", "Agent timeline", "agent-timeline", 0,
                "A replay of a multi-agent run on one time axis: each agent's thinking, tool calls and waiting, the handoffs between them, and the time, tokens and cost.",
                IsArc: false),
            new("ContextWindow", "Context window", "context-window", 0,
                "A model's context window as a grid of 200 cells that fills part by part, with a compaction threshold, a legend and actions that show what still fits.",
                IsArc: false),
        ], IsArc: false),
        new("Illustrations", "Illustrations", "插图",
        [
            new("DataFlow", "Data flow", "data-flow", 0,
                "Events stream from their sources through a queue and a batch transform into a warehouse and live dashboards.",
                IsArc: false),
            new("PaymentFlow", "Payment flow", "payment-flow", 0,
                "A card payment from checkout through tokenization, authorization and the issuer's decision to settlement.",
                IsArc: false),
            new("MultiRegionFailover", "Multi-region failover", "multi-region-failover", 0,
                "A global load balancer shifts traffic to a standby region and promotes its database when the primary region goes down.",
                IsArc: false),
            new("VectorSearch", "Vector search", "vector-search", 0,
                "A question is embedded as a vector and answered with its nearest documents by cosine similarity.",
                IsArc: false),
            new("AgentLoop", "Agent loop", "agent-loop", 0,
                "An agent gathers context, calls tools and checks the result, going round the loop until the task is done.",
                IsArc: false),
            new("MixtureOfExperts", "Mixture of experts", "mixture-of-experts", 0,
                "A router scores every expert for each token and sends it to its top choices, whose outputs blend into the result.",
                IsArc: false),
            new("SpeculativeDecoding", "Speculative decoding", "speculative-decoding", 0,
                "A small draft model guesses a few tokens ahead and the large model checks them in one pass, keeping the ones it agrees with.",
                IsArc: false),
            new("PromptCache", "Prompt cache", "prompt-cache", 0,
                "Requests that share a prompt prefix read it from the cache instead of processing it again, and the savings add up.",
                IsArc: false),
            new("ReasoningTree", "Reasoning tree", "reasoning-tree", 0,
                "A search over candidate thoughts: each is scored, weak branches are pruned, the best expand and the winning path draws in.",
                IsArc: false),
        ], IsArc: false),
    ];

    /// <summary>
    /// The Blocks group under Components, with its subgroups and blocks from the TKCode.Arc.Blocks catalog. Not part of
    /// the 110 Arc components (<see cref="Categories"/> / <see cref="Count"/>). Ports of free Arc blocks link to their
    /// uiarc.dev page; original blocks have none.
    /// </summary>
    public static readonly GalleryCategory Blocks = new("Blocks", "Blocks", "区块", [],
        Subcategories: BlockCatalog.Groups.Select(g => new GalleryCategory(g.Code, g.Title, g.ChineseTitle,
            BlockCatalog.Blocks.Where(b => b.Group == g.Code)
                .Select(b => new GalleryEntry(b.ClassName, b.Title, b.Slug, 0, b.Description, IsArc: b.IsArcPort, ArcPath: $"blocks/{b.Slug}"))
                .ToArray())).ToArray());

    /// <summary>
    /// The Docs section of the sidebar: the gallery's own documentation pages, listed first, as on uiarc.dev. Not Arc
    /// components, and their pages are planned.
    /// </summary>
    public static readonly GalleryCategory Docs = new("Docs", "Docs", "文档",
    [
        new("DocsIntroduction", "Introduction", "introduction", 0, "What Arc is and how the gallery is organized.", IsArc: false),
        new("DocsInstallation", "Installation", "installation", 0, "Add the Arc packages to a project.", IsArc: false),
        new("DocsTheming", "Theming", "theming", 0, "Themes, design tokens and appearance.", IsArc: false),
        new("DocsMotion", "Motion", "motion", 0, "The motion engine and its presets.", IsArc: false),
        new("DocsAiAndMcp", "AI and MCP", "ai-and-mcp", 0, "Use Arc with AI assistants: llms.txt, Markdown pages, an agent skill and MCP.", IsArc: false),
        new("DocsChangelog", "Changelog", "changelog", 0, "Release notes.", IsArc: false),
    ], IsArc: false);

    /// <summary>The advanced charts group (TKCode.Arc.AdvancedCharts); listed after Charts, but not part of the 110 Arc components.</summary>
    public static GalleryCategory AdvancedCharts => Extras[0];

    /// <summary>The workflow group (TKCode.Arc.Workflow); listed after Advanced charts, not part of the 110 Arc components.</summary>
    public static GalleryCategory Workflow => Extras[1];

    /// <summary>The Components section of the sidebar: Arc's groups with Advanced charts and Workflow after Charts, then the other extras.</summary>
    public static IEnumerable<GalleryCategory> AllCategories =>
        Categories.SelectMany(c => c.Code == "Charts" ? new[] { c, AdvancedCharts, Workflow } : new[] { c })
                  .Concat(Extras.Where(e => !ReferenceEquals(e, AdvancedCharts) && !ReferenceEquals(e, Workflow)));

    /// <summary>Every entry the gallery lists: the Docs, Components, Blocks and Special components sections.</summary>
    public static IEnumerable<GalleryEntry> AllEntries =>
        Docs.AllEntries.Concat(AllCategories.SelectMany(c => c.AllEntries)).Concat(Blocks.AllEntries).Concat(Special.SelectMany(c => c.AllEntries));

    /// <summary>Every entry of the Components section: Arc's components and the extras.</summary>
    public static IEnumerable<GalleryEntry> ComponentEntries => AllCategories.SelectMany(c => c.Entries);

    /// <summary>Number of Arc components (extras not included).</summary>
    public static int Count => Categories.Sum(c => c.Entries.Length);

    /// <summary>Number the Components section lists: the Arc components and the extras.</summary>
    public static int ComponentCount => ComponentEntries.Count();
}
