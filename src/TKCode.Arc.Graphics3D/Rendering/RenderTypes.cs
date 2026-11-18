using System.Numerics;
using System.Runtime.InteropServices;

namespace TKCode.Arc.Graphics3D;

/// <summary>How scene-referred (HDR) colors are compressed into the displayable range.</summary>
public enum ToneMapping
{
    /// <summary>No compression: values above 1 clip.</summary>
    None,

    /// <summary>Exposure only.</summary>
    Linear,
    Reinhard,
    Cineon,

    /// <summary>The ACES filmic curve: contrasty, saturated highlights shift toward white.</summary>
    AcesFilmic,

    /// <summary>AgX: softer, keeps hue in bright colors.</summary>
    AgX,

    /// <summary>Khronos PBR Neutral: keeps base colors true; meant for product display.</summary>
    Neutral,
}

/// <summary>What the last <see cref="Renderer.Render(Scene, Camera, RenderTarget?)"/> call did.</summary>
public struct RenderStatistics
{
    /// <summary>Renderable objects in the scene.</summary>
    public int Objects;

    /// <summary>Objects that passed culling in the main view.</summary>
    public int Visible;

    /// <summary>Draw calls issued, shadow passes included.</summary>
    public int DrawCalls;

    /// <summary>Draw calls that were saved by merging equal objects into instanced draws.</summary>
    public int MergedDraws;
    public long Triangles;

    /// <summary>Shadow map regions rendered.</summary>
    public int ShadowViews;

    /// <summary>Programs compiled during the frame.</summary>
    public int ProgramsCompiled;

    /// <summary>Bytes uploaded to the GPU during the frame.</summary>
    public long UploadBytes;

    /// <summary>Time spent on the CPU in the call, in milliseconds.</summary>
    public double CpuMilliseconds;
}

internal struct RenderItem
{
    public Renderable Object;
    public BufferGeometry Geometry;
    public Material Material;
    public int Start;
    public int Count;
    public int Proxy;
}

/// <summary>Draws of one pass, sortable by a 64-bit key.</summary>
internal sealed class RenderList
{
    public RenderItem[] Items = new RenderItem[256];
    public ulong[] Keys = new ulong[256];
    public int Count;

    public void Add(in RenderItem item, ulong key)
    {
        if (Count == Items.Length)
        {
            Array.Resize(ref Items, Count * 2);
            Array.Resize(ref Keys, Count * 2);
        }

        Items[Count] = item;
        Keys[Count] = key;
        Count++;
    }

    public void Sort()
    {
        if (Count > 1)
        {
            Array.Sort(Keys, Items, 0, Count);
        }
    }

    public void Clear()
    {
        // Drop object references so removed objects can be collected.
        Array.Clear(Items, 0, Count);
        Count = 0;
    }

    /// <summary>Opaque key: render order, then material and geometry (so equal draws are adjacent and merge into
    /// instanced draws), then front to back.</summary>
    public static ulong OpaqueKey(int renderOrder, int materialId, int geometryId, float depth01)
    {
        var order = (ulong)Math.Clamp(renderOrder + 128, 0, 255);
        var d = (ulong)(Math.Clamp(depth01, 0f, 1f) * 65535f);
        return (order << 56) | (((ulong)materialId & 0xFFFFF) << 36) | (((ulong)geometryId & 0xFFFFF) << 16) | d;
    }

    /// <summary>Transparent key: render order, then back to front.</summary>
    public static ulong TransparentKey(int renderOrder, float depth, int objectId)
    {
        var order = (ulong)Math.Clamp(renderOrder + 128, 0, 255);
        var bits = BitConverter.SingleToUInt32Bits(MathF.Max(depth, 0f));
        return (order << 56) | ((ulong)(uint.MaxValue - bits) << 24) | ((ulong)objectId & 0xFFFFFF);
    }
}

/// <summary>Frustum culling over the render world's arrays. Large worlds are split into chunks that the thread pool
/// and the calling thread work through together; the job object is reused, so a frame allocates nothing.</summary>
internal sealed class Culler : IThreadPoolWorkItem
{
    private const int ParallelThreshold = 8192;
    private const int Chunk = 2048;
    private readonly CountdownEvent _done = new(1);
    private readonly int _workers = Math.Clamp(Environment.ProcessorCount - 1, 0, 7);
    private byte[] _visible = new byte[256];
    private ProxyFlags[] _flags = [];
    private BoundingSphere[] _spheres = [];
    private uint[] _layers = [];
    private Frustum _frustum;
    private ProxyFlags _need;
    private uint _layerMask;
    private int _count;
    private int _nextChunk;

    /// <summary>Collects the slots that are alive, visible, in the layers, have the required flags and touch the frustum.</summary>
    public int Cull(RenderWorld world, Frustum frustum, uint layerMask, ProxyFlags required, ref int[] result)
    {
        var n = world.HighWater;
        if (result.Length < n)
        {
            result = new int[Math.Max(n, result.Length * 2)];
        }

        var need = ProxyFlags.Alive | ProxyFlags.Visible | required;
        var flags = world.Flags;
        var spheres = world.Spheres;
        var layers = world.LayerMasks;
        var count = 0;
        if (n < ParallelThreshold || _workers == 0)
        {
            for (var i = 0; i < n; i++)
            {
                var f = flags[i];
                if ((f & need) != need || (layers[i] & layerMask) == 0)
                {
                    continue;
                }

                if ((f & ProxyFlags.FrustumCulled) != 0 && !frustum.Intersects(spheres[i]))
                {
                    continue;
                }

                result[count++] = i;
            }

            return count;
        }

        if (_visible.Length < n)
        {
            _visible = new byte[Math.Max(n, _visible.Length * 2)];
        }

        _flags = flags;
        _spheres = spheres;
        _layers = layers;
        _frustum = frustum;
        _need = need;
        _layerMask = layerMask;
        _count = n;
        _nextChunk = 0;
        var helpers = Math.Min(_workers, ((n + Chunk - 1) / Chunk) - 1);
        _done.Reset(helpers + 1);
        for (var i = 0; i < helpers; i++)
        {
            ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false);
        }

        Execute();
        _done.Wait();
        var visible = _visible;
        for (var i = 0; i < n; i++)
        {
            if (visible[i] != 0)
            {
                result[count++] = i;
            }
        }

        return count;
    }

    /// <summary>Takes chunks until none are left. Runs on pool threads and on the caller.</summary>
    public void Execute()
    {
        try
        {
            var n = _count;
            var flags = _flags;
            var spheres = _spheres;
            var layers = _layers;
            var visible = _visible;
            var need = _need;
            var layerMask = _layerMask;
            ref readonly var frustum = ref _frustum;
            while (true)
            {
                var start = (Interlocked.Increment(ref _nextChunk) - 1) * Chunk;
                if (start >= n)
                {
                    break;
                }

                var end = Math.Min(n, start + Chunk);
                for (var i = start; i < end; i++)
                {
                    var f = flags[i];
                    var ok = (f & need) == need && (layers[i] & layerMask) != 0
                        && ((f & ProxyFlags.FrustumCulled) == 0 || frustum.Intersects(spheres[i]));
                    visible[i] = ok ? (byte)1 : (byte)0;
                }
            }
        }
        finally
        {
            _done.Signal();
        }
    }
}

/// <summary>The per-frame float data the vertex stage fetches by index: object records (world matrix and parameters),
/// bone matrices and morph weights. One texture upload per frame replaces per-draw uniform updates and lets equal
/// objects be drawn as one instanced call.</summary>
internal sealed class FrameData
{
    public const int Width = Shading.ShaderLibrary.FrameDataWidth;
    private float[] _data = new float[Width * 4 * 16];

    /// <summary>Texels in use.</summary>
    public int Texels { get; private set; }

    public void Reset() => Texels = 0;

    /// <summary>Reserves texels and returns the index of the first.</summary>
    public int Allocate(int texels)
    {
        var start = Texels;
        Texels += texels;
        var need = Texels * 4;
        if (need > _data.Length)
        {
            var size = _data.Length;
            while (size < need)
            {
                size *= 2;
            }

            Array.Resize(ref _data, size);
        }

        return start;
    }

    /// <summary>Writes an affine matrix as three texels (the rotation/scale rows with the translation in w).</summary>
    public void WriteAffine(int texel, in Matrix4x4 m)
    {
        var d = _data.AsSpan(texel * 4, 12);
        d[0] = m.M11;
        d[1] = m.M12;
        d[2] = m.M13;
        d[3] = m.M41;
        d[4] = m.M21;
        d[5] = m.M22;
        d[6] = m.M23;
        d[7] = m.M42;
        d[8] = m.M31;
        d[9] = m.M32;
        d[10] = m.M33;
        d[11] = m.M43;
    }

    public void Write(int texel, float x, float y, float z, float w)
    {
        var d = _data.AsSpan(texel * 4, 4);
        d[0] = x;
        d[1] = y;
        d[2] = z;
        d[3] = w;
    }

    /// <summary>Rows of the texture that hold data.</summary>
    public int Rows => (Texels + Width - 1) / Width;

    /// <summary>The bytes of the used rows.</summary>
    public ReadOnlySpan<byte> RowBytes
    {
        get
        {
            var floats = Rows * Width * 4;
            if (floats > _data.Length)
            {
                Array.Resize(ref _data, floats);
            }

            return MemoryMarshal.AsBytes(_data.AsSpan(0, floats));
        }
    }
}
