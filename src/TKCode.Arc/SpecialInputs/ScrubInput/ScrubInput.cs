using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What produced a <see cref="ScrubInput"/> value change (Arc's <c>details.source</c>).</summary>
public enum ScrubInputChangeSource
{
    /// <summary>
    /// <see cref="ScrubInput.Value"/> was set from code or a binding (raises only <see cref="ScrubInput.ValueChanged"/>,
    /// never <see cref="ScrubInput.ValueCommitted"/>, and gives no feedback).
    /// </summary>
    Programmatic,

    /// <summary>A drag on the label (also a drag cancelled with Escape).</summary>
    Scrub,

    /// <summary>A typed entry committed with Enter, blur, an arrow key or assistive technology.</summary>
    Type,

    /// <summary>An ArrowUp / ArrowDown step.</summary>
    Key,

    /// <summary>A double click on the label returning to <see cref="ScrubInput.ResetValue"/>.</summary>
    Reset
}

/// <summary>Data of <see cref="ScrubInput.ValueChanged"/> and <see cref="ScrubInput.ValueCommitted"/>.</summary>
public sealed class ScrubInputValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public ScrubInputValueChangedEventArgs(RoutedEvent routedEvent, double oldValue, double newValue,
                                           ScrubInputChangeSource source, string unit)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
        ChangeSource = source;
        Unit = unit;
    }

    /// <summary>
    /// The value before the change. For <see cref="ScrubInput.ValueCommitted"/> after a scrub it is the value the drag
    /// started from.
    /// </summary>
    public double OldValue { get; }

    /// <summary>The value after the change.</summary>
    public double NewValue { get; }

    /// <summary>
    /// What produced the change (Arc's <c>details.source</c>; named so that it does not hide
    /// <see cref="RoutedEventArgs.Source"/>, the raising element).
    /// </summary>
    public ScrubInputChangeSource ChangeSource { get; }

    /// <summary>The unit in effect after the change (empty when the field has none).</summary>
    public string Unit { get; }
}

/// <summary>
/// Arc's Scrub input: a compact, Figma-style number field for inspectors and property panels. Drag the label sideways to
/// scrub the value (Shift ten times coarser, Alt ten times finer, switching live and shown in a tag above the field),
/// or click in and type an exact value: entries accept arithmetic such as <c>12*2</c> or <c>(100-24)/2</c> and a unit
/// from <see cref="Units"/> such as <c>50%</c> (see <see cref="ScrubExpression"/>). Arrow keys step, Enter commits,
/// Escape restores the last value, and a double click on the label restores <see cref="ResetValue"/>. At
/// <see cref="Minimum"/> and <see cref="Maximum"/> the field strains past the limit with rubber band resistance and
/// springs back on release; a rejected entry shakes and turns the edge red for 900ms.
/// <code>
/// &lt;arc:ScrubInput Label="W" Value="{Binding Width}" Minimum="0" Unit="px" /&gt;
/// &lt;arc:ScrubInput Label="Rotation" Minimum="0" Maximum="360" IsWrapping="True" Unit="°" /&gt;
/// </code>
/// The row has no label or message rows of its own (the label is the drag handle) and fills its column by default.
/// </summary>
[TemplatePart(ShellPart, typeof(ScrubInputShell))]
[TemplatePart(HandlePart, typeof(ContentPresenter))]
[TemplatePart(BodyPart, typeof(Border))]
[TemplatePart(EditorPart, typeof(ScrubInputEditor))]
[TemplatePart(UnitPart, typeof(ScrubInputUnitButton))]
[TemplatePart(TagPart, typeof(Border))]
[TemplatePart(TagTextPart, typeof(TextBlock))]
[PseudoClasses(ScrubbingPseudoClass, InvalidPseudoClass, HoverPseudoClass, HandleHoverPseudoClass, TextLabelPseudoClass)]
public class ScrubInput : TemplatedControl
{
    internal const string ShellPart = "PART_Shell";
    internal const string HandlePart = "PART_Handle";
    internal const string BodyPart = "PART_Body";
    internal const string EditorPart = "PART_Editor";
    internal const string UnitPart = "PART_Unit";
    internal const string TagPart = "PART_Tag";
    internal const string TagTextPart = "PART_TagText";

    /// <summary>Set while a drag on the label is in progress (Arc <c>data-scrubbing</c>).</summary>
    public const string ScrubbingPseudoClass = ":scrubbing";

    /// <summary>Set for 900ms after a rejected entry (Arc <c>data-invalid</c>).</summary>
    public const string InvalidPseudoClass = ":invalid";

    /// <summary>A fine pointer (mouse or pen) is over the field; never set by touch or while disabled.</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>A fine pointer is over the label handle.</summary>
    public const string HandleHoverPseudoClass = ":handle-hover";

    /// <summary>The label is text (drawn at weight 500), not a glyph or other control.</summary>
    public const string TextLabelPseudoClass = ":text-label";

    /// <summary>Arc's SLOP: pointer travel before a press on the label becomes a scrub.</summary>
    private const double Slop = 3;

    /// <summary>Most pixels the field strains past a limit.</summary>
    private const double StrainLimit = 12;

    /// <summary>Stored overshoot cap in px, so reversing relaxes after a short way instead of a long walk back.</summary>
    private const double OvershootCap = 90;

    private const double KickVelocity = 260;

    /// <summary>Arc's <c>settle</c>: <c>physical(.34, .18)</c>.</summary>
    internal static readonly SpringSpec SettleSpring = SpringSpec.FromVisual(0.34, 0.18);

    /// <summary>Arc's <c>shake</c>: <c>physical(.3, .42)</c>.</summary>
    internal static readonly SpringSpec ShakeSpring = SpringSpec.FromVisual(0.3, 0.42);

    private static readonly TimeSpan InvalidDuration = TimeSpan.FromMilliseconds(900);
    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };
    private static readonly Avalonia.Animation.Easings.Easing s_easeInOut = new Avalonia.Animation.Easings.SplineEasing(0.42, 0, 0.58, 1);

    #region Properties

    /// <summary>The label, which is also the drag handle: text (weight 500) or a control such as a 14px <see cref="ArcGlyph"/>.</summary>
    public static readonly StyledProperty<object?> LabelProperty =
        AvaloniaProperty.Register<ScrubInput, object?>(nameof(Label));

    /// <summary>The value (two-way, default 0).</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ScrubInput, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Lower limit (default negative infinity: none).</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<ScrubInput, double>(nameof(Minimum), double.NegativeInfinity);

    /// <summary>Upper limit (default positive infinity: none).</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ScrubInput, double>(nameof(Maximum), double.PositiveInfinity);

    /// <summary>One step for arrows and scrubbing (default 1).</summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<ScrubInput, double>(nameof(Step), 1);

    /// <summary>Step with Alt held (null: a tenth of <see cref="Step"/>).</summary>
    public static readonly StyledProperty<double?> FineStepProperty =
        AvaloniaProperty.Register<ScrubInput, double?>(nameof(FineStep));

    /// <summary>Step with Shift held (null: ten times <see cref="Step"/>).</summary>
    public static readonly StyledProperty<double?> CoarseStepProperty =
        AvaloniaProperty.Register<ScrubInput, double?>(nameof(CoarseStep));

    /// <summary>Decimal places kept (null: the larger of the decimals of <see cref="Step"/> and the fine step).</summary>
    public static readonly StyledProperty<int?> PrecisionProperty =
        AvaloniaProperty.Register<ScrubInput, int?>(nameof(Precision));

    /// <summary>Pointer travel in px for one step while scrubbing (default 2).</summary>
    public static readonly StyledProperty<double> PixelsPerStepProperty =
        AvaloniaProperty.Register<ScrubInput, double>(nameof(PixelsPerStep), 2);

    /// <summary>Values wrap from the maximum to the minimum instead of stopping (needs both limits, Maximum &gt; Minimum).</summary>
    public static readonly StyledProperty<bool> IsWrappingProperty =
        AvaloniaProperty.Register<ScrubInput, bool>(nameof(IsWrapping));

    /// <summary>The unit after the number (two-way). Null shows the first of <see cref="Units"/>, or none.</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ScrubInput, string?>(nameof(Unit), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Units a typed value may carry; with more than one the unit becomes a button that cycles them.</summary>
    public static readonly StyledProperty<IList<string>?> UnitsProperty =
        AvaloniaProperty.Register<ScrubInput, IList<string>?>(nameof(Units));

    /// <summary>A double click on the label returns here (null: no reset).</summary>
    public static readonly StyledProperty<double?> ResetValueProperty =
        AvaloniaProperty.Register<ScrubInput, double?>(nameof(ResetValue));

    /// <summary>Shows the value's position between the limits as a quiet wash (null: on when both limits are finite).</summary>
    public static readonly StyledProperty<bool?> IsTrackVisibleProperty =
        AvaloniaProperty.Register<ScrubInput, bool?>(nameof(IsTrackVisible));

    /// <summary>Small (30px, 12px text) or Medium (36px, 14px text, default). Arc has no large size: Large renders as Medium.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<ScrubInput, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>
    /// Arc's <c>pointerLock</c>, kept for API parity (default true). Avalonia has no pointer lock, so scrubbing always
    /// uses normal pointer capture, as Arc does when a browser refuses the lock: a long drag can reach the screen edge.
    /// </summary>
    public static readonly StyledProperty<bool> IsPointerLockEnabledProperty =
        AvaloniaProperty.Register<ScrubInput, bool>(nameof(IsPointerLockEnabled), true);

    /// <summary>Accessible name of the unit button; <c>{0}</c> is the unit (Arc: "Unit: px. Change unit").</summary>
    public static readonly StyledProperty<string> UnitButtonNameFormatProperty =
        AvaloniaProperty.Register<ScrubInput, string>(nameof(UnitButtonNameFormat), "Unit: {0}. Change unit");

    /// <summary>True for 900ms after a rejected entry.</summary>
    public static readonly DirectProperty<ScrubInput, bool> IsInvalidProperty =
        AvaloniaProperty.RegisterDirect<ScrubInput, bool>(nameof(IsInvalid), o => o.IsInvalid);

    /// <summary>True while the label is being dragged.</summary>
    public static readonly DirectProperty<ScrubInput, bool> IsScrubbingProperty =
        AvaloniaProperty.RegisterDirect<ScrubInput, bool>(nameof(IsScrubbing), o => o.IsScrubbing);

    /// <summary>
    /// Raised for every real change of <see cref="Value"/>: each step of a scrub, typed entries, key steps and resets
    /// (Arc's <c>onValueChange</c>), and values set from code or a binding (<see cref="ScrubInputChangeSource.Programmatic"/>).
    /// </summary>
    public static readonly RoutedEvent<ScrubInputValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<ScrubInput, ScrubInputValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised once when a change settles: a scrub ends (or is cancelled), Enter or blur commits typing, a key step lands,
    /// or a reset. Like Arc it also fires when a commit leaves the value unchanged (a clamped entry at the limit).
    /// </summary>
    public static readonly RoutedEvent<ScrubInputValueChangedEventArgs> ValueCommittedEvent =
        RoutedEvent.Register<ScrubInput, ScrubInputValueChangedEventArgs>(nameof(ValueCommitted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="LabelProperty"/>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="MinimumProperty"/>
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <inheritdoc cref="MaximumProperty"/>
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <inheritdoc cref="StepProperty"/>
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <inheritdoc cref="FineStepProperty"/>
    public double? FineStep { get => GetValue(FineStepProperty); set => SetValue(FineStepProperty, value); }

    /// <inheritdoc cref="CoarseStepProperty"/>
    public double? CoarseStep { get => GetValue(CoarseStepProperty); set => SetValue(CoarseStepProperty, value); }

    /// <inheritdoc cref="PrecisionProperty"/>
    public int? Precision { get => GetValue(PrecisionProperty); set => SetValue(PrecisionProperty, value); }

    /// <inheritdoc cref="PixelsPerStepProperty"/>
    public double PixelsPerStep { get => GetValue(PixelsPerStepProperty); set => SetValue(PixelsPerStepProperty, value); }

    /// <inheritdoc cref="IsWrappingProperty"/>
    public bool IsWrapping { get => GetValue(IsWrappingProperty); set => SetValue(IsWrappingProperty, value); }

    /// <inheritdoc cref="UnitProperty"/>
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <inheritdoc cref="UnitsProperty"/>
    public IList<string>? Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }

    /// <inheritdoc cref="ResetValueProperty"/>
    public double? ResetValue { get => GetValue(ResetValueProperty); set => SetValue(ResetValueProperty, value); }

    /// <inheritdoc cref="IsTrackVisibleProperty"/>
    public bool? IsTrackVisible { get => GetValue(IsTrackVisibleProperty); set => SetValue(IsTrackVisibleProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="IsPointerLockEnabledProperty"/>
    public bool IsPointerLockEnabled { get => GetValue(IsPointerLockEnabledProperty); set => SetValue(IsPointerLockEnabledProperty, value); }

    /// <inheritdoc cref="UnitButtonNameFormatProperty"/>
    public string UnitButtonNameFormat { get => GetValue(UnitButtonNameFormatProperty); set => SetValue(UnitButtonNameFormatProperty, value); }

    /// <inheritdoc cref="IsInvalidProperty"/>
    public bool IsInvalid { get => _isInvalid; private set => SetAndRaise(IsInvalidProperty, ref _isInvalid, value); }

    /// <inheritdoc cref="IsScrubbingProperty"/>
    public bool IsScrubbing { get => _isScrubbing; private set => SetAndRaise(IsScrubbingProperty, ref _isScrubbing, value); }

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<ScrubInputValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <inheritdoc cref="ValueCommittedEvent"/>
    public event EventHandler<ScrubInputValueChangedEventArgs>? ValueCommitted
    {
        add => AddHandler(ValueCommittedEvent, value);
        remove => RemoveHandler(ValueCommittedEvent, value);
    }

    #endregion

    private enum ScrubModifier
    {
        None,
        Fine,
        Coarse
    }

    private sealed class ScrubDrag
    {
        public double Raw;
        public double Start;
        public bool Moved;
        public bool FinePointer;
    }

    private bool _isInvalid;
    private bool _isScrubbing;
    private ScrubInputShell? _shell;
    private ContentPresenter? _handle;
    private Border? _body;
    private ScrubInputEditor? _editor;
    private ScrubInputUnitButton? _unit;
    private Border? _tag;
    private TextBlock? _tagText;
    private readonly TranslateTransform _lean = new();
    private readonly TranslateTransform _tagOffset = new();
    private readonly ScaleTransform _tagScale = new();
    private DragTracker? _fineTracker;
    private DragTracker? _touchTracker;
    private ScrubDrag? _drag;
    private ScrubModifier _modifier;
    private string? _draft;
    private bool _syncingText;
    private bool _emitting;
    private bool _selectAllOnRelease;
    private bool _pointerInside;
    private bool _tagShown;
    private TopLevel? _keyRoot;
    private DispatcherTimer? _invalidTimer;
    private MotionValue<double>? _strain;
    private MotionValue<double>? _fill;
    private MotionValue<double>? _tagRise;
    private MotionValue<double>? _tagFade;

    static ScrubInput()
    {
        FocusableProperty.OverrideDefaultValue<ScrubInput>(false);
    }

    #region Derived values

    /// <summary>The step used with Alt: <see cref="FineStep"/> or a tenth of <see cref="Step"/>.</summary>
    public double EffectiveFineStep => FineStep ?? Step / 10;

    /// <summary>The step used with Shift: <see cref="CoarseStep"/> or ten times <see cref="Step"/>.</summary>
    public double EffectiveCoarseStep => CoarseStep ?? Step * 10;

    /// <summary>Decimal places kept: <see cref="Precision"/> or the larger of the decimals of the step and the fine step.</summary>
    public int EffectivePrecision => Precision ?? Math.Max(ScrubInputNumber.DecimalsOf(Step), ScrubInputNumber.DecimalsOf(EffectiveFineStep));

    /// <summary>The unit shown: <see cref="Unit"/>, else the first of <see cref="Units"/>, else empty.</summary>
    public string EffectiveUnit => Unit ?? (Units is { Count: > 0 } units ? units[0] : string.Empty);

    /// <summary>The value as the field shows it (rounded to <see cref="EffectivePrecision"/>, trailing zeros trimmed).</summary>
    public string FormattedValue => ScrubInputNumber.Format(Value, EffectivePrecision);

    internal bool HasFiniteRange => double.IsFinite(Minimum) && double.IsFinite(Maximum);

    internal bool CanWrap => IsWrapping && double.IsFinite(Minimum) && double.IsFinite(Maximum) && Maximum > Minimum;

    internal bool ShowTrack => (IsTrackVisible ?? true) && double.IsFinite(Minimum) && double.IsFinite(Maximum) && Maximum > Minimum;

    private IReadOnlyList<string> UnitList
    {
        get
        {
            if (Units is { } units)
            {
                return units as IReadOnlyList<string> ?? units.ToArray();
            }
            var unit = EffectiveUnit;
            return unit.Length > 0 ? [unit] : [];
        }
    }

    /// <summary>Arc's <c>aria-valuetext</c>: "50 percent", "90 degrees", "12 px".</summary>
    internal string ValueText
    {
        get
        {
            var unit = EffectiveUnit;
            var text = FormattedValue;
            if (unit.Length == 0)
            {
                return text;
            }
            var spoken = unit == "%" ? "percent" : unit is "deg" or "°" ? "degrees" : unit;
            return text + " " + spoken;
        }
    }

    /// <summary>The accessible name: <see cref="AutomationProperties.NameProperty"/>, else a text label.</summary>
    internal string? AutomationName
    {
        get
        {
            var name = AutomationProperties.GetName(this);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            return Label switch
            {
                string text => text,
                IFormattable number when IsTextLabel(Label) => number.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => null
            };
        }
    }

    /// <summary>The modifier tag text while scrubbing ("×10" / "×0.1"), or null.</summary>
    internal string? MultiplierText => _modifier switch
    {
        ScrubModifier.Coarse => "×" + ScrubInputNumber.Format(EffectiveCoarseStep / Step, 2),
        ScrubModifier.Fine => "×" + ScrubInputNumber.Format(EffectiveFineStep / Step, 3),
        _ => null
    };

    /// <summary>The current track fill fraction drawn by the shell (tests).</summary>
    internal double DisplayedFraction => _shell?.Fraction ?? 0;

    /// <summary>The current strain in px (tests).</summary>
    internal double DisplayedStrain => _strain?.Value ?? 0;

    internal ScrubInputEditor? Editor => _editor;

    private double TargetFraction => ShowTrack ? Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1) : 0;

    private static bool IsTextLabel(object? label) => label is string or int or long or short or byte or double or float or decimal;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();

        _shell = e.NameScope.Find<ScrubInputShell>(ShellPart);
        _handle = e.NameScope.Find<ContentPresenter>(HandlePart);
        _body = e.NameScope.Find<Border>(BodyPart);
        _editor = e.NameScope.Find<ScrubInputEditor>(EditorPart);
        _unit = e.NameScope.Find<ScrubInputUnitButton>(UnitPart);
        _tag = e.NameScope.Find<Border>(TagPart);
        _tagText = e.NameScope.Find<TextBlock>(TagTextPart);

        if (_body is not null)
        {
            _body.RenderTransform = _lean;
        }
        if (_tag is not null)
        {
            _tag.RenderTransform = new TransformGroup { Children = { _tagScale, _tagOffset } };
            _tag.IsVisible = false;
        }
        if (_tagText is not null)
        {
            _tagText.FontFeatures = s_tabular;
        }
        if (_handle is not null)
        {
            // Mouse and pen: Arc starts after 3px of travel whatever the direction. Touch: a vertical start scrolls (touch-action: pan-y).
            _fineTracker = new DragTracker(_handle, new DragOptions { Axis = DragAxis.Horizontal, Threshold = Slop, RejectCrossAxis = false })
            {
                CanBegin = e2 => e2.Pointer.Type != PointerType.Touch
            };
            _touchTracker = new DragTracker(_handle, new DragOptions { Axis = DragAxis.Horizontal, Threshold = Slop, RejectCrossAxis = true })
            {
                CanBegin = e2 => e2.Pointer.Type == PointerType.Touch
            };
            foreach (var tracker in new[] { _fineTracker, _touchTracker })
            {
                tracker.Pressed += OnScrubPressed;
                tracker.Started += OnScrubStarted;
                tracker.Moved += OnScrubMoved;
                tracker.Completed += OnScrubCompleted;
                tracker.Cancelled += OnScrubCancelled;
                tracker.Released += OnScrubReleased;
            }
            _handle.AddHandler(PointerPressedEvent, OnHandlePressedTunnel, RoutingStrategies.Tunnel, true);
            _handle.PointerEntered += OnHandlePointerEntered;
            _handle.PointerExited += OnHandlePointerExited;
            _handle.DoubleTapped += OnHandleDoubleTapped;
        }
        if (_editor is not null)
        {
            _editor.FontFeatures = s_tabular;
            _editor.TextChanging += OnEditorTextChanging;
            _editor.GotFocus += OnEditorGotFocus;
            _editor.LostFocus += OnEditorLostFocus;
            _editor.AddHandler(PointerReleasedEvent, OnEditorPointerReleased, RoutingStrategies.Bubble, true);
        }
        if (_unit is not null)
        {
            _unit.Click += OnUnitClick;
        }

        _strain = new MotionValue<double>(this, 0, MotionVectors.Double);
        _strain.Changed += OnStrainChanged;
        _fill = new MotionValue<double>(this, TargetFraction, MotionVectors.Double);
        _fill.Changed += OnFillChanged;
        _tagRise = new MotionValue<double>(this, 1, MotionVectors.Double);
        _tagRise.Changed += _ => ApplyTag();
        _tagFade = new MotionValue<double>(this, 0, MotionVectors.Double);
        _tagFade.Changed += _ => ApplyTag();

        UpdateLabel();
        UpdateUnit();
        UpdateShell();
        if (_shell is not null)
        {
            _shell.Fraction = TargetFraction;
        }
        SyncText();
    }

    private void DetachParts()
    {
        CancelScrubSilently();
        if (_fineTracker is not null)
        {
            _fineTracker.Dispose();
            _fineTracker = null;
        }
        if (_touchTracker is not null)
        {
            _touchTracker.Dispose();
            _touchTracker = null;
        }
        if (_handle is not null)
        {
            _handle.RemoveHandler(PointerPressedEvent, OnHandlePressedTunnel);
            _handle.PointerEntered -= OnHandlePointerEntered;
            _handle.PointerExited -= OnHandlePointerExited;
            _handle.DoubleTapped -= OnHandleDoubleTapped;
        }
        if (_editor is not null)
        {
            _editor.TextChanging -= OnEditorTextChanging;
            _editor.GotFocus -= OnEditorGotFocus;
            _editor.LostFocus -= OnEditorLostFocus;
            _editor.RemoveHandler(PointerReleasedEvent, OnEditorPointerReleased);
        }
        if (_unit is not null)
        {
            _unit.Click -= OnUnitClick;
        }
        _strain?.Stop();
        _fill?.Stop();
        _tagRise?.Stop();
        _tagFade?.Stop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelScrubSilently();
        _invalidTimer?.Stop();
        if (_isInvalid)
        {
            IsInvalid = false;
            PseudoClasses.Set(InvalidPseudoClass, false);
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ValueProperty)
        {
            SyncText();
            UpdateFill();
            // Values set from code or a binding are reported too (the Avalonia RangeBase convention), with no feedback;
            // changes made through the field are reported by Emit with their own source.
            if (!_emitting)
            {
                RaiseEvent(new ScrubInputValueChangedEventArgs(ValueChangedEvent, change.GetOldValue<double>(),
                    change.GetNewValue<double>(), ScrubInputChangeSource.Programmatic, EffectiveUnit));
            }
        }
        else if (property == MinimumProperty || property == MaximumProperty || property == IsTrackVisibleProperty)
        {
            UpdateShell();
            UpdateFill();
        }
        else if (property == StepProperty || property == FineStepProperty || property == PrecisionProperty)
        {
            SyncText();
        }
        else if (property == UnitProperty || property == UnitsProperty || property == UnitButtonNameFormatProperty)
        {
            UpdateUnit();
        }
        else if (property == LabelProperty)
        {
            UpdateLabel();
        }
        else if (property == IsEffectivelyEnabledProperty)
        {
            if (!IsEffectivelyEnabled)
            {
                CancelScrubSilently();
            }
            UpdateHover();
        }
    }

    private void UpdateLabel()
    {
        var label = Label;
        PseudoClasses.Set(TextLabelPseudoClass, IsTextLabel(label));
        // Arc draws a glyph label at 14px (.handle svg) unless the caller sized it.
        if (label is ArcGlyph glyph && !glyph.IsSet(ArcGlyph.SizeProperty))
        {
            glyph.SetCurrentValue(ArcGlyph.SizeProperty, 14.0);
        }
    }

    private void UpdateUnit()
    {
        if (_unit is null)
        {
            return;
        }
        var unit = EffectiveUnit;
        var cycling = Units is { Count: > 1 };
        _unit.IsVisible = unit.Length > 0;
        _unit.IsCycling = cycling;
        _unit.Unit = unit;
        AutomationProperties.SetName(_unit, string.Format(System.Globalization.CultureInfo.InvariantCulture, UnitButtonNameFormat, unit));
    }

    private void UpdateShell()
    {
        if (_shell is not null)
        {
            _shell.IsTrackVisible = ShowTrack;
        }
    }

    #endregion

    #region Motion

    // Code-driven motion: the strain and the fill are velocity-carrying springs fed by the pointer and by kicks with an
    // initial velocity, and the tag mixes a spring rise with a tween fade; ControlTheme transitions cannot express these.

    private void OnStrainChanged(double strain)
    {
        if (_shell is not null)
        {
            _shell.Strain = strain;
        }
        _lean.X = strain * 0.5;
    }

    private void OnFillChanged(double fraction)
    {
        if (_shell is not null)
        {
            _shell.Fraction = fraction;
        }
    }

    /// <summary>The fill follows a drag 1:1 and glides to typed or stepped values on the smooth spring.</summary>
    private void UpdateFill()
    {
        if (_fill is null)
        {
            return;
        }
        var target = TargetFraction;
        if (_drag is { Moved: true } || !ArcMotion.IsFull(this))
        {
            _fill.Jump(target);
            return;
        }
        if (_fill.Target != target || _fill.Value != target)
        {
            _ = _fill.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
    }

    /// <summary>A velocity kick on the strain: the field leans toward the push and springs home (none under reduced motion).</summary>
    private void Bump(int direction, SpringSpec spring)
    {
        if (_strain is null || !ArcMotion.IsFull(this))
        {
            return;
        }
        _ = _strain.AnimateTo(0, MotionSpec.Spring(spring), direction * KickVelocity);
    }

    private void ShowTag(string text)
    {
        if (_tag is null || _tagText is null || _tagRise is null || _tagFade is null)
        {
            return;
        }
        _tagText.Text = text;
        if (_tagShown)
        {
            return;
        }
        _tagShown = true;
        _tag.IsVisible = true;
        if (ArcMotion.IsFull(this))
        {
            _tagFade.Jump(1);
            _tagRise.Jump(0);
            _ = _tagRise.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _tagRise.Jump(1);
            _tagFade.Jump(0);
            _ = _tagFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, s_easeInOut));
        }
    }

    private async Task HideTagAsync()
    {
        if (!_tagShown || _tag is null || _tagRise is null || _tagFade is null)
        {
            return;
        }
        _tagShown = false;
        _tagRise.Stop();
        var reached = await _tagFade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, s_easeInOut));
        if (reached && !_tagShown)
        {
            _tag.IsVisible = false;
        }
    }

    private void ApplyTag()
    {
        if (_tag is null || _tagRise is null || _tagFade is null)
        {
            return;
        }
        var rise = _tagRise.Value;
        _tagOffset.Y = 4 * (1 - rise);
        var scale = 0.96 + 0.04 * rise;
        _tagScale.ScaleX = scale;
        _tagScale.ScaleY = scale;
        _tag.Opacity = Math.Clamp(rise, 0, 1) * Math.Clamp(_tagFade.Value, 0, 1);
    }

    private void SetModifier(ScrubModifier modifier)
    {
        if (_modifier == modifier)
        {
            return;
        }
        _modifier = modifier;
        if (MultiplierText is { } text)
        {
            ShowTag(text);
        }
        else
        {
            _ = HideTagAsync();
        }
    }

    private void FlagInvalid()
    {
        IsInvalid = true;
        PseudoClasses.Set(InvalidPseudoClass, true);
        if (_invalidTimer is null)
        {
            _invalidTimer = new DispatcherTimer { Interval = InvalidDuration };
            _invalidTimer.Tick += OnInvalidTimerTick;
        }
        _invalidTimer.Stop();
        _invalidTimer.Start();
        Bump(-1, ShakeSpring);
    }

    private void OnInvalidTimerTick(object? sender, EventArgs e)
    {
        _invalidTimer?.Stop();
        IsInvalid = false;
        PseudoClasses.Set(InvalidPseudoClass, false);
    }

    #endregion

    #region Value model

    /// <summary>Rounds to the precision, then wraps or clamps to the limits (Arc's <c>constrain</c>).</summary>
    internal double Constrain(double next)
    {
        var places = EffectivePrecision;
        var rounded = ScrubInputNumber.RoundTo(next, places);
        var min = Minimum;
        var max = Maximum;
        if (CanWrap)
        {
            var span = max - min;
            return ScrubInputNumber.RoundTo(((rounded - min) % span + span) % span + min, places);
        }
        return Math.Min(max, Math.Max(min, rounded));
    }

    private void Emit(double next, ScrubInputChangeSource source, bool commit, double? committedFrom = null)
    {
        var old = Value;
        if (!next.Equals(old))
        {
            _emitting = true;
            try
            {
                SetCurrentValue(ValueProperty, next);
            }
            finally
            {
                _emitting = false;
            }
            RaiseEvent(new ScrubInputValueChangedEventArgs(ValueChangedEvent, old, next, source, EffectiveUnit));
        }
        if (commit)
        {
            RaiseEvent(new ScrubInputValueChangedEventArgs(ValueCommittedEvent, committedFrom ?? old, Value, source, EffectiveUnit));
        }
    }

    private void SetUnitFromUser(string unit)
    {
        if (unit != EffectiveUnit)
        {
            SetCurrentValue(UnitProperty, unit);
        }
    }

    /// <summary>Commits the typed draft. Returns false when the entry was rejected (the old value stays).</summary>
    private bool CommitDraft()
    {
        var text = _draft;
        _draft = null;
        if (text is null)
        {
            return true;
        }
        if (text.Trim() == FormattedValue)
        {
            SyncText();
            return true;
        }
        var parsed = ScrubExpression.Evaluate(text, UnitList);
        if (parsed is not { } result)
        {
            SyncText();
            FlagInvalid();
            return false;
        }
        if (result.Unit is { } unit && unit != EffectiveUnit)
        {
            SetUnitFromUser(unit);
        }
        var next = Constrain(result.Value);
        if (!CanWrap && (result.Value > Maximum || result.Value < Minimum))
        {
            Bump(result.Value > Maximum ? 1 : -1, SettleSpring);
        }
        Emit(next, ScrubInputChangeSource.Type, true);
        SyncText();
        return true;
    }

    private void StepBy(int direction, KeyModifiers modifiers)
    {
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var alt = modifiers.HasFlag(KeyModifiers.Alt);
        var amount = shift ? EffectiveCoarseStep : alt ? EffectiveFineStep : Step;
        var current = Value;
        if (!CanWrap && ((direction > 0 && current >= Maximum) || (direction < 0 && current <= Minimum)))
        {
            Bump(direction, SettleSpring);
            return;
        }
        if (!(amount > 0))
        {
            return;
        }
        // Coarse steps land on the coarse grid, so 13 with Shift goes to 20, not 23.
        var snapped = shift
            ? (direction > 0 ? Math.Floor(current / amount + 1e-9) + 1 : Math.Ceiling(current / amount - 1e-9) - 1) * amount
            : current + direction * amount;
        Emit(Constrain(snapped), ScrubInputChangeSource.Key, true);
    }

    internal void SetValueFromAutomation(double value)
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        _draft = null;
        Emit(Constrain(value), ScrubInputChangeSource.Type, true);
        SyncText();
    }

    #endregion

    #region Editor

    private void SyncText()
    {
        if (_editor is null || _draft is not null)
        {
            return;
        }
        var text = FormattedValue;
        if (_editor.Text == text)
        {
            return;
        }
        _syncingText = true;
        try
        {
            _editor.Text = text;
        }
        finally
        {
            _syncingText = false;
        }
    }

    private void SelectAll()
    {
        _editor?.SelectAll();
    }

    // TextChanging is synchronous (TextChanged is posted), so text written by SyncText is never mistaken for a draft.
    private void OnEditorTextChanging(object? sender, TextChangingEventArgs e)
    {
        if (_syncingText || _editor is null)
        {
            return;
        }
        _draft = _editor.Text ?? string.Empty;
    }

    private void OnEditorGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // Focus selects everything; a click lands its caret after focus, so a pointer focus selects on release.
        if (e.NavigationMethod == NavigationMethod.Pointer)
        {
            _selectAllOnRelease = true;
        }
        else
        {
            SelectAll();
        }
    }

    private void OnEditorPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_selectAllOnRelease || _editor is null)
        {
            return;
        }
        _selectAllOnRelease = false;
        if (_editor.SelectionStart == _editor.SelectionEnd)
        {
            SelectAll();
        }
    }

    private void OnEditorLostFocus(object? sender, FocusChangedEventArgs e)
    {
        _selectAllOnRelease = false;
        CommitDraft();
    }

    internal void HandleEditorKeyDown(KeyEventArgs e)
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
            {
                e.Handled = true;
                if (_draft is not null && !CommitDraft())
                {
                    return;
                }
                StepBy(e.Key == Key.Up ? 1 : -1, e.KeyModifiers);
                SyncText();
                SelectAll();
                return;
            }
            case Key.Enter:
            {
                e.Handled = true;
                CommitDraft();
                SelectAll();
                return;
            }
            case Key.Escape when _draft is not null:
            {
                e.Handled = true;
                _draft = null;
                SyncText();
                SelectAll();
                return;
            }
        }
    }

    private void OnUnitClick(object? sender, RoutedEventArgs e)
    {
        var list = UnitList;
        if (list.Count < 2 || !IsEffectivelyEnabled)
        {
            return;
        }
        var index = -1;
        var unit = EffectiveUnit;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == unit)
            {
                index = i;
                break;
            }
        }
        SetUnitFromUser(list[(index + 1) % list.Count]);
    }

    #endregion

    #region Scrubbing

    private void OnHandlePressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        // A touch press whose release went to a scrolling parent leaves a tracker pending and a stale press; start fresh.
        if (_fineTracker is { IsDragging: true } || _touchTracker is { IsDragging: true } || _drag is { Moved: true })
        {
            return;
        }
        _drag = null;
        _fineTracker?.Cancel();
        _touchTracker?.Cancel();
    }

    private void OnScrubPressed(object? sender, DragGestureEventArgs e)
    {
        if (!IsEffectivelyEnabled || _drag is not null)
        {
            return;
        }
        if (_draft is not null)
        {
            CommitDraft();
        }
        _drag = new ScrubDrag { Raw = Value, Start = Value, FinePointer = e.PointerType != PointerType.Touch };
    }

    private void OnScrubStarted(object? sender, DragGestureEventArgs e)
    {
        if (_drag is null)
        {
            return;
        }
        _drag.Moved = true;
        // The editor gives up focus while scrubbing (Arc blurs the input).
        if (_editor is { IsKeyboardFocusWithin: true } && TopLevel.GetTopLevel(this) is { FocusManager: { } focus })
        {
            focus.Focus(null);
        }
        IsScrubbing = true;
        PseudoClasses.Set(ScrubbingPseudoClass, true);
        UpdateHover();
        AttachKeys();
    }

    private void OnScrubMoved(object? sender, DragGestureEventArgs e)
    {
        var state = _drag;
        if (state is not { Moved: true })
        {
            return;
        }
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var amount = shift ? EffectiveCoarseStep : alt ? EffectiveFineStep : Step;
        SetModifier(shift ? ScrubModifier.Coarse : alt ? ScrubModifier.Fine : ScrubModifier.None);
        if (!(amount > 0))
        {
            return;
        }
        var pixelsPerStep = PixelsPerStep > 0 ? PixelsPerStep : 2;
        state.Raw += e.Delta.X / pixelsPerStep * amount;

        if (!CanWrap)
        {
            var perPx = amount / pixelsPerStep;
            var max = Maximum;
            var min = Minimum;
            var over = state.Raw > max ? (state.Raw - max) / perPx : state.Raw < min ? (state.Raw - min) / perPx : 0;
            // Cap the stored overshoot so reversing starts moving the value again after a short relax, not a long walk back.
            if (Math.Abs(over) > OvershootCap)
            {
                state.Raw = (over > 0 ? max : min) + Math.Sign(over) * OvershootCap * perPx;
            }
            _strain?.Jump(ArcMotion.IsFull(this) ? RubberBand.Apply(Math.Clamp(over, -OvershootCap, OvershootCap), StrainLimit) : 0);
        }
        var snapped = ScrubInputNumber.JsRound(state.Raw / amount) * amount;
        var next = Constrain(snapped);
        if (!next.Equals(Value))
        {
            Emit(next, ScrubInputChangeSource.Scrub, false);
        }
    }

    private void OnScrubCompleted(object? sender, DragGestureEventArgs e) => EndScrub(false);

    // Capture lost is Arc's pointercancel: the scrub ends and commits where it is.
    private void OnScrubCancelled(object? sender, DragGestureEventArgs e) => EndScrub(false);

    private void OnScrubReleased(object? sender, DragGestureEventArgs e)
    {
        var wasPressed = _drag is not null;
        EndScrub(false);
        if (!wasPressed || !IsEffectivelyEnabled || _editor is null)
        {
            return;
        }
        // A touch that turned into a scroll is not a click.
        var travel = e.Position - e.Start;
        if (Math.Sqrt(travel.X * travel.X + travel.Y * travel.Y) >= Slop)
        {
            return;
        }
        // A plain click on the label focuses the number and selects it.
        _editor.Focus();
        SelectAll();
    }

    private void OnHandleDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!IsEffectivelyEnabled || ResetValue is not { } reset)
        {
            return;
        }
        Emit(Constrain(reset), ScrubInputChangeSource.Reset, true);
        SyncText();
    }

    private void EndScrub(bool cancel)
    {
        var state = _drag;
        _drag = null;
        if (state is null)
        {
            return;
        }
        DetachKeys();
        IsScrubbing = false;
        PseudoClasses.Set(ScrubbingPseudoClass, false);
        SetModifier(ScrubModifier.None);
        UpdateHover();
        if (!state.Moved)
        {
            return;
        }
        if (cancel)
        {
            Emit(state.Start, ScrubInputChangeSource.Scrub, true, state.Start);
        }
        else
        {
            RaiseEvent(new ScrubInputValueChangedEventArgs(ValueCommittedEvent, state.Start, Value, ScrubInputChangeSource.Scrub, EffectiveUnit));
        }
        if (_strain is not null)
        {
            if (ArcMotion.IsFull(this))
            {
                _ = _strain.AnimateTo(0, MotionSpec.Spring(SettleSpring));
            }
            else
            {
                _strain.Jump(0);
            }
        }
    }

    /// <summary>Ends a scrub without events (template change, detach, disable).</summary>
    private void CancelScrubSilently()
    {
        if (_drag is null)
        {
            return;
        }
        _drag = null;
        DetachKeys();
        IsScrubbing = false;
        PseudoClasses.Set(ScrubbingPseudoClass, false);
        SetModifier(ScrubModifier.None);
        _fineTracker?.Cancel();
        _touchTracker?.Cancel();
        _strain?.Jump(0);
        UpdateHover();
    }

    private void AttachKeys()
    {
        if (_keyRoot is not null)
        {
            return;
        }
        _keyRoot = TopLevel.GetTopLevel(this);
        _keyRoot?.AddHandler(KeyDownEvent, OnScrubKeyDown, RoutingStrategies.Tunnel, true);
        _keyRoot?.AddHandler(KeyUpEvent, OnScrubKeyUp, RoutingStrategies.Tunnel, true);
    }

    private void DetachKeys()
    {
        if (_keyRoot is null)
        {
            return;
        }
        _keyRoot.RemoveHandler(KeyDownEvent, OnScrubKeyDown);
        _keyRoot.RemoveHandler(KeyUpEvent, OnScrubKeyUp);
        _keyRoot = null;
    }

    private void OnScrubKeyDown(object? sender, KeyEventArgs e)
    {
        if (_drag is not { Moved: true })
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            // Escape cancels back to where the drag started.
            e.Handled = true;
            EndScrub(true);
            _fineTracker?.Cancel();
            _touchTracker?.Cancel();
            return;
        }
        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            SetModifier(ScrubModifier.Coarse);
        }
        else if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            SetModifier(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? ScrubModifier.Coarse : ScrubModifier.Fine);
        }
    }

    private void OnScrubKeyUp(object? sender, KeyEventArgs e)
    {
        if (_drag is not { Moved: true })
        {
            return;
        }
        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            SetModifier(e.KeyModifiers.HasFlag(KeyModifiers.Alt) ? ScrubModifier.Fine : ScrubModifier.None);
        }
        else if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            SetModifier(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? ScrubModifier.Coarse : ScrubModifier.None);
        }
    }

    #endregion

    #region Hover

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _pointerInside = e.Pointer.Type != PointerType.Touch;
        UpdateHover();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerInside = false;
        UpdateHover();
    }

    // Under pointer capture the browser keeps :hover on the captured label and its ancestors, so a mouse scrub stays hovered.
    private void UpdateHover() =>
        PseudoClasses.Set(HoverPseudoClass, IsEffectivelyEnabled && (_pointerInside || _drag is { Moved: true, FinePointer: true }));

    private void OnHandlePointerEntered(object? sender, PointerEventArgs e)
    {
        PseudoClasses.Set(HandleHoverPseudoClass, IsEffectivelyEnabled && e.Pointer.Type != PointerType.Touch);
    }

    private void OnHandlePointerExited(object? sender, PointerEventArgs e)
    {
        PseudoClasses.Set(HandleHoverPseudoClass, false);
    }

    #endregion
}
