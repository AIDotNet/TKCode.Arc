using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out a <see cref="WorkflowNodeCard"/>'s icon, text and state mark (children in that order). Standard (Arc
/// <c>.node</c>, flex with gap 12): icon, text and state in one centered row, the text taking the rest. Compact (Arc
/// <c>.node[data-compact]</c>, grid <c>1fr auto</c> with gaps 8 × 4): icon and state on the first row, the text across
/// the second, the two rows centered as a block.
/// </summary>
internal sealed class WorkflowCardPanel : Panel
{
    /// <summary>Arc's compact column gap (<c>gap: 8px 4px</c>).</summary>
    private const double CompactColumnGap = 4;

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<WorkflowCardPanel, bool>(nameof(IsCompact));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<WorkflowCardPanel, double>(nameof(Gap), 12);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<WorkflowCardPanel, double>(nameof(RowGap), 8);

    static WorkflowCardPanel()
    {
        // Decorative: the card's name says it all.
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WorkflowCardPanel>(AccessibilityView.Raw);
        AffectsMeasure<WorkflowCardPanel>(IsCompactProperty, GapProperty, RowGapProperty);
    }

    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double RowGap
    {
        get => GetValue(RowGapProperty);
        set => SetValue(RowGapProperty, value);
    }

    private Control? Icon => Children.Count > 0 ? Children[0] : null;

    private Control? Text => Children.Count > 1 ? Children[1] : null;

    private Control? State => Children.Count > 2 ? Children[2] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        Icon?.Measure(infinite);
        State?.Measure(infinite);
        var icon = Icon?.DesiredSize ?? default;
        var state = State?.DesiredSize ?? default;
        if (IsCompact)
        {
            Text?.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            var text = Text?.DesiredSize ?? default;
            var top = Math.Max(icon.Height, state.Height);
            return new Size(Math.Max(text.Width, icon.Width + CompactColumnGap + state.Width), top + RowGap + text.Height);
        }
        var used = icon.Width + Gap + (state.Width > 0 ? Gap + state.Width : 0);
        var textWidth = Math.Max(0, availableSize.Width - used);
        Text?.Measure(new Size(textWidth, double.PositiveInfinity));
        var textSize = Text?.DesiredSize ?? default;
        return new Size(used + textSize.Width, Math.Max(textSize.Height, Math.Max(icon.Height, state.Height)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var icon = Icon?.DesiredSize ?? default;
        var state = State?.DesiredSize ?? default;
        var text = Text?.DesiredSize ?? default;
        if (IsCompact)
        {
            var top = Math.Max(icon.Height, state.Height);
            var y = (finalSize.Height - (top + RowGap + text.Height)) / 2;
            Icon?.Arrange(new Rect(0, y + (top - icon.Height) / 2, icon.Width, icon.Height));
            State?.Arrange(new Rect(finalSize.Width - state.Width, y + (top - state.Height) / 2, state.Width, state.Height));
            Text?.Arrange(new Rect(0, y + top + RowGap, finalSize.Width, text.Height));
            return finalSize;
        }
        Icon?.Arrange(new Rect(0, (finalSize.Height - icon.Height) / 2, icon.Width, icon.Height));
        State?.Arrange(new Rect(finalSize.Width - state.Width, (finalSize.Height - state.Height) / 2, state.Width, state.Height));
        var left = icon.Width + Gap;
        var right = state.Width > 0 ? state.Width + Gap : 0;
        Text?.Arrange(new Rect(left, (finalSize.Height - text.Height) / 2, Math.Max(0, finalSize.Width - left - right), text.Height));
        return finalSize;
    }
}
