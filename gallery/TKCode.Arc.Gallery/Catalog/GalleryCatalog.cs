namespace TKCode.Arc.Gallery.Catalog;

/// <param name="IsArc">True for Arc components (uiarc.dev originals); false for TKCode extras such as Markdown.</param>
/// <param name="ArcPath">The page under uiarc.dev/components/ when it is not the slug (blocks live under <c>blocks/</c>).</param>
/// <param name="AddedOn">ISO date (yyyy-MM-dd) the component joined the gallery; null for components that are not new.</param>
public sealed record GalleryEntry(string ClassName, string Title, string Slug, int Phase, string Description, bool IsArc = true,
    string? ArcPath = null, string? AddedOn = null)
{
    /// <summary>True while the entry carries a date it was added on and <see cref="GalleryCatalog.NewWindowDays"/> have not passed since (see <see cref="IsNewOn"/>).</summary>
    public bool IsNew => IsNewOn(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>Whether the entry counts as new on <paramref name="today"/>: added within the last <see cref="GalleryCatalog.NewWindowDays"/> days (never before its date).</summary>
    public bool IsNewOn(DateOnly today) =>
        AddedOn is not null
        && DateOnly.TryParseExact(AddedOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var added)
        && today >= added && today.DayNumber - added.DayNumber < GalleryCatalog.NewWindowDays;

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
    /// <summary>How many days after <see cref="GalleryEntry.AddedOn"/> an entry keeps its "New" badge.</summary>
    public const int NewWindowDays = 60;

    /// <summary>
    /// The dates the Blocks and Mobile entries joined the gallery (<see cref="GalleryEntry.AddedOn"/>): their catalogs
    /// (<see cref="BlockCatalog"/>, <see cref="MobileCatalog"/>) carry none. Declared before <see cref="Blocks"/> and
    /// <see cref="Mobile"/>, which read it while they initialize.
    /// </summary>
    private static readonly Dictionary<string, string> s_addedOn = new (string Date, string[] Classes)[]
    {
        ("2026-10-21", ["Sidebar", "PageHeader", "CommandPalette", "NotificationCenter", "EmptyStates", "SearchResults",
            "SettingsPage", "SettingsCommand", "SecuritySettings", "BillingOverview", "UsageBilling", "CancelFlow"]),
        ("2026-10-23", ["ModelPicker", "AiSidePanel", "PromptStudio", "DocumentExtraction", "ModelArena", "AiAnswer", "AiChat",
            "AiComposer", "AgentMemory", "ActivityTerrain", "RevenueGlobe", "CustomerGalaxy", "SemanticZoom", "LayoutMorph",
            "ExplodedView", "WeekCalendar", "AvailabilityPicker", "ProjectBoard", "ReleaseReadiness", "KanbanBoard", "SiteHeader",
            "SiteFooter", "CtaSection", "NewsletterSignup"]),
        ("2026-10-24", ["Login", "OnboardingIllustration", "OtpInput", "MultiStepForm"]),
        ("2026-10-25", ["MobilePage", "MobileNavbar", "MobileTabbar", "MobileButton", "MobileSwitch"]),
        ("2026-10-26", ["MetricsDashboard", "KpiDrilldown", "MetricExplorer", "RevenueExplorer", "MrrWaterfall", "CohortRetention",
            "UsageForecast", "JourneyFlow", "UptimeStatus", "ChoroplethExplorer", "ConnectedScatter", "HorizonChart",
            "MarketTerminal", "BudgetVariance", "SmallMultiples", "LineReplay", "TeamMembers", "InvitePeople", "TeamDirectory",
            "RolesPermissions", "ProfilePage", "ApiKeys", "Webhooks", "Integrations", "EnvVariables",
            "MobileToolbar", "MobileNavigationStack", "MobileListGroup", "MobilePullToRefresh", "MobileDialog", "MobileToast",
            "MobileSheet", "MobileActionSheet", "MobileTextField", "MobileSearchbar", "MobileCheckbox", "MobileRadio",
            "MobileIconButton", "MobileSegmented"]),
        ("2026-10-27", ["FileUpload", "MediaPlayer"]),
        ("2026-10-29", ["DomainVerify", "WebhookTester"]),
    }.SelectMany(g => g.Classes.Select(c => (c, g.Date))).ToDictionary(e => e.c, e => e.Date);

    private static string? AddedOnOf(string className) => s_addedOn.GetValueOrDefault(className);

    /// <summary>
    /// TKCode components with no Arc original, listed after Arc's groups. They are not part of the 112 Arc components
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
                IsArc: false, AddedOn: "2026-10-24"),
        ], IsArc: false),
        new("Content", "Extras", "扩展",
        [
            new("Markdown", "Markdown", "markdown", 0,
                "Render Markdown documents and streamed replies in Arc's style, with fenced code highlighted in Arc's syntax palette.",
                IsArc: false),
            new("TokenTextEditor", "Token text editor", "token-text-editor", 0,
                "A multi-line text editor whose tokens, such as commands and mentions, behave as single units, with triggers that open a query for your own list.",
                IsArc: false),
            new("Cascader", "Cascader", "cascader", 0,
                "Pick one node of a hierarchy level by level, such as province, city and district, with a field and menu that match Select.",
                IsArc: false, AddedOn: "2026-10-28"),
        ], IsArc: false),
    ];

    /// <summary>
    /// The groups of the Special components section, listed after Blocks as on uiarc.dev. Their pages live under
    /// <c>/components/</c> like any component, but they are not part of the 112 Arc components (<see cref="Count"/>).
    /// </summary>
    public static readonly GalleryCategory[] Special =
    [
        new("Playful", "Playful", "趣味",
        [
            new("StickerPeel", "Sticker peel", "sticker-peel", 0,
                "Stickers on a surface that lift a corner on hover, fold back along a real crease when pulled, and stick down where you drop them.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("PageCurl", "Page curl", "page-curl", 0,
                "A small book that opens on its cover: tap or drag a page corner and it curls over along a fold that follows the pointer.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("OrbitLogos", "Orbit logos", "orbit-logos", 0,
                "An integrations orbit: your mark at the center, logos on two slowly turning rings, and a detail card that morphs out of the chosen logo.",
                IsArc: false, AddedOn: "2026-10-29"),
        ], IsArc: false),
        new("Cursor", "Cursor", "光标",
        [
            new("DotGrid", "Dot grid", "dot-grid", 0,
                "A field of dots that swells away from the pointer like a lens and sends a ripple out from every click.",
                IsArc: false, AddedOn: "2026-10-27"),
            new("GravityWell", "Gravity well", "gravity-well", 0,
                "Tiles that lean toward the pointer, shrink and dim as if pulled into a well, with a click that rolls a shockwave through the grid.",
                IsArc: false, AddedOn: "2026-10-27"),
            new("InkFluid", "Ink fluid", "ink-fluid", 0,
                "A fluid simulation behind your content: the pointer stirs soft ink that swirls and fades, and a slow stir keeps it moving when nobody does.",
                IsArc: false, AddedOn: "2026-10-27"),
        ], IsArc: false),
        new("Ai", "AI", "AI",
        [
            new("AgentStatus", "Agent status", "agent-status", 0,
                "A live status pill for a running agent: its mark in a spinning ring, the current step and a timer, opening into the recent steps with pause, stop and approval controls.",
                IsArc: false, AddedOn: "2026-10-23"),
            new("ToolCall", "Tool call", "tool-call", 0,
                "A tool call row for an agent transcript: the tool, what it did and how it went, opening into the call's input and output, with approval and retry in place.",
                IsArc: false, AddedOn: "2026-10-23"),
            new("AgentPlan", "Agent plan", "agent-plan", 0,
                "An agent's plan as an editable checklist: reorder, rename and add steps while it is a draft, then follow the run step by step once it is approved.",
                IsArc: false, AddedOn: "2026-10-23"),
            new("AgentTimeline", "Agent timeline", "agent-timeline", 0,
                "A replay of a multi-agent run on one time axis: each agent's thinking, tool calls and waiting, the handoffs between them, and the time, tokens and cost.",
                IsArc: false, AddedOn: "2026-10-23"),
            new("ContextWindow", "Context window", "context-window", 0,
                "A model's context window as a grid of 200 cells that fills part by part, with a compaction threshold, a legend and actions that show what still fits.",
                IsArc: false, AddedOn: "2026-10-23"),
            new("ReasoningBlock", "Reasoning block", "reasoning-block", 0,
                "The collapsible thinking block of an AI reply: a shimmering label that counts the seconds, the latest reasoning streaming underneath, and the full reasoning as calm steps once it has finished.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("SourceCards", "Source cards", "source-cards", 0, "Cited sources under an AI answer: cards stream into reserved slots while the model searches, the rest collapse into a stack of site marks, and inline [n] citations highlight their card.", IsArc: false, AddedOn: "2026-10-29"),
            new("ModelCard", "Model card", "model-card", 0, "A model details card where switching models rolls every price, limit and score in place, a volume slider estimates the monthly cost, benchmark bars show each model's rank, and Compare crossfades to a side by side table against any rival.", IsArc: false, AddedOn: "2026-10-29"),
            new("AiWorkflow", "AI workflow", "ai-workflow", 0, "An automation card where a trigger, an AI step, a branch and actions sit on one rail, a test run glides down it opening each step's output, and steps are added from a compact command menu.", IsArc: false, AddedOn: "2026-10-29"),
            new("CodeSuggestion", "Code suggestion", "code-suggestion", 0, "An AI code edit suggestion where the proposed changes stream into a small editor panel as inline diff hunks, and the person accepts or rejects each one, or all at once with Cmd or Ctrl + Enter.", IsArc: false, AddedOn: "2026-10-29"),
            new("GhostText", "Ghost text", "ghost-text", 0, "An inline AI autocomplete where muted ghost text continues the sentence at the caret: Tab accepts it in place, Cmd+Right takes one word, Escape dismisses it, and a calm chip shows the model behind it.", IsArc: false, AddedOn: "2026-10-29"),
            new("ImageGeneration", "Image generation", "image-generation", 0, "An image generation card where four images resolve as a pixel mosaic, from coarse colour blocks to full detail, each with its own progress ring; pick one to upscale, vary or download.", IsArc: false, AddedOn: "2026-10-29"),
            new("DictationButton", "Dictation button", "dictation-button", 0, "A chat composer with voice input: the mic springs into a recording pill with a live waveform, a timer, cancel and insert, and words stream into the field as you speak, muted until the recognizer settles them.", IsArc: false, AddedOn: "2026-10-29"),
            new("SpeechPlayer", "Speech player", "speech-player", 0,
                "A read-aloud player for AI replies: Read aloud turns into a compact player with play, a scrubbable progress bar, speed and a voice picker, and a highlight follows the spoken word through the reply.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("MeetingSummary", "Meeting summary", "meeting-summary", 0, "AI notes for a call: the platform, attendees and length, then Summary, Decisions and Action items that stream in, each line linked to its transcript quote and each task ready to check off or send to a tracker.", IsArc: false, AddedOn: "2026-10-29"),
            new("ReplyDraft", "Reply draft", "reply-draft", 0, "An AI reply drafter under a received email: quick replies steer a fresh streaming draft, tone and length rewrite it with one crossfade, and the reader edits it before Insert.", IsArc: false, AddedOn: "2026-10-29"),
            new("AgentHandoff", "Agent handoff", "agent-handoff", 0, "A support chat where an AI assistant hands off to a person: a card shows the summary it passes to the team and the typical wait, then the person joins, with a thin timeline marking each step.", IsArc: false, AddedOn: "2026-10-29"),
            new("PiiRedaction", "PII redaction", "pii-redaction", 0, "A guardrail card where names, emails, phone numbers and cards are swapped for placeholders before the prompt reaches the model, and the reply comes back with the values put back.", IsArc: false, AddedOn: "2026-10-29"),
            new("ResponseFeedback", "Response feedback", "response-feedback", 0, "A feedback control for AI replies: thumbs down opens an inline panel with reason chips and a note, thumbs up asks what was good, and a submit shows a short thank-you before folding back into the row.", IsArc: false, AddedOn: "2026-10-29"),
            new("LlmCostCalculator", "LLM cost calculator", "llm-cost-calculator", 0,
                "A pricing calculator for LLM workloads: set tokens per request, requests per day and prompt caching, and the models re-rank by monthly cost as bars glide and prices roll.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("ProviderKeys", "Provider keys", "provider-keys", 0,
                "A settings card for bring-your-own-key AI products: each provider's masked key, status and last use, with reveal, copy, rotate and remove with undo, and an inline form that tests a new key before saving.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("GenerationStats", "Generation stats", "generation-stats", 0, "A live stats strip for a streaming model response: time to first token, speed with a sparkline, output tokens and cost count up, then settle and open into a latency and token cost breakdown.", IsArc: false, AddedOn: "2026-10-29"),
            new("AiTranslate", "AI translate", "ai-translate", 0,
                "A message card where picking a language streams the AI translation in place, sentence by sentence over the dimmed original, with a detected-language chip, a Show original toggle and a status line.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("TldrToggle", "TL;DR toggle", "tldr-toggle", 0, "An article card that streams an AI summary or key points in place of the article, with a length slider and key points that jump back to their paragraphs.", IsArc: false, AddedOn: "2026-10-29"),
            new("ConnectorSetup", "Connector setup", "connector-setup", 0, "A \"Connect your tools\" card for an AI assistant: each app connects inline through redirect, authorize and a first sync with a progress hairline, then lists the scopes it was granted and offers Disconnect with an inline confirmation.", IsArc: false, AddedOn: "2026-10-29"),
            new("SmartFormFill", "Smart form fill", "smart-form-fill", 0, "A paste-to-fill form card: AI reads an email signature or messy notes and fills a contact form field by field, with a confidence dot per value, its source lit up in the text, review for guesses and Undo.", IsArc: false, AddedOn: "2026-10-29"),
            new("EnrichColumn", "Enrich column", "enrich-column", 0,
                "An AI enrichment column for a data table, where one prompt runs down every row and each cell shimmers while it works, then settles into a value with its source or a failure you can retry.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("TicketRouter", "Ticket router", "ticket-router", 0, "Support messages arrive one at a time, an AI classifier labels intent, sentiment and confidence, and each ticket travels into a team lane with an assignee or waits in a review lane for one click.", IsArc: false, AddedOn: "2026-10-29"),
            new("AskYourData", "Ask your data", "ask-your-data", 0, "A natural language to chart card: the question goes in, the SQL streams into a collapsible, syntax tinted panel, the query runs with a rolling row count, and the answer builds as stacked bars, lines or a table with a one line insight underneath.", IsArc: false, AddedOn: "2026-10-29"),
            new("AgentCursor", "Agent cursor", "agent-cursor", 0, "A live document where an AI agent edits like a collaborator: its labeled cursor selects, rewrites and fixes text while a teammate reads along, and the changes stay tinted until someone accepts or undoes them.", IsArc: false, AddedOn: "2026-10-29"),
            new("ProviderStatus", "Provider status", "provider-status", 0, "A status card for the AI providers your product depends on: health as an icon and label, median latency and a 30 day uptime bar with a tooltip per day, incidents that open into their updates, and a visible countdown to the next check.", IsArc: false, AddedOn: "2026-10-29"),
            new("TrainingRun", "Training run", "training-run", 0,
                "A fine-tuning job card where a live train and validation loss curve draws across a fixed step axis that doubles as the epoch progress bar, with the learning rate, elapsed time, ETA, recent events, pause and cancel, and results with Deploy.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("DesignToCode", "Design to code", "design-to-code", 0, "A screenshot-to-code card: a scan line finds the layout regions of a dropped screenshot or design file frame, then code streams in beside it, linked region by region, with a live preview one tab away.", IsArc: false, AddedOn: "2026-10-29"),
            new("AiCommandPalette", "AI command", "ai-command", 0,
                "A Cmd+K command palette with an AI mode: search pages and actions, or press Tab to ask, and the answer streams in place with numbered sources and next steps that run from the list.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("StarterPrompts", "Starter prompts", "starter-prompts", 0, "A greeting and starter cards for the empty state of an AI app, with category tabs, a shuffle that crossfades the cards, and a composer that a picked card fills.", IsArc: false, AddedOn: "2026-10-29"),
            new("AiMascot", "AI mascot", "ai-mascot", 0, "A small assistant character that shows what the AI is doing: it breathes when idle, leans in to listen, watches its dot orbit while thinking, reads along as it writes, squints when done and shakes once on an error.", IsArc: false, AddedOn: "2026-10-29"),
            new("ImageReveal", "Image reveal", "image-reveal", 0, "An image generation canvas where a soft, blurred preview made from the image's own colors sharpens top to bottom behind a soft-edged band, with fine film grain, a translucent progress pill and calm queued and error states.", IsArc: false, AddedOn: "2026-10-29"),
            new("AiSpark", "AI spark", "ai-spark", 0, "An animated spark mark for AI status: four tapered rays and four lighter diagonals breathe at rest, ripple while thinking, pulse like a heartbeat while writing, then gather and settle when done.", IsArc: false, AddedOn: "2026-10-29"),
            new("SketchToImage", "Sketch to image", "sketch-to-image", 0, "An image generation visual that paints the way an artist does: pencil strokes sketch the scene, a round brush blocks in flat colors region by region, grain and warm light refine it, and it resolves into the finished picture.", IsArc: false, AddedOn: "2026-10-29"),
            new("BuildingBlocks", "Building blocks", "building-blocks", 0, "A loading visual for AI app builders: rounded blocks snap into a tiny app layout with no bounce, fill with content and pick up the accent while the label steps from Laying out to Ready.", IsArc: false, AddedOn: "2026-10-29"),
            new("PixelMascot", "Pixel mascot", "pixel-mascot", 0, "A pixel-art robot that shows what an AI assistant is doing. It idles, thinks, types, celebrates and errors in frame-stepped sprite animations, and stays crisp at every integer size from 24px to 192px.", IsArc: false, AddedOn: "2026-10-29"),
            new("GhostMascot", "Ghost mascot", "ghost-mascot", 0, "A floating ghost mascot whose hem ripples while it hovers: it blinks when idle, listens, thinks under a swirl, writes with a tiny pen, sparkles when done and wobbles on an error.", IsArc: false, AddedOn: "2026-10-29"),
            new("CubeMascot", "Cube mascot", "cube-mascot", 0,
                "An isometric cube mascot whose shaded faces and painted-on face show what the assistant is doing: it turns in eased quarter turns while thinking, prints lines onto the floor while writing, settles with a sparkle when done and shakes once on error.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("ToneTuner", "Tone tuner", "tone-tuner", 0, "A brand voice card where two sliders (formality and detail) and voice chips rewrite a customer reply: each changed sentence crossfades in place, new words can be highlighted against the original, and Copy takes the current version.", IsArc: false, AddedOn: "2026-10-29"),
            new("UpgradePrompt", "Upgrade prompt", "upgrade-prompt", 0, "An upgrade nudge for the moment someone hits a limit, with a usage meter, a rolling price with a monthly and yearly toggle, three concrete unlocks and a success state, in a card that never changes size.", IsArc: false, AddedOn: "2026-10-29"),
            new("ReferralCard", "Referral card", "referral-card", 0, "A give-and-get referral card: copy or share the invite link, invite friends by email as chips, and watch invites move from invited to signed up to earned as the credit counts up and a milestone unlocks.", IsArc: false, AddedOn: "2026-10-29"),
            new("TrialBanner", "Trial banner", "trial-banner", 0, "A trial status banner where a ring counts down the days left, the copy names what the workspace loses, and the upgrade path is one click away, as a slim banner, a sidebar card or a header pill.", IsArc: false, AddedOn: "2026-10-29"),
            new("TeamSwitcher", "Team switcher", "team-switcher", 0, "A workspace switcher: the trigger shows the current workspace's logo and plan, and the menu searches your workspaces, shows your role and a Cmd/Ctrl+number shortcut for each, and creates or joins one inline.", IsArc: false, AddedOn: "2026-10-29"),
            new("LiveVisitors", "Live visitors", "live-visitors", 0,
                "A realtime visitors card where the live count rolls digit by digit, new arrivals ping in on a dotted world map, the top pages reorder with bars, and signups and upgrades tick in.",
                IsArc: false, AddedOn: "2026-10-29"),
            new("SocialProofToast", "Social proof toast", "social-proof-toast", 0, "A stack of social proof notifications for landing and pricing pages, where recent trials, upgrades and verified purchases from people and companies arrive every few seconds; it pauses and fans out on hover, and visitors can dismiss any toast.", IsArc: false, AddedOn: "2026-10-29"),
            new("InstallSnippet", "Install snippet", "install-snippet", 0, "A quickstart card where framework tabs, three numbered steps and highlighted snippets take a developer from install to the first event, and Generate key wipes a fresh key into place.", IsArc: false, AddedOn: "2026-10-29"),
        ], IsArc: false),
        new("Illustrations", "Illustrations", "插图",
        [
            new("DataFlow", "Data flow", "data-flow", 0,
                "Events stream from their sources through a queue and a batch transform into a warehouse and live dashboards.",
                IsArc: false, AddedOn: "2026-10-21"),
            new("PaymentFlow", "Payment flow", "payment-flow", 0,
                "A card payment from checkout through tokenization, authorization and the issuer's decision to settlement.",
                IsArc: false, AddedOn: "2026-10-21"),
            new("MultiRegionFailover", "Multi-region failover", "multi-region-failover", 0,
                "A global load balancer shifts traffic to a standby region and promotes its database when the primary region goes down.",
                IsArc: false, AddedOn: "2026-10-21"),
            new("VectorSearch", "Vector search", "vector-search", 0,
                "A question is embedded as a vector and answered with its nearest documents by cosine similarity.",
                IsArc: false, AddedOn: "2026-10-21"),
            new("AgentLoop", "Agent loop", "agent-loop", 0,
                "An agent gathers context, calls tools and checks the result, going round the loop until the task is done.",
                IsArc: false, AddedOn: "2026-10-23"),
            new("MixtureOfExperts", "Mixture of experts", "mixture-of-experts", 0,
                "A router scores every expert for each token and sends it to its top choices, whose outputs blend into the result.",
                IsArc: false, AddedOn: "2026-10-23"),
            new("SpeculativeDecoding", "Speculative decoding", "speculative-decoding", 0,
                "A small draft model guesses a few tokens ahead and the large model checks them in one pass, keeping the ones it agrees with.",
                IsArc: false, AddedOn: "2026-10-23"),
            new("PromptCache", "Prompt cache", "prompt-cache", 0,
                "Requests that share a prompt prefix read it from the cache instead of processing it again, and the savings add up.",
                IsArc: false, AddedOn: "2026-10-23"),
            new("ReasoningTree", "Reasoning tree", "reasoning-tree", 0,
                "A search over candidate thoughts: each is scored, weak branches are pruned, the best expand and the winning path draws in.",
                IsArc: false, AddedOn: "2026-10-23"),
        ], IsArc: false),
    ];

    /// <summary>
    /// The Blocks group under Components, with its subgroups and blocks from the TKCode.Arc.Blocks catalog. Not part of
    /// the 112 Arc components (<see cref="Categories"/> / <see cref="Count"/>). Ports of free Arc blocks link to their
    /// uiarc.dev page; original blocks have none.
    /// </summary>
    public static readonly GalleryCategory Blocks = new("Blocks", "Blocks", "区块", [],
        Subcategories: BlockCatalog.Groups.Select(g => new GalleryCategory(g.Code, g.Title, g.ChineseTitle,
            BlockCatalog.Blocks.Where(b => b.Group == g.Code)
                .Select(b => new GalleryEntry(b.ClassName, b.Title, b.Slug, 0, b.Description, IsArc: b.IsArcPort, ArcPath: $"blocks/{b.Slug}",
                    AddedOn: AddedOnOf(b.ClassName)))
                .ToArray())).ToArray());

    /// <summary>
    /// The Mobile section of the sidebar: the components of TKCode.Arc.Mobile, whose live demos run inside a phone frame
    /// (<see cref="Shell.PhoneFrame"/>). Their pages live under <c>/components/</c> like the Special components, and they
    /// are not part of the 112 Arc components (<see cref="Count"/>).
    /// </summary>
    public static readonly GalleryCategory Mobile = new(MobileCatalog.Folder, "Mobile", "移动端",
        MobileCatalog.Components.Select(c => new GalleryEntry(c.ClassName, c.Title, c.Slug, 0, c.Description, IsArc: false, AddedOn: AddedOnOf(c.ClassName))).ToArray(),
        IsArc: false);

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

    /// <summary>The advanced charts group (TKCode.Arc.AdvancedCharts); listed after Charts, but not part of the 112 Arc components.</summary>
    public static GalleryCategory AdvancedCharts => Extras[0];

    /// <summary>The workflow group (TKCode.Arc.Workflow); listed after Advanced charts, not part of the 112 Arc components.</summary>
    public static GalleryCategory Workflow => Extras[1];

    /// <summary>The Components section of the sidebar: Arc's groups with Advanced charts and Workflow after Charts, then the other extras.</summary>
    public static IEnumerable<GalleryCategory> AllCategories =>
        Categories.SelectMany(c => c.Code == "Charts" ? new[] { c, AdvancedCharts, Workflow } : new[] { c })
                  .Concat(Extras.Where(e => !ReferenceEquals(e, AdvancedCharts) && !ReferenceEquals(e, Workflow)));

    /// <summary>Every entry the gallery lists: the Docs, Components, Blocks and Special components sections.</summary>
    public static IEnumerable<GalleryEntry> AllEntries =>
        Docs.AllEntries.Concat(AllCategories.SelectMany(c => c.AllEntries)).Concat(Blocks.AllEntries).Concat(Special.SelectMany(c => c.AllEntries)).Concat(Mobile.AllEntries);

    /// <summary>Every entry of the Components section: Arc's components and the extras.</summary>
    public static IEnumerable<GalleryEntry> ComponentEntries => AllCategories.SelectMany(c => c.Entries);

    /// <summary>Number of Arc components (extras not included).</summary>
    public static int Count => Categories.Sum(c => c.Entries.Length);

    /// <summary>Number the Components section lists: the Arc components and the extras.</summary>
    public static int ComponentCount => ComponentEntries.Count();
}
