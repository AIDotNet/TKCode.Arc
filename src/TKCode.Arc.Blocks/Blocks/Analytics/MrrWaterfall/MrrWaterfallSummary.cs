using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The summary's headline: a label (14, TextSecondary), the ending revenue rolling digit by digit (28 → 22 in narrow
/// blocks, Medium, tabular) and the change line: an arrow, the signed amount and percentage, and "since …" in muted text.
/// The label and the change texts rise out and in when the period changes.
/// </summary>
internal sealed class MrrWaterfallHeadline : Control
{
    private readonly AnalyticsTextSwap _label = new() { LineHeight = 19.6, FontSize = 14, Swap = AnalyticsSwapStyle.Rise };
    private readonly AnalyticsRollingNumber _value = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly AnalyticsDelta _delta = new()
    {
        IconSize = 16, IconStroke = 1.75, Spacing = 6, ContextSpacing = 6, Swap = AnalyticsSwapStyle.Rise
    };
    private double _valueSize = 28;

    public MrrWaterfallHeadline()
    {
        foreach (var child in new Control[] { _label, _value, _delta })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        ArcTokens.Bind(_label, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_label, AnalyticsTextSwap.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_value, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_value, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ApplySize();
    }

    /// <summary>The value (tests).</summary>
    internal AnalyticsRollingNumber Value => _value;

    /// <summary>The change line (tests).</summary>
    internal AnalyticsDelta Delta => _delta;

    /// <summary>The label (tests).</summary>
    internal AnalyticsTextSwap Label => _label;

    /// <summary>The value's size (28; 22 in narrow blocks).</summary>
    public double ValueSize
    {
        get => _valueSize;
        set
        {
            if (Math.Abs(_valueSize - value) > 0.01)
            {
                _valueSize = value;
                ApplySize();
                InvalidateMeasure();
            }
        }
    }

    private double ValueLine => Math.Round(_valueSize * 1.2, 1);

    private void ApplySize()
    {
        _value.FontSize = _valueSize;
    }

    public void Set(string label, double value, AnalyticsNumberFormat format, string deltaText, AnalyticsDirection direction, AnalyticsTone tone, string context)
    {
        _label.Text = label;
        _value.Format = format;
        _value.Amount = value;
        _delta.Direction = direction;
        _delta.Tone = tone;
        _delta.Text = deltaText;
        _delta.Context = context;
        AutomationProperties.SetName(this, $"{label}, {_value.FullText}, {_delta.SpokenText}");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _label.Measure(inf);
        _value.Measure(inf);
        _delta.Measure(inf);
        var width = Math.Max(_label.DesiredSize.Width, Math.Max(_value.DesiredSize.Width, _delta.DesiredSize.Width));
        return new Size(width, 19.6 + 2 + ValueLine + 2 + Math.Max(16.8, _delta.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _label.Arrange(new Rect(0, 0, _label.DesiredSize.Width, 19.6));
        _value.Arrange(new Rect(0, 19.6 + 2 + (ValueLine - _value.DesiredSize.Height) / 2, _value.DesiredSize.Width, _value.DesiredSize.Height));
        var deltaTop = 19.6 + 2 + ValueLine + 2;
        _delta.Arrange(new Rect(0, deltaTop + (16.8 - _delta.DesiredSize.Height) / 2, _delta.DesiredSize.Width, _delta.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>One statistic of the strip: a label (12, TextSecondary) over a rolling value (18, Medium, tabular).</summary>
internal sealed class MrrWaterfallStat : Control
{
    private readonly TextBlock _label = new() { LineHeight = 16.8, TextWrapping = TextWrapping.Wrap };
    private readonly AnalyticsRollingNumber _value = new() { HorizontalAlignment = HorizontalAlignment.Left, FontSize = 18 };
    private readonly TextBlock _missing = new() { Text = AnalyticsFormat.Missing, FontSize = 18, LineHeight = 23.4, IsVisible = false };
    private double _valueSize = 18;

    public MrrWaterfallStat()
    {
        foreach (var child in new Control[] { _label, _value, _missing })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        ArcTokens.Bind(_label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_value, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_value, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_missing, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_missing, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
    }

    /// <summary>The value (tests).</summary>
    internal AnalyticsRollingNumber Value => _value;

    /// <summary>The label.</summary>
    public string? Label => _label.Text;

    /// <summary>The value's size (18; 16 in narrow blocks).</summary>
    public double ValueSize
    {
        get => _valueSize;
        set
        {
            if (Math.Abs(_valueSize - value) > 0.01)
            {
                _valueSize = value;
                _value.FontSize = value;
                _missing.FontSize = value;
                InvalidateMeasure();
            }
        }
    }

    public void Set(string label, double? value, AnalyticsNumberFormat format)
    {
        _label.Text = label;
        _missing.IsVisible = value is null;
        _value.IsVisible = value is not null;
        if (value is { } v)
        {
            _value.Format = format;
            _value.Amount = v;
        }
        AutomationProperties.SetName(this, $"{label}, {(value is null ? AnalyticsFormat.Missing : _value.FullText)}");
    }

    private Control Shown => _missing.IsVisible ? _missing : _value;

    protected override Size MeasureOverride(Size availableSize)
    {
        var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
        // A finite width wraps the label (stretched strips); the natural width of a strip is measured without one.
        _label.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        Shown.Measure(inf);
        return new Size(Math.Max(_label.DesiredSize.Width, Shown.DesiredSize.Width), _label.DesiredSize.Height + 2 + 23.4);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _label.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, _label.DesiredSize.Width), _label.DesiredSize.Height));
        var shown = Shown;
        (shown == _value ? (Control)_missing : _value).Arrange(default);
        shown.Arrange(new Rect(0, Math.Max(_label.DesiredSize.Height + 2, finalSize.Height - 23.4), Math.Min(finalSize.Width, shown.DesiredSize.Width), 23.4));
        return finalSize;
    }
}

/// <summary>
/// The strip of three statistics (Arc's <c>dl</c>): cells as wide as their content with 20 of padding beside the 1px lines
/// between them; stretched (a narrow block) the three share the full width equally.
/// </summary>
internal sealed class MrrWaterfallStats : Control
{
    public static readonly StyledProperty<double> InlinePaddingProperty =
        AvaloniaProperty.Register<MrrWaterfallStats, double>(nameof(InlinePadding), 20);

    public static readonly StyledProperty<bool> IsStretchedProperty =
        AvaloniaProperty.Register<MrrWaterfallStats, bool>(nameof(IsStretched));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<MrrWaterfallStats, IBrush?>(nameof(LineBrush));

    private readonly MrrWaterfallStat[] _items;
    private readonly List<Rect> _lines = new();

    static MrrWaterfallStats()
    {
        AffectsMeasure<MrrWaterfallStats>(InlinePaddingProperty, IsStretchedProperty);
        AffectsRender<MrrWaterfallStats>(LineBrushProperty);
    }

    public MrrWaterfallStats()
    {
        _items = [Retention, QuickRatio, LogoChurn];
        foreach (var item in _items)
        {
            VisualChildren.Add(item);
            LogicalChildren.Add(item);
        }
    }

    /// <summary>Net revenue retention.</summary>
    public MrrWaterfallStat Retention { get; } = new();

    /// <summary>Quick ratio.</summary>
    public MrrWaterfallStat QuickRatio { get; } = new();

    /// <summary>Logo churn.</summary>
    public MrrWaterfallStat LogoChurn { get; } = new();

    public double InlinePadding { get => GetValue(InlinePaddingProperty); set => SetValue(InlinePaddingProperty, value); }

    public bool IsStretched { get => GetValue(IsStretchedProperty); set => SetValue(IsStretchedProperty, value); }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    /// <summary>Sets the three values' type size (18, or 16 in narrow blocks).</summary>
    public void SetValueSize(double size)
    {
        foreach (var item in _items)
        {
            item.ValueSize = size;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
        var natural = 0.0;
        var height = 0.0;
        if (IsStretched && !double.IsInfinity(availableSize.Width))
        {
            var column = availableSize.Width / _items.Length;
            for (var i = 0; i < _items.Length; i++)
            {
                _items[i].Measure(new Size(Math.Max(0, column - (i == 0 ? InlinePadding : InlinePadding * 2 + 1)), double.PositiveInfinity));
                height = Math.Max(height, _items[i].DesiredSize.Height);
            }
            return new Size(availableSize.Width, height);
        }
        for (var i = 0; i < _items.Length; i++)
        {
            _items[i].Measure(inf);
            natural += _items[i].DesiredSize.Width + (i == 0 ? InlinePadding : InlinePadding * 2 + 1);
            height = Math.Max(height, _items[i].DesiredSize.Height);
        }
        return new Size(natural, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _lines.Clear();
        if (IsStretched)
        {
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
        var x = 0.0;
        for (var i = 0; i < _items.Length; i++)
        {
            if (i > 0)
            {
                _lines.Add(new Rect(x, 0, 1, finalSize.Height));
                x += 1 + InlinePadding;
            }
            var w = _items[i].DesiredSize.Width;
            _items[i].Arrange(new Rect(x, 0, w, finalSize.Height));
            x += w + InlinePadding;
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
