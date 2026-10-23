using System.Collections.Specialized;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The sources of an AI answer (Arc answer's sources row): a quiet disclosure button, the first four site marks overlapping,
/// "8 sources" and a chevron, that opens the sources as small cards. Wide, the cards form a two-column grid; when the row
/// (with the room it may <see cref="Bleed"/> into) is 576px or narrower, they form one line of fixed-width cards that
/// scrolls sideways, reaches the edges of the page, snaps a card's start to the column after a scroll and fades out on the
/// right while more cards follow.
/// <code>
/// &lt;arc:AiSourcesRow Sources="{Binding Sources}" LabelText="8 sources" CardName="{Binding SourceCardName}" /&gt;
/// </code>
/// <para>
/// Lighting (the answer's three-way highlight): pointing at a card or focusing it with the keyboard raises
/// <see cref="CardActivatedEvent"/> with its source number (0 when it is left), so the owner can light the sentences that
/// cite it; the owner sets <see cref="ActiveNumber"/> while a citation is active, which lights that card and, in the narrow
/// row, scrolls it into view. Pressing a card raises <see cref="CardInvokedEvent"/> with its source.
/// </para>
/// <para>
/// The list follows the source collection while sources stream in: the stack and the cards grow, and cards that arrive
/// while the row is open fade in 40ms + 20ms per card after one another (at most 160ms). The cards open and close on
/// <see cref="AiCollapsible"/> (height spring and fade); closed, they leave the tree, so they cost nothing and take no
/// focus, and while closing they are already out of reach (Arc's <c>inert</c>). Every word comes from the owner
/// (<see cref="LabelText"/>, <see cref="CardName"/>, <see cref="ListName"/>).
/// </para>
/// </summary>
[TemplatePart(TogglePart, typeof(AiSourcesToggle))]
[TemplatePart(BodyPart, typeof(AiCollapsible))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(CardsPart, typeof(AiSourceCardPanel))]
[PseudoClasses(ExpandedPseudoClass, NarrowPseudoClass)]
internal sealed partial class AiSourcesRow : TemplatedControl
{
    public const string TogglePart = "PART_Toggle";
    public const string BodyPart = "PART_Body";
    public const string ScrollerPart = "PART_Scroller";
    public const string CardsPart = "PART_Cards";

    /// <summary>The cards are open.</summary>
    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>The cards form the narrow scrolling row.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>At or below this width (the row plus its bleed on both sides, Arc's 36rem container query) the cards scroll in a row.</summary>
    public const double NarrowWidth = 576;

    /// <summary>The first arriving card waits this long ...</summary>
    internal static readonly TimeSpan ArriveDelay = TimeSpan.FromMilliseconds(40);

    /// <summary>... and each later one this much more ...</summary>
    internal static readonly TimeSpan ArriveStagger = TimeSpan.FromMilliseconds(20);

    /// <summary>... up to this many steps (Arc's <c>40 + 20 · min(i, 6)</c>).</summary>
    internal const int ArriveMaxSteps = 6;

    #region Properties

    /// <summary>The sources, numbered by their <see cref="AiSource.Index"/>; a collection that notifies is followed.</summary>
    public static readonly StyledProperty<IList<AiSource>?> SourcesProperty =
        AvaloniaProperty.Register<AiSourcesRow, IList<AiSource>?>(nameof(Sources));

    /// <summary>Whether the cards are open (two-way); the button toggles it.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiSourcesRow, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The button's label, formatted by the owner ("8 sources").</summary>
    public static readonly StyledProperty<string?> LabelTextProperty =
        AvaloniaProperty.Register<AiSourcesRow, string?>(nameof(LabelText));

    /// <summary>The number of the source whose citation is active (0 = none): its card is lit and, when narrow, scrolled into view.</summary>
    public static readonly StyledProperty<int> ActiveNumberProperty =
        AvaloniaProperty.Register<AiSourcesRow, int>(nameof(ActiveNumber));

    /// <summary>Names a card for automation from its number, title and domain ("Source 1: title, domain"); null reads the title.</summary>
    public static readonly StyledProperty<Func<int, string, string?, string>?> CardNameProperty =
        AvaloniaProperty.Register<AiSourcesRow, Func<int, string, string?, string>?>(nameof(CardName));

    /// <summary>The accessible name of the list of cards ("Sources"); null leaves it unnamed.</summary>
    public static readonly StyledProperty<string?> ListNameProperty =
        AvaloniaProperty.Register<AiSourcesRow, string?>(nameof(ListName));

    /// <summary>
    /// How far the narrow row may reach past the row on each side (the column's side padding): the cards scroll from edge to
    /// edge of the page while a card's start snaps to the column. It also counts toward <see cref="NarrowWidth"/>.
    /// </summary>
    public static readonly StyledProperty<double> BleedProperty =
        AvaloniaProperty.Register<AiSourcesRow, double>(nameof(Bleed));

    /// <inheritdoc cref="SourcesProperty"/>
    public IList<AiSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="LabelTextProperty"/>
    public string? LabelText { get => GetValue(LabelTextProperty); set => SetValue(LabelTextProperty, value); }

    /// <inheritdoc cref="ActiveNumberProperty"/>
    public int ActiveNumber { get => GetValue(ActiveNumberProperty); set => SetValue(ActiveNumberProperty, value); }

    /// <inheritdoc cref="CardNameProperty"/>
    public Func<int, string, string?, string>? CardName { get => GetValue(CardNameProperty); set => SetValue(CardNameProperty, value); }

    /// <inheritdoc cref="ListNameProperty"/>
    public string? ListName { get => GetValue(ListNameProperty); set => SetValue(ListNameProperty, value); }

    /// <inheritdoc cref="BleedProperty"/>
    public double Bleed { get => GetValue(BleedProperty); set => SetValue(BleedProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when a card is pointed at or focused with the keyboard (its source number), and when none is any more (0).</summary>
    public static readonly RoutedEvent<AiSourceCardEventArgs> CardActivatedEvent =
        RoutedEvent.Register<AiSourcesRow, AiSourceCardEventArgs>(nameof(CardActivated), RoutingStrategies.Bubble);

    /// <summary>Raised when a card is pressed (click, Enter, Space): open its source.</summary>
    public static readonly RoutedEvent<AiSourceCardEventArgs> CardInvokedEvent =
        RoutedEvent.Register<AiSourcesRow, AiSourceCardEventArgs>(nameof(CardInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="CardActivatedEvent"/>
    public event EventHandler<AiSourceCardEventArgs>? CardActivated { add => AddHandler(CardActivatedEvent, value); remove => RemoveHandler(CardActivatedEvent, value); }

    /// <inheritdoc cref="CardInvokedEvent"/>
    public event EventHandler<AiSourceCardEventArgs>? CardInvoked { add => AddHandler(CardInvokedEvent, value); remove => RemoveHandler(CardInvokedEvent, value); }

    #endregion

    private readonly List<AiSourceCard> _cards = new();
    private AiSourcesToggle? _toggle;
    private AiCollapsible? _body;
    private ScrollViewer? _scroller;
    private AiSourceCardPanel? _panel;
    private INotifyCollectionChanged? _observed;
    private bool _cardsBuilt;
    private bool _isNarrow;
    private int _activated;
    private int _arrivals;
    private bool _arrivalResetPosted;

    /// <summary>The disclosure button.</summary>
    internal AiSourcesToggle? Toggle => _toggle;

    /// <summary>The collapsible body.</summary>
    internal AiCollapsible? Body => _body;

    /// <summary>The scroller around the cards.</summary>
    internal ScrollViewer? Scroller => _scroller;

    /// <summary>The panel of cards.</summary>
    internal AiSourceCardPanel? Panel => _panel;

    /// <summary>The cards, in source order (empty until the row first opens).</summary>
    internal IReadOnlyList<AiSourceCard> Cards => _cards;

    /// <summary>Whether the cards form the narrow scrolling row.</summary>
    internal bool IsNarrow => _isNarrow;

    /// <summary>The source number last raised with <see cref="CardActivatedEvent"/>.</summary>
    internal int ActivatedNumber => _activated;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _toggle = e.NameScope.Find<AiSourcesToggle>(TogglePart);
        _body = e.NameScope.Find<AiCollapsible>(BodyPart);
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        _panel = e.NameScope.Find<AiSourceCardPanel>(CardsPart);
        if (_toggle is not null)
        {
            _toggle.Click += OnToggleClick;
        }
        AttachScroller();
        // Cards built for an earlier template move to the new panel.
        if (_panel is not null)
        {
            foreach (var card in _cards)
            {
                _panel.Children.Add(card);
            }
            AutomationProperties.SetControlTypeOverride(_panel, AutomationControlType.List);
            UpdateListName();
        }
        ApplyMode();
        ApplyExpanded();
    }

    private void DetachParts()
    {
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
        }
        DetachScroller();
        _panel?.Children.Clear();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(IsExpandedProperty, !IsExpanded);
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourcesProperty)
        {
            Observe(this.IsAttachedToVisualTree() ? change.GetNewValue<IList<AiSource>?>() : null);
            // A new list is a new answer's sources: new cards, arriving like sources that stream in.
            while (_cards.Count > 0)
            {
                RemoveCard(_cards[^1]);
            }
            Rebuild(animate: true);
        }
        else if (change.Property == IsExpandedProperty)
        {
            ApplyExpanded();
        }
        else if (change.Property == ActiveNumberProperty)
        {
            UpdateLit();
            RevealActive();
        }
        else if (change.Property == CardNameProperty)
        {
            var name = change.GetNewValue<Func<int, string, string?, string>?>();
            foreach (var card in _cards)
            {
                card.CardName = name;
            }
        }
        else if (change.Property == ListNameProperty)
        {
            UpdateListName();
        }
        else if (change.Property == BleedProperty)
        {
            UpdateNarrow(Bounds.Width);
            ApplyMode();
        }
    }

    private void UpdateListName()
    {
        if (_panel is null)
        {
            return;
        }
        if (ListName is { } name)
        {
            AutomationProperties.SetName(_panel, name);
        }
        else
        {
            _panel.ClearValue(AutomationProperties.NameProperty);
        }
    }

    #region Open and close

    /// <summary>
    /// Opens or closes the body. The cards are built the first time it opens (a closed row never pays for them). Closing
    /// puts them out of reach at once (no hit testing, no Tab), as Arc's panel turns inert before it shrinks; focus inside
    /// moves to the button first so it is not lost when the cards leave.
    /// </summary>
    private void ApplyExpanded()
    {
        var open = IsExpanded;
        PseudoClasses.Set(ExpandedPseudoClass, open);
        if (open)
        {
            EnsureCards();
        }
        else if (_scroller is not null && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused
                 && _scroller.IsVisualAncestorOf(focused))
        {
            var keyboard = focused is AiSourceCard { IsKeyboardFocused: true };
            _toggle?.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }
        if (_scroller is not null)
        {
            _scroller.IsHitTestVisible = open;
            KeyboardNavigation.SetTabNavigation(_scroller, open ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
        }
        if (_body is not null)
        {
            _body.IsExpanded = open;
        }
        if (!open)
        {
            StopScroll();
        }
    }

    #endregion

    #region Cards

    private void Observe(IList<AiSource>? sources)
    {
        var next = sources as INotifyCollectionChanged;
        if (ReferenceEquals(_observed, next))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourcesChanged;
        }
        _observed = next;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnSourcesChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Sources);
        // The list may have changed while the row was away.
        if (_cardsBuilt)
        {
            Rebuild(animate: false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
        StopScroll();
        Raise(0, null);
    }

    private void EnsureCards()
    {
        if (_cardsBuilt)
        {
            return;
        }
        _cardsBuilt = true;
        // The cards present when the row opens come in with the body's fade, not one by one.
        Rebuild(animate: false);
        // A citation may already be active: its card shows in the narrow row once the cards are laid out.
        RevealActiveAfterLayout();
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_cardsBuilt)
        {
            return;
        }
        var sources = Sources;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is not null && e.NewStartingIndex >= 0 && sources is not null:
            {
                for (var i = 0; i < e.NewItems.Count; i++)
                {
                    var index = Math.Min(e.NewStartingIndex + i, _cards.Count);
                    var card = CreateCard((AiSource)e.NewItems[i]!);
                    _cards.Insert(index, card);
                    _panel?.Children.Insert(index, card);
                    Arrive(card);
                }
                break;
            }
            case NotifyCollectionChangedAction.Remove when e.OldItems is not null && e.OldStartingIndex >= 0:
            {
                for (var i = 0; i < e.OldItems.Count && e.OldStartingIndex < _cards.Count; i++)
                {
                    RemoveCard(_cards[e.OldStartingIndex]);
                }
                break;
            }
            case NotifyCollectionChangedAction.Replace when e.NewItems is not null && e.NewStartingIndex >= 0:
            {
                for (var i = 0; i < e.NewItems.Count && e.NewStartingIndex + i < _cards.Count; i++)
                {
                    _cards[e.NewStartingIndex + i].Source = (AiSource)e.NewItems[i]!;
                }
                break;
            }
            default:
                Rebuild(animate: true);
                break;
        }
        UpdateLit();
    }

    /// <summary>
    /// Makes the cards match the list by position, reusing cards; new cards at the end arrive with the stagger when
    /// <paramref name="animate"/> and the row is open on screen.
    /// </summary>
    private void Rebuild(bool animate)
    {
        if (!_cardsBuilt)
        {
            return;
        }
        var sources = Sources;
        var count = sources?.Count ?? 0;
        while (_cards.Count > count)
        {
            RemoveCard(_cards[^1]);
        }
        for (var i = 0; i < count; i++)
        {
            if (i < _cards.Count)
            {
                _cards[i].Source = sources![i];
                continue;
            }
            var card = CreateCard(sources![i]);
            _cards.Add(card);
            _panel?.Children.Add(card);
            if (animate)
            {
                Arrive(card);
            }
        }
        UpdateLit();
    }

    private AiSourceCard CreateCard(AiSource source)
    {
        var card = new AiSourceCard { Source = source, CardName = CardName, IsNarrow = _isNarrow };
        card.ActivityChanged += OnCardActivityChanged;
        card.NumberChanged += OnCardNumberChanged;
        return card;
    }

    private void RemoveCard(AiSourceCard card)
    {
        _cards.Remove(card);
        _panel?.Children.Remove(card);
        card.ActivityChanged -= OnCardActivityChanged;
        card.NumberChanged -= OnCardNumberChanged;
        OnCardActivityChanged(card, EventArgs.Empty);
    }

    /// <summary>
    /// Fades in a card that arrived while the row is open on screen. Cards arriving in the same turn (a batch of sources)
    /// wait 40ms + 20ms per earlier card of the batch, at most 160ms (Arc's <c>40 + 20 · min(i, 6)</c>).
    /// </summary>
    private void Arrive(AiSourceCard card)
    {
        if (!IsExpanded || _body is not { IsMounted: true } || !IsLoaded)
        {
            return;
        }
        var step = Math.Min(_arrivals++, ArriveMaxSteps);
        if (!_arrivalResetPosted)
        {
            _arrivalResetPosted = true;
            Dispatcher.UIThread.Post(() =>
            {
                _arrivals = 0;
                _arrivalResetPosted = false;
            }, DispatcherPriority.Background);
        }
        card.Arrive(ArriveDelay + ArriveStagger * step);
    }

    #endregion

    #region Lighting

    private void OnCardNumberChanged(object? sender, EventArgs e)
    {
        UpdateLit();
        OnCardActivityChanged(sender, e);
    }

    /// <summary>The card whose source number is <see cref="ActiveNumber"/> is lit; every other card is not.</summary>
    private void UpdateLit()
    {
        var active = ActiveNumber;
        foreach (var card in _cards)
        {
            card.IsLit = active != 0 && card.Number == active;
        }
    }

    /// <summary>
    /// The active card is the one under the pointer, else the one with keyboard focus; its number is raised when it changes
    /// (0 when there is none), so the owner lights the sentences that cite it.
    /// </summary>
    private void OnCardActivityChanged(object? sender, EventArgs e)
    {
        AiSourceCard? active = null;
        foreach (var card in _cards)
        {
            if (card.IsHovered)
            {
                active = card;
                break;
            }
        }
        if (active is null)
        {
            foreach (var card in _cards)
            {
                if (card.IsKeyboardFocused)
                {
                    active = card;
                    break;
                }
            }
        }
        Raise(active?.Number ?? 0, active);
    }

    private void Raise(int number, AiSourceCard? card)
    {
        if (number == _activated)
        {
            return;
        }
        _activated = number;
        // Raised from the card (it bubbles through the row) so the source of the event is the card; none from the row.
        var target = card is not null && card.IsAttachedToVisualTree() ? (Interactive)card : this;
        target.RaiseEvent(new AiSourceCardEventArgs(CardActivatedEvent, target, number, card?.Source));
    }

    #endregion
}
