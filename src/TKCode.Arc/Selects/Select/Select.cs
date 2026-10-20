using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Arc select: a labelled, compact choice field with a keyboard friendly menu (Radix Select semantics). The shown
/// value rolls in the direction of the list, the menu drops from the trigger, a highlight glides between options
/// and the chosen option carries a check. Use it for short, fixed lists such as regions or sort orders.
/// <para>Options come from <c>ItemsSource</c> (any objects, <see cref="SelectOption"/> records) or inline
/// <see cref="SelectItem"/> children; labels use <see cref="TextSelector"/>, <c>DisplayMemberBinding</c> or
/// <c>ToString()</c>, values use <see cref="ValueSelector"/>, <c>SelectedValueBinding</c> or the item.</para>
/// <para>Keyboard: Enter, Space and the arrows open; arrows, Home / End and PageUp / PageDown move the highlight;
/// Enter or Space picks; Escape closes without a change; typing jumps to the next matching option (and picks it
/// directly while the menu is closed).</para>
/// </summary>
[TemplatePart(TriggerPart, typeof(Border))]
[TemplatePart(ValuePart, typeof(SelectValueRoll))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(OptionsPart, typeof(SelectOptionView))]
[TemplatePart(ScrollUpPart, typeof(Border))]
[TemplatePart(ScrollDownPart, typeof(Border))]
[PseudoClasses(OpenPseudoClass, PlaceholderPseudoClass, PressedPseudoClass)]
public class Select : SelectingItemsControl, ISelectOptionHost, ISelectFieldAutomation
{
    public const string TriggerPart = "PART_Trigger";
    public const string ValuePart = "PART_Value";
    public const string PopupPart = "PART_Popup";
    public const string SurfacePart = "PART_Surface";
    public const string ScrollPart = "PART_Scroll";
    public const string OptionsPart = "PART_Options";
    public const string ScrollUpPart = "PART_ScrollUp";
    public const string ScrollDownPart = "PART_ScrollDown";
    public const string AnnouncerPart = "PART_Announcer";
    public const string OpenPseudoClass = ":open";
    public const string PlaceholderPseudoClass = ":placeholder";
    public const string PressedPseudoClass = ":pressed";

    #region 公共属性定义

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Select, string?>(nameof(Label));

    /// <summary>Defines the <see cref="Description"/> property.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<Select, string?>(nameof(Description));

    /// <summary>Defines the <see cref="Placeholder"/> property.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<Select, string?>(nameof(Placeholder), "Select an option");

    /// <summary>Defines the <see cref="IsOpen"/> property.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Select, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="MaxDropDownHeight"/> property.</summary>
    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        AvaloniaProperty.Register<Select, double>(nameof(MaxDropDownHeight), 320);

    /// <summary>Defines the <see cref="TextSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object?, string?>?> TextSelectorProperty =
        AvaloniaProperty.Register<Select, Func<object?, string?>?>(nameof(TextSelector));

    /// <summary>Defines the <see cref="ValueSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object?, object?>?> ValueSelectorProperty =
        AvaloniaProperty.Register<Select, Func<object?, object?>?>(nameof(ValueSelector));

    /// <summary>Defines the <see cref="DisabledSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object?, bool>?> DisabledSelectorProperty =
        AvaloniaProperty.Register<Select, Func<object?, bool>?>(nameof(DisabledSelector));

    /// <summary>Defines the <see cref="SelectedText"/> property.</summary>
    public static readonly DirectProperty<Select, string?> SelectedTextProperty =
        AvaloniaProperty.RegisterDirect<Select, string?>(nameof(SelectedText), o => o.SelectedText);

    /// <summary>Visible label above the trigger; also the accessible name.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Muted hint under the trigger; also the accessible help text.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Muted text shown while nothing is selected (Arc: "Select an option").</summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Whether the menu is open (two-way).</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Tallest the menu gets before it scrolls (Arc: 320px, or the room available).</summary>
    public double MaxDropDownHeight
    {
        get => GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    /// <summary>Label of a data item (AOT-safe alternative to <c>DisplayMemberBinding</c>).</summary>
    public Func<object?, string?>? TextSelector
    {
        get => GetValue(TextSelectorProperty);
        set => SetValue(TextSelectorProperty, value);
    }

    /// <summary>Value of a data item (AOT-safe alternative to <c>SelectedValueBinding</c>).</summary>
    public Func<object?, object?>? ValueSelector
    {
        get => GetValue(ValueSelectorProperty);
        set => SetValue(ValueSelectorProperty, value);
    }

    /// <summary>Returns true for data items that are shown but cannot be picked.</summary>
    public Func<object?, bool>? DisabledSelector
    {
        get => GetValue(DisabledSelectorProperty);
        set => SetValue(DisabledSelectorProperty, value);
    }

    /// <summary>Label of the selected option, or null.</summary>
    public string? SelectedText
    {
        get => _selectedText;
        private set => SetAndRaise(SelectedTextProperty, ref _selectedText, value);
    }

    #endregion

    private readonly SelectEntrySource _entries;
    private readonly List<int> _all = new();
    private readonly SelectTypeahead _typeahead = new(1000);
    private string? _selectedText;
    private Border? _trigger;
    private SelectValueRoll? _value;
    private ArcPopup? _popup;
    private Border? _surface;
    private ScrollViewer? _scroll;
    private SelectOptionView? _options;
    private Border? _scrollUp;
    private Border? _scrollDown;
    private TextBlock? _announcer;
    private DispatcherTimer? _autoScroll;
    private int _autoScrollDirection;
    private int _lastIndex = -1;
    private bool _optionsDirty = true;
    private bool _pressedOnTrigger;

    static Select()
    {
        FocusableProperty.OverrideDefaultValue<Select>(true);
        SelectionModeProperty.OverrideDefaultValue<Select>(SelectionMode.Single);
        IsTextSearchEnabledProperty.OverrideDefaultValue<Select>(false);
    }

    public Select()
    {
        _entries = new SelectEntrySource(this, () => TextSelector, () => DisabledSelector);
        _entries.Changed += (_, _) => OnEntriesChanged();
        SelectedValueBinding = SelectValueConverter.CreateBinding(() => ValueSelector);
    }

    SelectEntrySource ISelectOptionHost.Entries => _entries;

    string? ISelectFieldAutomation.AutomationLabel => Label;
    string? ISelectFieldAutomation.AutomationDescription => Description;
    string? ISelectFieldAutomation.AutomationValue => SelectedText;

    bool ISelectFieldAutomation.AutomationIsOpen
    {
        get => IsOpen;
        set => SetCurrentValue(IsOpenProperty, value);
    }

    /// <summary>Opens the menu with the selected option highlighted.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the menu without changing the value.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    protected override AutomationPeer OnCreateAutomationPeer() => new SelectFieldAutomationPeer(this, this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.PointerPressed -= OnTriggerPressed;
            _trigger.PointerReleased -= OnTriggerReleased;
            _trigger.PointerCaptureLost -= OnTriggerCaptureLost;
            _trigger.SizeChanged -= OnTriggerSizeChanged;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.Opened -= OnPopupOpened;
        }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
        }
        if (_options is not null)
        {
            _options.Host = null;
        }
        DetachScrollButtons();

        _trigger = e.NameScope.Find<Border>(TriggerPart);
        _value = e.NameScope.Find<SelectValueRoll>(ValuePart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _surface = e.NameScope.Find<Border>(SurfacePart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _options = e.NameScope.Find<SelectOptionView>(OptionsPart);
        _scrollUp = e.NameScope.Find<Border>(ScrollUpPart);
        _scrollDown = e.NameScope.Find<Border>(ScrollDownPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);

        if (_trigger is not null)
        {
            _trigger.PointerPressed += OnTriggerPressed;
            _trigger.PointerReleased += OnTriggerReleased;
            _trigger.PointerCaptureLost += OnTriggerCaptureLost;
            _trigger.SizeChanged += OnTriggerSizeChanged;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = _trigger;
            _popup.OverlayInputPassThroughElement = _trigger;
            _popup.Closed += OnPopupClosed;
            _popup.Opened += OnPopupOpened;
        }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
            UpdateScrollMaxHeight();
        }
        if (_options is not null)
        {
            _options.Host = this;
            _optionsDirty = true;
        }
        AttachScrollButtons();
        UpdateValue(animate: false);
        if (IsOpen)
        {
            OpenPopup();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (open)
            {
                OpenPopup();
            }
            else
            {
                ClosePopup();
            }
            (ControlAutomationPeer.FromElement(this) as SelectFieldAutomationPeer)?.RaiseExpandChanged(open);
        }
        else if (change.Property == SelectedIndexProperty)
        {
            UpdateValue(animate: true);
            _options?.InvalidateRows();
        }
        else if (change.Property == PlaceholderProperty)
        {
            UpdateValue(animate: false);
        }
        else if (change.Property == TextSelectorProperty || change.Property == DisabledSelectorProperty ||
                 change.Property == DisplayMemberBindingProperty)
        {
            _entries.Invalidate();
        }
        else if (change.Property == MaxDropDownHeightProperty)
        {
            UpdateScrollMaxHeight();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void OnEntriesChanged()
    {
        _optionsDirty = true;
        if (IsOpen)
        {
            EnsureOptions();
        }
        Dispatcher.UIThread.Post(() => UpdateValue(animate: false), DispatcherPriority.Background);
    }

    private void EnsureOptions()
    {
        if (_options is null || !_optionsDirty)
        {
            return;
        }
        _optionsDirty = false;
        _entries.Filter(null, null, _all);
        _options.SetOptions(_all);
    }

    private void UpdateValue(bool animate)
    {
        var index = SelectedIndex;
        var text = index >= 0 ? _entries.TextAt(index) : null;
        SelectedText = text;
        PseudoClasses.Set(PlaceholderPseudoClass, text is null);
        if (_value is not null)
        {
            var direction = index > _lastIndex ? 1 : -1;
            _value.Show(text ?? Placeholder ?? string.Empty, null, text is null, direction, animate: animate);
        }
        _lastIndex = index;
    }

    // ---------------------------------------------------------------- popup

    private void OpenPopup()
    {
        if (!IsEffectivelyEnabled || _popup is null)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            return;
        }
        _typeahead.Reset();
        EnsureOptions();
        UpdateSurfaceWidth();
        if (_popup.IsOpen)
        {
            return;
        }
        if (_options is not null)
        {
            var start = SelectedIndex >= 0 && _entries.IsEnabledAt(SelectedIndex) ? SelectedIndex : FirstEnabled(0, 1);
            _options.SetActive(start, animate: false);
        }
        _popup.IsOpen = true;
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        // The menu lays out after the popup host shows it; scroll and play the indicator once it has a size.
        Dispatcher.UIThread.Post(() =>
        {
            if (_options is null || !IsOpen)
            {
                return;
            }
            var active = _options.ActiveOption;
            if (active >= 0)
            {
                _options.SetActive(active, animate: false);
                _options.ScrollIntoView(active);
            }
            if (SelectedIndex >= 0)
            {
                // Radix mounts the item indicator with the menu: it grows in from 0.6 after 40ms.
                _options.PlayCheck(SelectedIndex, on: true, fromZero: true, delay: TimeSpan.FromMilliseconds(40));
            }
            UpdateScrollButtons();
        }, DispatcherPriority.Loaded);
    }

    private void ClosePopup()
    {
        StopAutoScroll();
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        _options?.SetActive(-1, animate: false);
        _typeahead.Reset();
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void OnTriggerSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateSurfaceWidth();

    private void UpdateSurfaceWidth()
    {
        if (_surface is not null && _trigger is not null && _trigger.Bounds.Width > 0)
        {
            // Radix: the content is exactly as wide as the trigger.
            _surface.Width = _trigger.Bounds.Width;
        }
    }

    private void UpdateScrollMaxHeight()
    {
        if (_scroll is not null)
        {
            // The surface adds 6px padding, a 1px border and the viewport's 2px inset on each side.
            _scroll.MaxHeight = Math.Max(0, MaxDropDownHeight - 18);
        }
    }

    // ---------------------------------------------------------------- pointer

    private void OnTriggerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(_trigger).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        PseudoClasses.Set(PressedPseudoClass, true);
        _pressedOnTrigger = true;
        e.Handled = true;
        // Radix opens on pointer down for a mouse (so press-drag-release picks); touch waits for the release.
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetCurrentValue(IsOpenProperty, !IsOpen);
            _pressedOnTrigger = false;
        }
    }

    private void OnTriggerReleased(object? sender, PointerReleasedEventArgs e)
    {
        PseudoClasses.Set(PressedPseudoClass, false);
        if (_pressedOnTrigger && _trigger is not null && new Rect(_trigger.Bounds.Size).Contains(e.GetPosition(_trigger)))
        {
            SetCurrentValue(IsOpenProperty, !IsOpen);
        }
        _pressedOnTrigger = false;
    }

    private void OnTriggerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        PseudoClasses.Set(PressedPseudoClass, false);
        _pressedOnTrigger = false;
    }

    // ---------------------------------------------------------------- keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled)
        {
            base.OnKeyDown(e);
            return;
        }
        if (!IsOpen)
        {
            switch (e.Key)
            {
                case Key.Space when _typeahead.IsTyping:
                    return;
                case Key.Enter:
                case Key.Space:
                case Key.Down:
                case Key.Up:
                    e.Handled = true;
                    Open();
                    return;
            }
            base.OnKeyDown(e);
            return;
        }
        var options = _options;
        var active = options?.ActiveOption ?? -1;
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Close();
                return;
            case Key.Tab:
                Close();
                return;
            case Key.Space when _typeahead.IsTyping:
                return;
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                if (active >= 0)
                {
                    Commit(active);
                }
                else
                {
                    Close();
                }
                return;
            case Key.Down:
                e.Handled = true;
                MoveActive(active < 0 ? FirstEnabled(0, 1) : NextEnabled(active, 1));
                return;
            case Key.Up:
                e.Handled = true;
                MoveActive(active < 0 ? FirstEnabled(_all.Count - 1, -1) : NextEnabled(active, -1));
                return;
            case Key.Home:
                e.Handled = true;
                MoveActive(FirstEnabled(0, 1));
                return;
            case Key.End:
                e.Handled = true;
                MoveActive(FirstEnabled(_all.Count - 1, -1));
                return;
            case Key.PageDown:
                e.Handled = true;
                MoveActive(FirstEnabled(Math.Min(_all.Count - 1, Math.Max(active, 0) + 8), -1));
                return;
            case Key.PageUp:
                e.Handled = true;
                MoveActive(FirstEnabled(Math.Max(0, active - 8), 1));
                return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled || string.IsNullOrEmpty(e.Text))
        {
            base.OnTextInput(e);
            return;
        }
        if (e.Text == " " && !_typeahead.IsTyping)
        {
            return;
        }
        e.Handled = true;
        var count = _entries.Count;
        if (IsOpen)
        {
            var current = _options?.ActiveOption ?? -1;
            var next = _typeahead.Next(e.Text, current, count, _entries.TextAt, _entries.IsEnabledAt);
            if (next >= 0)
            {
                MoveActive(next);
            }
            return;
        }
        // Radix: typing on a closed trigger picks the match without opening the menu.
        var match = _typeahead.Next(e.Text, SelectedIndex, count, _entries.TextAt, _entries.IsEnabledAt);
        if (match >= 0)
        {
            SetCurrentValue(SelectedIndexProperty, match);
        }
    }

    private void MoveActive(int position)
    {
        if (_options is null || position < 0)
        {
            return;
        }
        _options.SetActive(position, animate: true);
        _options.ScrollIntoView(position);
        // Focus stays on the trigger, so the highlighted option is announced (Radix focuses the item instead).
        if (_announcer is not null)
        {
            _announcer.Text = SelectOptionView.Describe(_entries, _options.EntryAt(position), position, _all.Count);
        }
    }

    private int NextEnabled(int from, int step)
    {
        // Radix select does not loop: the highlight stops at either end.
        for (var i = from + step; i >= 0 && i < _all.Count; i += step)
        {
            if (_entries.IsEnabledAt(_all[i]))
            {
                return i;
            }
        }
        return from;
    }

    private int FirstEnabled(int start, int step)
    {
        for (var i = start; i >= 0 && i < _all.Count; i += step)
        {
            if (_entries.IsEnabledAt(_all[i]))
            {
                return i;
            }
        }
        return -1;
    }

    private void Commit(int position)
    {
        var entry = _options?.EntryAt(position) ?? -1;
        if (entry < 0 || !_entries.IsEnabledAt(entry))
        {
            return;
        }
        SetCurrentValue(SelectedIndexProperty, entry);
        Close();
        Focus(NavigationMethod.Pointer);
    }

    // ---------------------------------------------------------------- ISelectOptionHost

    bool ISelectOptionHost.IsEntrySelected(int entryIndex) => entryIndex == SelectedIndex;

    void ISelectOptionHost.OnOptionHovered(int position) => _options?.SetActive(position, animate: true);

    void ISelectOptionHost.OnOptionInvoked(int position) => Commit(position);

    void ISelectOptionHost.OnOptionsPointerExited()
    {
        // Radix clears the highlight when the pointer leaves the items.
        if (IsOpen)
        {
            _options?.SetActive(-1, animate: true);
        }
    }

    // ---------------------------------------------------------------- Radix scroll buttons

    private void AttachScrollButtons()
    {
        foreach (var button in new[] { _scrollUp, _scrollDown })
        {
            if (button is null)
            {
                continue;
            }
            button.PointerEntered += OnScrollButtonEntered;
            button.PointerExited += OnScrollButtonExited;
        }
    }

    private void DetachScrollButtons()
    {
        foreach (var button in new[] { _scrollUp, _scrollDown })
        {
            if (button is null)
            {
                continue;
            }
            button.PointerEntered -= OnScrollButtonEntered;
            button.PointerExited -= OnScrollButtonExited;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateScrollButtons();

    private void UpdateScrollButtons()
    {
        if (_scroll is null)
        {
            return;
        }
        var offset = _scroll.Offset.Y;
        var max = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);
        if (_scrollUp is not null)
        {
            _scrollUp.IsVisible = offset > 0.5;
        }
        if (_scrollDown is not null)
        {
            _scrollDown.IsVisible = max - offset > 0.5;
        }
        if ((_autoScrollDirection < 0 && offset <= 0.5) || (_autoScrollDirection > 0 && max - offset <= 0.5))
        {
            StopAutoScroll();
        }
    }

    private void OnScrollButtonEntered(object? sender, PointerEventArgs e)
    {
        _autoScrollDirection = ReferenceEquals(sender, _scrollUp) ? -1 : 1;
        _autoScroll ??= new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, (_, _) => StepAutoScroll());
        _autoScroll.Start();
    }

    private void OnScrollButtonExited(object? sender, PointerEventArgs e) => StopAutoScroll();

    private void StepAutoScroll()
    {
        if (_scroll is null || _autoScrollDirection == 0)
        {
            StopAutoScroll();
            return;
        }
        var step = (_options?.RowHeight ?? 36) * _autoScrollDirection;
        _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, _scroll.Offset.Y + step));
    }

    private void StopAutoScroll()
    {
        _autoScrollDirection = 0;
        _autoScroll?.Stop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopAutoScroll();
        SetCurrentValue(IsOpenProperty, false);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        PseudoClasses.Set(PressedPseudoClass, false);
    }
}
