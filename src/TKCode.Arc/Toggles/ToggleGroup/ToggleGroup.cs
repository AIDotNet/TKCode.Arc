// Ported from Arc Toggle group (MIT, Copyright (c) 2026 Elia Kuratli): the value model, the roving tab stop, the keyboard
// rules and the look of both modes.
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
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
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>How a <see cref="ToggleGroup"/> treats its options.</summary>
public enum ToggleGroupMode
{
    /// <summary>One option at most is pressed. Choosing another moves the pressed state; the pressed option stays pressed.</summary>
    Single,

    /// <summary>Each option toggles on its own.</summary>
    Multiple
}

/// <summary>Arguments of <see cref="ToggleGroup.ValueChanged"/>.</summary>
public sealed class ToggleGroupValueChangedEventArgs : RoutedEventArgs
{
    public ToggleGroupValueChangedEventArgs(RoutedEvent routedEvent, object? value, bool isPressed, IReadOnlyList<object?> values) : base(routedEvent)
    {
        Value = value;
        IsPressed = isPressed;
        Values = values;
    }

    /// <summary>The value of the option the person toggled.</summary>
    public object? Value { get; }

    /// <summary>Whether that option is pressed now.</summary>
    public bool IsPressed { get; }

    /// <summary>The values of all pressed options, in the order they were pressed.</summary>
    public IReadOnlyList<object?> Values { get; }
}

/// <summary>
/// Arc toggle group: a flat row of toggle buttons on the surface. In single mode one pressed fill glides from option to
/// option on the morph spring; in multiple mode each fill fades in on its own and neighbours that are both on join into
/// one shape. The arrow keys move focus without pressing, Home and End jump to the ends, Space and Enter toggle, and the
/// group is one tab stop: the last focused option, else the first pressed one, else the first.
/// </summary>
[TemplatePart(SelectionPart, typeof(GlidingIndicator))]
[PseudoClasses(SmallPseudoClass, MultiplePseudoClass)]
public class ToggleGroup : SelectingItemsControl
{
    public const string SelectionPart = "PART_Selection";
    public const string SmallPseudoClass = ":small";
    public const string MultiplePseudoClass = ":multiple";

    /// <summary>The accessible name of the group, such as "Text style" or "Alignment".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ToggleGroup, string?>(nameof(Label));

    /// <summary>Whether one option or any number of options can be pressed.</summary>
    public static readonly StyledProperty<ToggleGroupMode> ModeProperty =
        AvaloniaProperty.Register<ToggleGroup, ToggleGroupMode>(nameof(Mode));

    /// <summary>The height of the options: Small is 28 px; Medium, the default, and Large are 36 px.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<ToggleGroup, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>The values of the pressed options, in the order they were pressed. In XAML, a comma-separated list. A value with no option yet is kept and applies when the option arrives.</summary>
    public static readonly StyledProperty<IReadOnlyList<object?>> SelectedValuesProperty =
        AvaloniaProperty.Register<ToggleGroup, IReadOnlyList<object?>>(nameof(SelectedValues), Array.Empty<object?>(),
            defaultBindingMode: BindingMode.TwoWay, coerce: static (_, values) => values ?? Array.Empty<object?>());

    /// <summary>Raised when a person toggles an option, not when code sets the value.</summary>
    public static readonly RoutedEvent<ToggleGroupValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<ToggleGroup, ToggleGroupValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> FillMixProperty =
        AvaloniaProperty.Register<ToggleGroup, double>(nameof(FillMix), 0.11);

    internal static readonly StyledProperty<IBrush?> PressedFillProperty =
        AvaloniaProperty.Register<ToggleGroup, IBrush?>(nameof(PressedFill), inherits: true);

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<ToggleGroupValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ModeProperty"/>
    public ToggleGroupMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="SelectedValuesProperty"/>
    public IReadOnlyList<object?> SelectedValues { get => GetValue(SelectedValuesProperty); set => SetValue(SelectedValuesProperty, value); }

    internal double FillMix { get => GetValue(FillMixProperty); set => SetValue(FillMixProperty, value); }

    internal IBrush? PressedFill { get => GetValue(PressedFillProperty); set => SetValue(PressedFillProperty, value); }

    private readonly TogglesGroupHost _host;
    private GlidingIndicator? _indicator;
    private ToggleGroupItem? _lastFocused;
    private bool _syncing;
    private readonly BindingBase _defaultValueBinding;
    private SelectBindingEvaluator? _valueEvaluator;

    static ToggleGroup()
    {
        FocusableProperty.OverrideDefaultValue<ToggleGroup>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<ToggleGroup>(KeyboardNavigationMode.Once);
    }

    public ToggleGroup()
    {
        _host = new TogglesGroupHost(this, flipInRtl: false, OnOptionApplied);
        _defaultValueBinding = TogglesValueBinding.Create();
        SelectedValueBinding = _defaultValueBinding;
        SelectionChanged += OnSelectionChanged;
        ActualThemeVariantChanged += (_, _) => UpdateFill();
        ResourcesChanged += (_, _) => UpdateFill();
    }

    protected override Type StyleKeyOverride => typeof(ToggleGroup);

    private bool IsMultiple => Mode == ToggleGroupMode.Multiple;

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<ToggleGroupItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new ToggleGroupItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        ApplyOption(container, item);
        if (container is ToggleGroupItem option)
        {
            option.SetLook(Size == ControlSize.Small, IsMultiple);
        }
    }

    private static void ApplyOption(Control container, object? item)
    {
        if (container is not ToggleGroupItem target || item is not ToggleGroupOption option)
        {
            return;
        }
        var textFromLabel = option.Content is null && option.Icon is null;
        target.Content = textFromLabel ? option.Label ?? option.Value?.ToString() : option.Content;
        target.Icon = option.Icon;
        target.IsEnabled = !option.IsDisabled;
        AutomationProperties.SetName(target, option.Label);
        Tooltip.SetTip(target, option.Title ?? (textFromLabel ? null : option.Label));
    }

    private void OnOptionApplied(Control container, object? item)
    {
        ApplyOption(container, item);
        ApplyValuesToSelection(fromValues: false);
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        // The options of a XAML list arrive after the attributes, so values set earlier apply here.
        ApplyValuesToSelection(fromValues: false);
        RefreshTabStop();
        UpdateJoins();
    }

    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        UpdateJoins();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is ToggleGroupItem option)
        {
            option.ClearValue(ContentControl.ContentProperty);
            option.ClearValue(ToggleGroupItem.IconProperty);
            option.ClearValue(IsEnabledProperty);
            option.ClearValue(AutomationProperties.NameProperty);
            option.ClearValue(Tooltip.TipProperty);
            option.SetJoins(false, false);
            if (ReferenceEquals(_lastFocused, option))
            {
                _lastFocused = null;
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _indicator = e.NameScope.Find<GlidingIndicator>(SelectionPart);
        AttachIndicator();
    }

    /// <summary>Only single mode has the gliding fill; it appears in place and disappears at once, like Arc's layoutId fill.</summary>
    private void AttachIndicator()
    {
        if (IsMultiple)
        {
            _indicator?.Hide(animate: false);
        }
        _host.AttachIndicator(IsMultiple ? null : _indicator, inset: 0, fadeInFirst: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            SelectionMode = IsMultiple ? SelectionMode.Multiple : SelectionMode.Single;
            PseudoClasses.Set(MultiplePseudoClass, IsMultiple);
            UpdateLooks();
            AttachIndicator();
            ApplyValuesToSelection(fromValues: true);
            RefreshTabStop();
            UpdateJoins();
        }
        else if (change.Property == SizeProperty)
        {
            PseudoClasses.Set(SmallPseudoClass, Size == ControlSize.Small);
            UpdateLooks();
        }
        else if (change.Property == SelectedValuesProperty)
        {
            if (!_syncing)
            {
                ApplyValuesToSelection(fromValues: true);
            }
        }
        else if (change.Property == FillMixProperty)
        {
            UpdateFill();
        }
        else if (change.Property == IsEnabledProperty)
        {
            RefreshTabStop();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateFill();
    }

    /// <summary>
    /// CSS <c>color-mix(in oklab, control-on x%, surface)</c>. ControlOn follows the accent, so a control token cannot hold
    /// the result; it is mixed here when the tokens change.
    /// </summary>
    private void UpdateFill()
    {
        var color = Oklch.MixOklab(
            ArcTokens.GetColor(this, ArcDesignTokenKind.Surface),
            ArcTokens.GetColor(this, ArcDesignTokenKind.ControlOn),
            FillMix);
        if (PressedFill is not ISolidColorBrush current || current.Color != color)
        {
            SetCurrentValue(PressedFillProperty, new ImmutableSolidColorBrush(color));
        }
    }

    private void UpdateLooks()
    {
        var small = Size == ControlSize.Small;
        foreach (var container in GetRealizedContainers())
        {
            (container as ToggleGroupItem)?.SetLook(small, IsMultiple);
        }
    }

    internal void OnItemPressedChanged() => UpdateJoins();

    /// <summary>In multiple mode a pressed option next to another pressed option shares one shape with it.</summary>
    private void UpdateJoins()
    {
        var count = ItemCount;
        for (var index = 0; index < count; index++)
        {
            if (ContainerFromIndex(index) is not ToggleGroupItem item)
            {
                continue;
            }
            var on = IsMultiple && item.IsSelected;
            item.SetJoins(on && IsPressedAt(index - 1), on && IsPressedAt(index + 1));
        }
    }

    private bool IsPressedAt(int index) => index >= 0 && index < ItemCount && ContainerFromIndex(index) is ToggleGroupItem { IsSelected: true };

    private int IndexOfValue(object? value)
    {
        var count = ItemCount;
        for (var index = 0; index < count; index++)
        {
            if (Equals(ValueAt(index), value))
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// The value the item at <paramref name="index"/> reports, read through the current <c>SelectedValueBinding</c>
    /// (the default one maps an option to its <c>Value</c> and any other item to itself).
    /// </summary>
    private object? ValueAt(int index)
    {
        var item = Items[index];
        var binding = SelectedValueBinding;
        if (binding is null || ReferenceEquals(binding, _defaultValueBinding))
        {
            return TogglesValueBinding.ValueOf(item);
        }
        _valueEvaluator ??= new SelectBindingEvaluator();
        var value = _valueEvaluator.Evaluate(binding, item);
        _valueEvaluator.Clear();
        return value;
    }

    /// <summary>
    /// Presses the options <see cref="SelectedValues"/> names; does nothing when they are already pressed.
    /// <paramref name="fromValues"/> is true when <see cref="SelectedValues"/> (or the mode) changed: only then does
    /// single mode push the value into <c>SelectedValue</c>, so preparing options never overwrites a bound value.
    /// </summary>
    private void ApplyValuesToSelection(bool fromValues)
    {
        var values = SelectedValues;
        if (!IsMultiple)
        {
            if (fromValues && values.Count > 0 && !_syncing && !Equals(SelectedValue, values[0]))
            {
                SetCurrentValue(SelectedValueProperty, values[0]);
            }
            return;
        }
        var wanted = new SortedSet<int>();
        foreach (var value in values)
        {
            var index = IndexOfValue(value);
            if (index >= 0)
            {
                wanted.Add(index);
            }
        }
        if (wanted.SetEquals(Selection.SelectedIndexes))
        {
            return;
        }
        var syncing = _syncing;
        _syncing = true;
        Selection.BeginBatchUpdate();
        try
        {
            Selection.Clear();
            foreach (var index in wanted)
            {
                Selection.Select(index);
            }
        }
        finally
        {
            Selection.EndBatchUpdate();
            _syncing = syncing;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _host.OnSelectionChanged();
        ApplyTabStop();
        UpdateJoins();
        if (!_syncing)
        {
            ReadValuesFromSelection();
        }
    }

    /// <summary>The selection moved on its own (code set <c>SelectedIndex</c>, the list changed): the values follow it.</summary>
    private void ReadValuesFromSelection()
    {
        IReadOnlyList<object?> next;
        if (IsMultiple)
        {
            var pressed = new List<object?>();
            // Values with no option are kept; the order is the order of pressing.
            foreach (var value in SelectedValues)
            {
                var index = IndexOfValue(value);
                if (index < 0 || Selection.IsSelected(index))
                {
                    pressed.Add(value);
                }
            }
            foreach (var index in Selection.SelectedIndexes)
            {
                var value = ValueAt(index);
                if (!pressed.Contains(value))
                {
                    pressed.Add(value);
                }
            }
            next = pressed;
        }
        else
        {
            next = SelectedIndex >= 0 && SelectedIndex < ItemCount ? [ValueAt(SelectedIndex)] : [];
        }
        if (next.SequenceEqual(SelectedValues))
        {
            return;
        }
        _syncing = true;
        SetCurrentValue(SelectedValuesProperty, next);
        _syncing = false;
    }

    /// <summary>A person toggled <paramref name="item"/> (click, Space, Enter, automation).</summary>
    internal void Toggle(ToggleGroupItem item)
    {
        var index = IndexFromContainer(item);
        if (index < 0 || !item.IsEffectivelyEnabled)
        {
            return;
        }
        var value = ValueAt(index);
        if (IsMultiple)
        {
            var on = item.IsSelected;
            var next = new List<object?>(SelectedValues);
            if (on)
            {
                next.RemoveAll(entry => Equals(entry, value));
            }
            else
            {
                next.Add(value);
            }
            SetCurrentValue(SelectedValuesProperty, next);
            RaiseEvent(new ToggleGroupValueChangedEventArgs(ValueChangedEvent, value, !on, SelectedValues));
            return;
        }
        // Single choice behaves like a radio group: choosing the pressed option again keeps it.
        if (SelectedIndex == index)
        {
            return;
        }
        SelectedIndex = index;
        RaiseEvent(new ToggleGroupValueChangedEventArgs(ValueChangedEvent, value, true, SelectedValues));
    }

    private void RefreshTabStop()
    {
        _host.UpdateTabStop();
        ApplyTabStop();
    }

    /// <summary>The roving tab stop: the last focused option, else the first pressed one, else the first (disabled ones never).</summary>
    private void ApplyTabStop()
    {
        Control? stop = null;
        if (_lastFocused is { } focused && IndexFromContainer(focused) is var at && at >= 0 && _host.Usable(at))
        {
            stop = focused;
        }
        if (stop is null)
        {
            foreach (var value in SelectedValues)
            {
                var index = IndexOfValue(value);
                if (index >= 0 && _host.Usable(index))
                {
                    stop = ContainerFromIndex(index);
                    break;
                }
            }
        }
        if (stop is null)
        {
            var count = ItemCount;
            for (var index = 0; index < count; index++)
            {
                if (_host.Usable(index))
                {
                    stop = ContainerFromIndex(index);
                    break;
                }
            }
        }
        KeyboardNavigation.SetTabOnceActiveElement(this, stop);
        if (ItemsPanelRoot is { } panel)
        {
            KeyboardNavigation.SetTabOnceActiveElement(panel, stop);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ItemOf(e.Source) is { } item)
        {
            _lastFocused = item;
        }
        ApplyTabStop();
    }

    private static ToggleGroupItem? ItemOf(object? source)
        => source is ToggleGroupItem item ? item : (source as Visual)?.FindAncestorOfType<ToggleGroupItem>();

    /// <summary>The keys only move focus, unlike the other toggle groups; pressing is Space and Enter (Arc).</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && ItemOf(e.Source) is { } item)
        {
            var current = IndexFromContainer(item);
            if (current >= 0 && TogglesNavigation.Move(current, ItemCount, e.Key, _host.Usable, flipHorizontal: false) is { } target)
            {
                e.Handled = true;
                ContainerFromIndex(target)?.Focus(NavigationMethod.Directional);
                return;
            }
        }
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleGroupAutomationPeer(this);

    private sealed class ToggleGroupAutomationPeer : SelectingItemsControlAutomationPeer
    {
        public ToggleGroupAutomationPeer(ToggleGroup owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? ((ToggleGroup)Owner).Label : name;
        }
    }
}
