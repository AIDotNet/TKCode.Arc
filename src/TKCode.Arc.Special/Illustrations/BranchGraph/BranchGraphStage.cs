using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The commit list of a <see cref="BranchGraph"/>: one 44 px row per commit, newest on top, with the lanes drawn in the
/// gutter on the left (straight lines in a lane, S-bends where a branch leaves and where it merges back, dots for commits
/// and rings for merges), the message with its branch pills, and the author, hash and date columns; plus the detail card
/// of the inspected commit. It animates between the four previews of a <see cref="BranchGraphModel"/>: every edge is
/// computed each frame from the live positions of its two commits, so lines stretch and bend as the commits travel.
/// The frames come from one <see cref="LiveLoop"/> that runs only while something moves (the choreography of a pick is
/// staged per commit with lifts, arcs and rings a ControlTheme transition cannot express) and only at full motion;
/// reduced motion and Off show each state at once. One tab stop: Up / Down / Home / End move through the commits,
/// Escape closes the card.
/// </summary>
internal sealed partial class BranchGraphStage : Control
{
    internal static readonly StyledProperty<IBrush?> Lane1Property =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(Lane1));

    internal static readonly StyledProperty<IBrush?> Lane2Property =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(Lane2));

    internal static readonly StyledProperty<IBrush?> Lane3Property =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(Lane3));

    internal static readonly StyledProperty<IBrush?> Lane4Property =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(Lane4));

    internal static readonly StyledProperty<IBrush?> StageInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(StageInk));

    internal static readonly StyledProperty<IBrush?> StageBorderInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(StageBorderInk));

    internal static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(SurfaceInk));

    internal static readonly StyledProperty<IBrush?> RaisedInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(RaisedInk));

    internal static readonly StyledProperty<IBrush?> HoverInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(HoverInk));

    internal static readonly StyledProperty<IBrush?> CardBorderInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(CardBorderInk));

    internal static readonly StyledProperty<IBrush?> FocusInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(FocusInk));

    internal static readonly StyledProperty<IBrush?> TextInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(TextInk));

    internal static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(SecondaryInk));

    internal static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(MutedInk));

    internal static readonly StyledProperty<IBrush?> SuccessInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(SuccessInk));

    internal static readonly StyledProperty<IBrush?> DangerInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(DangerInk));

    internal static readonly StyledProperty<BoxShadows> CardShadowProperty =
        AvaloniaProperty.Register<BranchGraphStage, BoxShadows>(nameof(CardShadow));

    internal static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<BranchGraphStage, double>(nameof(RowHeight), 44);

    internal static readonly StyledProperty<double> LaneGapProperty =
        AvaloniaProperty.Register<BranchGraphStage, double>(nameof(LaneGap), 18);

    internal static readonly StyledProperty<double> LaneStartProperty =
        AvaloniaProperty.Register<BranchGraphStage, double>(nameof(LaneStart), 22);

    internal static readonly StyledProperty<double> GutterWidthProperty =
        AvaloniaProperty.Register<BranchGraphStage, double>(nameof(GutterWidth), 80);

    internal static readonly StyledProperty<double> DetailWidthProperty =
        AvaloniaProperty.Register<BranchGraphStage, double>(nameof(DetailWidth), 340);

    internal static readonly StyledProperty<CornerRadius> StageRadiusProperty =
        AvaloniaProperty.Register<BranchGraphStage, CornerRadius>(nameof(StageRadius), new CornerRadius(16));

    internal static readonly StyledProperty<CornerRadius> RowRadiusProperty =
        AvaloniaProperty.Register<BranchGraphStage, CornerRadius>(nameof(RowRadius), new CornerRadius(14));

    internal static readonly StyledProperty<CornerRadius> CardRadiusProperty =
        AvaloniaProperty.Register<BranchGraphStage, CornerRadius>(nameof(CardRadius), new CornerRadius(18));

    internal static readonly StyledProperty<CornerRadius> BlockRadiusProperty =
        AvaloniaProperty.Register<BranchGraphStage, CornerRadius>(nameof(BlockRadius), new CornerRadius(2));

    internal static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<BranchGraphStage>();

    internal static readonly StyledProperty<FontFamily?> MonoFontFamilyProperty =
        AvaloniaProperty.Register<BranchGraphStage, FontFamily?>(nameof(MonoFontFamily));

    /// <summary>Opacity of the rows, dots and lines of the other branches while a commit is inspected (Arc: 0.42, 0.35, 0.28).</summary>
    internal const double DimText = 0.42;
    internal const double DimNode = 0.35;
    internal const double DimEdge = 0.28;

    /// <summary>The inspected dot's scale.</summary>
    internal const double InspectScale = 1.28;

    private const double DimSeconds = 0.2;
    private const double FillSeconds = 0.15;
    private const double ScaleTau = 0.055;
    private const double CardFollowTau = 0.045;
    private const double CardInSeconds = 0.2;
    private const double CardOutSeconds = 0.15;
    private const double CardOutDelay = 0.08;
    private const double TopInset = 6;
    private const double BottomInset = 4;
    private const double SideInset = 4;

    private readonly LiveLoop _loop;
    private BranchGraph? _owner;
    private BranchGraphModel? _model;
    private BranchGraphLayout? _layout;

    // Choreography (story-paced seconds: the clock runs at the owner's Speed).
    private double _t;
    private double _wall;
    private double _transitionStart;
    private double _transitionEnd;
    private bool _sweeping;
    private BranchGraphTrack[] _tracks = [];
    private readonly List<BranchGraphEdgeTrack> _edgeTracks = new();
    private BranchGraphPose[] _poses = [];
    private double[] _edgeProgress = [];
    private double _heightFrom;
    private double _heightTo;
    private double _heightStart;
    private double _rows;
    private string[] _hash = [];
    private string?[] _hashNext = [];
    private double[] _hashSwapAt = [];
    private double[] _flashStart = [];
    private BranchGraphPill[][] _pills = [];
    private double[] _pillStart = [];
    private bool[] _reported = [];

    // Inspection (wall seconds).
    private int _hover = -1;
    private int _active = -1;
    private int _inspected = -1;
    private double[] _dim = [];
    private double[] _dimFrom = [];
    private double[] _dimTo = [];
    private double _dimStart = double.NegativeInfinity;
    private double[] _fill = [];
    private double[] _fillFrom = [];
    private double[] _fillTo = [];
    private double _fillStart = double.NegativeInfinity;
    private double[] _hoverScale = [];

    // Detail card.
    private int _cardItem = -1;
    private double _cardY;
    private bool _cardAbove;
    private double _cardShownAt = double.NegativeInfinity;
    private double _cardHideAt = double.PositiveInfinity;
    private double _cardOpacity;
    private double _cardRise;
    private double _cardScale = 1;

    static BranchGraphStage()
    {
        FocusableProperty.OverrideDefaultValue<BranchGraphStage>(true);
        FocusAdornerProperty.OverrideDefaultValue<BranchGraphStage>(null);
        AffectsRender<BranchGraphStage>(Lane1Property, Lane2Property, Lane3Property, Lane4Property, StageInkProperty,
            StageBorderInkProperty, SurfaceInkProperty, RaisedInkProperty, HoverInkProperty, CardBorderInkProperty, FocusInkProperty,
            TextInkProperty, SecondaryInkProperty, MutedInkProperty, SuccessInkProperty, DangerInkProperty, CardShadowProperty,
            StageRadiusProperty, RowRadiusProperty, CardRadiusProperty, BlockRadiusProperty, DetailWidthProperty);
        AffectsMeasure<BranchGraphStage>(RowHeightProperty, LaneGapProperty, LaneStartProperty, GutterWidthProperty, FontFamilyProperty,
            MonoFontFamilyProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<BranchGraphStage>(AutomationControlType.List);
    }

    public BranchGraphStage()
    {
        ClipToBounds = false;
        _loop = new LiveLoop(this, OnFrame);
        ActualThemeVariantChanged += (_, _) =>
        {
            ClearCaches();
            InvalidateVisual();
        };
    }

    public IBrush? Lane1 { get => GetValue(Lane1Property); set => SetValue(Lane1Property, value); }

    public IBrush? Lane2 { get => GetValue(Lane2Property); set => SetValue(Lane2Property, value); }

    public IBrush? Lane3 { get => GetValue(Lane3Property); set => SetValue(Lane3Property, value); }

    public IBrush? Lane4 { get => GetValue(Lane4Property); set => SetValue(Lane4Property, value); }

    public IBrush? StageInk { get => GetValue(StageInkProperty); set => SetValue(StageInkProperty, value); }

    public IBrush? StageBorderInk { get => GetValue(StageBorderInkProperty); set => SetValue(StageBorderInkProperty, value); }

    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }

    public IBrush? RaisedInk { get => GetValue(RaisedInkProperty); set => SetValue(RaisedInkProperty, value); }

    public IBrush? HoverInk { get => GetValue(HoverInkProperty); set => SetValue(HoverInkProperty, value); }

    public IBrush? CardBorderInk { get => GetValue(CardBorderInkProperty); set => SetValue(CardBorderInkProperty, value); }

    public IBrush? FocusInk { get => GetValue(FocusInkProperty); set => SetValue(FocusInkProperty, value); }

    public IBrush? TextInk { get => GetValue(TextInkProperty); set => SetValue(TextInkProperty, value); }

    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }

    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }

    public IBrush? SuccessInk { get => GetValue(SuccessInkProperty); set => SetValue(SuccessInkProperty, value); }

    public IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }

    public BoxShadows CardShadow { get => GetValue(CardShadowProperty); set => SetValue(CardShadowProperty, value); }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public double LaneGap { get => GetValue(LaneGapProperty); set => SetValue(LaneGapProperty, value); }

    public double LaneStart { get => GetValue(LaneStartProperty); set => SetValue(LaneStartProperty, value); }

    public double GutterWidth { get => GetValue(GutterWidthProperty); set => SetValue(GutterWidthProperty, value); }

    public double DetailWidth { get => GetValue(DetailWidthProperty); set => SetValue(DetailWidthProperty, value); }

    public CornerRadius StageRadius { get => GetValue(StageRadiusProperty); set => SetValue(StageRadiusProperty, value); }

    public CornerRadius RowRadius { get => GetValue(RowRadiusProperty); set => SetValue(RowRadiusProperty, value); }

    public CornerRadius CardRadius { get => GetValue(CardRadiusProperty); set => SetValue(CardRadiusProperty, value); }

    public CornerRadius BlockRadius { get => GetValue(BlockRadiusProperty); set => SetValue(BlockRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public FontFamily? MonoFontFamily { get => GetValue(MonoFontFamilyProperty); set => SetValue(MonoFontFamilyProperty, value); }

    // ---- state for the owner and tests ---------------------------------------------------------------------------------

    /// <summary>The preview shown (or being animated to).</summary>
    public BranchGraphStrategy Strategy => _layout?.Strategy ?? BranchGraphStrategy.Before;

    /// <summary>True while a pick, the first-load sweep or an inspection is still animating.</summary>
    public bool IsMoving => _loop.IsActive;

    /// <summary>True while the first-load sweep runs.</summary>
    public bool IsSweeping => _sweeping;

    /// <summary>The item whose detail card is shown (-1 for none).</summary>
    public int CardItem => _cardOpacity > 0.001 || _cardHideAt > _wall ? _cardItem : -1;

    /// <summary>The item hovered by the pointer or chosen with the keys or a tap (-1 for none).</summary>
    public int InspectedItem => _inspected;

    /// <summary>The item the keyboard or a tap made active (-1 for none).</summary>
    public int ActiveItem => _active;

    /// <summary>The words a screen reader hears for the list now.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>An item's current pose (tests).</summary>
    public BranchGraphPose PoseOf(int item) => item >= 0 && item < _poses.Length ? _poses[item] : default;

    /// <summary>The hash an item shows now (tests).</summary>
    public string HashShownBy(int item) => item >= 0 && item < _hash.Length ? _hash[item] : string.Empty;

    /// <summary>How much of an edge is drawn (tests; 0 when the edge is not part of the picture).</summary>
    public double EdgeProgress(int parent, int child)
    {
        for (var i = 0; i < _edgeTracks.Count; i++)
        {
            if (_edgeTracks[i].Spec.Parent == parent && _edgeTracks[i].Spec.Child == child)
            {
                return i < _edgeProgress.Length ? _edgeProgress[i] : 0;
            }
        }
        return 0;
    }

    /// <summary>The rows the stage is sized for now (animates with the stage's height).</summary>
    public double RowsShown => _rows;

    /// <summary>A row's top, an item's lane x, a dot's center (stage coordinates).</summary>
    public double RowTop(double rowY) => 1 + TopInset + rowY;

    public Point DotOf(in BranchGraphPose pose) => new(pose.X, RowTop(pose.RowY) + RowHeight / 2);

    private double LaneX(int lane) => 1 + LaneStart + lane * LaneGap;

    private double GutterOf(int lanes) => Math.Max(GutterWidth, LaneStart * 2 + (lanes - 1) * LaneGap);

    // ---- model and picks -----------------------------------------------------------------------------------------------

    public void Attach(BranchGraph owner)
    {
        _owner = owner;
        if (owner.GraphModel is { } model)
        {
            SetModel(model, owner.SelectedStrategy, owner.IsDrawn);
        }
    }

    /// <summary>Starts over on <paramref name="model"/> showing <paramref name="strategy"/>: drawn at once, or hidden until <see cref="StartSweep"/>.</summary>
    public void SetModel(BranchGraphModel model, BranchGraphStrategy strategy, bool drawn)
    {
        _model = model;
        _layout = model[strategy];
        var n = model.Items.Length;
        _tracks = new BranchGraphTrack[n];
        _poses = new BranchGraphPose[n];
        _hash = new string[n];
        _hashNext = new string?[n];
        _hashSwapAt = new double[n];
        _flashStart = new double[n];
        _pills = new BranchGraphPill[n][];
        _pillStart = new double[n];
        _reported = new bool[n];
        _dim = new double[n];
        _dimFrom = new double[n];
        _dimTo = new double[n];
        _fill = new double[n];
        _fillFrom = new double[n];
        _fillTo = new double[n];
        _hoverScale = new double[n];
        for (var i = 0; i < n; i++)
        {
            var row = _layout.Row[i];
            var x = LaneX(_layout.Lane[i]);
            var y = Math.Max(0, row) * RowHeight;
            _poses[i] = drawn && row >= 0 ? BranchGraphPose.Shown(x, y) : BranchGraphPose.Hidden(x, y);
            _tracks[i] = new BranchGraphTrack { Kind = BranchGraphTrackKind.Static, From = _poses[i], To = _poses[i], Ring = double.NaN };
            _hash[i] = _layout.Hash[i];
            _hashSwapAt[i] = double.NaN;
            _flashStart[i] = double.NaN;
            _pills[i] = _layout.Pills[i];
            _pillStart[i] = double.NegativeInfinity;
            _hoverScale[i] = 1;
            _reported[i] = false;
        }
        _edgeTracks.Clear();
        foreach (var edge in _layout.Edges)
        {
            _edgeTracks.Add(new BranchGraphEdgeTrack { Spec = edge, From = drawn ? 1 : 0, Show = drawn, Start = 0 });
        }
        _edgeProgress = new double[_edgeTracks.Count];
        _sweeping = false;
        _transitionStart = _t;
        _transitionEnd = _t;
        _rows = _heightFrom = _heightTo = _layout.RowCount;
        _heightStart = _t;
        _hover = -1;
        _active = -1;
        _inspected = -1;
        _cardItem = -1;
        _cardOpacity = 0;
        _cardHideAt = double.NegativeInfinity;
        _dimStart = _fillStart = double.NegativeInfinity;
        ClearCaches();
        Evaluate();
        if (drawn)
        {
            ReportAll(model);
        }
        UpdateSummary();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private bool Animates => ArcMotion.IsFull(this) && (_owner?.Speed ?? 1) > 0 && _loop.CanRun;

    /// <summary>Draws the picture in: the lines rise from the oldest commit at a steady pace and each commit lands as its line reaches it.</summary>
    public void StartSweep()
    {
        if (_model is null || _layout is null)
        {
            return;
        }
        if (!Animates)
        {
            ShowAll();
            return;
        }
        var layout = _layout;
        var n = _model.Items.Length;
        var bottom = 0.0;
        for (var i = 0; i < n; i++)
        {
            if (layout.Row[i] >= 0)
            {
                bottom = Math.Max(bottom, layout.Row[i] * RowHeight);
            }
        }
        var end = 0.0;
        for (var i = 0; i < n; i++)
        {
            var row = layout.Row[i];
            var target = BranchGraphPose.Shown(LaneX(layout.Lane[i]), Math.Max(0, row) * RowHeight);
            if (row < 0)
            {
                _tracks[i] = new BranchGraphTrack { Kind = BranchGraphTrackKind.Static, From = BranchGraphPose.Hidden(target.X, target.RowY), To = BranchGraphPose.Hidden(target.X, target.RowY), Ring = double.NaN };
                continue;
            }
            var land = BranchGraphMotion.SweepStart + (bottom - target.RowY) / BranchGraphMotion.SweepSpeed - BranchGraphMotion.SweepLead;
            land = Math.Max(0, land);
            _tracks[i] = new BranchGraphTrack
            {
                Kind = BranchGraphTrackKind.Appear,
                From = BranchGraphPose.Hidden(target.X, target.RowY),
                To = target,
                Start = land,
                Land = land,
                Ring = double.NaN
            };
            _pillStart[i] = _t + Math.Max(BranchGraphMotion.TagDelay, land);
            _reported[i] = false;
            end = Math.Max(end, land + BranchGraphMotion.LandSettle);
        }
        _edgeTracks.Clear();
        foreach (var edge in layout.Edges)
        {
            _edgeTracks.Add(new BranchGraphEdgeTrack { Spec = edge, From = 0, Show = true, Sweep = true });
        }
        _edgeProgress = new double[_edgeTracks.Count];
        _sweeping = true;
        _transitionStart = _t;
        _transitionEnd = _t + end;
        Evaluate();
        Wake();
    }

    /// <summary>Shows the whole picture at once (reduced motion, <see cref="BranchGraph.StartsFinished"/>).</summary>
    public void ShowAll()
    {
        if (_model is null || _layout is null)
        {
            return;
        }
        Jump(_layout);
    }

    /// <summary>Moves to <paramref name="strategy"/>: staged when motion is full, at once otherwise.</summary>
    public void GoTo(BranchGraphStrategy strategy)
    {
        if (_model is null || _layout is null)
        {
            return;
        }
        var to = _model[strategy];
        if (!Animates)
        {
            Jump(to);
            return;
        }
        Plan(to);
        Wake();
    }

    private void Jump(BranchGraphLayout to)
    {
        var model = _model!;
        _layout = to;
        for (var i = 0; i < _poses.Length; i++)
        {
            var row = to.Row[i];
            var x = LaneX(to.Lane[i]);
            var y = Math.Max(0, row) * RowHeight;
            var pose = row >= 0 ? BranchGraphPose.Shown(x, y) : BranchGraphPose.Hidden(x, y);
            _tracks[i] = new BranchGraphTrack { Kind = BranchGraphTrackKind.Static, From = pose, To = pose, Ring = double.NaN };
            _hash[i] = to.Hash[i];
            _hashNext[i] = null;
            _hashSwapAt[i] = double.NaN;
            _flashStart[i] = double.NaN;
            _pills[i] = to.Pills[i];
            _pillStart[i] = double.NegativeInfinity;
        }
        _edgeTracks.Clear();
        foreach (var edge in to.Edges)
        {
            _edgeTracks.Add(new BranchGraphEdgeTrack { Spec = edge, From = 1, Show = true });
        }
        _edgeProgress = new double[_edgeTracks.Count];
        _rows = _heightFrom = _heightTo = to.RowCount;
        _sweeping = false;
        _transitionStart = _transitionEnd = _t;
        ReportAll(model);
        Evaluate();
        UpdateInspection(animate: false);
        UpdateSummary();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void ReportAll(BranchGraphModel model)
    {
        var order = model.Items.Where(i => i.Commit is not null && !_reported[i.Index] && _layout!.Shows(i.Index)).OrderBy(i => i.CommitIndex).ToArray();
        foreach (var item in order)
        {
            _reported[item.Index] = true;
            _owner?.OnCommitDrawn(item);
        }
    }

    /// <summary>Stages a pick from wherever things are now.</summary>
    private void Plan(BranchGraphLayout to)
    {
        var model = _model!;
        if (_sweeping)
        {
            ReportAll(model);
            _sweeping = false;
        }
        var now = _t;
        var from = (BranchGraphPose[])_poses.Clone();
        var fromEdges = new Dictionary<long, (double Progress, BranchGraphEdgeSpec Spec)>();
        for (var i = 0; i < _edgeTracks.Count; i++)
        {
            var progress = i < _edgeProgress.Length ? _edgeProgress[i] : 0;
            if (progress > 0.0001)
            {
                fromEdges[EdgeKey(_edgeTracks[i].Spec)] = (progress, _edgeTracks[i].Spec);
            }
        }
        var n = from.Length;
        var squash = model.SquashItem;
        var squashPoint = squash >= 0 && to.Shows(squash) ? new Point(LaneX(to.Lane[squash]), to.Row[squash] * RowHeight) : (Point?)null;
        var squashNow = squash >= 0 && from[squash].IsShown ? new Point(from[squash].X, from[squash].RowY) : (Point?)null;
        // Flyers: the pull request's commits when they change lane or fold into / come out of the squash point.
        var flyers = new List<int>();
        foreach (var i in model.PullRequest)
        {
            var shownNow = from[i].Opacity > 0.01;
            var shownThen = to.Shows(i);
            var laneChange = Math.Abs(from[i].X - LaneX(to.Lane[i])) > 0.5;
            if ((shownNow && shownThen && laneChange) || (shownNow && !shownThen && squashPoint is not null) || (!shownNow && shownThen && squashNow is not null))
            {
                flyers.Add(i);
            }
        }
        var squashOut = squashNow is not null && squashPoint is null;
        var lastLand = 0.0;
        for (var k = 0; k < flyers.Count; k++)
        {
            var i = flyers[k];
            var target = BranchGraphPose.Shown(LaneX(to.Lane[i]), Math.Max(0, to.Row[i]) * RowHeight);
            var track = new BranchGraphTrack { From = from[i], To = target, Ring = double.NaN };
            if (from[i].Opacity > 0.01 && !to.Shows(i))
            {
                track.Kind = BranchGraphTrackKind.SquashIn;
                track.Start = BranchGraphMotion.FirstLift + BranchGraphMotion.Stagger * k;
                track.Land = track.Start + BranchGraphMotion.FlightDelay + BranchGraphMotion.FlightSeconds;
                track.PointX = squashPoint!.Value.X;
                track.PointRowY = squashPoint.Value.Y;
                track.To = BranchGraphPose.Hidden(track.PointX, track.PointRowY);
            }
            else if (from[i].Opacity <= 0.01)
            {
                track.Kind = BranchGraphTrackKind.SquashOut;
                track.Start = BranchGraphMotion.DisappearSeconds + BranchGraphMotion.Stagger * k;
                track.Land = track.Start + BranchGraphMotion.SquashAppear + BranchGraphMotion.FlightSeconds;
                track.PointX = squashNow!.Value.X;
                track.PointRowY = squashNow.Value.Y;
                track.Ring = track.Land;
            }
            else
            {
                track.Kind = BranchGraphTrackKind.Fly;
                track.Start = BranchGraphMotion.FirstLift + BranchGraphMotion.Stagger * k;
                track.Land = track.Start + BranchGraphMotion.FlightDelay + BranchGraphMotion.FlightSeconds;
                track.Ring = track.Land;
            }
            _tracks[i] = track;
            lastLand = Math.Max(lastLand, track.Land);
        }
        var hasFlyers = flyers.Count > 0;
        var appearAt = hasFlyers ? lastLand + BranchGraphMotion.AfterLand : BranchGraphMotion.MergeAppear;
        if (squashOut)
        {
            appearAt = hasFlyers ? lastLand + BranchGraphMotion.AfterLand : BranchGraphMotion.MergeAppear;
        }
        var reflowStart = hasFlyers ? BranchGraphMotion.ReflowStartWithFlights : BranchGraphMotion.ReflowStart;
        var end = 0.0;
        for (var i = 0; i < n; i++)
        {
            var target = BranchGraphPose.Shown(LaneX(to.Lane[i]), Math.Max(0, to.Row[i]) * RowHeight);
            if (flyers.Contains(i))
            {
                end = Math.Max(end, BranchGraphMotion.EndOf(_tracks[i]));
                continue;
            }
            var shownNow = from[i].IsShown;
            var shownThen = to.Shows(i);
            BranchGraphTrack track;
            if (shownNow && shownThen)
            {
                var distance = Math.Abs(target.RowY - from[i].RowY);
                track = new BranchGraphTrack
                {
                    Kind = BranchGraphTrackKind.Reflow,
                    From = from[i],
                    To = target,
                    Start = reflowStart,
                    Duration = Math.Clamp(0.56 + 0.002 * distance, 0.4, 0.9),
                    Ring = double.NaN
                };
                if (from[i].Opacity < 0.99)
                {
                    // Interrupted mid-landing: finish landing where it goes.
                    track.Kind = BranchGraphTrackKind.Appear;
                    track.Start = 0;
                }
            }
            else if (shownThen)
            {
                var virtualItem = model.Items[i].Commit is null;
                track = new BranchGraphTrack
                {
                    Kind = BranchGraphTrackKind.Appear,
                    From = BranchGraphPose.Hidden(target.X, target.RowY),
                    To = target,
                    Start = virtualItem ? appearAt : Math.Min(appearAt, BranchGraphMotion.MergeAppear),
                    Ring = double.NaN
                };
                track.Land = track.Start;
                if (virtualItem)
                {
                    track.Ring = track.Start + BranchGraphMotion.AppearRingDelay;
                }
            }
            else if (shownNow)
            {
                track = new BranchGraphTrack { Kind = BranchGraphTrackKind.Disappear, From = from[i], To = BranchGraphPose.Hidden(from[i].X, from[i].RowY), Start = 0, Ring = double.NaN };
            }
            else
            {
                var hidden = BranchGraphPose.Hidden(target.X, target.RowY);
                track = new BranchGraphTrack { Kind = BranchGraphTrackKind.Static, From = hidden, To = hidden, Ring = double.NaN };
            }
            _tracks[i] = track;
            end = Math.Max(end, BranchGraphMotion.EndOf(track));
            if (!double.IsNaN(track.Ring))
            {
                end = Math.Max(end, track.Ring + BranchGraphMotion.RingSeconds);
            }
        }
        foreach (var i in flyers)
        {
            if (!double.IsNaN(_tracks[i].Ring))
            {
                end = Math.Max(end, _tracks[i].Ring + BranchGraphMotion.RingSeconds);
            }
        }
        // Edges: kept ones stay drawn and bend with their commits, gone ones retract, new ones draw in once both ends have landed.
        _edgeTracks.Clear();
        var seen = new HashSet<long>();
        foreach (var edge in to.Edges)
        {
            var key = EdgeKey(edge);
            seen.Add(key);
            var p0 = fromEdges.TryGetValue(key, out var was) ? was.Progress : 0;
            var start = Math.Max(EdgeReady(edge.Child), EdgeReady(edge.Parent));
            if (start <= 0)
            {
                start = p0 > 0 ? 0 : BranchGraphMotion.RetractDelay;
            }
            var track = new BranchGraphEdgeTrack { Spec = edge, From = p0 >= 0.999 ? 1 : p0, Show = true, Start = start };
            _edgeTracks.Add(track);
            end = Math.Max(end, BranchGraphMotion.EdgeEnd(track));
        }
        foreach (var (key, was) in fromEdges)
        {
            if (seen.Contains(key))
            {
                continue;
            }
            var track = new BranchGraphEdgeTrack { Spec = was.Spec, From = was.Progress, Show = false, Start = BranchGraphMotion.RetractDelay };
            _edgeTracks.Add(track);
            end = Math.Max(end, BranchGraphMotion.EdgeEnd(track));
        }
        _edgeProgress = new double[_edgeTracks.Count];
        // The stage grows at once and shrinks once the commits have gone.
        _heightFrom = _rows;
        _heightTo = to.RowCount;
        _heightStart = _heightTo >= _heightFrom ? 0 : hasFlyers ? Math.Max(0.1, lastLand - 0.1) : 0.1;
        end = Math.Max(end, _heightStart + BranchGraphMotion.StageSeconds);
        // Hashes rewrite as their commit lands; pills move after a short pause.
        for (var i = 0; i < n; i++)
        {
            if (_hash[i] != to.Hash[i] || (_hashNext[i] is { } next && next != to.Hash[i]))
            {
                var at = _tracks[i].Kind switch
                {
                    BranchGraphTrackKind.Fly => _tracks[i].Land - BranchGraphMotion.FlightSeconds * (1 - BranchGraphMotion.HashAt),
                    BranchGraphTrackKind.SquashOut => _tracks[i].Land - BranchGraphMotion.FlightSeconds * (1 - BranchGraphMotion.HashAt),
                    _ => 0
                };
                _hashNext[i] = to.Hash[i];
                _hashSwapAt[i] = now + at;
                end = Math.Max(end, at + BranchGraphMotion.HashFlashSeconds);
            }
            if (!SamePills(_pills[i], to.Pills[i]))
            {
                _pills[i] = to.Pills[i];
                _pillStart[i] = now + BranchGraphMotion.TagDelay;
                end = Math.Max(end, BranchGraphMotion.TagDelay + BranchGraphMotion.TagSeconds);
            }
        }
        _layout = to;
        _transitionStart = now;
        _transitionEnd = now + end;
        Evaluate();
        UpdateInspection(animate: true);
        UpdateSummary();
    }

    private double EdgeReady(int item)
    {
        var track = _tracks[item];
        return track.Kind switch
        {
            BranchGraphTrackKind.Fly or BranchGraphTrackKind.SquashOut => track.Land + BranchGraphMotion.AfterLand,
            BranchGraphTrackKind.Appear => track.Start + BranchGraphMotion.AppearEdgeDelay,
            _ => 0
        };
    }

    private static long EdgeKey(in BranchGraphEdgeSpec edge) => ((long)edge.Parent << 32) | (uint)edge.Child;

    private static bool SamePills(BranchGraphPill[] a, BranchGraphPill[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>The end of everything the last pick or sweep started, in seconds from now at speed 1.</summary>
    internal double RemainingSeconds => Math.Max(0, _transitionEnd - _t);

    /// <summary>How long a pick from <paramref name="from"/> to <paramref name="to"/> takes when nothing is moving (the autoplay's pace).</summary>
    internal static double DurationOf(BranchGraphModel model, BranchGraphStrategy from, BranchGraphStrategy to)
    {
        if (from == to)
        {
            return 0;
        }
        var a = model[from];
        var b = model[to];
        var flyers = model.PullRequest.Count(i => a.Shows(i) != b.Shows(i) || (a.Shows(i) && b.Shows(i) && a.Lane[i] != b.Lane[i]));
        var appears = Enumerable.Range(0, model.Items.Length).Any(i => model.Items[i].Commit is null && b.Shows(i) && !a.Shows(i));
        if (flyers == 0)
        {
            return appears ? BranchGraphMotion.MergeAppear + BranchGraphMotion.AppearRingDelay + BranchGraphMotion.RingSeconds : 0.9;
        }
        var lastLand = BranchGraphMotion.FirstLift + BranchGraphMotion.Stagger * (flyers - 1) + BranchGraphMotion.FlightDelay +
            BranchGraphMotion.FlightSeconds;
        return appears
            ? lastLand + BranchGraphMotion.AfterLand + BranchGraphMotion.AppearRingDelay + BranchGraphMotion.RingSeconds
            : lastLand + BranchGraphMotion.RingSeconds;
    }

    // ---- frames ----------------------------------------------------------------------------------------------------------

    private void Wake()
    {
        if (NeedsFrames())
        {
            _loop.Start();
        }
        InvalidateVisual();
    }

    private bool NeedsFrames()
    {
        if (_t < _transitionEnd)
        {
            return true;
        }
        if (_wall < _dimStart + DimSeconds || _wall < _fillStart + FillSeconds)
        {
            return true;
        }
        if (_cardItem >= 0 && (_wall < _cardShownAt + CardInSeconds || (!double.IsInfinity(_cardHideAt) && _wall < _cardHideAt + CardOutSeconds) || Math.Abs(_cardY - CardTargetY()) > 0.2))
        {
            return true;
        }
        for (var i = 0; i < _hoverScale.Length; i++)
        {
            if (Math.Abs(_hoverScale[i] - (i == _inspected ? InspectScale : 1)) > 0.002)
            {
                return true;
            }
        }
        return false;
    }

    private bool OnFrame(double dt)
    {
        var speed = Math.Max(0, _owner?.Speed ?? 1);
        _wall += dt;
        _t += dt * speed;
        StepInspection(dt);
        Evaluate();
        InvalidateVisual();
        if (!NeedsFrames())
        {
            Settle();
            return false;
        }
        return true;
    }

    /// <summary>Jumps every animation to its end (the loop stopped, motion turned down, or the speed is 0).</summary>
    private void Settle()
    {
        _t = Math.Max(_t, _transitionEnd);
        _wall = Math.Max(_wall, Math.Max(_dimStart + DimSeconds, _fillStart + FillSeconds));
        for (var i = 0; i < _hoverScale.Length; i++)
        {
            _hoverScale[i] = i == _inspected ? InspectScale : 1;
        }
        _cardY = CardTargetY();
        Evaluate();
        InvalidateVisual();
    }

    /// <summary>Computes the poses, edges, hashes and stage height at the clock's time; reports landed commits.</summary>
    private void Evaluate()
    {
        if (_model is null)
        {
            return;
        }
        var local = _t - _transitionStart;
        for (var i = 0; i < _poses.Length; i++)
        {
            _poses[i] = BranchGraphMotion.Evaluate(_tracks[i], local);
            if (_hashNext[i] is { } next && _t >= _hashSwapAt[i])
            {
                _hash[i] = next;
                _hashNext[i] = null;
                _flashStart[i] = Animates ? _hashSwapAt[i] : double.NaN;
            }
        }
        if (_sweeping)
        {
            var bottom = 0.0;
            for (var i = 0; i < _poses.Length; i++)
            {
                if (_layout!.Row[i] >= 0)
                {
                    bottom = Math.Max(bottom, _layout.Row[i] * RowHeight);
                }
            }
            var front = bottom - BranchGraphMotion.SweepSpeed * (local - BranchGraphMotion.SweepStart);
            for (var i = 0; i < _edgeTracks.Count; i++)
            {
                var spec = _edgeTracks[i].Spec;
                var yp = _tracks[spec.Parent].To.RowY;
                var yc = _tracks[spec.Child].To.RowY;
                _edgeProgress[i] = Math.Abs(yp - yc) < 0.5 ? (front <= yp ? 1 : 0) : BranchGraphMotion.Clamp01((yp - front) / (yp - yc));
            }
            for (var i = 0; i < _poses.Length; i++)
            {
                if (!_reported[i] && _model.Items[i].Commit is not null && _layout!.Row[i] >= 0 && local >= _tracks[i].Start)
                {
                    _reported[i] = true;
                    _owner?.OnCommitDrawn(_model.Items[i]);
                }
            }
            if (_t >= _transitionEnd)
            {
                _sweeping = false;
            }
        }
        else
        {
            for (var i = 0; i < _edgeTracks.Count; i++)
            {
                _edgeProgress[i] = BranchGraphMotion.EvaluateEdge(_edgeTracks[i], local);
            }
        }
        var h = _heightTo;
        if (Math.Abs(_heightTo - _heightFrom) > 0.001)
        {
            var p = BranchGraphMotion.StageHeight.Ease(BranchGraphMotion.Clamp01((local - _heightStart) / BranchGraphMotion.StageSeconds));
            h = BranchGraphMotion.Lerp(_heightFrom, _heightTo, p);
        }
        if (Math.Abs(h - _rows) > 0.0001)
        {
            _rows = h;
            InvalidateMeasure();
        }
    }

    // ---- inspection: hover, keys, taps -----------------------------------------------------------------------------------

    private void SetHover(int item)
    {
        if (item == _hover)
        {
            return;
        }
        _hover = item;
        UpdateInspection(animate: true);
    }

    /// <summary>Makes <paramref name="item"/> the active commit (keys, taps, <see cref="BranchGraph.SelectedCommit"/>); -1 clears it.</summary>
    public void SetActive(int item, bool fromOwner = false)
    {
        if (item == _active)
        {
            return;
        }
        _active = item;
        if (!fromOwner)
        {
            _owner?.OnActiveChanged(item >= 0 && _model is not null ? _model.Items[item].Commit : null);
        }
        UpdateInspection(animate: true);
        UpdateSummary();
    }

    /// <summary>Activates the item of <paramref name="commit"/> (from <see cref="BranchGraph.SelectedCommit"/>).</summary>
    public void SetActiveCommit(BranchGraphCommit? commit)
    {
        var index = -1;
        if (commit is not null && _model is not null)
        {
            foreach (var item in _model.Items)
            {
                if (ReferenceEquals(item.Commit, commit))
                {
                    index = item.Index;
                    break;
                }
            }
        }
        if (index < 0 && _active >= 0 && _model?.Items[_active].Commit is null && commit is null)
        {
            // An added commit (merge or squash) is active: it has no BranchGraphCommit to report.
            return;
        }
        SetActive(index, fromOwner: true);
    }

    private void UpdateInspection(bool animate)
    {
        var inspected = _hover >= 0 ? _hover : _active;
        if (inspected >= 0 && _layout is not null && !_layout.Shows(inspected))
        {
            inspected = -1;
        }
        var changed = inspected != _inspected;
        _inspected = inspected;
        var animates = animate && Animates;
        var group = inspected >= 0 && _layout is not null ? _layout.Group[inspected] : null;
        var dimsChanged = false;
        for (var i = 0; i < _dim.Length; i++)
        {
            var dim = group is not null && _layout!.Group[i] != group ? 1 : 0;
            if (Math.Abs(dim - _dimTo[i]) > 0.001)
            {
                dimsChanged = true;
            }
        }
        if (dimsChanged || changed)
        {
            for (var i = 0; i < _dim.Length; i++)
            {
                _dimFrom[i] = _dim[i];
                _dimTo[i] = group is not null && _layout!.Group[i] != group ? 1 : 0;
                _fillFrom[i] = _fill[i];
                _fillTo[i] = i == inspected ? 1 : 0;
            }
            _dimStart = _fillStart = animates ? _wall : double.NegativeInfinity;
            if (!animates)
            {
                for (var i = 0; i < _dim.Length; i++)
                {
                    _dim[i] = _dimTo[i];
                    _fill[i] = _fillTo[i];
                    _hoverScale[i] = i == inspected ? InspectScale : 1;
                }
            }
        }
        UpdateCard(animates);
        if (animates)
        {
            Wake();
        }
        else
        {
            InvalidateVisual();
        }
    }

    private void StepInspection(double dt)
    {
        var dimP = BranchGraphMotion.Clamp01((_wall - _dimStart) / DimSeconds);
        var fillP = BranchGraphMotion.Clamp01((_wall - _fillStart) / FillSeconds);
        var dimE = ArcEasings.Standard.Ease(dimP);
        var fillE = ArcEasings.Standard.Ease(fillP);
        var k = 1 - Math.Exp(-dt / ScaleTau);
        for (var i = 0; i < _dim.Length; i++)
        {
            _dim[i] = BranchGraphMotion.Lerp(_dimFrom[i], _dimTo[i], dimE);
            _fill[i] = BranchGraphMotion.Lerp(_fillFrom[i], _fillTo[i], fillE);
            var target = i == _inspected ? InspectScale : 1;
            _hoverScale[i] += (target - _hoverScale[i]) * k;
        }
        if (_cardItem >= 0)
        {
            var target = CardTargetY();
            _cardY += (target - _cardY) * (1 - Math.Exp(-dt / CardFollowTau));
        }
        StepCard();
    }

    private void UpdateCard(bool animate)
    {
        var item = _inspected;
        if (item >= 0)
        {
            var wasHidden = _cardItem < 0 || _cardOpacity <= 0.001 || _wall >= _cardHideAt + CardOutSeconds;
            _cardItem = item;
            _cardHideAt = double.PositiveInfinity;
            if (wasHidden)
            {
                _cardY = CardTargetY();
                _cardAbove = CardPlacedAbove(item);
                _cardShownAt = animate ? _wall : double.NegativeInfinity;
            }
            if (!animate)
            {
                _cardY = CardTargetY();
            }
        }
        else if (_cardItem >= 0 && double.IsPositiveInfinity(_cardHideAt))
        {
            _cardHideAt = animate ? _wall + CardOutDelay : double.NegativeInfinity;
        }
        StepCard();
    }

    private void StepCard()
    {
        if (_cardItem < 0)
        {
            _cardOpacity = 0;
            return;
        }
        var inP = BranchGraphMotion.Clamp01((_wall - _cardShownAt) / CardInSeconds);
        var inE = ArcEasings.Standard.Ease(inP);
        var outP = BranchGraphMotion.Clamp01((_wall - _cardHideAt) / CardOutSeconds);
        var outE = ArcEasings.Exit.Ease(outP);
        _cardOpacity = inE * (1 - outE);
        _cardScale = 0.97 + 0.03 * inE;
        _cardRise = (1 - inE) * 4 * (_cardAbove ? 1 : -1);
        if (outP >= 1)
        {
            _cardItem = -1;
            _cardOpacity = 0;
        }
    }

    // ---- input -----------------------------------------------------------------------------------------------------------

    /// <summary>The item whose row is under <paramref name="point"/>, or -1.</summary>
    public int ItemAt(Point point)
    {
        if (_model is null)
        {
            return -1;
        }
        var best = -1;
        var bestLift = -1.0;
        for (var i = 0; i < _poses.Length; i++)
        {
            var pose = _poses[i];
            if (pose.RowOpacity < 0.5)
            {
                continue;
            }
            var top = RowTop(pose.RowY);
            if (point.Y >= top && point.Y < top + RowHeight && point.X >= 1 && point.X <= Bounds.Width - 1 && pose.Lift > bestLift)
            {
                best = i;
                bestLift = pose.Lift;
            }
        }
        return best;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        SetHover(ItemAt(e.GetPosition(this)));
        _owner?.OnHoverChanged(true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(-1);
        _owner?.OnHoverChanged(false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (_owner is null || (e.Pointer.Type != PointerType.Touch && !point.Properties.IsLeftButtonPressed))
        {
            return;
        }
        _owner.OnUserInteraction();
        var item = ItemAt(point.Position);
        SetActive(item >= 0 && item == _active ? -1 : item);
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_layout is null || e.Handled)
        {
            return;
        }
        var rows = _layout.RowItems;
        if (rows.Length == 0)
        {
            return;
        }
        var position = Array.IndexOf(rows, _active);
        int? next = e.Key switch
        {
            Key.Down => position < 0 ? 0 : Math.Min(rows.Length - 1, position + 1),
            Key.Up => position < 0 ? 0 : Math.Max(0, position - 1),
            Key.Home => 0,
            Key.End => rows.Length - 1,
            _ => null
        };
        if (next is { } p)
        {
            _owner?.OnUserInteraction();
            SetActive(rows[p]);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _active >= 0)
        {
            _owner?.OnUserInteraction();
            SetActive(-1);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional && _active < 0 && _layout is { RowItems.Length: > 0 })
        {
            // Arriving with the keyboard inspects the newest commit.
            SetActive(_layout.RowItems[0]);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_t < _transitionEnd)
        {
            Wake();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ArcMotion.LevelProperty && !ArcMotion.IsFull(this))
        {
            Settle();
        }
        else if (change.Property == RowHeightProperty || change.Property == LaneGapProperty || change.Property == LaneStartProperty)
        {
            if (_model is not null && _layout is not null)
            {
                Jump(_layout);
            }
        }
    }

    // ---- accessibility ---------------------------------------------------------------------------------------------------

    public void UpdateSummary()
    {
        if (_owner is null || _model is null || _layout is null)
        {
            return;
        }
        Summary = _active >= 0 ? _owner.Describe(_model.Items[_active], _layout) : _owner.Labels.CommitList + ". " + _layout.Summary;
        AutomationProperties.SetName(this, Summary);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    // ---- layout ----------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        return new Size(width, StageHeightFor(_rows));
    }

    private double StageHeightFor(double rows) => 2 + TopInset + BottomInset + Math.Max(1, rows) * RowHeight;
}
