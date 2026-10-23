using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A blind test of two models: one prompt goes to two anonymous models, both answers stream in at the same pace, and only
/// after the person votes are the models revealed. Each answer shows how fast it started, how fast it came and what it cost,
/// and a small standings list ranks the models by win rate.
/// <code>
/// &lt;arc:ModelArena Contestants="{Binding Models}" Prompts="{Binding Prompts}" Runner="{Binding Runner}" /&gt;
/// </code>
/// </summary>
public partial class ModelArena : TemplatedControl
{
    #region Properties

    /// <summary>The prompt in the box (two-way). The first time <see cref="Prompts"/> has an item the box shows its text.</summary>
    public static readonly StyledProperty<string> PromptTextProperty =
        AvaloniaProperty.Register<ModelArena, string>(nameof(PromptText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Streams the models' answers; null cannot run (the Gallery supplies a scripted runner).</summary>
    public static readonly StyledProperty<IArenaRunner?> RunnerProperty =
        AvaloniaProperty.Register<ModelArena, IArenaRunner?>(nameof(Runner));

    /// <summary>Whether the models are hidden until the vote (two-way). Turning it off shows them at once and turning it on hides them again.</summary>
    public static readonly StyledProperty<bool> IsBlindProperty =
        AvaloniaProperty.Register<ModelArena, bool>(nameof(IsBlind), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the first round starts by itself when the arena is shown.</summary>
    public static readonly StyledProperty<bool> AutoStartProperty =
        AvaloniaProperty.Register<ModelArena, bool>(nameof(AutoStart));

    /// <summary>The most rows the standings show.</summary>
    public static readonly StyledProperty<int> LeaderboardSizeProperty =
        AvaloniaProperty.Register<ModelArena, int>(nameof(LeaderboardSize), 5);

    /// <summary>Whether the standings popover is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsLeaderboardOpenProperty =
        AvaloniaProperty.Register<ModelArena, bool>(nameof(IsLeaderboardOpen), false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Chooses the pair of each round; null picks two random models and avoids repeating the last pair.</summary>
    public static readonly StyledProperty<IArenaPairPicker?> PairPickerProperty =
        AvaloniaProperty.Register<ModelArena, IArenaPairPicker?>(nameof(PairPicker));

    /// <summary>How the two answers are paced when shown.</summary>
    public static readonly StyledProperty<ArenaPacing> PacingProperty =
        AvaloniaProperty.Register<ModelArena, ArenaPacing>(nameof(Pacing), ArenaPacing.Default);

    /// <summary>Run when a vote is cast, with the <see cref="ArenaVote"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> VoteCommandProperty =
        AvaloniaProperty.Register<ModelArena, ICommand?>(nameof(VoteCommand));

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<ModelArenaLabels> LabelsProperty =
        AvaloniaProperty.Register<ModelArena, ModelArenaLabels>(nameof(Labels), new ModelArenaLabels());

    /// <summary>The number of the current round; it grows with every run (read-only).</summary>
    public static readonly DirectProperty<ModelArena, int> RoundProperty =
        AvaloniaProperty.RegisterDirect<ModelArena, int>(nameof(Round), o => o.Round);

    /// <summary>Where the round is (read-only).</summary>
    public static readonly DirectProperty<ModelArena, ArenaPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<ModelArena, ArenaPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The first answer (read-only).</summary>
    public static readonly DirectProperty<ModelArena, ArenaPaneState> PaneAProperty =
        AvaloniaProperty.RegisterDirect<ModelArena, ArenaPaneState>(nameof(PaneA), o => o.PaneA);

    /// <summary>The second answer (read-only).</summary>
    public static readonly DirectProperty<ModelArena, ArenaPaneState> PaneBProperty =
        AvaloniaProperty.RegisterDirect<ModelArena, ArenaPaneState>(nameof(PaneB), o => o.PaneB);

    /// <summary>The last vote of this round, or null (read-only).</summary>
    public static readonly DirectProperty<ModelArena, ArenaVote?> LastVoteProperty =
        AvaloniaProperty.RegisterDirect<ModelArena, ArenaVote?>(nameof(LastVote), o => o.LastVote);

    /// <summary>Whether a vote was cast that the person has not yet seen in the standings (read-only).</summary>
    public static readonly DirectProperty<ModelArena, bool> HasUnseenResultProperty =
        AvaloniaProperty.RegisterDirect<ModelArena, bool>(nameof(HasUnseenResult), o => o.HasUnseenResult);

    #endregion

    #region Events

    /// <summary>Raised once for each vote.</summary>
    public static readonly RoutedEvent<ArenaVoteEventArgs> VoteCastEvent =
        RoutedEvent.Register<ModelArena, ArenaVoteEventArgs>(nameof(VoteCast), RoutingStrategies.Bubble);

    /// <summary>Raised when a round starts (every run starts a round).</summary>
    public static readonly RoutedEvent<ArenaRoundEventArgs> RoundStartedEvent =
        RoutedEvent.Register<ModelArena, ArenaRoundEventArgs>(nameof(RoundStarted), RoutingStrategies.Bubble);

    /// <summary>Raised when a run is stopped before both answers finish.</summary>
    public static readonly RoutedEvent<ArenaRoundEventArgs> RunStoppedEvent =
        RoutedEvent.Register<ModelArena, ArenaRoundEventArgs>(nameof(RunStopped), RoutingStrategies.Bubble);

    /// <summary>Raised when one answer fails.</summary>
    public static readonly RoutedEvent<ArenaPaneFailedEventArgs> PaneFailedEvent =
        RoutedEvent.Register<ModelArena, ArenaPaneFailedEventArgs>(nameof(PaneFailed), RoutingStrategies.Bubble);

    /// <summary>Raised when the standings are reset.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> LeaderboardResetEvent =
        RoutedEvent.Register<ModelArena, RoutedEventArgs>(nameof(LeaderboardReset), RoutingStrategies.Bubble);

    /// <summary>Raised once for each vote.</summary>
    public event EventHandler<ArenaVoteEventArgs>? VoteCast { add => AddHandler(VoteCastEvent, value); remove => RemoveHandler(VoteCastEvent, value); }

    /// <summary>Raised when a round starts.</summary>
    public event EventHandler<ArenaRoundEventArgs>? RoundStarted { add => AddHandler(RoundStartedEvent, value); remove => RemoveHandler(RoundStartedEvent, value); }

    /// <summary>Raised when a run is stopped.</summary>
    public event EventHandler<ArenaRoundEventArgs>? RunStopped { add => AddHandler(RunStoppedEvent, value); remove => RemoveHandler(RunStoppedEvent, value); }

    /// <summary>Raised when one answer fails.</summary>
    public event EventHandler<ArenaPaneFailedEventArgs>? PaneFailed { add => AddHandler(PaneFailedEvent, value); remove => RemoveHandler(PaneFailedEvent, value); }

    /// <summary>Raised when the standings are reset.</summary>
    public event EventHandler<RoutedEventArgs>? LeaderboardReset { add => AddHandler(LeaderboardResetEvent, value); remove => RemoveHandler(LeaderboardResetEvent, value); }

    #endregion

    private int _round;
    private ArenaPhase _phase;
    private ArenaVote? _lastVote;
    private bool _hasUnseenResult;
    private ArenaPaneState _paneA = new(ArenaSlot.A);
    private ArenaPaneState _paneB = new(ArenaSlot.B);

    /// <summary>The models a round draws its pair from; fewer than two disables Run.</summary>
    public AvaloniaList<AiModel> Contestants { get; } = new();

    /// <summary>The sample prompts shown as chips; none hides the chip row.</summary>
    public AvaloniaList<ArenaPrompt> Prompts { get; } = new();

    /// <summary>
    /// The standings by model id: set it before the first vote to start from saved results, and read it to save them. The
    /// arena updates the entries on every vote and replaces them with an empty set on Reset.
    /// </summary>
    public AvaloniaDictionary<string, ArenaRecord> Standings { get; } = new();

    /// <inheritdoc cref="PromptTextProperty"/>
    public string PromptText { get => GetValue(PromptTextProperty); set => SetValue(PromptTextProperty, value); }

    /// <inheritdoc cref="RunnerProperty"/>
    public IArenaRunner? Runner { get => GetValue(RunnerProperty); set => SetValue(RunnerProperty, value); }

    /// <inheritdoc cref="IsBlindProperty"/>
    public bool IsBlind { get => GetValue(IsBlindProperty); set => SetValue(IsBlindProperty, value); }

    /// <inheritdoc cref="AutoStartProperty"/>
    public bool AutoStart { get => GetValue(AutoStartProperty); set => SetValue(AutoStartProperty, value); }

    /// <inheritdoc cref="LeaderboardSizeProperty"/>
    public int LeaderboardSize { get => GetValue(LeaderboardSizeProperty); set => SetValue(LeaderboardSizeProperty, value); }

    /// <inheritdoc cref="IsLeaderboardOpenProperty"/>
    public bool IsLeaderboardOpen { get => GetValue(IsLeaderboardOpenProperty); set => SetValue(IsLeaderboardOpenProperty, value); }

    /// <inheritdoc cref="PairPickerProperty"/>
    public IArenaPairPicker? PairPicker { get => GetValue(PairPickerProperty); set => SetValue(PairPickerProperty, value); }

    /// <inheritdoc cref="PacingProperty"/>
    public ArenaPacing Pacing { get => GetValue(PacingProperty); set => SetValue(PacingProperty, value); }

    /// <inheritdoc cref="VoteCommandProperty"/>
    public ICommand? VoteCommand { get => GetValue(VoteCommandProperty); set => SetValue(VoteCommandProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ModelArenaLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="RoundProperty"/>
    public int Round { get => _round; private set => SetAndRaise(RoundProperty, ref _round, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public ArenaPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <inheritdoc cref="PaneAProperty"/>
    public ArenaPaneState PaneA => _paneA;

    /// <inheritdoc cref="PaneBProperty"/>
    public ArenaPaneState PaneB => _paneB;

    /// <inheritdoc cref="LastVoteProperty"/>
    public ArenaVote? LastVote { get => _lastVote; private set => SetAndRaise(LastVoteProperty, ref _lastVote, value); }

    /// <inheritdoc cref="HasUnseenResultProperty"/>
    public bool HasUnseenResult { get => _hasUnseenResult; private set => SetAndRaise(HasUnseenResultProperty, ref _hasUnseenResult, value); }
}
