using System.Runtime.CompilerServices;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The home page, written as TKCode's product site: what TKCode is, its size in numbers, the four things it does, what you
/// can build, how it works, how to start, where it runs and who supports it. It shows no components or project cards: those
/// have pages of their own (<c>/components</c>, <c>/projects</c>). The site says it runs in WebAssembly and offers the desktop app; the desktop
/// app shows its version and updates there instead (<see cref="GalleryHost"/>). The hero first draws on the static
/// <see cref="AuroraSurface"/>; the live dot field follows once the first frame is out, since it lives in the Special
/// components, which the browser host loads on demand.
/// </summary>
public sealed class GalleryHomeView : UserControl
{
    private const double HeroPhoneSize = 44;
    private const double HeroWideSize = 88;

    /// <summary>The community's QQ group.</summary>
    internal const string CommunityGroupNumber = "922379598";

    /// <summary>The app and item templates of TKCode.Arc.Templates.</summary>
    private const int TemplateCount = 13;

    /// <summary>The icons of TKCode.Arc.Icons.Lucide, which this assembly does not reference.</summary>
    private const int LucideIconCount = 2155;

    private readonly GalleryShell _shell;
    private readonly ScrollViewer _scroll;
    private readonly Panel _heroLayer = new();
    private readonly Border _heroContent = new();
    private readonly TextBlock _updateTitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 16, FontWeight.Medium);
    private readonly TextBlock _updateHint = GalleryUi.Mono(GalleryHost.Version, ArcDesignTokenKind.TextMuted, 12.5);
    private bool _touched;

    public GalleryHomeView(GalleryShell shell)
    {
        _shell = shell;
        var column = new StackPanel { Spacing = 72, MaxWidth = GallerySite.PageMaxWidth, HorizontalAlignment = HorizontalAlignment.Stretch };
        column.Children.Add(BuildNumbers());
        column.Children.Add(BuildAreas());
        column.Children.Add(BuildUses());
        column.Children.Add(BuildPrinciples());
        column.Children.Add(BuildStart());
        column.Children.Add(BuildSupport());
        var host = new StackPanel { Spacing = 16 };
        host.Children.Add(GalleryHost.IsWeb ? BuildWebStrip() : BuildDesktopStrip());
        if (GalleryHost.IsWeb)
        {
            Download = new GalleryDownloadSection { IsVisible = false };
            host.Children.Add(Download);
        }
        column.Children.Add(host);
        column.Children.Add(BuildCommunity());
        column.Children.Add(BuildSupporters());
        column.Children.Add(GallerySiteFooter.Create(shell));
        var margins = new Border { Padding = new Thickness(24, 40, 24, 0), Child = column };
        margins.SizeChanged += (_, e) =>
        {
            var phone = e.NewSize.Width < GallerySite.PhoneWidth;
            margins.Padding = phone ? new Thickness(16, 32, 16, 0) : new Thickness(24, 40, 24, 0);
            column.Spacing = phone ? 56 : 72;
        };

        var page = new StackPanel();
        page.Children.Add(BuildHero());
        page.Children.Add(margins);
        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            BringIntoViewOnFocusChange = false,
            Content = page
        };
        KeepAtTopUntilTouched(_scroll);
        Content = _scroll;

        if (GalleryHost.IsDesktop)
        {
            AttachedToVisualTree += (_, _) => GalleryUpdates.Changed += SyncUpdate;
            DetachedFromVisualTree += (_, _) => GalleryUpdates.Changed -= SyncUpdate;
            GalleryText.Track(this, SyncUpdate);
        }
        // After the first frame, so the dot field never holds up the page.
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => _ = AddDotFieldAsync(), DispatcherPriority.Background);
    }

    /// <summary>The list of files and the comparison, shown when asked for; null in the desktop app.</summary>
    internal GalleryDownloadSection? Download { get; }

    /// <summary>The site's line about WebAssembly and the desktop app; null in the desktop app.</summary>
    internal Control? WebStrip { get; private set; }

    /// <summary>Whether the live dot field replaced the hero's static background.</summary>
    internal bool HasDotField { get; private set; }

    /// <summary>Shows the files of the desktop app and why, and scrolls to them.</summary>
    public void ShowDownload()
    {
        if (Download is not { } section)
        {
            return;
        }
        _touched = true;
        section.IsVisible = true;
        Dispatcher.UIThread.Post(() => section.BringIntoView(new Rect(section.Bounds.Size)), DispatcherPriority.Background);
    }

    /// <summary>Hands the scroll over, as the first wheel, key or press does (tools that scroll the page themselves).</summary>
    public void MarkTouched() => _touched = true;

    /// <summary>
    /// The page opens at the top and stays there until the visitor scrolls, so nothing that is laid out late carries it away;
    /// the first wheel, key or press hands the scroll over.
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
        var badge = new Badge { Tone = BadgeTone.Neutral, HorizontalAlignment = HorizontalAlignment.Left };
        GalleryText.Bind(badge, ContentControl.ContentProperty, GalleryHost.IsWeb ? GalleryShellLangResourceKind.HomeBadge : GalleryShellLangResourceKind.HomeDesktopBadge);

        var title = new TextReveal { Level = TextLevel.H1, FontSize = HeroWideSize, MaxWidthInChars = 0 };
        GalleryText.Bind(title, TextReveal.TextProperty, GalleryShellLangResourceKind.HomeTitle);

        var subtitle = GallerySite.Text(GalleryShellLangResourceKind.HomeSubtitle, ArcDesignTokenKind.TextSecondary, 19);
        subtitle.MaxWidth = 760;
        subtitle.HorizontalAlignment = HorizontalAlignment.Left;

        var actions = GallerySite.Actions(
            GallerySite.Action(GalleryShellLangResourceKind.HomeExplore, ButtonVariant.Primary, () => _shell.Navigate(GalleryRoute.ComponentsIndex), ArcIcons.ArrowRight),
            GallerySite.Action(GalleryShellLangResourceKind.NavProjects, ButtonVariant.Secondary, () => _shell.Navigate(GalleryRoute.Projects)));

        var content = new StackPanel { Spacing = 22, MaxWidth = GallerySite.PageMaxWidth, HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(badge);
        content.Children.Add(title);
        content.Children.Add(subtitle);
        content.Children.Add(actions);

        _heroContent.Child = content;
        _heroContent.Padding = new Thickness(24, 104, 24, 96);
        _heroContent.SizeChanged += (_, e) =>
        {
            var phone = e.NewSize.Width < GallerySite.PhoneWidth;
            _heroContent.Padding = phone ? new Thickness(16, 56, 16, 48) : new Thickness(24, 104, 24, 96);
            subtitle.FontSize = phone ? 16 : 19;
            // The title follows the width between a phone's and a wide window's size.
            title.FontSize = Math.Clamp(e.NewSize.Width * 0.075, HeroPhoneSize, HeroWideSize);
        };

        var surface = new AuroraSurface();
        surface.Children.Add(_heroContent);
        _heroLayer.Children.Add(surface);
        return _heroLayer;
    }

    /// <summary>Puts the hero's content in the live dot field; the static hero stays if the components cannot be loaded.</summary>
    private async Task AddDotFieldAsync()
    {
        // A still field adds nothing over the static dots, and motion off is often what a slow device needs.
        if (HasDotField || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || !await GalleryModules.EnsureSpecialAsync())
        {
            return;
        }
        // The page can be attached again while the components load: only the first call that gets here wraps the hero. A second
        // field would draw its dots over the hero, and wrapping the same content twice throws in the middle of a layout pass.
        if (HasDotField)
        {
            return;
        }
        HasDotField = true;
        WrapHeroInDotField();
    }

    // In a method of its own: the runtime resolves DotGrid's assembly when it compiles this, which must not happen before
    // GalleryModules has loaded it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WrapHeroInDotField()
    {
        var surface = (AuroraSurface)_heroLayer.Children[0];
        surface.Children.Remove(_heroContent);
        surface.Children.Add(new DotGrid { Content = _heroContent, Gap = 24, DotSize = 2, Radius = 180 });
    }

    #endregion

    #region Below the hero

    /// <summary>The size of the library. The first three come from the catalog; a new component counts by itself.</summary>
    private static Control BuildNumbers()
    {
        var specials = GalleryCatalog.Special.Sum(category => category.AllEntries.Count());
        var grid = new GalleryGrid { MinItemWidth = 150, MaxColumns = 6, Spacing = 16 };
        grid.Children.Add(Number(GalleryCatalog.Count, GalleryShellLangResourceKind.HomeNumberComponents));
        grid.Children.Add(Number(GalleryCatalog.Blocks.AllEntries.Count(), GalleryShellLangResourceKind.HomeNumberBlocks));
        grid.Children.Add(Number(specials, GalleryShellLangResourceKind.HomeNumberEffects));
        grid.Children.Add(Number(LucideIconCount, GalleryShellLangResourceKind.HomeNumberIcons));
        grid.Children.Add(Number(TemplateCount, GalleryShellLangResourceKind.HomeNumberTemplates));
        grid.Children.Add(Number(GalleryLanguages.All.Count, GalleryShellLangResourceKind.HomeNumberLanguages));
        return grid;
    }

    private static Control Number(int value, GalleryShellLangResourceKind label)
    {
        var counter = new AnimatedCounter { Value = value, FontSize = 34, HorizontalAlignment = HorizontalAlignment.Left };
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(counter);
        stack.Children.Add(GallerySite.Text(label, ArcDesignTokenKind.TextMuted, 13));
        return stack;
    }

    /// <summary>The four things TKCode does; each names the project that does it and goes to its page.</summary>
    private Control BuildAreas()
    {
        var grid = new GalleryGrid { MinItemWidth = 240, MaxColumns = 4, Spacing = 14 };
        grid.Children.Add(Area(ArcIcons.Layers, GalleryShellLangResourceKind.HomeAreaInterface, GalleryShellLangResourceKind.HomeAreaInterfaceText, "Arc", () => GalleryRoute.ComponentsIndex));
        grid.Children.Add(Area(ArcIcons.Zap, GalleryShellLangResourceKind.HomeAreaText, GalleryShellLangResourceKind.HomeAreaTextText, "StreamMark", () => GalleryRoute.StreamMark));
        grid.Children.Add(Area(ArcIcons.Code, GalleryShellLangResourceKind.HomeAreaScript, GalleryShellLangResourceKind.HomeAreaScriptText, "Jaunt", () => GalleryRoute.Projects));
        grid.Children.Add(Area(ArcIcons.Search, GalleryShellLangResourceKind.HomeAreaDebug, GalleryShellLangResourceKind.HomeAreaDebugText, "Spyglass", () => GalleryRoute.Projects));
        return Section(GalleryShellLangResourceKind.HomeAreasEyebrow, GalleryShellLangResourceKind.HomeAreasTitle, null, grid);
    }

    private Control Area(Geometry glyph, GalleryShellLangResourceKind title, GalleryShellLangResourceKind text, string project, Func<GalleryRoute> route)
    {
        var mark = GalleryUi.Glyph(glyph, 20, ArcDesignTokenKind.Foreground);
        mark.HorizontalAlignment = HorizontalAlignment.Left;
        var open = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-10, 2, 0, 0) };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new TextBlock { Text = project, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new ArcGlyph { Data = ArcIcons.ArrowRight, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        open.Content = row;
        open.Click += (_, _) => _shell.Navigate(route());
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(mark);
        stack.Children.Add(GallerySite.Text(title, ArcDesignTokenKind.Foreground, 19, FontWeight.Medium));
        stack.Children.Add(GallerySite.Text(text, ArcDesignTokenKind.TextSecondary, 14));
        stack.Children.Add(open);
        return GallerySite.Panel(stack, padding: 18);
    }

    /// <summary>What people build: the finished app templates, which are real and can be created with one command.</summary>
    private Control BuildUses()
    {
        var grid = new GalleryGrid { MinItemWidth = 300, MaxColumns = 3, Spacing = 28 };
        grid.Children.Add(Use(GalleryShellLangResourceKind.HomeUseDesktop, GalleryShellLangResourceKind.HomeUseDesktopText, "tkcode-arc"));
        grid.Children.Add(Use(GalleryShellLangResourceKind.HomeUseChat, GalleryShellLangResourceKind.HomeUseChatText, "tkcode-arc-ai-chat"));
        grid.Children.Add(Use(GalleryShellLangResourceKind.HomeUseAdmin, GalleryShellLangResourceKind.HomeUseAdminText, "tkcode-arc-admin"));
        grid.Children.Add(Use(GalleryShellLangResourceKind.HomeUseDevTool, GalleryShellLangResourceKind.HomeUseDevToolText, "tkcode-arc-devtool"));
        grid.Children.Add(Use(GalleryShellLangResourceKind.HomeUseWorkflow, GalleryShellLangResourceKind.HomeUseWorkflowText, "tkcode-arc-workflow"));
        grid.Children.Add(Use(GalleryShellLangResourceKind.HomeUseAnalytics, GalleryShellLangResourceKind.HomeUseAnalyticsText, "tkcode-arc-analytics"));
        return Section(GalleryShellLangResourceKind.HomeUsesEyebrow, GalleryShellLangResourceKind.HomeUsesTitle, GalleryShellLangResourceKind.HomeUsesText, grid);
    }

    private static Control Use(GalleryShellLangResourceKind title, GalleryShellLangResourceKind text, string template)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(GallerySite.Text(title, ArcDesignTokenKind.Foreground, 16, FontWeight.Medium));
        stack.Children.Add(GallerySite.Text(text, ArcDesignTokenKind.TextSecondary, 14));
        stack.Children.Add(GalleryUi.Mono($"dotnet new {template}", ArcDesignTokenKind.TextMuted, 12));
        return stack;
    }

    private static Control BuildPrinciples()
    {
        var grid = new GalleryGrid { MinItemWidth = 260, MaxColumns = 3, Spacing = 32 };
        grid.Children.Add(Point(GalleryShellLangResourceKind.HomePointNative, GalleryShellLangResourceKind.HomePointNativeText));
        grid.Children.Add(Point(GalleryShellLangResourceKind.HomePointLight, GalleryShellLangResourceKind.HomePointLightText));
        grid.Children.Add(Point(GalleryShellLangResourceKind.HomePointOpen, GalleryShellLangResourceKind.HomePointOpenText));
        return Section(GalleryShellLangResourceKind.HomePrinciplesEyebrow, GalleryShellLangResourceKind.HomePrinciplesTitle, null, grid);
    }

    /// <summary>Three real commands, in the order they run.</summary>
    private Control BuildStart()
    {
        var grid = new GalleryGrid { MinItemWidth = 300, MaxColumns = 3, Spacing = 14 };
        grid.Children.Add(Step(1, GalleryShellLangResourceKind.HomeStepInstall, "dotnet new install TKCode.Arc.Templates"));
        grid.Children.Add(Step(2, GalleryShellLangResourceKind.HomeStepCreate, "dotnet new tkcode-arc -n MyApp"));
        grid.Children.Add(Step(3, GalleryShellLangResourceKind.HomeStepRun, "cd MyApp && dotnet run"));
        var docs = GallerySite.Action(GalleryShellLangResourceKind.HomeReadDocs, ButtonVariant.Secondary, () => _shell.Navigate(GallerySite.Docs("DocsInstallation")), ArcIcons.ArrowRight);
        docs.HorizontalAlignment = HorizontalAlignment.Left;
        var body = new StackPanel { Spacing = 18 };
        body.Children.Add(grid);
        body.Children.Add(docs);
        return Section(GalleryShellLangResourceKind.HomeStartEyebrow, GalleryShellLangResourceKind.HomeStartTitle, null, body);
    }

    private static Control Step(int number, GalleryShellLangResourceKind title, string command)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(GalleryUi.Mono(number.ToString(System.Globalization.CultureInfo.InvariantCulture), ArcDesignTokenKind.TextMuted, 13));
        stack.Children.Add(GallerySite.Text(title, ArcDesignTokenKind.Foreground, 16, FontWeight.Medium));
        stack.Children.Add(GallerySite.Install(command));
        return stack;
    }

    /// <summary>Where it runs, and what it offers AI assistants.</summary>
    private Control BuildSupport()
    {
        var runs = new StackPanel { Spacing = 14 };
        runs.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.HomeRunsTitle, ArcDesignTokenKind.Foreground, 19, FontWeight.Medium));
        runs.Children.Add(Fact(GalleryShellLangResourceKind.HomeRunsSystems, GalleryShellLangResourceKind.HomeRunsSystemsValue));
        runs.Children.Add(FactText(GalleryShellLangResourceKind.HomeRunsDotNet, ".NET 8 · 10 · 11"));
        runs.Children.Add(FactText(GalleryShellLangResourceKind.HomeRunsAvalonia, $"Avalonia {typeof(Avalonia.Application).Assembly.GetName().Version?.ToString(3)}"));
        runs.Children.Add(Fact(GalleryShellLangResourceKind.HomeRunsLicense, GalleryShellLangResourceKind.FooterLicense));

        var ai = new StackPanel { Spacing = 14 };
        ai.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.HomeAiTitle, ArcDesignTokenKind.Foreground, 19, FontWeight.Medium));
        ai.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.HomeAiText, ArcDesignTokenKind.TextSecondary, 14));
        ai.Children.Add(GallerySite.Checks(
            GallerySite.Text(GalleryShellLangResourceKind.HomeAiLlms),
            GallerySite.Text(GalleryShellLangResourceKind.HomeAiSkill),
            GallerySite.Text(GalleryShellLangResourceKind.HomeAiMcp)));
        var open = GallerySite.Action(GalleryShellLangResourceKind.HomeAiOpen, ButtonVariant.Secondary, () => _shell.Navigate(GallerySite.Docs("DocsAiAndMcp")), ArcIcons.ArrowRight, ControlSize.Small);
        open.HorizontalAlignment = HorizontalAlignment.Left;
        ai.Children.Add(open);

        var grid = new GalleryGrid { MinItemWidth = 340, MaxColumns = 2, Spacing = 16 };
        grid.Children.Add(GallerySite.Panel(runs, ArcDesignTokenKind.SurfaceMuted, 24));
        grid.Children.Add(GallerySite.Panel(ai, ArcDesignTokenKind.SurfaceMuted, 24));
        return grid;
    }

    private static Control Fact(GalleryShellLangResourceKind label, GalleryShellLangResourceKind value) => FactRow(label, GallerySite.Text(value, ArcDesignTokenKind.Foreground, 14));

    private static Control FactText(GalleryShellLangResourceKind label, string value) => FactRow(label, GalleryUi.Text(value, ArcDesignTokenKind.Foreground, 14));

    private static Control FactRow(GalleryShellLangResourceKind label, TextBlock value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*"), ColumnSpacing = 12 };
        Grid.SetColumn(value, 1);
        row.Children.Add(GallerySite.Text(label, ArcDesignTokenKind.TextMuted, 14));
        row.Children.Add(value);
        return row;
    }

    /// <summary>
    /// The community's QQ group. The code is decoded on first sight, not with the page; the number can be copied, since a
    /// phone cannot scan its own screen.
    /// </summary>
    private Control BuildCommunity()
    {
        var text = new StackPanel { Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(GallerySite.Heading(GalleryShellLangResourceKind.HomeCommunityEyebrow, GallerySite.Reveal(GalleryShellLangResourceKind.HomeCommunityTitle), GalleryShellLangResourceKind.HomeCommunityText));
        var number = GalleryUi.Text(CommunityGroupNumber, ArcDesignTokenKind.Foreground, 15, FontWeight.Medium);
        number.VerticalAlignment = VerticalAlignment.Center;
        var copy = new CopyButton { Value = CommunityGroupNumber, IsIconOnly = true, VerticalAlignment = VerticalAlignment.Center };
        Avalonia.Automation.AutomationProperties.SetName(copy, GalleryText.Get(GalleryShellLangResourceKind.HomeCommunityCopy));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var group = GallerySite.Text(GalleryShellLangResourceKind.HomeCommunityGroup, ArcDesignTokenKind.TextMuted, 14);
        group.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(group);
        row.Children.Add(number);
        row.Children.Add(copy);
        row.VerticalAlignment = VerticalAlignment.Center;
        text.Children.Add(row);
        var issues = GallerySite.Action(GalleryShellLangResourceKind.AboutIssues, ButtonVariant.Secondary, () => GallerySite.Open(this, new Uri($"{GalleryGitHub.RepositoryUrl}/issues")), ArcIcons.ArrowUpRight, ControlSize.Small);
        issues.HorizontalAlignment = HorizontalAlignment.Left;
        text.Children.Add(issues);

        var code = new Image { Width = 244, Height = 310, Stretch = Stretch.Uniform };
        Avalonia.Automation.AutomationProperties.SetName(code, GalleryText.Get(GalleryShellLangResourceKind.HomeCommunityTitle));
        var frame = new Border { Width = 244, Height = 310, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center, Child = code };
        ArcTokens.Bind(frame, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusPanel);
        CommunityCode = code;
        _ = new GalleryFirstSight(frame, () => code.Source ??= GalleryBrand.CreateCommunityCode());

        var pane = new GalleryTwoPane { EndWidth = 244, StackBelow = 640, Spacing = 32 };
        pane.Children.Add(text);
        pane.Children.Add(frame);
        return GallerySite.Panel(pane, ArcDesignTokenKind.SurfaceMuted, 24);
    }

    /// <summary>The community's QR code.</summary>
    internal Image CommunityCode { get; private set; } = null!;

    /// <summary>The main sponsor on a line of its own, then the friends.</summary>
    private Control BuildSupporters()
    {
        var list = new StackPanel { Spacing = 12 };
        foreach (var sponsor in GalleryFriends.Sponsors)
        {
            list.Children.Add(Supporter(sponsor));
        }
        var friends = new GalleryGrid { MinItemWidth = 240, MaxColumns = 4, Spacing = 12 };
        foreach (var friend in GalleryFriends.Friends)
        {
            friends.Children.Add(Supporter(friend));
        }
        list.Children.Add(friends);
        var all = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left, Content = GalleryFriendsView.LabelWithGlyph(GalleryShellLangResourceKind.HomeSeeAll, ArcIcons.ArrowRight) };
        all.Click += (_, _) => _shell.Navigate(GalleryRoute.Friends);
        list.Children.Add(all);
        return Section(null, GalleryShellLangResourceKind.HomeFriendsTitle, GalleryShellLangResourceKind.HomeFriendsSubtitle, list);
    }

    private Control Supporter(GalleryFriend site)
    {
        var description = GallerySite.Text(site.Description, ArcDesignTokenKind.TextSecondary, 13);
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(GalleryUi.Text(site.Name, ArcDesignTokenKind.Foreground, 15, FontWeight.Medium));
        stack.Children.Add(description);
        var visit = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(-10, 2, 0, 0), Content = GalleryFriendsView.LabelWithGlyph(GalleryShellLangResourceKind.FriendsVisit, ArcIcons.ArrowUpRight) };
        Avalonia.Automation.AutomationProperties.SetName(visit, site.Name);
        visit.Click += (_, _) => GallerySite.Open(this, site.Url);
        stack.Children.Add(visit);
        return GallerySite.Panel(stack, padding: 16);
    }

    private static Control Section(GalleryShellLangResourceKind? eyebrow, GalleryShellLangResourceKind title, GalleryShellLangResourceKind? text, Control body)
    {
        var section = new StackPanel { Spacing = 24 };
        section.Children.Add(GallerySite.Heading(eyebrow, GallerySite.Reveal(title), text));
        section.Children.Add(body);
        return section;
    }

    private static Control Point(GalleryShellLangResourceKind title, GalleryShellLangResourceKind text)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(GallerySite.Text(title, ArcDesignTokenKind.Foreground, 19, FontWeight.Medium));
        stack.Children.Add(GallerySite.Text(text, ArcDesignTokenKind.TextSecondary, 14.5));
        return stack;
    }

    private Control Strip(Control text, params Control[] actions)
    {
        var row = new GalleryTwoPane { EndWidth = 300, StackBelow = 760, Spacing = 16 };
        row.Children.Add(text);
        var buttons = GallerySite.Actions(actions);
        buttons.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(buttons);
        return GallerySite.Panel(row);
    }

    private Control BuildWebStrip()
    {
        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.HomeWebTitle, ArcDesignTokenKind.Foreground, 16, FontWeight.Medium));
        text.Children.Add(GallerySite.Text(GalleryShellLangResourceKind.HomeWebNoteText, ArcDesignTokenKind.TextSecondary, 13.5));
        WebStrip = Strip(text, GallerySite.Action(GalleryShellLangResourceKind.NavDownload, ButtonVariant.Primary, ShowDownload, ArcIcons.Download, ControlSize.Small));
        return WebStrip;
    }

    private Control BuildDesktopStrip()
    {
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(_updateTitle);
        text.Children.Add(_updateHint);
        return Strip(text,
            GallerySite.Action(GalleryShellLangResourceKind.SettingsAbout, ButtonVariant.Secondary, () => _shell.Navigate(GalleryRoute.Settings(GallerySettingsView.AboutPage)), null, ControlSize.Small),
            GallerySite.Action(GalleryShellLangResourceKind.AboutChangelog, ButtonVariant.Ghost, () => _shell.Navigate(GallerySite.Docs("DocsChangelog")), null, ControlSize.Small));
    }

    private void SyncUpdate()
    {
        _updateTitle.Text = GalleryUpdates.Status switch
        {
            GalleryUpdateStatus.Available => GalleryText.Format(GalleryShellLangResourceKind.UpdateAvailable, GalleryUpdates.Available!.Version),
            GalleryUpdateStatus.UpToDate => GalleryText.Get(GalleryShellLangResourceKind.UpdateUpToDate),
            GalleryUpdateStatus.Checking => GalleryText.Get(GalleryShellLangResourceKind.UpdateChecking),
            _ => GalleryText.Get(GalleryShellLangResourceKind.HomeUpdateTitle)
        };
    }

    #endregion
}
