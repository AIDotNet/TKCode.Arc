// The uncompressed 8-bit formats a KTX2 file may use here, and names for the formats that are rejected.
//
// Format numbers are VkFormat values (Vulkan specification); color model numbers are from the Khronos Data
// Format Specification and the KTX 2.0 specification.

namespace TKCode.Arc.Graphics3D;

internal static class Ktx2Formats
{
    private const uint R8Unorm = 9;
    private const uint R8Srgb = 15;
    private const uint R8G8Unorm = 16;
    private const uint R8G8Srgb = 22;
    private const uint R8G8B8Unorm = 23;
    private const uint R8G8B8Srgb = 29;
    private const uint B8G8R8Unorm = 30;
    private const uint B8G8R8Srgb = 36;
    private const uint R8G8B8A8Unorm = 37;
    private const uint R8G8B8A8Srgb = 43;
    private const uint B8G8R8A8Unorm = 44;
    private const uint B8G8R8A8Srgb = 50;

    private static readonly string[] Suffixes8 = ["UNORM", "SNORM", "USCALED", "SSCALED", "UINT", "SINT", "SRGB"];
    private static readonly string[] Suffixes16 = ["UNORM", "SNORM", "USCALED", "SSCALED", "UINT", "SINT", "SFLOAT"];
    private static readonly string[] Suffixes32 = ["UINT", "SINT", "SFLOAT"];
    private static readonly string[] Channels8 = ["R8", "R8G8", "R8G8B8", "B8G8R8", "R8G8B8A8", "B8G8R8A8"];
    private static readonly string[] Channels16 = ["R16", "R16G16", "R16G16B16", "R16G16B16A16"];
    private static readonly string[] Channels32 = ["R32", "R32G32", "R32G32B32", "R32G32B32A32"];

    private static readonly string[] PackedFormats =
    [
        "R4G4_UNORM_PACK8", "R4G4B4A4_UNORM_PACK16", "B4G4R4A4_UNORM_PACK16", "R5G6B5_UNORM_PACK16",
        "B5G6R5_UNORM_PACK16", "R5G5B5A1_UNORM_PACK16", "B5G5R5A1_UNORM_PACK16", "A1R5G5B5_UNORM_PACK16",
    ];

    private static readonly string[] BlockFormats =
    [
        "BC1_RGB_UNORM", "BC1_RGB_SRGB", "BC1_RGBA_UNORM", "BC1_RGBA_SRGB", "BC2_UNORM", "BC2_SRGB", "BC3_UNORM", "BC3_SRGB",
        "BC4_UNORM", "BC4_SNORM", "BC5_UNORM", "BC5_SNORM", "BC6H_UFLOAT", "BC6H_SFLOAT", "BC7_UNORM", "BC7_SRGB",
        "ETC2_R8G8B8_UNORM", "ETC2_R8G8B8_SRGB", "ETC2_R8G8B8A1_UNORM", "ETC2_R8G8B8A1_SRGB", "ETC2_R8G8B8A8_UNORM", "ETC2_R8G8B8A8_SRGB",
        "EAC_R11_UNORM", "EAC_R11_SNORM", "EAC_R11G11_UNORM", "EAC_R11G11_SNORM",
    ];

    private static readonly string[] AstcSizes =
    [
        "4x4", "5x4", "5x5", "6x5", "6x6", "8x5", "8x6", "8x8", "10x5", "10x6", "10x8", "10x10", "12x10", "12x12",
    ];

    /// <summary>Bytes per pixel of a supported uncompressed format, 0 for any other format.</summary>
    public static int BytesPerPixel(uint vkFormat)
    {
        return vkFormat switch
        {
            R8Unorm or R8Srgb => 1,
            R8G8Unorm or R8G8Srgb => 2,
            R8G8B8Unorm or R8G8B8Srgb or B8G8R8Unorm or B8G8R8Srgb => 3,
            R8G8B8A8Unorm or R8G8B8A8Srgb or B8G8R8A8Unorm or B8G8R8A8Srgb => 4,
            _ => 0,
        };
    }

    public static bool HasAlpha(uint vkFormat)
    {
        return vkFormat is R8G8B8A8Unorm or R8G8B8A8Srgb or B8G8R8A8Unorm or B8G8R8A8Srgb;
    }

    /// <summary>
    /// Expands pixels of a supported format to RGBA8: missing green and blue are 0, missing alpha is 255,
    /// the way a GPU samples such a texture.
    /// </summary>
    public static void ToRgba(uint vkFormat, ReadOnlySpan<byte> source, Span<byte> rgba)
    {
        int count = rgba.Length / 4;
        switch (vkFormat)
        {
            case R8G8B8A8Unorm:
            case R8G8B8A8Srgb:
                source.Slice(0, count * 4).CopyTo(rgba);
                break;

            case B8G8R8A8Unorm:
            case B8G8R8A8Srgb:
                for (int i = 0; i < count; i++)
                {
                    rgba[i * 4] = source[i * 4 + 2];
                    rgba[i * 4 + 1] = source[i * 4 + 1];
                    rgba[i * 4 + 2] = source[i * 4];
                    rgba[i * 4 + 3] = source[i * 4 + 3];
                }

                break;

            case R8G8B8Unorm:
            case R8G8B8Srgb:
            case B8G8R8Unorm:
            case B8G8R8Srgb:
                int red = vkFormat is R8G8B8Unorm or R8G8B8Srgb ? 0 : 2;
                for (int i = 0; i < count; i++)
                {
                    rgba[i * 4] = source[i * 3 + red];
                    rgba[i * 4 + 1] = source[i * 3 + 1];
                    rgba[i * 4 + 2] = source[i * 3 + 2 - red];
                    rgba[i * 4 + 3] = 255;
                }

                break;

            case R8G8Unorm:
            case R8G8Srgb:
                for (int i = 0; i < count; i++)
                {
                    rgba[i * 4] = source[i * 2];
                    rgba[i * 4 + 1] = source[i * 2 + 1];
                    rgba[i * 4 + 2] = 0;
                    rgba[i * 4 + 3] = 255;
                }

                break;

            default:
                for (int i = 0; i < count; i++)
                {
                    rgba[i * 4] = source[i];
                    rgba[i * 4 + 1] = 0;
                    rgba[i * 4 + 2] = 0;
                    rgba[i * 4 + 3] = 255;
                }

                break;
        }
    }

    /// <summary>The Vulkan name of a format, for error messages.</summary>
    public static string Name(uint vkFormat)
    {
        string? name = null;
        if (vkFormat >= 1 && vkFormat <= 8)
        {
            name = PackedFormats[vkFormat - 1];
        }
        else if (vkFormat >= 9 && vkFormat <= 50)
        {
            name = Channels8[(vkFormat - 9) / 7] + "_" + Suffixes8[(vkFormat - 9) % 7];
        }
        else if (vkFormat >= 70 && vkFormat <= 97)
        {
            name = Channels16[(vkFormat - 70) / 7] + "_" + Suffixes16[(vkFormat - 70) % 7];
        }
        else if (vkFormat >= 98 && vkFormat <= 109)
        {
            name = Channels32[(vkFormat - 98) / 3] + "_" + Suffixes32[(vkFormat - 98) % 3];
        }
        else if (vkFormat == 122)
        {
            name = "B10G11R11_UFLOAT_PACK32";
        }
        else if (vkFormat == 123)
        {
            name = "E5B9G9R9_UFLOAT_PACK32";
        }
        else if (vkFormat >= 131 && vkFormat <= 156)
        {
            name = BlockFormats[vkFormat - 131] + "_BLOCK";
        }
        else if (vkFormat >= 157 && vkFormat <= 184)
        {
            name = "ASTC_" + AstcSizes[(vkFormat - 157) / 2] + ((vkFormat & 1) != 0 ? "_UNORM_BLOCK" : "_SRGB_BLOCK");
        }
        else if (vkFormat >= 1000066000 && vkFormat <= 1000066013)
        {
            name = "ASTC_" + AstcSizes[vkFormat - 1000066000] + "_SFLOAT_BLOCK";
        }

        return name is null ? $"VkFormat {vkFormat}" : $"VK_FORMAT_{name} ({vkFormat})";
    }

    /// <summary>A name for a data format descriptor color model, for error messages.</summary>
    public static string ColorModelName(int colorModel)
    {
        string? name = colorModel switch
        {
            1 => "RGBSDA",
            128 => "BC1A",
            129 => "BC2",
            130 => "BC3",
            131 => "BC4",
            132 => "BC5",
            133 => "BC6H",
            134 => "BC7",
            160 => "ETC1",
            161 => "ETC2",
            162 => "ASTC",
            164 => "PVRTC",
            165 => "PVRTC2",
            167 => "UASTC HDR 4x4",
            168 => "UASTC HDR 6x6",
            169 => "XUASTC LDR",
            _ => null,
        };

        return name is null ? colorModel.ToString() : $"{colorModel} ({name})";
    }
}
