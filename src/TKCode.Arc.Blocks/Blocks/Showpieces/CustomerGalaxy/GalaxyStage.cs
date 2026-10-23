using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>What <see cref="GalaxyStage"/> asks of the <see cref="CustomerGalaxy"/> that hosts it.</summary>
internal interface IGalaxyStageHost
{
    /// <summary>The pointer moved over the stage (<paramref name="position"/> null: it left) above <paramref name="account"/>.</summary>
    void OnStageHover(Point? position, GalaxyAccount? account);

    /// <summary>A click on <paramref name="account"/> (null: empty space).</summary>
    void OnStageTap(GalaxyAccount? account);

    /// <summary>A double click on cluster <paramref name="group"/> (−1: none).</summary>
    void OnStageDoubleTap(int group);

    /// <summary>A key on the focused stage; true when handled.</summary>
    bool OnStageKey(KeyEventArgs e);

    /// <summary>0 / Home / Fit: back to every cluster.</summary>
    void OnStageReset();

    /// <summary>The scene moved (camera or dots): labels and the card follow.</summary>
    void OnStageFrame();

    /// <summary>The screen-reader summary of the stage.</summary>
    string? StageSummary { get; }

    /// <summary>The label size (px) of cluster <paramref name="group"/> of the current layout.</summary>
    Size LabelSize(int group);
}

/// <summary>
/// The self-drawn scene of <see cref="CustomerGalaxy"/> (template part <c>PART_Stage</c>): every account a dot,
/// drawn in batches per (health color, dimming level) with cached immutable brushes, the hovered dot grown and ringed,
/// the selected one ringed. A 2D camera (fit … 9× fit, pointer-anchored wheel zoom on ω 10.3, flights on ω 5.7, pans
/// 1:1 with a rubber band and no glide, bounds applied after the gesture) frames the overview spiral or the clusters.
/// Hit testing uses a screen grid of 24 px cells (nearest dot within its radius + 10 px).
/// </summary>
/// <remarks>
/// Code-driven motion: rotation, drift, per-dot springs, dimming and hover growth all advance in the stage's single frame
/// loop with explicit steps (see <see cref="ShowpieceStage"/>); a ControlTheme cannot drive self-drawn scene state.
/// </remarks>
internal sealed class GalaxyStage : ShowpieceStage
{
    /// <summary>A dot is hit within its radius plus this many px (Arc: a 2.5 px dot at 10.6 px, a 9 px dot at 13 px).</summary>
    public const double HitTolerance = 10;

    /// <summary>The hovered dot grows to 1.84× (at least 1.5 px), starting at 1.3×, at 10.4/s; the ring sits 3 px out.</summary>
    public const double HoverGrowth = 1.84;

    public const double HoverStartGrowth = 1.3;

    public const double HoverMinRadius = 1.5;

    public const double HoverRate = 10.4;

    public const double RingGap = 3;

    public const double RingThickness = 1.5;

    /// <summary>Alpha of the hovered dot's overlay.</summary>
    public const double HoverAlpha = 0.95;

    private const double GridCell = 24;

    private readonly ShowpieceCamera2D _camera = new()
    {
        MinZoom = 1,
        MaxZoom = 9,
        Insets = new Thickness(14),
        BoundsMode = ShowpieceBoundsMode.AfterGesture,
        FollowSpring = ShowpieceSprings.ZoomFollow,
        FlySpring = ShowpieceSprings.FlyFollow,
        SettleSpring = ShowpieceSprings.Recenter
    };

    private readonly GalaxyField _field = new();
    private IGalaxyStageHost? _host;
    private ImmutableSolidColorBrush[] _brushes = Array.Empty<ImmutableSolidColorBrush>();
    private ImmutableSolidColorBrush[] _hoverBrushes = Array.Empty<ImmutableSolidColorBrush>();
    private Color[] _stateColors = Array.Empty<Color>();
    private ImmutablePen? _ringPen;
    private bool _colorsDirty = true;
    private int _hovered = -1;
    private int _selected = -1;
    private double _growth = HoverGrowth;
    private Point? _pointer;
    private int _focusGroup = -1;
    private Rect _baseContent;
    private double _fit = 1;

    // Hit-test grid (screen space), rebuilt lazily when the scene or the camera moved.
    private int[] _cellStart = Array.Empty<int>();
    private int[] _cellItems = Array.Empty<int>();
    private int[] _cellOf = Array.Empty<int>();
    private int _gridColumns;
    private int _gridRows;
    private int _sceneVersion;
    private int _gridVersion = -1;
    private int _gridCamera = -1;
    private double _gridMaxRadius;

    public GalaxyStage()
    {
        WheelMode = ShowpieceWheelMode.NotchedZoom;
        KeyZoomFactor = 1.6;
        RoleDescription = "interactive chart";
        Camera = _camera;
        _camera.Changed += OnCameraMoved;
    }

    internal IGalaxyStageHost? Host
    {
        get => _host;
        set => _host = value;
    }

    /// <summary>The camera (tests and the host).</summary>
    internal ShowpieceCamera2D View => _camera;

    /// <summary>The dots (tests and the host).</summary>
    internal GalaxyField Field => _field;

    /// <summary>Spins the scene (the host turns it off for Reduced / Off motion or when auto-rotation is off).</summary>
    internal bool IsRotating { get; set; } = true;

    /// <summary>Rotation speed in rad/s.</summary>
    internal double RotationSpeed { get; set; } = GalaxyLayout.RotationSpeed;

    /// <summary>The dot under the pointer (−1 none).</summary>
    internal int HoveredIndex => _hovered;

    /// <summary>The selected dot (−1 none).</summary>
    internal int SelectedIndex => _selected;

    /// <summary>The pointer over the stage (null when outside).</summary>
    internal Point? PointerPosition => _pointer;

    /// <summary>The current fit scale.</summary>
    internal double FitScale => _fit;

    /// <summary>The cluster the camera framed last (−1 none).</summary>
    internal int FocusGroup => _focusGroup;

    /// <summary>Current hover growth factor (tests).</summary>
    internal double HoverGrowthValue => _growth;

    private bool Animates => MotionLevel == ArcMotionLevel.Full;

    // Scene ----------------------------------------------------------------------------------------------------------

    /// <summary>New data: every dot jumps to its slot and the camera fits.</summary>
    internal void Load(GalaxyModel model, GalaxyGroupSet? layout)
    {
        _hovered = -1;
        _selected = -1;
        _focusGroup = -1;
        _field.Load(model, layout);
        UpdateFraming();
        _camera.Fit(animate: false);
        SceneMoved();
        RequestFrames();
    }

    /// <summary>A grouping change: dots spring to their new slots and the camera flies to the new fit.</summary>
    internal void ShowLayout(GalaxyGroupSet? layout)
    {
        _focusGroup = -1;
        _field.MorphTo(layout, Animates);
        _field.DimAllBut(-1, Animates);
        UpdateFraming();
        if (Animates)
        {
            _camera.Fit();
        }
        else
        {
            _camera.Fit(animate: false);
        }
        SceneMoved();
        RequestFrames();
    }

    /// <summary>Dims every group of the dim set except <paramref name="group"/> (−1 restores them).</summary>
    internal void Dim(int group)
    {
        _field.DimAllBut(group, Animates);
        InvalidateVisual();
        RequestFrames();
    }

    /// <summary>Flies the camera into cluster <paramref name="group"/> of the layout (spring ω 5.7); −1 returns to fit.</summary>
    internal void FlyToGroup(int group, bool animate = true)
    {
        _focusGroup = group;
        UpdateFraming();
        var layout = _field.Layout;
        if (group < 0 || layout is not { HasGeometry: true } || group >= layout.Count)
        {
            _camera.Fit(animate && Animates);
            return;
        }
        var (center, scale) = FlightFor(layout, group);
        if (animate && Animates)
        {
            _camera.FlyTo(center, scale / _fit);
        }
        else
        {
            _camera.JumpTo(center, scale / _fit);
        }
        RequestFrames();
    }

    /// <summary>Centers the camera on dot <paramref name="index"/>, zoomed in so it reads (at least 3× fit).</summary>
    internal void FlyToDot(int index)
    {
        if (index < 0 || index >= _field.Count)
        {
            return;
        }
        var zoom = Math.Max(_camera.TargetZoom, 3);
        var point = new Point(_field.X[index], _field.Y[index]);
        var k = _fit * Math.Min(zoom, _camera.MaxZoom);
        // Let the bounds include the dot's frame so the flight can center it.
        var half = new Size(Bounds.Width / 2 / k, Bounds.Height / 2 / k);
        var frame = new Rect(point.X - half.Width, point.Y - half.Height, half.Width * 2, half.Height * 2);
        _camera.SetContent(_baseContent.Union(frame), _fit);
        if (Animates)
        {
            _camera.FlyTo(point, zoom);
        }
        else
        {
            _camera.JumpTo(point, zoom);
        }
        RequestFrames();
    }

    private (Point Center, double Scale) FlightFor(GalaxyGroupSet layout, int group)
    {
        var size = Bounds.Size;
        var (center, scale) = GalaxyLayout.ClusterFlight(layout.Centers[group], layout.Radii[group], size);
        scale = Math.Clamp(scale, _fit, _fit * _camera.MaxZoom);
        // Recompute the vertical offset for the clamped scale.
        var label = GalaxyLayout.LabelGap + GalaxyLayout.LabelHeight;
        center = new Point(layout.Centers[group].X, layout.Centers[group].Y - label / 2 / scale);
        return (center, scale);
    }

    /// <summary>
    /// Fit and camera content for the current layout and stage size; while a cluster is framed the content also covers
    /// that flight's view, so the bounds never pull a framed edge cluster off center.
    /// </summary>
    private void UpdateFraming()
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            size = new Size(663, 360);
        }
        var layout = _field.Layout;
        if (layout is { HasGeometry: true })
        {
            _fit = layout.Fit(size, out _baseContent);
        }
        else
        {
            _fit = GalaxyLayout.AllFit(size, _field.Model.CoreRadius);
            _baseContent = GalaxyLayout.AllContent(_field.Model.CoreRadius, _fit);
        }
        var content = _baseContent;
        if (_focusGroup >= 0 && layout is { HasGeometry: true } && _focusGroup < layout.Count)
        {
            var (center, scale) = FlightFor(layout, _focusGroup);
            var inset = _camera.Insets;
            var w = (size.Width - inset.Left - inset.Right) / scale;
            var h = (size.Height - inset.Top - inset.Bottom) / scale;
            content = content.Union(new Rect(center.X - w / 2, center.Y - h / 2, w, h));
        }
        _camera.SetContent(content, _fit);
    }

    /// <summary>Restores the camera content without the framed cluster (before a Fit).</summary>
    internal void ClearFocusFraming()
    {
        _focusGroup = -1;
        UpdateFraming();
    }

    /// <summary>Selects dot <paramref name="index"/> (−1 clears): it keeps a ring.</summary>
    internal void Select(int index)
    {
        if (_selected == index)
        {
            return;
        }
        _selected = index;
        InvalidateVisual();
    }

    /// <summary>The cluster at a stage point (disc + 10 px, or its label), −1 when none.</summary>
    internal int ClusterAt(Point p)
    {
        var layout = _field.Layout;
        if (layout is not { HasGeometry: true } || _host is null)
        {
            return -1;
        }
        var k = _camera.Scale;
        for (var g = 0; g < layout.Count; g++)
        {
            var c = _camera.WorldToScreen(layout.Centers[g]);
            var r = layout.Radii[g] * k;
            var dx = p.X - c.X;
            var dy = p.Y - c.Y;
            if (dx * dx + dy * dy <= (r + HitTolerance) * (r + HitTolerance))
            {
                return g;
            }
            if (LabelRect(g).Contains(p))
            {
                return g;
            }
        }
        return -1;
    }

    /// <summary>Screen rectangle of cluster <paramref name="group"/>'s label (centered, 10 px above the disc).</summary>
    internal Rect LabelRect(int group)
    {
        var layout = _field.Layout;
        if (layout is not { HasGeometry: true } || group < 0 || group >= layout.Count || _host is null)
        {
            return default;
        }
        var c = _camera.WorldToScreen(layout.Centers[group]);
        var size = _host.LabelSize(group);
        return GalaxyLayout.LabelRect(c, layout.Radii[group] * _camera.Scale, size, GalaxyLayout.LabelGap);
    }

    /// <summary>Screen position and radius of dot <paramref name="index"/>.</summary>
    internal (Point Center, double Radius) DotOnScreen(int index)
    {
        if (index < 0 || index >= _field.Count)
        {
            return default;
        }
        return (_camera.WorldToScreen(new Point(_field.X[index], _field.Y[index])), _field.R[index] * _camera.Scale);
    }

    private void SceneMoved()
    {
        _sceneVersion++;
        InvalidateVisual();
        _host?.OnStageFrame();
    }

    private void OnCameraMoved()
    {
        _host?.OnStageFrame();
    }

    // Frames ---------------------------------------------------------------------------------------------------------

    /// <summary>One frame by hand (tests and the snapshot harness): the camera, then the scene; true while anything moves.</summary>
    internal bool Step(double dt)
    {
        var camera = _camera.Advance(dt);
        var scene = OnAdvance(dt);
        InvalidateVisual();
        return camera || scene;
    }

    protected override bool OnAdvance(double dt)
    {
        var full = Animates;
        var rotate = full && IsRotating && _field.Count > 0;
        var spinTarget = IsPointerOver ? 0 : 1;
        var fieldMoving = _field.Step(dt, full, rotate, spinTarget, RotationSpeed);
        var growing = false;
        if (_hovered >= 0 && _growth < HoverGrowth)
        {
            _growth = full ? ShowpieceInertia.Approach(_growth, HoverGrowth, 1 / HoverRate, dt, 2e-3) : HoverGrowth;
            growing = _growth < HoverGrowth;
            InvalidateVisual();
        }
        if (fieldMoving)
        {
            _sceneVersion++;
            _host?.OnStageFrame();
            if (_pointer is { } p && !IsDragging)
            {
                // Dots move under a resting pointer (a morph): keep the hover on what is under it.
                var hit = HitIndex(p);
                if (hit != _hovered)
                {
                    SetHovered(hit);
                    _host?.OnStageHover(p, hit >= 0 ? _field.Model.Accounts[hit] : null);
                }
            }
        }
        return fieldMoving || growing;
    }

    protected override void OnLoopStopped()
    {
        _sceneVersion++;
    }

    protected override void OnMotionLevelChanged(ArcMotionLevel level)
    {
        if (level != ArcMotionLevel.Full)
        {
            if (_field.IsMorphing)
            {
                _field.MorphTo(_field.Layout, animate: false);
            }
            _field.Step(1e-3, animate: false, rotate: false, 0, 0);
            _growth = HoverGrowth;
            SceneMoved();
        }
        RequestFrames();
    }

    protected override void OnThemeChanged()
    {
        _colorsDirty = true;
        _ringPen = null;
    }

    /// <summary>The host's health states changed (colors, opacities, order).</summary>
    internal void InvalidateColors()
    {
        _colorsDirty = true;
        InvalidateVisual();
    }

    /// <summary>The resolved color of every health state (the host's legend and bars use the same).</summary>
    internal Func<GalaxyHealthState, Color>? ColorResolver { get; set; }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        var wasAtFit = _camera.IsAtFit || !_camera.IsMoving && Math.Abs(_camera.TargetZoom - 1) < 1e-3;
        base.OnSizeChanged(e);
        UpdateFraming();
        if (_focusGroup >= 0)
        {
            FlyToGroup(_focusGroup, animate: false);
        }
        else if (wasAtFit)
        {
            _camera.Fit(animate: false);
        }
        SceneMoved();
    }

    // Input ----------------------------------------------------------------------------------------------------------

    protected override object? HitTestItem(Point position)
    {
        var index = HitIndex(position);
        return index >= 0 ? _field.Model.Accounts[index] : null;
    }

    protected override void OnHoverChanged(Point? position, object? item)
    {
        var wasInside = _pointer is not null;
        _pointer = position;
        var index = item is GalaxyAccount account ? _field.Model.IndexOf(account) : -1;
        SetHovered(index);
        _host?.OnStageHover(position, index >= 0 ? _field.Model.Accounts[index] : null);
        if (wasInside && position is null)
        {
            // The pointer left: the rotation recovers.
            RequestFrames();
        }
        else if (!wasInside && position is not null)
        {
            RequestFrames();
        }
    }

    private void SetHovered(int index)
    {
        if (_hovered == index)
        {
            return;
        }
        _hovered = index;
        _growth = Animates ? HoverStartGrowth : HoverGrowth;
        InvalidateVisual();
        if (index >= 0)
        {
            RequestFrames();
        }
    }

    protected override void OnTap(Point position, object? item, KeyModifiers modifiers) =>
        _host?.OnStageTap(item as GalaxyAccount);

    protected override void OnDoubleTap(Point position, object? item) => _host?.OnStageDoubleTap(ClusterAt(position));

    protected override void OnResetView()
    {
        if (_host is not null)
        {
            _host.OnStageReset();
            return;
        }
        base.OnResetView();
    }

    /// <summary>Fits the whole layout (the host's reset).</summary>
    internal void FitView(bool animate = true)
    {
        ClearFocusFraming();
        _camera.Fit(animate && Animates);
        RequestFrames();
    }

    protected override bool OnStageKeyDown(KeyEventArgs e)
    {
        if (_host is not null && _host.OnStageKey(e))
        {
            return true;
        }
        return base.OnStageKeyDown(e);
    }

    protected override string? GetAccessibleSummary() => _host?.StageSummary;

    // Hit testing ----------------------------------------------------------------------------------------------------

    /// <summary>The dot nearest to a stage point by edge distance, within its radius + <see cref="HitTolerance"/> (−1 none).</summary>
    internal int HitIndex(Point p)
    {
        var n = _field.Count;
        if (n == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return -1;
        }
        EnsureGrid();
        var reach = HitTolerance + _gridMaxRadius;
        var c0 = Math.Max(0, (int)Math.Floor((p.X - reach) / GridCell) + 1);
        var c1 = Math.Min(_gridColumns - 1, (int)Math.Floor((p.X + reach) / GridCell) + 1);
        var r0 = Math.Max(0, (int)Math.Floor((p.Y - reach) / GridCell) + 1);
        var r1 = Math.Min(_gridRows - 1, (int)Math.Floor((p.Y + reach) / GridCell) + 1);
        var k = _camera.Scale;
        var m = _camera.WorldToScreenMatrix;
        var best = -1;
        var bestEdge = double.PositiveInfinity;
        for (var row = r0; row <= r1; row++)
        {
            for (var col = c0; col <= c1; col++)
            {
                var cell = row * _gridColumns + col;
                for (var at = _cellStart[cell]; at < _cellStart[cell + 1]; at++)
                {
                    var i = _cellItems[at];
                    var sx = _field.X[i] * k + m.M31;
                    var sy = _field.Y[i] * k + m.M32;
                    var r = _field.R[i] * k;
                    var dx = p.X - sx;
                    var dy = p.Y - sy;
                    var edge = Math.Sqrt(dx * dx + dy * dy) - r;
                    if (edge <= HitTolerance && (edge < bestEdge || edge == bestEdge && r > _field.R[best] * k))
                    {
                        best = i;
                        bestEdge = edge;
                    }
                }
            }
        }
        return best;
    }

    private void EnsureGrid()
    {
        if (_gridVersion == _sceneVersion && _gridCamera == _camera.Version && _cellStart.Length > 0)
        {
            return;
        }
        _gridVersion = _sceneVersion;
        _gridCamera = _camera.Version;
        var n = _field.Count;
        // One cell of margin on every side: dots just outside the stage still catch the pointer at the edge.
        _gridColumns = (int)Math.Ceiling(Bounds.Width / GridCell) + 2;
        _gridRows = (int)Math.Ceiling(Bounds.Height / GridCell) + 2;
        var cells = _gridColumns * _gridRows;
        if (_cellStart.Length < cells + 1)
        {
            _cellStart = new int[cells + 1];
        }
        else
        {
            Array.Clear(_cellStart, 0, cells + 1);
        }
        if (_cellItems.Length < n)
        {
            _cellItems = new int[n];
            _cellOf = new int[n];
        }
        var k = _camera.Scale;
        var m = _camera.WorldToScreenMatrix;
        _gridMaxRadius = 0;
        for (var i = 0; i < n; i++)
        {
            var sx = _field.X[i] * k + m.M31;
            var sy = _field.Y[i] * k + m.M32;
            var col = (int)Math.Floor(sx / GridCell) + 1;
            var row = (int)Math.Floor(sy / GridCell) + 1;
            if (col < 0 || row < 0 || col >= _gridColumns || row >= _gridRows)
            {
                _cellOf[i] = -1;
                continue;
            }
            var cell = row * _gridColumns + col;
            _cellOf[i] = cell;
            _cellStart[cell + 1]++;
            _gridMaxRadius = Math.Max(_gridMaxRadius, _field.R[i] * k);
        }
        for (var c = 0; c < cells; c++)
        {
            _cellStart[c + 1] += _cellStart[c];
        }
        // Fill (reusing the counts as cursors, then restoring the starts).
        for (var i = 0; i < n; i++)
        {
            var cell = _cellOf[i];
            if (cell < 0)
            {
                continue;
            }
            _cellItems[_cellStart[cell]++] = i;
        }
        for (var c = cells; c > 0; c--)
        {
            _cellStart[c] = _cellStart[c - 1];
        }
        _cellStart[0] = 0;
    }

    // Rendering ------------------------------------------------------------------------------------------------------

    private void EnsureColors()
    {
        var states = _field.Model.States;
        if (!_colorsDirty && _stateColors.Length == states.Length)
        {
            return;
        }
        _colorsDirty = false;
        var levels = GalaxyLayout.DimLevels;
        _stateColors = new Color[states.Length];
        _brushes = new ImmutableSolidColorBrush[states.Length * levels];
        _hoverBrushes = new ImmutableSolidColorBrush[states.Length];
        for (var s = 0; s < states.Length; s++)
        {
            var color = ColorResolver?.Invoke(states[s]) ?? Colors.Gray;
            _stateColors[s] = color;
            for (var level = 0; level < levels; level++)
            {
                var alpha = Math.Clamp(states[s].DotOpacity, 0, 1) * (level + 1) / levels;
                _brushes[s * levels + level] = new ImmutableSolidColorBrush(color, alpha);
            }
            _hoverBrushes[s] = new ImmutableSolidColorBrush(color, HoverAlpha);
        }
    }

    private ImmutablePen RingPen()
    {
        if (_ringPen is null)
        {
            var color = ArcTokens.GetColor(this, TKCode.Arc.DesignTokens.ArcDesignTokenKind.Foreground);
            _ringPen = new ImmutablePen(new ImmutableSolidColorBrush(color), RingThickness);
        }
        return _ringPen;
    }

    protected override void RenderStage(DrawingContext context)
    {
        var model = _field.Model;
        var n = model.Count;
        if (n == 0)
        {
            return;
        }
        EnsureColors();
        var levels = GalaxyLayout.DimLevels;
        var dimLevels = _field.DimLevels;
        var dimSet = _field.DimSet;
        var groupOf = dimSet?.GroupOf;
        // Which levels are in use (bit per level); the full level is drawn last so the focused cluster sits on top.
        var used = 0;
        for (var g = 0; g < dimLevels.Length; g++)
        {
            used |= 1 << dimLevels[g];
        }
        if (used == 0)
        {
            used = 1 << (levels - 1);
        }
        var k = _camera.Scale;
        var m = _camera.WorldToScreenMatrix;
        var ox = m.M31;
        var oy = m.M32;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var xs = _field.X;
        var ys = _field.Y;
        var rs = _field.R;
        var layerOrder = model.LayerOrder;
        var single = (used & (used - 1)) == 0;
        for (var level = 0; level < levels; level++)
        {
            if ((used & (1 << level)) == 0)
            {
                continue;
            }
            foreach (var s in layerOrder)
            {
                var brush = _brushes[s * levels + level];
                var order = model.DrawOrder[s];
                for (var o = 0; o < order.Length; o++)
                {
                    var i = order[o];
                    if (!single && groupOf is not null && dimLevels[groupOf[i]] != level)
                    {
                        continue;
                    }
                    var r = rs[i] * k;
                    var x = xs[i] * k + ox;
                    var y = ys[i] * k + oy;
                    if (x + r < 0 || y + r < 0 || x - r > width || y - r > height)
                    {
                        continue;
                    }
                    context.DrawEllipse(brush, null, new Point(x, y), r, r);
                }
            }
        }
        if (_selected >= 0 && _selected < n && _selected != _hovered)
        {
            var (c, r) = DotOnScreen(_selected);
            context.DrawEllipse(null, RingPen(), c, r + RingGap, r + RingGap);
        }
        if (_hovered >= 0 && _hovered < n)
        {
            var (c, r) = DotOnScreen(_hovered);
            var grown = Math.Max(HoverMinRadius, _growth * r);
            context.DrawEllipse(_hoverBrushes[model.Health[_hovered]], null, c, grown, grown);
            context.DrawEllipse(null, RingPen(), c, grown + RingGap, grown + RingGap);
        }
    }
}
