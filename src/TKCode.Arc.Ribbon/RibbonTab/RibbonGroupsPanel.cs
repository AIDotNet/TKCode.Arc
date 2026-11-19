using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out the groups of a <see cref="RibbonTab"/> side by side and decides how far each one shrinks. Every group knows its
/// width at each step (as declared, at most medium, at most small, collapsed); while the row is wider than the room, the
/// panel takes one more step on one group: the highest <see cref="RibbonGroup.ReductionPriority"/> first, then the group at
/// the largest size, then the rightmost. Steps that would not make a group narrower are skipped. The result depends only on
/// the widths, so it is the same however the window got to its size.
/// <para>
/// Groups are visual children only; their logical parent is the tab.
/// </para>
/// </summary>
internal sealed class RibbonGroupsPanel : Control
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<RibbonGroupsPanel, double>(nameof(Spacing));

    static RibbonGroupsPanel()
    {
        AffectsMeasure<RibbonGroupsPanel>(SpacingProperty);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private readonly List<RibbonGroup> _groups = new();
    private readonly List<RibbonGroup> _visible = new();
    private int[] _levels = [];
    private double[,] _widths = new double[0, 0];

    public IReadOnlyList<RibbonGroup> Groups => _groups;

    public void Insert(int index, RibbonGroup group)
    {
        _groups.Insert(index, group);
        VisualChildren.Insert(index, group);
        InvalidateMeasure();
    }

    public void Remove(RibbonGroup group)
    {
        if (_groups.Remove(group))
        {
            VisualChildren.Remove(group);
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _visible.Clear();
        foreach (var group in _groups)
        {
            if (group.IsVisible)
            {
                _visible.Add(group);
            }
        }
        var count = _visible.Count;
        if (_levels.Length != count)
        {
            _levels = new int[count];
            _widths = new double[count, RibbonGroup.CollapsedLevel + 1];
        }
        Array.Clear(_levels);
        var spacing = Spacing;
        var total = count > 1 ? spacing * (count - 1) : 0;
        for (var i = 0; i < count; i++)
        {
            for (var level = 0; level <= RibbonGroup.CollapsedLevel; level++)
            {
                _widths[i, level] = double.NaN;
            }
            total += WidthAt(i, 0);
        }

        while (total > availableSize.Width + 0.5)
        {
            var pick = -1;
            var pickNext = 0;
            for (var i = 0; i < count; i++)
            {
                var next = NextUsefulLevel(i);
                if (next < 0)
                {
                    continue;
                }
                if (pick < 0 || Precedes(i, pick))
                {
                    pick = i;
                    pickNext = next;
                }
            }
            if (pick < 0)
            {
                break;
            }
            total += WidthAt(pick, pickNext) - WidthAt(pick, _levels[pick]);
            _levels[pick] = pickNext;
        }

        var width = 0.0;
        var height = 0.0;
        for (var i = 0; i < count; i++)
        {
            var group = _visible[i];
            group.SetLevel(_levels[i]);
            group.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += group.DesiredSize.Width;
            height = Math.Max(height, group.DesiredSize.Height);
        }
        foreach (var group in _groups)
        {
            if (!group.IsVisible)
            {
                group.Measure(default);
            }
        }
        if (count > 1)
        {
            width += spacing * (count - 1);
        }
        return new Size(width, height);
    }

    // Which group shrinks next: higher priority first, then the one at the shallower step (so equal groups take turns), then
    // the rightmost.
    private bool Precedes(int candidate, int current)
    {
        var a = _visible[candidate];
        var b = _visible[current];
        if (a.ReductionPriority != b.ReductionPriority)
        {
            return a.ReductionPriority > b.ReductionPriority;
        }
        if (_levels[candidate] != _levels[current])
        {
            return _levels[candidate] < _levels[current];
        }
        return candidate > current;
    }

    // The next step that makes the group narrower, or -1.
    private int NextUsefulLevel(int index)
    {
        var current = WidthAt(index, _levels[index]);
        var max = _visible[index].MaxLevel;
        for (var level = _levels[index] + 1; level <= max; level++)
        {
            if (WidthAt(index, level) < current - 0.5)
            {
                return level;
            }
        }
        return -1;
    }

    private double WidthAt(int index, int level)
    {
        var width = _widths[index, level];
        if (double.IsNaN(width))
        {
            width = _visible[index].GetWidth(level);
            _widths[index, level] = width;
        }
        return width;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var spacing = Spacing;
        foreach (var group in _groups)
        {
            if (!group.IsVisible)
            {
                continue;
            }
            group.Arrange(new Rect(x, 0, group.DesiredSize.Width, finalSize.Height));
            x += group.DesiredSize.Width + spacing;
        }
        return finalSize;
    }
}
