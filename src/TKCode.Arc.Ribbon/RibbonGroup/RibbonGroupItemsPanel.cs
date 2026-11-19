using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out the commands of a <see cref="RibbonGroup"/> in columns. A large command, and a <see cref="RibbonSeparator"/>, take a
/// column of the full content height; medium and small commands, and any other control, stack in columns as many as fit
/// (three 24px rows by default), spread evenly over the height. The group's step (<see cref="Level"/>) caps the size of the
/// ribbon buttons: 0 shows each at its <see cref="RibbonButton.Size"/>, 1 at most Medium, 2 at most Small.
/// <para>
/// The commands are visual children only: their logical parent is the group, so the panel can move between the group and its
/// drop-down without touching bindings.
/// </para>
/// </summary>
internal sealed class RibbonGroupItemsPanel : Control
{
    public static readonly StyledProperty<double> ContentHeightProperty =
        AvaloniaProperty.Register<RibbonGroupItemsPanel, double>(nameof(ContentHeight), 78);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<RibbonGroupItemsPanel, double>(nameof(ColumnSpacing), 2);

    static RibbonGroupItemsPanel()
    {
        AffectsMeasure<RibbonGroupItemsPanel>(ContentHeightProperty, ColumnSpacingProperty);
    }

    public double ContentHeight
    {
        get => GetValue(ContentHeightProperty);
        set => SetValue(ContentHeightProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    private readonly List<Control> _items = new();
    private readonly List<Rect> _rects = new();
    private readonly List<int> _column = new();
    private int _level;

    public IReadOnlyList<Control> Items => _items;

    /// <summary>The group's step: 0 (as declared), 1 (at most Medium) or 2 (at most Small).</summary>
    public int Level
    {
        get => _level;
        set
        {
            value = Math.Clamp(value, 0, 2);
            if (_level != value)
            {
                _level = value;
                InvalidateMeasure();
            }
        }
    }

    public void Insert(int index, Control item)
    {
        _items.Insert(index, item);
        VisualChildren.Insert(index, item);
        InvalidateMeasure();
    }

    public void Remove(Control item)
    {
        if (_items.Remove(item))
        {
            VisualChildren.Remove(item);
            if (item is RibbonButton button)
            {
                button.SetSizeCap(null);
            }
            InvalidateMeasure();
        }
    }

    public void Clear()
    {
        foreach (var item in _items.ToArray())
        {
            Remove(item);
        }
    }

    internal static RibbonItemSize? CapOf(int level) => level switch
    {
        <= 0 => null,
        1 => RibbonItemSize.Medium,
        _ => RibbonItemSize.Small
    };

    /// <summary>The width the commands take at <paramref name="level"/>, from their natural sizes (nothing is changed).</summary>
    public double ComputeWidth(int level) => Plan(level, null);

    protected override Size MeasureOverride(Size availableSize)
    {
        var cap = CapOf(_level);
        foreach (var item in _items)
        {
            if (item is RibbonButton button)
            {
                button.SetSizeCap(cap);
            }
        }
        _rects.Clear();
        var width = Plan(_level, _rects);
        return new Size(width, ContentHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < _items.Count && i < _rects.Count; i++)
        {
            _items[i].Arrange(_rects[i]);
        }
        return finalSize;
    }

    /// <summary>
    /// Places the commands for <paramref name="level"/> and returns the width. With <paramref name="rects"/> the commands are
    /// measured for real (their size cap is already set) and their rectangles collected, one per item in order (hidden items
    /// get an empty one).
    /// </summary>
    private double Plan(int level, List<Rect>? rects)
    {
        var height = ContentHeight;
        var spacing = ColumnSpacing;
        var cap = CapOf(level);
        var x = 0.0;
        var columns = 0;
        var columnWidth = 0.0;
        var columnHeight = 0.0;
        _column.Clear();

        void Flush()
        {
            if (_column.Count == 0)
            {
                return;
            }
            if (rects is not null)
            {
                // Spread the column's items evenly over the content height.
                var gap = Math.Max(0, (height - columnHeight) / (_column.Count + 1));
                var y = gap;
                foreach (var index in _column)
                {
                    var size = _items[index].DesiredSize;
                    rects[index] = new Rect(x, y, size.Width, size.Height);
                    y += size.Height + gap;
                }
            }
            x += columnWidth + spacing;
            columns++;
            _column.Clear();
            columnWidth = 0;
            columnHeight = 0;
        }

        for (var i = 0; i < _items.Count; i++)
        {
            rects?.Add(default);
            var item = _items[i];
            if (!item.IsVisible)
            {
                continue;
            }
            Size size;
            var fullHeight = false;
            if (item is RibbonButton button)
            {
                var effective = button.SizeUnder(cap);
                if (rects is not null)
                {
                    button.Measure(Size.Infinity);
                    size = button.DesiredSize;
                }
                else
                {
                    size = button.GetNaturalSize(effective);
                }
                fullHeight = effective == RibbonItemSize.Large;
            }
            else
            {
                item.Measure(new Size(double.PositiveInfinity, height));
                size = item.DesiredSize;
                fullHeight = item is RibbonSeparator;
            }

            if (fullHeight)
            {
                Flush();
                if (rects is not null)
                {
                    rects[i] = new Rect(x, 0, size.Width, height);
                }
                x += size.Width + spacing;
                columns++;
                continue;
            }

            if (_column.Count > 0 && columnHeight + size.Height > height)
            {
                Flush();
            }
            _column.Add(i);
            columnHeight += size.Height;
            columnWidth = Math.Max(columnWidth, size.Width);
        }
        Flush();
        return columns == 0 ? 0 : x - spacing;
    }
}
