using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>A viewpoint. Cameras look down their local -Z axis with +Y up. <see cref="ProjectionMatrix"/> is the
/// canonical projection: right-handed, clip depth 0 (near) to 1 (far); the renderer adapts it to the device.</summary>
public abstract class Camera : Object3D
{
    private Matrix4x4 _projection;
    private bool _projectionDirty = true;
    private float _near;
    private float _far;

    private protected Camera(float near, float far)
    {
        _near = near;
        _far = far;
    }

    /// <summary>Distance of the near plane.</summary>
    public float Near
    {
        get => _near;
        set
        {
            _near = value;
            _projectionDirty = true;
        }
    }

    /// <summary>Distance of the far plane.</summary>
    public float Far
    {
        get => _far;
        set
        {
            _far = value;
            _projectionDirty = true;
        }
    }

    /// <summary>Magnification; 2 shows half as much.</summary>
    public float Zoom
    {
        get;
        set
        {
            field = MathF.Max(value, 1e-6f);
            _projectionDirty = true;
        }
    } = 1f;

    /// <summary>True for orthographic cameras.</summary>
    public abstract bool IsOrthographic { get; }

    /// <summary>View space to clip space (clip depth 0..1).</summary>
    public Matrix4x4 ProjectionMatrix
    {
        get
        {
            if (_projectionDirty)
            {
                _projection = ComputeProjection();
                _projectionDirty = false;
            }

            return _projection;
        }
    }

    /// <summary>World space to view space.</summary>
    public Matrix4x4 ViewMatrix => Matrix4x4.Invert(WorldMatrix, out var view) ? view : Matrix4x4.Identity;

    /// <summary>World space to clip space.</summary>
    public Matrix4x4 ViewProjectionMatrix => ViewMatrix * ProjectionMatrix;

    /// <inheritdoc/>
    protected override bool LooksDownNegativeZ => true;

    /// <summary>Marks the projection for recomputation.</summary>
    protected void InvalidateProjection() => _projectionDirty = true;

    /// <summary>Builds the projection matrix.</summary>
    protected abstract Matrix4x4 ComputeProjection();

    /// <summary>The view frustum in world space.</summary>
    public Frustum GetFrustum() => Frustum.FromViewProjection(ViewProjectionMatrix);

    /// <summary>Projects a world position to normalized device coordinates: x and y in -1..1 (y up), z in 0..1.</summary>
    public Vector3 Project(Vector3 world)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), ViewProjectionMatrix);
        return clip.W != 0f ? new Vector3(clip.X, clip.Y, clip.Z) / clip.W : default;
    }

    /// <summary>Converts normalized device coordinates (z in 0..1) back to a world position.</summary>
    public Vector3 Unproject(Vector3 ndc)
    {
        if (!Matrix4x4.Invert(ViewProjectionMatrix, out var inverse))
        {
            return default;
        }

        var p = Vector4.Transform(new Vector4(ndc, 1f), inverse);
        return p.W != 0f ? new Vector3(p.X, p.Y, p.Z) / p.W : default;
    }

    /// <summary>The world-space ray through a point of the view given in normalized device coordinates (-1..1, y up).</summary>
    public Ray ScreenPointToRay(Vector2 ndc)
    {
        var near = Unproject(new Vector3(ndc, 0f));
        var far = Unproject(new Vector3(ndc, 1f));
        return new Ray(near, far - near);
    }

    /// <summary>The world-space ray through a pixel of a viewport (origin top-left, y down).</summary>
    public Ray ScreenPointToRay(float x, float y, float viewportWidth, float viewportHeight) =>
        ScreenPointToRay(new Vector2(((x / viewportWidth) * 2f) - 1f, 1f - ((y / viewportHeight) * 2f)));
}

/// <summary>A camera with perspective: distant objects are smaller.</summary>
public class PerspectiveCamera : Camera
{
    private float _fov;
    private float _aspect;

    /// <summary>Creates a perspective camera.</summary>
    /// <param name="fov">Vertical field of view in degrees.</param>
    /// <param name="aspect">Width divided by height.</param>
    /// <param name="near">Near plane distance.</param>
    /// <param name="far">Far plane distance.</param>
    public PerspectiveCamera(float fov = 50f, float aspect = 1f, float near = 0.1f, float far = 2000f)
        : base(near, far)
    {
        _fov = fov;
        _aspect = aspect;
    }

    /// <summary>Vertical field of view in degrees.</summary>
    public float Fov
    {
        get => _fov;
        set
        {
            _fov = value;
            InvalidateProjection();
        }
    }

    /// <summary>Width divided by height.</summary>
    public float Aspect
    {
        get => _aspect;
        set
        {
            _aspect = value;
            InvalidateProjection();
        }
    }

    /// <inheritdoc/>
    public override bool IsOrthographic => false;

    /// <inheritdoc/>
    protected override Matrix4x4 ComputeProjection()
    {
        var fov = 2f * MathF.Atan(MathF.Tan(MathUtil.ToRadians(Math.Clamp(_fov, 0.01f, 179f)) * 0.5f) / Zoom);
        return Matrix4x4.CreatePerspectiveFieldOfView(fov, MathF.Max(_aspect, 1e-6f), Near, Far);
    }
}

/// <summary>A camera without perspective: size does not depend on distance.</summary>
public class OrthographicCamera : Camera
{
    private float _left, _right, _top, _bottom;

    /// <summary>Creates an orthographic camera from the edges of its view volume.</summary>
    public OrthographicCamera(float left = -1f, float right = 1f, float top = 1f, float bottom = -1f, float near = 0.1f, float far = 2000f)
        : base(near, far)
    {
        _left = left;
        _right = right;
        _top = top;
        _bottom = bottom;
    }

    public float Left
    {
        get => _left;
        set
        {
            _left = value;
            InvalidateProjection();
        }
    }

    public float Right
    {
        get => _right;
        set
        {
            _right = value;
            InvalidateProjection();
        }
    }

    public float Top
    {
        get => _top;
        set
        {
            _top = value;
            InvalidateProjection();
        }
    }

    public float Bottom
    {
        get => _bottom;
        set
        {
            _bottom = value;
            InvalidateProjection();
        }
    }

    /// <inheritdoc/>
    public override bool IsOrthographic => true;

    /// <inheritdoc/>
    protected override Matrix4x4 ComputeProjection()
    {
        var dx = (_right - _left) / (2f * Zoom);
        var dy = (_top - _bottom) / (2f * Zoom);
        var cx = (_right + _left) * 0.5f;
        var cy = (_top + _bottom) * 0.5f;
        return Matrix4x4.CreateOrthographicOffCenter(cx - dx, cx + dx, cy - dy, cy + dy, Near, Far);
    }
}
