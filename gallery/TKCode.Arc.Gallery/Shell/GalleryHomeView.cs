using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The gallery's home page: a hero with the two ways in (the components, the docs) and the install command, four live
/// examples that open their pages, the size of the library in numbers, and a footer. The hero is drawn, with no live
/// content, so the first frame stays cheap in the browser; an example is built when its card first scrolls into view.
/// </summary>
public sealed class GalleryHomeView : UserControl
{
    private const double ContentMaxWidth = 1120;
    private const double OneColumnBelow = 760;
    private const double ThreeColumnsFrom = 1000;

    /// <summary>The languages the hosts offer (en-US, zh-CN, zh-TW).</summary>
    private const int LanguageCount = 3;

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

    public GalleryHomeView(GalleryShell shell)
    {
        _shell = shell;
        // Stretch with a maximum width: the column fills a narrow window and is centered in a wide one.
        var column = new StackPanel { Spacing = 72, MaxWidth = ContentMaxWidth, HorizontalAlignment = HorizontalAlignment.Stretch };
        column.Children.Add(BuildHero());
        column.Children.Add(BuildLive());
        column.Children.Add(BuildExplode());
        column.Children.Add(BuildStats());
        column.Children.Add(BuildFriends());
        column.Children.Add(BuildCta());
        column.Children.Add(BuildFooter());
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BringIntoViewOnFocusChange = false,
            Content = new Border { Padding = new Thickness(24, 24, 24, 48), Child = column }
        };
        KeepAtTopUntilTouched(scroll);
        Content = scroll;
    }

    /// <summary>
    /// The page opens at the top and stays there until the visitor scrolls. A live example that takes the focus or asks to be
    /// brought into view when it is built (the cards and the exploded view are built as they come into view) would otherwise
    /// carry the page away from the top; the first wheel, key or press hands the scroll over.
    /// </summary>
    private static void KeepAtTopUntilTouched(ScrollViewer scroll)
    {
        var touched = false;
        void Touch(object? sender, RoutedEventArgs e) => touched = true;
        scroll.AddHandler(PointerWheelChangedEvent, Touch, RoutingStrategies.Tunnel);
        scroll.AddHandler(PointerPressedEvent, Touch, RoutingStrategies.Tunnel);
        scroll.AddHandler(KeyDownEvent, Touch, RoutingStrategies.Tunnel);
        scroll.ScrollChanged += (_, _) =>
        {
            if (!touched && scroll.Offset.Y > 0)
            {
                scroll.Offset = default;
            }
        };
    }

    /// <summary>The cards of the live examples, in order.</summary>
    public IReadOnlyList<LiveCard> Cards => _cards;

    #region Hero

    private Control BuildHero()
    {
        var badge = new Badge { Tone = BadgeTone.Info, Icon = new ArcGlyph { Data = ArcIcons.Sparkles, Size = 12 }, HorizontalAlignment = HorizontalAlignment.Center };
        GalleryText.Bind(badge, ContentControl.ContentProperty, GalleryShellLangResourceKind.HomeBadge);

        var title = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 52, FontWeight.Medium);
        title.TextAlignment = TextAlignment.Center;
        title.MaxWidth = 720;
        title.TextWrapping = TextWrapping.Wrap;
        GalleryText.Bind(title, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeTitle);

        var subtitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 16);
        subtitle.TextAlignment = TextAlignment.Center;
        subtitle.MaxWidth = 520;
        GalleryText.BindFormat(subtitle, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeSubtitle,
            GalleryCatalog.Count, GalleryCatalog.Categories.Length, GalleryCatalog.Blocks.AllEntries.Count());

        var explore = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Medium, Content = WithArrow(GalleryShellLangResourceKind.HomeExplore) };
        explore.Click += (_, _) => _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Components));
        var docs = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Medium, Content = WithArrow(GalleryShellLangResourceKind.HomeReadDocs) };
        docs.Click += (_, _) => _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Docs));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        actions.Children.Add(explore);
        actions.Children.Add(docs);

        var command = GalleryUi.Mono($"$ {InstallCommand}", ArcDesignTokenKind.TextSecondary, 13);
        command.VerticalAlignment = VerticalAlignment.Center;
        var copy = new CopyButton { Value = InstallCommand, IsIconOnly = true };
        var install = new Border
        {
            Padding = new Thickness(14, 4, 6, 4),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { command, copy } }
        };
        ArcTokens.Bind(install, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(install, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var content = new StackPanel { Spacing = 20, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(24, 80) };
        content.Children.Add(badge);
        content.Children.Add(title);
        content.Children.Add(subtitle);
        content.Children.Add(actions);
        content.Children.Add(install);

        var surface = new AuroraSurface();
        surface.Children.Add(content);
        var hero = new Border { CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1), ClipToBounds = true, Child = surface };
        ArcTokens.Bind(hero, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return hero;
    }

    private static Control WithArrow(GalleryShellLangResourceKind label)
    {
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(text, TextBlock.TextProperty, label);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(text);
        row.Children.Add(new ArcGlyph { Data = ArcIcons.ArrowRight, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    #endregion

    #region Live examples

    private Control BuildLive()
    {
        var title = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 30, FontWeight.Medium);
        GalleryText.Bind(title, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeWallTitle);
        var subtitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 14);
        GalleryText.Bind(subtitle, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeWallSubtitle);
        var heading = new StackPanel { Spacing = 6 };
        heading.Children.Add(title);
        heading.Children.Add(subtitle);

        var all = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, VerticalAlignment = VerticalAlignment.Bottom };
        var allLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.BindFormat(allLabel, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeBrowseAll, GalleryCatalog.Count);
        var allRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        allRow.Children.Add(allLabel);
        allRow.Children.Add(new ArcGlyph { Data = ArcIcons.ArrowRight, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        all.Content = allRow;
        all.Click += (_, _) => _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Components));

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(all, 1);
        header.Children.Add(heading);
        header.Children.Add(all);

        var grid = new UniformGrid { Columns = 3 };
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
        grid.SizeChanged += (_, e) => grid.Columns = e.NewSize.Width < OneColumnBelow ? 1 : e.NewSize.Width < ThreeColumnsFrom ? 2 : 3;

        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(header);
        section.Children.Add(grid);
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
            Margin = new Thickness(0, 0, 14, 14);
            Padding = new Thickness(14, 14, 14, 8);
            CornerRadius = new CornerRadius(20);
            BorderThickness = new Thickness(1);
            ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            // The demo is built when the card first reaches the viewport, not with the page.
            EffectiveViewportChanged += (_, e) =>
            {
                if (e.EffectiveViewport.Height > 0)
                {
                    EnsureCreated();
                }
            };
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
        var title = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 30, FontWeight.Medium);
        GalleryText.Bind(title, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeExplodeTitle);
        var subtitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 14);
        GalleryText.Bind(subtitle, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeExplodeSubtitle);

        // The view is a tall track in this page's scroll viewer (ScrollMode.Page) and is built when it first comes into view.
        var host = ExplodeHost = new Border { MinHeight = 560 };
        host.EffectiveViewportChanged += (_, e) =>
        {
            if (e.EffectiveViewport.Height > 0)
            {
                EnsureExplodeCreated();
            }
        };

        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(new StackPanel { Spacing = 6, Children = { title, subtitle } });
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

    #region Friends and call to action

    private Control BuildFriends()
    {
        var title = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 30, FontWeight.Medium);
        GalleryText.Bind(title, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeFriendsTitle);
        var subtitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 14);
        GalleryText.Bind(subtitle, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeFriendsSubtitle);

        var all = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, VerticalAlignment = VerticalAlignment.Bottom, Content = GalleryFriendsView.LabelWithGlyph(GalleryShellLangResourceKind.HomeSeeAll, ArcIcons.ArrowRight) };
        all.Click += (_, _) => _shell.Navigate(GalleryRoute.Friends);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(all, 1);
        header.Children.Add(new StackPanel { Spacing = 6, Children = { title, subtitle } });
        header.Children.Add(all);

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
        Avalonia.Automation.AutomationProperties.SetName(carousel, GalleryText.Get(GalleryShellLangResourceKind.HomeFriendsTitle));
        var grid = carousel;

        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(header);
        section.Children.Add(grid);
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
        Avalonia.Automation.AutomationProperties.SetName(visit, site.Name);
        visit.Click += (_, _) => _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(site.Url);

        var body = new StackPanel { Spacing = 6, Margin = new Thickness(18, 14, 18, 18) };
        body.Children.Add(name);
        body.Children.Add(description);
        body.Children.Add(new Border { Margin = new Thickness(0, 6, 0, 0), Child = visit });
        var stack = new StackPanel();
        stack.Children.Add(new FriendMark(site, 56, 120));
        stack.Children.Add(body);
        var frame = new Border { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), ClipToBounds = true, Child = stack };
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        return frame;
    }

    /// <summary>The closing band: a line, the install command to copy and the way in.</summary>
    /// <summary>The closing call to action: the library's own <see cref="CtaSection"/> block.</summary>
    private Control BuildCta()
    {
        var primary = new CtaAction();
        var cta = new CtaSection { Variant = CtaVariant.Centered, PrimaryAction = primary, IsSecondaryActionVisible = false };
        void Refresh()
        {
            cta.Title = GalleryText.Get(GalleryShellLangResourceKind.HomeCtaTitle);
            cta.Description = GalleryText.Get(GalleryShellLangResourceKind.HomeCtaSubtitle);
            primary.Label = GalleryText.Get(GalleryShellLangResourceKind.HomeExplore);
        }
        Refresh();
        // The text is plain strings, not bindings: it is set again when the language changes.
        GalleryText.Track(cta, Refresh);
        cta.ActionInvoked += (_, e) =>
        {
            e.Handled = true;
            _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Components));
        };
        return cta;
    }

    #endregion

    #region Numbers and footer

    private Control BuildStats()
    {
        var grid = new UniformGrid { Columns = 4 };
        AddStat(grid, GalleryCatalog.Count, GalleryShellLangResourceKind.HomeStatComponents);
        AddStat(grid, GalleryCatalog.Categories.Length, GalleryShellLangResourceKind.HomeStatGroups);
        AddStat(grid, GalleryCatalog.Blocks.AllEntries.Count(), GalleryShellLangResourceKind.HomeStatBlocks);
        AddStat(grid, LanguageCount, GalleryShellLangResourceKind.HomeStatLanguages);
        grid.SizeChanged += (_, e) => grid.Columns = e.NewSize.Width < OneColumnBelow ? 2 : 4;
        var frame = new Border { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), ClipToBounds = true, Child = grid };
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return frame;
    }

    private static void AddStat(Panel grid, int value, GalleryShellLangResourceKind label)
    {
        var number = GalleryUi.Text(value.ToString(System.Globalization.CultureInfo.InvariantCulture), ArcDesignTokenKind.Foreground, 30, FontWeight.Medium);
        var caption = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 13);
        GalleryText.Bind(caption, TextBlock.TextProperty, label);
        var cell = new StackPanel { Spacing = 2, Margin = new Thickness(20, 18) };
        cell.Children.Add(number);
        cell.Children.Add(caption);
        grid.Children.Add(cell);
    }

    /// <summary>The page footer: the library's own <see cref="SiteFooter"/> block in its one-row layout.</summary>
    private Control BuildFooter()
    {
        var docs = new SiteFooterLink();
        var changelog = new SiteFooterLink();
        var friends = new SiteFooterLink();
        var github = new SiteFooterLink { Label = "GitHub", IsExternal = true };
        var footer = new SiteFooter
        {
            Variant = SiteFooterVariant.Minimal,
            BrandName = "TKCode.Arc",
            // The Gallery's own logo, not the arch of the block's default.
            BrandMark = new Image { Source = GalleryBrand.CreateLogo(), Width = 22, Height = 22 },
            // The brand line is the same in every language: product names, not translated text.
            Tagline = "Powered by Avalonia and .NET 11",
            IsStatusVisible = false,
            BorderThickness = new Thickness(0)
        };
        footer.Links = new Avalonia.Collections.AvaloniaList<SiteFooterLink> { docs, changelog, friends };
        footer.SocialLinks.Add(github);
        void Refresh()
        {
            docs.Label = GalleryText.Get(GalleryShellLangResourceKind.Category_Docs);
            changelog.Label = GalleryText.Get(GalleryShellLangResourceKind.NavChangelog);
            friends.Label = GalleryText.Get(GalleryShellLangResourceKind.NavFriends);
        }
        Refresh();
        GalleryText.Track(footer, Refresh);
        footer.Navigate += (_, e) =>
        {
            e.Handled = true;
            if (ReferenceEquals(e.Link, docs))
            {
                _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Docs));
            }
            else if (ReferenceEquals(e.Link, changelog))
            {
                _shell.Navigate(GalleryRoute.To(GalleryCatalog.Docs.Entries.First(entry => entry.ClassName == "DocsChangelog")));
            }
            else if (ReferenceEquals(e.Link, friends))
            {
                _shell.Navigate(GalleryRoute.Friends);
            }
            else if (ReferenceEquals(e.Link, github))
            {
                _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(GalleryGitHub.RepositoryUrl);
            }
        };
        return footer;
    }

    #endregion
}
