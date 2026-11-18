using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>Lets the user orbit a camera around a target, zoom and pan, with the behaviour of the three.js control of
/// the same name. It knows nothing about any UI toolkit: the host forwards pointer, wheel and key input through the
/// input methods and calls <see cref="Update"/> once per frame; <see cref="Update"/> tells whether the camera moved,
/// so the host can render only then. Coordinates are pixels with the origin top-left and y down. The world up axis is
/// +Y, and the camera is assumed to have no transformed parent.</summary>
public class OrbitControls
{
    private const float PolarEpsilon = 1e-4f;

    // Remaining damped rotation below this (radians) is applied at once: it is far less than a pixel.
    private const float AngleSnap = 5e-5f;
    private const float TwoPi = MathF.PI * 2f;

    private readonly Camera _camera;
    private Vector3 _target;
    private float _thetaDelta;
    private float _phiDelta;
    private float _scale = 1f;
    private Vector3 _panOffset;
    private State _state;
    private Vector2 _start;
    private float _pinchStart;
    private int _touchCount;
    private int _touchId0;
    private int _touchId1;
    private Vector2 _touch0;
    private Vector2 _touch1;
    private int _mousePointer = -1;
    private bool _cursorZoom;
    private Vector2 _cursorNdc;
    private Vector3 _dollyDirection;
    private bool _applied;
    private Vector3 _appliedPosition;
    private Quaternion _appliedRotation;
    private float _appliedZoom;
    private Vector3 _appliedTarget;
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private Vector3 _lastTarget;
    private float _lastZoom;
    private Vector3 _savedTarget;
    private Vector3 _savedPosition;
    private float _savedZoom;

    private enum State
    {
        None,
        Rotate,
        Dolly,
        Pan,
        TouchRotate,
        TouchPan,
        TouchDollyPan,
        TouchDollyRotate,
    }

    /// <summary>Creates controls for a camera, orbiting the world origin.</summary>
    public OrbitControls(Camera camera)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _lastPosition = camera.Position;
        _lastRotation = camera.Rotation;
        _lastZoom = camera.Zoom;
        SaveState();
    }

    /// <summary>The controlled camera.</summary>
    public Camera Camera => _camera;

    /// <summary>The point the camera orbits and looks at.</summary>
    public Vector3 Target
    {
        get => _target;
        set => _target = value;
    }

    /// <summary>False ignores all input (programmatic changes and auto-rotation still apply).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Closest the camera may get to the target (perspective cameras).</summary>
    public float MinDistance { get; set; }

    /// <summary>Farthest the camera may get from the target (perspective cameras).</summary>
    public float MaxDistance { get; set; } = float.PositiveInfinity;

    /// <summary>Smallest zoom (orthographic cameras).</summary>
    public float MinZoom { get; set; }

    /// <summary>Largest zoom (orthographic cameras).</summary>
    public float MaxZoom { get; set; } = float.PositiveInfinity;

    /// <summary>Smallest angle from the +Y axis, in radians (0 looks straight down).</summary>
    public float MinPolarAngle { get; set; }

    /// <summary>Largest angle from the +Y axis, in radians (π looks straight up).</summary>
    public float MaxPolarAngle { get; set; } = MathF.PI;

    /// <summary>Lower limit of the horizontal angle, in radians (-2π..2π); infinite means no limit.</summary>
    public float MinAzimuthAngle { get; set; } = float.NegativeInfinity;

    /// <summary>Upper limit of the horizontal angle, in radians (-2π..2π); infinite means no limit.</summary>
    public float MaxAzimuthAngle { get; set; } = float.PositiveInfinity;

    public bool EnableRotate { get; set; } = true;
    public bool EnableZoom { get; set; } = true;
    public bool EnablePan { get; set; } = true;
    public float RotateSpeed { get; set; } = 1f;
    public float ZoomSpeed { get; set; } = 1f;
    public float PanSpeed { get; set; } = 1f;

    /// <summary>Pixels one arrow key press pans.</summary>
    public float KeyPanSpeed { get; set; } = 7f;

    /// <summary>Speed of rotating with arrow keys and a modifier.</summary>
    public float KeyRotateSpeed { get; set; } = 1f;

    /// <summary>True lets movement ease out after the input stops. <see cref="Update"/> must then be called every frame
    /// until it returns false.</summary>
    public bool EnableDamping { get; set; }

    /// <summary>Share of the remaining movement applied per 1/60 s while damping (0..1). The result does not depend
    /// on the frame rate.</summary>
    public float DampingFactor { get; set; } = 0.05f;

    /// <summary>True orbits the target continuously while the user is not dragging.</summary>
    public bool AutoRotate { get; set; }

    /// <summary>Auto-rotation speed: 2 is one turn in 30 seconds; negative turns the other way.</summary>
    public float AutoRotateSpeed { get; set; } = 2f;

    /// <summary>True pans in the camera's plane; false pans in the horizontal plane (maps).</summary>
    public bool ScreenSpacePanning { get; set; } = true;

    /// <summary>True zooms toward the point under the cursor instead of the target.</summary>
    public bool ZoomToCursor { get; set; }

    /// <summary>Width of the view in pixels. The host keeps it current.</summary>
    public float ViewportWidth { get; set; } = 800f;

    /// <summary>Height of the view in pixels. The host keeps it current.</summary>
    public float ViewportHeight { get; set; } = 600f;

    /// <summary>What dragging with the left button does.</summary>
    public ControlAction LeftButton { get; set; } = ControlAction.Rotate;

    /// <summary>What dragging with the middle button does.</summary>
    public ControlAction MiddleButton { get; set; } = ControlAction.Dolly;

    /// <summary>What dragging with the right button does.</summary>
    public ControlAction RightButton { get; set; } = ControlAction.Pan;

    /// <summary>What one finger does: <see cref="TouchAction.Rotate"/> or <see cref="TouchAction.Pan"/>.</summary>
    public TouchAction OneFinger { get; set; } = TouchAction.Rotate;

    /// <summary>What two fingers do: <see cref="TouchAction.DollyPan"/> or <see cref="TouchAction.DollyRotate"/>.</summary>
    public TouchAction TwoFingers { get; set; } = TouchAction.DollyPan;

    /// <summary>True while a drag or touch gesture is in progress.</summary>
    public bool IsInteracting => _state != State.None;

    /// <summary>Raised by <see cref="Update"/> when the camera moved.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when an interaction starts.</summary>
    public event EventHandler? Start;

    /// <summary>Raised when an interaction ends.</summary>
    public event EventHandler? End;

    /// <summary>The distance from the camera to the target.</summary>
    public float Distance => Vector3.Distance(_camera.Position, _target);

    /// <summary>The current angle from the +Y axis, in radians.</summary>
    public float PolarAngle
    {
        get
        {
            var offset = _camera.Position - _target;
            var radius = offset.Length();
            return radius > 0f ? MathF.Acos(Math.Clamp(offset.Y / radius, -1f, 1f)) : 0f;
        }
    }

    /// <summary>The current horizontal angle, in radians (0 when the camera is on the +Z side of the target).</summary>
    public float AzimuthAngle
    {
        get
        {
            var offset = _camera.Position - _target;
            return MathF.Atan2(offset.X, offset.Z);
        }
    }

    // ---- Programmatic movement ----

    /// <summary>Orbits horizontally by an angle in radians (positive moves the camera to its left). Applied by the
    /// next <see cref="Update"/>.</summary>
    public void RotateLeft(float angle) => _thetaDelta -= angle;

    /// <summary>Orbits vertically by an angle in radians (positive moves the camera up). Applied by the next
    /// <see cref="Update"/>.</summary>
    public void RotateUp(float angle) => _phiDelta -= angle;

    /// <summary>Multiplies the distance to the target (orthographic cameras: divides the zoom) by a factor: below 1
    /// moves in, above 1 moves out. Applied by the next <see cref="Update"/>.</summary>
    public void Dolly(float factor)
    {
        if (factor > 0f && float.IsFinite(factor))
        {
            _scale *= factor;
        }
    }

    /// <summary>Pans by a distance in pixels, as dragging by that distance would (positive x drags the scene right,
    /// positive y drags it down). Applied by the next <see cref="Update"/>.</summary>
    public void Pan(float deltaX, float deltaY)
    {
        var world = _camera.WorldMatrix;
        var right = Vector3.Normalize(new Vector3(world.M11, world.M12, world.M13));
        var projection = _camera.ProjectionMatrix;
        float left, up;
        if (_camera.IsOrthographic)
        {
            // 2 / M11 and 2 / M22 are the width and height of the view volume, zoom included.
            left = deltaX * (2f / projection.M11) / MathF.Max(ViewportWidth, 1f);
            up = deltaY * (2f / projection.M22) / MathF.Max(ViewportHeight, 1f);
        }
        else
        {
            // 1 / M22 is tan(fov / 2): half the view height at distance 1.
            var targetDistance = Vector3.Distance(_camera.Position, _target) / projection.M22;
            left = 2f * deltaX * targetDistance / MathF.Max(ViewportHeight, 1f);
            up = 2f * deltaY * targetDistance / MathF.Max(ViewportHeight, 1f);
        }

        _panOffset -= right * left;
        var upAxis = ScreenSpacePanning
            ? Vector3.Normalize(new Vector3(world.M21, world.M22, world.M23))
            : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, right));
        _panOffset += upAxis * up;
    }

    /// <summary>Remembers the current target, camera position and zoom for <see cref="Reset"/>.</summary>
    public void SaveState()
    {
        _savedTarget = _target;
        _savedPosition = _camera.Position;
        _savedZoom = _camera.Zoom;
    }

    /// <summary>Returns to the state remembered by <see cref="SaveState"/> (initially: the state at construction).</summary>
    public void Reset()
    {
        _target = _savedTarget;
        _camera.Position = _savedPosition;
        _camera.Zoom = _savedZoom;
        ClearMotion();
        _state = State.None;
        _touchCount = 0;
        _mousePointer = -1;
        Update(0f);
    }

    private void ClearMotion()
    {
        _thetaDelta = 0f;
        _phiDelta = 0f;
        _panOffset = Vector3.Zero;
        _scale = 1f;
        _cursorZoom = false;
    }

    /// <summary>Moves the camera, keeping its viewing direction, so that a box is entirely in view, and makes the
    /// box's center the target. The near and far planes are widened when the box would be clipped.</summary>
    /// <param name="box">The box to frame, in world space.</param>
    /// <param name="padding">Room around the box: 1 fits it exactly, 1.2 leaves a 20 % margin.</param>
    public void FitToBounds(BoundingBox box, float padding = 1.2f)
    {
        if (box.IsEmpty)
        {
            return;
        }

        var center = box.Center;
        var radius = MathF.Max((box.Max - box.Min).Length() * 0.5f, 1e-6f) * MathF.Max(padding, 1e-3f);
        var direction = _camera.Position - _target;
        direction = direction.LengthSquared() > 1e-12f ? Vector3.Normalize(direction) : Vector3.UnitZ;
        var projection = _camera.ProjectionMatrix;
        float distance;
        if (_camera.IsOrthographic)
        {
            // Zoom so that the sphere around the box fits the smaller half extent of the view volume.
            var halfWidth = _camera.Zoom / projection.M11;
            var halfHeight = _camera.Zoom / projection.M22;
            _camera.Zoom = Math.Clamp(MathF.Min(halfWidth, halfHeight) / radius, MinZoom, MaxZoom);
            distance = MathF.Max(Vector3.Distance(_camera.Position, _target), radius + (_camera.Near * 2f));
        }
        else
        {
            // The sphere touches the frustum planes of the narrower field of view.
            var tanHalf = MathF.Min(1f / projection.M22, 1f / projection.M11);
            distance = radius / MathF.Sin(MathF.Atan(tanHalf));
            distance = Math.Clamp(distance, MinDistance, MaxDistance);
        }

        if (distance - radius < _camera.Near)
        {
            _camera.Near = MathF.Max((distance - radius) * 0.5f, distance * 1e-4f);
        }

        if (distance + radius > _camera.Far)
        {
            _camera.Far = (distance + radius) * 2f;
        }

        _target = center;
        _camera.Position = center + (direction * distance);
        ClearMotion();
        Update(0f);
    }

    // ---- Per-frame update ----

    /// <summary>Applies pending input, damping and auto-rotation to the camera, and enforces the limits.</summary>
    /// <param name="deltaSeconds">Time since the previous call, in seconds.</param>
    /// <returns>True when the camera moved or movement is still easing out: the host should render and call again
    /// next frame. False when there is nothing to do; the camera is then not touched.</returns>
    public bool Update(float deltaSeconds)
    {
        var dt = float.IsFinite(deltaSeconds) && deltaSeconds > 0f ? deltaSeconds : 0f;
        var camera = _camera;
        var offset = camera.Position - _target;
        var radius = offset.Length();
        var theta = MathF.Atan2(offset.X, offset.Z);
        var phi = radius > 0f ? MathF.Acos(Math.Clamp(offset.Y / radius, -1f, 1f)) : MathF.PI / 2f;
        if (AutoRotate && _state == State.None)
        {
            RotateLeft(TwoPi / 60f * AutoRotateSpeed * dt);
        }

        var perspective = !camera.IsOrthographic;
        var pending = _thetaDelta != 0f || _phiDelta != 0f || _panOffset != Vector3.Zero || _scale != 1f;
        var external = !_applied || camera.Position != _appliedPosition || camera.Rotation != _appliedRotation
            || camera.Zoom != _appliedZoom || _target != _appliedTarget;
        if (!pending && !external && !ViolatesLimits(theta, phi, radius, perspective))
        {
            return false;
        }

        // Share of the pending movement applied now. three.js applies DampingFactor per frame; this is the same
        // curve at 60 frames per second, expressed in time. What is left when it gets too small to see is applied
        // in one go, so the movement ends at exactly the pose the input asked for.
        var k = EnableDamping ? 1f - MathF.Pow(1f - Math.Clamp(DampingFactor, 0f, 1f), dt * 60f) : 1f;
        var thetaStep = MathF.Abs(_thetaDelta) < AngleSnap ? _thetaDelta : _thetaDelta * k;
        var phiStep = MathF.Abs(_phiDelta) < AngleSnap ? _phiDelta : _phiDelta * k;
        var panStep = _panOffset.Length() < MathF.Max(radius, 1e-3f) * 1e-5f ? _panOffset : _panOffset * k;
        _thetaDelta -= thetaStep;
        _phiDelta -= phiStep;
        _panOffset -= panStep;
        theta += thetaStep;
        phi += phiStep;

        theta = ClampAzimuth(theta);
        phi = MathF.Max(MinPolarAngle, MathF.Min(MaxPolarAngle, phi));
        phi = Math.Clamp(phi, PolarEpsilon, MathF.PI - PolarEpsilon);
        _target += panStep;

        var cursorZoom = ZoomToCursor && _cursorZoom;
        if (perspective)
        {
            radius = ClampDistance(cursorZoom ? radius : radius * _scale);
        }
        else
        {
            radius = ClampDistance(radius);
        }

        var sinPhi = MathF.Sin(phi);
        offset = new Vector3(radius * sinPhi * MathF.Sin(theta), radius * MathF.Cos(phi), radius * sinPhi * MathF.Cos(theta));
        camera.Position = _target + offset;
        camera.LookAt(_target);

        if (perspective)
        {
            if (cursorZoom)
            {
                // Move along the ray through the cursor, so what is under the cursor stays under it.
                var newRadius = ClampDistance(radius * _scale);
                camera.Position += _dollyDirection * (radius - newRadius);
                Retarget(newRadius);
            }
        }
        else
        {
            var zoom = Math.Clamp(camera.Zoom / _scale, MathF.Max(MinZoom, 1e-6f), MaxZoom);
            if (zoom != camera.Zoom)
            {
                if (cursorZoom)
                {
                    var before = camera.Unproject(new Vector3(_cursorNdc, 0f));
                    camera.Zoom = zoom;
                    var after = camera.Unproject(new Vector3(_cursorNdc, 0f));
                    camera.Position += before - after;
                    Retarget(radius);
                }
                else
                {
                    camera.Zoom = zoom;
                }
            }
        }

        _scale = 1f;
        _cursorZoom = false;
        _applied = true;
        _appliedPosition = camera.Position;
        _appliedRotation = camera.Rotation;
        _appliedZoom = camera.Zoom;
        _appliedTarget = _target;

        var changed = _appliedPosition != _lastPosition || _appliedRotation != _lastRotation || _appliedZoom != _lastZoom || _target != _lastTarget;
        if (changed)
        {
            _lastPosition = _appliedPosition;
            _lastRotation = _appliedRotation;
            _lastTarget = _target;
            _lastZoom = _appliedZoom;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return changed || _thetaDelta != 0f || _phiDelta != 0f || _panOffset != Vector3.Zero;
    }

    private float ClampAzimuth(float theta)
    {
        float min = MinAzimuthAngle, max = MaxAzimuthAngle;
        if (!float.IsFinite(min) || !float.IsFinite(max))
        {
            return theta;
        }

        min = WrapAngle(min);
        max = WrapAngle(max);
        if (min <= max)
        {
            return Math.Clamp(theta, min, max);
        }

        return theta > (min + max) / 2f ? MathF.Max(min, theta) : MathF.Min(max, theta);
    }

    /// <summary>True when the current pose is outside the limits by more than rounding noise.</summary>
    private bool ViolatesLimits(float theta, float phi, float radius, bool perspective)
    {
        const float Tolerance = 1e-5f;
        var polar = Math.Clamp(MathF.Max(MinPolarAngle, MathF.Min(MaxPolarAngle, phi)), PolarEpsilon, MathF.PI - PolarEpsilon);
        if (MathF.Abs(polar - phi) > Tolerance || MathF.Abs(ClampAzimuth(theta) - theta) > Tolerance)
        {
            return true;
        }

        if (MathF.Abs(ClampDistance(radius) - radius) > Tolerance * MathF.Max(radius, 1f))
        {
            return true;
        }

        if (!perspective)
        {
            var zoom = _camera.Zoom;
            return MathF.Abs(Math.Clamp(zoom, MathF.Max(MinZoom, 1e-6f), MaxZoom) - zoom) > Tolerance * MathF.Max(zoom, 1f);
        }

        return false;
    }

    /// <summary>After a zoom toward the cursor moved the camera: puts the target back in front of it.</summary>
    private void Retarget(float radius)
    {
        var world = _camera.WorldMatrix;
        var forward = Vector3.Normalize(new Vector3(-world.M31, -world.M32, -world.M33));
        if (ScreenSpacePanning)
        {
            _target = _camera.Position + (forward * radius);
            return;
        }

        // Keep the target on its horizontal plane, unless the view is too flat for that to be stable.
        if (MathF.Abs(forward.Y) < 0.342f)
        {
            _camera.LookAt(_target);
            return;
        }

        var t = (_target.Y - _camera.Position.Y) / forward.Y;
        if (t > 0f)
        {
            _target = _camera.Position + (forward * t);
        }
    }

    private float ClampDistance(float distance) => MathF.Max(MinDistance, MathF.Min(MaxDistance, distance));

    private static float WrapAngle(float angle)
    {
        if (angle < -MathF.PI)
        {
            return angle + TwoPi;
        }

        return angle > MathF.PI ? angle - TwoPi : angle;
    }

    private float ZoomScale(float delta) => MathF.Pow(0.95f, ZoomSpeed * MathF.Abs(delta * 0.01f));

    // ---- Input ----

    /// <summary>A mouse button was pressed or a finger touched the view.</summary>
    public void PointerDown(int pointerId, float x, float y, PointerButton button, ControlModifiers modifiers = ControlModifiers.None)
    {
        if (!Enabled)
        {
            return;
        }

        if (button == PointerButton.Touch)
        {
            if (_touchCount >= 2 || _mousePointer >= 0)
            {
                return;
            }

            if (_touchCount == 0)
            {
                _touchId0 = pointerId;
                _touch0 = new Vector2(x, y);
            }
            else
            {
                _touchId1 = pointerId;
                _touch1 = new Vector2(x, y);
            }

            _touchCount++;
            var wasIdle = _state == State.None;
            BeginTouch();
            if (wasIdle && _state != State.None)
            {
                Start?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (_state != State.None)
        {
            return;
        }

        var action = button switch
        {
            PointerButton.Left => LeftButton,
            PointerButton.Middle => MiddleButton,
            _ => RightButton,
        };
        var modified = (modifiers & (ControlModifiers.Control | ControlModifiers.Meta | ControlModifiers.Shift)) != 0;
        if (modified && action == ControlAction.Rotate)
        {
            action = ControlAction.Pan;
        }
        else if (modified && action == ControlAction.Pan)
        {
            action = ControlAction.Rotate;
        }

        var state = action switch
        {
            ControlAction.Rotate when EnableRotate => State.Rotate,
            ControlAction.Dolly when EnableZoom => State.Dolly,
            ControlAction.Pan when EnablePan => State.Pan,
            _ => State.None,
        };
        if (state == State.None)
        {
            return;
        }

        _state = state;
        _mousePointer = pointerId;
        _start = new Vector2(x, y);
        Start?.Invoke(this, EventArgs.Empty);
    }

    private void BeginTouch()
    {
        if (_touchCount == 1)
        {
            _start = _touch0;
            _state = OneFinger switch
            {
                TouchAction.Rotate when EnableRotate => State.TouchRotate,
                TouchAction.Pan when EnablePan => State.TouchPan,
                _ => State.None,
            };
            return;
        }

        if (!EnableZoom && !EnablePan && !EnableRotate)
        {
            _state = State.None;
            return;
        }

        _start = (_touch0 + _touch1) * 0.5f;
        _pinchStart = Vector2.Distance(_touch0, _touch1);
        _state = TwoFingers switch
        {
            TouchAction.DollyPan => State.TouchDollyPan,
            TouchAction.DollyRotate => State.TouchDollyRotate,
            _ => State.None,
        };
    }

    /// <summary>A pressed pointer moved.</summary>
    public void PointerMove(int pointerId, float x, float y)
    {
        if (!Enabled || _state == State.None)
        {
            return;
        }

        var current = new Vector2(x, y);
        switch (_state)
        {
            case State.Rotate when pointerId == _mousePointer:
                RotateBy(current - _start);
                _start = current;
                break;
            case State.Dolly when pointerId == _mousePointer:
            {
                var dy = current.Y - _start.Y;
                if (dy > 0f)
                {
                    _scale /= ZoomScale(dy);
                }
                else if (dy < 0f)
                {
                    _scale *= ZoomScale(dy);
                }

                _start = current;
                break;
            }

            case State.Pan when pointerId == _mousePointer:
            {
                var delta = (current - _start) * PanSpeed;
                Pan(delta.X, delta.Y);
                _start = current;
                break;
            }

            case State.TouchRotate when pointerId == _touchId0:
                _touch0 = current;
                RotateBy(current - _start);
                _start = current;
                break;
            case State.TouchPan when pointerId == _touchId0:
            {
                _touch0 = current;
                var delta = (current - _start) * PanSpeed;
                Pan(delta.X, delta.Y);
                _start = current;
                break;
            }

            case State.TouchDollyPan:
            case State.TouchDollyRotate:
            {
                if (pointerId == _touchId0)
                {
                    _touch0 = current;
                }
                else if (pointerId == _touchId1)
                {
                    _touch1 = current;
                }
                else
                {
                    break;
                }

                if (EnableZoom)
                {
                    var distance = Vector2.Distance(_touch0, _touch1);
                    if (distance > 0f && _pinchStart > 0f)
                    {
                        // Fingers moving apart bring the camera closer.
                        _scale /= MathF.Pow(distance / _pinchStart, ZoomSpeed);
                    }

                    _pinchStart = distance;
                }

                var center = (_touch0 + _touch1) * 0.5f;
                if (_state == State.TouchDollyPan)
                {
                    if (EnablePan)
                    {
                        var delta = (center - _start) * PanSpeed;
                        Pan(delta.X, delta.Y);
                    }
                }
                else if (EnableRotate)
                {
                    RotateBy(center - _start);
                }

                _start = center;
                break;
            }
        }
    }

    private void RotateBy(Vector2 pixels)
    {
        // A drag across the full height of the view is one full turn.
        var height = MathF.Max(ViewportHeight, 1f);
        RotateLeft(TwoPi * pixels.X * RotateSpeed / height);
        RotateUp(TwoPi * pixels.Y * RotateSpeed / height);
    }

    /// <summary>A mouse button was released or a finger left the view.</summary>
    public void PointerUp(int pointerId)
    {
        if (_mousePointer >= 0)
        {
            if (pointerId == _mousePointer)
            {
                _mousePointer = -1;
                Finish();
            }

            return;
        }

        if (_touchCount == 0)
        {
            return;
        }

        if (pointerId == _touchId0)
        {
            _touchId0 = _touchId1;
            _touch0 = _touch1;
        }
        else if (!(_touchCount == 2 && pointerId == _touchId1))
        {
            return;
        }

        _touchCount--;
        if (_touchCount == 0)
        {
            Finish();
            return;
        }

        // One finger is left: continue with the one-finger gesture from where it is.
        var wasActive = _state != State.None;
        BeginTouch();
        if (wasActive && _state == State.None)
        {
            End?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The system took the pointer away (same effect as releasing it).</summary>
    public void PointerCancel(int pointerId) => PointerUp(pointerId);

    private void Finish()
    {
        if (_state != State.None)
        {
            _state = State.None;
            End?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The wheel turned. Positive <paramref name="deltaY"/> zooms out, as in browsers; one notch is about
    /// 100. (Avalonia reports +1 per notch away from the user: pass <c>-delta.Y * 100</c>.)</summary>
    /// <param name="deltaY">Scroll amount.</param>
    /// <param name="x">Cursor x in pixels, used by <see cref="ZoomToCursor"/>.</param>
    /// <param name="y">Cursor y in pixels.</param>
    public void Wheel(float deltaY, float x, float y)
    {
        if (!Enabled || !EnableZoom || _state != State.None || deltaY == 0f)
        {
            return;
        }

        Start?.Invoke(this, EventArgs.Empty);
        if (ZoomToCursor)
        {
            _cursorZoom = true;
            _cursorNdc = new Vector2(((x / MathF.Max(ViewportWidth, 1f)) * 2f) - 1f, 1f - ((y / MathF.Max(ViewportHeight, 1f)) * 2f));
            var through = _camera.Unproject(new Vector3(_cursorNdc, 1f)) - _camera.WorldPosition;
            _dollyDirection = through.LengthSquared() > 0f ? Vector3.Normalize(through) : Vector3.Zero;
        }

        if (deltaY < 0f)
        {
            _scale *= ZoomScale(deltaY);
        }
        else
        {
            _scale /= ZoomScale(deltaY);
        }

        End?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>An arrow key was pressed: pans, or with Shift, Control or Meta orbits.</summary>
    /// <returns>True when the key was used.</returns>
    public bool KeyDown(ControlKey key, ControlModifiers modifiers = ControlModifiers.None)
    {
        if (!Enabled)
        {
            return false;
        }

        if ((modifiers & (ControlModifiers.Control | ControlModifiers.Meta | ControlModifiers.Shift)) != 0)
        {
            if (!EnableRotate)
            {
                return false;
            }

            var angle = TwoPi * KeyRotateSpeed / MathF.Max(ViewportHeight, 1f);
            switch (key)
            {
                case ControlKey.Up:
                    RotateUp(angle);
                    break;
                case ControlKey.Down:
                    RotateUp(-angle);
                    break;
                case ControlKey.Left:
                    RotateLeft(angle);
                    break;
                default:
                    RotateLeft(-angle);
                    break;
            }

            return true;
        }

        if (!EnablePan)
        {
            return false;
        }

        switch (key)
        {
            case ControlKey.Up:
                Pan(0f, KeyPanSpeed);
                break;
            case ControlKey.Down:
                Pan(0f, -KeyPanSpeed);
                break;
            case ControlKey.Left:
                Pan(KeyPanSpeed, 0f);
                break;
            default:
                Pan(-KeyPanSpeed, 0f);
                break;
        }

        return true;
    }
}

/// <summary><see cref="OrbitControls"/> set up for maps and floor plans: the left button and one finger pan in the
/// horizontal plane, the right button and two fingers rotate.</summary>
public class MapControls : OrbitControls
{
    /// <summary>Creates map controls for a camera.</summary>
    public MapControls(Camera camera)
        : base(camera)
    {
        ScreenSpacePanning = false;
        LeftButton = ControlAction.Pan;
        RightButton = ControlAction.Rotate;
        OneFinger = TouchAction.Pan;
        TwoFingers = TouchAction.DollyRotate;
    }
}
