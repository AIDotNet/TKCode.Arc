using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The lifted row of a <see cref="ReorderableList"/> drag, drawn in the window's overlay layer so no scroll viewport
/// (or other clipping ancestor) cuts it or its shadow (#193). The real row stays in the list as the drag's model: it
/// keeps its springs, pointer capture and focus, and only turns transparent while the preview shows it. The preview
/// paints the row's template root through a <see cref="VisualBrush"/> (the row's own pixels, whatever its template,
/// item template, theme scope or flow direction), follows the row's full transform into the layer every time the row
/// moves or the layout changes, and copies the row's lift shadow, which the brush would clip at the row's bounds. When
/// the drag ends the row is shown again exactly where the preview was and settles by itself.
/// </summary>
internal sealed class ReorderableListDragPreview : Panel, IMotionDriver
{
    /// <summary>Frames the content keeps refreshing after the last change (handle tints, the rolling position, the lift fade).</summary>
    private const int QuietFrames = 45;

    private readonly ReorderableListItem _row;
    private readonly Panel _layer;
    private readonly Border _shadow = new()
    {
        IsHitTestVisible = false,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };
    private readonly Border _image = new() { IsHitTestVisible = false };
    private readonly VisualBrush _brush = new()
    {
        Stretch = Stretch.None,
        AlignmentX = AlignmentX.Left,
        AlignmentY = AlignmentY.Top,
        TileMode = TileMode.None
    };
    private readonly MatrixTransform _transform = new();
    private Visual? _target;
    private int _framesLeft;
    private bool _registered;
    private bool _closed;

    private ReorderableListDragPreview(ReorderableListItem row, Panel layer)
    {
        _row = row;
        _layer = layer;
        IsHitTestVisible = false;
        Focusable = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        // The matrix from the row into the layer already carries every flow-direction mirror between them; a mirror of the
        // preview's own (an RTL preview on the overlay layer, which bypasses flow direction) would flip the row again.
        FlowDirection = FlowDirection.LeftToRight;
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        RenderTransform = _transform;
        _image.Background = _brush;
        Children.Add(_shadow);
        Children.Add(_image);
    }

    /// <summary>The layer the preview is drawn on.</summary>
    internal Panel Layer => _layer;

    /// <summary>The row the preview shows.</summary>
    internal ReorderableListItem Row => _row;

    /// <summary>
    /// Lifts <paramref name="row"/> out of its list into the nearest layer above it, or returns null (no layer, or the
    /// row has no template yet): the row then keeps drawing itself in place, as before.
    /// </summary>
    public static ReorderableListDragPreview? Show(ReorderableList list, ReorderableListItem row)
    {
        if (FindLayer(list) is not { } layer || FirstChild(row) is null)
        {
            return null;
        }
        var preview = new ReorderableListDragPreview(row, layer);
        layer.Children.Add(preview);
        row.Opacity = 0;
        row.Preview = preview;
        preview.Sync();
        preview.Touch();
        return preview;
    }

    /// <summary>
    /// The layer above the list: the popup layer the list's popup sits on (the window's overlay layer is below popups), else
    /// the window's overlay layer, else its adorner layer (a native popup window has no overlay layer).
    /// </summary>
    private static Panel? FindLayer(ReorderableList list)
    {
        if (list.FindAncestorOfType<OverlayPopupHost>() is { } host && host.GetVisualParent() is Panel popupLayer)
        {
            return popupLayer;
        }
        return (Panel?)OverlayLayer.GetOverlayLayer(list) ?? AdornerLayer.GetAdornerLayer(list);
    }

    private static Visual? FirstChild(Visual visual)
    {
        foreach (var child in visual.GetVisualChildren())
        {
            return child;
        }
        return null;
    }

    /// <summary>Removes the preview and shows the row again where the preview was.</summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }
        _closed = true;
        _framesLeft = 0;
        if (ReferenceEquals(_row.Preview, this))
        {
            _row.Preview = null;
        }
        _row.ClearValue(OpacityProperty);
        _brush.Visual = null;
        _target = null;
        _layer.Children.Remove(this);
    }

    /// <summary>Keeps the content refreshing for a while (something in the row changed or is about to).</summary>
    public void Touch()
    {
        if (_closed)
        {
            return;
        }
        _framesLeft = QuietFrames;
        if (!_registered)
        {
            _registered = MotionScheduler.Register(this, _row);
        }
    }

    /// <summary>Places the preview over the row (full transform from the row's template root into the layer) and copies the lift shadow.</summary>
    public void Sync()
    {
        if (_closed)
        {
            return;
        }
        var target = FirstChild(_row);
        if (!ReferenceEquals(target, _target))
        {
            _target = target;
            _brush.Visual = target;
        }
        if (target is null || ToLayer(target) is not { } matrix)
        {
            return;
        }
        var size = target.Bounds.Size;
        Width = size.Width;
        Height = size.Height;
        _transform.Matrix = matrix;
        if (_row.LiftLayer is { } lift)
        {
            var bounds = ReferenceEquals(lift.GetVisualParent(), target) ? lift.Bounds : new Rect(size);
            _shadow.Margin = new Thickness(bounds.X, bounds.Y, 0, 0);
            _shadow.Width = bounds.Width;
            _shadow.Height = bounds.Height;
            _shadow.CornerRadius = lift.CornerRadius;
            _shadow.BoxShadow = lift.BoxShadow;
            _shadow.Opacity = lift.Opacity;
        }
        else
        {
            _shadow.Opacity = 0;
        }
    }

    /// <summary>
    /// The transform from <paramref name="target"/> into the layer, through the root: <c>TransformToVisual</c>
    /// between the two directly goes through their nearest common ancestor and misplaces the row when flow-direction mirrors
    /// sit between them (the overlay layer undoes its RTL parent's mirror). Both paths to the root carry every mirror.
    /// </summary>
    private Matrix? ToLayer(Visual target)
    {
        if (TopLevel.GetTopLevel(_layer) is not { } root || target.TransformToVisual(root) is not { } toRoot ||
            _layer.TransformToVisual(root) is not { } layerToRoot || !layerToRoot.TryInvert(out var rootToLayer))
        {
            return null;
        }
        return toRoot * rootToLayer;
    }

    /// <summary>Records the row again: a <see cref="VisualBrush"/> keeps what it drew until one of its properties changes.</summary>
    private void Refresh()
    {
        if (_target is not { } target)
        {
            return;
        }
        _brush.Visual = null;
        _brush.Visual = target;
        _image.InvalidateVisual();
    }

    Visual? IMotionDriver.Owner => _closed ? null : _row;

    bool IMotionDriver.Advance(double dt)
    {
        if (_closed)
        {
            _registered = false;
            return false;
        }
        Sync();
        Refresh();
        if (--_framesLeft > 0)
        {
            return true;
        }
        _registered = false;
        return false;
    }

    void IMotionDriver.Snap()
    {
        // The row left the tree or was hidden: the list ends the drag (and closes the preview); stop refreshing.
        _registered = false;
        _framesLeft = 0;
    }
}
