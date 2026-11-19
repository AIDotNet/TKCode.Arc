using Avalonia;

namespace StreamMark.Avalonia.Images;

/// <summary>加载结果种类。</summary>
public enum ImageLoadStatus
{
    /// <summary>已加载：<see cref="ImageLoadResult.Lease"/> 可用。</summary>
    Loaded,
    /// <summary>失败（网络、格式、超限等）；已进入负缓存，期满前再次请求直接返回失败。</summary>
    Failed,
    /// <summary>需要用户同意（<see cref="ImagePolicy.AutoLoad"/> = false）；同意后调用 <see cref="ImagePipeline.Approve"/> 再重新请求。</summary>
    NeedsConsent,
    /// <summary>被策略拒绝（协议、域名、本地目录），不会重试。</summary>
    Blocked,
}

/// <summary>
/// <see cref="ImagePipeline.LoadAsync"/> 的结果。加载成功时持有一个 <see cref="ImageLease"/>，
/// 调用方用完（视图卸载或换图）后必须释放（释放本结果或其 Lease 均可）。
/// </summary>
public sealed class ImageLoadResult : IDisposable
{
    private ImageLoadResult(ImageLoadStatus status, ImageLease? lease, string? reason, string? host)
    {
        Status = status;
        Lease = lease;
        Reason = reason;
        Host = host;
    }

    public ImageLoadStatus Status { get; }

    /// <summary>图片句柄（仅 <see cref="ImageLoadStatus.Loaded"/>）。</summary>
    public ImageLease? Lease { get; }

    /// <summary>原始像素尺寸（仅 Loaded）。</summary>
    public PixelSize PixelSize => Lease?.PixelSize ?? default;

    /// <summary>失败或拒绝原因（Failed / Blocked）。</summary>
    public string? Reason { get; }

    /// <summary>需要同意的域名（NeedsConsent），用于占位显示与 <see cref="ImagePipeline.Approve"/>。</summary>
    public string? Host { get; }

    public static ImageLoadResult Loaded(ImageLease lease) => new(ImageLoadStatus.Loaded, lease ?? throw new ArgumentNullException(nameof(lease)), null, null);

    public static ImageLoadResult Failed(string reason) => new(ImageLoadStatus.Failed, null, reason, null);

    public static ImageLoadResult NeedsConsent(string host) => new(ImageLoadStatus.NeedsConsent, null, null, host);

    public static ImageLoadResult Blocked(string reason) => new(ImageLoadStatus.Blocked, null, reason, null);

    public void Dispose() => Lease?.Dispose();

    public override string ToString() => Status switch
    {
        ImageLoadStatus.Loaded => $"Loaded {PixelSize}",
        ImageLoadStatus.NeedsConsent => $"NeedsConsent {Host}",
        _ => $"{Status} {Reason}",
    };
}
