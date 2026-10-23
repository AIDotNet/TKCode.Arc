using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

// The story of a PromptCache, on the explainer's story clock (it pauses, steps and scales with Speed).
//
// The script only ever holds the future: a pending list mirrors it, and Commit() rebuilds the script from that list, so
// the plan can change while the story runs (the change toggle re-plans the next request, a step skips a pause or starts a
// new round, a looping story continues after the cache expires). PlanFrom(cursor) plans every request left in the round
// from the state after the last one (or after the request in progress, whose beats are kept), then the idle drain and the
// expiry. A request's last beat ("end", once its numbers have rolled) is a stop, so stepping sends exactly one request.
public partial class PromptCache
{
    private static readonly Easing s_rollEasing = new CubicEaseOut();

    private sealed class Pending
    {
        public Pending(PromptCache owner, double at, Action act, bool stop, int group)
        {
            At = at;
            IsStop = stop;
            Group = group;
            Run = () =>
            {
                owner._pending.Remove(this);
                act();
            };
        }

        public double At { get; }

        public bool IsStop { get; }

        public int Group { get; }

        public Action Run { get; }
    }

    private sealed class RowState
    {
        public PromptCacheRequestPlan? Plan;
        public PromptCacheResult? Result;
        public bool Started;
        public bool Done;
        public ExplainerValue Processed;
        public ExplainerValue Cost;
        public ExplainerValue FirstToken;
        public double FadeFrom = double.NaN;
        public bool PendingReset;
    }

    private readonly record struct SimLeg(double T0, double T1, double S0, double S1, bool Smooth);

    private struct PlanCursor
    {
        public double ReadyAt;
        public double Sim;
        public bool Warm;
        public double LastUse;
        public int NextRow;
        public double Dwell;
    }

    private readonly List<Pending> _pending = new();
    private readonly List<SimLeg> _legs = new();
    private readonly List<PromptCacheResult> _results = new();
    private ExplainerScript? _script;
    private bool _isBuilt;
    private bool _building;

    // Data snapshot.
    private PromptCacheSegment[] _segments = [];
    private PromptCacheRequest[] _requests = [];
    private int[] _tokens = [];
    private double[] _grows = [];
    private double _messageGrow;
    private double[] _durations = [];
    private int _changeIndex = -1;
    private PromptCachePricing _pricing = new();
    private PromptCacheFirstTokenEstimator _firstToken = PromptCacheMath.DefaultFirstToken;
    private double _ttl = PromptCacheDefaults.Ttl;
    private bool _full = true;

    // Story state.
    private RowState[] _rows = [];
    private PlanCursor _cursor;
    private double _simBase;
    private double _roundOrigin;
    private bool _warm;
    private double _lastUse = double.NegativeInfinity;
    private int _activeGroup = -1;
    private int _groupSeed;
    private double[] _stepStarts = [];
    private double _lastPlannedEnd = double.NaN;
    private double _expiryAt = double.NaN;
    private bool _roundComplete;
    private bool _idleShown;
    private bool _releasingArm;

    // Cache panel.
    private bool[] _panelOpen = [];
    private double[] _panelCloseAt = [];
    private ExplainerValue _panelTokens;
    private double _ringDisplay = 1;
    private double _lastFrame;
    private PromptCacheRequestPlan? _panelBand;

    // Flight.
    private int _flightRow = -1;
    private int _flightFrom;
    private double _flightStart = double.NaN;

    // Statistics.
    private ExplainerValue _percent;
    private ExplainerValue _costSum;
    private ExplainerValue _uncachedSum;
    private ExplainerValue _speedup;
    private ExplainerValue _avgFirst;
    private ExplainerValue _avgUncached;
    private bool _hasCost;
    private bool _higher;
    private bool _hasHits;

    private string _caption = string.Empty;

    /// <summary>The story clock in seconds (tests).</summary>
    internal double StoryTime => Now;

    /// <summary>The simulated clock in seconds (tests).</summary>
    internal double SimulatedTime => SimAt(Now);

    /// <summary>The cache panel's ring: the share of the time to live that is shown as left (tests).</summary>
    internal double RingShown => 1 - _ringDisplay;

    /// <summary>The bar of a request row (tests).</summary>
    internal PromptCacheBar? RowBar(int row) => row >= 0 && row < _views.Length ? _views[row].Bar : null;

    /// <summary>The current caption (tests).</summary>
    internal string Caption => _caption;

    private protected override bool RestartKeepsPlayState => true;

    private protected override bool StepsPlayThrough => true;

    /// <summary>Story time for planning: 0 while the base rebuilds the story (its clock is reset right after).</summary>
    private double StoryNow => _building ? 0 : Now;

    private int SegmentCount => _segments.Length;

    private int Prefix => PromptCacheMath.Sum(_tokens);

    private double RollTime => _full ? PromptCacheTimeline.RollSeconds : 0;

    // ---- data -------------------------------------------------------------------------------------------------------

    private void SnapshotData()
    {
        _segments = EffectiveSegments().ToArray();
        _requests = EffectiveRequests().ToArray();
        _tokens = _segments.Select(s => Math.Max(0, s.Tokens)).ToArray();
        _grows = PromptCacheTimeline.Grows(_tokens);
        var maxMessage = _requests.Length == 0 ? 0 : _requests.Max(r => Math.Max(0, r.Tokens));
        _messageGrow = Math.Sqrt(maxMessage);
        if (_messageGrow <= 0)
        {
            _messageGrow = 1;
        }
        _durations = PromptCacheTimeline.SweepDurations(_grows, _messageGrow);
        var id = ChangeSegmentId;
        _changeIndex = id is null ? -1 : Array.FindIndex(_segments, s => s.Id == id);
        _pricing = Pricing ?? PromptCacheDefaults.CreatePricing();
        _firstToken = FirstTokenEstimator ?? PromptCacheMath.DefaultFirstToken;
        _ttl = Ttl;
        _rows = new RowState[_requests.Length];
        _stepStarts = new double[_requests.Length];
        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i] = new RowState();
        }
        _panelOpen = new bool[_segments.Length];
        _panelCloseAt = new double[_segments.Length];
        Array.Fill(_panelCloseAt, double.NaN);
    }

    private protected override void BuildStory(ExplainerScript script)
    {
        _script = script;
        _isBuilt = true;
        _rebuildQueued = false;
        _building = true;
        try
        {
            SnapshotData();
            _full = IsTravelEnabled;
            _pending.Clear();
            _legs.Clear();
            _results.Clear();
            _simBase = 0;
            _roundOrigin = 0;
            _warm = false;
            _lastUse = double.NegativeInfinity;
            _activeGroup = -1;
            _roundComplete = _rows.Length == 0;
            _idleShown = false;
            _flightRow = -1;
            _cursor = new PlanCursor
            {
                ReadyAt = 0,
                Sim = 0,
                Warm = false,
                LastUse = double.NegativeInfinity,
                NextRow = 0,
                Dwell = _full ? PromptCacheTimeline.OpeningSeconds : PromptCacheTimeline.ReducedOpening
            };
            PlanFrom(_cursor);
            Commit();
        }
        finally
        {
            _building = false;
        }
    }

    private protected override void ResetScene()
    {
        // The base resets the clock to 0 right after this; every time here is relative to that 0.
        var animate = _hasEverRun && IsPlaying && _full;
        _hasEverRun = true;
        if (IsChangeArmed)
        {
            _releasingArm = true;
            try
            {
                SetCurrentValue(IsChangeArmedProperty, false);
            }
            finally
            {
                _releasingArm = false;
            }
        }
        foreach (var row in _rows)
        {
            ResetRow(row);
        }
        for (var i = 0; i < _panelOpen.Length; i++)
        {
            var wasOpen = _panelOpen[i];
            _panelOpen[i] = false;
            _panelCloseAt[i] = animate && wasOpen ? (_panelOpen.Length - 1 - i) * PromptCacheTimeline.CloseStagger : double.NaN;
        }
        if (animate)
        {
            // Roll down from what is shown now; the clock restarts at 0.
            _panelTokens.Jump(_panelTokens.Value(_lastFrame));
            _panelTokens = Rolled(_panelTokens, 0, 0);
        }
        else
        {
            _panelTokens.Jump(0);
        }
        _panelBand = null;
        _ringDisplay = 1;
        _lastFrame = 0;
        _flightRow = -1;
        _flightStart = double.NaN;
        _hasCost = false;
        _hasHits = false;
        _higher = false;
        _percent.Jump(0);
        _costSum.Jump(0);
        _uncachedSum.Jump(0);
        _speedup.Jump(0);
        _avgFirst.Jump(0);
        _avgUncached.Jump(0);
        SetCacheState(PromptCacheState.Empty);
        SetCaption(Labels.Opening(PromptCacheMath.FormatTokens(Prefix)));
        RefreshAllViews();
    }

    private bool _hasEverRun;

    // ---- planning ---------------------------------------------------------------------------------------------------

    private Pending AddBeat(double at, Action act, bool stop = false, int group = -1)
    {
        var beat = new Pending(this, Math.Max(at, StoryNow), act, stop, group);
        _pending.Add(beat);
        return beat;
    }

    /// <summary>Rebuilds the script from the pending beats.</summary>
    private void Commit()
    {
        if (_script is null)
        {
            return;
        }
        _script.Clear();
        foreach (var p in _pending)
        {
            _script.At(p.At, p.Run, p.IsStop);
        }
        UpdateLength();
    }

    /// <summary>Sets the story's length for the base class (its end, and where a step aims when no stop is left).</summary>
    private void UpdateLength()
    {
        if (_script is null)
        {
            return;
        }
        var max = 0.0;
        foreach (var p in _pending)
        {
            max = Math.Max(max, p.At);
        }
        double length;
        if (!_roundComplete && !double.IsNaN(_lastPlannedEnd))
        {
            // The story's "end" is the round's last request, so opening on a finished round (reduced motion) lands there.
            length = _lastPlannedEnd;
        }
        else if (_full && !double.IsNaN(_expiryAt) && _pending.Count > 0)
        {
            // No stops while the cache idles: a step aims here and is re-aimed at a new round (AlignStep).
            length = _expiryAt + PromptCacheTimeline.LongestRequest + PromptCacheTimeline.RollSeconds + 0.5;
        }
        else
        {
            length = max;
        }
        _script.Length = _pending.Count == 0 ? 0 : length;
    }

    private double SimAt(double t)
    {
        var s = _simBase;
        foreach (var leg in _legs)
        {
            if (t < leg.T0)
            {
                break;
            }
            if (t >= leg.T1)
            {
                s = leg.S1;
                continue;
            }
            var p = leg.T1 > leg.T0 ? (t - leg.T0) / (leg.T1 - leg.T0) : 1;
            s = leg.S0 + (leg.S1 - leg.S0) * (leg.Smooth ? PromptCacheTimeline.SmoothStep(p) : p);
            break;
        }
        return s;
    }

    private void PlanFrom(PlanCursor c)
    {
        _full = IsTravelEnabled;
        _pending.RemoveAll(p => _activeGroup < 0 || p.Group != _activeGroup);
        _legs.Clear();
        _simBase = c.Sim;
        _lastPlannedEnd = double.NaN;
        _expiryAt = double.NaN;
        if (_rows.Length == 0 || SegmentCount == 0)
        {
            return;
        }
        var t = c.ReadyAt;
        var sim = c.Sim;
        var warm = c.Warm;
        var lastUse = c.LastUse;
        var armed = IsChangeArmed && _changeIndex >= 0;
        var dwell = c.Dwell;
        for (var row = c.NextRow; row < _rows.Length; row++)
        {
            double gapStart, start;
            var simAt = sim;
            if (row == 0)
            {
                start = t + dwell;
                gapStart = start;
            }
            else
            {
                gapStart = t + dwell;
                simAt = sim + Math.Max(0, _requests[row].Gap);
                start = _full ? gapStart + PromptCacheTimeline.GapSeconds : gapStart;
                _legs.Add(new SimLeg(gapStart, start, sim, simAt, true));
            }
            _stepStarts[row] = gapStart;
            var warmAt = warm && simAt - lastUse < _ttl;
            var (kind, brk) = PromptCacheMath.Classify(warmAt, armed, _changeIndex, SegmentCount);
            var plan = PromptCacheTimeline.Plan(kind, brk, _durations, start, _full);
            var result = Outcome(row, kind, brk, simAt, armed, !warmAt);
            AddRequestBeats(row, plan, result, armed, simAt);
            warm = true;
            lastUse = simAt;
            sim = simAt;
            t = plan.Done;
            armed = false;
            dwell = _full ? PromptCacheTimeline.DwellSeconds : PromptCacheTimeline.ReducedBetween;
            _lastPlannedEnd = plan.Done + RollTime;
        }
        if (!warm)
        {
            return;
        }
        if (_full)
        {
            var idleAt = t + PromptCacheTimeline.IdleSeconds;
            var remaining = Math.Max(0, _ttl - (sim - lastUse));
            var expiry = idleAt + PromptCacheTimeline.DrainTime(remaining, _ttl);
            _legs.Add(new SimLeg(idleAt, expiry, sim, sim + remaining, false));
            if (!_idleShown || idleAt > StoryNow)
            {
                AddBeat(idleAt, OnIdle);
            }
            _expiryAt = Math.Max(expiry, StoryNow);
            var at = _expiryAt;
            AddBeat(at, () => OnExpiry(at));
        }
        else
        {
            // Reduced motion: a step here sends a new round at once; playing on expires the cache after a pause.
            var stopAt = Math.Max(t + 0.05, StoryNow);
            AddBeat(stopAt, () => OnReducedIdleStop(stopAt), stop: true);
            var expiry = Math.Max(t + PromptCacheTimeline.ReducedIdle, StoryNow);
            _legs.Add(new SimLeg(expiry, expiry, sim, Math.Max(sim, lastUse + _ttl), false));
            _expiryAt = expiry;
            AddBeat(expiry, () => OnExpiry(expiry), stop: true);
        }
    }

    private PromptCacheResult Outcome(int row, PromptCacheRequestKind kind, int? brk, double simAt, bool changed, bool cold)
    {
        var request = _requests[row];
        var r = PromptCacheMath.Compute(_tokens, request.Tokens, kind, brk, _pricing, _firstToken);
        return new PromptCacheResult
        {
            Index = row,
            Time = Math.Max(0, simAt - _roundOrigin),
            Kind = r.Kind,
            BreakIndex = r.BreakIndex,
            BreakSegmentId = r.BreakIndex is { } k && k < SegmentCount ? _segments[k].Id : null,
            CachedTokens = r.CachedTokens,
            WrittenTokens = r.WrittenTokens,
            ProcessedTokens = r.ProcessedTokens,
            Cost = r.Cost,
            UncachedCost = r.UncachedCost,
            FirstToken = r.FirstToken,
            UncachedFirstToken = r.UncachedFirstToken,
            IsChanged = changed,
            IsCold = cold,
            Message = request.Message
        };
    }

    private void AddRequestBeats(int row, PromptCacheRequestPlan plan, PromptCacheResult result, bool consumesArm, double simAt)
    {
        var g = ++_groupSeed;
        AddBeat(plan.Start, () => OnRequestStart(row, plan, result, consumesArm, simAt, g), group: g);
        if (!double.IsNaN(plan.PanelWritingAt))
        {
            AddBeat(plan.PanelWritingAt, () => OnPanelWriting(plan), group: g);
        }
        if (plan.Kind == PromptCacheRequestKind.Hit)
        {
            AddBeat(plan.RefreshAt, () => OnRefresh(plan.RefreshAt), group: g);
        }
        if (_full && !double.IsNaN(plan.FlightStart))
        {
            AddBeat(plan.FlightStart, () => OnFlight(row, plan), group: g);
        }
        if (!double.IsNaN(plan.Land))
        {
            AddBeat(plan.Land, () => OnLand(plan), group: g);
        }
        AddBeat(plan.Done, () => OnRequestDone(row, plan, result), group: g);
        var end = plan.Done + RollTime;
        AddBeat(end, () => OnRequestEnd(g), stop: true, group: g);
    }

    /// <summary>Plans again from the state after the last request (the change toggle, the motion level).</summary>
    private void Replan()
    {
        if (!_isBuilt || _script is null || IsFinished)
        {
            return;
        }
        PlanFrom(_cursor);
        Commit();
    }

    // ---- beats ------------------------------------------------------------------------------------------------------

    private void OnRequestStart(int row, PromptCacheRequestPlan plan, PromptCacheResult result, bool consumesArm, double simAt, int group)
    {
        _activeGroup = group;
        CheckExpiry(SimAt(plan.Start));
        var r = _rows[row];
        ResetRow(r);
        r.Plan = plan;
        r.Result = result;
        r.Started = true;
        if (consumesArm && IsChangeArmed)
        {
            _releasingArm = true;
            try
            {
                SetCurrentValue(IsChangeArmedProperty, false);
            }
            finally
            {
                _releasingArm = false;
            }
        }
        _cursor = new PlanCursor
        {
            ReadyAt = plan.Done,
            Sim = simAt,
            Warm = true,
            LastUse = simAt,
            NextRow = row + 1,
            Dwell = _full ? PromptCacheTimeline.DwellSeconds : PromptCacheTimeline.ReducedBetween
        };
        var labels = Labels;
        switch (plan.Kind)
        {
            case PromptCacheRequestKind.Hit:
                SetCaption(labels.HitStarted(row + 1));
                SetRowStatus(row, labels.ReadingCache, null, default, busy: true);
                SetCacheState(PromptCacheState.Reading);
                _panelBand = plan;
                break;
            case PromptCacheRequestKind.Partial:
                SetCaption(labels.PartialStarted(SegmentLabel(plan.Break ?? 0), row + 1));
                SetRowStatus(row, labels.Processing, null, default, busy: true);
                break;
            default:
                SetCaption(labels.WriteStarted(row + 1, PromptCacheMath.FormatTokens(result.ProcessedTokens)));
                SetRowStatus(row, labels.Processing, null, default, busy: true);
                break;
        }
        RefreshRowText(row);
    }

    private void OnPanelWriting(PromptCacheRequestPlan plan)
    {
        SetCacheState(PromptCacheState.Writing);
        if (plan.Kind != PromptCacheRequestKind.Partial || plan.Break is not { } k)
        {
            return;
        }
        // The changed segment and everything after it leave the cache, the rightmost first.
        CloseSegments(k, plan.PanelWritingAt);
        _panelTokens = Rolled(_panelTokens, PromptCacheMath.Sum(_tokens, 0, k), plan.PanelWritingAt);
    }

    private void OnRefresh(double at)
    {
        _lastUse = SimAt(at);
        _warm = true;
    }

    private void OnFlight(int row, PromptCacheRequestPlan plan)
    {
        _flightRow = row;
        _flightFrom = plan.Kind == PromptCacheRequestKind.Partial ? plan.Break ?? 0 : 0;
        _flightStart = plan.FlightStart;
    }

    private void OnLand(PromptCacheRequestPlan plan)
    {
        _flightRow = -1;
        _flightStart = double.NaN;
        var hidden = CacheState is PromptCacheState.Empty or PromptCacheState.Expired || !_warm;
        var from = plan.Kind == PromptCacheRequestKind.Partial ? plan.Break ?? 0 : 0;
        for (var i = from; i < _panelOpen.Length; i++)
        {
            _panelOpen[i] = true;
            _panelCloseAt[i] = double.NaN;
        }
        _warm = true;
        _lastUse = SimAt(plan.Land);
        _panelTokens = Rolled(_panelTokens, Prefix, plan.Land);
        if (hidden)
        {
            // The first write fills the ring at once and fades it in; later refreshes chase back to full.
            _ringDisplay = 0;
        }
        SetCacheState(PromptCacheState.Warm);
    }

    private void OnRequestDone(int row, PromptCacheRequestPlan plan, PromptCacheResult result)
    {
        var r = _rows[row];
        r.Done = true;
        var at = plan.Done;
        PromptCacheResult? previous = row > 0 ? _rows[row - 1].Result : null;
        RollNumbers(r, previous, result, at);
        var labels = Labels;
        string caption;
        switch (result.Kind)
        {
            case PromptCacheRequestKind.Hit:
                SetRowStatus(row, labels.CacheHit, ArcIcons.Zap, DesignTokens.ArcDesignTokenKind.Success, busy: false);
                caption = labels.HitDone(PromptCacheMath.FormatTokens(result.CachedTokens),
                    PromptCacheMath.FormatRatio(Ratio(_pricing.CacheReadPrice)), PromptCacheMath.FormatTokens(_requests[row].Tokens));
                break;
            case PromptCacheRequestKind.Partial:
                SetRowStatus(row, labels.PartialHit, ArcIcons.Unlink, DesignTokens.ArcDesignTokenKind.Warning, busy: false);
                caption = labels.PartialDone(PromptCacheMath.FormatTokens(result.CachedTokens),
                    PromptCacheMath.FormatTokens(result.WrittenTokens), SegmentLabel(result.BreakIndex ?? 0));
                break;
            default:
                SetRowStatus(row, labels.CacheWrite, ArcIcons.PenLine, DesignTokens.ArcDesignTokenKind.Accent, busy: false);
                caption = labels.WriteDone(PromptCacheMath.DurationWords(_ttl), PromptCacheMath.FormatRatio(Ratio(_pricing.CacheWritePrice)));
                break;
        }
        if (_panelBand == plan)
        {
            _panelBand = null;
        }
        if (CacheState is PromptCacheState.Reading or PromptCacheState.Writing)
        {
            SetCacheState(PromptCacheState.Warm);
        }
        _results.Add(result);
        RollStats(at);
        SetCaption(caption);
        if (row == _rows.Length - 1)
        {
            _roundComplete = true;
            UpdateLength();
        }
        RefreshRowText(row);
        RaiseEvent(new PromptCacheRequestCompletedEventArgs(RequestCompletedEvent, this, result));
        AnnounceBeat(caption + " " + labels.Simulated);
    }

    private void OnRequestEnd(int group)
    {
        if (_activeGroup == group)
        {
            _activeGroup = -1;
        }
    }

    private void OnIdle()
    {
        _idleShown = true;
        SetCaption(Labels.Idle(PromptCacheMath.DurationWords(_ttl)));
    }

    private void OnReducedIdleStop(double at)
    {
        if (IsPlaying)
        {
            return;
        }
        // A step while the reduced-motion card rests on a finished round sends the next round's first request.
        StartNewRound(at, at);
    }

    private void OnExpiry(double at)
    {
        if (IsLooping && IsPlaying && !_full)
        {
            Expire(at, visual: true);
            _pending.RemoveAll(p => p.Group != _activeGroup || _activeGroup < 0);
            var next = at + PromptCacheTimeline.ReducedExpired;
            AddBeat(next, () => StartContinuation(next), stop: true);
            Commit();
            return;
        }
        if (IsLooping)
        {
            // Looping: the cache expires and the next round is already under way, its first request written at once.
            Expire(at, visual: !_full);
            StartContinuation(at);
            return;
        }
        Expire(at, visual: true, settle: true);
        _pending.Clear();
        Commit();
    }

    // ---- rounds -----------------------------------------------------------------------------------------------------

    private void BeginRound(double at, bool fade)
    {
        _roundComplete = _rows.Length == 0;
        _results.Clear();
        _idleShown = false;
        _activeGroup = -1;
        foreach (var row in _rows)
        {
            if (fade && row.Started)
            {
                row.FadeFrom = at;
                row.PendingReset = true;
            }
            else
            {
                ResetRow(row);
            }
        }
        for (var i = 0; i < _rows.Length; i++)
        {
            RefreshRowText(i);
        }
    }

    /// <summary>The next round after the cache expired: its first request is written at once (no sweep, no flight).</summary>
    private void StartContinuation(double at)
    {
        _pending.Clear();
        var simAt = SimAt(at);
        _legs.Clear();
        _simBase = simAt;
        BeginRound(at, fade: false);
        _roundOrigin = simAt;
        if (_rows.Length == 0 || SegmentCount == 0)
        {
            Commit();
            return;
        }
        var result = Outcome(0, PromptCacheRequestKind.Write, null, simAt, changed: false, cold: true);
        var plan = PromptCacheTimeline.Plan(PromptCacheRequestKind.Write, null, _durations, at, full: false);
        var r = _rows[0];
        r.Plan = plan;
        r.Result = result;
        r.Started = true;
        r.Done = true;
        r.Processed.Jump(result.ProcessedTokens);
        r.Cost.Jump(result.Cost);
        r.FirstToken.Jump(result.FirstToken);
        SetRowStatus(0, Labels.CacheWrite, ArcIcons.PenLine, DesignTokens.ArcDesignTokenKind.Accent, busy: false);
        for (var i = 0; i < _panelOpen.Length; i++)
        {
            _panelOpen[i] = true;
            _panelCloseAt[i] = double.NaN;
        }
        var wasHidden = CacheState is PromptCacheState.Empty or PromptCacheState.Expired;
        _warm = true;
        _lastUse = simAt;
        _panelTokens = Rolled(_panelTokens, Prefix, at);
        if (wasHidden && !_full)
        {
            _ringDisplay = 0;
        }
        SetCacheState(PromptCacheState.Warm);
        _results.Add(result);
        RollStats(at);
        SetCaption(Labels.WriteDone(PromptCacheMath.DurationWords(_ttl), PromptCacheMath.FormatRatio(Ratio(_pricing.CacheWritePrice))));
        _roundComplete = _rows.Length == 1;
        RefreshRowText(0);
        _cursor = new PlanCursor
        {
            ReadyAt = at,
            Sim = simAt,
            Warm = true,
            LastUse = simAt,
            NextRow = 1,
            Dwell = _full ? PromptCacheTimeline.ContinueSeconds : PromptCacheTimeline.ReducedBetween
        };
        PlanFrom(_cursor);
        Commit();
        RaiseEvent(new PromptCacheRequestCompletedEventArgs(RequestCompletedEvent, this, result));
    }

    /// <summary>A new round whose first request is sent at <paramref name="start"/> (a step after a finished round).</summary>
    private void StartNewRound(double now, double start)
    {
        var simNow = SimAt(now);
        CheckExpiry(simNow);
        _pending.Clear();
        _legs.Clear();
        _simBase = simNow;
        BeginRound(start, fade: _full);
        _roundOrigin = simNow;
        _cursor = new PlanCursor
        {
            ReadyAt = start,
            Sim = simNow,
            Warm = _warm,
            LastUse = _lastUse,
            NextRow = 0,
            Dwell = 0
        };
        PlanFrom(_cursor);
        Commit();
    }

    /// <summary>
    /// Runs when a step begins (full motion: the base plays the story to its next stop). A step sends the next request at
    /// once: a pause before it is skipped, and after a finished round it starts a new one. The base has already chosen the
    /// step's end (the next stop, or the story's length while the cache idles), so the story is moved so that the request
    /// ends exactly there.
    /// </summary>
    private void AlignStep()
    {
        if (!_isBuilt || _script is null || _activeGroup >= 0 || _rows.Length == 0 || SegmentCount == 0)
        {
            return;
        }
        var now = Now;
        if (!_roundComplete)
        {
            var next = _cursor.NextRow;
            if (next < _stepStarts.Length && now < _stepStarts[next])
            {
                Advance(_stepStarts[next] - now);
            }
            return;
        }
        var target = _script.NextStopTime() ?? _script.Length;
        if (target <= now)
        {
            return;
        }
        var simNow = SimAt(now);
        var warm = _warm && simNow - _lastUse < _ttl;
        var armed = IsChangeArmed && _changeIndex >= 0;
        var (kind, brk) = PromptCacheMath.Classify(warm, armed, _changeIndex, SegmentCount);
        var probe = PromptCacheTimeline.Plan(kind, brk, _durations, 0, _full);
        var start = Math.Max(now, target - (probe.Done + RollTime));
        StartNewRound(now, start);
        if (start > now)
        {
            Advance(start - now);
        }
    }

    // ---- cache ------------------------------------------------------------------------------------------------------

    private void CheckExpiry(double sim)
    {
        if (_warm && sim - _lastUse >= _ttl)
        {
            Expire(StoryNow, visual: true);
        }
    }

    private void Expire(double at, bool visual, bool settle = false)
    {
        var wasWarm = _warm;
        _warm = false;
        if (visual)
        {
            if (settle || !_full)
            {
                Array.Fill(_panelOpen, false);
                Array.Fill(_panelCloseAt, double.NaN);
                _panelTokens.Jump(0);
            }
            else
            {
                CloseSegments(0, at);
                _panelTokens = Rolled(_panelTokens, 0, at);
            }
            _panelBand = null;
            SetCacheState(PromptCacheState.Expired);
            SetCaption(Labels.ExpiredCaption);
        }
        if (wasWarm)
        {
            RaiseEvent(new RoutedEventArgs(ExpiredEvent, this));
        }
    }

    private void CloseSegments(int from, double at)
    {
        var n = _panelOpen.Length;
        for (var i = from; i < n; i++)
        {
            if (!_panelOpen[i])
            {
                continue;
            }
            _panelOpen[i] = false;
            _panelCloseAt[i] = _full ? at + (n - 1 - i) * PromptCacheTimeline.CloseStagger : double.NaN;
        }
    }

    private void SetCacheState(PromptCacheState state)
    {
        CacheState = state;
        PseudoClasses.Set(EmptyPseudoClass, state == PromptCacheState.Empty);
        PseudoClasses.Set(WritingPseudoClass, state == PromptCacheState.Writing);
        PseudoClasses.Set(ReadingPseudoClass, state == PromptCacheState.Reading);
        PseudoClasses.Set(WarmPseudoClass, state == PromptCacheState.Warm);
        PseudoClasses.Set(ExpiredPseudoClass, state == PromptCacheState.Expired);
        RefreshCacheStatus(force: true);
    }

    // ---- numbers ----------------------------------------------------------------------------------------------------

    private double Ratio(double price) => _pricing.InputPrice > 0 ? price / _pricing.InputPrice : 0;

    private string SegmentLabel(int index) => index >= 0 && index < SegmentCount ? _segments[index].Label : string.Empty;

    private ExplainerValue Rolled(ExplainerValue value, double to, double at)
    {
        if (_full)
        {
            value.Tween(to, at, PromptCacheTimeline.RollSeconds, s_rollEasing);
        }
        else
        {
            value.Jump(to);
        }
        return value;
    }

    private void RollNumbers(RowState r, PromptCacheResult? previous, PromptCacheResult result, double at)
    {
        r.Processed.Jump(previous?.ProcessedTokens ?? 0);
        r.Cost.Jump(previous?.Cost ?? 0);
        r.FirstToken.Jump(previous?.FirstToken ?? 0);
        r.Processed = Rolled(r.Processed, result.ProcessedTokens, at);
        r.Cost = Rolled(r.Cost, result.Cost, at);
        r.FirstToken = Rolled(r.FirstToken, result.FirstToken, at);
    }

    private void RollStats(double at)
    {
        var (cost, uncached, percent, higher) = PromptCacheMath.CostSummary(_results);
        var (hits, first, uncachedFirst, speedup) = PromptCacheMath.SpeedSummary(_results);
        if (!_hasCost)
        {
            _percent.Jump(0);
            _costSum.Jump(0);
            _uncachedSum.Jump(0);
        }
        _hasCost = _results.Count > 0;
        _higher = higher;
        _percent = Rolled(_percent, percent, at);
        _costSum = Rolled(_costSum, cost, at);
        _uncachedSum = Rolled(_uncachedSum, uncached, at);
        if (hits > 0)
        {
            if (!_hasHits)
            {
                _speedup.Jump(0);
                _avgFirst.Jump(first);
                _avgUncached.Jump(uncachedFirst);
            }
            _hasHits = true;
            _speedup = Rolled(_speedup, speedup, at);
            _avgFirst = Rolled(_avgFirst, first, at);
            _avgUncached = Rolled(_avgUncached, uncachedFirst, at);
        }
        else
        {
            _hasHits = false;
        }
        RefreshStatLabels();
    }

    private static void ResetRow(RowState row)
    {
        row.Plan = null;
        row.Result = null;
        row.Started = false;
        row.Done = false;
        row.Processed.Jump(0);
        row.Cost.Jump(0);
        row.FirstToken.Jump(0);
        row.FadeFrom = double.NaN;
        row.PendingReset = false;
    }

    // ---- the change toggle ------------------------------------------------------------------------------------------

    private void OnArmedChanged()
    {
        PseudoClasses.Set(ArmedPseudoClass, IsChangeArmed);
        SyncToggle();
        if (_releasingArm || !_isBuilt)
        {
            return;
        }
        if (_changeIndex < 0)
        {
            return;
        }
        var label = SegmentLabel(_changeIndex);
        var labels = Labels;
        var text = !IsChangeArmed
            ? labels.ChangeReleased(label)
            : _warm ? labels.ChangeArmed(label) : labels.ChangeArmedCold(label);
        SetCaption(text);
        Announce(text);
        Replan();
    }

    private void OnMotionLevelChanged()
    {
        if (_isBuilt && IsTravelEnabled != _full)
        {
            Replan();
        }
    }

    private protected override void OnPlaybackChanged()
    {
        if (_userToggledPlay && !IsPlaying && !IsFinished)
        {
            Announce(Labels.Paused);
        }
        _userToggledPlay = false;
    }

    private bool _userToggledPlay;
}
