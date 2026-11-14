namespace TKCode.Arc;

/// <summary>
/// YUV 4:2:0 to BGRA for backends whose decoder hands out planar or semi-planar pictures (Android's <c>ImageReader</c> with
/// <c>YUV_420_888</c>): converts and scales down in one pass, so only the target size is ever written. Limited range
/// ("video" levels), BT.709 for HD pictures and BT.601 for SD ones, as players do when the stream says nothing.
/// </summary>
internal static class VideoYuv
{
    // Coefficients * 1024 for limited range: Y' = 1.164 (Y - 16); R = Y' + rv V; G = Y' - gu U - gv V; B = Y' + bu U.
    private const int Luma = 1192;
    private readonly record struct Matrix(int Rv, int Gu, int Gv, int Bu);

    private static readonly Matrix s_bt601 = new(1634, 401, 832, 2066);
    private static readonly Matrix s_bt709 = new(1836, 218, 546, 2163);

    /// <summary>Whether a picture of this size is treated as HD (BT.709) when the stream names no matrix.</summary>
    public static bool IsHd(int width, int height) => width >= 1280 || height >= 720;

    /// <summary>
    /// Whether every sample <see cref="ToBgra"/> reads for a <paramref name="width"/> × <paramref name="height"/> picture lies inside
    /// the three plane buffers (given their lengths in bytes). A decoder's buffers can be shorter than the strides promise, and
    /// reading past them would throw; interleaved chroma (pixel stride 2) leaves the first chroma plane one byte short, which fits.
    /// </summary>
    public static bool PlanesFit(int width, int height, int yLength, int yRowStride, int uLength, int vLength, int chromaRowStride, int chromaPixelStride)
    {
        if (width <= 0 || height <= 0 || yRowStride < width || chromaRowStride <= 0 || chromaPixelStride <= 0)
        {
            return false;
        }
        var lastLuma = (long)(height - 1) * yRowStride + width - 1;
        var lastChroma = (long)((height - 1) >> 1) * chromaRowStride + (long)((width - 1) >> 1) * chromaPixelStride;
        return lastLuma < yLength && lastChroma < uLength && lastChroma < vLength;
    }

    /// <summary>
    /// Converts a <paramref name="width"/> × <paramref name="height"/> 4:2:0 picture to opaque BGRA at
    /// <paramref name="targetWidth"/> × <paramref name="targetHeight"/> (nearest sample, no larger than the picture), rows of
    /// <paramref name="targetWidth"/> * 4 bytes. The chroma planes are addressed like Android's <c>Image.Plane</c>:
    /// <paramref name="chromaPixelStride"/> is 1 for planar (I420) and 2 for interleaved (NV12 / NV21) data.
    /// </summary>
    public static void ToBgra(ReadOnlySpan<byte> yPlane, int yRowStride, ReadOnlySpan<byte> uPlane, ReadOnlySpan<byte> vPlane,
        int chromaRowStride, int chromaPixelStride, int width, int height, Span<byte> destination, int targetWidth, int targetHeight)
    {
        if (width <= 0 || height <= 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            return;
        }
        if (destination.Length < targetWidth * targetHeight * 4)
        {
            throw new ArgumentException("The destination is smaller than the target size.", nameof(destination));
        }
        var m = IsHd(width, height) ? s_bt709 : s_bt601;
        Span<int> columns = targetWidth <= 4096 ? stackalloc int[targetWidth] : new int[targetWidth];
        for (var x = 0; x < targetWidth; x++)
        {
            // The centre of the destination pixel, in source pixels.
            columns[x] = Math.Min(width - 1, (int)(((long)x * 2 + 1) * width / (targetWidth * 2L)));
        }
        for (var y = 0; y < targetHeight; y++)
        {
            var sy = Math.Min(height - 1, (int)(((long)y * 2 + 1) * height / (targetHeight * 2L)));
            var yRow = yPlane.Slice(sy * yRowStride);
            var chromaRow = (sy >> 1) * chromaRowStride;
            var row = destination.Slice(y * targetWidth * 4, targetWidth * 4);
            for (var x = 0; x < targetWidth; x++)
            {
                var sx = columns[x];
                var c = chromaRow + (sx >> 1) * chromaPixelStride;
                var luma = (yRow[sx] - 16) * Luma;
                var u = uPlane[c] - 128;
                var v = vPlane[c] - 128;
                var o = x * 4;
                row[o] = Clamp((luma + m.Bu * u + 512) >> 10);
                row[o + 1] = Clamp((luma - m.Gu * u - m.Gv * v + 512) >> 10);
                row[o + 2] = Clamp((luma + m.Rv * v + 512) >> 10);
                row[o + 3] = 255;
            }
        }
    }

    private static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);
}
