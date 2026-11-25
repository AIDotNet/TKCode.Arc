using System.Globalization;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>How a <see cref="TimePicker"/> shows times.</summary>
public enum TimePickerClockFormat
{
    /// <summary>From the culture's short time pattern ("H" → 24-hour, otherwise 12-hour).</summary>
    Auto,

    /// <summary>"9:15 AM" (Arc's default).</summary>
    TwelveHour,

    /// <summary>"09:15".</summary>
    TwentyFourHour
}

/// <summary>Raised when <see cref="TimePicker.SelectedTime"/> changes.</summary>
public sealed class TimePickerSelectedTimeChangedEventArgs : RoutedEventArgs
{
    public TimePickerSelectedTimeChangedEventArgs(RoutedEvent routedEvent, TimeOnly? oldTime, TimeOnly? newTime)
        : base(routedEvent)
    {
        OldTime = oldTime;
        NewTime = newTime;
    }

    public TimeOnly? OldTime { get; }

    public TimeOnly? NewTime { get; }
}

/// <summary>
/// Arc time field: a trigger over a scrolling list of times generated every <see cref="MinuteStep"/> minutes. The
/// list opens centred on the chosen time, arrow keys move the highlight (the list glides to keep it in view, wrapping
/// at both ends), Enter or a click chooses and closes, Escape closes. The shown time rolls like a clock face: a later
/// time rises from below, an earlier one drops from above. 12- or 24-hour display follows the culture unless
/// <see cref="ClockFormat"/> says otherwise; the value is always a 24-hour <see cref="TimeOnly"/>.
/// With <see cref="Precision"/> set to <see cref="TimePickerPrecision.Second"/> (or <see cref="DropDownLayout"/> set to
/// <see cref="TimePickerDropDownLayout.Columns"/>) the drop-down shows one column per part instead of the list.
/// </summary>
[TemplatePart(FramePart, typeof(FieldFrame))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(ValuePart, typeof(Control))]
[PseudoClasses(OpenPseudoClass, PressedPseudoClass)]
public partial class TimePicker : TemplatedControl, IPickerAutomation
{
    public const string FramePart = "PART_Frame";
    public const string PopupPart = "PART_Popup";
    public const string ValuePart = "PART_Value";
    public const string OpenPseudoClass = ":open";
    public const string PressedPseudoClass = ":pressed";

    #region Properties

    /// <summary>The chosen time (two-way). Arc's default is 09:00; null shows the placeholder.</summary>
    public static readonly StyledProperty<TimeOnly?> SelectedTimeProperty =
        AvaloniaProperty.Register<TimePicker, TimeOnly?>(nameof(SelectedTime), new TimeOnly(9, 0), defaultBindingMode: BindingMode.TwoWay,
            enableDataValidation: true);

    /// <summary>Whether the list is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<TimePicker, bool>(nameof(IsDropDownOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Minutes between options (Arc: 1, 5, 10, 15 or 30; default 15).</summary>
    public static readonly StyledProperty<int> MinuteStepProperty =
        AvaloniaProperty.Register<TimePicker, int>(nameof(MinuteStep), 15, coerce: (_, v) => Math.Clamp(v, 1, 720));

    /// <summary>
    /// Smallest part shown and chosen: <see cref="TimePickerPrecision.Minute"/> (default) or
    /// <see cref="TimePickerPrecision.Second"/>, which shows the seconds and, with the <c>Auto</c> layout, opens columns.
    /// </summary>
    public static readonly StyledProperty<TimePickerPrecision> PrecisionProperty =
        AvaloniaProperty.Register<TimePicker, TimePickerPrecision>(nameof(Precision));

    /// <summary>Seconds between the options of the seconds column (1 to 30; default 1).</summary>
    public static readonly StyledProperty<int> SecondStepProperty =
        AvaloniaProperty.Register<TimePicker, int>(nameof(SecondStep), 1, coerce: (_, v) => Math.Clamp(v, 1, 30));

    /// <summary>
    /// List or columns: <see cref="TimePickerDropDownLayout.Auto"/> (default) shows the list at minute precision and
    /// columns at second precision.
    /// </summary>
    public static readonly StyledProperty<TimePickerDropDownLayout> DropDownLayoutProperty =
        AvaloniaProperty.Register<TimePicker, TimePickerDropDownLayout>(nameof(DropDownLayout));

    /// <summary>
    /// Content before the value. The theme sets a clock icon (one per picker); set another icon or text to replace
    /// it, or <c>null</c> to hide it (the frame's prefix slot and its gap collapse).
    /// </summary>
    public static readonly StyledProperty<object?> PrefixProperty =
        FieldFrame.PrefixProperty.AddOwner<TimePicker>();

    public static readonly StyledProperty<TimePickerClockFormat> ClockFormatProperty =
        AvaloniaProperty.Register<TimePicker, TimePickerClockFormat>(nameof(ClockFormat));

    /// <summary>Culture for the clock format and the AM / PM designators (null = current UI culture).</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<TimePicker, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<string?> LabelProperty = Field.LabelProperty.AddOwner<TimePicker>();
    public static readonly StyledProperty<string?> DescriptionProperty = Field.DescriptionProperty.AddOwner<TimePicker>();
    public static readonly StyledProperty<string?> ErrorMessageProperty = Field.ErrorMessageProperty.AddOwner<TimePicker>();
    public static readonly StyledProperty<bool> IsLabelVisibleProperty =
        Field.IsLabelVisibleProperty.AddOwner<TimePicker>(new StyledPropertyMetadata<bool>(coerce: (o, v) => v && !FormFieldHost.IsHosted((TimePicker)o)));

    /// <summary>The error actually shown: <see cref="ErrorMessage"/>, else the validation message of the <see cref="SelectedTime"/> binding.</summary>
    public static readonly DirectProperty<TimePicker, string?> EffectiveErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<TimePicker, string?>(nameof(EffectiveErrorMessage), o => o.EffectiveErrorMessage);
    public static readonly StyledProperty<ControlSize> SizeProperty = Field.SizeProperty.AddOwner<TimePicker>();

    /// <summary>Shown while there is no time (Arc: "Select a time").</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<TimePicker, string>(nameof(Placeholder), "Select a time");

    /// <summary>Tallest the list grows before it scrolls (Arc: 250px).</summary>
    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        AvaloniaProperty.Register<TimePicker, double>(nameof(MaxDropDownHeight), 250);

    public static readonly RoutedEvent<TimePickerSelectedTimeChangedEventArgs> SelectedTimeChangedEvent =
        RoutedEvent.Register<TimePicker, TimePickerSelectedTimeChangedEventArgs>(nameof(SelectedTimeChanged), RoutingStrategies.Bubble);

    internal static readonly DirectProperty<TimePicker, bool> IsInvalidProperty =
        AvaloniaProperty.RegisterDirect<TimePicker, bool>(nameof(IsInvalid), o => o.IsInvalid);

    /// <summary>The chosen time.</summary>
    public TimeOnly? SelectedTime { get => GetValue(SelectedTimeProperty); set => SetValue(SelectedTimeProperty, value); }

    /// <summary>Whether the list is open.</summary>
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }

    /// <summary>Minutes between options.</summary>
    public int MinuteStep { get => GetValue(MinuteStepProperty); set => SetValue(MinuteStepProperty, value); }

    /// <summary>Smallest part shown and chosen (minutes by default, or seconds).</summary>
    public TimePickerPrecision Precision { get => GetValue(PrecisionProperty); set => SetValue(PrecisionProperty, value); }

    /// <summary>Seconds between the options of the seconds column.</summary>
    public int SecondStep { get => GetValue(SecondStepProperty); set => SetValue(SecondStepProperty, value); }

    /// <summary>Whether the drop-down is one list of times or one column per part.</summary>
    public TimePickerDropDownLayout DropDownLayout { get => GetValue(DropDownLayoutProperty); set => SetValue(DropDownLayoutProperty, value); }

    /// <summary>Content before the value (the theme's clock icon by default; null hides it).</summary>
    public object? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>12- or 24-hour display (Auto follows the culture).</summary>
    public TimePickerClockFormat ClockFormat { get => GetValue(ClockFormatProperty); set => SetValue(ClockFormatProperty, value); }

    /// <summary>Culture for the clock format.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <inheritdoc cref="Field.LabelPlacementProperty"/>
    public static readonly AttachedProperty<FormLabelPlacement> LabelPlacementProperty =
        Field.LabelPlacementProperty.AddOwner<TimePicker>();

    /// <inheritdoc cref="Field.LabelWidthProperty"/>
    public static readonly AttachedProperty<double> LabelWidthProperty =
        Field.LabelWidthProperty.AddOwner<TimePicker>();

    /// <inheritdoc cref="Field.LabelFontWeightProperty"/>
    public static readonly AttachedProperty<FontWeight?> LabelFontWeightProperty =
        Field.LabelFontWeightProperty.AddOwner<TimePicker>();

    /// <summary>Where the label sits: Top (default) or Left of the editor. Inherited from <c>arc:Field.LabelPlacement</c> on a container.</summary>
    public FormLabelPlacement LabelPlacement { get => GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <summary>Width of the left label column; NaN (default) takes the widest left label of the scope, up to 240 px.</summary>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>
    /// Font weight of the visible field label. Set it on a container to apply it to the Arc field labels inside,
    /// or on a single field. Null uses the label's theme weight. This property only styles the label text.
    /// </summary>
    public FontWeight? LabelFontWeight { get => GetValue(LabelFontWeightProperty); set => SetValue(LabelFontWeightProperty, value); }

    /// <summary>The visible label above the trigger (Arc: required).</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Helper copy under the trigger.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Error copy under the trigger; non-empty makes the field invalid.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>When false the label is hidden but still names the field.</summary>
    public bool IsLabelVisible { get => GetValue(IsLabelVisibleProperty); set => SetValue(IsLabelVisibleProperty, value); }

    /// <summary>Trigger height: Small 36, Medium 44 (default), Large 50.</summary>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>Shown while there is no time.</summary>
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>Tallest the list grows.</summary>
    public double MaxDropDownHeight { get => GetValue(MaxDropDownHeightProperty); set => SetValue(MaxDropDownHeightProperty, value); }

    /// <summary>True while an error is shown.</summary>
    public bool IsInvalid => _isInvalid;

    /// <summary>The error actually shown.</summary>
    public string? EffectiveErrorMessage
    {
        get => _effectiveError;
        private set => SetAndRaise(EffectiveErrorMessageProperty, ref _effectiveError, value);
    }

    private bool _isInvalid;
    private string? _effectiveError;
    private string? _validationMessage;

    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        base.UpdateDataValidation(property, state, error);
        _validationMessage = FormFieldHost.ValidationMessage(state, error);
        UpdateEffectiveError();
    }

    private void UpdateEffectiveError()
    {
        EffectiveErrorMessage = FormFieldHost.EffectiveError(this, ErrorMessage, _validationMessage);
        var invalid = FormFieldHost.IsInvalid(FormFieldHost.StateOf(this), !string.IsNullOrEmpty(ErrorMessage), _validationMessage is not null);
        if (invalid != _isInvalid)
        {
            _isInvalid = invalid;
            RaisePropertyChanged(IsInvalidProperty, !invalid, invalid);
        }
        UpdateAutomation();
    }

    /// <summary>Raised when <see cref="SelectedTime"/> changes.</summary>
    public event EventHandler<TimePickerSelectedTimeChangedEventArgs>? SelectedTimeChanged
    {
        add => AddHandler(SelectedTimeChangedEvent, value);
        remove => RemoveHandler(SelectedTimeChangedEvent, value);
    }

    #endregion

    private FieldFrame? _frame;
    private ArcPopup? _popup;
    private PickerRollText? _value;
    private TimePickerPanel? _panel;
    private PickerAutomationPeer? _peer;
    private MotionValue<double>? _scrollMotion;
    private bool _pressedOnFrame;
    private bool _centerPending;

    static TimePicker()
    {
        FocusableProperty.OverrideDefaultValue<TimePicker>(true);
        FocusAdornerProperty.OverrideDefaultValue<TimePicker>(null);
    }

    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentUICulture;

    /// <summary>Whether times show on a 12-hour clock.</summary>
    public bool IsTwelveHour => ClockFormat switch
    {
        TimePickerClockFormat.TwelveHour => true,
        TimePickerClockFormat.TwentyFourHour => false,
        _ => !EffectiveCulture.DateTimeFormat.ShortTimePattern.Contains('H')
    };

    /// <summary>Number of options (1440 / step, rounded up).</summary>
    public int OptionCount => (int)Math.Ceiling(1440.0 / MinuteStep);

    /// <summary>The time of option <paramref name="index"/>.</summary>
    public TimeOnly OptionAt(int index) => TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(index * MinuteStep % 1440));

    /// <summary>
    /// Formats a time the way the trigger and the list show it, with the seconds when <see cref="Precision"/> is
    /// <see cref="TimePickerPrecision.Second"/> ("9:15:30 AM", "09:15:30").
    /// </summary>
    public string FormatTime(TimeOnly time)
    {
        var seconds = Precision == TimePickerPrecision.Second
            ? ":" + time.Second.ToString("00", CultureInfo.InvariantCulture)
            : string.Empty;
        if (!IsTwelveHour)
        {
            return $"{time.Hour:00}:{time.Minute:00}{seconds}";
        }
        var hour = time.Hour % 12 == 0 ? 12 : time.Hour % 12;
        return $"{hour}:{time.Minute:00}{seconds} {Designator(time.Hour >= 12)}";
    }

    /// <summary>The culture's AM or PM word, or "AM" / "PM" when it has none.</summary>
    private string Designator(bool pm)
    {
        var info = EffectiveCulture.DateTimeFormat;
        if (pm)
        {
            return string.IsNullOrEmpty(info.PMDesignator) ? "PM" : info.PMDesignator;
        }
        return string.IsNullOrEmpty(info.AMDesignator) ? "AM" : info.AMDesignator;
    }

    /// <summary>The option at or just before the chosen time.</summary>
    internal int SelectedIndex => SelectedTime is { } time ? (int)Math.Min(OptionCount - 1, (time.Hour * 60 + time.Minute) / MinuteStep) : -1;

    internal int ExactSelectedIndex
    {
        get
        {
            if (SelectedTime is not { } time)
            {
                return -1;
            }
            var minutes = time.Hour * 60 + time.Minute;
            return minutes % MinuteStep == 0 && time.Second == 0 ? minutes / MinuteStep : -1;
        }
    }

    internal TimePickerList? List => _panel?.List;

    internal TimePickerPanel? Panel => _panel;

    internal ArcPopup? Popup => _popup;

    string? IPickerAutomation.AutomationLabel => Label;

    string? IPickerAutomation.AutomationValue => SelectedTime is { } t ? FormatTime(t) : Placeholder;

    bool IPickerAutomation.AutomationIsOpen
    {
        get => IsDropDownOpen;
        set => SetCurrentValue(IsDropDownOpenProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        _peer = new PickerAutomationPeer(this, this, AutomationControlType.ComboBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_frame is not null)
        {
            _frame.PointerPressed -= OnFramePressed;
            _frame.PointerReleased -= OnFrameReleased;
            _frame.PointerCaptureLost -= OnFrameCaptureLost;
            _frame.SizeChanged -= OnFrameSizeChanged;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.Opened -= OnPopupOpened;
            _popup.Child = null;
        }
        _frame = e.NameScope.Find<FieldFrame>(FramePart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _value = e.NameScope.Find<PickerRollText>(ValuePart);
        if (_frame is not null)
        {
            _frame.PointerPressed += OnFramePressed;
            _frame.PointerReleased += OnFrameReleased;
            _frame.PointerCaptureLost += OnFrameCaptureLost;
            _frame.SizeChanged += OnFrameSizeChanged;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = _frame;
            _popup.OverlayInputPassThroughElement = _frame;
            _popup.Closed += OnPopupClosed;
            _popup.Opened += OnPopupOpened;
            if (_panel is not null)
            {
                _popup.Child = _panel;
            }
        }
        UpdateValue(animate: false, direction: 1);
        UpdateAutomation();
        if (IsDropDownOpen)
        {
            OpenPopup();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedTimeProperty)
        {
            var oldTime = change.GetOldValue<TimeOnly?>();
            var newTime = change.GetNewValue<TimeOnly?>();
            var direction = oldTime is { } o && newTime is { } n && n < o ? -1 : 1;
            UpdateValue(animate: true, direction);
            List?.SetSelected(ExactSelectedIndex);
            UpdateColumnSelection();
            UpdateAutomation();
            RaiseEvent(new TimePickerSelectedTimeChangedEventArgs(SelectedTimeChangedEvent, oldTime, newTime));
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
        else if (change.Property == MinuteStepProperty || change.Property == ClockFormatProperty || change.Property == CultureProperty
                 || change.Property == PrecisionProperty || change.Property == SecondStepProperty || change.Property == DropDownLayoutProperty)
        {
            UpdateValue(animate: false, direction: 1);
            ConfigureList();
            UpdatePanelSize();
            UpdateAutomation();
        }
        else if (change.Property == PlaceholderProperty)
        {
            UpdateValue(animate: false, direction: 1);
        }
        else if (change.Property == ErrorMessageProperty)
        {
            UpdateEffectiveError();
        }
        else if (change.Property == FormFieldHost.StateProperty)
        {
            CoerceValue(IsLabelVisibleProperty);
            UpdateEffectiveError();
        }
        else if (change.Property == LabelProperty || change.Property == DescriptionProperty)
        {
            UpdateAutomation();
        }
    }

    private void UpdateValue(bool animate, int direction)
    {
        if (_value is null)
        {
            return;
        }
        if (SelectedTime is { } time)
        {
            _value.Show(FormatTime(time), placeholder: false, direction, animate);
        }
        else
        {
            _value.Show(Placeholder, placeholder: true, direction, animate);
        }
    }

    private void UpdateAutomation()
    {
        FormFieldHost.SetHelpText(this, FieldText.HelpText(Description, EffectiveErrorMessage));
        if (_panel is not null)
        {
            var name = string.IsNullOrWhiteSpace(Label)
                ? PickerLangResources.Get(PickerLangResourceKind.TimeOptions)
                : PickerLangResources.Format(PickerLangResourceKind.TimeOptionsFor, Label);
            AutomationProperties.SetName(_panel, name.Trim());
        }
    }

    // ---------------------------------------------------------------- popup

    private void EnsurePanel()
    {
        if (_panel is not null || _popup is null)
        {
            return;
        }
        _panel = new TimePickerPanel();
        _popup.Child = _panel;
        _panel.ApplyTemplate();
        if (_panel.List is { } list)
        {
            list.RowHovered += (_, index) => list.SetActive(index, animate: true);
            list.RowChosen += (_, index) => Choose(index);
        }
        if (_panel.Scroll is { } scroll)
        {
            scroll.LayoutUpdated += OnScrollLayoutUpdated;
        }
        HookColumns(_panel);
        UpdateAutomation();
    }

    private void ConfigureList()
    {
        if (_panel is null)
        {
            return;
        }
        _panel.SetLayout(UsesColumns, Precision == TimePickerPrecision.Second, IsTwelveHour);
        ConfigureColumns();
        if (List is not { } list)
        {
            return;
        }
        list.SetOptions(OptionCount, i => FormatTime(OptionAt(i)));
        list.SetSelected(ExactSelectedIndex);
    }

    private void OpenPopup()
    {
        if (_popup is null || !IsEffectivelyEnabled)
        {
            if (!IsEffectivelyEnabled)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
            }
            return;
        }
        EnsurePanel();
        ConfigureList();
        UpdatePanelSize();
        if (_panel?.UsesColumns == true)
        {
            ActivateColumns();
        }
        else
        {
            var start = SelectedIndex >= 0 ? SelectedIndex : 0;
            List?.SetActive(start, animate: false);
        }
        _centerPending = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        if (!_popup.IsOpen)
        {
            _popup.IsOpen = true;
        }
        _peer?.RaiseExpandChanged(true);
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        // The list lays out after the host shows it; centre once it has a size (the popup still fades in from 0).
        Dispatcher.UIThread.Post(() =>
        {
            if (_centerPending)
            {
                CenterActive();
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnScrollLayoutUpdated(object? sender, EventArgs e)
    {
        if (_centerPending && _panel?.Scroll is { Viewport.Height: > 0 })
        {
            CenterActive();
        }
    }

    /// <summary>Scroll only the list, never the page: the chosen time opens centred.</summary>
    private void CenterActive()
    {
        if (_panel?.UsesColumns == true)
        {
            CenterColumns();
            return;
        }
        if (_panel?.Scroll is not { } scroll || List is not { } list || list.ActiveIndex < 0 || scroll.Viewport.Height <= 0)
        {
            return;
        }
        _centerPending = false;
        _scrollMotion?.Stop();
        CenterRow(scroll, list, list.ActiveIndex);
    }

    private static void CenterRow(ScrollViewer scroll, TimePickerList list, int index)
    {
        var row = list.RowRect(index);
        var max = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        var y = Math.Clamp(row.Y - (scroll.Viewport.Height - row.Height) / 2, 0, max);
        scroll.Offset = new Vector(0, y);
    }

    /// <summary>Arrow keys keep the highlight in view; the list glides there on the smooth spring.</summary>
    private void RevealActive()
    {
        if (_panel?.Scroll is not { } scroll || List is not { } list)
        {
            return;
        }
        _scrollMotion = Reveal(scroll, list, _scrollMotion);
    }

    /// <summary>
    /// Glides <paramref name="scroll"/> until the active row of <paramref name="list"/> is in view, on
    /// <paramref name="motion"/> (created when null and returned).
    /// </summary>
    private MotionValue<double>? Reveal(ScrollViewer scroll, TimePickerList list, MotionValue<double>? motion)
    {
        if (list.ActiveIndex < 0)
        {
            return motion;
        }
        var row = list.RowRect(list.ActiveIndex);
        var current = motion?.IsAnimating == true ? motion.Target : scroll.Offset.Y;
        var viewport = scroll.Viewport.Height;
        double target;
        if (row.Top < current)
        {
            target = row.Top;
        }
        else if (row.Bottom > current + viewport)
        {
            target = row.Bottom - viewport;
        }
        else
        {
            return motion;
        }
        target = Math.Clamp(target, 0, Math.Max(0, scroll.Extent.Height - viewport));
        // Wrapping from the last time to the first is a jump, not a scroll through the whole day.
        if (!IsLoaded || !ArcMotion.IsFull(this) || Math.Abs(target - scroll.Offset.Y) > viewport * 2)
        {
            motion?.Stop();
            scroll.Offset = new Vector(0, target);
            return motion;
        }
        if (motion is null)
        {
            motion = new MotionValue<double>(scroll, scroll.Offset.Y, MotionVectors.Double);
            motion.Changed += y => scroll.Offset = new Vector(0, y);
        }
        else if (!motion.IsAnimating)
        {
            motion.Jump(scroll.Offset.Y);
        }
        _ = motion.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        return motion;
    }

    private void UpdatePanelSize()
    {
        if (_panel is null)
        {
            return;
        }
        if (_frame is not null && _frame.Bounds.Width > 0)
        {
            if (_panel.UsesColumns)
            {
                // Columns are at least as wide as the trigger and grow past a narrow one.
                _panel.Width = double.NaN;
                _panel.MinWidth = _frame.Bounds.Width;
            }
            else
            {
                // The list is exactly as wide as the trigger.
                _panel.MinWidth = 0;
                _panel.Width = _frame.Bounds.Width;
            }
        }
        _panel.MaxHeight = MaxDropDownHeight;
    }

    private void OnFrameSizeChanged(object? sender, SizeChangedEventArgs e) => UpdatePanelSize();

    private void ClosePopup()
    {
        _centerPending = false;
        _scrollMotion?.Stop();
        StopColumnMotions();
        PseudoClasses.Set(OpenPseudoClass, false);
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
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

    private void Choose(int index)
    {
        if (index < 0 || index >= OptionCount)
        {
            return;
        }
        SetCurrentValue(SelectedTimeProperty, OptionAt(index));
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    // ---------------------------------------------------------------- pointer

    private void OnFramePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(_frame).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        PseudoClasses.Set(PressedPseudoClass, true);
        _pressedOnFrame = true;
        e.Handled = true;
    }

    private void OnFrameReleased(object? sender, PointerReleasedEventArgs e)
    {
        PseudoClasses.Set(PressedPseudoClass, false);
        if (_pressedOnFrame && _frame is not null && new Rect(_frame.Bounds.Size).Contains(e.GetPosition(_frame)))
        {
            SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);
        }
        _pressedOnFrame = false;
    }

    private void OnFrameCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        PseudoClasses.Set(PressedPseudoClass, false);
        _pressedOnFrame = false;
    }

    // ---------------------------------------------------------------- keyboard and focus

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_frame is not null && ReferenceEquals(e.Source, this))
        {
            _frame.IsFocusActive = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _frame?.IsFocusActive = false;
        if (IsDropDownOpen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsFocused && IsDropDownOpen)
                {
                    SetCurrentValue(IsDropDownOpenProperty, false);
                }
            }, DispatcherPriority.Input);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled)
        {
            return;
        }
        if (IsDropDownOpen && _panel?.UsesColumns == true && HandleColumnKey(e))
        {
            e.Handled = true;
            return;
        }
        var list = List;
        switch (e.Key)
        {
            case Key.Escape when IsDropDownOpen:
                e.Handled = true;
                SetCurrentValue(IsDropDownOpenProperty, false);
                return;
            case Key.Enter or Key.Space:
                e.Handled = true;
                if (IsDropDownOpen && list is { ActiveIndex: >= 0 })
                {
                    Choose(list.ActiveIndex);
                }
                else
                {
                    SetCurrentValue(IsDropDownOpenProperty, true);
                }
                return;
            case Key.Down or Key.Up:
                e.Handled = true;
                if (!IsDropDownOpen || list is null)
                {
                    SetCurrentValue(IsDropDownOpenProperty, true);
                    return;
                }
                var count = list.Count;
                var next = ((list.ActiveIndex + (e.Key == Key.Down ? 1 : -1)) % count + count) % count;
                list.SetActive(next, animate: true);
                RevealActive();
                return;
            case Key.Home or Key.End when IsDropDownOpen && list is not null:
                e.Handled = true;
                list.SetActive(e.Key == Key.Home ? 0 : list.Count - 1, animate: true);
                RevealActive();
                return;
        }
    }

    public TimePicker()
    {
        AttachedToVisualTree += (_, _) =>
        {
            ArcLanguage.Changed += OnLanguageChanged;
            UpdateAutomation();
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateAutomation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ArcLanguage.Changed -= OnLanguageChanged;
        base.OnDetachedFromVisualTree(e);
        if (IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }
}
