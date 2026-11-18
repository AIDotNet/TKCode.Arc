using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>Base of all lights. Lights that have a direction shine from their position toward <c>Target</c>, or along
/// their local -Z axis when <c>Target</c> is null.</summary>
public abstract class Light : Object3D
{
    private protected Light(ColorF color, float intensity)
    {
        Color = color;
        Intensity = intensity;
    }

    /// <summary>The light color.</summary>
    public ColorF Color { get; set; }

    /// <summary>The strength. Directional: illuminance scale; point and spot: candela.</summary>
    public float Intensity { get; set; }

    /// <inheritdoc/>
    protected override bool LooksDownNegativeZ => true;

    private protected override void OnAttached(Scene scene)
    {
        scene.Lights.Add(this);
        scene.NotifyChanged();
    }

    private protected override void OnDetached(Scene scene)
    {
        scene.Lights.Remove(this);
        scene.NotifyChanged();
    }

    private protected override void OnWorldChanged(Scene scene) => scene.NotifyChanged();

    private protected override void OnRenderStateChanged(Scene scene) => scene.NotifyChanged();
}

/// <summary>Light that reaches every surface equally from all directions.</summary>
public sealed class AmbientLight : Light
{
    /// <summary>Creates an ambient light.</summary>
    public AmbientLight(ColorF color, float intensity = 1f)
        : base(color, intensity)
    {
    }

    /// <summary>Creates a white ambient light.</summary>
    public AmbientLight(float intensity = 1f)
        : base(ColorF.White, intensity)
    {
    }
}

/// <summary>Sky light: <see cref="Light.Color"/> from above, <see cref="GroundColor"/> from below. "Above" is the
/// direction from the origin to the light's position.</summary>
public sealed class HemisphereLight : Light
{
    /// <summary>Creates a hemisphere light.</summary>
    public HemisphereLight(ColorF skyColor, ColorF groundColor, float intensity = 1f)
        : base(skyColor, intensity)
    {
        GroundColor = groundColor;
        Position = Vector3.UnitY;
    }

    /// <summary>The color arriving from below.</summary>
    public ColorF GroundColor { get; set; }
}

/// <summary>Shadow settings of a light.</summary>
public sealed class LightShadow
{
    /// <summary>Width and height of the light's region in the shadow atlas, in texels.</summary>
    public int MapSize { get; set; } = 1024;

    /// <summary>Depth offset that removes shadow acne (in light clip depth; negative moves the shadow away).</summary>
    public float Bias { get; set; } = -0.0005f;

    /// <summary>Offset along the surface normal, in world units, that removes acne on slopes.</summary>
    public float NormalBias { get; set; } = 0.02f;

    /// <summary>Blur radius in texels of the soft shadow filter.</summary>
    public float Radius { get; set; } = 1.5f;

    /// <summary>How dark the shadow is (0..1).</summary>
    public float Intensity { get; set; } = 1f;

    /// <summary>Near plane of the shadow camera.</summary>
    public float Near { get; set; } = 0.5f;

    /// <summary>Far plane of the shadow camera.</summary>
    public float Far { get; set; } = 500f;

    /// <summary>Half extent of a directional light's shadow volume, in world units.</summary>
    public float OrthoSize { get; set; } = 5f;

    /// <summary>When true, a directional light fits its shadow volume to the visible shadow casters instead of using
    /// <see cref="OrthoSize"/> around its target.</summary>
    public bool AutoFit { get; set; }
}

/// <summary>Parallel light, like the sun. Shines from its position toward <see cref="Target"/>.</summary>
public sealed class DirectionalLight : Light
{
    /// <summary>Creates a directional light.</summary>
    public DirectionalLight(ColorF color, float intensity = 1f)
        : base(color, intensity)
    {
        Position = Vector3.UnitY;
    }

    /// <summary>Creates a white directional light.</summary>
    public DirectionalLight(float intensity = 1f)
        : this(ColorF.White, intensity)
    {
    }

    /// <summary>The world position the light points at (default: the origin). Null makes the light shine along its
    /// node's local -Z axis instead, so that rotating or animating the node turns the light (the glTF convention).</summary>
    public Vector3? Target { get; set; } = Vector3.Zero;

    /// <summary>Shadow settings; used when <see cref="Object3D.CastShadow"/> is true.</summary>
    public LightShadow Shadow { get; } = new();

    /// <summary>The normalized direction the light travels in, in world space.</summary>
    public Vector3 Direction
    {
        get
        {
            if (Target is not { } target)
            {
                return -WorldDirection;
            }

            var d = target - WorldPosition;
            return d.LengthSquared() > 1e-12f ? Vector3.Normalize(d) : -Vector3.UnitY;
        }
    }
}

/// <summary>Light from a point in all directions.</summary>
public sealed class PointLight : Light
{
    /// <summary>Creates a point light.</summary>
    public PointLight(ColorF color, float intensity = 1f, float distance = 0f, float decay = 2f)
        : base(color, intensity)
    {
        Distance = distance;
        Decay = decay;
    }

    /// <summary>Where the light fades to zero; 0 means it never does.</summary>
    public float Distance { get; set; }

    /// <summary>How fast the light dims with distance; 2 is physically correct.</summary>
    public float Decay { get; set; }

    /// <summary>Shadow settings; used when <see cref="Object3D.CastShadow"/> is true.</summary>
    public LightShadow Shadow { get; } = new() { MapSize = 512, NormalBias = 0.03f };
}

/// <summary>A cone of light from a point toward <see cref="Target"/>.</summary>
public sealed class SpotLight : Light
{
    /// <summary>Creates a spot light.</summary>
    public SpotLight(ColorF color, float intensity = 1f, float distance = 0f, float angle = MathF.PI / 3f, float penumbra = 0f, float decay = 2f)
        : base(color, intensity)
    {
        Distance = distance;
        Angle = angle;
        Penumbra = penumbra;
        Decay = decay;
        Position = Vector3.UnitY;
    }

    /// <summary>The world position the light points at (default: the origin). Null makes the light shine along its
    /// node's local -Z axis instead, so that rotating or animating the node turns the light (the glTF convention).</summary>
    public Vector3? Target { get; set; } = Vector3.Zero;

    /// <summary>Where the light fades to zero; 0 means it never does.</summary>
    public float Distance { get; set; }

    /// <summary>Half angle of the cone in radians (at most π/2).</summary>
    public float Angle { get; set; }

    /// <summary>Share of the cone that fades out toward the edge, 0..1.</summary>
    public float Penumbra { get; set; }

    /// <summary>How fast the light dims with distance; 2 is physically correct.</summary>
    public float Decay { get; set; }

    /// <summary>Shadow settings; used when <see cref="Object3D.CastShadow"/> is true.</summary>
    public LightShadow Shadow { get; } = new();

    /// <summary>The normalized direction the light travels in, in world space.</summary>
    public Vector3 Direction
    {
        get
        {
            if (Target is not { } target)
            {
                return -WorldDirection;
            }

            var d = target - WorldPosition;
            return d.LengthSquared() > 1e-12f ? Vector3.Normalize(d) : -Vector3.UnitY;
        }
    }
}
