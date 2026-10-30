using Avalonia.Controls.Documents;
using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(TopPart, typeof(Border))]
[TemplatePart(AddPart, typeof(DeveloperMorphPanel))]
[TemplatePart(EndpointsHostPart, typeof(Panel))]
[TemplatePart(DetailHostPart, typeof(Border))]
[TemplatePart(SplitPart, typeof(ArcContentSwap))]
[TemplatePart(AnnouncerPart, typeof(DeveloperAnnouncer))]
[PseudoClasses(StackedPseudoClass, NarrowPseudoClass, CompactPseudoClass, AddingPseudoClass, RevealingPseudoClass)]
public partial class Webhooks
{
    public const string TitlePart = "PART_Title";
    public const string DescriptionPart = "PART_Description";
    public const string TopPart = "PART_Top";
    public const string AddPart = "PART_Add";
    public const string EndpointsHostPart = "PART_EndpointsHost";
    public const string DetailHostPart = "PART_DetailHost";
    public const string SplitPart = "PART_Split";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set at or below 880 wide: the deliveries and the inspector stack.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set at or below 760 wide: endpoint rows keep the name, the rate and the badge.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set at or below 540 wide: tighter paddings, no toolbar note.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the "Add endpoint" panel is open.</summary>
    public const string AddingPseudoClass = ":adding";

    /// <summary>Set while the new endpoint's signing secret is shown.</summary>
    public const string RevealingPseudoClass = ":revealing";

    #region Token-driven internals

    internal static readonly StyledProperty<double> InlineGutterProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(InlineGutter), 20);
    internal static readonly StyledProperty<double> CompactInlineGutterProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(CompactInlineGutter), 16);
    internal static readonly StyledProperty<double> RateColumnWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(RateColumnWidth), 72);
    internal static readonly StyledProperty<double> StateColumnWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(StateColumnWidth), 104);
    internal static readonly StyledProperty<double> RowColumnGapProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(RowColumnGap), 20);
    internal static readonly StyledProperty<double> ChartHeightProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(ChartHeight), 56);
    internal static readonly StyledProperty<double> BarGapProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(BarGap), 5);
    internal static readonly StyledProperty<double> DeliveryRowHeightProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(DeliveryRowHeight), 40);
    internal static readonly StyledProperty<double> ActionColumnWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(ActionColumnWidth), 100);
    internal static readonly StyledProperty<double> StackedPanelHeightProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(StackedPanelHeight), 256);
    internal static readonly StyledProperty<double> MinPanelHeightProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(MinPanelHeight), 160);
    internal static readonly StyledProperty<double> StackedWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(StackedWidth), 880);
    internal static readonly StyledProperty<double> EventsTwoColumnWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(EventsTwoColumnWidth), 800);
    internal static readonly StyledProperty<double> NarrowWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(NarrowWidth), 760);
    internal static readonly StyledProperty<double> FormSingleColumnWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(FormSingleColumnWidth), 560);
    internal static readonly StyledProperty<double> CompactWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(CompactWidth), 540);
    internal static readonly StyledProperty<double> DeliveryTwoLineWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(DeliveryTwoLineWidth), 460);
    internal static readonly StyledProperty<double> HeadersStackedWidthProperty = AvaloniaProperty.Register<Webhooks, double>(nameof(HeadersStackedWidth), 420);

    internal static readonly StyledProperty<int> MaxChipsProperty =
        AvaloniaProperty.Register<Webhooks, int>(nameof(MaxChips), 3);

    internal static readonly StyledProperty<Thickness> RowPlateInsetProperty =
        AvaloniaProperty.Register<Webhooks, Thickness>(nameof(RowPlateInset), new Thickness(8, 2));

    /// <summary>The row plate fill: SurfaceMuted 80% into Surface (mixed at run time).</summary>
    internal static readonly StyledProperty<IBrush?> PlateBrushProperty =
        AvaloniaProperty.Register<Webhooks, IBrush?>(nameof(PlateBrush));

    internal double InlineGutter { get => GetValue(InlineGutterProperty); set => SetValue(InlineGutterProperty, value); }
    internal double CompactInlineGutter { get => GetValue(CompactInlineGutterProperty); set => SetValue(CompactInlineGutterProperty, value); }
    internal double RateColumnWidth { get => GetValue(RateColumnWidthProperty); set => SetValue(RateColumnWidthProperty, value); }
    internal double StateColumnWidth { get => GetValue(StateColumnWidthProperty); set => SetValue(StateColumnWidthProperty, value); }
    internal double RowColumnGap { get => GetValue(RowColumnGapProperty); set => SetValue(RowColumnGapProperty, value); }
    internal double ChartHeight { get => GetValue(ChartHeightProperty); set => SetValue(ChartHeightProperty, value); }
    internal double BarGap { get => GetValue(BarGapProperty); set => SetValue(BarGapProperty, value); }
    internal double DeliveryRowHeight { get => GetValue(DeliveryRowHeightProperty); set => SetValue(DeliveryRowHeightProperty, value); }
    internal double ActionColumnWidth { get => GetValue(ActionColumnWidthProperty); set => SetValue(ActionColumnWidthProperty, value); }
    internal double StackedPanelHeight { get => GetValue(StackedPanelHeightProperty); set => SetValue(StackedPanelHeightProperty, value); }
    internal double MinPanelHeight { get => GetValue(MinPanelHeightProperty); set => SetValue(MinPanelHeightProperty, value); }
    internal double StackedWidth { get => GetValue(StackedWidthProperty); set => SetValue(StackedWidthProperty, value); }
    internal double EventsTwoColumnWidth { get => GetValue(EventsTwoColumnWidthProperty); set => SetValue(EventsTwoColumnWidthProperty, value); }
    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal double FormSingleColumnWidth { get => GetValue(FormSingleColumnWidthProperty); set => SetValue(FormSingleColumnWidthProperty, value); }
    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal double DeliveryTwoLineWidth { get => GetValue(DeliveryTwoLineWidthProperty); set => SetValue(DeliveryTwoLineWidthProperty, value); }
    internal double HeadersStackedWidth { get => GetValue(HeadersStackedWidthProperty); set => SetValue(HeadersStackedWidthProperty, value); }
    internal int MaxChips { get => GetValue(MaxChipsProperty); set => SetValue(MaxChipsProperty, value); }
    internal Thickness RowPlateInset { get => GetValue(RowPlateInsetProperty); set => SetValue(RowPlateInsetProperty, value); }
    internal IBrush? PlateBrush { get => GetValue(PlateBrushProperty); set => SetValue(PlateBrushProperty, value); }

    /// <summary>The token-driven geometry (the parts read it from the block).</summary>
    internal Webhooks Metrics => this;

    #endregion

    #region Seams (tests)

    /// <summary>Waits for the simulated server and the selection follow-ups (tests replace it).</summary>
    internal Func<TimeSpan, CancellationToken, Task> DelayAsync { get; set; } = static (delay, token) => Task.Delay(delay, token);

    /// <summary>The clock new deliveries and endpoints are dated with (tests give their own).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    #endregion

    private TextBlock? _titleText;
    private TextBlock? _descriptionText;
    private Border? _top;
    private DeveloperMorphPanel? _add;
    private Panel? _endpointsHost;
    private Border? _detailHost;
    private ArcContentSwap? _splitSwap;
    private DeveloperAnnouncer? _announcer;

    private GlidingIndicator _endpointHighlight = null!;
    private DeveloperListPanel _endpointList = null!;
    private WebhookDetail _detail = null!;
    private WebhookSlotText _countText = null!;
    private TextBlock _countWord = null!;
    private ArcContentSwap _note = null!;
    private StackPanel _noteHost = null!;
    private StackPanel _leading = null!;
    private WebhookAddForm? _form;
    private WebhookReveal? _reveal;
    private WebhookSplitView? _split;

    private IEnumerable? _effective;
    private readonly HashSet<WebhookEndpoint> _watchedEndpoints = new();
    private WebhookChangeSource? _endpointSource;
    private WebhookChangeSource? _deliverySource;
    private DateTime _endpointGlideUntil;
    private bool _stacked;
    private bool _narrow;
    private bool _compact;
    private bool _layoutKnown;
    private bool _detailQueued;

    /// <summary>Whether the endpoint rows are in their narrow form.</summary>
    internal bool IsNarrow => _narrow;

    /// <summary>Whether the deliveries and the inspector stack.</summary>
    internal bool IsStacked => _stacked;

    /// <summary>The side padding now (20, or 16 when compact).</summary>
    internal double Gutter => _compact ? CompactInlineGutter : InlineGutter;

    /// <summary>The endpoint list.</summary>
    internal DeveloperListPanel EndpointList => _endpointList;

    /// <summary>The selected endpoint's plate.</summary>
    internal GlidingIndicator EndpointHighlight => _endpointHighlight;

    /// <summary>The detail section.</summary>
    internal WebhookDetail Detail => _detail;

    /// <summary>The lower half of the selected endpoint.</summary>
    internal WebhookSplitView? Split => _split;

    /// <summary>The "Add endpoint" morph panel.</summary>
    internal DeveloperMorphPanel? AddPanel => _add;

    /// <summary>The form (created on first use).</summary>
    internal WebhookAddForm Form => _form ??= new WebhookAddForm(this);

    /// <summary>The one-time secret view (created on first use).</summary>
    internal WebhookReveal Reveal => _reveal ??= new WebhookReveal(this);

    /// <summary>The announcer.</summary>
    internal DeveloperAnnouncer? Announcer => _announcer;

    /// <summary>The toolbar's endpoint count.</summary>
    internal WebhookSlotText CountText => _countText;

    /// <summary>The toolbar's note ("1 failing" / "all healthy").</summary>
    internal ArcContentSwap Note => _note;

    /// <summary>The endpoints shown, in order.</summary>
    internal IReadOnlyList<WebhookEndpoint> EffectiveEndpoints => (_effective ?? Endpoints).OfType<WebhookEndpoint>().ToList();

    /// <summary>The row of an endpoint, if it is shown.</summary>
    internal WebhookEndpointRow? RowFor(WebhookEndpoint endpoint) => _endpointList.ContainerFor(endpoint) as WebhookEndpointRow;

    private void InitializeView()
    {
        _endpointHighlight = new GlidingIndicator { IsHitTestVisible = false }.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        ArcTokens.Bind(_endpointHighlight, GlidingIndicator.FillProperty, ArcDesignTokenKind.AccentSubtle);
        _endpointList = new DeveloperListPanel
        {
            Motion = DeveloperListMotion.Insert,
            FlashCornerRadius = new CornerRadius(10),
            VerticalAlignment = VerticalAlignment.Top,
            ItemTemplate = new FuncDataTemplate<WebhookEndpoint>((endpoint, _) => CreateEndpointRow(endpoint)),
        };
        _endpointList.LayoutUpdated += (_, _) => PlaceEndpointHighlight(DateTime.UtcNow < _endpointGlideUntil);
        _endpointList.ItemPrepared += (_, _) => Dispatcher.UIThread.Post(UpdateSeparators, DispatcherPriority.Render);
        _endpointList.ItemExited += (_, _) => UpdateSeparators();
        _detail = new WebhookDetail(this);

        _countText = new WebhookSlotText { FontWeight = FontWeight.Medium, LineHeight = 19.6, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_countText, WebhookSlotText.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_countText, WebhookSlotText.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_countText, WebhookSlotText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _countWord = WebhookUi.Text(null, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 19.6);
        _countWord.VerticalAlignment = VerticalAlignment.Center;
        _note = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_note, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_note, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _noteHost = WebhookUi.Row(8, WebhookUi.Dot(), _note);
        _noteHost.Margin = new Thickness(4, 0, 0, 0);
        _leading = WebhookUi.Row(4, _countText, _countWord, _noteHost);
        _leading.VerticalAlignment = VerticalAlignment.Center;

        Endpoints.CollectionChanged += (_, _) =>
        {
            if (EndpointsSource is null)
            {
                OnEndpointsChanged();
            }
        };
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
    }

    private Control CreateEndpointRow(WebhookEndpoint endpoint)
    {
        var row = new WebhookEndpointRow(this, endpoint)
        {
            PlateInset = RowPlateInset,
            IsSelected = ReferenceEquals(endpoint, SelectedEndpoint),
        };
        row[!WebhookRowFrame.HoverBrushProperty] = this[!PlateBrushProperty];
        AutomationProperties.SetName(row, endpoint.DisplayName);
        return row;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_add is not null)
        {
            _add.Closed -= OnAddClosed;
            _add.Opened -= OnAddOpened;
            _add.Leading = null;
            _add.Content = null;
        }
        _endpointsHost?.Children.Clear();
        if (_detailHost is not null)
        {
            _detailHost.Child = null;
        }
        _titleText = e.NameScope.Find<TextBlock>(TitlePart);
        _descriptionText = e.NameScope.Find<TextBlock>(DescriptionPart);
        _top = e.NameScope.Find<Border>(TopPart);
        _add = e.NameScope.Find<DeveloperMorphPanel>(AddPart);
        _endpointsHost = e.NameScope.Find<Panel>(EndpointsHostPart);
        _detailHost = e.NameScope.Find<Border>(DetailHostPart);
        _splitSwap = e.NameScope.Find<ArcContentSwap>(SplitPart);
        _announcer = e.NameScope.Find<DeveloperAnnouncer>(AnnouncerPart);
        if (_endpointsHost is not null)
        {
            _endpointsHost.Children.Add(_endpointHighlight);
            _endpointsHost.Children.Add(_endpointList);
        }
        if (_detailHost is not null)
        {
            _detailHost.Child = _detail;
        }
        if (_add is not null)
        {
            _add.Leading = _leading;
            _add.TitleFontWeight = FontWeight.Normal;
            _add.Closed += OnAddClosed;
            _add.Opened += OnAddOpened;
            Form.Reset(EventTypes);
            _add.Content = Form;
            _add.InitialFocus = Form.UrlInput;
            _add.CloseName = Labels.Close;
            _add.Title = IsAddPanelOpen ? Labels.NewEndpoint : Labels.AddEndpoint;
            _add.OpenName = Labels.AddEndpoint;
            _add.IsOpen = IsAddPanelOpen;
        }
        AutomationProperties.SetName(_endpointList, Labels.EndpointsListName);
        _endpointList.FlashInset = RowPlateInset;
        UpdateHeader();
        ResolveEndpoints();
        ApplyWidth(Bounds.Width, force: true);
        ShowEndpoint(SelectedEndpoint, animate: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;
        UpdatePalette();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
        CancelAll();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdatePalette();

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => UpdatePalette();

    private void UpdatePalette()
    {
        if (VisualRoot is null)
        {
            return;
        }
        try
        {
            var surface = ArcTokens.GetColor(this, ArcDesignTokenKind.Surface);
            var muted = ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted);
            PlateBrush = new ImmutableSolidColorBrush(Oklch.MixOklab(surface, muted, 0.8));
        }
        catch (Exception)
        {
            // No theme scope yet: the next attach or theme change mixes it.
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EndpointsSourceProperty)
        {
            ResolveEndpoints();
        }
        else if (change.Property == SelectedEndpointProperty)
        {
            var source = _endpointSource ?? WebhookChangeSource.Programmatic;
            _endpointSource = null;
            OnEndpointSelected(change.GetOldValue<WebhookEndpoint?>(), change.GetNewValue<WebhookEndpoint?>(), source);
        }
        else if (change.Property == SelectedDeliveryProperty)
        {
            var source = _deliverySource ?? WebhookChangeSource.Programmatic;
            _deliverySource = null;
            OnDeliverySelected(change.GetOldValue<WebhookDelivery?>(), change.GetNewValue<WebhookDelivery?>(), source);
        }
        else if (change.Property == InspectorTabProperty)
        {
            _split?.SyncTab(CanAnimate);
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty)
        {
            UpdateHeader();
        }
        else if (change.Property == IsAddPanelOpenProperty)
        {
            OnIsAddPanelOpenChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == AddPanelStateProperty)
        {
            PseudoClasses.Set(RevealingPseudoClass, AddPanelState == WebhookAddState.Revealing);
        }
        else if (change.Property == LabelsProperty)
        {
            RebuildForLabels();
        }
        else if (change.Property == ShowsRetryProperty)
        {
            RefreshDeliveryRows();
        }
        else if (change.Property == RowPlateInsetProperty)
        {
            _endpointList.FlashInset = RowPlateInset;
        }
    }

    private bool CanAnimate => IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;

    private void UpdateHeader()
    {
        if (_titleText is not null)
        {
            _titleText.Text = Title ?? Labels.Title;
        }
        if (_descriptionText is not null)
        {
            _descriptionText.Text = Description;
            _descriptionText.IsVisible = !string.IsNullOrWhiteSpace(Description);
        }
    }

    private void RebuildForLabels()
    {
        _form = null;
        _reveal = null;
        if (_add is not null && !IsAddPanelOpen)
        {
            Form.Reset(EventTypes);
            _add.Content = Form;
            _add.InitialFocus = Form.UrlInput;
            _add.Title = Labels.AddEndpoint;
            _add.OpenName = Labels.AddEndpoint;
            _add.CloseName = Labels.Close;
        }
        _endpointList.ItemTemplate = new FuncDataTemplate<WebhookEndpoint>((endpoint, _) => CreateEndpointRow(endpoint));
        AutomationProperties.SetName(_endpointList, Labels.EndpointsListName);
        UpdateHeader();
        UpdateToolbar(animate: false);
        ShowEndpoint(SelectedEndpoint, animate: false);
    }

    // ---- Endpoints ----

    private void ResolveEndpoints()
    {
        if (_effective is INotifyCollectionChanged old && !ReferenceEquals(_effective, Endpoints))
        {
            old.CollectionChanged -= OnSourceChanged;
        }
        _effective = EndpointsSource ?? Endpoints;
        if (_effective is INotifyCollectionChanged next && !ReferenceEquals(_effective, Endpoints))
        {
            next.CollectionChanged += OnSourceChanged;
        }
        _endpointList.Items = _effective;
        OnEndpointsChanged();
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnEndpointsChanged();

    private void OnEndpointsChanged()
    {
        var current = EffectiveEndpoints;
        foreach (var gone in _watchedEndpoints.Where(w => !current.Contains(w)).ToList())
        {
            gone.StatsChanged -= OnEndpointStatsChanged;
            _watchedEndpoints.Remove(gone);
        }
        foreach (var endpoint in current.Where(c => !_watchedEndpoints.Contains(c)))
        {
            endpoint.StatsChanged += OnEndpointStatsChanged;
            _watchedEndpoints.Add(endpoint);
        }
        if (SelectedEndpoint is null || !current.Contains(SelectedEndpoint))
        {
            SelectEndpoint(current.Count > 0 ? current[0] : null, WebhookChangeSource.Auto);
        }
        UpdateToolbar(CanAnimate);
        UpdateSeparators();
    }

    private void OnEndpointStatsChanged(object? sender, EventArgs e) => UpdateToolbar(CanAnimate);

    private void UpdateToolbar(bool animate)
    {
        var endpoints = EffectiveEndpoints;
        var labels = Labels;
        _countText.Text = endpoints.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        _countWord.Text = labels.EndpointsWord(endpoints.Count);
        var failing = endpoints.Count(e => e.FailingCount > 0);
        var note = failing > 0 ? labels.FailingNote(failing) : labels.HealthyNote;
        _note.Mode = animate ? ContentSwapMode.BlurSwap : ContentSwapMode.None;
        if (!Equals(_note.Content, note))
        {
            _note.Content = note;
        }
        _noteHost.IsVisible = !_compact && endpoints.Count > 0;
        AutomationProperties.SetName(_leading, $"{_countText.Text} {_countWord.Text}{(_noteHost.IsVisible ? ", " + note : string.Empty)}");
    }

    private void UpdateSeparators()
    {
        var first = true;
        foreach (var item in _endpointList.LiveItems)
        {
            if (_endpointList.ContainerFor(item) is WebhookEndpointRow row)
            {
                row.ShowsSeparator = !first;
                first = false;
            }
        }
    }

    private void SelectEndpoint(WebhookEndpoint? endpoint, WebhookChangeSource source)
    {
        if (ReferenceEquals(SelectedEndpoint, endpoint))
        {
            return;
        }
        _endpointSource = source;
        SetCurrentValue(SelectedEndpointProperty, endpoint);
        _endpointSource = null;
    }

    internal void SelectDelivery(WebhookDelivery? delivery, WebhookChangeSource source)
    {
        if (ReferenceEquals(SelectedDelivery, delivery))
        {
            return;
        }
        _deliverySource = source;
        SetCurrentValue(SelectedDeliveryProperty, delivery);
        _deliverySource = null;
    }

    internal void SelectEndpointFromRow(WebhookEndpoint endpoint, WebhookChangeSource source) => SelectEndpoint(endpoint, source);

    internal void SelectDeliveryFromRow(WebhookDelivery delivery, WebhookChangeSource source) => SelectDelivery(delivery, source);

    private void OnEndpointSelected(WebhookEndpoint? oldEndpoint, WebhookEndpoint? newEndpoint, WebhookChangeSource source)
    {
        var animate = CanAnimate && oldEndpoint is not null;
        foreach (var item in _endpointList.LiveItems)
        {
            if (_endpointList.ContainerFor(item) is WebhookEndpointRow row)
            {
                row.IsSelected = ReferenceEquals(item, newEndpoint);
            }
        }
        if (animate && ArcMotion.IsFull(this))
        {
            _endpointGlideUntil = DateTime.UtcNow.AddMilliseconds(700);
        }
        PlaceEndpointHighlight(animate && ArcMotion.IsFull(this));
        ShowEndpoint(newEndpoint, animate);
        RaiseEvent(new WebhookEndpointSelectionChangedEventArgs(EndpointSelectionChangedEvent, this, oldEndpoint, newEndpoint, source));
    }

    private void ShowEndpoint(WebhookEndpoint? endpoint, bool animate)
    {
        _detail.Show(endpoint, animate);
        if (_splitSwap is null)
        {
            return;
        }
        if (_split is not null && ReferenceEquals(_split.Endpoint, endpoint))
        {
            _split.Select(SelectedDelivery, animate: false);
            return;
        }
        // The focus would leave with the old half: remember it and hand it to the new one.
        var focusInside = _split is not null && IsFocusWithin(_split);
        var previous = _split;
        _split = endpoint is null ? null : new WebhookSplitView(this, endpoint);
        _splitSwap.Mode = animate ? ContentSwapMode.Lift : ContentSwapMode.None;
        _splitSwap.Content = _split;
        if (previous is not null)
        {
            previous.IsHitTestVisible = false;
            ReleaseWhenGone(previous);
        }
        var delivery = endpoint is null ? null : WebhookMath.DefaultDelivery(endpoint.Deliveries);
        if (ReferenceEquals(SelectedDelivery, delivery))
        {
            _split?.Select(delivery, animate: false);
        }
        else
        {
            SelectDelivery(delivery, WebhookChangeSource.Auto);
        }
        if (focusInside)
        {
            // An endpoint picked with the mouse: its delivery takes focus without the keyboard ring.
            var method = BlockFocusModality.For(this);
            Dispatcher.UIThread.Post(() => FocusDelivery(SelectedDelivery, method), DispatcherPriority.Loaded);
        }
    }

    private static void ReleaseWhenGone(WebhookSplitView view)
    {
        if (TopLevel.GetTopLevel(view) is null)
        {
            view.Release();
            return;
        }
        void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            view.DetachedFromVisualTree -= OnDetached;
            view.Release();
        }
        view.DetachedFromVisualTree += OnDetached;
    }

    private void OnDeliverySelected(WebhookDelivery? oldDelivery, WebhookDelivery? newDelivery, WebhookChangeSource source)
    {
        _split?.Select(newDelivery, CanAnimate && oldDelivery is not null);
        RaiseEvent(new WebhookDeliverySelectionChangedEventArgs(DeliverySelectionChangedEvent, this, oldDelivery, newDelivery, source));
    }

    private void PlaceEndpointHighlight(bool glide)
    {
        var selected = SelectedEndpoint;
        if (selected is null || _endpointList.ContainerFor(selected) is not { } row || _endpointList.StateOf(selected) is not { } state || state.IsLeaving)
        {
            _endpointHighlight.Hide(animate: false);
            return;
        }
        var inset = RowPlateInset;
        var rect = new Rect(row.Bounds.X + inset.Left, row.Bounds.Y + state.Offset + inset.Top,
            Math.Max(0, row.Bounds.Width - inset.Left - inset.Right), Math.Max(0, row.Bounds.Height - inset.Top - inset.Bottom));
        if (rect == _endpointHighlight.CurrentRect && _endpointHighlight.CurrentOpacity >= 1)
        {
            return;
        }
        if (glide && _endpointHighlight.CurrentOpacity > 0)
        {
            _endpointHighlight.MoveTo(rect, DeveloperSprings.Smooth);
        }
        else
        {
            _endpointHighlight.MoveTo(rect, animate: false);
        }
    }

    // ---- Keyboard ----

    /// <summary>↑ ↓ Home End on an endpoint row: focus and select the target row (no wrap). Returns whether it moved.</summary>
    internal bool MoveEndpointFocus(WebhookEndpoint from, Key key)
    {
        var items = _endpointList.LiveItems.OfType<WebhookEndpoint>().ToList();
        var target = Step(items, from, key);
        if (target is null)
        {
            return key is Key.Up or Key.Down or Key.Home or Key.End;
        }
        SelectEndpoint(target, WebhookChangeSource.Keyboard);
        RowFor(target)?.Button.Focus(NavigationMethod.Directional);
        return true;
    }

    /// <summary>↑ ↓ Home End on a delivery row (Retry buttons are skipped).</summary>
    internal bool MoveDeliveryFocus(WebhookEndpoint endpoint, WebhookDelivery from, Key key)
    {
        if (_split is null || !ReferenceEquals(_split.Endpoint, endpoint))
        {
            return false;
        }
        var items = _split.List.LiveItems.OfType<WebhookDelivery>().ToList();
        var target = Step(items, from, key);
        if (target is null)
        {
            return key is Key.Up or Key.Down or Key.Home or Key.End;
        }
        SelectDelivery(target, WebhookChangeSource.Keyboard);
        _split.RowFor(target)?.Button.Focus(NavigationMethod.Directional);
        return true;
    }

    private static T? Step<T>(IReadOnlyList<T> items, T from, Key key) where T : class
    {
        var index = -1;
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], from))
            {
                index = i;
                break;
            }
        }
        if (index < 0 || items.Count == 0)
        {
            return null;
        }
        var next = key switch
        {
            Key.Up => index - 1,
            Key.Down => index + 1,
            Key.Home => 0,
            Key.End => items.Count - 1,
            _ => -1,
        };
        return next < 0 || next >= items.Count || next == index ? null : items[next];
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        // Escape while the form waits for the server cancels the request and stays in the form (the panel's own
        // Escape runs later, on the bubbling route, and sees the event handled).
        if (e.Key == Key.Escape && AddPanelState == WebhookAddState.Submitting)
        {
            CancelCreate();
            e.Handled = true;
        }
    }

    // ---- Focus ----

    internal void FocusDelivery(WebhookDelivery? delivery, NavigationMethod method)
    {
        if (delivery is null || _split?.RowFor(delivery) is not { } row)
        {
            return;
        }
        row.Button.Focus(method);
    }

    private bool IsFocusWithin(Visual visual)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && (ReferenceEquals(focused, visual) || visual.IsVisualAncestorOf(focused));
    }

    // ---- Layout ----

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyWidth(e.NewSize.Width, force: false);
    }

    private static bool Below(bool current, double width, double threshold) =>
        current ? width <= threshold + 1 : width <= threshold;

    private void ApplyWidth(double width, bool force)
    {
        if (width <= 0)
        {
            if (!force)
            {
                return;
            }
            width = double.PositiveInfinity;
        }
        var stacked = Below(_stacked, width, StackedWidth);
        var narrow = Below(_narrow, width, NarrowWidth);
        var compact = Below(_compact, width, CompactWidth);
        var changed = force || !_layoutKnown || stacked != _stacked || narrow != _narrow || compact != _compact;
        _stacked = stacked;
        _narrow = narrow;
        _compact = compact;
        _layoutKnown = true;
        _form?.ApplyWidth(width);
        if (!changed)
        {
            return;
        }
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        var gutter = Gutter;
        if (_top is not null)
        {
            _top.Padding = new Thickness(gutter, 16);
        }
        foreach (var item in _endpointList.LiveItems)
        {
            if (_endpointList.ContainerFor(item) is WebhookEndpointRow row)
            {
                row.ApplyLayout(narrow, gutter);
            }
        }
        _endpointList.ItemPrepared -= OnEndpointPrepared;
        _endpointList.ItemPrepared += OnEndpointPrepared;
        _detail.ApplyLayout(gutter);
        _split?.ApplyLayout(stacked, gutter);
        UpdateToolbar(animate: false);
    }

    private void OnEndpointPrepared(object? sender, DeveloperListItemEventArgs e)
    {
        if (e.Visual is WebhookEndpointRow row)
        {
            row.ApplyLayout(_narrow, Gutter);
            row.IsSelected = ReferenceEquals(row.Endpoint, SelectedEndpoint);
        }
    }

    // ---- Misc ----

    internal void Announce(string? text) => _announcer?.Announce(text);

    internal void QueueDetailRefresh()
    {
        if (_detailQueued)
        {
            return;
        }
        _detailQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _detailQueued = false;
            _detail.Refresh(CanAnimate);
        });
    }

    internal void OnInspectorCopied(WebhookCopiedContent content, string text)
    {
        Announce(Labels.CopiedAnnouncement(content == WebhookCopiedContent.Headers ? Labels.Headers : Labels.Payload));
        RaiseEvent(new WebhookContentCopiedEventArgs(ContentCopiedEvent, this, content, text));
    }

    private void RefreshDeliveryRows()
    {
        if (_split is null)
        {
            return;
        }
        foreach (var item in _split.List.LiveItems)
        {
            (_split.List.ContainerFor(item) as WebhookDeliveryRow)?.RefreshAction();
        }
    }
}
