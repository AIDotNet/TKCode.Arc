using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The banner's row, a container query on its own width. Its four children are, in order: the lead (the ring), the copy,
/// the actions and the tools (snooze and dismiss). Wide, they sit on one line, vertically centered, and the copy takes the
/// rest. Under <see cref="CompactBelow"/> the actions move under the copy, aligned with its left edge, and the copy may
/// take two lines (<see cref="CompactChanged"/> is raised before the copy is measured, so it can reserve them).
/// </summary>
internal sealed class TrialBannerRow : Panel
{
    /// <summary>The space between the lead, the copy, the actions and the tools.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<TrialBannerRow, double>(nameof(Gap), 12);

    /// <summary>The row's width under which the actions move under the copy.</summary>
    public static readonly StyledProperty<double> CompactBelowProperty =
        AvaloniaProperty.Register<TrialBannerRow, double>(nameof(CompactBelow), 600);

    /// <summary>The space between the copy and the actions when they are stacked.</summary>
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<TrialBannerRow, double>(nameof(RowGap), 10);

    private bool _compact;

    static TrialBannerRow()
    {
        AffectsMeasure<TrialBannerRow>(GapProperty, CompactBelowProperty, RowGapProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double CompactBelow { get => GetValue(CompactBelowProperty); set => SetValue(CompactBelowProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>Whether the actions are under the copy.</summary>
    public bool IsCompact => _compact;

    /// <summary>Raised during measure, before the copy is measured, when the row switches between one and two lines.</summary>
    public event Action<bool>? CompactChanged;

    private Control? Lead => Children.Count > 0 ? Children[0] : null;

    private Control? Copy => Children.Count > 1 ? Children[1] : null;

    private Control? Actions => Children.Count > 2 ? Children[2] : null;

    private Control? Tools => Children.Count > 3 ? Children[3] : null;

    private static Size Desired(Control? control) => control is { IsVisible: true } ? control.DesiredSize : default;

    private double GapAfter(Control? control) => control is { IsVisible: true } && control.DesiredSize.Width > 0 ? Gap : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var compact = !double.IsInfinity(width) && width < CompactBelow;
        if (compact != _compact)
        {
            _compact = compact;
            CompactChanged?.Invoke(compact);
        }
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        Lead?.Measure(infinite);
        Tools?.Measure(infinite);
        var lead = Desired(Lead);
        var indent = lead.Width + GapAfter(Lead);
        if (compact)
        {
            Actions?.Measure(new Size(Math.Max(0, width - indent), double.PositiveInfinity));
        }
        else
        {
            Actions?.Measure(infinite);
        }
        var actions = Desired(Actions);
        var tools = Desired(Tools);
        var toolsSpace = tools.Width > 0 ? tools.Width + Gap / 2 : 0;
        var actionsSpace = compact || actions.Width <= 0 ? 0 : actions.Width + Gap;
        var copyWidth = double.IsInfinity(width) ? double.PositiveInfinity : Math.Max(0, width - indent - actionsSpace - toolsSpace);
        Copy?.Measure(new Size(copyWidth, double.PositiveInfinity));
        var copy = Desired(Copy);
        var lineHeight = Math.Max(Math.Max(lead.Height, copy.Height), tools.Height);
        double height;
        if (compact)
        {
            height = lineHeight + (actions.Height > 0 ? RowGap + actions.Height : 0);
        }
        else
        {
            height = Math.Max(lineHeight, actions.Height);
        }
        var total = double.IsInfinity(width) ? indent + copy.Width + actionsSpace + toolsSpace : width;
        return new Size(total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var lead = Desired(Lead);
        var copy = Desired(Copy);
        var actions = Desired(Actions);
        var tools = Desired(Tools);
        var indent = lead.Width + GapAfter(Lead);
        var toolsSpace = tools.Width > 0 ? tools.Width + Gap / 2 : 0;
        if (_compact)
        {
            var line = Math.Max(Math.Max(lead.Height, copy.Height), tools.Height);
            Lead?.Arrange(new Rect(0, 0, lead.Width, lead.Height));
            Copy?.Arrange(new Rect(indent, (line - copy.Height) / 2, Math.Max(0, width - indent - toolsSpace), copy.Height));
            Tools?.Arrange(new Rect(width - tools.Width, 0, tools.Width, tools.Height));
            Actions?.Arrange(new Rect(indent, line + RowGap, Math.Max(0, width - indent), actions.Height));
            return finalSize;
        }
        var height = finalSize.Height;
        var actionsSpace = actions.Width > 0 ? actions.Width + Gap : 0;
        Lead?.Arrange(new Rect(0, (height - lead.Height) / 2, lead.Width, lead.Height));
        Copy?.Arrange(new Rect(indent, (height - copy.Height) / 2, Math.Max(0, width - indent - actionsSpace - toolsSpace), copy.Height));
        Actions?.Arrange(new Rect(width - toolsSpace - actions.Width, (height - actions.Height) / 2, actions.Width, actions.Height));
        Tools?.Arrange(new Rect(width - tools.Width, (height - tools.Height) / 2, tools.Width, tools.Height));
        return finalSize;
    }
}
