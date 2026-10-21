using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Size of a <see cref="BillingToggle"/>.</summary>
public enum BillingToggleSize
{
    /// <summary>34px options (Arc <c>md</c>).</summary>
    Medium,

    /// <summary>42px options with base-size text (Arc <c>lg</c>).</summary>
    Large
}

/// <summary>Arguments of <see cref="BillingToggle.ValueChanged"/>.</summary>
public sealed class BillingToggleValueChangedEventArgs : RoutedEventArgs
{
    public BillingToggleValueChangedEventArgs(RoutedEvent routedEvent, string? oldValue, string? newValue) : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public string? OldValue { get; }

    public string? NewValue { get; }
}

/// <summary>
/// Arc billing toggle: a billing period switch. One thumb glides between the options on a critically damped spring,
/// and the savings note on the cheaper period tints with <see cref="BadgeAccent"/> and rewrites itself in place once it
/// is chosen. Defaults to Monthly / Yearly ("Save 20%"); supply <see cref="BillingToggleOption"/> items (inline or via
/// <see cref="ItemsControl.ItemsSource"/>) for other periods. <see cref="Value"/> is two-way. Pair it with
/// <see cref="BillingPrice"/> for prices that roll to the new amount.
/// </summary>
[TemplatePart(ThumbPart, typeof(GlidingIndicator))]
public class BillingToggle : SelectingItemsControl
{
    public const string ThumbPart = "PART_Thumb";

    /// <summary>Arc's <c>glide</c>: <c>{ type: "spring", visualDuration: 0.34, bounce: 0 }</c> (critically damped).</summary>
    internal static readonly SpringSpec ThumbGlide = SpringSpec.FromVisual(0.34, 0);

    /// <summary>Accessible name used when none is set (Arc's <c>label</c> default).</summary>
    public const string DefaultLabel = "Billing period";

    #region 公共属性定义

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<BillingToggle, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Option size: Medium (34px, default) or Large (42px).</summary>
    public static readonly StyledProperty<BillingToggleSize> SizeProperty =
        AvaloniaProperty.Register<BillingToggle, BillingToggleSize>(nameof(Size));

    /// <summary>Color of a selected savings badge (Arc's <c>--billing-accent</c>, default <c>Success</c>).</summary>
    public static readonly StyledProperty<IBrush?> BadgeAccentProperty =
        AvaloniaProperty.Register<BillingToggle, IBrush?>(nameof(BadgeAccent));

    public static readonly RoutedEvent<BillingToggleValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<BillingToggle, BillingToggleValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>The selected option's value (two-way).</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public BillingToggleSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public IBrush? BadgeAccent
    {
        get => GetValue(BadgeAccentProperty);
        set => SetValue(BadgeAccentProperty, value);
    }

    /// <summary>Raised when <see cref="Value"/> changes, with the old and new values.</summary>
    public event EventHandler<BillingToggleValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    #endregion

    #region 内部属性定义

    internal static readonly StyledProperty<IBrush?> BadgeInkProperty =
        AvaloniaProperty.Register<BillingToggle, IBrush?>(nameof(BadgeInk));

    internal static readonly StyledProperty<double> BadgeTintOpacityProperty =
        AvaloniaProperty.Register<BillingToggle, double>(nameof(BadgeTintOpacity), 0.13);

    internal static readonly StyledProperty<double> BadgeInkMixProperty =
        AvaloniaProperty.Register<BillingToggle, double>(nameof(BadgeInkMix), 0.12);

    /// <summary>The text color the selected badge mixes toward (<c>Foreground</c>).</summary>
    internal IBrush? BadgeInk { get => GetValue(BadgeInkProperty); set => SetValue(BadgeInkProperty, value); }
    internal double BadgeTintOpacity { get => GetValue(BadgeTintOpacityProperty); set => SetValue(BadgeTintOpacityProperty, value); }
    internal double BadgeInkMix { get => GetValue(BadgeInkMixProperty); set => SetValue(BadgeInkMixProperty, value); }

    #endregion

    /// <summary>Arc's default options: Monthly, and Yearly with "Save 20%".</summary>
    public static IReadOnlyList<BillingToggleOption> DefaultOptions { get; } =
    [
        new BillingToggleOption("monthly", "Monthly"),
        new BillingToggleOption("yearly", "Yearly", "Save 20%")
    ];

    private readonly TogglesGroupHost _host;
    private IBrush? _activeBadgeBackground;
    private IBrush? _activeBadgeForeground;
    private bool _syncing;

    static BillingToggle()
    {
        FocusableProperty.OverrideDefaultValue<BillingToggle>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<BillingToggle>(KeyboardNavigationMode.Once);
    }

    public BillingToggle()
    {
        _host = new TogglesGroupHost(this, flipInRtl: false);
        SelectedValueBinding = TogglesValueBinding.Create();
        SelectionChanged += (_, _) => _host.OnSelectionChanged();
        ItemsView.CollectionChanged += (_, _) => EnsureSelection();
        AutomationProperties.SetName(this, DefaultLabel);
    }

    protected override Type StyleKeyOverride => typeof(BillingToggle);

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        // With no options of its own the toggle shows Arc's Monthly / Yearly pair.
        if (ItemsSource is null && Items.Count == 0)
        {
            ItemsSource = DefaultOptions;
        }
        base.OnAttachedToLogicalTree(e);
        EnsureSelection();
    }

    /// <summary>Like Arc, an unset value shows the first option as chosen.</summary>
    private void EnsureSelection()
    {
        if (SelectedIndex < 0 && SelectedValue is null && Value is null && ItemCount > 0 && ((ILogical)this).IsAttachedToLogicalTree)
        {
            var first = TogglesValueBinding.ValueOf(Items[0]);
            SetCurrentValue(ValueProperty, first as string ?? first?.ToString());
        }
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<BillingToggleItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new BillingToggleItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is not BillingToggleItem option)
        {
            return;
        }
        if (item is BillingToggleOption data)
        {
            option.Content = data.Label ?? data.Value;
            option.Badge = data.Badge;
            option.ActiveBadge = data.ActiveBadge;
        }
        option.IsLarge = Size == BillingToggleSize.Large;
        option.ActiveBadgeBackground = _activeBadgeBackground;
        option.ActiveBadgeForeground = _activeBadgeForeground;
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        _host.UpdateTabStop();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is BillingToggleItem option)
        {
            option.ClearValue(ContentControl.ContentProperty);
            option.ClearValue(BillingToggleItem.BadgeProperty);
            option.ClearValue(BillingToggleItem.ActiveBadgeProperty);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host.AttachIndicator(e.NameScope.Find<GlidingIndicator>(ThumbPart), inset: 0, fadeInFirst: false, ThumbGlide);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            if (!_syncing)
            {
                _syncing = true;
                SetCurrentValue(SelectedValueProperty, Value);
                _syncing = false;
            }
            RaiseEvent(new BillingToggleValueChangedEventArgs(ValueChangedEvent, change.GetOldValue<string?>(), change.GetNewValue<string?>()));
        }
        else if (change.Property == SelectedValueProperty)
        {
            if (!_syncing)
            {
                _syncing = true;
                SetCurrentValue(ValueProperty, SelectedValue as string ?? SelectedValue?.ToString());
                _syncing = false;
            }
        }
        else if (change.Property == SizeProperty)
        {
            ForEachOption(option => option.IsLarge = Size == BillingToggleSize.Large);
        }
        else if (change.Property == BadgeAccentProperty || change.Property == BadgeInkProperty ||
                 change.Property == BadgeTintOpacityProperty || change.Property == BadgeInkMixProperty)
        {
            UpdateBadgeBrushes();
        }
        else if (change.Property == IsEnabledProperty)
        {
            _host.UpdateTabStop();
        }
    }

    /// <summary>
    /// The selected badge: accent at 13% (dark 18%) behind text mixed 88% accent / 12% foreground (dark: the accent).
    /// Derived here because the accent is a brush the app may set, not a token.
    /// </summary>
    private void UpdateBadgeBrushes()
    {
        if (BadgeAccent is ISolidColorBrush accent)
        {
            var ink = (BadgeInk as ISolidColorBrush)?.Color ?? accent.Color;
            _activeBadgeBackground = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent.Color, BadgeTintOpacity * accent.Color.A / 255.0));
            _activeBadgeForeground = new ImmutableSolidColorBrush(BadgeInkMix > 0 ? Oklch.MixOklab(accent.Color, ink, BadgeInkMix) : accent.Color);
        }
        else
        {
            _activeBadgeBackground = TogglesBrushes.WithOpacity(BadgeAccent, BadgeTintOpacity);
            _activeBadgeForeground = BadgeAccent;
        }
        ForEachOption(option =>
        {
            option.ActiveBadgeBackground = _activeBadgeBackground;
            option.ActiveBadgeForeground = _activeBadgeForeground;
        });
    }

    private void ForEachOption(Action<BillingToggleItem> action)
    {
        for (var index = 0; index < ItemCount; index++)
        {
            if (ContainerFromIndex(index) is BillingToggleItem option)
            {
                action(option);
            }
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
