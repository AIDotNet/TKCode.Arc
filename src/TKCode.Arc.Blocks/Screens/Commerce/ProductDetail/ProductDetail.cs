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
using Avalonia.Threading;
using TKCode.Arc.Primitives;

using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A product page: the pictures of the chosen color with thumbnails, the brand, name, rating and price, color swatches,
/// size buttons (sold-out sizes are struck through and low stock is called out), a quantity, Add to cart and Save, and the
/// details and the reviews behind a switch. Add to cart waits for a size when the product has sizes.
/// <code>
/// &lt;arc:ProductDetail Brand="Fernhill" ProductName="Field tote" Price="58" Rating="4.8" ReviewCount="128" /&gt;
/// </code>
/// </summary>
[TemplatePart(MainPart, typeof(Image))]
[TemplatePart(StagePart, typeof(ProductDetailStage))]
[TemplatePart(ThumbsPart, typeof(WrapPanel))]
[TemplatePart(InfoPart, typeof(StackPanel))]
[TemplatePart(AnnouncerPart, typeof(FinanceAnnouncer))]
[PseudoClasses(CompactPseudoClass)]
public class ProductDetail : TemplatedControl
{
    public const string MainPart = "PART_Main";
    public const string StagePart = "PART_Stage";
    public const string ThumbsPart = "PART_Thumbs";
    public const string InfoPart = "PART_Info";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The block is narrower than its compact threshold: the pictures sit above the information.</summary>
    public const string CompactPseudoClass = ":compact";

    // Lucide "heart" (ISC).
    private static readonly Geometry s_heart = ArcIcons.Heart;
    private static readonly TimeSpan AddedFor = TimeSpan.FromMilliseconds(1600);

    #region Properties

    /// <summary>The maker, shown small over the name.</summary>
    public static readonly StyledProperty<string?> BrandProperty =
        AvaloniaProperty.Register<ProductDetail, string?>(nameof(Brand));

    /// <summary>The product name.</summary>
    public static readonly StyledProperty<string?> ProductNameProperty =
        AvaloniaProperty.Register<ProductDetail, string?>(nameof(ProductName));

    /// <summary>The price.</summary>
    public static readonly StyledProperty<decimal> PriceProperty =
        AvaloniaProperty.Register<ProductDetail, decimal>(nameof(Price));

    /// <summary>The price before a reduction, struck through; null when not reduced.</summary>
    public static readonly StyledProperty<decimal?> CompareAtPriceProperty =
        AvaloniaProperty.Register<ProductDetail, decimal?>(nameof(CompareAtPrice));

    /// <summary>The average rating out of 5; 0 hides it.</summary>
    public static readonly StyledProperty<double> RatingProperty =
        AvaloniaProperty.Register<ProductDetail, double>(nameof(Rating));

    /// <summary>How many reviews the rating is based on (may be more than <see cref="Reviews"/> lists).</summary>
    public static readonly StyledProperty<int> ReviewCountProperty =
        AvaloniaProperty.Register<ProductDetail, int>(nameof(ReviewCount));

    /// <summary>The paragraph of the Details tab.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ProductDetail, string?>(nameof(Description));

    /// <summary>The bullet points of the Details tab.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> DetailsProperty =
        AvaloniaProperty.Register<ProductDetail, IReadOnlyList<string>>(nameof(Details), []);

    /// <summary>The index of the chosen color in <see cref="Colors"/> (two-way).</summary>
    public static readonly StyledProperty<int> SelectedColorIndexProperty =
        AvaloniaProperty.Register<ProductDetail, int>(nameof(SelectedColorIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The index of the chosen size in <see cref="Sizes"/>, or −1 before one is chosen (two-way).</summary>
    public static readonly StyledProperty<int> SelectedSizeIndexProperty =
        AvaloniaProperty.Register<ProductDetail, int>(nameof(SelectedSizeIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many to add (two-way); kept between 1 and the chosen size's stock (at most 10).</summary>
    public static readonly StyledProperty<int> QuantityProperty =
        AvaloniaProperty.Register<ProductDetail, int>(nameof(Quantity), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the product is saved to a wish list (two-way).</summary>
    public static readonly StyledProperty<bool> IsSavedProperty =
        AvaloniaProperty.Register<ProductDetail, bool>(nameof(IsSaved), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The symbol written before prices ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<ProductDetail, string>(nameof(CurrencySymbol), "$");

    /// <summary>The block's built-in text.</summary>
    public static readonly StyledProperty<ProductDetailLabels> LabelsProperty =
        AvaloniaProperty.Register<ProductDetail, ProductDetailLabels>(nameof(Labels), new ProductDetailLabels());

    /// <summary>The index of the picture shown, among the chosen color's pictures.</summary>
    public static readonly DirectProperty<ProductDetail, int> PictureIndexProperty =
        AvaloniaProperty.RegisterDirect<ProductDetail, int>(nameof(PictureIndex), o => o.PictureIndex, (o, v) => o.ShowPicture(v));

    internal static readonly StyledProperty<double> CompactThresholdProperty =
        AvaloniaProperty.Register<ProductDetail, double>(nameof(CompactThreshold), 820);

    #endregion

    /// <summary>Raised when Add to cart adds the product.</summary>
    public static readonly RoutedEvent<ProductDetailAddedEventArgs> AddedToCartEvent =
        RoutedEvent.Register<ProductDetail, ProductDetailAddedEventArgs>(nameof(AddedToCart), RoutingStrategies.Bubble);

    /// <inheritdoc cref="AddedToCartEvent"/>
    public event EventHandler<ProductDetailAddedEventArgs>? AddedToCart
    {
        add => AddHandler(AddedToCartEvent, value);
        remove => RemoveHandler(AddedToCartEvent, value);
    }

    private readonly List<ProductDetailOption> _thumbs = new();
    private readonly List<ProductDetailOption> _swatches = new();
    private readonly List<ProductDetailOption> _sizes = new();
    private INameScope? _scope;
    private Image? _main;
    private ProductDetailStage? _stage;
    private WrapPanel? _thumbHost;
    private StackPanel? _info;
    private FinanceAnnouncer? _announcer;
    private TextBlock? _colorLine;
    private TextBlock? _stockNote;
    private TextBlock? _sizeError;
    private NumberField? _quantity;
    private Button? _add;
    private Button? _save;
    private SegmentedControl? _tabs;
    private Panel? _tabHost;
    private DispatcherTimer? _addedTimer;
    private int _pictureIndex;
    private bool _syncing;
    private bool _isCompact;
    private bool _showingAdded;

    static ProductDetail()
    {
        FocusableProperty.OverrideDefaultValue<ProductDetail>(false);
    }

    public ProductDetail()
    {
        Colors.CollectionChanged += (_, _) => Rebuild();
        Sizes.CollectionChanged += (_, _) => Rebuild();
        Reviews.CollectionChanged += (_, _) => BuildTab();
    }

    /// <inheritdoc cref="BrandProperty"/>
    public string? Brand { get => GetValue(BrandProperty); set => SetValue(BrandProperty, value); }

    /// <inheritdoc cref="ProductNameProperty"/>
    public string? ProductName { get => GetValue(ProductNameProperty); set => SetValue(ProductNameProperty, value); }

    /// <inheritdoc cref="PriceProperty"/>
    public decimal Price { get => GetValue(PriceProperty); set => SetValue(PriceProperty, value); }

    /// <inheritdoc cref="CompareAtPriceProperty"/>
    public decimal? CompareAtPrice { get => GetValue(CompareAtPriceProperty); set => SetValue(CompareAtPriceProperty, value); }

    /// <inheritdoc cref="RatingProperty"/>
    public double Rating { get => GetValue(RatingProperty); set => SetValue(RatingProperty, value); }

    /// <inheritdoc cref="ReviewCountProperty"/>
    public int ReviewCount { get => GetValue(ReviewCountProperty); set => SetValue(ReviewCountProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="DetailsProperty"/>
    public IReadOnlyList<string> Details { get => GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }

    /// <inheritdoc cref="SelectedColorIndexProperty"/>
    public int SelectedColorIndex { get => GetValue(SelectedColorIndexProperty); set => SetValue(SelectedColorIndexProperty, value); }

    /// <inheritdoc cref="SelectedSizeIndexProperty"/>
    public int SelectedSizeIndex { get => GetValue(SelectedSizeIndexProperty); set => SetValue(SelectedSizeIndexProperty, value); }

    /// <inheritdoc cref="QuantityProperty"/>
    public int Quantity { get => GetValue(QuantityProperty); set => SetValue(QuantityProperty, value); }

    /// <inheritdoc cref="IsSavedProperty"/>
    public bool IsSaved { get => GetValue(IsSavedProperty); set => SetValue(IsSavedProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ProductDetailLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PictureIndexProperty"/>
    public int PictureIndex { get => _pictureIndex; set => ShowPicture(value); }

    internal double CompactThreshold { get => GetValue(CompactThresholdProperty); set => SetValue(CompactThresholdProperty, value); }

    /// <summary>The colors, each with its pictures.</summary>
    public AvaloniaList<ProductColorOption> Colors { get; } = new();

    /// <summary>The sizes with their stock; empty for a one-size product.</summary>
    public AvaloniaList<ProductSizeOption> Sizes { get; } = new();

    /// <summary>The reviews listed under the Reviews tab, newest first.</summary>
    public AvaloniaList<ProductReview> Reviews { get; } = new();

    /// <summary>The chosen color, or null when there are no colors.</summary>
    public ProductColorOption? SelectedColor => SelectedColorIndex >= 0 && SelectedColorIndex < Colors.Count ? Colors[SelectedColorIndex] : null;

    /// <summary>The chosen size, or null.</summary>
    public ProductSizeOption? SelectedSize => SelectedSizeIndex >= 0 && SelectedSizeIndex < Sizes.Count ? Sizes[SelectedSizeIndex] : null;

    /// <summary>Most that can be added now: the chosen size's stock (or 10), at most 10.</summary>
    public int MaxQuantity => Math.Max(1, Math.Min(10, SelectedSize?.Stock ?? 10));

    /// <summary>True while the block is narrower than its compact threshold.</summary>
    public bool IsCompact => _isCompact;

    /// <summary>The last text sent to the live region (tests and diagnostics).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    /// <summary>The shown error under the sizes, or null (tests).</summary>
    internal string? SizeError => _sizeError is { IsVisible: true } ? _sizeError.Text : null;

    /// <summary>
    /// Adds the chosen color, size and quantity to the cart. Returns false, shows a message and moves the focus to the sizes
    /// when the product has sizes and none is chosen.
    /// </summary>
    public bool AddToCart()
    {
        if (Sizes.Count > 0 && SelectedSize is null)
        {
            if (_sizeError is not null)
            {
                _sizeError.Text = Labels.NeedSize;
                _sizeError.IsVisible = true;
            }
            _announcer?.Announce(Labels.NeedSize);
            _sizes.FirstOrDefault(s => s.IsEnabled)?.Focus(NavigationMethod.Tab);
            return false;
        }
        if (SelectedSize is { Stock: <= 0 })
        {
            return false;
        }
        var quantity = Math.Clamp(Quantity, 1, MaxQuantity);
        _announcer?.Announce(Labels.AnnounceAdded(quantity, ProductName ?? string.Empty));
        ShowAdded();
        RaiseEvent(new ProductDetailAddedEventArgs(AddedToCartEvent, SelectedColor, SelectedSize, quantity));
        return true;
    }

    internal string Money(decimal amount)
    {
        var format = (NumberFormatInfo)Labels.Culture.NumberFormat.Clone();
        format.CurrencySymbol = CurrencySymbol;
        return amount.ToString(amount == Math.Round(amount) ? "C0" : "C2", format);
    }

    private IReadOnlyList<IImage> Pictures => SelectedColor?.Images ?? [];

    private void ShowPicture(int index)
    {
        var pictures = Pictures;
        var clamped = pictures.Count == 0 ? 0 : Math.Clamp(index, 0, pictures.Count - 1);
        SetAndRaise(PictureIndexProperty, ref _pictureIndex, clamped);
        if (_main is not null)
        {
            _main.Source = pictures.Count > 0 ? pictures[clamped] : null;
        }
        for (var i = 0; i < _thumbs.Count; i++)
        {
            _thumbs[i].Classes.Set("selected", i == clamped);
        }
        if (_stage is not null)
        {
            AutomationProperties.SetName(_stage, Labels.PictureName(clamped + 1, Math.Max(1, pictures.Count)));
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scope = e.NameScope;
        _main = _scope.Find<Image>(MainPart);
        _stage = _scope.Find<ProductDetailStage>(StagePart);
        _thumbHost = _scope.Find<WrapPanel>(ThumbsPart);
        _info = _scope.Find<StackPanel>(InfoPart);
        _announcer = _scope.Find<FinanceAnnouncer>(AnnouncerPart);
        Rebuild();
        UpdateCompact(force: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == SelectedColorIndexProperty)
        {
            BuildThumbs();
            ShowPicture(0);
            UpdateSelections();
        }
        else if (p == SelectedSizeIndexProperty)
        {
            UpdateSelections();
            if (_sizeError is not null && SelectedSize is not null)
            {
                _sizeError.IsVisible = false;
            }
            var max = MaxQuantity;
            if (Quantity > max)
            {
                SetCurrentValue(QuantityProperty, max);
            }
            SyncQuantity();
        }
        else if (p == QuantityProperty)
        {
            SyncQuantity();
        }
        else if (p == IsSavedProperty)
        {
            UpdateSave();
        }
        else if (p == BrandProperty || p == ProductNameProperty || p == PriceProperty || p == CompareAtPriceProperty || p == RatingProperty
                 || p == ReviewCountProperty || p == LabelsProperty || p == CurrencySymbolProperty || p == DescriptionProperty || p == DetailsProperty)
        {
            Rebuild();
        }
        else if (p == BoundsProperty || p == CompactThresholdProperty)
        {
            UpdateCompact();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _addedTimer?.Stop();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var delta = e.Key switch
        {
            Key.Left or Key.Up => -1,
            Key.Right or Key.Down => 1,
            _ => 0,
        };
        if (delta == 0)
        {
            return;
        }
        if (_stage is { IsFocused: true } && (e.Key is Key.Left or Key.Right))
        {
            var count = Pictures.Count;
            if (count > 1)
            {
                ShowPicture((PictureIndex + delta + count) % count);
            }
            e.Handled = true;
            return;
        }
        // Arrow keys move along a row of options (thumbnails, swatches, sizes), skipping sold-out sizes.
        foreach (var group in new[] { _thumbs, _swatches, _sizes })
        {
            var index = group.FindIndex(o => o.IsFocused);
            if (index < 0)
            {
                continue;
            }
            for (var i = index + delta; i >= 0 && i < group.Count; i += delta)
            {
                if (group[i].IsEnabled)
                {
                    group[i].Focus(NavigationMethod.Directional);
                    break;
                }
            }
            e.Handled = true;
            return;
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
    }

    private void Rebuild()
    {
        BuildThumbs();
        ShowPicture(_pictureIndex);
        BuildInfo();
    }

    private void BuildThumbs()
    {
        if (_thumbHost is null)
        {
            return;
        }
        _thumbHost.Children.Clear();
        _thumbs.Clear();
        var pictures = Pictures;
        if (pictures.Count < 2)
        {
            return;
        }
        for (var i = 0; i < pictures.Count; i++)
        {
            var index = i;
            var thumb = new ProductDetailOption
            {
                Index = i,
                Content = new Image { Source = pictures[i], Stretch = Stretch.UniformToFill }
            };
            thumb.Classes.Add("thumb");
            AutomationProperties.SetName(thumb, Labels.PictureName(i + 1, pictures.Count));
            thumb.Click += (_, _) => ShowPicture(index);
            _thumbs.Add(thumb);
            _thumbHost.Children.Add(thumb);
        }
    }

    private void BuildInfo()
    {
        if (_info is null)
        {
            return;
        }
        var labels = Labels;
        _info.Children.Clear();
        _swatches.Clear();
        _sizes.Clear();

        // Name, rating and price.
        var head = new StackPanel { Spacing = 6 };
        if (!string.IsNullOrEmpty(Brand))
        {
            head.Children.Add(Tone(new TextBlock { Text = Brand }, ArcDesignTokenKind.TextMuted));
        }
        var name = new TextBlock { Text = ProductName, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(name, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(name, TextBlock.FontSizeProperty, ArcDesignTokenKind.Type2XL);
        ArcTokens.Bind(name, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        head.Children.Add(name);
        if (Rating > 0)
        {
            var rating = Rating.ToString("0.0", CultureInfo.InvariantCulture);
            var stars = Stars(Rating, 15);
            stars.Children.Add(Tone(new TextBlock { Text = labels.RatingLine(rating, ReviewCount), Margin = new Thickness(6, 0, 0, 0) },
                ArcDesignTokenKind.TextSecondary));
            AutomationProperties.SetName(stars, labels.RatingName(rating));
            head.Children.Add(stars);
        }
        var price = new TextBlock { Text = Money(Price), FontFeatures = FontFeatureCollection.Parse("+tnum") };
        ArcTokens.Bind(price, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXL);
        var priceRow = new WrapPanel { ItemSpacing = 10, Children = { price } };
        if (CompareAtPrice is { } before && before > Price)
        {
            var was = Tone(new TextBlock { Text = Money(before), TextDecorations = TextDecorations.Strikethrough, VerticalAlignment = VerticalAlignment.Center },
                ArcDesignTokenKind.TextMuted);
            priceRow.Children.Add(was);
            priceRow.Children.Add(new Badge { Content = $"−{(int)Math.Round((before - Price) / before * 100)}%", Tone = BadgeTone.Danger, VerticalAlignment = VerticalAlignment.Center });
        }
        head.Children.Add(priceRow);
        _info.Children.Add(head);

        // Colors.
        if (Colors.Count > 0)
        {
            _colorLine = new TextBlock();
            var row = new WrapPanel { ItemSpacing = 10, LineSpacing = 10 };
            for (var i = 0; i < Colors.Count; i++)
            {
                var index = i;
                var option = Colors[i];
                var swatch = new ProductDetailOption
                {
                    Index = i,
                    Content = new Border { Background = new ImmutableSolidColorBrush(option.Swatch) }.BindCornerRadius(ArcDesignTokenKind.RadiusPill)
                };
                swatch.Classes.Add("swatch");
                AutomationProperties.SetName(swatch, option.Name);
                swatch.Click += (_, _) => SetCurrentValue(SelectedColorIndexProperty, index);
                _swatches.Add(swatch);
                row.Children.Add(swatch);
            }
            _info.Children.Add(new StackPanel { Spacing = 8, Children = { _colorLine, row } });
        }

        // Sizes.
        if (Sizes.Count > 0)
        {
            var row = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
            for (var i = 0; i < Sizes.Count; i++)
            {
                var index = i;
                var option = Sizes[i];
                var soldOut = option.Stock <= 0;
                var label = new TextBlock { Text = option.Label, TextDecorations = soldOut ? TextDecorations.Strikethrough : null };
                var button = new ProductDetailOption { Index = i, Content = label, IsEnabled = !soldOut };
                if (soldOut)
                {
                    button.Classes.Add("sold-out");
                }
                AutomationProperties.SetName(button, soldOut ? labels.SoldOutSize(option.Label) : option.Label);
                button.Click += (_, _) => SetCurrentValue(SelectedSizeIndexProperty, index);
                _sizes.Add(button);
                row.Children.Add(button);
            }
            _stockNote = Tone(new TextBlock(), ArcDesignTokenKind.Warning);
            _sizeError = Tone(new TextBlock { IsVisible = false }, ArcDesignTokenKind.Danger);
            var heading = new TextBlock { Text = labels.Size };
            ArcTokens.Bind(heading, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            _info.Children.Add(new StackPanel { Spacing = 8, Children = { heading, row, _stockNote, _sizeError } });
        }
        else
        {
            _stockNote = null;
            _sizeError = null;
        }

        // Quantity, add and save.
        _quantity = new NumberField { Label = labels.Quantity, Minimum = 1, Maximum = MaxQuantity, Step = 1, Width = 120 };
        _quantity.PropertyChanged += (_, e) =>
        {
            if (e.Property == NumberField.ValueProperty && !_syncing && _quantity is not null)
            {
                SetCurrentValue(QuantityProperty, (int)_quantity.Value);
            }
        };
        _add = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Large, VerticalAlignment = VerticalAlignment.Bottom, MinWidth = 180 };
        _add.Click += (_, _) => AddToCart();
        _save = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Large, VerticalAlignment = VerticalAlignment.Bottom };
        _save.Click += (_, _) => SetCurrentValue(IsSavedProperty, !IsSaved);
        _info.Children.Add(new WrapPanel { ItemSpacing = 10, LineSpacing = 10, Children = { _quantity, _add, _save } });

        // Details and reviews.
        _tabs = new SegmentedControl
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = new[] { new SegmentedOption("details", labels.DetailsTab), new SegmentedOption("reviews", labels.ReviewsTab(ReviewCount)) },
            SelectedValue = "details"
        };
        _tabs.SelectionChanged += (_, _) => BuildTab();
        _tabHost = new Panel();
        _info.Children.Add(new StackPanel { Spacing = 14, Margin = new Thickness(0, 6, 0, 0), Children = { _tabs, _tabHost } });

        UpdateSelections();
        SyncQuantity();
        UpdateSave();
        BuildTab();
    }

    private void UpdateSelections()
    {
        var labels = Labels;
        for (var i = 0; i < _swatches.Count; i++)
        {
            _swatches[i].Classes.Set("selected", i == SelectedColorIndex);
        }
        for (var i = 0; i < _sizes.Count; i++)
        {
            _sizes[i].Classes.Set("selected", i == SelectedSizeIndex);
        }
        if (_colorLine is not null)
        {
            _colorLine.Text = labels.ColorLine(SelectedColor?.Name ?? string.Empty);
        }
        if (_stockNote is not null)
        {
            var stock = SelectedSize?.Stock ?? 0;
            _stockNote.IsVisible = SelectedSize is not null && stock is > 0 and <= 3;
            _stockNote.Text = labels.LowStock(stock);
        }
        UpdateAddButton();
    }

    private void UpdateAddButton()
    {
        if (_add is null)
        {
            return;
        }
        var labels = Labels;
        var needsSize = Sizes.Count > 0 && SelectedSize is null;
        _add.Content = _showingAdded ? Glyphed(ArcIcons.Check, labels.Added) : needsSize ? labels.ChooseSize : labels.AddToCart;
        AutomationProperties.SetName(_add, _showingAdded ? labels.Added : needsSize ? labels.ChooseSize : labels.AddToCart);
    }

    private void SyncQuantity()
    {
        if (_quantity is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _quantity.Maximum = MaxQuantity;
            _quantity.Value = Math.Clamp(Quantity, 1, MaxQuantity);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateSave()
    {
        if (_save is null)
        {
            return;
        }
        var text = Labels.Save(IsSaved);
        var glyph = new ArcGlyph { Data = s_heart, Size = 16, VerticalAlignment = VerticalAlignment.Center };
        if (IsSaved)
        {
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
        }
        _save.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { glyph, new TextBlock { Text = text } } };
        AutomationProperties.SetName(_save, text);
    }

    private void ShowAdded()
    {
        _showingAdded = true;
        UpdateAddButton();
        _addedTimer ??= new DispatcherTimer { Interval = AddedFor };
        _addedTimer.Stop();
        _addedTimer.Tick -= OnAddedElapsed;
        _addedTimer.Tick += OnAddedElapsed;
        _addedTimer.Start();
    }

    private void OnAddedElapsed(object? sender, EventArgs e)
    {
        _addedTimer?.Stop();
        _showingAdded = false;
        UpdateAddButton();
    }

    private void BuildTab()
    {
        if (_tabHost is null || _tabs is null)
        {
            return;
        }
        var labels = Labels;
        _tabHost.Children.Clear();
        if (_tabs.SelectedValue as string == "reviews")
        {
            _tabHost.Children.Add(BuildReviews(labels));
            return;
        }
        var details = new StackPanel { Spacing = 8 };
        if (!string.IsNullOrEmpty(Description))
        {
            details.Children.Add(Tone(new TextBlock { Text = Description, TextWrapping = TextWrapping.Wrap }, ArcDesignTokenKind.TextSecondary));
        }
        foreach (var line in Details)
        {
            var dot = new Border { Width = 5, Height = 5, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2, 7, 10, 0) }
                .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(dot, Border.BackgroundProperty, ArcDesignTokenKind.TextMuted);
            var row = new DockPanel();
            DockPanel.SetDock(dot, Dock.Left);
            row.Children.Add(dot);
            row.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap });
            details.Children.Add(row);
        }
        _tabHost.Children.Add(details);
    }

    private Control BuildReviews(ProductDetailLabels labels)
    {
        var panel = new StackPanel { Spacing = 16 };
        if (Reviews.Count == 0)
        {
            panel.Children.Add(Tone(new TextBlock { Text = labels.NoReviews }, ArcDesignTokenKind.TextMuted));
            return panel;
        }
        // Breakdown of the listed reviews by stars.
        var breakdown = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, RowSpacing = 6, MaxWidth = 360, HorizontalAlignment = HorizontalAlignment.Left };
        for (var stars = 5; stars >= 1; stars--)
        {
            var row = 5 - stars;
            breakdown.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var count = Reviews.Count(r => r.Rating == stars);
            var starGlyph = new ArcGlyph { Data = ArcIcons.Star, Size = 12, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(starGlyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Warning);
            var label = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 3,
                Children = { Tone(new TextBlock { Text = stars.ToString(CultureInfo.InvariantCulture) }, ArcDesignTokenKind.TextSecondary), starGlyph }
            };
            var bar = new ProductDetailBar { Fraction = (double)count / Reviews.Count, VerticalAlignment = VerticalAlignment.Center };
            var number = Tone(new TextBlock { Text = count.ToString(CultureInfo.InvariantCulture) }, ArcDesignTokenKind.TextMuted);
            Grid.SetRow(label, row);
            Grid.SetRow(bar, row);
            Grid.SetColumn(bar, 1);
            Grid.SetRow(number, row);
            Grid.SetColumn(number, 2);
            breakdown.Children.Add(label);
            breakdown.Children.Add(bar);
            breakdown.Children.Add(number);
        }
        panel.Children.Add(breakdown);
        foreach (var review in Reviews)
        {
            var title = new TextBlock { Text = review.Title, TextWrapping = TextWrapping.Wrap, IsVisible = !string.IsNullOrEmpty(review.Title) };
            ArcTokens.Bind(title, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            var meta = Tone(new TextBlock { Text = $"{review.Author} · {labels.Date(review.Date)}" }, ArcDesignTokenKind.TextMuted);
            ArcTokens.Bind(meta, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
            var stars = Stars(review.Rating, 13);
            AutomationProperties.SetName(stars, labels.RatingName(review.Rating.ToString(CultureInfo.InvariantCulture)));
            var item = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    stars, title,
                    Tone(new TextBlock { Text = review.Body, TextWrapping = TextWrapping.Wrap, IsVisible = !string.IsNullOrEmpty(review.Body) }, ArcDesignTokenKind.TextSecondary),
                    meta
                }
            };
            panel.Children.Add(item);
        }
        return panel;
    }

    /// <summary>Five stars, the first <paramref name="rating"/> (rounded to the nearest whole star) in the warning color.</summary>
    private static StackPanel Stars(double rating, double size)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var full = (int)Math.Round(rating);
        for (var i = 0; i < 5; i++)
        {
            var star = new ArcGlyph { Data = ArcIcons.Star, Size = size, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(star, ArcGlyph.ForegroundProperty, i < full ? ArcDesignTokenKind.Warning : ArcDesignTokenKind.BorderStrong);
            row.Children.Add(star);
        }
        return row;
    }

    private static Control Glyphed(Geometry glyph, string text) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        Children = { new ArcGlyph { Data = glyph, Size = 16, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = text } }
    };

    private static TextBlock Tone(TextBlock text, ArcDesignTokenKind kind)
    {
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, kind);
        return text;
    }
}
