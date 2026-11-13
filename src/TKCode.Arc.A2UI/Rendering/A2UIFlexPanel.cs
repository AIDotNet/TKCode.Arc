using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.A2UI;

/// <summary>Main-axis distribution of a Row or Column.</summary>
internal enum A2UIJustify
{
    Start,
    Center,
    End,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
    Stretch
}

/// <summary>Cross-axis alignment of a Row or Column.</summary>
internal enum A2UIAlign
{
    Start,
    Center,
    End,
    Stretch
}

/// <summary>
/// The layout of A2UI Row, Column and List: children in a line with a gap, CSS-like <c>justify</c> on the main axis,
/// <c>align</c> on the cross axis, and <c>weight</c> (flex grow) on children.
/// </summary>
internal sealed class A2UIFlexPanel : Panel
{
    public static readonly AttachedProperty<double> WeightProperty =
        AvaloniaProperty.RegisterAttached<A2UIFlexPanel, Control, double>("Weight");

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<A2UIFlexPanel, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<A2UIFlexPanel, double>(nameof(Gap), 8);

    private A2UIJustify _justify;
    private A2UIAlign _align = A2UIAlign.Stretch;

    static A2UIFlexPanel()
    {
        AffectsMeasure<A2UIFlexPanel>(OrientationProperty, GapProperty);
        AffectsParentMeasure<A2UIFlexPanel>(WeightProperty);
    }

    public static double GetWeight(Control control) => control.GetValue(WeightProperty);

    public static void SetWeight(Control control, double value) => control.SetValue(WeightProperty, value);

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public A2UIJustify Justify
    {
        get => _justify;
        set
        {
            if (_justify != value)
            {
                _justify = value;
                InvalidateArrange();
            }
        }
    }

    public A2UIAlign Align
    {
        get => _align;
        set
        {
            if (_align != value)
            {
                _align = value;
                InvalidateMeasure();
            }
        }
    }

    private bool Horizontal => Orientation == Orientation.Horizontal;

    private static double Main(Size s, bool horizontal) => horizontal ? s.Width : s.Height;

    private static double Cross(Size s, bool horizontal) => horizontal ? s.Height : s.Width;

    private static Size Make(double main, double cross, bool horizontal) => horizontal ? new Size(main, cross) : new Size(cross, main);

    protected override Size MeasureOverride(Size availableSize)
    {
        var horizontal = Horizontal;
        var availableMain = Main(availableSize, horizontal);
        var availableCross = Cross(availableSize, horizontal);
        var visible = Children.Where(c => c.IsVisible).ToList();
        var gaps = Math.Max(0, visible.Count - 1) * Gap;
        double used = gaps, cross = 0, totalWeight = 0;

        foreach (var child in visible)
        {
            var weight = GetWeight(child);
            if (weight > 0 && !double.IsInfinity(availableMain))
            {
                totalWeight += weight;
                continue;
            }

            child.Measure(Make(double.PositiveInfinity, availableCross, horizontal));
            used += Main(child.DesiredSize, horizontal);
            cross = Math.Max(cross, Cross(child.DesiredSize, horizontal));
        }

        if (totalWeight > 0)
        {
            var remaining = Math.Max(0, availableMain - used);
            foreach (var child in visible.Where(c => GetWeight(c) > 0))
            {
                var share = remaining * GetWeight(child) / totalWeight;
                child.Measure(Make(share, availableCross, horizontal));
                used += share;
                cross = Math.Max(cross, Cross(child.DesiredSize, horizontal));
            }
        }

        return Make(double.IsInfinity(availableMain) ? used : Math.Min(used, availableMain), cross, horizontal);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var horizontal = Horizontal;
        var finalMain = Main(finalSize, horizontal);
        var finalCross = Cross(finalSize, horizontal);
        var visible = Children.Where(c => c.IsVisible).ToList();
        if (visible.Count == 0)
        {
            return finalSize;
        }

        var sizes = new double[visible.Count];
        double fixedTotal = 0, totalWeight = 0;
        for (var i = 0; i < visible.Count; i++)
        {
            var weight = GetWeight(visible[i]);
            if (weight > 0)
            {
                totalWeight += weight;
            }
            else
            {
                sizes[i] = Main(visible[i].DesiredSize, horizontal);
                fixedTotal += sizes[i];
            }
        }

        var gap = Gap;
        var free = Math.Max(0, finalMain - fixedTotal - gap * (visible.Count - 1));
        if (totalWeight > 0)
        {
            for (var i = 0; i < visible.Count; i++)
            {
                var weight = GetWeight(visible[i]);
                if (weight > 0)
                {
                    sizes[i] = free * weight / totalWeight;
                }
            }

            free = 0;
        }
        else if (Justify == A2UIJustify.Stretch)
        {
            var extra = free / visible.Count;
            for (var i = 0; i < sizes.Length; i++)
            {
                sizes[i] += extra;
            }

            free = 0;
        }

        double offset = 0, between = gap;
        switch (Justify)
        {
            case A2UIJustify.Center:
                offset = free / 2;
                break;
            case A2UIJustify.End:
                offset = free;
                break;
            case A2UIJustify.SpaceBetween when visible.Count > 1:
                between = gap + free / (visible.Count - 1);
                break;
            case A2UIJustify.SpaceAround:
                offset = free / visible.Count / 2;
                between = gap + free / visible.Count;
                break;
            case A2UIJustify.SpaceEvenly:
                offset = free / (visible.Count + 1);
                between = gap + free / (visible.Count + 1);
                break;
        }

        for (var i = 0; i < visible.Count; i++)
        {
            var child = visible[i];
            var childCross = Align == A2UIAlign.Stretch ? finalCross : Math.Min(finalCross, Cross(child.DesiredSize, horizontal));
            var crossOffset = Align switch
            {
                A2UIAlign.Center => (finalCross - childCross) / 2,
                A2UIAlign.End => finalCross - childCross,
                _ => 0
            };
            var rect = horizontal
                ? new Rect(offset, crossOffset, sizes[i], childCross)
                : new Rect(crossOffset, offset, childCross, sizes[i]);
            child.Arrange(rect);
            offset += sizes[i] + between;
        }

        return finalSize;
    }
}
