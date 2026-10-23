using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>What <see cref="SemanticZoomStage"/> asks of the <see cref="SemanticZoom"/> that hosts it.</summary>
internal interface ISemanticZoomStageHost
{
    /// <summary>The scope under the camera changed (during flights, pans and zooms too).</summary>
    void OnStageScopeChanged(SemanticZoomScope scope);

    /// <summary>The rendered camera moved (readout, minimap, card anchor).</summary>
    void OnStageCamera();

    /// <summary>The pointer moved over <paramref name="target"/> (a node or a checklist item; null for none, position null: left).</summary>
    void OnStageHover(Point? position, object? target);

    /// <summary>A click on <paramref name="target"/> (a node or a checklist item; null: empty space).</summary>
    void OnStageTap(object? target);

    /// <summary>A manual camera input (wheel, pinch, drag, keys, buttons): stops the tour and retires the hint.</summary>
    void OnStageInput();

    /// <summary>A key on the focused stage; true when handled.</summary>
    bool OnStageKey(KeyEventArgs e);

    /// <summary>0 / Home / Fit: back to the whole organization.</summary>
    void OnStageReset();

    /// <summary>The screen-reader name of the stage.</summary>
    string? StageSummary { get; }
}

/// <summary>
/// The self-drawn map of <see cref="SemanticZoom"/> (template part <c>PART_Stage</c>): a logarithmic dot grid, team panels,
/// project cards, task chips that become blocks and then task details, people rows; every level of detail fades in by
/// on-screen size, text through <see cref="ShowpieceTextCache"/>. A 2D camera (fit … 53×, pointer-anchored wheel zoom
/// following on a critically damped ω 25 spring, rubber band past the limits, 1:1 pans with a glide, van Wijk flights of
/// <c>460 + 380·S</c> ms) frames the organization, and the scope (breadcrumbs, panel) is derived from it.
/// </summary>
/// <remarks>
/// Code-driven motion: the camera, the hover outlines, the dimming and the level-of-detail fades all advance in the
/// stage's single frame loop with explicit steps (see <see cref="ShowpieceStage"/>); a ControlTheme cannot drive
/// self-drawn scene state.
/// </remarks>
internal sealed partial class SemanticZoomStage : ShowpieceStage
{
    /// <summary>Safe area at desktop sizes: 24 at the sides, 64 on top (breadcrumbs, tour), 60 below (zoom bar).</summary>
    public static readonly Thickness WideInsets = new(24, 64, 24, 60);

    /// <summary>Safe area in containers under 600 px: 12 at the sides, 56 above and below.</summary>
    public static readonly Thickness CompactInsets = new(12, 56, 12, 56);

    /// <summary>Framing fill of teams and projects (0.97) and of tasks (0.96).</summary>
    public const double TeamFill = 0.97;

    public const double TaskFill = 0.96;

    /// <summary>The scope is read 2 px below the safe-area center.</summary>
    public const double ScopeProbeOffset = 2;

    private readonly ShowpieceCamera2D _camera = new()
    {
        MinZoom = 1,
        MaxZoom = 53.04,
        Insets = WideInsets,
        ZoomOverflow = ShowpieceZoomOverflow.RubberBand,
        HasInertia = true,
        FlightMode = ShowpieceFlightMode.VanWijk,
        FlySpring = ShowpieceSprings.CameraFollow
    };

    private readonly SemanticZoomScene _scene = new();
    private SemanticZoomLabels _labels = SemanticZoomLabels.Empty;
    private ISemanticZoomStageHost? _host;
    private bool _compact;
    private int _cursorItem = -1;
    private bool _loaded;
    private bool _sized;

    public SemanticZoomStage()
    {
        WheelMode = ShowpieceWheelMode.Zoom;
        KeyZoomFactor = 1.5;
        RoleDescription = "zoomable map";
        Camera = _camera;
        _camera.Changed += OnCameraMoved;
    }

    internal ISemanticZoomStageHost? Host
    {
        get => _host;
        set => _host = value;
    }

    /// <summary>The camera (tests and the host).</summary>
    internal ShowpieceCamera2D View => _camera;

    /// <summary>The scene state (tests and the host).</summary>
    internal SemanticZoomScene Scene => _scene;

    internal SemanticZoomLayout Layout => _scene.Layout;

    /// <summary>Text shown on the map (rebuilt by the host when data or wording change).</summary>
    internal SemanticZoomLabels Labels
    {
        get => _labels;
        set
        {
            _labels = value;
            InvalidateVisual();
        }
    }

    /// <summary>Containers under 600 px: smaller safe area.</summary>
    internal bool IsCompact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }
            _compact = value;
            var zoom = _camera.TargetZoom;
            var center = _camera.TargetCenter;
            var atFit = !_sized || _camera.IsAtFit;
            _camera.Insets = value ? CompactInsets : WideInsets;
            if (atFit || !_loaded)
            {
                _camera.JumpTo(_camera.FitCenter, 1);
            }
            else
            {
                _camera.JumpTo(center, zoom);
            }
        }
    }

    /// <summary>Largest readout zoom.</summary>
    internal double MaxZoom
    {
        get => _camera.MaxZoom;
        set => _camera.MaxZoom = Math.Max(1, value);
    }

    /// <summary>The scope under the camera.</summary>
    internal SemanticZoomScope CurrentScope => _scene.Scope;

    /// <summary>Texts built so far by the text cache (tests and the frame-cost harness).</summary>
    internal int TextCacheMisses => TextCache.Misses;

    /// <summary>The checklist item under the keyboard cursor (−1 none).</summary>
    internal int CursorItem => _cursorItem;

    private bool Animates => MotionLevel == ArcMotionLevel.Full;

    // Scene ------------------------------------------------------------------------------------------------------------

    /// <summary>New data. <paramref name="keepView"/>: keep the camera where it is (same structure, new values).</summary>
    internal void Load(SemanticZoomLayout layout, bool keepView)
    {
        var center = _camera.TargetCenter;
        var zoom = _camera.TargetZoom;
        _camera.SetContent(layout.ContentBounds);
        if (keepView && _loaded)
        {
            _camera.JumpTo(center, zoom);
        }
        else
        {
            _camera.Fit(animate: false);
        }
        _loaded = true;
        _scene.Animates = Animates;
        _scene.Load(layout, _camera.Scale);
        _cursorItem = -1;
        UpdateScope(force: true);
        InvalidateVisual();
    }

    /// <summary>Flies (or jumps) so box <paramref name="box"/> fills the safe area; −1 fits the organization.</summary>
    internal void FlyToBox(int box, bool animate = true)
    {
        var layout = _scene.Layout;
        if (box < 0 || box >= layout.Boxes.Length)
        {
            FitView(animate);
            return;
        }
        var b = layout.Boxes[box];
        var fill = b.Kind == SemanticZoomNodeKind.Task ? TaskFill : TeamFill;
        if (animate && Animates)
        {
            _camera.FlyToRect(b.Rect, fill);
        }
        else
        {
            var (center, zoom) = _camera.FrameRect(b.Rect, fill);
            _camera.JumpTo(center, zoom);
        }
        RequestFrames();
    }

    /// <summary>Back to zoom 1 (animated at full motion).</summary>
    internal void FitView(bool animate = true)
    {
        _camera.Fit(animate && Animates);
        RequestFrames();
    }

    /// <summary>Zooms by <paramref name="factor"/> about <paramref name="anchor"/> (stage px; default the safe-area center).</summary>
    internal void ZoomAbout(double factor, Point? anchor = null)
    {
        _camera.ZoomBy(factor, anchor);
        RequestFrames();
    }

    /// <summary>Moves the camera center to a world point keeping the zoom (minimap), following on the ω 25 spring.</summary>
    internal void CenterOn(Point world)
    {
        if (Animates)
        {
            _camera.FlyTo(world, _camera.TargetZoom, ShowpieceFlightMode.Spring);
        }
        else
        {
            _camera.JumpTo(world, _camera.TargetZoom);
        }
        RequestFrames();
    }

    /// <summary>Sets the keyboard cursor: a node box, or (with <paramref name="item"/> ≥ 0) a checklist item of the scoped task.</summary>
    internal void SetCursor(int box, int item = -1)
    {
        _cursorItem = item;
        _scene.SetCursor(item >= 0 ? -1 : box);
        InvalidateVisual();
        RequestFrames();
    }

    private void OnCameraMoved()
    {
        UpdateScope(force: false);
        _host?.OnStageCamera();
    }

    private void UpdateScope(bool force)
    {
        _scene.SetScale(_camera.Scale);
        var probe = _camera.SafeCenter + new Vector(0, ScopeProbeOffset);
        var scope = _scene.ScopeAt(_camera.ScreenToWorld(probe), _camera.Zoom);
        if (!force && scope == _scene.Scope)
        {
            return;
        }
        var previous = _scene.Scope;
        _scene.SetScope(scope, immediate: force);
        if (scope.Task != previous.Task)
        {
            _cursorItem = -1;
        }
        _host?.OnStageScopeChanged(scope);
        RequestFrames();
    }

    // Frames -----------------------------------------------------------------------------------------------------------

    protected override bool OnAdvance(double dt) => _scene.Step(dt);

    protected override void OnLoopStopped()
    {
        // Resting frame: whatever still smooths lands on its target.
        _scene.Step(1);
    }

    protected override void OnMotionLevelChanged(ArcMotionLevel level)
    {
        _scene.Animates = level == ArcMotionLevel.Full;
        InvalidateVisual();
    }

    protected override void OnThemeChanged()
    {
        _paletteDirty = true;
        _pens.Clear();
        _avatarCache.Clear();
    }

    // Input --------------------------------------------------------------------------------------------------------------

    protected override object? HitTestItem(Point position) => TargetAt(position);

    /// <summary>The node or checklist item at a stage point under the current hit rules (null for none).</summary>
    internal object? TargetAt(Point position)
    {
        var layout = _scene.Layout;
        if (layout.IsEmpty)
        {
            return null;
        }
        var item = ChecklistItemAt(position);
        if (item >= 0 && layout.Boxes[_scene.Scope.Task].Node is SemanticZoomTask task)
        {
            return task.Checklist[item];
        }
        var box = _scene.HitTest(_camera.ScreenToWorld(position));
        return box >= 0 ? layout.Boxes[box].Node : null;
    }

    /// <summary>The checklist item of the scoped task at a stage point, when its rows are shown (−1 none).</summary>
    internal int ChecklistItemAt(Point position)
    {
        var count = ChecklistRowsShown();
        for (var i = 0; i < count; i++)
        {
            if (ChecklistItemRect(i).Contains(position))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>How many checklist rows of the scoped task are on its card now (0 when the checklist is not shown).</summary>
    internal int ChecklistRowsShown()
    {
        var scope = _scene.Scope;
        if (scope.Task < 0 || _scene.LodTarget(SemanticZoomLayer.Checklist) < 0.5)
        {
            return 0;
        }
        var box = _scene.Layout.Boxes[scope.Task];
        if (box.Node is not SemanticZoomTask task)
        {
            return 0;
        }
        var g = new SemanticZoomTaskGeometry(ToScreen(box.Rect), _camera.Scale);
        var shown = 0;
        while (shown < task.Checklist.Count && g.ItemRect(shown).Bottom <= g.Bottom)
        {
            shown++;
        }
        return shown;
    }

    /// <summary>Stage rectangle of checklist row <paramref name="i"/> of the scoped task.</summary>
    internal Rect ChecklistItemRect(int i)
    {
        var scope = _scene.Scope;
        if (scope.Task < 0)
        {
            return default;
        }
        var g = new SemanticZoomTaskGeometry(ToScreen(_scene.Layout.Boxes[scope.Task].Rect), _camera.Scale);
        return g.ItemRect(i);
    }

    /// <summary>Stage rectangle of box <paramref name="box"/>.</summary>
    internal Rect BoxOnScreen(int box) =>
        box >= 0 && box < _scene.Layout.Boxes.Length ? ToScreen(_scene.Layout.Boxes[box].Rect) : default;

    protected override void OnHoverChanged(Point? position, object? item)
    {
        var box = item is SemanticZoomNode node ? _scene.Layout.IndexOf(node) : -1;
        if (box != _scene.Hovered)
        {
            _scene.SetHovered(box);
            RequestFrames();
            InvalidateVisual();
        }
        _host?.OnStageHover(position, item);
    }

    protected override void OnTap(Point position, object? item, KeyModifiers modifiers) => _host?.OnStageTap(item);

    protected override void OnDragStarted(Point position)
    {
        _host?.OnStageInput();
        base.OnDragStarted(position);
    }

    protected override void OnZoom(double factor, Point? anchor, ShowpieceZoomSource source)
    {
        _host?.OnStageInput();
        base.OnZoom(factor, anchor, source);
        RequestFrames();
    }

    protected override void OnResetView()
    {
        if (_host is not null)
        {
            _host.OnStageReset();
            return;
        }
        FitView();
    }

    protected override bool OnStageKeyDown(KeyEventArgs e)
    {
        if (_host?.OnStageKey(e) == true)
        {
            return true;
        }
        return base.OnStageKeyDown(e);
    }

    protected override string? GetAccessibleSummary() => _host?.StageSummary;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        // A view at fit stays at fit (the fit scale changes with the size); otherwise the readout zoom and center stay.
        var atFit = !_sized || _camera.IsAtFit;
        base.OnSizeChanged(e);
        _sized = e.NewSize.Width > 0 && e.NewSize.Height > 0;
        if (atFit && !_camera.IsFlying)
        {
            _camera.JumpTo(_camera.FitCenter, 1);
        }
        UpdateScope(force: false);
    }
}
