using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A grid of feature cards lit by a spotlight: a soft accent light follows the pointer across the whole grid, warms the
/// cards under it and makes their borders glow, neighbours included, as if one lamp moved over the page. Keyboard focus
/// carries the light to the focused card. Each card has an icon, a title, a sentence and an optional badge.
/// <code>
/// &lt;arc:SpotlightGrid Title="Everything in one place"&gt;
///     &lt;arc:SpotlightGridItem Title="Instant search" Description="..." Icon="{x:Static arc:ArcIcons.Search}" /&gt;
/// &lt;/arc:SpotlightGrid&gt;
/// </code>
/// </summary>
[TemplatePart(SurfacePart, typeof(SpotlightGridSurface))]
[PseudoClasses(CompactPseudoClass, NoHeaderPseudoClass)]
public class SpotlightGrid : TemplatedControl
{
    public const string SurfacePart = "PART_Surface";
    public const string CompactPseudoClass = ":compact";
    public const string NoHeaderPseudoClass = ":no-header";

    /// <summary>The small line above the title; null leaves it out.</summary>
    public static readonly StyledProperty<string?> EyebrowProperty =
        AvaloniaProperty.Register<SpotlightGrid, string?>(nameof(Eyebrow), "Features");

    /// <summary>The section's heading; null leaves it out (with the eyebrow and description, the grid stands alone).</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SpotlightGrid, string?>(nameof(Title), "Built for the way teams actually work");

    /// <summary>The paragraph under the heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SpotlightGrid, string?>(nameof(Description),
            "Fast where it matters, quiet where it doesn't. Move across the grid to look closer.");

    /// <summary>Whether the invented sample features show while <see cref="Items"/> is empty.</summary>
    public static readonly StyledProperty<bool> ShowsSampleItemsProperty =
        AvaloniaProperty.Register<SpotlightGrid, bool>(nameof(ShowsSampleItems), true);

    /// <summary>The narrowest a column gets before the grid drops a column (260).</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<SpotlightGrid, double>(nameof(MinColumnWidth), 260);

    /// <summary>The most columns the grid uses (3).</summary>
    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<SpotlightGrid, int>(nameof(MaxColumns), 3);

    /// <summary>Whether the spotlight follows the pointer and the focus; false leaves plain cards.</summary>
    public static readonly StyledProperty<bool> IsSpotlightEnabledProperty =
        AvaloniaProperty.Register<SpotlightGrid, bool>(nameof(IsSpotlightEnabled), true);

    /// <summary>Below this width of the block (720) the heading and the paddings get smaller.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<SpotlightGrid, double>(nameof(CompactWidth), 720);

    /// <summary>Raised when a card is pressed (click, tap, Enter or Space).</summary>
    public static readonly RoutedEvent<SpotlightGridItemEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<SpotlightGrid, SpotlightGridItemEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    private static readonly SpotlightGridItem[] s_samples = SpotlightGridSamples.Create();
    private readonly AvaloniaList<SpotlightGridItem> _items = new();
    private SpotlightGridSurface? _surface;

    public SpotlightGrid()
    {
        _items.CollectionChanged += OnItemsChanged;
    }

    /// <summary>The feature cards, in order. While it is empty the grid shows invented samples (see <see cref="ShowsSampleItems"/>).</summary>
    [Content]
    public AvaloniaList<SpotlightGridItem> Items => _items;

    /// <summary>The cards shown: <see cref="Items"/>, or the samples while that is empty.</summary>
    public IReadOnlyList<SpotlightGridItem> EffectiveItems => _items.Count > 0 || !ShowsSampleItems ? _items : s_samples;

    /// <inheritdoc cref="EyebrowProperty"/>
    public string? Eyebrow { get => GetValue(EyebrowProperty); set => SetValue(EyebrowProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ShowsSampleItemsProperty"/>
    public bool ShowsSampleItems { get => GetValue(ShowsSampleItemsProperty); set => SetValue(ShowsSampleItemsProperty, value); }

    /// <inheritdoc cref="MinColumnWidthProperty"/>
    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    /// <inheritdoc cref="MaxColumnsProperty"/>
    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    /// <inheritdoc cref="IsSpotlightEnabledProperty"/>
    public bool IsSpotlightEnabled { get => GetValue(IsSpotlightEnabledProperty); set => SetValue(IsSpotlightEnabledProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<SpotlightGridItemEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    internal SpotlightGridSurface? Surface => _surface;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _surface?.SetCards([]);
        _surface = e.NameScope.Find<SpotlightGridSurface>(SurfacePart);
        RebuildCards();
        UpdateHeader();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowsSampleItemsProperty)
        {
            RebuildCards();
        }
        else if (change.Property == TitleProperty || change.Property == EyebrowProperty || change.Property == DescriptionProperty)
        {
            UpdateHeader();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, Bounds.Width > 0 && Bounds.Width < CompactWidth);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _surface is null || e.Source is not SpotlightGridCard current)
        {
            return;
        }
        var cards = _surface.Cards;
        var index = cards.ToList().IndexOf(current);
        var columns = Math.Max(1, _surface.Columns);
        var target = e.Key switch
        {
            Key.Right => index + 1,
            Key.Left => index - 1,
            Key.Down => index + columns,
            Key.Up => index - columns,
            Key.Home => 0,
            Key.End => cards.Count - 1,
            _ => -2
        };
        if (target == -2)
        {
            return;
        }
        e.Handled = true;
        if (target >= 0 && target < cards.Count && target != index)
        {
            cards[target].Focus(NavigationMethod.Directional);
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildCards();

    private void RebuildCards()
    {
        if (_surface is null)
        {
            return;
        }
        foreach (var old in _surface.Cards)
        {
            old.Invoked -= OnCardInvoked;
        }
        var cards = EffectiveItems.Select(item => new SpotlightGridCard(item)).ToList();
        foreach (var card in cards)
        {
            card.Invoked += OnCardInvoked;
        }
        _surface.SetCards(cards);
    }

    private void UpdateHeader()
        => PseudoClasses.Set(NoHeaderPseudoClass,
            string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Eyebrow) && string.IsNullOrEmpty(Description));

    private void OnCardInvoked(object? sender, EventArgs e)
    {
        if (sender is SpotlightGridCard card)
        {
            RaiseEvent(new SpotlightGridItemEventArgs(ItemInvokedEvent, card.Item));
        }
    }
}
