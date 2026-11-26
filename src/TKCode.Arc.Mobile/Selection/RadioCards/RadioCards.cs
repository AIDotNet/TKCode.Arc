using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Mobile radio cards: a grid of selectable cards for a choice that reads better as pictures or short descriptions than as a
/// list (a shape, a color, a plan). One card is selected; a ring glides to it on a spring and its radio dot springs in. It
/// has a press state and no hover state, so it suits touch. The cards come from inline <see cref="RadioCardOption"/>s or
/// from <see cref="ItemsControl.ItemsSource"/>; <see cref="SelectingItemsControl.SelectedValue"/> (two-way) is the selected
/// option's <see cref="RadioCardOption.Value"/>, and <c>SelectionChanged</c> reports every change of the selection.
/// <para>
/// Keyboard: the group is one tab stop; the arrow keys move through the grid and select, skipping disabled and hidden cards;
/// Home and End jump to the first and last card; Enter and Space choose the focused card.
/// </para>
/// </summary>
[TemplatePart(RingPart, typeof(GlidingIndicator))]
public class RadioCards : SelectingItemsControl
{
    /// <summary>The selection ring that glides to the selected card.</summary>
    public const string RingPart = "PART_Ring";

    /// <summary>How many equal-width columns the cards share: 2. Cards in one row are as tall as the tallest of them.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<RadioCards, int>(nameof(Columns), 2, coerce: static (_, value) => Math.Max(1, value));

    /// <summary>
    /// Whether each card shows a radio indicator (default true). Turn it off for compact visual choices (color swatches,
    /// shapes in four columns): the gliding ring alone then marks the selected card.
    /// </summary>
    public static readonly StyledProperty<bool> ShowIndicatorProperty =
        AvaloniaProperty.Register<RadioCards, bool>(nameof(ShowIndicator), true);

    internal static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<RadioCards, double>(nameof(ColumnGap), 10);

    internal static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<RadioCards, double>(nameof(RowGap), 10);

    // An option reports its Value, any other item reports itself; a compiled path, so trimming and AOT keep working.
    private static readonly ClrPropertyInfo s_value = new("Value", static target => target is RadioCardOption option ? option.Value : target, null, typeof(object));

    private SelectionGlider? _glider;

    static RadioCards()
    {
        FocusableProperty.OverrideDefaultValue<RadioCards>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<RadioCards>(KeyboardNavigationMode.Once);
        AffectsMeasure<RadioCards>(ColumnsProperty, ColumnGapProperty, RowGapProperty);
    }

    /// <summary>Creates an empty group.</summary>
    public RadioCards()
    {
        SelectedValueBinding = new CompiledBinding(new CompiledBindingPathBuilder()
            .Property(s_value, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
            .Build());
        SelectionChanged += (_, _) => UpdateTabStop();
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="ShowIndicatorProperty"/>
    public bool ShowIndicator
    {
        get => GetValue(ShowIndicatorProperty);
        set => SetValue(ShowIndicatorProperty, value);
    }

    internal double ColumnGap
    {
        get => GetValue(ColumnGapProperty);
        set => SetValue(ColumnGapProperty, value);
    }

    internal double RowGap
    {
        get => GetValue(RowGapProperty);
        set => SetValue(RowGapProperty, value);
    }

    /// <summary>The gliding selection ring, for tests.</summary>
    internal GlidingIndicator? Ring => _glider?.Glider;

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
        card.IsIndicatorShown = ShowIndicator;
        if (item is RadioCardOption option)
        {
            card.Follow(option);
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        UpdateTabStop();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is RadioCard { FollowsOption: true } card)
        {
            card.Follow(null);
            card.ClearValue(ContentControl.ContentProperty);
            card.ClearValue(RadioCard.DescriptionProperty);
            card.ClearValue(RadioCard.IconProperty);
            card.ClearValue(IsEnabledProperty);
            card.ClearValue(IsVisibleProperty);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glider?.Detach();
        // The ring's stroke is centered half a stroke inside the card, so it covers the card's own outline.
        _glider = e.NameScope.Find<GlidingIndicator>(RingPart) is { } ring
            ? new SelectionGlider(this, ring, static container => container, insetByHalfStroke: true)
            : null;
        if (this.IsAttachedToVisualTree())
        {
            _glider?.Attach();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _glider?.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _glider?.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        _glider?.OnOwnerArranged();
        return size;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColumnsProperty || change.Property == ColumnGapProperty || change.Property == RowGapProperty)
        {
            ItemsPanelRoot?.InvalidateMeasure();
        }
        else if (change.Property == ShowIndicatorProperty)
        {
            for (var index = 0; index < ItemCount; index++)
            {
                if (ContainerFromIndex(index) is RadioCard card)
                {
                    card.IsIndicatorShown = ShowIndicator;
                }
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && Move(e.Key, e.Source))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>Keeps the single tab stop on the selected card (or the first one that can be chosen).</summary>
    private void UpdateTabStop()
    {
        var stop = ContainerFromIndex(SelectedIndex) is RadioCard { IsVisible: true, IsEffectivelyEnabled: true } selected
            ? selected
            : Enumerable.Range(0, ItemCount).Select(ContainerFromIndex).OfType<RadioCard>().FirstOrDefault(IsChoosable);
        KeyboardNavigation.SetTabOnceActiveElement(this, stop);
    }

    private static bool IsChoosable(RadioCard card) => card.IsVisible && card.IsEffectivelyEnabled;

    /// <summary>
    /// Moves the selection by an arrow key in the grid of shown cards (Left / Right in reading order, mirrored right to left,
    /// and wrapping; Up / Down by a row, stopping at the edge), or to the first / last card with Home / End. Disabled cards
    /// are stepped over. Returns whether the key is one of these.
    /// </summary>
    private bool Move(Key key, object? source)
    {
        var rtl = FlowDirection == Avalonia.Media.FlowDirection.RightToLeft;
        int step;
        bool wrap;
        switch (key)
        {
            case Key.Left:
                (step, wrap) = (rtl ? 1 : -1, true);
                break;
            case Key.Right:
                (step, wrap) = (rtl ? -1 : 1, true);
                break;
            case Key.Up:
                (step, wrap) = (-Columns, false);
                break;
            case Key.Down:
                (step, wrap) = (Columns, false);
                break;
            case Key.Home:
            case Key.End:
                (step, wrap) = (0, false);
                break;
            default:
                return false;
        }

        // The shown cards in layout order: hidden ones take no room, so they are not part of the grid.
        var shown = new List<RadioCard>();
        for (var index = 0; index < ItemCount; index++)
        {
            if (ContainerFromIndex(index) is RadioCard { IsVisible: true } card)
            {
                shown.Add(card);
            }
        }
        if (shown.Count == 0)
        {
            return true;
        }
        RadioCard? target = null;
        if (key == Key.Home)
        {
            target = shown.FirstOrDefault(c => c.IsEffectivelyEnabled);
        }
        else if (key == Key.End)
        {
            target = shown.LastOrDefault(c => c.IsEffectivelyEnabled);
        }
        else
        {
            var from = (source as Visual)?.FindAncestorOfType<RadioCard>(includeSelf: true);
            var position = from is null ? -1 : shown.IndexOf(from);
            if (position < 0)
            {
                position = ContainerFromIndex(SelectedIndex) is RadioCard selected ? shown.IndexOf(selected) : -1;
            }
            if (position < 0)
            {
                target = shown.FirstOrDefault(c => c.IsEffectivelyEnabled);
            }
            else
            {
                for (var next = position + step; ; next += step)
                {
                    if (wrap)
                    {
                        next = ((next % shown.Count) + shown.Count) % shown.Count;
                        if (next == position)
                        {
                            break;
                        }
                    }
                    else if (next < 0 || next >= shown.Count)
                    {
                        break;
                    }
                    if (shown[next].IsEffectivelyEnabled)
                    {
                        target = shown[next];
                        break;
                    }
                }
            }
        }
        if (target is not null)
        {
            target.Select();
            target.Focus(NavigationMethod.Directional);
        }
        return true;
    }
}
