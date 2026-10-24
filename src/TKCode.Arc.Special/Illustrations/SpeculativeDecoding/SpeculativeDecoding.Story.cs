using System.Text;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class SpeculativeDecoding
{
    private enum StatusKind
    {
        None,
        Ready,
        Drafting,
        Verifying,
        LastToken,
        Kept,
        FirstMissed,
        AllKept,
        Done,
        SpeculationDone,
        CompareDone,
        Loading,
        Error
    }

    private readonly MotionValue<double> _compareOpen;
    private DecodePlan _plan = DecodePlan.Empty;
    private DecodeTimeline? _timeline;
    private string? _signature;
    private SpeculativeDecodingStage? _stage;
    private SpeculativeDecodingStatusLine? _status;
    private Slider? _slider;
    private Switch? _switch;
    private AvaloniaButton? _retry;
    private AvaloniaButton? _playButton;
    private AvaloniaButton? _restartButton;
    private SpeculativeDecodingStat? _speedup;
    private SpeculativeDecodingStat? _acceptance;
    private SpeculativeDecodingStat? _tokensPerPass;
    private SpeculativeDecodingSpeech? _roundSpeech;
    private SpeculativeDecodingSpeech? _outputSpeech;
    private SpeculativeDecodingSpeech? _compareSpeech;
    private AvaloniaList<DecodeToken>? _observedTokens;
    private AvaloniaList<DecodeRound>? _observedRounds;
    private DecodeTimeline? _fadeTimeline;
    private string? _pendingAnnouncement;
    private const double EndTail = 0.8;
    private double _finishedWall = double.NaN;
    private bool _storyBuilt;
    private bool _inputsDirty = true;
    private bool _isReplay;
    private bool _resuming;
    private bool _replanning;
    private bool _syncing;
    private bool _sliderDriven;
    private bool _autoPlayPending;
    private bool _compareAnnounced;
    private bool _isNarrow;
    private StatusKind _statusKind = StatusKind.None;
    private int _statusA = -1;
    private int _statusB = -1;
    private int _statsShown = -2;
    private int _outputShown = -1;
    private int _compareShown = -1;
    private int _summaryShown = -2;

    // ---- the story (Explainer) --------------------------------------------------------------------------------------

    private protected override bool OpensFinishedWhenReduced => false;

    private protected override void BuildStory(ExplainerScript script)
    {
        EnsurePlan();
        _storyBuilt = true;
        var timeline = _timeline!;
        // Everything on screen is a function of the story time (the stage draws the run from the timeline), so the only
        // beat is the end of the speculative line, where Completed is raised. The story runs on to the end of the
        // target-only line while comparing.
        if (timeline.Count > 0)
        {
            script.At(timeline.MainEnd, OnMainDone);
        }
        script.Length = StoryLength();
    }

    private protected override void ResetScene()
    {
        _compareAnnounced = false;
        _finishedWall = double.NaN;
        if (_resuming)
        {
            return;
        }
        if (_stage is not null && ReferenceEquals(_fadeTimeline, _timeline) && Now > 0)
        {
            // What the last run wrote fades out where it stands while the new run starts.
            _stage.BeginRestartFade(Now, WallTime);
            RequestFrames(0.3);
        }
        _fadeTimeline = _timeline;
        if (!_replanning && (Now > 0 || IsFinished))
        {
            // Spoken by the play / restart click that caused it (their handlers run right after this).
            _pendingAnnouncement = IsFinished ? Labels.PlayingFromStart : Labels.Restarted;
            Dispatcher.UIThread.Post(() => _pendingAnnouncement = null, DispatcherPriority.Background);
        }
    }

    private protected override void OnTimeChanged()
    {
        if (!_resuming)
        {
            UpdateScene();
        }
    }

    private protected override void OnPlaybackChanged()
    {
        if (_resuming)
        {
            return;
        }
        if (IsPlaying && Status != SpeculativeDecodingStatus.Ready)
        {
            // Nothing plays while the data loads or failed; autoplay waits for Ready.
            _autoPlayPending = true;
            Pause();
            return;
        }
        UpdateScene();
    }

    private protected override void OnFinished()
    {
        // The last round settles just before the end, and its tokens are still flying (a flight takes about 0.57 s) while
        // the summary fades in. The story clock stops here, so the drawing runs on for a short tail of wall time.
        _finishedWall = WallTime;
        RequestFrames(EndTail);
        UpdateScene();
    }

    /// <summary>Story time drawn: the clock, plus up to <see cref="EndTail"/> of wall time once the story has finished.</summary>
    private double DrawnTime()
    {
        if (!IsFinished || double.IsNaN(_finishedWall))
        {
            return Now;
        }
        return Now + Math.Clamp(WallTime - _finishedWall, 0, EndTail) * Speed;
    }

    private double StoryLength()
    {
        var timeline = _timeline!;
        return IsComparing ? Math.Max(timeline.MainEnd, timeline.CompareEnd) : timeline.MainEnd;
    }

    private void OnMainDone()
    {
        if (_resuming)
        {
            return;
        }
        var stats = _plan.Total(DraftCost);
        if (stats is null)
        {
            return;
        }
        Announce(Labels.Finished(stats.Tokens, stats.Passes, Math.Round(stats.Speedup, 1)));
        RaiseEvent(new SpeculativeDecodingCompletedEventArgs(CompletedEvent, this, stats));
    }

    // ---- the plan ---------------------------------------------------------------------------------------------------

    /// <summary>Plans the run again when its inputs changed (compared through a serialized copy). True when it changed.</summary>
    private bool EnsurePlan()
    {
        if (!_inputsDirty && _timeline is not null)
        {
            return false;
        }
        _inputsDirty = false;
        var rounds = EffectiveRounds();
        var tokens = rounds.Count > 0 ? Array.Empty<DecodeToken>() : EffectiveTokens();
        var signature = Signature(tokens, rounds);
        if (_timeline is not null && signature == _signature)
        {
            return false;
        }
        _signature = signature;
        _plan = rounds.Count > 0 ? DecodePlanner.Replay(rounds) : DecodePlanner.Simulate(tokens, DraftLength, Seed, DefaultEase);
        _timeline = new DecodeTimeline(_plan, DraftCost, PassDuration.TotalSeconds);
        PlannedStats = _plan.Total(DraftCost);
        _isReplay = rounds.Count > 0;
        PseudoClasses.Set(ReplayPseudoClass, _isReplay);
        _statsShown = -2;
        _outputShown = -1;
        _compareShown = -1;
        _summaryShown = -2;
        _statusKind = StatusKind.None;
        SyncStage();
        UpdateEnabled();
        return true;
    }

    private IReadOnlyList<DecodeRound> EffectiveRounds()
    {
        if (RoundsSource is { } source)
        {
            return source as IReadOnlyList<DecodeRound> ?? source.ToList();
        }
        return Rounds is { Count: > 0 } rounds ? rounds : Array.Empty<DecodeRound>();
    }

    private IReadOnlyList<DecodeToken> EffectiveTokens()
    {
        if (TokensSource is { } source)
        {
            return source as IReadOnlyList<DecodeToken> ?? source.ToList();
        }
        return Tokens is { Count: > 0 } tokens ? tokens : SpeculativeDecodingSamples.Tokens;
    }

    private string Signature(IReadOnlyList<DecodeToken> tokens, IReadOnlyList<DecodeRound> rounds)
    {
        var text = new StringBuilder();
        text.Append(DraftCost.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(PassDuration.Ticks).Append('|');
        if (rounds.Count > 0)
        {
            foreach (var round in rounds)
            {
                text.Append('\u0002').Append(round.Accepted).Append('\u0001').Append(round.TargetToken);
                foreach (var draft in round.Drafts)
                {
                    text.Append('\u0001').Append(draft);
                }
            }
            return text.ToString();
        }
        text.Append(DraftLength).Append('|').Append(Seed);
        foreach (var token in tokens)
        {
            text.Append('\u0002').Append(token.Text).Append('\u0001').Append(token.Ease?.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .Append('\u0001').Append(token.Miss);
        }
        return text.ToString();
    }

    /// <summary>The inputs changed: replan, and when the plan differs start over (playing on if it was playing).</summary>
    private void OnDataChanged(bool? play)
    {
        _inputsDirty = true;
        if (!EnsurePlan())
        {
            return;
        }
        _replanning = true;
        try
        {
            Rebuild(play ?? IsPlaying);
        }
        finally
        {
            _replanning = false;
        }
        UpdateScene();
    }

    // ---- compare ----------------------------------------------------------------------------------------------------

    private void OnComparingChanged()
    {
        PseudoClasses.Set(ComparingPseudoClass, IsComparing);
        SyncSwitch();
        _ = _compareOpen.AnimateTo(IsComparing ? 1 : 0, MotionSpec.Spring(ArcSpring.Smooth));
        if (_stage is not null)
        {
            _stage.Wall = WallTime;
            _stage.IsCompareLabel = IsComparing;
        }
        RequestFrames(0.3);
        _compareAnnounced = false;
        if (!_storyBuilt)
        {
            return;
        }
        EnsurePlan();
        var length = StoryLength();
        if (IsFinished && IsComparing && DrawnTime() < length)
        {
            // The speculative line is done but the target alone is not: the clock carries on from the moment on screen.
            // The base class cannot reopen a finished story, so rebuild it and run it back to here without side effects.
            var now = DrawnTime();
            _resuming = true;
            try
            {
                Rebuild(play: Status == SpeculativeDecodingStatus.Ready);
                Advance(now);
            }
            finally
            {
                _resuming = false;
            }
            _fadeTimeline = _timeline;
        }
        else
        {
            Script.Length = length;
            if (!IsFinished && Now >= length)
            {
                Advance(0);
            }
        }
        UpdateScene();
    }

    private void OnCompareOpenChanged(double value)
    {
        if (_stage is not null)
        {
            _stage.CompareOpen = value;
        }
    }

    // ---- the scene (a function of the story time) -------------------------------------------------------------------

    private bool ShowsSlots => Status != SpeculativeDecodingStatus.Ready || (Now <= 0 && !IsPlaying && !IsFinished);

    private void UpdateScene()
    {
        EnsurePlan();
        var timeline = _timeline!;
        var t = Now;
        var slots = ShowsSlots;
        if (_stage is not null)
        {
            _stage.ShowsSlots = slots;
            _stage.IsTravel = IsTravelEnabled;
            _stage.Wall = WallTime;
            _stage.Time = DrawnTime();
        }
        UpdateStatusLine(timeline, t, slots);
        UpdateStats(timeline, t, slots);
        UpdateSpeech(timeline, t, slots);
        var count = timeline.Plan.Tokens.Count;
        if (IsComparing && !slots && !_compareAnnounced && count > 0 && t >= timeline.CompareEnd)
        {
            _compareAnnounced = true;
            Announce(Labels.TargetOnlyFinished(count));
        }
    }

    private void UpdateStatusLine(DecodeTimeline timeline, double t, bool slots)
    {
        StatusKind kind;
        int a = 0, b = 0;
        if (Status == SpeculativeDecodingStatus.Loading)
        {
            kind = StatusKind.Loading;
        }
        else if (Status == SpeculativeDecodingStatus.Error)
        {
            kind = StatusKind.Error;
        }
        else if (slots || timeline.Count == 0)
        {
            kind = StatusKind.Ready;
            a = _isReplay ? _plan.Width : DraftLength;
        }
        else if (t >= timeline.MainEnd)
        {
            a = _plan.Passes;
            b = _plan.Tokens.Count;
            kind = !IsComparing ? StatusKind.Done : t >= timeline.CompareEnd ? StatusKind.CompareDone : StatusKind.SpeculationDone;
        }
        else
        {
            var r = Math.Max(0, timeline.RoundAt(t));
            var round = _plan.Rounds[r];
            a = round.Accepted;
            b = round.Count;
            if (round.Count == 0)
            {
                kind = StatusKind.LastToken;
            }
            else if (t < timeline.VerifyStart(r))
            {
                kind = StatusKind.Drafting;
            }
            else if (t < timeline.Settle(r))
            {
                kind = StatusKind.Verifying;
            }
            else
            {
                kind = round.IsBonus ? StatusKind.AllKept : round.Accepted == 0 ? StatusKind.FirstMissed : StatusKind.Kept;
            }
        }
        if (kind == _statusKind && a == _statusA && b == _statusB)
        {
            return;
        }
        _statusKind = kind;
        _statusA = a;
        _statusB = b;
        var labels = Labels;
        var (text, icon, tone, busy) = kind switch
        {
            StatusKind.Loading => (labels.Loading, null, ExplainerTone.Neutral, true),
            StatusKind.Error => (string.IsNullOrEmpty(ErrorText) ? labels.Error : ErrorText, ArcIcons.CircleAlert, ExplainerTone.Danger, false),
            StatusKind.Ready => (labels.Ready(a), null, ExplainerTone.Neutral, false),
            StatusKind.Drafting => (labels.Drafting(b), null, ExplainerTone.Neutral, false),
            StatusKind.Verifying => (labels.Verifying(b), null, ExplainerTone.Neutral, false),
            StatusKind.LastToken => (labels.LastToken, null, ExplainerTone.Neutral, false),
            StatusKind.Kept => (labels.Kept(a, b), null, ExplainerTone.Neutral, false),
            StatusKind.FirstMissed => (labels.FirstMissed, null, ExplainerTone.Neutral, false),
            StatusKind.AllKept => (labels.AllKept(b), null, ExplainerTone.Neutral, false),
            StatusKind.Done => (labels.Done(a), ArcIcons.Check, ExplainerTone.Success, false),
            StatusKind.SpeculationDone => (labels.SpeculationDone(a), ArcIcons.Check, ExplainerTone.Success, false),
            _ => (labels.CompareDone(a, b), (Avalonia.Media.Geometry?)ArcIcons.Check, ExplainerTone.Success, false)
        };
        _status?.Show(text, icon, tone, busy);
    }

    private void UpdateStats(DecodeTimeline timeline, double t, bool slots)
    {
        var settled = slots ? 0 : timeline.SettledAt(t);
        if (settled == _statsShown)
        {
            return;
        }
        _statsShown = settled;
        var stats = _plan.StatsAfter(settled, DraftCost);
        Stats = stats;
        var labels = Labels;
        if (stats is null)
        {
            SetStat(_speedup, null, labels.NotYet);
            SetStat(_acceptance, null, labels.NotYet);
            SetStat(_tokensPerPass, null, labels.NotYet);
            return;
        }
        var speedup = Math.Round(stats.Speedup, 1, MidpointRounding.AwayFromZero);
        var percent = (int)Math.Round(stats.Acceptance * 100, MidpointRounding.AwayFromZero);
        var perPass = Math.Round(stats.TokensPerPass, 1, MidpointRounding.AwayFromZero);
        SetStat(_speedup, speedup, labels.SpeedupSpoken(speedup));
        SetStat(_acceptance, percent, labels.AcceptanceSpoken(percent));
        SetStat(_tokensPerPass, perPass, labels.TokensPerPassSpoken(perPass));
    }

    private static void SetStat(SpeculativeDecodingStat? stat, double? value, string spoken)
    {
        if (stat is null)
        {
            return;
        }
        stat.Spoken = spoken;
        stat.SetCurrentValue(SpeculativeDecodingStat.HasValueProperty, value is not null);
        stat.SetCurrentValue(SpeculativeDecodingStat.ValueProperty, value ?? 0);
    }

    private void UpdateSpeech(DecodeTimeline timeline, double t, bool slots)
    {
        var tokens = _plan.Tokens;
        var written = 0;
        if (!slots)
        {
            while (written < tokens.Count && timeline.LandTime(written) <= t)
            {
                written++;
            }
        }
        if (written != _outputShown && _outputSpeech is not null)
        {
            _outputShown = written;
            _outputSpeech.Text = Labels.OutputName(Join(tokens, written));
        }
        var compared = IsComparing ? (slots ? 0 : timeline.CompareWritten(t)) : -1;
        if (compared != _compareShown && _compareSpeech is not null)
        {
            _compareShown = compared;
            _compareSpeech.Text = compared < 0 ? null : Labels.TargetOnlyName(Join(tokens, compared));
        }
        var summary = -1;
        if (!slots && t < timeline.MainEnd)
        {
            var r = timeline.RoundAt(t);
            if (r >= 0 && r < timeline.Count && t >= timeline.LastVerdict(r))
            {
                summary = r;
            }
        }
        if (summary != _summaryShown && _roundSpeech is not null)
        {
            _summaryShown = summary;
            _roundSpeech.Text = summary < 0 ? null : RoundSummary(_plan.Rounds[summary]);
        }
    }

    private string RoundSummary(DecodePlanRound round)
    {
        var labels = Labels;
        var number = round.Index + 1;
        if (round.Count == 0)
        {
            return labels.RoundTargetOnly(number, SpeculativeDecodingStage.Display(round.Target));
        }
        var drafts = string.Join(", ", round.Drafts.Select(SpeculativeDecodingStage.Display));
        return round.IsBonus
            ? labels.RoundAllKept(number, drafts, round.Count, SpeculativeDecodingStage.Display(round.Target))
            : labels.RoundCorrected(number, drafts, round.Accepted, SpeculativeDecodingStage.Display(round.Target),
                SpeculativeDecodingStage.Display(round.Missed ?? string.Empty));
    }

    private static string Join(IReadOnlyList<string> tokens, int count)
    {
        var text = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            text.Append(tokens[i]);
        }
        return text.ToString().Trim();
    }

    // ---- template and controls --------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_slider is not null)
        {
            _slider.ValueChanged -= OnSliderChanged;
            _slider.ValueCommitted -= OnSliderCommitted;
        }
        if (_switch is not null)
        {
            _switch.IsCheckedChanged -= OnSwitchChanged;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        if (_playButton is not null)
        {
            _playButton.Click -= OnPlayClicked;
        }
        if (_restartButton is not null)
        {
            _restartButton.Click -= OnRestartClicked;
        }
        _stage = e.NameScope.Find<SpeculativeDecodingStage>(StagePart);
        _status = e.NameScope.Find<SpeculativeDecodingStatusLine>(StatusPart);
        _slider = e.NameScope.Find<Slider>(DraftLengthPart);
        _switch = e.NameScope.Find<Switch>(ComparePart);
        _retry = e.NameScope.Find<AvaloniaButton>(RetryPart);
        _playButton = e.NameScope.Find<AvaloniaButton>(PlayButtonPart);
        _restartButton = e.NameScope.Find<AvaloniaButton>(RestartButtonPart);
        _speedup = e.NameScope.Find<SpeculativeDecodingStat>(SpeedupPart);
        _acceptance = e.NameScope.Find<SpeculativeDecodingStat>(AcceptancePart);
        _tokensPerPass = e.NameScope.Find<SpeculativeDecodingStat>(TokensPerPassPart);
        _roundSpeech = e.NameScope.Find<SpeculativeDecodingSpeech>(RoundSpeechPart);
        _outputSpeech = e.NameScope.Find<SpeculativeDecodingSpeech>(OutputSpeechPart);
        _compareSpeech = e.NameScope.Find<SpeculativeDecodingSpeech>(CompareSpeechPart);
        if (_slider is not null)
        {
            _slider.Minimum = MinDraftLength;
            _slider.Maximum = MaxDraftLength;
            _slider.SmallChange = 1;
            _slider.LargeChange = 1;
            _slider.ValueChanged += OnSliderChanged;
            _slider.ValueCommitted += OnSliderCommitted;
        }
        if (_switch is not null)
        {
            _switch.IsCheckedChanged += OnSwitchChanged;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        if (_playButton is not null)
        {
            _playButton.Click += OnPlayClicked;
        }
        if (_restartButton is not null)
        {
            _restartButton.Click += OnRestartClicked;
        }
        ApplyLabels();
        SyncSlider();
        SyncSwitch();
        SyncStage();
        UpdateEnabled();
        UpdateRetry();
        _statusKind = StatusKind.None;
        _statsShown = -2;
        _outputShown = -1;
        _compareShown = -1;
        _summaryShown = -2;
        UpdateScene();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            SetNarrow(availableSize.Width < NarrowWidth);
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        SetNarrow(e.NewSize.Width < NarrowWidth);
    }

    private void SetNarrow(bool narrow)
    {
        if (narrow == _isNarrow && PseudoClasses.Contains(NarrowPseudoClass) == narrow)
        {
            return;
        }
        _isNarrow = narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (_stage is not null)
        {
            _stage.IsNarrow = narrow;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TokensProperty)
        {
            Observe(ref _observedTokens, IsAttached ? Tokens : null);
            OnDataChanged(play: null);
        }
        else if (property == RoundsProperty)
        {
            Observe(ref _observedRounds, IsAttached ? Rounds : null);
            OnDataChanged(play: null);
        }
        else if (property == TokensSourceProperty || property == RoundsSourceProperty || property == SeedProperty ||
                 property == DraftCostProperty || property == PassDurationProperty)
        {
            OnDataChanged(play: null);
        }
        else if (property == DraftLengthProperty)
        {
            SyncSlider();
            OnDataChanged(play: _sliderDriven ? true : null);
        }
        else if (property == IsComparingProperty)
        {
            OnComparingChanged();
        }
        else if (property == DraftLabelProperty || property == DraftDetailProperty || property == TargetLabelProperty || property == TargetDetailProperty)
        {
            SyncStage();
        }
        else if (property == LabelsProperty)
        {
            ApplyLabels();
            _statusKind = StatusKind.None;
            _statsShown = -2;
            _outputShown = -1;
            _compareShown = -1;
            _summaryShown = -2;
            UpdateScene();
        }
        else if (property == StatusProperty || property == ErrorTextProperty)
        {
            OnStatusChanged();
        }
        else if (property == RetryCommandProperty)
        {
            UpdateRetry();
        }
        else if (property == TitleProperty)
        {
            Avalonia.Automation.AutomationProperties.SetName(this, Title);
        }
        else if (property == ArcMotion.LevelProperty)
        {
            UpdateScene();
        }
    }

    private protected override void WatchData(bool watch)
    {
        // A view model's lists can outlive the card: they are observed only while it is attached.
        Observe(ref _observedTokens, watch ? Tokens : null);
        Observe(ref _observedRounds, watch ? Rounds : null);
        if (watch)
        {
            // Catch up with changes made while nothing listened; the plan's signature tells whether anything changed.
            OnDataChanged(play: null);
        }
    }

    private void Observe<T>(ref AvaloniaList<T>? field, AvaloniaList<T>? list)
    {
        if (field is not null)
        {
            field.CollectionChanged -= OnCollectionChanged;
        }
        field = list;
        if (field is not null)
        {
            field.CollectionChanged += OnCollectionChanged;
        }
    }

    private void OnStatusChanged()
    {
        PseudoClasses.Set(LoadingPseudoClass, Status == SpeculativeDecodingStatus.Loading);
        PseudoClasses.Set(ErrorPseudoClass, Status == SpeculativeDecodingStatus.Error);
        UpdateEnabled();
        UpdateRetry();
        if (Status != SpeculativeDecodingStatus.Ready && IsPlaying)
        {
            _autoPlayPending = true;
            Pause();
        }
        else if (Status == SpeculativeDecodingStatus.Ready && _autoPlayPending)
        {
            _autoPlayPending = false;
            if (AutoPlay && IsTravelEnabled)
            {
                Play();
            }
        }
        _statusKind = StatusKind.None;
        UpdateScene();
    }

    private void UpdateEnabled()
    {
        var ready = Status == SpeculativeDecodingStatus.Ready;
        var playable = ready && _plan.Rounds.Count > 0;
        if (_playButton is not null)
        {
            _playButton.IsEnabled = playable;
        }
        if (_restartButton is not null)
        {
            _restartButton.IsEnabled = playable;
        }
        if (_slider is not null)
        {
            _slider.IsEnabled = ready;
        }
        if (_switch is not null)
        {
            _switch.IsEnabled = ready;
        }
    }

    private void UpdateRetry()
    {
        var visible = false;
        if (Status == SpeculativeDecodingStatus.Error)
        {
            if (RetryCommand is not null)
            {
                visible = true;
            }
            else
            {
                using var route = BuildEventRoute(RetryRequestedEvent);
                visible = route.HasHandlers;
            }
        }
        PseudoClasses.Set(RetryPseudoClass, visible);
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        if (_slider is not null)
        {
            _slider.Label = labels.DraftLength;
            _slider.ValueFormatter = value => labels.DraftLengthValue((int)Math.Round(value));
        }
        if (_switch is not null)
        {
            _switch.Content = labels.Compare;
        }
        if (_retry is not null)
        {
            _retry.Content = labels.Retry;
        }
        if (_speedup is not null)
        {
            _speedup.Label = labels.Speedup;
        }
        if (_acceptance is not null)
        {
            _acceptance.Label = labels.Acceptance;
        }
        if (_tokensPerPass is not null)
        {
            _tokensPerPass.Label = labels.TokensPerPass;
        }
        if (_stage is not null)
        {
            _stage.Labels = labels;
        }
    }

    private void SyncStage()
    {
        if (_stage is null)
        {
            return;
        }
        _stage.SetLanes(DraftLabel, DraftDetail, TargetLabel, TargetDetail);
        _stage.Timeline = _timeline;
        _stage.SlotCount = _isReplay ? _plan.Width : DraftLength;
        _stage.IsNarrow = _isNarrow;
        _stage.SetCompareLabel(IsComparing, animate: false);
        _stage.CompareOpen = _compareOpen.Value;
    }

    private void SyncSlider()
    {
        if (_slider is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _slider.Value = DraftLength;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncSwitch()
    {
        if (_switch is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _switch.IsChecked = IsComparing;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncing || _slider is null)
        {
            return;
        }
        var length = (int)Math.Round(_slider.Value);
        if (length == DraftLength)
        {
            return;
        }
        // The run replans and plays from the start on every step, also while dragging.
        _sliderDriven = true;
        try
        {
            SetCurrentValue(DraftLengthProperty, length);
        }
        finally
        {
            _sliderDriven = false;
        }
    }

    private void OnSliderCommitted(object? sender, SliderValueCommittedEventArgs e)
    {
        if (PlannedStats is not { } stats)
        {
            return;
        }
        Announce(Labels.DraftLengthChanged(DraftLength, (int)Math.Round(stats.Acceptance * 100, MidpointRounding.AwayFromZero),
            Math.Round(stats.Speedup, 1, MidpointRounding.AwayFromZero)));
    }

    private void OnSwitchChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _switch is null)
        {
            return;
        }
        var on = _switch.IsChecked == true;
        if (on == IsComparing)
        {
            return;
        }
        SetCurrentValue(IsComparingProperty, on);
        Announce(on ? Labels.CompareOn : Labels.CompareOff);
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(RetryRequestedEvent, this));
        if (RetryCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void OnPlayClicked(object? sender, RoutedEventArgs e)
    {
        // Runs after the base class has played or paused.
        var text = _pendingAnnouncement ?? (IsPlaying ? Labels.Playing : Labels.Paused);
        _pendingAnnouncement = null;
        Announce(text);
    }

    private void OnRestartClicked(object? sender, RoutedEventArgs e)
    {
        Announce(_pendingAnnouncement ?? Labels.Restarted);
        _pendingAnnouncement = null;
    }
}
