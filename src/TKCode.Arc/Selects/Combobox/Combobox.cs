using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc combobox: a searchable single-choice field. Typing filters the options (label and keywords) and the list
/// springs to its new height; arrow keys move a highlight while focus stays in the field, Enter picks, Escape
/// closes and drops the query. The chosen label settles into the field from below; a clear button pops in while
/// something is selected. Use it for long lists people know by name (time zones, countries).
/// </summary>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(InputHostPart, typeof(Decorator))]
[TemplatePart(ClearPart, typeof(AvaloniaButton))]
[TemplatePart(ClearHostPart, typeof(PresenceHost))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(OptionsPart, typeof(SelectOptionView))]
[TemplatePart(EmptyHostPart, typeof(PresenceHost))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[PseudoClasses(OpenPseudoClass)]
public class Combobox : SelectingItemsControl, ISelectOptionHost, ISelectFieldAutomation
{
    public const string FramePart = "PART_Frame";
    public const string InputHostPart = "PART_InputHost";
    public const string ClearPart = "PART_Clear";
    public const string ClearHostPart = "PART_ClearHost";
    public const string PopupPart = "PART_Popup";
    public const string SurfacePart = "PART_Surface";
    public const string ScrollPart = "PART_Scroll";
    public const string OptionsPart = "PART_Options";
    public const string EmptyHostPart = "PART_EmptyHost";
    public const string AnnouncerPart = "PART_Announcer";
    public const string DescriptionPart = "PART_Description";
    public const string OpenPseudoClass = ":open";

    #region 公共属性定义

    public static readonly StyledProperty<string?> LabelProperty = Select.LabelProperty.AddOwner<Combobox>();

    public static readonly StyledProperty<string?> DescriptionProperty = Select.DescriptionProperty.AddOwner<Combobox>();

    public static readonly StyledProperty<string?> PlaceholderProperty =
        Select.PlaceholderProperty.AddOwner<Combobox>(new StyledPropertyMetadata<string?>("Search or select…"));

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<Combobox, string?>(nameof(EmptyText), "No matches found");

    public static readonly StyledProperty<bool> IsOpenProperty = Select.IsOpenProperty.AddOwner<Combobox>();

    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        Select.MaxDropDownHeightProperty.AddOwner<Combobox>(new StyledPropertyMetadata<double>(300));

    /// <summary>Label of a data item (AOT-safe alternative to <c>DisplayMemberBinding</c>).</summary>
    public static readonly StyledProperty<Func<object?, string?>?> TextSelectorProperty = Select.TextSelectorProperty.AddOwner<Combobox>();

    /// <summary>Value of a data item (AOT-safe alternative to <c>SelectedValueBinding</c>).</summary>
    public static readonly StyledProperty<Func<object?, object?>?> ValueSelectorProperty = Select.ValueSelectorProperty.AddOwner<Combobox>();

    /// <summary>Returns true for data items that are shown but cannot be picked.</summary>
    public static readonly StyledProperty<Func<object?, bool>?> DisabledSelectorProperty = Select.DisabledSelectorProperty.AddOwner<Combobox>();

    /// <summary>Defines the <see cref="FilterPredicate"/> property.</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterPredicateProperty =
        AvaloniaProperty.Register<Combobox, Func<object?, string, bool>?>(nameof(FilterPredicate));

    /// <summary>Defines the <see cref="SearchText"/> property.</summary>
    public static readonly DirectProperty<Combobox, string> SearchTextProperty =
        AvaloniaProperty.RegisterDirect<Combobox, string>(nameof(SearchText), o => o.SearchText);

    public static readonly DirectProperty<Combobox, string?> SelectedTextProperty =
        AvaloniaProperty.RegisterDirect<Combobox, string?>(nameof(SelectedText), o => o.SelectedText);

    /// <summary>Visible label above the field; also names the field and its list.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Muted hint under the field.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Shown while the field is empty (Arc: "Search or select…"). While searching the chosen label takes its place.</summary>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>Status row when nothing matches (Arc: "No matches found").</summary>
    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <summary>Whether the list is open (two-way).</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Tallest the list gets (Arc: min(300px, 40% of the window)).</summary>
    public double MaxDropDownHeight { get => GetValue(MaxDropDownHeightProperty); set => SetValue(MaxDropDownHeightProperty, value); }

    /// <inheritdoc cref="Select.TextSelector"/>
    public Func<object?, string?>? TextSelector { get => GetValue(TextSelectorProperty); set => SetValue(TextSelectorProperty, value); }

    /// <inheritdoc cref="Select.ValueSelector"/>
    public Func<object?, object?>? ValueSelector { get => GetValue(ValueSelectorProperty); set => SetValue(ValueSelectorProperty, value); }

    /// <inheritdoc cref="Select.DisabledSelector"/>
    public Func<object?, bool>? DisabledSelector { get => GetValue(DisabledSelectorProperty); set => SetValue(DisabledSelectorProperty, value); }

    /// <summary>
    /// Custom match test receiving the source item and the trimmed query. By default the label, meta and keywords
    /// are matched case-insensitively as substrings.
    /// </summary>
    public Func<object?, string, bool>? FilterPredicate { get => GetValue(FilterPredicateProperty); set => SetValue(FilterPredicateProperty, value); }

    /// <summary>The current query (empty when the list is closed).</summary>
    public string SearchText
    {
        get => _query;
        private set => SetAndRaise(SearchTextProperty, ref _query, value);
    }

    /// <summary>Label of the selected option, or null.</summary>
    public string? SelectedText
    {
        get => _selectedText;
        private set => SetAndRaise(SelectedTextProperty, ref _selectedText, value);
    }

    #endregion

    private readonly SelectEntrySource _entries;
    private readonly List<int> _visible = new();
    private string _query = string.Empty;
    private string? _selectedText;
    private Border? _frame;
    private Decorator? _inputHost;
    private TextBox? _input;
    private AvaloniaButton? _clear;
    private PresenceHost? _clearHost;
    private ArcPopup? _popup;
    private Border? _surface;
    private ScrollViewer? _scroll;
    private SelectOptionView? _options;
    private PresenceHost? _emptyHost;
    private TextBlock? _announcer;
    private TextBlock? _description;
    private bool _syncingText;
    private MotionValue<double>? _settle;
    private TranslateTransform? _settleShift;

    static Combobox()
    {
        FocusableProperty.OverrideDefaultValue<Combobox>(false);
        SelectionModeProperty.OverrideDefaultValue<Combobox>(SelectionMode.Single);
    }

    public Combobox()
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

    /// <summary>The text input (for focus or IME scenarios).</summary>
    public TextBox? InputBox => _input;

    /// <summary>Moves keyboard focus to the field (which opens the list, like Arc).</summary>
    public void FocusInput() => _input?.Focus(NavigationMethod.Tab);

    /// <summary>Clears the selection and the query.</summary>
    public void Clear()
    {
        SetCurrentValue(SelectedIndexProperty, -1);
        SetQuery(string.Empty);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SelectFieldAutomationPeer(this, this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_input is not null)
        {
            _input.RemoveHandler(KeyDownEvent, OnInputKeyDown);
            _input.RemoveHandler(PointerPressedEvent, OnInputPointerPressed);
            _input.PropertyChanged -= OnInputPropertyChanged;
            _input.GotFocus -= OnInputGotFocus;
            _inputHost!.Child = null;
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
        }
        if (_options is not null)
        {
            _options.Host = null;
        }

        _frame = e.NameScope.Find<Border>(FramePart);
        _inputHost = e.NameScope.Find<Decorator>(InputHostPart);
        _clear = e.NameScope.Find<AvaloniaButton>(ClearPart);
        _clearHost = e.NameScope.Find<PresenceHost>(ClearHostPart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _surface = e.NameScope.Find<Border>(SurfacePart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _options = e.NameScope.Find<SelectOptionView>(OptionsPart);
        _emptyHost = e.NameScope.Find<PresenceHost>(EmptyHostPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);

        if (_inputHost is not null)
        {
            _input = SelectInputBox.Create();
            _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
            _input.AddHandler(PointerPressedEvent, OnInputPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _input.PropertyChanged += OnInputPropertyChanged;
            _input.GotFocus += OnInputGotFocus;
            _inputHost.Child = _input;
            _settleShift = new TranslateTransform();
            _input.RenderTransform = _settleShift;
        }
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }
        if (_popup is not null)
        {
            _popup.OverlayInputPassThroughElement = _frame;
            _popup.Closed += OnPopupClosed;
        }
        if (_options is not null)
        {
            _options.Host = this;
        }
        SyncInput();
        UpdateClear();
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
            SyncInput();
            (ControlAutomationPeer.FromElement(this) as SelectFieldAutomationPeer)?.RaiseExpandChanged(open);
        }
        else if (change.Property == SelectedIndexProperty)
        {
            var index = change.GetNewValue<int>();
            SelectedText = index >= 0 ? _entries.TextAt(index) : null;
            SyncInput();
            UpdateClear();
            _options?.InvalidateRows();
            PlaySettle(index >= 0);
        }
        else if (change.Property == PlaceholderProperty)
        {
            SyncInput();
        }
        else if (change.Property == TextSelectorProperty || change.Property == DisabledSelectorProperty ||
                 change.Property == DisplayMemberBindingProperty)
        {
            _entries.Invalidate();
        }
        else if (change.Property == FilterPredicateProperty)
        {
            if (IsOpen)
            {
                Refilter();
            }
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && !IsKeyboardFocusWithin && IsOpen)
        {
            // Focus left the field (Tab or a click elsewhere that takes focus): fold the list away.
            SetCurrentValue(IsOpenProperty, false);
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
            UpdateClear();
        }
    }

    private void OnEntriesChanged()
    {
        if (IsOpen)
        {
            Refilter();
        }
        Dispatcher.UIThread.Post(() =>
        {
            SelectedText = SelectedIndex >= 0 ? _entries.TextAt(SelectedIndex) : null;
            SyncInput();
        }, DispatcherPriority.Background);
    }

    /// <summary>The field shows the query while open and the chosen label (not the value) while closed.</summary>
    private void SyncInput()
    {
        if (_input is null)
        {
            return;
        }
        var text = IsOpen ? _query : SelectedText ?? string.Empty;
        // While searching, the chosen label stays in place as muted placeholder copy instead of vanishing.
        var placeholder = IsOpen ? SelectedText ?? Placeholder : Placeholder;
        _syncingText = true;
        try
        {
            if (_input.Text != text)
            {
                _input.Text = text;
                _input.CaretIndex = text.Length;
            }
            _input.SetValue(TextBox.PlaceholderTextProperty, placeholder, BindingPriority.Template);
        }
        finally
        {
            _syncingText = false;
        }
    }

    private void UpdateClear()
    {
        if (_clearHost is not null)
        {
            _clearHost.IsPresent = SelectedIndex >= 0 && IsEffectivelyEnabled;
        }
    }

    private void SetQuery(string query)
    {
        SearchText = query;
        if (IsOpen)
        {
            Refilter();
        }
        SyncInput();
    }

    // ---------------------------------------------------------------- popup

    private void OpenMenu()
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        SearchText = string.Empty;
        if (IsOpen)
        {
            Refilter();
            SyncInput();
            return;
        }
        SetCurrentValue(IsOpenProperty, true);
    }

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
        // Arc positions the popover 8px under the whole field, so it hangs below the description when there is one.
        _popup.PlacementTarget = _description is { IsVisible: true } ? _description : _frame;
        if (_surface is not null && _frame is not null && _frame.Bounds.Width > 0)
        {
            _surface.Width = _frame.Bounds.Width;
        }
        if (_scroll is not null)
        {
            var window = TopLevel.GetTopLevel(this)?.ClientSize.Height ?? double.PositiveInfinity;
            _scroll.MaxHeight = Math.Min(MaxDropDownHeight, window * 0.4);
        }
        Refilter();
        if (!_popup.IsOpen)
        {
            _popup.IsOpen = true;
        }
    }

    private void ClosePopup()
    {
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        SearchText = string.Empty;
        _options?.SetActive(-1, animate: false);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void Refilter()
    {
        if (_options is null)
        {
            return;
        }
        _entries.Filter(_query, FilterPredicate, _visible);
        _options.SetOptions(_visible);
        _scroll?.SetCurrentValue(ScrollViewer.OffsetProperty, default(Vector));
        if (_emptyHost is not null)
        {
            _emptyHost.IsPresent = _visible.Count == 0;
        }
        Announce(_query.Trim().Length == 0
            ? string.Empty
            : _visible.Count == 0 ? EmptyText ?? string.Empty : $"{_visible.Count} {(_visible.Count == 1 ? "result" : "results")}");
    }

    private void Announce(string text)
    {
        // An empty live region makes Avalonia.Native raise a macOS accessibility notification with a nil value, which
        // terminates the app (NSInvalidArgumentException), so a clear leaves the last message in place.
        if (_announcer is not null && text.Length > 0)
        {
            _announcer.Text = text;
        }
    }

    // ---------------------------------------------------------------- input

    private void OnInputGotFocus(object? sender, FocusChangedEventArgs e) => OpenMenu();

    private void OnInputPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen)
        {
            OpenMenu();
        }
    }

    /// <summary>Typing (synchronous Text changes that did not come from <see cref="SyncInput"/>) searches.</summary>
    private void OnInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncingText || _input is null)
        {
            return;
        }
        SearchText = _input.Text ?? string.Empty;
        if (!IsOpen)
        {
            SetCurrentValue(IsOpenProperty, true);
        }
        else
        {
            Refilter();
        }
        _options?.SetActive(-1, animate: false);
        SyncInput();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEffectivelyEnabled || _options is null)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
            {
                e.Handled = true;
                if (!IsOpen)
                {
                    OpenMenu();
                    return;
                }
                var next = Step(_options.ActiveOption, e.Key == Key.Down ? 1 : -1);
                if (next >= 0)
                {
                    SetActive(next, animate: false);
                }
                return;
            }
            case Key.Enter when IsOpen && _options.ActiveOption >= 0:
                e.Handled = true;
                Choose(_options.ActiveOption);
                return;
            case Key.Escape when IsOpen:
                e.Handled = true;
                SetCurrentValue(IsOpenProperty, false);
                return;
            case Key.Tab when IsOpen:
                SetCurrentValue(IsOpenProperty, false);
                return;
        }
    }

    /// <summary>Next enabled option in the filtered list, wrapping (Arc's combobox loops).</summary>
    private int Step(int from, int step)
    {
        var count = _visible.Count;
        if (count == 0)
        {
            return -1;
        }
        var index = from;
        for (var i = 0; i < count; i++)
        {
            index = from < 0 && i == 0
                ? (step > 0 ? 0 : count - 1)
                : ((index + step) % count + count) % count;
            if (_entries.IsEnabledAt(_visible[index]))
            {
                return index;
            }
        }
        return -1;
    }

    private void SetActive(int position, bool animate)
    {
        if (_options is null)
        {
            return;
        }
        _options.SetActive(position, animate);
        _options.ScrollIntoView(position);
        Announce(SelectOptionView.Describe(_entries, _options.EntryAt(position), position, _visible.Count));
    }

    private void Choose(int position)
    {
        var entry = _options?.EntryAt(position) ?? -1;
        if (entry < 0 || !_entries.IsEnabledAt(entry))
        {
            return;
        }
        SearchText = string.Empty;
        SetCurrentValue(IsOpenProperty, false);
        SetCurrentValue(SelectedIndexProperty, entry);
        SyncInput();
        _input?.Focus(NavigationMethod.Pointer);
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(SelectedIndexProperty, -1);
        SearchText = string.Empty;
        _input?.Focus(NavigationMethod.Pointer);
        if (!IsOpen)
        {
            SetCurrentValue(IsOpenProperty, true);
        }
        else
        {
            Refilter();
        }
        SyncInput();
    }

    /// <summary>
    /// A chosen label settles into the field: it rises 0.35em out of a soft blur. Clearing fades the placeholder in.
    /// Code-driven because it is a one-shot keyed to the commit, which a ControlTheme transition cannot express.
    /// </summary>
    private void PlaySettle(bool chosen)
    {
        if (_input is null || _settleShift is null || !IsLoaded || IsOpen)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level != ArcMotionLevel.Full)
        {
            return;
        }
        _settle ??= CreateSettle();
        var travel = 0.35 * _input.FontSize;
        _settle.Jump(0);
        _settleTravel = chosen ? travel : 0;
        _settleBlurRadius = chosen ? 4 : 0;
        ApplySettle(0);
        _ = _settle.AnimateTo(1, MotionSpec.Tween(chosen ? ArcMotionPresets.Standard : ArcMotionPresets.Fast, ArcEasings.Enter));
    }

    private double _settleTravel;
    private double _settleBlurRadius;

    private MotionValue<double> CreateSettle()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += ApplySettle;
        return value;
    }

    private void ApplySettle(double p)
    {
        if (_input is null || _settleShift is null)
        {
            return;
        }
        _input.Opacity = p;
        _settleShift.Y = _settleTravel * (1 - p);
        var radius = _settleBlurRadius * (1 - p);
        if (radius < 0.05)
        {
            _input.Effect = null;
        }
        else
        {
            _input.Effect = ArcBlur.Get(radius);
        }
    }

    // ---------------------------------------------------------------- focus

    // ---------------------------------------------------------------- ISelectOptionHost

    bool ISelectOptionHost.IsEntrySelected(int entryIndex) => entryIndex == SelectedIndex;

    void ISelectOptionHost.OnOptionHovered(int position) => _options?.SetActive(position, animate: false);

    void ISelectOptionHost.OnOptionInvoked(int position) => Choose(position);

    void ISelectOptionHost.OnOptionsPointerExited()
    {
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        SetCurrentValue(IsOpenProperty, false);
    }
}
