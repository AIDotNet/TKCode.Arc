using Avalonia;
using Avalonia.Media.Imaging;

namespace TKCode.Arc;

/// <summary>
/// A set of pages a <see cref="PageViewer"/> shows and renders on demand. Sizes are in page DIPs: at
/// <see cref="PageViewer.Zoom"/> 1 a page shows at exactly <see cref="GetPageSize"/>.
/// </summary>
public interface IPageSource
{
    /// <summary>How many pages there are.</summary>
    int PageCount { get; }

    /// <summary>The size of a page in page DIPs.</summary>
    Size GetPageSize(int pageIndex);

    /// <summary>
    /// Renders a page or a part of it. Called on the UI thread; the work may finish on any thread. Return null when the
    /// page has no content for now (the viewer keeps its placeholder), and throw <see cref="OperationCanceledException"/>
    /// when <paramref name="cancellationToken"/> fires.
    /// </summary>
    ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken);

    /// <summary>The page count or a page size changed. Raised on the UI thread; the viewer drops its bitmaps and asks again.</summary>
    event EventHandler? PagesChanged;
}

/// <summary>What <see cref="IPageSource.RenderAsync"/> is asked to draw.</summary>
/// <param name="PageIndex">The page, from 0.</param>
/// <param name="TargetSize">How many pixels the whole page maps to.</param>
/// <param name="Region">
/// Null draws the whole page into a bitmap of <paramref name="TargetSize"/>. Otherwise only this rectangle, in the
/// coordinates of <paramref name="TargetSize"/>, into a bitmap of the rectangle's size (the detail layer at high zoom).
/// </param>
public readonly record struct PageRenderRequest(int PageIndex, PixelSize TargetSize, PixelRect? Region);

/// <summary>A rendered page, as returned by <see cref="IPageSource.RenderAsync"/>.</summary>
public sealed class PageImage
{
    /// <summary>Wraps a bitmap.</summary>
    /// <param name="bitmap">The rendered pixels.</param>
    /// <param name="ownsBitmap">True hands the bitmap to the viewer, which disposes it after evicting it.</param>
    public PageImage(Bitmap bitmap, bool ownsBitmap = true)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Bitmap = bitmap;
        OwnsBitmap = ownsBitmap;
    }

    /// <summary>The rendered pixels.</summary>
    public Bitmap Bitmap { get; }

    /// <summary>True when the viewer disposes <see cref="Bitmap"/> after evicting it.</summary>
    public bool OwnsBitmap { get; }
}
