using Avalonia;
using Avalonia.Automation;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class TicketRouter
{
    private readonly List<TicketRouterLaneView> _laneViews = [];
    private readonly List<RoutedTicket> _history = [];
    private readonly Dictionary<string, int> _turns = new(StringComparer.Ordinal);
    private List<TicketLane>? _defaultLanes;
    private LiveLoop? _loop;
    private TicketRouterPhase _phase = TicketRouterPhase.Empty;
    private double _phaseTime;
    private int _next;
    private TicketMessage? _current;
    private TicketClassification? _classification;
    private TicketLane? _target;
    private TicketLane? _suggested;
    private CancellationTokenSource? _cancel;
    private int _classifyRun;
    private bool _classifyPending;
    private bool _reclassify;
    private bool _flightDone;
    private int _swapRun;
    private SharedElementScope? _stageFlight;

    /// <summary>Where the flow is with the current ticket.</summary>
    internal TicketRouterPhase Phase => _phase;

    /// <summary>The message on the stage, if any.</summary>
    internal TicketMessage? CurrentMessage => _current;

    /// <summary>The current ticket's classification, once read.</summary>
    internal TicketClassification? CurrentClassification => _classification;

    /// <summary>The lane the current ticket goes to (the review lane for low confidence).</summary>
    internal TicketLane? TargetLane => _target;

    /// <summary>Tests drive the flow by hand: the frame loop never starts.</summary>
    internal bool ManualClock { get; set; }

    /// <summary>True while the ticket's row flies from the stage into its lane.</summary>
    internal bool IsFlying => _stageFlight?.IsActive == true;

    /// <summary>True while a classifier call is running.</summary>
    internal bool IsClassifying => _classifyPending;

    /// <summary>True while the simulation's inbox plays (the app has set no tickets).</summary>
    internal bool ShowsSimulation => Tickets is null && Simulation is not null;

    /// <summary>The lanes on screen: yours, else the simulation's while it plays, else the default lanes.</summary>
    internal IReadOnlyList<TicketLane> EffectiveLanes =>
        Lanes is { Count: > 0 } lanes ? lanes as IReadOnlyList<TicketLane> ?? [.. lanes]
        : ShowsSimulation && Simulation!.Lanes is { Count: > 0 } simulated ? simulated as IReadOnlyList<TicketLane> ?? [.. simulated]
        : _defaultLanes ??= DefaultLanes(Labels);

    /// <summary>True when neither the app nor the simulation gives lanes.</summary>
    private bool UsesDefaultLanes => Lanes is not { Count: > 0 } && !(ShowsSimulation && Simulation!.Lanes is { Count: > 0 });

    /// <summary>The classifier's name: <see cref="Model"/>, else the simulation's while it plays.</summary>
    internal string? EffectiveModel => Model ?? (ShowsSimulation ? Simulation!.Model : null);

    private IList<TicketMessage> EffectiveTickets => Tickets ?? Simulation?.Tickets ?? [];

    private bool EffectiveLoop => Loop ?? ShowsSimulation;

    /// <summary>Billing, Engineering, Success (the lanes <see cref="DefaultRoute"/> picks) and Needs review, with no assignees.</summary>
    private static List<TicketLane> DefaultLanes(TicketRouterLabels labels) =>
    [
        new TicketLane(BillingLaneId, labels.BillingLane),
        new TicketLane(EngineeringLaneId, labels.EngineeringLane),
        new TicketLane(SuccessLaneId, labels.SuccessLane),
        new TicketLane(ReviewLaneId, labels.NeedsReview) { IsReview = true },
    ];

    /// <summary>Tries the classification again after a failure.</summary>
    public void Retry()
    {
        if (_phase != TicketRouterPhase.Failed || _current is null)
        {
            return;
        }
        StartClassify();
    }

    /// <summary>Sends a ticket whose classification failed to the review lane, unclassified.</summary>
    public void SendToReview()
    {
        if (_phase != TicketRouterPhase.Failed || _current is null)
        {
            return;
        }
        _classification = null;
        _target = ReviewLane() ?? FirstTeamLane();
        _suggested = null;
        ShowError(false);
        StartRouting();
    }

    /// <summary>
    /// Routes a ticket from the review lane to its suggested lane (the first team lane when it has none) with the next
    /// assignee there, as a click on the ticket does. Returns false when the ticket is not waiting for review.
    /// </summary>
    public bool ConfirmReview(RoutedTicket ticket)
    {
        var lanes = EffectiveLanes;
        var from = lanes.FirstOrDefault(l => l.Id == ticket.LaneId);
        if (!_history.Contains(ticket) || from is not { IsReview: true })
        {
            return false;
        }
        var lane = lanes.FirstOrDefault(l => l.Id == ticket.SuggestedLaneId && !l.IsReview) ?? FirstTeamLane();
        if (lane is null)
        {
            return false;
        }
        var routed = new RoutedTicket(ticket.Message, ticket.Classification, lane.Id, NextAssignee(lane));
        var index = _history.IndexOf(ticket);
        _history.RemoveAt(index);
        _history.Insert(0, routed);
        var source = ViewOf(from);
        var target = ViewOf(lane);
        SyncLanes(animate: true);
        Fly(source, target, routed, stage: false);
        RaiseEvent(new TicketReviewConfirmedEventArgs(ReviewConfirmedEvent, routed, lane.Id));
        Announce(Labels.AnnounceConfirmed(ticket.Message.Subject, lane.Label, routed.Assignee));
        return true;
    }

    /// <summary>Starts over: the lanes go back to <see cref="InitialRouted"/> and the queue to its first ticket.</summary>
    public void Restart()
    {
        CancelClassifier(restart: false);
        _stageFlight?.Reset();
        _stageFlight = null;
        _history.Clear();
        _turns.Clear();
        _next = 0;
        _current = null;
        _classification = null;
        _target = null;
        _suggested = null;
        IEnumerable<RoutedTicket>? initial = (IEnumerable<RoutedTicket>?)InitialRouted ?? (ShowsSimulation ? Simulation!.Routed : null);
        if (initial is not null)
        {
            _history.AddRange(initial.Take(HistoryCap));
        }
        _connector?.Clear();
        if (_ticket is null)
        {
            _phase = TicketRouterPhase.Empty;
            return;
        }
        SyncLanes(animate: false);
        Next(instant: true);
        RefreshTexts();
    }

    /// <summary>Runs the flow forward by <paramref name="seconds"/> of wall time (tests, and each frame).</summary>
    internal void Step(double seconds)
    {
        if (IsPaused || seconds <= 0)
        {
            return;
        }
        _phaseTime += seconds * Speed;
        switch (_phase)
        {
            case TicketRouterPhase.Arriving when _phaseTime >= TicketRouterMotion.Arrive:
                StartClassify();
                break;
            case TicketRouterPhase.Classifying when Classifier is null && _phaseTime >= TicketRouterMotion.Classify:
                OnClassified(_current?.Sample);
                break;
            case TicketRouterPhase.Labeled when _phaseTime >= TicketRouterMotion.Labeled:
                StartRouting();
                break;
            case TicketRouterPhase.Routing when _phaseTime >= TicketRouterMotion.Pulse:
                Land();
                break;
            case TicketRouterPhase.Landing when _phaseTime >= TicketRouterMotion.Landing && _flightDone:
                Next();
                break;
        }
    }

    private void Enter(TicketRouterPhase phase)
    {
        _phase = phase;
        _phaseTime = 0;
        UpdateStatus();
        UpdateLoop();
    }

    /// <summary>Takes the next ticket from the queue, or rests in "All caught up" (or "No tickets yet").</summary>
    private void Next(bool instant = false)
    {
        var tickets = EffectiveTickets;
        if (_next >= tickets.Count && EffectiveLoop && tickets.Count > 0)
        {
            _next = 0;
        }
        if (_next >= tickets.Count)
        {
            _current = null;
            _classification = null;
            Enter(tickets.Count == 0 ? TicketRouterPhase.Empty : TicketRouterPhase.CaughtUp);
            ShowNotice(instant);
            if (_phase == TicketRouterPhase.CaughtUp && !instant)
            {
                Announce(Labels.CaughtUp);
            }
            return;
        }
        _current = tickets[_next++];
        _classification = null;
        _target = null;
        _suggested = null;
        Enter(TicketRouterPhase.Arriving);
        HideLabels();
        ShowError(false);
        if (_classifying is not null)
        {
            _classifying.IsVisible = false;
            _classifying.IsShimmering = false;
        }
        _ = SwapTicketAsync(_current, instant);
    }

    /// <summary>The ticket crossfade: the old card out over 100ms, the new one in over 160ms with a small rise.</summary>
    private async Task SwapTicketAsync(TicketMessage message, bool instant)
    {
        var run = ++_swapRun;
        if (_ticketFade is null || _ticket is null)
        {
            return;
        }
        if (!instant && _ticketFade.Opacity > 0 && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            await _ticketFade.Out(TicketRouterMotion.SwapOut);
            if (run != _swapRun)
            {
                return;
            }
        }
        // The last flight's source stays hidden until the card shows a new ticket.
        _stageFlight?.Reset();
        _stageFlight = null;
        _ticket.FlightKey = null;
        _ticket.Show(message, Labels);
        _ticket.IsVisible = true;
        if (_notice is not null)
        {
            _notice.IsVisible = false;
        }
        if (instant)
        {
            _ticketFade.Set(true);
        }
        else
        {
            await _ticketFade.In(TicketRouterMotion.SwapIn, TimeSpan.Zero, TicketRouterMotion.Rise);
        }
    }

    private void ShowNotice(bool instant)
    {
        _swapRun++;
        HideLabels();
        ShowError(false);
        if (_classifying is not null)
        {
            _classifying.IsVisible = false;
            _classifying.IsShimmering = false;
        }
        if (_notice is null || _ticket is null || _ticketFade is null)
        {
            return;
        }
        _stageFlight?.Reset();
        _stageFlight = null;
        _ticket.IsVisible = false;
        _notice.IsVisible = true;
        var caughtUp = _phase == TicketRouterPhase.CaughtUp;
        _notice.Glyph = caughtUp ? ArcIcons.CheckCheck : ArcIcons.Inbox;
        _notice.Text = caughtUp ? Labels.CaughtUp : Labels.Empty;
        _notice.Hint = caughtUp ? Labels.CaughtUpHint : Labels.EmptyHint;
        if (instant)
        {
            _ticketFade.Set(true);
        }
        else
        {
            _ = _ticketFade.In(TicketRouterMotion.SwapIn, TimeSpan.Zero, TicketRouterMotion.Rise);
        }
    }

    private void StartClassify()
    {
        if (_current is null)
        {
            return;
        }
        Enter(TicketRouterPhase.Classifying);
        ShowError(false);
        HideLabels();
        if (_classifying is not null)
        {
            _classifying.Text = ClassifyingLabel();
            _classifying.IsVisible = true;
            _classifying.IsShimmering = true;
        }
        if (Classifier is { } classifier && !IsPaused)
        {
            _ = ClassifyAsync(classifier, _current);
        }
        else if (Classifier is not null)
        {
            _reclassify = true;
        }
    }

    private async Task ClassifyAsync(Func<TicketMessage, CancellationToken, Task<TicketClassification>> classifier, TicketMessage message)
    {
        CancelClassifier(restart: false);
        var cancel = new CancellationTokenSource();
        _cancel = cancel;
        var run = ++_classifyRun;
        _classifyPending = true;
        _reclassify = false;
        TicketClassification? result = null;
        Exception? error = null;
        try
        {
            result = await classifier(message, cancel.Token);
            if (result is null)
            {
                error = new InvalidOperationException("The classifier returned no classification.");
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            error = ex;
        }
        if (Dispatcher.UIThread.CheckAccess())
        {
            Finish();
        }
        else
        {
            Dispatcher.UIThread.Post(Finish);
        }

        void Finish()
        {
            if (run != _classifyRun || cancel.IsCancellationRequested || !ReferenceEquals(_current, message))
            {
                return;
            }
            _classifyPending = false;
            _cancel = null;
            cancel.Dispose();
            if (error is not null)
            {
                OnFailed(message, error);
            }
            else
            {
                OnClassified(result);
            }
        }
    }

    /// <summary>Cancels a running classifier call; with <paramref name="restart"/> it runs again when the flow resumes.</summary>
    private void CancelClassifier(bool restart)
    {
        if (_cancel is { } cancel)
        {
            _cancel = null;
            _classifyRun++;
            cancel.Cancel();
            cancel.Dispose();
        }
        if (_classifyPending)
        {
            _classifyPending = false;
            _reclassify = restart;
        }
    }

    private void OnClassified(TicketClassification? classification)
    {
        if (_current is null)
        {
            return;
        }
        // A sample-less message with no classifier is read as an unsure question.
        classification ??= new TicketClassification(TicketIntent.Question, TicketSentiment.Neutral, 0);
        _classification = classification;
        var lane = ResolveLane(_current, classification);
        var review = ReviewLane();
        if (classification.Confidence < ReviewThreshold && review is not null)
        {
            _target = review;
            _suggested = lane;
        }
        else
        {
            _target = lane;
            _suggested = null;
        }
        Enter(TicketRouterPhase.Labeled);
        if (_classifying is not null)
        {
            _classifying.IsShimmering = false;
            _classifying.IsVisible = false;
        }
        UpdateStageLabels();
        for (var i = 0; i < _labelFades.Length; i++)
        {
            _ = _labelFades[i].In(TicketRouterMotion.LabelFade, TimeSpan.FromTicks(TicketRouterMotion.Stagger.Ticks * i), 2);
        }
    }

    private void OnFailed(TicketMessage message, Exception error)
    {
        Enter(TicketRouterPhase.Failed);
        if (_classifying is not null)
        {
            _classifying.IsShimmering = false;
            _classifying.IsVisible = false;
        }
        HideLabels();
        ShowError(true);
        RaiseEvent(new TicketClassificationFailedEventArgs(ClassificationFailedEvent, message, error));
        Announce(Labels.AnnounceFailed(message.Subject));
    }

    private void StartRouting()
    {
        Enter(TicketRouterPhase.Routing);
        var lanes = EffectiveLanes;
        var index = _target is null ? -1 : IndexOf(lanes, _target);
        _connector?.Pulse(index);
    }

    /// <summary>The ticket lands: it joins its lane's history, its row enters and flies there from the stage.</summary>
    private void Land()
    {
        if (_current is not { } message || _target is not { } lane)
        {
            Next();
            return;
        }
        var routed = lane.IsReview
            ? new RoutedTicket(message, _classification, lane.Id, null, _suggested?.Id)
            : new RoutedTicket(message, _classification, lane.Id, NextAssignee(lane));
        _history.Insert(0, routed);
        if (_history.Count > HistoryCap)
        {
            _history.RemoveRange(HistoryCap, _history.Count - HistoryCap);
        }
        Enter(TicketRouterPhase.Landing);
        _flightDone = false;
        if (_ticket is not null)
        {
            _ticket.FlightKey = routed.Key;
        }
        SyncLanes(animate: true);
        Fly(_stage, ViewOf(lane), routed, stage: true);
        RaiseEvent(new TicketRoutedEventArgs(TicketRoutedEvent, routed));
        if (lane.IsReview)
        {
            var percent = _classification is null ? Labels.Unclassified : Labels.Percent(_classification.Confidence);
            Announce(Labels.AnnounceReview(message.Subject, percent, _suggested?.Label ?? lane.Label));
        }
        else
        {
            Announce(Labels.AnnounceRouted(message.Subject, lane.Label, routed.Assignee));
        }
    }

    /// <summary>
    /// Flies the new row of <paramref name="ticket"/> from its piece in <paramref name="source"/> (the stage's subject line,
    /// or the ticket's row in the review lane) to its place in <paramref name="target"/> on a position spring
    /// (<see cref="SharedElementScope"/>, Position mode: the row keeps its size and its top-left travels), then lights it.
    /// Reduced motion and Off drop the travel; the landing highlight stays with reduced motion.
    /// </summary>
    private void Fly(Avalonia.Visual? source, TicketRouterLaneView? target, RoutedTicket ticket, bool stage)
    {
        var row = target?.RowOf(ticket);
        if (row is null || target is null)
        {
            _flightDone = true;
            return;
        }
        row.Highlight();
        if (source is null || !IsLoaded || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            if (stage)
            {
                _flightDone = true;
                HideStageSubject(source, target);
            }
            return;
        }
        // Place the new row now, so the flight starts from its first frame.
        UpdateLayout();
        var scope = new SharedElementScope(source, target);
        if (stage)
        {
            _stageFlight?.Reset();
            _stageFlight = scope;
        }
        _ = FlyAsync(scope, stage);
    }

    private void HideStageSubject(Avalonia.Visual? source, TicketRouterLaneView target)
    {
        if (source is null)
        {
            return;
        }
        // Without travel the subject line still leaves the stage as the row appears.
        UpdateLayout();
        var scope = new SharedElementScope(source, target);
        _stageFlight?.Reset();
        _stageFlight = scope;
        scope.HideSource();
    }

    private async Task FlyAsync(SharedElementScope scope, bool stage)
    {
        await scope.FlyAsync(1, MotionSpec.Spring(TicketRouterMotion.Travel));
        if (stage)
        {
            if (ReferenceEquals(scope, _stageFlight) || _stageFlight is null)
            {
                _flightDone = true;
            }
        }
        else
        {
            scope.Dispose();
        }
    }

    private void HideLabels()
    {
        foreach (var fade in _labelFades)
        {
            fade.Set(false);
        }
    }

    private void ShowError(bool shown)
    {
        if (_error is not null)
        {
            _error.IsVisible = shown;
        }
    }

    /// <summary>Puts the current classification into the strip's chips, the meter and the route target.</summary>
    private void UpdateStageLabels()
    {
        var labels = Labels;
        if (_classification is not { } c)
        {
            return;
        }
        if (_intent is not null)
        {
            _intent.Glyph = TicketRouterGlyphs.Intent(c.Intent);
            _intent.Text = labels.Intent(c.Intent);
        }
        if (_sentiment is not null)
        {
            _sentiment.Glyph = TicketRouterGlyphs.Sentiment(c.Sentiment);
            _sentiment.Text = labels.Sentiment(c.Sentiment);
            _sentiment.Tone = c.Sentiment switch
            {
                TicketSentiment.Positive => TicketRouterTone.Success,
                TicketSentiment.Negative => TicketRouterTone.Danger,
                _ => TicketRouterTone.Neutral,
            };
        }
        var percent = labels.Percent(c.Confidence);
        if (_confidence is not null)
        {
            _confidence.Value = c.Confidence;
            _confidence.Text = percent;
            _confidence.IsLow = c.Confidence < ReviewThreshold;
            AutomationProperties.SetName(_confidence, labels.Confident(percent));
        }
        if (_routeChip is not null && _target is { } target)
        {
            if (target.IsReview)
            {
                _routeChip.Glyph = ArcIcons.CircleHelp;
                _routeChip.Text = _suggested is null ? labels.NeedsReview : labels.NeedsReview + " · " + labels.Suggested(_suggested.Label);
                _routeChip.Tone = TicketRouterTone.Warning;
            }
            else
            {
                _routeChip.Glyph = ArcIcons.ArrowRight;
                _routeChip.Text = target.Label;
                _routeChip.Tone = TicketRouterTone.Accent;
            }
        }
    }

    private void OnPausedChanged()
    {
        if (IsPaused)
        {
            CancelClassifier(restart: true);
            if (_classifying is not null)
            {
                _classifying.IsShimmering = false;
            }
            Announce(Labels.AnnouncePaused);
        }
        else
        {
            if (_phase == TicketRouterPhase.Classifying)
            {
                if (_classifying is not null)
                {
                    _classifying.IsShimmering = true;
                }
                if (_reclassify && Classifier is { } classifier && _current is not null)
                {
                    _ = ClassifyAsync(classifier, _current);
                }
            }
            Announce(Labels.AnnounceResumed);
        }
        UpdateStatus();
        UpdateLoop();
    }

    /// <summary>One frame loop runs the flow while there is timed work; it waits off screen and when unloaded.</summary>
    private void UpdateLoop()
    {
        if (ManualClock)
        {
            return;
        }
        var wants = !IsPaused && _phase is TicketRouterPhase.Arriving or TicketRouterPhase.Classifying or TicketRouterPhase.Labeled
            or TicketRouterPhase.Routing or TicketRouterPhase.Landing;
        if (wants)
        {
            if (_loop is null)
            {
                _loop = new LiveLoop(this, dt =>
                {
                    Step(dt);
                    return true;
                })
                { MinimumLevel = ArcMotionLevel.Off };
                _loop.RunningChanged += OnLoopRunningChanged;
            }
            _loop.Start();
        }
        else
        {
            _loop?.Stop();
        }
    }

    /// <summary>Off screen or unloaded the loop waits: a running classifier call is cancelled and runs again on return.</summary>
    private void OnLoopRunningChanged(bool running)
    {
        if (!running && _loop?.IsActive == true)
        {
            CancelClassifier(restart: true);
        }
        else if (running && _reclassify && !IsPaused && _phase == TicketRouterPhase.Classifying && Classifier is { } classifier
                 && _current is not null)
        {
            _ = ClassifyAsync(classifier, _current);
        }
    }

    /// <summary>Simulates the frame loop's gate closing (off screen) for tests.</summary>
    internal void SimulateOffScreen(bool offScreen)
    {
        if (offScreen)
        {
            CancelClassifier(restart: true);
        }
        else if (_reclassify && _phase == TicketRouterPhase.Classifying && Classifier is { } classifier && _current is not null)
        {
            _ = ClassifyAsync(classifier, _current);
        }
    }

    private void BuildLanes()
    {
        foreach (var view in _laneViews)
        {
            view.ReviewPressed -= OnReviewPressed;
        }
        _laneViews.Clear();
        if (_lanes is null)
        {
            return;
        }
        _lanes.Children.Clear();
        var lanes = EffectiveLanes;
        foreach (var lane in lanes)
        {
            var view = new TicketRouterLaneView(lane) { Label = lane.Label, IsReview = lane.IsReview };
            view.ReviewPressed += OnReviewPressed;
            _laneViews.Add(view);
            _lanes.Children.Add(view);
        }
        if (_connector is not null)
        {
            _connector.LaneCount = lanes.Count;
        }
    }

    /// <summary>Brings every lane in line with the history: its visible rows (newest first), its count and its name.</summary>
    private void SyncLanes(bool animate, bool relabel = false)
    {
        if (_laneViews.Count == 0)
        {
            return;
        }
        var visible = _narrow ? Math.Min(MaxVisible, 2) : MaxVisible;
        animate = animate && IsLoaded;
        foreach (var view in _laneViews)
        {
            var lane = view.Lane;
            var tickets = _history.Where(t => t.LaneId == lane.Id).ToList();
            var count = lane.Count + tickets.Count;
            view.VisibleRows = visible;
            view.Label = lane.Label;
            view.Count = count;
            AutomationProperties.SetName(view, Labels.LaneName(lane.Label, count));
            if (relabel)
            {
                view.Sync([], animate: false, CreateRow);
            }
            view.Sync(tickets.Take(visible).ToList(), animate, CreateRow);
        }
    }

    private TicketRouterRow CreateRow(RoutedTicket ticket)
    {
        var labels = Labels;
        var lanes = EffectiveLanes;
        var lane = lanes.FirstOrDefault(l => l.Id == ticket.LaneId);
        var row = new TicketRouterRow(ticket) { Subject = ticket.Message.Subject };
        if (lane is { IsReview: true })
        {
            var suggested = lanes.FirstOrDefault(l => l.Id == ticket.SuggestedLaneId && !l.IsReview) ?? FirstTeamLane();
            var percent = ticket.Classification is { } c ? labels.Percent(c.Confidence) : labels.Unclassified;
            var to = suggested?.Label ?? string.Empty;
            row.IsReview = true;
            row.Meta = ticket.Classification is null ? labels.Unclassified + " → " + to : percent + " → " + to;
            AutomationProperties.SetName(row, labels.ReviewButton(ticket.Message.Subject, percent, to));
        }
        else
        {
            row.Assignee = ticket.Assignee;
            var intent = ticket.Classification is { } c ? labels.Intent(c.Intent) : labels.Unclassified;
            row.Meta = intent + " · " + ticket.Message.Customer;
            AutomationProperties.SetName(row, ticket.Assignee is null
                ? ticket.Message.Subject
                : ticket.Message.Subject + ", " + ticket.Assignee);
        }
        return row;
    }

    private TicketRouterLaneView? ViewOf(TicketLane lane) => _laneViews.FirstOrDefault(v => ReferenceEquals(v.Lane, lane))
        ?? _laneViews.FirstOrDefault(v => v.Lane.Id == lane.Id);

    private TicketLane ResolveLane(TicketMessage message, TicketClassification classification)
    {
        var id = (Route ?? DefaultRoute)(message, classification);
        var lanes = EffectiveLanes;
        return lanes.FirstOrDefault(l => l.Id == id && !l.IsReview) ?? FirstTeamLane() ?? lanes[0];
    }

    private TicketLane? FirstTeamLane() => EffectiveLanes.FirstOrDefault(l => !l.IsReview);

    private TicketLane? ReviewLane() => EffectiveLanes.FirstOrDefault(l => l.IsReview);

    private static int IndexOf(IReadOnlyList<TicketLane> lanes, TicketLane lane)
    {
        for (var i = 0; i < lanes.Count; i++)
        {
            if (ReferenceEquals(lanes[i], lane) || lanes[i].Id == lane.Id)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The lane's next assignee, round robin.</summary>
    private string? NextAssignee(TicketLane lane)
    {
        if (lane.Assignees.Count == 0)
        {
            return null;
        }
        _turns.TryGetValue(lane.Id, out var turn);
        _turns[lane.Id] = turn + 1;
        return lane.Assignees[turn % lane.Assignees.Count];
    }
}
