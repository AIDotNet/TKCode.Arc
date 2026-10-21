using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out an optional lead (brand mark), a body (text) and a trailing part (price or buttons) on one line, and moves the
/// trailing part under the body, aligned with it, when the panel is narrower than <see cref="StackWidth"/>, when
/// <see cref="IsStackForced"/> is set, or when the body's single-line width and the trailing part do not fit side by side.
/// Children in order: lead, body, trailing (three children) or body, trailing (two).
/// </summary>
internal sealed class BillingOverviewSplitPanel : Panel
{
    public static readonly StyledProperty<double> StackWidthProperty =
        AvaloniaProperty.Register<BillingOverviewSplitPanel, double>(nameof(StackWidth));

    public static readonly StyledProperty<bool> IsStackForcedProperty =
        AvaloniaProperty.Register<BillingOverviewSplitPanel, bool>(nameof(IsStackForced));

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<BillingOverviewSplitPanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> StackedGapProperty =
        AvaloniaProperty.Register<BillingOverviewSplitPanel, double>(nameof(StackedGap), 12);

    static BillingOverviewSplitPanel()
    {
        AffectsMeasure<BillingOverviewSplitPanel>(StackWidthProperty, IsStackForcedProperty, ColumnGapProperty, StackedGapProperty);
    }

    /// <summary>Below this width the trailing part always moves under the body.</summary>
    public double StackWidth
    {
        get => GetValue(StackWidthProperty);
        set => SetValue(StackWidthProperty, value);
    }

    /// <summary>Keeps the trailing part under the body at any width.</summary>
    public bool IsStackForced
    {
        get => GetValue(IsStackForcedProperty);
        set => SetValue(IsStackForcedProperty, value);
    }

    /// <summary>Gap after the lead and before the trailing part.</summary>
    public double ColumnGap
    {
        get => GetValue(ColumnGapProperty);
        set => SetValue(ColumnGapProperty, value);
    }

    /// <summary>Gap above the trailing part once it sits under the body.</summary>
    public double StackedGap
    {
        get => GetValue(StackedGapProperty);
        set => SetValue(StackedGapProperty, value);
    }

    /// <summary>Whether the trailing part sits under the body after the last measure.</summary>
    public bool IsStacked { get; private set; }

    private void GetParts(out Control? lead, out Control? body, out Control? trailing)
    {
        var children = Children;
        lead = null;
        body = null;
        trailing = null;
        if (children.Count >= 3)
        {
            lead = children[0];
            body = children[1];
            trailing = children[2];
        }
        else if (children.Count == 2)
        {
            body = children[0];
            trailing = children[1];
        }
        else if (children.Count == 1)
        {
            body = children[0];
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        GetParts(out var lead, out var body, out var trailing);
        var gap = ColumnGap;
        var width = availableSize.Width;
        var unbounded = double.IsInfinity(width);
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);

        var leadSize = default(Size);
        if (lead is { IsVisible: true })
        {
            lead.Measure(infinite);
            leadSize = lead.DesiredSize;
        }
        var leadSpan = leadSize.Width > 0 ? leadSize.Width + gap : 0;

        var trailSize = default(Size);
        var hasTrail = trailing is { IsVisible: true };
        if (hasTrail)
        {
            trailing!.Measure(infinite);
            trailSize = trailing.DesiredSize;
        }
        var trailSpan = hasTrail && trailSize.Width > 0 ? trailSize.Width + gap : 0;

        var stacked = false;
        if (body is null)
        {
            IsStacked = false;
            return new Size(leadSpan + trailSize.Width, Math.Max(leadSize.Height, trailSize.Height));
        }
        if (!unbounded && hasTrail)
        {
            if (IsStackForced || width < StackWidth)
            {
                stacked = true;
            }
            else
            {
                // The body's single-line width: wrap the trailing part before wrapping the text.
                body.Measure(infinite);
                stacked = leadSpan + body.DesiredSize.Width + trailSpan > width + 0.5;
            }
        }
        IsStacked = stacked;

        var bodyWidth = unbounded ? double.PositiveInfinity : Math.Max(0, width - leadSpan - (stacked ? 0 : trailSpan));
        body.Measure(new Size(bodyWidth, double.PositiveInfinity));
        var bodySize = body.DesiredSize;
        double height;
        if (stacked)
        {
            trailing!.Measure(new Size(bodyWidth, double.PositiveInfinity));
            trailSize = trailing.DesiredSize;
            height = Math.Max(leadSize.Height, bodySize.Height + StackedGap + trailSize.Height);
        }
        else
        {
            height = Math.Max(leadSize.Height, Math.Max(bodySize.Height, trailSize.Height));
        }
        var desiredWidth = unbounded ? leadSpan + bodySize.Width + trailSpan : width;
        return new Size(desiredWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        GetParts(out var lead, out var body, out var trailing);
        var gap = ColumnGap;
        var height = finalSize.Height;
        var leadSpan = 0.0;
        if (lead is { IsVisible: true })
        {
            var size = lead.DesiredSize;
            var y = IsStacked ? 0 : (height - size.Height) / 2;
            lead.Arrange(new Rect(0, y, size.Width, size.Height));
            leadSpan = size.Width > 0 ? size.Width + gap : 0;
        }
        var hasTrail = trailing is { IsVisible: true };
        var trailSize = hasTrail ? trailing!.DesiredSize : default;
        var trailSpan = hasTrail && trailSize.Width > 0 ? trailSize.Width + gap : 0;
        if (body is null)
        {
            if (hasTrail)
            {
                trailing!.Arrange(new Rect(finalSize.Width - trailSize.Width, (height - trailSize.Height) / 2, trailSize.Width, trailSize.Height));
            }
            return finalSize;
        }
        var bodySize = body.DesiredSize;
        if (IsStacked)
        {
            var bodyWidth = Math.Max(0, finalSize.Width - leadSpan);
            body.Arrange(new Rect(leadSpan, 0, bodyWidth, bodySize.Height));
            if (hasTrail)
            {
                trailing!.Arrange(new Rect(leadSpan, bodySize.Height + StackedGap, Math.Min(trailSize.Width, bodyWidth), trailSize.Height));
            }
        }
        else
        {
            var bodyWidth = Math.Max(0, finalSize.Width - leadSpan - trailSpan);
            body.Arrange(new Rect(leadSpan, (height - bodySize.Height) / 2, bodyWidth, bodySize.Height));
            if (hasTrail)
            {
                trailing!.Arrange(new Rect(finalSize.Width - trailSize.Width, (height - trailSize.Height) / 2, trailSize.Width, trailSize.Height));
            }
        }
        return finalSize;
    }
}
