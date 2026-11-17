using Avalonia.Media;

namespace TKCode.Arc.Tests.Special.AudioVideo;

/// <summary>
/// The optimised texture (per-texel invariants cached, band angle from a table, swirl-free fast path, texels beyond the
/// outline skipped) against the direct per-texel formula it replaced: same pixels within rounding.
/// </summary>
public class VoiceOrbTextureTests
{
    private static readonly VoiceOrbTexture.Palette s_palette =
        new(Color.FromRgb(40, 44, 60), Color.FromRgb(170, 180, 230), Color.FromRgb(240, 240, 255), Colors.White, 0.18, 0.12, 0.08);

    [Theory]
    [InlineData(VoiceOrbState.Idle, false, 1.0)]
    [InlineData(VoiceOrbState.Listening, false, 2.5)]
    [InlineData(VoiceOrbState.Thinking, false, 3.0)]
    [InlineData(VoiceOrbState.Speaking, false, 1.7)]
    [InlineData(VoiceOrbState.Listening, true, 2.0)]
    public void Matches_the_direct_formula(VoiceOrbState state, bool muted, double seconds)
    {
        var model = new VoiceOrbModel();
        model.SetState(state, muted);
        for (var t = 0.0; t < seconds; t += 1 / 60.0)
        {
            model.Step(1 / 60.0, 0.4 + 0.3 * Math.Sin(t * 9));
        }
        var texture = new VoiceOrbTexture();
        texture.Ensure(VoiceOrbTexture.SideFor(208));
        texture.Render(model, s_palette);
        var side = texture.Side;
        var reference = Reference(model, s_palette, side);
        var step = 2 * VoiceOrbTexture.Extent / side;
        var compared = 0;
        var worst = 0;
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var ux = -VoiceOrbTexture.Extent + (x + 0.5) * step;
                var uy = -VoiceOrbTexture.Extent + (y + 0.5) * step;
                if (Math.Sqrt(ux * ux + uy * uy) > VoiceOrbTexture.Extent)
                {
                    continue;
                }
                var a = texture.Pixels[y * side + x];
                var b = reference[y * side + x];
                for (var shift = 0; shift <= 16; shift += 8)
                {
                    worst = Math.Max(worst, Math.Abs(((a >> shift) & 0xFF) - ((b >> shift) & 0xFF)));
                }
                compared++;
            }
        }
        compared.ShouldBeGreaterThan(side * side / 2);
        // Float caches and the 1024-step band table: at most a few levels off.
        worst.ShouldBeLessThanOrEqualTo(3);
    }

    /// <summary>The body formula as first written: every term evaluated per texel per frame.</summary>
    private static int[] Reference(VoiceOrbModel model, VoiceOrbTexture.Palette palette, int side)
    {
        const double extent = VoiceOrbTexture.Extent;
        var pixels = new int[side * side];
        var flow = model.Flow;
        var twist = model.Twist;
        var band = model.BandStrength;
        var bandAngle = model.BandAngle;
        var saturation = model.SaturationFactor;
        var glow = 0.12 * band;
        double dr = palette.Dark.R / 255.0, dg = palette.Dark.G / 255.0, db = palette.Dark.B / 255.0;
        double lr = palette.Light.R / 255.0, lg = palette.Light.G / 255.0, lb = palette.Light.B / 255.0;
        double br = palette.Band.R / 255.0, bg = palette.Band.G / 255.0, bb = palette.Band.B / 255.0;
        double hr = palette.Highlight.R / 255.0, hg = palette.Highlight.G / 255.0, hb = palette.Highlight.B / 255.0;
        var spin = 0.07 * flow;
        var driftX = 0.11 * flow;
        var driftY = -0.07 * flow;
        var cosBand = Math.Cos(bandAngle);
        var sinBand = Math.Sin(bandAngle);
        var step = 2 * extent / side;
        for (var y = 0; y < side; y++)
        {
            var uy = -extent + (y + 0.5) * step;
            for (var x = 0; x < side; x++)
            {
                var ux = -extent + (x + 0.5) * step;
                var r = Math.Sqrt(ux * ux + uy * uy);
                var rc = Math.Min(r, 1);
                var turn = twist * (1 - rc) * (1 - rc) + spin;
                var ct = Math.Cos(turn);
                var st = Math.Sin(turn);
                var qx = ux * ct - uy * st + driftX;
                var qy = ux * st + uy * ct + driftY;
                var w1 = VoiceOrbTexture.Noise(qx + 1.7, qy + 9.2 + 0.13 * flow);
                var w2 = VoiceOrbTexture.Noise(qx + 8.3 - 0.1 * flow, qy + 2.8);
                var m = VoiceOrbTexture.Noise(qx * 1.3 + 1.3 * w1, qy * 1.3 + 1.3 * w2);
                var s = Math.Clamp((m + 0.5) / 1.05, 0, 1);
                var stripe = s * s * (3 - 2 * s);
                var t = 0.1 + 0.9 * stripe * stripe * (0.6 + 0.4 * stripe);
                var cr = dr + (lr - dr) * t;
                var cg = dg + (lg - dg) * t;
                var cb = db + (lb - db) * t;
                cr += (lr - cr) * glow;
                cg += (lg - cg) * glow;
                cb += (lb - cb) * glow;
                var shade = 1 - palette.Limb * rc * rc;
                cr *= shade;
                cg *= shade;
                cb *= shade;
                var fd = (rc - 0.93) / 0.06;
                var fresnel = palette.Fresnel * Math.Exp(-fd * fd);
                cr += (lr - cr) * fresnel;
                cg += (lg - cg) * fresnel;
                cb += (lb - cb) * fresnel;
                var sx = ux + 0.38;
                var sy = uy + 0.42;
                var spec = palette.Specular * Math.Exp(-(sx * sx + sy * sy) / 0.16);
                cr += (hr - cr) * spec;
                cg += (hg - cg) * spec;
                cb += (hb - cb) * spec;
                if (band > 0.002 && r > 0.35)
                {
                    var dot = (ux * cosBand + uy * sinBand) / r;
                    var cross = (uy * cosBand - ux * sinBand) / r;
                    var ad = Math.Atan2(cross, dot) / 0.63;
                    var rd = (rc - 0.86) / 0.17;
                    var weight = 0.9 * band * Math.Exp(-ad * ad - rd * rd);
                    cr += (br - cr) * weight;
                    cg += (bg - cg) * weight;
                    cb += (bb - cb) * weight;
                }
                if (saturation < 0.999)
                {
                    var luma = 0.2126 * cr + 0.7152 * cg + 0.0722 * cb;
                    var dim = 0.9 + 0.1 * saturation;
                    cr = (luma + (cr - luma) * saturation) * dim;
                    cg = (luma + (cg - luma) * saturation) * dim;
                    cb = (luma + (cb - luma) * saturation) * dim;
                }
                var R = (int)(Math.Clamp(cr, 0, 1) * 255 + 0.5);
                var G = (int)(Math.Clamp(cg, 0, 1) * 255 + 0.5);
                var B = (int)(Math.Clamp(cb, 0, 1) * 255 + 0.5);
                pixels[y * side + x] = unchecked((int)(0xFF000000u | ((uint)R << 16) | ((uint)G << 8) | (uint)B));
            }
        }
        return pixels;
    }
}
