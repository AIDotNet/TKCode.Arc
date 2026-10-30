using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One sheet of <see cref="PageCurl"/>: a page's content on paper at the page size. The surface moves it with a render
/// transform and a polygon clip, so turning a page never lays anything out. A page is hosted by at most one sheet at a
/// time, so the surface keeps up to four (the pages of the spread and the pages the turning sheet shows and uncovers).
/// While a page turns its content draws from a bitmap cache, so a frame moves pictures instead of laying text out again
/// at a new angle; the cache sits inside the sheet, under its clip and transform, which change every frame.
/// </summary>
internal sealed class PageCurlPage : Border
{
    private readonly MatrixTransform _transform = new();
    private readonly Decorator _host = new();

    public PageCurlPage(int index)
    {
        Index = index;
        IsHitTestVisible = false;
        IsVisible = false;
        ClipToBounds = true;
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.TopLeft;
        Child = _host;
    }

    /// <summary>The slot in the surface's pool.</summary>
    public int Index { get; }

    /// <summary>The one-based page this sheet shows, or 0 when it is free.</summary>
    public int PageNumber { get; private set; }

    /// <summary>The page's content (a control as given, or a presenter built from the template).</summary>
    public Control? Content => _host.Child;

    /// <summary>Draws the content from <paramref name="cache"/> (while turning), or as vectors when null.</summary>
    public void SetCache(CacheMode? cache)
    {
        if (!ReferenceEquals(_host.CacheMode, cache))
        {
            _host.CacheMode = cache;
        }
    }

    public Matrix Placement
    {
        get => _transform.Matrix;
        set
        {
            if (_transform.Matrix != value)
            {
                _transform.Matrix = value;
            }
        }
    }

    /// <summary>Shows <paramref name="item"/> as page <paramref name="pageNumber"/>: a control is used as it is, anything else is built with the template.</summary>
    public void Show(int pageNumber, object? item, IDataTemplate? template)
    {
        Release();
        PageNumber = pageNumber;
        if (item is Control control)
        {
            _host.Child = control;
        }
        else if (item is not null)
        {
            _host.Child = new ContentPresenter { Content = item, ContentTemplate = template };
        }
    }

    /// <summary>Takes the content off and frees the sheet.</summary>
    public void Release()
    {
        PageNumber = 0;
        _host.Child = null;
        SetCache(null);
        Clip = null;
        IsVisible = false;
    }
}
