using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

/// <summary>Procedural content for render tests, so no asset files are needed.</summary>
internal static class TestScenes
{
    /// <summary>A panorama with a blue sky, a warm sun, a horizon haze and a dark ground, in linear HDR.</summary>
    public static Texture SkyEnvironment(int width = 512, int height = 256)
    {
        var data = new float[width * height * 4];
        var sun = Vector3.Normalize(new Vector3(0.5f, 0.6f, 0.4f));
        for (var y = 0; y < height; y++)
        {
            // Row 0 is the top of the image: the zenith.
            var elevation = (0.5f - ((y + 0.5f) / height)) * MathF.PI;
            for (var x = 0; x < width; x++)
            {
                var azimuth = (((x + 0.5f) / width) - 0.5f) * MathF.PI * 2f;
                var dir = new Vector3(MathF.Cos(elevation) * MathF.Cos(azimuth), MathF.Sin(elevation), MathF.Cos(elevation) * MathF.Sin(azimuth));
                Vector3 color;
                if (dir.Y >= 0f)
                {
                    var t = MathF.Pow(1f - dir.Y, 3f);
                    color = Vector3.Lerp(new Vector3(0.15f, 0.35f, 0.9f), new Vector3(0.9f, 0.95f, 1.1f), t);
                }
                else
                {
                    color = Vector3.Lerp(new Vector3(0.5f, 0.45f, 0.4f), new Vector3(0.08f, 0.07f, 0.06f), MathF.Min(1f, -dir.Y * 4f));
                }

                var d = Vector3.Dot(dir, sun);
                if (d > 0.995f)
                {
                    color += new Vector3(60f, 50f, 35f);
                }
                else if (d > 0.9f)
                {
                    color += new Vector3(1.2f, 1f, 0.7f) * MathF.Pow((d - 0.9f) / 0.095f, 4f);
                }

                var o = ((y * width) + x) * 4;
                data[o] = color.X;
                data[o + 1] = color.Y;
                data[o + 2] = color.Z;
                data[o + 3] = 1f;
            }
        }

        return new Texture(new TextureImage(width, height, Rhi.TextureFormat.RGBA32F, data))
        {
            Name = "sky",
            Mapping = TextureMapping.Equirectangular,
            GenerateMipmaps = false,
            MipFilter = Rhi.MipFilter.None,
            WrapV = Rhi.TextureWrap.ClampToEdge,
        };
    }

    /// <summary>An RGBA8 checkerboard.</summary>
    public static Texture Checker(int size = 256, int cells = 8, uint a = 0xFFFFFF, uint b = 0x8899AA)
    {
        var data = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var c = (((x * cells) / size) + ((y * cells) / size)) % 2 == 0 ? a : b;
                var o = ((y * size) + x) * 4;
                data[o] = (byte)(c >> 16);
                data[o + 1] = (byte)(c >> 8);
                data[o + 2] = (byte)c;
                data[o + 3] = 255;
            }
        }

        return new Texture(new TextureImage(size, size, data), TextureColorSpace.Srgb) { Name = "checker", Anisotropy = 8f };
    }
}
