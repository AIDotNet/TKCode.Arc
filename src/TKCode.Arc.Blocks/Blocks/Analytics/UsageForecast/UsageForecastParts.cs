using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Two children on one line, the first at the start and the second at the end (CSS <c>space-between</c> with
/// <c>flex-wrap</c>): when they do not fit side by side with <see cref="ColumnGap"/>, or <see cref="IsWrapForced"/>, they
/// stack at the start with <see cref="RowGap"/>. Used for the header (titles and metric switch).
/// </summary>
internal sealed class UsageForecastSplitPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<UsageForecastSplitPanel, double>(nameof(ColumnGap), 24);
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<UsageForecastSplitPanel, double>(nameof(RowGap), 12);
    public static readonly StyledProperty<bool> IsWrapForcedProperty =
        AvaloniaProperty.Register<UsageForecastSplitPanel, bool>(nameof(IsWrapForced));

    private bool _wrapped;

    static UsageForecastSplitPanel()
    {
        AffectsMeasure<UsageForecastSplitPanel>(ColumnGapProperty, RowGapProperty, IsWrapForcedProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }
    public bool IsWrapForced { get => GetValue(IsWrapForcedProperty); set => SetValue(IsWrapForcedProperty, value); }

    private (Control? Lead, Control? Trail) Parts()
    {
        Control? lead = null;
        Control? trail = null;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            if (lead is null)
            {
                lead = child;
            }
            else if (trail is null)
            {
                trail = child;
            }
        }
        return (lead, trail);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (lead, trail) = Parts();
        var width = availableSize.Width;
        if (lead is null)
        {
            _wrapped = false;
            return default;
        }
        if (trail is null)
        {
            _wrapped = false;
            lead.Measure(availableSize);
            return lead.DesiredSize;
        }
        var inf = new Size(double.PositiveInfinity, availableSize.Height);
        lead.Measure(inf);
        trail.Measure(inf);
        _wrapped = IsWrapForced || lead.DesiredSize.Width + ColumnGap + trail.DesiredSize.Width > width;
        if (_wrapped)
        {
            lead.Measure(new Size(width, double.PositiveInfinity));
            trail.Measure(new Size(width, double.PositiveInfinity));
            return new Size(Math.Max(lead.DesiredSize.Width, trail.DesiredSize.Width), lead.DesiredSize.Height + RowGap + trail.DesiredSize.Height);
        }
        lead.Measure(new Size(Math.Max(0, width - ColumnGap - trail.DesiredSize.Width), availableSize.Height));
        return new Size(lead.DesiredSize.Width + ColumnGap + trail.DesiredSize.Width, Math.Max(lead.DesiredSize.Height, trail.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (lead, trail) = Parts();
        if (lead is null)
        {
            return finalSize;
        }
        if (trail is null)
        {
            lead.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, lead.DesiredSize.Width), finalSize.Height));
            return finalSize;
        }
        if (_wrapped)
        {
            lead.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, lead.DesiredSize.Width), lead.DesiredSize.Height));
            trail.Arrange(new Rect(0, lead.DesiredSize.Height + RowGap, Math.Min(finalSize.Width, trail.DesiredSize.Width), trail.DesiredSize.Height));
            return finalSize;
        }
        lead.Arrange(new Rect(0, 0, lead.DesiredSize.Width, lead.DesiredSize.Height));
        trail.Arrange(new Rect(finalSize.Width - trail.DesiredSize.Width, (finalSize.Height - trail.DesiredSize.Height) / 2, trail.DesiredSize.Width, trail.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The legend under the chips: a 2px line for the measured part, a dashed 1.75px line for the forecast, a 12px swatch for the
/// expected range and a red line for the part over the budget, each with its text (12 on 16.8, TextMuted), wrapping with
/// 6px between lines and 16px (12 when compact) between entries.
/// </summary>
internal sealed class UsageLegend : Control
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<UsageLegend, double>(nameof(ColumnGap), 16);
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<UsageLegend, double>(nameof(RowGap), 6);

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(64);
    private string[] _texts = ["", "", "", ""];

    static UsageLegend()
    {
        AffectsMeasure<UsageLegend>(ColumnGapProperty, RowGapProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<UsageLegend>(false);
    }

    public UsageLegend()
    {
        ActualThemeVariantChanged += (_, _) =>
        {
            _palette.Refresh(this);
            _text.Clear();
            InvalidateVisual();
        };
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>The four entries' texts: measured, projected, expected range, over budget.</summary>
    public void SetTexts(string measured, string projected, string range, string over)
    {
        _texts = [measured, projected, range, over];
        InvalidateMeasure();
        InvalidateVisual();
    }

    private IEnumerable<(Rect Rect, int Index)> Flow(double width)
    {
        if (!_palette.IsReady && this.IsAttachedToVisualTree())
        {
            _palette.Refresh(this);
        }
        var face = new Typeface(_palette.BodyFont);
        double x = 0, y = 0;
        for (var i = 0; i < _texts.Length; i++)
        {
            var sample = i == 2 ? 12 : 16;
            var w = sample + 6 + _text.Get(_texts[i], face, 12).Width;
            if (x > 0 && x + w > width + 0.01)
            {
                x = 0;
                y += 16.8 + RowGap;
            }
            yield return (new Rect(x, y, w, 16.8), i);
            x += w + ColumnGap;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 10000 : availableSize.Width;
        var rects = Flow(width).ToList();
        return new Size(Math.Min(width, rects.Max(r => r.Rect.Right)), rects.Max(r => r.Rect.Bottom));
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            if (!this.IsAttachedToVisualTree())
            {
                return;
            }
            _palette.Refresh(this);
        }
        var p = _palette;
        var face = new Typeface(p.BodyFont);
        foreach (var (rect, i) in Flow(Bounds.Width))
        {
            var cy = rect.Y + rect.Height / 2;
            switch (i)
            {
                case 0:
                    context.DrawLine(p.Pen(p.Accent, 2), new Point(rect.X + 1, cy), new Point(rect.X + 15, cy));
                    break;
                case 1:
                    context.DrawLine(p.Pen(p.Accent, 1.75, 1, 4, 4, PenLineCap.Flat), new Point(rect.X, cy), new Point(rect.X + 16, cy));
                    break;
                case 2:
                    context.DrawRectangle(p.Brush(p.Accent, 0.12), null, new RoundedRect(new Rect(rect.X, cy - 5, 12, 10), 2));
                    break;
                default:
                    context.DrawLine(p.Pen(p.Danger, 2), new Point(rect.X + 1, cy), new Point(rect.X + 15, cy));
                    break;
            }
            var text = _text.Get(_texts[i], face, 12);
            text.Draw(context, p.Brush(p.TextMuted), new Point(rect.X + (i == 2 ? 12 : 16) + 6, rect.Y + (rect.Height - text.Height) / 2));
        }
    }
}

/// <summary>
/// The days as a table for screen readers (Arc's visually hidden <c>&lt;table&gt;</c>): a zero-size control whose peer is a
/// table named by its caption with a row per day (date, reading, expected range). Nothing is drawn or takes input.
/// </summary>
internal sealed class UsageForecastDataTable : Control
{
    private string _caption = string.Empty;
    private string[] _rowHeaders = [];
    private string[][] _cells = [];

    static UsageForecastDataTable()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<UsageForecastDataTable>(false);
        FocusableProperty.OverrideDefaultValue<UsageForecastDataTable>(false);
    }

    /// <summary>The caption.</summary>
    public string Caption => _caption;

    /// <summary>The row names (dates).</summary>
    public IReadOnlyList<string> RowHeaders => _rowHeaders;

    /// <summary>The cell texts, row by row (reading, range).</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows => _cells;

    /// <summary>Replaces the table.</summary>
    public void Update(string caption, string[] rowHeaders, string[][] cells)
    {
        _caption = caption;
        _rowHeaders = rowHeaders;
        _cells = cells;
        if (ControlAutomationPeer.FromElement(this) is UsageForecastDataTablePeer peer)
        {
            peer.Rebuild();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new UsageForecastDataTablePeer(this);
}

internal sealed class UsageForecastDataTablePeer(UsageForecastDataTable owner) : ControlAutomationPeer(owner)
{
    private List<AutomationPeer>? _rows;

    private new UsageForecastDataTable Owner => (UsageForecastDataTable)base.Owner;

    internal void Rebuild()
    {
        _rows = null;
        InvalidateChildren();
        RaisePropertyChangedEvent(Avalonia.Automation.AutomationElementIdentifiers.NameProperty, null, Owner.Caption);
    }

    protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

    protected override string? GetNameCore() => Owner.Caption;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override IReadOnlyList<AutomationPeer> GetChildrenCore()
    {
        if (_rows is null)
        {
            _rows = new List<AutomationPeer>(Owner.RowHeaders.Count);
            for (var r = 0; r < Owner.RowHeaders.Count; r++)
            {
                var row = new UsageForecastTextPeer(this, Owner.RowHeaders[r], AutomationControlType.DataItem);
                var children = new List<AutomationPeer> { new UsageForecastTextPeer(row, Owner.RowHeaders[r], AutomationControlType.HeaderItem) };
                if (r < Owner.Rows.Count)
                {
                    foreach (var cell in Owner.Rows[r])
                    {
                        children.Add(new UsageForecastTextPeer(row, cell, AutomationControlType.Text));
                    }
                }
                row.SetChildren(children);
                _rows.Add(row);
            }
        }
        return _rows;
    }
}

/// <summary>A peer with a fixed name and type that is not backed by a control (a table row or cell).</summary>
internal sealed class UsageForecastTextPeer(AutomationPeer parent, string name, AutomationControlType type) : AutomationPeer
{
    private IReadOnlyList<AutomationPeer> _children = [];

    internal void SetChildren(IReadOnlyList<AutomationPeer> children) => _children = children;

    protected override void BringIntoViewCore()
    {
    }

    protected override string? GetAcceleratorKeyCore() => null;

    protected override string? GetAccessKeyCore() => null;

    protected override AutomationControlType GetAutomationControlTypeCore() => type;

    protected override string? GetAutomationIdCore() => null;

    protected override Rect GetBoundingRectangleCore() => default;

    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => _children;

    protected override string GetClassNameCore() => type == AutomationControlType.DataItem ? "row" : "cell";

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override string? GetNameCore() => name;

    protected override AutomationPeer? GetParentCore() => parent;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsEnabledCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override void SetFocusCore()
    {
    }

    protected override bool ShowContextMenuCore() => false;

    protected override bool TrySetParent(AutomationPeer? parent) => false;
}
