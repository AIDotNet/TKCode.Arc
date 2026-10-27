using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Two children on one line, the first at the start and the second at the end (CSS <c>space-between</c> with
/// <c>flex-wrap</c>): when they do not fit beside each other with <see cref="ColumnGap"/>, or <see cref="IsWrapForced"/>,
/// they stack at the start with <see cref="RowGap"/>, in reverse order when <see cref="IsReversedWhenWrapped"/>. Used for
/// the header (titles and segment switch) and the toolbar (legend and view switch).
/// </summary>
internal sealed class CohortRetentionSplitPanel : Panel
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<CohortRetentionSplitPanel, double>(nameof(ColumnGap), 16);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<CohortRetentionSplitPanel, double>(nameof(RowGap), 12);

    public static readonly StyledProperty<bool> IsWrapForcedProperty =
        AvaloniaProperty.Register<CohortRetentionSplitPanel, bool>(nameof(IsWrapForced));

    public static readonly StyledProperty<bool> IsReversedWhenWrappedProperty =
        AvaloniaProperty.Register<CohortRetentionSplitPanel, bool>(nameof(IsReversedWhenWrapped));

    static CohortRetentionSplitPanel()
    {
        AffectsMeasure<CohortRetentionSplitPanel>(ColumnGapProperty, RowGapProperty, IsWrapForcedProperty, IsReversedWhenWrappedProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    public bool IsWrapForced { get => GetValue(IsWrapForcedProperty); set => SetValue(IsWrapForcedProperty, value); }

    public bool IsReversedWhenWrapped { get => GetValue(IsReversedWhenWrappedProperty); set => SetValue(IsReversedWhenWrappedProperty, value); }

    /// <summary>True when the last layout stacked the children.</summary>
    public bool IsWrapped { get; private set; }

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
            IsWrapped = false;
            return default;
        }
        if (trail is null)
        {
            IsWrapped = false;
            lead.Measure(availableSize);
            return lead.DesiredSize;
        }
        trail.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        lead.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var natural = lead.DesiredSize.Width;
        IsWrapped = IsWrapForced || natural + ColumnGap + trail.DesiredSize.Width > width;
        if (IsWrapped)
        {
            lead.Measure(new Size(width, double.PositiveInfinity));
            trail.Measure(new Size(width, double.PositiveInfinity));
            return new Size(Math.Max(lead.DesiredSize.Width, trail.DesiredSize.Width),
                lead.DesiredSize.Height + RowGap + trail.DesiredSize.Height);
        }
        lead.Measure(new Size(Math.Max(0, width - ColumnGap - trail.DesiredSize.Width), availableSize.Height));
        return new Size(lead.DesiredSize.Width + ColumnGap + trail.DesiredSize.Width,
            Math.Max(lead.DesiredSize.Height, trail.DesiredSize.Height));
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
        if (IsWrapped)
        {
            var first = IsReversedWhenWrapped ? trail : lead;
            var second = IsReversedWhenWrapped ? lead : trail;
            first.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, first.DesiredSize.Width), first.DesiredSize.Height));
            second.Arrange(new Rect(0, first.DesiredSize.Height + RowGap, Math.Min(finalSize.Width, second.DesiredSize.Width),
                second.DesiredSize.Height));
            return finalSize;
        }
        var height = finalSize.Height;
        lead.Arrange(new Rect(0, Offset(lead, height), lead.DesiredSize.Width, lead.DesiredSize.Height));
        var trailWidth = trail.DesiredSize.Width;
        trail.Arrange(new Rect(finalSize.Width - trailWidth, Offset(trail, height), trailWidth, trail.DesiredSize.Height));
        return finalSize;
    }

    private static double Offset(Control child, double height) => child.VerticalAlignment switch
    {
        VerticalAlignment.Center => (height - child.DesiredSize.Height) / 2,
        VerticalAlignment.Bottom => height - child.DesiredSize.Height,
        _ => 0
    };
}

/// <summary>
/// One headline metric of a <see cref="CohortRetention"/> (a <c>dt</c> / <c>dd</c> pair): label (14, TextSecondary), the
/// value rolling digit by digit (28 → 22 with the block's width, Medium, tabular), and the caption line: the change
/// (arrow + points, Success / Danger) followed by muted context, which drops to its own line when it does not fit.
/// Stacked (block under 440): label and caption on the left, the value right-aligned beside them.
/// </summary>
internal sealed class CohortRetentionKpi : Control
{
    private readonly TextBlock _label = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly AnalyticsRollingNumber _rolling = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly AnalyticsCountUp _counting = new() { IsVisible = false };
    private readonly TextBlock _missing = new() { Text = AnalyticsFormat.Missing, IsVisible = false };
    private readonly AnalyticsDelta _delta = new()
    {
        IconSize = 14, IconStroke = 1.75, Spacing = 4, Swap = AnalyticsSwapStyle.Rise, IsToneSlow = true, IsVisible = false
    };
    // The context does not change between segments (only the change does), so it is plain wrapping text.
    private readonly TextBlock _context = new() { LineHeight = 16.8, TextWrapping = TextWrapping.Wrap };
    private double _valueSize = 28;
    private bool _stacked;
    private bool _contextWrapped;
    private double _contextHeight = 16.8;

    public CohortRetentionKpi()
    {
        foreach (var child in new Control[] { _label, _rolling, _counting, _missing, _delta, _context })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        _label.LineHeight = 19.6;
        ArcTokens.Bind(_label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_rolling, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_rolling, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_counting, AnalyticsCountUp.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_counting, AnalyticsCountUp.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_missing, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_missing, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_context, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_context, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _delta.Margin = new Thickness(0);
        ApplyValueSize();
    }

    /// <summary>The value part (tests).</summary>
    internal AnalyticsRollingNumber Rolling => _rolling;

    /// <summary>The change part (tests).</summary>
    internal AnalyticsDelta Delta => _delta;

    /// <summary>The context text (tests).</summary>
    internal string? ContextText => _context.Text;

    /// <summary>The label.</summary>
    public string? Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <summary>Label and caption left, value right (the block is under 440 wide).</summary>
    public bool IsStacked
    {
        get => _stacked;
        set
        {
            if (_stacked != value)
            {
                _stacked = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The value's font size (22–28).</summary>
    public double ValueSize
    {
        get => _valueSize;
        set
        {
            if (Math.Abs(_valueSize - value) > 0.01)
            {
                _valueSize = value;
                ApplyValueSize();
                InvalidateMeasure();
            }
        }
    }

    /// <summary>What a screen reader hears: label, value, change and context.</summary>
    public string SpokenText
    {
        get
        {
            var value = _missing.IsVisible ? AnalyticsFormat.Missing : _counting.IsVisible ? _counting.FinalText : _rolling.FullText;
            var text = $"{Label}, {value}";
            if (_delta.IsVisible)
            {
                text += $", {_delta.SpokenText}";
            }
            return string.IsNullOrEmpty(_context.Text) ? text : $"{text} {_context.Text}";
        }
    }

    /// <summary>Shows a value: rolling with <paramref name="format"/>, or counting with <paramref name="formatter"/>; null shows "—".</summary>
    public void SetValue(double? value, AnalyticsNumberFormat format, Func<double, string>? formatter)
    {
        _missing.IsVisible = value is null;
        _rolling.IsVisible = value is not null && formatter is null;
        _counting.IsVisible = value is not null && formatter is not null;
        if (value is not { } v)
        {
            UpdateName();
            return;
        }
        if (formatter is null)
        {
            _rolling.Format = format;
            _rolling.Amount = v;
        }
        else
        {
            _counting.Formatter = formatter;
            _counting.Value = v;
        }
        UpdateName();
    }

    /// <summary>Shows a change in points (null hides it) with an optional custom text.</summary>
    public void SetChange(double? points, Func<double, string>? formatter)
    {
        _delta.IsVisible = points is not null;
        if (points is { } change)
        {
            var format = AnalyticsNumberFormat.Points(1, signed: true);
            _delta.Show(change, format);
            if (formatter is not null)
            {
                _delta.Text = formatter(change);
            }
        }
        UpdateName();
    }

    /// <summary>The muted context after the change ("newest 3 vs oldest 3 cohorts").</summary>
    public void SetContext(string? context)
    {
        _context.Text = context;
        UpdateName();
    }

    private void UpdateName() => AutomationProperties.SetName(this, SpokenText);

    private void ApplyValueSize()
    {
        _rolling.FontSize = _valueSize;
        _counting.FontSize = _valueSize;
        _counting.LineHeight = Math.Round(_valueSize * 1.25, 1);
        _missing.FontSize = _valueSize;
        _missing.LineHeight = Math.Round(_valueSize * 1.25, 1);
    }

    private Control Value => _missing.IsVisible ? _missing : _counting.IsVisible ? _counting : _rolling;

    private double ValueLine => Math.Round(_valueSize * 1.25, 1);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
        var value = Value;
        value.Measure(inf);
        if (_stacked)
        {
            var leftWidth = Math.Max(0, width - 12 - value.DesiredSize.Width);
            _label.Measure(new Size(leftWidth, double.PositiveInfinity));
            var caption = MeasureCaption(leftWidth);
            var left = 19.6 + 2 + caption;
            return new Size(width, Math.Max(left, ValueLine));
        }
        _label.Measure(new Size(width, double.PositiveInfinity));
        var captionHeight = MeasureCaption(width);
        return new Size(width, 19.6 + 2 + 2 + ValueLine + 4 + captionHeight);
    }

    private double MeasureCaption(double width)
    {
        var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _delta.Measure(inf);
        var hasDelta = _delta.IsVisible;
        var hasContext = !string.IsNullOrEmpty(_context.Text);
        if (!hasDelta && !hasContext)
        {
            _contextWrapped = false;
            return 0;
        }
        var deltaWidth = hasDelta ? _delta.DesiredSize.Width : 0;
        _context.Measure(inf);
        _contextWrapped = hasDelta && hasContext && deltaWidth + 4 + _context.DesiredSize.Width > width;
        if (!hasContext)
        {
            return 16.8;
        }
        // Context beside the change when it fits, else on its own line(s) under it (Arc's flex-wrap).
        _context.Measure(new Size(Math.Max(0, _contextWrapped || !hasDelta ? width : width - deltaWidth - 4), double.PositiveInfinity));
        _contextHeight = Math.Max(16.8, _context.DesiredSize.Height);
        return _contextWrapped ? 16.8 + 2 + _contextHeight : Math.Max(16.8, _contextHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var value = Value;
        foreach (var hidden in new Control[] { _rolling, _counting, _missing })
        {
            if (hidden != value)
            {
                hidden.Arrange(default);
            }
        }
        double captionTop;
        double captionWidth;
        if (_stacked)
        {
            var valueWidth = value.DesiredSize.Width;
            value.Arrange(new Rect(finalSize.Width - valueWidth, (finalSize.Height - value.DesiredSize.Height) / 2, valueWidth, value.DesiredSize.Height));
            captionWidth = Math.Max(0, finalSize.Width - 12 - valueWidth);
            var leftHeight = 19.6 + 2 + CaptionHeight();
            var top = (finalSize.Height - leftHeight) / 2;
            _label.Arrange(new Rect(0, top, captionWidth, 19.6));
            captionTop = top + 19.6 + 2;
        }
        else
        {
            captionWidth = finalSize.Width;
            _label.Arrange(new Rect(0, 0, finalSize.Width, 19.6));
            var valueTop = 19.6 + 2 + 2;
            value.Arrange(new Rect(0, valueTop + (ValueLine - value.DesiredSize.Height) / 2, Math.Min(finalSize.Width, value.DesiredSize.Width),
                value.DesiredSize.Height));
            captionTop = valueTop + ValueLine + 4;
        }
        var x = 0.0;
        if (_delta.IsVisible)
        {
            _delta.Arrange(new Rect(0, captionTop + (16.8 - _delta.DesiredSize.Height) / 2, _delta.DesiredSize.Width, _delta.DesiredSize.Height));
            x = _delta.DesiredSize.Width + 4;
        }
        if (_contextWrapped)
        {
            _context.Arrange(new Rect(0, captionTop + 16.8 + 2, captionWidth, _contextHeight));
        }
        else
        {
            _context.Arrange(new Rect(x, captionTop, Math.Max(0, captionWidth - x), _contextHeight));
        }
        return finalSize;
    }

    private double CaptionHeight()
    {
        var hasDelta = _delta.IsVisible;
        var hasContext = !string.IsNullOrEmpty(_context.Text);
        if (!hasDelta && !hasContext)
        {
            return 0;
        }
        return _contextWrapped ? 16.8 + 2 + _contextHeight : Math.Max(16.8, hasContext ? _contextHeight : 16.8);
    }
}

/// <summary>
/// The row of headline metrics (Arc's <c>dl</c>): three equal columns divided by 1px Border lines, padding 20 beside each
/// line (12 in narrow blocks); stacked (under 440) one metric per row with lines between and 10 above and below each.
/// </summary>
internal sealed class CohortRetentionKpiRow : Control
{
    public static readonly StyledProperty<double> InlinePaddingProperty =
        AvaloniaProperty.Register<CohortRetentionKpiRow, double>(nameof(InlinePadding), 20);

    public static readonly StyledProperty<double> StackedPaddingProperty =
        AvaloniaProperty.Register<CohortRetentionKpiRow, double>(nameof(StackedPadding), 10);

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<CohortRetentionKpiRow, bool>(nameof(IsStacked));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<CohortRetentionKpiRow, IBrush?>(nameof(LineBrush));

    private readonly CohortRetentionKpi[] _items;
    private readonly List<Rect> _lines = new();

    static CohortRetentionKpiRow()
    {
        AffectsMeasure<CohortRetentionKpiRow>(InlinePaddingProperty, StackedPaddingProperty, IsStackedProperty);
        AffectsRender<CohortRetentionKpiRow>(LineBrushProperty);
    }

    public CohortRetentionKpiRow()
    {
        _items = [First, Second, Total];
        foreach (var item in _items)
        {
            VisualChildren.Add(item);
            LogicalChildren.Add(item);
        }
    }

    /// <summary>The first metric (month 1 retention by default).</summary>
    public CohortRetentionKpi First { get; } = new();

    /// <summary>The second metric (month 6 retention by default).</summary>
    public CohortRetentionKpi Second { get; } = new();

    /// <summary>The total of everyone who started.</summary>
    public CohortRetentionKpi Total { get; } = new();

    public double InlinePadding { get => GetValue(InlinePaddingProperty); set => SetValue(InlinePaddingProperty, value); }

    public double StackedPadding { get => GetValue(StackedPaddingProperty); set => SetValue(StackedPaddingProperty, value); }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 900 : availableSize.Width;
        if (IsStacked)
        {
            var height = 0.0;
            for (var i = 0; i < _items.Length; i++)
            {
                _items[i].IsStacked = true;
                _items[i].Measure(new Size(width, double.PositiveInfinity));
                height += _items[i].DesiredSize.Height + (i > 0 ? 1 + StackedPadding * 2 : 0);
            }
            return new Size(width, height);
        }
        var column = width / _items.Length;
        var max = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            _items[i].IsStacked = false;
            var inner = column - (i == 0 ? InlinePadding : InlinePadding * 2 + 1);
            _items[i].Measure(new Size(Math.Max(0, inner), double.PositiveInfinity));
            max = Math.Max(max, _items[i].DesiredSize.Height);
        }
        return new Size(width, max);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _lines.Clear();
        if (IsStacked)
        {
            var y = 0.0;
            for (var i = 0; i < _items.Length; i++)
            {
                if (i > 0)
                {
                    y += StackedPadding;
                    _lines.Add(new Rect(0, y, finalSize.Width, 1));
                    y += 1 + StackedPadding;
                }
                var h = _items[i].DesiredSize.Height;
                _items[i].Arrange(new Rect(0, y, finalSize.Width, h));
                y += h;
            }
            return finalSize;
        }
        var column = finalSize.Width / _items.Length;
        for (var i = 0; i < _items.Length; i++)
        {
            var left = i * column;
            if (i > 0)
            {
                _lines.Add(new Rect(Math.Round(left), 0, 1, finalSize.Height));
                left = Math.Round(left) + 1 + InlinePadding;
            }
            var right = (i + 1) * column - InlinePadding;
            _items[i].Arrange(new Rect(left, 0, Math.Max(0, right - left), finalSize.Height));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (LineBrush is not { } brush)
        {
            return;
        }
        foreach (var line in _lines)
        {
            context.FillRectangle(brush, line);
        }
    }
}
