using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>How fog density grows with distance.</summary>
public enum FogMode
{
    /// <summary>Linear between <see cref="Fog.Near"/> and <see cref="Fog.Far"/>.</summary>
    Linear,

    /// <summary>Exponential squared with <see cref="Fog.Density"/>.</summary>
    Exponential,
}

/// <summary>Distance fog blended over lit materials.</summary>
public sealed class Fog
{
    /// <summary>Linear fog.</summary>
    public Fog(ColorF color, float near, float far)
    {
        Color = color;
        Near = near;
        Far = far;
        Mode = FogMode.Linear;
    }

    /// <summary>Exponential squared fog.</summary>
    public Fog(ColorF color, float density)
    {
        Color = color;
        Density = density;
        Mode = FogMode.Exponential;
    }

    public ColorF Color { get; set; }
    public FogMode Mode { get; set; }
    public float Near { get; set; } = 1f;
    public float Far { get; set; } = 1000f;
    public float Density { get; set; } = 0.00025f;
}

/// <summary>The root of a scene graph, plus what surrounds the objects: background, environment lighting and fog.</summary>
public class Scene : Object3D
{
    internal readonly RenderWorld World;

    /// <summary>Creates an empty scene.</summary>
    public Scene() => World = new RenderWorld(this);
    internal readonly List<Light> Lights = [];

    /// <summary>Background color; null leaves the target's clear color to the renderer.</summary>
    public ColorF? Background { get; set; }

    /// <summary>Background image: an equirectangular or cube texture drawn behind everything. Takes precedence over
    /// <see cref="Background"/>.</summary>
    public Texture? BackgroundTexture { get; set; }

    /// <summary>Multiplies the background image.</summary>
    public float BackgroundIntensity { get; set; } = 1f;

    /// <summary>0 shows the background image sharp, 1 fully blurred (needs <see cref="BackgroundTexture"/> to be the
    /// <see cref="Environment"/>).</summary>
    public float BackgroundBlurriness { get; set; }

    /// <summary>Image-based lighting for physically based materials: an equirectangular or cube texture.</summary>
    public Texture? Environment { get; set; }

    /// <summary>Multiplies the environment lighting.</summary>
    public float EnvironmentIntensity { get; set; } = 1f;

    /// <summary>Rotates environment and background about the Y axis, in radians.</summary>
    public float EnvironmentRotation { get; set; }

    /// <summary>Fog, or null.</summary>
    public Fog? Fog { get; set; }

    /// <summary>When set, every object is drawn with this material.</summary>
    public Material? OverrideMaterial { get; set; }

    /// <summary>Raised when something a picture of the scene depends on changed in the scene graph: an object was
    /// added, removed, moved, shown or hidden, or changed its geometry bounds. Raised once, then not again until the
    /// scene has been rendered. Hosts that render on demand use it to schedule a frame. Changes to materials, textures
    /// and light colors are not reported: ask the host for a frame after those.</summary>
    public event Action? Changed;

    internal void NotifyChanged()
    {
        if (!_changeReported)
        {
            _changeReported = true;
            Changed?.Invoke();
        }
    }

    /// <summary>Lets <see cref="Changed"/> fire again; called by the renderer after it read the scene.</summary>
    internal void AcknowledgeChanges() => _changeReported = false;

    private bool _changeReported;

    /// <summary>The box around every visible renderable object, in world space.</summary>
    public BoundingBox ComputeBounds()
    {
        World.Sync();
        var box = BoundingBox.Empty;
        for (var i = 0; i < World.HighWater; i++)
        {
            if ((World.Flags[i] & (ProxyFlags.Alive | ProxyFlags.Visible)) == (ProxyFlags.Alive | ProxyFlags.Visible))
            {
                var obj = World.Objects[i]!;
                box.Expand(obj.LocalBoundingBox.Transform(World.Matrices[i]));
            }
        }

        return box;
    }
}

[Flags]
internal enum ProxyFlags : byte
{
    None = 0,
    Alive = 1,
    Visible = 2,
    FrustumCulled = 4,
    CastShadow = 8,
    ReceiveShadow = 16,
    Mirrored = 32,
    Dirty = 128,
}

/// <summary>What the renderer reads each frame, stored as parallel arrays so that culling walks contiguous memory
/// instead of the object graph. Slots are updated only for objects that changed.</summary>
internal sealed class RenderWorld
{
    public Renderable?[] Objects = new Renderable?[64];
    public Matrix4x4[] Matrices = new Matrix4x4[64];
    public BoundingSphere[] Spheres = new BoundingSphere[64];
    public uint[] LayerMasks = new uint[64];
    public ProxyFlags[] Flags = new ProxyFlags[64];
    private readonly Stack<int> _free = new();
    private readonly List<int> _dirty = [];
    private readonly Scene _scene;

    public RenderWorld(Scene scene) => _scene = scene;

    /// <summary>One past the highest slot in use.</summary>
    public int HighWater { get; private set; }

    /// <summary>Number of live slots.</summary>
    public int Count { get; private set; }

    /// <summary>Changes whenever a slot was added, removed or updated.</summary>
    public int Version { get; private set; }

    public int Add(Renderable obj)
    {
        int index;
        if (_free.Count > 0)
        {
            index = _free.Pop();
        }
        else
        {
            index = HighWater++;
            if (index == Objects.Length)
            {
                var size = Objects.Length * 2;
                Array.Resize(ref Objects, size);
                Array.Resize(ref Matrices, size);
                Array.Resize(ref Spheres, size);
                Array.Resize(ref LayerMasks, size);
                Array.Resize(ref Flags, size);
            }
        }

        Objects[index] = obj;
        Flags[index] = ProxyFlags.Alive | ProxyFlags.Dirty;
        _dirty.Add(index);
        Count++;
        Version++;
        _scene.NotifyChanged();
        return index;
    }

    public void Remove(int index)
    {
        Objects[index] = null;
        // Keep the Dirty bit when set, so the stale entry in the dirty list is not added twice if the slot is reused.
        Flags[index] &= ProxyFlags.Dirty;
        _free.Push(index);
        Count--;
        Version++;
        _scene.NotifyChanged();
    }

    public void MarkDirty(int index)
    {
        if ((Flags[index] & ProxyFlags.Dirty) == 0)
        {
            Flags[index] |= ProxyFlags.Dirty;
            _dirty.Add(index);
            _scene.NotifyChanged();
        }
    }

    /// <summary>Brings the slots of changed objects up to date.</summary>
    public void Sync()
    {
        if (_dirty.Count == 0)
        {
            return;
        }

        for (var n = 0; n < _dirty.Count; n++)
        {
            var i = _dirty[n];
            var obj = Objects[i];
            if (obj is null)
            {
                Flags[i] = ProxyFlags.None;
                continue;
            }

            var world = obj.WorldMatrix;
            Matrices[i] = world;
            Spheres[i] = obj.LocalBoundingSphere.Transform(world);
            LayerMasks[i] = obj.Layers.Mask;
            var flags = ProxyFlags.Alive;
            if (obj.IsEffectivelyVisible())
            {
                flags |= ProxyFlags.Visible;
            }

            if (obj.FrustumCulled)
            {
                flags |= ProxyFlags.FrustumCulled;
            }

            if (obj.CastShadow)
            {
                flags |= ProxyFlags.CastShadow;
            }

            if (obj.ReceiveShadow)
            {
                flags |= ProxyFlags.ReceiveShadow;
            }

            // A negative determinant flips triangle winding.
            var det = (world.M11 * ((world.M22 * world.M33) - (world.M23 * world.M32)))
                - (world.M12 * ((world.M21 * world.M33) - (world.M23 * world.M31)))
                + (world.M13 * ((world.M21 * world.M32) - (world.M22 * world.M31)));
            if (det < 0f)
            {
                flags |= ProxyFlags.Mirrored;
            }

            Flags[i] = flags;
        }

        _dirty.Clear();
        Version++;
    }
}
