using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="Illustration.StepChanged"/>.</summary>
public sealed class IllustrationStepChangedEventArgs(int step, string caption) : EventArgs
{
    /// <summary>The step now shown (0-based).</summary>
    public int Step { get; } = step;

    /// <summary>The caption of the step.</summary>
    public string Caption { get; } = caption;
}

/// <summary>
/// Base of the animated system illustrations (<see cref="DataFlow"/>, <see cref="PaymentFlow"/>,
/// <see cref="MultiRegionFailover"/>, <see cref="VectorSearch"/>): a diagram of nodes, connections and travelling
/// packets drawn in one pass, a looping script on the scene clock and a caption that follows the steps.
/// </summary>
/// <remarks>
/// The scene clock runs on a Core <see cref="LiveLoop"/>, so it pauses while the illustration is off-screen, hidden or
/// detached. Under reduced motion (or Off) the illustration shows a still "poster" frame of its steady state and the
/// caption summarizes the whole story. Space or Enter plays and pauses, R starts over.
/// </remarks>
[TemplatePart(CanvasPart, typeof(Control))]
[TemplatePart(CaptionPart, typeof(ArcContentSwap))]
[TemplatePart(PlayPart, typeof(Avalonia.Controls.Button))]
[PseudoClasses(PlayingPseudoClass, PosterPseudoClass)]
public abstract class Illustration : TemplatedControl
{
    public const string CanvasPart = "PART_Canvas";
    public const string CaptionPart = "PART_Caption";
    public const string PlayPart = "PART_Play";

    /// <summary>Set while the scene clock is meant to run (<see cref="IsPlaying"/>).</summary>
    public const string PlayingPseudoClass = ":playing";

    /// <summary>Set while the still frame is shown (reduced motion or Off).</summary>
    public const string PosterPseudoClass = ":poster";

    private const int PacketCapacity = 64;

    #region Public properties

    /// <summary>Whether the illustration plays (two way). Paused, it holds the current frame.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<Illustration, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Playback speed (0.25–4).</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<Illustration, double>(nameof(Speed), 1, coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0.25, 4) : 1);

    /// <summary>The accessible name of the illustration.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Illustration, string?>(nameof(Label));

    /// <summary>Shows the caption line under the diagram.</summary>
    public static readonly StyledProperty<bool> IsCaptionVisibleProperty =
        AvaloniaProperty.Register<Illustration, bool>(nameof(IsCaptionVisible), true);

    /// <summary>Shows the play / pause button.</summary>
    public static readonly StyledProperty<bool> IsPlaybackButtonVisibleProperty =
        AvaloniaProperty.Register<Illustration, bool>(nameof(IsPlaybackButtonVisible), true);

    public static readonly DirectProperty<Illustration, string> CaptionProperty =
        AvaloniaProperty.RegisterDirect<Illustration, string>(nameof(Caption), o => o.Caption);

    public static readonly DirectProperty<Illustration, int> StepProperty =
        AvaloniaProperty.RegisterDirect<Illustration, int>(nameof(Step), o => o.Step);

    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public bool IsCaptionVisible { get => GetValue(IsCaptionVisibleProperty); set => SetValue(IsCaptionVisibleProperty, value); }

    public bool IsPlaybackButtonVisible { get => GetValue(IsPlaybackButtonVisibleProperty); set => SetValue(IsPlaybackButtonVisibleProperty, value); }

    /// <summary>What the current step shows (also the accessible help text).</summary>
    public string Caption { get => _caption; private set => SetAndRaise(CaptionProperty, ref _caption, value); }

    /// <summary>The current step (0-based); see <see cref="StepCount"/>.</summary>
    public int Step { get => _step; private set => SetAndRaise(StepProperty, ref _step, value); }

    /// <summary>Number of steps in one loop of the story.</summary>
    public abstract int StepCount { get; }

    /// <summary>Raised when the story moves to another step.</summary>
    public event EventHandler<IllustrationStepChangedEventArgs>? StepChanged;

    #endregion

    #region Token properties (set by the themes)

    internal static readonly StyledProperty<IBrush?> SecondaryBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(SecondaryBrush));
    internal static readonly StyledProperty<IBrush?> MutedBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(MutedBrush));
    internal static readonly StyledProperty<IBrush?> LineBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(LineBrush));
    internal static readonly StyledProperty<IBrush?> SubtleLineBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(SubtleLineBrush));
    internal static readonly StyledProperty<IBrush?> StrongLineBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(StrongLineBrush));
    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(SurfaceBrush));
    internal static readonly StyledProperty<IBrush?> SurfaceMutedBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(SurfaceMutedBrush));
    internal static readonly StyledProperty<IBrush?> RaisedBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(RaisedBrush));
    internal static readonly StyledProperty<IBrush?> AccentBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(AccentBrush));
    internal static readonly StyledProperty<IBrush?> AccentSubtleBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(AccentSubtleBrush));
    internal static readonly StyledProperty<IBrush?> AccentForegroundBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(AccentForegroundBrush));
    internal static readonly StyledProperty<IBrush?> SuccessBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(SuccessBrush));
    internal static readonly StyledProperty<IBrush?> WarningBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(WarningBrush));
    internal static readonly StyledProperty<IBrush?> DangerBrushProperty = AvaloniaProperty.Register<Illustration, IBrush?>(nameof(DangerBrush));
    internal static readonly StyledProperty<BoxShadows> RestingShadowProperty = AvaloniaProperty.Register<Illustration, BoxShadows>(nameof(RestingShadow));
    internal static readonly StyledProperty<FontFamily?> MonoFontFamilyProperty = AvaloniaProperty.Register<Illustration, FontFamily?>(nameof(MonoFontFamily));
    internal static readonly StyledProperty<FontWeight> MediumWeightProperty = AvaloniaProperty.Register<Illustration, FontWeight>(nameof(MediumWeight), FontWeight.Medium);
    internal static readonly StyledProperty<double> TextSizeXSProperty = AvaloniaProperty.Register<Illustration, double>(nameof(TextSizeXS), 12);
    internal static readonly StyledProperty<double> TextSizeSMProperty = AvaloniaProperty.Register<Illustration, double>(nameof(TextSizeSM), 13);
    internal static readonly StyledProperty<double> NodeRadiusProperty = AvaloniaProperty.Register<Illustration, double>(nameof(NodeRadius), 12);
    internal static readonly StyledProperty<double> PacketSizeProperty = AvaloniaProperty.Register<Illustration, double>(nameof(PacketSize), 7);

    internal IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }
    internal IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    internal IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    internal IBrush? SubtleLineBrush { get => GetValue(SubtleLineBrushProperty); set => SetValue(SubtleLineBrushProperty, value); }
    internal IBrush? StrongLineBrush { get => GetValue(StrongLineBrushProperty); set => SetValue(StrongLineBrushProperty, value); }
    internal IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    internal IBrush? SurfaceMutedBrush { get => GetValue(SurfaceMutedBrushProperty); set => SetValue(SurfaceMutedBrushProperty, value); }
    internal IBrush? RaisedBrush { get => GetValue(RaisedBrushProperty); set => SetValue(RaisedBrushProperty, value); }
    internal IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    internal IBrush? AccentSubtleBrush { get => GetValue(AccentSubtleBrushProperty); set => SetValue(AccentSubtleBrushProperty, value); }
    internal IBrush? AccentForegroundBrush { get => GetValue(AccentForegroundBrushProperty); set => SetValue(AccentForegroundBrushProperty, value); }
    internal IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }
    internal IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }
    internal IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    internal BoxShadows RestingShadow { get => GetValue(RestingShadowProperty); set => SetValue(RestingShadowProperty, value); }
    internal FontFamily? MonoFontFamily { get => GetValue(MonoFontFamilyProperty); set => SetValue(MonoFontFamilyProperty, value); }
    internal FontWeight MediumWeight { get => GetValue(MediumWeightProperty); set => SetValue(MediumWeightProperty, value); }
    internal double TextSizeXS { get => GetValue(TextSizeXSProperty); set => SetValue(TextSizeXSProperty, value); }
    internal double TextSizeSM { get => GetValue(TextSizeSMProperty); set => SetValue(TextSizeSMProperty, value); }
    internal double NodeRadius { get => GetValue(NodeRadiusProperty); set => SetValue(NodeRadiusProperty, value); }
    internal double PacketSize { get => GetValue(PacketSizeProperty); set => SetValue(PacketSizeProperty, value); }

    #endregion

    private readonly LiveLoop _loop;
    private readonly IllustrationPalette _palette = new();
    private readonly IllustrationPacket[] _packets = new IllustrationPacket[PacketCapacity];
    private readonly List<IllustrationNode> _nodes = new();
    private readonly List<IllustrationEdge> _edges = new();
    private readonly List<IllustrationGroup> _groups = new();
    private readonly List<IllustrationLabel> _labels = new();
    private readonly IllustrationScript _script = new();
    private readonly List<(double At, Action Act)> _pending = new();
    private IllustrationCanvas? _canvas;
    private ArcContentSwap? _captionSwap;
    private Avalonia.Controls.Button? _play;
    private bool _paletteDirty = true;
    private bool _staticDirty = true;
    private bool _staticWasMoving;
    private bool _motionDrawn;
    private bool _built;
    private bool _poster;
    private double _time;
    private double _scriptTime;
    private string _caption = string.Empty;
    private int _step = -1;
    private IllustrationNode? _hovered;
    private static readonly StreamGeometry s_busyArc = CreateBusyArc();

    static Illustration()
    {
        FocusableProperty.OverrideDefaultValue<Illustration>(true);
        AffectsRender<Illustration>(ForegroundProperty);
    }

    protected Illustration()
    {
        _loop = new LiveLoop(this, StepFrame);
        _loop.RunningChanged += _ => UpdateMode();
        PseudoClasses.Set(PlayingPseudoClass, true);
        _loop.Start();
    }

    #region Public API

    /// <summary>Plays from the current frame.</summary>
    public void Play() => SetCurrentValue(IsPlayingProperty, true);

    /// <summary>Holds the current frame.</summary>
    public void Pause() => SetCurrentValue(IsPlayingProperty, false);

    /// <summary>Starts the story over from its first step.</summary>
    public void Restart()
    {
        EnsureBuilt();
        ResetScene();
        _scriptTime = 0;
        _script.Rewind();
        OnRestart();
        if (_poster)
        {
            ApplyPoster();
        }
        else
        {
            _script.Advance(ref _scriptTime, 0);
        }
        Invalidate();
    }

    #endregion

    #region Scene for subclasses

    /// <summary>Width of the scene's coordinate space (px at scale 1).</summary>
    private protected abstract double SceneWidth { get; }

    /// <summary>Height of the scene's coordinate space.</summary>
    private protected abstract double SceneHeight { get; }

    /// <summary>The scene clock in seconds (monotonic; animations evaluate against it).</summary>
    private protected double Now => _time;

    private protected IllustrationPalette Palette => _palette;

    private protected IReadOnlyList<IllustrationNode> Nodes => _nodes;

    private protected IReadOnlyList<IllustrationEdge> Edges => _edges;

    private protected bool IsPoster => _poster;

    /// <summary>Builds nodes, edges, groups and labels (called once, and again after <see cref="Rebuild"/>).</summary>
    private protected abstract void BuildScene();

    /// <summary>Adds the timed beats of one loop of the story.</summary>
    private protected abstract void BuildScript(IllustrationScript script);

    /// <summary>Shows the still frame used under reduced motion.</summary>
    private protected abstract void ApplyPoster();

    /// <summary>Called when the story starts over (reset counters and per-run state).</summary>
    private protected virtual void OnRestart()
    {
    }

    /// <summary>Called every frame after the clock advanced (continuous generators).</summary>
    private protected virtual void OnTick(double dt)
    {
    }

    /// <summary>A packet reached the end of its edge.</summary>
    private protected virtual void OnPacketArrived(IllustrationEdge edge, int tag, IllustrationTone tone)
    {
    }

    /// <summary>Draws behind the groups, edges and nodes (scene coordinates).</summary>
    private protected virtual void RenderUnderlay(DrawingContext context, double scale)
    {
    }

    /// <summary>Draws over everything else on the static layer (scene coordinates).</summary>
    private protected virtual void RenderOverlay(DrawingContext context, double scale)
    {
    }

    /// <summary>Draws on the motion layer (redrawn every frame while <see cref="NeedsMotionFrame"/> or packets travel).</summary>
    private protected virtual void RenderMotionOverlay(DrawingContext context, double scale)
    {
    }

    /// <summary>True while the subclass's own static parts animate (its underlay and overlay need redrawing).</summary>
    private protected virtual bool IsMoving(double now) => false;

    /// <summary>True when the subclass's motion overlay needs a frame (a caret blink).</summary>
    private protected virtual bool NeedsMotionFrame(double now) => false;

    private protected IllustrationNode AddNode(IllustrationNode node)
    {
        _nodes.Add(node);
        return node;
    }

    private protected IllustrationEdge AddEdge(IllustrationEdge edge)
    {
        _edges.Add(edge);
        return edge;
    }

    private protected IllustrationGroup AddGroup(IllustrationGroup group)
    {
        _groups.Add(group);
        return group;
    }

    private protected IllustrationLabel AddLabel(IllustrationLabel label)
    {
        _labels.Add(label);
        return label;
    }

    /// <summary>Shows a badge on <paramref name="node"/> (pops in on the snappy spring).</summary>
    private protected void ShowBadge(IllustrationNode node, string text, IllustrationTone tone, Geometry? icon = null, bool below = false)
    {
        node.Badge ??= new IllustrationBadge();
        var badge = node.Badge;
        badge.Text = text;
        badge.Tone = tone;
        badge.Icon = icon;
        badge.Below = below;
        if (_poster)
        {
            badge.Visible.Jump(1);
        }
        else
        {
            badge.Visible.Jump(Math.Min(badge.Visible.Value(_time), 0.6));
            badge.Visible.Spring(1, _time, ArcMotionPresets.Snappy);
        }
    }

    private protected void HideBadge(IllustrationNode node)
    {
        if (node.Badge is { } badge)
        {
            badge.Visible.Tween(0, _time, 0.16);
        }
    }

    /// <summary>Sends a packet along <paramref name="edge"/>; returns false when the pool is full.</summary>
    private protected bool Spawn(IllustrationEdge edge, double duration, IllustrationTone tone = IllustrationTone.Accent,
                                 bool reverse = false, double lane = 0, int tag = 0, double delay = 0, double dropAt = double.NaN,
                                 bool eased = false)
    {
        var index = _edges.IndexOf(edge);
        for (var i = 0; i < _packets.Length; i++)
        {
            if (_packets[i].Alive)
            {
                continue;
            }
            _packets[i] = new IllustrationPacket
            {
                Alive = true,
                Edge = index,
                Start = _time + delay,
                Duration = Math.Max(0.05, duration),
                Reverse = reverse,
                Tone = tone,
                Lane = lane,
                Tag = tag,
                DroppedAt = double.NaN,
                DropAt = dropAt,
                Eased = eased
            };
            return true;
        }
        return false;
    }

    /// <summary>Drops (fades out in place) every packet on <paramref name="edge"/>.</summary>
    private protected void DropPackets(IllustrationEdge edge)
    {
        var index = _edges.IndexOf(edge);
        for (var i = 0; i < _packets.Length; i++)
        {
            ref var packet = ref _packets[i];
            if (packet.Alive && packet.Edge == index && double.IsNaN(packet.DroppedAt) && packet.Start <= _time)
            {
                packet.DroppedAt = _time;
            }
            else if (packet.Alive && packet.Edge == index && packet.Start > _time)
            {
                packet.Alive = false;
            }
        }
    }

    /// <summary>Runs <paramref name="action"/> after <paramref name="seconds"/> of scene time (state-machine steps).</summary>
    private protected void After(double seconds, Action action)
    {
        if (_poster)
        {
            return;
        }
        _pending.Add((_time + seconds, action));
    }

    /// <summary>Forgets every action scheduled with <see cref="After"/>.</summary>
    private protected void CancelPending() => _pending.Clear();

    /// <summary>Removes every packet at once.</summary>
    private protected void ClearPackets()
    {
        for (var i = 0; i < _packets.Length; i++)
        {
            _packets[i].Alive = false;
        }
    }

    /// <summary>Nodes in the scene, hidden anchors included (tests).</summary>
    internal int SceneNodeCount
    {
        get
        {
            EnsureBuilt();
            return _nodes.Count;
        }
    }

    /// <summary>Packets alive now (tests).</summary>
    internal int PacketCount
    {
        get
        {
            var count = 0;
            foreach (var packet in _packets)
            {
                if (packet.Alive)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>Moves the story to <paramref name="step"/> and shows its caption.</summary>
    private protected void SetStep(int step, string caption)
    {
        if (step == _step && caption == _caption)
        {
            return;
        }
        Step = step;
        Caption = caption;
        UpdateCaption();
        AutomationProperties.SetHelpText(this, caption);
        StepChanged?.Invoke(this, new IllustrationStepChangedEventArgs(step, caption));
    }

    /// <summary>Throws away the scene and builds it again (a property that changes the diagram changed).</summary>
    private protected void Rebuild()
    {
        if (!_built)
        {
            return;
        }
        _built = false;
        _nodes.Clear();
        _edges.Clear();
        _groups.Clear();
        _labels.Clear();
        _hovered = null;
        ClearPackets();
        EnsureBuilt();
        Restart();
        InvalidateMeasure();
        _canvas?.InvalidateMeasure();
    }

    #endregion

    #region Clock and modes

    private void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }
        _built = true;
        BuildScene();
        _script.Clear();
        BuildScript(_script);
    }

    /// <summary>Advances the scene clock (the loop calls it every frame; tests call it directly).</summary>
    internal void Advance(double seconds)
    {
        EnsureBuilt();
        if (_poster || seconds <= 0)
        {
            return;
        }
        _time += seconds;
        OnTick(seconds);
        _script.Advance(ref _scriptTime, seconds);
        RunPending();
        UpdatePackets();
        InvalidateLayers();
    }

    /// <summary>
    /// The static layer (cards, text, lines) redraws only while one of its values moves, for one frame after, or when a
    /// title, status, readout or badge changed; the motion layer redraws while something travels or works.
    /// </summary>
    private void InvalidateLayers()
    {
        if (_canvas is null)
        {
            return;
        }
        var moving = IsStaticMoving(_time);
        if (_staticDirty || moving || _staticWasMoving || TextChanged())
        {
            _staticDirty = false;
            _canvas.InvalidateVisual();
        }
        _staticWasMoving = moving;
        var motion = HasMotion();
        if (motion || _motionDrawn)
        {
            _canvas.InvalidateMotion();
        }
        _motionDrawn = motion;
    }

    private bool IsStaticMoving(double now)
    {
        foreach (var node in _nodes)
        {
            if (node.ToneMix.IsMoving(now) || node.Emphasis.IsMoving(now) || node.Muted.IsMoving(now) ||
                node.Badge is { } badge && badge.Visible.IsMoving(now))
            {
                return true;
            }
        }
        foreach (var edge in _edges)
        {
            if (edge.Active.IsMoving(now) || edge.Dim.IsMoving(now) || edge.Hover.IsMoving(now) || edge.Shown.IsMoving(now))
            {
                return true;
            }
        }
        foreach (var group in _groups)
        {
            if (group.ToneMix.IsMoving(now) || group.Alarm.IsMoving(now))
            {
                return true;
            }
        }
        foreach (var label in _labels)
        {
            if (label.Opacity.IsMoving(now))
            {
                return true;
            }
        }
        return IsMoving(now);
    }

    /// <summary>A title, status, readout or badge differs from what was last drawn (reference checks, no allocation).</summary>
    private bool TextChanged()
    {
        foreach (var node in _nodes)
        {
            if (!ReferenceEquals(node.Title, node.DrawnTitle) || !ReferenceEquals(node.Subtitle, node.DrawnSubtitle) ||
                node.Badge is { } badge && (!ReferenceEquals(badge.Text, badge.DrawnText) || badge.Tone != badge.DrawnTone))
            {
                return true;
            }
        }
        foreach (var group in _groups)
        {
            if (!ReferenceEquals(group.Status, group.DrawnStatus))
            {
                return true;
            }
        }
        foreach (var label in _labels)
        {
            if (!ReferenceEquals(label.Text, label.DrawnText))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasMotion()
    {
        foreach (var packet in _packets)
        {
            if (packet.Alive)
            {
                return true;
            }
        }
        foreach (var node in _nodes)
        {
            if (node.IsBusy || node.Meter.IsMoving(_time))
            {
                return true;
            }
        }
        return NeedsMotionFrame(_time);
    }

    private void RunPending()
    {
        // Actions may schedule more actions; run what is due in time order.
        while (true)
        {
            var due = -1;
            for (var i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].At <= _time && (due < 0 || _pending[i].At < _pending[due].At))
                {
                    due = i;
                }
            }
            if (due < 0)
            {
                return;
            }
            var action = _pending[due].Act;
            _pending.RemoveAt(due);
            action();
        }
    }

    private bool StepFrame(double seconds)
    {
        Advance(seconds * Speed);
        return true;
    }

    private void UpdatePackets()
    {
        for (var i = 0; i < _packets.Length; i++)
        {
            ref var packet = ref _packets[i];
            if (!packet.Alive)
            {
                continue;
            }
            if (!double.IsNaN(packet.DroppedAt))
            {
                if (_time - packet.DroppedAt > 0.3)
                {
                    packet.Alive = false;
                }
                continue;
            }
            if (!double.IsNaN(packet.DropAt) && _time >= packet.Start + packet.Duration * packet.DropAt)
            {
                packet.DroppedAt = packet.Start + packet.Duration * packet.DropAt;
                continue;
            }
            if (_time >= packet.Start + packet.Duration)
            {
                packet.Alive = false;
                if (packet.Edge >= 0 && packet.Edge < _edges.Count)
                {
                    OnPacketArrived(_edges[packet.Edge], packet.Tag, packet.Tone);
                }
            }
        }
    }

    private void ResetScene()
    {
        ClearPackets();
        _pending.Clear();
        foreach (var node in _nodes)
        {
            node.Reset();
        }
        foreach (var edge in _edges)
        {
            edge.Active.Jump(0);
            edge.Dim.Jump(0);
            edge.Shown.Jump(1);
            edge.ActiveTone = IllustrationTone.Accent;
        }
        foreach (var group in _groups)
        {
            group.SetTone(IllustrationTone.Success, _time, animate: false);
            group.Alarm.Jump(0);
        }
        if (_hovered is { } hovered)
        {
            hovered.Emphasis.Jump(1);
        }
    }

    /// <summary>Full motion plays the story; reduced motion or Off shows the still poster frame.</summary>
    private void UpdateMode()
    {
        var full = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        if (!full && !_poster)
        {
            _poster = true;
            PseudoClasses.Set(PosterPseudoClass, true);
            Restart();
        }
        else if (full && _poster)
        {
            _poster = false;
            PseudoClasses.Set(PosterPseudoClass, false);
            Restart();
        }
    }

    /// <summary>Redraws both layers (scene rebuilt, theme or hover changed).</summary>
    private void Invalidate()
    {
        _staticDirty = true;
        _canvas?.InvalidateVisual();
        _canvas?.InvalidateMotion();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var started = _built;
        EnsureBuilt();
        UpdateMode();
        if (!started && !_poster)
        {
            Restart();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlayingProperty)
        {
            PseudoClasses.Set(PlayingPseudoClass, IsPlaying);
            if (IsPlaying)
            {
                _loop.Start();
            }
            else
            {
                _loop.Stop();
            }
            UpdatePlayGlyph();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            UpdateMode();
        }
        else if (AffectsPalette(change.Property))
        {
            _paletteDirty = true;
            Invalidate();
        }
    }

    /// <summary>Token properties (brushes, fonts, sizes) feed the cached palette.</summary>
    private static bool AffectsPalette(AvaloniaProperty property) =>
        property == ForegroundProperty || property == FontFamilyProperty ||
        (property.OwnerType == typeof(Illustration) && !property.IsDirect && property != IsPlayingProperty && property != SpeedProperty &&
         property != LabelProperty && property != IsCaptionVisibleProperty && property != IsPlaybackButtonVisibleProperty);

    #endregion

    #region Template, input, accessibility

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_play is not null)
        {
            _play.Click -= OnPlayClick;
        }
        _canvas = e.NameScope.Find<IllustrationCanvas>(CanvasPart);
        _captionSwap = e.NameScope.Find<ArcContentSwap>(CaptionPart);
        _play = e.NameScope.Find<Avalonia.Controls.Button>(PlayPart);
        if (_canvas is not null)
        {
            _canvas.Owner = this;
        }
        UpdateCaption();
        if (_play is not null)
        {
            _play.Click += OnPlayClick;
        }
        UpdatePlayGlyph();
    }

    private void UpdateCaption()
    {
        if (_captionSwap is not null)
        {
            _captionSwap.Content = new TextBlock { Text = _caption, TextWrapping = TextWrapping.Wrap };
        }
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsPlayingProperty, !IsPlaying);

    private void UpdatePlayGlyph()
    {
        if (_play is null)
        {
            return;
        }
        var name = IsPlaying ? "Pause" : "Play";
        AutomationProperties.SetName(_play, name);
        if (_play.Content is ArcContentSwap swap)
        {
            swap.Content = new ArcGlyph { Data = IsPlaying ? ArcIcons.Pause : ArcIcons.Play, Size = 14, StrokeThickness = 1.75 };
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !ReferenceEquals(e.Source, this))
        {
            return;
        }
        if (e.Key is Key.Space or Key.Enter)
        {
            SetCurrentValue(IsPlayingProperty, !IsPlaying);
            e.Handled = true;
        }
        else if (e.Key == Key.R)
        {
            Restart();
            e.Handled = true;
        }
    }

    /// <summary>The node under a scene point gets the accent halo and its connections light up.</summary>
    internal void SetHover(Point? scenePoint)
    {
        IllustrationNode? hit = null;
        if (scenePoint is { } p)
        {
            for (var i = _nodes.Count - 1; i >= 0; i--)
            {
                if (!_nodes[i].IsHidden && _nodes[i].Bounds.Inflate(4).Contains(p))
                {
                    hit = _nodes[i];
                    break;
                }
            }
        }
        if (ReferenceEquals(hit, _hovered))
        {
            return;
        }
        _hovered?.Emphasis.Tween(0, _time, 0.24);
        _hovered = hit;
        hit?.Emphasis.Tween(1, _time, 0.16);
        foreach (var edge in _edges)
        {
            var connected = hit is not null && (ReferenceEquals(edge.From, hit) || ReferenceEquals(edge.To, hit));
            edge.Hover.Tween(connected ? 1 : 0, _time, 0.2);
        }
        Invalidate();
        if (!_loop.IsRunning)
        {
            // Paused or a poster: finish the hover fade without the clock.
            foreach (var node in _nodes)
            {
                node.Emphasis.Jump(node.Emphasis.Target);
            }
            foreach (var edge in _edges)
            {
                edge.Hover.Jump(edge.Hover.Target);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new IllustrationAutomationPeer(this);

    private sealed class IllustrationAutomationPeer(Illustration owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetNameCore() => owner.Label ?? base.GetNameCore() ?? string.Empty;

        protected override string? GetHelpTextCore() => owner.Caption;
    }

    #endregion

    #region Rendering

    internal Size SceneSize => new(SceneWidth, SceneHeight);

    /// <summary>Draws the whole scene in scene coordinates (the canvas sets up the scale).</summary>
    private void EnsurePalette()
    {
        if (_paletteDirty)
        {
            _palette.Capture(this);
            _paletteDirty = false;
        }
    }

    /// <summary>The static layer: groups, lines, cards, badges, readouts (scene coordinates).</summary>
    internal void RenderStatic(DrawingContext context, double scale)
    {
        EnsureBuilt();
        EnsurePalette();
        var lod = scale < 0.5 ? 2 : scale < 0.72 ? 1 : 0;
        RenderUnderlay(context, scale);
        foreach (var group in _groups)
        {
            DrawGroup(context, group);
        }
        foreach (var edge in _edges)
        {
            DrawEdge(context, edge);
        }
        foreach (var group in _groups)
        {
            DrawGroupTitle(context, group);
            group.DrawnStatus = group.Status;
        }
        foreach (var node in _nodes)
        {
            if (!node.IsHidden)
            {
                DrawNode(context, node, lod);
            }
        }
        foreach (var node in _nodes)
        {
            if (node.Badge is { } badge)
            {
                DrawBadge(context, node, badge);
            }
        }
        foreach (var label in _labels)
        {
            DrawLabel(context, label);
            label.DrawnText = label.Text;
        }
        RenderOverlay(context, scale);
    }

    /// <summary>The motion layer: packets, busy rings and meters (scene coordinates).</summary>
    internal void RenderMotion(DrawingContext context, double scale)
    {
        EnsureBuilt();
        EnsurePalette();
        DrawPackets(context);
        var p = _palette;
        foreach (var node in _nodes)
        {
            if (node.IsHidden)
            {
                continue;
            }
            var b = node.Bounds;
            if (node.IsBusy)
            {
                var center = scale < 0.5 || string.IsNullOrEmpty(node.Title) ? b.Center : new Point(b.X + 24, b.Center.Y);
                var angle = _time * 450 % 360 * Math.PI / 180;
                using (context.PushTransform(Matrix.CreateRotation(angle) * Matrix.CreateTranslation(center.X, center.Y)))
                {
                    context.DrawGeometry(null, p.Pen(IllustrationInk.Accent, 1.5), s_busyArc);
                }
            }
            var meter = node.Meter.Value(_time);
            if (meter >= 0)
            {
                var track = new Rect(b.X + 14, b.Bottom - 6, b.Width - 28, 2);
                context.DrawRectangle(p.Brush(IllustrationInk.SubtleLine), null, new RoundedRect(track, 1));
                var fill = track.WithWidth(track.Width * Math.Clamp(meter, 0, 1));
                if (fill.Width > 0.5)
                {
                    context.DrawRectangle(p.Tint(IllustrationInk.Accent, 0.7), null, new RoundedRect(fill, 1));
                }
            }
        }
        RenderMotionOverlay(context, scale);
    }

    private void DrawGroup(DrawingContext context, IllustrationGroup group)
    {
        var p = _palette;
        var frame = new RoundedRect(group.Bounds, 16);
        context.DrawRectangle(p.Tint(IllustrationInk.SurfaceMuted, 0.55), p.Pen(IllustrationInk.StrongLine, 1, dashed: true), frame);
        var alarm = group.Alarm.Value(_time);
        if (alarm > 0.01)
        {
            context.DrawRectangle(p.Tint(IllustrationInk.Danger, 0.06 * alarm), null, frame);
        }
    }

    /// <summary>The group's health dot, title and status, drawn above the edges on a backing so lines never cross the text.</summary>
    private void DrawGroupTitle(DrawingContext context, IllustrationGroup group)
    {
        var p = _palette;
        var dot = new Point(group.Bounds.X + 16, group.Bounds.Y + 17);
        var title = p.Text(group.Title, IllustrationInk.Secondary, small: true, medium: true, mono: true);
        var status = string.IsNullOrEmpty(group.Status) ? null : p.Text(group.Status, IllustrationPalette.InkOf(group.Tone), small: true);
        var width = 9 + title.Width + (status is null ? 0 : 8 + status.Width);
        var backing = new Rect(dot.X - 8, dot.Y - title.Height / 2 - 3, width + 14, title.Height + 6);
        context.DrawRectangle(p.Brush(IllustrationInk.Surface), null, new RoundedRect(backing, backing.Height / 2));
        context.DrawRectangle(p.Tint(IllustrationInk.SurfaceMuted, 0.55), null, new RoundedRect(backing, backing.Height / 2));
        var mix = Math.Clamp(group.ToneMix.Value(_time), 0, 1);
        if (mix < 1)
        {
            context.DrawEllipse(p.Tint(IllustrationPalette.InkOf(group.PreviousTone), 1 - mix), null, dot, 3.5, 3.5);
        }
        context.DrawEllipse(p.Tint(IllustrationPalette.InkOf(group.Tone), mix), null, dot, 3.5, 3.5);
        context.DrawText(title, new Point(dot.X + 9, dot.Y - title.Height / 2));
        if (status is not null)
        {
            context.DrawText(status, new Point(dot.X + 9 + title.Width + 8, dot.Y - status.Height / 2));
        }
    }

    private void DrawEdge(DrawingContext context, IllustrationEdge edge)
    {
        var p = _palette;
        var shown = Math.Clamp(edge.Shown.Value(_time), 0, 1);
        if (shown <= 0.01)
        {
            return;
        }
        using var visible = context.PushOpacity(shown);
        var dim = edge.Dim.Value(_time);
        using (context.PushOpacity(1 - 0.65 * Math.Clamp(dim, 0, 1)))
        {
            context.DrawGeometry(null, p.Pen(IllustrationInk.StrongLine, 1, edge.IsDashed), edge.Geometry);
        }
        var active = Math.Max(edge.Active.Value(_time), edge.Hover.Value(_time));
        if (active > 0.01)
        {
            using (context.PushOpacity(Math.Clamp(active, 0, 1)))
            {
                context.DrawGeometry(null, p.Pen(IllustrationPalette.InkOf(edge.ActiveTone), 1.5, edge.IsDashed), edge.Geometry);
            }
        }
    }

    private void DrawPackets(DrawingContext context)
    {
        var p = _palette;
        var size = PacketSize;
        for (var i = 0; i < _packets.Length; i++)
        {
            ref var packet = ref _packets[i];
            if (!packet.Alive || packet.Start > _time || packet.Edge < 0 || packet.Edge >= _edges.Count)
            {
                continue;
            }
            var edge = _edges[packet.Edge];
            double t;
            var opacity = 1.0;
            var ink = IllustrationPalette.InkOf(packet.Tone);
            if (!double.IsNaN(packet.DroppedAt))
            {
                t = Math.Clamp((packet.DroppedAt - packet.Start) / packet.Duration, 0, 1);
                t = packet.Eased ? ArcEasings.InOut.Ease(t) : t;
                opacity = 1 - Math.Clamp((_time - packet.DroppedAt) / 0.3, 0, 1);
                ink = IllustrationInk.Danger;
            }
            else
            {
                t = Math.Clamp((_time - packet.Start) / packet.Duration, 0, 1);
                t = packet.Eased ? ArcEasings.InOut.Ease(t) : t;
            }
            var (point, direction) = edge.Sample(packet.Reverse ? 1 - t : t);
            if (packet.Lane != 0)
            {
                point += new Vector(-direction.Y, direction.X) * packet.Lane;
            }
            var grow = Math.Clamp((_time - packet.Start) / 0.08, 0, 1);
            var radius = size / 2 * (0.4 + 0.6 * grow);
            // Fade in and out at the ends: the motion layer is above the cards the packet leaves and reaches.
            var along = packet.Reverse ? 1 - t : t;
            opacity *= Math.Clamp(Math.Min(along, 1 - along) / 0.08, 0, 1);
            if (opacity <= 0.01)
            {
                continue;
            }
            if (opacity >= 0.99)
            {
                context.DrawEllipse(p.Tint(ink, 0.14), null, point, radius * 1.7, radius * 1.7);
                context.DrawEllipse(p.Brush(ink), null, point, radius, radius);
                continue;
            }
            using (context.PushOpacity(opacity))
            {
                context.DrawEllipse(p.Tint(ink, 0.14), null, point, radius * 1.7, radius * 1.7);
                context.DrawEllipse(p.Brush(ink), null, point, radius, radius);
            }
        }
    }

    private void DrawNode(DrawingContext context, IllustrationNode node, int lod)
    {
        var p = _palette;
        var b = node.Bounds;
        var radius = NodeRadius;
        var muted = Math.Clamp(node.Muted.Value(_time), 0, 1);
        using var dimmed = context.PushOpacity(1 - 0.55 * muted);
        var emphasis = Math.Clamp(node.Emphasis.Value(_time), 0, 1);
        if (emphasis > 0.01)
        {
            context.DrawRectangle(p.Tint(IllustrationInk.Accent, 0.14 * emphasis), null, new RoundedRect(b.Inflate(4), radius + 4));
        }
        var card = new RoundedRect(b, radius);
        context.DrawRectangle(p.Brush(IllustrationInk.Surface), p.Pen(IllustrationInk.Line, 1), card, p.Shadow);
        var mix = Math.Clamp(node.ToneMix.Value(_time), 0, 1);
        DrawToneLayer(context, card, node.PreviousTone, 1 - mix);
        DrawToneLayer(context, card, node.Tone, mix);

        var tile = new Rect(b.X + 10, b.Center.Y - 14, 28, 28);
        if (lod == 2 || string.IsNullOrEmpty(node.Title))
        {
            tile = new Rect(b.Center.X - 14, b.Center.Y - 14, 28, 28);
        }
        var tileRect = new RoundedRect(tile, 8);
        context.DrawRectangle(p.Brush(IllustrationInk.SurfaceMuted), null, tileRect);
        DrawToneTile(context, tileRect, node.PreviousTone, 1 - mix);
        DrawToneTile(context, tileRect, node.Tone, mix);
        if (node.Icon is { } icon)
        {
            var glyph = new Rect(tile.Center.X - 8, tile.Center.Y - 8, 16, 16);
            DrawIcon(context, icon, glyph, InkFor(node.PreviousTone), 1 - mix);
            DrawIcon(context, icon, glyph, InkFor(node.Tone), mix);
        }
        if (lod < 2 && !string.IsNullOrEmpty(node.Title))
        {
            var x = tile.Right + 10;
            var room = Math.Max(8, b.Right - x - 8);
            var title = p.Text(node.Title, IllustrationInk.Foreground, small: false, medium: true, maxWidth: room);
            var subtitle = lod == 0 && !string.IsNullOrEmpty(node.Subtitle) ? p.Text(node.Subtitle!, IllustrationInk.Muted, maxWidth: room) : null;
            var height = title.Height + (subtitle?.Height ?? 0);
            var y = b.Center.Y - height / 2;
            context.DrawText(title, new Point(x, y));
            if (subtitle is not null)
            {
                context.DrawText(subtitle, new Point(x, y + title.Height));
            }
        }
        node.DrawnTitle = node.Title;
        node.DrawnSubtitle = node.Subtitle;
    }

    private static IllustrationInk InkFor(IllustrationTone tone) => tone == IllustrationTone.Neutral ? IllustrationInk.Secondary : IllustrationPalette.InkOf(tone);

    private void DrawToneLayer(DrawingContext context, RoundedRect card, IllustrationTone tone, double amount)
    {
        if (tone == IllustrationTone.Neutral || amount <= 0.01)
        {
            return;
        }
        var ink = IllustrationPalette.InkOf(tone);
        using (context.PushOpacity(amount))
        {
            context.DrawRectangle(_palette.Tint(ink, 0.06), _palette.TintPen(ink, 0.45, 1), card);
        }
    }

    private void DrawToneTile(DrawingContext context, RoundedRect tile, IllustrationTone tone, double amount)
    {
        if (tone == IllustrationTone.Neutral || amount <= 0.01)
        {
            return;
        }
        context.DrawRectangle(_palette.Tint(IllustrationPalette.InkOf(tone), 0.14 * amount), null, tile);
    }

    /// <summary>Draws a 24×24 Lucide glyph into <paramref name="target"/> with a 1.75px stroke.</summary>
    private protected void DrawIcon(DrawingContext context, Geometry icon, Rect target, IllustrationInk ink, double opacity = 1)
    {
        if (opacity <= 0.01)
        {
            return;
        }
        var scale = target.Width / 24;
        using (context.PushOpacity(opacity))
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(target.X, target.Y)))
        {
            context.DrawGeometry(null, _palette.Pen(ink, 1.75 / scale), icon);
        }
    }

    private void DrawBadge(DrawingContext context, IllustrationNode node, IllustrationBadge badge)
    {
        badge.DrawnText = badge.Text;
        badge.DrawnTone = badge.Tone;
        var v = badge.Visible.Value(_time);
        if (v <= 0.01)
        {
            return;
        }
        var p = _palette;
        var ink = InkFor(badge.Tone);
        var text = p.Text(badge.Text, ink, small: true, medium: true);
        var width = 16 + text.Width + (badge.Icon is null ? 0 : 16);
        const double height = 22;
        var x = node.Bounds.Center.X - width / 2;
        var y = badge.Below ? node.Bounds.Bottom + 8 : node.Bounds.Top - 8 - height;
        var rect = new Rect(x, y, width, height);
        var scale = 0.6 + 0.4 * Math.Max(0, v);
        var center = rect.Center;
        using (context.PushOpacity(Math.Clamp(v, 0, 1)))
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            var pill = new RoundedRect(rect, height / 2);
            context.DrawRectangle(p.Brush(IllustrationInk.Raised), p.Pen(IllustrationInk.Line, 1), pill);
            if (badge.Tone != IllustrationTone.Neutral)
            {
                var toneInk = IllustrationPalette.InkOf(badge.Tone);
                context.DrawRectangle(p.Tint(toneInk, 0.1), p.TintPen(toneInk, 0.4, 1), pill);
            }
            var tx = x + 8;
            if (badge.Icon is { } icon)
            {
                DrawIcon(context, icon, new Rect(tx, y + 5, 12, 12), ink);
                tx += 16;
            }
            context.DrawText(text, new Point(tx, y + (height - text.Height) / 2));
        }
    }

    private void DrawLabel(DrawingContext context, IllustrationLabel label)
    {
        var opacity = label.Opacity.Value(_time);
        if (opacity <= 0.01 || string.IsNullOrEmpty(label.Text))
        {
            return;
        }
        var text = _palette.Text(label.Text, label.Ink, label.IsSmall, label.IsMedium, label.IsMono);
        var x = label.Align switch
        {
            0 => label.Anchor.X - text.Width / 2,
            > 0 => label.Anchor.X - text.Width,
            _ => label.Anchor.X
        };
        using (context.PushOpacity(Math.Clamp(opacity, 0, 1)))
        {
            context.DrawText(text, new Point(x, label.Anchor.Y));
        }
    }

    private static StreamGeometry CreateBusyArc()
    {
        // Three quarters of a 17px ring around a 28px tile, centred on the origin.
        const double r = 17;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(r, 0), false);
            context.ArcTo(new Point(0, -r), new Size(r, r), 0, true, SweepDirection.Clockwise);
            context.EndFigure(false);
        }
        return geometry;
    }

    #endregion
}
