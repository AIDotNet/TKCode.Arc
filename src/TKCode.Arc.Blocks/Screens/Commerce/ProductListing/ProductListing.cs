using System.Globalization;
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
using Avalonia.Media.Immutable;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A product listing page: category chips, a price range, an in-stock switch and a sort, over a grid of product cards that
/// fits as many columns as the block's width allows. Each card shows the picture, a tag, the name, the rating, the price
/// (with the price before a reduction) and the colors, and has a quick add button; the header keeps the count of what was
/// added. In a narrow block the filters fold behind a button.
/// <code>
/// &lt;arc:ProductListing Title="Everyday goods" ProductsSource="{Binding Products}" /&gt;
/// </code>
/// </summary>
[TemplatePart(GridPart, typeof(ProductListingGrid))]
[TemplatePart(SortPart, typeof(Select))]
[TemplatePart(CategoriesPart, typeof(ChipGroup))]
[TemplatePart(PriceRangePart, typeof(Slider))]
[TemplatePart(InStockPart, typeof(Switch))]
[TemplatePart(CartPart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(FinanceAnnouncer))]
[PseudoClasses(CompactPseudoClass, FiltersOpenPseudoClass, EmptyPseudoClass)]
public class ProductListing : TemplatedControl
{
    public const string GridPart = "PART_Grid";
    public const string SortPart = "PART_Sort";
    public const string CategoriesPart = "PART_Categories";
    public const string PriceRangePart = "PART_PriceRange";
    public const string InStockPart = "PART_InStock";
    public const string CartPart = "PART_Cart";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The block is narrower than its compact threshold: the filters fold behind a button.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>The folded filters are open.</summary>
    public const string FiltersOpenPseudoClass = ":filters-open";

    /// <summary>No product passes the filters.</summary>
    public const string EmptyPseudoClass = ":empty";

    // Lucide "shopping-bag", "star" (ISC).
    private static readonly Geometry s_bag = ArcIcons.Parse(
        "M16 10a4 4 0 0 1-8 0 M3.103 6.034h17.794 M3.4 5.467a2 2 0 0 0-.4 1.2V20a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6.667a2 2 0 0 0-.4-1.2l-2-2.667A2 2 0 0 0 17 2H7a2 2 0 0 0-1.6.8z");

    private static readonly TimeSpan AddedFor = TimeSpan.FromMilliseconds(1400);

    #region Properties

    /// <summary>The heading over the grid.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ProductListing, string?>(nameof(Title), "Products");

    /// <summary>The products, when bound; otherwise <see cref="Products"/> is used.</summary>
    public static readonly StyledProperty<IEnumerable<ListingProduct>?> ProductsSourceProperty =
        AvaloniaProperty.Register<ProductListing, IEnumerable<ListingProduct>?>(nameof(ProductsSource));

    /// <summary>How the grid is ordered (two-way).</summary>
    public static readonly StyledProperty<ListingSort> SortProperty =
        AvaloniaProperty.Register<ProductListing, ListingSort>(nameof(Sort), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The categories shown; empty shows every category.</summary>
    public static readonly DirectProperty<ProductListing, IReadOnlyList<string>> SelectedCategoriesProperty =
        AvaloniaProperty.RegisterDirect<ProductListing, IReadOnlyList<string>>(nameof(SelectedCategories), o => o.SelectedCategories,
            (o, v) => o.SelectedCategories = v);

    /// <summary>The lowest price shown (two-way); null starts at the cheapest product.</summary>
    public static readonly StyledProperty<decimal?> MinPriceProperty =
        AvaloniaProperty.Register<ProductListing, decimal?>(nameof(MinPrice), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The highest price shown (two-way); null ends at the most expensive product.</summary>
    public static readonly StyledProperty<decimal?> MaxPriceProperty =
        AvaloniaProperty.Register<ProductListing, decimal?>(nameof(MaxPrice), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether sold-out products are hidden (two-way).</summary>
    public static readonly StyledProperty<bool> InStockOnlyProperty =
        AvaloniaProperty.Register<ProductListing, bool>(nameof(InStockOnly), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many items were added with the quick add buttons (two-way, so the app can reset it).</summary>
    public static readonly StyledProperty<int> CartCountProperty =
        AvaloniaProperty.Register<ProductListing, int>(nameof(CartCount), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Most columns the grid uses (4).</summary>
    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<ProductListing, int>(nameof(MaxColumns), 4, coerce: (_, v) => Math.Max(1, v));

    /// <summary>The symbol written before prices ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<ProductListing, string>(nameof(CurrencySymbol), "$");

    /// <summary>The block's built-in text.</summary>
    public static readonly StyledProperty<ProductListingLabels> LabelsProperty =
        AvaloniaProperty.Register<ProductListing, ProductListingLabels>(nameof(Labels), new ProductListingLabels());

    /// <summary>The products that pass the filters, in the sort order.</summary>
    public static readonly DirectProperty<ProductListing, IReadOnlyList<ListingProduct>> VisibleProductsProperty =
        AvaloniaProperty.RegisterDirect<ProductListing, IReadOnlyList<ListingProduct>>(nameof(VisibleProducts), o => o.VisibleProducts);

    internal static readonly StyledProperty<double> CompactThresholdProperty =
        AvaloniaProperty.Register<ProductListing, double>(nameof(CompactThreshold), 760);

    #endregion

    #region Events

    /// <summary>Raised when a quick add button is pressed.</summary>
    public static readonly RoutedEvent<ListingProductEventArgs> AddedToCartEvent =
        RoutedEvent.Register<ProductListing, ListingProductEventArgs>(nameof(AddedToCart), RoutingStrategies.Bubble);

    /// <summary>Raised when a card is pressed (to open the product).</summary>
    public static readonly RoutedEvent<ListingProductEventArgs> ProductInvokedEvent =
        RoutedEvent.Register<ProductListing, ListingProductEventArgs>(nameof(ProductInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when the cart button is pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CartInvokedEvent =
        RoutedEvent.Register<ProductListing, RoutedEventArgs>(nameof(CartInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="AddedToCartEvent"/>
    public event EventHandler<ListingProductEventArgs>? AddedToCart
    {
        add => AddHandler(AddedToCartEvent, value);
        remove => RemoveHandler(AddedToCartEvent, value);
    }

    /// <inheritdoc cref="ProductInvokedEvent"/>
    public event EventHandler<ListingProductEventArgs>? ProductInvoked
    {
        add => AddHandler(ProductInvokedEvent, value);
        remove => RemoveHandler(ProductInvokedEvent, value);
    }

    /// <inheritdoc cref="CartInvokedEvent"/>
    public event EventHandler<RoutedEventArgs>? CartInvoked
    {
        add => AddHandler(CartInvokedEvent, value);
        remove => RemoveHandler(CartInvokedEvent, value);
    }

    #endregion

    private sealed class Card
    {
        public required ListingProduct Product { get; init; }
        public required Control Root { get; init; }
        public required ProductListingCardButton Open { get; init; }
        public required Button Add { get; init; }
        public DispatcherTimer? AddedTimer { get; set; }
    }

    private readonly Dictionary<ListingProduct, Card> _cards = new();
    private IReadOnlyList<string> _selectedCategories = [];
    private IReadOnlyList<ListingProduct> _visible = [];
    private INameScope? _scope;
    private ProductListingGrid? _grid;
    private Select? _sort;
    private ChipGroup? _categories;
    private Slider? _price;
    private Switch? _inStock;
    private Button? _cart;
    private FinanceAnnouncer? _announcer;
    private bool _syncing;
    private bool _isCompact;
    private bool _filtersOpen;

    static ProductListing()
    {
        FocusableProperty.OverrideDefaultValue<ProductListing>(false);
    }

    public ProductListing()
    {
        Products.CollectionChanged += (_, _) => OnProductsChanged();
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ProductsSourceProperty"/>
    public IEnumerable<ListingProduct>? ProductsSource { get => GetValue(ProductsSourceProperty); set => SetValue(ProductsSourceProperty, value); }

    /// <inheritdoc cref="SortProperty"/>
    public ListingSort Sort { get => GetValue(SortProperty); set => SetValue(SortProperty, value); }

    /// <inheritdoc cref="SelectedCategoriesProperty"/>
    public IReadOnlyList<string> SelectedCategories
    {
        get => _selectedCategories;
        set
        {
            if (SetAndRaise(SelectedCategoriesProperty, ref _selectedCategories, value ?? []))
            {
                SyncFilters();
                Apply(announce: true);
            }
        }
    }

    /// <inheritdoc cref="MinPriceProperty"/>
    public decimal? MinPrice { get => GetValue(MinPriceProperty); set => SetValue(MinPriceProperty, value); }

    /// <inheritdoc cref="MaxPriceProperty"/>
    public decimal? MaxPrice { get => GetValue(MaxPriceProperty); set => SetValue(MaxPriceProperty, value); }

    /// <inheritdoc cref="InStockOnlyProperty"/>
    public bool InStockOnly { get => GetValue(InStockOnlyProperty); set => SetValue(InStockOnlyProperty, value); }

    /// <inheritdoc cref="CartCountProperty"/>
    public int CartCount { get => GetValue(CartCountProperty); set => SetValue(CartCountProperty, value); }

    /// <inheritdoc cref="MaxColumnsProperty"/>
    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ProductListingLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="VisibleProductsProperty"/>
    public IReadOnlyList<ListingProduct> VisibleProducts { get => _visible; private set => SetAndRaise(VisibleProductsProperty, ref _visible, value); }

    internal double CompactThreshold { get => GetValue(CompactThresholdProperty); set => SetValue(CompactThresholdProperty, value); }

    /// <summary>The products, used when <see cref="ProductsSource"/> is not set.</summary>
    [Content]
    public AvaloniaList<ListingProduct> Products { get; } = new();

    /// <summary>The products in use: <see cref="ProductsSource"/> when set, otherwise <see cref="Products"/>.</summary>
    public IReadOnlyList<ListingProduct> EffectiveProducts => ProductsSource is { } source ? source.ToList() : Products;

    /// <summary>The categories of the products, in first-seen order (the chips).</summary>
    public IReadOnlyList<string> Categories => EffectiveProducts.Select(p => p.Category).OfType<string>().Distinct().ToList();

    /// <summary>True while the block is narrower than its compact threshold.</summary>
    public bool IsCompact => _isCompact;

    /// <summary>The number of grid columns of the last layout.</summary>
    public int ColumnCount => _grid?.ColumnCount ?? 1;

    /// <summary>The last text sent to the live region (tests and diagnostics).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    /// <summary>Clears the categories, the price range and the in-stock switch.</summary>
    public void ClearFilters()
    {
        _syncing = true;
        try
        {
            SetAndRaise(SelectedCategoriesProperty, ref _selectedCategories, []);
            SetCurrentValue(MinPriceProperty, null);
            SetCurrentValue(MaxPriceProperty, null);
            SetCurrentValue(InStockOnlyProperty, false);
        }
        finally
        {
            _syncing = false;
        }
        SyncFilters();
        Apply(announce: true);
    }

    /// <summary>Adds one of <paramref name="product"/> as its card's button does; returns false for a sold-out product.</summary>
    public bool AddToCart(ListingProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!product.IsInStock)
        {
            return false;
        }
        SetCurrentValue(CartCountProperty, CartCount + 1);
        _announcer?.Announce(Labels.AnnounceAdded(product.Name));
        if (_cards.TryGetValue(product, out var card))
        {
            ShowAdded(card);
        }
        RaiseEvent(new ListingProductEventArgs(AddedToCartEvent, product));
        return true;
    }

    internal string Money(decimal amount)
    {
        var format = (NumberFormatInfo)Labels.Culture.NumberFormat.Clone();
        format.CurrencySymbol = CurrencySymbol;
        var decimals = amount == Math.Round(amount) ? "C0" : "C2";
        return amount.ToString(decimals, format);
    }

    private (decimal Min, decimal Max) Bounds2()
    {
        var products = EffectiveProducts;
        if (products.Count == 0)
        {
            return (0, 0);
        }
        return (Math.Floor(products.Min(p => p.Price)), Math.Ceiling(products.Max(p => p.Price)));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Unhook();
        base.OnApplyTemplate(e);
        _scope = e.NameScope;
        _grid = _scope.Find<ProductListingGrid>(GridPart);
        _sort = _scope.Find<Select>(SortPart);
        _categories = _scope.Find<ChipGroup>(CategoriesPart);
        _price = _scope.Find<Slider>(PriceRangePart);
        _inStock = _scope.Find<Switch>(InStockPart);
        _cart = _scope.Find<Button>(CartPart);
        _announcer = _scope.Find<FinanceAnnouncer>(AnnouncerPart);
        if (_sort is not null)
        {
            _sort.ItemsSource = Enum.GetValues<ListingSort>().Cast<object>().ToList();
            _sort.SelectionChanged += OnSortChanged;
        }
        if (_categories is not null)
        {
            _categories.SelectionChanged += OnCategoriesChanged;
        }
        if (_price is not null)
        {
            _price.PropertyChanged += OnPriceChanged;
        }
        if (_inStock is not null)
        {
            _inStock.IsCheckedChanged += OnInStockChanged;
        }
        if (_cart is not null)
        {
            _cart.Click += OnCartClick;
        }
        if (_scope.Find<Button>("PART_FiltersToggle") is { } toggle)
        {
            toggle.Click += (_, _) => SetFiltersOpen(!_filtersOpen);
        }
        foreach (var name in new[] { "PART_Clear", "PART_EmptyClear" })
        {
            if (_scope.Find<Button>(name) is { } clear)
            {
                clear.Click += (_, _) => ClearFilters();
            }
        }
        if (_grid is not null)
        {
            _grid.MaxColumns = MaxColumns;
        }
        _cards.Clear();
        UpdateTexts();
        OnProductsChanged();
        UpdateCompact(force: true);
    }

    private void Unhook()
    {
        if (_sort is not null)
        {
            _sort.SelectionChanged -= OnSortChanged;
        }
        if (_categories is not null)
        {
            _categories.SelectionChanged -= OnCategoriesChanged;
        }
        if (_price is not null)
        {
            _price.PropertyChanged -= OnPriceChanged;
        }
        if (_inStock is not null)
        {
            _inStock.IsCheckedChanged -= OnInStockChanged;
        }
        if (_cart is not null)
        {
            _cart.Click -= OnCartClick;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == ProductsSourceProperty)
        {
            OnProductsChanged();
        }
        else if (p == SortProperty || p == MinPriceProperty || p == MaxPriceProperty || p == InStockOnlyProperty)
        {
            if (!_syncing)
            {
                SyncFilters();
                Apply(announce: p != SortProperty);
            }
        }
        else if (p == CartCountProperty || p == TitleProperty)
        {
            UpdateTexts();
        }
        else if (p == LabelsProperty || p == CurrencySymbolProperty)
        {
            _cards.Clear();
            UpdateTexts();
            OnProductsChanged();
        }
        else if (p == MaxColumnsProperty && _grid is not null)
        {
            _grid.MaxColumns = MaxColumns;
        }
        else if (p == BoundsProperty || p == CompactThresholdProperty)
        {
            UpdateCompact();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var card in _cards.Values)
        {
            card.AddedTimer?.Stop();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _grid is null)
        {
            return;
        }
        if (e.Key == Key.Escape && _isCompact && _filtersOpen)
        {
            SetFiltersOpen(false);
            e.Handled = true;
            return;
        }
        // Arrow keys move between the cards' open buttons in reading order.
        var cards = _visible.Select(p => _cards.GetValueOrDefault(p)).OfType<Card>().ToList();
        var index = cards.FindIndex(c => c.Open.IsFocused);
        if (index < 0)
        {
            return;
        }
        var columns = Math.Max(1, _grid.ColumnCount);
        var target = e.Key switch
        {
            Key.Right => index + 1,
            Key.Left => index - 1,
            Key.Down => index + columns,
            Key.Up => index - columns,
            Key.Home => 0,
            Key.End => cards.Count - 1,
            _ => int.MinValue,
        };
        if (target == int.MinValue)
        {
            return;
        }
        cards[Math.Clamp(target, 0, cards.Count - 1)].Open.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private void SetFiltersOpen(bool open)
    {
        _filtersOpen = open;
        PseudoClasses.Set(FiltersOpenPseudoClass, open);
        if (_scope?.Find<Button>("PART_FiltersToggle") is { } toggle)
        {
            AutomationProperties.SetName(toggle, Labels.Filters);
            toggle.Classes.Set("open", open);
        }
    }

    private void OnSortChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _sort?.SelectedItem is ListingSort sort)
        {
            SetCurrentValue(SortProperty, sort);
        }
    }

    private void OnCategoriesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _categories is null)
        {
            return;
        }
        SetAndRaise(SelectedCategoriesProperty, ref _selectedCategories, _categories.SelectedValues.OfType<string>().ToList());
        Apply(announce: true);
    }

    private void OnPriceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncing || _price is null || (e.Property != Slider.LowerValueProperty && e.Property != Slider.UpperValueProperty))
        {
            return;
        }
        var (min, max) = Bounds2();
        var lower = (decimal)_price.LowerValue;
        var upper = (decimal)_price.UpperValue;
        _syncing = true;
        try
        {
            SetCurrentValue(MinPriceProperty, lower <= min ? null : lower);
            SetCurrentValue(MaxPriceProperty, upper >= max ? null : upper);
        }
        finally
        {
            _syncing = false;
        }
        Apply(announce: false);
    }

    private void OnInStockChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing && _inStock is not null)
        {
            SetCurrentValue(InStockOnlyProperty, _inStock.IsChecked == true);
        }
    }

    private void OnCartClick(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(CartInvokedEvent));

    private void OnProductsChanged()
    {
        if (_categories is not null)
        {
            _syncing = true;
            _categories.ItemsSource = Categories;
            _syncing = false;
        }
        var live = EffectiveProducts.ToHashSet();
        foreach (var gone in _cards.Keys.Where(p => !live.Contains(p)).ToList())
        {
            _cards.Remove(gone);
        }
        SyncFilters();
        Apply(announce: false);
    }

    private void SyncFilters()
    {
        _syncing = true;
        try
        {
            if (_sort is not null)
            {
                _sort.SelectedItem = Sort;
                _sort.TextSelector = o => o is ListingSort s ? Labels.Sort(s) : null;
            }
            if (_categories is not null)
            {
                var items = Categories;
                _categories.SelectedItems?.Clear();
                for (var i = 0; i < items.Count; i++)
                {
                    if (_selectedCategories.Contains(items[i]))
                    {
                        _categories.Selection.Select(i);
                    }
                }
            }
            var (min, max) = Bounds2();
            if (_price is not null)
            {
                _price.Minimum = (double)min;
                _price.Maximum = (double)Math.Max(max, min + 1);
                _price.SmallChange = 1;
                _price.LowerValue = (double)Math.Clamp(MinPrice ?? min, min, max);
                _price.UpperValue = (double)Math.Clamp(MaxPrice ?? max, min, max);
            }
            if (_inStock is not null)
            {
                _inStock.IsChecked = InStockOnly;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Apply(bool announce)
    {
        var (min, max) = Bounds2();
        var low = MinPrice ?? min;
        var high = MaxPrice ?? max;
        var visible = ProductListingMath.Apply(EffectiveProducts, _selectedCategories, low, high, InStockOnly, Sort);
        VisibleProducts = visible;
        if (_scope?.Find<TextBlock>("PART_PriceReadout") is { } readout)
        {
            readout.Text = Labels.PriceRange(Money(low), Money(high));
        }
        UpdateTexts();
        PseudoClasses.Set(EmptyPseudoClass, visible.Count == 0);
        if (_scope?.Find<StackPanel>("PART_Empty") is { } empty)
        {
            empty.IsVisible = visible.Count == 0;
        }
        if (_grid is null)
        {
            return;
        }
        var arriving = new List<Control>();
        _grid.Children.Clear();
        foreach (var product in visible)
        {
            if (!_cards.TryGetValue(product, out var card))
            {
                card = BuildCard(product);
                _cards[product] = card;
            }
            if (card.Root.Parent is null && ArcMotion.IsFull(this))
            {
                arriving.Add(card.Root);
            }
            _grid.Children.Add(card.Root);
        }
        // Cards that join the grid fade in; the others keep their place.
        foreach (var root in arriving)
        {
            root.Opacity = 0;
        }
        if (arriving.Count > 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var root in arriving)
                {
                    root.Opacity = 1;
                }
            }, DispatcherPriority.Render);
        }
        if (announce)
        {
            _announcer?.Announce(Labels.AnnounceResults(visible.Count));
        }
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        SetText("PART_Title", Title);
        SetText("PART_Count", labels.ResultCount(_visible.Count));
        SetText("PART_InStockLabel", labels.InStockOnly);
        SetText("PART_EmptyText", labels.NoResults);
        if (_categories is not null)
        {
            _categories.Label = labels.Category;
        }
        if (_price is not null)
        {
            _price.Label = labels.Price;
        }
        if (_sort is not null)
        {
            AutomationProperties.SetName(_sort, labels.SortBy);
        }
        if (_inStock is not null)
        {
            AutomationProperties.SetName(_inStock, labels.InStockOnly);
        }
        foreach (var name in new[] { "PART_Clear", "PART_EmptyClear" })
        {
            if (_scope?.Find<Button>(name) is { } clear)
            {
                clear.Content = labels.ClearFilters;
            }
        }
        if (_scope?.Find<Button>("PART_FiltersToggle") is { } toggle)
        {
            toggle.Content = Glyphed(ArcIcons.SlidersHorizontal, labels.Filters);
            AutomationProperties.SetName(toggle, labels.Filters);
        }
        if (_cart is not null)
        {
            _cart.Content = Glyphed(s_bag, CartCount.ToString(CultureInfo.InvariantCulture));
            AutomationProperties.SetName(_cart, labels.CartName(CartCount));
        }
    }

    private void UpdateCompact(bool force = false)
    {
        var width = Bounds.Width;
        var compact = width > 0 ? width < CompactThreshold : _isCompact;
        if (!force && compact == _isCompact)
        {
            return;
        }
        _isCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (!compact)
        {
            SetFiltersOpen(false);
        }
    }

    private Card BuildCard(ListingProduct product)
    {
        var labels = Labels;
        var picture = new ProductListingPicture
        {
            Child = new Image { Source = product.Image, Stretch = Stretch.UniformToFill }
        };
        var pictureHost = new Panel { Children = { new ProductListingAspect { Child = picture } } };
        var badgeText = product.Badge;
        if (string.IsNullOrEmpty(badgeText) && product.CompareAtPrice is { } before && before > product.Price && before > 0)
        {
            badgeText = labels.Reduction((int)Math.Round((before - product.Price) / before * 100));
        }
        if (!string.IsNullOrEmpty(badgeText))
        {
            pictureHost.Children.Add(new Badge
            {
                Content = badgeText,
                Tone = product.CompareAtPrice is not null && string.IsNullOrEmpty(product.Badge) ? BadgeTone.Danger : BadgeTone.Neutral,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10)
            });
        }

        var name = new TextBlock { Text = product.Name, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(name, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        var texts = new StackPanel { Spacing = 4, Margin = new Thickness(0, 10, 0, 0), Children = { name } };
        string? rating = null;
        if (product.Rating > 0)
        {
            rating = product.Rating.ToString("0.0", CultureInfo.InvariantCulture);
            var star = new ArcGlyph { Data = ArcIcons.Star, Size = 13, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(star, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Warning);
            var score = Small(new TextBlock { Text = $"{rating} ({product.ReviewCount.ToString("N0", CultureInfo.InvariantCulture)})", VerticalAlignment = VerticalAlignment.Center });
            ArcTokens.Bind(score, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            texts.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { star, score } });
        }
        var price = new TextBlock { Text = Money(product.Price), FontFeatures = FontFeatureCollection.Parse("+tnum") };
        var priceRow = new WrapPanel { ItemSpacing = 8, Children = { price } };
        if (product.CompareAtPrice is { } compare && compare > product.Price)
        {
            var was = Small(new TextBlock { Text = Money(compare), TextDecorations = TextDecorations.Strikethrough, VerticalAlignment = VerticalAlignment.Center });
            ArcTokens.Bind(was, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            priceRow.Children.Add(was);
        }
        texts.Children.Add(priceRow);
        if (product.Colors.Count > 0)
        {
            var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(0, 2, 0, 0) };
            foreach (var color in product.Colors.Take(5))
            {
                dots.Children.Add(new ProductListingSwatch { Background = new ImmutableSolidColorBrush(color) });
            }
            texts.Children.Add(dots);
        }

        var open = new ProductListingCardButton { Product = product, Content = new StackPanel { Children = { pictureHost, texts } } };
        AutomationProperties.SetName(open, labels.CardName(product.Name, Money(product.Price), rating));
        open.Click += (_, _) => RaiseEvent(new ListingProductEventArgs(ProductInvokedEvent, product));
        open.PointerEntered += (_, e) =>
        {
            if (e.Pointer.Type != PointerType.Touch)
            {
                picture.Classes.Add("hover");
            }
        };
        open.PointerExited += (_, _) => picture.Classes.Remove("hover");

        var add = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            IsEnabled = product.IsInStock,
            Content = product.IsInStock ? labels.AddToCart : labels.SoldOut
        };
        AutomationProperties.SetName(add, product.IsInStock ? labels.AddName(product.Name) : labels.SoldOut);
        var card = new Card { Product = product, Open = open, Add = add, Root = new ProductListingCard { Children = { open, add } } };
        add.Click += (_, _) => AddToCart(product);
        return card;
    }

    private void ShowAdded(Card card)
    {
        card.Add.Content = Glyphed(ArcIcons.Check, Labels.Added);
        card.AddedTimer ??= new DispatcherTimer { Interval = AddedFor };
        card.AddedTimer.Stop();
        card.AddedTimer.Tick += Reset;
        card.AddedTimer.Start();

        void Reset(object? sender, EventArgs e)
        {
            card.AddedTimer!.Stop();
            card.AddedTimer.Tick -= Reset;
            card.Add.Content = Labels.AddToCart;
        }
    }

    private static Control Glyphed(Geometry glyph, string text) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        Children = { new ArcGlyph { Data = glyph, Size = 14, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = text } }
    };

    private static TextBlock Small(TextBlock text)
    {
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        return text;
    }

    private void SetText(string part, string? text)
    {
        if (_scope?.Find<TextBlock>(part) is { } block)
        {
            block.Text = text;
        }
    }
}
