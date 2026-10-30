using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How <see cref="RadioCards"/> places its cards.</summary>
public enum RadioCardsLayout
{
    /// <summary>Responsive columns no narrower than <see cref="RadioCards.MinColumnWidth"/>.</summary>
    Grid,

    /// <summary>Full-width rows with the meta at the end.</summary>
    List
}

/// <summary>
/// Arc radio cards: selectable option cards for choices that need more than a label (plans, shipping speeds, regions).
/// One selection ring springs from card to card, so the eye follows the change, and a 6px dot pops into the chosen
/// card's indicator. Behaves as a radio group: one tab stop, arrows (mirrored in right-to-left) move and select,
/// Home / End jump to the ends and Enter / Space select. Disabled cards are skipped and show their reason.
/// Cards come from <see cref="ItemsControl.ItemsSource"/> or inline <see cref="RadioCardOption"/> items;
/// <see cref="SelectingItemsControl.SelectedValue"/> (two-way) is the selected option's value.
/// </summary>
[TemplatePart(RingPart, typeof(GlidingIndicator))]
public class RadioCards : SelectingItemsControl
{
    public const string RingPart = "PART_Ring";

    /// <summary>Arc's ring glide: <c>physical(0.42, 0.14)</c>, a touch less bouncy than the morph preset (0.42 / 0.16).</summary>
    internal static readonly SpringSpec RingGlide = SpringSpec.FromVisual(0.42, 0.14);

    #region 公共属性定义

    /// <summary>Card placement: Grid (responsive columns, the default) or List (full-width rows with the meta at the end).</summary>
    public static readonly StyledProperty<RadioCardsLayout> LayoutProperty =
        AvaloniaProperty.Register<RadioCards, RadioCardsLayout>(nameof(Layout));

    /// <summary>Narrowest a grid column may get before the grid drops a column (Arc default 180).</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<RadioCards, double>(nameof(MinColumnWidth), 180);

    /// <summary>A choice is required (reported to assistive technology).</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<RadioCards, bool>(nameof(IsRequired));

    /// <inheritdoc cref="LayoutProperty"/>
    public RadioCardsLayout Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public bool IsRequired
    {
        get => GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    #endregion

    #region 内部属性定义

    internal static readonly StyledProperty<double> GridGapProperty =
        AvaloniaProperty.Register<RadioCards, double>(nameof(GridGap), 10);

    internal static readonly StyledProperty<double> ListGapProperty =
        AvaloniaProperty.Register<RadioCards, double>(nameof(ListGap), 8);

    internal double GridGap { get => GetValue(GridGapProperty); set => SetValue(GridGapProperty, value); }
    internal double ListGap { get => GetValue(ListGapProperty); set => SetValue(ListGapProperty, value); }

    #endregion

    private readonly TogglesGroupHost _host;

    static RadioCards()
    {
        FocusableProperty.OverrideDefaultValue<RadioCards>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<RadioCards>(KeyboardNavigationMode.Once);
        AffectsMeasure<RadioCards>(LayoutProperty, MinColumnWidthProperty);
    }

    public RadioCards()
    {
        _host = new TogglesGroupHost(this, flipInRtl: true, ApplyOption);
        SelectedValueBinding = TogglesValueBinding.Create();
        SelectionChanged += (_, _) => _host.OnSelectionChanged();
    }

    protected override Type StyleKeyOverride => typeof(RadioCards);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<RadioCard>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new RadioCard();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not RadioCard card)
        {
            return;
        }
        card.IsListLayout = Layout == RadioCardsLayout.List;
        ApplyOption(card, item);
    }

    private static void ApplyOption(Control container, object? item)
    {
        if (container is RadioCard card && item is RadioCardOption option)
        {
            card.Content = option.Label ?? option.Value?.ToString();
            card.Description = option.Description;
            card.Meta = option.Meta;
            card.Icon = option.Icon switch
            {
                // Re-applied when any property of the option changes: keep the glyph that already shows this geometry.
                Geometry geometry when card.Icon is ArcGlyph shown && ReferenceEquals(shown.Data, geometry) => shown,
                Geometry geometry => new ArcGlyph { Data = geometry, Size = 18 },
                var icon => icon
            };
            card.DisabledReason = option.DisabledReason;
            card.IsEnabled = !option.IsDisabled;
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        _host.UpdateTabStop();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is RadioCard card)
        {
            card.ClearValue(ContentControl.ContentProperty);
            card.ClearValue(RadioCard.DescriptionProperty);
            card.ClearValue(RadioCard.MetaProperty);
            card.ClearValue(RadioCard.IconProperty);
            card.ClearValue(RadioCard.DisabledReasonProperty);
            card.ClearValue(IsEnabledProperty);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // The ring's 1.5px stroke is centered half a stroke inside the card, like a CSS border.
        _host.AttachIndicator(e.NameScope.Find<GlidingIndicator>(RingPart), inset: 0.75, fadeInFirst: true, RingGlide);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutProperty)
        {
            for (var index = 0; index < ItemCount; index++)
            {
                if (ContainerFromIndex(index) is RadioCard card)
                {
                    card.IsListLayout = Layout == RadioCardsLayout.List;
                }
            }
            ItemsPanelRoot?.InvalidateMeasure();
        }
        else if (change.Property == MinColumnWidthProperty || change.Property == GridGapProperty || change.Property == ListGapProperty)
        {
            ItemsPanelRoot?.InvalidateMeasure();
        }
        else if (change.Property == IsRequiredProperty)
        {
            AutomationProperties.SetIsRequiredForForm(this, IsRequired);
        }
        else if (change.Property == IsEnabledProperty)
        {
            _host.UpdateTabStop();
        }
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new TogglesGroupAutomationPeer(this);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_host.HandleKey(e))
        {
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _host.UpdateTabStop();
    }
}
