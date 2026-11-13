using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Which page of a <see cref="HelpCenter"/> shows.</summary>
public enum HelpCenterView
{
    /// <summary>Search, categories and popular articles.</summary>
    Home,

    /// <summary>The articles of <see cref="HelpCenter.CurrentCategory"/>.</summary>
    Category,

    /// <summary>Articles matching <see cref="HelpCenter.SearchText"/>.</summary>
    Search,

    /// <summary><see cref="HelpCenter.CurrentArticle"/> with its table of contents.</summary>
    Article
}

/// <summary>
/// A help center: a home with a search field, categories and popular articles; search results that follow every
/// keystroke; a page per category; and an article view with a breadcrumb, a table of contents that scrolls to each
/// section, related articles and a "Was this helpful?" question that asks what was missing after a No.
/// <code>
/// &lt;arc:HelpCenter Title="How can we help?" /&gt;
/// </code>
/// Articles are Markdown (<see cref="HelpCenterArticle.Body"/>). Give your own <see cref="Categories"/> and
/// <see cref="Articles"/>; a help center without articles when it first loads shows a sample.
/// </summary>
[TemplatePart(HostPart, typeof(Decorator))]
[PseudoClasses(NarrowPseudoClass)]
public class HelpCenter : TemplatedControl
{
    public const string HostPart = "PART_Host";

    /// <summary>Set below the narrow width: categories take one column and the table of contents hides.</summary>
    public const string NarrowPseudoClass = ":narrow";

    #region Properties

    /// <summary>The heading of the home ("How can we help?").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<HelpCenter, string>(nameof(Title), "How can we help?");

    /// <summary>The name of the help center in the breadcrumb ("Help center").</summary>
    public static readonly StyledProperty<string> HomeLabelProperty =
        AvaloniaProperty.Register<HelpCenter, string>(nameof(HomeLabel), "Help center");

    /// <summary>The search text (two-way); typing shows results, clearing goes back.</summary>
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<HelpCenter, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The open article (two-way); null leaves the article view.</summary>
    public static readonly StyledProperty<HelpCenterArticle?> CurrentArticleProperty =
        AvaloniaProperty.Register<HelpCenter, HelpCenterArticle?>(nameof(CurrentArticle), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The open category (two-way).</summary>
    public static readonly StyledProperty<HelpCenterCategory?> CurrentCategoryProperty =
        AvaloniaProperty.Register<HelpCenter, HelpCenterCategory?>(nameof(CurrentCategory), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether a help center without articles when it first loads gets the sample (default true).</summary>
    public static readonly StyledProperty<bool> UsesSampleDataProperty =
        AvaloniaProperty.Register<HelpCenter, bool>(nameof(UsesSampleData), true);

    /// <summary>The page showing now.</summary>
    public static readonly DirectProperty<HelpCenter, HelpCenterView> ViewProperty =
        AvaloniaProperty.RegisterDirect<HelpCenter, HelpCenterView>(nameof(View), o => o.View);

    /// <summary>Raised when an article opens.</summary>
    public static readonly RoutedEvent<HelpCenterArticleEventArgs> ArticleOpenedEvent =
        RoutedEvent.Register<HelpCenter, HelpCenterArticleEventArgs>(nameof(ArticleOpened), RoutingStrategies.Bubble);

    /// <summary>Raised when the reader answers "Was this helpful?" (after a No, once they send or skip the comment).</summary>
    public static readonly RoutedEvent<HelpCenterFeedbackEventArgs> FeedbackGivenEvent =
        RoutedEvent.Register<HelpCenter, HelpCenterFeedbackEventArgs>(nameof(FeedbackGiven), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="HomeLabelProperty"/>
    public string HomeLabel { get => GetValue(HomeLabelProperty); set => SetValue(HomeLabelProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="CurrentArticleProperty"/>
    public HelpCenterArticle? CurrentArticle { get => GetValue(CurrentArticleProperty); set => SetValue(CurrentArticleProperty, value); }

    /// <inheritdoc cref="CurrentCategoryProperty"/>
    public HelpCenterCategory? CurrentCategory { get => GetValue(CurrentCategoryProperty); set => SetValue(CurrentCategoryProperty, value); }

    /// <inheritdoc cref="UsesSampleDataProperty"/>
    public bool UsesSampleData { get => GetValue(UsesSampleDataProperty); set => SetValue(UsesSampleDataProperty, value); }

    /// <inheritdoc cref="ViewProperty"/>
    public HelpCenterView View { get => _view; private set => SetAndRaise(ViewProperty, ref _view, value); }

    /// <summary>The categories, in the order they show.</summary>
    public AvaloniaList<HelpCenterCategory> Categories { get; } = new();

    /// <summary>The articles.</summary>
    public AvaloniaList<HelpCenterArticle> Articles { get; } = new();

    /// <inheritdoc cref="ArticleOpenedEvent"/>
    public event EventHandler<HelpCenterArticleEventArgs>? ArticleOpened
    {
        add => AddHandler(ArticleOpenedEvent, value);
        remove => RemoveHandler(ArticleOpenedEvent, value);
    }

    /// <inheritdoc cref="FeedbackGivenEvent"/>
    public event EventHandler<HelpCenterFeedbackEventArgs>? FeedbackGiven
    {
        add => AddHandler(FeedbackGivenEvent, value);
        remove => RemoveHandler(FeedbackGivenEvent, value);
    }

    #endregion

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<HelpCenter, double>(nameof(NarrowWidth), 720);

    internal static readonly StyledProperty<double> TocWidthProperty =
        AvaloniaProperty.Register<HelpCenter, double>(nameof(TocWidth), 200);

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    internal double TocWidth { get => GetValue(TocWidthProperty); set => SetValue(TocWidthProperty, value); }

    internal static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private HelpCenterView _view;
    private readonly Grid _root;
    private readonly WrapPanel _crumbs;
    private readonly StackPanel _hero;
    private readonly TextBlock _titleText;
    private readonly SearchField _search;
    private readonly ArcContentSwap _swap;
    private readonly SupportKitAnnouncer _announcer = new();
    private Decorator? _host;
    private HelpCenterArticlePage? _articlePage;
    private bool _loaded;
    private bool _narrow;
    private bool _navigating;

    /// <summary>Creates the help center.</summary>
    public HelpCenter()
    {
        BlockFocusModality.Watch();
        _crumbs = new WrapPanel { ItemSpacing = 2, LineSpacing = 2, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(_crumbs, "Breadcrumb");
        _titleText = SupportKit.Heading(Title, ArcDesignTokenKind.Type2XL, 1);
        _titleText.HorizontalAlignment = HorizontalAlignment.Center;
        _titleText.TextAlignment = TextAlignment.Center;
        _search = new SearchField { Placeholder = "Search articles", MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_search, "Search articles");
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !_navigating)
            {
                SetCurrentValue(SearchTextProperty, _search.Text);
            }
        };
        _search.SearchSubmitted += (_, e) =>
        {
            // Enter opens the first result.
            if (Results(e.Query).FirstOrDefault() is { } first)
            {
                OpenArticle(first);
            }
        };
        _hero = new StackPanel { Spacing = 14, Margin = new Thickness(0, 8, 0, 4), Children = { _titleText, _search } };
        _swap = new ArcContentSwap { Mode = ContentSwapMode.Slide, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };

        _root = SupportKit.Grid("*", "Auto,Auto,*", rowSpacing: 18);
        _root.Children.Add(_crumbs.At(0, 0));
        _root.Children.Add(_hero.At(0, 1));
        _root.Children.Add(_swap.At(0, 2));
        _root.Children.Add(_announcer.At(0, 2));

        Articles.CollectionChanged += (_, _) => Render(0);
        Categories.CollectionChanged += (_, _) => Render(0);
        AddHandler(KeyDownEvent, OnBlockKeyDown, RoutingStrategies.Bubble);
        Render(0);
    }

    #region Internals for tests

    internal SearchField SearchBox => _search;

    internal object? Page => _swap.Content;

    internal HelpCenterArticlePage? ArticlePage => _articlePage;

    internal IReadOnlyList<Button> Breadcrumb => _crumbs.Children.OfType<Button>().ToList();

    internal bool IsNarrow => _narrow;

    internal string? LastAnnouncement => _announcer.Announcement;

    #endregion

    #region Public navigation

    /// <summary>Opens <paramref name="article"/> and raises <see cref="ArticleOpened"/>.</summary>
    public void OpenArticle(HelpCenterArticle article) => SetCurrentValue(CurrentArticleProperty, article);

    /// <summary>Opens the page of <paramref name="category"/>.</summary>
    public void OpenCategory(HelpCenterCategory category)
    {
        _navigating = true;
        try
        {
            SetCurrentValue(CurrentArticleProperty, null);
            SetCurrentValue(SearchTextProperty, null);
            SetCurrentValue(CurrentCategoryProperty, category);
        }
        finally
        {
            _navigating = false;
        }
        Render(1);
    }

    /// <summary>Goes back to the home: clears the search, the category and the article.</summary>
    public void GoHome()
    {
        _navigating = true;
        try
        {
            SetCurrentValue(CurrentArticleProperty, null);
            SetCurrentValue(SearchTextProperty, null);
            SetCurrentValue(CurrentCategoryProperty, null);
        }
        finally
        {
            _navigating = false;
        }
        Render(-1);
    }

    /// <summary>Goes one level up: from an article to its category, from a category or a search to the home.</summary>
    public void GoBack()
    {
        if (View == HelpCenterView.Article && CategoryOf(CurrentArticle) is { } category && string.IsNullOrWhiteSpace(SearchText))
        {
            _navigating = true;
            try
            {
                SetCurrentValue(CurrentArticleProperty, null);
                SetCurrentValue(CurrentCategoryProperty, category);
            }
            finally
            {
                _navigating = false;
            }
            Render(-1);
        }
        else if (View == HelpCenterView.Article)
        {
            SetCurrentValue(CurrentArticleProperty, null);
        }
        else if (View != HelpCenterView.Home)
        {
            GoHome();
        }
    }

    /// <summary>The articles matching <paramref name="query"/>: titles first, then summaries, then bodies.</summary>
    public IReadOnlyList<HelpCenterArticle> Results(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<HelpCenterArticle>();
        }
        return Effective
            .Select(a => (Article: a, Score: SupportKit.Matches(a.Title, query) ? 3 : SupportKit.Matches(a.Summary, query) ? 2 : SupportKit.Matches(a.Body, query) ? 1 : 0))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Select(r => r.Article)
            .ToList();
    }

    #endregion

    private IEnumerable<HelpCenterArticle> Effective => Articles;

    internal HelpCenterCategory? CategoryOf(HelpCenterArticle? article) =>
        article is null ? null : Categories.FirstOrDefault(c => c.Id == article.CategoryId);

    #region Template, layout and properties

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Decorator>(HostPart);
        SupportKit.Mount(_host, _root);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        if (UsesSampleData && Articles.Count == 0)
        {
            if (Categories.Count == 0)
            {
                Categories.AddRange(HelpCenterSamples.Categories());
            }
            Articles.AddRange(HelpCenterSamples.Articles());
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width < NarrowWidth;
        if (narrow != _narrow)
        {
            _narrow = narrow;
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            Render(0);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TitleProperty)
        {
            _titleText.Text = Title;
        }
        else if (property == HomeLabelProperty)
        {
            Render(0);
        }
        else if (property == SearchTextProperty)
        {
            if (_search.Text != SearchText)
            {
                _search.Text = SearchText;
            }
            if (!_navigating)
            {
                // Typing leaves an open article or category for the results.
                _navigating = true;
                try
                {
                    SetCurrentValue(CurrentArticleProperty, null);
                }
                finally
                {
                    _navigating = false;
                }
                Render(string.IsNullOrWhiteSpace(SearchText) ? -1 : 0);
                var count = Results(SearchText).Count;
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    _announcer.Announce(count == 0 ? "No articles found." : SupportKit.Count(count, "article found", "articles found"));
                }
            }
        }
        else if (property == CurrentArticleProperty)
        {
            if (!_navigating)
            {
                Render(change.GetNewValue<HelpCenterArticle?>() is null ? -1 : 1);
            }
            if (change.GetNewValue<HelpCenterArticle?>() is { } article)
            {
                _announcer.Announce($"{article.Title}, {article.ReadingMinutes} minute read.");
                RaiseEvent(new HelpCenterArticleEventArgs(ArticleOpenedEvent, article));
            }
        }
        else if (property == CurrentCategoryProperty && !_navigating)
        {
            Render(1);
        }
    }

    #endregion

    #region Pages

    /// <summary>Shows the page the state asks for, sliding forward (1), back (-1) or in place (0).</summary>
    private void Render(int direction)
    {
        var view = CurrentArticle is not null ? HelpCenterView.Article
            : !string.IsNullOrWhiteSpace(SearchText) ? HelpCenterView.Search
            : CurrentCategory is not null ? HelpCenterView.Category
            : HelpCenterView.Home;
        var same = view == View && direction == 0 && _swap.Content is not null;
        View = view;
        _hero.IsVisible = view is HelpCenterView.Home or HelpCenterView.Search;
        UpdateCrumbs();
        _swap.Direction = direction < 0 ? -1 : 1;
        if (same)
        {
            _swap.Mode = ContentSwapMode.None;
        }
        _articlePage = null;
        _swap.Content = view switch
        {
            HelpCenterView.Article => _articlePage = new HelpCenterArticlePage(this, CurrentArticle!, _narrow),
            HelpCenterView.Search => SearchPage(),
            HelpCenterView.Category => CategoryPage(CurrentCategory!),
            _ => HomePage(),
        };
        _swap.Mode = ContentSwapMode.Slide;
    }

    private void UpdateCrumbs()
    {
        _crumbs.Children.Clear();
        _crumbs.IsVisible = View != HelpCenterView.Home;
        AddCrumb(HomeLabel, GoHome);
        var category = View == HelpCenterView.Article ? CategoryOf(CurrentArticle) : View == HelpCenterView.Category ? CurrentCategory : null;
        if (category is not null)
        {
            AddSeparator();
            if (View == HelpCenterView.Article)
            {
                AddCrumb(category.Title, () => OpenCategory(category));
            }
            else
            {
                _crumbs.Children.Add(SupportKit.Text(category.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS, FontWeight.Medium));
            }
        }
        if (View == HelpCenterView.Search)
        {
            AddSeparator();
            _crumbs.Children.Add(SupportKit.Text("Search", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS, FontWeight.Medium));
        }
    }

    private void AddCrumb(string text, Action go)
    {
        var button = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = text, Padding = new Thickness(6, 0) };
        ArcTokens.Bind(button, ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetName(button, text);
        button.Click += (_, _) => go();
        _crumbs.Children.Add(button);
    }

    private void AddSeparator() => _crumbs.Children.Add(SupportKit.Glyph(ArcIcons.ChevronRight, 13, ArcDesignTokenKind.TextMuted));

    private Control HomePage()
    {
        var grid = new UniformGrid { Columns = _narrow ? 1 : Math.Clamp(Categories.Count, 1, 3) };
        foreach (var category in Categories)
        {
            var count = Articles.Count(a => a.CategoryId == category.Id);
            var icon = new Border { Width = 36, Height = 36, Child = SupportKit.Glyph(category.Icon ?? ArcIcons.BookOpen, 18, ArcDesignTokenKind.AccentStrong) }
                .BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            ArcTokens.Bind(icon, Border.BackgroundProperty, ArcDesignTokenKind.AccentSubtle);
            var body = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    icon,
                    SupportKit.Text(category.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.SemiBold, wrap: true),
                    SupportKit.Text(category.Description, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, wrap: true),
                    SupportKit.Text(SupportKit.Count(count, "article", "articles"), ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS),
                },
            };
            body.Children[2].IsVisible = !string.IsNullOrEmpty(category.Description);
            var card = new SupportKitRow { Content = body, Item = category, Padding = new Thickness(16), Margin = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Top };
            ArcTokens.Bind(card, BorderBrushProperty, ArcDesignTokenKind.Border);
            ArcTokens.Bind(card, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
            AutomationProperties.SetName(card, $"{category.Title}, {SupportKit.Count(count, "article", "articles")}");
            card.Click += (_, _) => OpenCategory(category);
            grid.Children.Add(card);
        }
        var page = new StackPanel { Spacing = 22 };
        page.Children.Add(new StackPanel { Spacing = 8, Children = { Section("Browse by topic"), grid } });
        var popular = Articles.Where(a => a.IsPopular).Take(6).ToList();
        if (popular.Count > 0)
        {
            page.Children.Add(new StackPanel { Spacing = 6, Children = { Section("Popular articles"), ArticleList(popular, showCategory: true) } });
        }
        return Scroll(page);
    }

    private Control CategoryPage(HelpCenterCategory category)
    {
        var articles = Articles.Where(a => a.CategoryId == category.Id).ToList();
        var header = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                SupportKit.Heading(category.Title, ArcDesignTokenKind.TypeXL, 1),
                SupportKit.Text(category.Description, ArcDesignTokenKind.TextSecondary, wrap: true),
                SupportKit.Text(SupportKit.Count(articles.Count, "article", "articles"), ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS),
            },
        };
        header.Children[1].IsVisible = !string.IsNullOrEmpty(category.Description);
        return Scroll(new StackPanel { Spacing = 16, Children = { header, ArticleList(articles, showCategory: false) } });
    }

    private Control SearchPage()
    {
        var results = Results(SearchText);
        if (results.Count == 0)
        {
            var none = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(0, 24),
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    SupportKit.Glyph(ArcIcons.Search, 22, ArcDesignTokenKind.TextMuted),
                    SupportKit.Text($"No articles match “{SearchText!.Trim()}”.", ArcDesignTokenKind.Foreground, wrap: true),
                    SupportKit.Text("Try fewer words, or browse the topics.", ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, wrap: true),
                },
            };
            foreach (var child in none.Children.OfType<Layoutable>())
            {
                child.HorizontalAlignment = HorizontalAlignment.Center;
            }
            var browse = SupportKit.Button("Browse topics", ButtonVariant.Secondary);
            browse.HorizontalAlignment = HorizontalAlignment.Center;
            browse.Click += (_, _) => GoHome();
            none.Children.Add(browse);
            return none;
        }
        var label = Section(SupportKit.Count(results.Count, "result", "results"));
        return Scroll(new StackPanel { Spacing = 6, Children = { label, ArticleList(results, showCategory: true) } });
    }

    internal Control ArticleList(IReadOnlyList<HelpCenterArticle> articles, bool showCategory)
    {
        var list = new StackPanel { Spacing = 2 };
        foreach (var article in articles)
        {
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(SupportKit.Text(article.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium, wrap: true));
            if (!string.IsNullOrEmpty(article.Summary))
            {
                var summary = SupportKit.Text(article.Summary, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, wrap: true);
                summary.MaxLines = 2;
                summary.TextTrimming = TextTrimming.CharacterEllipsis;
                text.Children.Add(summary);
            }
            var meta = showCategory && CategoryOf(article) is { } category
                ? $"{category.Title} · {article.ReadingMinutes} min read"
                : $"{article.ReadingMinutes} min read";
            text.Children.Add(SupportKit.Text(meta, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS));
            var grid = SupportKit.Grid("Auto,*,Auto", columnSpacing: 12);
            grid.Children.Add(SupportKit.Glyph(ArcIcons.FileText, 16, ArcDesignTokenKind.TextMuted).At(0));
            grid.Children.Add(text.At(1));
            grid.Children.Add(SupportKit.Glyph(ArcIcons.ChevronRight, 16, ArcDesignTokenKind.TextMuted).At(2));
            var row = new SupportKitRow { Content = grid, Item = article };
            AutomationProperties.SetName(row, $"{article.Title}, {meta}");
            row.Click += (_, _) => OpenArticle(article);
            list.Children.Add(row);
        }
        AutomationProperties.SetName(list, "Articles");
        return list;
    }

    private static TextBlock Section(string text)
    {
        var block = SupportKit.Text(text, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.SemiBold);
        AutomationProperties.SetHeadingLevel(block, 2);
        block.Margin = new Thickness(4, 0);
        return block;
    }

    private static ScrollViewer Scroll(Control content) =>
        new() { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

    internal void RaiseFeedback(HelpCenterArticle article, bool helpful, string? comment)
    {
        _announcer.Announce("Thanks for the feedback.");
        RaiseEvent(new HelpCenterFeedbackEventArgs(FeedbackGivenEvent, article, helpful, comment));
    }

    #endregion

    private void OnBlockKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.Escape && View is HelpCenterView.Article or HelpCenterView.Category && e.Source is not TextBox)
        {
            GoBack();
            e.Handled = true;
        }
        else if (e.Key == Key.Left && (e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            GoBack();
            e.Handled = true;
        }
        else if (e.Key == Key.OemQuestion && e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && _hero.IsVisible)
        {
            _search.Focus(NavigationMethod.Tab);
            e.Handled = true;
        }
    }

    /// <summary>The anchor of a heading as Markdown links name it: lower case, letters and digits, spaces as hyphens.</summary>
    internal static string Slug(string heading)
    {
        var builder = new StringBuilder(heading.Length);
        foreach (var c in heading.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (c is ' ' or '-')
            {
                builder.Append('-');
            }
        }
        return builder.ToString();
    }
}
