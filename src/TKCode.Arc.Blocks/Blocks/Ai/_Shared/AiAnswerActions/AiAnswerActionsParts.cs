using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out the answer's action bar: the toolbar (first child) on the left and the model line (second child) on the right
/// of the same row, <see cref="ColumnGap"/> apart at least, centered on the row; when both do not fit, the model line moves
/// under the toolbar, <see cref="RowGap"/> below it, at the left edge (Arc: <c>flex-wrap</c> with
/// <c>justify-content: space-between</c>).
/// </summary>
internal sealed class AiAnswerActionsLayout : Panel
{
    /// <summary>The least space between the toolbar and the model line on one row (16).</summary>
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<AiAnswerActionsLayout, double>(nameof(ColumnGap), 16);

    /// <summary>The space between the rows when the model line wraps (4).</summary>
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<AiAnswerActionsLayout, double>(nameof(RowGap), 4);

    private bool _wrapped;

    static AiAnswerActionsLayout()
    {
        AffectsMeasure<AiAnswerActionsLayout>(ColumnGapProperty, RowGapProperty);
    }

    /// <inheritdoc cref="ColumnGapProperty"/>
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <inheritdoc cref="RowGapProperty"/>
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>True when the model line sits on its own row.</summary>
    internal bool IsWrapped => _wrapped;

    private Control? First => Children.Count > 0 ? Children[0] : null;

    private Control? Second => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var first = First;
        var second = Second;
        first?.Measure(availableSize);
        var bar = first?.DesiredSize ?? default;
        if (second is null)
        {
            _wrapped = false;
            return new Size(double.IsInfinity(availableSize.Width) ? bar.Width : Math.Max(bar.Width, 0), bar.Height);
        }
        // The line's natural width decides whether it fits next to the toolbar.
        second.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var line = second.DesiredSize;
        _wrapped = !double.IsInfinity(availableSize.Width) && bar.Width + ColumnGap + line.Width > availableSize.Width + 0.01;
        if (!_wrapped)
        {
            var width = double.IsInfinity(availableSize.Width) ? bar.Width + ColumnGap + line.Width : availableSize.Width;
            return new Size(width, Math.Max(bar.Height, line.Height));
        }
        second.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        line = second.DesiredSize;
        return new Size(availableSize.Width, bar.Height + RowGap + line.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var first = First;
        var second = Second;
        var bar = first?.DesiredSize ?? default;
        if (second is null)
        {
            first?.Arrange(new Rect(0, 0, bar.Width, finalSize.Height));
            return finalSize;
        }
        var line = second.DesiredSize;
        if (!_wrapped)
        {
            var rowHeight = Math.Max(bar.Height, line.Height);
            first?.Arrange(new Rect(0, (rowHeight - bar.Height) / 2, bar.Width, bar.Height));
            second.Arrange(new Rect(Math.Max(0, finalSize.Width - line.Width), (rowHeight - line.Height) / 2, line.Width, line.Height));
            return finalSize;
        }
        first?.Arrange(new Rect(0, 0, bar.Width, bar.Height));
        second.Arrange(new Rect(0, bar.Height + RowGap, Math.Min(line.Width, finalSize.Width), line.Height));
        return finalSize;
    }
}

/// <summary>The buttons of the answer's action bar in a row, read as a toolbar (Arc: <c>role="toolbar"</c>, no arrow-key roving).</summary>
internal sealed class AiAnswerToolbar : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ToolbarPeer(this);

    private sealed class ToolbarPeer(AiAnswerToolbar owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The version pager of an AI answer (Arc answer): a 1 × 16 Border line 8px after the actions and 6px before the pager,
/// then "‹ 2 / 2 ›" with 30px round arrows (16px chevrons, the action buttons' look) around the count in 14px secondary
/// text with tabular figures. Everything else (rolling numbers, the name "Version 2 of 2", the ends, focus hand-over) is
/// <see cref="AiVersionPager"/>'s; it hides itself with fewer than two versions.
/// </summary>
internal sealed class AiAnswerVersionPager : AiVersionPager
{
}
