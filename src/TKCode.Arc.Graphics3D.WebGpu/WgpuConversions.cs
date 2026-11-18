namespace TKCode.Arc.Graphics3D.Rhi.WebGpu;

/// <summary>Maps the engine's formats to WebGPU's and converts pixel data between what the engine hands over and what
/// WebGPU stores.</summary>
internal static class WgpuConversions
{
    public static uint Texture(TextureFormat format) => format switch
    {
        TextureFormat.R8 => Wgpu.FormatR8Unorm,
        TextureFormat.RG8 => Wgpu.FormatRG8Unorm,
        TextureFormat.RGBA8 => Wgpu.FormatRGBA8Unorm,
        TextureFormat.RGBA8Srgb => Wgpu.FormatRGBA8UnormSrgb,
        TextureFormat.R16F => Wgpu.FormatR16Float,
        TextureFormat.RG16F => Wgpu.FormatRG16Float,
        TextureFormat.RGBA16F => Wgpu.FormatRGBA16Float,
        TextureFormat.R32F => Wgpu.FormatR32Float,
        TextureFormat.RG32F => Wgpu.FormatRG32Float,
        TextureFormat.RGBA32F => Wgpu.FormatRGBA32Float,
        TextureFormat.RG11B10F => Wgpu.FormatRG11B10Ufloat,
        TextureFormat.Depth16 => Wgpu.FormatDepth16Unorm,

        // WebGPU has no exact 24-bit depth format: "depth24plus" is at least 24 bits.
        TextureFormat.Depth24 => Wgpu.FormatDepth24Plus,
        TextureFormat.Depth32F => Wgpu.FormatDepth32Float,
        TextureFormat.Depth24Stencil8 => Wgpu.FormatDepth24PlusStencil8,
        TextureFormat.Bc1Rgba => Wgpu.FormatBc1RgbaUnorm,
        TextureFormat.Bc1RgbaSrgb => Wgpu.FormatBc1RgbaUnormSrgb,
        TextureFormat.Bc3Rgba => Wgpu.FormatBc3RgbaUnorm,
        TextureFormat.Bc3RgbaSrgb => Wgpu.FormatBc3RgbaUnormSrgb,
        TextureFormat.Bc4R => Wgpu.FormatBc4RUnorm,
        TextureFormat.Bc5RG => Wgpu.FormatBc5RgUnorm,
        TextureFormat.Bc6HRgbUFloat => Wgpu.FormatBc6hRgbUfloat,
        TextureFormat.Bc7Rgba => Wgpu.FormatBc7RgbaUnorm,
        TextureFormat.Bc7RgbaSrgb => Wgpu.FormatBc7RgbaUnormSrgb,
        TextureFormat.Etc2Rgb8 => Wgpu.FormatEtc2Rgb8Unorm,
        TextureFormat.Etc2Rgb8Srgb => Wgpu.FormatEtc2Rgb8UnormSrgb,
        TextureFormat.Etc2Rgba8 => Wgpu.FormatEtc2Rgba8Unorm,
        TextureFormat.Etc2Rgba8Srgb => Wgpu.FormatEtc2Rgba8UnormSrgb,
        TextureFormat.Astc4x4 => Wgpu.FormatAstc4x4Unorm,
        TextureFormat.Astc4x4Srgb => Wgpu.FormatAstc4x4UnormSrgb,
        _ => throw new NotSupportedException($"Texture format {format} is not supported."),
    };

    /// <summary>Bytes of one 4x4 block of a compressed format.</summary>
    public static int BlockBytes(TextureFormat format) => format switch
    {
        TextureFormat.Bc1Rgba or TextureFormat.Bc1RgbaSrgb or TextureFormat.Bc4R or TextureFormat.Etc2Rgb8 or TextureFormat.Etc2Rgb8Srgb => 8,
        _ => 16,
    };

    /// <summary>Bytes per pixel as WebGPU stores an uncompressed color format.</summary>
    public static int StoredBytesPerPixel(TextureFormat format) => format.BytesPerPixel();

    /// <summary>Bytes per pixel the engine hands to <see cref="GraphicsDevice.WriteTexture"/>: float formats come as
    /// 32-bit floats per channel (three for RG11B10F), as the OpenGL device takes them.</summary>
    public static int UploadBytesPerPixel(TextureFormat format) => format switch
    {
        TextureFormat.R8 => 1,
        TextureFormat.RG8 => 2,
        TextureFormat.RGBA8 or TextureFormat.RGBA8Srgb => 4,
        TextureFormat.R16F or TextureFormat.R32F => 4,
        TextureFormat.RG16F or TextureFormat.RG32F => 8,
        TextureFormat.RGBA16F or TextureFormat.RGBA32F => 16,
        TextureFormat.RG11B10F => 12,
        _ => 0,
    };

    /// <summary>The WebGPU vertex format of an attribute, or 0 when WebGPU has none: it has no three-component
    /// integer formats and none that turn integers into unnormalized floats.</summary>
    public static uint Vertex(VertexFormat format) => format switch
    {
        VertexFormat.Float1 => Wgpu.VertexFloat32,
        VertexFormat.Float2 => Wgpu.VertexFloat32x2,
        VertexFormat.Float3 => Wgpu.VertexFloat32x3,
        VertexFormat.Float4 => Wgpu.VertexFloat32x4,
        VertexFormat.UByte4Norm => Wgpu.VertexUnorm8x4,
        VertexFormat.UByte2Norm => Wgpu.VertexUnorm8x2,
        VertexFormat.SByte4Norm => Wgpu.VertexSnorm8x4,
        VertexFormat.SByte2Norm => Wgpu.VertexSnorm8x2,
        VertexFormat.UShort4Norm => Wgpu.VertexUnorm16x4,
        VertexFormat.UShort2Norm => Wgpu.VertexUnorm16x2,
        VertexFormat.Short4Norm => Wgpu.VertexSnorm16x4,
        VertexFormat.Short2Norm => Wgpu.VertexSnorm16x2,
        _ => 0,
    };

    public static void FloatToHalf(ReadOnlySpan<float> source, Span<ushort> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = BitConverter.HalfToUInt16Bits((Half)source[i]);
        }
    }

    public static float HalfToFloat(ushort bits) => (float)BitConverter.UInt16BitsToHalf(bits);

    /// <summary>Packs RGB floats into the shared-exponent-free 11/11/10-bit unsigned float format.</summary>
    public static uint PackRg11b10(float r, float g, float b) => UFloat(r, 6) | (UFloat(g, 6) << 11) | (UFloat(b, 5) << 22);

    public static void UnpackRg11b10(uint packed, out float r, out float g, out float b)
    {
        r = FromUFloat(packed & 0x7FF, 6);
        g = FromUFloat((packed >> 11) & 0x7FF, 6);
        b = FromUFloat(packed >> 22, 5);
    }

    // An unsigned float with a 5-bit exponent and the given number of mantissa bits: the bits of a half float without
    // the sign, with the mantissa cut to size.
    private static uint UFloat(float value, int mantissaBits)
    {
        if (float.IsNaN(value) || value <= 0f)
        {
            return 0;
        }

        var half = BitConverter.HalfToUInt16Bits((Half)MathF.Min(value, 64000f));
        return (uint)(half & 0x7FFF) >> (10 - mantissaBits);
    }

    private static float FromUFloat(uint bits, int mantissaBits) => HalfToFloat((ushort)(bits << (10 - mantissaBits)));
}
