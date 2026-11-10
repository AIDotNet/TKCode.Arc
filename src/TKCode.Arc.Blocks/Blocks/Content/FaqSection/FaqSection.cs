// Ported from Arc's free faq-section block (MIT, Copyright (c) 2026 Elia Kuratli): faq-section.tsx and
// faq-section.module.css. The sample questions are this library's own.

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
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Frequently asked questions in three layouts: one centered accordion, a topic rail beside the questions of the chosen
/// topic, and a list that filters and highlights as you type. Answers open on a height spring while the plus turns into a
/// close mark, and the arrow keys move between questions. With no <see cref="Items"/> it shows sample questions.
/// </summary>
/// <remarks>Ported from Arc's free faq-section block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(HostPart, typeof(Panel))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, ColumnsPseudoClass)]
public class FaqSection : TemplatedControl
{
    public const string HostPart = "PART_Host";

    /// <summary>Set while the control is at most 760 wide: the rail sits above the questions and scrolls sideways.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the control is at most 480 wide: tighter padding.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set for <see cref="FaqSectionVariant.Columns"/>.</summary>
    public const string ColumnsPseudoClass = ":columns";

    #region Public properties

    /// <summary>The layout: Accordion (default), Columns or Search.</summary>
    public static readonly StyledProperty<FaqSectionVariant> VariantProperty =
        AvaloniaProperty.Register<FaqSection, FaqSectionVariant>(nameof(Variant));

    /// <summary>The section heading.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FaqSection, string?>(nameof(Title), "Frequently asked questions");

    /// <summary>One line under the heading; null or empty hides it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FaqSection, string?>(nameof(Description), "The short answers, before you need to ask.");

    /// <summary>The questions; while the list is empty the section shows sample questions.</summary>
    public static readonly DirectProperty<FaqSection, AvaloniaList<FaqItem>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<FaqSection, AvaloniaList<FaqItem>>(nameof(Items), o => o.Items);

    /// <summary>Allows several answers to be open at once.</summary>
    public static readonly StyledProperty<bool> IsMultipleProperty =
        AvaloniaProperty.Register<FaqSection, bool>(nameof(IsMultiple));

    /// <summary>The ids of the open questions (two way); null opens the first question. An id defaults to the question's text.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> OpenItemsProperty =
        AvaloniaProperty.Register<FaqSection, IReadOnlyList<string>?>(nameof(OpenItems), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The chosen topic of the columns layout (two way); null chooses the first topic.</summary>
    public static readonly StyledProperty<string?> SelectedCategoryProperty =
        AvaloniaProperty.Register<FaqSection, string?>(nameof(SelectedCategory), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The search text of the search layout (two way).</summary>
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<FaqSection, string>(nameof(Query), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The way to reach a person; null shows a default row, which only raises <see cref="ContactInvoked"/>.</summary>
    public static readonly StyledProperty<FaqContact?> ContactProperty =
        AvaloniaProperty.Register<FaqSection, FaqContact?>(nameof(Contact));

    /// <summary>False hides the contact row.</summary>
    public static readonly StyledProperty<bool> IsContactVisibleProperty =
        AvaloniaProperty.Register<FaqSection, bool>(nameof(IsContactVisible), true);

    /// <summary>The accessible name of the topic rail.</summary>
    public static readonly StyledProperty<string> TopicsLabelProperty =
        AvaloniaProperty.Register<FaqSection, string>(nameof(TopicsLabel), "Question topics");

    /// <summary>The label of the search field.</summary>
    public static readonly StyledProperty<string> SearchLabelProperty =
        AvaloniaProperty.Register<FaqSection, string>(nameof(SearchLabel), "Search questions");

    /// <summary>The placeholder of the search field.</summary>
    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<FaqSection, string>(nameof(SearchPlaceholder), "Try refund, seats or offline");

    /// <summary>The count line with no search text; {0} is the number of questions.</summary>
    public static readonly StyledProperty<string> TotalFormatProperty =
        AvaloniaProperty.Register<FaqSection, string>(nameof(TotalFormat), "{0} questions");

    /// <summary>The count line with several results; {0} is the number of answers.</summary>
    public static readonly StyledProperty<string> ResultsFormatProperty =
        AvaloniaProperty.Register<FaqSection, string>(nameof(ResultsFormat), "{0} answers");

    /// <summary>The count line with one result; {0} is 1.</summary>
    public static readonly StyledProperty<string> OneResultFormatProperty =
        AvaloniaProperty.Register<FaqSection, string>(nameof(OneResultFormat), "{0} answer");

    /// <summary>The line shown when nothing matches; {0} is the search text.</summary>
    public static readonly StyledProperty<string> NoResultsFormatProperty =
        AvaloniaProperty.Register<FaqSection, string>(nameof(NoResultsFormat), "No answers mention “{0}”.");

    /// <summary>The button that clears a search with no results.</summary>
    public static readonly StyledProperty<string> ClearSearchLabelProperty =
        AvaloniaProperty.Register<FaqSection, string>(nameof(ClearSearchLabel), "Clear search");

    /// <summary>The contact row was pressed (before it runs its command or opens its address).</summary>
    public static readonly RoutedEvent<FaqContactEventArgs> ContactInvokedEvent =
        RoutedEvent.Register<FaqSection, FaqContactEventArgs>(nameof(ContactInvoked), RoutingStrategies.Bubble);

    private readonly AvaloniaList<FaqItem> _items = new();

    /// <inheritdoc cref="VariantProperty"/>
    public FaqSectionVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ItemsProperty"/>
    public AvaloniaList<FaqItem> Items => _items;

    /// <inheritdoc cref="IsMultipleProperty"/>
    public bool IsMultiple { get => GetValue(IsMultipleProperty); set => SetValue(IsMultipleProperty, value); }

    /// <inheritdoc cref="OpenItemsProperty"/>
    public IReadOnlyList<string>? OpenItems { get => GetValue(OpenItemsProperty); set => SetValue(OpenItemsProperty, value); }

    /// <inheritdoc cref="SelectedCategoryProperty"/>
    public string? SelectedCategory { get => GetValue(SelectedCategoryProperty); set => SetValue(SelectedCategoryProperty, value); }

    /// <inheritdoc cref="QueryProperty"/>
    public string Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <inheritdoc cref="ContactProperty"/>
    public FaqContact? Contact { get => GetValue(ContactProperty); set => SetValue(ContactProperty, value); }

    /// <inheritdoc cref="IsContactVisibleProperty"/>
    public bool IsContactVisible { get => GetValue(IsContactVisibleProperty); set => SetValue(IsContactVisibleProperty, value); }

    /// <inheritdoc cref="TopicsLabelProperty"/>
    public string TopicsLabel { get => GetValue(TopicsLabelProperty); set => SetValue(TopicsLabelProperty, value); }

    /// <inheritdoc cref="SearchLabelProperty"/>
    public string SearchLabel { get => GetValue(SearchLabelProperty); set => SetValue(SearchLabelProperty, value); }

    /// <inheritdoc cref="SearchPlaceholderProperty"/>
    public string SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <inheritdoc cref="TotalFormatProperty"/>
    public string TotalFormat { get => GetValue(TotalFormatProperty); set => SetValue(TotalFormatProperty, value); }

    /// <inheritdoc cref="ResultsFormatProperty"/>
    public string ResultsFormat { get => GetValue(ResultsFormatProperty); set => SetValue(ResultsFormatProperty, value); }

    /// <inheritdoc cref="OneResultFormatProperty"/>
    public string OneResultFormat { get => GetValue(OneResultFormatProperty); set => SetValue(OneResultFormatProperty, value); }

    /// <inheritdoc cref="NoResultsFormatProperty"/>
    public string NoResultsFormat { get => GetValue(NoResultsFormatProperty); set => SetValue(NoResultsFormatProperty, value); }

    /// <inheritdoc cref="ClearSearchLabelProperty"/>
    public string ClearSearchLabel { get => GetValue(ClearSearchLabelProperty); set => SetValue(ClearSearchLabelProperty, value); }

    /// <inheritdoc cref="ContactInvokedEvent"/>
    public event EventHandler<FaqContactEventArgs>? ContactInvoked
    {
        add => AddHandler(ContactInvokedEvent, value);
        remove => RemoveHandler(ContactInvokedEvent, value);
    }

    #endregion

    private FaqContact? _defaultContact;
    private FaqItem[]? _samples;
    private readonly List<FaqRow> _rows = new();
    private readonly Dictionary<string, Control> _topicLists = new();
    private readonly List<FaqCategoryButton> _categoryButtons = new();
    private Panel? _host;
    private Grid? _columns;
    private Control? _rail;
    private Control? _stageColumn;
    private ContentFaceSwap? _stage;
    private StackPanel? _categoryStack;
    private ScrollViewer? _categoryScroller;
    private GlidingIndicator? _categoryHighlight;
    private SearchField? _search;
    private TextBlock? _countLine;
    private SpringSizeDecorator? _emptyHost;
    private TextBlock? _emptyLine;
    private FaqContactButton? _contactButton;
    private CtaBalancedText? _titleBlock;
    private CtaBalancedText? _descriptionBlock;
    private Border? _body;
    private bool _indicatorPlaced;
    private bool _rebuilding;

    public FaqSection()
    {
        _items.CollectionChanged += (_, _) => Rebuild();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Bubble);
    }

    private IReadOnlyList<FaqItem> EffectiveItems => _items.Count > 0 ? _items : _samples ??= FaqText.CreateSamples();

    /// <summary>The topics in the order they first appear.</summary>
    internal IReadOnlyList<string> Categories => EffectiveItems.Select(i => i.Topic).Distinct().ToArray();

    /// <summary>The topic in effect.</summary>
    internal string ActiveCategory
    {
        get
        {
            var categories = Categories;
            return SelectedCategory is { } chosen && categories.Contains(chosen) ? chosen : categories.Count > 0 ? categories[0] : "General";
        }
    }

    /// <summary>Every question row built for the current layout.</summary>
    internal IReadOnlyList<FaqRow> Rows => _rows;

    /// <summary>The topic stage of the columns layout.</summary>
    internal ContentFaceSwap? Stage => _stage;

    internal IReadOnlyList<FaqCategoryButton> CategoryButtons => _categoryButtons;

    internal SearchField? SearchBox => _search;

    internal TextBlock? CountLine => _countLine;

    internal bool IsEmptyShown => _emptyHost?.IsExpanded == true;

    internal TextBlock? EmptyLine => _emptyLine;

    internal FaqContactButton? ContactRow => _contactButton;

    /// <summary>The rail's position: 0 in the first column, 1 stacked above the questions.</summary>
    internal bool IsStacked => _columns is not null && Grid.GetRow(_stageColumn!) == 1;

    /// <summary>Clears the search text and puts focus back in the field.</summary>
    public void ClearSearch()
    {
        SetCurrentValue(QueryProperty, string.Empty);
        _search?.Focus();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>(HostPart);
        _body = e.NameScope.Find<Border>("PART_Body");
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty)
        {
            Rebuild();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateWidthState(((Rect)change.NewValue!).Width);
        }
        else if (change.Property == OpenItemsProperty)
        {
            UpdateOpenRows();
        }
        else if (change.Property == SelectedCategoryProperty)
        {
            ShowCategory();
        }
        else if (change.Property == QueryProperty)
        {
            UpdateSearch();
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty)
        {
            UpdateHeading();
        }
        else if (change.Property == TotalFormatProperty || change.Property == ResultsFormatProperty
                 || change.Property == OneResultFormatProperty || change.Property == NoResultsFormatProperty)
        {
            UpdateSearch();
        }
        else if (change.Property == ContactProperty || change.Property == IsContactVisibleProperty)
        {
            UpdateContact();
        }
        else if (change.Property == SearchLabelProperty || change.Property == SearchPlaceholderProperty)
        {
            if (_search is not null)
            {
                _search.Label = SearchLabel;
                _search.Placeholder = SearchPlaceholder;
            }
        }
    }

    private void UpdateWidthState(double width)
    {
        var narrow = width <= 760;
        var compact = width <= 480;
        var changedNarrow = PseudoClasses.Contains(NarrowPseudoClass) != narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        foreach (var row in _rows)
        {
            row.IsNarrow = compact;
        }
        if (_titleBlock is not null)
        {
            HeroText.SetTitleSize(_titleBlock, TitleSize(width));
        }
        if (_body is not null)
        {
            var columns = Variant == FaqSectionVariant.Columns;
            _body.Padding = compact ? new Thickness(16, 48) : columns && !narrow ? new Thickness(32, 80) : new Thickness(24, 80);
        }
        if (changedNarrow || _columns is not null)
        {
            UpdateColumns(narrow);
        }
    }

    private double TitleSize(double width)
    {
        var kind = width >= 720 ? ArcDesignTokenKind.Type4XL : ArcDesignTokenKind.Type3XL;
        return ArcTokens.Get<double>(this, kind);
    }

    private void UpdateColumns(bool narrow)
    {
        if (_columns is null || _rail is null || _stageColumn is null)
        {
            return;
        }
        _columns.ColumnDefinitions = narrow ? new ColumnDefinitions("*") : new ColumnDefinitions("0.8*,1.2*");
        _columns.RowDefinitions = narrow ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
        _columns.ColumnSpacing = narrow ? 0 : 64;
        _columns.RowSpacing = narrow ? 32 : 0;
        Grid.SetColumn(_stageColumn, narrow ? 0 : 1);
        Grid.SetRow(_stageColumn, narrow ? 1 : 0);
        if (_categoryStack is not null && _categoryScroller is not null)
        {
            _categoryStack.Orientation = narrow ? Orientation.Horizontal : Orientation.Vertical;
            _categoryScroller.HorizontalScrollBarVisibility = narrow ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        }
    }

    private void Rebuild()
    {
        if (_host is null || _rebuilding)
        {
            return;
        }
        _rebuilding = true;
        PseudoClasses.Set(ColumnsPseudoClass, Variant == FaqSectionVariant.Columns);
        _shownCategory = null;
        foreach (var row in _rows)
        {
            row.Toggled -= OnRowToggled;
        }
        _host.Children.Clear();
        _rows.Clear();
        _topicLists.Clear();
        _categoryButtons.Clear();
        _columns = null;
        _rail = null;
        _stageColumn = null;
        _stage = null;
        _categoryStack = null;
        _categoryScroller = null;
        _categoryHighlight = null;
        _search = null;
        _countLine = null;
        _emptyHost = null;
        _emptyLine = null;
        _contactButton = null;
        _indicatorPlaced = false;

        if (OpenItems is null && EffectiveItems.Count > 0)
        {
            SetCurrentValue(OpenItemsProperty, new[] { EffectiveItems[0].Key });
        }

        BuildHeading();
        switch (Variant)
        {
            case FaqSectionVariant.Columns:
                BuildColumns();
                break;
            case FaqSectionVariant.Search:
                BuildSearch();
                break;
            default:
                BuildAccordion();
                break;
        }
        _rebuilding = false;
        UpdateHeading();
        UpdateContact();
        UpdateOpenRows();
        UpdateSearch();
        ShowCategory();
        UpdateWidthState(Bounds.Width);
    }

    private void BuildHeading()
    {
        _titleBlock = new CtaBalancedText
        {
            TextWrapping = TextWrapping.WrapWithOverflow,
            IsBalanced = true,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ArcTokens.Bind(_titleBlock, CtaBalancedText.LeadingProperty, ArcDesignTokenKind.LeadingDisplay);
        ArcTokens.Bind(_titleBlock, CtaBalancedText.TrackingProperty, ArcDesignTokenKind.TrackingDisplay);
        ArcTokens.Bind(_titleBlock, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_titleBlock, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_titleBlock, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _titleBlock.FontSize = 36;
        AutomationProperties.SetHeadingLevel(_titleBlock, 2);

        _descriptionBlock = new CtaBalancedText
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 480
        };
        ArcTokens.Bind(_descriptionBlock, CtaBalancedText.LeadingProperty, ArcDesignTokenKind.LeadingBody);
        ArcTokens.Bind(_descriptionBlock, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeBase);
        ArcTokens.Bind(_descriptionBlock, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        if (Variant != FaqSectionVariant.Columns)
        {
            _titleBlock.HorizontalAlignment = HorizontalAlignment.Center;
            _titleBlock.TextAlignment = TextAlignment.Center;
            _descriptionBlock.HorizontalAlignment = HorizontalAlignment.Center;
            _descriptionBlock.TextAlignment = TextAlignment.Center;
        }
    }

    private Control Intro() => new StackPanel { Spacing = 12, Children = { _titleBlock!, _descriptionBlock! } };

    private void UpdateHeading()
    {
        if (_titleBlock is not null)
        {
            _titleBlock.Text = Title;
            _titleBlock.IsVisible = !string.IsNullOrEmpty(Title);
            AutomationProperties.SetName(this, Title ?? string.Empty);
        }
        if (_descriptionBlock is not null)
        {
            _descriptionBlock.Text = Description;
            _descriptionBlock.IsVisible = !string.IsNullOrEmpty(Description);
        }
    }

    private FaqRow CreateRow(FaqItem item)
    {
        var row = new FaqRow(item);
        row.Toggled += OnRowToggled;
        _rows.Add(row);
        return row;
    }

    private Control CreateList(IEnumerable<FaqItem> items)
    {
        var stack = new StackPanel();
        foreach (var item in items)
        {
            stack.Children.Add(CreateRow(item));
        }
        var list = new Border { Child = stack, BorderThickness = new Thickness(0, 1, 0, 0), Classes = { "faq-list" } };
        ArcTokens.Bind(list, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return list;
    }

    private FaqContactButton CreateContact()
    {
        _contactButton = new FaqContactButton();
        _contactButton.Click += OnContactClick;
        return _contactButton;
    }

    private void UpdateContact()
    {
        if (_contactButton is null)
        {
            return;
        }
        var contact = Contact ?? (_defaultContact ??= new FaqContact { Label = "Still have a question?", Description = "A person answers within a day." });
        _contactButton.Label = contact.Label;
        _contactButton.Description = contact.Description;
        _contactButton.IsVisible = IsContactVisible;
        AutomationProperties.SetName(_contactButton, contact.Label);
    }

    private void OnContactClick(object? sender, RoutedEventArgs e)
    {
        var contact = Contact ?? (_defaultContact ??= new FaqContact { Label = "Still have a question?", Description = "A person answers within a day." });
        var args = new FaqContactEventArgs(ContactInvokedEvent, this, contact);
        RaiseEvent(args);
        if (contact.Command?.CanExecute(contact.CommandParameter) == true)
        {
            contact.Command.Execute(contact.CommandParameter);
        }
        if (!args.Handled && !string.IsNullOrWhiteSpace(contact.Href))
        {
            SiteLinks.TryLaunch(this, contact.Href);
        }
    }

    private void BuildAccordion()
    {
        _host!.Children.Add(new StackPanel
        {
            Spacing = 32,
            Children = { Intro(), CreateList(EffectiveItems), CreateContact() }
        });
    }

    private void BuildSearch()
    {
        _search = new SearchField { Label = SearchLabel, Placeholder = SearchPlaceholder, Text = Query };
        _search.TextChanged += OnSearchTextChanged;
        _countLine = new TextBlock();
        ArcTokens.Bind(_countLine, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_countLine, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        AutomationProperties.SetLiveSetting(_countLine, AutomationLiveSetting.Polite);

        _emptyLine = new TextBlock { TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(_emptyLine, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_emptyLine, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var clear = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Center };
        clear.Bind(ContentControl.ContentProperty, this.GetObservable(ClearSearchLabelProperty));
        clear.Click += (_, _) => ClearSearch();
        _emptyHost = new SpringSizeDecorator
        {
            Axis = SpringSizeAxis.Height,
            Spring = ArcSpring.Smooth,
            IsExpanded = false,
            Child = new StackPanel { Spacing = 12, Margin = new Thickness(0, 24), Children = { _emptyLine, clear } }
        };

        _host!.Children.Add(new StackPanel
        {
            Spacing = 32,
            Children =
            {
                Intro(),
                new StackPanel { Spacing = 8, Children = { _search, _countLine } },
                new StackPanel { Children = { CreateList(EffectiveItems), _emptyHost } },
                CreateContact()
            }
        });
    }

    private void BuildColumns()
    {
        var items = EffectiveItems;
        _categoryHighlight = new GlidingIndicator { Spring = ArcSpring.Morph, IsHitTestVisible = false };
        ArcTokens.Bind(_categoryHighlight, GlidingIndicator.FillProperty, ArcDesignTokenKind.SurfaceMuted);
        _categoryHighlight.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        _categoryStack = new StackPanel { Spacing = 2 };
        AutomationProperties.SetName(_categoryStack, TopicsLabel);
        foreach (var topic in Categories)
        {
            var button = new FaqCategoryButton { Topic = topic, Count = items.Count(i => i.Topic == topic) };
            AutomationProperties.SetName(button, topic);
            button.Click += (_, _) => ChooseCategory(topic);
            _categoryButtons.Add(button);
            _categoryStack.Children.Add(button);
        }
        var rail = new Panel { Children = { _categoryHighlight, _categoryStack } };
        rail.SizeChanged += (_, _) => PlaceIndicator(false);
        _categoryScroller = new ScrollViewer
        {
            Content = rail,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _rail = new StackPanel { Spacing = 24, Children = { Intro(), _categoryScroller, CreateContact() } };

        _stage = new ContentFaceSwap
        {
            EnterTravel = 16,
            ExitTravel = 12,
            BlurRadius = 2,
            EnterDuration = ArcMotionPresets.Fast,
            EnterDelay = TimeSpan.Zero,
            ExitDuration = ArcMotionPresets.Instant,
            ReducedEnterDuration = TimeSpan.Zero,
            ReducedExitDuration = TimeSpan.Zero
        };
        _stageColumn = _stage;
        _columns = new Grid { Children = { _rail, _stageColumn } };
        _host!.Children.Add(_columns);
    }

    private void ChooseCategory(string topic)
    {
        if (topic != ActiveCategory)
        {
            SetCurrentValue(SelectedCategoryProperty, topic);
        }
    }

    private string? _shownCategory;

    private void ShowCategory()
    {
        if (_stage is null)
        {
            return;
        }
        var topic = ActiveCategory;
        if (!_topicLists.TryGetValue(topic, out var list))
        {
            list = CreateList(EffectiveItems.Where(i => i.Topic == topic));
            _topicLists[topic] = list;
            UpdateOpenRows();
        }
        var categories = Categories.ToList();
        var direction = _shownCategory is null ? 1 : Math.Sign(categories.IndexOf(topic) - categories.IndexOf(_shownCategory));
        _shownCategory = topic;
        _stage.Swap(list, direction == 0 ? 1 : direction);
        foreach (var button in _categoryButtons)
        {
            button.IsActive = button.Topic == topic;
        }
        PlaceIndicator(_indicatorPlaced);
    }

    private void PlaceIndicator(bool animate)
    {
        if (_categoryHighlight is null)
        {
            return;
        }
        var button = _categoryButtons.FirstOrDefault(b => b.IsActive);
        if (button is null)
        {
            return;
        }
        // The stack and the highlight share the rail, so a button's bounds are the highlight's coordinates.
        var rect = button.Bounds;
        if (rect.Width <= 0)
        {
            Dispatcher.UIThread.Post(() => PlaceIndicator(false), DispatcherPriority.Loaded);
            return;
        }
        _categoryHighlight.MoveTo(rect, animate && ArcMotion.IsFull(this));
        _indicatorPlaced = true;
    }

    private void OnRowToggled(object? sender, EventArgs e)
    {
        if (sender is FaqRow row)
        {
            Toggle(row.Item.Key);
        }
    }

    private void Toggle(string key)
    {
        var open = OpenItems ?? [];
        string[] next = open.Contains(key)
            ? open.Where(k => k != key).ToArray()
            : IsMultiple ? [.. open, key] : [key];
        SetCurrentValue(OpenItemsProperty, next);
    }

    private void UpdateOpenRows()
    {
        var open = OpenItems ?? [];
        foreach (var row in _rows)
        {
            row.IsOpen = open.Contains(row.Item.Key);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = _search?.Text ?? string.Empty;
        if (text != Query)
        {
            SetCurrentValue(QueryProperty, text);
        }
    }

    private void UpdateSearch()
    {
        if (_search is null)
        {
            return;
        }
        if ((_search.Text ?? string.Empty) != Query)
        {
            _search.Text = Query;
        }
        var query = Query.Trim();
        var results = 0;
        foreach (var row in _rows)
        {
            var match = query.Length == 0 || FaqText.Contains($"{row.Item.Question} {row.Item.Answer}", query);
            row.IsPresent = match;
            row.Query = Query;
            results += match ? 1 : 0;
        }
        if (_countLine is not null)
        {
            _countLine.Text = query.Length == 0
                ? string.Format(TotalFormat, _rows.Count)
                : string.Format(results == 1 ? OneResultFormat : ResultsFormat, results);
        }
        var empty = query.Length > 0 && results == 0;
        if (empty && _emptyLine is not null)
        {
            _emptyLine.Text = string.Format(NoResultsFormat, query);
        }
        if (_emptyHost is not null)
        {
            _emptyHost.IsExpanded = empty;
        }
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Up or Key.Home or Key.End) || e.Source is not Visual source)
        {
            return;
        }
        var row = _rows.FirstOrDefault(r => r.Trigger is { } trigger && (ReferenceEquals(trigger, source) || trigger.IsVisualAncestorOf(source)));
        if (row is null)
        {
            return;
        }
        // One list: the rows of the topic on stage, or those a search leaves.
        var list = _rows.Where(r => r.IsPresent && r.IsAttachedToVisualTree() && r.Trigger is not null).ToList();
        var index = list.IndexOf(row);
        if (index < 0)
        {
            return;
        }
        var next = e.Key switch
        {
            Key.Home => 0,
            Key.End => list.Count - 1,
            Key.Down => (index + 1) % list.Count,
            _ => (index - 1 + list.Count) % list.Count
        };
        list[next].Trigger!.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
