using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A wallet of payment cards, passes or memberships with real materials and depth (Arc Wallet stack, design reference):
/// brushed metal, a black card with a guilloche rosette, frosted glass and deep color, each with an EMV chip, a
/// contactless mark, an embossed number and a light that follows the pointer. Hover to fan the stack, drag or use the
/// arrow keys to cycle cards, and choose a card to lift it into its balance and recent activity.
/// </summary>
/// <remarks>
/// <para>Every card's place is one pure function of a continuous stack position, the open amount and the fan amount, so
/// springs, drags and interruptions all move the same shape; the first card is always on top. A vertical drag cycles
/// the cards 1:1, a release projects 0.2s ahead and settles at most one card away, and the ends rubber-band. Pull the
/// open card down to put it back.</para>
/// <para>The stack is a labelled group with a keyboard hint; only the front (or open) card is a tab stop. Arrow keys,
/// Home and End cycle cards (and switch the open card), Enter or Space opens and closes, Escape closes and keeps focus on
/// the card. Changes are announced politely. Under reduced motion positions change at once, nothing tilts and the light
/// stays still; fades remain.</para>
/// </remarks>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(CountPart, typeof(WalletStackCount))]
[TemplatePart(AllCardsPart, typeof(WalletStackHeaderButton))]
[TemplatePart(StagePart, typeof(WalletStackStage))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(OpenPseudoClass, FannedPseudoClass, DraggingPseudoClass)]
public partial class WalletStack : TemplatedControl
{
    public const string TitlePart = "PART_Title";
    public const string CountPart = "PART_Count";
    public const string AllCardsPart = "PART_AllCards";
    public const string StagePart = "PART_Stage";
    public const string AnnouncerPart = "PART_Announcer";
    public const string OpenPseudoClass = ":open";
    public const string FannedPseudoClass = ":fanned";
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>The cards in stack order, the first in front.</summary>
    public static readonly StyledProperty<IList<WalletStackCard>?> CardsProperty =
        AvaloniaProperty.Register<WalletStack, IList<WalletStackCard>?>(nameof(Cards));

    /// <summary>The heading above the stack, also the stack's accessible name.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<WalletStack, string>(nameof(Label), "Wallet");

    /// <summary>ISO 4217 code of the balances and amounts (USD by default).</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<WalletStack, string>(nameof(Currency), "USD");

    /// <summary>The culture that formats balances and amounts; en-US when null.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<WalletStack, CultureInfo?>(nameof(Culture));

    /// <summary>The index of the card in front (two-way). Setting it cycles the stack there; while a card is open it switches the open card.</summary>
    public static readonly StyledProperty<int> PositionProperty =
        AvaloniaProperty.Register<WalletStack, int>(nameof(Position), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The open card, or null while all cards are in the stack (two-way).</summary>
    public static readonly StyledProperty<WalletStackCard?> SelectedCardProperty =
        AvaloniaProperty.Register<WalletStack, WalletStackCard?>(nameof(SelectedCard), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether hovering with a mouse fans the stack and lifts the hovered card.</summary>
    public static readonly StyledProperty<bool> IsFanEnabledProperty =
        AvaloniaProperty.Register<WalletStack, bool>(nameof(IsFanEnabled), true);

    /// <summary>Whether the hovered card tilts toward the mouse and its light follows it.</summary>
    public static readonly StyledProperty<bool> IsTiltEnabledProperty =
        AvaloniaProperty.Register<WalletStack, bool>(nameof(IsTiltEnabled), true);

    /// <summary>Every text the stack shows or speaks; replace it to translate.</summary>
    public static readonly StyledProperty<WalletStackLabels> LabelsProperty =
        AvaloniaProperty.Register<WalletStack, WalletStackLabels>(nameof(Labels), new WalletStackLabels());

    /// <summary>Raised when a card opens, the open card switches, or the open card goes back into the stack.</summary>
    public static readonly RoutedEvent<WalletStackSelectedCardChangedEventArgs> SelectedCardChangedEvent =
        RoutedEvent.Register<WalletStack, WalletStackSelectedCardChangedEventArgs>(nameof(SelectedCardChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when another card comes to the front (by drag, keys, a click or <see cref="Position"/>).</summary>
    public static readonly RoutedEvent<WalletStackPositionChangedEventArgs> PositionChangedEvent =
        RoutedEvent.Register<WalletStack, WalletStackPositionChangedEventArgs>(nameof(PositionChanged), RoutingStrategies.Bubble);

    private readonly List<WalletStackCard> _cards = new();
    private readonly List<MotionValue<double>> _lifts = new();
    private readonly List<MotionValue<double>> _raises = new();
    private readonly MotionValue<double> _position;
    private readonly MotionValue<double> _fan;
    private readonly MotionValue<double> _countShown;
    private readonly MotionValue<double> _buttonShown;
    private readonly ScaleTransform _countScale = new();
    private readonly ScaleTransform _buttonScale = new();
    private WalletStackFormat _format = new("USD", null);
    private WalletStackStage? _stage;
    private TextBlock? _title;
    private WalletStackCount? _count;
    private WalletStackHeaderButton? _allCards;
    private ExplainerAnnouncer? _announcer;
    private DragTracker? _drag;
    private INotifyCollectionChanged? _observed;
    private WalletStackCard? _open;
    private int _raised = -1;
    private int _front;
    private int _previousRaised = -1;
    private bool _syncing;

    static WalletStack()
    {
        FocusableProperty.OverrideDefaultValue<WalletStack>(false);
    }

    public WalletStack()
    {
        _position = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fan = new MotionValue<double>(this, 0, MotionVectors.Double);
        _countShown = new MotionValue<double>(this, 1, MotionVectors.Double);
        _buttonShown = new MotionValue<double>(this, 0, MotionVectors.Double);
        _position.Changed += _ => Relayout();
        _fan.Changed += value =>
        {
            PseudoClasses.Set(FannedPseudoClass, value > 0.5);
            Relayout();
        };
        _countShown.Changed += ApplyCountPresence;
        _buttonShown.Changed += ApplyButtonPresence;
    }

    /// <inheritdoc cref="CardsProperty"/>
    public IList<WalletStackCard>? Cards
    {
        get => GetValue(CardsProperty);
        set => SetValue(CardsProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency
    {
        get => GetValue(CurrencyProperty);
        set => SetValue(CurrencyProperty, value);
    }

    /// <inheritdoc cref="CultureProperty"/>
    public CultureInfo? Culture
    {
        get => GetValue(CultureProperty);
        set => SetValue(CultureProperty, value);
    }

    /// <inheritdoc cref="PositionProperty"/>
    public int Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <inheritdoc cref="SelectedCardProperty"/>
    public WalletStackCard? SelectedCard
    {
        get => GetValue(SelectedCardProperty);
        set => SetValue(SelectedCardProperty, value);
    }

    /// <inheritdoc cref="IsFanEnabledProperty"/>
    public bool IsFanEnabled
    {
        get => GetValue(IsFanEnabledProperty);
        set => SetValue(IsFanEnabledProperty, value);
    }

    /// <inheritdoc cref="IsTiltEnabledProperty"/>
    public bool IsTiltEnabled
    {
        get => GetValue(IsTiltEnabledProperty);
        set => SetValue(IsTiltEnabledProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public WalletStackLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="SelectedCardChangedEvent"/>
    public event EventHandler<WalletStackSelectedCardChangedEventArgs>? SelectedCardChanged
    {
        add => AddHandler(SelectedCardChangedEvent, value);
        remove => RemoveHandler(SelectedCardChangedEvent, value);
    }

    /// <inheritdoc cref="PositionChangedEvent"/>
    public event EventHandler<WalletStackPositionChangedEventArgs>? PositionChanged
    {
        add => AddHandler(PositionChangedEvent, value);
        remove => RemoveHandler(PositionChangedEvent, value);
    }

    /// <summary>Whether a card is open.</summary>
    public bool IsOpen => _open is not null;

    internal WalletStackStage? Stage => _stage;

    internal WalletStackCount? CountPresenter => _count;

    internal WalletStackHeaderButton? AllCardsButton => _allCards;

    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <summary>The continuous stack position (tests read it).</summary>
    internal double StackPosition => _position.Value;

    /// <summary>The fan amount (tests read it).</summary>
    internal double FanAmount => _fan.Value;

    /// <summary>The summed open amount of all cards (tests read it).</summary>
    internal double OpenAmount
    {
        get
        {
            var sum = 0.0;
            foreach (var raise in _raises)
            {
                sum += raise.Value;
            }
            return sum;
        }
    }

    internal WalletStackFormat Format => _format;

    /// <summary>Brings the next card to the front (or opens the next card while one is open).</summary>
    public void Next() => Step(1, focus: false);

    /// <summary>Brings the previous card to the front (or opens the previous card while one is open).</summary>
    public void Previous() => Step(-1, focus: false);

    /// <summary>Brings card <paramref name="index"/> to the front (or switches the open card to it).</summary>
    public void GoTo(int index) => Navigate(index, 0, focus: false, fromKeyboard: false);

    /// <summary>Opens <paramref name="card"/> (it must be one of <see cref="Cards"/>).</summary>
    public void Open(WalletStackCard card)
    {
        var index = _cards.IndexOf(card);
        if (index >= 0)
        {
            OpenCard(index, 0);
        }
    }

    /// <summary>Opens the card at <paramref name="index"/>.</summary>
    public void Open(int index)
    {
        if (index >= 0 && index < _cards.Count)
        {
            OpenCard(index, 0);
        }
    }

    /// <summary>Puts the open card back into the stack, in front.</summary>
    public void Close() => CloseCard(0, focus: false);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _count = e.NameScope.Find<WalletStackCount>(CountPart);
        _allCards = e.NameScope.Find<WalletStackHeaderButton>(AllCardsPart);
        _stage = e.NameScope.Find<WalletStackStage>(StagePart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_count is not null)
        {
            _count.RenderTransform = _countScale;
        }
        if (_allCards is not null)
        {
            _allCards.RenderTransform = _buttonScale;
            _allCards.Click += OnAllCardsClick;
        }
        if (_stage is not null)
        {
            _stage.Owner = this;
            _stage.GeometryChanged += Relayout;
            AttachInput(_stage);
            _stage.SetCards(_cards);
        }
        UpdateTexts();
        UpdateHeader(animate: false);
        Relayout();
    }

    private void DetachParts()
    {
        if (_allCards is not null)
        {
            _allCards.Click -= OnAllCardsClick;
        }
        if (_stage is not null)
        {
            DetachInput(_stage);
            _stage.GeometryChanged -= Relayout;
            _stage.Owner = null;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == CardsProperty)
        {
            ObserveCards(change.GetNewValue<IList<WalletStackCard>?>());
            RebuildCards();
        }
        else if (property == CurrencyProperty || property == CultureProperty)
        {
            _format = new WalletStackFormat(Currency, Culture);
            UpdateTexts();
            if (_open is { } open && _stage is not null)
            {
                _stage.Detail.Reset();
                _stage.Detail.Show(open, _format, Labels, _stage.Inks);
            }
        }
        else if (property == LabelProperty)
        {
            UpdateTexts();
        }
        else if (property == LabelsProperty)
        {
            UpdateTexts();
            _stage?.Rebake();
        }
        else if (property == PositionProperty && !_syncing)
        {
            Navigate(change.GetNewValue<int>(), 0, focus: false, fromKeyboard: false);
        }
        else if (property == SelectedCardProperty && !_syncing)
        {
            var card = change.GetNewValue<WalletStackCard?>();
            var index = card is null ? -1 : _cards.IndexOf(card);
            if (index >= 0)
            {
                OpenCard(index, 0);
            }
            else
            {
                CloseCard(0, focus: false);
                if (card is not null)
                {
                    // Not one of the cards: nothing is open.
                    _syncing = true;
                    try
                    {
                        SetCurrentValue(SelectedCardProperty, null);
                    }
                    finally
                    {
                        _syncing = false;
                    }
                }
            }
        }
        else if (property == IsFanEnabledProperty && !IsFanEnabled)
        {
            SetFan(0);
        }
        else if (property == IsTiltEnabledProperty && !IsTiltEnabled && _stage is not null)
        {
            foreach (var view in _stage.Views)
            {
                view.Tilt.Leave();
            }
        }
    }

    private void ObserveCards(IList<WalletStackCard>? cards)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnCardsCollectionChanged;
        }
        _observed = cards as INotifyCollectionChanged;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnCardsCollectionChanged;
        }
    }

    private void OnCardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildCards();

    private void RebuildCards()
    {
        var open = _open;
        _cards.Clear();
        if (Cards is { } cards)
        {
            _cards.AddRange(cards);
        }
        foreach (var value in _lifts)
        {
            value.Dispose();
        }
        foreach (var value in _raises)
        {
            value.Dispose();
        }
        _lifts.Clear();
        _raises.Clear();
        for (var i = 0; i < _cards.Count; i++)
        {
            var lift = new MotionValue<double>(this, 0, MotionVectors.Double);
            lift.Changed += _ => Relayout();
            _lifts.Add(lift);
            var raise = new MotionValue<double>(this, 0, MotionVectors.Double);
            raise.Changed += _ => Relayout();
            _raises.Add(raise);
        }
        // Keep the open card when it is still there (matched by id), otherwise close.
        var openIndex = open is null ? -1 : _cards.FindIndex(c => ReferenceEquals(c, open) || c.Id.Length > 0 && c.Id == open.Id);
        _raised = openIndex;
        _previousRaised = -1;
        var front = Math.Clamp(openIndex >= 0 ? openIndex : Position, 0, Math.Max(0, _cards.Count - 1));
        _position.Jump(front);
        _front = front;
        if (openIndex >= 0)
        {
            _raises[openIndex].Jump(1);
        }
        _syncing = true;
        try
        {
            SetCurrentValue(PositionProperty, front);
            _open = openIndex >= 0 ? _cards[openIndex] : null;
            SetCurrentValue(SelectedCardProperty, _open);
        }
        finally
        {
            _syncing = false;
        }
        PseudoClasses.Set(OpenPseudoClass, openIndex >= 0);
        _stage?.SetCards(_cards);
        _stage?.Detail.Reset();
        if (openIndex >= 0 && _stage is not null)
        {
            _stage.Detail.Show(_cards[openIndex], _format, Labels, _stage.Inks);
        }
        UpdateTexts();
        UpdateHeader(animate: false);
        Relayout();
    }

    /// <summary>Applies the layout function to every card.</summary>
    private void Relayout()
    {
        if (_stage is null)
        {
            return;
        }
        var g = _stage.Geometry;
        var count = _cards.Count;
        var position = _position.Value;
        var fan = _fan.Value;
        var open = OpenAmount;
        var slots = _stage.Slots;
        for (var i = 0; i < slots.Count && i < _raises.Count; i++)
        {
            var rank = WalletStackLayout.TuckRank(i, _raised, _previousRaised);
            var pose = WalletStackLayout.Pose(i, count, g, position, fan, _lifts[i].Value, open, _raises[i].Value, rank);
            slots[i].Apply(pose, g.CardWidth);
        }
        _stage.ApplyDetail(open, open > 0.001 || _open is not null);
        _stage.InvalidateGlass();
    }

    private void UpdateTexts()
    {
        if (_title is not null)
        {
            _title.Text = Label;
        }
        if (_stage is not null)
        {
            AutomationProperties.SetName(_stage, Label);
            AutomationProperties.SetHelpText(_stage, Labels.KeyboardHint);
            var count = _cards.Count;
            foreach (var view in _stage.Views)
            {
                var card = view.Card;
                AutomationProperties.SetName(view, Labels.CardName(card, view.Index + 1, count, _format.Money(card.Balance)));
            }
        }
        if (_allCards is not null)
        {
            _allCards.Content = Labels.AllCards;
            AutomationProperties.SetName(_allCards, Labels.AllCards);
        }
        UpdateCount(animate: false);
        UpdateTabStops();
    }

    private int FrontIndex => _raised >= 0 && _open is not null ? _raised : Math.Clamp(_front, 0, Math.Max(0, _cards.Count - 1));

    private void UpdateCount(bool animate)
    {
        if (_count is null)
        {
            return;
        }
        var total = _cards.Count;
        var front = total == 0 ? 0 : FrontIndex + 1;
        _count.Set(Labels.CountBefore(total), front, _format.Integer(front), Labels.CountAfter(total), animate && IsAttached);
    }

    private bool IsAttached => VisualRoot is not null;

    private void UpdateTabStops()
    {
        if (_stage is null)
        {
            return;
        }
        var front = FrontIndex;
        foreach (var view in _stage.Views)
        {
            view.IsTabStop = view.Index == front;
            view.IsExpanded = _open is not null && view.Index == _raised;
        }
    }

    // Count out (72ms ease-out, to 0.96) and All cards in (160ms ease-out, 40ms later), or the other way round.
    private void UpdateHeader(bool animate)
    {
        var open = _open is not null;
        if (_allCards is not null)
        {
            _allCards.IsTabStop = open;
            _allCards.IsHitTestVisible = open;
            if (open)
            {
                _allCards.IsVisible = true;
            }
        }
        if (_count is not null && !open)
        {
            _count.IsVisible = true;
        }
        if (!animate || !IsAttached)
        {
            _countShown.Jump(open ? 0 : 1);
            _buttonShown.Jump(open ? 1 : 0);
            return;
        }
        var exit = MotionSpec.Tween(TimeSpan.FromMilliseconds(72), WalletStackEasings.EaseOut);
        var enter = MotionSpec.Tween(ArcMotionPresets.Fast, WalletStackEasings.EaseOut, TimeSpan.FromMilliseconds(40));
        _ = _countShown.AnimateTo(open ? 0 : 1, open ? exit : enter);
        _ = _buttonShown.AnimateTo(open ? 1 : 0, open ? enter : exit);
    }

    private void ApplyCountPresence(double value)
    {
        if (_count is null)
        {
            return;
        }
        _count.Opacity = value;
        var scale = ArcMotion.IsFull(this) ? 0.96 + 0.04 * value : 1;
        _countScale.ScaleX = _countScale.ScaleY = scale;
        _count.IsVisible = value > 0.001 || _open is null;
    }

    private void ApplyButtonPresence(double value)
    {
        if (_allCards is null)
        {
            return;
        }
        _allCards.Opacity = value;
        var scale = ArcMotion.IsFull(this) ? 0.96 + 0.04 * value : 1;
        _buttonScale.ScaleX = _buttonScale.ScaleY = scale;
        _allCards.IsVisible = value > 0.001 || _open is not null;
    }

    private void OnAllCardsClick(object? sender, RoutedEventArgs e)
    {
        var index = _raised;
        CloseCard(0, focus: false);
        FocusCard(index, NavigationMethod.Tab);
    }

    /// <summary>Opens card <paramref name="index"/>, or closes it when it is the open card (a click or Invoke).</summary>
    internal void ActivateCard(int index, bool keyboard)
    {
        if (_open is not null && index == _raised)
        {
            CloseCard(0, focus: keyboard);
        }
        else
        {
            OpenCard(index, 0);
        }
    }

    private void Step(int delta, bool focus)
    {
        if (_cards.Count == 0)
        {
            return;
        }
        var from = _open is not null ? _raised : _front;
        Navigate(from + delta, 0, focus, fromKeyboard: focus);
    }

    private void Navigate(int index, double velocity, bool focus, bool fromKeyboard)
    {
        if (_cards.Count == 0)
        {
            return;
        }
        index = Math.Clamp(index, 0, _cards.Count - 1);
        if (_open is not null)
        {
            if (index != _raised)
            {
                OpenCard(index, 0);
            }
        }
        else
        {
            SettlePosition(index, velocity);
        }
        if (focus)
        {
            FocusCard(index, NavigationMethod.Directional);
        }
        _ = fromKeyboard;
    }

    // Springs the stack to index (keeping any release velocity, in cards per second) and reports the new front card.
    private void SettlePosition(int index, double velocity)
    {
        if (_position.Target != index || _position.Value != index)
        {
            _ = MomentumProjector.SettleTo(_position, index, velocity, ArcSpring.Smooth);
        }
        if (_front != index)
        {
            SetPosition(index);
            Announce(Labels.FrontAnnouncement(_cards[index], index + 1, _cards.Count));
        }
    }

    // Records index as the front card: the property, the count, the tab stop and PositionChanged.
    private void SetPosition(int index)
    {
        var old = _front;
        _front = index;
        if (Position != index)
        {
            _syncing = true;
            try
            {
                SetCurrentValue(PositionProperty, index);
            }
            finally
            {
                _syncing = false;
            }
        }
        UpdateCount(animate: true);
        UpdateTabStops();
        if (old != index)
        {
            RaiseEvent(new WalletStackPositionChangedEventArgs(PositionChangedEvent, old, index, _cards[index]));
        }
    }

    private void OpenCard(int index, double velocity)
    {
        if (index < 0 || index >= _cards.Count || _stage is null && !IsAttached && _raises.Count == 0)
        {
            return;
        }
        var old = _open;
        var card = _cards[index];
        if (ReferenceEquals(old, card) && _raised == index)
        {
            return;
        }
        if (old is not null && _raised >= 0)
        {
            // Switch: the old card drops into the slot the new one rises from.
            _previousRaised = _raised;
            _ = _raises[_raised].AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _previousRaised = -1;
        }
        _raised = index;
        _ = _raises[index].AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth), velocity);
        SetFan(0);
        ClearHover();
        if (_position.Target != index)
        {
            _ = _position.AnimateTo(index, MotionSpec.Spring(ArcSpring.Smooth));
        }
        _syncing = true;
        try
        {
            _open = card;
            SetCurrentValue(SelectedCardProperty, card);
        }
        finally
        {
            _syncing = false;
        }
        PseudoClasses.Set(OpenPseudoClass, true);
        if (_front != index)
        {
            SetPosition(index);
        }
        UpdateTabStops();
        UpdateHeader(animate: old is null);
        if (_stage is not null)
        {
            _stage.Detail.Show(card, _format, Labels, _stage.Inks);
        }
        Announce(Labels.OpenAnnouncement(card));
        RaiseEvent(new WalletStackSelectedCardChangedEventArgs(SelectedCardChangedEvent, old, card));
        Relayout();
    }

    private void CloseCard(double velocity, bool focus)
    {
        var old = _open;
        if (old is null || _raised < 0)
        {
            return;
        }
        var index = _raised;
        _previousRaised = -1;
        _ = _raises[index].AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth), velocity);
        _syncing = true;
        try
        {
            _open = null;
            SetCurrentValue(SelectedCardProperty, null);
        }
        finally
        {
            _syncing = false;
        }
        PseudoClasses.Set(OpenPseudoClass, false);
        _stage?.Detail.Reset();
        if (_pointerInside && _hoverIsMouse)
        {
            SetFan(1);
        }
        UpdateTabStops();
        UpdateCount(animate: false);
        UpdateHeader(animate: true);
        Announce(Labels.FrontAnnouncement(_cards[index], index + 1, _cards.Count));
        RaiseEvent(new WalletStackSelectedCardChangedEventArgs(SelectedCardChangedEvent, old, null));
        if (focus)
        {
            FocusCard(index, NavigationMethod.Directional);
        }
        Relayout();
    }

    private void FocusCard(int index, NavigationMethod method)
    {
        if (_stage is null || index < 0 || index >= _stage.Views.Count)
        {
            return;
        }
        _stage.Views[index].Focus(method);
    }

    private void Announce(string text) => _announcer?.Announce(text);

    private void SetFan(double target)
    {
        if (!IsFanEnabled)
        {
            target = 0;
        }
        if (_fan.Target != target || _fan.Value != target && !_fan.IsAnimating)
        {
            _ = _fan.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not Visual source || !IsFromCard(source))
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Down:
            case Key.Right:
                Step(1, focus: true);
                e.Handled = true;
                break;
            case Key.Up:
            case Key.Left:
                Step(-1, focus: true);
                e.Handled = true;
                break;
            case Key.Home:
                Navigate(0, 0, focus: true, fromKeyboard: true);
                e.Handled = true;
                break;
            case Key.End:
                Navigate(_cards.Count - 1, 0, focus: true, fromKeyboard: true);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                if (_open is not null)
                {
                    CloseCard(0, focus: true);
                }
                else if (_cards.Count > 0)
                {
                    var front = _front;
                    OpenCard(front, 0);
                    FocusCard(front, NavigationMethod.Directional);
                }
                e.Handled = true;
                break;
            case Key.Escape:
                if (_open is not null)
                {
                    CloseCard(0, focus: true);
                    e.Handled = true;
                }
                break;
        }
    }

    private static bool IsFromCard(Visual source)
    {
        for (Visual? v = source; v is not null; v = v.GetVisualParent())
        {
            if (v is WalletStackCardView)
            {
                return true;
            }
            if (v is WalletStack)
            {
                return false;
            }
        }
        return false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WalletStackAutomationPeer(this);

    private sealed class WalletStackAutomationPeer : ControlAutomationPeer
    {
        public WalletStackAutomationPeer(WalletStack owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((WalletStack)Owner).Label;
    }
}

/// <summary>Easing curves of the wallet stack that the shared presets do not cover.</summary>
internal static class WalletStackEasings
{
    /// <summary>CSS <c>ease-out</c>.</summary>
    public static readonly Avalonia.Animation.Easings.Easing EaseOut = new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1);
}
