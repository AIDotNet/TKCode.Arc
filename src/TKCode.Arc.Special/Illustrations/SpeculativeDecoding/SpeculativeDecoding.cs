using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// An animated explainer of speculative decoding in one card. A small draft model proposes a few ghost tokens quickly;
/// the large target model checks them in one sweep. Accepted guesses get a check and fly into the answer, the first miss
/// is struck through and replaced by the target's own token, and the guesses after it fade. When every guess is kept the
/// target adds a bonus token. Rounds repeat until the sentence is written; a switch races a line where the target writes
/// one token per pass on the same clock, and the speedup, the share of drafts accepted and the tokens per pass roll up
/// below. Every token of the answer is underlined in the color of the model that wrote it. With <see cref="Tokens"/> the
/// run is simulated from each token's ease and the <see cref="Seed"/>; with <see cref="Rounds"/> a recorded run is
/// replayed. The numbers are illustrative. The run plays once when the card comes into view (never on its own under
/// reduced motion, where tokens appear in place and the sweep steps a cell at a time).
/// </summary>
[PseudoClasses(NarrowPseudoClass, ComparingPseudoClass, LoadingPseudoClass, ErrorPseudoClass, ReplayPseudoClass, RetryPseudoClass)]
public partial class SpeculativeDecoding : Explainer
{
    /// <summary>Set while the card is narrower than <see cref="NarrowWidth"/>.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the target-only line is shown.</summary>
    public const string ComparingPseudoClass = ":comparing";

    /// <summary>Set while <see cref="Status"/> is Loading.</summary>
    public const string LoadingPseudoClass = ":loading";

    /// <summary>Set while <see cref="Status"/> is Error.</summary>
    public const string ErrorPseudoClass = ":error";

    /// <summary>Set while a recorded run (<see cref="Rounds"/>) is replayed; the draft length slider is hidden.</summary>
    public const string ReplayPseudoClass = ":replay";

    /// <summary>Set while the error state offers a retry button (someone handles <see cref="RetryRequested"/> or set <see cref="RetryCommand"/>).</summary>
    public const string RetryPseudoClass = ":retry";

    /// <summary>Below this card width (px) the lane labels become a legend and the line labels sit above their text.</summary>
    public const double NarrowWidth = 560;

    /// <summary>How likely the draft guesses a token that has no <see cref="DecodeToken.Ease"/>.</summary>
    public const double DefaultEase = 0.9;

    /// <summary>The shortest draft length.</summary>
    public const int MinDraftLength = 2;

    /// <summary>The longest draft length.</summary>
    public const int MaxDraftLength = 8;

    internal const string StagePart = "PART_Stage";
    internal const string StatusPart = "PART_Status";
    internal const string DraftLengthPart = "PART_DraftLength";
    internal const string ComparePart = "PART_Compare";
    internal const string RetryPart = "PART_Retry";
    internal const string SpeedupPart = "PART_Speedup";
    internal const string AcceptancePart = "PART_Acceptance";
    internal const string TokensPerPassPart = "PART_TokensPerPass";
    internal const string RoundSpeechPart = "PART_RoundSpeech";
    internal const string OutputSpeechPart = "PART_OutputSpeech";
    internal const string CompareSpeechPart = "PART_CompareSpeech";

    /// <summary>Defines the <see cref="Tokens"/> property.</summary>
    public static readonly StyledProperty<AvaloniaList<DecodeToken>> TokensProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, AvaloniaList<DecodeToken>>(nameof(Tokens));

    /// <summary>Defines the <see cref="TokensSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<DecodeToken>?> TokensSourceProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, IEnumerable<DecodeToken>?>(nameof(TokensSource));

    /// <summary>Defines the <see cref="Rounds"/> property.</summary>
    public static readonly StyledProperty<AvaloniaList<DecodeRound>> RoundsProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, AvaloniaList<DecodeRound>>(nameof(Rounds));

    /// <summary>Defines the <see cref="RoundsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<DecodeRound>?> RoundsSourceProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, IEnumerable<DecodeRound>?>(nameof(RoundsSource));

    /// <summary>Defines the <see cref="DraftLength"/> property.</summary>
    public static readonly StyledProperty<int> DraftLengthProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, int>(nameof(DraftLength), 4, coerce: CoerceDraftLength, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsComparing"/> property.</summary>
    public static readonly StyledProperty<bool> IsComparingProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, bool>(nameof(IsComparing), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="Seed"/> property.</summary>
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, int>(nameof(Seed), SpeculativeDecodingSamples.Seed);

    /// <summary>Defines the <see cref="DraftCost"/> property.</summary>
    public static readonly StyledProperty<double> DraftCostProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, double>(nameof(DraftCost), 0.1, coerce: CoerceDraftCost);

    /// <summary>Defines the <see cref="PassDuration"/> property.</summary>
    public static readonly StyledProperty<TimeSpan> PassDurationProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, TimeSpan>(nameof(PassDuration), TimeSpan.FromMilliseconds(640), coerce: CoercePassDuration);

    /// <summary>Defines the <see cref="DraftLabel"/> property.</summary>
    public static readonly StyledProperty<string?> DraftLabelProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, string?>(nameof(DraftLabel), "Draft model");

    /// <summary>Defines the <see cref="DraftDetail"/> property.</summary>
    public static readonly StyledProperty<string?> DraftDetailProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, string?>(nameof(DraftDetail), "Small, fast");

    /// <summary>Defines the <see cref="TargetLabel"/> property.</summary>
    public static readonly StyledProperty<string?> TargetLabelProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, string?>(nameof(TargetLabel), "Target model");

    /// <summary>Defines the <see cref="TargetDetail"/> property.</summary>
    public static readonly StyledProperty<string?> TargetDetailProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, string?>(nameof(TargetDetail), "Large");

    /// <summary>Defines the <see cref="Status"/> property.</summary>
    public static readonly StyledProperty<SpeculativeDecodingStatus> StatusProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, SpeculativeDecodingStatus>(nameof(Status));

    /// <summary>Defines the <see cref="ErrorText"/> property.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, string?>(nameof(ErrorText));

    /// <summary>Defines the <see cref="RetryCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, ICommand?>(nameof(RetryCommand));

    /// <summary>Defines the <see cref="Labels"/> property.</summary>
    public static readonly StyledProperty<SpeculativeDecodingLabels> LabelsProperty =
        AvaloniaProperty.Register<SpeculativeDecoding, SpeculativeDecodingLabels>(nameof(Labels), new SpeculativeDecodingLabels());

    /// <summary>Defines the <see cref="Stats"/> property.</summary>
    public static readonly DirectProperty<SpeculativeDecoding, DecodeStats?> StatsProperty =
        AvaloniaProperty.RegisterDirect<SpeculativeDecoding, DecodeStats?>(nameof(Stats), o => o.Stats);

    /// <summary>Defines the <see cref="PlannedStats"/> property.</summary>
    public static readonly DirectProperty<SpeculativeDecoding, DecodeStats?> PlannedStatsProperty =
        AvaloniaProperty.RegisterDirect<SpeculativeDecoding, DecodeStats?>(nameof(PlannedStats), o => o.PlannedStats);

    /// <summary>Defines the <see cref="Completed"/> routed event.</summary>
    public static readonly RoutedEvent<SpeculativeDecodingCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<SpeculativeDecoding, SpeculativeDecodingCompletedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="RetryRequested"/> routed event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<SpeculativeDecoding, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    private DecodeStats? _stats;
    private DecodeStats? _plannedStats;

    static SpeculativeDecoding()
    {
        TitleProperty.OverrideDefaultValue<SpeculativeDecoding>("Speculative decoding");
    }

    /// <summary>Creates the card with the built-in sample answer.</summary>
    public SpeculativeDecoding()
    {
        SetCurrentValue(TokensProperty, new AvaloniaList<DecodeToken>());
        SetCurrentValue(RoundsProperty, new AvaloniaList<DecodeRound>());
        _compareOpen = new MotionValue<double>(this, 0, MotionVectors.Double);
        _compareOpen.Changed += OnCompareOpenChanged;
        AutomationProperties.SetName(this, Title);
    }

    /// <summary>
    /// The target model's answer, one entry per token with its leading space, for a simulated run. Empty uses the built-in
    /// sample (<see cref="SpeculativeDecodingSamples.Tokens"/>); <see cref="TokensSource"/> takes precedence when set.
    /// Replace an item (rather than editing it) to replan.
    /// </summary>
    [Content]
    public AvaloniaList<DecodeToken> Tokens { get => GetValue(TokensProperty); set => SetValue(TokensProperty, value); }

    /// <summary>A sequence of tokens (for binding); replaces <see cref="Tokens"/> when set.</summary>
    public IEnumerable<DecodeToken>? TokensSource { get => GetValue(TokensSourceProperty); set => SetValue(TokensSourceProperty, value); }

    /// <summary>
    /// A recorded run to replay instead of the simulation: per round the drafts, how many were accepted from the start and
    /// the target's own token. A non-empty list hides the draft length slider. <see cref="RoundsSource"/> takes precedence.
    /// </summary>
    public AvaloniaList<DecodeRound> Rounds { get => GetValue(RoundsProperty); set => SetValue(RoundsProperty, value); }

    /// <summary>A sequence of recorded rounds (for binding); replaces <see cref="Rounds"/> when set.</summary>
    public IEnumerable<DecodeRound>? RoundsSource { get => GetValue(RoundsSourceProperty); set => SetValue(RoundsSourceProperty, value); }

    /// <summary>Tokens the draft guesses per round, 2 to 8 (default 4). Changing it replans the run and plays it from the start.</summary>
    public int DraftLength { get => GetValue(DraftLengthProperty); set => SetValue(DraftLengthProperty, value); }

    /// <summary>Shows the target-only line and races it on the same clock. Turning it on does not restart the run.</summary>
    public bool IsComparing { get => GetValue(IsComparingProperty); set => SetValue(IsComparingProperty, value); }

    /// <summary>The seed of the simulated guesses: the same seed, tokens and draft length always give the same run.</summary>
    public int Seed { get => GetValue(SeedProperty); set => SetValue(SeedProperty, value); }

    /// <summary>
    /// The cost of one draft token as a share of a target pass (default 0.1, 0 to 1). It sets how long drafting takes on
    /// screen and counts toward the speedup.
    /// </summary>
    public double DraftCost { get => GetValue(DraftCostProperty); set => SetValue(DraftCostProperty, value); }

    /// <summary>How long one target pass takes on screen (default 640 ms).</summary>
    public TimeSpan PassDuration { get => GetValue(PassDurationProperty); set => SetValue(PassDurationProperty, value); }

    /// <summary>The name of the draft lane ("Draft model"). Keep it generic unless the run came from a named model.</summary>
    public string? DraftLabel { get => GetValue(DraftLabelProperty); set => SetValue(DraftLabelProperty, value); }

    /// <summary>The detail under the draft lane's name ("Small, fast").</summary>
    public string? DraftDetail { get => GetValue(DraftDetailProperty); set => SetValue(DraftDetailProperty, value); }

    /// <summary>The name of the target lane ("Target model").</summary>
    public string? TargetLabel { get => GetValue(TargetLabelProperty); set => SetValue(TargetLabelProperty, value); }

    /// <summary>The detail under the target lane's name ("Large").</summary>
    public string? TargetDetail { get => GetValue(TargetDetailProperty); set => SetValue(TargetDetailProperty, value); }

    /// <summary>
    /// Ready (default), Loading (the lanes dim, the status line shimmers) or Error (the status line shows
    /// <see cref="ErrorText"/> and a retry button). The controls are disabled and the run does not play unless Ready.
    /// </summary>
    public SpeculativeDecodingStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>The message of the error state; null uses <see cref="SpeculativeDecodingLabels.Error"/>.</summary>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <summary>Runs when the retry button is pressed (after <see cref="RetryRequested"/>); setting it shows the button.</summary>
    public ICommand? RetryCommand { get => GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }

    /// <summary>Every text the card shows or announces (status lines, line names, statistics, controls, announcements).</summary>
    public SpeculativeDecodingLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The totals of the rounds that have settled so far (null before the first round settles).</summary>
    public DecodeStats? Stats { get => _stats; private set => SetAndRaise(StatsProperty, ref _stats, value); }

    /// <summary>The totals of the whole planned run (what the run will show when it finishes; null without tokens).</summary>
    public DecodeStats? PlannedStats { get => _plannedStats; private set => SetAndRaise(PlannedStatsProperty, ref _plannedStats, value); }

    /// <summary>Raised each time the speculative line finishes, with the run's totals.</summary>
    public event EventHandler<SpeculativeDecodingCompletedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    /// <summary>Raised by the retry button in the error state. The button shows only when someone handles this or <see cref="RetryCommand"/> is set.</summary>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add
        {
            AddHandler(RetryRequestedEvent, value);
            UpdateRetry();
        }
        remove
        {
            RemoveHandler(RetryRequestedEvent, value);
            UpdateRetry();
        }
    }

    /// <summary>The plan being played (tests).</summary>
    internal DecodePlan Plan
    {
        get
        {
            EnsurePlan();
            return _plan;
        }
    }

    /// <summary>The timeline being played (tests).</summary>
    internal DecodeTimeline Timeline
    {
        get
        {
            EnsurePlan();
            return _timeline!;
        }
    }

    /// <summary>The story time in seconds (tests).</summary>
    internal double StoryTime => Now;

    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    private static int CoerceDraftLength(AvaloniaObject owner, int value) => Math.Clamp(value, MinDraftLength, MaxDraftLength);

    private static double CoerceDraftCost(AvaloniaObject owner, double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.1;

    private static TimeSpan CoercePassDuration(AvaloniaObject owner, TimeSpan value)
        => value < TimeSpan.FromMilliseconds(50) ? TimeSpan.FromMilliseconds(50) : value > TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(10) : value;

    private sealed class CardPeer : ControlAutomationPeer
    {
        public CardPeer(SpeculativeDecoding owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnDataChanged(play: null);
}
