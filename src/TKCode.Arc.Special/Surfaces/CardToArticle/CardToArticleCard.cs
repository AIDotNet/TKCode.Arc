using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// A story card in the grid: a button that is the picture (<see cref="CardToArticleHero"/>) on a rounded muted surface.
/// A mouse lifts it to 1.012 with the raised shadow and a press settles it to 0.985 (theme transitions, 240ms
/// standard); touch shows no hover. While its article is open the card's pieces are hidden by the shared-element flight
/// and its slot keeps its place.
/// </summary>
[TemplatePart(HeroPart, typeof(CardToArticleHero))]
[PseudoClasses(HoverPseudoClass, AwayPseudoClass)]
internal sealed class CardToArticleCard : Avalonia.Controls.Button
{
    public const string HeroPart = "PART_Hero";
    public const string HoverPseudoClass = ":hover";
    public const string AwayPseudoClass = ":away";

    public static readonly StyledProperty<CardToArticleItem?> ItemProperty =
        CardToArticleHero.ItemProperty.AddOwner<CardToArticleCard>();

    public static readonly StyledProperty<double> AspectRatioProperty =
        CardToArticleHero.AspectRatioProperty.AddOwner<CardToArticleCard>();

    public CardToArticleItem? Item { get => GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }

    private bool _keyboard;
    private bool _expanded;

    /// <summary>Raised when the card is activated; the argument says whether it was the keyboard.</summary>
    public event Action<CardToArticleCard, bool>? Activated;

    public CardToArticleHero? Hero { get; private set; }

    /// <summary>True while this card's article is open (the slot stays, the card is away).</summary>
    public bool IsAway
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
            {
                return;
            }
            _expanded = value;
            PseudoClasses.Set(AwayPseudoClass, value);
            if (value)
            {
                PseudoClasses.Set(HoverPseudoClass, false);
            }
        }
    }

    protected override Type StyleKeyOverride => typeof(CardToArticleCard);

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Hero = e.NameScope.Find<CardToArticleHero>(HeroPart);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Hover lifts are for pointers that hover (Arc: hover media query); a finger shows none.
        if (e.Pointer.Type != PointerType.Touch && !_expanded)
        {
            PseudoClasses.Set(HoverPseudoClass, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _keyboard = false;
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            _keyboard = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnClick()
    {
        base.OnClick();
        var keyboard = _keyboard;
        _keyboard = false;
        Activated?.Invoke(this, keyboard);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    /// <summary>A button that opens a dialog: it reports whether its article is expanded (Arc: aria-expanded).</summary>
    private sealed class CardPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public CardPeer(CardToArticleCard owner) : base(owner)
        {
        }

        private CardToArticleCard Card => (CardToArticleCard)Owner;

        public ExpandCollapseState ExpandCollapseState => Card._expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Card._expanded)
            {
                Card.Activated?.Invoke(Card, false);
            }
        }

        public void Collapse()
        {
        }
    }
}
