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
    public IEnumerable<GalleryEntry> AllEntries { get; } =
        Entries.Concat((Subcategories ?? Array.Empty<GalleryCategory>()).SelectMany(s => s.AllEntries)).ToArray();
}

public static partial class GalleryCatalog
{
    /// <summary>How many days after <see cref="GalleryEntry.AddedOn"/> an entry keeps its "New" badge.</summary>
    public const int NewWindowDays = 15;

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
        ("2026-11-06", ["GanttTimeline"]),
        ("2026-11-08", ["PlanComparison", "PricingCalculator", "UsagePricing", "CartDrawer", "CheckoutSummary", "LiveCheckout", "SeatManager", "CreditTopUp"]),
        ("2026-11-09", ["KnowledgeBase"]),
        ("2026-11-09", ["AgentOrchestrator"]),
        ("2026-11-09", ["AgentBuilder"]),
        ("2026-11-09", ["AgentGallery"]),
        ("2026-11-09", ["McpServers"]),
        ("2026-11-09", ["EvalSuite"]),
        ("2026-11-09", ["AiInsightCard"]),
        ("2026-11-09", ["AiFeatureLaunch"]),
        ("2026-11-09", ["AiSearchBar"]),
        ("2026-11-09", ["AiSetupWizard"]),
        ("2026-11-09", ["CatchUp"]),
        ("2026-11-09", ["SmartScheduler"]),
        ("2026-11-09", ["SupportConversation", "SupportWidget", "InboxTriage", "ActivityFeed", "FeedbackBoard", "HelpCenter", "FeatureVoting"]),
        ("2026-11-09", ["DocsSidebar", "InboxSidebar", "SelectionToolbar", "TwoFactorSetup", "OnboardingSurvey", "MagicLinkSignIn"]),
        ("2026-11-09", ["AgentRoutines"]),
        ("2026-11-09", ["AgentApprovals"]),
        ("2026-11-09", ["SeatMap"]),
        ("2026-11-09", ["InvoiceStudio"]),
        ("2026-11-09", ["DeliveryTracker"]),
        ("2026-11-09", ["AgentTasks"]),
        ("2026-11-09", ["SpendAlert"]),
        ("2026-11-09", ["ProductDetail"]),
        ("2026-11-09", ["PivotBuilder"]),
        ("2026-11-09", ["AiInlineEdit"]),
        ("2026-11-09", ["AccountHealth"]),
        ("2026-11-09", ["WalletCard"]),
        ("2026-11-09", ["TaskBreakdown"]),
        ("2026-11-09", ["ResearchRun"]),
        ("2026-11-09", ["ProductListing"]),
        ("2026-11-09", ["LeadScoring"]),
        ("2026-11-09", ["GoalTracker"]),
        ("2026-11-09", ["CallInsights"]),
        ("2026-11-09", ["AgentRun"]),
        ("2026-11-09", ["HeroSignup", "ScrollStory", "SpotlightGrid", "FeatureBento", "TeamShowcase", "StudioPerspectives"]),
        ("2026-11-09", ["LogoMarquee"]),
        ("2026-11-09", ["ComparisonTable"]),
        ("2026-11-09", ["ChangelogFeed"]),
        ("2026-11-09", ["BlogGrid"]),
        ("2026-11-09", ["StatsBand"]),
        ("2026-11-09", ["ContactSection"]),
        ("2026-11-09", ["FaqSection"]),
        ("2026-11-09", ["HeroSection"]),
    }.SelectMany(g => g.Classes.Select(c => (c, g.Date))).ToDictionary(e => e.c, e => e.Date);

    private static string? AddedOnOf(string className) => s_addedOn.GetValueOrDefault(className);

    /// <summary>
    /// TKCode components with no Arc original, listed after Arc's groups. They are not part of the Arc components
    /// (<see cref="Categories"/> / <see cref="Count"/>).
    /// </summary>
    public static readonly GalleryCategory[] Extras =
    [
        new("AdvancedCharts", "Advanced charts", "高级图表",
        [
            new("DataGrid", "Data grid", "data-grid", 0,
                "A spreadsheet grid with range selection, inline editing, a fill handle, clipboard, undo, sorting, filters, grouping, totals and export.",
                IsArc: false),
            new("Sunburst", "Sunburst", "sunburst", 0,
                "A hierarchy drawn as concentric rings: click a segment to make it the new centre, with a breadcrumb back, a rolling total and a legend of shares.",
                IsArc: false, AddedOn: "2026-11-08"),
            new("SankeyFlow", "Sankey flow", "sankey-flow", 0,
                "Flows between stages drawn as ribbons with drifting particles: hover or focus a node or ribbon to trace its share through the whole journey.",
                IsArc: false, AddedOn: "2026-11-08"),
            new("FunnelChart", "Funnel chart", "funnel-chart", 0,
                "A tapered conversion funnel with the drop-off at each step: split or isolate segments on a spring, and watch dots leave the band at the real rates.",
                IsArc: false, AddedOn: "2026-11-08"),
            new("RadarChart", "Radar chart", "radar-chart", 0,
                "Compare several profiles on one web of measures: swap a profile and its shape morphs, sweep the axes and each value reads out.",
                IsArc: false, AddedOn: "2026-11-08"),
            new("RealtimeStream", "Realtime stream", "realtime-stream", 0,
                "A live line chart that scrolls as readings arrive, holds still under the pointer so a point can be read, and marks readings flagged as anomalies.",
                IsArc: false, AddedOn: "2026-11-08"),
            new("RaceBarChart", "Race bar chart", "race-bar-chart", 0,
                "A ranking that changes over time: bars slide past each other as their values climb, and a timeline plays, pauses or scrubs the periods.",
                IsArc: false, AddedOn: "2026-11-08"),
        ], IsArc: false),
        new("Workflow", "Workflow", "工作流",
        [
            new("WorkflowCanvas", "Workflow canvas", "workflow-canvas", 0,
                "Show a workflow on a dotted canvas and play its runs: each step lights up in turn, the data draws along the edges, and every step reports its result and time.",
                IsArc: false, AddedOn: "2026-10-24"),
        ], IsArc: false),
        new("Dock", "Dock", "停靠布局",
        [
            new("DockLayout", "Dock layout", "dock-layout", 0,
                "An IDE-style layout of tabbed panes in nested, resizable splits around a document area, with commands to move and dock panes, keyboard control, and layouts saved to JSON.",
                IsArc: false, AddedOn: "2026-11-05"),
        ], IsArc: false),
        new("Video", "Video", "视频",
        [
            new("VideoPlayer", "Video player", "video-player", 0,
                "Play a video with controls over the picture: seek with chapters, volume, speed, captions and full screen, drawn in Arc's style and decoded by the platform's own player.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("VideoSurface", "Video surface", "video-surface", 0,
                "Show a video's picture with nothing over it, as a background or under controls of your own; it clips, fades and transforms like any control.",
                IsArc: false, AddedOn: "2026-11-07"),
        ], IsArc: false),
        new("Pdf", "PDF", "PDF 文档",
        [
            new("PdfViewer", "PDF viewer", "pdf-viewer", 0,
                "View a PDF with thumbnails, a table of contents, search, text selection, links and password prompts, rendered by PDFium on desktop and pdf.js in a browser.",
                IsArc: false, AddedOn: "2026-11-08"),
        ], IsArc: false),
        new("Layout", "Layout", "布局",
        [
            new("WaterfallPanel", "Waterfall panel", "waterfall-panel", 0,
                "A masonry panel: cards of different heights flow into equal columns, each to the shortest one, so they pack without gaps.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("TilePanel", "Tile panel", "tile-panel", 0,
                "A start-screen tile grid: tiles span rows and columns, take the first free place without overlapping, and resize with the panel.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("CyclicPanel", "Cyclic panel", "cyclic-panel", 0,
                "A list that loops without a seam: after the last item comes the first, scrolled by wheel, touchpad or code, with the visible items reported.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("Sticky", "Sticky", "sticky", 0,
                "Keep any element of a scrolling page at the top once it scrolls there, like CSS sticky: toolbars stay in view and group headers push each other away.",
                IsArc: false, AddedOn: "2026-11-10"),
            new("BackToTop", "Back to top", "back-to-top", 0,
                "A round button that appears once a long list, log or feed has scrolled past a threshold and smoothly scrolls it back to the top.",
                IsArc: false, AddedOn: "2026-11-10"),
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
            new("TreeSelect", "Tree select", "tree-select", 0,
                "Pick one node or check several in a dropdown tree, with a search that keeps the path to every match and parents that check their whole branch.",
                IsArc: false, AddedOn: "2026-11-10"),
            new("TransferBox", "Transfer box", "transfer-box", 0,
                "Move items between two lists, one by one, as a selection or all at once, with select all, search and the bound collections edited in place.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("PropertyGrid", "Property grid", "property-grid", 0,
                "Edit the properties of one or more objects in rows of label and editor, grouped by category, with search, nested values and lists.",
                IsArc: false, AddedOn: "2026-11-03"),
            new("ArcWindow", "Window", "arc-window", 0,
                "A desktop window with an Arc title bar: left, center and right areas for your controls, native dragging, snapping and resizing, and caption buttons that follow the theme.",
                IsArc: false, AddedOn: "2026-11-04"),
            new("WindowCaptionButtons", "Window caption buttons", "window-caption-buttons", 0,
                "Minimize, maximize and close for a window whose content reaches the top: put them in your own header, and they follow the window and open Snap Layouts on Windows.",
                IsArc: false, AddedOn: "2026-11-11"),
            new("NumberFilterBox", "Number filter box", "number-filter-box", 0,
                "One number filter condition: a checkbox to switch it on, an operator shown as a symbol and a value, with an optional tolerance.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("TextFilterBox", "Text filter box", "text-filter-box", 0,
                "One text filter condition: a checkbox to switch it on, contains, starts with, ends with, equals or a regular expression, and case sensitivity.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("EnumSelector", "Enum selector", "enum-selector", 0,
                "Pick from the members of an enum: radio options for a plain enum, and checkboxes that combine flags for a [Flags] enum, described at compile time without reflection.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("FluidTabControl", "Fluid tab control", "fluid-tab-control", 0,
                "Tabs beside one scroll area that holds every tab's content in order. Picking a tab scrolls to its section, and scrolling selects the tab of the section in view.",
                IsArc: false, AddedOn: "2026-11-07"),
        ], IsArc: false),
    ];

    /// <summary>
    /// The groups of the Special components section, listed after Blocks as on uiarc.dev. Their pages live under
    /// <c>/components/</c> like any component, but they are not part of the Arc components (<see cref="Count"/>).
    /// </summary>
    public static readonly GalleryCategory[] Special =
    [
        new("Loaders", "Loaders", "加载",
        [
            new("StretchRefresh", "Stretch refresh", "stretch-refresh", 0,
                "A feed you pull down to refresh: the rows spread apart, a line stretches across the gap until it is time to let go, then runs while new items arrive.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("MorphLoader", "Morph loader", "morph-loader", 0,
                "A tiny loader whose four strokes morph between dots, bars, a ring and a square, then fold into a check or a cross when the work finishes.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("SkeletonMorph", "Skeleton morph", "skeleton-morph", 0,
                "Loading placeholders that stretch into the real content block by block, while the card eases to its new height.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("BlackHole", "Black hole", "black-hole", 0,
                "A delete transition that spirals the chosen items into a small singularity while the rest of the list bends around it, and spins them back out on undo.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("ChargeSession", "Charge session", "charge-session", 0,
                "A live charging card: energy flows from the charger along the cable into the battery, which fills toward a draggable limit while power, energy, time and cost update.",
                IsArc: false, AddedOn: "2026-11-07"),
        ], IsArc: false),
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
            new("TicketTear", "Ticket tear", "ticket-tear", 0,
                "An event ticket with a stub you tear off: it rips along the perforation hole by hole, following your hand, then falls away.",
                IsArc: false, AddedOn: "2026-11-08"),
            new("SandText", "Sand text", "sand-text", 0,
                "A line of text that crumbles into grains of sand and drifts off on the wind, then gathers back grain by grain into the same letters.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("TypewriterTerminal", "Typewriter terminal", "typewriter-terminal", 0,
                "A terminal window that plays a script: commands typed at a person's pace, output line by line, and a caret that blinks while it waits.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("PrizeWheel", "Prize wheel", "prize-wheel", 0,
                "A prize wheel with weighted slices: a spin draws the winner by its odds and slows to a stop on it while the pegs flick the pointer.",
                IsArc: false, AddedOn: "2026-11-09"),
        ], IsArc: false),
        new("Data", "Data", "数据",
        [
            new("BookingPill", "Booking pill", "booking-pill", 0,
                "A compact booking bar for place, dates and guests: each segment opens its own picker, and the pill widens around the open one while a raised chip glides under it.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("ActivityRings", "Activity rings", "activity-rings", 0,
                "Concentric rings for daily goals that fill on a spring, close at the goal and keep going round past it, with a legend whose numbers roll.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("TimeDial", "Time dial", "time-dial", 0,
                "A round dial for a time of day or a length of time: drag the handle around the face, and it snaps to steps and answers the arrow keys.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("DateReel", "Date reel", "date-reel", 0,
                "A date picker of turning wheels for day, month and year: flick a wheel and it coasts, then comes to rest on a row.",
                IsArc: false, AddedOn: "2026-11-09"),
        ], IsArc: false),
        new("Widgets", "Widgets", "小组件",
        [
            new("KeycapKeyboard", "Keycap keyboard", "keycap-keyboard", 0,
                "A keyboard of keycaps that lights up a shortcut, shows what keys do while modifiers are held, and presses keys as you type or click.",
                IsArc: false, AddedOn: "2026-11-09"),
        ], IsArc: false),
        new("AudioVideo", "Audio and video", "音频与视频",
        [
            new("VoiceOrb", "Voice orb", "voice-orb", 0,
                "A glowing voice-mode sphere that breathes at rest, ripples while it listens, swirls while it thinks and pulses as it speaks, with live captions and call controls.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("NowPlaying", "Now playing", "now-playing", 0,
                "A mini player bar that grows into a full player in one continuous morph, with a waveform scrubber and artwork that slides the way you skip.",
                IsArc: false, AddedOn: "2026-11-07"),
            new("VoiceRecorder", "Voice recorder", "voice-recorder", 0,
                "A voice note composer: record into a live waveform, review and scrub the take, then send it into the thread as a voice bubble.",
                IsArc: false, AddedOn: "2026-11-07"),
        ], IsArc: false),
        new("Surfaces", "Surfaces", "表面",
        [
            new("SheetStack", "Sheet stack", "sheet-stack", 0,
                "Drill-down panels that rise over one another, keep the level below in view, drag away to close and turn into stacked dialogs on wide screens.",
                IsArc: false, AddedOn: "2026-10-27"),
            new("WalletStack", "Wallet stack", "wallet-stack", 0,
                "A wallet of payment cards in real materials: fan the stack, flick through it and lift a card out to see its balance and recent activity.",
                IsArc: false, AddedOn: "2026-10-27"),
            new("ControlCenter", "Control center", "control-center", 0,
                "A quick settings panel whose tiles grow into their details, with a focus-session dial and volume and text-size meters that stretch at their ends.",
                IsArc: false, AddedOn: "2026-10-27"),
            new("HoloCard", "Holo card", "holo-card", 0,
                "A collectible card drawn in perspective that follows the pointer with a shifting rainbow sheen, then turns over to reveal a second side.",
                IsArc: false, AddedOn: "2026-10-27"),
            new("CardToArticle", "Card to article", "card-to-article", 0,
                "Story cards that grow into full articles in one continuous morph and shrink back into their place when you pull them down.",
                IsArc: false, AddedOn: "2026-10-27"),
            new("LinkUnfurl", "Link unfurl", "link-unfurl", 0,
                "A message composer that turns a pasted address into a link, shows it loading, then unrolls it into a preview card you can fold back or remove.",
                IsArc: false, AddedOn: "2026-10-27"),
        ], IsArc: false),
        new("Glass", "Glass", "玻璃",
        [
            new("GlassCard", "Glass card", "glass-card", 0,
                "A card of frosted glass that blurs what lies behind it, with a soft edge light and a sheen that follows the pointer or a tilt signal.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("GlassMenu", "Glass menu", "glass-menu", 0,
                "A menu on frosted glass whose highlight glides from row to row under the pointer, with a soft glow that follows it along the row.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("GlassSegmented", "Glass segmented", "glass-segmented", 0,
                "A segmented control on frosted glass with a selection lens that stretches as it glides between segments and can be dragged.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("GlassTabBar", "Glass tab bar", "glass-tab-bar", 0,
                "A floating bottom tab bar of frosted glass for app shells, with a selection lens that glides and stretches between tabs.",
                IsArc: false, AddedOn: "2026-11-09"),
        ], IsArc: false),
        new("Photos", "Photos", "照片",
        [
            new("ProductGallery", "Product gallery", "product-gallery", 0,
                "The pictures of a product page: a large picture with a strip of thumbnails, a zoom that follows the mouse over the picture, and a swipe on touch screens.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("LightboxGallery", "Lightbox gallery", "lightbox-gallery", 0,
                "A grid of photos that opens full screen: the tile grows into the lightbox, where arrows and swipes move between photos and the wheel or a pinch zooms in.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("PhotoGrid", "Photo grid", "photo-grid", 0,
                "Photos in justified rows of one height or in masonry columns, where a selected photo opens wide in place and every photo glides to its new spot on resize.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("CoverFlow", "Cover flow", "cover-flow", 0,
                "A row of album or book covers in perspective: the centred cover faces you, the rest turn toward it, and a flick coasts and snaps to a cover.",
                IsArc: false, AddedOn: "2026-11-09"),
        ], IsArc: false),
        new("Publishing", "Publishing", "内容发布",
        [
            new("MegaMenuMorph", "Mega menu morph", "mega-menu-morph", 0,
                "A site navigation bar whose one dropdown panel springs to each item's size and place as the pointer moves along the bar, with the content sliding in from the side it travelled to.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("TocProgress", "Table of contents progress", "table-of-contents-progress", 0,
                "A table of contents for a long article that highlights the section being read, fills a rail down the list as you scroll and jumps to any section; on narrow screens it folds into a progress button.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("ShareAccess", "Share access", "share-access", 0,
                "A document share dialog: invite people by email with a role, change or remove each person's access, choose who else can open the link and copy it.",
                IsArc: false, AddedOn: "2026-11-09"),
        ], IsArc: false),
        new("Inputs", "Inputs", "输入",
        [
            new("EmojiPicker", "Emoji picker", "emoji-picker", 0,
                "Find an emoji by name or browse every category: recently used first, a skin tone for people and hands, and a grid you can move through with the keyboard.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("ConfirmPopover", "Confirm popover", "confirm-popover", 0,
                "A small question next to the button that asked it, for actions such as delete: focus starts on Cancel, Escape backs out, and the work can run before it closes.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("UndoToast", "Undo toast", "undo-toast", 0,
                "A toast for an action that can be taken back, with a countdown ring and an Undo button; the countdown holds while the pointer or focus is on it.",
                IsArc: false, AddedOn: "2026-11-09"),
        ], IsArc: false),
        new("DocksTabs", "Docks and tabs", "程序坞与标签栏",
        [
            new("ShareSheet", "Share sheet", "share-sheet", 0,
                "Sharing in place: the Share button stretches into a card to copy the item's link, set who may open it, post it to a channel or send it to a few people, and shrinks into a confirmation once sent.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("ActionMorph", "Action morph", "action-morph", 0,
                "A corner button for the few entries people add most: it opens into a short list of actions, the chosen action reshapes the same surface into a small form, and a successful save shrinks it to the button with a check mark.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("MorphNav", "Morph nav", "morph-nav", 0,
                "A website's top bar in which sections, their link panels and search share one reshaping surface; it slims down once the page scrolls and gathers its sections into a single menu when narrow.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("ToolDock", "Tool dock", "tool-dock", 0,
                "A floating toolbar for the tools and modes of an editor, such as a canvas: the tool in use keeps a tinted backdrop, hovering shows its name and key, and variants of one tool share a slot that opens into a tray.",
                IsArc: false, AddedOn: "2026-11-11"),
            new("LiquidTabBar", "Liquid tab bar", "liquid-tab-bar", 0,
                "Glyph tabs for an app's main sections. A solid lens marks the chosen tab and travels to the next one, painting the tab beneath it in reverse colors so its outline glyph turns solid; it can also be dragged or flicked.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("OrbitMenu", "Orbit menu", "orbit-menu", 0,
                "Quick actions for one object, fanned out on an arc from a round button. A single press, drag and release runs one; a short tap leaves the arc open for clicking.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("MagnifyDock", "Magnify dock", "magnify-dock", 0,
                "An app dock whose icons swell like a lens under the pointer, push their neighbours aside, show their names and bounce when launched.",
                IsArc: false, AddedOn: "2026-11-09"),
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
            new("ImageTrail", "Image trail", "image-trail", 0,
                "A surface where the pointer leaves a trail of pictures that pop in along its path, rest for a moment and sink away.",
                IsArc: false, AddedOn: "2026-11-09"),
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
            new("DnsResolution", "DNS resolution", "dns-resolution", 0,
                "A browser asks its resolver for a name, the resolver walks from a root server to the domain's own name server, caches the answer and replies from the cache next time.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("ModelTraining", "Model training", "model-training", 0,
                "A training loop step by step: a batch flows forward to a prediction, the loss is measured and flows back, the weights move and the loss curve falls.",
                IsArc: false, AddedOn: "2026-11-09"),
            new("BranchGraph", "Branch graph", "branch-graph", 0,
                "A git-style history drawn commit by commit: branches split off into their own lanes and merge back, with branch and tag labels and each commit's history on selection.",
                IsArc: false, AddedOn: "2026-11-09"),
        ], IsArc: false),
    ];

    /// <summary>
    /// The Blocks group under Components, with its subgroups and blocks from the TKCode.Arc.Blocks catalog. Not part of
    /// the Arc components (<see cref="Categories"/> / <see cref="Count"/>). Ports of free Arc blocks link to their
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
    /// are not part of the Arc components (<see cref="Count"/>).
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
        new("DocsTemplates", "Templates", "templates", 0, "Start an app from a dotnet new template, and add pages, windows, dialogs and controls to it.", IsArc: false),
        new("DocsTheming", "Theming", "theming", 0, "Themes, design tokens and appearance.", IsArc: false),
        new("DocsMotion", "Motion", "motion", 0, "The motion engine and its presets.", IsArc: false),
        new("DocsAiAndMcp", "AI and MCP", "ai-and-mcp", 0, "Use Arc with AI assistants: llms.txt, Markdown pages, an agent skill and MCP.", IsArc: false),
        new("DocsChangelog", "Changelog", "changelog", 0, "Release notes.", IsArc: false),
    ], IsArc: false);

    /// <summary>The advanced charts group (TKCode.Arc.AdvancedCharts); listed after Charts, but not part of the Arc components.</summary>
    public static GalleryCategory AdvancedCharts => Extras[0];

    /// <summary>The workflow group (TKCode.Arc.Workflow); listed after Advanced charts, not part of the Arc components.</summary>
    public static GalleryCategory Workflow => Extras[1];

    /// <summary>The dock group (TKCode.Arc.Dock); listed after Workflow, not part of the Arc components.</summary>
    public static GalleryCategory Dock => Extras[2];

    /// <summary>The video group (TKCode.Arc.Video); listed after Dock, not part of the Arc components.</summary>
    public static GalleryCategory Video => Extras[3];

    /// <summary>The PDF group (TKCode.Arc.Pdf); listed after Video, not part of the Arc components.</summary>
    public static GalleryCategory Pdf => Extras[4];

    /// <summary>The Components section of the sidebar: Arc's groups with Advanced charts, Workflow, Dock, Video and PDF after Charts, then the other extras.</summary>
    public static IEnumerable<GalleryCategory> AllCategories => s_allCategories ??=
        Categories.SelectMany(c => c.Code == "Charts" ? new[] { c, AdvancedCharts, Workflow, Dock, Video, Pdf } : new[] { c })
                  .Concat(Extras.Where(e => !ReferenceEquals(e, AdvancedCharts) && !ReferenceEquals(e, Workflow) && !ReferenceEquals(e, Dock) && !ReferenceEquals(e, Video) && !ReferenceEquals(e, Pdf)))
                  .ToArray();

    // The lists below are built once, when first read: they are read on every page opening. Not in field initializers, whose
    // order across this class's files is not one to rely on.
    private static GalleryCategory[]? s_allCategories;
    private static GalleryEntry[]? s_allEntries;
    private static GalleryEntry[]? s_componentEntries;

    /// <summary>Every entry the gallery lists: the Docs, Components, Blocks and Special components sections.</summary>
    public static IEnumerable<GalleryEntry> AllEntries => s_allEntries ??=
        Docs.AllEntries.Concat(AllCategories.SelectMany(c => c.AllEntries)).Concat(Blocks.AllEntries).Concat(Special.SelectMany(c => c.AllEntries)).Concat(Mobile.AllEntries).ToArray();

    /// <summary>Every entry of the Components section: Arc's components and the extras.</summary>
    public static IEnumerable<GalleryEntry> ComponentEntries => s_componentEntries ??= AllCategories.SelectMany(c => c.Entries).ToArray();

    /// <summary>Number of Arc components (extras not included).</summary>
    public static int Count => Categories.Sum(c => c.Entries.Length);

    /// <summary>Number the Components section lists: the Arc components and the extras.</summary>
    public static int ComponentCount => ComponentEntries.Count();
}
