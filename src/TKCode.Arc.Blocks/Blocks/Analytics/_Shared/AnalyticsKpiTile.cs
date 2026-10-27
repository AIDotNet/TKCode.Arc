using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>How a KPI value moves to a new number.</summary>
internal enum AnalyticsNumberMotion
{
    /// <summary>Per-digit odometer (<see cref="AnalyticsRollingNumber"/>, Smooth spring): most blocks.</summary>
    Roll,

    /// <summary>Text count-up, 480ms (.22, 1, .36, 1) (<see cref="AnalyticsCountUp"/>): Metrics dashboard.</summary>
    CountUp,

    /// <summary>Replace at once (Uptime inspection, Horizon readout).</summary>
    Instant
}

/// <summary>
/// The content column of a KPI: label, big value, change line (<see cref="AnalyticsDelta"/>). Blocks put it inside their
/// own frame (a tab with an underline, a card with a sparkline, a <c>dl</c> cell with dividers) and keep hover, focus and
/// selection there. Defaults are Metrics dashboard's tab (label 14/19.6 TextSecondary, value 22/28.6 500 tabular
/// Foreground, delta 12/16.8, rows 2 apart); KPI drilldown sets <see cref="IsDeltaInline"/> (value and delta on one row,
/// 10 apart), 28px display font. Set <see cref="Change"/> to null to hide the change line.
/// </summary>
internal sealed class AnalyticsKpiTile : Control
{
    /// <summary>Label text.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, string?>(nameof(Label));

    /// <summary>The value.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, double>(nameof(Value));

    /// <summary>Value format (rolls and reads the same).</summary>
    public static readonly StyledProperty<AnalyticsNumberFormat> FormatProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, AnalyticsNumberFormat>(nameof(Format), AnalyticsNumberFormat.Integer);

    /// <summary>Custom value text for <see cref="AnalyticsNumberMotion.CountUp"/> / <see cref="AnalyticsNumberMotion.Instant"/> (wins over <see cref="Format"/>).</summary>
    public static readonly StyledProperty<Func<double, string>?> FormatterProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, Func<double, string>?>(nameof(Formatter));

    /// <summary>How the value moves (default Roll).</summary>
    public static readonly StyledProperty<AnalyticsNumberMotion> NumberMotionProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, AnalyticsNumberMotion>(nameof(NumberMotion));

    /// <summary>Value font size (22; KPI drilldown 28 → 22 compact; MRR headline 28; Metrics dashboard small 18).</summary>
    public static readonly StyledProperty<double> ValueFontSizeProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, double>(nameof(ValueFontSize), 22);

    /// <summary>Value in the display font (Geist: KPI drilldown) instead of the body font (Inter).</summary>
    public static readonly StyledProperty<bool> IsDisplayFontProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, bool>(nameof(IsDisplayFont));

    /// <summary>The change in the change format's units; null hides the change line.</summary>
    public static readonly StyledProperty<double?> ChangeProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, double?>(nameof(Change));

    /// <summary>Change format (default one-decimal percent, unsigned: "11.7%"; ratios: <c>Points()</c> "0.1 pts").</summary>
    public static readonly StyledProperty<AnalyticsNumberFormat> ChangeFormatProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, AnalyticsNumberFormat>(nameof(ChangeFormat), AnalyticsNumberFormat.Percent(1));

    /// <summary>A fall is good (churn, latency, bounce rate).</summary>
    public static readonly StyledProperty<bool> LowerIsBetterProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, bool>(nameof(LowerIsBetter));

    /// <summary>Muted text after the change ("since Jul").</summary>
    public static readonly StyledProperty<string?> ChangeContextProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, string?>(nameof(ChangeContext));

    /// <summary>Value and change share one row (KPI drilldown cards, MRR headline) instead of stacking.</summary>
    public static readonly StyledProperty<bool> IsDeltaInlineProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, bool>(nameof(IsDeltaInline));

    /// <summary>Vertical gap between label, value and change (2).</summary>
    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, double>(nameof(RowSpacing), 2);

    /// <summary>How label and change texts swap (default instant).</summary>
    public static readonly StyledProperty<AnalyticsSwapStyle> TextSwapProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, AnalyticsSwapStyle>(nameof(TextSwap), AnalyticsSwapStyle.Instant);

    /// <summary>Label in Foreground (a selected tab) instead of TextSecondary.</summary>
    public static readonly StyledProperty<bool> IsLabelStrongProperty =
        AvaloniaProperty.Register<AnalyticsKpiTile, bool>(nameof(IsLabelStrong));

    private readonly StackPanel _stack = new() { Orientation = Orientation.Vertical };
    private readonly StackPanel _valueRow = new() { Orientation = Orientation.Horizontal, Spacing = 10 };
    private readonly AnalyticsTextSwap _label = new() { LineHeight = 19.6 };
    private readonly AnalyticsRollingNumber _rolling = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly AnalyticsCountUp _countUp = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly AnalyticsDelta _delta = new() { VerticalAlignment = VerticalAlignment.Center };
    private IDisposable? _labelColor;
    private IDisposable? _rollingFamily;
    private IDisposable? _countFamily;
    private bool? _labelStrong;
    private bool? _displayFont;

    /// <summary>Creates the tile.</summary>
    public AnalyticsKpiTile()
    {
        _valueRow.Children.Add(_rolling);
        _valueRow.Children.Add(_countUp);
        _stack.Children.Add(_label);
        _stack.Children.Add(_valueRow);
        _stack.Children.Add(_delta);
        VisualChildren.Add(_stack);
        LogicalChildren.Add(_stack);
        ArcTokens.Bind(_label, AnalyticsTextSwap.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_rolling, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_countUp, AnalyticsCountUp.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_rolling, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_countUp, AnalyticsCountUp.ForegroundProperty, ArcDesignTokenKind.Foreground);
        Sync();
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="FormatProperty"/>
    public AnalyticsNumberFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <inheritdoc cref="FormatterProperty"/>
    public Func<double, string>? Formatter { get => GetValue(FormatterProperty); set => SetValue(FormatterProperty, value); }

    /// <inheritdoc cref="NumberMotionProperty"/>
    public AnalyticsNumberMotion NumberMotion { get => GetValue(NumberMotionProperty); set => SetValue(NumberMotionProperty, value); }

    /// <inheritdoc cref="ValueFontSizeProperty"/>
    public double ValueFontSize { get => GetValue(ValueFontSizeProperty); set => SetValue(ValueFontSizeProperty, value); }

    /// <inheritdoc cref="IsDisplayFontProperty"/>
    public bool IsDisplayFont { get => GetValue(IsDisplayFontProperty); set => SetValue(IsDisplayFontProperty, value); }

    /// <inheritdoc cref="ChangeProperty"/>
    public double? Change { get => GetValue(ChangeProperty); set => SetValue(ChangeProperty, value); }

    /// <inheritdoc cref="ChangeFormatProperty"/>
    public AnalyticsNumberFormat ChangeFormat { get => GetValue(ChangeFormatProperty); set => SetValue(ChangeFormatProperty, value); }

    /// <inheritdoc cref="LowerIsBetterProperty"/>
    public bool LowerIsBetter { get => GetValue(LowerIsBetterProperty); set => SetValue(LowerIsBetterProperty, value); }

    /// <inheritdoc cref="ChangeContextProperty"/>
    public string? ChangeContext { get => GetValue(ChangeContextProperty); set => SetValue(ChangeContextProperty, value); }

    /// <inheritdoc cref="IsDeltaInlineProperty"/>
    public bool IsDeltaInline { get => GetValue(IsDeltaInlineProperty); set => SetValue(IsDeltaInlineProperty, value); }

    /// <inheritdoc cref="RowSpacingProperty"/>
    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    /// <inheritdoc cref="TextSwapProperty"/>
    public AnalyticsSwapStyle TextSwap { get => GetValue(TextSwapProperty); set => SetValue(TextSwapProperty, value); }

    /// <inheritdoc cref="IsLabelStrongProperty"/>
    public bool IsLabelStrong { get => GetValue(IsLabelStrongProperty); set => SetValue(IsLabelStrongProperty, value); }

    /// <summary>The change line (set its icon size, stroke, spacing or swap for a block's look).</summary>
    public AnalyticsDelta Delta => _delta;

    /// <summary>The rolling value (Roll mode).</summary>
    public AnalyticsRollingNumber RollingValue => _rolling;

    /// <summary>The counting value (CountUp and Instant modes).</summary>
    public AnalyticsCountUp CountingValue => _countUp;

    /// <summary>What screen readers hear: "Visitors, 36,686, up 11.7%".</summary>
    public string SpokenText
    {
        get
        {
            var value = NumberMotion == AnalyticsNumberMotion.Roll ? _rolling.FullText : _countUp.FinalText;
            var text = string.IsNullOrEmpty(Label) ? value : $"{Label}, {value}";
            return Change is null ? text : $"{text}, {_delta.SpokenText}";
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BoundsProperty && change.Property.OwnerType == typeof(AnalyticsKpiTile))
        {
            Sync();
        }
    }

    private void Sync()
    {
        _label.Text = Label;
        _label.Swap = TextSwap;
        if (_labelStrong != IsLabelStrong)
        {
            _labelStrong = IsLabelStrong;
            _labelColor?.Dispose();
            _labelColor = ArcTokens.Bind(_label, AnalyticsTextSwap.ForegroundProperty, IsLabelStrong ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
        }

        var motion = NumberMotion;
        _rolling.IsVisible = motion == AnalyticsNumberMotion.Roll;
        _countUp.IsVisible = motion != AnalyticsNumberMotion.Roll;
        _rolling.Format = Format;
        _rolling.Amount = Value;
        _rolling.FontSize = ValueFontSize;
        _countUp.Format = Format;
        _countUp.Formatter = Formatter;
        _countUp.Duration = motion == AnalyticsNumberMotion.Instant ? TimeSpan.Zero : ArcMotionPresets.Considered;
        _countUp.Value = Value;
        _countUp.FontSize = ValueFontSize;
        _countUp.LineHeight = Math.Round(ValueFontSize * 1.3, 1);
        if (_displayFont != IsDisplayFont)
        {
            _displayFont = IsDisplayFont;
            var family = IsDisplayFont ? ArcDesignTokenKind.DisplayFontFamily : ArcDesignTokenKind.BodyFontFamily;
            _rollingFamily?.Dispose();
            _countFamily?.Dispose();
            _rollingFamily = ArcTokens.Bind(_rolling, RollingDigits.FontFamilyProperty, family);
            _countFamily = ArcTokens.Bind(_countUp, AnalyticsCountUp.FontFamilyProperty, family);
        }

        _stack.Spacing = RowSpacing;
        _delta.Swap = TextSwap;
        _delta.Context = ChangeContext;
        _delta.IsVisible = Change is not null;
        if (Change is { } delta)
        {
            _delta.Show(delta, ChangeFormat, LowerIsBetter);
        }
        var parent = IsDeltaInline ? (Panel)_valueRow : _stack;
        if (_delta.Parent != parent)
        {
            ((Panel?)_delta.Parent)?.Children.Remove(_delta);
            parent.Children.Add(_delta);
        }
        AutomationProperties.SetName(this, SpokenText);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _stack.Measure(availableSize);
        return _stack.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _stack.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
