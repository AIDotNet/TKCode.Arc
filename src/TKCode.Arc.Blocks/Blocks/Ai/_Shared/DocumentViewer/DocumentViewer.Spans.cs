using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <remarks>
/// Spans and the scan. The page's spans are forwarded (<see cref="SetSpan"/>, <see cref="ClearSpans"/>) and measured through
/// the page's transform (<see cref="TryGetSpanBounds"/>). <see cref="ScrollToSpan"/> scrolls only the canvas, never the
/// host: an eased offset (<see cref="MotionValue{T}"/> of the canvas offset, 220 ms on the enter curve) that gives way to any
/// other scroll; code-driven because the target is measured from the span and the viewport when the call comes.
/// </remarks>
internal sealed partial class DocumentViewer
{
    /// <summary>How long a scroll to a span takes (Arc: 200–240 ms, a strong ease-out).</summary>
    internal static readonly TimeSpan ScrollDuration = TimeSpan.FromMilliseconds(220);

    private static readonly MotionSpec s_scrollSpec = MotionSpec.Tween(ScrollDuration, ArcEasings.Enter);

    private MotionValue<Vector>? _scrollMotion;
    private ScanBeam? _scan;

    /// <summary>Whether an eased scroll to a span is running (tests).</summary>
    internal bool IsScrollingToSpan => _scrollMotion is { IsAnimating: true };

    /// <summary>The scan beam while it sweeps (tests).</summary>
    internal ScanBeam? Scan => _scan;

    #region Spans

    /// <summary>Paints span <paramref name="id"/> of the page, or clears it with null (see <see cref="DocumentPage.SetSpan"/>).</summary>
    public void SetSpan(string id, DocumentSpanPaint? paint) => _page?.SetSpan(id, paint);

    /// <summary>Clears every span's paint.</summary>
    public void ClearSpans() => _page?.ClearSpans();

    /// <summary>
    /// The bounds of span <paramref name="id"/> in <paramref name="relativeTo"/>'s coordinates: the union of the rectangles of
    /// its lines (with the padding its tint is painted with), through the zoom and the scroll. <paramref name="isVisibleInCanvas"/>
    /// tells whether they cross the canvas viewport. False for an unknown span or one not laid out yet.
    /// </summary>
    public bool TryGetSpanBounds(string id, Visual relativeTo, out Rect bounds, out bool isVisibleInCanvas)
    {
        bounds = default;
        isVisibleInCanvas = false;
        if (_page is null || !_page.TryGetSpanRects(id, out var rects) || _page.TransformToVisual(relativeTo) is not { } toTarget)
        {
            return false;
        }
        var union = rects[0];
        for (var i = 1; i < rects.Count; i++)
        {
            union = union.Union(rects[i]);
        }
        bounds = union.TransformToAABB(toTarget);
        if (_canvas is not null && _page.TransformToVisual(_canvas) is { } toCanvas)
        {
            isVisibleInCanvas = union.TransformToAABB(toCanvas).Intersects(new Rect(_canvas.Viewport));
        }
        return true;
    }

    /// <summary>
    /// Scrolls the canvas so span <paramref name="id"/> is in view: nothing when it is inside the viewport already, else the
    /// shortest scroll that leaves it about <see cref="ScrollMargin"/> from the nearest edge (a span larger than the room shows
    /// its start). Eased over 220 ms at full motion (any other scroll of the canvas stops it), a jump otherwise.
    /// </summary>
    public void ScrollToSpan(string id, bool animate = true)
    {
        if (_canvas is null || !TryGetSpanBounds(id, _canvas, out var rect, out _))
        {
            return;
        }
        var viewport = _canvas.Viewport;
        var dx = NearestDelta(rect.Left, rect.Right, viewport.Width);
        var dy = NearestDelta(rect.Top, rect.Bottom, viewport.Height);
        if (dx == 0 && dy == 0)
        {
            return;
        }
        var max = MaxOffset();
        var from = _canvas.Offset;
        var target = new Vector(Math.Clamp(from.X + dx, 0, max.X), Math.Clamp(from.Y + dy, 0, max.Y));
        if (target == from)
        {
            return;
        }
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            StopScrollMotion();
            SetCanvasOffset(target);
            return;
        }
        if (_scrollMotion is null)
        {
            _scrollMotion = new MotionValue<Vector>(this, from, MotionVectors.Vector);
            _scrollMotion.Changed += SetCanvasOffset;
        }
        _scrollMotion.Jump(from);
        _ = _scrollMotion.AnimateTo(target, s_scrollSpec);
    }

    /// <summary>
    /// How far to scroll one axis so the range [<paramref name="start"/>, <paramref name="end"/>] (viewport coordinates) is in
    /// a viewport <paramref name="size"/> long: 0 when it is inside, else the "nearest" scroll with the margin (smaller when
    /// the range barely fits).
    /// </summary>
    internal static double NearestDelta(double start, double end, double size)
    {
        if (start >= 0 && end <= size)
        {
            return 0;
        }
        var length = end - start;
        var margin = Math.Min(ScrollMargin, Math.Max(0, (size - length) / 2));
        if (start < 0 || length > size - 2 * margin)
        {
            return start - margin;
        }
        return end - size + margin;
    }

    /// <summary>Stops an eased scroll to a span where it is.</summary>
    private void StopScrollMotion()
    {
        if (_scrollMotion is { IsAnimating: true } motion)
        {
            motion.Stop();
        }
    }

    #endregion

    #region Scan

    /// <summary>
    /// Sweeps the scan beam over the page once (a running sweep starts over). Only at full motion: under reduced motion or
    /// with motion off no beam is created (spec 5.3).
    /// </summary>
    public void StartScan()
    {
        if (_stage is null || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            StopScan();
            return;
        }
        if (_scan is null)
        {
            _scan = new ScanBeam();
            _scan.Finished += OnScanFinished;
            _stage.Children.Add(_scan);
        }
        _scan.Play();
    }

    /// <summary>Removes the scan beam at once.</summary>
    public void StopScan()
    {
        if (_scan is not { } scan)
        {
            return;
        }
        _scan = null;
        scan.Finished -= OnScanFinished;
        scan.Stop();
        _stage?.Children.Remove(scan);
    }

    private void OnScanFinished(object? sender, EventArgs e) => StopScan();

    #endregion
}
