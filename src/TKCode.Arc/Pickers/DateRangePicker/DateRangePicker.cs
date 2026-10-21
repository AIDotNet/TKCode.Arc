using System.Globalization;
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
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Arc date range picker: a compact trigger whose surface grows into the panel it opens (P9) — two months beside a
/// preset rail, or one month under a scrolling preset row when the boundary is narrower than 712px. The range
/// highlight stretches across each week as you hover or move with the keyboard, its solid ends glide between days,
/// months slide in the direction you travel, the preset highlight glides between presets, and on Apply the formatted
/// label flies back into the trigger as the surface shrinks around it (P8). Picking is a draft: only Apply commits
/// (<see cref="SelectedRangeChanged"/>); Cancel, Escape or a press outside discard it.
/// </summary>
[TemplatePart(TriggerPart, typeof(Control))]
[TemplatePart(ValuePart, typeof(Control))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[PseudoClasses(OpenPseudoClass, EmptyPseudoClass)]
public class DateRangePicker : TemplatedControl, IPickerAutomation
{
    public const string TriggerPart = "PART_Trigger";
    public const string ValuePart = "PART_Value";
    public const string IconPart = "PART_TriggerIcon";
    public const string ChevronPart = "PART_Chevron";
    public const string PopupPart = "PART_Popup";
    public const string OpenPseudoClass = ":open";
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Arc's springs, restated physically so retargets keep their velocity.</summary>
    internal static readonly SpringSpec Grow = SpringSpec.FromVisual(0.48, 0.12);
    internal static readonly SpringSpec Shrink = SpringSpec.FromVisual(0.38, 0);
    private const double TriggerRadius = 18;
    private const double PanelRadius = 26;
    private const double WideMin = 712;
    private const double Edge = 8;
    private const double WideCell = 36;

    #region Properties

    /// <summary>The applied range (two-way). Null shows the placeholder.</summary>
    public static readonly StyledProperty<DateRange?> SelectedRangeProperty =
        AvaloniaProperty.Register<DateRangePicker, DateRange?>(nameof(SelectedRange), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the panel is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<DateRangePicker, bool>(nameof(IsDropDownOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Accessible name of the trigger and the panel (Arc: "Date range"); shown above when <see cref="IsLabelVisible"/>.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        Field.LabelProperty.AddOwner<DateRangePicker>(new StyledPropertyMetadata<string?>("Date range"));

    /// <summary>Arc names the trigger with its label but does not show it; set true for a visible field label.</summary>
    public static readonly StyledProperty<bool> IsLabelVisibleProperty =
        Field.IsLabelVisibleProperty.AddOwner<DateRangePicker>(new StyledPropertyMetadata<bool>(false));

    public static readonly StyledProperty<string?> DescriptionProperty = Field.DescriptionProperty.AddOwner<DateRangePicker>();
    public static readonly StyledProperty<string?> ErrorMessageProperty = Field.ErrorMessageProperty.AddOwner<DateRangePicker>();

    /// <summary>Shown while no range is applied (Arc: "Select dates").</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(Placeholder), "Select dates");

    /// <summary>The preset rail (null = Arc's eight defaults, <see cref="DateRangePresets.Default"/>).</summary>
    public static readonly StyledProperty<IReadOnlyList<DateRangePreset>?> PresetsProperty =
        AvaloniaProperty.Register<DateRangePicker, IReadOnlyList<DateRangePreset>?>(nameof(Presets));

    public static readonly StyledProperty<DateOnly?> MinDateProperty = Calendar.MinDateProperty.AddOwner<DateRangePicker>();
    public static readonly StyledProperty<DateOnly?> MaxDateProperty = Calendar.MaxDateProperty.AddOwner<DateRangePicker>();
    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty = Calendar.FirstDayOfWeekProperty.AddOwner<DateRangePicker>();
    public static readonly StyledProperty<CultureInfo?> CultureProperty = Calendar.CultureProperty.AddOwner<DateRangePicker>();

    /// <summary>One or two months; Auto picks two when the boundary is at least 712px wide.</summary>
    public static readonly StyledProperty<DateRangePickerMonths> MonthCountProperty =
        AvaloniaProperty.Register<DateRangePicker, DateRangePickerMonths>(nameof(MonthCount));

    /// <summary>The element the panel stays inside (null = the window).</summary>
    public static readonly StyledProperty<Control?> BoundaryProperty =
        AvaloniaProperty.Register<DateRangePicker, Control?>(nameof(Boundary));

    /// <summary>Label of the button that applies the picked range (default Apply).</summary>
    public static readonly StyledProperty<string> ApplyTextProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(ApplyText), "Apply");

    /// <summary>Label of the cancel button in the panel (default Cancel).</summary>
    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(CancelText), "Cancel");

    /// <summary>Trigger text when no range is selected (default No dates).</summary>
    public static readonly StyledProperty<string> NoDatesTextProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(NoDatesText), "No dates");

    /// <summary>Readout while the hovered date is the start date and the end is still to be picked (default Pick an end date).</summary>
    public static readonly StyledProperty<string> PickEndTextProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(PickEndText), "Pick an end date");

    /// <summary>Day count; {0} is the number (Arc: "1 day" / "12 days").</summary>
    public static readonly StyledProperty<string> DayCountFormatProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(DayCountFormat), "{0} day");

    /// <summary>Day count for two or more days; {0} is the number (default "{0} days").</summary>
    public static readonly StyledProperty<string> DaysCountFormatProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(DaysCountFormat), "{0} days");

    /// <summary>Accessible name of the previous-month button (default Previous month).</summary>
    public static readonly StyledProperty<string> PreviousMonthLabelProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(PreviousMonthLabel), "Previous month");

    /// <summary>Accessible name of the next-month button (default Next month).</summary>
    public static readonly StyledProperty<string> NextMonthLabelProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(NextMonthLabel), "Next month");

    public static readonly RoutedEvent<DateRangePickerSelectedRangeChangedEventArgs> SelectedRangeChangedEvent =
        RoutedEvent.Register<DateRangePicker, DateRangePickerSelectedRangeChangedEventArgs>(nameof(SelectedRangeChanged), RoutingStrategies.Bubble);

    /// <summary>The applied range.</summary>
    public DateRange? SelectedRange { get => GetValue(SelectedRangeProperty); set => SetValue(SelectedRangeProperty, value); }

    /// <summary>Whether the panel is open.</summary>
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }

    /// <summary>Accessible name of the trigger and the panel.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Whether the label shows above the trigger.</summary>
    public bool IsLabelVisible { get => GetValue(IsLabelVisibleProperty); set => SetValue(IsLabelVisibleProperty, value); }

    /// <summary>Helper copy under the trigger.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Error copy under the trigger.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>Shown while no range is applied.</summary>
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>The preset rail.</summary>
    public IReadOnlyList<DateRangePreset>? Presets { get => GetValue(PresetsProperty); set => SetValue(PresetsProperty, value); }

    /// <summary>Earliest selectable day.</summary>
    public DateOnly? MinDate { get => GetValue(MinDateProperty); set => SetValue(MinDateProperty, value); }

    /// <summary>Latest selectable day.</summary>
    public DateOnly? MaxDate { get => GetValue(MaxDateProperty); set => SetValue(MaxDateProperty, value); }

    /// <summary>First column of the months (Arc's weekStartsOn: Sunday or Monday).</summary>
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>Culture for month titles, weekday heads and the range label.</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <summary>One or two months.</summary>
    public DateRangePickerMonths MonthCount { get => GetValue(MonthCountProperty); set => SetValue(MonthCountProperty, value); }

    /// <summary>The element the panel stays inside.</summary>
    public Control? Boundary { get => GetValue(BoundaryProperty); set => SetValue(BoundaryProperty, value); }

    /// <inheritdoc cref="ApplyTextProperty"/>
    public string ApplyText { get => GetValue(ApplyTextProperty); set => SetValue(ApplyTextProperty, value); }
    /// <inheritdoc cref="CancelTextProperty"/>
    public string CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }
    /// <inheritdoc cref="NoDatesTextProperty"/>
    public string NoDatesText { get => GetValue(NoDatesTextProperty); set => SetValue(NoDatesTextProperty, value); }
    /// <inheritdoc cref="PickEndTextProperty"/>
    public string PickEndText { get => GetValue(PickEndTextProperty); set => SetValue(PickEndTextProperty, value); }
    public string DayCountFormat { get => GetValue(DayCountFormatProperty); set => SetValue(DayCountFormatProperty, value); }
    /// <inheritdoc cref="DaysCountFormatProperty"/>
    public string DaysCountFormat { get => GetValue(DaysCountFormatProperty); set => SetValue(DaysCountFormatProperty, value); }
    /// <inheritdoc cref="PreviousMonthLabelProperty"/>
    public string PreviousMonthLabel { get => GetValue(PreviousMonthLabelProperty); set => SetValue(PreviousMonthLabelProperty, value); }
    /// <inheritdoc cref="NextMonthLabelProperty"/>
    public string NextMonthLabel { get => GetValue(NextMonthLabelProperty); set => SetValue(NextMonthLabelProperty, value); }

    /// <summary>Raised when a range is applied.</summary>
    public event EventHandler<DateRangePickerSelectedRangeChangedEventArgs>? SelectedRangeChanged
    {
        add => AddHandler(SelectedRangeChangedEvent, value);
        remove => RemoveHandler(SelectedRangeChangedEvent, value);
    }

    // Theme-provided brushes (ArcDesign tokens) for the drawn months and the surface.
    internal static readonly StyledProperty<IBrush?> AccentBrushProperty = AvaloniaProperty.Register<DateRangePicker, IBrush?>(nameof(AccentBrush));
    internal static readonly StyledProperty<IBrush?> AccentSubtleBrushProperty = AvaloniaProperty.Register<DateRangePicker, IBrush?>(nameof(AccentSubtleBrush));
    internal static readonly StyledProperty<IBrush?> AccentForegroundBrushProperty = AvaloniaProperty.Register<DateRangePicker, IBrush?>(nameof(AccentForegroundBrush));
    internal static readonly StyledProperty<IBrush?> DayForegroundProperty = AvaloniaProperty.Register<DateRangePicker, IBrush?>(nameof(DayForeground));
    internal static readonly StyledProperty<IBrush?> DayMutedProperty = AvaloniaProperty.Register<DateRangePicker, IBrush?>(nameof(DayMuted));
    internal static readonly StyledProperty<IBrush?> DayHoverFillProperty = AvaloniaProperty.Register<DateRangePicker, IBrush?>(nameof(DayHoverFill));
    internal static readonly StyledProperty<IBrush?> SurfaceFillProperty = AvaloniaProperty.Register<DateRangePicker, IBrush?>(nameof(SurfaceFill));
    internal static readonly StyledProperty<IBrush?> SurfaceBorderProperty = AvaloniaProperty.Register<DateRangePicker, IBrush?>(nameof(SurfaceBorder));
    internal static readonly StyledProperty<BoxShadows> RestShadowProperty = AvaloniaProperty.Register<DateRangePicker, BoxShadows>(nameof(RestShadow));
    internal static readonly StyledProperty<BoxShadows> OpenShadowProperty = AvaloniaProperty.Register<DateRangePicker, BoxShadows>(nameof(OpenShadow));

    internal IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    internal IBrush? AccentSubtleBrush { get => GetValue(AccentSubtleBrushProperty); set => SetValue(AccentSubtleBrushProperty, value); }
    internal IBrush? AccentForegroundBrush { get => GetValue(AccentForegroundBrushProperty); set => SetValue(AccentForegroundBrushProperty, value); }
    internal IBrush? DayForeground { get => GetValue(DayForegroundProperty); set => SetValue(DayForegroundProperty, value); }
    internal IBrush? DayMuted { get => GetValue(DayMutedProperty); set => SetValue(DayMutedProperty, value); }
    internal IBrush? DayHoverFill { get => GetValue(DayHoverFillProperty); set => SetValue(DayHoverFillProperty, value); }
    internal IBrush? SurfaceFill { get => GetValue(SurfaceFillProperty); set => SetValue(SurfaceFillProperty, value); }
    internal IBrush? SurfaceBorder { get => GetValue(SurfaceBorderProperty); set => SetValue(SurfaceBorderProperty, value); }
    internal BoxShadows RestShadow { get => GetValue(RestShadowProperty); set => SetValue(RestShadowProperty, value); }
    internal BoxShadows OpenShadow { get => GetValue(OpenShadowProperty); set => SetValue(OpenShadowProperty, value); }

    #endregion

    private Control? _trigger;
    private PickerRollText? _value;
    private Control? _icon;
    private Control? _chevron;
    private PickerMorphPopup? _popup;
    private PickerMorphSurface? _surface;
    private DateRangePickerPanel? _panel;
    private PickerRollText? _flyer;
    private ArcGlyph? _ghostIcon;
    private ArcGlyph? _ghostChevron;
    private readonly TranslateTransform _flyerShift = new();
    private readonly TranslateTransform _faceShift = new();
    private readonly TranslateTransform _countShift = new();
    private readonly TranslateTransform _actionsShift = new();
    private MotionValue<Point>? _flight;
    private MotionValue<double>? _face;
    private MotionValue<double>? _quiet;
    private MotionValue<double>? _flyerFade;
    private PickerAutomationPeer? _peer;
    private int _generation;
    private bool _pressedOnTrigger;
    private bool _keyboardOpen;
    private bool _closeReturnsFocus;
    private TopLevel? _watchTop;
    private long? _committedKey;
    private int _committedDirection = 1;
    private PickerMorphShape _panelShape;
    private double _rise;
    private PickerRevealScroll? _reveal;
    private Point _flyerHome;
    private Point _flyerAway;

    static DateRangePicker()
    {
        FocusableProperty.OverrideDefaultValue<DateRangePicker>(true);
        FocusAdornerProperty.OverrideDefaultValue<DateRangePicker>(null);
    }

    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentUICulture;

    internal IReadOnlyList<DateRangePreset> EffectivePresets => Presets ?? DateRangePresets.Default;

    internal DateRangePickerPanel? Panel => _panel;

    internal PickerMorphSurface? Surface => _surface;

    internal ArcPopup? Popup => _popup;

    internal PickerRollText? Flyer => _flyer;

    /// <summary>The applied range as text (the placeholder when empty).</summary>
    public string FormattedValue => SelectedRange is { } range ? FormatRange(range) : Placeholder;

    internal string FormatDate(DateOnly date) => PickerDateFormat.Format(date, PickerDateFormat.MediumPattern(EffectiveCulture), EffectiveCulture);

    internal string FormatRange(DateRange range) => PickerDateFormat.FormatRange(range.Start, range.End, EffectiveCulture);

    internal string FormatDays(int days) => string.Format(EffectiveCulture, days == 1 ? DayCountFormat : DaysCountFormat, days);

    string? IPickerAutomation.AutomationLabel => Label;

    string? IPickerAutomation.AutomationValue => FormattedValue;

    bool IPickerAutomation.AutomationIsOpen
    {
        get => IsDropDownOpen;
        set => SetCurrentValue(IsDropDownOpenProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        _peer = new PickerAutomationPeer(this, this, AutomationControlType.Button);

    #region Template and value

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.PointerPressed -= OnTriggerPressed;
            _trigger.PointerReleased -= OnTriggerReleased;
            _trigger.PointerCaptureLost -= OnTriggerCaptureLost;
        }
        if (_popup is not null)
        {
            _popup.Child = null;
        }
        _trigger = e.NameScope.Find<Control>(TriggerPart);
        _value = e.NameScope.Find<PickerRollText>(ValuePart);
        _icon = e.NameScope.Find<Control>(IconPart);
        _chevron = e.NameScope.Find<Control>(ChevronPart);
        _popup = e.NameScope.Find<PickerMorphPopup>(PopupPart);
        if (_trigger is not null)
        {
            _trigger.PointerPressed += OnTriggerPressed;
            _trigger.PointerReleased += OnTriggerReleased;
            _trigger.PointerCaptureLost += OnTriggerCaptureLost;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = _trigger;
            if (_surface is not null)
            {
                _popup.Child = _surface;
            }
        }
        _committedKey = Key(SelectedRange);
        UpdateValue(animate: false);
        if (IsDropDownOpen)
        {
            OpenPanel();
        }
    }

    private static long? Key(DateRange? range) => range is { } r ? r.Start.DayNumber * 2L + r.End.DayNumber : null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedRangeProperty)
        {
            var key = Key(SelectedRange);
            _committedDirection = key is null || _committedKey is null || key >= _committedKey ? 1 : -1;
            _committedKey = key;
            UpdateValue(animate: true);
        }
        else if (change.Property == IsDropDownOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                OpenPanel();
            }
            else
            {
                ClosePanel(_closeReturnsFocus);
                _closeReturnsFocus = false;
            }
        }
        else if (change.Property == CultureProperty || change.Property == PlaceholderProperty)
        {
            UpdateValue(animate: false);
            _panel?.Refresh(animate: false);
        }
        else if (change.Property == MinDateProperty || change.Property == MaxDateProperty || change.Property == FirstDayOfWeekProperty ||
                 change.Property == PresetsProperty)
        {
            _panel?.Refresh(animate: false);
        }
        else if (change.Property == AccentBrushProperty || change.Property == AccentSubtleBrushProperty ||
                 change.Property == AccentForegroundBrushProperty || change.Property == DayForegroundProperty ||
                 change.Property == DayMutedProperty || change.Property == DayHoverFillProperty ||
                 change.Property == FontFamilyProperty || change.Property == FontSizeProperty)
        {
            UpdatePalette();
        }
        else if (change.Property == SurfaceFillProperty || change.Property == SurfaceBorderProperty ||
                 change.Property == RestShadowProperty || change.Property == OpenShadowProperty)
        {
            StyleSurface();
        }
        else if (change.Property == LabelProperty || change.Property == DescriptionProperty || change.Property == ErrorMessageProperty)
        {
            UpdateAutomation();
        }
    }

    /// <summary>The trigger's words roll in time's direction: a later range rises from below, an earlier one drops from above.</summary>
    private void UpdateValue(bool animate)
    {
        PseudoClasses.Set(EmptyPseudoClass, SelectedRange is null);
        _value?.ShowWords(FormattedValue, SelectedRange is null, _committedDirection, animate);
        UpdateAutomation();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetHelpText(this, FieldText.HelpText(Description, ErrorMessage));
        if (_panel is not null)
        {
            AutomationProperties.SetName(_panel, Label ?? string.Empty);
        }
    }

    private void UpdatePalette()
    {
        _panel?.UpdatePalette(GetValue(AccentBrushProperty), GetValue(AccentSubtleBrushProperty), GetValue(AccentForegroundBrushProperty),
            GetValue(DayForegroundProperty), GetValue(DayMutedProperty), GetValue(DayHoverFillProperty), FontFamily, FontSize,
            Math.Round(FontSize * 12 / 14), FontWeight.Medium);
    }

    private void StyleSurface()
    {
        if (_surface is null)
        {
            return;
        }
        _surface.Fill = PickerDrawing.ColorOf(GetValue(SurfaceFillProperty));
        _surface.BorderPen = GetValue(SurfaceBorderProperty) is { } border ? new Pen(border, 1) : null;
        _surface.RestShadow = GetValue(RestShadowProperty);
        _surface.OpenShadow = GetValue(OpenShadowProperty);
        _surface.InvalidateVisual();
    }

    #endregion

    #region Panel

    private void EnsurePanel()
    {
        if (_panel is not null || _popup is null)
        {
            return;
        }
        _panel = new DateRangePickerPanel(this);
        _surface = new PickerMorphSurface(_panel);
        _surface.OutsidePressed += (_, _) => Close(returnFocus: false);
        _surface.ContentResized += (_, _) => Dispatcher.UIThread.Post(Relayout);
        _popup.Child = _surface;
        _panel.ApplyTemplate();
        UpdatePalette();
        StyleSurface();
        if (_panel.Body is { } body)
        {
            body.RenderTransform = _faceShift;
        }
        if (_panel.CountText is { } count)
        {
            count.RenderTransform = _countShift;
        }
        if (_panel.Actions is { } actions)
        {
            actions.RenderTransform = _actionsShift;
        }
        _panel.ApplyRequested += (_, _) => Apply();
        _panel.CancelRequested += (_, _) => Close(returnFocus: true);
        _panel.ShownChanged += (_, _) => UpdateFlyerText(animate: true);
        _panel.KeyDown += OnPanelKeyDown;
        _panel.LostFocus += (_, _) => Dispatcher.UIThread.Post(CheckFocusLeft, DispatcherPriority.Input);

        // The flying value and the trigger's icons are drawn above the clip: they cross the surface's edge.
        _flyer = new PickerRollText
        {
            Matching = PickerRollMatching.Words,
            SegmentGap = 0.28,
            EnterTravel = 0.45,
            ExitTravel = 0.45,
            EnterBlur = 2,
            ExitBlur = 2,
            EnterDuration = TimeSpan.FromMilliseconds(260),
            ExitDuration = TimeSpan.FromMilliseconds(140),
            RenderTransform = _flyerShift
        };
        CopyValueStyle(_flyer);
        _ghostIcon = new ArcGlyph { Data = ArcIcons.CalendarDays, Size = 16 };
        _ghostChevron = new ArcGlyph { Data = ArcIcons.ChevronDown, Size = 16 };
        if (_icon is ArcGlyph icon)
        {
            _ghostIcon.Foreground = icon.Foreground;
        }
        if (_chevron is ArcGlyph chevron)
        {
            _ghostChevron.Foreground = chevron.Foreground;
        }
        _surface.Overlay.Children.Add(_ghostIcon);
        _surface.Overlay.Children.Add(_ghostChevron);
        _surface.Overlay.Children.Add(_flyer);
        UpdateAutomation();
    }

    private void CopyValueStyle(PickerRollText target)
    {
        if (_value is null)
        {
            return;
        }
        target.FontFamily = _value.FontFamily;
        target.FontSize = _value.FontSize;
        target.FontWeight = _value.FontWeight;
        target.LineHeight = _value.LineHeight;
        target.Foreground = _value.Foreground;
        target.PlaceholderForeground = _value.PlaceholderForeground;
        target.PlaceholderFontWeight = _value.PlaceholderFontWeight;
    }

    /// <summary>Two months when the boundary is wide enough, otherwise one compact month (Arc's measureLayout).</summary>
    private (bool Single, double Width, double Cell, double Left, double Right) MeasureLayout()
    {
        var top = TopLevel.GetTopLevel(this);
        var left = 0.0;
        var right = top?.ClientSize.Width ?? 1024;
        if (Boundary is { } boundary && top is not null && boundary.TranslatePoint(default, top) is { } origin)
        {
            left = Math.Max(left, origin.X);
            right = Math.Min(right, origin.X + boundary.Bounds.Width);
        }
        var available = Math.Max(0, right - left - Edge * 2);
        var single = MonthCount == DateRangePickerMonths.One || (MonthCount == DateRangePickerMonths.Auto && available < WideMin);
        var width = Math.Min(352, available);
        var cell = single ? Math.Max(32, Math.Min(42, Math.Floor((width - 24) / 7))) : WideCell;
        return (single, width, cell, left, right);
    }

    private PickerMorphShape TriggerShape => _trigger is { } trigger
        ? new PickerMorphShape(0, 0, trigger.Bounds.Width, trigger.Bounds.Height, TriggerRadius)
        : new PickerMorphShape(0, 0, 200, 44, TriggerRadius);

    /// <summary>
    /// Lays the panel out and works out its shape, the surface's region and the popup offset. On <paramref name="begin"/>
    /// it also fits the panel below the trigger and returns the page scroll that reveals it.
    /// </summary>
    private PickerMorphFit Layout(bool begin)
    {
        if (_panel is null || _surface is null || _popup is null)
        {
            return default;
        }
        var layout = MeasureLayout();
        if (begin)
        {
            _panel.Begin(SelectedRange, ArcToday.Today, layout.Single, layout.Width, layout.Cell);
        }
        else
        {
            _panel.SetLayout(layout.Single, layout.Width, layout.Cell);
            _panel.Refresh(animate: false);
        }
        _panel.Measure(Size.Infinity);
        _panel.Arrange(new Rect(_panel.DesiredSize));
        var size = _panel.DesiredSize;
        _surface.ExpectContentSize(size);
        var trigger = TriggerShape;
        var top = TopLevel.GetTopLevel(this);
        var triggerLeft = top is not null && _trigger?.TranslatePoint(default, top) is { } p ? p.X : 0;
        // Keep the panel EDGE px inside the boundary.
        var offset = Math.Min(0, layout.Right - Edge - (triggerLeft + size.Width));
        offset = Math.Max(offset, layout.Left + Edge - triggerLeft);
        // Below, the page scrolls first and the panel rises only for the rest; the popup stays where it opened.
        var fit = begin ? PickerMorphPlacement.Fit(this, _trigger, size.Height, Edge) : default;
        if (begin)
        {
            _rise = fit.Rise;
        }
        _panelShape = new PickerMorphShape(offset, _rise, size.Width, size.Height, PanelRadius);
        _surface.Contain(trigger, _panelShape);
        _surface.ShadowRoom = PickerMorphPlacement.Room(this, _trigger, _surface, fit.Scroll);
        _popup.ContentOffset = new Point(-_surface.Origin.X, -_surface.Origin.Y);
        // Where the value rests in the trigger, and where it lands in the panel's footer (surface coordinates).
        var origin = _surface.Origin;
        _flyerHome = origin + (_value is not null && _trigger is not null && _value.TranslatePoint(default, _trigger) is { } v ? v : default);
        _flyerAway = origin + new Point(offset, _rise) +
                     (_panel.SummarySlot?.TranslatePoint(default, _panel) is { } slot ? slot : default);
        PlaceGhosts();
        return fit;
    }

    private void PlaceGhosts()
    {
        if (_surface is null || _trigger is null)
        {
            return;
        }
        var origin = _surface.Origin;
        void Place(Control? source, Control? ghost)
        {
            if (source is not null && ghost is not null && source.TranslatePoint(default, _trigger) is { } p)
            {
                Canvas.SetLeft(ghost, origin.X + p.X);
                Canvas.SetTop(ghost, origin.Y + p.Y);
            }
        }
        Place(_icon, _ghostIcon);
        Place(_chevron, _ghostChevron);
        if (_flyer is not null)
        {
            Canvas.SetLeft(_flyer, 0);
            Canvas.SetTop(_flyer, 0);
        }
    }

    private void OpenPanel()
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
        if (_panel is null || _surface is null || _flyer is null)
        {
            return;
        }
        var generation = ++_generation;
        PseudoClasses.Set(OpenPseudoClass, true);
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var reopening = _popup.IsOpen;
        var fit = Layout(begin: !reopening);
        CopyValueStyle(_flyer);
        if (!reopening)
        {
            _surface.Jump(TriggerShape, 0);
            _flyer.ShowWords(FormattedValue, SelectedRange is null, 1, animate: false);
            Flight.Jump(_flyerHome);
            SetQuiet(0, animate: false);
            Face.Jump(0);
            FlyerFade.Jump(1);
            _popup.IsOpen = true;
            StartWatch();
            (_reveal ??= new PickerRevealScroll(this)).Start(fit, animate: level == ArcMotionLevel.Full);
        }
        switch (level)
        {
            case ArcMotionLevel.Full:
                _ = _surface.MorphTo(_panelShape, Grow, 1, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
                _ = Flight.AnimateTo(_flyerAway, MotionSpec.Spring(Grow));
                // The panel content arrives just behind the growing surface.
                _ = Face.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(260), ArcEasings.Enter, TimeSpan.FromMilliseconds(70)));
                SetQuiet(0, animate: false);
                break;
            case ArcMotionLevel.Reduced:
                _surface.Jump(_panelShape, 1);
                Flight.Jump(_flyerAway);
                FlyerFade.Jump(0);
                _ = FlyerFade.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard));
                _ = Face.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard));
                break;
            default:
                _surface.Jump(_panelShape, 1);
                Flight.Jump(_flyerAway);
                Face.Jump(1);
                break;
        }
        UpdateFlyerText(animate: true);
        _peer?.RaiseExpandChanged(true);
        var method = _keyboardOpen ? NavigationMethod.Directional : NavigationMethod.Pointer;
        _keyboardOpen = false;
        Dispatcher.UIThread.Post(() =>
        {
            if (generation == _generation && IsDropDownOpen)
            {
                // Opening moves focus to the day that owns the tab stop, so arrows work at once.
                _panel.FocusDay(_panel.Tabbable, method);
            }
        }, DispatcherPriority.Loaded);
    }

    private void UpdateFlyerText(bool animate)
    {
        if (_flyer is null || _panel is null || !IsDropDownOpen)
        {
            return;
        }
        var shown = _panel.Shown;
        _flyer.ShowWords(shown is { } r ? FormatRange(r) : NoDatesText, placeholder: false, _panel.ShownDirection, animate);
    }

    private MotionValue<Point> Flight
    {
        get
        {
            if (_flight is null)
            {
                _flight = new MotionValue<Point>(_flyer!, default, MotionVectors.Point);
                _flight.Changed += p =>
                {
                    _flyerShift.X = p.X;
                    _flyerShift.Y = p.Y;
                };
            }
            return _flight;
        }
    }

    private MotionValue<double> Face
    {
        get
        {
            if (_face is null)
            {
                _face = new MotionValue<double>(_surface!, 1, MotionVectors.Double);
                _face.Changed += ApplyFace;
            }
            return _face;
        }
    }

    private MotionValue<double> FlyerFade
    {
        get
        {
            if (_flyerFade is null)
            {
                _flyerFade = new MotionValue<double>(_flyer!, 1, MotionVectors.Double);
                _flyerFade.Changed += v => _flyer!.Opacity = Math.Clamp(v, 0, 1);
            }
            return _flyerFade;
        }
    }

    /// <summary>faceIn: opacity, y −4 → 0 and a soft blur clearing; "gone" lifts 2px and blurs again.</summary>
    private void ApplyFace(double p)
    {
        if (_panel is null)
        {
            return;
        }
        var full = ArcMotion.IsFull(this);
        var opacity = Math.Clamp(p, 0, 1);
        var y = full ? (_closing ? -2 * (1 - p) : -4 * (1 - p)) : 0;
        var blur = full ? 4 * (1 - opacity) : 0;
        ApplyFacePart(_panel.Body, _faceShift, opacity, y, blur);
        ApplyFacePart(_panel.CountText, _countShift, opacity, y, blur);
        ApplyFacePart(_panel.Actions, _actionsShift, opacity, y, blur);
    }

    private static void ApplyFacePart(Control? part, TranslateTransform shift, double opacity, double y, double blur)
    {
        if (part is null)
        {
            return;
        }
        part.Opacity = opacity;
        part.Effect = PickerDrawing.Blur(blur);
        shift.Y = y;
    }

    private bool _closing;

    /// <summary>The trigger copies' icons fade back in as the surface folds home (0.22s, after 0.1s).</summary>
    private void SetQuiet(double opacity, bool animate)
    {
        if (_ghostIcon is null || _ghostChevron is null)
        {
            return;
        }
        if (_quiet is null)
        {
            _quiet = new MotionValue<double>(_ghostIcon, opacity, MotionVectors.Double);
            _quiet.Changed += v =>
            {
                _ghostIcon.Opacity = Math.Clamp(v, 0, 1);
                _ghostChevron.Opacity = Math.Clamp(v, 0, 1);
            };
        }
        if (animate)
        {
            _ = _quiet.AnimateTo(opacity, MotionSpec.Tween(TimeSpan.FromMilliseconds(220), ArcEasings.Enter, TimeSpan.FromMilliseconds(100)));
        }
        else
        {
            _quiet.Jump(opacity);
        }
    }

    private void Apply()
    {
        if (_panel is null)
        {
            return;
        }
        var next = _panel.Anchor is { } anchor ? new DateRange(anchor, anchor) : _panel.Draft;
        if (next is not { } range)
        {
            return;
        }
        var old = SelectedRange;
        SetCurrentValue(SelectedRangeProperty, range);
        RaiseEvent(new DateRangePickerSelectedRangeChangedEventArgs(SelectedRangeChangedEvent, old, range));
        Close(returnFocus: true);
    }

    /// <summary>Closes the panel; <paramref name="returnFocus"/> puts focus back on the trigger.</summary>
    public void Close(bool returnFocus)
    {
        _closeReturnsFocus = returnFocus;
        SetCurrentValue(IsDropDownOpenProperty, false);
        _closeReturnsFocus = false;
    }

    private async void ClosePanel(bool returnFocus)
    {
        PseudoClasses.Set(OpenPseudoClass, false);
        StopWatch();
        _reveal?.Stop();
        if (_popup is not { IsOpen: true } popup || _surface is null || _panel is null || _flyer is null)
        {
            return;
        }
        var generation = ++_generation;
        var focusInside = _panel.IsKeyboardFocusWithin;
        if (returnFocus || focusInside)
        {
            Focus(returnFocus ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }
        _peer?.RaiseExpandChanged(false);
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (level == ArcMotionLevel.Off)
        {
            popup.IsOpen = false;
            return;
        }
        // The trigger takes its new width first, so the surface shrinks onto the size it will rest at.
        UpdateLayout();
        PlaceGhosts();
        _flyerHome = _surface.Origin + (_value is not null && _trigger is not null && _value.TranslatePoint(default, _trigger) is { } v ? v : default);
        _closing = true;
        _flyer.ShowWords(FormattedValue, SelectedRange is null, _committedDirection, animate: true);
        if (level == ArcMotionLevel.Full)
        {
            _ = Face.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard));
            _ = Flight.AnimateTo(_flyerHome, MotionSpec.Spring(Shrink));
            SetQuiet(1, animate: true);
            await _surface.MorphTo(TriggerShape, Shrink, 0, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
        }
        else
        {
            _ = FlyerFade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(80), ArcEasings.Standard));
            await Face.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(80), ArcEasings.Standard));
        }
        _closing = false;
        if (generation == _generation && !IsDropDownOpen)
        {
            popup.IsOpen = false;
        }
    }

    private void StartWatch()
    {
        StopWatch();
        _watchTop = TopLevel.GetTopLevel(this);
        if (_watchTop is not null)
        {
            _watchTop.AddHandler(PointerPressedEvent, OnTopLevelPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _watchTop.SizeChanged += OnTopLevelSizeChanged;
        }
        ArcToday.Changed += OnTodayChanged;
    }

    private void StopWatch()
    {
        if (_watchTop is not null)
        {
            _watchTop.RemoveHandler(PointerPressedEvent, OnTopLevelPressed);
            _watchTop.SizeChanged -= OnTopLevelSizeChanged;
        }
        _watchTop = null;
        ArcToday.Changed -= OnTodayChanged;
    }

    private void OnTodayChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => _panel?.SetToday(ArcToday.Today));

    /// <summary>A resized window may switch between one and two months; the surface springs to the new panel.</summary>
    private void OnTopLevelSizeChanged(object? sender, SizeChangedEventArgs e) => Relayout();

    private void Relayout()
    {
        if (!IsDropDownOpen || _surface is null || _closing)
        {
            return;
        }
        Layout(begin: false);
        if (_surface.IsMoving)
        {
            _surface.Retarget(_panelShape, Grow);
            _ = Flight.AnimateTo(_flyerAway, MotionSpec.Spring(Grow));
        }
        else
        {
            _surface.Jump(_panelShape, 1);
            Flight.Jump(_flyerAway);
        }
    }

    private void OnTopLevelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (InSurface(source) || ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)))
        {
            return;
        }
        Close(returnFocus: false);
    }

    private bool InSurface(Visual visual) => _surface is not null && (ReferenceEquals(visual, _surface) || _surface.IsVisualAncestorOf(visual));

    /// <summary>Tabbing out of the panel closes it; focus moving within keeps it open.</summary>
    private void CheckFocusLeft()
    {
        if (!IsDropDownOpen || IsFocused || _panel?.IsKeyboardFocusWithin == true)
        {
            return;
        }
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not null)
        {
            Close(returnFocus: false);
        }
    }

    #endregion

    #region Input

    private void OnTriggerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(_trigger).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressedOnTrigger = true;
        e.Handled = true;
    }

    private void OnTriggerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressedOnTrigger && _trigger is not null && new Rect(_trigger.Bounds.Size).Contains(e.GetPosition(_trigger)) && !IsDropDownOpen)
        {
            Focus(NavigationMethod.Pointer);
            SetCurrentValue(IsDropDownOpenProperty, true);
        }
        _pressedOnTrigger = false;
    }

    private void OnTriggerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _pressedOnTrigger = false;

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Avalonia.Input.Key.Escape)
        {
            e.Handled = true;
            Close(returnFocus: true);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Avalonia.Input.Key.Escape && IsDropDownOpen)
        {
            e.Handled = true;
            Close(returnFocus: true);
            return;
        }
        if (ReferenceEquals(e.Source, this) && e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Space && !IsDropDownOpen)
        {
            e.Handled = true;
            _keyboardOpen = true;
            SetCurrentValue(IsDropDownOpenProperty, true);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (IsDropDownOpen)
        {
            Dispatcher.UIThread.Post(CheckFocusLeft, DispatcherPriority.Input);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopWatch();
        _reveal?.Stop();
        if (IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
        if (_popup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
    }

    #endregion
}
