using Avalonia.Automation;
using Avalonia.Media;

namespace TKCode.Arc;

public partial class MixtureOfExperts
{
    private bool _reducedRun;
    private double _storyLength;
    private double _heldTotal;
    private double _lastNow;
    private int _nextRouted;
    private int _runs;
    private bool _sceneApplied;
    private MixtureOfExpertsScene? _scene;
    private (int Token, MixtureOfExpertsPhase Phase, MixtureOfExpertsInspectKind Kind, int Index, int Plan) _sceneKey = (-2, default, default, -1, -1);

    /// <summary>"Route again" turns the play button into a counter-clockwise arrow.</summary>
    private protected override Geometry ReplayIcon => ArcIcons.RotateCcw;

    /// <summary>The summary rests 3.6 s before a looping story starts again.</summary>
    private protected override double LoopDelay => MixtureOfExpertsTimeline.SummaryHold;

    /// <summary>The story's own time: the clock minus the time it was held still by an inspection.</summary>
    internal double SceneTime => Math.Max(0, Now - _heldTotal);

    /// <summary>True when this run uses the reduced-motion beats (1.8 s per token, no travel, no loop).</summary>
    internal bool IsReducedRun => _reducedRun;

    /// <summary>Packets show only in a full-motion run at full motion.</summary>
    internal bool IsTravelShown => !_reducedRun && IsTravelEnabled;

    /// <summary>The scene the card shows (tests).</summary>
    internal MixtureOfExpertsScene? Scene => _scene;

    /// <summary>The moment of the story the card is at.</summary>
    internal MixtureOfExpertsMoment Moment => MixtureOfExpertsTimeline.At(SceneTime, _plan.Routes.Length, _reducedRun);

    private protected override void BuildStory(ExplainerScript script)
    {
        EnsureData();
        // Reduced motion: the measured 1.8 s beats with no travel, and the story stops on its summary.
        _reducedRun = !IsTravelEnabled;
        CoerceValue(IsLoopingProperty);
        _storyLength = MixtureOfExpertsTimeline.Length(_plan.Routes.Length, _reducedRun);
        // The scene is a pure function of story time (an inspection can show any token at once), so the script holds
        // no beats: its length is the end of the routing, where the summary shows.
        script.Length = _storyLength;
    }

    private protected override void ResetScene()
    {
        _heldTotal = 0;
        _lastNow = 0;
        _nextRouted = 0;
        _runs++;
        if (_pinKind != MixtureOfExpertsInspectKind.None)
        {
            // A new run (a loop or a restart) releases a pin; its pause belonged to the old run.
            _pinKind = MixtureOfExpertsInspectKind.None;
            _pinIndex = -1;
            _resumeOnUnpin = false;
            UpdateInspect();
        }
        RefreshScene(force: true);
    }

    /// <summary>
    /// Every frame of the clock: hold the story still while something is inspected (the base clock moves on, so the held
    /// time is subtracted and the end pushed back), raise <see cref="Routed"/> for tokens that left the router, and move
    /// the scene and packets to the new moment. Packets run on story time so they pause, scale with Speed and hold for an
    /// inspection; the parts' fades run on wall time like CSS transitions.
    /// </summary>
    private protected override void OnTimeChanged()
    {
        ReleaseExpiredHover();
        var now = Now;
        var delta = now - _lastNow;
        _lastNow = now;
        if (delta > 0 && IsHeld)
        {
            _heldTotal += delta;
        }
        Script.Length = _storyLength + _heldTotal;
        RaiseRoutes();
        RefreshScene(force: false);
        var moment = Moment;
        _stage?.Packets.SetTime(moment.IsSummary ? -1 : moment.Token, moment.Local, WallTime);
        _stage?.Tick(WallTime);
        if (_sequence is not null)
        {
            foreach (var token in _sequence.Tokens)
            {
                token.Tick(WallTime);
            }
        }
        _loadBar?.Tick(WallTime);
    }

    private protected override void OnPlaybackChanged()
    {
        if (IsPlaying && _pinKind != MixtureOfExpertsInspectKind.None)
        {
            // Playing (the button, a binding) releases the pin.
            _pinKind = MixtureOfExpertsInspectKind.None;
            _pinIndex = -1;
            _resumeOnUnpin = false;
            UpdateInspect();
        }
    }

    /// <summary>Raises <see cref="Routed"/> for every token whose packets left the router since the last frame (while playing).</summary>
    private void RaiseRoutes()
    {
        var routes = _plan.Routes;
        var period = MixtureOfExpertsTimeline.PeriodOf(_reducedRun);
        var send = MixtureOfExpertsTimeline.SendTime(_reducedRun);
        var time = SceneTime;
        var live = IsPlaying || IsStepping;
        while (_nextRouted < routes.Length && time >= _nextRouted * period + send - 1e-9)
        {
            var route = routes[_nextRouted++];
            if (live)
            {
                RaiseEvent(new MixtureOfExpertsRoutedEventArgs(RoutedEvent, route));
            }
        }
    }

    /// <summary>
    /// Recomputes the scene when the token, its phase, what is inspected or the plan changed (or when forced), and moves
    /// every part to it: fading on wall time at full motion once the card has shown, at once otherwise.
    /// </summary>
    internal void RefreshScene(bool force)
    {
        var moment = Moment;
        var key = (moment.Token, moment.Phase, _inspectKind, _inspectIndex, _planVersion);
        if (!force && key == _sceneKey)
        {
            return;
        }
        _sceneKey = key;
        var plan = _plan;
        var scene = MixtureOfExpertsScene.Compute(plan, moment, _reducedRun, _inspectKind, _inspectIndex);
        _scene = scene;
        var wall = WallTime;
        var animate = _sceneApplied && IsTravelEnabled && _palette.IsReady;
        var loads = plan.LoadsAfter(scene.Counted);
        _stage?.Apply(plan, scene, wall, animate);
        if (_stage is not null)
        {
            var tiles = _stage.Tiles;
            for (var e = 0; e < tiles.Count && e < loads.Count; e++)
            {
                var load = loads[e];
                tiles[e].Apply(scene.Tiles[e], load, plan.CapacityFill(load, loads), !plan.IsUnlimited && load >= plan.Capacity, wall, animate);
            }
            _stage.Packets.SetShown(scene.PacketsVisible, wall, animate);
        }
        if (_sequence is not null)
        {
            var tokens = _sequence.Tokens;
            for (var t = 0; t < tokens.Count && t < scene.TokenLooks.Length; t++)
            {
                tokens[t].Apply(scene.TokenLooks[t], scene.TokenDim[t], wall, animate);
            }
        }
        _loadBar?.Apply(loads, scene.LoadLooks, wall, animate);
        var balance = plan.BalanceAfter(scene.Counted);
        LoadBalance = balance;
        if (_loadValue is not null && _loadEmpty is not null)
        {
            _loadValue.IsVisible = balance is not null;
            _loadEmpty.IsVisible = balance is null;
            if (balance is { } b)
            {
                _loadValue.Value = MixtureOfExpertsPlanner.Percent(b);
            }
        }
        if (_loadHead is not null)
        {
            var labels = Labels;
            AutomationProperties.SetName(_loadHead, balance is { } value
                ? labels.LoadBalanceSpoken(MixtureOfExpertsPlanner.Percent(value), plan.ExpertCount, plan.RoutesAfter(scene.Counted))
                : labels.LoadBalance + ": " + labels.NothingRouted);
        }
        CurrentTokenIndex = moment.IsSummary ? -1 : moment.Token;
        UpdateExpertNames(scene, moment);
        UpdateFooter(scene);
        if (animate)
        {
            RequestFrames(0.6);
        }
        _sceneApplied = _stage is not null;
    }
}
