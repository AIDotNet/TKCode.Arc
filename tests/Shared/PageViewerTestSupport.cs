using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc.Tests.Media;

/// <summary>A bitmap that knows whether it was disposed.</summary>
internal sealed class TrackedBitmap : WriteableBitmap
{
    public TrackedBitmap(PixelSize size)
        : base(size, new Vector(96, 96), PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul)
    {
    }

    public bool IsDisposed { get; private set; }

    public override void Dispose()
    {
        IsDisposed = true;
        base.Dispose();
    }
}

/// <summary>
/// A counting <see cref="IPageSource"/>. By default requests finish at once with a small owned bitmap (its pixels stay
/// few however large the request, unless <see cref="MatchTargetSize"/>). With <see cref="Hold"/> they wait for
/// <see cref="Release"/>.
/// </summary>
internal sealed class FakePageSource : IPageSource
{
    private readonly List<Size> _sizes;
    private readonly List<(PageRenderRequest Request, TaskCompletionSource Gate)> _waiting = new();

    public FakePageSource(int count, Size pageSize)
    {
        _sizes = Enumerable.Repeat(pageSize, count).ToList();
    }

    /// <summary>Everything asked for, in order.</summary>
    public List<PageRenderRequest> Requests { get; } = new();

    /// <summary>Requests that were cancelled.</summary>
    public List<PageRenderRequest> Cancelled { get; } = new();

    /// <summary>Bitmaps handed out.</summary>
    public List<TrackedBitmap> Bitmaps { get; } = new();

    public bool Hold { get; set; }

    public bool MatchTargetSize { get; set; }

    public bool ReturnsNull { get; set; }

    public bool Throws { get; set; }

    public int WaitingCount => _waiting.Count;

    /// <summary>How many requests ran at the same time, at most, per page.</summary>
    public int MaxRunningPerPage { get; private set; }

    public int SubscriberCount => PagesChangedHandlers?.GetInvocationList().Length ?? 0;

    private EventHandler? PagesChangedHandlers;

    public event EventHandler? PagesChanged
    {
        add => PagesChangedHandlers += value;
        remove => PagesChangedHandlers -= value;
    }

    public int PageCount => _sizes.Count;

    public Size GetPageSize(int pageIndex) => _sizes[pageIndex];

    public void SetPages(int count, Size pageSize)
    {
        _sizes.Clear();
        _sizes.AddRange(Enumerable.Repeat(pageSize, count));
        PagesChangedHandlers?.Invoke(this, EventArgs.Empty);
    }

    public void SetPageSize(int index, Size size) => _sizes[index] = size;

    public async ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var running = _waiting.Count(w => w.Request.PageIndex == request.PageIndex) + 1;
        MaxRunningPerPage = Math.Max(MaxRunningPerPage, running);
        if (Hold)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var entry = (request, gate);
            _waiting.Add(entry);
            try
            {
                await using (cancellationToken.Register(() => gate.TrySetCanceled(cancellationToken)))
                {
                    await gate.Task;
                }
            }
            catch (OperationCanceledException)
            {
                Cancelled.Add(request);
                throw;
            }
            finally
            {
                _waiting.Remove(entry);
            }
        }
        if (Throws)
        {
            throw new InvalidOperationException("Cannot render.");
        }
        if (ReturnsNull)
        {
            return null;
        }
        var size = request.Region is { } region ? region.Size : request.TargetSize;
        var bitmap = new TrackedBitmap(MatchTargetSize ? size : new PixelSize(4, 4));
        Bitmaps.Add(bitmap);
        return new PageImage(bitmap);
    }

    /// <summary>Lets every held request finish.</summary>
    public void Release()
    {
        foreach (var (_, gate) in _waiting.ToArray())
        {
            gate.TrySetResult();
        }
    }
}
