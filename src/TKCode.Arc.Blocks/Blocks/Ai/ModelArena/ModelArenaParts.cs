using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The arena's stage (spec 3.1, 7): side by side, the two panes share the width equally with the 1px divider between them
/// and the stage is as tall as the taller pane; narrow, the tab strip sits on top and the selected pane below it at the full
/// width. The owner says which children play which part (<see cref="Tabs"/>, <see cref="PaneA"/>, <see cref="Divider"/>,
/// <see cref="PaneB"/>) and shows or hides them; a hidden child takes no space.
/// </summary>
internal sealed class ArenaStagePanel : Panel
{
    /// <summary>The narrow layout: the tab strip and one pane.</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<ArenaStagePanel, bool>(nameof(IsNarrow));

    static ArenaStagePanel()
    {
        AffectsMeasure<ArenaStagePanel>(IsNarrowProperty);
    }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <summary>The narrow layout's tab strip.</summary>
    public Control? Tabs { get; set; }

    /// <summary>The first pane.</summary>
    public Control? PaneA { get; set; }

    /// <summary>The line between the panes side by side.</summary>
    public Control? Divider { get; set; }

    /// <summary>The second pane.</summary>
    public Control? PaneB { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsNarrow)
        {
            var top = Measure(Tabs, availableSize).Height;
            var below = availableSize.WithHeight(Math.Max(0, availableSize.Height - top));
            var a = Measure(PaneA, below);
            var b = Measure(PaneB, below);
            return new Size(Math.Max(Math.Max(a.Width, b.Width), Tabs?.DesiredSize.Width ?? 0), top + Math.Max(a.Height, b.Height));
        }
        var line = Visible(Divider) ? 1.0 : 0.0;
        Measure(Divider, availableSize);
        var width = availableSize.Width;
        if (double.IsInfinity(width))
        {
            var left = Measure(PaneA, availableSize);
            var right = Measure(PaneB, availableSize);
            return new Size(left.Width + line + right.Width, Math.Max(left.Height, right.Height));
        }
        var (first, second) = Split(width, line);
        var sizeA = Measure(PaneA, availableSize.WithWidth(first));
        var sizeB = Measure(PaneB, availableSize.WithWidth(second));
        return new Size(width, Math.Max(sizeA.Height, sizeB.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (IsNarrow)
        {
            var top = Visible(Tabs) ? Tabs!.DesiredSize.Height : 0;
            Tabs?.Arrange(new Rect(0, 0, finalSize.Width, top));
            var rest = new Rect(0, top, finalSize.Width, Math.Max(0, finalSize.Height - top));
            PaneA?.Arrange(rest);
            PaneB?.Arrange(rest);
            Divider?.Arrange(default);
            return finalSize;
        }
        var line = Visible(Divider) ? 1.0 : 0.0;
        var (first, second) = Split(finalSize.Width, line);
        PaneA?.Arrange(new Rect(0, 0, first, finalSize.Height));
        Divider?.Arrange(new Rect(first, 0, line, finalSize.Height));
        PaneB?.Arrange(new Rect(first + line, 0, second, finalSize.Height));
        Tabs?.Arrange(default);
        return finalSize;
    }

    /// <summary>The two pane widths: equal halves, the extra pixel of an odd width to the second (391 / 392 at 784).</summary>
    private static (double First, double Second) Split(double width, double line)
    {
        var room = Math.Max(0, width - line);
        var first = Math.Floor(room / 2);
        return (first, room - first);
    }

    private static Size Measure(Control? child, Size available)
    {
        if (child is null)
        {
            return default;
        }
        child.Measure(available);
        return child.IsVisible ? child.DesiredSize : default;
    }

    private static bool Visible(Control? child) => child is { IsVisible: true };
}
