namespace StreamMark.Avalonia.Images;

/// <summary>
/// 图片加载策略（宿主可配置，不可变）。默认：https 自动加载，http 禁止，不限制域名，
/// 不允许任何本地目录；单张 20MB、超时 30 秒、最多 5 次重定向。
/// </summary>
/// <remarks>
/// 域名列表中的条目不区分大小写：<c>example.com</c> 只匹配该域名本身，
/// <c>*.example.com</c> 或 <c>.example.com</c> 匹配其所有子域名（不含 example.com 本身）。
/// </remarks>
public sealed record ImagePolicy
{
    /// <summary>默认策略。</summary>
    public static ImagePolicy Default { get; } = new();

    /// <summary>远程图片总开关；为 false 时所有远程图片都需要用户同意（<see cref="ImagePipeline.Approve"/>）才加载。</summary>
    public bool AutoLoad { get; init; } = true;

    /// <summary>是否自动加载 https（及已允许的 http）图片；为 false 时与 <see cref="AutoLoad"/> = false 等效，但只作用于网络图片。</summary>
    public bool AutoLoadHttps { get; init; } = true;

    /// <summary>是否允许明文 http（含重定向到 http）。默认禁止。</summary>
    public bool AllowHttp { get; init; }

    /// <summary>允许的域名；null 表示不限制。</summary>
    public IReadOnlyCollection<string>? AllowedHosts { get; init; }

    /// <summary>禁止的域名；null 表示不禁止。优先于 <see cref="AllowedHosts"/>。</summary>
    public IReadOnlyCollection<string>? BlockedHosts { get; init; }

    /// <summary>允许读取 <c>file:</c> 图片的根目录（例如项目工作区）；为空时禁止所有本地文件。</summary>
    public IReadOnlyList<string> AllowedFileRoots { get; init; } = [];

    /// <summary>单张图片原始字节上限，默认 20MB。</summary>
    public long MaxBytes { get; init; } = 20L * 1024 * 1024;

    /// <summary>单次网络获取（含重定向）的总超时，默认 30 秒。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>最多跟随的重定向次数，默认 5。</summary>
    public int MaxRedirects { get; init; } = 5;

    /// <summary>请求使用的 User-Agent；null 或空表示不发送。</summary>
    public string? UserAgent { get; init; } = "StreamMark";

    /// <summary>
    /// 检查一个绝对地址（含重定向目标）的协议和域名是否被策略允许。
    /// 返回 null 表示允许，否则返回拒绝原因。不检查同意状态和本地目录。
    /// </summary>
    public string? CheckUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            return "relative url";
        }
        var scheme = uri.Scheme;
        if (string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) && !AllowHttp)
        {
            return "http not allowed";
        }
        if (uri.IsFile)
        {
            return null;
        }
        var host = NormalizeHost(uri);
        if (host.Length == 0)
        {
            return null;
        }
        if (BlockedHosts is { Count: > 0 } && MatchesAny(host, BlockedHosts))
        {
            return "host blocked: " + host;
        }
        if (AllowedHosts is not null && !MatchesAny(host, AllowedHosts))
        {
            return "host not allowed: " + host;
        }
        return null;
    }

    /// <summary>判断网络/远程地址在当前策略下是否需要用户同意。</summary>
    internal bool RequiresConsent(Uri? uri)
    {
        if (!AutoLoad)
        {
            return true;
        }
        if (!AutoLoadHttps && uri is not null
            && (string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return false;
    }

    internal static string NormalizeHost(Uri uri)
    {
        var host = uri.IdnHost;
        return string.IsNullOrEmpty(host) ? "" : host.ToLowerInvariant();
    }

    private static bool MatchesAny(string host, IReadOnlyCollection<string> patterns)
    {
        foreach (var raw in patterns)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }
            var pattern = raw.Trim();
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                pattern = pattern[1..];
            }
            if (pattern.StartsWith('.'))
            {
                if (host.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
