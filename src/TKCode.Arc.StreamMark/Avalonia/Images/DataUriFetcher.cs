namespace StreamMark.Avalonia.Images;

/// <summary>解析 <c>data:image/*[;base64],...</c> 内联图片，不发起任何请求。</summary>
public sealed class DataUriFetcher : IImageFetcher
{
    public bool CanFetch(string scheme) => scheme == "data";

    public bool IsRemote => false;

    public Task<ImageFetchResult> FetchAsync(ImageFetchRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Parse(request.Url, request.Policy.MaxBytes));
    }

    internal static ImageFetchResult Parse(string url, long maxBytes)
    {
        if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return ImageFetchResult.Failed("not a data uri");
        }
        var comma = url.IndexOf(',');
        if (comma < 0)
        {
            return ImageFetchResult.Failed("malformed data uri");
        }
        var header = url.AsSpan(5, comma - 5);
        var base64 = header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
        if (base64)
        {
            header = header[..^7];
        }
        var semicolon = header.IndexOf(';');
        var mediaType = (semicolon >= 0 ? header[..semicolon] : header).Trim().ToString().ToLowerInvariant();
        if (!mediaType.StartsWith("image/", StringComparison.Ordinal))
        {
            return ImageFetchResult.Failed("not an image: " + (mediaType.Length == 0 ? "(none)" : mediaType));
        }
        var payload = url.AsSpan(comma + 1);
        // base64 每 4 个字符 3 个字节，先粗略判断上限，避免解码超大内容
        if ((base64 ? payload.Length / 4 * 3 : payload.Length / 3) > maxBytes)
        {
            return ImageFetchResult.Failed("image too large");
        }
        byte[] data;
        try
        {
            data = base64
                ? Convert.FromBase64String(payload.ToString())
                : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload.ToString()));
        }
        catch (FormatException)
        {
            return ImageFetchResult.Failed("malformed data uri");
        }
        if (data.LongLength > maxBytes)
        {
            return ImageFetchResult.Failed("image too large");
        }
        return ImageFetchResult.Ok(data, mediaType);
    }
}
