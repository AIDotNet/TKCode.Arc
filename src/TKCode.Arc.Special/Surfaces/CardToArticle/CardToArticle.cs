using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media.Immutable;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An editorial grid of story cards that open into articles (Arc Card to article, design reference). Each card is a
/// picture with its tag, title and reading time drawn over it; pressing it (or Enter / Space) grows that very card into
/// a centred article sheet: the frame and its corners stretch on one spring, the picture scales as a whole into the
/// article's hero, the text rises in behind it and the close button pops in last. The article scrolls with a reading
/// progress bar along its top and closes with the close button, Escape, or by pulling it down (a mouse on the picture,
/// a finger anywhere once the article is at its top, or the wheel scrolling up past the top); it lands back on the
/// card's slot, measured again at that moment, and focus returns to the card.
/// <para>
/// <see cref="OpenItemId"/> is the open article (two-way; set it to open or close with the morph);
/// <see cref="OpenItemChanged"/> reports every change with its cause. The grid has two columns while the control is
/// wider than <see cref="ColumnBreakpoint"/> and one below; the article sizes itself to the window.
/// </para>
/// </summary>
[TemplatePart(GridPart, typeof(CardToArticleGrid))]
[PseudoClasses(NarrowPseudoClass, OpenPseudoClass)]
public class CardToArticle : TemplatedControl
{
    internal const string GridPart = "PART_Grid";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string OpenPseudoClass = ":open";

    /// <summary>The article grows out of its card (Arc: critically damped, 0.52s).</summary>
    internal static readonly SpringSpec OpenSpring = SpringSpec.FromVisual(0.52, 0);

    /// <summary>The article flies back onto its card (Arc: critically damped, 0.44s), the lift with it.</summary>
    internal static readonly SpringSpec CloseSpring = SpringSpec.FromVisual(0.44, 0);

    /// <summary>The close button pops in (Arc: critically damped, 0.30s, 175ms after the morph starts).</summary>
    internal static readonly SpringSpec PopSpring = SpringSpec.FromVisual(0.30, 0);

    internal static readonly TimeSpan BodyDelay = TimeSpan.FromMilliseconds(135);
    internal static readonly TimeSpan BodyDuration = TimeSpan.FromMilliseconds(360);
    internal static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(175);
    internal static readonly TimeSpan BackdropInDuration = TimeSpan.FromMilliseconds(350);
    internal static readonly TimeSpan BackdropOutDuration = TimeSpan.FromMilliseconds(290);
    internal static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(120);
    internal static readonly TimeSpan ReducedBackdropDuration = TimeSpan.FromMilliseconds(120);
    internal static readonly TimeSpan ReducedBodyDuration = TimeSpan.FromMilliseconds(350);
    internal static readonly TimeSpan ReducedCloseDuration = TimeSpan.FromMilliseconds(300);

    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);

    #region Properties

    /// <summary>The stories, in grid order.</summary>
    public static readonly DirectProperty<CardToArticle, IList<CardToArticleItem>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<CardToArticle, IList<CardToArticleItem>>(nameof(Items), o => o.Items, (o, v) => o.Items = v);

    /// <summary>The id of the open article; null when every card is closed. Two-way: opening and closing write it back.</summary>
    public static readonly StyledProperty<string?> OpenItemIdProperty =
        AvaloniaProperty.Register<CardToArticle, string?>(nameof(OpenItemId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The width-to-height ratio of the cards and of the article's hero picture (default 4:3).</summary>
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<CardToArticle, double>(nameof(AspectRatio), 4.0 / 3.0);

    /// <summary>The template that shows an item's <see cref="CardToArticleItem.Body"/> when it is not a string or a control.</summary>
    public static readonly StyledProperty<IDataTemplate?> BodyTemplateProperty =
        AvaloniaProperty.Register<CardToArticle, IDataTemplate?>(nameof(BodyTemplate));

    /// <summary>The grid has one column while the control is at most this wide, two above (default 520).</summary>
    public static readonly StyledProperty<double> ColumnBreakpointProperty =
        AvaloniaProperty.Register<CardToArticle, double>(nameof(ColumnBreakpoint), CardToArticleLayout.ColumnBreakpoint);

    /// <summary>The words the control shows and reads out (English by default).</summary>
    public static readonly StyledProperty<CardToArticleLabels> LabelsProperty =
        AvaloniaProperty.Register<CardToArticle, CardToArticleLabels>(nameof(Labels), new CardToArticleLabels());

    /// <summary>The story whose article is open (or opening); null when none is.</summary>
    public static readonly DirectProperty<CardToArticle, CardToArticleItem?> OpenItemProperty =
        AvaloniaProperty.RegisterDirect<CardToArticle, CardToArticleItem?>(nameof(OpenItem), o => o.OpenItem);

    internal static readonly StyledProperty<double> BackdropOpacityProperty =
        AvaloniaProperty.Register<CardToArticle, double>(nameof(BackdropOpacity), 0.55);

    internal static readonly StyledProperty<double> BackdropBlurRadiusProperty =
        AvaloniaProperty.Register<CardToArticle, double>(nameof(BackdropBlurRadius), 10);

    /// <summary>Raised when the open article changes (opened, closed or switched), with the cause.</summary>
    public static readonly RoutedEvent<CardToArticleOpenChangedEventArgs> OpenItemChangedEvent =
        RoutedEvent.Register<CardToArticle, CardToArticleOpenChangedEventArgs>(nameof(OpenItemChanged), RoutingStrategies.Bubble);

    private IList<CardToArticleItem> _items = new AvaloniaList<CardToArticleItem>();
    private CardToArticleItem? _openItem;

    /// <summary>The stories, in grid order.</summary>
    [Content]
    public IList<CardToArticleItem> Items
    {
        get => _items;
        set
        {
            var old = _items;
            if (SetAndRaise(ItemsProperty, ref _items, value ?? new AvaloniaList<CardToArticleItem>()))
            {
                OnItemsReplaced(old);
            }
        }
    }

    /// <summary>The id of the open article; null when every card is closed. Two-way: opening and closing write it back.</summary>
    public string? OpenItemId { get => GetValue(OpenItemIdProperty); set => SetValue(OpenItemIdProperty, value); }

    /// <summary>The width-to-height ratio of the cards and of the article's hero picture (default 4:3).</summary>
    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }

    /// <summary>The template that shows an item's <see cref="CardToArticleItem.Body"/> when it is not a string or a control.</summary>
    public IDataTemplate? BodyTemplate { get => GetValue(BodyTemplateProperty); set => SetValue(BodyTemplateProperty, value); }

    /// <summary>The grid has one column while the control is at most this wide, two above (default 520).</summary>
    public double ColumnBreakpoint { get => GetValue(ColumnBreakpointProperty); set => SetValue(ColumnBreakpointProperty, value); }

    /// <summary>The words the control shows and reads out (English by default).</summary>
    public CardToArticleLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The story whose article is open (or opening); null when none is.</summary>
    public CardToArticleItem? OpenItem { get => _openItem; private set => SetAndRaise(OpenItemProperty, ref _openItem, value); }

    internal double BackdropOpacity { get => GetValue(BackdropOpacityProperty); set => SetValue(BackdropOpacityProperty, value); }
    internal double BackdropBlurRadius { get => GetValue(BackdropBlurRadiusProperty); set => SetValue(BackdropBlurRadiusProperty, value); }

    /// <summary>Raised when the open article changes (opened, closed or switched), with the cause.</summary>
    public event EventHandler<CardToArticleOpenChangedEventArgs>? OpenItemChanged
    {
        add => AddHandler(OpenItemChangedEvent, value);
        remove => RemoveHandler(OpenItemChangedEvent, value);
    }

    #endregion

    private readonly Dictionary<string, CardToArticleCard> _cards = new(StringComparer.Ordinal);
    private readonly List<CardToArticleCard> _cardList = new();
    private CardToArticleGrid? _grid;
    private CardToArticleArticle? _article;
    private ModalPresence? _presence;
    private SharedElementScope? _flight;
    private CardToArticleCard? _flightCard;
    private MotionValue<double>? _scrim;
    private MotionValue<double>? _bodyReveal;
    private MotionValue<double>? _closeReveal;
    private CardToArticleChangeReason? _pendingReason;
    private bool _bodyTravel = true;
    private bool _closePop = true;
    private bool _instant;
    private bool _pendingOpen;

    static CardToArticle()
    {
        FocusableProperty.OverrideDefaultValue<CardToArticle>(false);
    }

    public CardToArticle()
    {
        if (_items is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnItemsChanged;
        }
    }

    #region Public methods

    /// <summary>Opens the article of the story with <paramref name="id"/> (growing out of its card).</summary>
    public void Open(string id)
    {
        _pendingReason = CardToArticleChangeReason.Programmatic;
        SetCurrentValue(OpenItemIdProperty, id);
        _pendingReason = null;
    }

    /// <summary>Closes the open article (it flies back onto its card).</summary>
    public void Close()
    {
        _pendingReason = CardToArticleChangeReason.Programmatic;
        SetCurrentValue(OpenItemIdProperty, null);
        _pendingReason = null;
    }

    #endregion

    #region Internals for tests

    internal CardToArticleGrid? Grid => _grid;
    internal CardToArticleArticle? Article => _article;
    internal ModalPresence? Presence => _presence;
    internal SharedElementScope? Flight => _flight;
    internal IReadOnlyList<CardToArticleCard> Cards => _cardList;
    internal double ScrimValue => _scrim?.Value ?? 0;
    internal double BodyRevealValue => _bodyReveal?.Value ?? 0;
    internal double CloseRevealValue => _closeReveal?.Value ?? 0;

    internal CardToArticleCard? CardFor(string id) => _cards.GetValueOrDefault(id);

    #endregion

    #region Template, items and cards

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _grid?.Children.Clear();
        _grid = e.NameScope.Find<CardToArticleGrid>(GridPart);
        RebuildCards();
    }

    private void OnItemsReplaced(IList<CardToArticleItem> old)
    {
        if (old is INotifyCollectionChanged oldObservable)
        {
            oldObservable.CollectionChanged -= OnItemsChanged;
        }
        if (_items is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnItemsChanged;
        }
        RebuildCards();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildCards();

    private void RebuildCards()
    {
        var reuse = new Dictionary<CardToArticleItem, CardToArticleCard>(ReferenceEqualityComparer.Instance);
        foreach (var card in _cardList)
        {
            if (card.Item is { } item)
            {
                reuse[item] = card;
            }
        }
        _cards.Clear();
        _cardList.Clear();
        _grid?.Children.Clear();
        foreach (var item in _items)
        {
            if (item is null || _cards.ContainsKey(item.Id))
            {
                continue;
            }
            if (!reuse.Remove(item, out var card))
            {
                card = new CardToArticleCard { Item = item };
                card.Activated += OnCardActivated;
            }
            card.AspectRatio = AspectRatio;
            AutomationProperties.SetName(card, Labels.CardName(item));
            _cards[item.Id] = card;
            _cardList.Add(card);
            _grid?.Children.Add(card);
        }
        foreach (var gone in reuse.Values)
        {
            gone.Activated -= OnCardActivated;
        }
        // The open story left the list: its article goes at once.
        if (_flightCard is { } flown && !_cardList.Contains(flown))
        {
            ShutDown();
            if (OpenItemId is not null)
            {
                SetCurrentValue(OpenItemIdProperty, null);
            }
        }
    }

    private void OnCardActivated(CardToArticleCard card, bool keyboard)
    {
        if (card.Item is not { } item)
        {
            return;
        }
        if (OpenItemId == item.Id && _presence is { IsOpen: true })
        {
            return;
        }
        _pendingReason = keyboard ? CardToArticleChangeReason.Keyboard : CardToArticleChangeReason.Click;
        SetCurrentValue(OpenItemIdProperty, item.Id);
        _pendingReason = null;
    }

    private void RequestClose(CardToArticleChangeReason reason)
    {
        if (OpenItemId is null)
        {
            return;
        }
        _pendingReason = reason;
        SetCurrentValue(OpenItemIdProperty, null);
        _pendingReason = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OpenItemIdProperty)
        {
            var reason = _pendingReason ?? CardToArticleChangeReason.Programmatic;
            var oldId = change.GetOldValue<string?>();
            var newId = change.GetNewValue<string?>();
            ApplyOpenId(newId, reason);
            RaiseEvent(new CardToArticleOpenChangedEventArgs(OpenItemChangedEvent, oldId, newId, reason));
        }
        else if (change.Property == AspectRatioProperty)
        {
            foreach (var card in _cardList)
            {
                card.AspectRatio = AspectRatio;
            }
            if (_article is { } article)
            {
                article.AspectRatio = AspectRatio;
            }
        }
        else if (change.Property == LabelsProperty)
        {
            foreach (var card in _cardList)
            {
                if (card.Item is { } item)
                {
                    AutomationProperties.SetName(card, Labels.CardName(item));
                }
            }
            if (_article is { } article)
            {
                article.CloseLabel = Labels.Close;
                if (article.Item is { } open)
                {
                    AutomationProperties.SetName(article, Labels.ArticleName(open));
                }
            }
        }
        else if (change.Property == BodyTemplateProperty && _article is { } article)
        {
            article.BodyTemplate = BodyTemplate;
        }
        else if (change.Property == BoundsProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, CardToArticleLayout.Columns(Bounds.Width, ColumnBreakpoint) == 1);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (OpenItemId is not null && _presence is not { IsOpen: true })
        {
            // Opened before the control was shown: the article is simply there (no morph from a card nobody saw).
            _pendingOpen = true;
            Dispatcher.UIThread.Post(() =>
            {
                if (_pendingOpen && OpenItemId is { } id)
                {
                    _pendingOpen = false;
                    OpenArticle(id, instant: true);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pendingOpen = false;
    }

    #endregion

    #region Open and close

    private void ApplyOpenId(string? id, CardToArticleChangeReason reason)
    {
        if (id is null)
        {
            _pendingOpen = false;
            CloseArticle(reason);
            return;
        }
        if (TopLevel.GetTopLevel(this) is null || _grid is null)
        {
            _pendingOpen = true;
            return;
        }
        OpenArticle(id, instant: false);
    }

    private void EnsureArticle()
    {
        if (_article is not null)
        {
            return;
        }
        var article = new CardToArticleArticle
        {
            AspectRatio = AspectRatio,
            BodyTemplate = BodyTemplate,
            CloseLabel = Labels.Close
        };
        article.CloseRequested += (reason, _) => RequestClose(reason);
        LogicalChildren.Add(article);
        _article = article;

        _presence = new ModalPresence(article, this)
        {
            Entrance = ModalEntrance.Custom,
            DismissOnScrimPress = false,
            CustomMotion = RunMotion
        };
        _presence.Dismissing += (_, e) =>
        {
            // The control owns the decision, so OpenItemId (and its two-way bindings) sees every close.
            e.Cancel = true;
            RequestClose(e.Reason == DismissReason.Escape ? CardToArticleChangeReason.Escape : CardToArticleChangeReason.Programmatic);
        };
        _presence.Closed += OnPresenceClosed;

        _scrim = CreateValue(0, v => { if (_presence is { } p) { p.ScrimOpacity = v; } });
        _bodyReveal = CreateValue(0, v => _article?.SetBodyReveal(v, _bodyTravel));
        _closeReveal = CreateValue(0, v => _article?.SetCloseReveal(v, _closePop));
    }

    private MotionValue<double> CreateValue(double initial, Action<double> apply)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }

    private void OpenArticle(string id, bool instant)
    {
        if (!_cards.TryGetValue(id, out var card) || card.Item is not { } item)
        {
            return;
        }
        EnsureArticle();
        var presence = _presence!;
        var article = _article!;
        if (presence.IsOpen && ReferenceEquals(_flightCard, card))
        {
            return;
        }
        if (presence.State == ModalState.Closing && ReferenceEquals(_flightCard, card))
        {
            // Caught on the way home: the same spring turns around with its velocity.
            _instant = instant;
            card.IsAway = true;
            OpenItem = item;
            PseudoClasses.Set(OpenPseudoClass, true);
            presence.Open();
            return;
        }
        var switching = presence.IsOpen;
        if (_flight is not null)
        {
            // Another story was open or on its way home: hand its card back at once.
            _flight.ProgressChanged -= OnFlightProgress;
            _flight.Dispose();
            _flight = null;
        }
        if (_flightCard is { } previous && !ReferenceEquals(previous, card))
        {
            previous.IsAway = false;
        }
        _flightCard = card;
        card.IsAway = true;
        OpenItem = item;
        PseudoClasses.Set(OpenPseudoClass, true);
        article.Item = item;
        article.AspectRatio = AspectRatio;
        article.BodyTemplate = BodyTemplate;
        article.CloseLabel = Labels.Close;
        AutomationProperties.SetName(article, Labels.ArticleName(item));
        article.Opacity = 1;
        article.ResetForOpen();
        _flight = new SharedElementScope(card, article) { RevealsSourceOnArrival = false };
        _flight.ProgressChanged += OnFlightProgress;
        _instant = instant;

        if (switching)
        {
            // Switched while open (code changed OpenItemId): the new story is simply there.
            article.UpdateLayout();
            _flight.HideSource();
            _flight.Jump(1);
            article.UpdateFlightCorrection();
            return;
        }
        article.ApplyTemplate();
        presence.InitialFocus = article.CloseButton;
        var background = ArcTokens.GetColor(this, ArcDesignTokenKind.Background);
        presence.ScrimBrush = new ImmutableSolidColorBrush(Oklch.WithAlpha(background, BackdropOpacity));
        presence.BackdropBlur = BackdropBlurRadius;
        if (presence.State == ModalState.Closed)
        {
            _scrim!.Jump(0);
            _bodyReveal!.Jump(0);
            _closeReveal!.Jump(0);
        }
        if (!presence.Open())
        {
            ShutDown();
            _pendingOpen = true;
        }
    }

    private void CloseArticle(CardToArticleChangeReason reason)
    {
        if (_presence is not { IsOpen: true } presence)
        {
            return;
        }
        PseudoClasses.Set(OpenPseudoClass, false);
        presence.Close(reason switch
        {
            CardToArticleChangeReason.Escape => DismissReason.Escape,
            CardToArticleChangeReason.CloseButton => DismissReason.CloseButton,
            CardToArticleChangeReason.Drag or CardToArticleChangeReason.Wheel => DismissReason.Gesture,
            _ => DismissReason.Programmatic
        });
    }

    private void OnFlightProgress(double progress) => _article?.UpdateFlightCorrection();

    /// <summary>
    /// The article's enter and exit (<see cref="ModalEntrance.Custom"/>). Full motion: the card's frame and picture fly
    /// to the sheet on the open spring while the backdrop fades in (350ms, enter curve), the text rises 16px and fades in
    /// from 135ms (360ms, standard) and the close button pops from 0.8 at 175ms; closing flies them home on the close
    /// spring while the text and button fade at once and the backdrop fades out (290ms), then hands the card back in the
    /// same frame. Reduced: no travel (the sheet is in place at once), fades only; closing removes it at once.
    /// Code-driven: the article lives on the overlay layer outside any theme, its exit gates the removal, a reopen must
    /// reverse with the spring's velocity, and the flight reads boxes from two visual trees every frame.
    /// </summary>
    private async Task<bool> RunMotion(ModalPresence presence, bool entering)
    {
        var flight = _flight;
        var article = _article;
        if (flight is null || article is null)
        {
            return true;
        }
        var instant = _instant;
        _instant = false;
        var full = !instant && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var reduced = !instant && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced;
        if (entering)
        {
            if (full)
            {
                _bodyTravel = true;
                _closePop = true;
                _ = _bodyReveal!.AnimateTo(1, MotionSpec.Tween(BodyDuration, ArcEasings.Standard, BodyDelay));
                _ = _closeReveal!.AnimateTo(1, MotionSpec.Spring(PopSpring, CloseDelay));
                var scrim = _scrim!.AnimateTo(1, MotionSpec.Tween(BackdropInDuration, ArcEasings.Enter));
                var grown = await flight.FlyAsync(1, MotionSpec.Spring(OpenSpring));
                article.UpdateFlightCorrection();
                return grown & await scrim;
            }
            flight.HideSource();
            flight.Jump(1);
            _bodyTravel = false;
            _closePop = false;
            article.UpdateFlightCorrection();
            if (!reduced)
            {
                _scrim!.Jump(1);
                _bodyReveal!.Jump(1);
                _closeReveal!.Jump(1);
                return true;
            }
            var body = _bodyReveal!.AnimateTo(1, MotionSpec.Tween(ReducedBodyDuration, ArcEasings.Standard));
            var close = _closeReveal!.AnimateTo(1, MotionSpec.Tween(ReducedCloseDuration, ArcEasings.Standard));
            var backdrop = _scrim!.AnimateTo(1, MotionSpec.Tween(ReducedBackdropDuration, s_easeOut));
            return await body & await close & await backdrop;
        }

        if (!full)
        {
            _scrim!.Jump(0);
            _bodyReveal!.Jump(0);
            _closeReveal!.Jump(0);
            _ = article.SettlePull(MotionSpec.Instant);
            HandBack();
            return true;
        }
        _bodyTravel = false;
        _ = _bodyReveal!.AnimateTo(0, MotionSpec.Tween(FadeOutDuration, s_easeOut));
        _ = _closeReveal!.AnimateTo(0, MotionSpec.Tween(FadeOutDuration, s_easeOut));
        var scrimAway = _scrim!.AnimateTo(0, MotionSpec.Tween(BackdropOutDuration, ArcEasings.Enter));
        _ = article.SettlePull(MotionSpec.Spring(CloseSpring));
        var landed = await flight.FlyAsync(0, MotionSpec.Spring(CloseSpring));
        if (landed && ReferenceEquals(flight, _flight))
        {
            HandBack();
        }
        return landed & await scrimAway;
    }

    /// <summary>The sheet sits exactly on the card: hide the article and show the card in the same frame.</summary>
    private void HandBack()
    {
        if (_article is { } article)
        {
            article.Opacity = 0;
        }
        _flight?.Reset();
        if (_flightCard is { } card)
        {
            card.IsAway = false;
        }
        _article?.UpdateFlightCorrection();
    }

    private void OnPresenceClosed(object? sender, ModalClosedEventArgs e)
    {
        HandBack();
        if (_flight is { } flight)
        {
            flight.ProgressChanged -= OnFlightProgress;
            flight.Dispose();
            _flight = null;
        }
        _flightCard = null;
        OpenItem = null;
        PseudoClasses.Set(OpenPseudoClass, false);
        if (OpenItemId is not null)
        {
            // Removed without a close request (the control left the window).
            _pendingReason = CardToArticleChangeReason.Programmatic;
            SetCurrentValue(OpenItemIdProperty, null);
            _pendingReason = null;
        }
    }

    /// <summary>Ends everything at once (the open story was removed, or the window could not host the article).</summary>
    private void ShutDown()
    {
        if (_presence is { } presence && presence.State != ModalState.Closed)
        {
            _instant = true;
            presence.Close();
        }
        HandBack();
        _flightCard = null;
        OpenItem = null;
        PseudoClasses.Set(OpenPseudoClass, false);
    }

    #endregion
}
