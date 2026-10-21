using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>Raised when <see cref="DatePicker.SelectedDate"/> changes.</summary>
public sealed class DatePickerSelectedDateChangedEventArgs : RoutedEventArgs
{
    public DatePickerSelectedDateChangedEventArgs(RoutedEvent routedEvent, DateOnly? oldDate, DateOnly? newDate)
        : base(routedEvent)
    {
        OldDate = oldDate;
        NewDate = newDate;
    }

    /// <summary>The previous date (null when there was none).</summary>
    public DateOnly? OldDate { get; }

    /// <summary>The new date (null after Clear or an emptied field).</summary>
    public DateOnly? NewDate { get; }
}

/// <summary>
/// Arc date field: type a date (parsed with <see cref="Culture"/>) or open the calendar popup (click, Alt+Down or
/// ArrowDown) and pick a day. The shown date rolls part by part in time's direction (a later date rises from
/// below, an earlier one drops from above; parts after a wider or narrower part slide along), the picked day's disc
/// glides in the calendar and the footer confirms it before the popup returns to the field. Escape closes and keeps
/// focus in the field; <see cref="MinDate"/>, <see cref="MaxDate"/> and <see cref="DisabledDates"/> reach the
/// calendar and also reject typed dates. Label, description, error and size come from the field foundation.
/// </summary>
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(DropDownButtonPart, typeof(AvaloniaButton))]
[TemplatePart(ValuePart, typeof(Control))]
[TemplatePart(PlaceholderPart, typeof(TextBlock))]
[TemplatePart(TextPresenterPart, typeof(Control))]
[PseudoClasses(OpenPseudoClass)]
public class DatePicker : FieldTextBox
{
    public const string PopupPart = "PART_Popup";
    public const string DropDownButtonPart = "PART_DropDownButton";
    public const string ValuePart = "PART_Value";
    public const string PlaceholderPart = "PART_Placeholder";
    public const string TextPresenterPart = "PART_TextPresenter";

    /// <summary>Set while the calendar popup is open.</summary>
    public const string OpenPseudoClass = ":open";

    #region Properties

    /// <summary>The picked date (two-way). Null shows the placeholder.</summary>
    public static readonly StyledProperty<DateOnly?> SelectedDateProperty =
        AvaloniaProperty.Register<DatePicker, DateOnly?>(nameof(SelectedDate), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the calendar popup is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<DatePicker, bool>(nameof(IsDropDownOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DateOnly?> MinDateProperty =
        Calendar.MinDateProperty.AddOwner<DatePicker>();

    public static readonly StyledProperty<DateOnly?> MaxDateProperty =
        Calendar.MaxDateProperty.AddOwner<DatePicker>();

    /// <summary>Extra disabled days (weekends, booked dates...), passed to the calendar.</summary>
    public static readonly StyledProperty<Func<DateOnly, bool>?> DisabledDatesProperty =
        Calendar.DisabledDatesProperty.AddOwner<DatePicker>();

    /// <summary>Culture for formatting, parsing and the calendar (null = current UI culture).</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        Calendar.CultureProperty.AddOwner<DatePicker>();

    /// <summary>
    /// .NET date pattern of the shown value. Null uses the culture's long date without the weekday and with the
    /// abbreviated month (en-US "Oct 20, 2026", Arc's default <c>{ month: "short", day: "numeric", year: "numeric" }</c>).
    /// </summary>
    public static readonly StyledProperty<string?> DisplayFormatProperty =
        AvaloniaProperty.Register<DatePicker, string?>(nameof(DisplayFormat));

    /// <summary>Adds a Today button to the calendar header.</summary>
    public static readonly StyledProperty<bool> ShowTodayProperty =
        Calendar.ShowTodayProperty.AddOwner<DatePicker>();

    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        Calendar.FirstDayOfWeekProperty.AddOwner<DatePicker>();

    /// <summary>Footer button that clears the value (Arc: "Clear").</summary>
    public static readonly StyledProperty<string> ClearTextProperty =
        AvaloniaProperty.Register<DatePicker, string>(nameof(ClearText), "Clear");

    /// <summary>Footer status without a value (Arc: "Choose a day").</summary>
    public static readonly StyledProperty<string> EmptyStatusTextProperty =
        AvaloniaProperty.Register<DatePicker, string>(nameof(EmptyStatusText), "Choose a day");

    /// <summary>Footer status with a value; {0} is the formatted date (Arc: "Selected {0}").</summary>
    public static readonly StyledProperty<string> SelectedStatusFormatProperty =
        AvaloniaProperty.Register<DatePicker, string>(nameof(SelectedStatusFormat), "Selected {0}");

    /// <summary>Raised when <see cref="SelectedDate"/> changes.</summary>
    public static readonly RoutedEvent<DatePickerSelectedDateChangedEventArgs> SelectedDateChangedEvent =
        RoutedEvent.Register<DatePicker, DatePickerSelectedDateChangedEventArgs>(nameof(SelectedDateChanged), RoutingStrategies.Bubble);

    /// <summary>The picked date.</summary>
    public DateOnly? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }

    /// <summary>Whether the calendar popup is open.</summary>
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }

    /// <summary>Earliest selectable date.</summary>
    public DateOnly? MinDate { get => GetValue(MinDateProperty); set => SetValue(MinDateProperty, value); }

    /// <summary>Latest selectable date.</summary>
    public DateOnly? MaxDate { get => GetValue(MaxDateProperty); set => SetValue(MaxDateProperty, value); }

    /// <summary>Extra disabled days.</summary>
    public Func<DateOnly, bool>? DisabledDates { get => GetValue(DisabledDatesProperty); set => SetValue(DisabledDatesProperty, value); }

    /// <summary>Culture for formatting, parsing and the calendar.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <summary>.NET date pattern of the shown value.</summary>
    public string? DisplayFormat { get => GetValue(DisplayFormatProperty); set => SetValue(DisplayFormatProperty, value); }

    /// <summary>Adds a Today button to the calendar header.</summary>
    public bool ShowToday { get => GetValue(ShowTodayProperty); set => SetValue(ShowTodayProperty, value); }

    /// <summary>First column of the calendar.</summary>
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>Footer clear button text.</summary>
    public string ClearText { get => GetValue(ClearTextProperty); set => SetValue(ClearTextProperty, value); }

    /// <summary>Footer status without a value.</summary>
    public string EmptyStatusText { get => GetValue(EmptyStatusTextProperty); set => SetValue(EmptyStatusTextProperty, value); }

    /// <summary>Footer status with a value ({0} = formatted date).</summary>
    public string SelectedStatusFormat { get => GetValue(SelectedStatusFormatProperty); set => SetValue(SelectedStatusFormatProperty, value); }

    /// <summary>Raised when <see cref="SelectedDate"/> changes.</summary>
    public event EventHandler<DatePickerSelectedDateChangedEventArgs>? SelectedDateChanged
    {
        add => AddHandler(SelectedDateChangedEvent, value);
        remove => RemoveHandler(SelectedDateChangedEvent, value);
    }

    #endregion

    /// <summary>Arc closes 300ms after a pick, so the disc settles and the footer confirms it first.</summary>
    internal static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(300);

    private ArcPopup? _popup;
    private AvaloniaButton? _dropDownButton;
    private PickerRollText? _value;
    private TextBlock? _placeholder;
    private Control? _textPresenter;
    private DatePickerPanel? _panel;
    private PickerShake? _shake;
    private readonly List<PickerRollSegment> _parts = new();
    private string _formatted = string.Empty;
    private bool _dirty;
    private bool _syncingText;
    private bool _keyboardOpen;
    private IDisposable? _closeTimer;
    private DatePickerAutomationPeer? _peer;
    private Field? _field;
    private MotionValue<double>? _hintFade;

    static DatePicker()
    {
        PlaceholderProperty.OverrideDefaultValue<DatePicker>("Select a date");
        PlaceholderTextProperty.OverrideDefaultValue<DatePicker>("Select a date");
        AcceptsReturnProperty.OverrideDefaultValue<DatePicker>(false);
    }

    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentUICulture;

    internal string EffectivePattern => PickerDateFormat.Effective(DisplayFormat, EffectiveCulture);

    /// <summary>The value as shown (empty without a value).</summary>
    public string FormattedValue => _formatted;

    internal DatePickerPanel? Panel => _panel;

    internal Calendar? PopupCalendar => _panel?.Calendar;

    internal ArcPopup? Popup => _popup;

    /// <summary>True when the date may be picked (inside the limits and not disabled).</summary>
    public bool IsDateAllowed(DateOnly date) =>
        !(MinDate is { } min && date < min) && !(MaxDate is { } max && date > max) && DisabledDates?.Invoke(date) != true;

    /// <summary>Reads typed text. Override to accept other notations; the default tries the display pattern, the culture's patterns, then a lenient parse.</summary>
    protected virtual bool TryParseDate(string text, out DateOnly date) =>
        PickerDateFormat.TryParse(text, EffectivePattern, EffectiveCulture, out date);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_dropDownButton is not null)
        {
            _dropDownButton.Click -= OnDropDownButtonClick;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.Opened -= OnPopupOpened;
            _popup.Child = null;
        }
        if (Frame is not null)
        {
            Frame.RemoveHandler(PointerPressedEvent, OnFramePressed);
        }
        if (_value is not null)
        {
            _value.Settled -= OnValueSettled;
        }
        base.OnApplyTemplate(e);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _dropDownButton = e.NameScope.Find<AvaloniaButton>(DropDownButtonPart);
        _value = e.NameScope.Find<PickerRollText>(ValuePart);
        _placeholder = e.NameScope.Find<TextBlock>(PlaceholderPart);
        _textPresenter = e.NameScope.Find<Control>(TextPresenterPart);
        _field = e.NameScope.Find<Field>("PART_Field");
        _hintFade = null;
        if (_dropDownButton is not null)
        {
            _dropDownButton.Click += OnDropDownButtonClick;
        }
        if (Frame is not null)
        {
            Frame.AddHandler(PointerPressedEvent, OnFramePressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _shake = new PickerShake(Frame);
        }
        if (_value is not null)
        {
            _value.Settled += OnValueSettled;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = Frame;
            _popup.OverlayInputPassThroughElement = Frame;
            _popup.Closed += OnPopupClosed;
            _popup.Opened += OnPopupOpened;
            if (_panel is not null)
            {
                _popup.Child = _panel;
            }
        }
        _panel = _popup?.Surface as DatePickerPanel;
        SyncText();
        UpdateValue(animate: false, direction: 1);
        UpdateFrame();
        if (IsDropDownOpen)
        {
            OpenPopup();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new DatePickerAutomationPeer(this);

    #endregion

    #region Value

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDateProperty)
        {
            var oldDate = change.GetOldValue<DateOnly?>();
            var newDate = change.GetNewValue<DateOnly?>();
            var direction = oldDate is { } o && newDate is { } n && n < o ? -1 : 1;
            SyncText();
            UpdateValue(animate: true, direction);
            SyncPanel();
            RaiseEvent(new DatePickerSelectedDateChangedEventArgs(SelectedDateChangedEvent, oldDate, newDate));
        }
        else if (change.Property == TextProperty)
        {
            if (!_syncingText)
            {
                _dirty = (Text ?? string.Empty) != _formatted;
                PreviewTypedDate();
                UpdateOverlay();
            }
        }
        else if (change.Property == IsDropDownOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                OpenPopup();
            }
            else
            {
                ClosePopup();
            }
        }
        else if (change.Property == CultureProperty || change.Property == DisplayFormatProperty)
        {
            if (!_dirty)
            {
                SyncText();
            }
            UpdateValue(animate: false, direction: 1);
            SyncPanel();
        }
        else if (change.Property == MinDateProperty || change.Property == MaxDateProperty ||
                 change.Property == DisabledDatesProperty || change.Property == ShowTodayProperty ||
                 change.Property == FirstDayOfWeekProperty || change.Property == EmptyStatusTextProperty ||
                 change.Property == SelectedStatusFormatProperty)
        {
            SyncPanel();
        }
        else if (change.Property == IsFocusedProperty)
        {
            UpdateFrame();
            UpdateOverlay();
        }
    }

    /// <summary>Shows the formatted value in the editor (not while the user's own edit is pending).</summary>
    private void SyncText()
    {
        _formatted = SelectedDate is { } date ? PickerDateFormat.Format(date, EffectivePattern, EffectiveCulture) : string.Empty;
        _syncingText = true;
        try
        {
            SetCurrentValue(TextProperty, _formatted);
            if (IsFocused)
            {
                SetCurrentValue(CaretIndexProperty, _formatted.Length);
            }
        }
        finally
        {
            _syncingText = false;
        }
        _dirty = false;
        UpdateOverlay();
    }

    /// <summary>Rolls the shown copy: changed parts only, in time's direction; the placeholder drops in from above.</summary>
    private void UpdateValue(bool animate, int direction)
    {
        if (_value is null)
        {
            return;
        }
        if (SelectedDate is { } date)
        {
            PickerDateFormat.FormatParts(date, EffectivePattern, EffectiveCulture, _parts);
            _value.Show(_parts, placeholder: false, direction, animate, exitDirection: 1);
        }
        else
        {
            _value.Show(Placeholder ?? string.Empty, placeholder: true, direction: -1, animate, exitDirection: 1);
        }
        UpdateOverlay();
        UpdateValueAutomation();
    }

    private void OnValueSettled(object? sender, EventArgs e) => UpdateOverlay();

    /// <summary>
    /// The rolling copy covers the editor while the field is at rest (or while a roll is still playing); the editor's
    /// own text returns for editing, so the caret, selection and IME always work on real text.
    /// </summary>
    private void UpdateOverlay()
    {
        if (_value is null)
        {
            return;
        }
        var rolling = _value.IsRolling;
        var overlay = !_dirty && (!IsFocused || rolling || SelectedDate is null);
        _value.IsVisible = overlay;
        if (_textPresenter is not null)
        {
            _textPresenter.Opacity = overlay && SelectedDate is not null ? 0 : 1;
        }
        if (_placeholder is not null)
        {
            _placeholder.IsVisible = !overlay && string.IsNullOrEmpty(Text);
        }
    }

    /// <summary>A typed date that parses moves the open calendar to its month (it is picked on Enter or blur).</summary>
    private void PreviewTypedDate()
    {
        if (!IsDropDownOpen || PopupCalendar is not { } calendar || string.IsNullOrWhiteSpace(Text))
        {
            return;
        }
        if (TryParseDate(Text!, out var date))
        {
            calendar.GoToMonth(date, raise: true);
        }
    }

    /// <summary>Commits typed text: empty clears, a valid allowed date is picked, anything else shakes and reverts.</summary>
    internal bool CommitText()
    {
        if (!_dirty)
        {
            return true;
        }
        var text = Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetCurrentValue(SelectedDateProperty, null);
            SyncText();
            return true;
        }
        if (TryParseDate(text, out var date) && IsDateAllowed(date))
        {
            if (SelectedDate == date)
            {
                SyncText();
            }
            else
            {
                SetCurrentValue(SelectedDateProperty, date);
            }
            return true;
        }
        SyncText();
        _shake?.Play();
        return false;
    }

    private void UpdateValueAutomation()
    {
        var label = Label;
        var shown = SelectedDate is null ? Placeholder : _formatted;
        AutomationProperties.SetHelpText(this, FieldText.HelpText(string.IsNullOrEmpty(Description) ? shown : $"{shown}. {Description}", EffectiveErrorMessage));
        if (_panel is not null)
        {
            AutomationProperties.SetName(_panel, $"{label} calendar".Trim());
        }
    }

    #endregion

    #region Popup

    private void OnDropDownButtonClick(object? sender, RoutedEventArgs e)
    {
        _keyboardOpen = false;
        SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);
        if (!IsFocused && !(_panel?.IsKeyboardFocusWithin ?? false))
        {
            Focus(NavigationMethod.Pointer);
        }
    }

    private void OnFramePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || IsDropDownOpen || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (e.Source is Visual source && _dropDownButton is not null &&
            (ReferenceEquals(source, _dropDownButton) || _dropDownButton.IsVisualAncestorOf(source)))
        {
            return;
        }
        // Arc: a press on the field opens the calendar; the caret stays in the text (a pointer open moves no focus).
        _keyboardOpen = false;
        SetCurrentValue(IsDropDownOpenProperty, true);
    }

    private void EnsurePanel()
    {
        if (_panel is not null || _popup is null)
        {
            return;
        }
        _panel = new DatePickerPanel();
        _popup.Child = _panel;
        _panel.ApplyTemplate();
        if (_panel.Calendar is { } calendar)
        {
            calendar.DateSelected += OnCalendarDateSelected;
        }
        if (_panel.ClearButton is { } clear)
        {
            clear.Click += OnClearClick;
        }
        _panel.KeyDown += OnPanelKeyDown;
        _panel.LostFocus += OnPanelLostFocus;
        UpdateValueAutomation();
    }

    private void SyncPanel()
    {
        if (_panel is null)
        {
            return;
        }
        if (_panel.Calendar is { } calendar)
        {
            calendar.MinDate = MinDate;
            calendar.MaxDate = MaxDate;
            calendar.DisabledDates = DisabledDates;
            calendar.ShowToday = ShowToday;
            calendar.FirstDayOfWeek = FirstDayOfWeek;
            calendar.Culture = Culture;
            if (calendar.SelectedDate != SelectedDate)
            {
                calendar.SelectedDate = SelectedDate;
            }
        }
        if (_panel.ClearButton is { } clear)
        {
            clear.Content = ClearText;
            clear.IsEnabled = SelectedDate is not null;
        }
        if (_panel.Status is { } status)
        {
            var text = SelectedDate is not null ? string.Format(EffectiveCulture, SelectedStatusFormat, _formatted) : EmptyStatusText;
            status.Show(text, placeholder: false, direction: 1, animate: _popup?.IsOpen == true);
        }
    }

    private void OpenPopup()
    {
        _closeTimer?.Dispose();
        _closeTimer = null;
        if (_popup is null || !IsEffectivelyEnabled)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
            }
            return;
        }
        EnsurePanel();
        SyncPanel();
        // The calendar always opens on the month of the value, or today's.
        PopupCalendar?.GoToMonth(SelectedDate ?? ArcToday.Today, raise: true);
        SizePanel();
        PseudoClasses.Set(OpenPseudoClass, true);
        UpdateFrame();
        FadeHint(hidden: true);
        if (!_popup.IsOpen)
        {
            _popup.IsOpen = true;
        }
        _peer?.RaiseExpandChanged(true);
    }

    /// <summary>Arc: min(320px, viewport − 32px); on phone widths the popup matches the field with a tighter inset.</summary>
    private void SizePanel()
    {
        if (_panel is null || _popup is null)
        {
            return;
        }
        var viewport = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? double.PositiveInfinity;
        var narrow = viewport <= 360;
        _panel.IsCompact = narrow;
        _panel.Width = narrow && Frame is not null ? Frame.Bounds.Width : Math.Min(_panel.PreferredWidth, Math.Max(0, viewport - 32));
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (!_keyboardOpen)
        {
            return;
        }
        _keyboardOpen = false;
        // Opening from the keyboard moves focus to the day that owns the tab stop, so arrows work at once.
        Dispatcher.UIThread.Post(FocusCalendar, DispatcherPriority.Loaded);
    }

    internal void FocusCalendar()
    {
        if (IsDropDownOpen)
        {
            PopupCalendar?.FocusTabbableDay();
        }
    }

    private void ClosePopup()
    {
        _closeTimer?.Dispose();
        _closeTimer = null;
        PseudoClasses.Set(OpenPseudoClass, false);
        UpdateFrame();
        FadeHint(hidden: false);
        if (_popup is null)
        {
            return;
        }
        // Closing hands focus back to the field when it was inside the calendar.
        var focusInside = _panel?.IsKeyboardFocusWithin == true;
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
        }
        if (focusInside)
        {
            Focus(NavigationMethod.Unspecified);
        }
        _peer?.RaiseExpandChanged(false);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    private void OnCalendarDateSelected(object? sender, CalendarDateSelectedEventArgs e) => SelectDate(e.Date);

    private void OnClearClick(object? sender, RoutedEventArgs e) => SelectDate(null);

    /// <summary>A picked day lets the disc glide onto it and the footer confirm it, then the calendar returns to the field.</summary>
    private void SelectDate(DateOnly? date)
    {
        SetCurrentValue(SelectedDateProperty, date);
        _closeTimer?.Dispose();
        _closeTimer = null;
        if (date is null || !IsLoaded || !ArcMotion.IsFull(this))
        {
            Close();
            return;
        }
        _closeTimer = DispatcherTimer.RunOnce(Close, CloseDelay);
    }

    private void Close()
    {
        _closeTimer?.Dispose();
        _closeTimer = null;
        var focusInside = _panel?.IsKeyboardFocusWithin == true;
        SetCurrentValue(IsDropDownOpenProperty, false);
        if (focusInside)
        {
            Focus(NavigationMethod.Unspecified);
        }
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.Escape || (e.Key == Key.Up && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnPanelLostFocus(object? sender, RoutedEventArgs e) => Dispatcher.UIThread.Post(CheckFocusLeft, DispatcherPriority.Input);

    /// <summary>Tabbing out of the field closes the calendar and commits the typed text; focus moving within the field keeps it.</summary>
    private void CheckFocusLeft()
    {
        if (IsFocused || _panel?.IsKeyboardFocusWithin == true)
        {
            return;
        }
        CommitText();
        if (IsDropDownOpen && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not null)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    /// <summary>The open calendar covers the description; it fades (fast) so its first letters never peek past the popup's corner.</summary>
    private void FadeHint(bool hidden)
    {
        if (_hintFade is null)
        {
            var hint = _field?.GetVisualDescendants().OfType<FieldMessage>().FirstOrDefault(m => m.Name == Field.DescriptionPart);
            if (hint is null)
            {
                return;
            }
            _hintFade = new MotionValue<double>(hint, 1, MotionVectors.Double);
            _hintFade.Changed += v => hint.Opacity = v;
        }
        _ = _hintFade.AnimateTo(hidden ? 0 : 1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    private void UpdateFrame()
    {
        if (Frame is not null)
        {
            Frame.IsFocusActive = IsFocused;
        }
    }

    #endregion

    #region Input routing

    /// <summary>Events from the popup bubble here through the popup's logical parent; only the editor's own reach the text box.</summary>
    private bool FromPopup(object? source) =>
        source is Visual visual && _panel is not null && (ReferenceEquals(visual, _panel) || _panel.IsVisualAncestorOf(visual));

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (FromPopup(e.Source))
        {
            return;
        }
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        switch (e.Key)
        {
            case Key.Down when alt || !IsDropDownOpen:
                e.Handled = true;
                CommitText();
                if (IsDropDownOpen)
                {
                    FocusCalendar();
                }
                else
                {
                    _keyboardOpen = true;
                    SetCurrentValue(IsDropDownOpenProperty, true);
                }
                return;
            case Key.Down:
                e.Handled = true;
                CommitText();
                FocusCalendar();
                return;
            case Key.Up when alt && IsDropDownOpen:
                e.Handled = true;
                Close();
                return;
            case Key.Escape when IsDropDownOpen:
                e.Handled = true;
                Close();
                return;
            case Key.Escape when _dirty:
                e.Handled = true;
                SyncText();
                return;
            case Key.Enter:
                e.Handled = true;
                if (_dirty)
                {
                    CommitText();
                }
                else
                {
                    // Arc: Enter on the trigger opens the calendar (and moves focus into it).
                    _keyboardOpen = !IsDropDownOpen;
                    SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);
                }
                return;
            case Key.Tab when IsDropDownOpen && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                // Tab from the field continues into the open calendar.
                e.Handled = true;
                CommitText();
                FocusCalendar();
                return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (FromPopup(e.Source))
        {
            return;
        }
        base.OnKeyUp(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (FromPopup(e.Source))
        {
            return;
        }
        base.OnTextInput(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (FromPopup(e.Source))
        {
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (FromPopup(e.Source))
        {
            return;
        }
        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (FromPopup(e.Source))
        {
            return;
        }
        base.OnPointerReleased(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        if (FromPopup(e.Source))
        {
            return;
        }
        base.OnGotFocus(e);
        UpdateOverlay();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        if (FromPopup(e.Source))
        {
            return;
        }
        base.OnLostFocus(e);
        Dispatcher.UIThread.Post(CheckFocusLeft, DispatcherPriority.Input);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _closeTimer?.Dispose();
        _closeTimer = null;
        if (IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    #endregion

    /// <summary>Text box peer plus expand / collapse for the calendar (Arc: aria-haspopup="dialog", aria-expanded).</summary>
    private sealed class DatePickerAutomationPeer : TextBoxAutomationPeer, IExpandCollapseProvider
    {
        public DatePickerAutomationPeer(DatePicker owner) : base(owner)
        {
        }

        private DatePicker Picker => (DatePicker)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Picker.IsDropDownOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => Picker.SetCurrentValue(IsDropDownOpenProperty, true);

        public void Collapse() => Picker.SetCurrentValue(IsDropDownOpenProperty, false);

        internal void RaiseExpandChanged(bool open) => RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
            open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }
}
