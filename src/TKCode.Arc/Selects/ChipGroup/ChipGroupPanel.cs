using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Wrapping layout of a <see cref="ChipGroup"/> that re-flows in one pass and lets every chip glide from where it
/// was to its new place, even across lines (FLIP, P19). Leaving chips are out of the flow and fade where they were.
/// </summary>
internal sealed class ChipGroupPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ChipGroupPanel, double>(nameof(Spacing), 8);

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    static ChipGroupPanel()
    {
        AffectsMeasure<ChipGroupPanel>(SpacingProperty);
    }

    private IEnumerable<ChipGroupChip> Flow => Children.OfType<ChipGroupChip>().Where(c => !c.IsLeaving);

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxWidth = availableSize.Width;
        var spacing = Spacing;
        double x = 0, y = 0, lineHeight = 0, width = 0;
        var first = true;
        foreach (var child in Children)
        {
            child.Measure(new Size(maxWidth, double.PositiveInfinity));
        }
        foreach (var chip in Flow)
        {
            var size = chip.DesiredSize;
            if (!first && x + spacing + size.Width > maxWidth + 0.5)
            {
                y += lineHeight + spacing;
                x = 0;
                lineHeight = 0;
                first = true;
            }
            x += (first ? 0 : spacing) + size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
            width = Math.Max(width, x);
            first = false;
        }
        return new Size(width, y + lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var spacing = Spacing;
        double x = 0, y = 0, lineHeight = 0;
        var first = true;
        foreach (var chip in Flow)
        {
            var size = chip.DesiredSize;
            if (!first && x + spacing + size.Width > finalSize.Width + 0.5)
            {
                y += lineHeight + spacing;
                x = 0;
                lineHeight = 0;
                first = true;
            }
            var left = first ? x : x + spacing;
            var position = new Point(left, y);
            chip.Arrange(new Rect(position, size));
            if (chip.LastPosition is { } previous && !chip.IsFresh && previous != position)
            {
                chip.FlipBy(previous - position);
            }
            chip.LastPosition = position;
            chip.IsFresh = false;
            x = left + size.Width;
            lineHeight = Math.Max(lineHeight, size.Height);
            first = false;
        }
        foreach (var chip in Children.OfType<ChipGroupChip>().Where(c => c.IsLeaving))
        {
            chip.Arrange(new Rect(chip.LastPosition ?? default, chip.DesiredSize));
        }
        return finalSize;
    }
}
