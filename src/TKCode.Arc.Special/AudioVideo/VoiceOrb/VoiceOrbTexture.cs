using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The orb's body as pixels: a small square texture (about one texel per 2 px, smoothed when drawn) of a two-tint fluid
/// marble with a sphere's shading, a fresnel rim, a soft specular spot, the thinking band and the swirl. It is computed on
/// the CPU into a reused buffer (no allocation per frame) and drawn clipped to the outline geometry, which keeps the edge
/// sharp. There is no GPU shader: the repository uses Avalonia's drawing API only.
/// </summary>
internal sealed class VoiceOrbTexture
{
    /// <summary>The texture covers ±<see cref="Extent"/> mean radii around the centre (the swell never passes it).</summary>
    public const double Extent = 1.16;

    private static readonly byte[] s_perm = BuildPermutation();

    private int[] _pixels = [];

    // Per-texel values that depend only on the texel's place (rebuilt when the side changes): its coordinates, the swirl's
    // falloff, the angle around the centre and the base weights of the limb shading, the fresnel rim, the specular spot and
    // the band's radial profile. Each frame then only evaluates the noise and the motion.
    private float[] _ux = [];
    private float[] _uy = [];
    private float[] _falloff = [];
    private float[] _angle = [];
    private float[] _limb = [];
    private float[] _fresnel = [];
    private float[] _specular = [];
    private float[] _bandRadial = [];
    private bool[] _inside = [];

    // exp(-(δ/0.63)²) for the band's angular profile, δ in [-π, π].
    private const int BandTableSize = 1024;
    private static readonly float[] s_bandAngular = BuildBandTable();

    /// <summary>The texture's side in texels.</summary>
    public int Side { get; private set; }

    /// <summary>The last frame's pixels (premultiplied BGRA, row-major; texels beyond the outline's reach stay 0).</summary>
    public int[] Pixels => _pixels;

    /// <summary>The texture side for an orb of <paramref name="size"/> DIP: one texel per 2 DIP, 32 to 112.</summary>
    public static int SideFor(double size) => Math.Clamp((int)Math.Ceiling(size * 0.36), 32, 112);

    /// <summary>Resizes the buffers when <paramref name="side"/> changed and precomputes the per-texel values.</summary>
    public void Ensure(int side)
    {
        if (side == Side)
        {
            return;
        }
        Side = side;
        var n = side * side;
        _pixels = new int[n];
        _ux = new float[n];
        _uy = new float[n];
        _falloff = new float[n];
        _angle = new float[n];
        _limb = new float[n];
        _fresnel = new float[n];
        _specular = new float[n];
        _bandRadial = new float[n];
        _inside = new bool[n];
        var step = 2 * Extent / side;
        for (var y = 0; y < side; y++)
        {
            var uy = -Extent + (y + 0.5) * step;
            for (var x = 0; x < side; x++)
            {
                var ux = -Extent + (x + 0.5) * step;
                var i = y * side + x;
                var r = Math.Sqrt(ux * ux + uy * uy);
                var rc = Math.Min(r, 1);
                _ux[i] = (float)ux;
                _uy[i] = (float)uy;
                _falloff[i] = (float)((1 - rc) * (1 - rc));
                _angle[i] = (float)Math.Atan2(uy, ux);
                _limb[i] = (float)(rc * rc);
                var fd = (rc - 0.93) / 0.06;
                _fresnel[i] = (float)Math.Exp(-fd * fd);
                var sx = ux + 0.38;
                var sy = uy + 0.42;
                _specular[i] = (float)Math.Exp(-(sx * sx + sy * sy) / 0.16);
                var rd = (rc - 0.86) / 0.17;
                _bandRadial[i] = r > 0.35 ? (float)Math.Exp(-rd * rd) : 0f;
                // The outline never passes the texture's inscribed circle (Extent) and clips everything outside it: those
                // texels are never seen. One texel of margin keeps the bilinear filter at the edge fed.
                _inside[i] = r <= Extent + step;
            }
        }
    }

    /// <summary>Colors and finish for one theme and tint (rebuilt only when they change).</summary>
    public readonly record struct Palette(
        Color Dark,
        Color Light,
        Color Band,
        Color Highlight,
        double Limb,
        double Fresnel,
        double Specular);

    /// <summary>Renders the body for the current <paramref name="model"/>.</summary>
    public void Render(VoiceOrbModel model, in Palette palette)
    {
        var side = Side;
        if (side == 0)
        {
            return;
        }
        var flow = model.Flow;
        var twist = model.Twist;
        var band = model.BandStrength;
        var bandAngle = model.BandAngle;
        var saturation = model.SaturationFactor;
        var glow = 0.12 * band;
        var dr = palette.Dark.R / 255.0;
        var dg = palette.Dark.G / 255.0;
        var db = palette.Dark.B / 255.0;
        var lr = palette.Light.R / 255.0;
        var lg = palette.Light.G / 255.0;
        var lb = palette.Light.B / 255.0;
        var br = palette.Band.R / 255.0;
        var bg = palette.Band.G / 255.0;
        var bb = palette.Band.B / 255.0;
        var hr = palette.Highlight.R / 255.0;
        var hg = palette.Highlight.G / 255.0;
        var hb = palette.Highlight.B / 255.0;
        var limbK = palette.Limb;
        var fresnelK = palette.Fresnel;
        var specularK = palette.Specular;
        // The whole field turns slowly and drifts; the swirl twists it more near the centre.
        var spin = 0.07 * flow;
        var driftX = 0.11 * flow;
        var driftY = -0.07 * flow;
        var w1y = 9.2 + 0.13 * flow;
        var w2x = 8.3 - 0.1 * flow;
        // Without a swirl the turn is the same for every texel: one sine and cosine for the frame.
        var swirl = Math.Abs(twist) > 0.002;
        var cs = Math.Cos(spin);
        var ss = Math.Sin(spin);
        var drawBand = band > 0.002;
        var bandWeight = 0.9 * band;
        var tableScale = BandTableSize / (2 * Math.PI);
        var muted = saturation < 0.999;
        var dim = 0.9 + 0.1 * saturation;
        var n = side * side;
        var pixels = _pixels;
        for (var i = 0; i < n; i++)
        {
            if (!_inside[i])
            {
                continue;
            }
            double ux = _ux[i];
            double uy = _uy[i];
            var ct = cs;
            var st = ss;
            if (swirl)
            {
                var turn = twist * _falloff[i] + spin;
                ct = Math.Cos(turn);
                st = Math.Sin(turn);
            }
            var qx = ux * ct - uy * st + driftX;
            var qy = ux * st + uy * ct + driftY;
            // Domain warp: two noise fields displace the third, which gives the soft, folded marble.
            var w1 = Noise(qx + 1.7, qy + w1y);
            var w2 = Noise(qx + w2x, qy + 2.8);
            var m = Noise(qx * 1.3 + 1.3 * w1, qy * 1.3 + 1.3 * w2);
            // Broad, soft folds: mostly mid tones, the light tint only on the crests.
            var stripe = SmoothStep(-0.5, 0.55, m);
            var t = 0.1 + 0.9 * stripe * stripe * (0.6 + 0.4 * stripe);
            var cr = dr + (lr - dr) * t;
            var cg = dg + (lg - dg) * t;
            var cb = db + (lb - db) * t;
            // Thinking lifts the whole body toward the light tint.
            cr += (lr - cr) * glow;
            cg += (lg - cg) * glow;
            cb += (lb - cb) * glow;
            // Sphere shading (darker toward the limb) and a fresnel rim just inside the edge.
            var shade = 1 - limbK * _limb[i];
            cr *= shade;
            cg *= shade;
            cb *= shade;
            var fresnel = fresnelK * _fresnel[i];
            cr += (lr - cr) * fresnel;
            cg += (lg - cg) * fresnel;
            cb += (lb - cb) * fresnel;
            // A soft specular spot up and to the left.
            var spec = specularK * _specular[i];
            cr += (hr - cr) * spec;
            cg += (hg - cg) * spec;
            cb += (hb - cb) * spec;
            // The thinking band: a bright arc just inside the rim (half width ≈ 60°).
            if (drawBand && _bandRadial[i] > 0)
            {
                var delta = _angle[i] - bandAngle;
                delta -= 2 * Math.PI * Math.Floor((delta + Math.PI) / (2 * Math.PI));
                var slot = (int)((delta + Math.PI) * tableScale);
                var weight = bandWeight * s_bandAngular[Math.Clamp(slot, 0, BandTableSize - 1)] * _bandRadial[i];
                cr += (br - cr) * weight;
                cg += (bg - cg) * weight;
                cb += (bb - cb) * weight;
            }
            // Muted: color drains toward gray and dims a little.
            if (muted)
            {
                var luma = 0.2126 * cr + 0.7152 * cg + 0.0722 * cb;
                cr = (luma + (cr - luma) * saturation) * dim;
                cg = (luma + (cg - luma) * saturation) * dim;
                cb = (luma + (cb - luma) * saturation) * dim;
            }
            pixels[i] = Pack(cr, cg, cb);
        }
    }

    private static float[] BuildBandTable()
    {
        var table = new float[BandTableSize];
        for (var i = 0; i < BandTableSize; i++)
        {
            var delta = (i + 0.5) / BandTableSize * 2 * Math.PI - Math.PI;
            var ad = delta / 0.63;
            table[i] = (float)Math.Exp(-ad * ad);
        }
        return table;
    }

    private static int Pack(double r, double g, double b)
    {
        var R = (int)(Math.Clamp(r, 0, 1) * 255 + 0.5);
        var G = (int)(Math.Clamp(g, 0, 1) * 255 + 0.5);
        var B = (int)(Math.Clamp(b, 0, 1) * 255 + 0.5);
        return unchecked((int)(0xFF000000u | ((uint)R << 16) | ((uint)G << 8) | (uint)B));
    }

    private static double SmoothStep(double a, double b, double x)
    {
        var t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>2D gradient noise (Perlin), roughly −0.7 to 0.7.</summary>
    internal static double Noise(double x, double y)
    {
        var xf = Math.Floor(x);
        var yf = Math.Floor(y);
        var xi = (int)xf & 255;
        var yi = (int)yf & 255;
        x -= xf;
        y -= yf;
        var u = x * x * x * (x * (x * 6 - 15) + 10);
        var v = y * y * y * (y * (y * 6 - 15) + 10);
        var p = s_perm;
        var aa = p[p[xi] + yi];
        var ab = p[p[xi] + yi + 1];
        var ba = p[p[xi + 1] + yi];
        var bb = p[p[xi + 1] + yi + 1];
        var x1 = Lerp(Grad(aa, x, y), Grad(ba, x - 1, y), u);
        var x2 = Lerp(Grad(ab, x, y - 1), Grad(bb, x - 1, y - 1), u);
        return Lerp(x1, x2, v);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double Grad(int hash, double x, double y) => (hash & 7) switch
    {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        3 => -x - y,
        4 => x,
        5 => -x,
        6 => y,
        _ => -y
    };

    private static byte[] BuildPermutation()
    {
        // A fixed shuffle (xorshift with a constant seed), so every orb shows the same marble for the same time.
        var source = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            source[i] = (byte)i;
        }
        var state = 0x2545F491u;
        for (var i = 255; i > 0; i--)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            var j = (int)(state % (uint)(i + 1));
            (source[i], source[j]) = (source[j], source[i]);
        }
        var perm = new byte[512 + 2];
        for (var i = 0; i < perm.Length; i++)
        {
            perm[i] = source[i & 255];
        }
        return perm;
    }
}
