using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

    /// <summary>The languages the hosts offer (en-US, zh-CN, zh-TW).</summary>
    private const int LanguageCount = 3;

    private const string InstallCommand = "dotnet add package TKCode.Arc --prerelease";

    /// <summary>The examples on the cards: the page they open and the demo they show (<c>Component.Name</c>).</summary>
    private static readonly (string ClassName, string DemoId)[] s_examples =
    [
        ("ButtonGroup", "ButtonGroup.LiveValue"),
        ("Gauge", "Gauge.Thresholds"),
        ("ChatThread", "ChatThread.ComposerOnItsOwn"),
        ("CommandPalette", "CommandPalette.InlineItems")
    ];

    private readonly GalleryShell _shell;
    private readonly List<LiveCard> _cards = new();

    public GalleryHomeView(GalleryShell shell)
    {
        _shell = shell;
        // Stretch with a maximum width: the column fills a narrow window and is centered in a wide one.
        var column = new StackPanel { Spacing = 56, MaxWidth = ContentMaxWidth, HorizontalAlignment = HorizontalAlignment.Stretch };
        column.Children.Add(BuildHero());
        column.Children.Add(BuildLive());
        column.Children.Add(BuildStats());
        column.Children.Add(BuildFooter());
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border { Padding = new Thickness(24, 24, 24, 48), Child = column }
        };
    }

    /// <summary>The cards of the live examples, in order.</summary>
    public IReadOnlyList<LiveCard> Cards => _cards;

    #region Hero

    private Control BuildHero()
    {
        var title = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 46, FontWeight.Medium);
        title.TextAlignment = TextAlignment.Center;
        title.MaxWidth = 680;
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

        var content = new StackPanel { Spacing = 20, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(24, 56) };
        content.Children.Add(title);
        content.Children.Add(subtitle);
        content.Children.Add(actions);
        content.Children.Add(install);

        var surface = new HeroSurface();
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

    /// <summary>
    /// The hero's ground: the muted surface with a sand glow in one corner and a sage one in the opposite corner, drawn with
    /// two radial gradients whose colors follow the theme.
    /// </summary>
    private sealed class HeroSurface : Panel
    {
        private readonly GradientStop _sand = new(Colors.Transparent, 0);
        private readonly GradientStop _sage = new(Colors.Transparent, 0);
        private readonly GradientStop _sandEdge = new(Colors.Transparent, 1);
        private readonly GradientStop _sageEdge = new(Colors.Transparent, 1);

        public HeroSurface()
        {
            ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            Children.Add(Glow(new RelativePoint(0.06, 1.04, RelativeUnit.Relative), 0.75, 0.95, _sand, _sandEdge));
            Children.Add(Glow(new RelativePoint(0.9, 0.06, RelativeUnit.Relative), 0.62, 0.9, _sage, _sageEdge));
            ActualThemeVariantChanged += (_, _) => Apply();
            Apply();
        }

        private static Border Glow(RelativePoint center, double radiusX, double radiusY, GradientStop inner, GradientStop outer)
        {
            var brush = new RadialGradientBrush
            {
                Center = center,
                GradientOrigin = center,
                RadiusX = new RelativeScalar(radiusX, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(radiusY, RelativeUnit.Relative)
            };
            brush.GradientStops.Add(inner);
            brush.GradientStops.Add(outer);
            return new Border { Background = brush, IsHitTestVisible = false };
        }

        private void Apply()
        {
            var dark = ActualThemeVariant == ThemeVariant.Dark;
            var sand = dark ? Color.FromRgb(0x4A, 0x37, 0x18) : Color.FromRgb(0xF2, 0xDC, 0xAE);
            var sage = dark ? Color.FromRgb(0x17, 0x33, 0x2E) : Color.FromRgb(0xCD, 0xE6, 0xDB);
            _sand.Color = sand;
            _sandEdge.Color = Color.FromArgb(0, sand.R, sand.G, sand.B);
            _sage.Color = sage;
            _sageEdge.Color = Color.FromArgb(0, sage.R, sage.G, sage.B);
        }
    }

    #endregion

    #region Live examples

    private Control BuildLive()
    {
        var title = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 30, FontWeight.Medium);
        GalleryText.Bind(title, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeLiveTitle);
        var subtitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 14);
        GalleryText.Bind(subtitle, TextBlock.TextProperty, GalleryShellLangResourceKind.HomeLiveSubtitle);
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

        var grid = new UniformGrid { Columns = 2 };
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
        grid.SizeChanged += (_, e) => grid.Columns = e.NewSize.Width < OneColumnBelow ? 1 : 2;

        var section = new StackPanel { Spacing = 20 };
        section.Children.Add(header);
        section.Children.Add(grid);
        return section;
    }

    /// <summary>One example card: the live demo above its title and an Open button for its page.</summary>
    public sealed class LiveCard : Border
    {
        private const double StageHeight = 280;

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
            Margin = new Thickness(0, 0, 0, 0);
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

    private Control BuildFooter()
    {
        var left = GalleryUi.Text("TKCode.Arc", ArcDesignTokenKind.TextMuted, 13);
        left.VerticalAlignment = VerticalAlignment.Center;

        var github = LinkButton(new TextBlock { Text = "GitHub" }, () => _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(GalleryGitHub.RepositoryUrl));
        var docs = new TextBlock();
        GalleryText.Bind(docs, TextBlock.TextProperty, GalleryShellLangResourceKind.Category_Docs);
        var changelog = new TextBlock();
        GalleryText.Bind(changelog, TextBlock.TextProperty, GalleryShellLangResourceKind.NavChangelog);

        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        links.Children.Add(LinkButton(docs, () => _shell.Navigate(_shell.RouteFor(GalleryRouteKind.Docs))));
        links.Children.Add(LinkButton(changelog, () => _shell.Navigate(GalleryRoute.To(GalleryCatalog.Docs.Entries.First(e => e.ClassName == "DocsChangelog")))));
        links.Children.Add(github);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(4, 0) };
        Grid.SetColumn(links, 1);
        row.Children.Add(left);
        row.Children.Add(links);
        return row;
    }

    private static Button LinkButton(TextBlock label, Action click)
    {
        var button = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = label };
        button.Click += (_, _) => click();
        return button;
    }

    #endregion
}
