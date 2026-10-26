using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Lays out a <see cref="Dialog"/>'s actions: up to two in one row of equal columns when each fits its column, otherwise
/// stacked one above the other at the full width (the layout for three actions and for narrow widths).
/// </summary>
internal sealed class DialogActionsPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<DialogActionsPanel, double>(nameof(Spacing), 8);

    static DialogActionsPanel()
    {
        AffectsMeasure<DialogActionsPanel>(SpacingProperty);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Whether the actions are stacked (true) or share one row (false) after the last layout.</summary>
    public bool IsStacked { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        var count = visible.Count;
        if (count == 0)
        {
            IsStacked = false;
            return default;
        }
        var natural = new List<Size>();
        foreach (var child in visible)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            natural.Add(child.DesiredSize);
        }
        var column = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : (availableSize.Width - Spacing * (count - 1)) / count;
        IsStacked = count > 2 || natural.Any(s => s.Width > column);
        if (IsStacked)
        {
            double width = 0;
            double height = 0;
            foreach (var child in visible)
            {
                child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
                width = Math.Max(width, child.DesiredSize.Width);
                height += child.DesiredSize.Height;
            }
            return new Size(width, height + Spacing * (count - 1));
        }
        double rowHeight = 0;
        foreach (var child in visible)
        {
            child.Measure(new Size(column, availableSize.Height));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }
        return new Size(double.IsInfinity(column) ? natural.Sum(s => s.Width) + Spacing * (count - 1) : availableSize.Width, rowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        if (visible.Count == 0)
        {
            return finalSize;
        }
        if (IsStacked)
        {
            double y = 0;
            foreach (var child in visible)
            {
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + Spacing;
            }
            return finalSize;
        }
        var column = (finalSize.Width - Spacing * (visible.Count - 1)) / visible.Count;
        for (var i = 0; i < visible.Count; i++)
        {
            visible[i].Arrange(new Rect(i * (column + Spacing), 0, column, finalSize.Height));
        }
        return finalSize;
    }
}
