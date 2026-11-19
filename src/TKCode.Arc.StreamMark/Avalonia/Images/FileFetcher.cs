namespace StreamMark.Avalonia.Images;

/// <summary>
/// 读取 <c>file:</c> 图片，只允许位于 <see cref="ImagePolicy.AllowedFileRoots"/> 之下的文件。
/// 路径先规范化（消除 <c>..</c>），再逐级解析符号链接，防止越界。
/// </summary>
public sealed class FileFetcher : IImageFetcher
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    public bool CanFetch(string scheme) => scheme == "file";

    public bool IsRemote => false;

    public async Task<ImageFetchResult> FetchAsync(ImageFetchRequest request, CancellationToken cancellationToken)
    {
        var uri = request.Uri;
        if (uri is null || !uri.IsFile)
        {
            return ImageFetchResult.Failed("invalid file url");
        }
        if (!uri.IsLoopback && !string.IsNullOrEmpty(uri.Host))
        {
            return ImageFetchResult.Blocked("remote file share not allowed");
        }
        var path = ResolveAllowedPath(uri.LocalPath, request.Policy.AllowedFileRoots);
        if (path is null)
        {
            return ImageFetchResult.Blocked("path outside allowed roots");
        }
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return ImageFetchResult.Failed("file not found");
            }
            if (info.Length > request.Policy.MaxBytes)
            {
                return ImageFetchResult.Failed("image too large");
            }
            var data = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return ImageFetchResult.Ok(data, ContentTypeFromExtension(path));
        }
        catch (IOException ex)
        {
            return ImageFetchResult.Failed("io error: " + ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return ImageFetchResult.Failed("access denied");
        }
    }

    /// <summary>若路径（解析符号链接后）位于某个允许的根目录下，返回其真实路径，否则返回 null。</summary>
    internal static string? ResolveAllowedPath(string path, IReadOnlyList<string> roots)
    {
        if (roots.Count == 0)
        {
            return null;
        }
        string real;
        try
        {
            real = RealPath(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }
            string realRoot;
            try
            {
                realRoot = Path.TrimEndingDirectorySeparator(RealPath(Path.GetFullPath(root)));
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
            {
                continue;
            }
            if (real.Length > realRoot.Length
                && real.StartsWith(realRoot, PathComparison)
                && (real[realRoot.Length] == Path.DirectorySeparatorChar || real[realRoot.Length] == Path.AltDirectorySeparatorChar))
            {
                return real;
            }
        }
        return null;
    }

    /// <summary>逐级解析符号链接，得到真实绝对路径（不存在的部分原样保留）。</summary>
    internal static string RealPath(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath) ?? "";
        var current = root;
        var parts = fullPath[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var hops = 0;
        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists && info.LinkTarget is not null)
            {
                if (++hops > 40)
                {
                    throw new IOException("too many symbolic links");
                }
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is null)
                {
                    throw new IOException("broken symbolic link");
                }
                current = RealPath(Path.GetFullPath(target.FullName));
            }
        }
        return current;
    }

    internal static string? ContentTypeFromExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        _ => null,
    };
}
