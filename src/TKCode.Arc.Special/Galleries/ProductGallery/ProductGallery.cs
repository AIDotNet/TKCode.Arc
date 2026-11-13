using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ProductGallery"/> puts its thumbnails.</summary>
public enum ProductGalleryThumbnailPlacement
{
    /// <summary>A row under the picture.</summary>
    Bottom,

    /// <summary>A column on the left of the picture.</summary>
    Left
}

/// <summary>
/// The pictures of a product page: one large picture with a strip of thumbnails. Clicking a thumbnail, the arrow buttons
/// or the arrow keys slide to another picture. With a mouse or pen over the picture it zooms in toward the pointer, so
/// the texture of a fabric or the print on a label can be read without opening anything; on a touch screen the picture
/// is swiped instead, following the finger and keeping its speed when let go.
/// </summary>
/// <remarks>
/// Pictures come from <see cref="Items"/> (written inline) or <see cref="ItemsSource"/>, whose elements may be
/// <see cref="ProductGalleryImage"/>s, <see cref="ImageSource"/>s or addresses. <see cref="SelectedIndex"/> is the picture
/// on the stage and can be bound both ways. Under reduced motion pictures cross-fade instead of sliding and the zoom does
/// not animate; with motion off they change at once.
/// </remarks>
[TemplatePart(StagePart, typeof(ProductGalleryStage))]
[TemplatePart(StageHostPart, typeof(Panel))]
[TemplatePart(ThumbsPart, typeof(StackPanel))]
[TemplatePart(ThumbScrollerPart, typeof(ScrollViewer))]
[TemplatePart(PreviousPart, typeof(Button))]
[TemplatePart(NextPart, typeof(Button))]
[TemplatePart(CounterPart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(ThumbsLeftPseudoClass, ZoomedPseudoClass, SinglePseudoClass)]
public class ProductGallery : TemplatedControl
{
    internal const string StagePart = "PART_Stage";
    internal const string StageHostPart = "PART_StageHost";
    internal const string ThumbsPart = "PART_Thumbs";
    internal const string ThumbScrollerPart = "PART_ThumbScroller";
    internal const string PreviousPart = "PART_Previous";
    internal const string NextPart = "PART_Next";
    internal const string CounterPart = "PART_Counter";
    internal const string StatusPart = "PART_Status";
    internal const string ThumbsLeftPseudoClass = ":thumbs-left";
    internal const string ZoomedPseudoClass = ":zoomed";
    internal const string SinglePseudoClass = ":single";

    /// <summary>The pictures written inline, used while <see cref="ItemsSource"/> is null.</summary>
    public static readonly DirectProperty<ProductGallery, AvaloniaList<ProductGalleryImage>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<ProductGallery, AvaloniaList<ProductGalleryImage>>(nameof(Items), o => o.Items);

    /// <summary>
    /// The pictures to show: <see cref="ProductGalleryImage"/>s, <see cref="ImageSource"/>s or addresses. Wins over
    /// <see cref="Items"/>; a collection that reports changes is followed.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ProductGallery, IEnumerable?>(nameof(ItemsSource));

    /// <summary>The index of the picture on the stage; -1 when there are none. Two-way.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<ProductGallery, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Raised when <see cref="SelectedIndex"/> changes, whether a picture was chosen (a thumbnail, a swipe, the arrow keys)
    /// or the index was set in code or through a binding.
    /// </summary>
    public static readonly RoutedEvent<PhotosSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<ProductGallery, PhotosSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>How much a mouse or pen over the picture enlarges it (default 2); 1 turns the hover zoom off.</summary>
    public static readonly StyledProperty<double> ZoomScaleProperty =
        AvaloniaProperty.Register<ProductGallery, double>(nameof(ZoomScale), 2,
            coerce: static (_, value) => double.IsFinite(value) ? Math.Clamp(value, 1, 6) : 2);

    /// <summary>Whether a mouse or pen over the picture zooms into it (default true).</summary>
    public static readonly StyledProperty<bool> IsHoverZoomEnabledProperty =
        AvaloniaProperty.Register<ProductGallery, bool>(nameof(IsHoverZoomEnabled), true);

    /// <summary>Where the thumbnails go: under the picture (default) or down its left side.</summary>
    public static readonly StyledProperty<ProductGalleryThumbnailPlacement> ThumbnailPlacementProperty =
        AvaloniaProperty.Register<ProductGallery, ProductGalleryThumbnailPlacement>(nameof(ThumbnailPlacement));

    /// <summary>How a picture fills the stage (default Uniform: the whole product, on the stage's muted ground).</summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<ProductGallery, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>The stage's width divided by its height (default 1, a square).</summary>
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<ProductGallery, double>(nameof(AspectRatio), 1,
            coerce: static (_, value) => double.IsFinite(value) && value > 0 ? value : 1);

    /// <summary>The gallery's accessible name, such as the product's name (default "Product images").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ProductGallery, string>(nameof(Label), "Product images");

    /// <summary>How a picture is announced; {0} is its position and {1} the number of pictures (default "Image {0} of {1}").</summary>
    public static readonly StyledProperty<string> ImageNameFormatProperty =
        AvaloniaProperty.Register<ProductGallery, string>(nameof(ImageNameFormat), "Image {0} of {1}");

    /// <summary>Accessible name of the previous button (default "Previous image").</summary>
    public static readonly StyledProperty<string> PreviousLabelProperty =
        AvaloniaProperty.Register<ProductGallery, string>(nameof(PreviousLabel), "Previous image");

    /// <summary>Accessible name of the next button (default "Next image").</summary>
    public static readonly StyledProperty<string> NextLabelProperty =
        AvaloniaProperty.Register<ProductGallery, string>(nameof(NextLabel), "Next image");

    /// <summary>The picture on the stage, or null.</summary>
    public static readonly DirectProperty<ProductGallery, ProductGalleryImage?> SelectedImageProperty =
        AvaloniaProperty.RegisterDirect<ProductGallery, ProductGalleryImage?>(nameof(SelectedImage), o => o.SelectedImage);

    /// <summary>True while the picture is zoomed in under the pointer.</summary>
    public static readonly DirectProperty<ProductGallery, bool> IsZoomedProperty =
        AvaloniaProperty.RegisterDirect<ProductGallery, bool>(nameof(IsZoomed), o => o.IsZoomed);

    private readonly PhotosItemFeed<ProductGalleryImage> _feed;
    private readonly List<ProductGalleryThumb> _thumbs = new();
    private ProductGalleryStage? _stage;
    private Panel? _stageHost;
    private StackPanel? _thumbPanel;
    private Button? _previous;
    private Button? _next;
    private TextBlock? _counter;
    private TextBlock? _status;
    private LazyFocusRing? _focusRing;
    private ProductGalleryImage? _selectedImage;
    private bool _isZoomed;
    private bool _syncing;
    private (double Offset, double Velocity)? _swipe;

    static ProductGallery()
    {
        FocusableProperty.OverrideDefaultValue<ProductGallery>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ProductGallery>(false);
    }

    public ProductGallery()
    {
        _feed = new PhotosItemFeed<ProductGalleryImage>(Convert, OnItemsChanged);
    }

    /// <inheritdoc cref="ItemsProperty"/>
    [Content]
    public AvaloniaList<ProductGalleryImage> Items => _feed.Items;

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<PhotosSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="ZoomScaleProperty"/>
    public double ZoomScale
    {
        get => GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    /// <inheritdoc cref="IsHoverZoomEnabledProperty"/>
    public bool IsHoverZoomEnabled
    {
        get => GetValue(IsHoverZoomEnabledProperty);
        set => SetValue(IsHoverZoomEnabledProperty, value);
    }

    /// <inheritdoc cref="ThumbnailPlacementProperty"/>
    public ProductGalleryThumbnailPlacement ThumbnailPlacement
    {
        get => GetValue(ThumbnailPlacementProperty);
        set => SetValue(ThumbnailPlacementProperty, value);
    }

    /// <inheritdoc cref="StretchProperty"/>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <inheritdoc cref="AspectRatioProperty"/>
    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="ImageNameFormatProperty"/>
    public string ImageNameFormat
    {
        get => GetValue(ImageNameFormatProperty);
        set => SetValue(ImageNameFormatProperty, value);
    }

    /// <inheritdoc cref="PreviousLabelProperty"/>
    public string PreviousLabel
    {
        get => GetValue(PreviousLabelProperty);
        set => SetValue(PreviousLabelProperty, value);
    }

    /// <inheritdoc cref="NextLabelProperty"/>
    public string NextLabel
    {
        get => GetValue(NextLabelProperty);
        set => SetValue(NextLabelProperty, value);
    }

    /// <inheritdoc cref="SelectedImageProperty"/>
    public ProductGalleryImage? SelectedImage
    {
        get => _selectedImage;
        private set => SetAndRaise(SelectedImageProperty, ref _selectedImage, value);
    }

    /// <inheritdoc cref="IsZoomedProperty"/>
    public bool IsZoomed
    {
        get => _isZoomed;
        private set => SetAndRaise(IsZoomedProperty, ref _isZoomed, value);
    }

    /// <summary>The number of pictures.</summary>
    public int Count => _feed.Count;

    internal ProductGalleryStage? Stage => _stage;

    internal IReadOnlyList<ProductGalleryThumb> Thumbs => _thumbs;

    /// <summary>Slides to the next picture; returns false at the last one.</summary>
    public bool SelectNext() => Select(SelectedIndex + 1);

    /// <summary>Slides to the previous picture; returns false at the first one.</summary>
    public bool SelectPrevious() => Select(SelectedIndex - 1);

    private bool Select(int index)
    {
        if (index < 0 || index >= Count || index == SelectedIndex)
        {
            return false;
        }
        SetCurrentValue(SelectedIndexProperty, index);
        return true;
    }

    internal ImageSource? SourceAt(int index) => index >= 0 && index < Count ? _feed[index].Source : null;

    private static ProductGalleryImage? Convert(object? element) => element switch
    {
        ProductGalleryImage image => image,
        _ when PhotosItemFeed<ProductGalleryImage>.SourceOf(element) is { } source => new ProductGalleryImage { Source = source },
        _ => null
    };

    // Items and selection ------------------------------------------------------------------------------------------------

    private void OnItemsChanged()
    {
        var count = Count;
        var index = SelectedIndex;
        var clamped = count == 0 ? -1 : Math.Clamp(index < 0 ? 0 : index, 0, count - 1);
        if (clamped != index)
        {
            _syncing = true;
            SetCurrentValue(SelectedIndexProperty, clamped);
            _syncing = false;
        }
        RebuildThumbs();
        _stage?.ShowAtRest(clamped);
        UpdateState();
        RaiseSelectionChanged(index);
    }

    internal void SelectFromSwipe(int index, double offset, double velocity)
    {
        _swipe = (offset, velocity);
        SetCurrentValue(SelectedIndexProperty, index);
        _swipe = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _feed.ItemsSource = change.GetNewValue<IEnumerable?>();
        }
        else if (change.Property == SelectedIndexProperty)
        {
            OnSelectedIndexChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (change.Property == ThumbnailPlacementProperty)
        {
            PseudoClasses.Set(ThumbsLeftPseudoClass, change.GetNewValue<ProductGalleryThumbnailPlacement>() == ProductGalleryThumbnailPlacement.Left);
        }
        else if (change.Property == StretchProperty)
        {
            _stage?.SetStretch(change.GetNewValue<Stretch>());
        }
        else if (change.Property == AspectRatioProperty)
        {
            _stage?.InvalidateMeasure();
        }
        else if (change.Property == ZoomScaleProperty)
        {
            _stage?.RefreshZoom();
        }
        else if (change.Property == IsHoverZoomEnabledProperty && !change.GetNewValue<bool>())
        {
            _stage?.ZoomOut();
        }
        else if (change.Property == LabelProperty || change.Property == ImageNameFormatProperty)
        {
            UpdateState();
        }
        else if (change.Property == PreviousLabelProperty && _previous is not null)
        {
            AutomationProperties.SetName(_previous, change.GetNewValue<string>());
        }
        else if (change.Property == NextLabelProperty && _next is not null)
        {
            AutomationProperties.SetName(_next, change.GetNewValue<string>());
        }
    }

    private void OnSelectedIndexChanged(int previous, int index)
    {
        if (_syncing)
        {
            return;
        }
        var count = Count;
        if (count > 0 && (index < 0 || index >= count))
        {
            // Out of range: keep the nearest picture.
            _syncing = true;
            SetCurrentValue(SelectedIndexProperty, Math.Clamp(index, 0, count - 1));
            _syncing = false;
            index = SelectedIndex;
        }
        if (_stage is not null)
        {
            if (_swipe is { } swipe)
            {
                _stage.Show(index, previous, swipe.Offset, swipe.Velocity);
            }
            else if (previous >= 0 && previous < count)
            {
                _stage.Show(index, previous);
            }
            else
            {
                _stage.ShowAtRest(index);
            }
        }
        UpdateState();
        RaiseSelectionChanged(previous);
    }

    private void RaiseSelectionChanged(int oldIndex)
    {
        var newIndex = SelectedIndex;
        if (newIndex != oldIndex)
        {
            RaiseEvent(new PhotosSelectionChangedEventArgs(SelectionChangedEvent, oldIndex, newIndex));
        }
    }

    private void UpdateState()
    {
        var count = Count;
        var index = SelectedIndex;
        SelectedImage = index >= 0 && index < count ? _feed[index] : null;
        PseudoClasses.Set(SinglePseudoClass, count <= 1);
        for (var i = 0; i < _thumbs.Count; i++)
        {
            var thumb = _thumbs[i];
            var selected = i == index;
            thumb.IsSelected = selected;
            // One tab stop for the strip: the selected thumbnail (arrow keys move within the strip).
            thumb.IsTabStop = selected;
            AutomationProperties.SetName(thumb, NameOf(i));
            AutomationProperties.SetHelpText(thumb, selected ? "Selected" : null);
        }
        if (_previous is not null)
        {
            _previous.IsEnabled = index > 0;
        }
        if (_next is not null)
        {
            _next.IsEnabled = index >= 0 && index < count - 1;
        }
        var position = index >= 0 ? string.Format(CultureInfo.CurrentCulture, "{0} / {1}", index + 1, count) : string.Empty;
        if (_counter is not null)
        {
            _counter.Text = position;
        }
        var spoken = index >= 0 ? NameOf(index) : string.Empty;
        if (_status is not null)
        {
            _status.Text = spoken;
        }
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetHelpText(this, spoken);
        if (index >= 0 && index < _thumbs.Count && IsAttached)
        {
            _thumbs[index].BringIntoView();
        }
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    /// <summary>"Image 2 of 5: Side view".</summary>
    internal string NameOf(int index)
    {
        string name;
        try
        {
            name = string.Format(CultureInfo.CurrentCulture, ImageNameFormat, index + 1, Count);
        }
        catch (FormatException)
        {
            name = string.Format(CultureInfo.CurrentCulture, "Image {0} of {1}", index + 1, Count);
        }
        return _feed[index].Description is { Length: > 0 } description ? name + ": " + description : name;
    }

    private void RebuildThumbs()
    {
        if (_thumbPanel is null)
        {
            return;
        }
        var count = Count;
        // Thumbnails are reused: only the difference in number is built or dropped.
        while (_thumbs.Count > count)
        {
            var last = _thumbs[^1];
            _thumbs.RemoveAt(_thumbs.Count - 1);
            _thumbPanel.Children.Remove(last);
        }
        while (_thumbs.Count < count)
        {
            var thumb = new ProductGalleryThumb { Index = _thumbs.Count };
            thumb.Click += OnThumbClick;
            _thumbs.Add(thumb);
            _thumbPanel.Children.Add(thumb);
        }
        for (var i = 0; i < count; i++)
        {
            var image = _feed[i];
            _thumbs[i].Source = image.Thumbnail ?? image.Source;
        }
    }

    private void OnThumbClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ProductGalleryThumb thumb)
        {
            Select(thumb.Index);
        }
    }

    internal void OnZoomChanged(bool zoomed)
    {
        IsZoomed = zoomed;
        PseudoClasses.Set(ZoomedPseudoClass, zoomed);
    }

    // Template -----------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        if (_stage is not null)
        {
            _stage.Owner = null;
        }
        if (_thumbPanel is not null)
        {
            _thumbPanel.Children.Clear();
            _thumbs.Clear();
        }
        _stage = e.NameScope.Find<ProductGalleryStage>(StagePart);
        _stageHost = e.NameScope.Find<Panel>(StageHostPart);
        _thumbPanel = e.NameScope.Find<StackPanel>(ThumbsPart);
        _previous = e.NameScope.Find<Button>(PreviousPart);
        _next = e.NameScope.Find<Button>(NextPart);
        _counter = e.NameScope.Find<TextBlock>(CounterPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
            AutomationProperties.SetName(_previous, PreviousLabel);
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
            AutomationProperties.SetName(_next, NextLabel);
        }
        if (_stage is not null)
        {
            _stage.Owner = this;
            _stage.SetStretch(Stretch);
        }
        _focusRing ??= new LazyFocusRing(this, ProductGalleryTokens.Key(ProductGalleryTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep28));
        _focusRing.Attach(_stageHost);
        RebuildThumbs();
        _stage?.ShowAtRest(SelectedIndex);
        UpdateState();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _feed.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _feed.Detach();
        _stage?.ZoomOut();
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => SelectPrevious();

    private void OnNextClick(object? sender, RoutedEventArgs e) => SelectNext();

    // Keyboard -----------------------------------------------------------------------------------------------------------

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || Count == 0)
        {
            return;
        }
        var inStrip = e.Source is ProductGalleryThumb;
        var vertical = ThumbnailPlacement == ProductGalleryThumbnailPlacement.Left;
        var back = inStrip && vertical ? Key.Up : Key.Left;
        var forward = inStrip && vertical ? Key.Down : Key.Right;
        int target;
        if (e.Key == back)
        {
            target = SelectedIndex - 1;
        }
        else if (e.Key == forward)
        {
            target = SelectedIndex + 1;
        }
        else if (e.Key == Key.Home)
        {
            target = 0;
        }
        else if (e.Key == Key.End)
        {
            target = Count - 1;
        }
        else
        {
            return;
        }
        e.Handled = true;
        target = Math.Clamp(target, 0, Count - 1);
        Select(target);
        if (inStrip && target < _thumbs.Count)
        {
            _thumbs[target].Focus(NavigationMethod.Directional);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ProductGalleryAutomationPeer(this);

    private sealed class ProductGalleryAutomationPeer(ProductGallery owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => owner.Label;
    }
}
