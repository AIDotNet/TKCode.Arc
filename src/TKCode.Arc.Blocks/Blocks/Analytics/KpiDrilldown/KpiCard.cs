using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One KPI card of a <see cref="KpiDrilldown"/>: the whole card is one button (label, rolling value, rolling change and a
/// sparkline). Hover darkens the edge and lifts a resting shadow, pressing scales to .985, keyboard focus draws a 2px
/// ring 2px outside (notes 3 and 5). Its theme is in <c>Themes/KpiDrilldownTheme.axaml</c>.
/// </summary>
[TemplatePart(ValuePart, typeof(AnalyticsRollingNumber))]
[TemplatePart(PlainValuePart, typeof(TextBlock))]
[TemplatePart(DeltaPart, typeof(KpiDelta))]
[TemplatePart(SparklinePart, typeof(KpiSparkline))]
[PseudoClasses(CompactPseudoClass)]
internal sealed class KpiCard : Button
{
    internal const string ValuePart = "PART_Value";
    internal const string PlainValuePart = "PART_PlainValue";
    internal const string DeltaPart = "PART_Delta";
    internal const string SparklinePart = "PART_Sparkline";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>The label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<KpiCard, string?>(nameof(Label));

    /// <summary>Compact (phone) layout: 22px value, 44px sparkline.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<KpiCard, bool>(nameof(IsCompact));

    /// <summary>The value's font size (28; compact 22), set by the theme.</summary>
    public static readonly StyledProperty<double> ValueSizeProperty =
        AvaloniaProperty.Register<KpiCard, double>(nameof(ValueSize), 28);

    private AnalyticsRollingNumber? _value;
    private TextBlock? _plainValue;
    private KpiDelta? _delta;
    private KpiSparkline? _sparkline;

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public double ValueSize { get => GetValue(ValueSizeProperty); set => SetValue(ValueSizeProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>The metric shown.</summary>
    public KpiMetric? Metric { get; set; }

    /// <summary>Its position in the grid.</summary>
    public int Index { get; set; }

    internal AnalyticsRollingNumber? ValueView => _value;

    internal KpiDelta? Delta => _delta;

    internal KpiSparkline? Sparkline => _sparkline;

    protected override Type StyleKeyOverride => typeof(KpiCard);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _value = e.NameScope.Find<AnalyticsRollingNumber>(ValuePart);
        _plainValue = e.NameScope.Find<TextBlock>(PlainValuePart);
        _delta = e.NameScope.Find<KpiDelta>(DeltaPart);
        _sparkline = e.NameScope.Find<KpiSparkline>(SparklinePart);
        PartsReady?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised after the template parts exist (the block fills them).</summary>
    public event EventHandler? PartsReady;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
        }
    }

    /// <summary>
    /// Shows a metric for a range. <paramref name="animate"/> false places everything at once (first data, another
    /// metric in this slot); true rolls the change and morphs the sparkline.
    /// </summary>
    public void Show(KpiMetric metric, KpiPeriod period, double[] sparkline, bool animate, string spokenName)
    {
        Metric = metric;
        Label = metric.Label;
        var plain = metric.ValueFormatter is not null;
        if (_value is not null)
        {
            _value.IsVisible = !plain;
            _value.IsInstant = !animate;
            _value.Format = metric.NumberFormat;
            _value.Amount = double.IsFinite(period.Last) ? period.Last : 0;
            _value.IsInstant = false;
        }
        if (_plainValue is not null)
        {
            _plainValue.IsVisible = plain;
            _plainValue.Text = plain ? metric.FormatValue(period.Last) : null;
        }
        _delta?.Show(period.Change, metric.ChangeFormat, metric.IsLowerBetter, animate);
        _sparkline?.SetValues(sparkline, animate);
        AutomationProperties.SetName(this, spokenName);
    }
}

/// <summary>
/// A change: arrow (Lucide <c>arrow-up-right</c>, turned for down; <c>arrow-right</c> when flat) and the signed number
/// rolling digit by digit (12px 500 tabular), tinted Success / Danger / TextMuted with a 160ms colour transition. The
/// shared <see cref="AnalyticsDelta"/> swaps its text; KPI drilldown measured rolling digits (notes 6.3 / 6.6).
/// </summary>
[TemplatePart(ArrowPart, typeof(ArcGlyph))]
[TemplatePart(NumberPart, typeof(AnalyticsRollingNumber))]
[PseudoClasses(GoodPseudoClass, BadPseudoClass, FlatPseudoClass)]
internal sealed class KpiDelta : TemplatedControl
{
    internal const string ArrowPart = "PART_Arrow";
    internal const string NumberPart = "PART_Number";
    internal const string GoodPseudoClass = ":good";
    internal const string BadPseudoClass = ":bad";
    internal const string FlatPseudoClass = ":flat";

    private static readonly ITransform s_up = TransformOperations.Parse("rotate(0deg)");
    private static readonly ITransform s_down = TransformOperations.Parse("rotate(90deg)");

    private ArcGlyph? _arrow;
    private AnalyticsRollingNumber? _number;
    private double _amount;
    private AnalyticsNumberFormat _format = AnalyticsNumberFormat.Percent(1, true);
    private bool _animate;

    /// <summary>Direction of the last change shown.</summary>
    public AnalyticsDirection Direction { get; private set; }

    /// <summary>Tone of the last change shown.</summary>
    public AnalyticsTone Tone { get; private set; } = AnalyticsTone.Flat;

    /// <summary>The text shown ("+3.3%", "−0.2 pts").</summary>
    public string Text => _format.Format(_amount);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _arrow = e.NameScope.Find<ArcGlyph>(ArrowPart);
        _number = e.NameScope.Find<AnalyticsRollingNumber>(NumberPart);
        Apply(false);
    }

    /// <summary>Shows a change in the format's units (null hides the control).</summary>
    public void Show(double? change, AnalyticsNumberFormat format, bool lowerIsBetter, bool animate)
    {
        IsVisible = change is not null;
        if (change is not { } value)
        {
            return;
        }
        var tone = AnalyticsFormat.Tone(value, lowerIsBetter, format.Decimals);
        Tone = tone;
        Direction = tone == AnalyticsTone.Flat ? AnalyticsDirection.Flat : value > 0 ? AnalyticsDirection.Up : AnalyticsDirection.Down;
        _amount = value;
        _format = format;
        _animate = animate;
        Apply(animate);
    }

    private void Apply(bool animate)
    {
        PseudoClasses.Set(GoodPseudoClass, Tone == AnalyticsTone.Good);
        PseudoClasses.Set(BadPseudoClass, Tone == AnalyticsTone.Bad);
        PseudoClasses.Set(FlatPseudoClass, Tone == AnalyticsTone.Flat);
        if (_arrow is not null)
        {
            _arrow.Data = Direction == AnalyticsDirection.Flat ? ArcIcons.ArrowRight : ArcIcons.ArrowUpRight;
            _arrow.RenderTransform = Direction == AnalyticsDirection.Down ? s_down : s_up;
        }
        if (_number is not null)
        {
            _number.IsInstant = !animate && !_animate;
            _number.Format = _format;
            _number.Amount = _amount;
            _number.IsInstant = false;
        }
    }
}

/// <summary>
/// A card's sparkline: 40 resampled points of the current period, min-max normalised, drawn as a straight polyline
/// (1.5px Accent, round joins, flat caps) over an Accent 9% area, in a 100 × 32 view box stretched to the control
/// (2 units of air above and below; the stroke does not scale). A range switch morphs every point on one Smooth spring
/// (<see cref="AnalyticsSpringArray"/>).
/// </summary>
/// <remarks>Why code-driven: the 40 rows are coordinates inside one geometry, retargeted mid-flight with their velocity.</remarks>
internal sealed class KpiSparkline : Control
{
    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsSpringArray _rows;
    private readonly Point[] _points = new Point[KpiDrilldownMath.SparklinePoints];
    private StreamGeometry? _line;
    private StreamGeometry? _area;
    private Size _geometrySize;
    private bool _dirty = true;

    public KpiSparkline()
    {
        IsHitTestVisible = false;
        _rows = new AnalyticsSpringArray(this, AnalyticsSprings.Smooth);
        _rows.Changed += () =>
        {
            _dirty = true;
            InvalidateVisual();
        };
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>The springs (tests).</summary>
    internal AnalyticsSpringArray Rows => _rows;

    /// <summary>Sets the normalised rows (0–1); morphs when <paramref name="animate"/>.</summary>
    public void SetValues(ReadOnlySpan<double> normalized, bool animate)
    {
        if (animate)
        {
            _rows.SetTargets(normalized);
        }
        else
        {
            _rows.Jump(normalized);
        }
    }

    private void RefreshPalette()
    {
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var size = Bounds.Size;
        var n = _rows.Count;
        if (n < 2 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (_dirty || size != _geometrySize || _line is null || _area is null)
        {
            Build(size);
        }
        context.DrawGeometry(_palette.Brush(_palette.Accent, 0.09), null, _area!);
        context.DrawGeometry(null, _palette.Pen(_palette.Accent, 1.5, cap: PenLineCap.Flat), _line!);
    }

    private void Build(Size size)
    {
        var n = Math.Min(_rows.Count, _points.Length);
        var values = _rows.Values;
        for (var i = 0; i < n; i++)
        {
            _points[i] = new Point(i * size.Width / (n - 1), KpiDrilldownMath.SparkY(values[i], size.Height));
        }
        var points = _points.AsSpan(0, n);
        _line = AnalyticsGeometry.Line(points, AnalyticsCurve.Linear);
        _area = AnalyticsGeometry.Area(points, size.Height, AnalyticsCurve.Linear);
        _geometrySize = size;
        _dirty = false;
    }
}
