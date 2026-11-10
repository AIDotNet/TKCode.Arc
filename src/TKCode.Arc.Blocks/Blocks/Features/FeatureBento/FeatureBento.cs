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
/// A bento grid of features: cards of different sizes packed into six columns, each with a small live drawing (a chart
/// that keeps drawing, dots circling a hub, a terminal typing, bars, devices passing packets, a scanning shield), a title
/// and a sentence. The drawings move only while on screen and at full motion. Below 860 the grid has two columns, below
/// 520 one.
/// <code>
/// &lt;arc:FeatureBento Title="One platform, every stage"&gt;
///     &lt;arc:FeatureBentoItem Title="Live metrics" Art="Pulse" Size="Large" Description="..." /&gt;
/// &lt;/arc:FeatureBento&gt;
/// </code>
/// </summary>
[TemplatePart(PanelPart, typeof(FeatureBentoPanel))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, NoHeaderPseudoClass)]
public class FeatureBento : TemplatedControl
{
    public const string PanelPart = "PART_Panel";
    public const string CompactPseudoClass = ":compact";
    public const string NarrowPseudoClass = ":narrow";
    public const string NoHeaderPseudoClass = ":no-header";

    /// <summary>The small line above the title; null leaves it out.</summary>
    public static readonly StyledProperty<string?> EyebrowProperty =
        AvaloniaProperty.Register<FeatureBento, string?>(nameof(Eyebrow), "Platform");

    /// <summary>The section's heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FeatureBento, string?>(nameof(Title), "Everything a release needs, in one place");

    /// <summary>The paragraph under the heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FeatureBento, string?>(nameof(Description),
            "From the first commit to the last metric, every step lives on the same platform and talks to the next one.");

    /// <summary>Whether the invented sample cards show while <see cref="Items"/> is empty.</summary>
    public static readonly StyledProperty<bool> ShowsSampleItemsProperty =
        AvaloniaProperty.Register<FeatureBento, bool>(nameof(ShowsSampleItems), true);

    /// <summary>The terminal line of the <see cref="FeatureBentoArt.Typing"/> drawing.</summary>
    public static readonly StyledProperty<string> CommandTextProperty =
        AvaloniaProperty.Register<FeatureBento, string>(nameof(CommandText), "$ ship deploy --prod");

    /// <summary>The answer the <see cref="FeatureBentoArt.Typing"/> drawing shows after the command.</summary>
    public static readonly StyledProperty<string> ResultTextProperty =
        AvaloniaProperty.Register<FeatureBento, string>(nameof(ResultText), "✓ live in 38s");

    /// <summary>Below this width of the block (860) the grid has two columns.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<FeatureBento, double>(nameof(CompactWidth), 860);

    /// <summary>Below this width of the block (520) the grid has one column.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<FeatureBento, double>(nameof(NarrowWidth), 520);

    /// <summary>Raised when a card is pressed (click, tap, Enter or Space).</summary>
    public static readonly RoutedEvent<FeatureBentoItemEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<FeatureBento, FeatureBentoItemEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    private static readonly FeatureBentoItem[] s_samples = FeatureBentoSamples.Create();
    private readonly AvaloniaList<FeatureBentoItem> _items = new();
    private readonly List<FeatureBentoCard> _cards = new();
    private FeatureBentoPanel? _panel;

    public FeatureBento()
    {
        _items.CollectionChanged += OnItemsChanged;
    }

    /// <summary>The cards, in order. While it is empty the grid shows invented samples (see <see cref="ShowsSampleItems"/>).</summary>
    [Content]
    public AvaloniaList<FeatureBentoItem> Items => _items;

    /// <summary>The cards shown: <see cref="Items"/>, or the samples while that is empty.</summary>
    public IReadOnlyList<FeatureBentoItem> EffectiveItems => _items.Count > 0 || !ShowsSampleItems ? _items : s_samples;

    /// <inheritdoc cref="EyebrowProperty"/>
    public string? Eyebrow { get => GetValue(EyebrowProperty); set => SetValue(EyebrowProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ShowsSampleItemsProperty"/>
    public bool ShowsSampleItems { get => GetValue(ShowsSampleItemsProperty); set => SetValue(ShowsSampleItemsProperty, value); }

    /// <inheritdoc cref="CommandTextProperty"/>
    public string CommandText { get => GetValue(CommandTextProperty); set => SetValue(CommandTextProperty, value); }

    /// <inheritdoc cref="ResultTextProperty"/>
    public string ResultText { get => GetValue(ResultTextProperty); set => SetValue(ResultTextProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="NarrowWidthProperty"/>
    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<FeatureBentoItemEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    internal FeatureBentoPanel? Panel => _panel;

    internal IReadOnlyList<FeatureBentoCard> Cards => _cards;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        _panel = e.NameScope.Find<FeatureBentoPanel>(PanelPart);
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
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty || change.Property == NarrowWidthProperty)
        {
            var width = Bounds.Width;
            PseudoClasses.Set(CompactPseudoClass, width > 0 && width < CompactWidth);
            PseudoClasses.Set(NarrowPseudoClass, width > 0 && width < NarrowWidth);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not FeatureBentoCard current)
        {
            return;
        }
        // Reading order is the packing order; arrows walk it like a list.
        var index = _cards.IndexOf(current);
        var target = e.Key switch
        {
            Key.Right or Key.Down => index + 1,
            Key.Left or Key.Up => index - 1,
            Key.Home => 0,
            Key.End => _cards.Count - 1,
            _ => -2
        };
        if (target == -2)
        {
            return;
        }
        e.Handled = true;
        if (target >= 0 && target < _cards.Count && target != index)
        {
            _cards[target].Focus(NavigationMethod.Directional);
            _cards[target].BringIntoView();
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildCards();

    private void RebuildCards()
    {
        foreach (var card in _cards)
        {
            card.Invoked -= OnCardInvoked;
        }
        _cards.Clear();
        if (_panel is null)
        {
            return;
        }
        _panel.Children.Clear();
        foreach (var item in EffectiveItems)
        {
            var card = new FeatureBentoCard(item);
            card.Invoked += OnCardInvoked;
            _cards.Add(card);
            _panel.Children.Add(card);
        }
    }

    private void UpdateHeader()
        => PseudoClasses.Set(NoHeaderPseudoClass,
            string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Eyebrow) && string.IsNullOrEmpty(Description));

    private void OnCardInvoked(object? sender, EventArgs e)
    {
        if (sender is FeatureBentoCard card)
        {
            RaiseEvent(new FeatureBentoItemEventArgs(ItemInvokedEvent, card.Item));
        }
    }
}
