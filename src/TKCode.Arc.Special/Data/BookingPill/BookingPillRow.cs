using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The inside of a <see cref="BookingPill"/>: the raised chip, three segments with a line between each two, and the search
/// button, in that child order. The segments share the width; the open one takes <see cref="OpenGrow"/> more, and the chip
/// sits under it. Both follow one spring per segment, so the pill reshapes and the chip glides as one motion; only this
/// panel arranges again while they move.
/// </summary>
internal sealed class BookingPillRow : Panel
{
    /// <summary>How much wider the open segment gets, as a share of an even split.</summary>
    public static readonly StyledProperty<double> OpenGrowProperty =
        AvaloniaProperty.Register<BookingPillRow, double>(nameof(OpenGrow), 0.3);

    private readonly MotionValue<double>?[] _springs = new MotionValue<double>?[3];
    private readonly double[] _open = new double[3];
    private int _openIndex = -1;

    static BookingPillRow()
    {
        AffectsArrange<BookingPillRow>(OpenGrowProperty);
    }

    public double OpenGrow
    {
        get => GetValue(OpenGrowProperty);
        set => SetValue(OpenGrowProperty, value);
    }

    /// <summary>The open segment, 0 to 2, or −1.</summary>
    public int OpenIndex => _openIndex;

    /// <summary>How open each segment is drawn now, 0 to 1 (tests).</summary>
    internal IReadOnlyList<double> Openness => _open;

    private Control? Chip => Children.Count > 0 ? Children[0] : null;

    private Control? Segment(int index) => Children.Count > 1 + index * 2 ? Children[1 + index * 2] : null;

    private Control? Divider(int index) => Children.Count > 2 + index * 2 ? Children[2 + index * 2] : null;

    private Control? Search => Children.Count > 6 ? Children[6] : null;

    /// <summary>Opens a segment (or none, −1): the widths and the chip spring there together.</summary>
    public void Open(int index, bool animate)
    {
        _openIndex = index;
        for (var i = 0; i < 3; i++)
        {
            var target = i == index ? 1.0 : 0.0;
            if (!animate || TopLevel.GetTopLevel(this) is null || !ArcMotion.IsFull(this))
            {
                _springs[i]?.Jump(target);
                _open[i] = target;
                continue;
            }
            _ = Spring(i).AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
        }
        InvalidateArrange();
    }

    private MotionValue<double> Spring(int index)
    {
        if (_springs[index] is { } existing)
        {
            return existing;
        }
        var spring = new MotionValue<double>(this, _open[index], MotionVectors.Double);
        spring.Changed += value =>
        {
            _open[index] = value;
            InvalidateArrange();
        };
        _springs[index] = spring;
        return spring;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var search = Search;
        search?.Measure(availableSize);
        var searchWidth = search?.DesiredSize.Width ?? 0;
        var rest = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, availableSize.Width - searchWidth);
        var height = search?.DesiredSize.Height ?? 0;
        var width = 0.0;
        for (var i = 0; i < 3; i++)
        {
            if (Segment(i) is not { } segment)
            {
                continue;
            }
            segment.Measure(new Size(rest / 3, availableSize.Height));
            width += segment.DesiredSize.Width;
            height = Math.Max(height, segment.DesiredSize.Height);
        }
        Chip?.Measure(availableSize);
        for (var i = 0; i < 2; i++)
        {
            Divider(i)?.Measure(availableSize);
        }
        return new Size(width + searchWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var search = Search;
        var searchWidth = search?.DesiredSize.Width ?? 0;
        var height = finalSize.Height;
        search?.Arrange(new Rect(finalSize.Width - searchWidth, (height - (search.DesiredSize.Height)) / 2, searchWidth,
            search.DesiredSize.Height));
        var rest = Math.Max(0, finalSize.Width - searchWidth);
        var grow = OpenGrow;
        Span<double> weights = stackalloc double[3];
        var total = 0.0;
        for (var i = 0; i < 3; i++)
        {
            weights[i] = 1 + grow * _open[i];
            total += weights[i];
        }
        Span<Rect> boxes = stackalloc Rect[3];
        var x = 0.0;
        for (var i = 0; i < 3; i++)
        {
            var width = rest * weights[i] / total;
            boxes[i] = new Rect(x, 0, width, height);
            Segment(i)?.Arrange(boxes[i]);
            x += width;
        }
        // The chip blends the boxes of the segments that are (partly) open, so it glides from one to the next.
        var sum = _open[0] + _open[1] + _open[2];
        if (Chip is { } chip)
        {
            if (sum > 0.001)
            {
                double left = 0, right = 0;
                for (var i = 0; i < 3; i++)
                {
                    left += boxes[i].X * _open[i] / sum;
                    right += boxes[i].Right * _open[i] / sum;
                }
                chip.Arrange(new Rect(left, 0, Math.Max(0, right - left), height));
            }
            else
            {
                var at = _openIndex >= 0 ? boxes[_openIndex] : boxes[0];
                chip.Arrange(at);
            }
            chip.Opacity = Math.Clamp(sum, 0, 1);
        }
        // A line between two segments fades while either of them is open.
        for (var i = 0; i < 2; i++)
        {
            if (Divider(i) is not { } divider)
            {
                continue;
            }
            var size = divider.DesiredSize;
            divider.Arrange(new Rect(boxes[i].Right - size.Width / 2, (height - size.Height) / 2, size.Width, size.Height));
            divider.Opacity = 1 - Math.Clamp(Math.Max(_open[i], _open[i + 1]), 0, 1);
        }
        return finalSize;
    }
}
