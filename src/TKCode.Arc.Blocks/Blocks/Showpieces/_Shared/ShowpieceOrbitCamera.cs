using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A partial orbit camera pose: only the fields that are set are changed by <see cref="ShowpieceOrbitCamera.AnimateTo"/>
/// and <see cref="ShowpieceOrbitCamera.JumpTo"/>. Angles are in radians.
/// </summary>
internal readonly record struct ShowpieceOrbitPose
{
    public double? Yaw { get; init; }
    public double? Pitch { get; init; }
    public double? Zoom { get; init; }
    public double? Distance { get; init; }
    public ShowpieceVec3? Target { get; init; }
}

/// <summary>
/// A camera path evaluated along a spring-driven progress (<see cref="ShowpieceOrbitCamera.FlyAlong"/>): the globe's
/// great-circle flight lives in the RevenueGlobe block and implements this.
/// </summary>
internal interface IShowpieceOrbitPath
{
    /// <summary>The pose at <paramref name="progress"/> (0 → 1; a spring may overshoot slightly).</summary>
    ShowpieceOrbitPose Evaluate(double progress);
}

/// <summary>
/// An orbit camera (Activity terrain, Revenue globe): yaw, pitch, zoom, distance and a target point, each a spring
/// channel, advanced by explicit time steps. Yaw is unbounded; pitch is clamped to [<see cref="PitchMin"/>,
/// <see cref="PitchMax"/>] with a rubber band while dragging and returns inside (or to <see cref="PitchRest"/>) on
/// release; zoom is a multiplier clamped to [<see cref="ZoomMin"/>, <see cref="ZoomMax"/>]. Drags orbit at
/// <see cref="YawPerPixel"/> / <see cref="PitchPerPixel"/> rad per px and may glide on release; keyboard steps and
/// impulses, poses on a spring, spring-driven paths, and a back-and-forth yaw drift (the globe's idle motion) are
/// built in. At <see cref="ArcMotionLevel.Reduced"/> and <see cref="ArcMotionLevel.Off"/> changes land at once, releases do
/// not glide and there is no drift.
/// <para>
/// Projections: <see cref="GetView"/> gives the terrain's pinhole <see cref="ShowpieceOrbitView"/>;
/// <see cref="GetSphereView"/> reads yaw as the center longitude and pitch as the center latitude of a
/// <see cref="ShowpieceSphereView"/>.
/// </para>
/// </summary>
/// <remarks>
/// Code-driven motion: yaw, pitch and zoom are integrated together each frame with clamps, rubber band, glides that hand
/// over to a drift, and springs retargeted mid-flight; no ControlTheme transition can express that, and explicit steps
/// keep it unit-testable (see <see cref="ShowpieceSpringChannel"/>).
/// </remarks>
internal sealed class ShowpieceOrbitCamera : IShowpieceCamera
{
    private const double AngleRestDelta = 1e-5;
    private const double AngleRestSpeed = 1e-4;

    private ShowpieceSpringChannel _yaw = new(0, AngleRestDelta, AngleRestSpeed);
    private ShowpieceSpringChannel _pitch = new(0, AngleRestDelta, AngleRestSpeed);
    private ShowpieceSpringChannel _zoom = new(1, 1e-5, 1e-4);
    private ShowpieceSpringChannel _distance = new(1, 1e-4, 1e-3);
    private ShowpieceSpringChannel _tx = new(0, 1e-5, 1e-4);
    private ShowpieceSpringChannel _ty = new(0, 1e-5, 1e-4);
    private ShowpieceSpringChannel _tz = new(0, 1e-5, 1e-4);
    private ShowpieceInertia _yawGlide;
    private ShowpieceInertia _pitchGlide;

    private Size _viewport;
    private bool _dragging;
    private double _rawPitch;
    private bool _drifting;
    private double _driftVelocity;
    private int _driftDirection = 1;
    private IShowpieceOrbitPath? _path;
    private ShowpieceSpringChannel _progress = new(0, 1e-4, 1e-3);
    private double? _pitchRest;

    public ShowpieceOrbitCamera()
    {
        var tau = ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.OrbitFrameFactor);
        _yawGlide = new ShowpieceInertia(tau, 0.002);
        _pitchGlide = _yawGlide;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public int Version { get; private set; }

    // Options --------------------------------------------------------------------------------------------------------

    /// <summary>Lowest pitch in radians (Activity terrain 10°, Revenue globe −65°; default −90°).</summary>
    public double PitchMin { get; set; } = -Math.PI / 2;

    /// <summary>Highest pitch in radians (Activity terrain 84°, Revenue globe 65°; default 90°).</summary>
    public double PitchMax { get; set; } = Math.PI / 2;

    /// <summary>Rubber band dimension past the pitch limits, radians (terrain 23°, globe 25°).</summary>
    public double PitchRubberBandDimension { get; set; } = ShowpieceProjection.Radians(23);

    /// <summary>Rubber band factor past the pitch limits (terrain 0.55, globe 0.77).</summary>
    public double PitchRubberBandFactor { get; set; } = RubberBand.DefaultFactor;

    /// <summary>Spring back inside the pitch limits or to <see cref="PitchRest"/> (default <see cref="ShowpieceSprings.PitchReturn"/>).</summary>
    public SpringSpec PitchReturnSpring { get; set; } = ShowpieceSprings.PitchReturn;

    /// <summary>
    /// A pitch the camera always returns to when nothing holds it (Revenue globe 22°, clear it while a selection holds the
    /// view); null returns only inside the limits. Setting a value springs there.
    /// </summary>
    public double? PitchRest
    {
        get => _pitchRest;
        set
        {
            _pitchRest = value;
            if (value is { } rest && !_dragging && _path is null)
            {
                SpringPitch(rest, PitchReturnSpring);
                Notify();
            }
        }
    }

    /// <summary>Smallest zoom multiplier (Activity terrain 0.7).</summary>
    public double ZoomMin { get; set; } = 0.5;

    /// <summary>Largest zoom multiplier (Activity terrain 2.8).</summary>
    public double ZoomMax { get; set; } = 4;

    /// <summary>Spring for <see cref="ZoomBy"/> (default <see cref="ShowpieceSprings.OrbitZoom"/>).</summary>
    public SpringSpec ZoomSpring { get; set; } = ShowpieceSprings.OrbitZoom;

    /// <summary>Spring for <see cref="StepYaw"/> / <see cref="StepPitch"/> (default <see cref="ShowpieceSprings.OrbitStep"/>).</summary>
    public SpringSpec StepSpring { get; set; } = ShowpieceSprings.OrbitStep;

    /// <summary>Spring for <see cref="Reset"/> (default <see cref="ShowpieceSprings.OrbitReset"/>).</summary>
    public SpringSpec ResetSpring { get; set; } = ShowpieceSprings.OrbitReset;

    /// <summary>Yaw change per px of horizontal drag (terrain +1/180 rad/px; globe −1/R so the surface follows the pointer).</summary>
    public double YawPerPixel { get; set; } = 1.0 / 180;

    /// <summary>Pitch change per px of vertical drag (terrain +1/180 rad/px: dragging down looks more from above).</summary>
    public double PitchPerPixel { get; set; } = 1.0 / 180;

    /// <summary>Horizontal releases glide on in yaw (default true).</summary>
    public bool HasYawInertia { get; set; } = true;

    /// <summary>Vertical releases glide on in pitch, stopping at the limits (default false).</summary>
    public bool HasPitchInertia { get; set; }

    /// <summary>Glide time constant in seconds (default × 0.948 per frame, τ ≈ 0.31s; the globe uses × 0.987).</summary>
    public double InertiaTimeConstant
    {
        get => _yawGlide.TimeConstant;
        set
        {
            _yawGlide.TimeConstant = value;
            _pitchGlide.TimeConstant = value;
        }
    }

    /// <summary>Glide stop speed in rad/s (default 0.002).</summary>
    public double InertiaStopSpeed
    {
        get => _yawGlide.StopSpeed;
        set
        {
            _yawGlide.StopSpeed = value;
            _pitchGlide.StopSpeed = value;
        }
    }

    /// <summary>The pose <see cref="Reset"/> returns to and <see cref="IsAtRest"/> compares with.</summary>
    public ShowpieceOrbitPose RestPose { get; set; } = new()
    {
        Yaw = 0,
        Pitch = 0,
        Zoom = 1,
        Distance = 1,
        Target = ShowpieceVec3.Zero
    };

    /// <summary>Idle back-and-forth yaw drift (Revenue globe); runs at full motion while nothing else moves the yaw.</summary>
    public bool IsDriftEnabled { get; set; }

    /// <summary>Middle of the drift range (yaw, radians).</summary>
    public double DriftCenter { get; set; }

    /// <summary>Half width of the drift range (radians; the globe drifts ±43° around HQ + 40°).</summary>
    public double DriftHalfRange { get; set; } = ShowpieceProjection.Radians(43);

    /// <summary>Cruise speed in rad/s (globe 4°/s).</summary>
    public double DriftSpeed { get; set; } = ShowpieceProjection.Radians(4);

    /// <summary>Time constant of speed changes (turnarounds) in seconds (globe 1.4s).</summary>
    public double DriftTimeConstant { get; set; } = 1.4;

    /// <summary>A glide slower than this (rad/s) hands over to the drift (globe 10°/s).</summary>
    public double DriftHandoffSpeed { get; set; } = ShowpieceProjection.Radians(10);

    /// <inheritdoc />
    public ArcMotionLevel MotionLevel { get; set; } = ArcMotionLevel.Full;

    // State ------------------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public Size Viewport => _viewport;

    public double Yaw => _yaw.Value;
    public double Pitch => _pitch.Value;
    public double Zoom => _zoom.Value;
    public double Distance => _distance.Value;
    public ShowpieceVec3 Target => new(_tx.Value, _ty.Value, _tz.Value);

    public double TargetYaw => _yaw.Target;
    public double TargetPitch => _pitch.Target;
    public double TargetZoom => _zoom.Target;
    public double TargetDistance => _distance.Target;
    public ShowpieceVec3 TargetPoint => new(_tx.Target, _ty.Target, _tz.Target);

    /// <summary>Current yaw speed (rad/s) from a glide, drift or spring.</summary>
    public double YawVelocity => _yawGlide.IsActive ? _yawGlide.Velocity : _drifting ? _driftVelocity : _yaw.Velocity;

    public bool IsDragging => _dragging;
    public bool IsGliding => _yawGlide.IsActive || _pitchGlide.IsActive;
    public bool IsDrifting => _drifting;
    public bool IsFlying => _path is not null;

    /// <inheritdoc />
    public bool IsMoving =>
        _path is not null || _yaw.IsMoving || _pitch.IsMoving || _zoom.IsMoving || _distance.IsMoving || _tx.IsMoving ||
        _ty.IsMoving || _tz.IsMoving || IsGliding || DriftWanted;

    private bool DriftWanted => IsDriftEnabled && Animates && !_dragging && _path is null;

    private bool Animates => MotionLevel == ArcMotionLevel.Full;

    /// <summary>The camera is (heading to) <see cref="RestPose"/> (Activity terrain shows "Reset view" otherwise).</summary>
    public bool IsAtRest
    {
        get
        {
            var rest = RestPose;
            const double eps = 1e-3;
            if (rest.Yaw is { } yaw && Math.Abs(ShowpieceProjection.WrapAngle(_yaw.Target - yaw)) > eps)
            {
                return false;
            }
            if (rest.Pitch is { } pitch && Math.Abs(_pitch.Target - pitch) > eps)
            {
                return false;
            }
            if (rest.Zoom is { } zoom && Math.Abs(_zoom.Target - zoom) > eps)
            {
                return false;
            }
            if (rest.Distance is { } distance && Math.Abs(_distance.Target - distance) > eps * Math.Max(1, Math.Abs(distance)))
            {
                return false;
            }
            if (rest.Target is { } target && (TargetPoint - target).Length > eps)
            {
                return false;
            }
            return true;
        }
    }

    /// <summary>The pinhole view at the rendered pose (focal length × <see cref="Zoom"/>).</summary>
    public ShowpieceOrbitView GetView(Point center, double baseFocal) =>
        new(Yaw, Pitch, Distance, Target, baseFocal * Zoom, center);

    /// <summary>The sphere view at the rendered pose: yaw = center longitude, pitch = center latitude, radius × zoom.</summary>
    public ShowpieceSphereView GetSphereView(Point center, double baseRadius) => new(Yaw, Pitch, baseRadius * Zoom, center);

    // Commands ---------------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public void SetViewport(Size size)
    {
        if (size == _viewport)
        {
            return;
        }
        _viewport = size;
        Notify();
    }

    /// <summary>Sets the pose at once (unset fields keep their value).</summary>
    public void JumpTo(in ShowpieceOrbitPose pose)
    {
        _path = null;
        StopGlides();
        if (pose.Yaw is { } yaw)
        {
            _yaw.Jump(yaw);
        }
        if (pose.Pitch is { } pitch)
        {
            _pitch.Jump(pitch);
        }
        if (pose.Zoom is { } zoom)
        {
            _zoom.Jump(zoom);
        }
        if (pose.Distance is { } distance)
        {
            _distance.Jump(distance);
        }
        if (pose.Target is { } target)
        {
            _tx.Jump(target.X);
            _ty.Jump(target.Y);
            _tz.Jump(target.Z);
        }
        Notify();
    }

    /// <summary>
    /// Springs the set fields of <paramref name="pose"/> on <paramref name="spring"/> (velocities kept). With
    /// <paramref name="shortestYaw"/> the yaw turns the short way round (a reset from 90.6° goes back to 32°).
    /// </summary>
    public void AnimateTo(in ShowpieceOrbitPose pose, in SpringSpec spring, bool shortestYaw = true)
    {
        _path = null;
        if (pose.Yaw is not null)
        {
            StopYawMotion();
        }
        if (pose.Pitch is not null)
        {
            _pitchGlide.Stop();
        }
        if (pose.Yaw is { } yaw)
        {
            var target = shortestYaw ? _yaw.Value + ShowpieceProjection.WrapAngle(yaw - _yaw.Value) : yaw;
            Spring(ref _yaw, target, spring);
        }
        if (pose.Pitch is { } pitch)
        {
            Spring(ref _pitch, pitch, spring);
        }
        if (pose.Zoom is { } zoom)
        {
            Spring(ref _zoom, zoom, spring);
        }
        if (pose.Distance is { } distance)
        {
            Spring(ref _distance, distance, spring);
        }
        if (pose.Target is { } target3)
        {
            Spring(ref _tx, target3.X, spring);
            Spring(ref _ty, target3.Y, spring);
            Spring(ref _tz, target3.Z, spring);
        }
        Notify();
    }

    /// <summary>Back to <see cref="RestPose"/> on <paramref name="spring"/> (default <see cref="ResetSpring"/>).</summary>
    public void Reset(SpringSpec? spring = null)
    {
        _dragging = false;
        AnimateTo(RestPose, spring ?? ResetSpring);
    }

    /// <summary>Starts a drag at the rendered pose (stops springs on yaw and pitch, glides, drift and flights).</summary>
    public void BeginDrag()
    {
        _path = null;
        StopGlides();
        StopYawMotion();
        _yaw.Stop();
        _pitch.Stop();
        _dragging = true;
        _rawPitch = UnstretchPitch(_pitch.Value);
    }

    /// <summary>Orbits by a pointer delta in px (yaw 1:1 unbounded, pitch rubber-banded past its limits).</summary>
    public void DragBy(Vector deltaPx)
    {
        if (!_dragging)
        {
            BeginDrag();
        }
        _yaw.Jump(_yaw.Value + deltaPx.X * YawPerPixel);
        _rawPitch += deltaPx.Y * PitchPerPixel;
        _pitch.Jump(RubberBand.Clamp(_rawPitch, PitchMin, PitchMax, PitchRubberBandDimension, PitchRubberBandFactor));
        Notify();
    }

    /// <summary>
    /// Ends a drag: yaw glides on with the release velocity (px/s × <see cref="YawPerPixel"/>) when
    /// <see cref="HasYawInertia"/>; pitch returns inside its limits or to <see cref="PitchRest"/>, or glides when
    /// <see cref="HasPitchInertia"/>.
    /// </summary>
    public void EndDrag(Vector velocityPx = default)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        if (Animates && HasYawInertia)
        {
            _yawGlide.Start(velocityPx.X * YawPerPixel);
        }
        var pitch = _pitch.Value;
        var inside = pitch >= PitchMin - AngleRestDelta && pitch <= PitchMax + AngleRestDelta;
        if (!inside || _pitchRest is not null)
        {
            SpringPitch(_pitchRest ?? Math.Clamp(pitch, PitchMin, PitchMax), PitchReturnSpring);
        }
        else if (Animates && HasPitchInertia)
        {
            _pitchGlide.Start(velocityPx.Y * PitchPerPixel);
        }
        Notify();
    }

    /// <summary>Zooms the target by <paramref name="factor"/> about the target point (clamped).</summary>
    public void ZoomBy(double factor)
    {
        if (!(factor > 0) || double.IsInfinity(factor))
        {
            return;
        }
        Spring(ref _zoom, Math.Clamp(_zoom.Target * factor, ZoomMin, ZoomMax), ZoomSpring);
        Notify();
    }

    /// <summary>Turns the yaw target by <paramref name="radians"/> (terrain Shift + ←/→: ±15°).</summary>
    public void StepYaw(double radians, SpringSpec? spring = null)
    {
        _path = null;
        StopYawMotion();
        Spring(ref _yaw, _yaw.Target + radians, spring ?? StepSpring);
        Notify();
    }

    /// <summary>Tilts the pitch target by <paramref name="radians"/> within the limits (terrain Shift + ↑/↓: ±8°).</summary>
    public void StepPitch(double radians, SpringSpec? spring = null)
    {
        _path = null;
        _pitchGlide.Stop();
        Spring(ref _pitch, Math.Clamp(_pitch.Target + radians, PitchMin, PitchMax), spring ?? StepSpring);
        Notify();
    }

    /// <summary>
    /// Adds speed (rad/s): yaw speed joins the glide (globe arrows ≈ 55°/s, repeated presses add up); pitch speed kicks
    /// the pitch spring, which then returns to its rest or limit.
    /// </summary>
    public void Impulse(double yawVelocity, double pitchVelocity = 0)
    {
        if (!Animates)
        {
            // No glides under reduced motion: the impulse becomes a small immediate turn (a quarter second of travel).
            if (yawVelocity != 0)
            {
                _yaw.Jump(_yaw.Value + yawVelocity * 0.25);
            }
            Notify();
            return;
        }
        _path = null;
        if (yawVelocity != 0)
        {
            var current = YawVelocity;
            StopYawMotion();
            _yaw.Stop();
            _yawGlide.Start(current + yawVelocity);
        }
        if (pitchVelocity != 0)
        {
            _pitchGlide.Stop();
            var target = _pitchRest ?? Math.Clamp(_pitch.Target, PitchMin, PitchMax);
            _pitch.SpringTo(target, PitchReturnSpring, _pitch.Velocity + pitchVelocity);
        }
        Notify();
    }

    /// <summary>
    /// Follows <paramref name="path"/> while a progress value springs from 0 to 1 on <paramref name="progressSpring"/>
    /// (the globe's great-circle flights: <see cref="ShowpieceSprings.GlobeFly"/>). Drags and other commands interrupt it.
    /// </summary>
    public void FlyAlong(IShowpieceOrbitPath path, in SpringSpec progressSpring)
    {
        StopGlides();
        StopYawMotion();
        _dragging = false;
        if (!Animates)
        {
            _path = null;
            ApplyPose(path.Evaluate(1));
            Notify();
            return;
        }
        _path = path;
        _progress.Jump(0);
        _progress.SpringTo(1, progressSpring);
        Notify();
    }

    /// <summary>Stops every motion where it is (drift resumes on the next frame when enabled).</summary>
    public void Stop()
    {
        _path = null;
        StopGlides();
        StopYawMotion();
        _yaw.Stop();
        _pitch.Stop();
        _zoom.Stop();
        _distance.Stop();
        _tx.Stop();
        _ty.Stop();
        _tz.Stop();
        Notify();
    }

    private void StopGlides()
    {
        _yawGlide.Stop();
        _pitchGlide.Stop();
    }

    private void StopYawMotion()
    {
        _yawGlide.Stop();
        _drifting = false;
    }

    private double UnstretchPitch(double pitch)
    {
        if (pitch < PitchMin)
        {
            return PitchMin + RubberBand.Invert(pitch - PitchMin, PitchRubberBandDimension, PitchRubberBandFactor);
        }
        if (pitch > PitchMax)
        {
            return PitchMax + RubberBand.Invert(pitch - PitchMax, PitchRubberBandDimension, PitchRubberBandFactor);
        }
        return pitch;
    }

    private void SpringPitch(double target, in SpringSpec spring)
    {
        if (Animates)
        {
            _pitch.SpringTo(target, spring);
        }
        else
        {
            _pitch.Jump(target);
        }
    }

    private void Spring(ref ShowpieceSpringChannel channel, double target, in SpringSpec spring)
    {
        if (Animates)
        {
            channel.SpringTo(target, spring);
        }
        else
        {
            channel.Jump(target);
        }
    }

    private void ApplyPose(in ShowpieceOrbitPose pose)
    {
        if (pose.Yaw is { } yaw)
        {
            _yaw.Jump(yaw);
        }
        if (pose.Pitch is { } pitch)
        {
            _pitch.Jump(pitch);
        }
        if (pose.Zoom is { } zoom)
        {
            _zoom.Jump(zoom);
        }
        if (pose.Distance is { } distance)
        {
            _distance.Jump(distance);
        }
        if (pose.Target is { } target)
        {
            _tx.Jump(target.X);
            _ty.Jump(target.Y);
            _tz.Jump(target.Z);
        }
    }

    // Frames -----------------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public bool Advance(double dt)
    {
        if (!(dt > 0))
        {
            return IsMoving;
        }
        double y0 = _yaw.Value, p0 = _pitch.Value, z0 = _zoom.Value, d0 = _distance.Value, x0 = _tx.Value, ty0 = _ty.Value, tz0 = _tz.Value;
        if (_path is { } path)
        {
            var moving = _progress.Step(dt);
            ApplyPose(path.Evaluate(moving ? _progress.Value : 1));
            if (!moving)
            {
                _path = null;
                if (_pitchRest is { } rest)
                {
                    SpringPitch(rest, PitchReturnSpring);
                }
            }
        }
        if (!DriftWanted)
        {
            _drifting = false;
        }
        if (_yawGlide.IsActive)
        {
            _yaw.Jump(_yaw.Value + _yawGlide.Step(dt));
            if (IsDriftEnabled && Animates && _yawGlide.IsActive && Math.Abs(_yawGlide.Velocity) < DriftHandoffSpeed)
            {
                _driftVelocity = _yawGlide.Velocity;
                _yawGlide.Stop();
                _drifting = true;
            }
        }
        else if (DriftWanted && !_yaw.IsMoving)
        {
            StepDrift(dt);
        }
        if (_pitchGlide.IsActive)
        {
            var pitch = _pitch.Value + _pitchGlide.Step(dt);
            _pitch.Jump(pitch);
            if (pitch < PitchMin || pitch > PitchMax)
            {
                var velocity = _pitchGlide.Velocity;
                _pitchGlide.Stop();
                _pitch.SpringTo(Math.Clamp(pitch, PitchMin, PitchMax), PitchReturnSpring, velocity);
            }
        }
        _yaw.Step(dt);
        _pitch.Step(dt);
        _zoom.Step(dt);
        _distance.Step(dt);
        _tx.Step(dt);
        _ty.Step(dt);
        _tz.Step(dt);
        if (_yaw.Value != y0 || _pitch.Value != p0 || _zoom.Value != z0 || _distance.Value != d0 || _tx.Value != x0 ||
            _ty.Value != ty0 || _tz.Value != tz0)
        {
            Notify();
        }
        return IsMoving;
    }

    private void StepDrift(double dt)
    {
        if (!_drifting)
        {
            _drifting = true;
            _driftVelocity = 0;
        }
        var offset = ShowpieceProjection.WrapAngle(_yaw.Value - DriftCenter);
        if (offset > DriftHalfRange)
        {
            _driftDirection = -1;
        }
        else if (offset < -DriftHalfRange)
        {
            _driftDirection = 1;
        }
        else if (Math.Abs(_driftVelocity) > 1e-6)
        {
            _driftDirection = Math.Sign(_driftVelocity);
        }
        var target = _driftDirection * DriftSpeed;
        var tau = Math.Max(1e-3, DriftTimeConstant);
        var decay = Math.Exp(-dt / tau);
        // Exact integral of v(t) = target + (v0 − target)·e^(−t/τ).
        _yaw.Jump(_yaw.Value + target * dt + (_driftVelocity - target) * tau * (1 - decay));
        _driftVelocity = target + (_driftVelocity - target) * decay;
    }

    private void Notify()
    {
        Version++;
        Changed?.Invoke();
    }
}
