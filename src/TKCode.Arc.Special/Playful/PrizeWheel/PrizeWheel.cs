using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A promo or referral prize wheel with real momentum: flick the wheel and it keeps the speed you gave it, or hold the
/// hub to wind it back while a ring fills and let go to throw it. A flexible pointer bends against every peg and snaps
/// back with a short wobble, the wheel slows into a long settle on a slice drawn by weight, and the won slice lifts into a
/// reward card with a copyable code beside a list of every prize and its chance.
/// </summary>
/// <remarks>
/// The winner is decided when the spin starts (by the weights, or by <see cref="SpinTo"/> with a winner from your
/// server), so the motion only shows a result that is already known. Gestures throw the wheel at the speed of the hand;
/// <see cref="Spin"/> and <see cref="SpinTo"/> follow <see cref="Duration"/> and <see cref="Turns"/>. Under reduced motion
/// the wheel lands at once and the card appears in place.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(FacePart, typeof(PrizeWheelFace))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(SpinningPseudoClass, ChargingPseudoClass, WonPseudoClass, ClaimedPseudoClass)]
public class PrizeWheel : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string FacePart = "PART_Face";
    public const string StatusPart = "PART_Status";
    public const string SpinningPseudoClass = ":spinning";

    /// <summary>Set while the hub is held and the wheel winds up.</summary>
    public const string ChargingPseudoClass = ":charging";

    /// <summary>Set while the wheel rests on a prize.</summary>
    public const string WonPseudoClass = ":won";

    /// <summary>Set when a single-spin wheel has given its prize (<see cref="IsSingleSpin"/>).</summary>
    public const string ClaimedPseudoClass = ":claimed";

    internal const string IdlePart = "PART_Idle";
    internal const string PanelPart = "PART_Panel";
    internal const string CardPart = "PART_Card";
    internal const string SizerPart = "PART_Sizer";
    internal const string OddsPart = "PART_Odds";
    internal const string HubFocusPart = "PART_HubFocus";

    private const double HighlightSeconds = 0.36;
    private const double FlightDelay = 0.1;
    private const double FlightStartScale = 0.18;
    private const double CardExitSeconds = 0.15;
    private const double HubPressScale = 0.95;
    private const double HubSpring = 18.2;
    private static readonly SpringSpec s_flight = SpringSpec.FromPhysical(74, 15);

    /// <summary>
    /// How long a spin started from code (<see cref="Spin"/>, <see cref="SpinTo"/>) takes from the first push to the stop.
    /// Spins thrown by a flick or the hub last as long as their speed carries them.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<PrizeWheel, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(4.8));

    /// <summary>
    /// The least number of full turns a spin started from code (<see cref="Spin"/>, <see cref="SpinTo"/>) makes before
    /// it settles on the winner. Spins thrown by a flick or the hub turn as far as their speed carries them.
    /// </summary>
    public static readonly StyledProperty<int> TurnsProperty =
        AvaloniaProperty.Register<PrizeWheel, int>(nameof(Turns), 5);

    /// <summary>
    /// Whether slices are as wide as their odds (true) or all the same width. Slices too thin for a label drop it; the
    /// odds list still names them.
    /// </summary>
    public static readonly StyledProperty<bool> SizesByWeightProperty =
        AvaloniaProperty.Register<PrizeWheel, bool>(nameof(SizesByWeight), true);

    /// <summary>The word on the hub, which winds up and throws the wheel.</summary>
    public static readonly StyledProperty<string> SpinTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(SpinText), "Spin");

    /// <summary>The accessible name of the wheel.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(Label), "Prize wheel");

    /// <summary>
    /// Whether a person can spin the wheel by flicking it, holding the hub, pressing Enter or Space, or with "Spin again"
    /// (true). When false the wheel only spins from code and is not a Tab stop.
    /// </summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<PrizeWheel, bool>(nameof(IsInteractive), true);

    /// <summary>
    /// Whether the reward panel shows beside or under the wheel: the title, description and gesture hint, the reward
    /// card after a win, and the odds list (true). False leaves the wheel alone.
    /// </summary>
    public static readonly StyledProperty<bool> IsPanelVisibleProperty =
        AvaloniaProperty.Register<PrizeWheel, bool>(nameof(IsPanelVisible), true);

    /// <summary>Whether the panel lists every prize with its chance (true).</summary>
    public static readonly StyledProperty<bool> IsOddsVisibleProperty =
        AvaloniaProperty.Register<PrizeWheel, bool>(nameof(IsOddsVisible), true);

    /// <summary>
    /// Allows one spin only: once it lands the wheel locks on the prize, the hub reads <see cref="ClaimedText"/> and
    /// "Spin again" is hidden. <see cref="ClearResult"/> unlocks it.
    /// </summary>
    public static readonly StyledProperty<bool> IsSingleSpinProperty =
        AvaloniaProperty.Register<PrizeWheel, bool>(nameof(IsSingleSpin));

    /// <summary>Multiplies the slow drift of the wheel before its first spin (1); 0 holds it still.</summary>
    public static readonly StyledProperty<double> DriftSpeedProperty =
        AvaloniaProperty.Register<PrizeWheel, double>(nameof(DriftSpeed), 1);

    /// <summary>Holds the idle drift. A spin a person starts still runs to the end.</summary>
    public static readonly StyledProperty<bool> IsDriftPausedProperty =
        AvaloniaProperty.Register<PrizeWheel, bool>(nameof(IsDriftPaused));

    /// <summary>
    /// The color of the pointer, the featured prize, the charge ring and the won slice; the theme's accent by default.
    /// </summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<PrizeWheel, IBrush?>(nameof(AccentBrush));

    /// <summary>The heading beside the wheel before the first prize.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(Title), "Try your luck");

    /// <summary>One line under the heading.</summary>
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(Description), "Every slice is a reward you can use right away.");

    /// <summary>The gesture hint under the description; it also describes the wheel to assistive technology.</summary>
    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(HintText), "Drag the wheel and let go, or press and hold the hub.");

    /// <summary>The hint shown instead after a flick too gentle to spin the wheel.</summary>
    public static readonly StyledProperty<string> WeakHintTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(WeakHintText), "Too gentle. Give it a quicker flick.");

    /// <summary>The label of the card's button that spins once more.</summary>
    public static readonly StyledProperty<string> SpinAgainTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(SpinAgainText), "Spin again");

    /// <summary>The hub's word once a single-spin wheel has given its prize.</summary>
    public static readonly StyledProperty<string> ClaimedTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(ClaimedText), "Claimed");

    /// <summary>The Copy button's label.</summary>
    public static readonly StyledProperty<string> CopyTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(CopyText), "Copy code");

    /// <summary>The Copy button's label after a copy, also announced.</summary>
    public static readonly StyledProperty<string> CopiedTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(CopiedText), "Copied");

    /// <summary>The Copy button's label when the clipboard refuses, also announced.</summary>
    public static readonly StyledProperty<string> CopyFailedTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(CopyFailedText), "Copy failed");

    /// <summary>The heading of the odds list.</summary>
    public static readonly StyledProperty<string> OddsTitleProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(OddsTitle), "Odds");

    /// <summary>Announced when a spin starts.</summary>
    public static readonly StyledProperty<string> SpinningTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(SpinningText), "Spinning");

    /// <summary>Announced when the wheel rests; <c>{0}</c> is the prize.</summary>
    public static readonly StyledProperty<string> WonAnnouncementProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(WonAnnouncement), "You won {0}.");

    /// <summary>Announced after <see cref="WonAnnouncement"/> when the prize has a code; <c>{0}</c> is the code, spelled out.</summary>
    public static readonly StyledProperty<string> CodeAnnouncementProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(CodeAnnouncement), "Your code is {0}.");

    /// <summary>
    /// Chooses the winner when a person spins the wheel, such as a result your server already drew; it gets the slices
    /// and returns an index. Without it (or when it returns an index out of range) the wheel draws by weight, which suits
    /// a demo but not a real prize.
    /// </summary>
    public static readonly StyledProperty<Func<IReadOnlyList<PrizeWheelSegment>, int>?> WinnerPickerProperty =
        AvaloniaProperty.Register<PrizeWheel, Func<IReadOnlyList<PrizeWheelSegment>, int>?>(nameof(WinnerPicker));

    /// <summary>The index of the slice the wheel last stopped on; -1 before the first spin ends.</summary>
    public static readonly DirectProperty<PrizeWheel, int> ResultIndexProperty =
        AvaloniaProperty.RegisterDirect<PrizeWheel, int>(nameof(ResultIndex), o => o.ResultIndex);

    /// <summary>The slice the wheel last stopped on; null before the first spin ends.</summary>
    public static readonly DirectProperty<PrizeWheel, PrizeWheelSegment?> ResultProperty =
        AvaloniaProperty.RegisterDirect<PrizeWheel, PrizeWheelSegment?>(nameof(Result), o => o.Result);

    /// <summary>Whether the wheel is turning.</summary>
    public static readonly DirectProperty<PrizeWheel, bool> IsSpinningProperty =
        AvaloniaProperty.RegisterDirect<PrizeWheel, bool>(nameof(IsSpinning), o => o.IsSpinning);

    internal static readonly DirectProperty<PrizeWheel, string> DisplayHintProperty =
        AvaloniaProperty.RegisterDirect<PrizeWheel, string>(nameof(DisplayHint), o => o.DisplayHint);

    internal static readonly DirectProperty<PrizeWheel, bool> IsSpinAgainVisibleProperty =
        AvaloniaProperty.RegisterDirect<PrizeWheel, bool>(nameof(IsSpinAgainVisible), o => o.IsSpinAgainVisible);

    /// <summary>Raised when the wheel comes to rest, with the winning slice.</summary>
    public static readonly RoutedEvent<PrizeWheelResultEventArgs> SpinCompletedEvent =
        RoutedEvent.Register<PrizeWheel, PrizeWheelResultEventArgs>(nameof(SpinCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised each time a peg passes the pointer, for a click sound or a haptic tick.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> TickedEvent =
        RoutedEvent.Register<PrizeWheel, RoutedEventArgs>(nameof(Ticked), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when <see cref="Result"/> changes: to the winner when the wheel rests, and to none when a new spin starts or
    /// the result is cleared.
    /// </summary>
    public static readonly RoutedEvent<PrizeWheelSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<PrizeWheel, PrizeWheelSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after the reward card's Copy button put the won code on the clipboard.</summary>
    public static readonly RoutedEvent<PrizeWheelResultEventArgs> CodeCopiedEvent =
        RoutedEvent.Register<PrizeWheel, PrizeWheelResultEventArgs>(nameof(CodeCopied), RoutingStrategies.Bubble);

    private readonly PrizeWheelModel _model = new();
    private readonly LiveLoop _loop;
    private readonly double[] _sampleTimes = new double[24];
    private readonly double[] _sampleAngles = new double[24];
    private LazyFocusRing? _focusRing;
    private PrizeWheelFace? _face;
    private TextBlock? _status;
    private PrizeWheelCard? _card;
    private PrizeWheelCard? _sizer;
    private PrizeWheelOddsList? _odds;
    private readonly ScaleTransform _cardScale = new();
    private readonly TranslateTransform _cardShift = new();
    private TransformGroup? _cardTransform;
    private Phase _phase;
    private bool _statusFlip;
    private bool _dirty = true;
    private int _resultIndex = -1;
    private PrizeWheelSegment? _result;
    private bool _isSpinning;
    private bool _isClaimed;
    private bool _weak;
    private string _displayHint = string.Empty;
    private bool _isSpinAgainVisible = true;
    private double _highlight;
    private double _highlightTime;
    private double _charge;
    private bool _chargeByPointer;
    private Key _chargeKey;
    private double _pressRotation;
    private double _hubScale = 1;
    private double _hubScaleVelocity;
    private double _hubLabelOpacity = 1;
    private double _dragAngle;
    private int _sampleCount;
    private int _sampleHead;
    private double _flightTime = -1;
    private Vector _flightOffset;
    private double _exitTime = -1;
    private bool _ownsName;

    private enum Phase
    {
        Idle,
        Charging,
        Dragging,
        Spinning,
        Won
    }

    static PrizeWheel()
    {
        FocusableProperty.OverrideDefaultValue<PrizeWheel>(true);
        ClipToBoundsProperty.OverrideDefaultValue<PrizeWheel>(false);
    }

    public PrizeWheel()
    {
        // The spin, the drift, the pointer's spring and the card's flight are one continuous simulation driven by the
        // wheel's own angle and velocity, which a theme transition cannot express; the loop stops when all of it rests.
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _displayHint = HintText;
        Segments.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (PrizeWheelSegment segment in e.OldItems)
                {
                    segment.Changed -= OnSegmentChanged;
                }
            }
            if (e.NewItems is not null)
            {
                foreach (PrizeWheelSegment segment in e.NewItems)
                {
                    segment.Changed += OnSegmentChanged;
                }
            }
            SegmentsChanged();
        };
    }

    /// <summary>The slices, clockwise from the top: two to twelve, six to ten read best.</summary>
    [Content]
    public AvaloniaList<PrizeWheelSegment> Segments { get; } = [];

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <inheritdoc cref="TurnsProperty"/>
    public int Turns
    {
        get => GetValue(TurnsProperty);
        set => SetValue(TurnsProperty, value);
    }

    /// <inheritdoc cref="SizesByWeightProperty"/>
    public bool SizesByWeight
    {
        get => GetValue(SizesByWeightProperty);
        set => SetValue(SizesByWeightProperty, value);
    }

    /// <inheritdoc cref="SpinTextProperty"/>
    public string SpinText
    {
        get => GetValue(SpinTextProperty);
        set => SetValue(SpinTextProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IsInteractiveProperty"/>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <inheritdoc cref="IsPanelVisibleProperty"/>
    public bool IsPanelVisible
    {
        get => GetValue(IsPanelVisibleProperty);
        set => SetValue(IsPanelVisibleProperty, value);
    }

    /// <inheritdoc cref="IsOddsVisibleProperty"/>
    public bool IsOddsVisible
    {
        get => GetValue(IsOddsVisibleProperty);
        set => SetValue(IsOddsVisibleProperty, value);
    }

    /// <inheritdoc cref="IsSingleSpinProperty"/>
    public bool IsSingleSpin
    {
        get => GetValue(IsSingleSpinProperty);
        set => SetValue(IsSingleSpinProperty, value);
    }

    /// <inheritdoc cref="DriftSpeedProperty"/>
    public double DriftSpeed
    {
        get => GetValue(DriftSpeedProperty);
        set => SetValue(DriftSpeedProperty, value);
    }

    /// <inheritdoc cref="IsDriftPausedProperty"/>
    public bool IsDriftPaused
    {
        get => GetValue(IsDriftPausedProperty);
        set => SetValue(IsDriftPausedProperty, value);
    }

    /// <inheritdoc cref="AccentBrushProperty"/>
    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="HintTextProperty"/>
    public string HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    /// <inheritdoc cref="WeakHintTextProperty"/>
    public string WeakHintText
    {
        get => GetValue(WeakHintTextProperty);
        set => SetValue(WeakHintTextProperty, value);
    }

    /// <inheritdoc cref="SpinAgainTextProperty"/>
    public string SpinAgainText
    {
        get => GetValue(SpinAgainTextProperty);
        set => SetValue(SpinAgainTextProperty, value);
    }

    /// <inheritdoc cref="ClaimedTextProperty"/>
    public string ClaimedText
    {
        get => GetValue(ClaimedTextProperty);
        set => SetValue(ClaimedTextProperty, value);
    }

    /// <inheritdoc cref="CopyTextProperty"/>
    public string CopyText
    {
        get => GetValue(CopyTextProperty);
        set => SetValue(CopyTextProperty, value);
    }

    /// <inheritdoc cref="CopiedTextProperty"/>
    public string CopiedText
    {
        get => GetValue(CopiedTextProperty);
        set => SetValue(CopiedTextProperty, value);
    }

    /// <inheritdoc cref="CopyFailedTextProperty"/>
    public string CopyFailedText
    {
        get => GetValue(CopyFailedTextProperty);
        set => SetValue(CopyFailedTextProperty, value);
    }

    /// <inheritdoc cref="OddsTitleProperty"/>
    public string OddsTitle
    {
        get => GetValue(OddsTitleProperty);
        set => SetValue(OddsTitleProperty, value);
    }

    /// <inheritdoc cref="SpinningTextProperty"/>
    public string SpinningText
    {
        get => GetValue(SpinningTextProperty);
        set => SetValue(SpinningTextProperty, value);
    }

    /// <inheritdoc cref="WonAnnouncementProperty"/>
    public string WonAnnouncement
    {
        get => GetValue(WonAnnouncementProperty);
        set => SetValue(WonAnnouncementProperty, value);
    }

    /// <inheritdoc cref="CodeAnnouncementProperty"/>
    public string CodeAnnouncement
    {
        get => GetValue(CodeAnnouncementProperty);
        set => SetValue(CodeAnnouncementProperty, value);
    }

    /// <inheritdoc cref="WinnerPickerProperty"/>
    public Func<IReadOnlyList<PrizeWheelSegment>, int>? WinnerPicker
    {
        get => GetValue(WinnerPickerProperty);
        set => SetValue(WinnerPickerProperty, value);
    }

    /// <inheritdoc cref="ResultIndexProperty"/>
    public int ResultIndex
    {
        get => _resultIndex;
        private set => SetAndRaise(ResultIndexProperty, ref _resultIndex, value);
    }

    /// <inheritdoc cref="ResultProperty"/>
    public PrizeWheelSegment? Result
    {
        get => _result;
        private set => SetAndRaise(ResultProperty, ref _result, value);
    }

    /// <inheritdoc cref="IsSpinningProperty"/>
    public bool IsSpinning
    {
        get => _isSpinning;
        private set => SetAndRaise(IsSpinningProperty, ref _isSpinning, value);
    }

    /// <summary>The hint the panel shows now: the gesture hint, or the weak-flick hint after a gentle flick.</summary>
    internal string DisplayHint
    {
        get => _displayHint;
        private set => SetAndRaise(DisplayHintProperty, ref _displayHint, value);
    }

    /// <summary>Whether the reward card offers "Spin again".</summary>
    internal bool IsSpinAgainVisible
    {
        get => _isSpinAgainVisible;
        private set => SetAndRaise(IsSpinAgainVisibleProperty, ref _isSpinAgainVisible, value);
    }

    /// <inheritdoc cref="SpinCompletedEvent"/>
    public event EventHandler<PrizeWheelResultEventArgs>? SpinCompleted
    {
        add => AddHandler(SpinCompletedEvent, value);
        remove => RemoveHandler(SpinCompletedEvent, value);
    }

    /// <inheritdoc cref="TickedEvent"/>
    public event EventHandler<RoutedEventArgs>? Ticked
    {
        add => AddHandler(TickedEvent, value);
        remove => RemoveHandler(TickedEvent, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<PrizeWheelSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="CodeCopiedEvent"/>
    public event EventHandler<PrizeWheelResultEventArgs>? CodeCopied
    {
        add => AddHandler(CodeCopiedEvent, value);
        remove => RemoveHandler(CodeCopiedEvent, value);
    }

    /// <summary>The source of the draws, 0 (inclusive) to 1 (exclusive); <see cref="Random.Shared"/> by default.</summary>
    internal Func<double> Random { get; set; } = static () => System.Random.Shared.NextDouble();

    internal PrizeWheelModel Model
    {
        get
        {
            EnsureModel();
            return _model;
        }
    }

    /// <summary>How lit the winning slice is, 0 to 1.</summary>
    internal double Highlight => _highlight;

    /// <summary>How far the hub is charged, 0 to 1.</summary>
    internal double Charge => _phase == Phase.Charging ? _charge : 0;

    /// <summary>The hub's press scale.</summary>
    internal double HubScale => _hubScale;

    /// <summary>The hub label's opacity (dimmed while spinning).</summary>
    internal double HubLabelOpacity => _hubLabelOpacity;

    /// <summary>The hub's word now.</summary>
    internal string HubText => _isClaimed ? ClaimedText : SpinText;

    /// <summary>Whether a single-spin wheel has given its prize.</summary>
    internal bool IsClaimed => _isClaimed;

    /// <summary>The wheel's accessible description: every prize with its chance.</summary>
    internal string OddsDescription
    {
        get
        {
            EnsureModel();
            var text = new StringBuilder(Label);
            text.Append('.');
            for (var i = 0; i < Segments.Count && i < _model.Count; i++)
            {
                text.Append(i == 0 ? " " : "; ").Append(Segments[i].Label).Append(' ').Append(Percent(_model.Chance(i)));
            }
            return text.Append('.').ToString();
        }
    }

    /// <summary>
    /// Draws a winner by the slices' weights and spins the wheel to it over <see cref="Duration"/>, at least
    /// <see cref="Turns"/> turns. Returns the winner's index, or -1 when the wheel is already turning, has given its single
    /// prize, or has no slice that can win.
    /// </summary>
    public int Spin()
    {
        EnsureModel();
        if (!CanStart || _model.Count == 0)
        {
            return -1;
        }
        var index = _model.Pick(Random());
        if (index < 0)
        {
            return -1;
        }
        Begin(index, spot => _model.Plan(index, spot, Math.Max(1, Turns), Math.Max(0.3, Duration.TotalSeconds)));
        return index;
    }

    /// <summary>
    /// Spins the wheel to a winner chosen elsewhere, such as by your server, over <see cref="Duration"/>. Ignored while the
    /// wheel turns or once a single-spin wheel has given its prize.
    /// </summary>
    public void SpinTo(int index)
    {
        EnsureModel();
        if (!CanStart || index < 0 || index >= _model.Count)
        {
            return;
        }
        Begin(index, spot => _model.Plan(index, spot, Math.Max(1, Turns), Math.Max(0.3, Duration.TotalSeconds)));
    }

    /// <summary>
    /// Clears the result: the highlight and the reward card go, the idle text returns and a single-spin wheel unlocks.
    /// The wheel stays where it stopped.
    /// </summary>
    public void ClearResult()
    {
        Dismiss(animate: false);
        if (_isClaimed)
        {
            _isClaimed = false;
            PseudoClasses.Set(ClaimedPseudoClass, false);
        }
        if (_phase == Phase.Won)
        {
            _phase = Phase.Idle;
        }
        UpdateSpinAgain();
        _loop.Start();
        _face?.InvalidateVisual();
    }

    private bool CanStart => (_phase is Phase.Idle or Phase.Won) && !_isClaimed;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_face is not null)
        {
            _face.Owner = null;
        }
        if (_card is not null)
        {
            _card.SpinAgainRequested -= OnSpinAgain;
            _card.CopyStateChanged -= OnCopyState;
        }
        _face = e.NameScope.Find<PrizeWheelFace>(FacePart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        _card = e.NameScope.Find<PrizeWheelCard>(CardPart);
        _sizer = e.NameScope.Find<PrizeWheelCard>(SizerPart);
        _odds = e.NameScope.Find<PrizeWheelOddsList>(OddsPart);
        if (_face is not null)
        {
            _face.Owner = this;
            _face.Reset();
            AutomationProperties.SetName(_face, OddsDescription);
        }
        if (_card is not null)
        {
            _card.SpinAgainRequested += OnSpinAgain;
            _card.CopyStateChanged += OnCopyState;
            _cardTransform ??= new TransformGroup { Children = { _cardScale, _cardShift } };
            _card.IsVisible = _resultIndex >= 0;
            if (_resultIndex >= 0 && Result is { } result)
            {
                Fill(_card, result);
            }
        }
        _focusRing ??= new LazyFocusRing(this, PrizeWheelTokens.Key(PrizeWheelTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>(HubFocusPart));
        RebuildPanel();
        UpdateSpinAgain();
        _loop.Start();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SizesByWeightProperty)
        {
            SegmentsChanged();
        }
        else if (property == SpinTextProperty || property == ClaimedTextProperty)
        {
            _face?.InvalidateVisual();
        }
        else if (property == LabelProperty)
        {
            UpdateName();
            if (_face is not null)
            {
                AutomationProperties.SetName(_face, OddsDescription);
            }
        }
        else if (property == IsInteractiveProperty)
        {
            Focusable = IsInteractive;
            UpdateSpinAgain();
        }
        else if (property == IsSingleSpinProperty)
        {
            UpdateSpinAgain();
        }
        else if (property == HintTextProperty || property == WeakHintTextProperty)
        {
            DisplayHint = _weak ? WeakHintText : HintText;
        }
        else if (property == DriftSpeedProperty || property == IsDriftPausedProperty)
        {
            _loop.Start();
        }
        else if (property == ArcMotion.LevelProperty)
        {
            if (_phase == Phase.Spinning && !ArcMotion.IsFull(this))
            {
                // Motion was reduced mid-spin: land at once.
                _model.Jump(_model.Target, Random());
                Finish();
            }
            _loop.Start();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
        if (_phase == Phase.Charging && !_chargeByPointer)
        {
            Release();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !ReferenceEquals(e.Source, this) || !IsInteractive || e.Key is not (Key.Enter or Key.Space))
        {
            return;
        }
        e.Handled = true;
        if (_phase == Phase.Charging || !CanStart)
        {
            // A held key repeats; the charge keeps growing.
            return;
        }
        _chargeKey = e.Key;
        Press(byPointer: false);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_phase == Phase.Charging && !_chargeByPointer && e.Key == _chargeKey)
        {
            e.Handled = true;
            Release();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsInteractive || _face is not { } face || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (e.Source is Visual source && source.FindAncestorOfType<AvaloniaButton>(includeSelf: true) is not null)
        {
            return;
        }
        var point = e.GetPosition(face);
        if (!face.IsOnWheel(point))
        {
            return;
        }
        e.Handled = true;
        if (!CanStart)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(this);
        if (face.IsOnHub(point))
        {
            Press(byPointer: true);
            return;
        }
        Dismiss(animate: true);
        _model.Hold();
        _phase = Phase.Dragging;
        _dragAngle = face.AngleOf(point);
        _sampleCount = 0;
        _sampleHead = 0;
        Sample(e.Timestamp);
        _loop.Start();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_phase != Phase.Dragging || _face is not { } face)
        {
            return;
        }
        var angle = face.AngleOf(e.GetPosition(face));
        var delta = angle - _dragAngle;
        delta -= Math.Round(delta / 360) * 360;
        _dragAngle = angle;
        _model.TurnTo(_model.Rotation + delta);
        Sample(e.Timestamp);
        _loop.Start();
        face.InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_phase == Phase.Charging && _chargeByPointer)
        {
            e.Handled = true;
            Release();
        }
        else if (_phase == Phase.Dragging)
        {
            e.Handled = true;
            Sample(e.Timestamp);
            Throw(e.Timestamp);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_phase == Phase.Charging && _chargeByPointer)
        {
            Release();
        }
        else if (_phase == Phase.Dragging)
        {
            Throw(_sampleCount > 0 ? _sampleTimes[(_sampleHead + _sampleTimes.Length - 1) % _sampleTimes.Length] : 0);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PrizeWheelAutomationPeer(this);

    /// <summary>Spins at medium strength, as a click without a press would (assistive activation, "Spin again").</summary>
    internal void SpinMedium() => Gesture(PrizeWheelModel.MediumThrow, ramp: true);

    private void Press(bool byPointer)
    {
        Dismiss(animate: true);
        _model.Hold();
        _phase = Phase.Charging;
        _chargeByPointer = byPointer;
        _charge = 0;
        _pressRotation = _model.Rotation;
        PseudoClasses.Set(ChargingPseudoClass, true);
        if (byPointer && !ArcMotion.IsFull(this))
        {
            _hubScale = HubPressScale;
            _hubScaleVelocity = 0;
        }
        _loop.Start();
        _face?.InvalidateVisual();
    }

    private void Release()
    {
        var charge = _charge;
        _phase = Phase.Idle;
        PseudoClasses.Set(ChargingPseudoClass, false);
        if (!ArcMotion.IsFull(this))
        {
            _hubScale = 1;
        }
        _loop.Start();
        Gesture(518 + 1108 * charge, ramp: true);
    }

    private void Throw(double timestamp)
    {
        _phase = Phase.Idle;
        var velocity = ReleaseVelocity(timestamp);
        if (Math.Abs(velocity) >= PrizeWheelModel.WeakThrow)
        {
            Gesture(Math.Clamp(velocity, -PrizeWheelModel.MaxThrow, PrizeWheelModel.MaxThrow), ramp: false);
            return;
        }
        // Too gentle to spin: the wheel coasts back into its drift and the hint asks for a quicker flick.
        if (ArcMotion.IsFull(this))
        {
            _model.Coast(velocity);
        }
        _weak = true;
        DisplayHint = WeakHintText;
        _loop.Start();
    }

    /// <summary>The wheel's angular velocity over the last 100 ms of the drag, degrees per second.</summary>
    private double ReleaseVelocity(double now)
    {
        if (_sampleCount < 2)
        {
            return 0;
        }
        var size = _sampleTimes.Length;
        var newest = (_sampleHead + size - 1) % size;
        var oldest = newest;
        for (var i = 1; i < _sampleCount; i++)
        {
            var index = (newest - i + size) % size;
            if (now - _sampleTimes[index] > 100)
            {
                break;
            }
            oldest = index;
        }
        var dt = _sampleTimes[newest] - _sampleTimes[oldest];
        if (dt < 8 || now - _sampleTimes[newest] > 60)
        {
            return 0;
        }
        return (_sampleAngles[newest] - _sampleAngles[oldest]) / dt * 1000;
    }

    private void Sample(ulong timestamp) => Sample((double)timestamp);

    private void Sample(double timestamp)
    {
        _sampleTimes[_sampleHead] = timestamp;
        _sampleAngles[_sampleHead] = _model.Rotation;
        _sampleHead = (_sampleHead + 1) % _sampleTimes.Length;
        _sampleCount = Math.Min(_sampleCount + 1, _sampleTimes.Length);
    }

    private void Gesture(double velocity, bool ramp)
    {
        EnsureModel();
        if (_model.Count == 0 || _isClaimed)
        {
            return;
        }
        var index = WinnerPicker?.Invoke(Segments) ?? -1;
        if (index < 0 || index >= _model.Count)
        {
            index = _model.Pick(Random());
        }
        if (index < 0)
        {
            return;
        }
        Begin(index, spot => _model.Throw(index, spot, velocity, ramp));
    }

    private void Begin(int index, Action<double> plan)
    {
        Dismiss(animate: true);
        if (_weak)
        {
            _weak = false;
            DisplayHint = HintText;
        }
        if (_phase == Phase.Charging)
        {
            PseudoClasses.Set(ChargingPseudoClass, false);
        }
        var spot = Random();
        if (ArcMotion.IsFull(this) && VisualRoot is not null)
        {
            plan(spot);
            _phase = Phase.Spinning;
            IsSpinning = true;
            PseudoClasses.Set(SpinningPseudoClass, true);
            Announce(SpinningText);
            _loop.Start();
            _face?.InvalidateVisual();
            return;
        }
        _model.Jump(index, spot);
        Finish();
    }

    private bool Step(double dt)
    {
        EnsureModel();
        var full = ArcMotion.IsFull(this);
        _model.IsPointerLive = full;
        _model.Drift = full && _phase is Phase.Idle && _resultIndex < 0 && !_isClaimed && !IsDriftPaused
            ? PrizeWheelModel.DriftRate * Math.Max(0, DriftSpeed)
            : 0;
        if (_phase == Phase.Charging)
        {
            _charge = Math.Min(1, _charge + dt / PrizeWheelModel.ChargeSeconds);
            if (full)
            {
                var wind = _charge * _charge * (3 - 2 * _charge);
                _model.TurnTo(_pressRotation - PrizeWheelModel.WindBack * wind);
            }
        }
        var wasSpinning = _model.IsSpinning;
        var ticks = _model.Step(dt);
        for (var i = 0; i < ticks; i++)
        {
            RaiseEvent(new RoutedEventArgs(TickedEvent, this));
        }
        if (_phase == Phase.Spinning && wasSpinning && !_model.IsSpinning)
        {
            Finish();
        }
        if (_resultIndex >= 0 && _highlight < 1)
        {
            _highlightTime += dt;
            _highlight = ArcEasings.Standard.Ease(Math.Min(1, _highlightTime / HighlightSeconds));
        }
        var settling = StepHub(dt, full);
        settling |= StepCard(dt);
        _face?.InvalidateVisual();
        return _model.Drift != 0
               || _phase is Phase.Charging or Phase.Dragging or Phase.Spinning
               || _model.IsCoasting
               || _model.IsFlapMoving
               || (_resultIndex >= 0 && _highlight < 1)
               || settling;
    }

    /// <summary>The hub's press scale (a critically damped spring) and its label's dimming while the wheel spins.</summary>
    private bool StepHub(double dt, bool full)
    {
        var target = _phase == Phase.Charging && _chargeByPointer ? HubPressScale : 1;
        var moving = false;
        if (full)
        {
            var steps = Math.Max(1, (int)Math.Ceiling(dt * 240));
            var h = dt / steps;
            for (var i = 0; i < steps; i++)
            {
                var accel = HubSpring * HubSpring * (target - _hubScale) - 2 * HubSpring * _hubScaleVelocity;
                _hubScaleVelocity += accel * h;
                _hubScale += _hubScaleVelocity * h;
            }
            moving = Math.Abs(target - _hubScale) > 0.0005 || Math.Abs(_hubScaleVelocity) > 0.005;
            if (!moving)
            {
                _hubScale = target;
                _hubScaleVelocity = 0;
            }
        }
        else
        {
            _hubScale = target;
            _hubScaleVelocity = 0;
        }
        var opacity = _phase == Phase.Spinning ? 0.5 : 1;
        if (Math.Abs(_hubLabelOpacity - opacity) > 0.002)
        {
            _hubLabelOpacity += (opacity - _hubLabelOpacity) * (1 - Math.Exp(-dt / 0.06));
            moving = true;
        }
        else
        {
            _hubLabelOpacity = opacity;
        }
        return moving;
    }

    /// <summary>The reward card's flight out of the won slice (one spring) and its fade when it leaves.</summary>
    private bool StepCard(double dt)
    {
        if (_card is not { } card)
        {
            return false;
        }
        if (_flightTime >= 0)
        {
            _flightTime += dt;
            var t = _flightTime - FlightDelay;
            if (t <= 0)
            {
                ApplyFlight(card, 0);
                return true;
            }
            var (displacement, velocity) = SpringSolver.Evaluate(s_flight, 1, 0, t);
            if (t > 0.3 && Math.Abs(displacement) < 0.0008 && Math.Abs(velocity) < 0.01)
            {
                _flightTime = -1;
                card.RenderTransform = null;
                card.Opacity = 1;
                return false;
            }
            ApplyFlight(card, 1 - displacement);
            return true;
        }
        if (_exitTime >= 0)
        {
            _exitTime += dt;
            var u = Math.Min(1, _exitTime / CardExitSeconds);
            card.Opacity = 1 - ArcEasings.Standard.Ease(u);
            if (u >= 1)
            {
                _exitTime = -1;
                card.IsVisible = false;
                card.Opacity = 1;
                return false;
            }
            return true;
        }
        return false;
    }

    private void ApplyFlight(PrizeWheelCard card, double progress)
    {
        var scale = FlightStartScale + (1 - FlightStartScale) * progress;
        _cardScale.ScaleX = scale;
        _cardScale.ScaleY = scale;
        _cardShift.X = _flightOffset.X * (1 - progress);
        _cardShift.Y = _flightOffset.Y * (1 - progress);
        card.Opacity = Math.Clamp(progress, 0, 1);
    }

    private void Finish()
    {
        EnsureModel();
        var index = _model.Target;
        _phase = Phase.Won;
        IsSpinning = false;
        PseudoClasses.Set(SpinningPseudoClass, false);
        PseudoClasses.Set(ChargingPseudoClass, false);
        if (index < 0 || index >= Segments.Count)
        {
            _phase = Phase.Idle;
            return;
        }
        var segment = Segments[index];
        SetResult(index, segment);
        PseudoClasses.Set(WonPseudoClass, true);
        if (IsSingleSpin)
        {
            _isClaimed = true;
            PseudoClasses.Set(ClaimedPseudoClass, true);
        }
        UpdateSpinAgain();
        var level = ArcMotion.GetEffectiveLevel(this);
        _highlightTime = 0;
        _highlight = level == ArcMotionLevel.Off || VisualRoot is null ? 1 : 0;
        _face?.UpdateWinnerLabel();
        UpdateOddsRows();
        ShowCard(segment, fly: level == ArcMotionLevel.Full && VisualRoot is not null);
        _loop.Start();
        _face?.InvalidateVisual();
        var announcement = Format(WonAnnouncement, segment.Label ?? string.Empty);
        if (!string.IsNullOrEmpty(segment.Code))
        {
            announcement += " " + Format(CodeAnnouncement, Spell(segment.Code));
        }
        Announce(announcement);
        RaiseEvent(new PrizeWheelResultEventArgs(SpinCompletedEvent, index, segment));
    }

    private void ShowCard(PrizeWheelSegment segment, bool fly)
    {
        if (_card is not { } card || !IsPanelVisible)
        {
            return;
        }
        Fill(card, segment);
        _exitTime = -1;
        card.IsVisible = true;
        if (!fly || _face is not { } face || card.GetVisualParent() is not Visual parent)
        {
            _flightTime = -1;
            card.RenderTransform = null;
            card.Opacity = 1;
            return;
        }
        UpdateLayout();
        var start = face.TranslatePoint(face.FlightStart, parent);
        var center = card.Bounds.Center;
        _flightOffset = start is { } point ? point - center : default;
        _flightTime = 0;
        card.RenderTransform = _cardTransform;
        ApplyFlight(card, 0);
    }

    private static void Fill(PrizeWheelCard card, PrizeWheelSegment segment)
    {
        card.Prize = segment.Label;
        card.Detail = segment.Detail;
        card.Code = segment.Code;
    }

    /// <summary>Clears the shown result before a new spin or on request; the card fades out (or goes at once).</summary>
    private void Dismiss(bool animate)
    {
        if (_resultIndex < 0)
        {
            return;
        }
        SetResult(-1, null);
        PseudoClasses.Set(WonPseudoClass, false);
        _highlight = 0;
        _highlightTime = 0;
        _face?.UpdateWinnerLabel();
        UpdateOddsRows();
        if (_phase == Phase.Won)
        {
            _phase = Phase.Idle;
        }
        if (_card is { IsVisible: true } card)
        {
            _flightTime = -1;
            card.RenderTransform = null;
            if (animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && VisualRoot is not null)
            {
                _exitTime = 0;
                _loop.Start();
            }
            else
            {
                _exitTime = -1;
                card.IsVisible = false;
                card.Opacity = 1;
            }
        }
    }

    private void SetResult(int index, PrizeWheelSegment? segment)
    {
        var oldIndex = _resultIndex;
        var oldSegment = _result;
        if (oldIndex == index && ReferenceEquals(oldSegment, segment))
        {
            return;
        }
        ResultIndex = index;
        Result = segment;
        RaiseEvent(new PrizeWheelSelectionChangedEventArgs(SelectionChangedEvent, oldIndex, index, oldSegment, segment));
    }

    private void OnSpinAgain(object? sender, EventArgs e)
    {
        if (!IsInteractive || _isClaimed || _phase != Phase.Won)
        {
            return;
        }
        // The button leaves with the card; keep focus on the wheel.
        Focus(NavigationMethod.Tab);
        SpinMedium();
    }

    private void OnCopyState(object? sender, CopyState state)
    {
        if (state == CopyState.Copied)
        {
            Announce(CopiedText);
            if (_resultIndex >= 0 && _result is { } result)
            {
                RaiseEvent(new PrizeWheelResultEventArgs(CodeCopiedEvent, _resultIndex, result));
            }
        }
        else if (state == CopyState.Error)
        {
            Announce(CopyFailedText);
        }
    }

    private void UpdateSpinAgain() => IsSpinAgainVisible = IsInteractive && !IsSingleSpin;

    private void Announce(string message)
    {
        if (_status is null)
        {
            return;
        }
        // Alternate a trailing space so the same message twice in a row is announced again.
        _statusFlip = !_statusFlip;
        _status.Text = message.Length == 0 ? string.Empty : _statusFlip ? message : message + " ";
    }

    private void OnSegmentChanged(object? sender, EventArgs e) => SegmentsChanged();

    private void SegmentsChanged()
    {
        _dirty = true;
        _face?.Reset();
        RebuildPanel();
        if (_face is not null)
        {
            AutomationProperties.SetName(_face, OddsDescription);
        }
    }

    /// <summary>Rebuilds the odds rows and fills the sizer card with the longest texts, so the slot fits every card.</summary>
    private void RebuildPanel()
    {
        EnsureModel();
        if (_odds is { } odds)
        {
            odds.Clear();
            for (var i = 0; i < Segments.Count && i < _model.Count; i++)
            {
                odds.Add(new PrizeWheelOddsRow
                {
                    Label = Segments[i].Label,
                    Chance = Percent(_model.Chance(i)),
                    IsFeatured = Segments[i].IsFeatured,
                    IsWon = i == _resultIndex
                });
            }
        }
        if (_sizer is { } sizer)
        {
            string? Longest(Func<PrizeWheelSegment, string?> pick)
            {
                string? longest = null;
                foreach (var segment in Segments)
                {
                    if (pick(segment) is { } text && text.Length > (longest?.Length ?? -1))
                    {
                        longest = text;
                    }
                }
                return longest;
            }
            sizer.Prize = Longest(s => s.Label);
            sizer.Detail = Longest(s => s.Detail);
            sizer.Code = Longest(s => s.Code);
        }
    }

    private void UpdateOddsRows()
    {
        if (_odds is null)
        {
            return;
        }
        for (var i = 0; i < _odds.Rows.Count; i++)
        {
            _odds.Rows[i].IsWon = i == _resultIndex;
        }
    }

    private void EnsureModel()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        var weights = new double[Segments.Count];
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] = Segments[i].Weight;
        }
        _model.Load(weights, SizesByWeight);
        if (_resultIndex >= Segments.Count)
        {
            ClearResult();
        }
    }

    private void UpdateName()
    {
        if (AutomationProperties.GetName(this) is not { Length: > 0 } || _ownsName)
        {
            _ownsName = true;
            AutomationProperties.SetName(this, Label);
        }
    }

    private static string Percent(double chance)
    {
        var percent = chance * 100;
        if (percent <= 0)
        {
            return "0%";
        }
        if (percent < 1)
        {
            return "<1%";
        }
        return Math.Round(percent).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Spells a code character by character so a screen reader reads it letter by letter.</summary>
    private static string Spell(string code)
    {
        var text = new StringBuilder(code.Length * 2);
        foreach (var c in code)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }
            if (text.Length > 0)
            {
                text.Append(' ');
            }
            text.Append(c);
        }
        return text.ToString();
    }

    private static string Format(string format, string value)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, value);
        }
        catch (FormatException)
        {
            return format + " " + value;
        }
    }

    private sealed class PrizeWheelAutomationPeer(PrizeWheel owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string GetLocalizedControlTypeCore() => "prize wheel";

        protected override string? GetHelpTextCore() =>
            base.GetHelpTextCore() is { Length: > 0 } help ? help : owner.Result?.Label;

        public void Invoke()
        {
            if (owner.IsEffectivelyEnabled && owner.IsInteractive)
            {
                owner.SpinMedium();
            }
        }
    }
}

/// <summary>The slice a <see cref="PrizeWheel"/> stopped on.</summary>
public sealed class PrizeWheelResultEventArgs : RoutedEventArgs
{
    public PrizeWheelResultEventArgs(RoutedEvent routedEvent, int index, PrizeWheelSegment segment) : base(routedEvent)
    {
        Index = index;
        Segment = segment;
    }

    /// <summary>The slice's position in <see cref="PrizeWheel.Segments"/>.</summary>
    public int Index { get; }

    /// <summary>The winning slice.</summary>
    public PrizeWheelSegment Segment { get; }
}

/// <summary>The change of a <see cref="PrizeWheel"/>'s result: the slice before and after.</summary>
public sealed class PrizeWheelSelectionChangedEventArgs : RoutedEventArgs
{
    public PrizeWheelSelectionChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex, PrizeWheelSegment? oldItem,
        PrizeWheelSegment? newItem) : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        OldItem = oldItem;
        NewItem = newItem;
    }

    /// <summary>The previous result's index, or -1 when there was none.</summary>
    public int OldIndex { get; }

    /// <summary>The new result's index, or -1 when the result was cleared.</summary>
    public int NewIndex { get; }

    /// <summary>The previous winning slice, or null.</summary>
    public PrizeWheelSegment? OldItem { get; }

    /// <summary>The new winning slice, or null when the result was cleared.</summary>
    public PrizeWheelSegment? NewItem { get; }
}
