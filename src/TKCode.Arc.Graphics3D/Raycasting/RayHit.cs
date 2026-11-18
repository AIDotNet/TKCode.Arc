using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>One intersection of a ray with an object, as reported by <see cref="Raycaster"/>.</summary>
public struct RayHit
{
    /// <summary>Distance from the ray origin to <see cref="Point"/>, in world units.</summary>
    public float Distance;

    /// <summary>Where the ray hit, in world space. For points it is the closest point on the ray; for lines the
    /// closest point on the segment.</summary>
    public Vector3 Point;

    /// <summary>The object that was hit.</summary>
    public Object3D Object;

    /// <summary>Index of the triangle that was hit (element index divided by three); -1 for lines, points and sprites.</summary>
    public int FaceIndex;

    /// <summary>The triangle's geometric normal in world space (it follows the winding, not the ray).</summary>
    public Vector3 Normal;

    /// <summary>The first UV set at the hit, when the geometry has one.</summary>
    public Vector2 Uv;

    /// <summary>The second UV set at the hit, when the geometry has one.</summary>
    public Vector2 Uv1;

    /// <summary>Weights of the triangle's three vertices at the hit (they sum to 1).</summary>
    public Vector3 Barycentric;

    /// <summary>The instance of an <see cref="InstancedMesh"/> that was hit; -1 otherwise.</summary>
    public int InstanceId;

    /// <summary>For <see cref="Points"/>: the vertex index. For lines: the index of the segment's first element. -1 otherwise.</summary>
    public int Index;

    /// <summary>The geometry group's material index (0 without groups).</summary>
    public int MaterialIndex;

    /// <summary>For lines and points: how far the ray passed from the hit primitive, in world units; 0 otherwise.</summary>
    public float DistanceToRay;
}

/// <summary>A ray/triangle intersection in a geometry's local space, as reported by <see cref="MeshBvh"/>.</summary>
public struct MeshBvhHit
{
    /// <summary>Index of the triangle (element index divided by three).</summary>
    public int Triangle;

    /// <summary>Ray parameter of the hit: the point is <c>origin + direction * T</c>.</summary>
    public float T;

    /// <summary>Barycentric weight of the triangle's second vertex.</summary>
    public float U;

    /// <summary>Barycentric weight of the triangle's third vertex.</summary>
    public float V;
}
