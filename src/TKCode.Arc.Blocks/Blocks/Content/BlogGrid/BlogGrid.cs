// Ported from Arc's free blog-grid block (MIT, Copyright (c) 2026 Elia Kuratli): blog-grid.tsx.
using System.Globalization;
using System.Runtime.CompilerServices;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A blog index: a title, a category filter with a gliding highlight, a large featured post, a responsive grid of cards and
/// pagination. Without <see cref="HrefSelector"/> a card opens an in-place reader whose cover grows out of the card and returns
/// to it. Zero configuration shows original sample posts with drawn covers.
/// </summary>
/// <remarks>Ported from Arc's free blog-grid block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(StagePart, typeof(BlogStage))]
[TemplatePart(FiltersPart, typeof(GlidingPillBar))]
[TemplatePart(PagePart, typeof(BlockSwap))]
[TemplatePart(PaginationPart, typeof(BlockSwap))]
[TemplatePart(ReaderHostPart, typeof(Panel))]
[TemplatePart(AnnouncerPart, typeof(SiteAnnouncer))]
[PseudoClasses(MediumPseudoClass, SmallPseudoClass, TinyPseudoClass, ReadingPseudoClass)]
public class BlogGrid : TemplatedControl
{
    public const string StagePart = "PART_Stage";
    public const string FiltersPart = "PART_Filters";
    public const string PagePart = "PART_Page";
    public const string PaginationPart = "PART_Pagination";
    public const string ReaderHostPart = "PART_ReaderHost";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The value of <see cref="Category"/> that shows every post.</summary>
    public const string AllCategory = "All";

    /// <summary>Set at 900 wide or less: two columns.</summary>
    public const string MediumPseudoClass = ":medium";

    /// <summary>Set at 680 wide or less: tighter spacing and a stacked featured post.</summary>
    public const string SmallPseudoClass = ":small";

    /// <summary>Set at 540 wide or less: one column; Previous and Next collapse to arrows.</summary>
    public const string TinyPseudoClass = ":tiny";

    /// <summary>Set while the in-place reader shows.</summary>
    public const string ReadingPseudoClass = ":reading";

    /// <summary>The heading ("Journal").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<BlogGrid, string?>(nameof(Title), "Journal");

    /// <summary>The line under the heading. Null or empty hides it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<BlogGrid, string?>(nameof(Description), "Product news, design notes and engineering deep dives from the team.");

    /// <summary>The posts, in any order; they are listed newest first. Null shows the samples.</summary>
    public static readonly StyledProperty<IReadOnlyList<BlogPost>?> PostsProperty =
        AvaloniaProperty.Register<BlogGrid, IReadOnlyList<BlogPost>?>(nameof(Posts));

    /// <summary>The filter labels, in order; "All" goes in front. Null uses the samples' categories.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> CategoriesProperty =
        AvaloniaProperty.Register<BlogGrid, IReadOnlyList<string>?>(nameof(Categories));

    /// <summary>The active category, or "All" (two way). Picking a filter resets the page to 1.</summary>
    public static readonly StyledProperty<string> CategoryProperty =
        AvaloniaProperty.Register<BlogGrid, string>(nameof(Category), AllCategory, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The one based page (two way).</summary>
    public static readonly StyledProperty<int> PageProperty =
        AvaloniaProperty.Register<BlogGrid, int>(nameof(Page), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cards per page below the featured post (6).</summary>
    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<BlogGrid, int>(nameof(PageSize), 6);

    /// <summary>Shows the post marked featured, or the newest of the current filter, as a large card on page one (true).</summary>
    public static readonly StyledProperty<bool> ShowFeaturedProperty =
        AvaloniaProperty.Register<BlogGrid, bool>(nameof(ShowFeatured), true);

    /// <summary>The label of the filter that shows every post ("All").</summary>
    public static readonly StyledProperty<string> AllLabelProperty =
        AvaloniaProperty.Register<BlogGrid, string>(nameof(AllLabel), AllCategory);

    /// <summary>The reader's way back ("All posts").</summary>
    public static readonly StyledProperty<string> BackLabelProperty =
        AvaloniaProperty.Register<BlogGrid, string>(nameof(BackLabel), "All posts");

    /// <summary>The Previous button's text ("Previous").</summary>
    public static readonly StyledProperty<string> PreviousLabelProperty =
        AvaloniaProperty.Register<BlogGrid, string>(nameof(PreviousLabel), "Previous");

    /// <summary>The Next button's text ("Next").</summary>
    public static readonly StyledProperty<string> NextLabelProperty =
        AvaloniaProperty.Register<BlogGrid, string>(nameof(NextLabel), "Next");

    /// <summary>A page button's screen reader name; {0} is the number ("Page {0}").</summary>
    public static readonly StyledProperty<string> PageLabelFormatProperty =
        AvaloniaProperty.Register<BlogGrid, string>(nameof(PageLabelFormat), "Page {0}");

    /// <summary>The reading time; {0} is the minutes ("{0} min read").</summary>
    public static readonly StyledProperty<string> ReadTimeFormatProperty =
        AvaloniaProperty.Register<BlogGrid, string>(nameof(ReadTimeFormat), "{0} min read");

    /// <summary>The message for a filter with no posts; {0} is the category ("No posts in {0} yet.").</summary>
    public static readonly StyledProperty<string> EmptyFormatProperty =
        AvaloniaProperty.Register<BlogGrid, string>(nameof(EmptyFormat), "No posts in {0} yet.");

    /// <summary>The post open in the reader, or null (read only).</summary>
    public static readonly DirectProperty<BlogGrid, BlogPost?> ReadingPostProperty =
        AvaloniaProperty.RegisterDirect<BlogGrid, BlogPost?>(nameof(ReadingPost), o => o.ReadingPost);

    /// <summary>A post opened, from a link or the in-place reader. Mark the event handled to stop a link from opening.</summary>
    public static readonly RoutedEvent<BlogPostEventArgs> PostOpenedEvent =
        RoutedEvent.Register<BlogGrid, BlogPostEventArgs>(nameof(PostOpened), RoutingStrategies.Bubble);

    internal static readonly DirectProperty<BlogGrid, double> TitleFontSizeProperty =
        AvaloniaProperty.RegisterDirect<BlogGrid, double>(nameof(TitleFontSize), o => o.TitleFontSize);

    internal static readonly DirectProperty<BlogGrid, bool> HasDescriptionProperty =
        AvaloniaProperty.RegisterDirect<BlogGrid, bool>(nameof(HasDescription), o => o.HasDescription);

    private BlogPost? _readingPost;
    private double _titleFontSize = 36;
    private bool _hasDescription = true;

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="PostsProperty"/>
    public IReadOnlyList<BlogPost>? Posts { get => GetValue(PostsProperty); set => SetValue(PostsProperty, value); }

    /// <inheritdoc cref="CategoriesProperty"/>
    public IReadOnlyList<string>? Categories { get => GetValue(CategoriesProperty); set => SetValue(CategoriesProperty, value); }

    /// <inheritdoc cref="CategoryProperty"/>
    public string Category { get => GetValue(CategoryProperty); set => SetValue(CategoryProperty, value); }

    /// <inheritdoc cref="PageProperty"/>
    public int Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <inheritdoc cref="PageSizeProperty"/>
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <inheritdoc cref="ShowFeaturedProperty"/>
    public bool ShowFeatured { get => GetValue(ShowFeaturedProperty); set => SetValue(ShowFeaturedProperty, value); }

    /// <inheritdoc cref="AllLabelProperty"/>
    public string AllLabel { get => GetValue(AllLabelProperty); set => SetValue(AllLabelProperty, value); }

    /// <inheritdoc cref="BackLabelProperty"/>
    public string BackLabel { get => GetValue(BackLabelProperty); set => SetValue(BackLabelProperty, value); }

    /// <inheritdoc cref="PreviousLabelProperty"/>
    public string PreviousLabel { get => GetValue(PreviousLabelProperty); set => SetValue(PreviousLabelProperty, value); }

    /// <inheritdoc cref="NextLabelProperty"/>
    public string NextLabel { get => GetValue(NextLabelProperty); set => SetValue(NextLabelProperty, value); }

    /// <inheritdoc cref="PageLabelFormatProperty"/>
    public string PageLabelFormat { get => GetValue(PageLabelFormatProperty); set => SetValue(PageLabelFormatProperty, value); }

    /// <inheritdoc cref="ReadTimeFormatProperty"/>
    public string ReadTimeFormat { get => GetValue(ReadTimeFormatProperty); set => SetValue(ReadTimeFormatProperty, value); }

    /// <inheritdoc cref="EmptyFormatProperty"/>
    public string EmptyFormat { get => GetValue(EmptyFormatProperty); set => SetValue(EmptyFormatProperty, value); }

    /// <inheritdoc cref="ReadingPostProperty"/>
    public BlogPost? ReadingPost { get => _readingPost; private set => SetAndRaise(ReadingPostProperty, ref _readingPost, value); }

    /// <summary>The link of a post. When set, cards open links and the in-place reader is off.</summary>
    public Func<BlogPost, string?>? HrefSelector { get; set; }

    /// <inheritdoc cref="PostOpenedEvent"/>
    public event EventHandler<BlogPostEventArgs>? PostOpened
    {
        add => AddHandler(PostOpenedEvent, value);
        remove => RemoveHandler(PostOpenedEvent, value);
    }

    /// <summary>clamp(28, 16 + 3cqi, 52).</summary>
    internal double TitleFontSize { get => _titleFontSize; private set => SetAndRaise(TitleFontSizeProperty, ref _titleFontSize, value); }

    internal bool HasDescription { get => _hasDescription; private set => SetAndRaise(HasDescriptionProperty, ref _hasDescription, value); }

    private static readonly MotionSpec s_exit = MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard);
    private static readonly MotionSpec s_fade = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard);

    private static readonly BlockSwapMotion s_paginationMotion = new()
    {
        EnterFade = s_fade,
        EnterMove = MotionSpec.Instant,
        Exit = s_exit,
        ReducedEnter = ArcMotionPresets.Standard,
        ReducedExit = ArcMotionPresets.Exit,
    };

    private readonly List<BlogCard> _cards = new();
    private readonly BlockScroller _scroller;
    private BlogStage? _stage;
    private Control? _index;
    private Control? _header;
    private Control? _filtersHost;
    private GlidingPillBar? _filters;
    private BlockSwap? _page;
    private BlockSwap? _pagination;
    private Panel? _readerHost;
    private SiteAnnouncer? _announcer;
    private Control? _paginationView;
    private GlidingPillBar? _pageNumbers;
    private BlogPageStepButton? _previous;
    private BlogPageStepButton? _next;
    private BlogCardGrid? _grid;
    private SharedElementScope? _flight;
    private string? _lastOpenedId;
    private string? _pageKey;
    private IReadOnlyList<BlogPost> _allPosts = [];
    private IReadOnlyList<string> _tabs = [];
    private int _pageCount = 1;
    private int _safePage = 1;
    private int _direction;
    private double _width;
    private int _readerGeneration;
    private bool _syncing;

    static BlogGrid()
    {
        FocusableProperty.OverrideDefaultValue<BlogGrid>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<BlogGrid>(AutomationLandmarkType.Region);
    }

    /// <summary>Creates the block.</summary>
    public BlogGrid()
    {
        _scroller = new BlockScroller(this);
        BlockFocusModality.Watch();
    }

    internal static string CoverKey(BlogPost post) => "blog-cover-" + post.Id;

    internal static string FormatDate(DateOnly date) => date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>A cover filling its frame; the hover zoom moves the frame's child.</summary>
    internal static Control CreateCover(BlogPost post)
    {
        Control cover = post.Image is { } image
            ? new Image { Source = image, Stretch = Stretch.UniformToFill }
            : new ArcImage { Source = post.ImageSource, Stretch = Stretch.UniformToFill };
        AutomationProperties.SetName(cover, post.ImageAlt);
        return cover;
    }

    #region Test hooks

    internal IReadOnlyList<BlogCard> Cards => _cards;

    internal int PageCount => _pageCount;

    internal GlidingPillBar? Filters => _filters;

    internal GlidingPillBar? PageNumbers => _pageNumbers;

    internal BlogPageStepButton? PreviousButton => _previous;

    internal BlogPageStepButton? NextButton => _next;

    internal BlockSwap? PageHost => _page;

    internal BlogCardGrid? CardGrid => _grid;

    internal bool HasPagination => _pagination?.Current is not null;

    internal BlogBackButton? BackButton { get; private set; }

    internal BlogStage? Stage => _stage;

    internal Control? Index => _index;

    internal SiteAnnouncer? Announcer => _announcer;

    internal Control? EmptyMessage { get; private set; }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_filters is not null)
        {
            _filters.ItemPressed -= OnFilterPressed;
        }
        _stage = e.NameScope.Find<BlogStage>(StagePart);
        _index = e.NameScope.Find<Control>("PART_Index");
        _header = e.NameScope.Find<Control>("PART_Header");
        _filtersHost = e.NameScope.Find<Control>("PART_FiltersHost");
        _filters = e.NameScope.Find<GlidingPillBar>(FiltersPart);
        _page = e.NameScope.Find<BlockSwap>(PagePart);
        _pagination = e.NameScope.Find<BlockSwap>(PaginationPart);
        _readerHost = e.NameScope.Find<Panel>(ReaderHostPart);
        _announcer = e.NameScope.Find<SiteAnnouncer>(AnnouncerPart);
        if (_filters is not null)
        {
            _filters.ItemPressed += OnFilterPressed;
        }
        BuildPagination();
        RefreshData();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == PostsProperty || property == CategoriesProperty || property == AllLabelProperty)
        {
            RefreshData();
        }
        else if (property == PageSizeProperty || property == ShowFeaturedProperty || property == ReadTimeFormatProperty || property == EmptyFormatProperty)
        {
            RefreshPage(animate: false);
        }
        else if (property == CategoryProperty)
        {
            ApplyFilterSelection();
            if (!_syncing)
            {
                _direction = 0;
                RefreshPage(animate: true);
            }
        }
        else if (property == PageProperty && !_syncing)
        {
            _direction = change.GetNewValue<int>() > _safePage ? 1 : -1;
            RefreshPage(animate: true);
        }
        else if (property == DescriptionProperty)
        {
            HasDescription = !string.IsNullOrEmpty(Description);
        }
        else if (property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
        else if (property == PreviousLabelProperty && _previous is not null)
        {
            _previous.Label.Text = PreviousLabel;
        }
        else if (property == NextLabelProperty && _next is not null)
        {
            _next.Label.Text = NextLabel;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AutomationProperties.SetName(this, Title);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _flight?.Dispose();
        _flight = null;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _width = e.NewSize.Width;
        TitleFontSize = Math.Clamp(16 + 0.03 * _width, 28, 52);
        PseudoClasses.Set(MediumPseudoClass, _width <= 900);
        PseudoClasses.Set(SmallPseudoClass, _width <= 680);
        PseudoClasses.Set(TinyPseudoClass, _width <= 540);
        ApplyBreakpoints();
    }

    private double FeaturedTitleSize => Math.Clamp(16 + 0.022 * (_width > 0 ? _width : 1180), 28, 36);

    private void ApplyBreakpoints()
    {
        var medium = _width > 0 && _width <= 900;
        var small = _width > 0 && _width <= 680;
        var tiny = _width > 0 && _width <= 540;
        _grid?.SetLayout(tiny ? 1 : medium ? 2 : 3, small ? 16 : 24, small ? 40 : 48);
        foreach (var card in _cards)
        {
            card.SetBreakpoints(medium, small, FeaturedTitleSize);
        }
        if (_previous is not null)
        {
            _previous.Label.IsVisible = !tiny;
        }
        if (_next is not null)
        {
            _next.Label.IsVisible = !tiny;
        }
    }

    #region Data

    private void RefreshData()
    {
        _allPosts = (Posts ?? BlogGridSamples.Create()).OrderByDescending(p => p.Date).ToArray();
        var categories = Categories ?? (Posts is null ? BlogGridSamples.Categories : []);
        _tabs = new[] { AllCategory }.Concat(categories).ToArray();
        _filters?.SetItems(_tabs.Select(t => t == AllCategory ? AllLabel : t).ToArray());
        ApplyFilterSelection();
        _pageKey = null;
        RefreshPage(animate: false);
    }

    private void ApplyFilterSelection()
    {
        if (_filters is not null)
        {
            _filters.SelectedIndex = Array.IndexOf(_tabs as string[] ?? _tabs.ToArray(), Category);
        }
    }

    private List<BlogPost> FilteredPosts() =>
        Category == AllCategory ? _allPosts.ToList() : _allPosts.Where(p => p.Category == Category).ToList();

    private void RefreshPage(bool animate)
    {
        if (_page is null)
        {
            return;
        }
        var filtered = FilteredPosts();
        var featured = ShowFeatured && filtered.Count > 1 ? filtered.FirstOrDefault(p => p.IsFeatured) ?? filtered[0] : null;
        var rest = featured is null ? filtered : filtered.Where(p => !ReferenceEquals(p, featured)).ToList();
        var size = Math.Max(1, PageSize);
        _pageCount = Math.Max(1, (int)Math.Ceiling(rest.Count / (double)size));
        _safePage = Math.Clamp(Page, 1, _pageCount);
        var key = $"{Category}-{_safePage}";
        if (key != _pageKey || !animate)
        {
            _pageKey = key;
            var view = BuildPage(_safePage == 1 ? featured : null, rest.Skip((_safePage - 1) * size).Take(size).ToArray(), filtered.Count == 0);
            if (animate)
            {
                _page.Swap(view, PageMotion(_direction));
            }
            else
            {
                _page.Set(view);
            }
        }
        ApplyPagination(animate);
        // A reader open over a changed list has nothing to fly back to.
        if (_flight is not null)
        {
            _flight.Dispose();
            _flight = null;
        }
    }

    private static BlockSwapMotion PageMotion(int direction) => new()
    {
        Wait = true,
        EnterX = direction * 24,
        EnterY = direction == 0 ? 10 : 0,
        ExitX = direction * -24,
        EnterFade = s_fade,
        EnterMove = MotionSpec.Spring(ArcSpring.Smooth),
        Exit = s_exit,
        ReducedEnter = ArcMotionPresets.Standard,
        ReducedExit = ArcMotionPresets.Standard,
    };

    private Control BuildPage(BlogPost? featured, IReadOnlyList<BlogPost> posts, bool empty)
    {
        _cards.Clear();
        _grid = null;
        EmptyMessage = null;
        var stack = new StackPanel { Spacing = _width > 0 && _width <= 680 ? 40 : 48 };
        if (featured is not null)
        {
            stack.Children.Add(CreateCard(featured, true));
        }
        if (posts.Count > 0)
        {
            _grid = new BlogCardGrid();
            foreach (var post in posts)
            {
                _grid.Children.Add(CreateCard(post, false));
            }
            stack.Children.Add(_grid);
        }
        if (empty)
        {
            var message = BlockText.Create(string.Format(EmptyFormat, Category), ArcDesignTokenKind.TypeBase, 16, ArcDesignTokenKind.TextSecondary);
            message.TextAlignment = TextAlignment.Center;
            message.Margin = new Thickness(0, 64);
            EmptyMessage = message;
            stack.Children.Add(message);
        }
        ApplyBreakpoints();
        return stack;
    }

    private BlogCard CreateCard(BlogPost post, bool featured)
    {
        var card = new BlogCard(post, featured, m => string.Format(ReadTimeFormat, m), FeaturedTitleSize);
        card.Click += OnCardClick;
        _cards.Add(card);
        return card;
    }

    #endregion

    #region Filter and pagination

    private void OnFilterPressed(int index)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            SelectCategory(_tabs[index]);
        }
    }

    /// <summary>Picks a filter and goes back to page 1.</summary>
    private void SelectCategory(string category)
    {
        if (category == Category)
        {
            return;
        }
        _direction = 0;
        _syncing = true;
        try
        {
            SetCurrentValue(CategoryProperty, category);
            SetCurrentValue(PageProperty, 1);
        }
        finally
        {
            _syncing = false;
        }
        RefreshPage(animate: true);
    }

    /// <summary>Goes to a one based page; a page out of range or the current one does nothing.</summary>
    public void GoToPage(int page)
    {
        if (page == _safePage || page < 1 || page > _pageCount)
        {
            return;
        }
        _direction = page > _safePage ? 1 : -1;
        _syncing = true;
        try
        {
            SetCurrentValue(PageProperty, page);
        }
        finally
        {
            _syncing = false;
        }
        RefreshPage(animate: true);
        ScrollToTop();
    }

    private void BuildPagination()
    {
        _previous = new BlogPageStepButton(PreviousLabel, ArcIcons.ChevronLeft, iconFirst: true);
        _next = new BlogPageStepButton(NextLabel, ArcIcons.ChevronRight, iconFirst: false);
        AutomationProperties.SetName(_previous, PreviousLabel);
        AutomationProperties.SetName(_next, NextLabel);
        _previous.Click += (_, _) => GoToPage(_safePage - 1);
        _next.Click += (_, _) => GoToPage(_safePage + 1);
        _pageNumbers = new GlidingPillBar { Look = GlidingPillBarLook.Quiet, HorizontalAlignment = HorizontalAlignment.Center };
        _pageNumbers.ItemPressed += index => GoToPage(index + 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 24, 0, 0), ColumnSpacing = 12 };
        Grid.SetColumn(_previous, 0);
        Grid.SetColumn(_pageNumbers, 1);
        Grid.SetColumn(_next, 2);
        grid.Children.Add(_previous);
        grid.Children.Add(_pageNumbers);
        grid.Children.Add(_next);
        var rule = new Border { Child = grid };
        ArcTokens.Bind(rule, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        ArcTokens.Bind(rule, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        _paginationView = rule;
        _pageCountBuilt = 0;
    }

    private int _pageCountBuilt;

    private void ApplyPagination(bool animate)
    {
        if (_pagination is null || _pageNumbers is null || _previous is null || _next is null || _paginationView is null)
        {
            return;
        }
        if (_pageCountBuilt != _pageCount)
        {
            _pageCountBuilt = _pageCount;
            var labels = Enumerable.Range(1, _pageCount).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray();
            _pageNumbers.SetItems(labels, i => string.Format(PageLabelFormat, i + 1));
            foreach (var button in _pageNumbers.Buttons)
            {
                button.Padding = default;
            }
        }
        _pageNumbers.SelectedIndex = _safePage - 1;
        _previous.IsEnabled = _safePage > 1;
        _next.IsEnabled = _safePage < _pageCount;
        var next = _pageCount > 1 ? _paginationView : null;
        if (animate)
        {
            _pagination.Swap(next, s_paginationMotion);
        }
        else
        {
            _pagination.Set(next);
        }
    }

    private void ScrollToTop()
    {
        if (this.FindAncestorOfType<ScrollViewer>() is not { } scroller || this.TranslatePoint(default, scroller) is not { } point)
        {
            return;
        }
        if (point.Y < 0)
        {
            _scroller.To(scroller, scroller.Offset.Y + point.Y);
        }
    }

    #endregion

    #region Reader

    private void OnCardClick(object? sender, RoutedEventArgs e)
    {
        if (sender is BlogCard card)
        {
            Open(card);
        }
    }

    /// <summary>Opens <paramref name="post"/>: its link when <see cref="HrefSelector"/> gives one, else the in-place reader.</summary>
    public void OpenPost(BlogPost post)
    {
        var card = _cards.FirstOrDefault(c => ReferenceEquals(c.Post, post));
        if (card is not null)
        {
            Open(card);
        }
        else
        {
            OpenCore(post, null);
        }
    }

    private void Open(BlogCard card) => OpenCore(card.Post, card);

    private void OpenCore(BlogPost post, BlogCard? card)
    {
        var href = HrefSelector?.Invoke(post);
        var args = new BlogPostEventArgs(PostOpenedEvent, this, post, href);
        RaiseEvent(args);
        if (HrefSelector is not null)
        {
            if (!args.Handled)
            {
                SiteLinks.TryLaunch(this, href);
            }
            return;
        }
        ShowReader(post, card);
        ScrollToTop();
    }

    private readonly List<Control> _readerPieces = new();

    private void ShowReader(BlogPost post, BlogCard? card)
    {
        if (_stage is null || _readerHost is null || _index is null)
        {
            return;
        }
        _readerGeneration++;
        _flight?.Dispose();
        _flight = null;
        _lastOpenedId = post.Id;
        ReadingPost = post;
        PseudoClasses.Set(ReadingPseudoClass, true);
        var full = ArcMotion.IsFull(this);
        var (reader, body, frame) = BuildReader(post);
        _readerHost.Children.Clear();
        _readerHost.Children.Add(reader);
        _readerHost.IsVisible = true;
        _stage.ShowsSecond = true;
        LeaveIndex();
        UpdateLayout();
        var hasFlight = full && frame is not null && card?.Frame is not null;
        foreach (var piece in _readerPieces)
        {
            if (!ReferenceEquals(piece, frame) || !hasFlight)
            {
                Fade(piece, 0, 1, s_fade);
            }
        }
        RiseBody(body, full);
        if (hasFlight)
        {
            _flight = new SharedElementScope(_index, _readerHost);
            _ = _flight.FlyAsync(1, MotionSpec.Spring(ArcSpring.Morph));
        }
        BackButton?.Focus(BlockFocusModality.For(this, NavigationMethod.Tab));
    }

    private void LeaveIndex()
    {
        if (_index is null)
        {
            return;
        }
        _index.IsHitTestVisible = false;
        KeyboardNavigation.SetTabNavigation(_index, KeyboardNavigationMode.None);
        AutomationProperties.SetAccessibilityView(_index, AccessibilityView.Raw);
        Fade(_index, _index.Opacity, 0, s_exit);
    }

    private static void RiseBody(Control body, bool full)
    {
        var offset = new TranslateTransform(0, full ? 12 : 0);
        body.RenderTransform = offset;
        var fade = new MotionValue<double>(body, 0, MotionVectors.Double);
        body.Opacity = 0;
        fade.Changed += v => body.Opacity = Math.Clamp(v, 0, 1);
        var move = new MotionValue<double>(body, offset.Y, MotionVectors.Double);
        move.Changed += v => offset.Y = v;
        if (full)
        {
            var spec = MotionSpec.Spring(ArcSpring.Smooth, TimeSpan.FromSeconds(0.12));
            _ = fade.AnimateTo(1, spec);
            _ = move.AnimateTo(0, spec);
        }
        else
        {
            _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
    }

    private static readonly ConditionalWeakTable<Control, MotionValue<double>> s_fades = new();

    /// <summary>One fade per control: a new one takes over from a running one, so an open and a close cannot fight over its opacity.</summary>
    private static void Fade(Control control, double from, double to, MotionSpec spec)
    {
        var value = s_fades.GetValue(control, static c =>
        {
            var motion = new MotionValue<double>(c, c.Opacity, MotionVectors.Double);
            motion.Changed += v => c.Opacity = Math.Clamp(v, 0, 1);
            return motion;
        });
        value.Jump(from);
        control.Opacity = from;
        _ = value.AnimateTo(to, spec);
    }

    private (Control Reader, Control Body, Control? Frame) BuildReader(BlogPost post)
    {
        _readerPieces.Clear();
        var column = new StackPanel { Spacing = 32 };
        column.MaxWidth = 760;
        column.HorizontalAlignment = HorizontalAlignment.Center;

        var arrow = new ArcGlyph { Data = ArcIcons.ArrowLeft, Size = 16, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = BackLabel, VerticalAlignment = VerticalAlignment.Center };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(arrow);
        content.Children.Add(label);
        var back = new BlogBackButton { Content = content, Margin = new Thickness(-8, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(back, BackLabel);
        back.Click += (_, _) => CloseReader();
        BackButton = back;
        column.Children.Add(back);
        _readerPieces.Add(back);

        var head = new StackPanel { Spacing = 12 };
        head.Children.Add(BuildMeta(post));
        var size = Math.Clamp(16 + 0.034 * (_width > 0 ? _width : 1180), 28, 52);
        var title = new TextBlock { Text = post.Title, FontSize = size, LineHeight = size * 1.1, LetterSpacing = size * -0.03, FontWeight = FontWeight.Medium, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(title, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        AutomationProperties.SetHeadingLevel(title, 2);
        head.Children.Add(title);
        head.Children.Add(BuildByline(post));
        column.Children.Add(head);
        _readerPieces.Add(head);

        Control? frame = null;
        if (post.HasCover)
        {
            var cover = new BlogAspectFrame { Ratio = 16.0 / 9, ClipToBounds = true, Child = CreateCover(post) }
                .BindCornerRadius(ArcDesignTokenKind.RadiusPanel);
            ArcTokens.Bind(cover, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            SharedElement.SetKey(cover, CoverKey(post));
            column.Children.Add(cover);
            _readerPieces.Add(cover);
            frame = cover;
        }

        var small = _width > 0 && _width <= 680;
        var body = new StackPanel { Spacing = 20 };
        var leadSize = small ? 18 : 22;
        body.Children.Add(BlockText.Create(post.Excerpt, small ? ArcDesignTokenKind.TypeLG : ArcDesignTokenKind.TypeXL, leadSize, ArcDesignTokenKind.Foreground, lineHeight: leadSize * 1.5));
        var textSize = small ? 16 : 18;
        foreach (var paragraph in post.Body ?? [])
        {
            body.Children.Add(BlockText.Create(paragraph, small ? ArcDesignTokenKind.TypeBase : ArcDesignTokenKind.TypeLG, textSize, ArcDesignTokenKind.TextSecondary, lineHeight: textSize * 1.65));
        }
        column.Children.Add(body);
        return (column, body, frame);
    }

    private static Control BuildMeta(BlogPost post)
    {
        var meta = new WrapPanel { ItemSpacing = 6 };
        meta.Children.Add(BlockText.Create(post.Category, ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.Foreground, medium: true, wrapping: TextWrapping.NoWrap));
        meta.Children.Add(BlockText.Create("·", ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.TextMuted, wrapping: TextWrapping.NoWrap));
        meta.Children.Add(BlockText.Create(FormatDate(post.Date), ArcDesignTokenKind.TypeXS, 12, ArcDesignTokenKind.TextMuted, wrapping: TextWrapping.NoWrap));
        return meta;
    }

    private Control BuildByline(BlogPost post)
    {
        var row = new ArcShrinkRow { Spacing = 8 };
        var avatar = new Border { Width = 24, Height = 24, ClipToBounds = true }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(avatar, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        if (post.Author.Avatar is { } face)
        {
            avatar.Child = new Image { Source = face, Stretch = Stretch.UniformToFill };
        }
        row.Children.Add(avatar);
        var name = BlockText.Create(post.Author.Name, ArcDesignTokenKind.TypeSM, 14, wrapping: TextWrapping.NoWrap);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.VerticalAlignment = VerticalAlignment.Center;
        ArcShrinkRow.SetIsShrinkable(name, true);
        row.Children.Add(name);
        var time = BlockText.Create("· " + string.Format(ReadTimeFormat, post.ReadMinutes), ArcDesignTokenKind.TypeSM, 14, ArcDesignTokenKind.TextMuted, wrapping: TextWrapping.NoWrap);
        time.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(time);
        return row;
    }

    /// <summary>Goes back to the index: the cover returns to its card while the rest of the index fades in around it, and focus returns to the card.</summary>
    public async void CloseReader()
    {
        if (ReadingPost is null || _stage is null || _readerHost is null || _index is null)
        {
            return;
        }
        var generation = ++_readerGeneration;
        var full = ArcMotion.IsFull(this);
        var flight = _flight;
        _flight = null;
        ReadingPost = null;
        PseudoClasses.Set(ReadingPseudoClass, false);
        _stage.ShowsSecond = false;
        ReturnToIndex(full ? _lastOpenedId : null, hasFlight: flight is not null);
        foreach (var piece in _readerPieces)
        {
            if (flight is null || piece is not BlogAspectFrame)
            {
                Fade(piece, piece.Opacity, 0, s_exit);
            }
        }
        var card = _cards.FirstOrDefault(c => c.Post.Id == _lastOpenedId);
        card?.Focus(BlockFocusModality.For(this, NavigationMethod.Tab));
        if (flight is not null)
        {
            await flight.FlyAsync(0, MotionSpec.Spring(ArcSpring.Morph));
            flight.Dispose();
        }
        else if (full)
        {
            await Task.Delay(ArcMotionPresets.Exit);
        }
        if (generation == _readerGeneration && ReadingPost is null)
        {
            _readerHost.Children.Clear();
            _readerHost.IsVisible = false;
            _readerPieces.Clear();
            BackButton = null;
        }
    }

    private void ReturnToIndex(string? returningId, bool hasFlight)
    {
        var index = _index!;
        index.IsHitTestVisible = true;
        index.ClearValue(KeyboardNavigation.TabNavigationProperty);
        index.ClearValue(AutomationProperties.AccessibilityViewProperty);
        Fade(index, 1, 1, MotionSpec.Instant);
        var pieces = new List<Control?> { _header, _filtersHost, _pagination };
        foreach (var piece in pieces)
        {
            if (piece is not null)
            {
                Fade(piece, 0, 1, s_fade);
            }
        }
        foreach (var card in _cards)
        {
            if (!hasFlight || card.Post.Id != returningId)
            {
                Fade(card, 0, 1, s_fade);
            }
        }
    }

    #endregion
}
