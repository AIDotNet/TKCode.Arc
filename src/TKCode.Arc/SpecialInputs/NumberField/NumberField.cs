using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The limit a <see cref="NumberField"/> limit note talks about (Arc <c>"min" | "max"</c>).</summary>
public enum NumberFieldLimitEdge
{
    /// <summary>The <see cref="NumberField.Minimum"/>.</summary>
    Minimum,

    /// <summary>The <see cref="NumberField.Maximum"/>.</summary>
    Maximum
}

/// <summary>What changed a <see cref="NumberField"/> value.</summary>
public enum NumberFieldChangeSource
{
    /// <summary>Code set <see cref="NumberField.Value"/> (or called <see cref="NumberField.StepUp"/> / <see cref="NumberField.StepDown"/>).</summary>
    Programmatic,

    /// <summary>A stepper: a pointer press or hold, or a keyboard / assistive activation.</summary>
    Button,

    /// <summary>ArrowUp / ArrowDown / PageUp / PageDown (held keys repeat), Home or End.</summary>
    Key,

    /// <summary>The mouse wheel over the focused field (extension; Arc has none).</summary>
    Wheel,

    /// <summary>Dragging the label sideways (<see cref="NumberField.IsScrubEnabled"/>).</summary>
    Scrub,

    /// <summary>Typing: a valid draft applied live, a committed draft, Escape, or an assistive value.</summary>
    Type
}

/// <summary>Arguments of <see cref="NumberField.ValueChanged"/>.</summary>
public sealed class NumberFieldValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public NumberFieldValueChangedEventArgs(RoutedEvent routedEvent, decimal oldValue, decimal newValue, NumberFieldChangeSource source)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
        ChangeSource = source;
    }

    /// <summary>The value before the change.</summary>
    public decimal OldValue { get; }

    /// <summary>The value after the change.</summary>
    public decimal NewValue { get; }

    /// <summary>What changed it (named so it does not hide <see cref="RoutedEventArgs.Source"/>, the raising element).</summary>
    public NumberFieldChangeSource ChangeSource { get; }
}

/// <summary>
/// Arc number field: a bounded number with clear increment controls. The value rolls like an odometer, the steppers
/// and ArrowUp / ArrowDown repeat and speed up while held, Shift, PageUp and PageDown take large steps, Home and End
/// jump to the limits, and <see cref="IsScrubEnabled"/> lets the label be dragged sideways. Typing edits a plain
/// draft that applies live while valid and commits on Enter, blur or the next step (snapped to the step grid and
/// clamped). A press past a limit strains the value toward it, shakes the refused stepper and names the limit beside
/// the label (<c>"Max 10 seats"</c>).
/// <code>&lt;arc:NumberField Label="Seats" Minimum="1" Maximum="10" Value="{Binding Seats}" Suffix=" seats" /&gt;</code>
/// </summary>
/// <remarks>
/// The control is the draft editor (an Avalonia <see cref="TextBox"/>): its text is drawn only while a draft is open,
/// otherwise it draws just the caret and selection over the rolling digits. <see cref="FieldTextBox.Prefix"/> and
/// <see cref="FieldTextBox.Suffix"/> take strings that travel with the number (<c>"$"</c>, <c>" seats"</c>);
/// <see cref="PrefixFormatter"/> / <see cref="SuffixFormatter"/> compute them from the value. Read <see cref="Value"/>,
/// not <c>Text</c>.
/// </remarks>
[TemplatePart(HeadPart, typeof(Control))]
[TemplatePart(LabelPart, typeof(TextBlock))]
[TemplatePart(LimitNotePart, typeof(NumberFieldLimitNote))]
[TemplatePart(DecreaseButtonPart, typeof(FieldIconButton))]
[TemplatePart(IncreaseButtonPart, typeof(FieldIconButton))]
[TemplatePart(ValueGroupPart, typeof(NumberFieldValueGroup))]
[TemplatePart(DigitsPart, typeof(RollingDigits))]
[TemplatePart(LiveRegionPart, typeof(SpecialInputsLiveRegion))]
[PseudoClasses(EditingPseudoClass, WarnPseudoClass, StrainPseudoClass, StrainMaxPseudoClass, StrainMinPseudoClass,
    AtMaxPseudoClass, AtMinPseudoClass, DecreasePressedPseudoClass, IncreasePressedPseudoClass, ScrubPseudoClass,
    ScrubHoverPseudoClass, ScrubbingPseudoClass, SizedPseudoClass)]
public class NumberField : FieldTextBox
{
    public const string HeadPart = "PART_Head";
    public const string LabelPart = "PART_Label";
    public const string LimitNotePart = "PART_LimitNote";
    public const string DecreaseButtonPart = "PART_DecreaseButton";
    public const string IncreaseButtonPart = "PART_IncreaseButton";
    public const string ValueGroupPart = "PART_ValueGroup";
    public const string DigitsPart = "PART_Digits";
    public const string LiveRegionPart = "PART_LiveRegion";

    /// <summary>A draft is open (the typed text replaces the rolling digits).</summary>
    public const string EditingPseudoClass = ":editing";

    /// <summary>A typed value is past a limit (calm warning tint on the shell).</summary>
    public const string WarnPseudoClass = ":warn";

    /// <summary>A press just met a limit (420ms; <c>:strain-max</c> / <c>:strain-min</c> name the edge).</summary>
    public const string StrainPseudoClass = ":strain";
    public const string StrainMaxPseudoClass = ":strain-max";
    public const string StrainMinPseudoClass = ":strain-min";

    /// <summary>The shown value sits at the maximum / minimum (the stepper dims but stays pressable).</summary>
    public const string AtMaxPseudoClass = ":at-max";
    public const string AtMinPseudoClass = ":at-min";

    /// <summary>A stepper is held with a pointer.</summary>
    public const string DecreasePressedPseudoClass = ":decrease-pressed";
    public const string IncreasePressedPseudoClass = ":increase-pressed";

    /// <summary>The label scrubs (scrub enabled on an enabled field).</summary>
    public const string ScrubPseudoClass = ":scrub";

    /// <summary>The pointer is over a scrubbable label.</summary>
    public const string ScrubHoverPseudoClass = ":scrub-hover";

    /// <summary>The label is being dragged.</summary>
    public const string ScrubbingPseudoClass = ":scrubbing";

    /// <summary>An explicit <c>Width</c> is set: the shell and head stretch to it instead of the per-size width.</summary>
    public const string SizedPseudoClass = ":sized";

    private const double ScrubPixels = 6;
    private const int HoldDelay = 400;
    private const int HoldFastest = 40;
    private const int LimitPush = 240;
    private const int LimitHintMilliseconds = 1500;
    private const int UnderWarnMilliseconds = 700;
    private const int StrainMilliseconds = 420;
    private const int PushWindow = 700;
    private const double PushGain = 0.22;
    private const int PushCap = 4;
    private const double BumpVelocity = 130;

    private static readonly CultureInfo s_defaultCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    /// <summary>The value (two-way). Arc's <c>value</c> / <c>onValueChange</c>.</summary>
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<NumberField, decimal>(nameof(Value), 0m, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Lower limit and step-grid origin (default 0).</summary>
    public static readonly StyledProperty<decimal> MinimumProperty =
        AvaloniaProperty.Register<NumberField, decimal>(nameof(Minimum), 0m);

    /// <summary>Upper limit (default 9007199254740991, Arc's <c>Number.MAX_SAFE_INTEGER</c>).</summary>
    public static readonly StyledProperty<decimal> MaximumProperty =
        AvaloniaProperty.Register<NumberField, decimal>(nameof(Maximum), NumberFieldNumbers.MaxSafeInteger);

    /// <summary>One step (default 1; zero or less counts as 1). Its precision sets the default fraction digits.</summary>
    public static readonly StyledProperty<decimal> StepProperty =
        AvaloniaProperty.Register<NumberField, decimal>(nameof(Step), 1m);

    /// <summary>PageUp, PageDown and Shift with an arrow move this far (default ten steps).</summary>
    public static readonly StyledProperty<decimal?> LargeStepProperty =
        AvaloniaProperty.Register<NumberField, decimal?>(nameof(LargeStep));

    /// <summary>Formatting and parsing culture (Arc <c>locale</c>, default en-US, never the current culture).</summary>
    public static readonly StyledProperty<CultureInfo> CultureProperty =
        AvaloniaProperty.Register<NumberField, CultureInfo>(nameof(Culture), s_defaultCulture);

    /// <summary>Fewest fraction digits shown (default: the precision of <see cref="Step"/> and <see cref="Minimum"/>).</summary>
    public static readonly StyledProperty<int?> MinimumFractionDigitsProperty =
        AvaloniaProperty.Register<NumberField, int?>(nameof(MinimumFractionDigits));

    /// <summary>Most fraction digits shown (default: the precision of <see cref="Step"/> and <see cref="Minimum"/>).</summary>
    public static readonly StyledProperty<int?> MaximumFractionDigitsProperty =
        AvaloniaProperty.Register<NumberField, int?>(nameof(MaximumFractionDigits));

    /// <summary>Group separators in the display (default true).</summary>
    public static readonly StyledProperty<bool> UseGroupingProperty =
        AvaloniaProperty.Register<NumberField, bool>(nameof(UseGrouping), true);

    /// <summary>Drag the label sideways to scrub the value, one step every 6px (Arc <c>scrub</c>).</summary>
    public static readonly StyledProperty<bool> IsScrubEnabledProperty =
        AvaloniaProperty.Register<NumberField, bool>(nameof(IsScrubEnabled));

    /// <summary>Shows the limit note beside the label when a press meets a limit or a typed value passes one.</summary>
    public static readonly StyledProperty<bool> IsLimitHintEnabledProperty =
        AvaloniaProperty.Register<NumberField, bool>(nameof(IsLimitHintEnabled), true);

    /// <summary>Writes the limit note (default <c>"Max 10 seats"</c> / <c>"Min 1 seat"</c>).</summary>
    public static readonly StyledProperty<Func<NumberFieldLimitEdge, decimal, string>?> LimitHintFormatterProperty =
        AvaloniaProperty.Register<NumberField, Func<NumberFieldLimitEdge, decimal, string>?>(nameof(LimitHintFormatter));

    /// <summary>Computes the prefix from the value (Arc's function prefix); wins over <see cref="FieldTextBox.Prefix"/>.</summary>
    public static readonly StyledProperty<Func<decimal, string>?> PrefixFormatterProperty =
        AvaloniaProperty.Register<NumberField, Func<decimal, string>?>(nameof(PrefixFormatter));

    /// <summary>Computes the suffix from the value: <c>n =&gt; n == 1 ? " seat" : " seats"</c>.</summary>
    public static readonly StyledProperty<Func<decimal, string>?> SuffixFormatterProperty =
        AvaloniaProperty.Register<NumberField, Func<decimal, string>?>(nameof(SuffixFormatter));

    /// <summary>Accessible name prefix of the plus stepper (default "Increase", read as "Increase Seats").</summary>
    public static readonly StyledProperty<string> IncreaseTextProperty =
        AvaloniaProperty.Register<NumberField, string>(nameof(IncreaseText), "Increase");

    /// <summary>Accessible name prefix of the minus stepper (default "Decrease").</summary>
    public static readonly StyledProperty<string> DecreaseTextProperty =
        AvaloniaProperty.Register<NumberField, string>(nameof(DecreaseText), "Decrease");

    /// <summary>Raised after every real change of <see cref="Value"/> (never during template application).</summary>
    public static readonly RoutedEvent<NumberFieldValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<NumberField, NumberFieldValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>The value.</summary>
    public decimal Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Lower limit and step-grid origin.</summary>
    public decimal Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>Upper limit.</summary>
    public decimal Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>One step.</summary>
    public decimal Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>The large step (null: ten steps).</summary>
    public decimal? LargeStep { get => GetValue(LargeStepProperty); set => SetValue(LargeStepProperty, value); }

    /// <summary>Formatting and parsing culture.</summary>
    public CultureInfo Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <summary>Fewest fraction digits shown.</summary>
    public int? MinimumFractionDigits { get => GetValue(MinimumFractionDigitsProperty); set => SetValue(MinimumFractionDigitsProperty, value); }

    /// <summary>Most fraction digits shown.</summary>
    public int? MaximumFractionDigits { get => GetValue(MaximumFractionDigitsProperty); set => SetValue(MaximumFractionDigitsProperty, value); }

    /// <summary>Group separators in the display.</summary>
    public bool UseGrouping { get => GetValue(UseGroupingProperty); set => SetValue(UseGroupingProperty, value); }

    /// <summary>Drag the label to scrub.</summary>
    public bool IsScrubEnabled { get => GetValue(IsScrubEnabledProperty); set => SetValue(IsScrubEnabledProperty, value); }

    /// <summary>Shows the limit note.</summary>
    public bool IsLimitHintEnabled { get => GetValue(IsLimitHintEnabledProperty); set => SetValue(IsLimitHintEnabledProperty, value); }

    /// <summary>Writes the limit note.</summary>
    public Func<NumberFieldLimitEdge, decimal, string>? LimitHintFormatter { get => GetValue(LimitHintFormatterProperty); set => SetValue(LimitHintFormatterProperty, value); }

    /// <summary>Computes the prefix from the value.</summary>
    public Func<decimal, string>? PrefixFormatter { get => GetValue(PrefixFormatterProperty); set => SetValue(PrefixFormatterProperty, value); }

    /// <summary>Computes the suffix from the value.</summary>
    public Func<decimal, string>? SuffixFormatter { get => GetValue(SuffixFormatterProperty); set => SetValue(SuffixFormatterProperty, value); }

    /// <summary>Accessible name prefix of the plus stepper.</summary>
    public string IncreaseText { get => GetValue(IncreaseTextProperty); set => SetValue(IncreaseTextProperty, value); }

    /// <summary>Accessible name prefix of the minus stepper.</summary>
    public string DecreaseText { get => GetValue(DecreaseTextProperty); set => SetValue(DecreaseTextProperty, value); }

    /// <summary>Raised after every real change of <see cref="Value"/>.</summary>
    public event EventHandler<NumberFieldValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>The value as read aloud: prefix, formatted number and suffix (Arc <c>aria-valuetext</c>).</summary>
    public string ValueText => Spoken(Value);

    /// <summary>True while a typed draft is open.</summary>
    public bool IsEditing => _editing;

    private enum Region
    {
        Other,
        Head,
        Decrease,
        Increase,
        Value,
        Editor
    }

    // Draft and value state.
    private bool _syncingText;
    private bool _editing;
    private decimal _editStart;
    private bool _underWarn;
    private int _pushed;
    private int _direction = 1;
    private decimal _trailShown;
    private bool _trailStarted;
    private NumberFieldChangeSource? _pendingSource;
    private bool _sanitizePending;
    private (int Edge, int Count, long At) _effort;

    // Hold to repeat.
    private DispatcherTimer? _holdTimer;
    private int _holdToward;
    private long _holdSteps;
    private NumberFieldChangeSource _holdSource;
    private int _holdCount;
    private int _pressedSide;
    private IPointer? _holdPointer;
    private readonly HashSet<Key> _keysDown = new();
    private double _wheel;

    // Feedback timers.
    private DispatcherTimer? _pushedTimer;
    private DispatcherTimer? _underTimer;
    private DispatcherTimer? _strainTimer;

    // Scrub.
    private DragTracker? _scrub;
    private decimal _scrubFrom;
    private bool _scrubbing;

    // Parts.
    private Control? _head;
    private TextBlock? _label;
    private NumberFieldLimitNote? _note;
    private FieldIconButton? _decrease;
    private FieldIconButton? _increase;
    private NumberFieldStepGlyph? _decreaseGlyph;
    private NumberFieldStepGlyph? _increaseGlyph;
    private NumberFieldValueGroup? _group;
    private NumberFieldAffixText? _prefixText;
    private NumberFieldAffixText? _suffixText;
    private RollingDigits? _digits;
    private TextPresenter? _editor;
    private SpecialInputsLiveRegion? _live;
    private FieldMessage? _description;
    private FieldMessage? _error;

    /// <summary>Creates a number field.</summary>
    public NumberField()
    {
        // Steppers keep focus where it was and presses outside the editor never place a caret (Arc prevents the
        // mousedown default); handled in the tunnel so the focus manager never sees them.
        AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPreviewPointerReleased, RoutingStrategies.Tunnel);
        SyncText();
    }

    // ---- Number model -------------------------------------------------------------------------------------------

    internal decimal EffectiveStep => Step > 0 ? Step : 1m;

    internal decimal EffectiveLargeStep => LargeStep ?? EffectiveStep * 10;

    private CultureInfo EffectiveCulture => Culture ?? s_defaultCulture;

    private string GroupSeparator => EffectiveCulture.NumberFormat.NumberGroupSeparator;

    private string DecimalSeparator => EffectiveCulture.NumberFormat.NumberDecimalSeparator;

    private int Decimals => Math.Max(NumberFieldNumbers.DecimalsOf(EffectiveStep), NumberFieldNumbers.DecimalsOf(Minimum));

    private int MinFraction => Math.Clamp(MinimumFractionDigits ?? Decimals, 0, 28);

    private int MaxFraction => Math.Clamp(Math.Max(MinFraction, MaximumFractionDigits ?? Decimals), 0, 28);

    private decimal Clamp(decimal value) => Math.Min(Maximum, Math.Max(Minimum, value));

    private decimal Snap(decimal value) => NumberFieldNumbers.Snap(value, Minimum, EffectiveStep, Decimals);

    private decimal StepFrom(decimal from, long steps) => NumberFieldNumbers.StepFrom(from, steps, Minimum, EffectiveStep, Decimals);

    private decimal? Parse(string? text) => NumberFieldNumbers.Parse(text, GroupSeparator, DecimalSeparator);

    /// <summary>Formats a value with this field's culture, fraction digits and grouping (no prefix or suffix).</summary>
    public string FormatValue(decimal value)
        => NumberFieldNumbers.Format(value, EffectiveCulture, MinFraction, MaxFraction, UseGrouping);

    private string PrefixFor(decimal value) => PrefixFormatter?.Invoke(value) ?? Prefix as string ?? string.Empty;

    private string SuffixFor(decimal value) => SuffixFormatter?.Invoke(value) ?? Suffix as string ?? string.Empty;

    private string Spoken(decimal value) => $"{PrefixFor(value)}{FormatValue(value)}{SuffixFor(value)}".Trim();

    /// <summary>The value on display: the typed draft while it parses, else <see cref="Value"/>.</summary>
    private decimal Shown => _editing && Parse(Text) is { } typed ? typed : Value;

    /// <summary>1 / -1 while a typed draft is past the maximum / (after 700ms) under the minimum, else 0.</summary>
    private int Outside()
    {
        if (!_editing || Parse(Text) is not { } typed)
        {
            return 0;
        }
        if (typed > Maximum)
        {
            return 1;
        }
        return typed < Minimum && _underWarn ? -1 : 0;
    }

    // ---- Public actions -----------------------------------------------------------------------------------------

    /// <summary>Commits an open draft and moves one step up (quietly: no strain, no announcement).</summary>
    public void StepUp() => StepProgrammatic(1);

    /// <summary>Commits an open draft and moves one step down (quietly: no strain, no announcement).</summary>
    public void StepDown() => StepProgrammatic(-1);

    private void StepProgrammatic(int toward)
    {
        CommitDraft();
        var next = Clamp(StepFrom(Value, toward));
        if (next != Value)
        {
            SetValueFrom(next, NumberFieldChangeSource.Programmatic);
        }
    }

    internal void SetValueFromAutomation(decimal value)
    {
        CommitDraft();
        CommitValue(value, NumberFieldChangeSource.Type);
    }

    internal void SetTextFromAutomation(string? text)
    {
        if (Parse(text) is { } parsed)
        {
            SetValueFromAutomation(Snap(parsed));
        }
    }

    // ---- Template -------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _head = scope.Find<Control>(HeadPart);
        _label = scope.Find<TextBlock>(LabelPart);
        _note = scope.Find<NumberFieldLimitNote>(LimitNotePart);
        _decrease = scope.Find<FieldIconButton>(DecreaseButtonPart);
        _increase = scope.Find<FieldIconButton>(IncreaseButtonPart);
        _decreaseGlyph = scope.Find<NumberFieldStepGlyph>("PART_DecreaseGlyph");
        _increaseGlyph = scope.Find<NumberFieldStepGlyph>("PART_IncreaseGlyph");
        _group = scope.Find<NumberFieldValueGroup>(ValueGroupPart);
        _prefixText = scope.Find<NumberFieldAffixText>("PART_PrefixText");
        _suffixText = scope.Find<NumberFieldAffixText>("PART_SuffixText");
        _digits = scope.Find<RollingDigits>(DigitsPart);
        _editor = scope.Find<TextPresenter>("PART_TextPresenter");
        _live = scope.Find<SpecialInputsLiveRegion>(LiveRegionPart);
        _description = scope.Find<FieldMessage>("PART_Description");
        _error = scope.Find<FieldMessage>("PART_Error");

        foreach (var button in Steppers())
        {
            button.Click += OnStepperClick;
            button.PointerExited += OnStepperExited;
            button.PropertyChanged += OnStepperPropertyChanged;
            button.AddHandler(PointerMovedEvent, OnStepperMoved, RoutingStrategies.Bubble, true);
            button.AddHandler(PointerReleasedEvent, OnStepperReleased, RoutingStrategies.Bubble, true);
            button.AddHandler(PointerCaptureLostEvent, OnStepperCaptureLost, RoutingStrategies.Bubble | RoutingStrategies.Direct, true);
        }
        if (_label is not null)
        {
            _label.PointerEntered += OnLabelEntered;
            _label.PointerExited += OnLabelExited;
            _scrub = new DragTracker(_label, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 3, HandledEventsToo = true });
            _scrub.Pressed += OnScrubPressed;
            _scrub.Started += OnScrubStarted;
            _scrub.Moved += OnScrubMoved;
            _scrub.Completed += OnScrubEnded;
            _scrub.Cancelled += OnScrubEnded;
            _scrub.Released += OnScrubReleased;
        }
        if (_group is not null)
        {
            _group.IsEditing = _editing;
        }
        UpdateScrubState();
        UpdateSized();
        UpdateHead();
        UpdateDisplay();
        UpdateAutomation();
    }

    private IEnumerable<FieldIconButton> Steppers()
    {
        if (_decrease is not null)
        {
            yield return _decrease;
        }
        if (_increase is not null)
        {
            yield return _increase;
        }
    }

    private void DetachParts()
    {
        foreach (var button in Steppers())
        {
            button.Click -= OnStepperClick;
            button.PointerExited -= OnStepperExited;
            button.PropertyChanged -= OnStepperPropertyChanged;
            button.RemoveHandler(PointerMovedEvent, OnStepperMoved);
            button.RemoveHandler(PointerReleasedEvent, OnStepperReleased);
            button.RemoveHandler(PointerCaptureLostEvent, OnStepperCaptureLost);
        }
        if (_label is not null)
        {
            _label.PointerEntered -= OnLabelEntered;
            _label.PointerExited -= OnLabelExited;
        }
        _scrub?.Dispose();
        _scrub = null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new NumberFieldAutomationPeer(this);

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopHold();
        _keysDown.Clear();
        _strainTimer?.Stop();
        _pushedTimer?.Stop();
        _scrub?.Cancel();
        // Timed feedback ends with the timers: nothing is left stuck on when the field comes back.
        PseudoClasses.Set(StrainPseudoClass, false);
        PseudoClasses.Set(StrainMaxPseudoClass, false);
        PseudoClasses.Set(StrainMinPseudoClass, false);
        _pushed = 0;
        ClearUnder();
        UpdateNote();
    }

    // ---- Property changes -----------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ValueProperty)
        {
            OnValueChanged(change.GetOldValue<decimal>(), change.GetNewValue<decimal>());
        }
        else if (property == TextProperty)
        {
            if (!_syncingText)
            {
                OnUserTextChanged();
            }
        }
        else if (property == MinimumProperty || property == MaximumProperty || property == StepProperty ||
                 property == CultureProperty || property == MinimumFractionDigitsProperty ||
                 property == MaximumFractionDigitsProperty || property == UseGroupingProperty)
        {
            if (!_editing)
            {
                SyncText();
            }
            UpdateDisplay();
        }
        else if (property == PrefixProperty || property == SuffixProperty || property == PrefixFormatterProperty ||
                 property == SuffixFormatterProperty || property == IsLimitHintEnabledProperty ||
                 property == LimitHintFormatterProperty)
        {
            UpdateDisplay();
        }
        else if (property == IsScrubEnabledProperty || property == IsEffectivelyEnabledProperty || property == IsReadOnlyProperty)
        {
            UpdateScrubState();
            UpdateDisplay();
        }
        else if (property == IncreaseTextProperty || property == DecreaseTextProperty)
        {
            UpdateAutomation();
        }
        else if (property == WidthProperty)
        {
            UpdateSized();
        }
        else if (property == LabelProperty || property == IsLabelVisibleProperty)
        {
            UpdateHead();
        }
    }

    private void OnValueChanged(decimal oldValue, decimal newValue)
    {
        var source = _pendingSource ?? NumberFieldChangeSource.Programmatic;
        if (!_editing)
        {
            SyncText();
        }
        UpdateDisplay();
        RaiseEvent(new NumberFieldValueChangedEventArgs(ValueChangedEvent, oldValue, newValue, source));
    }

    private void SetValueFrom(decimal value, NumberFieldChangeSource source)
    {
        var previous = _pendingSource;
        _pendingSource = source;
        try
        {
            SetCurrentValue(ValueProperty, value);
        }
        finally
        {
            _pendingSource = previous;
        }
    }

    /// <summary>Writes the formatted value into the editor (no draft).</summary>
    private void SyncText()
    {
        var text = FormatValue(Value);
        if (string.Equals(Text, text, StringComparison.Ordinal))
        {
            return;
        }
        _syncingText = true;
        try
        {
            SetCurrentValue(TextProperty, text);
        }
        finally
        {
            _syncingText = false;
        }
        if (IsFocused)
        {
            CollapseCaretToEnd();
        }
    }

    private void CollapseCaretToEnd()
    {
        var end = Text?.Length ?? 0;
        SetCurrentValue(SelectionStartProperty, end);
        SetCurrentValue(SelectionEndProperty, end);
        SetCurrentValue(CaretIndexProperty, end);
    }

    // ---- Commit, strain, announce -------------------------------------------------------------------------------

    /// <summary>Every change lands here (Arc <c>commitValue</c>): clamp, strain and announce at a limit (not while scrubbing).</summary>
    private bool CommitValue(decimal next, NumberFieldChangeSource source)
    {
        var clamped = Clamp(next);
        var limit = next > clamped ? 1 : next < clamped ? -1 : 0;
        if (limit != 0 && source != NumberFieldChangeSource.Scrub)
        {
            Strain(limit, source);
            Announce($"{Spoken(clamped)}, {(limit > 0 ? "maximum" : "minimum")}");
        }
        else if (source == NumberFieldChangeSource.Button && clamped != Value)
        {
            Announce(Spoken(clamped));
        }
        if (clamped == Value)
        {
            return false;
        }
        SetValueFrom(clamped, source);
        return true;
    }

    private bool Nudge(int toward, long steps, NumberFieldChangeSource source)
        => CommitValue(StepFrom(Value, toward * steps), source);

    private void Announce(string message)
    {
        if (_live is null || string.Equals(_live.Message, message, StringComparison.Ordinal))
        {
            return;
        }
        _live.Announce(message);
    }

    /// <summary>
    /// A press past a limit: the value kicks toward it and springs home (a little further for each push in a row,
    /// capped), the refused stepper shakes once, the shell holds the strain state for 420ms and the note names the
    /// limit. Reduced motion keeps only a brief limit tint on the number (theme).
    /// </summary>
    private void Strain(int edge, NumberFieldChangeSource source)
    {
        var now = s_clock.ElapsedMilliseconds;
        var count = _effort.Edge == edge && now - _effort.At < PushWindow ? _effort.Count + 1 : 1;
        _effort = (edge, count, now);
        PseudoClasses.Set(StrainPseudoClass, true);
        PseudoClasses.Set(StrainMaxPseudoClass, edge > 0);
        PseudoClasses.Set(StrainMinPseudoClass, edge < 0);
        Restart(ref _strainTimer, StrainMilliseconds, () =>
        {
            PseudoClasses.Set(StrainPseudoClass, false);
            PseudoClasses.Set(StrainMaxPseudoClass, false);
            PseudoClasses.Set(StrainMinPseudoClass, false);
        });
        if (ArcMotion.IsFull(this))
        {
            _group?.Kick(-edge * BumpVelocity * (1 + Math.Min(count - 1, PushCap) * PushGain));
            if (source is NumberFieldChangeSource.Button or NumberFieldChangeSource.Key)
            {
                (edge > 0 ? _increaseGlyph : _decreaseGlyph)?.Shake();
            }
        }
        if (IsLimitHintEnabled)
        {
            _pushed = edge;
            Restart(ref _pushedTimer, LimitHintMilliseconds, () =>
            {
                _pushed = 0;
                UpdateNote();
            });
            UpdateNote();
        }
    }

    private static void Restart(ref DispatcherTimer? timer, int milliseconds, Action elapsed)
    {
        if (timer is null)
        {
            var created = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            created.Tick += (_, _) =>
            {
                created.Stop();
                (created.Tag as Action)?.Invoke();
            };
            timer = created;
        }
        timer.Stop();
        timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
        timer.Tag = elapsed;
        timer.Start();
    }

    // ---- Draft ------------------------------------------------------------------------------------------------------

    private bool AllowsDecimal => Decimals > 0 || MaxFraction > 0;

    private bool AllowsMinus => Minimum < 0;

    private string FilterDraft(string text)
        => NumberFieldNumbers.Filter(text, GroupSeparator, DecimalSeparator, AllowsDecimal, AllowsMinus);

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            // Typing while a stepper has focus must not edit the draft.
            return;
        }
        if (!e.Handled && e.Text is { Length: > 0 } text)
        {
            var filtered = FilterDraft(text);
            if (filtered.Length == 0)
            {
                e.Handled = true;
                return;
            }
            e.Text = filtered;
        }
        base.OnTextInput(e);
    }

    private void OnUserTextChanged()
    {
        var text = Text ?? string.Empty;
        if (!string.Equals(FilterDraft(text), text, StringComparison.Ordinal))
        {
            // Paste, drop or IME text with characters a number cannot hold: strip them once the edit settles.
            if (!_sanitizePending)
            {
                _sanitizePending = true;
                Dispatcher.UIThread.Post(Sanitize, DispatcherPriority.Input);
            }
            return;
        }
        OnDraft(text);
    }

    private void Sanitize()
    {
        _sanitizePending = false;
        var text = Text ?? string.Empty;
        var filtered = FilterDraft(text);
        if (string.Equals(filtered, text, StringComparison.Ordinal))
        {
            return;
        }
        var caret = Math.Clamp(CaretIndex, 0, text.Length);
        var removedBefore = caret - FilterDraft(text[..caret]).Length;
        _syncingText = true;
        try
        {
            SetCurrentValue(TextProperty, filtered);
        }
        finally
        {
            _syncingText = false;
        }
        var position = Math.Clamp(caret - removedBefore, 0, filtered.Length);
        SetCurrentValue(SelectionStartProperty, position);
        SetCurrentValue(SelectionEndProperty, position);
        SetCurrentValue(CaretIndexProperty, position);
        OnDraft(filtered);
    }

    /// <summary>Arc <c>onChange</c>: the draft applies live while valid, in range and on the grid.</summary>
    private void OnDraft(string draft)
    {
        if (!_editing)
        {
            _editStart = Value;
        }
        _group?.CaptureShift();
        SetEditing(true);
        var parsed = Parse(draft);
        if (parsed is { } value && value >= Minimum && value <= Maximum && Snap(value) == value)
        {
            CommitValue(value, NumberFieldChangeSource.Type);
        }
        // Past the maximum warns at once; under the minimum waits, since "1" is often the start of "12".
        ClearUnder();
        if (parsed is { } under && under < Minimum)
        {
            Restart(ref _underTimer, UnderWarnMilliseconds, () =>
            {
                _underWarn = true;
                UpdateNote();
            });
        }
        UpdateDisplay();
    }

    private void ClearUnder()
    {
        _underTimer?.Stop();
        _underWarn = false;
    }

    private void SetEditing(bool editing)
    {
        if (_editing == editing)
        {
            return;
        }
        _editing = editing;
        PseudoClasses.Set(EditingPseudoClass, editing);
        if (_group is not null)
        {
            _group.IsEditing = editing;
        }
    }

    /// <summary>Commits an open draft: snapped to the grid and clamped (with strain when clamped).</summary>
    private void CommitDraft()
    {
        if (!_editing)
        {
            return;
        }
        _group?.CaptureShift();
        var parsed = Parse(Text);
        SetEditing(false);
        ClearUnder();
        if (parsed is { } value)
        {
            CommitValue(Snap(value), NumberFieldChangeSource.Type);
        }
        SyncText();
        UpdateDisplay();
    }

    private void RevertDraft()
    {
        _group?.CaptureShift();
        SetEditing(false);
        ClearUnder();
        CommitValue(_editStart, NumberFieldChangeSource.Type);
        SyncText();
        CollapseCaretToEnd();
        UpdateDisplay();
    }

    // ---- Hold to repeat ---------------------------------------------------------------------------------------------

    /// <summary>One step now; after 400ms repeats that speed up (150ms × 0.86ⁿ, down to 40ms); held into a limit it
    /// keeps pushing every 240ms (the first push after 360ms).</summary>
    private void StartHold(int toward, decimal amount, NumberFieldChangeSource source)
    {
        StopHold();
        CommitDraft();
        var ratio = Math.Round(amount / EffectiveStep, MidpointRounding.AwayFromZero);
        _holdSteps = (long)Math.Clamp(ratio, 1m, int.MaxValue);
        _holdToward = toward;
        _holdSource = source;
        _holdCount = 0;
        if (source == NumberFieldChangeSource.Button)
        {
            SetPressedSide(toward);
        }
        var moved = Nudge(toward, _holdSteps, source);
        ScheduleHold(moved ? HoldDelay : LimitPush + 120);
    }

    private void ScheduleHold(double milliseconds)
    {
        if (_holdTimer is null)
        {
            _holdTimer = new DispatcherTimer();
            _holdTimer.Tick += (_, _) => HoldTick();
        }
        _holdTimer.Stop();
        _holdTimer.Interval = TimeSpan.FromMilliseconds(milliseconds);
        _holdTimer.Start();
    }

    /// <summary>The next repeat of a hold (internal for tests).</summary>
    internal void HoldTick()
    {
        _holdTimer?.Stop();
        if (_holdToward == 0)
        {
            return;
        }
        var moved = Nudge(_holdToward, _holdSteps, _holdSource);
        ScheduleHold(moved ? Math.Max(HoldFastest, 150 * Math.Pow(0.86, ++_holdCount)) : LimitPush);
    }

    /// <summary>The delay before the next repeat while a hold runs (tests); zero when idle.</summary>
    internal TimeSpan HoldInterval => _holdTimer is { IsEnabled: true } timer ? timer.Interval : TimeSpan.Zero;

    private void StopHold()
    {
        _holdTimer?.Stop();
        _holdToward = 0;
        _holdPointer = null;
        SetPressedSide(0);
    }

    private void SetPressedSide(int side)
    {
        _pressedSide = side;
        PseudoClasses.Set(DecreasePressedPseudoClass, side < 0);
        PseudoClasses.Set(IncreasePressedPseudoClass, side > 0);
        UpdateGlyphPress();
    }

    private void UpdateGlyphPress()
    {
        var shown = Shown;
        var enabled = IsEffectivelyEnabled;
        if (_decreaseGlyph is not null)
        {
            _decreaseGlyph.IsPressedVisual = enabled && shown > Minimum && (_pressedSide < 0 || _decrease?.IsPressed == true);
        }
        if (_increaseGlyph is not null)
        {
            _increaseGlyph.IsPressedVisual = enabled && shown < Maximum && (_pressedSide > 0 || _increase?.IsPressed == true);
        }
    }

    // ---- Keyboard -----------------------------------------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            // Keys pressed on a stepper belong to the stepper, not to the draft.
            return;
        }
        if (IsReadOnly || !IsEffectivelyEnabled)
        {
            base.OnKeyDown(e);
            return;
        }
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var step = EffectiveStep;
        var large = EffectiveLargeStep;
        (int Toward, decimal Amount)? move = e.Key switch
        {
            Key.Up => (1, shift ? large : step),
            Key.Down => (-1, shift ? large : step),
            Key.PageUp => (1, large),
            Key.PageDown => (-1, large),
            _ => null
        };
        if (move is { } m)
        {
            e.Handled = true;
            // Auto-repeat key downs are ignored: the hold timer repeats instead.
            if (_keysDown.Add(e.Key))
            {
                StartHold(m.Toward, m.Amount, NumberFieldChangeSource.Key);
            }
            return;
        }
        // Home and End reach the limits; while a draft is open they move the caret as in any text field.
        if (!_editing && (e.Key == Key.Home || (e.Key == Key.End && Maximum < NumberFieldNumbers.MaxSafeInteger)))
        {
            e.Handled = true;
            CommitValue(e.Key == Key.Home ? Minimum : Maximum, NumberFieldChangeSource.Key);
            return;
        }
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (_editing)
            {
                CommitDraft();
                CollapseCaretToEnd();
            }
            else
            {
                SelectAll();
            }
            return;
        }
        if (e.Key == Key.Escape && _editing)
        {
            e.Handled = true;
            RevertDraft();
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown)
        {
            _keysDown.Remove(e.Key);
            StopHold();
        }
        base.OnKeyUp(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            // A stepper took focus: the editor shows no caret.
            return;
        }
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        base.OnLostFocus(e);
        _keysDown.Clear();
        StopHold();
        CommitDraft();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Extension: one step per notch over the focused field; unfocused fields let the page scroll.
        if (!IsFocused || !IsEffectivelyEnabled || IsReadOnly || e.Delta.Y == 0)
        {
            base.OnPointerWheelChanged(e);
            return;
        }
        e.Handled = true;
        if (Math.Sign(e.Delta.Y) != Math.Sign(_wheel))
        {
            _wheel = 0;
        }
        _wheel += e.Delta.Y;
        while (Math.Abs(_wheel) >= 1)
        {
            var toward = Math.Sign(_wheel);
            _wheel -= toward;
            CommitDraft();
            Nudge(toward, 1, NumberFieldChangeSource.Wheel);
        }
    }

    // ---- Pointer ------------------------------------------------------------------------------------------------------

    private Region RegionOf(PointerEventArgs e)
    {
        for (var visual = e.Source as Visual; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, _decrease))
            {
                return Region.Decrease;
            }
            if (ReferenceEquals(visual, _increase))
            {
                return Region.Increase;
            }
            if (ReferenceEquals(visual, _head))
            {
                return Region.Head;
            }
            if (ReferenceEquals(visual, Frame) || ReferenceEquals(visual, _group))
            {
                if (_editor is not null && _group is not null)
                {
                    var p = e.GetPosition(_editor);
                    var g = e.GetPosition(_group);
                    if (p.X >= 0 && p.X <= _editor.Bounds.Width && g.Y >= 0 && g.Y <= _group.Bounds.Height)
                    {
                        return Region.Editor;
                    }
                }
                return Region.Value;
            }
        }
        return Region.Other;
    }

    private bool ScrubActive => IsScrubEnabled && IsEffectivelyEnabled && !IsReadOnly;

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        switch (RegionOf(e))
        {
            case Region.Decrease:
            case Region.Increase:
            {
                // Mouse presses keep focus where it was; an open draft commits with the step.
                e.Handled = true;
                var button = RegionOf(e) == Region.Increase ? _increase : _decrease;
                if (button is null || !IsEffectivelyEnabled || IsReadOnly)
                {
                    return;
                }
                if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    return;
                }
                // Capture first: the implicit capture it replaces must not read as the hold's capture loss.
                e.Pointer.Capture(button);
                StartHold(ReferenceEquals(button, _increase) ? 1 : -1, EffectiveStep, NumberFieldChangeSource.Button);
                _holdPointer = e.Pointer;
                break;
            }
            case Region.Head:
                if (ScrubActive)
                {
                    // The scrub tracker owns the press; a press that never drags focuses the editor on release.
                    e.Handled = true;
                }
                break;
            case Region.Value:
                // Arc: a click on the value area outside the editor focuses it and selects the number.
                e.Handled = true;
                if (IsEffectivelyEnabled)
                {
                    Focus(NavigationMethod.Pointer);
                    SelectAll();
                }
                break;
            case Region.Other:
                if (!ReferenceEquals(e.Source, this))
                {
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Touch focuses on release: keep steppers from taking focus there too.
        if (RegionOf(e) is Region.Decrease or Region.Increase)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (RegionOf(e) != Region.Editor)
        {
            e.Handled = true;
            return;
        }
        base.OnPointerPressed(e);
    }

    private void OnStepperReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ReferenceEquals(e.Pointer, _holdPointer))
        {
            StopHold();
            if (sender is IInputElement element && ReferenceEquals(e.Pointer.Captured, element))
            {
                e.Pointer.Capture(null);
            }
        }
    }

    private void OnStepperMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _holdPointer) || sender is not Control button)
        {
            return;
        }
        // Arc stops on pointerleave: leaving the stepper while held ends the hold.
        if (!new Rect(button.Bounds.Size).Contains(e.GetPosition(button)))
        {
            StopHold();
            e.Pointer.Capture(null);
        }
    }

    private void OnStepperExited(object? sender, PointerEventArgs e)
    {
        if (ReferenceEquals(e.Pointer, _holdPointer) && e.Pointer.Captured is null)
        {
            StopHold();
        }
    }

    private void OnStepperCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (ReferenceEquals(e.Pointer, _holdPointer))
        {
            StopHold();
        }
    }

    private void OnStepperClick(object? sender, RoutedEventArgs e)
    {
        // Keyboard and assistive activations take one step.
        if (IsReadOnly || !IsEffectivelyEnabled)
        {
            return;
        }
        CommitDraft();
        Nudge(ReferenceEquals(sender, _increase) ? 1 : -1, 1, NumberFieldChangeSource.Button);
    }

    private void OnStepperPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Avalonia.Controls.Button.IsPressedProperty)
        {
            UpdateGlyphPress();
        }
    }

    // ---- Scrub --------------------------------------------------------------------------------------------------------

    private void UpdateScrubState()
    {
        var active = ScrubActive;
        PseudoClasses.Set(ScrubPseudoClass, active);
        if (_scrub is not null)
        {
            _scrub.IsEnabled = active;
        }
        if (!active)
        {
            PseudoClasses.Set(ScrubHoverPseudoClass, false);
        }
    }

    private void OnLabelEntered(object? sender, PointerEventArgs e)
    {
        if (ScrubActive && e.Pointer.Type != PointerType.Touch)
        {
            PseudoClasses.Set(ScrubHoverPseudoClass, true);
        }
    }

    private void OnLabelExited(object? sender, PointerEventArgs e) => PseudoClasses.Set(ScrubHoverPseudoClass, false);

    private void OnScrubPressed(object? sender, DragGestureEventArgs e)
    {
        CommitDraft();
        _scrubFrom = Value;
    }

    private void OnScrubStarted(object? sender, DragGestureEventArgs e)
    {
        _scrubbing = true;
        PseudoClasses.Set(ScrubbingPseudoClass, true);
    }

    private void OnScrubMoved(object? sender, DragGestureEventArgs e)
    {
        var travel = e.Offset.X / ScrubPixels;
        var steps = (long)Math.Truncate(travel);
        CommitValue(steps != 0 ? StepFrom(_scrubFrom, steps) : _scrubFrom, NumberFieldChangeSource.Scrub);
        // Past a limit the value follows the pointer with resistance, then springs back on release.
        var step = (double)EffectiveStep;
        var raw = (double)_scrubFrom + travel * step;
        var max = (double)Maximum;
        var min = (double)Minimum;
        var over = raw > max ? (raw - max) / step * ScrubPixels : raw < min ? (raw - min) / step * ScrubPixels : 0;
        _group?.SetScrub(ArcMotion.IsFull(this) ? RubberBand.Apply(over, 10) : 0);
    }

    private void OnScrubEnded(object? sender, DragGestureEventArgs e)
    {
        if (!_scrubbing)
        {
            return;
        }
        _scrubbing = false;
        PseudoClasses.Set(ScrubbingPseudoClass, false);
        _group?.ReleaseScrub();
        Announce(Spoken(Value));
    }

    private void OnScrubReleased(object? sender, DragGestureEventArgs e)
    {
        // A press that never dragged is a label click: it focuses the editor.
        if (IsEffectivelyEnabled)
        {
            Focus(NavigationMethod.Pointer);
        }
    }

    // ---- Display --------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        FitMessage(_description);
        FitMessage(_error);
        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// A message row takes all the width it is offered; cap it at the wider of the shell and its own copy so a field in
    /// a row is as wide as Arc's grid (the shell, or a longer message) instead of the whole row. Copy still wraps when
    /// the field is narrower than that.
    /// </summary>
    private void FitMessage(FieldMessage? message)
    {
        if (message is null)
        {
            return;
        }
        var shell = Frame?.Width ?? double.NaN;
        if (double.IsNaN(shell))
        {
            message.MaxWidth = double.PositiveInfinity;
            return;
        }
        var text = message.Text;
        var cache = ReferenceEquals(message, _description) ? _descriptionFit : _errorFit;
        double natural;
        if (cache is { } hit && string.Equals(hit.Text, text, StringComparison.Ordinal) && hit.FontSize == message.FontSize)
        {
            natural = hit.Width;
        }
        else
        {
            natural = 0;
            if (!string.IsNullOrEmpty(text))
            {
                var layout = new Avalonia.Media.FormattedText(text, CultureInfo.CurrentCulture, Avalonia.Media.FlowDirection.LeftToRight,
                    new Avalonia.Media.Typeface(message.FontFamily, Avalonia.Media.FontStyle.Normal, message.FontWeight), message.FontSize, null);
                natural = Math.Ceiling(layout.WidthIncludingTrailingWhitespace) + message.Padding.Left + message.Padding.Right + 1;
            }
            if (ReferenceEquals(message, _description))
            {
                _descriptionFit = (text, message.FontSize, natural);
            }
            else
            {
                _errorFit = (text, message.FontSize, natural);
            }
        }
        message.MaxWidth = Math.Max(shell, natural);
    }

    private (string? Text, double FontSize, double Width)? _descriptionFit;
    private (string? Text, double FontSize, double Width)? _errorFit;

    private void UpdateSized() => PseudoClasses.Set(SizedPseudoClass, !double.IsNaN(Width));

    private void UpdateHead()
    {
        if (_label is not null)
        {
            _label.IsVisible = IsLabelVisible && !string.IsNullOrEmpty(Label);
        }
        if (_head is not null)
        {
            _head.IsVisible = (_label?.IsVisible ?? false) || (_note?.IsShown ?? false);
        }
    }

    private void UpdateDisplay()
    {
        var shown = Shown;
        if (!_trailStarted)
        {
            _trailStarted = true;
            _trailShown = shown;
        }
        else if (shown != _trailShown)
        {
            _direction = shown > _trailShown ? 1 : -1;
            _trailShown = shown;
        }
        if (_digits is not null)
        {
            var formatted = FormatValue(shown);
            _digits.CultureName = EffectiveCulture.Name;
            _digits.UseGrouping = UseGrouping;
            _digits.Decimals = NumberFieldNumbers.FractionDigitsIn(formatted, DecimalSeparator);
            _digits.Value = (double)NumberFieldNumbers.Round(shown, MaxFraction);
        }
        if (_prefixText is not null)
        {
            _prefixText.Direction = _direction;
            _prefixText.Text = PrefixFor(shown);
        }
        if (_suffixText is not null)
        {
            _suffixText.Direction = _direction;
            _suffixText.Text = SuffixFor(shown);
        }
        PseudoClasses.Set(AtMaxPseudoClass, shown >= Maximum);
        PseudoClasses.Set(AtMinPseudoClass, shown <= Minimum);
        UpdateGlyphPress();
        UpdateNote();
    }

    private string? NoteText(int edge)
    {
        if (edge == 0)
        {
            return null;
        }
        var limit = edge > 0 ? Maximum : Minimum;
        if (LimitHintFormatter is { } formatter)
        {
            return formatter(edge > 0 ? NumberFieldLimitEdge.Maximum : NumberFieldLimitEdge.Minimum, limit);
        }
        return $"{(edge > 0 ? "Max" : "Min")} {Spoken(limit)}";
    }

    private void UpdateNote()
    {
        var outside = Outside();
        PseudoClasses.Set(WarnPseudoClass, outside != 0);
        var edge = IsLimitHintEnabled ? (outside != 0 ? outside : _pushed) : 0;
        if (_note is not null)
        {
            if (NoteText(edge) is { } text)
            {
                _note.Show(text, edge);
            }
            else
            {
                _note.Hide();
            }
        }
        UpdateHead();
        UpdateAutomation();
    }

    /// <summary>The note's copy while it describes a typed value past a limit (joins the help text), else null.</summary>
    internal string? WarnNote => IsLimitHintEnabled ? NoteText(Outside()) : null;

    /// <summary>The note's copy while it is shown (tests).</summary>
    internal string? LimitNoteText => _note is { IsShown: true } note ? note.Text : null;

    /// <summary>The limit feedback group (tests).</summary>
    internal NumberFieldValueGroup? ValueGroup => _group;

    protected override void UpdateAutomation()
    {
        base.UpdateAutomation();
        var label = Label ?? string.Empty;
        if (_decrease is not null)
        {
            AutomationProperties.SetName(_decrease, $"{DecreaseText} {label}".Trim());
        }
        if (_increase is not null)
        {
            AutomationProperties.SetName(_increase, $"{IncreaseText} {label}".Trim());
        }
        if (WarnNote is { Length: > 0 } note)
        {
            var help = AutomationProperties.GetHelpText(this);
            AutomationProperties.SetHelpText(this, string.IsNullOrEmpty(help) ? note : $"{help} {note}");
        }
    }
}
