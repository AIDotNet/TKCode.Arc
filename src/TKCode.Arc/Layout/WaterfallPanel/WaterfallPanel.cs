using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// A masonry (waterfall) panel: children flow into equal-width columns (or equal-height rows), each going to the end of the
/// lane that is currently shortest, so cards of different heights pack without row gaps. Usable on its own or as an
/// <see cref="ItemsControl.ItemsPanel"/>.
/// </summary>
/// <remarks>
/// <para>
/// With <see cref="Orientation"/> <c>Vertical</c> (the default) the available width is split into <see cref="Columns"/>
/// columns; every child is measured at the column width and its own height, clamped to
/// <see cref="MinItemSize"/>..<see cref="MaxItemSize"/>. <c>Horizontal</c> swaps the axes: <see cref="Rows"/> rows of equal
/// height, children keep their own widths. Ties between equally short lanes go to the first one, so the same children
/// always produce the same layout.
/// </para>
/// <para>
/// A lane count of 0 picks as many lanes as fit at <see cref="MinLaneSize"/> or wider. Collapsed children
/// (<c>IsVisible="False"</c>) take no place.
/// </para>
/// </remarks>
public class WaterfallPanel : Panel
{
    /// <summary>The direction the lanes run: <c>Vertical</c> stacks children in columns, <c>Horizontal</c> in rows.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<WaterfallPanel, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>Number of columns when <see cref="Orientation"/> is <c>Vertical</c>; 0 fits as many as <see cref="MinLaneSize"/> allows.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<WaterfallPanel, int>(nameof(Columns), 3, coerce: CoerceLaneCount);

    /// <summary>Number of rows when <see cref="Orientation"/> is <c>Horizontal</c>; 0 fits as many as <see cref="MinLaneSize"/> allows.</summary>
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<WaterfallPanel, int>(nameof(Rows), 3, coerce: CoerceLaneCount);

    /// <summary>The gap between lanes and between consecutive children of a lane.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(Spacing), 12, coerce: CoerceNonNegative);

    /// <summary>The smallest size a child gets along the flow (its height in columns, its width in rows).</summary>
    public static readonly StyledProperty<double> MinItemSizeProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(MinItemSize), 0, coerce: CoerceNonNegative);

    /// <summary>The largest size a child gets along the flow; children are also measured within it.</summary>
    public static readonly StyledProperty<double> MaxItemSizeProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(MaxItemSize), double.PositiveInfinity, coerce: CoerceNonNegative);

    /// <summary>The narrowest lane (column width or row height) when <see cref="Columns"/> or <see cref="Rows"/> is 0.</summary>
    public static readonly StyledProperty<double> MinLaneSizeProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(MinLaneSize), 200, coerce: CoerceNonNegative);

    /// <summary>
    /// Stretches the last child of every lane to the panel's end, so all lanes end together. The end is the longest lane, or the
    /// panel's arranged size when its parent gives it more; it never reaches past the panel.
    /// </summary>
    public static readonly StyledProperty<bool> FillLastItemProperty =
        AvaloniaProperty.Register<WaterfallPanel, bool>(nameof(FillLastItem));

    // Placement of each child from the last measure, indexed like Children: lane (-1 = collapsed), start and extent along the flow.
    private int[] _lane = [];
    private double[] _start = [];
    private double[] _extent = [];
    private int _laneCount = 1;
    private double _measuredLaneSize;
    private bool _crossWasFinite;

    static WaterfallPanel()
    {
        AffectsMeasure<WaterfallPanel>(OrientationProperty, ColumnsProperty, RowsProperty, SpacingProperty, MinItemSizeProperty,
            MaxItemSizeProperty, MinLaneSizeProperty);
        AffectsArrange<WaterfallPanel>(FillLastItemProperty);
    }

    /// <inheritdoc cref="OrientationProperty"/>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="RowsProperty"/>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc cref="MinItemSizeProperty"/>
    public double MinItemSize
    {
        get => GetValue(MinItemSizeProperty);
        set => SetValue(MinItemSizeProperty, value);
    }

    /// <inheritdoc cref="MaxItemSizeProperty"/>
    public double MaxItemSize
    {
        get => GetValue(MaxItemSizeProperty);
        set => SetValue(MaxItemSizeProperty, value);
    }

    /// <inheritdoc cref="MinLaneSizeProperty"/>
    public double MinLaneSize
    {
        get => GetValue(MinLaneSizeProperty);
        set => SetValue(MinLaneSizeProperty, value);
    }

    /// <inheritdoc cref="FillLastItemProperty"/>
    public bool FillLastItem
    {
        get => GetValue(FillLastItemProperty);
        set => SetValue(FillLastItemProperty, value);
    }

    /// <summary>The number of lanes of the last layout pass (columns or rows, after resolving a count of 0).</summary>
    public int LaneCount => _laneCount;

    private static int CoerceLaneCount(AvaloniaObject sender, int value) => Math.Max(0, value);

    private static double CoerceNonNegative(AvaloniaObject sender, double value) => double.IsNaN(value) ? 0 : Math.Max(0, value);

    private bool IsVertical => Orientation == Orientation.Vertical;

    /// <summary>Lane count for a cross-axis size: the requested count, or as many lanes of at least <see cref="MinLaneSize"/> as fit.</summary>
    private int ResolveLaneCount(double cross, double spacing)
    {
        var requested = IsVertical ? Columns : Rows;
        if (requested > 0)
        {
            return requested;
        }
        if (!double.IsFinite(cross) || MinLaneSize <= 0)
        {
            return 1;
        }
        return Math.Max(1, (int)Math.Floor((cross + spacing) / (MinLaneSize + spacing)));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var vertical = IsVertical;
        var spacing = Spacing;
        var cross = vertical ? availableSize.Width : availableSize.Height;
        var lanes = ResolveLaneCount(cross, spacing);
        var children = Children;
        var count = children.Count;
        EnsureCapacity(count);

        var maxItem = Math.Max(MinItemSize, MaxItemSize);
        var minItem = MinItemSize;
        _crossWasFinite = double.IsFinite(cross);
        double laneSize;
        if (_crossWasFinite)
        {
            laneSize = Math.Max(0, (cross - (lanes - 1) * spacing) / lanes);
        }
        else
        {
            // Unbounded across the lanes: every lane is as wide as the widest child.
            laneSize = 0;
            for (var i = 0; i < count; i++)
            {
                var child = children[i];
                if (!child.IsVisible)
                {
                    continue;
                }
                child.Measure(vertical ? new Size(double.PositiveInfinity, maxItem) : new Size(maxItem, double.PositiveInfinity));
                laneSize = Math.Max(laneSize, vertical ? child.DesiredSize.Width : child.DesiredSize.Height);
            }
        }

        Span<double> ends = lanes <= 64 ? stackalloc double[lanes] : new double[lanes];
        Span<bool> used = lanes <= 64 ? stackalloc bool[lanes] : new bool[lanes];
        ends.Clear();
        used.Clear();
        for (var i = 0; i < count; i++)
        {
            var child = children[i];
            if (!child.IsVisible)
            {
                _lane[i] = -1;
                continue;
            }
            child.Measure(vertical ? new Size(laneSize, maxItem) : new Size(maxItem, laneSize));
            var natural = vertical ? child.DesiredSize.Height : child.DesiredSize.Width;
            var extent = Math.Clamp(natural, minItem, maxItem);
            // A child's own minimum wins over MaxItemSize, so its content is never cut below what it asked for.
            extent = Math.Max(extent, vertical ? child.MinHeight : child.MinWidth);

            var lane = 0;
            for (var l = 1; l < lanes; l++)
            {
                if (ends[l] < ends[lane] - 1e-9)
                {
                    lane = l;
                }
            }
            var start = used[lane] ? ends[lane] + spacing : 0;
            _lane[i] = lane;
            _start[i] = start;
            _extent[i] = extent;
            ends[lane] = start + extent;
            used[lane] = true;
        }

        var flow = 0.0;
        for (var l = 0; l < lanes; l++)
        {
            flow = Math.Max(flow, ends[l]);
        }
        _laneCount = lanes;
        _measuredLaneSize = laneSize;
        var crossDesired = _crossWasFinite ? cross : lanes * laneSize + (lanes - 1) * spacing;
        return vertical ? new Size(crossDesired, flow) : new Size(flow, crossDesired);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var vertical = IsVertical;
        var spacing = Spacing;
        var lanes = _laneCount;
        var cross = vertical ? finalSize.Width : finalSize.Height;
        var flowEnd = vertical ? finalSize.Height : finalSize.Width;
        var laneSize = _crossWasFinite ? Math.Max(0, (cross - (lanes - 1) * spacing) / lanes) : _measuredLaneSize;
        var children = Children;
        var count = Math.Min(children.Count, _lane.Length);

        Span<int> last = lanes <= 64 ? stackalloc int[lanes] : new int[lanes];
        last.Fill(-1);
        if (FillLastItem)
        {
            for (var i = 0; i < count; i++)
            {
                if (_lane[i] >= 0 && _lane[i] < lanes)
                {
                    last[_lane[i]] = i;
                }
            }
        }

        for (var i = 0; i < count; i++)
        {
            var child = children[i];
            var lane = _lane[i];
            if (lane < 0 || lane >= lanes)
            {
                child.Arrange(default);
                continue;
            }
            var start = _start[i];
            var extent = _extent[i];
            if (FillLastItem && last[lane] == i)
            {
                extent = Math.Max(extent, flowEnd - start);
            }
            var laneStart = lane * (laneSize + spacing);
            child.Arrange(vertical
                ? new Rect(laneStart, start, laneSize, extent)
                : new Rect(start, laneStart, extent, laneSize));
        }
        for (var i = count; i < children.Count; i++)
        {
            children[i].Arrange(default);
        }
        return finalSize;
    }

    private void EnsureCapacity(int count)
    {
        if (_lane.Length != count)
        {
            _lane = new int[count];
            _start = new double[count];
            _extent = new double[count];
        }
    }
}
