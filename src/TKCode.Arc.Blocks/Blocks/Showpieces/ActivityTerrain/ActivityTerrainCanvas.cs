using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The terrain's plot: 168 bars in perspective (or pressed flat into a heat map), a floor, axis labels and the hover
/// overlays, drawn in one <see cref="ShowpieceStage"/>. It owns the orbit camera, the fit to its size and every
/// per-bar spring (height, color, lift, dimming); the <see cref="ActivityTerrain"/> that hosts it owns the data, the
/// highlighted and focused cells, and the keyboard model.
/// </summary>
/// <remarks>
/// Code-driven motion: five spring families (bar heights with a per-bar wave delay, bar colors, the camera, hover
/// dimming and lift, and the fit chasing the pitch) advance together in the stage's single frame loop with explicit time
/// steps; a ControlTheme transition cannot drive self-drawn per-bar state or delays per bar.
/// </remarks>
internal sealed partial class ActivityTerrainCanvas : ShowpieceStage
{
    private const int Cells = ActivityTerrainMath.Cells;

    private readonly ShowpieceOrbitCamera _camera;
    private readonly object[] _cellItems = new object[Cells];

    private readonly ShowpieceSpringChannel[] _height = new ShowpieceSpringChannel[Cells];
    private readonly ShowpieceSpringChannel[] _colorL = new ShowpieceSpringChannel[Cells];
    private readonly ShowpieceSpringChannel[] _colorA = new ShowpieceSpringChannel[Cells];
    private readonly ShowpieceSpringChannel[] _colorB = new ShowpieceSpringChannel[Cells];
    private readonly ShowpieceSpringChannel[] _lift = new ShowpieceSpringChannel[Cells];
    private readonly ShowpieceSpringChannel[] _dim = new ShowpieceSpringChannel[Cells];
    private readonly double[] _delay = new double[Cells];
    private readonly double[] _pendingHeight = new double[Cells];
    private readonly ActivityTerrainOklab[] _pendingColor = new ActivityTerrainOklab[Cells];
    private SpringSpec _pendingSpring = ActivityTerrainMath.BarSpring;
    private int _delayed;

    private ShowpieceSpringChannel _fitScale = new(1, 1e-4, 1e-3);
    private ShowpieceSpringChannel _fitX = new(0, 0.01, 0.05);
    private ShowpieceSpringChannel _fitY = new(0, 0.01, 0.05);
    private bool _fitKnown;
    private Size _fitSize;

    private readonly double[] _values = new double[Cells];
    private double _max;
    private ActivityTerrainTone _tone;
    private bool _hasData;
    private bool _introPending = true;
    private ActivityTerrainViewMode _viewMode;
    private bool _narrow;
    private ActivityTerrainCell? _highlighted;
    private ActivityTerrainCell? _focused;
    private Point? _anchor;

    public ActivityTerrainCanvas()
    {
        for (var i = 0; i < Cells; i++)
        {
            _cellItems[i] = ActivityTerrainCell.FromIndex(i);
            _height[i] = new ShowpieceSpringChannel(0, 1e-4, 1e-3);
            _colorL[i] = new ShowpieceSpringChannel(0, 1e-4, 1e-3);
            _colorA[i] = new ShowpieceSpringChannel(0, 1e-4, 1e-3);
            _colorB[i] = new ShowpieceSpringChannel(0, 1e-4, 1e-3);
            _lift[i] = new ShowpieceSpringChannel(0, 1e-4, 1e-3);
            _dim[i] = new ShowpieceSpringChannel(0, 1e-3, 1e-2);
            _order[i] = i;
        }
        _camera = new ShowpieceOrbitCamera
        {
            PitchMin = ActivityTerrainMath.Radians(ActivityTerrainMath.PitchMinDegrees),
            PitchMax = ActivityTerrainMath.Radians(ActivityTerrainMath.PitchMaxDegrees),
            ZoomMin = ActivityTerrainMath.ZoomMin,
            ZoomMax = ActivityTerrainMath.ZoomMax,
            HasPitchInertia = true,
            RestPose = ActivityTerrainMath.RestPose(ActivityTerrainViewMode.Perspective, false)
        };
        _camera.JumpTo(_camera.RestPose);
        _camera.Changed += OnCameraMoved;
        Camera = _camera;
        WheelMode = ShowpieceWheelMode.CtrlZoom;
        PinchSensitivity = ActivityTerrainMath.PinchSensitivity;
        KeyZoomFactor = ActivityTerrainMath.KeyZoomFactor;
        RoleDescription = "interactive chart";
    }

    /// <summary>The block that hosts this plot (data, selection state and announcements live there).</summary>
    internal ActivityTerrain? Owner { get; set; }

    internal ShowpieceOrbitCamera OrbitCamera => _camera;

    /// <summary>Rendered yaw in degrees.</summary>
    internal double YawDegrees => _camera.Yaw * 180 / Math.PI;

    /// <summary>Rendered pitch in degrees.</summary>
    internal double PitchDegrees => _camera.Pitch * 180 / Math.PI;

    /// <summary>The camera is (heading to) the rest pose of the current view mode.</summary>
    internal bool IsCameraAtRest => _camera.IsAtRest;

    internal ActivityTerrainViewMode ViewMode => _viewMode;

    internal bool IsNarrow => _narrow;

    /// <summary>Current animated height of a bar (tests).</summary>
    internal double HeightAt(int index) => _height[index].Value;

    /// <summary>Current lift of a bar (tests).</summary>
    internal double LiftAt(int index) => _lift[index].Value;

    /// <summary>Current dimming of a bar, 0–1 (tests).</summary>
    internal double DimAt(int index) => _dim[index].Value;

    /// <summary>Bars still waiting for their wave delay (tests).</summary>
    internal int DelayedCount => _delayed;

    /// <summary>The fit in use: px per unit at the target and the target's offset from the center (tests).</summary>
    internal ActivityTerrainFit CurrentFit => new(_fitScale.Value, new Vector(_fitX.Value, _fitY.Value));

    /// <summary>The intro still waits for data or for the plot to be shown (tests).</summary>
    internal bool IsIntroPending => _introPending;

    // Data ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Shows a metric. The first data plays the intro (full motion); later changes rise in the diagonal wave (spec 6.5)
    /// when <paramref name="animate"/> is set, else land at once.
    /// </summary>
    internal void SetData(ActivityTerrainSummary summary, ActivityTerrainTone tone, bool animate)
    {
        Array.Copy(summary.Values, _values, Cells);
        _max = summary.Max;
        _tone = tone;
        _hasData = true;
        if (_introPending)
        {
            if (IsAttached)
            {
                StartIntroOrLand();
            }
            else
            {
                LandBars();
            }
            return;
        }
        if (animate && Animates)
        {
            StartWave(ActivityTerrainMath.BarSpring, intro: false);
        }
        else
        {
            LandBars();
        }
    }

    /// <summary>Plays the first reveal again (camera from far above, bars growing back to front).</summary>
    internal void ReplayIntro()
    {
        _introPending = true;
        if (_hasData && IsAttached)
        {
            StartIntroOrLand();
        }
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;

    private bool Animates => MotionLevel == ArcMotionLevel.Full;

    private void StartIntroOrLand()
    {
        _introPending = false;
        if (!Animates)
        {
            _camera.JumpTo(_camera.RestPose);
            LandBars();
            return;
        }
        var rest = _camera.RestPose;
        _camera.JumpTo(new ShowpieceOrbitPose
        {
            Yaw = (rest.Yaw ?? 0) + ActivityTerrainMath.Radians(ActivityTerrainMath.IntroYawOffsetDegrees),
            Pitch = ActivityTerrainMath.Radians(ActivityTerrainMath.IntroPitchDegrees),
            Zoom = ActivityTerrainMath.IntroZoom,
            Distance = rest.Distance,
            Target = rest.Target
        });
        _camera.AnimateTo(rest, ActivityTerrainMath.IntroCameraSpring);
        EnsureColors();
        for (var i = 0; i < Cells; i++)
        {
            // Bars grow from the floor, their color rising from the low end of the scale with them.
            _height[i].Jump(0);
            if (_colorsKnown)
            {
                _colorL[i].Jump(_low.L);
                _colorA[i].Jump(_low.A);
                _colorB[i].Jump(_low.B);
            }
        }
        _colorTargetsDirty = !_colorsKnown;
        _fitKnown = false;
        StartWave(ActivityTerrainMath.IntroBarSpring, intro: true);
    }

    private double TargetHeight(int index) => _viewMode == ActivityTerrainViewMode.Top
        ? ActivityTerrainMath.FlatHeight
        : ActivityTerrainMath.BarHeight(_values[index], _max);

    private ActivityTerrainOklab TargetColor(int index)
    {
        var t = _max > 0 ? Math.Clamp(_values[index] / _max, 0, 1) : 0;
        return _low.Mix(ToneColor(_tone), t);
    }

    private void StartWave(SpringSpec spring, bool intro)
    {
        EnsureColors();
        _pendingSpring = spring;
        _delayed = 0;
        for (var i = 0; i < Cells; i++)
        {
            var cell = ActivityTerrainCell.FromIndex(i);
            var delay = intro ? ActivityTerrainMath.IntroDelay(cell.Day, cell.Hour) : ActivityTerrainMath.MetricDelay(cell.Day, cell.Hour);
            _pendingHeight[i] = TargetHeight(i);
            _pendingColor[i] = _colorsKnown ? TargetColor(i) : default;
            if (delay <= 0)
            {
                StartBar(i);
            }
            else
            {
                _delay[i] = delay;
                _delayed++;
            }
        }
        if (!_colorsKnown)
        {
            _colorTargetsDirty = true;
        }
        RequestFrames();
        InvalidateVisual();
    }

    private void StartBar(int i)
    {
        _delay[i] = 0;
        _height[i].SpringTo(_pendingHeight[i], _pendingSpring);
        if (_colorsKnown && !_colorTargetsDirty)
        {
            var color = _pendingColor[i];
            _colorL[i].SpringTo(color.L, _pendingSpring);
            _colorA[i].SpringTo(color.A, _pendingSpring);
            _colorB[i].SpringTo(color.B, _pendingSpring);
        }
    }

    /// <summary>Every bar at its target height and color at once.</summary>
    private void LandBars()
    {
        _delayed = 0;
        for (var i = 0; i < Cells; i++)
        {
            _delay[i] = 0;
            _height[i].Jump(TargetHeight(i));
        }
        JumpColors();
        InvalidateVisual();
    }

    private void JumpColors()
    {
        if (!_colorsKnown)
        {
            _colorTargetsDirty = true;
            return;
        }
        _colorTargetsDirty = false;
        for (var i = 0; i < Cells; i++)
        {
            var color = TargetColor(i);
            _colorL[i].Jump(color.L);
            _colorA[i].Jump(color.A);
            _colorB[i].Jump(color.B);
        }
    }

    // View ---------------------------------------------------------------------------------------------------------

    /// <summary>Switches between the terrain and the flat heat map (camera and bar heights spring there).</summary>
    internal void SetViewMode(ActivityTerrainViewMode mode, bool animate)
    {
        if (mode == _viewMode)
        {
            return;
        }
        _viewMode = mode;
        var rest = ActivityTerrainMath.RestPose(mode, _narrow);
        _camera.RestPose = rest;
        var pose = new ShowpieceOrbitPose { Yaw = rest.Yaw, Pitch = rest.Pitch, Distance = rest.Distance };
        if (animate && Animates)
        {
            _camera.AnimateTo(pose, ShowpieceSprings.OrbitTopView);
            _pendingSpring = ActivityTerrainMath.BarSpring;
            for (var i = 0; i < Cells; i++)
            {
                if (_delay[i] > 0)
                {
                    _pendingHeight[i] = TargetHeight(i);
                }
                else
                {
                    _height[i].SpringTo(TargetHeight(i), ActivityTerrainMath.BarSpring);
                }
            }
            RequestFrames();
        }
        else
        {
            _camera.JumpTo(pose);
            for (var i = 0; i < Cells; i++)
            {
                _pendingHeight[i] = TargetHeight(i);
                if (_delay[i] <= 0)
                {
                    _height[i].Jump(TargetHeight(i));
                }
            }
            _fitKnown = false;
        }
        UpdateCardAnchor();
        InvalidateVisual();
    }

    /// <summary>Narrow containers orbit from 50° and use the narrow fit; a camera at rest moves to the new rest at once.</summary>
    internal void SetNarrow(bool narrow)
    {
        if (narrow == _narrow)
        {
            return;
        }
        var wasAtRest = _camera.IsAtRest;
        _narrow = narrow;
        _camera.RestPose = ActivityTerrainMath.RestPose(_viewMode, narrow);
        if (wasAtRest && _viewMode == ActivityTerrainViewMode.Perspective && !_camera.IsMoving)
        {
            _camera.JumpTo(new ShowpieceOrbitPose { Yaw = _camera.RestPose.Yaw });
        }
        _fitKnown = false;
        InvalidateVisual();
    }

    /// <summary>The highlighted cell (hover or keyboard): lifts its bar, dims the rest of the week off its row and column.</summary>
    internal void SetHighlighted(ActivityTerrainCell? cell)
    {
        if (cell == _highlighted)
        {
            return;
        }
        _highlighted = cell;
        RetargetHighlight();
    }

    /// <summary>The focused (flown-in) cell: its bar stays lifted.</summary>
    internal void SetFocused(ActivityTerrainCell? cell)
    {
        if (cell == _focused)
        {
            return;
        }
        _focused = cell;
        RetargetHighlight();
    }

    private void RetargetHighlight()
    {
        var animate = Animates && IsAttached;
        for (var i = 0; i < Cells; i++)
        {
            var cell = ActivityTerrainCell.FromIndex(i);
            var lifted = cell == _highlighted || cell == _focused;
            var dimmed = _highlighted is { } h && cell.Day != h.Day && cell.Hour != h.Hour;
            var lift = lifted ? ActivityTerrainMath.Lift : 0;
            var dim = dimmed ? 1 : 0;
            if (animate)
            {
                _lift[i].SpringTo(lift, ActivityTerrainMath.LiftSpring);
                _dim[i].SpringTo(dim, ActivityTerrainMath.DimSpring);
            }
            else
            {
                _lift[i].Jump(lift);
                _dim[i].Jump(dim);
            }
        }
        if (animate)
        {
            RequestFrames();
        }
        UpdateCardAnchor();
        InvalidateVisual();
    }

    // Camera commands -------------------------------------------------------------------------------------------------

    /// <summary>Flies the camera to a bar: the target to the bar's center, zoom to at least 1.5; yaw and pitch stay.</summary>
    internal void FlyTo(ActivityTerrainCell cell)
    {
        var height = TargetHeight(cell.Index);
        var target = new ShowpieceVec3(ActivityTerrainMath.CellX(cell.Hour), height / 2, ActivityTerrainMath.CellZ(cell.Day));
        _camera.AnimateTo(new ShowpieceOrbitPose
        {
            Target = target,
            Zoom = Math.Max(_camera.TargetZoom, ActivityTerrainMath.FlyZoom)
        }, ShowpieceSprings.OrbitFlyIn);
    }

    /// <summary>Back to the whole week: zoom 1 and the default target; yaw and pitch stay.</summary>
    internal void ShowWeek() =>
        _camera.AnimateTo(new ShowpieceOrbitPose { Target = ActivityTerrainMath.RestTarget, Zoom = 1 }, ShowpieceSprings.OrbitExit);

    /// <summary>Yaw, pitch, zoom and target back to the rest pose of the view mode.</summary>
    internal void ResetCamera() => _camera.Reset(ShowpieceSprings.OrbitReset);

    internal void ZoomCamera(double factor) => _camera.ZoomBy(factor);

    internal void StepYaw(double degrees) => _camera.StepYaw(ActivityTerrainMath.Radians(degrees));

    internal void StepPitch(double degrees) => _camera.StepPitch(ActivityTerrainMath.Radians(degrees));

    private void OnCameraMoved()
    {
        if (!IsLoopRunning)
        {
            UpdateCardAnchor();
        }
        Owner?.OnCanvasCameraChanged();
    }

    // Frames ---------------------------------------------------------------------------------------------------------

    protected override bool OnAdvance(double dt)
    {
        var moving = AdvanceBars(dt);
        moving |= AdvanceFit(dt);
        UpdateCardAnchor();
        return moving;
    }

    protected override void OnLoopStopped() => UpdateCardAnchor();

    private bool AdvanceBars(double dt)
    {
        var moving = false;
        for (var i = 0; i < Cells; i++)
        {
            var step = dt;
            if (_delay[i] > 0)
            {
                _delay[i] -= dt;
                if (_delay[i] > 0)
                {
                    moving = true;
                }
                else
                {
                    // The bar starts inside this frame: run the rest of the frame on its new spring.
                    step = -_delay[i];
                    _delayed--;
                    StartBar(i);
                }
            }
            moving |= _height[i].Step(step);
            moving |= _colorL[i].Step(step);
            moving |= _colorA[i].Step(step);
            moving |= _colorB[i].Step(step);
            moving |= _lift[i].Step(dt);
            moving |= _dim[i].Step(dt);
        }
        return moving;
    }

    private ActivityTerrainFit FitTarget(Size size) => ActivityTerrainMath.Fit(size, _camera.Pitch, _narrow, _viewMode);

    private bool AdvanceFit(double dt)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return false;
        }
        var target = FitTarget(size);
        if (!_fitKnown || size != _fitSize || !Animates)
        {
            JumpFit(target, size);
            return false;
        }
        _fitScale.SpringTo(target.Scale, ActivityTerrainMath.FitSpring);
        _fitX.SpringTo(target.Offset.X, ActivityTerrainMath.FitSpring);
        _fitY.SpringTo(target.Offset.Y, ActivityTerrainMath.FitSpring);
        var moving = _fitScale.Step(dt);
        moving |= _fitX.Step(dt);
        moving |= _fitY.Step(dt);
        return moving;
    }

    private void JumpFit(in ActivityTerrainFit fit, Size size)
    {
        _fitScale.Jump(fit.Scale);
        _fitX.Jump(fit.Offset.X);
        _fitY.Jump(fit.Offset.Y);
        _fitKnown = true;
        _fitSize = size;
    }

    /// <summary>The fit to draw with: the springing fit while frames run, else the fit of the current pitch.</summary>
    private void SettleFit(Size size)
    {
        if (!_fitKnown || size != _fitSize || !IsLoopRunning)
        {
            JumpFit(FitTarget(size), size);
        }
    }

    /// <summary>The projection of the rendered camera on a canvas of <paramref name="size"/>.</summary>
    internal ShowpieceOrbitView CurrentView(Size size)
    {
        SettleFit(size);
        var center = new Point(size.Width / 2 + _fitX.Value, size.Height / 2 + _fitY.Value);
        return _camera.GetView(center, _fitScale.Value * _camera.Distance);
    }

    /// <summary>Where the highlighted bar's top center is (the card's anchor), or null.</summary>
    private void UpdateCardAnchor()
    {
        Point? anchor = null;
        var size = Bounds.Size;
        if (_highlighted is { IsValid: true } cell && size.Width > 0 && size.Height > 0)
        {
            var view = CurrentView(size);
            var i = cell.Index;
            var top = new ShowpieceVec3(ActivityTerrainMath.CellX(cell.Hour), _lift[i].Value + Math.Max(0, _height[i].Value),
                ActivityTerrainMath.CellZ(cell.Day));
            if (view.Project(top, out var screen, out _))
            {
                anchor = screen;
            }
        }
        if (anchor == _anchor)
        {
            return;
        }
        _anchor = anchor;
        Owner?.OnCanvasAnchorChanged(anchor);
    }

    /// <summary>The card's anchor (the highlighted bar's top center), or null.</summary>
    internal Point? CardAnchor => _anchor;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCardAnchor();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_introPending && _hasData)
        {
            StartIntroOrLand();
        }
    }

    protected override void OnMotionLevelChanged(ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Full)
        {
            return;
        }
        // Reduced and Off: whatever is in flight lands now.
        if (_delayed > 0 || _height[0].IsMoving)
        {
            LandBars();
        }
        RetargetHighlight();
        _fitKnown = false;
    }

    // Input ----------------------------------------------------------------------------------------------------------

    protected override object? HitTestItem(Point position)
    {
        var index = HitTestIndex(position);
        return index >= 0 ? _cellItems[index] : null;
    }

    protected override void OnHoverChanged(Point? position, object? item) =>
        Owner?.OnCanvasHover(item is ActivityTerrainCell cell ? cell : null);

    protected override void OnTap(Point position, object? item, KeyModifiers modifiers) =>
        Owner?.OnCanvasTap(item is ActivityTerrainCell cell ? cell : null);

    // The heat map is seen straight from above: drags do not orbit it.
    protected override bool OnDragStarting(Point position, object? pressedItem) => _viewMode == ActivityTerrainViewMode.Perspective;

    protected override void OnDragMoved(Vector delta, Point position)
    {
        base.OnDragMoved(delta, position);
        // The fit chases the new pitch on its spring.
        RequestFrames();
    }

    protected override void OnResetView() => Owner?.ResetView();

    protected override bool OnStageKeyDown(KeyEventArgs e) => (Owner?.HandleCanvasKey(e) ?? false) || base.OnStageKeyDown(e);

    protected override string? GetAccessibleSummary() => Owner?.ChartSummary;
}
