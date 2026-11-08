using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The home page, written as the product site of TKCode: the theme panel beside a sample app (change a value and the page
/// follows), the two products, StreamMark streaming a reply, live examples, every component, a screen taken apart, the add-on
/// packages, the sponsor and friends, and the footer. The site says it runs in WebAssembly and recommends the desktop app;
/// the desktop app replaces that with its version and updates (<see cref="GalleryHost"/>). Everything below the first screen
/// is built when it first comes into view, so the first frame stays cheap in the browser.
/// </summary>
public sealed class GalleryHomeView : UserControl
{
    private const string InstallCommand = "dotnet add package TKCode.Arc --prerelease";

    /// <summary>The examples on the cards: the page they open and the demo they show (<c>Component.Name</c>).</summary>
    private static readonly (string ClassName, string DemoId)[] s_examples =
    [
        ("ButtonGroup", "ButtonGroup.LiveValue"),
        ("Gauge", "Gauge.Thresholds"),
        ("ChatThread", "ChatThread.ComposerOnItsOwn"),
        ("CommandPalette", "CommandPalette.InlineItems"),
        ("Calendar", "Calendar.SingleDate"),
        ("MultiSelect", "MultiSelect.Chips")
    ];

    private readonly GalleryShell _shell;
    private readonly List<LiveCard> _cards = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _updateTitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
    private readonly TextBlock _updateHint = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 12.5);
    private bool _touched;

    public GalleryHomeView(GalleryShell shell)
    {
        _shell = shell;
        var column = new StackPanel();
        column.Children.Add(BuildHero());
        column.Children.Add(BuildProducts());
        column.Children.Add(BuildStream());
        column.Children.Add(BuildLive());
        Atlas = new GalleryAtlas(shell);
        column.Children.Add(Atlas);
        column.Children.Add(BuildExplode());
        column.Children.Add(BuildExtras());
        if (GalleryHost.IsWeb)
        {
            Download = new GalleryDownloadSection();
            column.Children.Add(Download);
        }
        column.Children.Add(BuildFriends());
        column.Children.Add(GallerySiteFooter.Create(shell, GalleryHost.IsWeb ? ShowDownload : null));
        _scroll = GallerySite.Page(column, out _);
        KeepAtTopUntilTouched(_scroll);
        Content = _scroll;

        if (GalleryHost.IsDesktop)
        {
            AttachedToVisualTree += (_, _) => GalleryUpdates.Changed += SyncUpdate;
            DetachedFromVisualTree += (_, _) => GalleryUpdates.Changed -= SyncUpdate;
            GalleryText.Track(this, SyncUpdate);
        }
    }

    /// <summary>The cards of the live examples, in order.</summary>
    public IReadOnlyList<LiveCard> Cards => _cards;

    /// <summary>The section with every component.</summary>
    internal GalleryAtlas Atlas { get; }

    /// <summary>The recommendation of the desktop app; null in the desktop app.</summary>
    internal GalleryDownloadSection? Download { get; }

    /// <summary>The theme panel beside the sample app.</summary>
    internal GalleryThemeTuner Tuner { get; private set; } = null!;

    /// <summary>The line under the hero that says this page runs in WebAssembly; null in the desktop app.</summary>
    internal Alert? WebNote { get; private set; }

    /// <summary>Scrolls to the recommendation of the desktop app.</summary>
    public void ShowDownload()
    {
        if (Download is { } section)
        {
            _touched = true;
            section.BringIntoView(new Rect(section.Bounds.Size));
        }
    }

    /// <summary>Hands the scroll over, as the first wheel, key or press does (tools that scroll the page themselves).</summary>
    public void MarkTouched() => _touched = true;

    /// <summary>
    /// The page opens at the top and stays there until the visitor scrolls. A live example that takes the focus or asks to be
    /// brought into view when it is built (the cards and the exploded view are built as they come into view) would otherwise
    /// carry the page away from the top; the first wheel, key or press hands the scroll over.
    /// </summary>
    private void KeepAtTopUntilTouched(ScrollViewer scroll)
    {
        void Touch(object? sender, RoutedEventArgs e) => _touched = true;
        scroll.AddHandler(PointerWheelChangedEvent, Touch, RoutingStrategies.Tunnel);
        scroll.AddHandler(PointerPressedEvent, Touch, RoutingStrategies.Tunnel);
        scroll.AddHandler(KeyDownEvent, Touch, RoutingStrategies.Tunnel);
        scroll.ScrollChanged += (_, _) =>
        {
            if (!_touched && scroll.Offset.Y > 0)
            {
                scroll.Offset = default;
            }
        };
    }

    #region Hero

    /// <summary>The aurora card that opens a product page.</summary>
    internal static Control HeroCard(Control content)
    {
        var padding = new Border { Padding = new Thickness(40, 44), Child = content };
        padding.SizeChanged += (_, e) => padding.Padding = e.NewSize.Width < GallerySite.PhoneWidth ? new Thickness(20, 28) : new Thickness(40, 44);
        var surface = new AuroraSurface();
        surface.Children.Add(padding);
        var hero = new Border { BorderThickness = new Thickness(1), ClipToBounds = true, Child = surface };
        ArcTokens.Bind(hero, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(hero, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusSurface);
        return hero;
    }

    private Control BuildHero()
    {
        var web = GalleryHost.IsWeb;
        var badge = new Badge { Tone = BadgeTone.Info, Icon = new ArcGlyph { Data = ArcIcons.Sparkles, Size = 12 }, HorizontalAlignment = HorizontalAlignment.Left };
        GalleryText.Bind(badge, ContentControl.ContentProperty, web ? GalleryShellLangResourceKind.HomeBadge : GalleryShellLangResourceKind.HomeDesktopBadge);

        var title = GallerySite.Reveal(web ? GalleryShellLangResourceKind.HomeTitle : GalleryShellLangResourceKind.HomeDesktopTitle, TextLevel.H1);

        TextBlock subtitle;
        if (web)
        {
            subtitle = GallerySite.Format(GalleryShellLangResourceKind.HomeSubtitle, ArcDesignTokenKind.TextSecondary, 17, GalleryCatalog.Count);
        }
        else
        {
            subtitle = GallerySite.Text(GalleryShellLangResourceKind.HomeDesktopSubtitle, ArcDesignTokenKind.TextSecondary, 17);
        }

        var explore = GallerySite.Action(GalleryShellLangResourceKind.HomeExplore, ButtonVariant.Primary, () => _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Components)), ArcIcons.ArrowRight);
        var docs = GallerySite.Action(GalleryShellLangResourceKind.HomeReadDocs, ButtonVariant.Secondary, () => _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Docs)));
        // The dashboard demo: /demo, which opens its overview.
        var demo = GallerySite.Action(GalleryShellLangResourceKind.HomeDemo, ButtonVariant.Secondary, () => _shell.Navigate(GalleryRoute.Demo()));
        var actions = GallerySite.Actions(explore, docs, demo);

        var content = new StackPanel { Spacing = 18, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        content.Children.Add(badge);
        content.Children.Add(title);
        content.Children.Add(subtitle);
        content.Children.Add(actions);
        content.Children.Add(GallerySite.Install(InstallCommand));

        Tuner = new GalleryThemeTuner(_shell.Appearance, _shell);

        var hero = new StackPanel { Spacing = 14 };
        if (web)
        {
            hero.Children.Add(HeroCard(content));
            WebNote = BuildWebNote();
            hero.Children.Add(WebNote);
        }
        else
        {
            var top = new GalleryTwoPane { EndWidth = 320, StackBelow = 880, Spacing = 14 };
            top.Children.Add(HeroCard(content));
            top.Children.Add(BuildUpdateCard());
            hero.Children.Add(top);
        }
        hero.Children.Add(Tuner);
        return hero;
    }

    private Alert BuildWebNote()
    {
        var text = GallerySite.Text(GalleryShellLangResourceKind.HomeWebNoteText, ArcDesignTokenKind.TextSecondary, 13.5);
        var action = GallerySite.Action(GalleryShellLangResourceKind.HomeWebNoteAction, ButtonVariant.Secondary, ShowDownload, ArcIcons.ArrowRight, ControlSize.Small);
        action.HorizontalAlignment = HorizontalAlignment.Left;
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(text);
        stack.Children.Add(action);
        var note = new Alert { Tone = AlertTone.Warning, Content = stack };
        GalleryText.Bind(note, Alert.TitleProperty, GalleryShellLangResourceKind.HomeWebNoteTitle);
        return note;
    }

    /// <summary>The desktop app's own line at the top: its version, the update state, and the way to About.</summary>
    private Control BuildUpdateCard()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(GallerySite.Eyebrow(GalleryShellLangResourceKind.HomeUpdateTitle));
        stack.Children.Add(_updateTitle);
        stack.Children.Add(_updateHint);
        stack.Children.Add(Quick(GalleryShellLangResourceKind.Components, () => _shell.RouteFor(GalleryRouteKind.Components)));
        stack.Children.Add(Quick(GalleryShellLangResourceKind.Category_Blocks, () => _shell.RouteFor(GalleryRouteKind.Blocks)));
        stack.Children.Add(Quick(GalleryShellLangResourceKind.HomeDemo, () => GalleryRoute.Demo()));
        stack.Children.Add(Quick(GalleryShellLangResourceKind.ProductTemplates, () => GallerySite.Docs("DocsTemplates")));
        stack.Children.Add(GallerySite.Actions(
            GallerySite.Action(GalleryShellLangResourceKind.SettingsAbout, ButtonVariant.Secondary, () => _shell.Navigate(GalleryRoute.Settings(GallerySettingsView.AboutPage)), null, ControlSize.Small),
            GallerySite.Action(GalleryShellLangResourceKind.AboutChangelog, ButtonVariant.Ghost, () => _shell.Navigate(GallerySite.Docs("DocsChangelog")), null, ControlSize.Small)));
        return GallerySite.Panel(stack, ArcDesignTokenKind.SurfaceMuted);
    }

    /// <summary>A way into the gallery from the desktop app's first screen.</summary>
    private Button Quick(GalleryShellLangResourceKind label, Func<GalleryRoute> route)
    {
        var button = GallerySite.Action(label, ButtonVariant.Secondary, () => _shell.Navigate(route()), ArcIcons.ArrowRight, ControlSize.Small);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        return button;
    }

    private void SyncUpdate()
    {
        _updateTitle.Text = GalleryUpdates.Status switch
        {
            GalleryUpdateStatus.Available => GalleryText.Format(GalleryShellLangResourceKind.UpdateAvailable, GalleryUpdates.Available!.Version),
            GalleryUpdateStatus.UpToDate => GalleryText.Get(GalleryShellLangResourceKind.UpdateUpToDate),
            GalleryUpdateStatus.Checking => GalleryText.Get(GalleryShellLangResourceKind.UpdateChecking),
            _ => GalleryText.Format(GalleryShellLangResourceKind.UpdateIdle, GalleryHost.Version)
        };
        _updateHint.Text = GalleryHost.Version;
        _updateHint.IsVisible = GalleryUpdates.Status is GalleryUpdateStatus.Available or GalleryUpdateStatus.UpToDate;
    }

    #endregion

    #region Products

    private Control BuildProducts()
    {
        var specials = GalleryCatalog.Special.Sum(category => category.AllEntries.Count());
        var arc = ProductCard("TKCode.Arc", GalleryHost.Version, "Arc", GalleryShellLangResourceKind.HomeArcText, InstallCommand,
            [
                GallerySite.Format(GalleryShellLangResourceKind.HomeArcPoint1, ArcDesignTokenKind.Foreground, 14, GalleryCatalog.Count, GalleryCatalog.Blocks.AllEntries.Count(), specials),
                GallerySite.Text(GalleryShellLangResourceKind.HomeArcPoint2),
                GallerySite.Text(GalleryShellLangResourceKind.HomeArcPoint3)
            ],
            GallerySite.Action(GalleryShellLangResourceKind.HomeExplore, ButtonVariant.Primary, () => _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Components))),
            GallerySite.Action(GalleryShellLangResourceKind.HomeReadDocs, ButtonVariant.Ghost, () => _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Docs)), ArcIcons.ArrowRight));
        var streamMark = ProductCard("TKCode.StreamMark", GallerySite.StreamMarkVersion, "StreamMark", GalleryShellLangResourceKind.HomeStreamMarkText, "dotnet add package TKCode.StreamMark.Avalonia",
            [
                GallerySite.Text(GalleryShellLangResourceKind.StreamMarkPoint1),
                GallerySite.Text(GalleryShellLangResourceKind.StreamMarkPoint2),
                GallerySite.Text(GalleryShellLangResourceKind.StreamMarkPoint3)
            ],
            GallerySite.Action(GalleryShellLangResourceKind.StreamMarkLearn, ButtonVariant.Primary, () => _shell.Navigate(GalleryRoute.StreamMark)),
            GallerySite.Action(GalleryShellLangResourceKind.StreamMarkGitHub, ButtonVariant.Ghost, () => GallerySite.Open(this, GalleryStreamMarkView.RepositoryUrl), ArcIcons.ArrowUpRight));
        var grid = new GalleryGrid { MinItemWidth = 340, MaxColumns = 2, Spacing = 16 };
        grid.Children.Add(arc);
        grid.Children.Add(streamMark);

        var section = new StackPanel { Spacing = 24 };
        section.Children.Add(GallerySite.Heading(GalleryShellLangResourceKind.HomeProductsEyebrow,
                                                 GallerySite.Reveal(GalleryShellLangResourceKind.HomeProductsTitle),
                                                 GalleryShellLangResourceKind.HomeProductsSubtitle));
        section.Children.Add(grid);
        return section;
    }

    private static Control ProductCard(string package, string version, string name, GalleryShellLangResourceKind text, string install, Control[] points, params Control[] actions)
    {
        var tag = GalleryUi.Mono($"{package} · {version}", ArcDesignTokenKind.TextMuted, 12);
        tag.TextWrapping = TextWrapping.Wrap;
        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(tag);
        stack.Children.Add(GalleryUi.Text(name, ArcDesignTokenKind.Foreground, 32, FontWeight.SemiBold));
        stack.Children.Add(GallerySite.Text(text, ArcDesignTokenKind.TextSecondary, 15));
        stack.Children.Add(GallerySite.Checks(points));
        stack.Children.Add(GallerySite.Install(install));
        stack.Children.Add(GallerySite.Actions(actions));
        return GallerySite.Panel(stack, ArcDesignTokenKind.SurfaceMuted, 24);
    }

    private Control BuildStream()
    {
        var stage = new GalleryStreamStage { EndWidth = 280, StackBelow = 820 };
        var learn = GallerySite.Action(GalleryShellLangResourceKind.StreamMarkLearn, ButtonVariant.Secondary, () => _shell.Navigate(GalleryRoute.StreamMark), ArcIcons.ArrowRight);
        learn.HorizontalAlignment = HorizontalAlignment.Left;
        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(GallerySite.Heading(GalleryShellLangResourceKind.HomeStreamEyebrow,
                                                 GallerySite.Reveal(GalleryShellLangResourceKind.HomeStreamTitle),
                                                 GalleryShellLangResourceKind.HomeStreamText));
        section.Children.Add(stage);
        section.Children.Add(learn);
        return section;
    }

    #endregion

    #region Live examples

    private Control BuildLive()
    {
        var all = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left };
        var allLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.BindFormat(allLabel, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeBrowseAll, GalleryCatalog.Count);
        var allRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        allRow.Children.Add(allLabel);
        allRow.Children.Add(new ArcGlyph { Data = ArcIcons.ArrowRight, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        all.Content = allRow;
        all.Click += (_, _) => _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Components));

        var grid = new GalleryGrid { MinItemWidth = 300, MaxColumns = 3, Spacing = 14 };
        foreach (var (className, demoId) in s_examples)
        {
            var entry = GalleryCatalog.AllEntries.FirstOrDefault(e => e.ClassName == className);
            if (entry is null || GalleryDemos.Get(demoId) is null)
            {
                continue;
            }
            var card = new LiveCard(entry, demoId, () => _shell.Navigate(GalleryRoute.To(entry)));
            _cards.Add(card);
            grid.Children.Add(card);
        }

        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(GallerySite.Heading(null, GallerySite.Reveal(GalleryShellLangResourceKind.HomeWallTitle), GalleryShellLangResourceKind.HomeWallSubtitle));
        section.Children.Add(grid);
        section.Children.Add(all);
        return section;
    }

    /// <summary>One example card: the live demo above its title and an Open button for its page.</summary>
    public sealed class LiveCard : Border
    {
        private const double StageHeight = 240;

        private readonly Border _stage = new() { Height = StageHeight, ClipToBounds = true, CornerRadius = new CornerRadius(14) };
        private readonly string _demoId;
        private bool _created;

        public LiveCard(GalleryEntry entry, string demoId, Action open)
        {
            _demoId = demoId;
            Entry = entry;
            ArcTokens.Bind(_stage, Border.BackgroundProperty, ArcDesignTokenKind.Surface);

            var name = GalleryUi.Text(entry.Title, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
            name.VerticalAlignment = VerticalAlignment.Center;
            var openLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            GalleryText.Bind(openLabel, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeOpen);
            var openRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            openRow.Children.Add(openLabel);
            openRow.Children.Add(new ArcGlyph { Data = ArcIcons.ArrowUpRight, Size = 14, VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = openRow };
            button.Click += (_, _) => open();
            AutomationProperties.SetName(button, entry.Title);

            var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(button, 1);
            footer.Children.Add(name);
            footer.Children.Add(button);

            var stack = new StackPanel { Spacing = 10 };
            stack.Children.Add(_stage);
            stack.Children.Add(footer);
            Child = stack;
            Padding = new Thickness(14, 14, 14, 8);
            BorderThickness = new Thickness(1);
            ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            ArcTokens.Bind(this, CornerRadiusProperty, ArcDesignTokenKind.RadiusPanel);
            // The demo is built when the card first reaches the viewport, not with the page.
            _ = new GalleryFirstSight(this, EnsureCreated);
        }

        public GalleryEntry Entry { get; }

        /// <summary>Whether the demo has been built.</summary>
        public bool IsCreated => _created;

        /// <summary>Builds the demo if it has not been built yet.</summary>
        public void EnsureCreated()
        {
            if (_created)
            {
                return;
            }
            _created = true;
            if (GalleryDemos.Get(_demoId)?.Create() is not { } demo)
            {
                return;
            }
            // A demo is laid out for a page column; shrink one that is wider than the card instead of cutting it off.
            _stage.Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = demo };
        }
    }

    #endregion

    #region Exploded view

    /// <summary>The section that takes a screen apart as the page scrolls.</summary>
    private Control BuildExplode()
    {
        // The view is a tall track in this page's scroll viewer (ScrollMode.Page) and is built when it first comes into view.
        var host = ExplodeHost = new Border { MinHeight = 560 };
        _ = new GalleryFirstSight(host, EnsureExplodeCreated);

        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(GallerySite.Heading(null, GallerySite.Reveal(GalleryShellLangResourceKind.HomeExplodeTitle), GalleryShellLangResourceKind.HomeExplodeSubtitle));
        section.Children.Add(host);
        return section;
    }

    /// <summary>The placeholder that holds the exploded view.</summary>
    public Border ExplodeHost { get; private set; } = null!;

    /// <summary>Whether the exploded view has been built.</summary>
    public bool IsExplodeCreated => ExplodeHost.Child is ExplodedView;

    /// <summary>Builds the exploded view if it has not been built yet.</summary>
    public void EnsureExplodeCreated()
    {
        if (ExplodeHost.Child is not null)
        {
            return;
        }
        var view = new ExplodedView { ScrollMode = ExplodedScrollMode.Page, IsFramed = false, Length = 3 };
        Demos.ExplodedViewDemoData.Fill(view);
        view.Description = null;
        ExplodeHost.Child = view;
    }

    #endregion

    #region Add-on packages

    private Control BuildExtras()
    {
        var specials = GalleryCatalog.Special.Sum(category => category.AllEntries.Count());
        var grid = new GalleryGrid { MinItemWidth = 240, MaxColumns = 4, Spacing = 12 };
        grid.Children.Add(Extra(GalleryShellLangResourceKind.Category_Blocks, GalleryShellLangResourceKind.ExtraBlocksText, "TKCode.Arc.Blocks", () => _shell.RouteFor(GalleryRouteKind.Blocks), GalleryCatalog.Blocks.AllEntries.Count()));
        grid.Children.Add(Extra(GalleryShellLangResourceKind.ProductSpecial, GalleryShellLangResourceKind.ExtraSpecialText, "TKCode.Arc.Special", () => GalleryRoute.To(GalleryCatalog.Special[0].AllEntries.First()), specials));
        grid.Children.Add(Extra(GalleryShellLangResourceKind.ProductMobile, GalleryShellLangResourceKind.ExtraMobileText, "TKCode.Arc.Mobile", () => GalleryRoute.To(GalleryCatalog.Mobile.Entries[0]), GalleryCatalog.Mobile.Entries.Length));
        grid.Children.Add(Extra(GalleryShellLangResourceKind.ExtraWorkflow, GalleryShellLangResourceKind.ExtraWorkflowText, "TKCode.Arc.Workflow", () => Entry("WorkflowCanvas")));
        grid.Children.Add(Extra(GalleryShellLangResourceKind.ExtraDock, GalleryShellLangResourceKind.ExtraDockText, "TKCode.Arc.Dock", () => Entry("DockLayout")));
        // The terminal has no page in the Gallery: it needs a native process and does not run in the browser.
        grid.Children.Add(Extra(GalleryShellLangResourceKind.ExtraTerminal, GalleryShellLangResourceKind.ExtraTerminalText, "TKCode.Arc.Terminal", null));
        grid.Children.Add(Extra(GalleryShellLangResourceKind.ExtraMedia, GalleryShellLangResourceKind.ExtraMediaText, "TKCode.Arc.Video · Audio", () => Entry("VideoPlayer")));
        grid.Children.Add(Extra(GalleryShellLangResourceKind.ExtraCharts, GalleryShellLangResourceKind.ExtraChartsText, "TKCode.Arc.AdvancedCharts", () => Entry("DataGrid")));

        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(GallerySite.Heading(GalleryShellLangResourceKind.HomeExtrasEyebrow, GallerySite.Reveal(GalleryShellLangResourceKind.HomeExtrasTitle)));
        section.Children.Add(grid);
        return section;
    }

    private static GalleryRoute Entry(string className) => GalleryRoute.To(GalleryCatalog.AllEntries.First(entry => entry.ClassName == className));

    private Control Extra(GalleryShellLangResourceKind title, GalleryShellLangResourceKind text, string package, Func<GalleryRoute>? route, params object[] args)
    {
        var description = GallerySite.Format(text, ArcDesignTokenKind.TextSecondary, 13, args);
        var name = GalleryUi.Mono(package, ArcDesignTokenKind.TextMuted, 11.5);
        name.TextWrapping = TextWrapping.Wrap;

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(GallerySite.Text(title, ArcDesignTokenKind.Foreground, 15, FontWeight.Medium));
        stack.Children.Add(description);
        stack.Children.Add(name);
        if (route is { } target)
        {
            var open = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-8, 4, 0, 0), Content = GallerySite.Labelled(GalleryShellLangResourceKind.HomeOpen, ArcIcons.ArrowRight) };
            GalleryText.Bind(open, AutomationProperties.NameProperty, title);
            open.Click += (_, _) => _shell.Navigate(target());
            stack.Children.Add(open);
        }
        return GallerySite.Panel(stack, padding: 16);
    }

    #endregion

    #region Friends

    private Control BuildFriends()
    {
        var all = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left, Content = GalleryFriendsView.LabelWithGlyph(GalleryShellLangResourceKind.HomeSeeAll, ArcIcons.ArrowRight) };
        all.Click += (_, _) => _shell.Navigate(GalleryRoute.Friends);

        // The sites rotate in a Carousel: the active one in the center, its neighbours dimmed at the sides.
        var sites = GalleryFriends.Sponsors.Concat(GalleryFriends.Friends).ToList();
        var carousel = new Carousel
        {
            ItemsSource = sites,
            SlideWidthRatio = 0.34,
            MaxSlideWidth = 340,
            SlideGap = 16,
            Interval = TimeSpan.FromSeconds(4),
            IsAutoPlay = true,
            ItemTemplate = new FuncDataTemplate<GalleryFriend>((site, _) => site is null ? null : SiteSlide(site))
        };
        // On a phone a slide takes most of the width, with a glimpse of its neighbours.
        carousel.SizeChanged += (_, e) => carousel.SlideWidthRatio = e.NewSize.Width < GallerySite.PhoneWidth ? 0.8 : 0.34;
        AutomationProperties.SetName(carousel, GalleryText.Get(GalleryShellLangResourceKind.HomeFriendsTitle));

        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(GallerySite.Heading(null, GallerySite.Reveal(GalleryShellLangResourceKind.HomeFriendsTitle), GalleryShellLangResourceKind.HomeFriendsSubtitle));
        section.Children.Add(carousel);
        section.Children.Add(all);
        return section;
    }

    /// <summary>One site as a slide: its mark above its name, description and a link that opens the site.</summary>
    private Control SiteSlide(GalleryFriend site)
    {
        var name = GalleryUi.Text(site.Name, ArcDesignTokenKind.Foreground, 16, FontWeight.Medium);
        var description = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 13);
        description.TextWrapping = TextWrapping.Wrap;
        GalleryText.Bind(description, TextBlock.TextProperty, site.Description);
        var visit = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left, Content = GalleryFriendsView.LabelWithGlyph(GalleryShellLangResourceKind.FriendsVisit, ArcIcons.ArrowUpRight) };
        AutomationProperties.SetName(visit, site.Name);
        visit.Click += (_, _) => GallerySite.Open(this, site.Url);

        var body = new StackPanel { Spacing = 6, Margin = new Thickness(18, 14, 18, 18) };
        body.Children.Add(name);
        body.Children.Add(description);
        body.Children.Add(new Border { Margin = new Thickness(0, 6, 0, 0), Child = visit });
        var stack = new StackPanel();
        stack.Children.Add(new FriendMark(site, 56, 120));
        stack.Children.Add(body);
        var frame = new Border { BorderThickness = new Thickness(1), ClipToBounds = true, Child = stack };
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(frame, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusPanel);
        return frame;
    }

    #endregion
}
