using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <remarks>
/// <para>
/// Zoom: <see cref="Zoom"/> is the value the toolbar shows (it changes at once); the page is drawn at a second value that
/// springs toward it on <see cref="ArcSpring.Smooth"/> around the canvas center: the page point under the center when the
/// change starts stays under it while the scale moves. Every frame only the sizer's scale changes (its transform and the
/// size it reports); the page keeps its layout.
/// </para>
/// <para>
/// Code-driven motion (<see cref="MotionValue{T}"/>): the scroll offsets that hold the center depend on the viewport and on
/// the extent the new scale produces, so each frame must lay the sizer out and then write the offset; a ControlTheme
/// transition can animate the scale but not keep the canvas offsets in step with it.
/// </para>
/// <para>
/// Fit: while <see cref="IsFitWidth"/>, the zoom is (canvas width − 2 × padding − 3) / 540 within the steps' range, taken
/// from the width the viewer is laid out at (the canvas spans the viewer). A resize follows at once; turning Fit on springs.
/// </para>
/// </remarks>
internal sealed partial class DocumentViewer
{
    /// <summary>Steps closer than this (half a percent) to the zoom are not a step away.</summary>
    internal const double StepEpsilon = 0.005;

    private MotionValue<double>? _scale;
    private Point? _anchor;
    private Vector _anchorOffset;
    private bool _applyingScale;
    private bool _inLayout;
    private bool _followingWidth;
    private bool _jumpZoom;
    private bool _offsetAfterLayout;
    private double _canvasWidth = double.NaN;

    /// <summary>The zoom the page is drawn at now (it springs toward <see cref="Zoom"/>).</summary>
    internal double DrawnZoom => _sizer?.Scale ?? Zoom;

    /// <summary>Whether the drawn zoom is still springing.</summary>
    internal bool IsZooming => _scale is { IsAnimating: true };

    /// <summary>The smallest and largest step (0.4 and 2 without steps).</summary>
    internal (double Min, double Max) ZoomRange()
    {
        var steps = ZoomSteps;
        if (steps is null || steps.Count == 0)
        {
            return (0.4, 2);
        }
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        foreach (var step in steps)
        {
            min = Math.Min(min, step);
            max = Math.Max(max, step);
        }
        return (min, max);
    }

    private static double CoerceZoom(AvaloniaObject sender, double value)
    {
        var (min, max) = ((DocumentViewer)sender).ZoomRange();
        return double.IsFinite(value) ? Math.Clamp(value, min, max) : Math.Clamp(1, min, max);
    }

    /// <summary>The zoom that fits the page to a canvas <paramref name="width"/> wide: (width − 2 × padding − 3) / 540, within the steps.</summary>
    internal double FitZoom(double width)
    {
        var padding = _sizer?.Margin.Left ?? (IsStacked ? StackedCanvasPadding : CanvasPadding);
        var (min, max) = ZoomRange();
        return Math.Clamp((width - 2 * padding - FitSlack) / DocumentPage.BaseWidth, min, max);
    }

    /// <summary>The next step above (<paramref name="direction"/> &gt; 0) or below <paramref name="zoom"/>, or null at that end.</summary>
    internal double? StepFrom(double zoom, int direction)
    {
        double? best = null;
        foreach (var step in ZoomSteps)
        {
            if (direction > 0)
            {
                if (step > zoom + StepEpsilon && (best is null || step < best))
                {
                    best = step;
                }
            }
            else if (step < zoom - StepEpsilon && (best is null || step > best))
            {
                best = step;
            }
        }
        return best;
    }

    /// <summary>Zooms one step in (<paramref name="direction"/> &gt; 0) or out and turns Fit off; nothing at the end of the steps.</summary>
    public void StepZoom(int direction)
    {
        if (StepFrom(Zoom, direction) is not { } next)
        {
            return;
        }
        SetCurrentValue(IsFitWidthProperty, false);
        SetCurrentValue(ZoomProperty, next);
    }

    /// <summary>Shows the page at 100% and turns Fit off (the percentage button).</summary>
    public void ResetZoom()
    {
        SetCurrentValue(IsFitWidthProperty, false);
        SetCurrentValue(ZoomProperty, 1.0);
    }

    #region Following the width

    protected override Size MeasureOverride(Size availableSize)
    {
        _inLayout = true;
        try
        {
            if (double.IsFinite(availableSize.Width))
            {
                FollowWidth(availableSize.Width);
            }
            return base.MeasureOverride(availableSize);
        }
        finally
        {
            _inLayout = false;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _inLayout = true;
        try
        {
            FollowWidth(finalSize.Width);
            return base.ArrangeOverride(finalSize);
        }
        finally
        {
            _inLayout = false;
        }
    }

    /// <summary>The canvas is <paramref name="width"/> wide: a fitted page follows it at once (no spring while resizing).</summary>
    private void FollowWidth(double width)
    {
        if (Math.Abs(width - _canvasWidth) < 0.01)
        {
            return;
        }
        _canvasWidth = width;
        if (IsFitWidth)
        {
            SetZoomCore(FitZoom(width), jump: true);
        }
    }

    /// <summary>Fits again at once (the padding or the steps changed).</summary>
    private void RefitAtOnce()
    {
        if (IsFitWidth && double.IsFinite(_canvasWidth))
        {
            SetZoomCore(FitZoom(_canvasWidth), jump: true);
        }
    }

    private void SetZoomCore(double zoom, bool jump)
    {
        _followingWidth = true;
        _jumpZoom = jump;
        try
        {
            SetCurrentValue(ZoomProperty, zoom);
        }
        finally
        {
            _followingWidth = false;
            _jumpZoom = false;
        }
    }

    private void OnFitWidthChanged(bool fit)
    {
        if (_fit is not null)
        {
            _fit.IsOn = fit;
        }
        if (fit && double.IsFinite(_canvasWidth))
        {
            // A person turned Fit on: the page springs to the width.
            SetZoomCore(FitZoom(_canvasWidth), jump: false);
        }
    }

    #endregion

    #region Drawing the zoom

    private void OnZoomChanged(double zoom)
    {
        UpdateZoomFace();
        if (!_followingWidth && IsFitWidth && double.IsFinite(_canvasWidth) && Math.Abs(zoom - FitZoom(_canvasWidth)) > 1e-6)
        {
            // Zooming by hand (or a zoom set by the owner) leaves fit-to-width.
            SetCurrentValue(IsFitWidthProperty, false);
        }
        DrawZoom(zoom, animate: !_jumpZoom);
    }

    /// <summary>Moves the drawn zoom to <paramref name="zoom"/>: a Smooth spring at full motion, a jump otherwise (and before the first frame).</summary>
    private void DrawZoom(double zoom, bool animate)
    {
        StopScrollMotion();
        if (_sizer is null)
        {
            return;
        }
        if (_scale is null)
        {
            _scale = new MotionValue<double>(this, _sizer.Scale, MotionVectors.Double);
            _scale.Changed += ApplyScale;
        }
        CaptureAnchor();
        if (animate && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _scale.AnimateTo(zoom, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            // Reduced and Off: one step.
            _scale.Jump(zoom);
        }
    }

    /// <summary>Draws the page at <paramref name="scale"/> and scrolls so the anchored page point stays at the canvas center.</summary>
    private void ApplyScale(double scale)
    {
        if (_sizer is null || _canvas is null)
        {
            return;
        }
        if (_anchor is null || _canvas.Offset != _anchorOffset)
        {
            // Someone scrolled since the anchor was taken: hold the point that is at the center now.
            CaptureAnchor();
        }
        _applyingScale = true;
        try
        {
            _sizer.Scale = scale;
            if (_inLayout)
            {
                // The new extent is known only after this layout pass.
                QueueOffsetAfterLayout();
            }
            else
            {
                // The extent must follow the new size before the offset is written, or the offset is clamped to the old one.
                _canvas.UpdateLayout();
                WriteAnchoredOffset();
            }
        }
        finally
        {
            _applyingScale = false;
        }
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Remembers the page point (in page units) under the canvas center.</summary>
    private void CaptureAnchor()
    {
        if (_sizer is null || _canvas is null || _sizer.Scale <= 0)
        {
            return;
        }
        var viewport = _canvas.Viewport;
        var origin = _sizer.TranslatePoint(default, _canvas) ?? default;
        var scale = _sizer.Scale;
        _anchor = new Point((viewport.Width / 2 - origin.X) / scale, (viewport.Height / 2 - origin.Y) / scale);
        _anchorOffset = _canvas.Offset;
    }

    private void WriteAnchoredOffset()
    {
        if (_anchor is not { } anchor || _sizer is null || _canvas is null)
        {
            return;
        }
        var viewport = _canvas.Viewport;
        // The sizer's place in the content (its place in the canvas without the scroll).
        var origin = (_sizer.TranslatePoint(default, _canvas) ?? default) + _canvas.Offset;
        var scale = _sizer.Scale;
        var max = MaxOffset();
        var x = Math.Clamp(origin.X + anchor.X * scale - viewport.Width / 2, 0, max.X);
        var y = Math.Clamp(origin.Y + anchor.Y * scale - viewport.Height / 2, 0, max.Y);
        SetCanvasOffset(new Vector(x, y));
        _anchorOffset = _canvas.Offset;
    }

    private void QueueOffsetAfterLayout()
    {
        if (_offsetAfterLayout || _canvas is null || _canvas.Offset == default)
        {
            return;
        }
        _offsetAfterLayout = true;
        _canvas.LayoutUpdated += OnLayoutUpdatedOnce;
    }

    private void OnLayoutUpdatedOnce(object? sender, EventArgs e)
    {
        if (_canvas is not null)
        {
            _canvas.LayoutUpdated -= OnLayoutUpdatedOnce;
        }
        _offsetAfterLayout = false;
        _applyingScale = true;
        try
        {
            WriteAnchoredOffset();
        }
        finally
        {
            _applyingScale = false;
        }
    }

    #endregion
}
