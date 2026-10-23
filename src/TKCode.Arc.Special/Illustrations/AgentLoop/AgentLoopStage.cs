using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The agent loop's stage: the ring column (drawn ring, station labels, center, lanes) and the context stack, laid out in
/// real pixels from one <see cref="AgentLoopGeometry"/> solved at the measured width, so the drawn nodes and pins and the
/// real controls beside them always agree. From a 640px container the stack sits in a column to the right; narrower it
/// moves under the ring (two block columns from 460px). It shows the owner's <see cref="AgentLoopScene"/>: each beat
/// calls one of the <c>On…</c> methods (animated or in place), and a new template or layout shows the same moment with
/// <see cref="Sync"/>.
/// </summary>
internal sealed class AgentLoopStage : Control
{
    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<AgentLoopStage, IBrush?>(nameof(AccentInk));

    internal static readonly StyledProperty<IBrush?> BorderInkProperty =
        AvaloniaProperty.Register<AgentLoopStage, IBrush?>(nameof(BorderInk));

    internal static readonly StyledProperty<IBrush?> DangerInkProperty =
        AvaloniaProperty.Register<AgentLoopStage, IBrush?>(nameof(DangerInk));

    private static readonly Easing s_tokenEasing = ArcEasings.Standard;
    private readonly AgentLoopRing _ring = new();
    private readonly AgentLoopStationLabel[] _labels = new AgentLoopStationLabel[3];
    private readonly AgentLoopCenter _center = new();
    private readonly AgentLoopLane _checkLane = new();
    private readonly AgentLoopLane _callLane = new();
    private readonly TextBlock _contextHeading;
    private readonly TextBlock _tokensText;
    private readonly AgentLoopContextList _list = new();
    private readonly Dictionary<int, AgentLoopBlock> _blocks = new();
    private AgentLoop? _owner;
    private IReadOnlyList<AgentLoopIteration> _run = Array.Empty<AgentLoopIteration>();
    private AgentLoopGeometry? _geometry;
    private AgentLoopArrangement _arrangement;
    private bool _compact;
    private bool _laidOut;
    private AgentLoopInk _ink = AgentLoopInk.Fallback;
    private IPen? _dividerPen;
    private ExplainerValue _tokens;
    private long _tokensKey = -1;
    private Rect _divider;

    public AgentLoopStage()
    {
        _labels[0] = new AgentLoopStationLabel { Icon = ArcIcons.Layers };
        _labels[1] = new AgentLoopStationLabel { Icon = ArcIcons.Wrench };
        _labels[2] = new AgentLoopStationLabel { Icon = ArcIcons.ListChecks, IconAfter = true };
        _contextHeading = AgentLoopText.Line(14, 19.6, ArcDesignTokenKind.Foreground, FontWeight.Medium);
        _tokensText = AgentLoopText.Line(12, 16.8, ArcDesignTokenKind.TextMuted);
        _tokensText.FontFeatures = AgentLoopText.Tabular;
        _tokensText.TextTrimming = TextTrimming.None;
        Add(_ring);
        foreach (var label in _labels)
        {
            Add(label);
        }
        Add(_center);
        Add(_checkLane);
        Add(_callLane);
        Add(_contextHeading);
        Add(_tokensText);
        Add(_list);
        ClipToBounds = false;
    }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    public IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }

    public IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }

    /// <summary>The drawn layer (tests read it).</summary>
    internal AgentLoopRing Ring => _ring;

    /// <summary>The center (tests read it).</summary>
    internal AgentLoopCenter Center => _center;

    /// <summary>The station labels, Gather, Act, Verify (tests read them).</summary>
    internal IReadOnlyList<AgentLoopStationLabel> Labels => _labels;

    /// <summary>The check lane (the single lane on compact cards) and the tool call lane (tests read them).</summary>
    internal (AgentLoopLane Checks, AgentLoopLane Calls) Lanes => (_checkLane, _callLane);

    /// <summary>The context blocks (tests read them).</summary>
    internal AgentLoopContextList ContextList => _list;

    /// <summary>The context total as shown (tests read it).</summary>
    internal string? TokensText => _tokensText.Text;

    /// <summary>The arrangement at the last layout.</summary>
    internal AgentLoopArrangement Arrangement => _arrangement;

    /// <summary>The geometry at the last layout.</summary>
    internal AgentLoopGeometry? Geometry => _geometry;

    private AgentLoopLabels Words => _owner?.Labels ?? s_defaultLabels;

    private static readonly AgentLoopLabels s_defaultLabels = new();

    private double Wall => _owner?.Wall ?? 0;

    private void Add(Control child)
    {
        VisualChildren.Add(child);
        LogicalChildren.Add(child);
    }

    // ---- owner ----------------------------------------------------------------------------------------------------

    /// <summary>Connects the stage to its card and shows the card's current moment.</summary>
    public void Attach(AgentLoop owner)
    {
        _owner = owner;
        SetRun(owner.Run, owner.Timeline);
        UpdateModel();
        UpdateLabels();
        Sync();
    }

    /// <summary>A new run: reserves its context slots and gives the ring its timeline.</summary>
    public void SetRun(IReadOnlyList<AgentLoopIteration> run, AgentLoopTimeline timeline)
    {
        _run = run;
        _list.Slots = Math.Max(1, run.Count);
        _ring.SetTimeline(timeline);
        InvalidateMeasure();
    }

    public void UpdateModel() => _center.SetModel(_owner?.Model);

    /// <summary>Re-reads every word (station names, placeholders, the context heading).</summary>
    public void UpdateLabels()
    {
        var words = Words;
        _labels[0].StationName = words.Gather;
        _labels[1].StationName = words.Act;
        _labels[2].StationName = words.Verify;
        _contextHeading.Text = words.Context;
        _list.Placeholder = words.ContextPlaceholder;
        AutomationProperties.SetName(_list, words.ContextList);
        UpdatePlaceholders();
        _tokensKey = -1;
        ShowTokens(Wall);
        if (_owner is not null)
        {
            ShowCenterLine(animate: false);
        }
        InvalidateMeasure();
    }

    private void UpdatePlaceholders()
    {
        var words = Words;
        _checkLane.Placeholder = _compact ? words.LanePlaceholder : words.ChecksPlaceholder;
        _callLane.Placeholder = words.CallsPlaceholder;
    }

    /// <summary>Shows the scene as it is now, without animation (a new template, a new layout mode).</summary>
    public void Sync()
    {
        if (_owner is null)
        {
            return;
        }
        var scene = _owner.Scene;
        var wall = Wall;
        // Lanes and blocks start over in place, then show what the scene has.
        _checkLane.Clear(wall, false);
        _callLane.Clear(wall, false);
        if (_compact)
        {
            if (scene.LatestChip is { } latest)
            {
                _checkLane.Show(latest, Words, wall, false);
            }
        }
        else
        {
            if (scene.CheckChip is { } check)
            {
                _checkLane.Show(check, Words, wall, false);
            }
            if (scene.CallChip is { } call)
            {
                _callLane.Show(call, Words, wall, false);
            }
        }
        _list.Clear(wall, false);
        _blocks.Clear();
        for (var i = 0; i < scene.Blocks && i < _run.Count; i++)
        {
            _blocks[i] = _list.Add(i, _run[i].Gather, Words, wall, false);
        }
        _tokens.Jump(scene.Tokens);
        _tokensKey = -1;
        ShowTokens(wall);
        _ring.SetNodes(scene, wall, false);
        _ring.SetHead(scene.HeadVisible, wall, false);
        _ring.SetPinShown(AgentLoopStation.Act, scene.CallChip is not null, wall);
        _ring.SetPinShown(AgentLoopStation.Verify, scene.CheckChip is not null, wall);
        _ring.SetDone(scene.Ending == AgentLoopEnding.Done, wall, false);
        ApplyStations();
        ShowCenterLine(animate: false);
        Tick(_owner.StoryTime, wall);
    }

    // ---- beats ----------------------------------------------------------------------------------------------------

    /// <summary>Back to Ready: chips and blocks fade out, the placeholders and the empty total come back, the ring rests.</summary>
    public void ResetScene(bool animate)
    {
        if (_owner is null)
        {
            return;
        }
        var wall = Wall;
        var scene = _owner.Scene;
        _checkLane.Clear(wall, animate);
        _callLane.Clear(wall, animate);
        _list.Clear(wall, animate);
        _blocks.Clear();
        SetTokens(0, wall, animate);
        _ring.SetNodes(scene, wall, animate);
        _ring.SetHead(false, wall, animate);
        _ring.ClearPins(wall, animate);
        _ring.SetDone(false, wall, animate);
        ApplyStations();
        ShowCenterLine(animate);
        Tick(_owner.StoryTime, wall);
    }

    /// <summary>The stations changed state: labels, nodes, pins and chips follow.</summary>
    public void OnStations(bool animate)
    {
        if (_owner is null)
        {
            return;
        }
        _ring.SetNodes(_owner.Scene, Wall, animate);
        ApplyStations();
    }

    /// <summary>A new loop: the counter at the center rolls.</summary>
    public void OnLoop(bool animate) => ShowCenterLine(animate);

    /// <summary>The head appears or fades.</summary>
    public void OnHead(bool animate)
    {
        if (_owner is not null)
        {
            _ring.SetHead(_owner.Scene.HeadVisible, Wall, animate);
        }
    }

    /// <summary>The block of loop <paramref name="loop"/> joined: it drops in and the total counts up over 500ms.</summary>
    public void OnBlock(int loop, bool animate)
    {
        if (_owner is null || loop >= _run.Count)
        {
            return;
        }
        var wall = Wall;
        _blocks[loop] = _list.Add(loop, _run[loop].Gather, Words, wall, animate);
        SetTokens(_owner.Scene.Tokens, wall, animate);
        ApplyStations();
    }

    /// <summary>A chip dropped: into its lane (or the single lane), with its pin drawn down first.</summary>
    public void OnChip(AgentLoopChipState chip, bool animate)
    {
        var wall = Wall;
        var lane = _compact || chip.Station == AgentLoopStation.Verify ? _checkLane : _callLane;
        if (_compact)
        {
            lane.OriginX = PinX(chip.Station, lane);
            _ring.PinStation = chip.Station;
        }
        lane.Show(chip, Words, wall, animate);
        _ring.DrawPin(chip.Station, wall, animate);
        ApplyStations();
    }

    /// <summary>A call resolved: its spinner and "Calling …" crossfade to the result.</summary>
    public void OnResolved(AgentLoopChipState chip, bool animate)
    {
        foreach (var lane in new[] { _checkLane, _callLane })
        {
            if (lane.Current is { } control && control.State == chip)
            {
                control.Resolve(animate);
            }
        }
        ApplyStations();
    }

    /// <summary>The run ended: the ring draws in (done) or stays neutral (stopped), and the center shows the ending.</summary>
    public void OnEnding(bool animate)
    {
        if (_owner is null)
        {
            return;
        }
        _ring.SetDone(_owner.Scene.Ending == AgentLoopEnding.Done, Wall, animate);
        ApplyStations();
        ShowCenterLine(animate);
    }

    /// <summary>Moves everything that follows a clock to story time <paramref name="story"/> and wall time <paramref name="wall"/>.</summary>
    public void Tick(double story, double wall)
    {
        _ring.Update(story, wall, _owner?.Animates ?? false);
        _checkLane.Update(wall);
        _callLane.Update(wall);
        _list.Update(wall);
        ShowTokens(wall);
    }

    private void SetTokens(long total, double wall, bool animate)
    {
        if (animate)
        {
            _tokens.Tween(total, wall, 0.5, s_tokenEasing);
        }
        else
        {
            _tokens.Jump(total);
        }
        ShowTokens(wall);
    }

    private void ShowTokens(double wall)
    {
        var value = Math.Max(0, _tokens.Value(wall));
        // Only format when the shown text can change: whole tokens below 1,000, tenths of a thousand above.
        var key = value < 999.5 ? (long)Math.Round(value) : 1_000_000 + (long)Math.Round(value / 100);
        if (key == _tokensKey)
        {
            return;
        }
        _tokensKey = key;
        var words = Words;
        _tokensText.Text = words.Tokens(words.FormatTokens(value));
    }

    private void ShowCenterLine(bool animate)
    {
        if (_owner is null)
        {
            return;
        }
        var scene = _owner.Scene;
        var words = Words;
        var loops = _run.Count;
        if (scene.Ending == AgentLoopEnding.Done)
        {
            _center.ShowEnding(_compact ? words.DoneInShort(loops) : words.DoneIn(loops), stopped: false, animate);
        }
        else if (scene.Ending == AgentLoopEnding.Stopped)
        {
            _center.ShowEnding(_compact ? words.StoppedAfterShort(loops) : words.StoppedAfter(loops), stopped: true, animate);
        }
        else if (scene.Loop >= 0)
        {
            _center.ShowCounter(words.LoopCounter, scene.Loop + 1, animate);
        }
        else
        {
            _center.ShowText(words.Ready, animate);
        }
    }

    /// <summary>Labels, chips and the fresh block follow the stations' states.</summary>
    private void ApplyStations()
    {
        if (_owner is null)
        {
            return;
        }
        var scene = _owner.Scene;
        for (var i = 0; i < 3; i++)
        {
            _labels[i].State = scene[(AgentLoopStation)i];
        }
        var active = scene.ActiveStation;
        foreach (var lane in new[] { _checkLane, _callLane })
        {
            if (lane.Current is not { } chip)
            {
                continue;
            }
            var look = scene.Ending != AgentLoopEnding.None || !scene.HasStarted
                ? AgentLoopChipLook.Rest
                : active == chip.State.Station ? AgentLoopChipLook.Active : AgentLoopChipLook.Dim;
            chip.SetLook(look, _ink);
        }
        foreach (var (index, block) in _blocks)
        {
            block.SetFresh(index == scene.FreshBlock, _ink);
        }
    }

    // ---- palette --------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentInkProperty || change.Property == BorderInkProperty || change.Property == DangerInkProperty)
        {
            var border = (BorderInk as ISolidColorBrush)?.Color ?? Color.FromRgb(0xE6, 0xE6, 0xE6);
            var accent = (AccentInk as ISolidColorBrush)?.Color ?? Color.FromRgb(0x1F, 0x7D, 0xCF);
            var danger = (DangerInk as ISolidColorBrush)?.Color ?? Color.FromRgb(0xD9, 0x2D, 0x20);
            _ink = new AgentLoopInk(border, accent, danger);
            _dividerPen = new ImmutablePen(new ImmutableSolidColorBrush(border), 1);
            ApplyStations();
            InvalidateVisual();
        }
    }

    // ---- layout ---------------------------------------------------------------------------------------------------

    private double PinX(AgentLoopStation station, AgentLoopLane lane)
    {
        if (_geometry is not { } geometry)
        {
            return 0;
        }
        var lanes = geometry.Lanes;
        var laneLeft = ReferenceEquals(lane, _callLane) && !_compact ? lanes.Calls.X : lanes.Checks.X;
        return geometry.NodeOf(station).X - laneLeft;
    }

    private void SetCompact(bool compact)
    {
        if (_compact == compact && _laidOut)
        {
            return;
        }
        var changed = _compact != compact;
        _compact = compact;
        foreach (var label in _labels)
        {
            label.IsCompact = compact;
        }
        _center.IsCompact = compact;
        _callLane.IsVisible = !compact;
        UpdatePlaceholders();
        if (changed && _laidOut)
        {
            // Two lanes become one (or back): show the same chips in the new lanes, in place.
            _ring.PinStation = compact ? _owner?.Scene.LatestChip?.Station : null;
            Sync();
        }
        else if (!compact)
        {
            _ring.PinStation = null;
        }
        else
        {
            _ring.PinStation = _owner?.Scene.LatestChip?.Station;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 694;
        _arrangement = AgentLoopGeometry.ArrangementFor(width);
        var ringWidth = AgentLoopGeometry.RingColumnWidth(width);
        SetCompact(AgentLoopGeometry.IsCompactColumn(ringWidth));
        _laidOut = true;
        var geometry = AgentLoopGeometry.Solve(ringWidth, _labels[1].NaturalWidth, _labels[2].NaturalWidth);
        _geometry = geometry;
        _ring.SetGeometry(geometry);

        var lineHeight = _compact ? 16.8 : 20;
        _labels[0].Measure(new Size(ringWidth, lineHeight));
        _labels[1].Measure(new Size(geometry.LabelRoom(AgentLoopStation.Act), lineHeight));
        _labels[2].Measure(new Size(geometry.LabelRoom(AgentLoopStation.Verify), lineHeight));
        _center.MaxNameWidth = Math.Max(40, 2 * geometry.Radius - 28);
        _center.Measure(new Size(2 * geometry.Radius, 2 * geometry.Radius));
        _ring.Measure(new Size(ringWidth, geometry.LaneTop));
        var (checks, calls) = geometry.Lanes;
        _checkLane.Measure(checks.Size);
        _callLane.Measure(calls.Size);
        _checkLane.OriginX = PinX(_compact ? _ring.PinStation ?? AgentLoopStation.Verify : AgentLoopStation.Verify, _checkLane);
        _callLane.OriginX = PinX(AgentLoopStation.Act, _callLane);

        var contextWidth = ContextContentWidth(width);
        _list.Columns = _arrangement == AgentLoopArrangement.StackedTwoColumns ? 2 : 1;
        _contextHeading.Measure(new Size(contextWidth, 19.8));
        _tokensText.Measure(new Size(contextWidth, 19.8));
        _list.Measure(new Size(contextWidth, double.PositiveInfinity));
        var contextHeight = AgentLoopGeometry.ContextHeight(_list.Rows);
        var height = _arrangement == AgentLoopArrangement.Wide
            ? Math.Max(geometry.ColumnHeight, contextHeight)
            : geometry.ColumnHeight + AgentLoopGeometry.StackGap + 1 + AgentLoopGeometry.StackInset + contextHeight;
        return new Size(width, height);
    }

    private double ContextContentWidth(double width) => _arrangement == AgentLoopArrangement.Wide
        ? AgentLoopGeometry.ContextColumnWidth - AgentLoopGeometry.ContextInset - 1
        : width;

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_geometry is not { } geometry)
        {
            return finalSize;
        }
        var ringWidth = geometry.Width;
        _ring.Arrange(new Rect(0, 0, ringWidth, geometry.LaneTop));

        // Station labels beside their nodes: Gather centered above, Take action to the right, Verify to the left.
        var gather = geometry.NodeOf(AgentLoopStation.Gather);
        var act = geometry.NodeOf(AgentLoopStation.Act);
        var verify = geometry.NodeOf(AgentLoopStation.Verify);
        var lineHeight = _compact ? 16.8 : 20;
        var g = _labels[0].DesiredSize;
        _labels[0].Arrange(new Rect(gather.X - g.Width / 2, gather.Y - geometry.LabelGap - lineHeight, g.Width, lineHeight));
        var a = _labels[1].DesiredSize;
        _labels[1].Arrange(new Rect(act.X + geometry.LabelGap, act.Y - lineHeight / 2, a.Width, lineHeight));
        var v = _labels[2].DesiredSize;
        _labels[2].Arrange(new Rect(verify.X - geometry.LabelGap - v.Width, verify.Y - lineHeight / 2, v.Width, lineHeight));

        var c = _center.DesiredSize;
        _center.Arrange(new Rect(geometry.Center.X - c.Width / 2, geometry.Center.Y - c.Height / 2, c.Width, c.Height));

        var (checks, calls) = geometry.Lanes;
        _checkLane.Arrange(checks);
        _callLane.Arrange(_compact ? new Rect(calls.X, calls.Y, 0, 0) : calls);

        double contextX;
        double contextY;
        var contextWidth = ContextContentWidth(finalSize.Width);
        if (_arrangement == AgentLoopArrangement.Wide)
        {
            var border = ringWidth + AgentLoopGeometry.ColumnGap;
            _divider = new Rect(border + 0.5, 0, 0, finalSize.Height);
            contextX = border + 1 + AgentLoopGeometry.ContextInset;
            contextY = 0;
        }
        else
        {
            var border = geometry.ColumnHeight + AgentLoopGeometry.StackGap;
            _divider = new Rect(0, border + 0.5, finalSize.Width, 0);
            contextX = 0;
            contextY = border + 1 + AgentLoopGeometry.StackInset;
        }
        var heading = _contextHeading.DesiredSize;
        _contextHeading.Arrange(new Rect(contextX, contextY, Math.Min(heading.Width, contextWidth), 19.8));
        var tokens = _tokensText.DesiredSize;
        // Baseline-aligned with the heading: the 12px line sits 1.6 lower than the 14px one.
        _tokensText.Arrange(new Rect(contextX + contextWidth - tokens.Width, contextY + 1.5, tokens.Width, 16.8));
        var listTop = contextY + AgentLoopGeometry.ContextHeadingHeight + AgentLoopGeometry.ContextHeadingGap;
        _list.Arrange(new Rect(contextX, listTop, contextWidth, AgentLoopGeometry.BlocksHeight(_list.Rows)));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_dividerPen is null || _geometry is null)
        {
            return;
        }
        context.DrawLine(_dividerPen, _divider.TopLeft, _divider.BottomRight);
    }
}
