using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(RoundPart, typeof(Control))]
[TemplatePart(RoundWordPart, typeof(TextBlock))]
[TemplatePart(RoundDigitsPart, typeof(RollingDigits))]
[TemplatePart(LeaderboardPart, typeof(Button))]
[TemplatePart(LeaderboardTextPart, typeof(TextBlock))]
[TemplatePart(UnseenDotPart, typeof(Control))]
[TemplatePart(BlindHostPart, typeof(Control))]
[TemplatePart(BlindTextPart, typeof(TextBlock))]
[TemplatePart(BlindPart, typeof(Switch))]
[TemplatePart(StandingsPopoverPart, typeof(Popover))]
[TemplatePart(StandingsPart, typeof(ArenaStandings))]
[TemplatePart(FormPart, typeof(ArenaPromptForm))]
[TemplatePart(StagePart, typeof(ArenaStagePanel))]
[TemplatePart(TabsPart, typeof(ArenaPaneTabs))]
[TemplatePart(PaneAPart, typeof(ArenaPane))]
[TemplatePart(PaneBPart, typeof(ArenaPane))]
[TemplatePart(DividerPart, typeof(Control))]
[TemplatePart(VoteBarPart, typeof(ArenaVoteBar))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, RunningPseudoClass, VotingPseudoClass, VotedPseudoClass, BlindPseudoClass,
    UnseenPseudoClass, LeaderboardOpenPseudoClass)]
public partial class ModelArena
{
    public const string CardPart = "PART_Card";
    public const string TitlePart = "PART_Title";
    public const string RoundPart = "PART_Round";
    public const string RoundWordPart = "PART_RoundWord";
    public const string RoundDigitsPart = "PART_RoundDigits";
    public const string LeaderboardPart = "PART_Leaderboard";
    public const string LeaderboardTextPart = "PART_LeaderboardText";
    public const string UnseenDotPart = "PART_UnseenDot";
    public const string BlindHostPart = "PART_BlindHost";
    public const string BlindTextPart = "PART_BlindText";
    public const string BlindPart = "PART_Blind";
    public const string StandingsPopoverPart = "PART_StandingsPopover";
    public const string StandingsPart = "PART_Standings";
    public const string FormPart = "PART_Form";
    public const string StagePart = "PART_Stage";
    public const string TabsPart = "PART_Tabs";
    public const string PaneAPart = "PART_PaneA";
    public const string PaneBPart = "PART_PaneB";
    public const string DividerPart = "PART_Divider";
    public const string VoteBarPart = "PART_VoteBar";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the arena is narrower than 560: the answers are two tabs.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the arena is narrower than 480: the compact header, prompt row and vote strip.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while a model is thinking or writing.</summary>
    public const string RunningPseudoClass = ":running";

    /// <summary>Set while the vote is open.</summary>
    public const string VotingPseudoClass = ":voting";

    /// <summary>Set after the vote of the round.</summary>
    public const string VotedPseudoClass = ":voted";

    /// <summary>Set while <see cref="IsBlind"/>.</summary>
    public const string BlindPseudoClass = ":blind";

    /// <summary>Set while <see cref="HasUnseenResult"/>: the dot on the Leaderboard button shows.</summary>
    public const string UnseenPseudoClass = ":unseen";

    /// <summary>Set while the standings popover is open: the Leaderboard button looks hovered.</summary>
    public const string LeaderboardOpenPseudoClass = ":leaderboard-open";

    /// <summary>The Leaderboard button's side padding (spec 3.3: 0 10); none when compact, where it is a 36 × 36 icon.</summary>
    private static readonly Thickness s_leaderboardPadding = new(10, 0);

    /// <summary>The compact Leaderboard button is square: as wide as the small button is high (36).</summary>
    private const double CompactLeaderboardWidth = 36;

    private static readonly Easing s_linear = new LinearEasing();

    #region Token-driven internals

    internal static readonly StyledProperty<double> NarrowBreakpointProperty =
        AvaloniaProperty.Register<ModelArena, double>(nameof(NarrowBreakpoint), 560);

    internal static readonly StyledProperty<double> CompactBreakpointProperty =
        AvaloniaProperty.Register<ModelArena, double>(nameof(CompactBreakpoint), 480);

    /// <summary>Below this width the answers are two tabs (the token's 560).</summary>
    internal double NarrowBreakpoint { get => GetValue(NarrowBreakpointProperty); set => SetValue(NarrowBreakpointProperty, value); }

    /// <summary>Below this width the layout is compact (the token's 480).</summary>
    internal double CompactBreakpoint { get => GetValue(CompactBreakpointProperty); set => SetValue(CompactBreakpointProperty, value); }

    #endregion

    private Border? _card;
    private TextBlock? _title;
    private Control? _roundGroup;
    private TextBlock? _roundWord;
    private RollingDigits? _roundDigits;
    private Button? _leaderboard;
    private TextBlock? _leaderboardText;
    private Control? _unseenDot;
    private Control? _blindHost;
    private TextBlock? _blindText;
    private Switch? _blind;
    private Popover? _popover;
    private ArenaStandings? _standings;
    private ArenaPromptForm? _form;
    private ArenaStagePanel? _stage;
    private ArenaPaneTabs? _tabs;
    private ArenaPane? _paneViewA;
    private ArenaPane? _paneViewB;
    private Control? _divider;
    private ArenaVoteBar? _voteBar;
    private AiAnnouncer? _announcer;
    private MotionValue<double>? _fadeA;
    private MotionValue<double>? _fadeB;
    private bool _isNarrow;
    private bool _isCompact;
    private bool _keyboardMode;
    private int _shownRound;

    static ModelArena()
    {
        FocusableProperty.OverrideDefaultValue<ModelArena>(false);
    }

    /// <summary>Creates an empty arena: give it <see cref="Contestants"/>, <see cref="Prompts"/> and a <see cref="Runner"/>.</summary>
    public ModelArena()
    {
        _loop = new LiveLoop(this, Step)
        {
            // The answers keep their cadence at every motion level (only the fades go) and off-screen.
            MinimumLevel = ArcMotionLevel.Off,
            TracksViewport = false
        };
        // Where the last input came from decides whether a focus move shows the keyboard ring.
        AddHandler(KeyDownEvent, (_, _) => _keyboardMode = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardMode = false, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(ArenaPromptForm.RunRequestedEvent, OnRunRequested);
        AddHandler(ArenaPromptForm.StopRequestedEvent, OnStopRequested);
        AddHandler(ArenaPromptForm.PromptChosenEvent, OnPromptChosen);
        AddHandler(ArenaVoteBar.VoteRequestedEvent, OnVoteRequested);
        AddHandler(ArenaVoteBar.NextRoundRequestedEvent, OnNextRoundRequested);
        AddHandler(ArenaVoteBar.RunAgainRequestedEvent, OnRunAgainRequested);
        AddHandler(ArenaPane.ErrorActionRequestedEvent, OnErrorActionRequested);
        Contestants.CollectionChanged += OnContestantsChanged;
        Prompts.CollectionChanged += OnPromptsChanged;
        Standings.CollectionChanged += OnStandingsChanged;
        PseudoClasses.Set(BlindPseudoClass, IsBlind);
    }

    #region Test surface

    /// <summary>The live region's last announcement (tests).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    internal ArenaPromptForm? Form => _form;
    internal ArenaVoteBar? VoteBar => _voteBar;
    internal ArenaPane? PaneViewA => _paneViewA;
    internal ArenaPane? PaneViewB => _paneViewB;
    internal ArenaPaneTabs? Tabs => _tabs;
    internal ArenaStagePanel? Stage => _stage;
    internal ArenaStandings? StandingsView => _standings;
    internal Popover? StandingsPopover => _popover;
    internal Button? LeaderboardButton => _leaderboard;
    internal TextBlock? LeaderboardText => _leaderboardText;
    internal Control? UnseenDot => _unseenDot;
    internal Switch? BlindSwitch => _blind;
    internal Control? BlindHost => _blindHost;
    internal RollingDigits? RoundDigits => _roundDigits;
    internal TextBlock? RoundWord => _roundWord;
    internal Control? RoundGroup => _roundGroup;
    internal TextBlock? TitleText => _title;

    /// <summary>Whether the narrow (tabs) layout is in use.</summary>
    internal bool IsNarrowLayout => _isNarrow;

    /// <summary>Whether the compact layout is in use.</summary>
    internal bool IsCompactLayout => _isCompact;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _card = scope.Find<Border>(CardPart);
        _title = scope.Find<TextBlock>(TitlePart);
        _roundGroup = scope.Find<Control>(RoundPart);
        _roundWord = scope.Find<TextBlock>(RoundWordPart);
        _roundDigits = scope.Find<RollingDigits>(RoundDigitsPart);
        _leaderboard = scope.Find<Button>(LeaderboardPart);
        _leaderboardText = scope.Find<TextBlock>(LeaderboardTextPart);
        _unseenDot = scope.Find<Control>(UnseenDotPart);
        _blindHost = scope.Find<Control>(BlindHostPart);
        _blindText = scope.Find<TextBlock>(BlindTextPart);
        _blind = scope.Find<Switch>(BlindPart);
        _popover = scope.Find<Popover>(StandingsPopoverPart);
        _standings = scope.Find<ArenaStandings>(StandingsPart);
        _form = scope.Find<ArenaPromptForm>(FormPart);
        _stage = scope.Find<ArenaStagePanel>(StagePart);
        _tabs = scope.Find<ArenaPaneTabs>(TabsPart);
        _paneViewA = scope.Find<ArenaPane>(PaneAPart);
        _paneViewB = scope.Find<ArenaPane>(PaneBPart);
        _divider = scope.Find<Control>(DividerPart);
        _voteBar = scope.Find<ArenaVoteBar>(VoteBarPart);
        _announcer = scope.Find<AiAnnouncer>(AnnouncerPart);
        HookParts();

        if (_stage is not null)
        {
            _stage.Tabs = _tabs;
            _stage.PaneA = _paneViewA;
            _stage.Divider = _divider;
            _stage.PaneB = _paneViewB;
        }
        if (_paneViewA is not null)
        {
            _paneViewA.State = PaneA;
            _fadeA = new MotionValue<double>(_paneViewA, 1, MotionVectors.Double);
            _fadeA.Changed += opacity => _paneViewA.Opacity = opacity;
        }
        if (_paneViewB is not null)
        {
            _paneViewB.State = PaneB;
            _fadeB = new MotionValue<double>(_paneViewB, 1, MotionVectors.Double);
            _fadeB.Changed += opacity => _paneViewB.Opacity = opacity;
        }
        if (_tabs is not null)
        {
            _tabs.PaneA = PaneA;
            _tabs.PaneB = PaneB;
        }
        if (_form is not null)
        {
            _form.Prompts = Prompts;
            _form.ShortcutHint = PromptKbd.RunShortcutText;
        }
        if (_standings is not null)
        {
            _standings.Limit = LeaderboardSize;
        }
        _shownRound = 0;
        ApplyLabels();
        ApplyWidthClasses(force: true);
        SyncBlind();
        SyncReveal();
        SyncTiming();
        SyncRunParts();
        SyncVoteBar();
        SyncRound();
        SyncUnseen();
        SyncLeaderboardOpen();
    }

    private void HookParts()
    {
        if (_leaderboard is not null)
        {
            _leaderboard.Click += OnLeaderboardClick;
        }
        if (_blind is not null)
        {
            _blind.IsCheckedChanged += OnBlindSwitched;
        }
        if (_blindHost is not null)
        {
            _blindHost.Tapped += OnBlindHostTapped;
        }
        if (_popover is not null)
        {
            _popover.PropertyChanged += OnPopoverPropertyChanged;
            _popover.Opened += OnPopoverOpened;
        }
        if (_standings is not null)
        {
            _standings.ResetRequested += OnResetRequested;
        }
        if (_tabs is not null)
        {
            _tabs.PropertyChanged += OnTabsPropertyChanged;
        }
    }

    private void UnhookParts()
    {
        if (_leaderboard is not null)
        {
            _leaderboard.Click -= OnLeaderboardClick;
        }
        if (_blind is not null)
        {
            _blind.IsCheckedChanged -= OnBlindSwitched;
        }
        if (_blindHost is not null)
        {
            _blindHost.Tapped -= OnBlindHostTapped;
        }
        if (_popover is not null)
        {
            _popover.PropertyChanged -= OnPopoverPropertyChanged;
            _popover.Opened -= OnPopoverOpened;
            if (_popover.Popup is { } popup)
            {
                popup.Closed -= OnPopupClosed;
            }
        }
        if (_standings is not null)
        {
            _standings.ResetRequested -= OnResetRequested;
        }
        if (_tabs is not null)
        {
            _tabs.PropertyChanged -= OnTabsPropertyChanged;
        }
        _fadeA?.Dispose();
        _fadeA = null;
        _fadeB?.Dispose();
        _fadeB = null;
        _popupHooked = false;
    }

    /// <summary>Says <paramref name="text"/> through the polite live region.</summary>
    private void Announce(string? text) => _announcer?.Announce(text);

    /// <summary>How a focus move should look: with the ring after a key, without after a pointer.</summary>
    private NavigationMethod FocusMethod => _keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer;

    #endregion

    #region Words

    /// <summary>Every word on screen and every accessible name, from <see cref="Labels"/>.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = labels.Title;
        }
        if (_roundWord is not null)
        {
            _roundWord.Text = labels.RoundWord;
        }
        if (_leaderboardText is not null)
        {
            _leaderboardText.Text = labels.Leaderboard;
        }
        if (_blindText is not null)
        {
            _blindText.Text = labels.Blind;
        }
        if (_blind is not null)
        {
            AutomationProperties.SetName(_blind, labels.Blind);
        }
        if (_popover is not null)
        {
            AutomationProperties.SetName(_popover, labels.StandingsTitle);
        }
        if (_form is not null)
        {
            _form.Placeholder = labels.Placeholder;
            _form.PromptName = labels.PromptName;
            _form.RunText = labels.Run;
            _form.StopText = labels.Stop;
            _form.PromptsName = labels.SamplePrompts;
        }
        if (_voteBar is not null)
        {
            _voteBar.PendingText = labels.Waiting;
            _voteBar.QuestionText = labels.VoteQuestion;
            _voteBar.VoteAText = labels.VoteA;
            _voteBar.TieText = labels.VoteTie;
            _voteBar.VoteBText = labels.VoteB;
            _voteBar.BothBadText = labels.VoteBothBad;
            _voteBar.NextRoundText = labels.NextRound;
            _voteBar.StoppedText = labels.StoppedRow;
            _voteBar.RunAgainText = labels.RunAgain;
        }
        if (_standings is not null)
        {
            _standings.Title = labels.StandingsTitle;
            _standings.EmptyText = labels.EmptyStandings;
            _standings.FootnoteText = labels.TieRule;
            _standings.ResetText = labels.Reset;
        }
        if (_tabs is not null)
        {
            _tabs.Labels = labels;
            _tabs.TabsName = labels.Answers;
        }
        if (_paneViewA is not null)
        {
            _paneViewA.Labels = labels;
        }
        if (_paneViewB is not null)
        {
            _paneViewB.Labels = labels;
        }
        SyncRoundName();
        SyncUnseen();
        SyncVoteTexts();
        if (IsLeaderboardOpen)
        {
            FillStandings(replay: false);
        }
    }

    #endregion

    #region Header

    /// <summary>
    /// The round number rolls to its new value; the first number shows at once (nothing to roll from), and before the first
    /// run the "· Round" part is hidden.
    /// </summary>
    private void SyncRound()
    {
        if (_roundGroup is not null)
        {
            _roundGroup.IsVisible = Round > 0;
        }
        if (_roundDigits is not null && Round != _shownRound)
        {
            _roundDigits.IsInstant = _shownRound == 0 || !_roundDigits.IsAttachedToVisualTree();
            _roundDigits.Value = Round;
            _roundDigits.IsInstant = false;
            _shownRound = Round;
        }
        SyncRoundName();
    }

    /// <summary>The visible "Round" word reads the whole "Round 3" (the dot and the rolling digits are hidden from readers).</summary>
    private void SyncRoundName()
    {
        if (_roundWord is not null)
        {
            AutomationProperties.SetName(_roundWord, Round > 0 ? Labels.RoundName(Round) : null);
        }
    }

    /// <summary>The dot and the button's name follow <see cref="HasUnseenResult"/> ("Standings, updated").</summary>
    private void SyncUnseen()
    {
        PseudoClasses.Set(UnseenPseudoClass, HasUnseenResult);
        if (_leaderboard is not null)
        {
            AutomationProperties.SetName(_leaderboard, HasUnseenResult ? Labels.LeaderboardUpdated : Labels.Leaderboard);
        }
    }

    private void SyncBlind()
    {
        PseudoClasses.Set(BlindPseudoClass, IsBlind);
        if (_blind is not null && _blind.IsChecked != IsBlind)
        {
            _blind.IsChecked = IsBlind;
        }
    }

    private void OnBlindSwitched(object? sender, RoutedEventArgs e)
    {
        if (_blind is not null)
        {
            SetCurrentValue(IsBlindProperty, _blind.IsChecked == true);
        }
    }

    /// <summary>The word beside the switch is part of the same control (Arc's one button): a tap on it flips the switch.</summary>
    private void OnBlindHostTapped(object? sender, TappedEventArgs e)
    {
        if (_blind is null || e.Source is Visual source && (ReferenceEquals(source, _blind) || _blind.IsVisualAncestorOf(source)))
        {
            return;
        }
        e.Handled = true;
        SetCurrentValue(IsBlindProperty, !IsBlind);
    }

    #endregion

    #region Width classes

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Decided from the arranged width (not in measure, where a trial width could flip the layout back and forth).
        ApplyWidthClasses(force: false, finalSize.Width);
        return base.ArrangeOverride(finalSize);
    }

    /// <summary>
    /// <c>:narrow</c> below 560 and <c>:compact</c> below 480 (spec 7), with 1px of hysteresis: a layout leaves a mode only
    /// one pixel past the breakpoint, so a width that lands exactly on it does not flip back and forth.
    /// </summary>
    private void ApplyWidthClasses(bool force, double? width = null)
    {
        var narrow = _isNarrow;
        var compact = _isCompact;
        if (width is { } w && w > 0 && !double.IsInfinity(w))
        {
            narrow = _isNarrow ? w < NarrowBreakpoint + 1 : w < NarrowBreakpoint;
            compact = _isCompact ? w < CompactBreakpoint + 1 : w < CompactBreakpoint;
        }
        if (!force && narrow == _isNarrow && compact == _isCompact)
        {
            return;
        }
        var narrowChanged = narrow != _isNarrow;
        _isNarrow = narrow;
        _isCompact = compact;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_form is not null)
        {
            _form.IsCompact = compact;
        }
        if (_voteBar is not null)
        {
            _voteBar.IsNarrow = narrow;
            _voteBar.IsCompact = compact;
        }
        if (_leaderboard is not null)
        {
            // A 36 × 36 icon button when compact (its name stays).
            _leaderboard.Padding = compact ? default : s_leaderboardPadding;
            _leaderboard.Width = compact ? CompactLeaderboardWidth : double.NaN;
        }
        if (_leaderboardText is not null)
        {
            _leaderboardText.IsVisible = !compact;
        }
        if (force || narrowChanged)
        {
            ApplyStageLayout();
        }
    }

    /// <summary>
    /// Side by side: both panes and the divider; narrow: the tab strip and the selected pane only (the other is hidden, out
    /// of the Tab order, and keeps receiving its text).
    /// </summary>
    private void ApplyStageLayout()
    {
        if (_stage is not null)
        {
            _stage.IsNarrow = _isNarrow;
        }
        if (_tabs is not null)
        {
            _tabs.IsVisible = _isNarrow;
        }
        if (_divider is not null)
        {
            _divider.IsVisible = !_isNarrow;
        }
        var selected = _tabs?.SelectedSlot ?? ArenaSlot.A;
        ShowPane(_paneViewA, _fadeA, !_isNarrow || selected == ArenaSlot.A, animate: false);
        ShowPane(_paneViewB, _fadeB, !_isNarrow || selected == ArenaSlot.B, animate: false);
        // Narrow, the tabs carry each answer's identity and status (spec 7: the stage is 12 + 60 + 311, no 48px pane head).
        if (_paneViewA is not null)
        {
            _paneViewA.ShowsHeader = !_isNarrow;
        }
        if (_paneViewB is not null)
        {
            _paneViewB.ShowsHeader = !_isNarrow;
        }
    }

    private void OnTabsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ArenaPaneTabs.SelectedSlotProperty || !_isNarrow)
        {
            return;
        }
        var selected = e.GetNewValue<ArenaSlot>();
        // The old panel hides at once; the new one fades in (spec 5.1: 160ms, display:none for the other).
        ShowPane(_paneViewA, _fadeA, selected == ArenaSlot.A, animate: true);
        ShowPane(_paneViewB, _fadeB, selected == ArenaSlot.B, animate: true);
    }

    /// <summary>
    /// Shows or hides a pane; a pane that appears in the tab layout fades in over 160ms (Reduced 120ms linear, Off at once).
    /// Code-driven because the fade must start from 0 in the frame the hidden pane becomes visible, a value a ControlTheme
    /// opacity transition never sees.
    /// </summary>
    private static void ShowPane(ArenaPane? pane, MotionValue<double>? fade, bool show, bool animate)
    {
        if (pane is null)
        {
            return;
        }
        var appears = show && !pane.IsVisible;
        pane.IsVisible = show;
        if (fade is null)
        {
            return;
        }
        if (!show || !appears || !animate)
        {
            fade.Jump(1);
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(pane);
        if (level == ArcMotionLevel.Off)
        {
            fade.Jump(1);
            return;
        }
        fade.Jump(0);
        _ = fade.AnimateTo(1, level == ArcMotionLevel.Full
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
    }

    #endregion

    #region Our properties

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == LabelsProperty)
        {
            ApplyLabels();
        }
        else if (property == IsBlindProperty)
        {
            if (change.GetNewValue<bool>())
            {
                // Turning Blind on hides the models again, even after the vote (spec 1 item 9).
                _revealedByVote = false;
            }
            SyncBlind();
            SyncReveal();
        }
        else if (property == RunnerProperty)
        {
            SyncRunParts();
            TryAutoStart();
        }
        else if (property == AutoStartProperty || property == IsVisibleProperty)
        {
            TryAutoStart();
        }
        else if (property == IsLeaderboardOpenProperty)
        {
            SyncLeaderboardOpen();
        }
        else if (property == LeaderboardSizeProperty)
        {
            if (_standings is not null)
            {
                _standings.Limit = LeaderboardSize;
            }
            if (IsLeaderboardOpen)
            {
                FillStandings(replay: false);
            }
        }
        else if (property == RoundProperty)
        {
            SyncRound();
        }
        else if (property == HasUnseenResultProperty)
        {
            SyncUnseen();
        }
        else if (property == NarrowBreakpointProperty || property == CompactBreakpointProperty)
        {
            InvalidateArrange();
        }
    }

    private void OnContestantsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncRunParts();
        TryAutoStart();
        if (IsLeaderboardOpen)
        {
            FillStandings(replay: false);
        }
    }

    private void OnPromptsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Prefill();
        TryAutoStart();
    }

    /// <summary>The first time there are sample prompts, an empty box shows the first one (spec 2.2 <c>PromptText</c>).</summary>
    private void Prefill()
    {
        if (_prefilled || Prompts.Count == 0)
        {
            return;
        }
        var text = Prompts[0].Text;
        if (string.IsNullOrEmpty(text))
        {
            // Not filled in yet (an item added before its text): try again when the arena is loaded.
            return;
        }
        _prefilled = true;
        if (string.IsNullOrEmpty(PromptText))
        {
            SetCurrentValue(PromptTextProperty, text);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Prefill();
        TryAutoStart();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // An arena that leaves the window stops its answers and its clock (Arc's unmount aborts every stream).
        Detached();
    }

    #endregion

    #region Part events

    private void OnRunRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = RunAsync();
    }

    private void OnStopRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Stop();
    }

    /// <summary>A sample prompt chip fills the box and runs it (spec 1 item 3).</summary>
    private void OnPromptChosen(object? sender, ArenaPromptChosenEventArgs e)
    {
        e.Handled = true;
        _ = RunAsync(e.Prompt.Text);
    }

    /// <summary>
    /// A vote button: the strip itself moves the focus from the vote row to Next round (with the ring only after a key), so
    /// nothing is focused here.
    /// </summary>
    private void OnVoteRequested(object? sender, ArenaVoteRequestedEventArgs e)
    {
        e.Handled = true;
        Vote(e.Choice);
    }

    private void OnNextRoundRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        NextRound();
    }

    /// <summary>Run again runs the stopped round's prompt with the same pair; the focus moves to Stop (Run again is gone).</summary>
    private void OnRunAgainRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var hadFocus = IsKeyboardFocusWithin;
        var method = FocusMethod;
        var prompt = string.IsNullOrWhiteSpace(_roundPrompt) ? null : _roundPrompt;
        _ = RunAsync(prompt);
        if (hadFocus)
        {
            _form?.FocusRun(method);
        }
    }

    /// <summary>The error card's button: Retry the pane, or (not retryable) put the first sample prompt in the box and focus Run.</summary>
    private void OnErrorActionRequested(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Visual source)
        {
            return;
        }
        ArenaSlot slot;
        if (_paneViewA is not null && (ReferenceEquals(_paneViewA, source) || _paneViewA.IsVisualAncestorOf(source)))
        {
            slot = ArenaSlot.A;
        }
        else if (_paneViewB is not null && (ReferenceEquals(_paneViewB, source) || _paneViewB.IsVisualAncestorOf(source)))
        {
            slot = ArenaSlot.B;
        }
        else
        {
            return;
        }
        e.Handled = true;
        if (StateOf(slot).IsRetryable)
        {
            Retry(slot);
            return;
        }
        UseSamplePrompt();
    }

    /// <summary>
    /// "Try a sample prompt": the first sample prompt goes in the box and the focus moves to Run; nothing runs until the
    /// person presses it (spec 1 item 11, 10.4).
    /// </summary>
    private void UseSamplePrompt()
    {
        var method = FocusMethod;
        if (Prompts.Count > 0 && !string.IsNullOrEmpty(Prompts[0].Text))
        {
            SetCurrentValue(PromptTextProperty, Prompts[0].Text);
        }
        if (_form is null)
        {
            return;
        }
        if (!_form.FocusRun(method))
        {
            _form.FocusBox(method);
        }
    }

    #endregion
}
