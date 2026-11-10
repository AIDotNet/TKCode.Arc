using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A row of tabs on a track, with the selection shown as a drop of color under the selected tab. When the selection
/// changes the drop does not slide: its front end reaches for the new tab first, the drop stretches, thins and pinches
/// at its waist, and then its back end catches up and it settles into a round pill again.
/// </summary>
/// <remarks>
/// Items are <see cref="LiquidTabBarItem"/> elements written in XAML, or any data through
/// <see cref="ItemsControl.ItemsSource"/> (each item is shown by its string or by <see cref="ItemsControl.ItemTemplate"/>).
/// One tab is always selected while there are tabs. The arrow keys move the selection and the focus together, as in a
/// tab list.
/// </remarks>
[TemplatePart(BlobPart, typeof(LiquidTabBarBlob))]
public class LiquidTabBar : SelectingItemsControl
{
    internal const string BlobPart = "PART_Blob";

    /// <summary>
    /// How much the back of the drop lags behind its front while it moves, from 0 (the drop barely stretches) to 1 (a
    /// long, slow stretch). Default 0.5.
    /// </summary>
    public static readonly StyledProperty<double> ViscosityProperty =
        AvaloniaProperty.Register<LiquidTabBar, double>(nameof(Viscosity), 0.5);

    private LiquidTabBarBlob? _blob;
    private bool _animateNext;

    static LiquidTabBar()
    {
        SelectionModeProperty.OverrideDefaultValue<LiquidTabBar>(SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<LiquidTabBar>(false);
    }

    /// <inheritdoc cref="ViscosityProperty"/>
    public double Viscosity { get => GetValue(ViscosityProperty); set => SetValue(ViscosityProperty, value); }

    /// <summary>The drop (null before the template is applied).</summary>
    internal LiquidTabBarBlob? Blob => _blob;

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new LiquidTabBarItem();

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<LiquidTabBarItem>(item, out recycleKey);

    /// <inheritdoc />
    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        UpdateTabStops();
        InvalidateArrange();
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        UpdateTabStops();
        InvalidateArrange();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _blob = e.NameScope.Find<LiquidTabBarBlob>(BlobPart);
        _animateNext = false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            _animateNext = true;
            UpdateTabStops();
            InvalidateArrange();
        }
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        PlaceBlob();
        return size;
    }

    private void PlaceBlob()
    {
        if (_blob is null)
        {
            return;
        }
        if (SelectedIndex < 0 || ContainerFromIndex(SelectedIndex) is not { IsVisible: true } container
            || _blob.GetVisualParent() is not Visual stage || container.TranslatePoint(default, stage) is not { } origin)
        {
            _blob.Hide();
            return;
        }
        var animate = _animateNext && _blob.IsPlaced;
        _animateNext = false;
        _blob.Place(new Rect(origin, container.Bounds.Size), animate, Viscosity);
    }

    /// <summary>A press on a tab selects it.</summary>
    internal void OnItemPressed(LiquidTabBarItem item)
    {
        if (!item.IsEffectivelyEnabled)
        {
            return;
        }
        var index = IndexFromContainer(item);
        if (index >= 0)
        {
            SelectedIndex = index;
            item.Focus(NavigationMethod.Pointer);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source || FindItem(source) is not { } item)
        {
            base.OnKeyDown(e);
            return;
        }
        var current = IndexFromContainer(item);
        int? target = e.Key switch
        {
            Key.Left or Key.Up => Step(current, -1),
            Key.Right or Key.Down => Step(current, 1),
            Key.Home => FirstEnabled(0, 1),
            Key.End => FirstEnabled(ItemCount - 1, -1),
            _ => null
        };
        if (target is { } index)
        {
            e.Handled = true;
            if (index >= 0 && index != current)
            {
                SelectedIndex = index;
                ContainerFromIndex(index)?.Focus(NavigationMethod.Directional);
            }
            return;
        }
        if (e.Key is Key.Enter or Key.Space && current >= 0)
        {
            e.Handled = true;
            SelectedIndex = current;
            return;
        }
        base.OnKeyDown(e);
    }

    private LiquidTabBarItem? FindItem(Visual source)
    {
        for (Visual? v = source; v is not null && !ReferenceEquals(v, this); v = v.GetVisualParent())
        {
            if (v is LiquidTabBarItem item && IndexFromContainer(item) >= 0)
            {
                return item;
            }
        }
        return null;
    }

    /// <summary>The next enabled tab from <paramref name="from"/> in <paramref name="direction"/>, wrapping around the ends.</summary>
    private int Step(int from, int direction)
    {
        var count = ItemCount;
        for (var i = 1; i <= count; i++)
        {
            var index = ((from + direction * i) % count + count) % count;
            if (IsSelectable(index))
            {
                return index;
            }
        }
        return -1;
    }

    private int FirstEnabled(int from, int direction)
    {
        for (var index = from; index >= 0 && index < ItemCount; index += direction)
        {
            if (IsSelectable(index))
            {
                return index;
            }
        }
        return -1;
    }

    private bool IsSelectable(int index) => ContainerFromIndex(index) is not { } container || container.IsEnabled;

    /// <summary>Only the selected tab is a tab stop; the arrow keys move between tabs.</summary>
    private void UpdateTabStops()
    {
        var selected = SelectedIndex;
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is { } container)
            {
                container.IsTabStop = i == selected || (selected < 0 && i == 0);
            }
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LiquidTabBarAutomationPeer(this);

    private sealed class LiquidTabBarAutomationPeer : SelectingItemsControlAutomationPeer
    {
        public LiquidTabBarAutomationPeer(LiquidTabBar owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}
