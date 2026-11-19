using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using StreamMark.Avalonia.Images;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 一个视图中一张图片的加载状态与缓存句柄。视图持有句柄期间缓存不会释放该图片；
/// 视图回收、地址或解码宽度变化时归还句柄（<see cref="Dispose"/>）。只在界面线程使用。
/// </summary>
internal sealed class ImageSlot : IDisposable
{
    private readonly Action _changed;
    private ImagePipeline? _pipeline;
    private ImageLease? _lease;
    private int _bucket = -1;
    private int _generation;

    public ImageSlot(Action changed)
    {
        _changed = changed;
    }

    public string Url { get; private set; } = "";

    public ImageLoadStatus? Status { get; private set; }

    public string? Host { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>图片的自然像素尺寸（加载前可能已从缓存元数据得知，用于按宽高比占位）。</summary>
    public PixelSize? PixelSize { get; private set; }

    public IImage? Image => _lease?.Image;

    internal ImagePipeline? Pipeline => _pipeline;

    /// <summary>当前图片是否按显示宽度缩小解码（小于原始尺寸）。</summary>
    internal bool IsDownscaled => _lease is { } lease && lease.DecodedSize.Width < lease.PixelSize.Width;

    /// <summary>为当前图片再取一个句柄（例如查看器打开期间保持缩小图可用）。</summary>
    internal ImageLease? AcquireLease() => _lease?.AddRef();

    public bool IsLoading => Status is null && Url.Length > 0;

    /// <summary>确保按给定解码宽度加载；已有可用结果时不做任何事。</summary>
    public void Ensure(ImagePipeline pipeline, string url, int decodeWidth)
    {
        var bucket = ImagePipeline.GetDecodeBucket(decodeWidth);
        if (ReferenceEquals(pipeline, _pipeline) && url == Url && (Status is not null && Status != ImageLoadStatus.Loaded || bucket <= _bucket && _lease is not null || IsLoading && bucket == _bucket))
        {
            return;
        }
        Release();
        _pipeline = pipeline;
        Url = url;
        _bucket = bucket;
        Status = null;
        Host = null;
        Reason = null;
        if (pipeline.TryGetCached(url, decodeWidth) is { } cached)
        {
            _lease = cached;
            Status = ImageLoadStatus.Loaded;
            PixelSize = cached.PixelSize;
            return;
        }
        PixelSize = pipeline.TryGetPixelSize(url, out var size) ? size : null;
        Load(decodeWidth);
    }

    private async void Load(int decodeWidth)
    {
        var generation = ++_generation;
        var pipeline = _pipeline!;
        var url = Url;
        ImageLoadResult result;
        try
        {
            result = await pipeline.LoadAsync(url, decodeWidth);
        }
        catch (Exception ex)
        {
            result = ImageLoadResult.Failed(ex.Message);
        }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (generation != _generation)
            {
                result.Dispose();
                return;
            }
            Status = result.Status;
            Host = result.Host;
            Reason = result.Reason;
            if (result.Lease is { } lease)
            {
                _lease?.Dispose();
                _lease = lease;
                PixelSize = lease.PixelSize;
            }
            _changed();
        });
    }

    /// <summary>失败后重试（清除负缓存）。</summary>
    public void Retry()
    {
        if (_pipeline is null)
        {
            return;
        }
        _pipeline.ForgetFailure(Url);
        Status = null;
        Load(_bucket);
        _changed();
    }

    /// <summary>需要确认时：允许该主机并重新加载。</summary>
    public void Approve()
    {
        if (_pipeline is null || Host is null)
        {
            return;
        }
        _pipeline.Approve(Host);
        Status = null;
        Load(_bucket);
        _changed();
    }

    private void Release()
    {
        _generation++;
        _lease?.Dispose();
        _lease = null;
    }

    public void Dispose()
    {
        Release();
        _pipeline = null;
        Url = "";
        Status = null;
    }
}
