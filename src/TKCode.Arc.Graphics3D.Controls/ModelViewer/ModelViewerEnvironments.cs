using System.Numerics;
using TKCode.Arc.Graphics3D;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc;

/// <summary>The lighting a <see cref="ModelViewer"/> surrounds its model with.</summary>
public enum ModelViewerEnvironment
{
    /// <summary>A neutral studio: soft boxes above and to the sides on a gray cyclorama. Colors stay true.</summary>
    Studio,

    /// <summary>A clear sky with a warm sun over a neutral ground.</summary>
    Outdoor,

    /// <summary>No image-based lighting: only the viewer's two lights.</summary>
    None,
}

/// <summary>The viewer's environments are computed, not shipped as image files.</summary>
internal static class ModelViewerEnvironments
{
    private const int Width = 256;
    private const int Height = 128;
    private static Texture? s_studio;
    private static Texture? s_outdoor;

    public static Texture? Get(ModelViewerEnvironment environment) => environment switch
    {
        ModelViewerEnvironment.Studio => s_studio ??= Create("studio", Studio),
        ModelViewerEnvironment.Outdoor => s_outdoor ??= Create("outdoor", Outdoor),
        _ => null,
    };

    private static Texture Create(string name, Func<Vector3, Vector3> radiance)
    {
        var data = new float[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        {
            // Row 0 is the zenith.
            var elevation = (0.5f - ((y + 0.5f) / Height)) * MathF.PI;
            var (sinE, cosE) = MathF.SinCos(elevation);
            for (var x = 0; x < Width; x++)
            {
                var azimuth = (((x + 0.5f) / Width) - 0.5f) * MathF.PI * 2f;
                var (sinA, cosA) = MathF.SinCos(azimuth);
                var color = radiance(new Vector3(cosE * cosA, sinE, cosE * sinA));
                var o = ((y * Width) + x) * 4;
                data[o] = color.X;
                data[o + 1] = color.Y;
                data[o + 2] = color.Z;
                data[o + 3] = 1f;
            }
        }

        return new Texture(new TextureImage(Width, Height, TextureFormat.RGBA32F, data))
        {
            Name = "model-viewer-" + name,
            Mapping = TextureMapping.Equirectangular,
            GenerateMipmaps = false,
            MipFilter = MipFilter.None,
            WrapV = TextureWrap.ClampToEdge,
        };
    }

    /// <summary>A soft rectangular light: 1 inside the cone around <paramref name="center"/>, fading to 0 at its edge.</summary>
    private static float Box(Vector3 direction, Vector3 center, float halfAngle, float softness)
    {
        var cos = Vector3.Dot(direction, Vector3.Normalize(center));
        var edge = MathF.Cos(halfAngle);
        return MathUtil.SmoothStep(edge - softness, edge + softness, cos);
    }

    private static Vector3 Studio(Vector3 d)
    {
        // A gray room that is brighter toward the ceiling, with a darker floor.
        var room = d.Y >= 0f ? MathUtil.Lerp(0.28f, 0.55f, d.Y) : MathUtil.Lerp(0.28f, 0.1f, MathF.Min(1f, -d.Y * 2.5f));
        var color = new Vector3(room);
        color += new Vector3(9f, 8.8f, 8.5f) * Box(d, new Vector3(0.55f, 0.75f, 0.6f), 0.33f, 0.05f);
        color += new Vector3(3.2f, 3.3f, 3.5f) * Box(d, new Vector3(-0.8f, 0.35f, 0.35f), 0.42f, 0.08f);
        color += new Vector3(4.5f, 4.5f, 4.6f) * Box(d, new Vector3(-0.2f, 0.55f, -0.85f), 0.28f, 0.05f);
        color += new Vector3(1.6f) * Box(d, new Vector3(0f, 1f, 0f), 0.5f, 0.15f);
        return color;
    }

    private static Vector3 Outdoor(Vector3 d)
    {
        Vector3 color;
        if (d.Y >= 0f)
        {
            color = Vector3.Lerp(new Vector3(0.18f, 0.38f, 0.92f), new Vector3(0.9f, 0.95f, 1.08f), MathF.Pow(1f - d.Y, 3f));
        }
        else
        {
            color = Vector3.Lerp(new Vector3(0.5f, 0.47f, 0.43f), new Vector3(0.1f, 0.09f, 0.08f), MathF.Min(1f, -d.Y * 4f));
        }

        var sun = Vector3.Dot(d, Vector3.Normalize(new Vector3(0.5f, 0.62f, 0.45f)));
        if (sun > 0.995f)
        {
            color += new Vector3(55f, 47f, 34f);
        }
        else if (sun > 0.9f)
        {
            color += new Vector3(1.2f, 1f, 0.7f) * MathF.Pow((sun - 0.9f) / 0.095f, 4f);
        }

        return color;
    }
}
